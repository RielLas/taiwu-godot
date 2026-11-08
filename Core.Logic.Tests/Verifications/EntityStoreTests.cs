using System.Linq;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Tests
{
    public class EntityStoreTests
    {
        private class TestStatsComponent
        {
            public int Health { get; set; } = 100;
            public string Name { get; set; } = "Cultivator";
        }

        private class TestClanComponent
        {
            public string ClanName { get; set; } = "Gu Yue";
        }

        [Fact]
        public void EntityStore_CreateAndRetrieveComponents_WorksCorrectly()
        {
            var store = new EntityStore();
            var id = store.CreateEntity();

            Assert.True(store.IsAlive(id));
            Assert.False(store.Has<TestStatsComponent>(id));

            store.Set(id, new TestStatsComponent { Health = 80, Name = "Fang Yuan" });
            Assert.True(store.Has<TestStatsComponent>(id));

            var stats = store.Get<TestStatsComponent>(id);
            Assert.NotNull(stats);
            Assert.Equal(80, stats!.Health);
            Assert.Equal("Fang Yuan", stats.Name);
        }

        [Fact]
        public void EntityStore_SpatialIndexing_InstantLookupAndMovement()
        {
            var store = new EntityStore();
            var e1 = store.CreateEntity();
            var e2 = store.CreateEntity();

            store.Set(e1, new PositionComponent(10, 15));
            store.Set(e2, new PositionComponent(10, 15));

            var atTile = store.GetEntitiesAt(new GridCoord(10, 15)).ToList();
            Assert.Equal(2, atTile.Count);
            Assert.Contains(e1, atTile);
            Assert.Contains(e2, atTile);

            // Move e1
            store.SetPosition(e1, new GridCoord(11, 15));

            var tileOld = store.GetEntitiesAt(new GridCoord(10, 15)).ToList();
            var tileNew = store.GetEntitiesAt(new GridCoord(11, 15)).ToList();

            Assert.Single(tileOld);
            Assert.Equal(e2, tileOld[0]);

            Assert.Single(tileNew);
            Assert.Equal(e1, tileNew[0]);
        }

        [Fact]
        public void EntityStore_DestroyEntity_CleansUpSpatialAndComponents()
        {
            var store = new EntityStore();
            var e = store.CreateEntity();
            store.Set(e, new PositionComponent(5, 5));
            store.Set(e, new TestClanComponent { ClanName = "Bai Clan" });

            Assert.Single(store.GetEntitiesAt(new GridCoord(5, 5)));

            store.DestroyEntity(e);

            Assert.False(store.IsAlive(e));
            Assert.Null(store.Get<TestClanComponent>(e));
            Assert.Empty(store.GetEntitiesAt(new GridCoord(5, 5)));
        }
    }
}
