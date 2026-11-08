using System;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 2: Calendar & Temporal Simulation Service.
    /// Manages:
    /// - Turn-based Action Point (AP) deduction per overworld movement step (-1 AP).
    /// - 24 Solar Terms monthly calendar rollover when AP exhausts (30 AP -> 0 AP).
    /// - Annual rollover (24 Solar Terms = 1 Cultivation Year).
    /// - Mutates WorldCalendarState inside GameStateContext.
    /// </summary>
    public class CalendarTimeService : IDisposable
    {
        private IGameStateProvider? _provider;
        private GameStateContext _context => _provider!.Current;
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;

        public void Initialize(GameStateContext context, IGameStateProvider? provider = null)
        {
            _provider = provider ?? new StaticGameStateProvider(context);
            _isInitialized = true;

            GameEventManager.OnPlayerMoved                 += HandlePlayerMoved;
            GameEventManager.OnAdvanceSolarTermRequested  += HandleAdvanceSolarTermRequested;
            GameEventManager.OnDeductActionPointsRequested += HandleDeductActionPointsRequested;

            Debug.Log($"[CalendarTimeService] Initialized. Year {_context.Calendar.CurrentYear}, {_context.Calendar.ActiveSolarTerm} | AP: {_context.Calendar.ActionPointsRemaining}/{_context.Calendar.MonthlyAPBudget}");
        }

        public void Dispose()
        {
            GameEventManager.OnPlayerMoved                 -= HandlePlayerMoved;
            GameEventManager.OnAdvanceSolarTermRequested  -= HandleAdvanceSolarTermRequested;
            GameEventManager.OnDeductActionPointsRequested -= HandleDeductActionPointsRequested;
            _isInitialized = false;
        }

        public void BroadcastCurrentState()
        {
            if (!_isInitialized || _context == null) return;

            GameEventManager.TriggerActionPointsChanged(_context.Calendar.ActionPointsRemaining, _context.Calendar.MonthlyAPBudget);
            // Views render the date and the seasonal resonance from events only, so the boot
            // broadcast has to carry them instead of leaving the UI to guess the opening term.
            GameEventManager.TriggerSolarTermAdvanced(_context.Calendar.CurrentYear, _context.Calendar.ActiveSolarTerm, _context.Calendar.GetSolarTermName());
        }

        private void HandlePlayerMoved(Vector2Int newGridPos)
        {
            if (!_isInitialized || _context == null) return;

            _context.WorldMap.PlayerGridPos = newGridPos;
            DeductActionPoints(1);
        }

        private void HandleAdvanceSolarTermRequested()
        {
            if (!_isInitialized) return;

            Debug.Log("[CalendarTimeService] Player elected to Pass Month.");
            RollOverMonth();
        }

        private void HandleDeductActionPointsRequested(int apCost)
        {
            DeductActionPoints(apCost);
        }

        public bool DeductActionPoints(int apCost)
        {
            if (!_isInitialized || _context == null || apCost <= 0) return true;

            _context.Calendar.ActionPointsRemaining = Mathf.Max(0, _context.Calendar.ActionPointsRemaining - apCost);
            GameEventManager.TriggerActionPointsChanged(_context.Calendar.ActionPointsRemaining, _context.Calendar.MonthlyAPBudget);

            if (_context.Calendar.ActionPointsRemaining <= 0)
            {
                RollOverMonth();
            }

            return true;
        }

        public void RollOverMonth()
        {
            if (!_isInitialized || _context == null) return;

            // 1. Reset AP to derived monthly budget
            _context.Calendar.ActionPointsRemaining = _context.Calendar.MonthlyAPBudget;

            // 2. Advance 24 Solar Terms
            _context.Calendar.CurrentSolarTermIndex++;
            if (_context.Calendar.CurrentSolarTermIndex > WorldCalendarState.TERMS_PER_YEAR)
            {
                _context.Calendar.CurrentSolarTermIndex = 1;
                _context.Calendar.CurrentYear++;

                // Canon Inviolate Lifespan Law (寿元天则): Cultivator ages 1 year at Lichun
                _context.Cultivator.AgeOneYear();
                GameEventManager.TriggerLifespanChanged(_context.Cultivator.Age, _context.Cultivator.LifespanYears, _context.Cultivator.RemainingLifespanYears);
                Debug.Log($"[Lifespan Law] New Year {_context.Calendar.CurrentYear} arrived! {_context.Cultivator.Name} is now Age {_context.Cultivator.Age} / {_context.Cultivator.LifespanYears} ({_context.Cultivator.RemainingLifespanYears} years remaining).");

                if (!_context.Cultivator.IsAlive)
                {
                    Debug.LogError($"[MORTALITY CEILING] {_context.Cultivator.Name} has reached the mortal lifespan limit ({_context.Cultivator.LifespanYears} years) without consuming Lifespan Gu! Natural death occurs!");
                }
            }

            // 3. Advance forensic dissipation (crimes fade over 2 Solar Terms)
            _context.Forensics.AdvanceTerms();

            Debug.Log($"[Taiwu Calendar] Solar Term Month Rollover: Year {_context.Calendar.CurrentYear}, {_context.Calendar.ActiveSolarTerm} ({_context.Calendar.GetSolarTermName()}) | AP: {_context.Calendar.ActionPointsRemaining}/{_context.Calendar.MonthlyAPBudget}");

            // 4. Broadcast State Changed Events
            GameEventManager.TriggerSolarTermAdvanced(_context.Calendar.CurrentYear, _context.Calendar.ActiveSolarTerm, _context.Calendar.GetSolarTermName());
            GameEventManager.TriggerActionPointsChanged(_context.Calendar.ActionPointsRemaining, _context.Calendar.MonthlyAPBudget);
        }
    }
}
