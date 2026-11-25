using System;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing Tide-Pounding Major Realm Breakthrough Seclusion.
    /// </summary>
    public static class BreakthroughSystem
    {
        public static BreakthroughSessionComponent GetOrCreateSession(WorldStore world)
        {
            var s = world.Get<BreakthroughSessionComponent>(world.PlayerEntity);
            if (s == null)
            {
                s = world.Set(world.PlayerEntity, new BreakthroughSessionComponent());
            }
            return s;
        }

        public static bool OpenSession(WorldStore world)
        {
            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) ||
                !world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
                return false;

            if (ap.Substage != 4 || ap.Rank >= 5)
            {
                Debug.LogWarning("[BreakthroughSystem] Cannot initiate breakthrough: requires Peak Stage (Substage 4) below Rank 5.");
                return false;
            }

            var clock = world.GetSingleton<WorldClockComponent>();
            if (clock != null && clock.ActionPointsRemaining < 1)
            {
                Debug.LogWarning("[BreakthroughSystem] Insufficient Action Points for seclusion!");
                return false;
            }

            TimeSystem.DeductActionPoints(world, 1);

            int humanMarks = world.Get<DaoMarksComponent>(world.PlayerEntity)?.Ledger.HumanPathMarks ?? 0;
            if (humanMarks <= 0) humanMarks = ApertureComponent.DeriveInnateHumanPathMarks(cult.AptitudePercent);

            float maxDur = ApertureComponent.GetMaxWallDurabilityForRank(ap.Rank);
            ap.MaxWallDurability = maxDur;
            ap.ApertureWallDurability = maxDur;

            var s = GetOrCreateSession(world);
            s.IsOpen = true;
            s.CurrentRank = ap.Rank;
            s.HumanPathMarks = humanMarks;
            s.MembraneMaxHP = maxDur;
            s.MembraneCurrentHP = maxDur;
            s.MembraneNaturalRegenRate = 10f * (1.0f + (ap.Rank - 1) * 0.6f);
            s.CurrentTurbulence = 0f;
            s.SessionTime = 0f;
            s.LastWaveTime = 0f;
            s.State = BreakthroughState.PoundingInSession;
            s.StatusMessage = $"Entered Seclusion Chamber. Target: {ap.GetMembraneTypeName()} | Durability: {maxDur:F0} HP";

            WorldSignals.EmitUIModalActive(true);
            WorldSignals.EmitTopics(DirtyTopic.Breakthrough | DirtyTopic.Calendar);
            return true;
        }

        public static void CloseSession(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            if (!s.IsOpen) return;
            s.IsOpen = false;
            WorldSignals.EmitUIModalActive(false);
            WorldSignals.EmitTopics(DirtyTopic.Breakthrough);
        }

        public static void ReleaseEssenceWave(WorldStore world, float wavePowerFactor = 1.0f)
        {
            var s = GetOrCreateSession(world);
            if (!s.IsOpen || s.State != BreakthroughState.PoundingInSession) return;
            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) ||
                !world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
                return;

            float cost = cult.AptitudePercent * 0.12f * Math.Clamp(wavePowerFactor, 0.2f, 1.0f);
            if (ap.CurrentEssenceSea < cost)
            {
                s.StatusMessage = "Insufficient Primeval Essence to surge an assault wave!";
                if (ap.CurrentEssenceSea <= 0.8f && cult.PrimevalStones <= 0)
                {
                    EndBreakthrough(world, s, cult, ap, BreakthroughState.FailedEssenceDry);
                }
                else
                {
                    WorldSignals.EmitTopics(DirtyTopic.Breakthrough);
                }
                return;
            }

            ap.CurrentEssenceSea -= cost;
            s.LastWaveTime = s.SessionTime;

            float oscValue = MathF.Sin(s.SessionTime * MathF.PI * 2f * s.OscillationFrequency);
            float humanPrecisionBonus = Math.Clamp(s.HumanPathMarks * 0.015f, 0f, 0.20f);
            bool isHarmonic = oscValue >= (0.75f - humanPrecisionBonus);
            bool isDispersion = oscValue <= -0.45f;

            float baseDamage = 35f * (1.0f + (s.CurrentRank - 1) * 0.5f);
            float damage;

            if (isHarmonic)
            {
                float markAmp = 1.0f + (s.HumanPathMarks * 0.04f);
                damage = baseDamage * 2.2f * markAmp * (1f + (s.CurrentTurbulence / 250f));
                s.StatusMessage = $"【Harmonic Resonance!】 Tidal crash dealt {damage:F0} fracture damage! (Marks: {s.HumanPathMarks})";
            }
            else if (isDispersion)
            {
                damage = baseDamage * 0.25f;
                s.StatusMessage = "【Essence Dispersion】 Poorly timed wave dispersed weakly against the resilient membrane.";
            }
            else
            {
                damage = baseDamage;
                s.StatusMessage = $"Tidal wave struck the membrane for {damage:F0} impact damage.";
            }

            s.MembraneCurrentHP = MathF.Max(0f, s.MembraneCurrentHP - damage);
            ap.ApertureWallDurability = s.MembraneCurrentHP;
            ap.MaxWallDurability = s.MembraneMaxHP;

            WorldSignals.EmitBreakthroughWaveImpact(isHarmonic, isDispersion, damage);
            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Breakthrough);

            if (s.MembraneCurrentHP <= 0.001f)
            {
                EndBreakthrough(world, s, cult, ap, BreakthroughState.Succeeded);
            }
        }

        public static void CrushStone(WorldStore world, IRandomSource? rngOverride = null)
        {
            var s = GetOrCreateSession(world);
            if (!s.IsOpen || s.State != BreakthroughState.PoundingInSession) return;
            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) ||
                !world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
                return;

            if (cult.PrimevalStones <= 0)
            {
                s.StatusMessage = "Primeval Stone stockpile exhausted!";
                WorldSignals.EmitTopics(DirtyTopic.Breakthrough);
                return;
            }

            cult.PrimevalStones--;
            float recovery = ap.CalculateStoneRecoveryPercentage(cult.AptitudePercent);
            ap.CurrentEssenceSea = MathF.Min(cult.AptitudePercent, ap.CurrentEssenceSea + recovery);

            IRandomSource rng = rngOverride ?? new SeededRandom(world.RunSeed + cult.PrimevalStones);
            float turbulenceDamping = Math.Clamp(s.HumanPathMarks * 0.5f, 0f, 6f);
            float randGain = 12f + (float)(rng.NextDouble() * 6.0);
            float turbulenceGain = MathF.Max(5f, randGain - turbulenceDamping);
            s.CurrentTurbulence += turbulenceGain;

            s.StatusMessage = $"Crushed Stone: +{recovery:F1}% Essence restored! Foreign Turbulence: {s.CurrentTurbulence:F0}/{BreakthroughSessionComponent.TURBULENCE_LIMIT:F0}";

            WorldSignals.EmitSound(HalalSoundType.StoneShatter);
            WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Aperture | DirtyTopic.Breakthrough);

            if (s.CurrentTurbulence >= BreakthroughSessionComponent.TURBULENCE_LIMIT)
            {
                EndBreakthrough(world, s, cult, ap, BreakthroughState.FailedRupture);
            }
        }

        public static bool TryUseRelicGuBypass(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) ||
                !world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
                return false;

            if (ap.Substage != 4 || ap.Rank >= 5)
            {
                s.StatusMessage = "Cannot use Relic Gu at this stage.";
                return false;
            }

            EntityId? relicEntityId = FindOwnedRelicGu(world, ap.Rank);
            if (!relicEntityId.HasValue)
            {
                s.StatusMessage = "No Relic Gu owned in aperture to bypass breakthrough.";
                WorldSignals.EmitTopics(DirtyTopic.Breakthrough);
                return false;
            }

            int prevRank = ap.Rank;
            world.DestroyEntity(relicEntityId.Value);
            CultivationSystem.PromoteMajorRank(cult, ap);
            s.MembraneMaxHP = ap.MaxWallDurability;
            s.MembraneCurrentHP = ap.MaxWallDurability;
            s.StatusMessage = $"Consuming Relic Gu! Transcended from Rank {prevRank} to Rank {ap.Rank} {ap.GetMembraneTypeName()}!";

            EndBreakthrough(world, s, cult, ap, BreakthroughState.Succeeded);
            WorldSignals.EmitTopics(DirtyTopic.Inventory);
            return true;
        }

        public static bool TryStartSovereignTrial(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) ||
                !world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
                return false;

            if (ap.Rank < 5 || ap.Substage < 4)
            {
                s.StatusMessage = "Sovereign Trial requires Peak Rank 5 (Substage 4) cultivation.";
                WorldSignals.EmitTopics(DirtyTopic.Breakthrough);
                return false;
            }

            var clock = world.GetSingleton<WorldClockComponent>();
            if (clock != null && clock.ActionPointsRemaining < 1)
            {
                s.StatusMessage = "Insufficient Action Points to challenge the Sovereign Trial!";
                WorldSignals.EmitTopics(DirtyTopic.Breakthrough);
                return false;
            }

            bool started = CombatSystem.StartCombat(
                world,
                ArenaEncounterType.SovereignTrial,
                world.GetPlayerPosition(),
                isLethal: true,
                enemyName: "Heavenly Sovereign Elder (五转巅峰大能)");

            if (!started)
            {
                return false;
            }

            if (clock != null)
            {
                TimeSystem.DeductActionPoints(world, 1);
            }

            s.StatusMessage = $"{cult.Name} entered the Peak Rank 5 Sovereign Trial!";
            WorldSignals.EmitTopics(DirtyTopic.Breakthrough | DirtyTopic.Calendar);
            return true;
        }

        private static EntityId? FindOwnedRelicGu(WorldStore world, int currentRank)
        {
            EntityId? fallbackRelic = null;
            foreach (var (eid, gu) in world.GetOwned<GuWormComponent>(world.PlayerEntity))
            {
                if (gu.IsVitalGu || string.IsNullOrEmpty(gu.GuID)) continue;
                bool isRelic =
                    gu.GuID.Contains("relic", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(gu.GuID, "gu_green_copper_relic", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(gu.GuID, "gu_red_iron_relic", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(gu.GuID, "gu_white_silver_relic", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(gu.GuID, "gu_yellow_gold_relic", StringComparison.OrdinalIgnoreCase);

                if (!isRelic) continue;

                if (gu.Rank == currentRank)
                {
                    return eid;
                }
                fallbackRelic ??= eid;
            }
            return fallbackRelic;
        }

        public static void Tick(WorldStore world, float dt)
        {
            var s = world.Get<BreakthroughSessionComponent>(world.PlayerEntity);
            if (s == null || !s.IsOpen) return;

            s.SessionTime += dt;
            if (s.State != BreakthroughState.PoundingInSession) return;

            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) ||
                !world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
                return;

            if (s.SessionTime - s.LastWaveTime > 1.2f && s.MembraneCurrentHP < s.MembraneMaxHP)
            {
                s.MembraneCurrentHP = MathF.Min(s.MembraneMaxHP, s.MembraneCurrentHP + s.MembraneNaturalRegenRate * dt);
                ap.ApertureWallDurability = s.MembraneCurrentHP;
            }

            if (s.CurrentTurbulence > 0f)
            {
                s.CurrentTurbulence = MathF.Max(0f, s.CurrentTurbulence - 2.5f * dt);
            }

            if (ap.CurrentEssenceSea <= 0.8f && cult.PrimevalStones <= 0)
            {
                EndBreakthrough(world, s, cult, ap, BreakthroughState.FailedEssenceDry);
                return;
            }

            WorldSignals.EmitTopics(DirtyTopic.Breakthrough);
        }

        private static void EndBreakthrough(WorldStore world, BreakthroughSessionComponent s, CultivatorComponent cult, ApertureComponent ap, BreakthroughState result)
        {
            s.State = result;

            if (result == BreakthroughState.Succeeded)
            {
                if (ap.Substage == 4)
                {
                    CultivationSystem.PromoteMajorRank(cult, ap);
                }
                s.StatusMessage = $"★★★ MAJOR REALM TRANSCENDED ★★★ Ascended to Rank {ap.Rank} ({ap.GetMembraneTypeName()})!";
            }
            else if (result == BreakthroughState.FailedRupture)
            {
                ap.CurrentEssenceSea = 0f;
                ap.ApertureWallDurability = ap.MaxWallDurability;
                s.MembraneCurrentHP = s.MembraneMaxHP;
                s.StatusMessage = "【APERTURE RUPTURE】 Excessive Foreign Turbulence caused catastrophic essence feedback! Gu Master vomits blood and collapses!";
            }
            else if (result == BreakthroughState.FailedEssenceDry)
            {
                ap.ApertureWallDurability = ap.MaxWallDurability;
                s.MembraneCurrentHP = s.MembraneMaxHP;
                s.StatusMessage = "【Essence Depleted】 The Primeval Sea ran dry before the membrane cracked. The wall has rapidly healed itself shut.";
            }

            WorldSignals.EmitBreakthroughResolved(result, ap.Rank);
            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Cultivator | DirtyTopic.Breakthrough);
        }

        public static BreakthroughSessionSnapshot BuildSnapshot(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var ap = world.Get<ApertureComponent>(world.PlayerEntity);

            float rawSine = MathF.Sin(s.SessionTime * MathF.PI * 2f * s.OscillationFrequency);
            float normalizedOsc = (rawSine + 1f) * 0.5f;
            float sweetWidth = Math.Clamp(0.15f + (s.HumanPathMarks * 0.005f), 0.15f, 0.35f);

            int nextRank = Math.Min(5, s.CurrentRank + 1);
            string nextRankName = nextRank switch
            {
                2 => "Pale Red Iron (赤铁初阶)",
                3 => "White Silver (雪银初阶)",
                4 => "Yellow Gold (黄金初阶)",
                _ => "Crystal Purple (紫晶初阶)"
            };

            return new BreakthroughSessionSnapshot
            {
                State = s.State,
                CurrentRank = s.CurrentRank,
                TargetRank = nextRank,
                MembraneName = ap?.GetMembraneTypeName() ?? "Light Membrane (光膜)",
                TargetRankName = nextRankName,
                MembraneCurrentHP = s.MembraneCurrentHP,
                MembraneMaxHP = s.MembraneMaxHP,
                NaturalRegenRate = s.MembraneNaturalRegenRate,
                IsRegenerating = s.IsRegenerating,
                CurrentEssenceSea = ap?.CurrentEssenceSea ?? 0f,
                MaxEssenceCapacity = cult?.AptitudePercent ?? 100f,
                PrimevalStones = cult?.PrimevalStones ?? 0,
                HumanPathMarks = s.HumanPathMarks,
                CurrentTurbulence = s.CurrentTurbulence,
                TurbulenceLimit = BreakthroughSessionComponent.TURBULENCE_LIMIT,
                OscillationFrequency = s.OscillationFrequency,
                SweetSpotWidth = sweetWidth,
                NormalizedOscillation = normalizedOsc,
                RawSineValue = rawSine,
                ElapsedSessionTime = s.SessionTime,
                StatusMessage = s.StatusMessage
            };
        }
    }
}
