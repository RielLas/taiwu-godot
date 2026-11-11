using System.Linq;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Tests
{
    public class WorldStoreTests
    {
        private class TestStatsComponent
        {
            public int Health { get; set; } = 100;
            public string Name { get; set; } = "Cultivator";
        }

        [Fact]
        public void WorldStore_CreateAndRetrieveComponents_WorksCorrectly()
        {
            var store = new WorldStore();
            var id = store.CreateEntity("test_ent");

            Assert.True(store.IsAlive(id));
            Assert.False(store.Has<TestStatsComponent>(id));

            store.Set(id, new TestStatsComponent { Health = 80, Name = "Fang Yuan" });
            Assert.True(store.Has<TestStatsComponent>(id));

            var stats = store.Get<TestStatsComponent>(id);
            Assert.NotNull(stats);
            Assert.Equal(80, stats!.Health);
            Assert.Equal("Fang Yuan", stats.Name);
            Assert.Equal(id, store.FindByExternalId("test_ent"));
        }

        [Fact]
        public void WorldStore_SpatialIndexing_InstantLookupAndMovement()
        {
            var store = new WorldStore();
            var e1 = store.CreateEntity();
            var e2 = store.CreateEntity();

            store.Set(e1, new PositionComponent(10, 15, store.CurrentSectorKey));
            store.Set(e2, new PositionComponent(10, 15, store.CurrentSectorKey));

            var atTile = store.GetEntitiesAt(new GridCoord(10, 15)).ToList();
            Assert.Equal(2, atTile.Count);
            Assert.Contains(e1, atTile);
            Assert.Contains(e2, atTile);

            store.SetPosition(e1, new GridCoord(11, 15), store.CurrentSectorKey);

            var tileOld = store.GetEntitiesAt(new GridCoord(10, 15)).ToList();
            var tileNew = store.GetEntitiesAt(new GridCoord(11, 15)).ToList();

            Assert.Single(tileOld);
            Assert.Equal(e2, tileOld[0]);

            Assert.Single(tileNew);
            Assert.Equal(e1, tileNew[0]);
        }

        [Fact]
        public void WorldStore_OwnershipIndex_AndDestroyCleanup()
        {
            var store = new WorldStore();
            var owner = store.CreateEntity("owner");
            var gu = store.CreateEntity("gu");

            store.Set(gu, new PositionComponent(5, 5, store.CurrentSectorKey));
            store.Set(gu, new GuWormComponent { GuID = "gu_moonlight_01", Name = "Moonlight Gu" });
            store.Set(gu, new OwnerComponent(owner.Value, isEquipped: true, equipSlot: 0));

            Assert.Single(store.GetEntitiesAt(new GridCoord(5, 5)));
            Assert.Single(store.GetOwned<GuWormComponent>(owner));

            store.DestroyEntity(gu);

            Assert.False(store.IsAlive(gu));
            Assert.Empty(store.GetEntitiesAt(new GridCoord(5, 5)));
            Assert.Empty(store.GetOwned<GuWormComponent>(owner));
        }

        [Fact]
        public void WorldFactory_CreateDefault_SeedsCanonicalSingleSourceOfTruth()
        {
            var world = WorldFactory.CreateDefault();

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var ap = world.Get<ApertureComponent>(world.PlayerEntity);
            var clock = world.GetSingleton<WorldClockComponent>();
            var sector = world.GetCurrentSector();

            Assert.NotNull(cult);
            Assert.NotNull(ap);
            Assert.NotNull(clock);
            Assert.NotNull(sector);

            Assert.Equal("Gu Yue Fang Yuan", cult!.Name);
            Assert.Equal(AptitudeGrade.C_Grade, cult.Aptitude);
            Assert.Equal(44.0f, cult.AptitudePercent, 2);
            Assert.Equal(1, ap!.Rank);
            Assert.Equal(1, ap.Substage);
            Assert.Equal(250, cult.PrimevalStones);

            Assert.Equal(1, clock!.CurrentYear);
            Assert.Equal(4, clock.CurrentSolarTermIndex);
            Assert.Equal(30, clock.ActionPointsRemaining);

            Assert.Equal(2, world.GetOwned<GuWormComponent>(world.PlayerEntity).Count());
            Assert.Equal(6, world.GetAll<FactionComponent>().Count());
            Assert.True(world.GetAll<CultivatorComponent>().Count() > 1);
            Assert.Equal(900, sector!.EnsureHydrated().Length);
        }
    }
}
