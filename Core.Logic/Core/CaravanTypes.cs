namespace TaiwuGu.Core
{
    public enum CaravanStockKind
    {
        Fodder,
        Gu,
        RoughOre
    }

    public enum CaravanOreTier
    {
        Low,
        Mid,
        High
    }

    public enum CaravanGambleOutcome
    {
        Refused,
        Slag,
        StoneRebate,
        FodderCache,
        FossilGu
    }

    /// <summary>
    /// Presentation-safe result of one stone-gambling cut. The roll itself is
    /// resolved by CaravanEconomySystem from the persisted session sequence.
    /// </summary>
    public readonly record struct CaravanGambleResult(
        CaravanGambleOutcome Outcome,
        string Message,
        int StonesAwarded,
        string? GuId,
        GuDietType FodderDiet,
        int FodderQuantity)
    {
        public bool Success => Outcome != CaravanGambleOutcome.Refused;
    }
}
