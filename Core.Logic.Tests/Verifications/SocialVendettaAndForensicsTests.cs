using System;
using System.Linq;
using Xunit;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Psychology;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class SocialVendettaAndForensicsTests : IDisposable
    {
        public SocialVendettaAndForensicsTests() => WorldSignals.ClearAllSubscriptions();
        public void Dispose() => WorldSignals.ClearAllSubscriptions();

        [Fact]
        public void CombatSystem_WildBoarHunt_RewardsLootAndSpawnsCrimeScene()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var inv = world.Get<InventoryComponent>(world.PlayerEntity)!;
            var clock = world.GetSingleton<WorldClockComponent>()!;

            int startStones = cult.PrimevalStones;
            int startAP = clock.ActionPointsRemaining;
            int startMeat = inv.GetFoodQuantity(GuDietType.FreshBoarFlesh);

            GridCoord fightPos = new GridCoord(12, 18);
            CombatSystem.ResolveCombatConcluded(world, true, fightPos, "Frenzied Razor Boar", ArenaEncounterType.WildBoarHunt);

            Assert.Equal(startAP - 1, clock.ActionPointsRemaining);
            Assert.Equal(startStones + 15, cult.PrimevalStones);
            Assert.Equal(startMeat + 2, inv.GetFoodQuantity(GuDietType.FreshBoarFlesh));

            var scene = ForensicsSystem.GetActiveCrimeSceneAt(world, fightPos);
            Assert.NotNull(scene);
            Assert.Equal(CrimeSignature.BeastRemains, scene!.Value.Scene.Signature);
            Assert.Equal(2, scene.Value.Scene.LingeringSoulQiTerms);
        }

        [Fact]
        public void ForensicsSystem_AllThreeTamperChoices_AndTwoTermDissipation()
        {
            var world = WorldFactory.CreateDefault();
            var clock = world.GetSingleton<WorldClockComponent>()!;
            clock.ActionPointsRemaining = 30;

            // 1. Cleanse destroys crime entity & cleans residue
            GridCoord p1 = new GridCoord(5, 5);
            ForensicsSystem.SpawnCrimeScene(world, p1, "Elder Gu Yue", CrimeSignature.CleanIncisions, terms: 2);
            Assert.True(ForensicsSystem.TamperCrimeScene(world, p1, CrimeTamperChoice.Cleanse));
            Assert.Null(ForensicsSystem.GetActiveCrimeSceneAt(world, p1));
            Assert.Equal(29, clock.ActionPointsRemaining);

            // 2. Frame Wild Beasts
            GridCoord p2 = new GridCoord(6, 6);
            EntityId e2 = ForensicsSystem.SpawnCrimeScene(world, p2, "Rival Scout", CrimeSignature.CleanIncisions, terms: 2);
            Assert.True(ForensicsSystem.TamperCrimeScene(world, p2, CrimeTamperChoice.FrameWildBeasts));
            Assert.Equal(CrimeSignature.FramedWildBeast, world.Get<CrimeSceneComponent>(e2)!.Signature);
            Assert.Equal("Wild Boar Pack (野彘群)", world.Get<CrimeSceneComponent>(e2)!.FramedFaction);

            // 3. Frame Rival Clan
            GridCoord p3 = new GridCoord(7, 7);
            EntityId e3 = ForensicsSystem.SpawnCrimeScene(world, p3, "Caravan Guard", CrimeSignature.CleanIncisions, terms: 2);
            Assert.True(ForensicsSystem.TamperCrimeScene(world, p3, CrimeTamperChoice.FrameRivalClan));
            Assert.Equal(CrimeSignature.FramedRivalClan, world.Get<CrimeSceneComponent>(e3)!.Signature);
            Assert.Equal("Xiong Clan (熊家寨)", world.Get<CrimeSceneComponent>(e3)!.FramedFaction);

            // 4. Temporal Dissipation over 2 Solar Terms
            GridCoord p4 = new GridCoord(20, 20);
            ForensicsSystem.SpawnCrimeScene(world, p4, "Nameless", CrimeSignature.CleanIncisions, terms: 2);
            TimeSystem.RollOverMonth(world);
            Assert.Equal(1, ForensicsSystem.GetActiveCrimeSceneAt(world, p4)!.Value.Scene.LingeringSoulQiTerms);

            TimeSystem.RollOverMonth(world);
            Assert.Null(ForensicsSystem.GetActiveCrimeSceneAt(world, p4));
        }

        [Fact]
        public void SocialActions_AndKinshipSoulTabletRetribution_Work()
        {
            var world = WorldFactory.CreateDefault();
            EntityId chiId = world.FindByExternalId("npc_chi_disciple");
            EntityId moElderId = world.FindByExternalId("npc_mo_elder");
            Assert.True(chiId.IsValid);
            Assert.True(moElderId.IsValid);

            var chi = world.Get<CultivatorComponent>(chiId)!;
            var moElder = world.Get<CultivatorComponent>(moElderId)!;
            int favorBefore = chi.Favorability;

            // Barter exchanges stones & increases clan reputation + favorability
            SocialVendettaSystem.ExecuteBarter(world, chiId);
            Assert.Equal(favorBefore + 10, chi.Favorability);

            // Righteous NPC refuses extortion & slashes favorability
            EntityId enforcerId = world.FindByExternalId("npc_enforcer_01");
            var enforcer = world.Get<CultivatorComponent>(enforcerId)!;
            int enfFavorBefore = enforcer.Favorability;
            SocialVendettaSystem.ExecuteExtort(world, enforcerId);
            Assert.Equal(enfFavorBefore - 80, enforcer.Favorability);

            // Murdering npc_chi_disciple shatters Soul Tablet & provokes npc_mo_elder retribution
            int moFavorBefore = moElder.Favorability;
            SocialVendettaSystem.ProcessVictimExecution(world, "npc_chi_disciple", new GridCoord(15, 14), CrimeSignature.CleanIncisions);

            Assert.NotNull(ForensicsSystem.GetActiveCrimeSceneAt(world, new GridCoord(15, 14)));
            Assert.True(moElder.Favorability < moFavorBefore);
            Assert.Equal(VengeancePursuitType.SoloAmbush, moElder.PursuitState);
        }
    }
}
