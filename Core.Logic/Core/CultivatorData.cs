using System;

namespace TaiwuGu.Core
{
    public enum AptitudeGrade
    {
        D_Grade, // 20-39%
        C_Grade, // 40-59%
        B_Grade, // 60-79%
        A_Grade  // 80-99%
    }

    /// <summary>
    /// Core Cultivator Data model adhering to canonical mathematical constraints.
    /// Manages Rank, Substage, Aptitude, Primeval Sea capacity, and Stone Recovery.
    /// </summary>
    [Serializable]
    public class CultivatorData
    {
        public const float BASE_STONE_ESSENCE = 15.4f; // Constant BEU per unranked Primeval Stone

        public string Name = "Gu Yue Fang Yuan";
        public AptitudeGrade Aptitude = AptitudeGrade.C_Grade;
        public float AptitudePercent = 44.0f; // Canon C-Grade 44%

        public int Rank = 1;                  // Rank 1-5 Mortal Gu Master
        public int Substage = 1;              // 1: Initial, 2: Middle, 3: Upper, 4: Peak

        public float CurrentEssenceSea;       // Current Primeval Sea level (%)
        public int PrimevalStones = 250;      // Universal cultivation currency
        public float CurrentNurtureProgress = 0f;

        public float WallWashingProgress = 0f;
        public float ApertureWallDurability = 100f;
        public float MaxWallDurability = 100f;
        public int HumanPathDaoMarks = 4;
        public float MaxHealth = 100f;
        public float CurrentHealth = 100f;

        public string GetMembraneTypeName()
        {
            return Rank switch
            {
                1 => "Light Membrane (光膜)",
                2 => "Water Membrane (水膜)",
                3 => "Stone Membrane (石膜)",
                4 => "Crystal Membrane (晶膜)",
                5 => "Illusory Membrane (幻膜)",
                _ => "Primeval Membrane"
            };
        }

        public CultivatorData()
        {
            CurrentEssenceSea = AptitudePercent;
            HumanPathDaoMarks = CultivatorState.DeriveInnateHumanPathMarks(AptitudePercent);
            MaxWallDurability = CultivatorState.GetMaxWallDurabilityForRank(Rank);
            ApertureWallDurability = MaxWallDurability;
        }

        public CultivatorData(string name, AptitudeGrade aptitude, float aptitudePercent)
        {
            Name = name;
            Aptitude = aptitude;
            AptitudePercent = Mathf.Clamp(aptitudePercent, 20f, 100f);
            CurrentEssenceSea = AptitudePercent;
            HumanPathDaoMarks = CultivatorState.DeriveInnateHumanPathMarks(AptitudePercent);
            MaxWallDurability = CultivatorState.GetMaxWallDurabilityForRank(Rank);
            ApertureWallDurability = MaxWallDurability;
        }

        public string GetSubstageName()
        {
            return Substage switch
            {
                1 => "Initial Stage (初阶)",
                2 => "Middle Stage (中阶)",
                3 => "Upper Stage (高阶)",
                4 => "Peak Stage (巅峰)",
                _ => "Initial Stage"
            };
        }

        public static double CalculateDensityCoefficient(int rank, int substage)
        {
            return Math.Pow(80.0, rank - 1) * Math.Pow(2.0, substage - 1);
        }

        public double GetCurrentDensity()
        {
            return CalculateDensityCoefficient(Rank, Substage);
        }

        public static float CalculateStoneRecoveryPercentage(int rank, int substage, float aptitude)
        {
            double density = CalculateDensityCoefficient(rank, substage);
            double deltaPct = (BASE_STONE_ESSENCE / (aptitude * density)) * 100.0;
            return (float)deltaPct;
        }

        public float GetSingleStoneRecovery()
        {
            return CalculateStoneRecoveryPercentage(Rank, Substage, AptitudePercent);
        }

        public static float CalculateSubstageNurtureCost(int rank, int currentSubstage, float aptitude)
        {
            float baseCost = 25f * Mathf.Pow(2.2f, rank - 1) * Mathf.Pow(1.8f, currentSubstage - 1);
            float aptitudeEfficiency = 100f / aptitude;
            return baseCost * aptitudeEfficiency;
        }

        public static float CalculateBreakthroughCost(int rank, float aptitude)
        {
            float baseCost = 150f * Mathf.Pow(3.5f, rank - 1);
            float aptitudeEfficiency = 100f / aptitude;
            return baseCost * aptitudeEfficiency;
        }

        public float CalculateEssenceCostPercentage(float baseEssenceCostUnits)
        {
            double density = GetCurrentDensity();
            return (float)(baseEssenceCostUnits / density);
        }

        public bool TryConsumeEssence(float baseEssenceCostUnits)
        {
            float realCost = CalculateEssenceCostPercentage(baseEssenceCostUnits);
            if (CurrentEssenceSea >= realCost)
            {
                CurrentEssenceSea -= realCost;
                return true;
            }
            return false;
        }

        public bool CanNurtureSubstage()
        {
            if (Substage >= 4) return false;
            float requiredCost = CalculateSubstageNurtureCost(Rank, Substage, AptitudePercent);
            return PrimevalStones >= Mathf.CeilToInt(requiredCost);
        }

        public bool TryNurtureSubstage()
        {
            if (!CanNurtureSubstage()) return false;
            float cost = CalculateSubstageNurtureCost(Rank, Substage, AptitudePercent);
            int stoneCost = Mathf.CeilToInt(cost);
            PrimevalStones -= stoneCost;
            Substage++;
            CurrentNurtureProgress = 0f;
            return true;
        }

        public bool CanBreakthroughMajorRank()
        {
            if (Substage != 4 || Rank >= 5) return false;
            float cost = CalculateBreakthroughCost(Rank, AptitudePercent);
            return PrimevalStones >= Mathf.CeilToInt(cost);
        }

        public bool TryBreakthroughMajorRank()
        {
            if (!CanBreakthroughMajorRank()) return false;
            float cost = CalculateBreakthroughCost(Rank, AptitudePercent);
            int stoneCost = Mathf.CeilToInt(cost);
            PrimevalStones -= stoneCost;
            Rank++;
            Substage = 1;
            CurrentNurtureProgress = 0f;
            CurrentEssenceSea = AptitudePercent;
            return true;
        }
    }
}
