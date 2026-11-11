using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class LandmarkRegistryTests
    {
        [Fact]
        public void DefaultRegistryContainsStandardLandmarks()
        {
            var spiritDef = LandmarkRegistry.GetByPOI(TilePOIType.SpiritVein);
            Assert.NotNull(spiritDef);
            Assert.Equal("spirit_vein", spiritDef!.Id);
            Assert.Equal("泉", spiritDef.SealText);
            Assert.Equal(LandmarkCategory.Interactive, spiritDef.Category);
            Assert.True(spiritDef.IsTraversable);

            var wayDef = LandmarkRegistry.GetByPOI(TilePOIType.WayStation);
            Assert.NotNull(wayDef);
            Assert.Equal("way_station", wayDef!.Id);
            Assert.Equal("商", wayDef.SealText);
            Assert.Equal(LandmarkCategory.Interactive, wayDef.Category);

            var clanDef = LandmarkRegistry.GetByPOI(TilePOIType.ClanPost);
            Assert.NotNull(clanDef);
            Assert.Equal("clan_post", clanDef!.Id);
            Assert.Equal("寨", clanDef.SealText);
            Assert.Equal(LandmarkCategory.Interactive, clanDef.Category);

            var pineDef = LandmarkRegistry.GetByPOI(TilePOIType.AncientPine);
            Assert.NotNull(pineDef);
            Assert.Equal("松", pineDef!.SealText);
            Assert.Equal(LandmarkCategory.Scenic, pineDef.Category);
            Assert.True(pineDef.IsTraversable);

            var boulderDef = LandmarkRegistry.GetByPOI(TilePOIType.BoulderPrecipice);
            Assert.NotNull(boulderDef);
            Assert.Equal("障", boulderDef!.SealText);
            Assert.Equal(LandmarkCategory.Obstacle, boulderDef.Category);
            Assert.False(boulderDef.IsTraversable);
        }

        [Fact]
        public void FunctionalActionExecution_MutatesWorldStoreDirectly()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;
            var inv = world.Get<InventoryComponent>(world.PlayerEntity)!;
            var clock = world.GetSingleton<WorldClockComponent>()!;

            // 1. Spirit spring absorption
            var spiritDef = LandmarkRegistry.GetByPOI(TilePOIType.SpiritVein)!;
            ap.CurrentEssenceSea = 20.0f;
            spiritDef.ExecuteAction!(world, "spirit_vein_absorb", new GridCoord(14, 17));
            Assert.Equal(35.0f, ap.CurrentEssenceSea);

            // 2. Clan post rest action
            var clanDef = LandmarkRegistry.GetByPOI(TilePOIType.ClanPost)!;
            clock.ActionPointsRemaining = 10;
            clanDef.ExecuteAction!(world, "clan_post_rest", new GridCoord(15, 15));
            Assert.Equal(15, clock.ActionPointsRemaining);

            // 3. Way station purchases
            var wayDef = LandmarkRegistry.GetByPOI(TilePOIType.WayStation)!;
            cult.PrimevalStones = 100;
            int initialPetals = inv.GetFoodQuantity(GuDietType.MoonOrchidPetals);
            wayDef.ExecuteAction!(world, "waystation_buy_orchid", new GridCoord(19, 15));
            Assert.Equal(90, cult.PrimevalStones);
            Assert.Equal(initialPetals + 5, inv.GetFoodQuantity(GuDietType.MoonOrchidPetals));
        }

        [Fact]
        public void ExplorationSystem_InspectTile_RoutesScenicVsInteractive()
        {
            WorldSignals.ClearAllSubscriptions();
            var world = WorldFactory.CreateDefault();
            var sector = world.GetCurrentSector()!;
            var tiles = sector.EnsureHydrated();

            bool scenicFired = false;
            string inspectedTitle = "";
            string inspectedSeal = "";
            WorldSignals.OnScenicToastRequested += (title, seal, lore, pos) =>
            {
                scenicFired = true;
                inspectedTitle = title;
                inspectedSeal = seal;
            };

            bool poiModalFired = false;
            GridCoord openedCoord = default;
            WorldSignals.OnOpenTilePOIRequested += pos =>
            {
                poiModalFired = true;
                openedCoord = pos;
            };

            try
            {
                tiles[10 * sector.GridWidth + 10].POI = TilePOIType.AncientPine;
                ExplorationSystem.InspectTile(world, new GridCoord(10, 10));

                Assert.True(scenicFired);
                Assert.Equal("松", inspectedSeal);
                Assert.Contains("Thousand-Year Withered Pine", inspectedTitle);
                Assert.False(poiModalFired);

                tiles[12 * sector.GridWidth + 12].POI = TilePOIType.SpiritVein;
                ExplorationSystem.InspectTile(world, new GridCoord(12, 12));

                Assert.True(poiModalFired);
                Assert.Equal(new GridCoord(12, 12), openedCoord);
            }
            finally
            {
                WorldSignals.ClearAllSubscriptions();
            }
        }
    }
}
