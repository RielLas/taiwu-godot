using System.IO;
using Newtonsoft.Json;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Tests
{
    public class EntityStoreSerializationTests
    {
        public class CultivatorProfile
        {
            public string Name { get; set; } = "";
            public int Rank { get; set; } = 1;
        }

        [Fact]
        public void EntityStore_FullJsonRoundtrip_RebuildsSpatialIndexAndData()
        {
            var store = new EntityStore();
            var e1 = store.CreateEntity();
            var e2 = store.CreateEntity();

            store.Set(e1, new PositionComponent(12, 18));
            store.Set(e1, new CultivatorProfile { Name = "Fang Yuan", Rank = 3 });

            store.Set(e2, new PositionComponent(12, 18));
            store.Set(e2, new CultivatorProfile { Name = "Bai Ning Bing", Rank = 3 });

            // Serialize with TypeNameHandling so polymorphic objects survive
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented
            };

            string json = JsonConvert.SerializeObject(store, settings);

            // Deserialize into brand new instance
            var restored = JsonConvert.DeserializeObject<EntityStore>(json, settings);
            Assert.NotNull(restored);

            // Rebuild non-serialized spatial index
            restored!.RebuildSpatialIndex();

            Assert.True(restored.IsAlive(e1));
            Assert.True(restored.IsAlive(e2));

            // Test Spatial lookup
            var atTile = restored.GetEntitiesAt(new GridCoord(12, 18)).ToList();
            Assert.Equal(2, atTile.Count);

            var p1 = restored.Get<CultivatorProfile>(e1);
            var p2 = restored.Get<CultivatorProfile>(e2);

            Assert.NotNull(p1);
            Assert.NotNull(p2);
            Assert.Equal("Fang Yuan", p1!.Name);
            Assert.Equal(3, p1.Rank);
            Assert.Equal("Bai Ning Bing", p2!.Name);
        }
    }
}
