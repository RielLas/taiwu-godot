#nullable enable
using System;

namespace TaiwuGu.Tags;

public enum PredicateOp : byte
{
    HasTag = 0,
    SumTagWeightGte = 1,
    HungerBelow = 2,
    DaoMarksGte = 3,
    StatGte = 4,
    AnyFedGuHasTag = 5,
    AlwaysBlock = 6
}

public enum WorldStatId : ushort
{
    None = 0,
    PrimevalStones = 1,
    Rank = 2,
    BountyHeat = 3,
    AvgGuHunger = 4
}

public readonly record struct PredicateClause(PredicateOp Op, ushort KeyId, int Threshold);

public readonly record struct PredicateRule(PredicateClause[] AllOf, PredicateClause[] AnyOf)
{
    public static readonly PredicateRule AlwaysPass = new(Array.Empty<PredicateClause>(), Array.Empty<PredicateClause>());
    public static readonly PredicateRule PermanentBlock = new(new[] { new PredicateClause(PredicateOp.AlwaysBlock, 0, 0) }, Array.Empty<PredicateClause>());
}

/// <summary>
/// Zero-GC snapshot of an active Gu in the player's aperture.
/// HungerPercent is 0 (sated) to 100 (starving); IsFed holds when HungerPercent &lt; PenaltyThresholdPercent and not dormant.
/// </summary>
public readonly record struct GuSnapshot(
    ushort GuId,
    int HungerPercent,
    int PenaltyThresholdPercent,
    bool IsDormant,
    DaoTagVector Tags)
{
    public bool IsFed => !IsDormant && HungerPercent < PenaltyThresholdPercent;
}

public readonly struct WorldQueryCtx
{
    public readonly ReadOnlyMemory<DaoTagWeight> DaoMarks;
    public readonly ReadOnlyMemory<GuSnapshot> ActiveGu;
    public readonly DaoTagVector PlayerTags;
    public readonly DaoTagVector ContextTags;
    public readonly int PrimevalStones;
    public readonly int Rank;
    public readonly int BountyHeat;
    public readonly int AvgGuHungerPercent;

    public WorldQueryCtx(
        ReadOnlyMemory<DaoTagWeight> daoMarks,
        ReadOnlyMemory<GuSnapshot> activeGu,
        int primevalStones,
        int rank,
        int bountyHeat,
        int avgGuHungerPercent = 0,
        DaoTagVector playerTags = default,
        DaoTagVector contextTags = default)
    {
        DaoMarks = daoMarks;
        ActiveGu = activeGu;
        PrimevalStones = primevalStones;
        Rank = rank;
        BountyHeat = bountyHeat;
        AvgGuHungerPercent = avgGuHungerPercent;
        PlayerTags = playerTags;
        ContextTags = contextTags;
    }
}
