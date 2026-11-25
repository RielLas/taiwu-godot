using System;
using Newtonsoft.Json;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Map;

namespace TaiwuGu.Entities
{
    [Serializable]
    public class GuRefinementRecipe
    {
        public string RecipeName { get; set; } = "Moonglow Gu Refinement (月芒蛊合炼)";
        public string PrimaryGuID { get; set; } = "gu_moonlight_r1";
        public string SecondaryGuID { get; set; } = "gu_white_boar_r1";
        public string ResultGuID { get; set; } = "gu_moonglow_r2";
        public string ResultGuName { get; set; } = "Moonglow Gu (月芒蛊)";
        public int TargetRank { get; set; } = 2;
        public GuSlotCategory SlotCategory { get; set; } = GuSlotCategory.Attack;
        public GuDietType Diet { get; set; } = GuDietType.MoonOrchidPetals;
        public float BaseEssenceCost { get; set; } = 18f;
        public float BaseSuccessRate { get; set; } = 0.85f;

        public float Phase1Duration { get; set; } = 4.0f;
        public float Phase2Duration { get; set; } = 4.5f;
        public float Phase3Duration { get; set; } = 3.5f;
        public float DecayRateK { get; set; } = 18.0f;
    }

    [Serializable]
    public class CauldronSessionComponent
    {
        public bool IsActive { get; set; } = false;
        public RefinementPhase CurrentPhase { get; set; } = RefinementPhase.Idle;
        public RefinementFailureTier LastFailureTier { get; set; } = RefinementFailureTier.None;
        public GuRefinementRecipe ActiveRecipe { get; set; } = new();

        public float CauldronTemperature { get; set; } = 50f;
        public float Phase1Timer { get; set; } = 0f;
        public float OutOfBoundsTimer { get; set; } = 0f;

        public float WildWillResistance { get; set; } = 50f;
        public float MasterWillFocus { get; set; } = 0.5f;
        public float WildWillFrequency { get; set; } = 0.5f;
        public float WillAlignmentProgress { get; set; } = 0f;
        public float Phase2Timer { get; set; } = 0f;
        public bool WildNeedleMovingRight { get; set; } = true;

        public float NascentStability { get; set; } = 100f;
        public float Phase3Timer { get; set; } = 0f;
        public int StonesCrushed { get; set; } = 0;

        public int CalculateMinStonesRequired()
        {
            if (ActiveRecipe == null) return 10;
            return Math.Max(1, (int)MathF.Ceiling((ActiveRecipe.Phase3Duration * ActiveRecipe.DecayRateK * ActiveRecipe.TargetRank) / 12f));
        }

        public void ResetToIdle()
        {
            IsActive = false;
            CurrentPhase = RefinementPhase.Idle;
            LastFailureTier = RefinementFailureTier.None;
            CauldronTemperature = 50f;
            Phase1Timer = 0f;
            OutOfBoundsTimer = 0f;
            WildWillResistance = 50f;
            MasterWillFocus = 0.5f;
            WildWillFrequency = 0.5f;
            WillAlignmentProgress = 0f;
            Phase2Timer = 0f;
            WildNeedleMovingRight = true;
            NascentStability = 100f;
            Phase3Timer = 0f;
            StonesCrushed = 0;
        }
    }

    [Serializable]
    public class BreakthroughSessionComponent
    {
        public const float TURBULENCE_LIMIT = 100f;

        public bool IsOpen { get; set; } = false;
        public BreakthroughState State { get; set; } = BreakthroughState.Ready;
        public int CurrentRank { get; set; } = 1;
        public float MembraneMaxHP { get; set; } = 100f;
        public float MembraneCurrentHP { get; set; } = 100f;
        public float MembraneNaturalRegenRate { get; set; } = 10f;
        public float CurrentTurbulence { get; set; } = 0f;
        public float OscillationFrequency { get; set; } = 1.2f;
        public float LastWaveTime { get; set; } = 0f;
        public float SessionTime { get; set; } = 0f;
        public int HumanPathMarks { get; set; } = 4;
        public string StatusMessage { get; set; } = string.Empty;

        public bool IsRegenerating =>
            IsOpen &&
            State == BreakthroughState.PoundingInSession &&
            (SessionTime - LastWaveTime > 1.2f) &&
            MembraneCurrentHP < MembraneMaxHP;
    }

    [Serializable]
    public class WildEncounterSessionComponent
    {
        public bool IsOpen { get; set; } = false;
        public SubjugationState State { get; set; } = SubjugationState.Inactive;
        public WildOrganismData? Target { get; set; }
        public float FeralWillResistance { get; set; } = 100f;
        public float PlayerWillAlignment { get; set; } = 0f;
        public float PlayerWillNeedle { get; set; } = 0.5f;
        public float FeralWillFrequency { get; set; } = 0.5f;
        public bool FeralNeedleRight { get; set; } = true;
        public float ClashTimer { get; set; } = 0f;
        public string LastResolutionMessage { get; set; } = string.Empty;
    }

    [Serializable]
    public class TransitPendingComponent
    {
        public bool IsModalOpen { get; set; } = false;
        public bool IsRegionalMode { get; set; } = false;
        public SouthernMountainID? PendingMountainDestination { get; set; }
        public string? PendingSectorKey { get; set; }
    }

    /// <summary>
    /// Attaches the live arena simulation to WorldStore.WorldEntity while persisting a lightweight
    /// summary so a save never serializes the flat combat buffers.
    /// </summary>
    [Serializable]
    public class CombatSessionComponent
    {
        [JsonIgnore] public CombatArenaSimulation Simulation { get; } = new();

        public bool InBattle { get; set; } = false;
        public ArenaEncounterType EncounterType { get; set; } = ArenaEncounterType.WildBoarHunt;
        public GridCoord SourceTilePos { get; set; }
        public string TargetNpcExternalId { get; set; } = string.Empty;
        public bool IsLethal { get; set; } = true;
    }
}
