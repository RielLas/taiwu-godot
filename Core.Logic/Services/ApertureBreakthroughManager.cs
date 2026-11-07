using System;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 2 / Milestone 2.1: Tide-Pounding Major Realm Breakthrough Engine (Rank 1 Peak -> Rank 2 Initial, etc.).
    /// Pure Canonical Physics:
    /// - Rank 1 Aperture Wall: Brittle Green Glass Membrane (青铜光膜).
    /// - Compression & Release: Generating harmonic resonance waves breaks the membrane.
    /// - Foreign Turbulence: Crushing Primeval Stones mid-attempt restores essence but adds turbulence.
    /// - High Turbulence: Distorts timing frequency and risks aperture rupture.
    /// - Artifact Bypass: Green Copper Relic Gu (青铜舍利蛊) instantly succeeds with 0 loss.
    /// Pure C# Domain Service implementing CQRS pattern and IDisposable.
    /// </summary>
    public class ApertureBreakthroughManager : IDisposable
    {
        public const float TURBULENCE_LIMIT = 100f;

        private IGameStateProvider? _provider;
        private GameStateContext _context => _provider!.Current;
        private bool _isInitialized = false;

        private BreakthroughState _currentState = BreakthroughState.Ready;
        private float _membraneMaxHP = 100f;
        private float _membraneCurrentHP = 100f;
        private float _membraneNaturalRegenRate = 12f; // HP/sec when unpressured
        private float _currentTurbulence = 0f;         // [0..100]
        private float _oscillationFrequency = 1.2f;    // Natural aperture pulse Hz

        private bool _isMinigameOpen = false;
        private float _lastWaveTime = 0f;
        private float _sessionTime = 0f;
        private int _humanPathMarks = 4;
        private int _currentRank = 1;
        private readonly Random _rng = new Random();
        private string _statusMessage = string.Empty;

        public BreakthroughState CurrentState => _currentState;
        public float MembraneHPRatio => _membraneMaxHP > 0f ? Math.Clamp(_membraneCurrentHP / _membraneMaxHP, 0f, 1f) : 0f;
        public float TurbulenceRatio => Math.Clamp(_currentTurbulence / TURBULENCE_LIMIT, 0f, 1f);
        public float CurrentTurbulence => _currentTurbulence;
        public bool IsMinigameOpen => _isMinigameOpen;
        public bool IsRegenerating => _isMinigameOpen && _currentState == BreakthroughState.PoundingInSession && (_sessionTime - _lastWaveTime > 1.2f) && _membraneCurrentHP < _membraneMaxHP;

        public void Initialize(GameStateContext context, IGameStateProvider? provider = null)
        {
            _provider = provider ?? new StaticGameStateProvider(context);
            _isInitialized = true;

            GameEventManager.OnOpenBreakthroughMinigameRequested += HandleOpenMinigameRequested;
            GameEventManager.OnReleaseEssenceWaveRequested       += HandleReleaseEssenceWave;
            GameEventManager.OnBreakthroughCrushStoneRequested   += HandleCrushStoneRequested;
            GameEventManager.OnBreakthroughUseRelicGuRequested   += HandleUseRelicGuRequested;
            GameEventManager.OnCloseBreakthroughRequested        += HandleCloseSessionRequested;

            Debug.Log("[ApertureBreakthroughManager] Initialized domain service. Ready for breakthrough seclusion.");
        }

        public void Dispose()
        {
            GameEventManager.OnOpenBreakthroughMinigameRequested -= HandleOpenMinigameRequested;
            GameEventManager.OnReleaseEssenceWaveRequested       -= HandleReleaseEssenceWave;
            GameEventManager.OnBreakthroughCrushStoneRequested   -= HandleCrushStoneRequested;
            GameEventManager.OnBreakthroughUseRelicGuRequested   -= HandleUseRelicGuRequested;
            GameEventManager.OnCloseBreakthroughRequested        -= HandleCloseSessionRequested;

            if (_isMinigameOpen)
            {
                _isMinigameOpen = false;
                GameEventManager.TriggerUIModalActive(false);
                GameEventManager.TriggerBreakthroughSessionClosed();
            }
            _isInitialized = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_isInitialized || !_isMinigameOpen || _context?.Cultivator == null) return;

            _sessionTime += deltaTime;

            if (_currentState != BreakthroughState.PoundingInSession)
            {
                return;
            }

            var cult = _context.Cultivator;

            // 1. Rapid natural membrane regeneration if pressure lapses (>1.2s without wave release)
            if (_sessionTime - _lastWaveTime > 1.2f && _membraneCurrentHP < _membraneMaxHP)
            {
                _membraneCurrentHP = Math.Min(_membraneMaxHP, _membraneCurrentHP + _membraneNaturalRegenRate * deltaTime);
                cult.ApertureWallDurability = _membraneCurrentHP;
            }

            // 2. Slow natural dissipation of foreign turbulence
            if (_currentTurbulence > 0f)
            {
                _currentTurbulence = Math.Max(0f, _currentTurbulence - 2.5f * deltaTime);
            }

            // 3. Failure check: Essence depleted (<0.8%) & 0 stones
            if (cult.CurrentEssenceSea <= 0.8f && cult.PrimevalStones <= 0)
            {
                EndBreakthrough(BreakthroughState.FailedEssenceDry);
                return;
            }

            // Broadcast periodic state update
            BroadcastStateUpdate();
        }

        private void HandleOpenMinigameRequested()
        {
            if (!_isInitialized || _context?.Cultivator == null) return;
            var cult = _context.Cultivator;

            if (cult.Substage != 4)
            {
                Debug.LogWarning("[ApertureBreakthroughManager] Cannot initiate breakthrough: Cultivator must be at Peak Stage (Substage 4)!");
                return;
            }

            if (cult.Rank >= 5)
            {
                Debug.LogWarning("[ApertureBreakthroughManager] Pinnacle of Mortality Reached. Transcending to Rank 6 Gu Immortal requires Heavenly Tribulation.");
                return;
            }

            if (_context.Calendar != null && _context.Calendar.ActionPointsRemaining < 1)
            {
                Debug.LogWarning("[ApertureBreakthroughManager] Insufficient Action Points / Stamina to enter breakthrough seclusion!");
                return;
            }

            OpenBreakthroughSession();
        }

        public void OpenBreakthroughSession()
        {
            if (_context?.Cultivator == null) return;
            var cult = _context.Cultivator;

            _currentRank = cult.Rank;
            _humanPathMarks = cult.HumanPathDaoMarks > 0 ? cult.HumanPathDaoMarks : CultivatorState.DeriveInnateHumanPathMarks(cult.AptitudePercent);

            // Deduct 1 AP for entering the secluded chamber
            GameEventManager.RaiseRequestDeductActionPoints(1);

            _isMinigameOpen = true;
            GameEventManager.TriggerUIModalActive(true);

            // Scale membrane HP and rapid regen rate with Rank
            _membraneMaxHP = CultivatorState.GetMaxWallDurabilityForRank(_currentRank);
            _membraneCurrentHP = _membraneMaxHP;
            cult.MaxWallDurability = _membraneMaxHP;
            cult.ApertureWallDurability = _membraneCurrentHP;
            _membraneNaturalRegenRate = 10f * (1.0f + (_currentRank - 1) * 0.6f);
            _currentTurbulence = 0f;
            _sessionTime = 0f;
            _lastWaveTime = 0f;
            _currentState = BreakthroughState.PoundingInSession;
            _statusMessage = $"Entered Seclusion Chamber. Target: {cult.GetMembraneTypeName()} | Durability: {_membraneMaxHP:F0} HP";

            Debug.Log($"[ApertureBreakthroughManager] Entered Seclusion Chamber. Target: {cult.GetMembraneTypeName()} | Durability: {_membraneMaxHP:F0} | Regen: {_membraneNaturalRegenRate:F1} HP/s");

            var snapshot = BuildSnapshot();
            GameEventManager.TriggerBreakthroughSessionOpened(snapshot);
        }

        private void HandleCloseSessionRequested()
        {
            CloseSession();
        }

        public void CloseSession()
        {
            if (!_isMinigameOpen) return;

            _isMinigameOpen = false;
            GameEventManager.TriggerUIModalActive(false);
            GameEventManager.TriggerBreakthroughSessionClosed();
            Debug.Log("[ApertureBreakthroughManager] Exited Seclusion Chamber.");
        }

        private void HandleReleaseEssenceWave(float wavePowerFactor)
        {
            if (!_isMinigameOpen || _currentState != BreakthroughState.PoundingInSession) return;
            var cult = _context?.Cultivator;
            if (cult == null) return;

            // Essence cost: 12% of sea capacity scaled by wave power
            float cost = cult.AptitudePercent * 0.12f * Math.Clamp(wavePowerFactor, 0.2f, 1.0f);
            if (cult.CurrentEssenceSea < cost)
            {
                _statusMessage = "Insufficient Primeval Essence to surge an assault wave!";
                Debug.LogWarning($"[Breakthrough] {_statusMessage}");
                if (cult.CurrentEssenceSea <= 0.8f && cult.PrimevalStones <= 0)
                {
                    EndBreakthrough(BreakthroughState.FailedEssenceDry);
                }
                else
                {
                    BroadcastStateUpdate();
                }
                return;
            }

            cult.CurrentEssenceSea -= cost;
            _lastWaveTime = _sessionTime;

            // Calculate oscillation resonance
            // Sinusoidal wave: base frequency scaled, sweet spot broadened by Human Path marks
            float oscValue = MathF.Sin(_sessionTime * MathF.PI * 2f * _oscillationFrequency);
            float humanPrecisionBonus = Math.Clamp(_humanPathMarks * 0.015f, 0f, 0.20f);
            bool isHarmonic = oscValue >= (0.75f - humanPrecisionBonus);
            bool isDispersion = oscValue <= -0.45f;

            float baseDamage = 35f * (1.0f + (_currentRank - 1) * 0.5f);
            float damage;

            if (isHarmonic)
            {
                // Human Path Marks amplify resonance damage directly (e.g. +4% per mark)
                float markAmp = 1.0f + (_humanPathMarks * 0.04f);
                damage = baseDamage * 2.2f * markAmp * (1f + (_currentTurbulence / 250f));
                _statusMessage = $"【Harmonic Resonance!】 Tidal crash dealt {damage:F0} fracture damage! (Marks: {_humanPathMarks})";
                Debug.Log($"[Breakthrough] {_statusMessage}");
            }
            else if (isDispersion)
            {
                damage = baseDamage * 0.25f;
                _statusMessage = "【Essence Dispersion】 Poorly timed wave dispersed weakly against the resilient membrane.";
                Debug.LogWarning($"[Breakthrough] {_statusMessage}");
            }
            else
            {
                damage = baseDamage;
                _statusMessage = $"Tidal wave struck the membrane for {damage:F0} impact damage.";
            }

            _membraneCurrentHP = Math.Max(0f, _membraneCurrentHP - damage);
            cult.ApertureWallDurability = _membraneCurrentHP;
            cult.MaxWallDurability = _membraneMaxHP;

            GameEventManager.TriggerBreakthroughWaveImpact(isHarmonic, isDispersion, damage);
            GameEventManager.TriggerApertureWallStateChanged(cult.WallWashingProgress, cult.ApertureWallDurability, cult.MaxWallDurability, cult.GetMembraneTypeName());
            GameEventManager.TriggerPrimevalSeaChanged(cult.CurrentEssenceSea, cult.AptitudePercent);

            // Breakthrough success check!
            if (_membraneCurrentHP <= 0.001f)
            {
                EndBreakthrough(BreakthroughState.Succeeded);
            }
            else
            {
                BroadcastStateUpdate();
            }
        }

        private void HandleCrushStoneRequested()
        {
            if (!_isMinigameOpen || _currentState != BreakthroughState.PoundingInSession) return;
            var cult = _context?.Cultivator;
            if (cult == null) return;

            if (cult.PrimevalStones <= 0)
            {
                _statusMessage = "Primeval Stone stockpile exhausted!";
                Debug.LogWarning($"[Breakthrough] {_statusMessage}");
                BroadcastStateUpdate();
                return;
            }

            cult.PrimevalStones--;

            // Standard recovery: Recovery% = (15.4 / (Aptitude * Q(R, S))) * 100
            float recovery = cult.CalculateStoneRecoveryPercentage();
            cult.CurrentEssenceSea = Math.Min(cult.AptitudePercent, cult.CurrentEssenceSea + recovery);

            // Accumulate Foreign Turbulence (12-18 per stone, slightly dampened by Human Path Marks)
            float turbulenceDamping = Math.Clamp(_humanPathMarks * 0.5f, 0f, 6f);
            float randGain = 12f + (float)(_rng.NextDouble() * 6.0);
            float turbulenceGain = Math.Max(5f, randGain - turbulenceDamping);
            _currentTurbulence += turbulenceGain;

            _statusMessage = $"Crushed Stone: +{recovery:F1}% Essence restored! Foreign Turbulence: {_currentTurbulence:F0}/{TURBULENCE_LIMIT:F0}";
            Debug.Log($"[Breakthrough] {_statusMessage}");

            GameEventManager.TriggerPrimevalSeaChanged(cult.CurrentEssenceSea, cult.AptitudePercent);
            GameEventManager.TriggerPrimevalStonesChanged(cult.PrimevalStones);

            // Turbulence rupture check
            if (_currentTurbulence >= TURBULENCE_LIMIT)
            {
                EndBreakthrough(BreakthroughState.FailedRupture);
                return;
            }

            BroadcastStateUpdate();
        }

        private void HandleUseRelicGuRequested()
        {
            if (!_isMinigameOpen) return;
            TryUseRelicGuBypass();
        }

        public bool TryUseRelicGuBypass()
        {
            var cult = _context?.Cultivator;
            if (cult == null || cult.Substage != 4 || cult.Rank >= 5)
            {
                _statusMessage = "Cannot use Relic Gu at this stage.";
                return false;
            }

            int prevRank = cult.Rank;
            cult.Rank += 1;
            cult.Substage = 1;
            cult.CurrentEssenceSea = cult.AptitudePercent;
            cult.WallWashingProgress = 0f;
            cult.MaxWallDurability = CultivatorState.GetMaxWallDurabilityForRank(cult.Rank);
            cult.ApertureWallDurability = cult.MaxWallDurability;
            _membraneMaxHP = cult.MaxWallDurability;
            _membraneCurrentHP = cult.MaxWallDurability;

            _statusMessage = $"Consuming Relic Gu! Transcended from Rank {prevRank} to Rank {cult.Rank} {cult.GetMembraneTypeName()}!";
            Debug.Log($"[Breakthrough] {_statusMessage}");

            EndBreakthrough(BreakthroughState.Succeeded);
            return true;
        }

        private void EndBreakthrough(BreakthroughState result)
        {
            _currentState = result;
            var cult = _context?.Cultivator;

            if (result == BreakthroughState.Succeeded && cult != null)
            {
                if (cult.Substage == 4) // if not already promoted by relic
                {
                    cult.Rank = Math.Min(5, cult.Rank + 1);
                    cult.Substage = 1;
                    cult.CurrentEssenceSea = cult.AptitudePercent;
                    cult.WallWashingProgress = 0f;
                    cult.MaxWallDurability = CultivatorState.GetMaxWallDurabilityForRank(cult.Rank);
                    cult.ApertureWallDurability = cult.MaxWallDurability;
                }

                _statusMessage = $"★★★ MAJOR REALM TRANSCENDED ★★★ Ascended to Rank {cult.Rank} ({cult.GetMembraneTypeName()})!";
                Debug.Log($"[Breakthrough] {_statusMessage}");

                GameEventManager.TriggerCultivatorCultivationChanged(cult.Rank, cult.Substage, cult.GetSubstageName());
                GameEventManager.TriggerApertureWallStateChanged(cult.WallWashingProgress, cult.ApertureWallDurability, cult.MaxWallDurability, cult.GetMembraneTypeName());
                GameEventManager.TriggerPrimevalSeaChanged(cult.CurrentEssenceSea, cult.AptitudePercent);
            }
            else if (result == BreakthroughState.FailedRupture && cult != null)
            {
                // Severe Somatic Hemorrhage
                cult.CurrentEssenceSea = 0f;
                cult.ApertureWallDurability = cult.MaxWallDurability;
                _membraneCurrentHP = _membraneMaxHP;
                _statusMessage = "【APERTURE RUPTURE】 Excessive Foreign Turbulence caused catastrophic essence feedback! Gu Master vomits blood and collapses!";
                Debug.LogError($"[Breakthrough] {_statusMessage}");

                GameEventManager.TriggerPrimevalSeaChanged(cult.CurrentEssenceSea, cult.AptitudePercent);
                GameEventManager.TriggerApertureWallStateChanged(cult.WallWashingProgress, cult.ApertureWallDurability, cult.MaxWallDurability, cult.GetMembraneTypeName());
            }
            else if (result == BreakthroughState.FailedEssenceDry && cult != null)
            {
                cult.ApertureWallDurability = cult.MaxWallDurability;
                _membraneCurrentHP = _membraneMaxHP;
                _statusMessage = "【Essence Depleted】 The Primeval Sea ran dry before the membrane cracked. The wall has rapidly healed itself shut.";
                Debug.LogWarning($"[Breakthrough] {_statusMessage}");

                GameEventManager.TriggerApertureWallStateChanged(cult.WallWashingProgress, cult.ApertureWallDurability, cult.MaxWallDurability, cult.GetMembraneTypeName());
            }

            GameEventManager.TriggerBreakthroughResolved(result, cult?.Rank ?? _currentRank);
            BroadcastStateUpdate();
        }

        private void BroadcastStateUpdate()
        {
            var snapshot = BuildSnapshot();
            GameEventManager.TriggerBreakthroughStateUpdated(snapshot);
        }

        public BreakthroughSessionSnapshot BuildSnapshot()
        {
            var cult = _context?.Cultivator;
            float rawSine = MathF.Sin(_sessionTime * MathF.PI * 2f * _oscillationFrequency);
            float normalizedOsc = (rawSine + 1f) * 0.5f;
            float sweetWidth = Math.Clamp(0.15f + (_humanPathMarks * 0.005f), 0.15f, 0.35f);

            int nextRank = Math.Min(5, _currentRank + 1);
            string nextRankName = nextRank switch
            {
                2 => "Pale Red Iron (赤铁初阶)",
                3 => "White Silver (雪银初阶)",
                4 => "Yellow Gold (黄金初阶)",
                _ => "Crystal Purple (紫晶初阶)"
            };

            return new BreakthroughSessionSnapshot
            {
                State = _currentState,
                CurrentRank = _currentRank,
                TargetRank = nextRank,
                MembraneName = cult?.GetMembraneTypeName() ?? "Bronze Membrane (青铜光膜)",
                TargetRankName = nextRankName,
                MembraneCurrentHP = _membraneCurrentHP,
                MembraneMaxHP = _membraneMaxHP,
                NaturalRegenRate = _membraneNaturalRegenRate,
                IsRegenerating = IsRegenerating,
                CurrentEssenceSea = cult?.CurrentEssenceSea ?? 0f,
                MaxEssenceCapacity = cult?.AptitudePercent ?? 100f,
                PrimevalStones = cult?.PrimevalStones ?? 0,
                HumanPathMarks = _humanPathMarks,
                CurrentTurbulence = _currentTurbulence,
                TurbulenceLimit = TURBULENCE_LIMIT,
                OscillationFrequency = _oscillationFrequency,
                SweetSpotWidth = sweetWidth,
                NormalizedOscillation = normalizedOsc,
                RawSineValue = rawSine,
                ElapsedSessionTime = _sessionTime,
                StatusMessage = _statusMessage
            };
        }
    }
}
