#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace TaiwuGu.Tags;

/// <summary>
/// Single stateless, zero-allocation predicate evaluator shared across
/// terrain passability, encounter choices, and synthesis eligibility.
/// </summary>
public static class PredicateEvaluator
{
    public static bool Evaluate(in PredicateRule rule, in WorldQueryCtx ctx)
    {
        var allOf = rule.AllOf;
        if (allOf is not null)
        {
            for (int i = 0; i < allOf.Length; i++)
                if (!EvaluateClause(in allOf[i], in ctx)) return false;
        }

        var anyOf = rule.AnyOf;
        if (anyOf is not null && anyOf.Length > 0)
        {
            for (int i = 0; i < anyOf.Length; i++)
                if (EvaluateClause(in anyOf[i], in ctx)) return true;
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EvaluateClause(in PredicateClause clause, in WorldQueryCtx ctx) => clause.Op switch
    {
        PredicateOp.AlwaysBlock => false,
        PredicateOp.HasTag => EvalHasTag(clause.KeyId, in ctx),
        PredicateOp.SumTagWeightGte => SumTagWeight(clause.KeyId, in ctx) >= clause.Threshold,
        PredicateOp.HungerBelow => EvalHungerBelow(clause.KeyId, clause.Threshold, in ctx),
        PredicateOp.DaoMarksGte => SumDaoMarks(clause.KeyId, in ctx) >= clause.Threshold,
        PredicateOp.StatGte => GetStat(clause.KeyId, in ctx) >= clause.Threshold,
        PredicateOp.AnyFedGuHasTag => EvalAnyFedGuHasTag(clause.KeyId, clause.Threshold, in ctx),
        _ => false
    };

    private static bool EvalHasTag(ushort tagId, in WorldQueryCtx ctx)
    {
        if (tagId == 0) return false;
        if (ctx.PlayerTags.HasTag(tagId) || ctx.ContextTags.HasTag(tagId)) return true;
        var span = ctx.ActiveGu.Span;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Tags.HasTag(tagId)) return true;
        return false;
    }

    private static int SumTagWeight(ushort tagId, in WorldQueryCtx ctx)
    {
        if (tagId == 0) return 0;
        int sum = ctx.PlayerTags.GetWeight(tagId) + ctx.ContextTags.GetWeight(tagId);
        var span = ctx.ActiveGu.Span;
        for (int i = 0; i < span.Length; i++) sum += span[i].Tags.GetWeight(tagId);
        return sum;
    }

    private static bool EvalHungerBelow(ushort keyId, int threshold, in WorldQueryCtx ctx)
    {
        if (keyId == 0) return ctx.AvgGuHungerPercent < threshold;
        var span = ctx.ActiveGu.Span;
        for (int i = 0; i < span.Length; i++)
        {
            ref readonly var gu = ref span[i];
            if ((gu.GuId == keyId || gu.Tags.HasTag(keyId)) && gu.HungerPercent < threshold) return true;
        }
        return false;
    }

    private static int SumDaoMarks(ushort tagId, in WorldQueryCtx ctx)
    {
        int sum = 0;
        var span = ctx.DaoMarks.Span;
        for (int i = 0; i < span.Length; i++)
            if (tagId == 0 || span[i].TagId == tagId) sum += span[i].Weight;
        return sum;
    }

    private static int GetStat(ushort statId, in WorldQueryCtx ctx) => (WorldStatId)statId switch
    {
        WorldStatId.PrimevalStones => ctx.PrimevalStones,
        WorldStatId.Rank => ctx.Rank,
        WorldStatId.BountyHeat => ctx.BountyHeat,
        WorldStatId.AvgGuHunger => ctx.AvgGuHungerPercent,
        _ => 0
    };

    private static bool EvalAnyFedGuHasTag(ushort tagId, int minWeight, in WorldQueryCtx ctx)
    {
        if (tagId == 0) return false;
        int req = minWeight > 0 ? minWeight : 1;
        var span = ctx.ActiveGu.Span;
        for (int i = 0; i < span.Length; i++)
        {
            ref readonly var gu = ref span[i];
            if (gu.IsFed && gu.Tags.GetWeight(tagId) >= req) return true;
        }
        return false;
    }
}
