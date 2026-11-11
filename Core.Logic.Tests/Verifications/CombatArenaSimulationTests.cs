using System;
using System.Collections.Generic;
using Xunit;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Psychology;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class CombatArenaSimulationTests : IDisposable
    {
        public CombatArenaSimulationTests() => WorldSignals.ClearAllSubscriptions();
        public void Dispose() => WorldSignals.ClearAllSubscriptions();

        [Fact]
        public void Step_AfterWarmup_IsZeroGcAllocation()
        {
            var world = WorldFactory.CreateDefault();
            var simulation = CombatSystem.GetOrCreateArena(world);
            simulation.InitializeArena(
                world,
                ArenaEncounterType.WildBoarHunt,
                new GridCoord(15, 15),
                targetNpcId: null,
                isLethal: true,
                enemyName: "Zero-GC Boars",
                rng: new SeededRandom(7));

            ref var player = ref simulation.EntityBuffer[0];
            player.MaxHealth = 100000f;
            player.Health = 100000f;

            for (int i = 0; i < 100; i++) simulation.Step(world, 0.016f);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) simulation.Step(world, 0.016f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(0, allocated);
        }

        [Fact]
        public void Casting_UsesRealApertureEssence_AndStarvationScalesCost()
        {
            var world = WorldFactory.CreateDefault();
            var aperture = world.Get<ApertureComponent>(world.PlayerEntity)!;
            EntityId moonId = FindGu(world, "gu_moonlight_01");
            var moon = world.Get<GuWormComponent>(moonId)!;
            var simulation = CombatSystem.GetOrCreateArena(world);
            simulation.InitializeArena(
                world,
                ArenaEncounterType.WildBoarHunt,
                new GridCoord(15, 15),
                targetNpcId: null,
                isLethal: true,
                enemyName: "Essence Boars",
                rng: new SeededRandom(11));

            Assert.True(simulation.PlayerSkills[0].IsPopulated);
            Assert.Equal(GuSkillEffectKind.Projectile, simulation.PlayerSkills[0].EffectKind);

            float healthyCost = moon.GetRealEssenceCost(1, 1);
            aperture.CurrentEssenceSea = 44f;
            Assert.True(simulation.TryCastSkill(world, 0));
            Assert.Equal(44f - healthyCost, aperture.CurrentEssenceSea, 3);

            moon.Satiety = 40f;
            simulation.PlayerSkills[0].CooldownRemaining = 0f;
            aperture.CurrentEssenceSea = 44f;
            float hungryCost = moon.GetRealEssenceCost(1, 1);
            Assert.Equal(12.5f, hungryCost, 3);
            Assert.True(simulation.TryCastSkill(world, 0));
            Assert.Equal(44f - hungryCost, aperture.CurrentEssenceSea, 3);

            moon.Satiety = 0f;
            simulation.PlayerSkills[0].CooldownRemaining = 0f;
            aperture.CurrentEssenceSea = 44f;
            Assert.Equal(GuStarvationStage.Hibernating, moon.GetStarvationStage());
            Assert.False(simulation.TryCastSkill(world, 0));
            Assert.Equal(44f, aperture.CurrentEssenceSea, 3);
        }

        [Fact]
        public void CrushStone_RestoresSea_AndHonorsCooldown()
        {
            var world = WorldFactory.CreateDefault();
            var aperture = world.Get<ApertureComponent>(world.PlayerEntity)!;
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var simulation = CombatSystem.GetOrCreateArena(world);
            simulation.InitializeArena(
                world,
                ArenaEncounterType.WildBoarHunt,
                new GridCoord(15, 15),
                targetNpcId: null,
                isLethal: true,
                enemyName: "Stone Boars",
                rng: new SeededRandom(13));
            simulation.EntityBuffer[0].MaxHealth = 10000f;
            simulation.EntityBuffer[0].Health = 10000f;

            int stonesBefore = cult.PrimevalStones;
            aperture.CurrentEssenceSea = 0f;

            Assert.True(simulation.TryCrushStone(world));
            Assert.Equal(stonesBefore - 1, cult.PrimevalStones);
            Assert.Equal(35f, aperture.CurrentEssenceSea, 2);
            Assert.Equal(1.5f, simulation.StoneCrushCooldown, 3);
            Assert.False(simulation.TryCrushStone(world));

            for (int i = 0; i < 16; i++) simulation.Step(world, 0.1f);
            aperture.CurrentEssenceSea = 0f;
            Assert.True(simulation.TryCrushStone(world));
            Assert.Equal(stonesBefore - 2, cult.PrimevalStones);
        }

        [Fact]
        public void Spar_NonLethalVictory_AwardsFavor_AndCreatesNoCrimeScene()
        {
            var world = WorldFactory.CreateDefault();
            EntityId chiId = world.FindByExternalId("npc_chi_disciple");
            var chi = world.Get<CultivatorComponent>(chiId)!;
            int favorBefore = chi.Favorability;

            Assert.True(CombatSystem.StartCombat(
                world,
                ArenaEncounterType.ClanPatrolDuel,
                new GridCoord(15, 14),
                targetNpcId: chiId,
                isLethal: false,
                enemyName: chi.Name));

            var simulation = CombatSystem.GetOrCreateArena(world);
            simulation.EntityBuffer[1].Health = simulation.EntityBuffer[1].MaxHealth * 0.10f;
            CombatSystem.Tick(world, 0.016f);

            Assert.True(chi.IsAlive);
            Assert.Equal(favorBefore + 15, chi.Favorability);
            Assert.Equal(0, CountCrimeScenes(world));
            Assert.False(world.Get<CombatSessionComponent>(world.WorldEntity)!.InBattle);
            Assert.Equal(CombatOutcome.Victory, simulation.Outcome);
        }

        [Fact]
        public void Ambush_LethalVictory_KillsNpc_AndSpawnsForensicsAndRetribution()
        {
            var world = WorldFactory.CreateDefault();
            EntityId chiId = world.FindByExternalId("npc_chi_disciple");
            EntityId moId = world.FindByExternalId("npc_mo_elder");
            var chi = world.Get<CultivatorComponent>(chiId)!;
            var mo = world.Get<CultivatorComponent>(moId)!;

            GridCoord ambushPos = new GridCoord(15, 14);
            Assert.True(CombatSystem.StartCombat(
                world,
                ArenaEncounterType.DemonicCultivator,
                ambushPos,
                targetNpcId: chiId,
                isLethal: true,
                enemyName: chi.Name));

            var simulation = CombatSystem.GetOrCreateArena(world);
            ref var enemy = ref simulation.EntityBuffer[1];
            enemy.Health = 0f;
            enemy.Active = false;
            CombatSystem.Tick(world, 0.016f);

            Assert.False(chi.IsAlive);
            Assert.NotNull(ForensicsSystem.GetActiveCrimeSceneAt(world, ambushPos));
            Assert.True(mo.Favorability < 0);
            Assert.Equal(VengeancePursuitType.SoloAmbush, mo.PursuitState);
        }

        [Fact]
        public void Defeat_AndRetreat_RecoverThePlayer_AndEmitResolution()
        {
            var world = WorldFactory.CreateDefault();
            var aperture = world.Get<ApertureComponent>(world.PlayerEntity)!;
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            int stonesBefore = cult.PrimevalStones;

            var resolved = new List<CombatOutcome>();
            Action<ArenaEncounterType, CombatOutcome, string> handler =
                (encounter, outcome, message) => resolved.Add(outcome);
            WorldSignals.OnCombatResolved += handler;

            Assert.True(CombatSystem.StartCombat(
                world,
                ArenaEncounterType.WildBoarHunt,
                new GridCoord(15, 15),
                targetNpcId: null,
                isLethal: true,
                enemyName: "Defeat Boars"));

            var simulation = CombatSystem.GetOrCreateArena(world);
            simulation.EntityBuffer[0].Health = 0f;
            CombatSystem.Tick(world, 0.016f);

            Assert.Equal(CombatOutcome.Defeat, resolved[^1]);
            Assert.True(aperture.CurrentHealth >= 1f);
            Assert.True(cult.PrimevalStones < stonesBefore);
            Assert.False(world.Get<CombatSessionComponent>(world.WorldEntity)!.InBattle);

            var retreatWorld = WorldFactory.CreateDefault();
            var retreatAperture = retreatWorld.Get<ApertureComponent>(retreatWorld.PlayerEntity)!;
            Assert.True(CombatSystem.StartCombat(
                retreatWorld,
                ArenaEncounterType.WildBoarHunt,
                new GridCoord(15, 15),
                targetNpcId: null,
                isLethal: true,
                enemyName: "Retreat Boars"));

            var retreatSimulation = CombatSystem.GetOrCreateArena(retreatWorld);
            retreatSimulation.RequestRetreat();
            CombatSystem.Tick(retreatWorld, 0.016f);

            Assert.Equal(CombatOutcome.Retreated, resolved[^1]);
            Assert.True(retreatAperture.CurrentHealth >= 1f);
            Assert.False(retreatWorld.Get<CombatSessionComponent>(retreatWorld.WorldEntity)!.InBattle);

            WorldSignals.OnCombatResolved -= handler;
        }

        private static EntityId FindGu(WorldStore world, string guId)
        {
            foreach (var (eid, gu) in world.GetOwned<GuWormComponent>(world.PlayerEntity))
            {
                if (string.Equals(gu.GuID, guId, StringComparison.Ordinal)) return eid;
            }
            return EntityId.None;
        }

        private static int CountCrimeScenes(WorldStore world)
        {
            int count = 0;
            foreach (var _ in world.GetAll<CrimeSceneComponent>()) count++;
            return count;
        }
    }
}
