using System;
using System.Collections.Generic;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Map;

namespace TaiwuGu.Tests
{
    public class LandmarkRegistryTests
    {
        [Fact]
        public void Test01_DefaultRegistryContainsStandardLandmarks()
        {
            var spiritDef = LandmarkRegistry.GetByPOI(TilePOIType.SpiritVein);
            Assert.NotNull(spiritDef);
            Assert.Equal("spirit_vein", spiritDef.Id);
            Assert.Equal("泉", spiritDef.SealText);
            Assert.Equal(LandmarkCategory.Interactive, spiritDef.Category);
            Assert.True(spiritDef.IsTraversable);

            var wayDef = LandmarkRegistry.GetByPOI(TilePOIType.WayStation);
            Assert.NotNull(wayDef);
            Assert.Equal("way_station", wayDef.Id);
            Assert.Equal("商", wayDef.SealText);
            Assert.Equal(LandmarkCategory.Interactive, wayDef.Category);

            var clanDef = LandmarkRegistry.GetByPOI(TilePOIType.ClanPost);
            Assert.NotNull(clanDef);
            Assert.Equal("clan_post", clanDef.Id);
            Assert.Equal("寨", clanDef.SealText);
            Assert.Equal(LandmarkCategory.Interactive, clanDef.Category);

            var pineDef = LandmarkRegistry.GetByPOI(TilePOIType.AncientPine);
            Assert.NotNull(pineDef);
            Assert.Equal("ancient_pine", pineDef.Id);
            Assert.Equal("松", pineDef.SealText);
            Assert.Equal(LandmarkCategory.Scenic, pineDef.Category);
            Assert.True(pineDef.IsTraversable);

            var steleDef = LandmarkRegistry.GetByPOI(TilePOIType.ShatteredStele);
            Assert.NotNull(steleDef);
            Assert.Equal("shattered_stele", steleDef.Id);
            Assert.Equal("碑", steleDef.SealText);
            Assert.Equal(LandmarkCategory.Scenic, steleDef.Category);
            Assert.True(steleDef.IsTraversable);

            var boulderDef = LandmarkRegistry.GetByPOI(TilePOIType.BoulderPrecipice);
            Assert.NotNull(boulderDef);
            Assert.Equal("boulder_precipice", boulderDef.Id);
            Assert.Equal("障", boulderDef.SealText);
            Assert.Equal(LandmarkCategory.Obstacle, boulderDef.Category);
            Assert.False(boulderDef.IsTraversable);
        }

        [Fact]
        public void Test02_FunctionalActionRecallExecution()
        {
            var ctx = GameStateContext.CreateDefault();
            var spiritDef = LandmarkRegistry.GetByPOI(TilePOIType.SpiritVein);
            Assert.NotNull(spiritDef);
            Assert.NotNull(spiritDef.ExecuteAction);

            // 1. Spirit spring absorption
            ctx.Cultivator.CurrentEssenceSea = 20.0f;
            spiritDef.ExecuteAction(ctx, "spirit_vein_absorb", new Vector2Int(14, 17));
            Assert.Equal(35.0f, ctx.Cultivator.CurrentEssenceSea);

            // 2. Clan post rest action
            var clanDef = LandmarkRegistry.GetByPOI(TilePOIType.ClanPost);
            Assert.NotNull(clanDef);
            Assert.NotNull(clanDef.ExecuteAction);

            ctx.Calendar.ActionPointsRemaining = 10;
            clanDef.ExecuteAction(ctx, "clan_post_rest", new Vector2Int(15, 15));
            Assert.Equal(15, ctx.Calendar.ActionPointsRemaining);

            // 3. Way station purchases
            var wayDef = LandmarkRegistry.GetByPOI(TilePOIType.WayStation);
            Assert.NotNull(wayDef);
            Assert.NotNull(wayDef.ExecuteAction);

            ctx.Cultivator.PrimevalStones = 100;
            int initialPetals = ctx.GuInventory.GetFoodQuantity(GuDietType.MoonOrchidPetals);
            wayDef.ExecuteAction(ctx, "waystation_buy_orchid", new Vector2Int(19, 15));
            Assert.Equal(90, ctx.Cultivator.PrimevalStones);
            Assert.Equal(initialPetals + 5, ctx.GuInventory.GetFoodQuantity(GuDietType.MoonOrchidPetals));
        }

        [Fact]
        public void Test03_CustomLandmarkDynamicRegistrationAndRecall()
        {
            bool customActionInvoked = false;
            string customActionParam = "";

            var customLandmark = new LandmarkDefinition
            {
                Id = "custom_ancient_cauldron",
                Name = "Remnant Ancient Cauldron (古鼎残迹)",
                SealText = "鼎",
                Category = LandmarkCategory.Interactive,
                LoreDescription = "A colossal bronze alchemical cauldron half-buried in karst soil.",
                IsTraversable = true,
                MovementAPCost = 2,
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("inspect_ancient_cauldron", "Decipher Cauldron Runes", "Study the celestial seal script on the bronze cauldron.")
                },
                ExecuteAction = (context, actionId, pos) =>
                {
                    customActionInvoked = true;
                    customActionParam = actionId;
                }
            };

            LandmarkRegistry.Register(customLandmark);

            var retrieved = LandmarkRegistry.Get("custom_ancient_cauldron");
            Assert.NotNull(retrieved);
            Assert.Equal("鼎", retrieved.SealText);
            Assert.Equal(LandmarkCategory.Interactive, retrieved.Category);

            var ctx = GameStateContext.CreateDefault();
            retrieved.ExecuteAction!(ctx, "inspect_ancient_cauldron", new Vector2Int(10, 10));

            Assert.True(customActionInvoked);
            Assert.Equal("inspect_ancient_cauldron", customActionParam);
        }

        [Fact]
        public void Test04_ExplorationServiceDirectInspectAndScenicDispatches()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new ExplorationService();
            service.Initialize(ctx);

            bool scenicFired = false;
            string inspectedTitle = "";
            string inspectedSeal = "";

            GameEventManager.OnTileInspected += (title, seal, lore, pos) =>
            {
                scenicFired = true;
                inspectedTitle = title;
                inspectedSeal = seal;
            };

            bool poiModalFired = false;
            TilePOIType arrivedPOI = TilePOIType.None;

            GameEventManager.OnPOIArrived += (poi, pos, title, desc, danger) =>
            {
                poiModalFired = true;
                arrivedPOI = poi;
            };

            try
            {
                // Set tile (10, 10) as Scenic Thousand-Year Pine
                ctx.WorldMap.Tiles[10 * 30 + 10].POI = TilePOIType.AncientPine;

                // Inspecting the pine tile must trigger OnTileInspected (scenic flavor toast) and NOT OnPOIArrived!
                GameEventManager.RaiseRequestInspectTile(new Vector2Int(10, 10));

                Assert.True(scenicFired);
                Assert.Equal("松", inspectedSeal);
                Assert.Contains("Thousand-Year Withered Pine", inspectedTitle);
                Assert.False(poiModalFired);

                // Set tile (12, 12) as Interactive Spirit Spring
                ctx.WorldMap.Tiles[12 * 30 + 12].POI = TilePOIType.SpiritVein;

                // Inspecting the spring tile must trigger interactive arrival
                GameEventManager.RaiseRequestInspectTile(new Vector2Int(12, 12));

                Assert.True(poiModalFired);
                Assert.Equal(TilePOIType.SpiritVein, arrivedPOI);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test05_ObstacleImpassableTerrainConstraint()
        {
            Assert.False(LandmarkRegistry.IsTraversable(TilePOIType.BoulderPrecipice));
            Assert.True(LandmarkRegistry.IsTraversable(TilePOIType.AncientPine));
            Assert.True(LandmarkRegistry.IsTraversable(TilePOIType.SpiritVein));
            Assert.True(LandmarkRegistry.IsTraversable(TilePOIType.WayStation));
        }
    }
}
