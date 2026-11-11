using System;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing Mortal Cultivation, Primeval Sea scaling, Aperture Wall Washing, and Stone Consumption.
    /// </summary>
    public static class CultivationSystem
    {
        public static bool ConsumeStone(WorldStore world, EntityId? target = null)
        {
            EntityId eid = target ?? world.PlayerEntity;
            if (!world.TryGet<CultivatorComponent>(eid, out var cult) ||
                !world.TryGet<ApertureComponent>(eid, out var aperture))
                return false;

            if (cult.PrimevalStones < 1)
            {
                Debug.LogWarning("[CultivationSystem] Insufficient Primeval Stones!");
                return false;
            }

            if (aperture.CurrentEssenceSea >= cult.AptitudePercent)
            {
                Debug.LogWarning("[CultivationSystem] Primeval Sea is already at maximum capacity for current aperture!");
                return false;
            }

            cult.PrimevalStones--;
            float recoveryPct = aperture.CalculateStoneRecoveryPercentage(cult.AptitudePercent);
            aperture.CurrentEssenceSea = MathF.Min(cult.AptitudePercent, aperture.CurrentEssenceSea + recoveryPct);

            Debug.Log($"[CultivationSystem] Crushed 1 Primeval Stone. Recovered +{recoveryPct:F2}% essence. Sea: {aperture.CurrentEssenceSea:F1}%/{cult.AptitudePercent:F1}%");

            WorldSignals.EmitSound(HalalSoundType.StoneShatter);
            WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Aperture);
            return true;
        }

        public static bool Meditate(WorldStore world, EntityId? target = null)
        {
            EntityId eid = target ?? world.PlayerEntity;
            if (!world.TryGet<CultivatorComponent>(eid, out var cult) ||
                !world.TryGet<ApertureComponent>(eid, out var aperture))
                return false;

            var clock = world.GetSingleton<WorldClockComponent>();
            if (eid == world.PlayerEntity && (clock == null || clock.ActionPointsRemaining < 1))
            {
                Debug.LogWarning("[CultivationSystem] Insufficient Action Points / Stamina to meditate!");
                return false;
            }

            if (aperture.CurrentEssenceSea >= cult.AptitudePercent)
            {
                Debug.LogWarning("[CultivationSystem] Primeval Sea is already at its peak innate capacity!");
                return false;
            }

            if (eid == world.PlayerEntity) TimeSystem.DeductActionPoints(world, 1);

            float recovery = cult.AptitudePercent * 0.25f;
            aperture.CurrentEssenceSea = MathF.Min(cult.AptitudePercent, aperture.CurrentEssenceSea + recovery);

            Debug.Log($"[CultivationSystem] Deep Meditation completed (-1 AP). Sea +{recovery:F1}% -> {aperture.CurrentEssenceSea:F1}%/{cult.AptitudePercent:F1}%");
            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Calendar);
            return true;
        }

        public static bool WashApertureWall(WorldStore world, EntityId? target = null)
        {
            EntityId eid = target ?? world.PlayerEntity;
            if (!world.TryGet<CultivatorComponent>(eid, out var cult) ||
                !world.TryGet<ApertureComponent>(eid, out var aperture))
                return false;

            if (aperture.Substage >= 4)
            {
                Debug.LogWarning("[CultivationSystem] Aperture Wall is fully solidified at Peak Stage. Major Rank breakthrough required!");
                return false;
            }

            var clock = world.GetSingleton<WorldClockComponent>();
            if (eid == world.PlayerEntity && (clock == null || clock.ActionPointsRemaining < 1))
            {
                Debug.LogWarning("[CultivationSystem] Insufficient Action Points / Stamina to wash aperture wall!");
                return false;
            }

            if (aperture.CurrentEssenceSea < 15f)
            {
                Debug.LogWarning("[CultivationSystem] Primeval Sea too shallow (<15%) to wash aperture wall!");
                return false;
            }

            if (eid == world.PlayerEntity) TimeSystem.DeductActionPoints(world, 1);

            aperture.CurrentEssenceSea = MathF.Max(0f, aperture.CurrentEssenceSea - 15f);

            int humanMarks = world.Get<DaoMarksComponent>(eid)?.Ledger.HumanPathMarks
                ?? ApertureComponent.DeriveInnateHumanPathMarks(cult.AptitudePercent);

            float baseGain = 18f;
            float daoBonus = humanMarks * 0.5f;
            float aptitudeFactor = cult.AptitudePercent / 50f;
            float progressGain = (baseGain + daoBonus) * aptitudeFactor;

            aperture.WallWashingProgress += progressGain;
            if (aperture.WallWashingProgress >= 100f)
            {
                aperture.Substage++;
                aperture.WallWashingProgress = 0f;
                Debug.Log($"[CultivationSystem] Substage Breakthrough! Wall tempered to {aperture.GetRankDescription()}!");
            }

            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Calendar);
            return true;
        }

        public static bool BatterApertureWall(WorldStore world, EntityId? target = null)
        {
            EntityId eid = target ?? world.PlayerEntity;
            if (!world.TryGet<CultivatorComponent>(eid, out var cult) ||
                !world.TryGet<ApertureComponent>(eid, out var aperture))
                return false;

            if (aperture.Substage != 4 || aperture.Rank >= 5)
            {
                Debug.LogWarning("[CultivationSystem] Major rank breakthrough requires Peak Stage (Rank 1-4).");
                return false;
            }

            var clock = world.GetSingleton<WorldClockComponent>();
            if (eid == world.PlayerEntity && (clock == null || clock.ActionPointsRemaining < 1))
            {
                Debug.LogWarning("[CultivationSystem] Insufficient Action Points to summon battering waves!");
                return false;
            }

            if (aperture.CurrentEssenceSea < 25f)
            {
                Debug.LogWarning("[CultivationSystem] Primeval Sea too calm (<25%) to batter membrane!");
                return false;
            }

            if (eid == world.PlayerEntity) TimeSystem.DeductActionPoints(world, 1);

            aperture.CurrentEssenceSea = MathF.Max(0f, aperture.CurrentEssenceSea - 25f);
            int humanMarks = world.Get<DaoMarksComponent>(eid)?.Ledger.HumanPathMarks
                ?? ApertureComponent.DeriveInnateHumanPathMarks(cult.AptitudePercent);

            float baseDamage = 35f;
            float seaForce = aperture.CurrentEssenceSea * 0.4f;
            float daoAmp = 1.0f + (humanMarks * 0.01f);
            float wallDamage = (baseDamage + seaForce) * daoAmp;

            aperture.ApertureWallDurability = MathF.Max(0f, aperture.ApertureWallDurability - wallDamage);
            if (aperture.ApertureWallDurability <= 0.01f)
            {
                PromoteMajorRank(cult, aperture);
                Debug.Log($"[CultivationSystem] APERTURE MEMBRANE SHATTERED! Ascended to Rank {aperture.Rank}!");
            }

            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Calendar);
            return true;
        }

        public static bool CanNurtureSubstage(WorldStore world, EntityId? target = null)
        {
            EntityId eid = target ?? world.PlayerEntity;
            if (!world.TryGet<CultivatorComponent>(eid, out var cult) ||
                !world.TryGet<ApertureComponent>(eid, out var aperture))
                return false;

            if (aperture.Substage >= 4) return false;
            float requiredCost = ApertureComponent.CalculateSubstageNurtureCost(aperture.Rank, aperture.Substage, cult.AptitudePercent);
            return cult.PrimevalStones >= (int)MathF.Ceiling(requiredCost);
        }

        public static bool TryNurtureSubstage(WorldStore world, EntityId? target = null)
        {
            if (!CanNurtureSubstage(world, target)) return false;

            EntityId eid = target ?? world.PlayerEntity;
            var cult = world.Get<CultivatorComponent>(eid)!;
            var aperture = world.Get<ApertureComponent>(eid)!;

            float cost = ApertureComponent.CalculateSubstageNurtureCost(aperture.Rank, aperture.Substage, cult.AptitudePercent);
            cult.PrimevalStones -= (int)MathF.Ceiling(cost);
            aperture.Substage++;
            aperture.CurrentNurtureProgress = 0f;
            aperture.WallWashingProgress = 0f;

            WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Aperture);
            return true;
        }

        public static bool TryConsumeEssence(WorldStore world, float baseBeuCost, EntityId? target = null)
        {
            EntityId eid = target ?? world.PlayerEntity;
            if (!world.TryGet<ApertureComponent>(eid, out var aperture)) return false;

            double density = aperture.GetDensityCoefficient();
            float realCostPct = (float)(baseBeuCost / density);
            if (aperture.CurrentEssenceSea < realCostPct) return false;

            aperture.CurrentEssenceSea -= realCostPct;
            WorldSignals.EmitTopics(DirtyTopic.Aperture);
            return true;
        }

        public static void RegenerateOnSolarTerm(WorldStore world)
        {
            foreach (var (eid, aperture) in world.GetAll<ApertureComponent>())
            {
                if (!world.TryGet<CultivatorComponent>(eid, out var cult) || !cult.IsAlive) continue;

                float regenPct = world.Has<PlayerTagComponent>(eid) ? cult.AptitudePercent * 0.25f : 15f;
                aperture.CurrentEssenceSea = MathF.Min(cult.AptitudePercent, aperture.CurrentEssenceSea + regenPct);
            }
            WorldSignals.EmitTopics(DirtyTopic.Aperture);
        }

        internal static void PromoteMajorRank(CultivatorComponent cult, ApertureComponent aperture)
        {
            aperture.Rank = Math.Min(5, aperture.Rank + 1);
            aperture.Substage = 1;
            aperture.WallWashingProgress = 0f;
            aperture.MaxWallDurability = ApertureComponent.GetMaxWallDurabilityForRank(aperture.Rank);
            aperture.ApertureWallDurability = aperture.MaxWallDurability;
            aperture.CurrentEssenceSea = cult.AptitudePercent;
        }
    }
}
