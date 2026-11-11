using System;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Map;

namespace TaiwuGu.Entities
{
    [Flags]
    public enum DirtyTopic
    {
        None          = 0,
        Calendar      = 1 << 0,
        Cultivator    = 1 << 1,
        Aperture      = 1 << 2,
        Inventory     = 1 << 3,
        Map           = 1 << 4,
        Fog           = 1 << 5,
        PlayerPos     = 1 << 6,
        NPCs          = 1 << 7,
        Forensics     = 1 << 8,
        Factions      = 1 << 9,
        Cauldron      = 1 << 10,
        Breakthrough  = 1 << 11,
        WildEncounter = 1 << 12,
        Transit       = 1 << 13,
        All           = ~0
    }

    /// <summary>
    /// Minimal reactive bus for pure ECS presentation updates, 1-shot sensory feedback, and UI chrome.
    /// Views read directly from WorldStore rather than mirroring state through getter/setter RPC events.
    /// </summary>
    public static class WorldSignals
    {
        // --- Coarse State Change Broadcast ---
        public static event Action<DirtyTopic>? OnTopicsChanged;
        public static void EmitTopics(DirtyTopic topics)
        {
            if (topics != DirtyTopic.None) OnTopicsChanged?.Invoke(topics);
        }

        // --- Transient One-Shot Events (No persisted state to poll) ---
        public static event Action<HalalSoundType>? OnSoundRequested;
        public static event Action<string, string, string, GridCoord>? OnScenicToastRequested;
        public static event Action<GridCoord?, System.Collections.Generic.List<GridCoord>>? OnTargetTileSelected;
        public static event Action<string>? OnDialogueFeedback;
        public static event Action<GuWormComponent?, RefinementFailureTier, string>? OnRefinementResolved;
        public static event Action<bool, bool, float>? OnBreakthroughWaveImpact;
        public static event Action<BreakthroughState, int>? OnBreakthroughResolved;
        public static event Action<SubjugationState, GuWormComponent?, string>? OnWildEncounterResolved;
        public static event Action<ArenaEncounterType, GridCoord>? OnCombatRequested;
        public static event Action<ArenaEncounterType, CombatOutcome, string>? OnCombatResolved;
        public static event Action<GridCoord, string, int, float>? OnTerrainHazardTriggered;
        public static event Action<GridCoord, string>? OnWildBeastAmbushed;
        public static event Action<ArenaEncounterType, GridCoord>? OnRoadAmbushOccurred;
        public static event Action<string, string>? OnOnWorkNoticeRequested;
        public static event Action<string, bool>? OnGameSaved;
        public static event Action<string, bool>? OnGameLoaded;
        public static event Action<string, bool>? OnSaveDeleted;

        // --- Direct UI Navigation & Hardware Input Signals ---
        public static event Action<GridCoord>? OnOpenTilePOIRequested;
        public static event Action<EntityId>? OnOpenNPCDialogueRequested;
        public static event Action? OnCloseTilePOIRequested;
        public static event Action? OnToggleLedgerRequested;
        public static event Action? OnRecenterCameraRequested;
        public static event Action<WorldVec2>? OnPanRequested;
        public static event Action<float>? OnZoomRequested;
        public static event Action<WorldVec2>? OnWorldClickRequested;
        public static event Action? OnEscapeRequested;
        public static bool EscapeHandled { get; set; }
        public static event Action<IModalView>? OnModalOpened;
        public static event Action<IModalView>? OnModalClosed;
        public static event Action<bool>? OnUIModalActive;
        public static event Action<bool>? OnPauseToggled;
        public static event Action<bool>? OnBattleStageChanged;
        public static event Action<bool>? OnLedgerToggled;
        public static event Action<bool>? OnPOIModalToggled;
        public static event Action<bool>? OnCauldronModalToggled;
        public static event Action<bool>? OnTransitModalToggled;

        // --- Triggers ---
        public static void EmitSound(HalalSoundType sound) => OnSoundRequested?.Invoke(sound);
        public static void EmitScenicToast(string title, string seal, string lore, GridCoord pos) => OnScenicToastRequested?.Invoke(title, seal, lore, pos);
        public static void EmitTargetTileSelected(GridCoord? target, System.Collections.Generic.List<GridCoord> path) => OnTargetTileSelected?.Invoke(target, path);
        public static void EmitDialogueFeedback(string message) => OnDialogueFeedback?.Invoke(message);
        public static void EmitRefinementResolved(GuWormComponent? result, RefinementFailureTier tier, string message) => OnRefinementResolved?.Invoke(result, tier, message);
        public static void EmitBreakthroughWaveImpact(bool harmonic, bool dispersion, float damage) => OnBreakthroughWaveImpact?.Invoke(harmonic, dispersion, damage);
        public static void EmitBreakthroughResolved(BreakthroughState state, int newRank) => OnBreakthroughResolved?.Invoke(state, newRank);
        public static void EmitWildEncounterResolved(SubjugationState state, GuWormComponent? gu, string msg) => OnWildEncounterResolved?.Invoke(state, gu, msg);
        public static void EmitCombatRequested(ArenaEncounterType encounter, GridCoord pos) => OnCombatRequested?.Invoke(encounter, pos);
        public static void EmitCombatResolved(ArenaEncounterType encounter, CombatOutcome outcome, string message) => OnCombatResolved?.Invoke(encounter, outcome, message);
        public static void EmitTerrainHazardTriggered(GridCoord pos, string hazardName, int hpDmg, float seaDmg) => OnTerrainHazardTriggered?.Invoke(pos, hazardName, hpDmg, seaDmg);
        public static void EmitWildBeastAmbushed(GridCoord pos, string beastName) => OnWildBeastAmbushed?.Invoke(pos, beastName);
        public static void EmitRoadAmbushOccurred(ArenaEncounterType enc, GridCoord pos) => OnRoadAmbushOccurred?.Invoke(enc, pos);
        public static void EmitOnWorkNotice(string title, string desc) => OnOnWorkNoticeRequested?.Invoke(title, desc);
        public static void EmitGameSaved(string slot, bool ok) => OnGameSaved?.Invoke(slot, ok);
        public static void EmitGameLoaded(string slot, bool ok) => OnGameLoaded?.Invoke(slot, ok);
        public static void EmitSaveDeleted(string slot, bool ok) => OnSaveDeleted?.Invoke(slot, ok);

        public static void EmitOpenTilePOI(GridCoord coord) => OnOpenTilePOIRequested?.Invoke(coord);
        public static void EmitCloseTilePOI() => OnCloseTilePOIRequested?.Invoke();
        public static void EmitOpenNPCDialogue(EntityId npcId) => OnOpenNPCDialogueRequested?.Invoke(npcId);
        public static void EmitToggleLedger() => OnToggleLedgerRequested?.Invoke();
        public static void EmitRecenterCamera() => OnRecenterCameraRequested?.Invoke();
        public static void EmitPan(WorldVec2 delta) => OnPanRequested?.Invoke(delta);
        public static void EmitZoom(float delta) => OnZoomRequested?.Invoke(delta);
        public static void EmitWorldClick(WorldVec2 worldPos) => OnWorldClickRequested?.Invoke(worldPos);

        public static void EmitEscape()
        {
            EscapeHandled = false;
            OnEscapeRequested?.Invoke();
        }

        public static void EmitModalOpened(IModalView modal) => OnModalOpened?.Invoke(modal);
        public static void EmitModalClosed(IModalView modal) => OnModalClosed?.Invoke(modal);
        public static void EmitUIModalActive(bool active) => OnUIModalActive?.Invoke(active);
        public static void EmitPauseToggled(bool paused) => OnPauseToggled?.Invoke(paused);
        public static void EmitBattleStageChanged(bool inBattle) => OnBattleStageChanged?.Invoke(inBattle);
        public static void EmitLedgerToggled(bool open) => OnLedgerToggled?.Invoke(open);
        public static void EmitPOIModalToggled(bool open) => OnPOIModalToggled?.Invoke(open);
        public static void EmitCauldronModalToggled(bool open) => OnCauldronModalToggled?.Invoke(open);
        public static void EmitTransitModalToggled(bool open) => OnTransitModalToggled?.Invoke(open);

        public static void ClearAllSubscriptions()
        {
            OnTopicsChanged = null;
            OnSoundRequested = null;
            OnScenicToastRequested = null;
            OnTargetTileSelected = null;
            OnDialogueFeedback = null;
            OnRefinementResolved = null;
            OnBreakthroughWaveImpact = null;
            OnBreakthroughResolved = null;
            OnWildEncounterResolved = null;
            OnCombatRequested = null;
            OnCombatResolved = null;
            OnTerrainHazardTriggered = null;
            OnWildBeastAmbushed = null;
            OnRoadAmbushOccurred = null;
            OnOnWorkNoticeRequested = null;
            OnGameSaved = null;
            OnGameLoaded = null;
            OnSaveDeleted = null;

            OnOpenTilePOIRequested = null;
            OnOpenNPCDialogueRequested = null;
            OnCloseTilePOIRequested = null;
            OnToggleLedgerRequested = null;
            OnRecenterCameraRequested = null;
            OnPanRequested = null;
            OnZoomRequested = null;
            OnWorldClickRequested = null;
            OnEscapeRequested = null;
            EscapeHandled = false;
            OnModalOpened = null;
            OnModalClosed = null;
            OnUIModalActive = null;
            OnPauseToggled = null;
            OnBattleStageChanged = null;
            OnLedgerToggled = null;
            OnPOIModalToggled = null;
            OnCauldronModalToggled = null;
            OnTransitModalToggled = null;
        }
    }
}
