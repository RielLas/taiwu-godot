using System;
using System.Linq;
using Newtonsoft.Json;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Tests
{
    public class EntityStoreSerializationTests
    {
        [Fact]
        public void WorldStore_FullJsonRoundtrip_RebuildsSpatialOwnerAndFactionIndices()
        {
            var store = WorldFactory.CreateDefault();
            var e1 = store.CreateEntity("extra_01");
            store.Set(e1, new PositionComponent(12, 18, store.CurrentSectorKey));
            store.Set(e1, new CultivatorComponent { ExternalId = "extra_01", Name = "Custom Cultivator" });

            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new KnownComponentsBinder(),
                Formatting = Formatting.Indented
            };

            string json = JsonConvert.SerializeObject(store, settings);
            var restored = JsonConvert.DeserializeObject<WorldStore>(json, settings);
            Assert.NotNull(restored);

            restored!.RebuildIndices();

            Assert.True(restored.IsAlive(e1));
            var atTile = restored.GetEntitiesAt(new GridCoord(12, 18)).ToList();
            Assert.Contains(e1, atTile);
            Assert.Equal(2, restored.GetOwned<GuWormComponent>(restored.PlayerEntity).Count());
            Assert.NotNull(restored.GetFaction(FactionID.GuYueClan));
        }

        [Fact]
        public void KnownComponentsBinder_RejectsNonDomainTypes()
        {
            var binder = new KnownComponentsBinder();
            Assert.Throws<JsonSerializationException>(() => binder.BindToType("System.Diagnostics", "ProcessStartInfo"));
        }
    }
}
