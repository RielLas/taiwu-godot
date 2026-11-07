using System;
using TaiwuGu.Serialization;
using TaiwuGu.Psychology;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Save & persistence coordination (Phase 3a/3b).
    ///
    /// Reads live state through <see cref="IGameStateProvider"/> and installs a loaded save with
    /// <see cref="GameStateProvider.Replace"/>: a load is a pointer swap, so the running context is
    /// never half-populated and every service sees the new truth on its next read.
    ///
    /// Saving serializes on the calling thread (a coherent snapshot requires it) and only the disk
    /// write is asynchronous, so opening the menu never blocks a frame on I/O.
    /// </summary>
    public class SaveCoordinationService : IDisposable
    {
        private IGameStateProvider? _provider;
        private GameStateContext _context => _provider!.Current;

        private ExplorationService? _explorationService;
        private double _playtimeSeconds = 0;
        private bool _isDisposed = false;
        private bool _isSaving = false;

        public static Action<WorldSocialVendettaState>? ExportVendettaStateHook;
        public static Action<WorldSocialVendettaState>? ImportVendettaStateHook;

        public double TotalPlaytimeSeconds => _playtimeSeconds;

        /// <summary>True while an asynchronous save is in flight; a second request is rejected.</summary>
        public bool IsSaving => _isSaving;

        /// <summary>The live context, i.e. whatever the shared provider currently points at.</summary>
        public GameStateContext Context => _context;

        public void Initialize(GameStateContext context, ExplorationService? explorationService = null, IGameStateProvider? provider = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // A supplied provider is the shared live seam; otherwise own a swappable one so tests
            // and tooling can still exercise load.
            _provider = provider ?? new GameStateProvider(context);
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
            _provider = null;
        }

        private async void HandleSaveGameRequested(string slotID)
        {
            if (!SavePaths.IsValidSlotID(slotID))
            {
                Debug.LogError($"[SaveCoordinationService] Rejected invalid slot id '{slotID}'.");
                GameEventManager.TriggerGameSaved(slotID ?? "", false);
                return;
            }

            if (_isSaving)
            {
                // The UI disables its save buttons while IsSaving, but a double invocation must not
                // produce two interleaved writers on the same slot.
                Debug.LogWarning($"[SaveCoordinationService] Save to '{slotID}' ignored: another save is in flight.");
                return;
            }

            _isSaving = true;
            try
            {
                if (_context.SocialVendetta != null)
                {
                    ExportVendettaStateHook?.Invoke(_context.SocialVendetta);
                }

                bool success = await SaveLoadEngine.SaveContextAsync(slotID, _context, _playtimeSeconds);
                GameEventManager.TriggerGameSaved(slotID, success);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveCoordinationService] Save to '{slotID}' threw: {ex.Message}");
                GameEventManager.TriggerGameSaved(slotID, false);
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void HandleLoadGameRequested(string slotID)
        {
            if (!SavePaths.IsValidSlotID(slotID))
            {
                Debug.LogError($"[SaveCoordinationService] Rejected invalid slot id '{slotID}'.");
                GameEventManager.TriggerGameLoaded(slotID ?? "", false);
                return;
            }

            GameStateContext? loadedContext = SaveLoadEngine.LoadContext(slotID);
            if (loadedContext == null)
            {
                Debug.LogError($"[SaveCoordinationService] Failed to load or restore save slot '{slotID}'.");
                GameEventManager.TriggerGameLoaded(slotID, false);
                return;
            }

            GameStateProvider? liveProvider = _provider as GameStateProvider;
            if (liveProvider == null)
            {
                Debug.LogError("[SaveCoordinationService] Loaded state cannot be installed: the injected provider is fixed.");
                GameEventManager.TriggerGameLoaded(slotID, false);
                return;
            }

            // Atomic swap. The previous context is discarded whole; nothing was mutated while the
            // file was parsed, so a failure above leaves the running game untouched.
            liveProvider.Replace(loadedContext);
            _playtimeSeconds = loadedContext.Calendar != null ? _playtimeSeconds : 0;

            SaveMetadataDTO? metadata = SaveLoadEngine.LoadMetadata(slotID);
            if (metadata != null)
            {
                _playtimeSeconds = metadata.TotalPlaytimeSeconds;
            }

            ImportVendettaStateHook?.Invoke(_context.SocialVendetta);

            BroadcastLoadedState();

            Debug.Log($"[SaveCoordinationService] Loaded save slot '{slotID}'.");
            GameEventManager.TriggerGameLoaded(slotID, true);
        }

        private void HandleDeleteSaveRequested(string slotID)
        {
            bool deleted = SaveLoadEngine.DeleteSlot(slotID);
            GameEventManager.TriggerSaveDeleted(slotID, deleted);
        }

        public void BroadcastLoadedState()
        {
            if (_provider == null) return;

            GameStateContext context = _provider.Current;

            // 1. Calendar & AP
            if (context.Calendar != null)
            {
                GameEventManager.TriggerActionPointsChanged(context.Calendar.ActionPointsRemaining, context.Calendar.MonthlyAPBudget);
                GameEventManager.TriggerSolarTermAdvanced(context.Calendar.CurrentYear, context.Calendar.ActiveSolarTerm, context.Calendar.GetSolarTermName());
            }

            // 2. Cultivation & Essence Sea
            if (context.Cultivator != null)
            {
                GameEventManager.TriggerPrimevalSeaChanged(context.Cultivator.CurrentEssenceSea, context.Cultivator.AptitudePercent);
                GameEventManager.TriggerPrimevalStonesChanged(context.Cultivator.PrimevalStones);
                GameEventManager.TriggerCultivatorCultivationChanged(context.Cultivator.Rank, context.Cultivator.Substage, context.Cultivator.GetSubstageName());
            }

            // 3. World Map & Player Position
            if (context.WorldMap != null)
            {
                int sensoryRadius = _explorationService?.SensoryRadius ?? 4;
                GameEventManager.TriggerPlayerMoved(context.WorldMap.PlayerGridPos);
                GameEventManager.TriggerFogOfWarUpdated(context.WorldMap.PlayerGridPos, sensoryRadius);
            }

            // 4. Gu Inventory
            if (context.GuInventory != null)
            {
                GameEventManager.TriggerGuInventoryUpdated(context.GuInventory.EquippedGu, context.GuInventory.ReserveGu);
            }
        }
    }
}
