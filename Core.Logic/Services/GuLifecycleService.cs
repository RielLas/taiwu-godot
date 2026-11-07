using System;
using System.Collections.Generic;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 2: Gu Feeding Lifecycle & Parasitic Backlash Service.
    /// Manages:
    /// - Rank-scaled feeding intervals evaluated on OnSolarTermAdvanced:
    ///   (Rank 1: 1 term, Rank 2: 3 terms, Rank 3: 6 terms, Rank 4: 12 terms, Rank 5: 24 terms).
    /// - Automatic ration consumption from GuInventoryState.FoodStockpile.
    /// - Satiety degradation (-30% per missed cycle) and Deep Hibernation.
    /// - Parasitic/Cannibalistic Backlash: Starving predatory Gu drain host Primeval Sea.
    /// - Manual Gu feeding intent processing (CQRS-Lite).
    /// </summary>
    public class GuLifecycleService : IDisposable
    {
        private GameStateContext? _context;
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;

        public void Initialize(GameStateContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _isInitialized = true;

            GameEventManager.OnSolarTermAdvanced += HandleSolarTermAdvanced;
            GameEventManager.OnFeedGuRequested   += HandleFeedGuRequested;

            Debug.Log("[GuLifecycleService] Initialized. Gu feeding and starvation tracking active.");
        }

        public void Dispose()
        {
            GameEventManager.OnSolarTermAdvanced -= HandleSolarTermAdvanced;
            GameEventManager.OnFeedGuRequested   -= HandleFeedGuRequested;
            _isInitialized = false;
        }

        private void HandleSolarTermAdvanced(int year, SolarTerm term, string termName)
        {
            if (!_isInitialized) return;
            ProcessFeedingCycleForAllGu();
        }

        public void ProcessFeedingCycleForAllGu()
        {
            if (!_isInitialized || _context == null) return;

            var allGu = new List<GuWormData>(_context.GuInventory.EquippedGu);
            allGu.AddRange(_context.GuInventory.ReserveGu);

            int currentTermIndex = _context.Calendar.CurrentSolarTermIndex;

            foreach (var gu in allGu)
            {
                if (gu == null) continue;

                var def = GuDatabase.Get(gu.GuID);
                int rationAmount = 1;
                GuDietType dietType = gu.Diet;
                float degradation = 30f;

                if (def?.Diet != null)
                {
                    dietType = (GuDietType)def.Diet.DietType;
                    gu.Diet = dietType;
                    rationAmount = Mathf.Max(1, def.Diet.RationAmount);
                    degradation = def.Diet.StarvationGraceDays > 0 ? (100f / (def.Diet.StarvationGraceDays + 1)) : 35f;
                }

                int cadence = gu.Rank switch
                {
                    1 => 1,
                    2 => 3,
                    3 => 6,
                    4 => 12,
                    5 => 24,
                    _ => 1
                };

                if ((currentTermIndex % cadence) != 0)
                {
                    continue;
                }

                bool fed = _context.GuInventory.TryConsumeFood(dietType, rationAmount);
                gu.ApplyFeedingCycleResult(fed, degradation);

                var stage = gu.GetStarvationStage();

                if (fed)
                {
                    Debug.Log($"[GuLifecycleService] {gu.Name} consumed {rationAmount}x {dietType}. Satiety: 100%.");
                    GameEventManager.TriggerGuFed(gu.GuID, true, gu.Satiety);
                }
                else
                {
                    Debug.LogWarning($"[GuLifecycleService] {gu.Name} missed feeding ({rationAmount}x {dietType} missing)! Satiety: {gu.Satiety:F0}% | State: {stage}");
                    GameEventManager.TriggerGuFed(gu.GuID, false, gu.Satiety);
                    GameEventManager.TriggerGuStarvationChanged(gu.GuID, stage);

                    if (gu.IsPredatory && (stage == GuStarvationStage.Starving || stage == GuStarvationStage.Hibernating))
                    {
                        ApplyPredatoryBacklash(gu);
                    }
                }
            }
        }

        private void ApplyPredatoryBacklash(GuWormData predatoryGu)
        {
            if (_context == null) return;
            float essenceDrain = _context.Cultivator.AptitudePercent * 0.15f;
            _context.Cultivator.CurrentEssenceSea = Mathf.Max(0f, _context.Cultivator.CurrentEssenceSea - essenceDrain);

            Debug.LogError($"[PARASITIC BACKLASH] Starving Predatory Gu ({predatoryGu.Name}) devours host! Drained {essenceDrain:F1}% Primeval Sea!");
            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
        }

        private void HandleFeedGuRequested(string guId, GuDietType dietType, int quantity)
        {
            if (!_isInitialized || _context == null) return;

            var gu = _context.GuInventory.FindGu(guId);
            if (gu == null)
            {
                Debug.LogWarning($"[GuLifecycleService] Cannot feed unknown Gu '{guId}'");
                return;
            }

            var def = GuDatabase.Get(gu.GuID);
            GuDietType preferredDiet = def?.Diet != null ? (GuDietType)def.Diet.DietType : gu.Diet;
            if (dietType == GuDietType.None) dietType = preferredDiet;

            int neededRations = def?.Diet != null ? Mathf.Max(1, def.Diet.RationAmount) : 1;
            int toConsume = quantity > 0 ? quantity : neededRations;

            if (!_context.GuInventory.TryConsumeFood(dietType, toConsume))
            {
                Debug.LogWarning($"[GuLifecycleService] Insufficient food rations ({toConsume}x {dietType}) to feed {gu.Name}");
                GameEventManager.TriggerGuFed(guId, false, gu.Satiety);
                return;
            }

            float gain = (100f / neededRations) * toConsume;
            gu.Feed(gain);
            Debug.Log($"[GuLifecycleService] Manually fed {gu.Name} with {toConsume}x {dietType} (+{gain:F0}% Satiety). Satiety: {gu.Satiety:F0}%");

            GameEventManager.TriggerGuFed(guId, true, gu.Satiety);
            GameEventManager.TriggerGuStarvationChanged(guId, gu.GetStarvationStage());
            GameEventManager.TriggerGuInventoryUpdated(_context.GuInventory.EquippedGu, _context.GuInventory.ReserveGu);
        }
    }
}
