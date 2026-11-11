using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Serialization;
using TaiwuGu.Systems;
using Taiwu_Godot.Scripts.Map;
using Taiwu_Godot.Scripts.Combat;
using Taiwu_Godot.Scripts.Input;
using Taiwu_Godot.Scripts.UI;
using Taiwu_Godot.Scripts.Audio;

namespace Taiwu_Godot.Scripts
{
    /// <summary>
    /// Master Bootstrapper under ECS & Direct-Read Presentation architecture.
    /// Allocates WorldStore via WorldFactory, wires WorldStateProvider into presentation views
    /// and systems without scene searching, and drives SimulationLoop.Tick.
    /// </summary>
    public partial class TaiwuGameBootstrapper : Node2D
    {
        [Export] public Vector2I StartingGridPosition { get; set; } = new Vector2I(15, 15);

        // --- Presentation & View Layer Exports (Zero Scene Searching) ---
        [Export] public IsometricTileManager? TileManager { get; set; }
        [Export] public TaiwuInputRouter? InputRouter { get; set; }
        [Export] public TileLandmarkVisualManager? LandmarkManager { get; set; }
        [Export] public PlayerCultivatorController? PlayerController { get; set; }
        [Export] public CameraIsometricController? CameraController { get; set; }
        [Export] public CloudShadowController? CloudShadows { get; set; }
        [Export] public TaiwuHUDView? HUDView { get; set; }
        [Export] public TaiwuLedgerView? LedgerView { get; set; }
        [Export] public RefinementCauldronView? CauldronView { get; set; }
        [Export] public TilePOIModalView? POIModalView { get; set; }
        [Export] public TaiwuSystemMenuView? SystemMenuView { get; set; }
        [Export] public ApertureBreakthroughModalView? BreakthroughModalView { get; set; }
        [Export] public CaravanBazaarModalView? CaravanBazaarView { get; set; }
        [Export] public NPCDialoguePavilionView? NPCDialogueView { get; set; }
        [Export] public WildGuEncounterModalView? WildGuModalView { get; set; }
        [Export] public InterMountainTransitModalView? TransitModalView { get; set; }
        [Export] public CombatArenaView? CombatArenaView { get; set; }
        [Export] public CombatHUDView? CombatHUDView { get; set; }

        // --- Single Source of Truth ECS State Seam ---
        public WorldStateProvider StateProvider { get; private set; } = null!;
        public WorldStore World => StateProvider.Current;

        public TaiwuAudioService? AudioService { get; private set; }
        public TaiwuModalStack? ModalStack { get; private set; }

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Pausable;
            ExecuteBootSequence();
        }

        private void ExecuteBootSequence()
        {
            GD.Print("[Bootstrapper] Engaging Master Boot Sequence (ECS Godot 2D)...");

            // Step 0: Configure Save directory
            SavePaths.BaseSaveDirectory = ProjectSettings.GlobalizePath("user://Saves");
            GD.Print($"[Bootstrapper] Configured BaseSaveDirectory: {SavePaths.BaseSaveDirectory}");

            // Step 1: Initialize Static Gu Worm Database from JSON
            string jsonPath = "res://Resources/Data/gu_worms_database.json";
            if (FileAccess.FileExists(jsonPath))
            {
                string jsonContent = FileAccess.GetFileAsString(jsonPath);
                if (GuDatabase.Initialize(jsonContent))
                {
                    GD.Print($"[Bootstrapper] GuDatabase initialized from {jsonPath} ({GuDatabase.Count} catalog entries).");
                }
                else
                {
                    GD.PrintErr($"[Bootstrapper] GuDatabase failed to initialize from {jsonPath}.");
                }
            }
            else
            {
                GD.PrintErr($"[Bootstrapper] GuDatabase JSON file not found at {jsonPath}!");
            }

            // Step 2: Create & seed ECS WorldStore
            var startPos = new GridCoord(StartingGridPosition.X, StartingGridPosition.Y);
            StateProvider = new WorldStateProvider(WorldFactory.CreateDefault(runSeed: 1, playerStart: startPos));

            var playerCult = World.Get<CultivatorComponent>(World.PlayerEntity);
            var playerAp = World.Get<ApertureComponent>(World.PlayerEntity);
            GD.Print($"[Bootstrapper] ECS WorldStore seeded for {playerCult?.Name} ({playerAp?.GetRankDescription()}).");

            // Step 3: Wire Presentation and View Layer without Scene Searching
            if (TileManager != null)
            {
                TileManager.Initialize(StateProvider);
                LandmarkManager?.Initialize(TileManager, StateProvider);
                PlayerController?.Initialize(TileManager, StartingGridPosition, StateProvider);
                if (PlayerController != null)
                {
                    CameraController?.Initialize(PlayerController);
                }

                if (CloudShadows == null)
                {
                    CloudShadows = new CloudShadowController { Name = "AtmosphericCloudShadows" };
                    AddChild(CloudShadows);
                }
                if (PlayerController != null)
                {
                    CloudShadows.SetFollowTarget(PlayerController);
                }
            }

            if (BreakthroughModalView == null)
            {
                BreakthroughModalView = new ApertureBreakthroughModalView { Name = "BreakthroughModalView" };
                AddChild(BreakthroughModalView);
            }

            if (NPCDialogueView == null)
            {
                NPCDialogueView = new NPCDialoguePavilionView { Name = "NPCDialoguePavilionView" };
                AddChild(NPCDialogueView);
            }

            if (WildGuModalView == null)
            {
                WildGuModalView = new WildGuEncounterModalView { Name = "WildGuEncounterModalView" };
                AddChild(WildGuModalView);
            }

            if (TransitModalView == null)
            {
                TransitModalView = new InterMountainTransitModalView { Name = "InterMountainTransitModalView" };
                AddChild(TransitModalView);
            }

            if (AudioService == null)
            {
                AudioService = new TaiwuAudioService { Name = "TaiwuAudioService" };
                AddChild(AudioService);
            }

            ModalStack ??= new TaiwuModalStack();
            POIModalView?.Initialize(ModalStack, StateProvider);
            CauldronView?.Initialize(ModalStack, StateProvider);
            LedgerView?.Initialize(ModalStack, StateProvider);
            SystemMenuView?.Initialize(ModalStack, PlayerController, StateProvider);
            HUDView?.Initialize(ModalStack, StateProvider);
            BreakthroughModalView?.Initialize(StateProvider);
            CaravanBazaarView?.Initialize(ModalStack, StateProvider);
            WildGuModalView?.Initialize(StateProvider);
            NPCDialogueView?.Initialize(StateProvider);
            TransitModalView?.Initialize(StateProvider);
            PlayerController?.InitializeModalStack(ModalStack);
            CombatArenaView?.Initialize(StateProvider, CameraController);
            CombatHUDView?.Initialize(StateProvider);

            if (InputRouter != null)
            {
                InputRouter.Initialize(ModalStack, StateProvider);
                InputRouter.PlayerController = PlayerController;
                InputRouter.SystemMenuView = SystemMenuView;
                InputRouter.BreakthroughView = BreakthroughModalView;
            }

            // Defer initial broadcast until sibling views complete _Ready()
            CallDeferred(nameof(BroadcastInitialState));

            if (Bootstrap_Validation())
            {
                GD.Print("[Bootstrapper] System Boot Complete. Senses unlocked.");
            }
            else
            {
                GD.PrintErr("[Bootstrapper] System Boot encountered validation warnings.");
            }
        }

        private void BroadcastInitialState()
        {
            GD.Print("[Bootstrapper] Emitting initial DirtyTopic.All broadcast to ECS observers.");
            WorldSignals.EmitTopics(DirtyTopic.All);
        }

        public bool Bootstrap_Validation()
        {
            bool isValid = true;

            if (StateProvider == null || World.GetCurrentSector() == null)
            {
                GD.PrintErr("[Bootstrapper] Validation Failed: StateProvider or CurrentSector is null!");
                isValid = false;
            }

            if (TileManager == null || PlayerController == null || CameraController == null)
            {
                GD.Print("[Bootstrapper] Note: Presentation nodes will be wired via scene or bootstrapper assignment.");
            }
            else
            {
                GD.Print("[Bootstrapper] Presentation layer (TileManager, PlayerController, CameraController) successfully wired.");
            }

            if (HUDView != null && LedgerView != null && CauldronView != null && POIModalView != null && SystemMenuView != null)
            {
                GD.Print("[Bootstrapper] All 5 UI views (HUD, Ledger, Cauldron, POIModal, SystemMenu) wired via Bootstrapper exports.");
            }

            return isValid;
        }

        public override void _Process(double delta)
        {
            float dt = MathF.Min((float)delta, SimulationLoop.MAX_DELTA);
            SimulationLoop.Tick(World, dt);
            AudioService?.Tick(dt);
        }

        public override void _ExitTree()
        {
            ModalStack?.Dispose();
            AudioService?.Dispose();
            WorldSignals.ClearAllSubscriptions();
            GD.Print("[Bootstrapper] Clean shutdown complete. Subscriptions cleared.");
        }
    }
}
