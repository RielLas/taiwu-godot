using System;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Combat;

namespace Taiwu_Godot.Core.Logic.Tests.Verifications
{
    public class WildGuEncounterTests : IDisposable
    {
        public WildGuEncounterTests()
        {
            GameEventManager.ClearAllSubscriptions();
        }

        public void Dispose()
        {
            GameEventManager.ClearAllSubscriptions();
        }

        [Fact]
        public void Test_RankDifferential_And_HarmonicTolerance()
        {
            var ctx = GameStateContext.CreateDefault();
            ctx.Cultivator.Rank = 3;

            using var service = new WildGuEncounterService();
            service.Initialize(ctx);

            var docOrganism = new WildOrganismData("gu_test_r1", "Test Moonlight Gu", 1, GuSlotCategory.Attack, GuDietType.MoonOrchidPetals, 10f, WildOrganismTemperament.Docile, "Test Lore");
            GameEventManager.RaiseRequestOpenWildGuEncounter(docOrganism);

            Assert.True(service.IsEncounterOpen);
            Assert.Equal(SubjugationState.Clashing, service.CurrentState);
            Assert.Equal(2, service.CalculateRankDifferential()); // Rank 3 - Rank 1 = 2
            Assert.Equal(0.35f, service.GetHarmonicTolerance());
            Assert.False(service.IsInstantRefineAvailable());

            // Rank 4 vs Rank 1 => DeltaR = 3 => Instant Overwrite available
            ctx.Cultivator.Rank = 4;
            Assert.Equal(3, service.CalculateRankDifferential());
            Assert.True(service.IsInstantRefineAvailable());
        }

        [Fact]
        public void Test_InstantaneousOverwrite_AddsToReserveGu()
        {
            var ctx = GameStateContext.CreateDefault();
            ctx.Cultivator.Rank = 4;
            ctx.Cultivator.CurrentEssenceSea = 50f;
            int initialReserveCount = ctx.GuInventory.ReserveGu.Count;

            using var service = new WildGuEncounterService();
            service.Initialize(ctx);

            var wildTarget = new WildOrganismData("gu_boar_r1", "Wild White Boar Gu", 1, GuSlotCategory.Defense, GuDietType.FreshBoarFlesh, 10f, WildOrganismTemperament.Ferocious, "Test Boar");
            GameEventManager.RaiseRequestOpenWildGuEncounter(wildTarget);

            bool resolvedFired = false;
            SubjugationState resolvedState = SubjugationState.Inactive;
            GuWormData? capturedGu = null;

            GameEventManager.OnWildGuEncounterResolved += (st, gu, msg) =>
            {
                resolvedFired = true;
                resolvedState = st;
                capturedGu = gu;
            };

            GameEventManager.RaiseRequestEncounterInstantSubjugate();

            Assert.True(resolvedFired);
            Assert.Equal(SubjugationState.Success, resolvedState);
            Assert.NotNull(capturedGu);
            Assert.Equal("gu_boar_r1", capturedGu.GuID);
            Assert.Equal(initialReserveCount + 1, ctx.GuInventory.ReserveGu.Count);
        }

        [Fact]
        public void Test_PulseEssenceWave_HarmonicAlignment_SuppressesWildWill()
        {
            var ctx = GameStateContext.CreateDefault();
            ctx.Cultivator.Rank = 2;
            ctx.Cultivator.AptitudePercent = 80f;
            ctx.Cultivator.CurrentEssenceSea = 60f;

            using var service = new WildGuEncounterService();
            service.Initialize(ctx);

            var wildTarget = new WildOrganismData("gu_light_r1", "Little Light Gu", 2, GuSlotCategory.Movement, GuDietType.MoonOrchidPetals, 10f, WildOrganismTemperament.Docile, "Test Light");
            GameEventManager.RaiseRequestOpenWildGuEncounter(wildTarget);

            float initialFeralResistance = service.FeralResistance;

            // Align player needle exactly with feral frequency
            GameEventManager.RaiseRequestSetEncounterNeedle(service.FeralFrequency);
            GameEventManager.RaiseRequestEncounterPulseEssence();

            Assert.True(service.FeralResistance < initialFeralResistance);
            Assert.True(service.AlignmentProgress > 0f);
        }

        [Fact]
        public void Test_ExplorationService_POI_Subjugation_Triggers()
        {
            var ctx = GameStateContext.CreateDefault();
            using var exploreService = new ExplorationService();
            exploreService.Initialize(ctx);

            WildOrganismData? discoveredOrganism = null;
            GameEventManager.OnWildGuEncounterOpened += (snapshot) =>
            {
                discoveredOrganism = snapshot.Target;
            };

            using var encounterService = new WildGuEncounterService();
            encounterService.Initialize(ctx);

            // Trigger beast den subjugation
            GameEventManager.RaiseRequestPOIAction(TilePOIType.WildBeastDen, new Vector2Int(10, 10), "beast_den_subjugate");

            Assert.NotNull(discoveredOrganism);
            Assert.Equal(WildOrganismTemperament.Ferocious, discoveredOrganism.Temperament);

            // Close and test cave subjugation
            GameEventManager.RaiseRequestCloseWildGuEncounter();

            discoveredOrganism = null;
            GameEventManager.RaiseRequestPOIAction(TilePOIType.AncientGuCave, new Vector2Int(12, 12), "cave_subjugate");

            Assert.NotNull(discoveredOrganism);
            Assert.Equal(WildOrganismTemperament.Cunning, discoveredOrganism.Temperament);
        }
    }
}
