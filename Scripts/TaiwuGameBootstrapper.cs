using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Combat;
using TaiwuGu.Psychology;
using TaiwuGu.Serialization;
using Taiwu_Godot.Scripts.Map;
using Taiwu_Godot.Scripts.Input;
using Taiwu_Godot.Scripts.UI;
using Taiwu_Godot.Scripts.Audio;

namespace Taiwu_Godot.Scripts
{
    /// <summary>
    /// Master Bootstrapper & State Machine Dictator under Constitutional Law 3: Bootstrapper Dictatorship.
    /// Pure Godot 2D implementation with deterministic, synchronous _Ready() boot sequence.
    /// Allocates and seeds GameStateContext (Law 4) and initializes Domain Services Layer.
    /// Injects dependencies into Presentation Layer without scene searching.
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
        [Export] public FeatureOnWorkModalView? OnWorkModalView { get; set; }
        [Export] public NPCDialoguePavilionView? NPCDialogueView { get; set; }
        [Export] public WildGuEncounterModalView? WildGuModalView { get; set; }
        [Export] public InterMountainTransitModalView? TransitModalView { get; set; }

        // --- Master State Truth Container (Law 4) ---
        // The provider is the single live seam: services read Current, and a load replaces it whole.
        public GameStateProvider StateProvider { get; private set; } = null!;
        public GameStateContext StateContext => StateProvider.Current;

        // --- Domain Services Layer ---
        public CalendarTimeService? CalendarService { get; private set; }
        public GuLifecycleService? GuLifecycleService { get; private set; }
        public CultivationService? CultivationService { get; private set; }
        public ExplorationService? ExplorationService { get; private set; }
        public GuRefinementService? RefinementService { get; private set; }
        public CombatCoordinationService? CombatService { get; private set; }
        public CrimeAndForensicsService? ForensicsService { get; private set; }
        public SaveCoordinationService? SaveService { get; private set; }
        public ApertureBreakthroughManager? BreakthroughService { get; private set; }
        public ProceduralNPCWorldManager? NPCWorldManager { get; private set; }
        public WildGuEncounterService? WildEncounterService { get; private set; }
        public InterMountainTransitService? TransitService { get; private set; }
        public TaiwuAudioService? AudioService { get; private set; }
        public TaiwuModalStack? ModalStack { get; private set; }

        /// <summary>
        /// A frame hitch must never skip a refinement phase, so every ticked service receives the
        /// same clamped delta. The simulation is pausable; UI nodes stay Always and render only from
        /// ticked state (see RefinementCauldronView, which reads its timers from the cauldron state).
        /// </summary>
        private const float MAX_SIMULATION_DELTA = 0.1f;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Pausable;
            ExecuteBootSequence();
        }

        private void ExecuteBootSequence()
        {
            GD.Print("[Bootstrapper] Engaging Master Boot Sequence (Godot 2D)...");

            // Step 0: Invert path dependencies via Godot ProjectSettings
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
                    GD.PrintErr($"[Bootstrapper] GuDatabase failed to initialize from {jsonPath}. Refinement and wild encounters will have no catalog.");
                }
            }
            else
            {
                GD.PrintErr($"[Bootstrapper] GuDatabase JSON file not found at {jsonPath}! Gu catalog will be empty.");
            }

            // Step 2: Allocate & seed pure GameStateContext (Law 4: State Centralization & Truth)
            StateProvider = new GameStateProvider(GameStateContext.CreateDefault());
            StateContext.WorldMap.PlayerGridPos = new GridCoord(StartingGridPosition.X, StartingGridPosition.Y);
            GD.Print($"[Bootstrapper] Default GameStateContext allocated & seeded for {StateContext.Cultivator.Name} ({StateContext.Cultivator.GetRankDescription()}).");

            // Publish context resolvers so lower assemblies can resolve state without referencing App
            StateContextProvider.ContextResolver = () => StateContext;
            CombatContextProvider.CultivatorResolver = () => StateContext!.Cultivator.ToCultivatorData();
            CombatContextProvider.GuResolver = () => StateContext!.GuInventory.EquippedGu;
            MapContextProvider.WorldMapResolver = () => StateContext!.WorldMap;
            FactionContextProvider.FactionResolver = () => StateContext!.Faction;

            // Step 3: Initialize Domain Services Layer (Pure Logic & State Mutation)
            CalendarService = new CalendarTimeService();
            CalendarService.Initialize(StateContext, StateProvider);

            GuLifecycleService = new GuLifecycleService();
            GuLifecycleService.Initialize(StateContext, StateProvider);

            CultivationService = new CultivationService();
            CultivationService.Initialize(StateContext, StateProvider);

            ExplorationService = new ExplorationService();
            ExplorationService.Initialize(StateContext, provider: StateProvider);

            RefinementService = new GuRefinementService();
            RefinementService.Initialize(StateContext, StateProvider);

            CombatService = new CombatCoordinationService();
            CombatService.Initialize(StateContext, StateProvider);

            ForensicsService = new CrimeAndForensicsService();
            ForensicsService.Initialize(StateContext, StateProvider);

            BreakthroughService = new ApertureBreakthroughManager();
            BreakthroughService.Initialize(StateContext, StateProvider);

            NPCWorldManager = new ProceduralNPCWorldManager();
            NPCWorldManager.Initialize(StateContext, StateProvider);

            WildEncounterService = new WildGuEncounterService();
            WildEncounterService.Initialize(StateContext, StateProvider);

            TransitService = new InterMountainTransitService();
            TransitService.Initialize(StateContext, StateProvider);

            SaveService = new SaveCoordinationService();
            SaveService.Initialize(StateContext, ExplorationService, StateProvider);

            // Step 4: Wire Presentation and View Layer without Scene Searching
            if (TileManager != null)
            {
                TileManager.SyncFromStateContext(StateContext.WorldMap);
                LandmarkManager?.Initialize(TileManager);
                PlayerController?.Initialize(TileManager, StartingGridPosition);
                if (PlayerController != null)
                {
                    CameraController?.Initialize(PlayerController);
                }

                // Wire or instantiate atmospheric drifting cloud shadows
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

            // Ensure Breakthrough Modal View is present in scene tree
            if (BreakthroughModalView == null)
            {
                BreakthroughModalView = new ApertureBreakthroughModalView { Name = "BreakthroughModalView" };
                AddChild(BreakthroughModalView);
            }

            if (OnWorkModalView == null)
            {
                OnWorkModalView = new FeatureOnWorkModalView { Name = "FeatureOnWorkModalView" };
                AddChild(OnWorkModalView);
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

            // Halal Acoustic Foley & Atmosphere Engine
            if (AudioService == null)
            {
                AudioService = new TaiwuAudioService { Name = "TaiwuAudioService" };
                AddChild(AudioService);
            }

            // Centralized LIFO Modal Stack Manager
            ModalStack ??= new TaiwuModalStack();
            POIModalView?.InitializeModalStack(ModalStack);
            CauldronView?.InitializeModalStack(ModalStack);
            LedgerView?.InitializeModalStack(ModalStack);
            SystemMenuView?.InitializeModalStack(ModalStack, PlayerController);
            HUDView?.InitializeModalStack(ModalStack);
            PlayerController?.InitializeModalStack(ModalStack);
            InputRouter?.InitializeModalStack(ModalStack);
            TransitModalView?.InitializeTransit(TransitService, StateContext);

            // The router is the single input entry point; hand it the handlers it routes to.
            if (InputRouter != null)
            {
                InputRouter.PlayerController = PlayerController;
                InputRouter.SystemMenuView = SystemMenuView;
                InputRouter.BreakthroughView = BreakthroughModalView;
            }

            // Step 5: Broadcast Initial System State to Observers (HUD, Map, Fog).
            // The bootstrapper is the scene's first child, so its _Ready runs before the UI views
            // have subscribed; the broadcast is queued and flushed after the whole tree is ready.
            CallDeferred(nameof(BroadcastInitialState));

            // Master Living Cultivator tokens broadcast across sector
            NPCWorldManager?.BroadcastCultivatorMapTokens();

            // Step 6: Validate Boot Sequence Integrity
            if (Bootstrap_Validation())
            {
                GD.Print("[Bootstrapper] System Boot Complete. Senses unlocked.");
            }
            else
            {
                GD.PrintErr("[Bootstrapper] System Boot encountered validation warnings.");
            }
        }

        /// <summary>
        /// Step 5, deferred: views subscribe in _Ready, which Godot runs in sibling order, so a
        /// broadcast issued from this node's _Ready would reach nobody. The scene tree readies
        /// before the first frame flushes its message queue, so by the time this runs every view
        /// is listening and renders the seeded state instead of its empty defaults.
        /// </summary>
        private void BroadcastInitialState()
        {
            GD.Print("[Bootstrapper] Broadcasting the initial state to observers.");
            CalendarService?.BroadcastCurrentState();
            CultivationService?.BroadcastCurrentState();
            ExplorationService?.BroadcastCurrentState();
            if (StateContext?.GuInventory != null)
            {
                GameEventManager.TriggerGuInventoryUpdated(StateContext.GuInventory.EquippedGu, StateContext.GuInventory.ReserveGu);
            }
            GameEventManager.TriggerRegionChanged(WorldRegionID.SouthernBorder, "Qing Mao Mountain - Gu Yue Village (古月山寨)");
        }

        public bool Bootstrap_Validation()
        {
            bool isValid = true;

            if (StateProvider == null)
            {
                GD.PrintErr("[Bootstrapper] Validation Failed: StateProvider is null!");
                isValid = false;
            }

            if (CalendarService == null || GuLifecycleService == null || CultivationService == null ||
                ExplorationService == null || RefinementService == null || CombatService == null ||
                ForensicsService == null || SaveService == null)
            {
                GD.PrintErr("[Bootstrapper] Validation Failed: One or more domain services failed initialization!");
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

            if (AudioService != null)
            {
                GD.Print("[Bootstrapper] Halal Audio Engine successfully wired.");
            }

            return isValid;
        }

        public override void _Process(double delta)
        {
            float dt = MathF.Min((float)delta, MAX_SIMULATION_DELTA);
            RefinementService?.Tick(dt);
            SaveService?.Tick(dt);
            BreakthroughService?.Tick(dt);
            WildEncounterService?.Tick(dt);
            AudioService?.Tick(dt);
        }

        public override void _ExitTree()
        {
            ModalStack?.Dispose();
            CalendarService?.Dispose();
            GuLifecycleService?.Dispose();
            CultivationService?.Dispose();
            ExplorationService?.Dispose();
            RefinementService?.Dispose();
            CombatService?.Dispose();
            ForensicsService?.Dispose();
            BreakthroughService?.Dispose();
            NPCWorldManager?.Dispose();
            WildEncounterService?.Dispose();
            TransitService?.Dispose();
            AudioService?.Dispose();
            SaveService?.Dispose();

            StateContextProvider.ContextResolver = null;
            CombatContextProvider.CultivatorResolver = null;
            CombatContextProvider.GuResolver = null;
            MapContextProvider.WorldMapResolver = null;
            FactionContextProvider.FactionResolver = null;

            GameEventManager.ClearAllSubscriptions();
            GD.Print("[Bootstrapper] Clean shutdown complete. Subscriptions cleared.");
        }
    }
}
