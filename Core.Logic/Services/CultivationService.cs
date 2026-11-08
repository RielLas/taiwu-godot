using System;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 2: Cultivation & Primeval Sea Management Service.
    /// Implements canonical Reverend Insanity cultivation math:
    /// - Density Coefficient: Q(R, S) = 80^(R-1) * 2^(S-1)
    /// - Standardized Primeval Stone Recovery: Recovery% = (BaseStoneEssence / (Aptitude * Q(R, S))) * 100%
    /// - Substage Nurture & Major Rank Breakthrough costs in Primeval Stones.
    /// - Natural aperture essence regeneration on Solar Term advance.
    /// </summary>
    public class CultivationService : IDisposable
    {
        private IGameStateProvider? _provider;
        private GameStateContext _context => _provider!.Current;
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;

        public void Initialize(GameStateContext context, IGameStateProvider? provider = null)
        {
            _provider = provider ?? new StaticGameStateProvider(context);
            _isInitialized = true;

            GameEventManager.OnConsumeStoneRequested          += HandleConsumeStone;
            GameEventManager.OnNurtureSubstageRequested       += HandleNurtureSubstage;
            GameEventManager.OnBreakthroughMajorRankRequested += HandleBreakthroughMajorRank;
            GameEventManager.OnMeditateRequested              += HandleMeditate;
            GameEventManager.OnWashApertureWallRequested      += HandleWashApertureWall;
            GameEventManager.OnBatterApertureWallRequested    += HandleBatterApertureWall;
            GameEventManager.OnSolarTermAdvanced              += HandleSolarTermAdvanced;

            if (_context.Cultivator.MaxWallDurability <= 0f)
            {
                _context.Cultivator.MaxWallDurability = CultivatorState.GetMaxWallDurabilityForRank(_context.Cultivator.Rank);
                _context.Cultivator.ApertureWallDurability = _context.Cultivator.MaxWallDurability;
            }
            if (_context.Cultivator.HumanPathDaoMarks <= 0)
            {
                _context.Cultivator.HumanPathDaoMarks = CultivatorState.DeriveInnateHumanPathMarks(_context.Cultivator.AptitudePercent);
                _context.Cultivator.DaoMarks.HumanPathMarks = _context.Cultivator.HumanPathDaoMarks;
            }

            Debug.Log($"[CultivationService] Initialized. {_context.Cultivator.Name} | {_context.Cultivator.GetRankDescription()} | Essence: {_context.Cultivator.CurrentEssenceSea:F1}%/{_context.Cultivator.AptitudePercent:F1}% | Wall: {_context.Cultivator.GetMembraneTypeName()} | Human Dao Marks: {_context.Cultivator.HumanPathDaoMarks}");
        }

        public void Dispose()
        {
            GameEventManager.OnConsumeStoneRequested          -= HandleConsumeStone;
            GameEventManager.OnNurtureSubstageRequested       -= HandleNurtureSubstage;
            GameEventManager.OnBreakthroughMajorRankRequested -= HandleBreakthroughMajorRank;
            GameEventManager.OnMeditateRequested              -= HandleMeditate;
            GameEventManager.OnWashApertureWallRequested      -= HandleWashApertureWall;
            GameEventManager.OnBatterApertureWallRequested    -= HandleBatterApertureWall;
            GameEventManager.OnSolarTermAdvanced              -= HandleSolarTermAdvanced;
            _isInitialized = false;
        }

        public void BroadcastCurrentState()
        {
            if (!_isInitialized || _context == null) return;

            GameEventManager.TriggerCultivatorIdentityChanged(_context.Cultivator.Name);
            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
            GameEventManager.TriggerPrimevalStonesChanged(_context.Cultivator.PrimevalStones);
            GameEventManager.TriggerCultivatorCultivationChanged(_context.Cultivator.Rank, _context.Cultivator.Substage, _context.Cultivator.GetSubstageName());
            GameEventManager.TriggerApertureWallStateChanged(_context.Cultivator.WallWashingProgress, _context.Cultivator.ApertureWallDurability, _context.Cultivator.MaxWallDurability, _context.Cultivator.GetMembraneTypeName());
            GameEventManager.TriggerHumanPathDaoMarksChanged(_context.Cultivator.HumanPathDaoMarks);
            GameEventManager.TriggerLifespanChanged(_context.Cultivator.Age, _context.Cultivator.LifespanYears, _context.Cultivator.RemainingLifespanYears);
        }

        private void HandleConsumeStone()
        {
            if (!_isInitialized || _context == null) return;

            if (_context.Cultivator.PrimevalStones < 1)
            {
                Debug.LogWarning("[CultivationService] Insufficient Primeval Stones!");
                return;
            }

            if (_context.Cultivator.CurrentEssenceSea >= _context.Cultivator.AptitudePercent)
            {
                Debug.LogWarning("[CultivationService] Primeval Sea is already at maximum capacity for current aperture!");
                return;
            }

            _context.Cultivator.PrimevalStones--;
            float recoveryPct = _context.Cultivator.CalculateStoneRecoveryPercentage();
            _context.Cultivator.CurrentEssenceSea = Mathf.Min(_context.Cultivator.AptitudePercent, _context.Cultivator.CurrentEssenceSea + recoveryPct);

            Debug.Log($"[CultivationService] Crushed 1 Primeval Stone. Recovered +{recoveryPct:F2}% essence. Sea: {_context.Cultivator.CurrentEssenceSea:F1}%/{_context.Cultivator.AptitudePercent:F1}%");

            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
            GameEventManager.TriggerPrimevalStonesChanged(_context.Cultivator.PrimevalStones);
        }

        private void HandleMeditate()
        {
            if (!_isInitialized || _context == null) return;

            if (_context.Calendar.ActionPointsRemaining < 1)
            {
                Debug.LogWarning("[CultivationService] Insufficient Action Points / Stamina to meditate! Wait for next Solar Term or rest.");
                return;
            }

            if (_context.Cultivator.CurrentEssenceSea >= _context.Cultivator.AptitudePercent)
            {
                Debug.LogWarning("[CultivationService] Primeval Sea is already at its peak innate capacity!");
                return;
            }

            GameEventManager.RaiseRequestDeductActionPoints(1);

            float recovery = (_context.Cultivator.AptitudePercent * 0.25f);
            _context.Cultivator.CurrentEssenceSea = Mathf.Min(_context.Cultivator.AptitudePercent, _context.Cultivator.CurrentEssenceSea + recovery);

            Debug.Log($"[CultivationService] Deep Meditation completed (-1 AP). Primeval Sea replenished +{recovery:F1}%. Current: {_context.Cultivator.CurrentEssenceSea:F1}%/{_context.Cultivator.AptitudePercent:F1}%");

            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
        }

        private void HandleWashApertureWall()
        {
            if (!_isInitialized || _context == null) return;

            if (_context.Cultivator.Substage >= 4)
            {
                Debug.LogWarning("[CultivationService] Aperture Wall is fully solidified at Peak Stage. Major Rank breakthrough required!");
                return;
            }

            if (_context.Calendar.ActionPointsRemaining < 1)
            {
                Debug.LogWarning("[CultivationService] Insufficient Action Points / Stamina to wash aperture wall!");
                return;
            }

            if (_context.Cultivator.CurrentEssenceSea < 15f)
            {
                Debug.LogWarning("[CultivationService] Primeval Sea too shallow (<15%) to wash aperture wall! Meditate or crush stones first.");
                return;
            }

            GameEventManager.RaiseRequestDeductActionPoints(1);
            _context.Cultivator.CurrentEssenceSea = Mathf.Max(0f, _context.Cultivator.CurrentEssenceSea - 15f);

            float baseGain = 18f;
            float daoBonus = _context.Cultivator.HumanPathDaoMarks * 0.5f;
            float aptitudeFactor = (_context.Cultivator.AptitudePercent / 50f);
            float progressGain = (baseGain + daoBonus) * aptitudeFactor;

            _context.Cultivator.WallWashingProgress += progressGain;

            if (_context.Cultivator.WallWashingProgress >= 100f)
            {
                _context.Cultivator.Substage++;
                _context.Cultivator.WallWashingProgress = 0f;
                Debug.Log($"[CultivationService] Substage Breakthrough! Wall tempered to {_context.Cultivator.GetRankDescription()}!");
                GameEventManager.TriggerCultivatorCultivationChanged(_context.Cultivator.Rank, _context.Cultivator.Substage, _context.Cultivator.GetSubstageName());
            }
            else
            {
                Debug.Log($"[CultivationService] Aperture Wall washed (+{progressGain:F1}%). Progress: {_context.Cultivator.WallWashingProgress:F1}%/100% | Sea remaining: {_context.Cultivator.CurrentEssenceSea:F1}%");
            }

            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
            GameEventManager.TriggerApertureWallStateChanged(_context.Cultivator.WallWashingProgress, _context.Cultivator.ApertureWallDurability, _context.Cultivator.MaxWallDurability, _context.Cultivator.GetMembraneTypeName());
        }

        private void HandleBatterApertureWall()
        {
            if (!_isInitialized || _context == null) return;

            if (_context.Cultivator.Substage != 4)
            {
                Debug.LogWarning("[CultivationService] Major rank breakthrough requires reaching Peak Stage (Substage 4) first!");
                return;
            }

            if (_context.Cultivator.Rank >= 5)
            {
                Debug.LogWarning("[CultivationService] Pinnacle of Mortal Realm reached! Transcending to Rank 6 Immortal requires Heavenly Tribulation.");
                return;
            }

            if (_context.Calendar.ActionPointsRemaining < 1)
            {
                Debug.LogWarning("[CultivationService] Insufficient Action Points / Stamina to summon tidal waves!");
                return;
            }

            if (_context.Cultivator.CurrentEssenceSea < 25f)
            {
                Debug.LogWarning("[CultivationService] Primeval Sea too calm (<25%) to summon battering waves! Recover essence first.");
                return;
            }

            GameEventManager.RaiseRequestDeductActionPoints(1);
            _context.Cultivator.CurrentEssenceSea = Mathf.Max(0f, _context.Cultivator.CurrentEssenceSea - 25f);

            float baseDamage = 35f;
            float seaForce = (_context.Cultivator.CurrentEssenceSea * 0.4f);
            float daoAmplification = 1.0f + (_context.Cultivator.HumanPathDaoMarks * 0.01f);
            float wallDamage = (baseDamage + seaForce) * daoAmplification;

            _context.Cultivator.ApertureWallDurability = Mathf.Max(0f, _context.Cultivator.ApertureWallDurability - wallDamage);

            Debug.Log($"[CultivationService] Tidal Waves battered the {_context.Cultivator.GetMembraneTypeName()}! -{wallDamage:F1} Durability (Remaining: {_context.Cultivator.ApertureWallDurability:F1}/{_context.Cultivator.MaxWallDurability:F1})");

            if (_context.Cultivator.ApertureWallDurability <= 0.01f)
            {
                _context.Cultivator.Rank++;
                _context.Cultivator.Substage = 1;
                _context.Cultivator.WallWashingProgress = 0f;
                _context.Cultivator.MaxWallDurability = CultivatorState.GetMaxWallDurabilityForRank(_context.Cultivator.Rank);
                _context.Cultivator.ApertureWallDurability = _context.Cultivator.MaxWallDurability;
                _context.Cultivator.CurrentEssenceSea = _context.Cultivator.AptitudePercent;

                Debug.Log($"[CultivationService] APERTURE MEMBRANE SHATTERED! Ascended to Rank {_context.Cultivator.Rank} Gu Master! New {_context.Cultivator.GetMembraneTypeName()} formed!");

                GameEventManager.TriggerCultivatorCultivationChanged(_context.Cultivator.Rank, _context.Cultivator.Substage, _context.Cultivator.GetSubstageName());
            }

            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
            GameEventManager.TriggerApertureWallStateChanged(_context.Cultivator.WallWashingProgress, _context.Cultivator.ApertureWallDurability, _context.Cultivator.MaxWallDurability, _context.Cultivator.GetMembraneTypeName());
        }

        private void HandleNurtureSubstage()
        {
            if (!_isInitialized || _context == null) return;

            if (_context.Cultivator.Substage < 4)
            {
                _context.Cultivator.Substage++;
                _context.Cultivator.WallWashingProgress = 0f;
                Debug.Log($"[CultivationService] Substage Breakthrough! Wall tempered to {_context.Cultivator.GetRankDescription()}!");
                GameEventManager.TriggerCultivatorCultivationChanged(_context.Cultivator.Rank, _context.Cultivator.Substage, _context.Cultivator.GetSubstageName());
            }
        }

        private void HandleBreakthroughMajorRank()
        {
            HandleBatterApertureWall();
        }

        private void HandleSolarTermAdvanced(int year, SolarTerm term, string termName)
        {
            if (!_isInitialized || _context == null) return;

            float naturalRegen = _context.Cultivator.AptitudePercent * 0.25f;
            _context.Cultivator.CurrentEssenceSea = Mathf.Min(_context.Cultivator.AptitudePercent, _context.Cultivator.CurrentEssenceSea + naturalRegen);

            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
        }

        public bool TryConsumeEssence(float baseEssenceCostUnits)
        {
            if (!_isInitialized || _context == null) return false;

            double density = _context.Cultivator.GetDensityCoefficient();
            float realCostPct = (float)(baseEssenceCostUnits / density);

            if (_context.Cultivator.CurrentEssenceSea >= realCostPct)
            {
                _context.Cultivator.CurrentEssenceSea -= realCostPct;
                GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
                return true;
            }

            return false;
        }
    }
}
