using System.Linq;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Psychology;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class TileCultivatorInteractionTests
    {
        [Fact]
        public void DirectSpatialQuery_FindsAliveNPCsAtTileDeterministically_AcrossRepeatedInspects()
        {
            WorldSignals.ClearAllSubscriptions();
            var world = WorldFactory.CreateDefault();

            var npcId = world.CreateEntity("npc_elder_01");
            world.Set(npcId, new PositionComponent(15, 15, world.CurrentSectorKey));
            world.Set(npcId, new CultivatorComponent
            {
                ExternalId = "npc_elder_01",
                Name = "Gu Yue Bo (Clan Leader)",
                Clan = "GuYue",
                Disposition = TaiwuDisposition.Righteous
            });
            world.Set(npcId, new ApertureComponent { Rank = 4, Substage = 3 });

            // Repeated direct-read lookups always return the exact same resident NPCs without state caches
            var firstRead = SocialVendettaSystem.GetAliveNPCsAt(world, new GridCoord(15, 15));
            Assert.Contains(firstRead, n => n.Cult.Name == "Gu Yue Bo (Clan Leader)");

            var secondRead = SocialVendettaSystem.GetAliveNPCsAt(world, new GridCoord(15, 15));
            Assert.Contains(secondRead, n => n.Cult.Name == "Gu Yue Bo (Clan Leader)");
            Assert.Equal(firstRead.Count, secondRead.Count);
        }

        [Fact]
        public void DeadOrMovedNPCs_AreImmediatelyReflectedBySpatialQueries()
        {
            var world = WorldFactory.CreateDefault();
            EntityId npcId = world.FindByExternalId("npc_chi_disciple");
            Assert.True(npcId.IsValid);

            var pos = world.Get<PositionComponent>(npcId)!;
            GridCoord originalCoord = pos.Coord;

            Assert.Contains(SocialVendettaSystem.GetAliveNPCsAt(world, originalCoord), n => n.Id == npcId);

            // Moving updates _spatialIndex in O(1)
            world.SetPosition(npcId, new GridCoord(20, 20), world.CurrentSectorKey);
            Assert.DoesNotContain(SocialVendettaSystem.GetAliveNPCsAt(world, originalCoord), n => n.Id == npcId);
            Assert.Contains(SocialVendettaSystem.GetAliveNPCsAt(world, new GridCoord(20, 20)), n => n.Id == npcId);

            // Reaching mortal lifespan removes NPC from GetAliveNPCsAt
            var cult = world.Get<CultivatorComponent>(npcId)!;
            cult.Age = cult.LifespanYears;
            Assert.False(cult.IsAlive);
            Assert.DoesNotContain(SocialVendettaSystem.GetAliveNPCsAt(world, new GridCoord(20, 20)), n => n.Id == npcId);
        }
    }
}
