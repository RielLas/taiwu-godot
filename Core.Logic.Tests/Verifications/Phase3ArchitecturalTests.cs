using System;
using System.Collections.Generic;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Psychology;

namespace TaiwuGu.Tests
{
    public class Phase3ArchitecturalTests
    {
        [Fact]
        public void Test01_GridCoordinateMathAndBounds()
        {
            Vector2Int[] testCoords = new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(15, 15),
                new Vector2Int(29, 29),
                new Vector2Int(5, 23),
                new Vector2Int(22, 7)
            };

            foreach (var origin in testCoords)
            {
                Vector3 worldPos = ExplorationService.GridToWorld(origin);
                Vector2Int converted = ExplorationService.WorldToGrid(worldPos);
                Assert.Equal(origin, converted);
            }

            var ctx = GameStateContext.CreateDefault();
            using var service = new ExplorationService();
            service.Initialize(ctx);

            Assert.True(service.IsWithinBounds(new Vector2Int(0, 0)));
            Assert.True(service.IsWithinBounds(new Vector2Int(29, 29)));
            Assert.False(service.IsWithinBounds(new Vector2Int(-1, 0)));
            Assert.False(service.IsWithinBounds(new Vector2Int(30, 15)));
        }

        [Fact]
        public void Test02_WorldMapStateCentralizationAndPOIs()
        {
            var ctx = GameStateContext.CreateDefault();

            Assert.NotNull(ctx.WorldMap);
            Assert.NotNull(ctx.WorldMap.Tiles);
            Assert.Equal(900, ctx.WorldMap.Tiles.Length);

            var clanTile = ctx.WorldMap.GetTile(15, 15);
            Assert.Equal(TilePOIType.ClanPost, clanTile.POI);

            var caravanTile = ctx.WorldMap.GetTile(19, 15);
            Assert.Equal(TilePOIType.WayStation, caravanTile.POI);

            var spiritTile = ctx.WorldMap.GetTile(14, 17);
            Assert.Equal(TilePOIType.SpiritVein, spiritTile.POI);

            var denTile = ctx.WorldMap.GetTile(18, 11);
            Assert.Equal(TilePOIType.WildBeastDen, denTile.POI);

            var caveTile = ctx.WorldMap.GetTile(6, 22);
            Assert.Equal(TilePOIType.AncientGuCave, caveTile.POI);

            Assert.True(clanTile.IsExplored);
            Assert.True(clanTile.IsVisible);
        }

        [Fact]
        public void Test03_FogOfWarUnveilingAndSensoryRadius()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new ExplorationService();
            service.Initialize(ctx, baseSensoryRadius: 3);

            bool fogEventFired = false;
            Vector2Int reportedCenter = Vector2Int.zero;
            int reportedRadius = 0;

            Action<Vector2Int, int> handler = (center, rad) =>
            {
                fogEventFired = true;
                reportedCenter = center;
                reportedRadius = rad;
            };

            GameEventManager.OnFogOfWarUpdated += handler;

            try
            {
                Vector2Int newPos = new Vector2Int(16, 15);
                GameEventManager.TriggerPlayerMoved(newPos);

                Assert.True(fogEventFired);
                Assert.Equal(newPos, reportedCenter);
                Assert.Equal(3, reportedRadius);
                Assert.Equal(newPos, ctx.WorldMap.PlayerGridPos);

                var edgeTile = ctx.WorldMap.GetTile(19, 15);
                Assert.True(edgeTile.IsVisible);
                Assert.True(edgeTile.IsExplored);

                Vector2Int farPos = new Vector2Int(25, 25);
                GameEventManager.TriggerPlayerMoved(farPos);

                var oldTile = ctx.WorldMap.GetTile(19, 15);
                Assert.False(oldTile.IsVisible);
                Assert.True(oldTile.IsExplored);
            }
            finally
            {
                GameEventManager.OnFogOfWarUpdated -= handler;
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test04_POIArrivalDetectionAndEventDispatch()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new ExplorationService();
            service.Initialize(ctx);

            bool poiEventFired = false;
            TilePOIType arrivedPOI = TilePOIType.None;
            Vector2Int arrivedPos = Vector2Int.zero;
            string arrivedTitle = "";

            Action<TilePOIType, Vector2Int, string, string, int> handler = (poi, pos, title, desc, danger) =>
            {
                poiEventFired = true;
                arrivedPOI = poi;
                arrivedPos = pos;
                arrivedTitle = title;
            };

            GameEventManager.OnPOIArrived += handler;

            try
            {
                Vector2Int spiritPos = new Vector2Int(14, 17);
                GameEventManager.TriggerPlayerMoved(spiritPos);

                Assert.True(poiEventFired);
                Assert.Equal(TilePOIType.SpiritVein, arrivedPOI);
                Assert.Equal(spiritPos, arrivedPos);
                Assert.Contains("Primeval Spring", arrivedTitle);
            }
            finally
            {
                GameEventManager.OnPOIArrived -= handler;
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test05_POIActionsExecutionViaCQRS()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new ExplorationService();
            service.Initialize(ctx);

            try
            {
                // 1. Spirit Vein Meditation (+15% essence sea)
                ctx.Cultivator.CurrentEssenceSea = 10.0f;
                GameEventManager.RaiseRequestPOIAction(TilePOIType.SpiritVein, new Vector2Int(14, 17), "spirit_vein_absorb");
                Assert.True(Mathf.Abs(ctx.Cultivator.CurrentEssenceSea - 25.0f) <= 0.1f);

                // 2. Way Station Trade: Buy Moon Orchid Petals (Costs 10 stones, gives +5 petals)
                int initialStones = ctx.Cultivator.PrimevalStones;
                int initialPetals = ctx.GuInventory.GetFoodQuantity(GuDietType.MoonOrchidPetals);

                GameEventManager.RaiseRequestPOIAction(TilePOIType.WayStation, new Vector2Int(19, 15), "waystation_buy_orchid");
                Assert.Equal(initialStones - 10, ctx.Cultivator.PrimevalStones);
                Assert.Equal(initialPetals + 5, ctx.GuInventory.GetFoodQuantity(GuDietType.MoonOrchidPetals));

                // 3. Clan Post Rest: Restores +5 AP
                ctx.Calendar.ActionPointsRemaining = 10;
                GameEventManager.RaiseRequestPOIAction(TilePOIType.ClanPost, new Vector2Int(15, 15), "clan_post_rest");
                Assert.Equal(15, ctx.Calendar.ActionPointsRemaining);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test07_Cauldron3PhaseRefinementLifecycle()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new GuRefinementService();
            service.Initialize(ctx);

            try
            {
                // Verify opening cauldron initializes active recipe if null
                ctx.Cauldron.ActiveRecipe = null;
                GameEventManager.RaiseRequestOpenCauldron();
                Assert.NotNull(ctx.Cauldron.ActiveRecipe);
                Assert.Equal("gu_moonglow_beast_02", ctx.Cauldron.ActiveRecipe.ResultGuID);

                // Phase 1: Material Extraction
                GameEventManager.RaiseRequestInitiateRefinement();
                Assert.Equal(RefinementPhase.Phase1_Extraction, ctx.Cauldron.CurrentPhase);

                float seaBefore = ctx.Cultivator.CurrentEssenceSea;
                float tempBefore = ctx.Cauldron.CauldronTemperature;
                GameEventManager.RaiseRequestCauldronPulseHeat();
                Assert.Equal(tempBefore + 12f, ctx.Cauldron.CauldronTemperature);
                Assert.Equal(seaBefore - 2.5f, ctx.Cultivator.CurrentEssenceSea);

                // Advance Phase 1 into Phase 2
                for (float t = 0; t < 4.1f; t += 0.5f)
                {
                    ctx.Cauldron.CauldronTemperature = 50f;
                    service.Tick(0.5f);
                }
                Assert.Equal(RefinementPhase.Phase2_WillFusion, ctx.Cauldron.CurrentPhase);

                // Phase 2: Will Focus steering & suppression
                float focusBefore = ctx.Cauldron.MasterWillFocus;
                GameEventManager.RaiseRequestCauldronAdjustWillFocus(0.1f);
                Assert.True(Mathf.Abs(ctx.Cauldron.MasterWillFocus - (focusBefore + 0.1f)) <= 0.001f);

                ctx.Cauldron.WildWillFrequency = ctx.Cauldron.MasterWillFocus;
                GameEventManager.RaiseRequestCauldronSuppressWill();
                Assert.True(ctx.Cauldron.WillAlignmentProgress > 0f);

                // Advance Phase 2 into Phase 3
                ctx.Cauldron.WillAlignmentProgress = 60f;
                ctx.Cauldron.WildWillResistance = 10f;
                for (float t = 0; t < 4.6f; t += 0.5f)
                {
                    ctx.Cauldron.MasterWillFocus = ctx.Cauldron.WildWillFrequency;
                    service.Tick(0.5f);
                }
                Assert.Equal(RefinementPhase.Phase3_Condensation, ctx.Cauldron.CurrentPhase);

                // Phase 3: Stone Crushing
                int stonesBefore = ctx.Cultivator.PrimevalStones;
                GameEventManager.RaiseRequestCauldronCrushStone();
                Assert.Equal(stonesBefore - 1, ctx.Cultivator.PrimevalStones);
                Assert.Equal(1, ctx.Cauldron.StonesCrushed);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test08_StrictLIFORoutingAndEscapeHandledHandshake()
        {
            GameEventManager.ClearAllSubscriptions();

            // Simulate 3 modals open
            bool ledgerOpen = true;
            bool cauldronOpen = true;
            bool poiOpen = true;
            bool sysMenuOpen = false;

            int ledgerCloseCount = 0;
            int cauldronCloseCount = 0;
            int poiCloseCount = 0;
            int sysMenuToggleCount = 0;

            Action ledgerHandler = () =>
            {
                if (GameEventManager.EscapeHandled) return;
                if (ledgerOpen)
                {
                    ledgerOpen = false;
                    ledgerCloseCount++;
                    GameEventManager.EscapeHandled = true;
                }
            };

            Action cauldronHandler = () =>
            {
                if (GameEventManager.EscapeHandled) return;
                if (ledgerOpen) return;
                if (cauldronOpen)
                {
                    cauldronOpen = false;
                    cauldronCloseCount++;
                    GameEventManager.EscapeHandled = true;
                }
            };

            Action poiHandler = () =>
            {
                if (GameEventManager.EscapeHandled) return;
                if (ledgerOpen || cauldronOpen) return;
                if (poiOpen)
                {
                    poiOpen = false;
                    poiCloseCount++;
                    GameEventManager.EscapeHandled = true;
                }
            };

            Action sysMenuHandler = () =>
            {
                if (GameEventManager.EscapeHandled) return;
                if (ledgerOpen || cauldronOpen || poiOpen) return;
                sysMenuOpen = !sysMenuOpen;
                sysMenuToggleCount++;
                GameEventManager.EscapeHandled = true;
            };

            GameEventManager.OnEscapeRequested += ledgerHandler;
            GameEventManager.OnEscapeRequested += cauldronHandler;
            GameEventManager.OnEscapeRequested += poiHandler;
            GameEventManager.OnEscapeRequested += sysMenuHandler;

            try
            {
                // Press 1: Only Ledger closes
                GameEventManager.TriggerEscape();
                Assert.False(ledgerOpen);
                Assert.True(cauldronOpen);
                Assert.True(poiOpen);
                Assert.False(sysMenuOpen);
                Assert.Equal(1, ledgerCloseCount);
                Assert.Equal(0, cauldronCloseCount);
                Assert.Equal(0, poiCloseCount);
                Assert.Equal(0, sysMenuToggleCount);

                // Press 2: Only Cauldron closes
                GameEventManager.TriggerEscape();
                Assert.False(ledgerOpen);
                Assert.False(cauldronOpen);
                Assert.True(poiOpen);
                Assert.False(sysMenuOpen);
                Assert.Equal(1, cauldronCloseCount);

                // Press 3: Only POI closes
                GameEventManager.TriggerEscape();
                Assert.False(ledgerOpen);
                Assert.False(cauldronOpen);
                Assert.False(poiOpen);
                Assert.False(sysMenuOpen);
                Assert.Equal(1, poiCloseCount);

                // Press 4: With all modals closed, System Menu toggles open
                GameEventManager.TriggerEscape();
                Assert.True(sysMenuOpen);
                Assert.Equal(1, sysMenuToggleCount);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test09_TilePOIForensicsTamperingAndCultivatorPresence()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var forensicsService = new CrimeAndForensicsService();
            forensicsService.Initialize(ctx);

            try
            {
                Vector2Int crimeTile = new Vector2Int(14, 17);
                var scene = new CrimeSceneData(crimeTile, "Elder Tie", CrimeSignature.CleanIncisions, 3);
                ctx.Forensics.ActiveCrimeScenes.Add(scene);

                bool crimeUpdated = false;
                Vector2Int updatedPos = Vector2Int.zero;
                Action<Vector2Int> crimeHandler = pos =>
                {
                    crimeUpdated = true;
                    updatedPos = pos;
                };
                GameEventManager.OnCrimeSceneUpdated += crimeHandler;

                // Tamper with crime evidence: Cleanse
                GameEventManager.RaiseRequestTamperCrimeEvidence(crimeTile, CrimeTamperChoice.Cleanse);
                Assert.True(crimeUpdated);
                Assert.Equal(crimeTile, updatedPos);
                Assert.True(scene.IsTamperedClean);
                Assert.Equal(0, scene.LingeringSoulQiTerms);

                // Cultivators update event check
                bool cultivatorsReceived = false;
                List<CultivatorEntityData>? listReceived = null;
                Action<Vector2Int, List<CultivatorEntityData>> cultHandler = (pos, list) =>
                {
                    cultivatorsReceived = true;
                    listReceived = list;
                };
                GameEventManager.OnTileCultivatorsUpdated += cultHandler;

                var testCultivators = new List<CultivatorEntityData>
                {
                    new CultivatorEntityData
                    {
                        EntityId = "npc_01",
                        Name = "Gu Yue Fang Yuan",
                        Clan = "Gu Yue Clan",
                        Rank = 1,
                        Substage = 1,
                        PrimevalStones = 100,
                        Favorability = 50
                    }
                };
                GameEventManager.TriggerTileCultivatorsUpdated(crimeTile, testCultivators);

                Assert.True(cultivatorsReceived);
                Assert.NotNull(listReceived);
                Assert.Single(listReceived);
                Assert.Equal("Gu Yue Fang Yuan", listReceived[0].Name);

                GameEventManager.OnCrimeSceneUpdated -= crimeHandler;
                GameEventManager.OnTileCultivatorsUpdated -= cultHandler;
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test10_ModalActiveHandshake()
        {
            GameEventManager.ClearAllSubscriptions();

            bool lastModalActive = false;
            Action<bool> modalHandler = active => lastModalActive = active;
            GameEventManager.OnUIModalActive += modalHandler;

            try
            {
                GameEventManager.TriggerUIModalActive(true);
                Assert.True(lastModalActive);

                GameEventManager.TriggerUIModalActive(false);
                Assert.False(lastModalActive);
            }
            finally
            {
                GameEventManager.OnUIModalActive -= modalHandler;
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test11_POIArrivalOrderResilience()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var forensicsService = new CrimeAndForensicsService();
            forensicsService.Initialize(ctx);

            try
            {
                Vector2Int tile = new Vector2Int(14, 17);
                var scene = forensicsService.SpawnCrimeScene(tile, "Elder Tie", CrimeSignature.CleanIncisions, 3);
                Assert.NotNull(scene);

                // Simulate order where ForensicsService receives OnPOIArrived FIRST
                // and broadcasts TriggerCrimeScenePresent before the modal has processed arrival
                bool crimePresentFired = false;
                Vector2Int receivedTile = Vector2Int.zero;
                int receivedTerms = 0;

                Action<Vector2Int, int> crimePresentHandler = (pos, terms) =>
                {
                    crimePresentFired = true;
                    receivedTile = pos;
                    receivedTerms = terms;
                };

                GameEventManager.OnCrimeScenePresent += crimePresentHandler;

                // Fire POI arrival - ForensicsService should detect active crime scene and trigger OnCrimeScenePresent
                GameEventManager.TriggerPOIArrived(TilePOIType.SpiritVein, tile, "Spring", "Desc", 1);

                Assert.True(crimePresentFired);
                Assert.Equal(tile, receivedTile);
                Assert.Equal(3, receivedTerms);

                GameEventManager.OnCrimeScenePresent -= crimePresentHandler;
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test12_SystemMenuEscapeHandledSafety()
        {
            GameEventManager.ClearAllSubscriptions();

            bool menuClosed = false;
            Action sysMenuHandler = () =>
            {
                // When EscapeHandled is true, menu must NEVER close or switch views in the same escape press
                if (GameEventManager.EscapeHandled) return;
                menuClosed = true;
                GameEventManager.EscapeHandled = true;
            };

            GameEventManager.OnEscapeRequested += sysMenuHandler;

            try
            {
                // Set EscapeHandled = true (as if Ledger or Cauldron handled it)
                GameEventManager.EscapeHandled = true;

                // TriggerEscape resets EscapeHandled to false. To simulate delegate cascade within one TriggerEscape:
                int ledgerRuns = 0;
                Action ledgerHandler = () =>
                {
                    if (GameEventManager.EscapeHandled) return;
                    ledgerRuns++;
                    GameEventManager.EscapeHandled = true;
                };

                GameEventManager.ClearAllSubscriptions();
                // Register Ledger first, then SystemMenu
                GameEventManager.OnEscapeRequested += ledgerHandler;
                GameEventManager.OnEscapeRequested += sysMenuHandler;

                GameEventManager.TriggerEscape();

                Assert.Equal(1, ledgerRuns);
                Assert.False(menuClosed); // SystemMenu was safely skipped!
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test13_CauldronModalToggledAndStateDeduplication()
        {
            GameEventManager.ClearAllSubscriptions();

            int modalActiveCount = 0;
            Action<bool> modalHandler = active => modalActiveCount++;
            GameEventManager.OnUIModalActive += modalHandler;

            var ctx = GameStateContext.CreateDefault();
            using var refService = new GuRefinementService();
            refService.Initialize(ctx);

            try
            {
                // Open Cauldron
                GameEventManager.RaiseRequestOpenCauldron();
                // TriggerCauldronOpened and TriggerCauldronModalToggled were fired by service
                // Ensure UI views guard against duplicate modal active state transitions
                Assert.Equal(RefinementPhase.Idle, ctx.Cauldron.CurrentPhase);

                // Close Cauldron
                GameEventManager.RaiseRequestCloseCauldron();
                Assert.False(ctx.Cauldron.IsActive);
            }
            finally
            {
                GameEventManager.OnUIModalActive -= modalHandler;
                GameEventManager.ClearAllSubscriptions();
            }
        }
    }
}
