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
        public static CombatArenaSimulation GetOrCreateArena(WorldStore world)
            => world.GetOrCreateSingleton<CombatSessionComponent>().Simulation;

        public static bool StartCombat(
            WorldStore world,
            ArenaEncounterType encounter,
            GridCoord tilePos,
            EntityId? targetNpcId = null,
            bool isLethal = true,
            string? enemyName = null,
            IRandomSource? rng = null)
        {
            var session = world.GetOrCreateSingleton<CombatSessionComponent>();
            if (session.InBattle || session.Simulation.IsActive) return false;

            IRandomSource rngSource = rng ?? new SeededRandom(
                world.RunSeed ^ (tilePos.X * 73856093) ^ (tilePos.Y * 19349663));

            session.Simulation.InitializeArena(world, encounter, tilePos, targetNpcId, isLethal, enemyName, rngSource);
            session.InBattle = true;
            session.EncounterType = encounter;
            session.SourceTilePos = tilePos;
            session.IsLethal = isLethal;

            string targetExternalId = string.Empty;
            if (targetNpcId is { } npcId && world.TryGet<CultivatorComponent>(npcId, out var npcCult))
            {
                targetExternalId = npcCult.ExternalId;
            }
            session.TargetNpcExternalId = targetExternalId;

            WorldSignals.EmitBattleStageChanged(true);
            WorldSignals.EmitCombatRequested(encounter, tilePos);
            return true;
        }

        public static void Tick(WorldStore world, float dt)
        {
            var session = world.Get<CombatSessionComponent>(world.WorldEntity);
            if (session == null || !session.InBattle) return;

            var simulation = session.Simulation;
            if (!simulation.IsActive)
            {
                if (simulation.Outcome != CombatOutcome.Ongoing)
                {
                    ConcludeCombat(world, simulation.Outcome);
                }
                else
                {
                    // A loaded save may carry the battle flag without a live runtime simulation.
                    session.InBattle = false;
                }
                return;
            }

            CombatOutcome outcome = simulation.Step(world, dt);
            if (outcome != CombatOutcome.Ongoing)
            {
                ConcludeCombat(world, outcome);
            }
        }

        public static void ConcludeCombat(WorldStore world, CombatOutcome outcome)
        {
            var session = world.Get<CombatSessionComponent>(world.WorldEntity);
            if (session == null || !session.InBattle) return;

            var simulation = session.Simulation;
            session.InBattle = false;
            simulation.IsActive = false;

            var aperture = world.Get<ApertureComponent>(world.PlayerEntity);
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);

            if (aperture != null)
            {
                float health = simulation.EntityBuffer[0].Health;
                aperture.CurrentHealth = Math.Clamp(health, 0f, aperture.MaxHealth);
                if (outcome == CombatOutcome.Defeat || outcome == CombatOutcome.Retreated)
                {
                    aperture.CurrentHealth = MathF.Max(1f, aperture.MaxHealth * 0.20f);
                }
            }

            if (outcome == CombatOutcome.Defeat && cult != null)
            {
                int stoneLoss = Math.Min(cult.PrimevalStones, (int)(cult.PrimevalStones * 0.15f));
                cult.PrimevalStones -= stoneLoss;
            }

            ResolveCombatConcluded(
                world,
                outcome == CombatOutcome.Victory,
                simulation.SourceTilePos,
                simulation.EnemyDisplayName,
                simulation.EncounterType);

            if (outcome == CombatOutcome.Victory &&
                simulation.TargetNpcId is { } targetNpcId &&
                world.TryGet<CultivatorComponent>(targetNpcId, out var npcCult))
            {
                if (!simulation.IsLethal)
                {
                    npcCult.Favorability = Math.Min(300, npcCult.Favorability + 15);
                    npcCult.AddMemory("Defeated in fair sparring duel (+15 Favor).");
                    WorldSignals.EmitTopics(DirtyTopic.NPCs);
                }
                else
                {
                    npcCult.Age = npcCult.LifespanYears;
                    SocialVendettaSystem.ProcessVictimExecution(
                        world,
                        npcCult.ExternalId,
                        simulation.SourceTilePos,
                        CrimeSignature.CleanIncisions);
                }
            }

            string summary = outcome switch
            {
                CombatOutcome.Victory => $"Victory over {simulation.EnemyDisplayName}!",
                CombatOutcome.Defeat => $"Defeat at the hands of {simulation.EnemyDisplayName}. Barely escaped with your life.",
                CombatOutcome.Retreated => $"Retreated from {simulation.EnemyDisplayName}.",
                _ => string.Empty
            };

            WorldSignals.EmitCombatResolved(simulation.EncounterType, outcome, summary);
            WorldSignals.EmitBattleStageChanged(false);
            WorldSignals.EmitTopics(
                DirtyTopic.Cultivator | DirtyTopic.Aperture | DirtyTopic.Inventory |
                DirtyTopic.NPCs | DirtyTopic.Forensics | DirtyTopic.Calendar);
        }

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
                    string lootId = ExplorationSystem.PickWildGuForSector(world, world.CurrentSectorKey, rng);
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
