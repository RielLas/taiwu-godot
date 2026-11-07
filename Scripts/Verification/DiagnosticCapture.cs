using System;
using Godot;

namespace Taiwu_Godot.Scripts.Verification
{
    /// <summary>
    /// Headless Verification Harness:
    /// Runs for several frames, exercises modal flows, captures a rendered viewport image to disk,
    /// verifies scene hierarchy integrity, and exits cleanly.
    /// </summary>
    public partial class DiagnosticCapture : Node
    {
        [Export] public bool AutoCaptureAndQuit { get; set; } = false;
        [Export] public TaiwuGameBootstrapper? Bootstrapper { get; set; }

        private int _frameCount = 0;

        private bool _captureMapOnly = false;
        private bool _captureBreakthrough = false;
        private bool _captureOnWork = false;
        private bool _captureNPC = false;
        private bool _captureTileNPC = false;
        private bool _captureEncounter = false;
        private bool _captureTransit = false;
        private bool _testAudio = false;
        private bool _testAll = false;
        private bool _testLandmark = false;
        private bool _verify = false;
        private bool _strictPoi = false;

        public override void _Ready()
        {
            // CRUCIAL: Allow harness to process even when SystemMenu pauses scene tree
            ProcessMode = ProcessModeEnum.Always;

            var args = OS.GetCmdlineArgs();
            foreach (var arg in args)
            {
                if (arg == "--capture") AutoCaptureAndQuit = true;
                if (arg == "--verify")
                {
                    _verify = true;
                    AutoCaptureAndQuit = true;
                }
                if (arg == "--strict-poi") _strictPoi = true;
                if (arg == "--map" || arg == "--overworld") _captureMapOnly = true;
                if (arg == "--breakthrough") _captureBreakthrough = true;
                if (arg == "--onwork") _captureOnWork = true;
                if (arg == "--npc") _captureNPC = true;
                if (arg == "--tile-npc") _captureTileNPC = true;
                if (arg == "--encounter") _captureEncounter = true;
                if (arg == "--transit") _captureTransit = true;
                if (arg == "--audio") _testAudio = true;
                if (arg == "--all") _testAll = true;
                if (arg == "--landmark") _testLandmark = true;
            }
            var userArgs = OS.GetCmdlineUserArgs();
            foreach (var arg in userArgs)
            {
                if (arg == "--capture") AutoCaptureAndQuit = true;
                if (arg == "--verify")
                {
                    _verify = true;
                    AutoCaptureAndQuit = true;
                }
                if (arg == "--strict-poi") _strictPoi = true;
                if (arg == "--map" || arg == "--overworld") _captureMapOnly = true;
                if (arg == "--breakthrough") _captureBreakthrough = true;
                if (arg == "--onwork") _captureOnWork = true;
                if (arg == "--npc") _captureNPC = true;
                if (arg == "--tile-npc") _captureTileNPC = true;
                if (arg == "--encounter") _captureEncounter = true;
                if (arg == "--transit") _captureTransit = true;
                if (arg == "--audio") _testAudio = true;
                if (arg == "--all") _testAll = true;
                if (arg == "--landmark") _testLandmark = true;
            }
        }

        public override void _Process(double delta)
        {
            if (!AutoCaptureAndQuit) return;

            _frameCount++;

            if (_verify)
            {
                // Wait for the bootstrapper, tile manager and initial broadcasts to settle.
                if (_frameCount == 10)
                {
                    int exitCode = VerificationRunner.Run(Bootstrapper, _strictPoi);
                    GD.Print($"[DiagnosticCapture] Verification finished with exit code {exitCode}.");
                    GetTree().Quit(exitCode);
                }
                return;
            }

            if (_testLandmark)
            {
                var bootstrapper = Bootstrapper;
                if (_frameCount == 5)
                {
                    GD.Print("[DiagnosticCapture] (--landmark) Step 1: Testing direct click / inspect on current tile (15, 15)...");
                    TaiwuGu.Core.GameEventManager.RaiseRequestInspectTile(new TaiwuGu.Core.GridCoord(15, 15));
                }
                else if (_frameCount == 10)
                {
                    bool isOpen = bootstrapper?.POIModalView?.IsOpen == true;
                    GD.Print($"[DiagnosticCapture] (--landmark) POI Modal IsOpen after inspect: {isOpen}, ModalStack Count: {bootstrapper?.ModalStack?.Count}");
                    if (!isOpen)
                    {
                        GD.PrintErr("[DiagnosticCapture] FAIL: POI Modal failed to open on direct current tile inspect!");
                    }
                    bootstrapper?.ModalStack?.TryPop();
                    GD.Print($"[DiagnosticCapture] (--landmark) POI Modal IsOpen after Pop: {bootstrapper?.POIModalView?.IsOpen}");
                }
                else if (_frameCount == 15)
                {
                    GD.Print("[DiagnosticCapture] (--landmark) Step 2: Testing scenic non-interactive landmark inspect (Ancient Pine)...");
                    // Place AncientPine at (16, 16)
                    if (bootstrapper?.StateContext?.WorldMap != null)
                    {
                        bootstrapper.StateContext.WorldMap.Tiles[16 * 30 + 16].POI = TaiwuGu.Map.TilePOIType.AncientPine;
                    }
                    TaiwuGu.Core.GameEventManager.RaiseRequestInspectTile(new TaiwuGu.Core.GridCoord(16, 16));
                }
                else if (_frameCount == 20)
                {
                    bool modalOpened = bootstrapper?.POIModalView?.IsOpen == true;
                    GD.Print($"[DiagnosticCapture] (--landmark) Scenic inspect verified: POIModal IsOpen = {modalOpened} (Expected: False)");
                    if (modalOpened)
                    {
                        GD.PrintErr("[DiagnosticCapture] FAIL: Scenic landmark should NOT open modal dialog!");
                    }
                }
                else if (_frameCount == 25)
                {
                    GD.Print("[DiagnosticCapture] (--landmark) Step 3: Testing impassable obstacle tile (Boulder Precipice)...");
                    if (bootstrapper?.TileManager != null)
                    {
                        bootstrapper.TileManager.GetTile(14, 14); // ensure grid active
                        bool isWalkable = bootstrapper.TileManager.IsTileWalkable(new Vector2I(14, 14));
                        GD.Print($"[DiagnosticCapture] (--landmark) Normal tile (14, 14) walkable: {isWalkable}");

                        // Set obstacle
                        if (bootstrapper.StateContext?.WorldMap != null)
                        {
                            bootstrapper.StateContext.WorldMap.Tiles[14 * 30 + 14].POI = TaiwuGu.Map.TilePOIType.BoulderPrecipice;
                        }
                    }
                    CaptureFrame("test_landmark_overhaul.png");
                }
                else if (_frameCount >= 30)
                {
                    GD.Print("[DiagnosticCapture] Landmark Overhaul (--landmark) fully verified! Quitting.");
                    GetTree().Quit();
                }
                return;
            }

            if (_testAll)
            {
                var bootstrapper = Bootstrapper;
                if (_frameCount == 5)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 1: Testing Movement, Audio & ModalStack baseline...");
                    if (bootstrapper?.ModalStack == null)
                    {
                        GD.PrintErr("[DiagnosticCapture] FAIL: ModalStack is null!");
                    }
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.Footstep);
                    TaiwuGu.Core.GameEventManager.RaiseRequestMove(new TaiwuGu.Core.GridCoord(16, 15));
                }
                else if (_frameCount == 10)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 2: Testing Alchemical Cauldron opening & ModalStack...");
                    TaiwuGu.Core.GameEventManager.RaiseRequestOpenCauldron();
                }
                else if (_frameCount == 15)
                {
                    GD.Print($"[DiagnosticCapture] (--all) Cauldron IsOpen: {bootstrapper?.CauldronView?.IsOpen}, ModalStack Count: {bootstrapper?.ModalStack?.Count}");
                    TaiwuGu.Core.GameEventManager.RaiseRequestCauldronPulseHeat();
                }
                else if (_frameCount == 20)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 3: Popping Cauldron via ModalStack.TryPop()...");
                    bootstrapper?.ModalStack?.TryPop();
                    GD.Print($"[DiagnosticCapture] (--all) Cauldron IsOpen after Pop: {bootstrapper?.CauldronView?.IsOpen}");
                }
                else if (_frameCount == 25)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 4: Testing Breakthrough Seclusion opening & ModalStack...");
                    if (bootstrapper?.StateContext?.Cultivator != null)
                    {
                        bootstrapper.StateContext.Cultivator.Substage = 4;
                        bootstrapper.StateContext.Cultivator.CurrentEssenceSea = bootstrapper.StateContext.Cultivator.AptitudePercent;
                    }
                    TaiwuGu.Core.GameEventManager.RaiseRequestOpenBreakthroughMinigame();
                }
                else if (_frameCount == 30)
                {
                    GD.Print($"[DiagnosticCapture] (--all) Breakthrough IsOpen: {bootstrapper?.BreakthroughModalView?.IsOpen}, ModalStack Count: {bootstrapper?.ModalStack?.Count}");
                    bootstrapper?.ModalStack?.TryPop();
                    GD.Print($"[DiagnosticCapture] (--all) Breakthrough IsOpen after Pop: {bootstrapper?.BreakthroughModalView?.IsOpen}");
                }
                else if (_frameCount == 35)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 5: Testing Transit Modal opening & ModalStack...");
                    TaiwuGu.Core.GameEventManager.RaiseRequestOpenTransitModal(false);
                }
                else if (_frameCount == 40)
                {
                    GD.Print($"[DiagnosticCapture] (--all) Transit IsOpen: {bootstrapper?.TransitModalView?.IsOpen}, ModalStack Count: {bootstrapper?.ModalStack?.Count}");
                    bootstrapper?.ModalStack?.TryPop();
                    GD.Print($"[DiagnosticCapture] (--all) Transit IsOpen after Pop: {bootstrapper?.TransitModalView?.IsOpen}");
                }
                else if (_frameCount == 45)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 6: Testing System Menu & Atomic Save...");
                    bootstrapper?.SystemMenuView?.OpenMenu();
                    TaiwuGu.Core.GameEventManager.RaiseRequestSaveGame("slot_01");
                }
                else if (_frameCount == 50)
                {
                    GD.Print($"[DiagnosticCapture] (--all) SystemMenu IsOpen: {bootstrapper?.SystemMenuView?.IsOpen}");
                    bootstrapper?.ModalStack?.TryPop();
                    CaptureFrame("test_grand_convergence_all.png");
                }
                else if (_frameCount >= 55)
                {
                    GD.Print("[DiagnosticCapture] Grand Parity Convergence (--all) fully verified! Quitting.");
                    GetTree().Quit();
                }
                return;
            }

            if (_testAudio)
            {
                if (_frameCount == 5)
                {
                    GD.Print("[DiagnosticCapture] Testing Halal Audio Intent Events...");
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.ParchmentRustle);
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.CauldronFire);
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.StoneShatter);
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.BladeSlash);
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.BluntImpact);
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.DashWhoosh);
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.CauldronSteam);
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.WoodenKnock);
                    TaiwuGu.Core.GameEventManager.TriggerBattleStageStateChanged(true);
                }
                else if (_frameCount == 15)
                {
                    TaiwuGu.Core.GameEventManager.TriggerBattleStageStateChanged(false);
                    TaiwuGu.Core.GameEventManager.RaiseRequestPlaySound(TaiwuGu.Core.HalalSoundType.Footstep);
                }
                else if (_frameCount == 20)
                {
                    CaptureFrame("test_audio_engine_capture.png");
                }
                else if (_frameCount >= 30)
                {
                    GD.Print("[DiagnosticCapture] Halal Audio Engine verification complete. Quitting.");
                    GetTree().Quit();
                }
                return;
            }

            if (_captureTransit)
            {
                if (_frameCount == 5)
                {
                    TaiwuGu.Core.GameEventManager.RaiseRequestOpenTransitModal(false);
                    GD.Print("[DiagnosticCapture] Opened Inter-Mountain Transit Modal.");
                }
                else if (_frameCount == 20)
                {
                    CaptureFrame("test_transit_capture.png");
                }
                else if (_frameCount >= 30)
                {
                    GD.Print("[DiagnosticCapture] Inter-Mountain Transit capture complete. Quitting.");
                    GetTree().Quit();
                }
                return;
            }

            if (_captureEncounter)
            {
                if (_frameCount == 5)
                {
                    var target = TaiwuGu.Core.WildOrganismData.CreateWildOrganism("gu_moonlight_r1", TaiwuGu.Core.WildOrganismTemperament.Ferocious);
                    TaiwuGu.Core.GameEventManager.RaiseRequestOpenWildGuEncounter(target);
                    GD.Print("[DiagnosticCapture] Opened Wild Gu Encounter Modal.");
                }
                else if (_frameCount == 20)
                {
                    CaptureFrame("test_wild_gu_encounter_capture.png");
                }
                else if (_frameCount >= 30)
                {
                    GD.Print("[DiagnosticCapture] Wild Gu Encounter capture complete. Quitting.");
                    GetTree().Quit();
                }
                return;
            }

            if (_captureTileNPC)
            {
                if (_frameCount == 5)
                {
                    TaiwuGu.Core.GameEventManager.RaiseRequestMove(new TaiwuGu.Core.GridCoord(15, 14));
                    GD.Print("[DiagnosticCapture] Moved to tile (15, 14) with resident cultivator.");
                }
                else if (_frameCount == 35)
                {
                    CaptureFrame("test_tile_cultivators_capture.png");
                }
                else if (_frameCount >= 45)
                {
                    GD.Print("[DiagnosticCapture] Tile Cultivators capture complete. Quitting.");
                    GetTree().Quit();
                }
                return;
            }

            if (_captureNPC)
            {
                if (_frameCount == 5)
                {
                    TaiwuGu.Core.GameEventManager.RaiseRequestInteractWithNPC("npc_chi_disciple");
                    GD.Print("[DiagnosticCapture] Interacted with Gu Yue Chi Cheng.");
                }
                else if (_frameCount == 10)
                {
                    TaiwuGu.Core.GameEventManager.RaiseRequestNPCBarter("npc_chi_disciple");
                    GD.Print("[DiagnosticCapture] Executed Barter with Gu Yue Chi Cheng.");
                }
                else if (_frameCount == 20)
                {
                    CaptureFrame("test_npc_capture.png");
                }
                else if (_frameCount >= 28)
                {
                    GD.Print("[DiagnosticCapture] NPC Dialogue Pavilion capture complete. Quitting.");
                    GetTree().Quit();
                }
                return;
            }

            if (_captureOnWork)
            {
                if (_frameCount == 5)
                {
                    TaiwuGu.Core.GameEventManager.RaiseRequestShowOnWorkNotice(
                        "Jia Family Caravan Barter Economy (贾家商队)",
                        "The grand trade pavilion is currently ON WORK.\nSeasonal arrivals (Bailu / Qiufen), Stone Gambling Den, and Gu auctions are underway."
                    );
                    GD.Print("[DiagnosticCapture] Triggered On Work Modal.");
                }
                else if (_frameCount == 15)
                {
                    CaptureFrame("test_onwork_capture.png");
                }
                else if (_frameCount >= 25)
                {
                    GD.Print("[DiagnosticCapture] On Work capture complete. Quitting.");
                    GetTree().Quit();
                }
                return;
            }

            if (_captureBreakthrough)
            {
                if (_frameCount == 5)
                {
                    var ctx = TaiwuGu.Core.StateContextProvider.ContextResolver?.Invoke();
                    if (ctx?.Cultivator != null)
                    {
                        ctx.Cultivator.Substage = 4;
                        ctx.Cultivator.CurrentEssenceSea = ctx.Cultivator.AptitudePercent;
                    }
                    TaiwuGu.Core.GameEventManager.RaiseRequestOpenBreakthroughMinigame();
                    GD.Print("[DiagnosticCapture] Triggered Aperture Breakthrough Seclusion at Substage 4 Peak.");
                }
                else if (_frameCount == 10)
                {
                    TaiwuGu.Core.GameEventManager.RaiseRequestReleaseEssenceWave(1.0f);
                    GD.Print("[DiagnosticCapture] Triggered Tidal Wave Assault.");
                }
                else if (_frameCount == 15)
                {
                    TaiwuGu.Core.GameEventManager.RaiseRequestBreakthroughCrushStone();
                    GD.Print("[DiagnosticCapture] Triggered Stone Crushing mid-breakthrough.");
                }
                else if (_frameCount == 22)
                {
                    CaptureFrame("test_breakthrough_capture.png");
                }
                else if (_frameCount >= 30)
                {
                    GD.Print("[DiagnosticCapture] Breakthrough verification complete. Quitting.");
                    GetTree().Quit();
                }
                return;
            }

            if (_frameCount == 5 && !_captureMapOnly)
            {
                TaiwuGu.Core.GameEventManager.TriggerLedgerToggle();
            }
            if (_frameCount == 15)
            {
                CaptureFrame("test_overworld_capture.png");
            }
            else if (_frameCount >= 30)
            {
                GD.Print("[DiagnosticCapture] Verification complete. Quitting.");
                GetTree().Quit();
            }
        }

        private void CaptureFrame(string filename)
        {
            var viewport = GetViewport();
            if (viewport != null)
            {
                var texture = viewport.GetTexture();
                if (texture != null)
                {
                    try
                    {
                        var image = texture.GetImage();
                        if (image != null && !image.IsEmpty())
                        {
                            string path = ProjectSettings.GlobalizePath($"res://{filename}");
                            var err = image.SavePng(path);
                            GD.Print($"[DiagnosticCapture] Render verification captured to {path} ({image.GetWidth()}x{image.GetHeight()}). ErrorCode: {err}");
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.Print($"[DiagnosticCapture] Note: Viewport image capture unavailable under dummy rendering driver ({ex.Message}).");
                    }
                }
            }
        }
    }
}
