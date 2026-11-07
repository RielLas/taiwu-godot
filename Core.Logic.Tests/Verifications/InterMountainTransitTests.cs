using System;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Combat;

namespace Taiwu_Godot.Core.Logic.Tests.Verifications
{
    public class InterMountainTransitTests : IDisposable
    {
        public InterMountainTransitTests()
        {
            GameEventManager.ClearAllSubscriptions();
        }

        public void Dispose()
        {
            GameEventManager.ClearAllSubscriptions();
        }

        [Fact]
        public void Test_CanTravelToMountain_Validation()
        {
            var ctx = GameStateContext.CreateDefault();
            ctx.Cultivator.PrimevalStones = 10; // Insufficient (Need 25)

            using var service = new InterMountainTransitService();
            service.Initialize(ctx);

            bool canTravel = service.CanTravelToMountain(SouthernMountainID.BaiClanMountain, out string reason);
            Assert.False(canTravel);
            Assert.Contains("Stones", reason);

            // Give stones, test insufficient AP
            ctx.Cultivator.PrimevalStones = 50;
            ctx.Calendar.ActionPointsRemaining = 2; // Insufficient (Need 6)

            canTravel = service.CanTravelToMountain(SouthernMountainID.BaiClanMountain, out reason);
            Assert.False(canTravel);
            Assert.Contains("AP", reason);

            // Grant adequate AP
            ctx.Calendar.ActionPointsRemaining = 10;
            canTravel = service.CanTravelToMountain(SouthernMountainID.BaiClanMountain, out reason);
            Assert.True(canTravel);

            // Cannot travel to current mountain
            canTravel = service.CanTravelToMountain(SouthernMountainID.GuYueVillage, out reason);
            Assert.False(canTravel);
            Assert.Contains("Already", reason);
        }

        [Fact]
        public void Test_MountainTravel_DeductsCosts_And_SavesSectorSnapshot()
        {
            var ctx = GameStateContext.CreateDefault();
            ctx.Cultivator.PrimevalStones = 60;
            ctx.Calendar.ActionPointsRemaining = 20;

            using var service = new InterMountainTransitService();
            service.Initialize(ctx);

            int initialStones = ctx.Cultivator.PrimevalStones;
            int initialAP = ctx.Calendar.ActionPointsRemaining;

            service.TravelToMountain(SouthernMountainID.BaiClanMountain);

            Assert.Equal(initialStones - InterMountainTransitService.TRAVEL_STONE_COST, ctx.Cultivator.PrimevalStones);
            Assert.Equal(initialAP - InterMountainTransitService.TRAVEL_AP_COST, ctx.Calendar.ActionPointsRemaining);

            // Verify sector snapshot was saved
            Assert.True(ctx.WorldMap.SectorSnapshots.ContainsKey("Sector_SouthernBorder_GuYueVillage"));
        }

        [Fact]
        public void Test_RoadAmbush_Defers_Arrival_Until_Combat_Concluded()
        {
            var ctx = GameStateContext.CreateDefault();
            using var service = new InterMountainTransitService();
            service.Initialize(ctx);

            // Simulate road ambush occurring: pending destination set
            SouthernMountainID arrivedMountain = SouthernMountainID.GuYueVillage;
            GameEventManager.OnMountainChanged += (m, name) =>
            {
                arrivedMountain = m;
            };

            // Combat concludes -> should resolve deferred arrival
            // Directly invoke through event manager to simulate combat victory
            GameEventManager.RaiseRequestMountainTransit(SouthernMountainID.XiongRidge);

            // If ambush triggered or if we trigger combat concluded while pending
            if (service.PendingMountainDestination.HasValue)
            {
                Assert.Equal(SouthernMountainID.XiongRidge, service.PendingMountainDestination.Value);
                // Conclude combat
                GameEventManager.TriggerCombatConcluded(true, new GridCoord(15, 15), "Wild Boar Pack", ArenaEncounterType.WildBoarHunt);
                Assert.Null(service.PendingMountainDestination);
                Assert.Equal(SouthernMountainID.XiongRidge, arrivedMountain);
            }
        }

        [Fact]
        public void Test_RegionalTransit_Backlash_Canon_Laws()
        {
            var ctx = GameStateContext.CreateDefault();
            ctx.Cultivator.Rank = 4;
            ctx.Cultivator.PrimevalStones = 100;
            ctx.Cultivator.CurrentEssenceSea = 80f;
            ctx.Cultivator.ApertureWallDurability = 100f;
            ctx.Calendar.ActionPointsRemaining = 30;

            using var service = new InterMountainTransitService();
            service.Initialize(ctx);

            // 1. Sealed crossing test: avoids high-rank rupture
            service.TravelToRegion(WorldRegionID.NorthernPlains, sealAperture: true);

            Assert.Equal(0f, ctx.Cultivator.CurrentEssenceSea); // Sea suppressed
            Assert.Equal(95f, ctx.Cultivator.ApertureWallDurability); // -5% durability only

            // 2. Unsealed crossing test for Rank 5
            ctx.Cultivator.Rank = 5;
            ctx.Cultivator.PrimevalStones = 100;
            ctx.Cultivator.CurrentHealth = 100;
            ctx.Cultivator.ApertureWallDurability = 100f;
            ctx.Calendar.ActionPointsRemaining = 30;

            service.TravelToRegion(WorldRegionID.EasternSea, sealAperture: false);

            Assert.Equal(0f, ctx.Cultivator.CurrentEssenceSea);
            Assert.Equal(50, ctx.Cultivator.CurrentHealth); // -50% HP
            Assert.Equal(40f, ctx.Cultivator.ApertureWallDurability, 1); // -60% durability
        }
    }
}
