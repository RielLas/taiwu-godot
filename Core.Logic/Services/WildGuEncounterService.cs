using System;
using TaiwuGu.Combat;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 2.5 Domain Service: Wild Organism Will Subjugation & Capture Engine (野性镇压与强行炼化引擎).
    /// Pure C# domain logic managing canonical rank differential logic, harmonic frequency oscillation,
    /// primeval essence pulse waves, instant realm overpower overwrites, and aperture inventory additions.
    /// Strictly decoupled under Constitutional Laws 1, 2, 4, and 6.
    /// </summary>
    public class WildGuEncounterService : IDisposable
    {
        private GameStateContext? _context;
        private SubjugationState _currentState = SubjugationState.Inactive;
        private bool _isEncounterOpen = false;

        private WildOrganismData? _currentTarget;
        private float _feralWillResistance = 100f; // [0..100] target to deplete
        private float _playerWillAlignment = 0f;    // [0..100] target to reach
        private float _playerWillNeedle = 0.5f;     // [0..1]
        private float _feralWillFrequency = 0.5f;   // [0..1] moves back and forth
        private bool _feralNeedleRight = true;
        private float _clashTimer = 0f;
        private string _lastResolutionMessage = string.Empty;

        public SubjugationState CurrentState => _currentState;
        public bool IsEncounterOpen => _isEncounterOpen;
        public WildOrganismData? CurrentTarget => _currentTarget;
        public float FeralResistance => _feralWillResistance;
        public float AlignmentProgress => _playerWillAlignment;
        public float PlayerNeedle => _playerWillNeedle;
        public float FeralFrequency => _feralWillFrequency;
        public string ResolutionMessage => _lastResolutionMessage;

        public void Initialize(GameStateContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            GameEventManager.OnOpenWildGuEncounterRequested += HandleOpenEncounter;
            GameEventManager.OnSetEncounterNeedleRequested += HandleSetNeedle;
            GameEventManager.OnAdjustEncounterNeedleRequested += HandleAdjustNeedle;
            GameEventManager.OnEncounterPulseEssenceRequested += HandlePulseEssence;
            GameEventManager.OnEncounterInstantSubjugateRequested += HandleInstantSubjugate;
            GameEventManager.OnCloseWildGuEncounterRequested += HandleCloseEncounter;

            Debug.Log("[WildGuEncounterService] Initialized and listening for wild encounters.");
        }

        public void Dispose()
        {
            GameEventManager.OnOpenWildGuEncounterRequested -= HandleOpenEncounter;
            GameEventManager.OnSetEncounterNeedleRequested -= HandleSetNeedle;
            GameEventManager.OnAdjustEncounterNeedleRequested -= HandleAdjustNeedle;
            GameEventManager.OnEncounterPulseEssenceRequested -= HandlePulseEssence;
            GameEventManager.OnEncounterInstantSubjugateRequested -= HandleInstantSubjugate;
            GameEventManager.OnCloseWildGuEncounterRequested -= HandleCloseEncounter;
            _context = null;
        }

        public int CalculateRankDifferential()
        {
            if (_context?.Cultivator == null || _currentTarget == null) return 0;
            return _context.Cultivator.Rank - _currentTarget.Rank;
        }

        public bool IsInstantRefineAvailable()
        {
            if (_context?.Cultivator == null || _currentTarget == null) return false;
            return CalculateRankDifferential() >= 3;
        }

        public float GetHarmonicTolerance()
        {
            int deltaR = CalculateRankDifferential();
            if (deltaR >= 2) return 0.35f;
            if (deltaR == 1) return 0.25f;
            if (deltaR == 0) return 0.16f;
            if (deltaR == -1) return 0.08f;
            return 0.04f; // Extremely narrow for deltaR <= -2
        }

        public float GetFeralSpeed()
        {
            if (_currentTarget == null) return 1.0f;
            float baseSpeed = _currentTarget.Temperament switch
            {
                WildOrganismTemperament.Docile => 0.8f,
                WildOrganismTemperament.Ferocious => 1.4f,
                WildOrganismTemperament.Cunning => 1.8f,
                _ => 1.0f
            };

            int deltaR = CalculateRankDifferential();
            if (deltaR >= 2) baseSpeed *= 0.6f;
            else if (deltaR <= -1) baseSpeed *= 1.5f;

            return baseSpeed;
        }

        public SubjugationSessionSnapshot CreateSnapshot()
        {
            return new SubjugationSessionSnapshot(
                _currentTarget ?? new WildOrganismData(),
                CalculateRankDifferential(),
                _currentState,
                _feralWillResistance,
                _playerWillAlignment,
                _playerWillNeedle,
                _feralWillFrequency,
                GetHarmonicTolerance(),
                _clashTimer,
                _lastResolutionMessage,
                IsInstantRefineAvailable()
            );
        }

        private void HandleOpenEncounter(WildOrganismData target)
        {
            if (target == null || _context?.Cultivator == null) return;

            _currentTarget = target;
            _isEncounterOpen = true;
            _currentState = SubjugationState.Clashing;
            _lastResolutionMessage = string.Empty;

            _feralWillResistance = target.FeralWillPower;
            _playerWillAlignment = 0f;
            _playerWillNeedle = 0.5f;
            _feralWillFrequency = (float)(0.35 + Random.Shared.NextDouble() * 0.30);
            _clashTimer = 0f;
            _feralNeedleRight = Random.Shared.Next(0, 2) == 1;

            Debug.Log($"[WildGuEncounterService] Discovered {_currentTarget.Name} (Rank {_currentTarget.Rank}, {_currentTarget.Temperament})! Player Rank: {_context.Cultivator.Rank} (ΔR: {CalculateRankDifferential()})");

            GameEventManager.TriggerUIModalActive(true);
            GameEventManager.TriggerWildGuEncounterOpened(CreateSnapshot());
        }

        private void HandleSetNeedle(float position)
        {
            _playerWillNeedle = Math.Clamp(position, 0f, 1f);
        }

        private void HandleAdjustNeedle(float delta)
        {
            _playerWillNeedle = Math.Clamp(_playerWillNeedle + delta, 0f, 1f);
        }

        private void HandlePulseEssence()
        {
            if (_currentState != SubjugationState.Clashing || _context?.Cultivator == null || _currentTarget == null) return;

            float cost = _context.Cultivator.AptitudePercent * 0.04f;
            if (_context.Cultivator.CurrentEssenceSea < cost)
            {
                Debug.LogWarning("[WildGuEncounterService] Insufficient Primeval Essence to release an infusion wave!");
                return;
            }

            _context.Cultivator.CurrentEssenceSea = Math.Max(0f, _context.Cultivator.CurrentEssenceSea - cost);
            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);

            float dist = Math.Abs(_playerWillNeedle - _feralWillFrequency);
            float tol = GetHarmonicTolerance();

            if (dist <= tol)
            {
                // Resonant strike
                float damage = 22f + (CalculateRankDifferential() * 6f);
                _feralWillResistance = Math.Max(0f, _feralWillResistance - damage);
                _playerWillAlignment = Math.Min(100f, _playerWillAlignment + 18f);
                Debug.Log($"[WildGuEncounterService] Perfect harmonic alignment! Wild will suppressed -{damage:F0}!");
            }
            else
            {
                // Discordant pulse - feral will recoils
                _feralWillResistance = Math.Min(100f, _feralWillResistance + 8f);
                Debug.LogWarning("[WildGuEncounterService] Pulse missed harmonic frequency! Creature agitated!");
            }

            CheckWinLossConditions();
            GameEventManager.TriggerWildGuEncounterStateUpdated(CreateSnapshot());
        }

        private void HandleInstantSubjugate()
        {
            if (_currentState != SubjugationState.Clashing || _context?.Cultivator == null || _currentTarget == null || !IsInstantRefineAvailable())
                return;

            float cost = _context.Cultivator.AptitudePercent * 0.01f;
            _context.Cultivator.CurrentEssenceSea = Math.Max(0f, _context.Cultivator.CurrentEssenceSea - cost);
            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);

            ResolveSuccess("Instantaneous Will Overwrite (阶位秒炼)! The creature's primitive consciousness is effortlessly crushed by absolute high-rank realm majesty.");
        }

        private void HandleCloseEncounter()
        {
            _isEncounterOpen = false;
            _currentState = SubjugationState.Inactive;
            GameEventManager.TriggerUIModalActive(false);
            GameEventManager.TriggerWildGuEncounterClosed();
        }

        public void Tick(float dt)
        {
            if (!_isEncounterOpen || _currentState != SubjugationState.Clashing || _context?.Cultivator == null || _currentTarget == null)
                return;

            _clashTimer += dt;

            // Update Feral Frequency oscillation
            float speed = GetFeralSpeed() * dt;
            if (_feralNeedleRight)
            {
                _feralWillFrequency += speed;
                if (_feralWillFrequency >= 0.88f) _feralNeedleRight = false;
            }
            else
            {
                _feralWillFrequency -= speed;
                if (_feralWillFrequency <= 0.12f) _feralNeedleRight = true;
            }

            // Continuous passive alignment accumulation
            float dist = Math.Abs(_playerWillNeedle - _feralWillFrequency);
            float tol = GetHarmonicTolerance();

            if (dist <= tol)
            {
                _playerWillAlignment += (24f + CalculateRankDifferential() * 6f) * dt;
                _feralWillResistance = Math.Max(0f, _feralWillResistance - 15f * dt);
            }
            else
            {
                float regen = (_currentTarget.Temperament == WildOrganismTemperament.Ferocious ? 12f : 6f) * dt;
                _feralWillResistance = Math.Min(100f, _feralWillResistance + regen);
            }

            CheckWinLossConditions();
            GameEventManager.TriggerWildGuEncounterStateUpdated(CreateSnapshot());
        }

        private void CheckWinLossConditions()
        {
            if (_feralWillResistance <= 0f || _playerWillAlignment >= 100f)
            {
                ResolveSuccess("Wild consciousness subdued! Feral imprint dissolved into aperture obedience.");
            }
            else if (_feralWillResistance >= 100f && _clashTimer > 3.0f)
            {
                ResolveBacklash("Wild Will Outburst (野性噬主)! Creature violently rejected your essence infusion, damaging aperture meridians!");
            }
        }

        private void ResolveSuccess(string reason)
        {
            _currentState = SubjugationState.Success;
            _lastResolutionMessage = reason;

            if (_currentTarget == null || _context?.GuInventory == null) return;

            // Generate tamed Gu worm into inventory loadout/reserve
            GuWormData newGu = new GuWormData
            {
                GuID = _currentTarget.OrganismID,
                Name = _currentTarget.Name,
                Rank = _currentTarget.Rank,
                Slot = _currentTarget.SlotCategory,
                Diet = _currentTarget.Diet,
                BaseEssenceCost = _currentTarget.BaseEssenceCost,
                Satiety = 30f, // Starts hungry demanding immediate food
                IsVitalGu = false,
                HasHostileWill = false
            };

            _context.GuInventory.ReserveGu.Add(newGu);
            GameEventManager.TriggerGuInventoryUpdated(_context.GuInventory.EquippedGu, _context.GuInventory.ReserveGu);

            Debug.Log($"[WildGuEncounterService] [SUBJUGATION SUCCESS] {_currentTarget.Name} successfully refined into Aperture! Added to Reserve Gu.");
            GameEventManager.TriggerWildGuEncounterResolved(_currentState, newGu, reason);
        }

        private void ResolveBacklash(string reason)
        {
            _currentState = SubjugationState.FailedBacklash;
            _lastResolutionMessage = reason;

            if (_context?.Cultivator != null)
            {
                float backlash = _context.Cultivator.AptitudePercent * 0.20f;
                _context.Cultivator.CurrentEssenceSea = Math.Max(0f, _context.Cultivator.CurrentEssenceSea - backlash);
                GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
            }

            Debug.LogWarning($"[WildGuEncounterService] [SUBJUGATION BACKLASH] {reason}");
            GameEventManager.TriggerWildGuEncounterResolved(_currentState, null, reason);
        }
    }
}
