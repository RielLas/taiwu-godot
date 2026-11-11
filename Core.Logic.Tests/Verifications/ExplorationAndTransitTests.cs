using System;
using Xunit;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class ExplorationAndTransitTests : IDisposable
    {
        private sealed class FixedRandom : IRandomSource
        {
            private readonly double _d;
            public FixedRandom(double value) => _d = value;
            public double NextDouble() => _d;
            public int Next() => 0;
            public int Next(int maxExclusive) => 0;
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
        }

        public ExplorationAndTransitTests() => WorldSignals.ClearAllSubscriptions();
        public void Dispose() => WorldSignals.ClearAllSubscriptions();

        [Fact]
        public void GridToWorld_RoundTrips_AndFollowsIsometricAxes()
        {
            foreach (var coord in new[] { new GridCoord(0, 0), new GridCoord(15, 15), new GridCoord(29, 29), new GridCoord(5, 23) })
            {
                var w = ExplorationSystem.GridToWorld(coord);
                Assert.Equal(coord, ExplorationSystem.WorldToGrid(w));
            }

            var center = ExplorationSystem.GridToWorld(new GridCoord(15, 15));
            var sw = ExplorationSystem.GridToWorld(new GridCoord(15, 16));
            var se = ExplorationSystem.GridToWorld(new GridCoord(16, 15));
            var nw = ExplorationSystem.GridToWorld(new GridCoord(14, 15));
            var ne = ExplorationSystem.GridToWorld(new GridCoord(15, 14));

            Assert.True(sw.Y > center.Y && sw.X < center.X);
            Assert.True(se.Y > center.Y && se.X > center.X);
            Assert.True(nw.Y < center.Y);
            Assert.True(ne.Y < center.Y);
        }

        [Fact]
        public void MovePlayer_UpdatesPositionAP_AndUnveilsFogRadius()
        {
            var world = WorldFactory.CreateDefault();
            var sector = world.GetCurrentSector()!;
            var clock = world.GetSingleton<WorldClockComponent>()!;

            Assert.Equal(30, clock.ActionPointsRemaining);
            Assert.Equal(TilePOIType.ClanPost, sector.GetTile(15, 15).POI);
            Assert.Equal(TilePOIType.WayStation, sector.GetTile(19, 15).POI);
            Assert.Equal(TilePOIType.SpiritVein, sector.GetTile(14, 17).POI);

            var target = new GridCoord(16, 15);
            ExplorationSystem.MovePlayer(world, target, sensoryRadius: 3, evaluateWilderness: false);

            Assert.Equal(target, world.GetPlayerPosition());
            Assert.Equal(29, clock.ActionPointsRemaining);
            Assert.True(sector.GetTile(19, 15).IsVisible);
            Assert.True(sector.GetTile(19, 15).IsExplored);
        }

        [Fact]
        public void Transit_MountainValidation_CostDeduction_AndSnapshotPreservation()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var clock = world.GetSingleton<WorldClockComponent>()!;

            cult.PrimevalStones = 10;
            Assert.False(TransitSystem.CanTravelToMountain(world, SouthernMountainID.BaiClanMountain, out string reason));
            Assert.Contains("Stones", reason);

            cult.PrimevalStones = 60;
            clock.ActionPointsRemaining = 2;
            Assert.False(TransitSystem.CanTravelToMountain(world, SouthernMountainID.BaiClanMountain, out reason));
            Assert.Contains("AP", reason);

            clock.ActionPointsRemaining = 20;
            Assert.True(TransitSystem.CanTravelToMountain(world, SouthernMountainID.BaiClanMountain, out _));
            Assert.False(TransitSystem.CanTravelToMountain(world, SouthernMountainID.GuYueVillage, out reason));
            Assert.Contains("Already", reason);

            Assert.True(TransitSystem.TravelToMountain(world, SouthernMountainID.BaiClanMountain, new FixedRandom(0.9)));
            Assert.Equal(60 - TransitSystem.TRAVEL_STONE_COST, cult.PrimevalStones);
            Assert.Equal(20 - TransitSystem.TRAVEL_AP_COST, clock.ActionPointsRemaining);
            Assert.Equal(SouthernMountainID.BaiClanMountain, world.GetCurrentSector()!.MountainId);
            Assert.NotNull(world.GetSector(TransitSystem.MakeSectorKey(WorldRegionID.SouthernBorder, SouthernMountainID.GuYueVillage)));
        }

        [Fact]
        public void Transit_RoadAmbush_DefersArrivalUntilCombatConcluded()
        {
            var world = WorldFactory.CreateDefault();
            var session = TransitSystem.GetOrCreateSession(world);

            bool ambushFired = false;
            WorldSignals.OnRoadAmbushOccurred += (enc, pos) => ambushFired = true;

            // FixedRandom(0.05) < 0.25 triggers ambush
            Assert.True(TransitSystem.TravelToMountain(world, SouthernMountainID.XiongRidge, new FixedRandom(0.05)));
            Assert.True(ambushFired);
            Assert.Equal(SouthernMountainID.XiongRidge, session.PendingMountainDestination);
            Assert.Equal(SouthernMountainID.GuYueVillage, world.GetCurrentSector()!.MountainId);

            CombatSystem.ResolveCombatConcluded(world, true, world.GetPlayerPosition(), "Wild Boar", ArenaEncounterType.WildBoarHunt);
            Assert.Null(session.PendingMountainDestination);
            Assert.Equal(SouthernMountainID.XiongRidge, world.GetCurrentSector()!.MountainId);
        }

        [Fact]
        public void Transit_RegionalBoundaryWall_RestraintGuPreventsBacklash()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;
            var clock = world.GetSingleton<WorldClockComponent>()!;

            cult.PrimevalStones = 200;
            clock.ActionPointsRemaining = 30;
            ap.Rank = 5;
            ap.MaxHealth = 100f;
            ap.CurrentHealth = 100f;
            ap.MaxWallDurability = 2000f;
            ap.ApertureWallDurability = 2000f;
            ap.CurrentEssenceSea = 40f;

            // Sealed crossing protects health completely
            Assert.True(TransitSystem.TravelToRegion(world, WorldRegionID.NorthernPlains, sealAperture: true));
            Assert.Equal(WorldRegionID.NorthernPlains, world.GetCurrentSector()!.Region);
            Assert.Equal(100f, ap.CurrentHealth);
            Assert.Equal(0f, ap.CurrentEssenceSea);

            // Rank 5 brute crossing inflicts -50% health
            cult.PrimevalStones = 200;
            clock.ActionPointsRemaining = 30;
            Assert.True(TransitSystem.TravelToRegion(world, WorldRegionID.CentralContinent, sealAperture: false));
            Assert.Equal(WorldRegionID.CentralContinent, world.GetCurrentSector()!.Region);
            Assert.Equal(50f, ap.CurrentHealth);
        }
    }
}
