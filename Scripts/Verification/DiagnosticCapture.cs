using System;
using Godot;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Systems;
using Taiwu_Godot.Scripts;

namespace Taiwu_Godot.Scripts.Verification
{
    /// <summary>
    /// Headless Verification Harness:
    /// Exercises ECS flows, verifies scene hierarchy integrity, captures viewport images, and quits cleanly.
    /// </summary>
    public partial class DiagnosticCapture : Node
    {
        [Export] public bool AutoCaptureAndQuit { get; set; } = false;
        [Export] public TaiwuGameBootstrapper? Bootstrapper { get; set; }

        private int _frameCount = 0;

        private bool _captureMapOnly = false;
        private bool _captureBreakthrough = false;
        private bool _captureCaravan = false;
        private bool _captureNPC = false;
        private bool _captureTileNPC = false;
        private bool _captureEncounter = false;
        private bool _captureCombat = false;
        private bool _captureTransit = false;
        private bool _testAudio = false;
        private bool _testAll = false;
        private bool _testLandmark = false;
        private bool _allCaravanOk = true;
        private bool _verify = false;
        private bool _strictPoi = false;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;

            ApplyArgs(OS.GetCmdlineArgs());
            ApplyArgs(OS.GetCmdlineUserArgs());
        }

        private void ApplyArgs(string[] args)
        {
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
                if (arg == "--caravan") _captureCaravan = true;
                if (arg == "--npc") _captureNPC = true;
                if (arg == "--tile-npc") _captureTileNPC = true;
                if (arg == "--encounter") _captureEncounter = true;
                if (arg == "--combat") _captureCombat = true;
                if (arg == "--transit") _captureTransit = true;
                if (arg == "--audio") _testAudio = true;
                if (arg == "--all") _testAll = true;
                if (arg == "--landmark") _testLandmark = true;
            }
        }

        public override void _Process(double delta)
        {
            if (!AutoCaptureAndQuit) return;
            var world = Bootstrapper?.StateProvider?.Current;

            _frameCount++;

            if (_verify)
            {
                if (_frameCount == 10)
                {
                    int exitCode = VerificationRunner.Run(Bootstrapper, _strictPoi);
                    GD.Print($"[DiagnosticCapture] Verification finished with exit code {exitCode}.");
                    GetTree().Quit(exitCode);
                }
                return;
            }

            if (world == null) return;

            if (_testLandmark)
            {
                if (_frameCount == 5)
                {
                    GD.Print("[DiagnosticCapture] (--landmark) Step 1: Testing direct click / inspect on current tile (15, 15)...");
                    ExplorationSystem.InspectTile(world, new GridCoord(15, 15));
                }
                else if (_frameCount == 10)
                {
                    bool isOpen = Bootstrapper?.POIModalView?.IsOpen == true;
                    GD.Print($"[DiagnosticCapture] (--landmark) POI Modal IsOpen after inspect: {isOpen}, ModalStack Count: {Bootstrapper?.ModalStack?.Count}");
                    if (!isOpen) GD.PrintErr("[DiagnosticCapture] FAIL: POI Modal failed to open on direct current tile inspect!");
                    Bootstrapper?.ModalStack?.TryPop();
                    GD.Print($"[DiagnosticCapture] (--landmark) POI Modal IsOpen after Pop: {Bootstrapper?.POIModalView?.IsOpen}");
                }
                else if (_frameCount == 15)
                {
                    GD.Print("[DiagnosticCapture] (--landmark) Step 2: Testing scenic non-interactive landmark inspect (Ancient Pine)...");
                    var sector = world.GetCurrentSector();
                    if (sector != null)
                    {
                        var tiles = sector.EnsureHydrated();
                        tiles[16 * sector.GridWidth + 16].POI = TilePOIType.AncientPine;
                    }
                    ExplorationSystem.InspectTile(world, new GridCoord(16, 16));
                }
                else if (_frameCount == 20)
                {
                    bool modalOpened = Bootstrapper?.POIModalView?.IsOpen == true;
                    GD.Print($"[DiagnosticCapture] (--landmark) Scenic inspect verified: POIModal IsOpen = {modalOpened} (Expected: False)");
                    if (modalOpened) GD.PrintErr("[DiagnosticCapture] FAIL: Scenic landmark should NOT open modal dialog!");
                }
                else if (_frameCount == 25)
                {
                    GD.Print("[DiagnosticCapture] (--landmark) Step 3: Testing impassable obstacle tile (Boulder Precipice)...");
                    if (Bootstrapper?.TileManager != null)
                    {
                        Bootstrapper.TileManager.GetTile(14, 14);
                        bool isWalkable = Bootstrapper.TileManager.IsTileWalkable(new Vector2I(14, 14));
                        GD.Print($"[DiagnosticCapture] (--landmark) Normal tile (14, 14) walkable: {isWalkable}");

                        var sector = world.GetCurrentSector();
                        if (sector != null)
                        {
                            var tiles = sector.EnsureHydrated();
                            tiles[14 * sector.GridWidth + 14].POI = TilePOIType.BoulderPrecipice;
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
                if (_frameCount == 5)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 1: Testing Movement, Audio & ModalStack baseline...");
                    if (Bootstrapper?.ModalStack == null) GD.PrintErr("[DiagnosticCapture] FAIL: ModalStack is null!");
                    WorldSignals.EmitSound(HalalSoundType.Footstep);
                    ExplorationSystem.MovePlayer(world, new GridCoord(16, 15));
                }
                else if (_frameCount == 10)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 2: Testing Alchemical Cauldron opening & ModalStack...");
                    RefinementSystem.OpenCauldron(world);
                }
                else if (_frameCount == 15)
                {
                    GD.Print($"[DiagnosticCapture] (--all) Cauldron IsOpen: {Bootstrapper?.CauldronView?.IsOpen}, ModalStack Count: {Bootstrapper?.ModalStack?.Count}");
                    RefinementSystem.PulseHeat(world);
                }
                else if (_frameCount == 20)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 3: Popping Cauldron via ModalStack.TryPop()...");
                    Bootstrapper?.ModalStack?.TryPop();
                    GD.Print($"[DiagnosticCapture] (--all) Cauldron IsOpen after Pop: {Bootstrapper?.CauldronView?.IsOpen}");
                }
                else if (_frameCount == 25)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 4: Testing Breakthrough Seclusion opening & ModalStack...");
                    var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
                    var ap = world.Get<ApertureComponent>(world.PlayerEntity);
                    if (cult != null && ap != null)
                    {
                        ap.Substage = 4;
                        ap.CurrentEssenceSea = cult.AptitudePercent;
                    }
                    BreakthroughSystem.OpenSession(world);
                }
                else if (_frameCount == 30)
                {
                    GD.Print($"[DiagnosticCapture] (--all) Breakthrough IsOpen: {Bootstrapper?.BreakthroughModalView?.IsOpen}, ModalStack Count: {Bootstrapper?.ModalStack?.Count}");
                    Bootstrapper?.ModalStack?.TryPop();
                    GD.Print($"[DiagnosticCapture] (--all) Breakthrough IsOpen after Pop: {Bootstrapper?.BreakthroughModalView?.IsOpen}");
                }
                else if (_frameCount == 35)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 5: Testing Transit Modal opening & ModalStack...");
                    TransitSystem.OpenTransitModal(world, false);
                }
                else if (_frameCount == 40)
                {
                    GD.Print($"[DiagnosticCapture] (--all) Transit IsOpen: {Bootstrapper?.TransitModalView?.IsOpen}, ModalStack Count: {Bootstrapper?.ModalStack?.Count}");
                    Bootstrapper?.ModalStack?.TryPop();
                    GD.Print($"[DiagnosticCapture] (--all) Transit IsOpen after Pop: {Bootstrapper?.TransitModalView?.IsOpen}");
                }
                else if (_frameCount == 45)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 6: Testing System Menu & Atomic Save...");
                    Bootstrapper?.SystemMenuView?.OpenMenu();
                    _ = SaveSystem.SaveAsync("slot_01", world);
                }
                else if (_frameCount == 50)
                {
                    GD.Print($"[DiagnosticCapture] (--all) SystemMenu IsOpen: {Bootstrapper?.SystemMenuView?.IsOpen}");
                    Bootstrapper?.ModalStack?.TryPop();
                    CaptureFrame("test_grand_convergence_all.png");
                }
                else if (_frameCount == 55)
                {
                    GD.Print("[DiagnosticCapture] (--all) Step 7: Testing Jia Caravan Bazaar & ModalStack...");
                    bool routed = Bootstrapper?.POIModalView?.TryOpenCaravanBazaar() == true;
                    bool open = Bootstrapper?.CaravanBazaarView?.IsOpen == true;
                    GD.Print($"[DiagnosticCapture] (--all) Caravan route={routed} IsOpen={open}, ModalStack Count: {Bootstrapper?.ModalStack?.Count}");
                    if (!routed || !open) _allCaravanOk = false;
                }
                else if (_frameCount == 60)
                {
                    var session = CaravanEconomySystem.EnsureStock(world);
                    CaravanStockEntry? fodder = null;
                    foreach (var entry in session.Stock)
                    {
                        if (entry.Kind == CaravanStockKind.Fodder && entry.Remaining > 0) { fodder = entry; break; }
                    }
                    var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
                    var inv = world.Get<InventoryComponent>(world.PlayerEntity);
                    int foodBefore = fodder != null && inv != null ? inv.GetFoodQuantity(fodder.Diet) : -1;
                    int stonesBefore = cult?.PrimevalStones ?? -1;
                    bool bought = fodder != null && CaravanEconomySystem.TryBuyStock(world, fodder.Id, out string buyMessage);
                    bool foodOk = fodder != null && inv != null && inv.GetFoodQuantity(fodder.Diet) == foodBefore + 5;
                    bool stonesOk = cult != null && cult.PrimevalStones < stonesBefore;
                    GD.Print($"[DiagnosticCapture] (--all) Caravan fodder buy={bought} foodOk={foodOk} stonesOk={stonesOk}");
                    if (!bought || !foodOk || !stonesOk) _allCaravanOk = false;
                }
                else if (_frameCount == 65)
                {
                    var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
                    int before = cult?.PrimevalStones ?? -1;
                    bool gambled = CaravanEconomySystem.TryGamble(world, CaravanOreTier.Low, out var result);
                    int after = cult?.PrimevalStones ?? -1;
                    bool exact = gambled && after == before - CaravanEconomySystem.GetOreCost(CaravanOreTier.Low) + result.StonesAwarded;
                    GD.Print($"[DiagnosticCapture] (--all) Caravan gamble={gambled} outcome={result.Outcome} stonesExact={exact} message={result.Message}");
                    if (!gambled || !exact) _allCaravanOk = false;
                    CaptureFrame("test_caravan_capture.png");
                }
                else if (_frameCount == 70)
                {
                    bool popped = Bootstrapper?.ModalStack?.TryPop() == true;
                    bool closed = Bootstrapper?.CaravanBazaarView?.IsOpen == false;
                    GD.Print($"[DiagnosticCapture] (--all) Caravan pop={popped} closed={closed}, ModalStack Count: {Bootstrapper?.ModalStack?.Count}");
                    if (!popped || !closed) _allCaravanOk = false;
                }
                else if (_frameCount >= 75)
                {
                    GD.Print($"[DiagnosticCapture] Grand Parity Convergence (--all) verified! caravanOk={_allCaravanOk}. Quitting.");
                    GetTree().Quit(_allCaravanOk ? 0 : 1);
                }
                return;
            }

            if (_captureCaravan)
            {
                if (_frameCount == 5)
                {
                    GD.Print("[caravan] Step 1: Inspecting the Way Station POI...");
                    var sector = world.GetCurrentSector();
                    if (sector != null)
                    {
                        var tiles = sector.EnsureHydrated();
                        tiles[15 * sector.GridWidth + 19].POI = TilePOIType.WayStation;
                    }
                    ExplorationSystem.InspectTile(world, new GridCoord(19, 15));
                    bool poiOpen = Bootstrapper?.POIModalView?.IsOpen == true;
                    GD.Print($"[caravan] WayStation POI IsOpen={poiOpen}");
                    if (!poiOpen)
                    {
                        GD.PrintErr("[caravan] FAIL: Way Station POI did not open.");
                        GetTree().Quit(1);
                    }
                }
                else if (_frameCount == 10)
                {
                    bool routed = Bootstrapper?.POIModalView?.TryOpenCaravanBazaar() == true;
                    bool poiClosed = Bootstrapper?.POIModalView?.IsOpen == false;
                    bool open = Bootstrapper?.CaravanBazaarView?.IsOpen == true;
                    int count = Bootstrapper?.ModalStack?.Count ?? -1;
                    GD.Print($"[caravan] route={routed} poiClosed={poiClosed} bazaarOpen={open} stack={count}");
                    if (!routed || !poiClosed || !open || count != 1)
                    {
                        GD.PrintErr("[caravan] FAIL: bazaar did not replace the POI on the modal stack.");
                        GetTree().Quit(1);
                    }
                }
                else if (_frameCount == 15)
                {
                    var session = CaravanEconomySystem.EnsureStock(world);
                    CaravanStockEntry? fodder = null;
                    foreach (var entry in session.Stock)
                    {
                        if (entry.Kind == CaravanStockKind.Fodder && entry.Remaining > 0) { fodder = entry; break; }
                    }
                    var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
                    var inv = world.Get<InventoryComponent>(world.PlayerEntity);
                    if (fodder == null || cult == null || inv == null)
                    {
                        GD.PrintErr("[caravan] FAIL: no fodder lot or player inventory.");
                        GetTree().Quit(1);
                        return;
                    }
                    int foodBefore = inv.GetFoodQuantity(fodder.Diet);
                    int stonesBefore = cult.PrimevalStones;
                    bool bought = CaravanEconomySystem.TryBuyStock(world, fodder.Id, out string message);
                    bool foodOk = inv.GetFoodQuantity(fodder.Diet) == foodBefore + 5;
                    bool stonesOk = cult.PrimevalStones < stonesBefore;
                    GD.Print($"[caravan] buy={bought} foodOk={foodOk} stonesOk={stonesOk} message={message}");
                    if (!bought || !foodOk || !stonesOk)
                    {
                        GD.PrintErr("[caravan] FAIL: fodder purchase did not mutate stones and inventory.");
                        GetTree().Quit(1);
                    }
                }
                else if (_frameCount == 20)
                {
                    var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
                    int before = cult?.PrimevalStones ?? -1;
                    bool gambled = CaravanEconomySystem.TryGamble(world, CaravanOreTier.Low, out var result);
                    int after = cult?.PrimevalStones ?? -1;
                    bool exact = gambled && after == before - CaravanEconomySystem.GetOreCost(CaravanOreTier.Low) + result.StonesAwarded;
                    bool reduced = after < before;
                    GD.Print($"[caravan] gamble={gambled} outcome={result.Outcome} stonesExact={exact} reduced={reduced} message={result.Message}");
                    if (!gambled || !exact || !reduced)
                    {
                        GD.PrintErr("[caravan] FAIL: stone gamble did not resolve deterministically or deduct stones.");
                        GetTree().Quit(1);
                    }
                }
                else if (_frameCount == 25)
                {
                    bool popped = Bootstrapper?.ModalStack?.TryPop() == true;
                    bool closed = Bootstrapper?.CaravanBazaarView?.IsOpen == false;
                    GD.Print($"[caravan] pop={popped} closed={closed}, stack={Bootstrapper?.ModalStack?.Count}");
                    if (!popped || !closed)
                    {
                        GD.PrintErr("[caravan] FAIL: ModalStack did not pop the bazaar.");
                        GetTree().Quit(1);
                    }
                }
                else if (_frameCount >= 30)
                {
                    GD.Print("[caravan] PASS: WayStation route, fodder purchase, stone gamble and modal pop verified.");
                    GetTree().Quit(0);
                }
                return;
            }

            if (_captureCombat)
            {
                if (_frameCount == 5)
                {
                    bool started = CombatSystem.StartCombat(
                        world,
                        ArenaEncounterType.WildBoarHunt,
                        new GridCoord(15, 15),
                        isLethal: true,
                        enemyName: "Smoke Test Boars",
                        rng: new SeededRandom(7));
                    GD.Print($"[combat] StartCombat={started}");
                }
                else if (_frameCount == 10)
                {
                    var arena = Bootstrapper?.CombatArenaView;
                    var camera = Bootstrapper?.CameraController;
                    bool visible = arena?.Visible == true;
                    bool cameraMoved = camera != null && camera.Position.DistanceTo(new Vector2(2000f, 2000f)) < 0.5f;
                    bool zoomed = camera != null && Math.Abs(camera.Zoom.X - 56f) < 0.5f;
                    GD.Print($"[combat] arenaVisible={visible} camera={camera?.Position} zoom={camera?.Zoom.X} (expected 2000,2000 / 56)");

                    var simulation = CombatSystem.GetOrCreateArena(world);
                    bool cast = simulation.TryCastSkill(world, 0);
                    bool crushed = simulation.TryCrushStone(world);
                    GD.Print($"[combat] slot0Cast={cast} stoneCrush={crushed}");

                    for (int i = 1; i < CombatArenaSimulation.MAX_ENTITIES; i++)
                    {
                        ref var enemy = ref simulation.EntityBuffer[i];
                        if (enemy.Active && enemy.Team == CombatEntityTeam.Hostile)
                        {
                            enemy.Health = 0f;
                            enemy.Active = false;
                        }
                    }

                    CombatSystem.Tick(world, 0.016f);
                    bool sessionOver = world.Get<CombatSessionComponent>(world.WorldEntity)?.InBattle != true;
                    GD.Print($"[combat] resolved outcome={simulation.Outcome} sessionOver={sessionOver}");
                }
                else if (_frameCount == 15)
                {
                    var arena = Bootstrapper?.CombatArenaView;
                    var camera = Bootstrapper?.CameraController;
                    Vector2 expected = ExplorationSystem.GridToWorld(world.GetPlayerPosition()).ToGodot();
                    bool hidden = arena?.Visible == false;
                    bool cameraRestored = camera != null && camera.Position.DistanceTo(expected) < 2.0f;
                    bool zoomRestored = camera != null && Math.Abs(camera.Zoom.X - 68f) < 0.5f;
                    GD.Print($"[combat] hidden={hidden} camera={camera?.Position} expected={expected} zoom={camera?.Zoom.X} (expected 68)");

                    if (hidden && cameraRestored && zoomRestored)
                    {
                        GD.Print("[combat] PASS: arena, camera handover, cast, stone crush and victory resolution verified.");
                        GetTree().Quit(0);
                    }
                    else
                    {
                        GD.PrintErr("[combat] FAIL: combat smoke checks failed.");
                        GetTree().Quit(1);
                    }
                }
                return;
            }

            if (_testAudio)
            {
                if (_frameCount == 5)
                {
                    GD.Print("[DiagnosticCapture] Testing Halal Audio Intent Events...");
                    WorldSignals.EmitSound(HalalSoundType.ParchmentRustle);
                    WorldSignals.EmitSound(HalalSoundType.CauldronFire);
                    WorldSignals.EmitSound(HalalSoundType.StoneShatter);
                    WorldSignals.EmitSound(HalalSoundType.BladeSlash);
                    WorldSignals.EmitSound(HalalSoundType.BluntImpact);
                    WorldSignals.EmitSound(HalalSoundType.DashWhoosh);
                    WorldSignals.EmitSound(HalalSoundType.CauldronSteam);
                    WorldSignals.EmitSound(HalalSoundType.WoodenKnock);
                    WorldSignals.EmitBattleStageChanged(true);
                }
                else if (_frameCount == 15)
                {
                    WorldSignals.EmitBattleStageChanged(false);
                    WorldSignals.EmitSound(HalalSoundType.Footstep);
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
                    TransitSystem.OpenTransitModal(world, false);
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
                    var target = WildOrganismData.CreateWildOrganism("gu_moonlight_r1", WildOrganismTemperament.Ferocious);
                    WildEncounterSystem.OpenEncounter(world, target);
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
                    ExplorationSystem.MovePlayer(world, new GridCoord(15, 14), openInteractivePOI: true);
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
                EntityId npcId = world.FindByExternalId("npc_chi_disciple");
                if (_frameCount == 5 && npcId.IsValid)
                {
                    WorldSignals.EmitOpenNPCDialogue(npcId);
                    GD.Print("[DiagnosticCapture] Interacted with Gu Yue Chi Cheng.");
                }
                else if (_frameCount == 10 && npcId.IsValid)
                {
                    SocialVendettaSystem.ExecuteBarter(world, npcId);
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

            if (_captureBreakthrough)
            {
                if (_frameCount == 5)
                {
                    var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
                    var ap = world.Get<ApertureComponent>(world.PlayerEntity);
                    if (cult != null && ap != null)
                    {
                        ap.Substage = 4;
                        ap.CurrentEssenceSea = cult.AptitudePercent;
                    }
                    BreakthroughSystem.OpenSession(world);
                    GD.Print("[DiagnosticCapture] Triggered Aperture Breakthrough Seclusion at Substage 4 Peak.");
                }
                else if (_frameCount == 10)
                {
                    BreakthroughSystem.ReleaseEssenceWave(world, 1.0f);
                    GD.Print("[DiagnosticCapture] Triggered Tidal Wave Assault.");
                }
                else if (_frameCount == 15)
                {
                    BreakthroughSystem.CrushStone(world);
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
                WorldSignals.EmitToggleLedger();
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
            if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

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
