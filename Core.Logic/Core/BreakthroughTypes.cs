using System;

namespace TaiwuGu.Core
{
    public enum BreakthroughState
    {
        Ready,              // At Peak 100%
        PoundingInSession,  // Actively pounding the membrane
        Succeeded,          // Membrane shattered -> Rank transcended
        FailedEssenceDry,   // Ran out of essence & stones -> Wall sealed back
        FailedRupture       // Turbulence reached 100% -> Internal hemorrhage
    }

    /// <summary>
    /// Immutable state snapshot transmitted across the CQRS event bus
    /// from domain service to presentation views.
    /// </summary>
    public struct BreakthroughSessionSnapshot
    {
        public BreakthroughState State;
        public int CurrentRank;
        public int TargetRank;
        public string MembraneName;
        public string TargetRankName;
        public float MembraneCurrentHP;
        public float MembraneMaxHP;
        public float MembraneHPRatio => MembraneMaxHP > 0f ? Math.Clamp(MembraneCurrentHP / MembraneMaxHP, 0f, 1f) : 0f;
        public float NaturalRegenRate;
        public bool IsRegenerating;
        public float CurrentEssenceSea;
        public float MaxEssenceCapacity;
        public float EssenceRatio => MaxEssenceCapacity > 0f ? Math.Clamp(CurrentEssenceSea / MaxEssenceCapacity, 0f, 1f) : 0f;
        public int PrimevalStones;
        public int HumanPathMarks;
        public float CurrentTurbulence;
        public float TurbulenceLimit;
        public float TurbulenceRatio => TurbulenceLimit > 0f ? Math.Clamp(CurrentTurbulence / TurbulenceLimit, 0f, 1f) : 0f;
        public float OscillationFrequency;
        public float SweetSpotWidth;
        public float NormalizedOscillation; // [0..1]
        public float RawSineValue;          // [-1..1]
        public float ElapsedSessionTime;
        public string StatusMessage;
    }
}
