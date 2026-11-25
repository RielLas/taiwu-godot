using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing living Gu worm feeding, cadence starvation, hibernation death, and parasitic host backlash across ALL cultivators.
    /// </summary>
    public static class GuEcologySystem
    {
        public static void ProcessFeedingCycleForWorld(WorldStore world)
        {
            var clock = world.GetSingleton<WorldClockComponent>();
            int currentTermIndex = clock?.CurrentSolarTermIndex ?? 4;
            bool touchedPlayer = false;

            var guList = new List<(EntityId GuId, GuWormComponent Gu, OwnerComponent Owner)>();
            foreach (var (guEntity, gu) in world.GetAll<GuWormComponent>())
            {
                if (world.TryGet<OwnerComponent>(guEntity, out var owner))
                {
                    guList.Add((guEntity, gu, owner));
                }
            }

            foreach (var (guEntity, gu, owner) in guList)
            {
                var ownerEntity = new EntityId(owner.OwnerEntityId);
                if (!world.TryGet<CultivatorComponent>(ownerEntity, out var cult) || !cult.IsAlive)
                    continue;

                var def = GuDatabase.Get(gu.GuID);
                int rationAmount = 1;
                GuDietType dietType = gu.Diet;
                float degradation = 30f;

                if (def?.Diet != null)
                {
                    dietType = (GuDietType)def.Diet.DietType;
                    gu.Diet = dietType;
                    rationAmount = Math.Max(1, def.Diet.RationAmount);
                    degradation = def.Diet.StarvationGraceDays > 0 ? (100f / (def.Diet.StarvationGraceDays + 1)) : 35f;
                }

                int cadence = gu.Rank switch
                {
                    1 => 1,
                    2 => 3,
                    3 => 6,
                    4 => 12,
                    5 => 24,
                    _ => 1
                };

                if ((currentTermIndex % cadence) != 0) continue;

                // Autonomous NPC owners abstractly purchase rations for 10 stones when inventory is depleted; player must spend real stockpile rations
                bool fed = false;
                if (world.TryGet<InventoryComponent>(ownerEntity, out var inv))
                {
                    if (inv.TryConsumeFood(dietType, rationAmount))
                    {
                        fed = true;
                    }
                    else if (!world.Has<PlayerTagComponent>(ownerEntity) && cult.PrimevalStones >= 10)
                    {
                        cult.PrimevalStones -= 10;
                        fed = true;
                    }
                }

                gu.ApplyFeedingCycleResult(fed, degradation);
                var stage = gu.GetStarvationStage();

                if (ownerEntity == world.PlayerEntity) touchedPlayer = true;

                if (fed)
                {
                    Debug.Log($"[GuEcologySystem] {gu.Name} consumed {rationAmount}x {dietType}. Satiety: 100%.");
                }
                else
                {
                    Debug.LogWarning($"[GuEcologySystem] {gu.Name} missed feeding! Satiety: {gu.Satiety:F0}% | State: {stage}");

                    if (stage == GuStarvationStage.Dead)
                    {
                        Debug.LogWarning($"[GuEcologySystem] {gu.Name} perished permanently from starvation.");
                        world.DestroyEntity(guEntity);
                        continue;
                    }

                    if (gu.IsPredatory && (stage == GuStarvationStage.Starving || stage == GuStarvationStage.Hibernating))
                    {
                        if (world.TryGet<ApertureComponent>(ownerEntity, out var aperture))
                        {
                            float essenceDrain = cult.AptitudePercent * 0.15f;
                            aperture.CurrentEssenceSea = MathF.Max(0f, aperture.CurrentEssenceSea - essenceDrain);
                            Debug.LogError($"[PARASITIC BACKLASH] Starving {gu.Name} drained {essenceDrain:F1}% sea from {cult.Name}!");
                        }
                    }
                }
            }

            if (touchedPlayer)
            {
                WorldSignals.EmitTopics(DirtyTopic.Inventory | DirtyTopic.Aperture);
            }
        }

        public static bool FeedGu(WorldStore world, EntityId guEntityId, GuDietType dietOverride = GuDietType.None, int quantity = 0, EntityId? ownerOverride = null)
        {
            EntityId ownerId = ownerOverride ?? world.PlayerEntity;
            if (!world.IsAlive(guEntityId) ||
                !world.TryGet<GuWormComponent>(guEntityId, out var gu) ||
                !world.TryGet<OwnerComponent>(guEntityId, out var owner) ||
                owner.OwnerEntityId != ownerId.Value ||
                !world.TryGet<InventoryComponent>(ownerId, out var inv))
                return false;

            var def = GuDatabase.Get(gu.GuID);
            GuDietType preferredDiet = def?.Diet != null ? (GuDietType)def.Diet.DietType : gu.Diet;
            GuDietType diet = dietOverride == GuDietType.None ? preferredDiet : dietOverride;

            int neededRations = def?.Diet != null ? Math.Max(1, def.Diet.RationAmount) : 1;
            int toConsume = quantity > 0 ? quantity : neededRations;

            if (!inv.TryConsumeFood(diet, toConsume))
            {
                Debug.LogWarning($"[GuEcologySystem] Insufficient {toConsume}x {diet} to feed {gu.Name}.");
                return false;
            }

            float gain = (100f / neededRations) * toConsume;
            gu.Feed(gain);
            Debug.Log($"[GuEcologySystem] Fed {gu.Name} +{gain:F0}% Satiety -> {gu.Satiety:F0}%");

            WorldSignals.EmitTopics(DirtyTopic.Inventory);
            return true;
        }

        public static bool FeedGuByCatalogId(WorldStore world, string guId, GuDietType dietOverride = GuDietType.None, int quantity = 0, EntityId? ownerOverride = null)
        {
            EntityId ownerId = ownerOverride ?? world.PlayerEntity;
            foreach (var (eid, gu) in world.GetOwned<GuWormComponent>(ownerId))
            {
                if (string.Equals(gu.GuID, guId, StringComparison.OrdinalIgnoreCase))
                {
                    return FeedGu(world, eid, dietOverride, quantity, ownerId);
                }
            }
            return false;
        }

        public static (System.Collections.Generic.List<GuWormComponent> Equipped, System.Collections.Generic.List<GuWormComponent> Reserve) GetLoadout(WorldStore world, EntityId? ownerId = null)
        {
            EntityId target = ownerId ?? world.PlayerEntity;
            var equipped = new System.Collections.Generic.List<GuWormComponent>();
            var reserve = new System.Collections.Generic.List<GuWormComponent>();

            foreach (var (eid, gu) in world.GetOwned<GuWormComponent>(target))
            {
                if (world.TryGet<OwnerComponent>(eid, out var owner) && owner.IsEquipped)
                    equipped.Add(gu);
                else
                    reserve.Add(gu);
            }
            return (equipped, reserve);
        }

        public static EntityId? FindOwnedGuEntity(WorldStore world, EntityId ownerId, string guIdOrName)
        {
            if (string.IsNullOrEmpty(guIdOrName)) return null;

            string legacyId = guIdOrName
                .Replace("_r1", "_01", StringComparison.OrdinalIgnoreCase)
                .Replace("_r2", "_02", StringComparison.OrdinalIgnoreCase)
                .Replace("_r3", "_03", StringComparison.OrdinalIgnoreCase);

            string canonicalId = guIdOrName
                .Replace("_01", "_r1", StringComparison.OrdinalIgnoreCase)
                .Replace("_02", "_r2", StringComparison.OrdinalIgnoreCase)
                .Replace("_03", "_r3", StringComparison.OrdinalIgnoreCase);

            foreach (var (eid, gu) in world.GetOwned<GuWormComponent>(ownerId))
            {
                if (string.Equals(gu.GuID, guIdOrName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(gu.GuID, legacyId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(gu.GuID, canonicalId, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(gu.Name) && gu.Name.IndexOf(guIdOrName, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return eid;
                }
            }
            return null;
        }
    }
}
