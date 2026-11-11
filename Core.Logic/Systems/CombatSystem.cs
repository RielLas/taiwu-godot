using System;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Psychology;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing overworld combat outcomes, loot, crime creation, and deferred transit resumption.
    /// </summary>
    public static class CombatSystem
    {
        public static void ResolveCombatConcluded(WorldStore world, bool playerWon, GridCoord tilePos, string enemyName, ArenaEncounterType encounter, IRandomSource? rngOverride = null)
        {
            IRandomSource rng = rngOverride ?? new SeededRandom(world.RunSeed ^ tilePos.X ^ tilePos.Y);
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var ap = world.Get<ApertureComponent>(world.PlayerEntity);
            var inv = world.Get<InventoryComponent>(world.PlayerEntity);

            if (playerWon && cult != null && inv != null)
            {
                TimeSystem.DeductActionPoints(world, 1);

                if (encounter == ArenaEncounterType.WildBoarHunt)
                {
                    cult.PrimevalStones += 15;
                    inv.AddFood(GuDietType.FreshBoarFlesh, 2);

                    string droppedGuId = rng.NextDouble() < 0.5 ? "gu_white_boar_r1" : "gu_black_boar_r1";
                    if (rng.NextDouble() < 0.40)
                    {
                        var wildGu = GuDatabase.CreateGuWormComponent(droppedGuId, isWild: true);
                        if (wildGu != null) WorldFactory.SpawnGuForOwner(world, world.PlayerEntity, wildGu, isEquipped: false);
                    }

                    ForensicsSystem.SpawnCrimeScene(world, tilePos, enemyName, CrimeSignature.BeastRemains, terms: 2);
                }
                else if (encounter == ArenaEncounterType.DemonicCultivator)
                {
                    cult.PrimevalStones += 40;
                    string[] caveLootPool = { "gu_liquor_worm_r1", "gu_white_jade_r1", "gu_little_light_r1", "gu_bronze_skin_r1" };
                    string lootId = caveLootPool[rng.Next(0, caveLootPool.Length)];
                    var wildGu = GuDatabase.CreateGuWormComponent(lootId, isWild: true);
                    if (wildGu != null) WorldFactory.SpawnGuForOwner(world, world.PlayerEntity, wildGu, isEquipped: false);

                    inv.AddFood(GuDietType.VintageWine, 2);
                }
                else if (encounter == ArenaEncounterType.ClanPatrolDuel)
                {
                    cult.PrimevalStones += 25;
                    inv.AddFood(GuDietType.MoonOrchidPetals, 3);
                }

                WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Inventory | DirtyTopic.Calendar | DirtyTopic.Forensics);
            }
            else if (!playerWon && ap != null)
            {
                ap.CurrentEssenceSea = MathF.Max(0f, ap.CurrentEssenceSea - 15f);
                WorldSignals.EmitTopics(DirtyTopic.Aperture);
            }

            TransitSystem.CompletePendingAmbushArrival(world);
        }
    }
}
