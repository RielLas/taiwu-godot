using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Psychology;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Shared identity, social personality, lifespan, and wealth component for BOTH Player and NPCs.
    /// </summary>
    [Serializable]
    public class CultivatorComponent
    {
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = "Unknown Cultivator";
        public string Clan { get; set; } = string.Empty;
        public string HomeSectorKey { get; set; } = string.Empty;
        public TaiwuDisposition Disposition { get; set; } = TaiwuDisposition.Pragmatic;
        public AptitudeGrade Aptitude { get; set; } = AptitudeGrade.C_Grade;
        public float AptitudePercent { get; set; } = 44.0f;

        // --- Inviolate Mortal Lifespan Law (寿元天则) ---
        public int Age { get; set; } = 15;
        public int LifespanYears { get; set; } = 100;
        public int LifespanGuConsumed { get; set; } = 0;
        public bool IsAlive => Age < LifespanYears;
        public int RemainingLifespanYears => Math.Max(0, LifespanYears - Age);

        // --- Economy & Social ---
        public int PrimevalStones { get; set; } = 120;
        public int Favorability { get; set; } = 0; // [-300 Nemesis .. +300 Revered]
        public bool SoulTabletBound { get; set; } = false;
        public string PersonalityTrait { get; set; } = "Gangzheng";
        public VengeancePursuitType PursuitState { get; set; } = VengeancePursuitType.None;
        public string TargetSlayerId { get; set; } = string.Empty;
        public GridCoord InvestigationTargetPos { get; set; }
        public List<string> MemoryLogs { get; set; } = new();

        public void ConsumeLifespanGu(int years)
        {
            LifespanYears += years;
            LifespanGuConsumed += years;
        }

        public void AgeOneYear() => Age++;

        public void AddMemory(string log) => MemoryLogs.Add($"[{DateTime.UtcNow:HH:mm:ss}] {log}");
    }

    /// <summary>
    /// Canonical Reverend Insanity mortal aperture (Rank 1-5) state for any cultivator entity.
    /// </summary>
    [Serializable]
    public class ApertureComponent
    {
        public const float BASE_STONE_ESSENCE = 15.4f;

        public int Rank { get; set; } = 1;
        public int Substage { get; set; } = 1;
        public float CurrentEssenceSea { get; set; } = 44.0f;
        public float CurrentNurtureProgress { get; set; } = 0f;
        public float WallWashingProgress { get; set; } = 0f;
        public float ApertureWallDurability { get; set; } = 100f;
        public float MaxWallDurability { get; set; } = 100f;
        public float MaxHealth { get; set; } = 100f;
        public float CurrentHealth { get; set; } = 100f;

        public static int DeriveInnateHumanPathMarks(float aptitudePercent)
        {
            if (aptitudePercent >= 100f) return 75;
            return Math.Clamp((int)MathF.Floor(aptitudePercent / 10f) - 1, 1, 9);
        }

        public static float GetMaxWallDurabilityForRank(int rank) => rank switch
        {
            1 => 100f,
            2 => 250f,
            3 => 500f,
            4 => 1000f,
            5 => 2000f,
            _ => 100f * rank
        };

        public string GetMembraneTypeName() => Rank switch
        {
            1 => "Light Membrane (光膜)",
            2 => "Water Membrane (水膜)",
            3 => "Stone Membrane (石膜)",
            4 => "Crystal Membrane (晶膜)",
            5 => "Illusory Membrane (幻膜)",
            _ => "Primeval Membrane"
        };

        public Color GetPrimevalSeaColor() => Rank switch
        {
            1 => new Color(0.13f, 0.77f, 0.37f, 1f),
            2 => new Color(0.94f, 0.27f, 0.27f, 1f),
            3 => new Color(0.95f, 0.96f, 0.98f, 1f),
            4 => new Color(0.92f, 0.70f, 0.03f, 1f),
            5 => new Color(0.66f, 0.33f, 0.97f, 1f),
            _ => new Color(0.13f, 0.77f, 0.37f, 1f)
        };

        public string GetSubstageName() => Substage switch
        {
            1 => "Initial Stage (初阶)",
            2 => "Middle Stage (中阶)",
            3 => "Upper Stage (高阶)",
            4 => "Peak Stage (巅峰)",
            _ => "Initial Stage"
        };

        public string GetRankDescription() => $"Rank {Rank} {GetSubstageName()}";

        public double GetDensityCoefficient() => CalculateDensityCoefficient(Rank, Substage);

        public static double CalculateDensityCoefficient(int rank, int substage)
            => Math.Pow(80.0, rank - 1) * Math.Pow(2.0, substage - 1);

        public static float CalculateStoneRecoveryPercentage(int rank, int substage, float aptitudePercent)
        {
            double density = CalculateDensityCoefficient(rank, substage);
            double deltaPct = (BASE_STONE_ESSENCE / (aptitudePercent * density)) * 100.0;
            return (float)deltaPct;
        }

        public float CalculateStoneRecoveryPercentage(float aptitudePercent)
            => CalculateStoneRecoveryPercentage(Rank, Substage, aptitudePercent);

        public static float CalculateSubstageNurtureCost(int rank, int currentSubstage, float aptitudePercent)
        {
            float baseCost = 25f * MathF.Pow(2.2f, rank - 1) * MathF.Pow(1.8f, currentSubstage - 1);
            float aptitudeEfficiency = 100f / aptitudePercent;
            return baseCost * aptitudeEfficiency;
        }

        public static float CalculateBreakthroughCost(int rank, float aptitudePercent)
        {
            float baseCost = 150f * MathF.Pow(3.5f, rank - 1);
            float aptitudeEfficiency = 100f / aptitudePercent;
            return baseCost * aptitudeEfficiency;
        }
    }

    [Serializable]
    public class DaoMarksComponent
    {
        public DaoMarksLedger Ledger { get; set; } = new();
    }
}
