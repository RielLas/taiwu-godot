using System;
using TaiwuGu.Serialization;
using TaiwuGu.Psychology;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Module 7 & Phase 6: Save & Persistence Coordination Domain Service.
    /// Implements:
    /// - Constitutional Law 1: Observer Mandate via CQRS-Lite (listens to OnSaveGameRequested, OnLoadGameRequested).
    /// - Constitutional Law 4: State Centralization & Truth (hydrates GameStateContext directly from SavePayloadDTO).
    /// - Non-blocking atomic 3-step persistence execution via SaveLoadEngine.
    /// - Deterministic broadcast after load to synchronize HUD, Map, Player entity, and Gu inventory.
    /// </summary>
    public class SaveCoordinationService : IDisposable
    {
        private GameStateContext? _context;
        private ExplorationService? _explorationService;
        private double _playtimeSeconds = 0;
        private bool _isDisposed = false;

        public static Action<WorldSocialVendettaState>? ExportVendettaStateHook;
        public static Action<WorldSocialVendettaState>? ImportVendettaStateHook;

        public double TotalPlaytimeSeconds => _playtimeSeconds;

        public void Initialize(GameStateContext context, ExplorationService? explorationService = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _explorationService = explorationService;
            _playtimeSeconds = 0;

            SaveLoadEngine.Initialize();

            GameEventManager.OnSaveGameRequested   += HandleSaveGameRequested;
            GameEventManager.OnLoadGameRequested   += HandleLoadGameRequested;
            GameEventManager.OnDeleteSaveRequested += HandleDeleteSaveRequested;

            Debug.Log("[SaveCoordinationService] Initialized and monitoring atomic save/load intents.");
        }

        public void SetExplorationService(ExplorationService explorationService)
        {
            _explorationService = explorationService;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                _playtimeSeconds += deltaTime;
            }
        }

        public void SetPlaytime(double seconds)
        {
            _playtimeSeconds = Math.Max(0, seconds);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            GameEventManager.OnSaveGameRequested   -= HandleSaveGameRequested;
            GameEventManager.OnLoadGameRequested   -= HandleLoadGameRequested;
            GameEventManager.OnDeleteSaveRequested -= HandleDeleteSaveRequested;

            _explorationService = null;
            _context = null;
        }

        private void HandleSaveGameRequested(string slotID)
        {
            if (string.IsNullOrEmpty(slotID) || _context == null)
            {
                Debug.LogError("[SaveCoordinationService] Cannot save: invalid slotID or null GameStateContext.");
                GameEventManager.TriggerGameSaved(slotID, false);
                return;
            }

            if (_context.SocialVendetta != null)
            {
                ExportVendettaStateHook?.Invoke(_context.SocialVendetta);
            }

            bool success = SaveLoadEngine.SaveContext(slotID, _context, _playtimeSeconds);
            GameEventManager.TriggerGameSaved(slotID, success);
        }

        private void HandleLoadGameRequested(string slotID)
        {
            if (string.IsNullOrEmpty(slotID) || _context == null)
            {
                Debug.LogError("[SaveCoordinationService] Cannot load: invalid slotID or null GameStateContext.");
                GameEventManager.TriggerGameLoaded(slotID, false);
                return;
            }

            GameStateContext? loadedCtx = SaveLoadEngine.LoadContext(slotID);
            if (loadedCtx == null)
            {
                Debug.LogError($"[SaveCoordinationService] Failed to load or restore save slot '{slotID}'.");
                GameEventManager.TriggerGameLoaded(slotID, false);
                return;
            }

            SaveMetadataDTO? meta = SaveLoadEngine.LoadMetadata(slotID);
            if (meta != null)
            {
                _playtimeSeconds = meta.TotalPlaytimeSeconds;
            }

            SavePayloadDTO payload = SaveLoadEngine.ExtractPayload(slotID, loadedCtx, _playtimeSeconds);
            SaveLoadEngine.HydrateContext(payload, _context);

            if (payload.Metadata != null)
            {
                _playtimeSeconds = payload.Metadata.TotalPlaytimeSeconds;
            }

            if (_context.SocialVendetta != null)
            {
                ImportVendettaStateHook?.Invoke(_context.SocialVendetta);
            }

            BroadcastLoadedState();

            Debug.Log($"[SaveCoordinationService] Successfully loaded & hydrated save slot '{slotID}'.");
            GameEventManager.TriggerGameLoaded(slotID, true);
        }

        private void HandleDeleteSaveRequested(string slotID)
        {
            bool deleted = SaveLoadEngine.DeleteSlot(slotID);
            GameEventManager.TriggerSaveDeleted(slotID, deleted);
        }

        public void BroadcastLoadedState()
        {
            if (_context == null) return;

            // 1. Calendar & AP
            if (_context.Calendar != null)
            {
                GameEventManager.TriggerActionPointsChanged(_context.Calendar.ActionPointsRemaining, _context.Calendar.MonthlyAPBudget);
                GameEventManager.TriggerSolarTermAdvanced(_context.Calendar.CurrentYear, _context.Calendar.ActiveSolarTerm, _context.Calendar.GetSolarTermName());
            }

            // 2. Cultivation & Essence Sea
            if (_context.Cultivator != null)
            {
                GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
                GameEventManager.TriggerPrimevalStonesChanged(_context.Cultivator.PrimevalStones);
                GameEventManager.TriggerCultivatorCultivationChanged(_context.Cultivator.Rank, _context.Cultivator.Substage, _context.Cultivator.GetSubstageName());
            }

            // 3. World Map & Player Position
            if (_context.WorldMap != null)
            {
                int sensoryRadius = _explorationService?.SensoryRadius ?? 4;
                GameEventManager.TriggerPlayerMoved(_context.WorldMap.PlayerGridPos);
                GameEventManager.TriggerFogOfWarUpdated(_context.WorldMap.PlayerGridPos, sensoryRadius);
            }

            // 4. Gu Inventory
            if (_context.GuInventory != null)
            {
                GameEventManager.TriggerGuInventoryUpdated(_context.GuInventory.EquippedGu, _context.GuInventory.ReserveGu);
            }
        }
    }
}
