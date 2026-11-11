using System;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing Wild Gu / Beast Will Subjugation.
    /// </summary>
    public static class WildEncounterSystem
    {
        public static WildEncounterSessionComponent GetOrCreateSession(WorldStore world)
        {
            var s = world.Get<WildEncounterSessionComponent>(world.PlayerEntity);
            if (s == null)
            {
                s = world.Set(world.PlayerEntity, new WildEncounterSessionComponent());
            }
            return s;
        }

        public static int CalculateRankDifferential(WorldStore world, WildEncounterSessionComponent? s = null)
        {
            s ??= GetOrCreateSession(world);
            int playerRank = world.Get<ApertureComponent>(world.PlayerEntity)?.Rank ?? 1;
            int targetRank = s.Target?.Rank ?? 1;
            return playerRank - targetRank;
        }

        public static bool IsInstantRefineAvailable(WorldStore world, WildEncounterSessionComponent? s = null)
            => CalculateRankDifferential(world, s) >= 3;

        public static float GetHarmonicTolerance(WorldStore world, WildEncounterSessionComponent? s = null)
        {
            int deltaR = CalculateRankDifferential(world, s);
            if (deltaR >= 2) return 0.35f;
            if (deltaR == 1) return 0.25f;
            if (deltaR == 0) return 0.16f;
            if (deltaR == -1) return 0.08f;
            return 0.04f;
        }

        public static float GetFeralSpeed(WorldStore world, WildEncounterSessionComponent? s = null)
        {
            s ??= GetOrCreateSession(world);
            if (s.Target == null) return 1.0f;

            float baseSpeed = s.Target.Temperament switch
            {
                WildOrganismTemperament.Docile => 0.8f,
                WildOrganismTemperament.Ferocious => 1.4f,
                WildOrganismTemperament.Cunning => 1.8f,
                _ => 1.0f
            };

            int deltaR = CalculateRankDifferential(world, s);
            if (deltaR >= 2) baseSpeed *= 0.6f;
            else if (deltaR <= -1) baseSpeed *= 1.5f;

            return baseSpeed;
        }

        public static void OpenEncounter(WorldStore world, WildOrganismData target, IRandomSource? rngOverride = null)
        {
            IRandomSource rng = rngOverride ?? new SeededRandom(world.RunSeed + target.Rank);
            var s = GetOrCreateSession(world);
            s.Target = target;
            s.IsOpen = true;
            s.State = SubjugationState.Clashing;
            s.LastResolutionMessage = string.Empty;
            s.FeralWillResistance = target.FeralWillPower;
            s.PlayerWillAlignment = 0f;
            s.PlayerWillNeedle = 0.5f;
            s.FeralWillFrequency = (float)(0.35 + rng.NextDouble() * 0.30);
            s.ClashTimer = 0f;
            s.FeralNeedleRight = rng.Next(0, 2) == 1;

            WorldSignals.EmitUIModalActive(true);
            WorldSignals.EmitTopics(DirtyTopic.WildEncounter);
        }

        public static void CloseEncounter(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            if (!s.IsOpen) return;
            s.IsOpen = false;
            s.State = SubjugationState.Inactive;
            WorldSignals.EmitUIModalActive(false);
            WorldSignals.EmitTopics(DirtyTopic.WildEncounter);
        }

        public static void SetNeedle(WorldStore world, float position)
        {
            var s = GetOrCreateSession(world);
            s.PlayerWillNeedle = Math.Clamp(position, 0f, 1f);
            WorldSignals.EmitTopics(DirtyTopic.WildEncounter);
        }

        public static void AdjustNeedle(WorldStore world, float delta)
        {
            var s = GetOrCreateSession(world);
            s.PlayerWillNeedle = Math.Clamp(s.PlayerWillNeedle + delta, 0f, 1f);
            WorldSignals.EmitTopics(DirtyTopic.WildEncounter);
        }

        public static void PulseEssence(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            if (s.State != SubjugationState.Clashing || s.Target == null) return;
            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) ||
                !world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
                return;

            float cost = cult.AptitudePercent * 0.04f;
            if (ap.CurrentEssenceSea < cost) return;

            ap.CurrentEssenceSea = MathF.Max(0f, ap.CurrentEssenceSea - cost);

            float dist = MathF.Abs(s.PlayerWillNeedle - s.FeralWillFrequency);
            float tol = GetHarmonicTolerance(world, s);

            if (dist <= tol)
            {
                float damage = 22f + (CalculateRankDifferential(world, s) * 6f);
                s.FeralWillResistance = MathF.Max(0f, s.FeralWillResistance - damage);
                s.PlayerWillAlignment = MathF.Min(100f, s.PlayerWillAlignment + 18f);
            }
            else
            {
                s.FeralWillResistance = MathF.Min(100f, s.FeralWillResistance + 8f);
            }

            CheckWinLossConditions(world, s);
            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.WildEncounter);
        }

        public static void InstantSubjugate(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            if (s.State != SubjugationState.Clashing || s.Target == null || !IsInstantRefineAvailable(world, s))
                return;

            if (world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) &&
                world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
            {
                float cost = cult.AptitudePercent * 0.01f;
                ap.CurrentEssenceSea = MathF.Max(0f, ap.CurrentEssenceSea - cost);
            }

            ResolveSuccess(world, s, "Instantaneous Will Overwrite (阶位秒炼)! The creature's primitive consciousness is effortlessly crushed by absolute high-rank realm majesty.");
        }

        public static void Tick(WorldStore world, float dt)
        {
            var s = world.Get<WildEncounterSessionComponent>(world.PlayerEntity);
            if (s == null || !s.IsOpen || s.State != SubjugationState.Clashing || s.Target == null) return;

            s.ClashTimer += dt;
            float speed = GetFeralSpeed(world, s) * dt;
            if (s.FeralNeedleRight)
            {
                s.FeralWillFrequency += speed;
                if (s.FeralWillFrequency >= 0.88f) s.FeralNeedleRight = false;
            }
            else
            {
                s.FeralWillFrequency -= speed;
                if (s.FeralWillFrequency <= 0.12f) s.FeralNeedleRight = true;
            }

            float dist = MathF.Abs(s.PlayerWillNeedle - s.FeralWillFrequency);
            float tol = GetHarmonicTolerance(world, s);

            if (dist <= tol)
            {
                s.PlayerWillAlignment += (24f + CalculateRankDifferential(world, s) * 6f) * dt;
                s.FeralWillResistance = MathF.Max(0f, s.FeralWillResistance - 15f * dt);
            }
            else
            {
                float regen = (s.Target.Temperament == WildOrganismTemperament.Ferocious ? 12f : 6f) * dt;
                s.FeralWillResistance = MathF.Min(100f, s.FeralWillResistance + regen);
            }

            CheckWinLossConditions(world, s);
            WorldSignals.EmitTopics(DirtyTopic.WildEncounter);
        }

        private static void CheckWinLossConditions(WorldStore world, WildEncounterSessionComponent s)
        {
            if (s.FeralWillResistance <= 0f || s.PlayerWillAlignment >= 100f)
            {
                ResolveSuccess(world, s, "Wild consciousness subdued! Feral imprint dissolved into aperture obedience.");
            }
            else if (s.FeralWillResistance >= 100f && s.ClashTimer > 3.0f)
            {
                ResolveBacklash(world, s, "Wild Will Outburst (野性噬主)! Creature violently rejected your essence infusion, damaging aperture meridians!");
            }
        }

        private static void ResolveSuccess(WorldStore world, WildEncounterSessionComponent s, string reason)
        {
            s.State = SubjugationState.Success;
            s.LastResolutionMessage = reason;
            if (s.Target == null) return;

            var newGu = new GuWormComponent
            {
                GuID = s.Target.OrganismID,
                Name = s.Target.Name,
                Rank = s.Target.Rank,
                Slot = s.Target.SlotCategory,
                Diet = s.Target.Diet,
                BaseEssenceCost = s.Target.BaseEssenceCost,
                Satiety = 30f,
                IsVitalGu = false,
                HasHostileWill = false
            };

            WorldFactory.SpawnGuForOwner(world, world.PlayerEntity, newGu, isEquipped: false);

            WorldSignals.EmitWildEncounterResolved(s.State, newGu, reason);
            WorldSignals.EmitTopics(DirtyTopic.Inventory | DirtyTopic.WildEncounter);
        }

        private static void ResolveBacklash(WorldStore world, WildEncounterSessionComponent s, string reason)
        {
            s.State = SubjugationState.FailedBacklash;
            s.LastResolutionMessage = reason;

            if (world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) &&
                world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
            {
                float backlash = cult.AptitudePercent * 0.20f;
                ap.CurrentEssenceSea = MathF.Max(0f, ap.CurrentEssenceSea - backlash);
            }

            WorldSignals.EmitWildEncounterResolved(s.State, null, reason);
            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.WildEncounter);
        }

        public static SubjugationSessionSnapshot BuildSnapshot(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            return new SubjugationSessionSnapshot(
                s.Target ?? new WildOrganismData(),
                CalculateRankDifferential(world, s),
                s.State,
                s.FeralWillResistance,
                s.PlayerWillAlignment,
                s.PlayerWillNeedle,
                s.FeralWillFrequency,
                GetHarmonicTolerance(world, s),
                s.ClashTimer,
                s.LastResolutionMessage,
                IsInstantRefineAvailable(world, s)
            );
        }
    }
}
