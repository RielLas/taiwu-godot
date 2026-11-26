#nullable enable
using System;
using TaiwuGu.Tags;

namespace TaiwuGu.Gu;

public readonly record struct SynthesisBlueprint(
    string BlueprintId,
    string OutputGuId,
    DaoTagWeight[] RequiredTagWeights,
    int MinTotalRank,
    float BaseSuccessRate);

public readonly record struct SynthesisOutcome(
    bool MatchedBlueprint,
    bool Succeeded,
    string OutputGuId,
    string MutationTag,
    float FrictionScore,
    int BacklashSeaCost,
    int BacklashHpCost);

/// <summary>
/// Trait-vector Gu synthesis resolver. Evaluates any input tag combination against blueprints
/// and dynamically computes emergent mutations or Tag Friction backlash instead of rejecting input.
/// </summary>
public static class SynthesisResolver
{
    private const int MaxTags = 32;

    public static SynthesisOutcome Resolve(
        ReadOnlySpan<DaoTagWeight> combinedTags,
        int totalRank,
        ReadOnlySpan<SynthesisBlueprint> blueprints,
        DaoTagRegistry registry,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(rng);

        Span<DaoTagWeight> agg = stackalloc DaoTagWeight[MaxTags];
        int count = KillerMovePipelineCompiler.AggregateTags(combinedTags, agg);
        ReadOnlySpan<DaoTagWeight> active = agg.Slice(0, count);

        float friction = MathF.Round(KillerMovePipelineCompiler.ComputePairwiseFriction(active, registry), 4);
        int bestIdx = FindBestBlueprint(active, Math.Max(0, totalRank), blueprints);

        if (bestIdx >= 0)
        {
            var bp = blueprints[bestIdx];
            float rankBonus = Math.Clamp((totalRank - bp.MinTotalRank) * 0.04f, 0f, 0.20f);
            float chance = Math.Clamp(bp.BaseSuccessRate + rankBonus - friction * 0.25f, 0.05f, 0.98f);
            if (rng.NextDouble() < chance)
            {
                int residualSea = friction >= 0.75f ? Math.Max(1, (int)MathF.Ceiling((friction - 0.75f) * 12f)) : 0;
                return new SynthesisOutcome(true, true, bp.OutputGuId, friction >= 0.75f ? "volatile_resonance" : "", friction, residualSea, 0);
            }

            int sea = Math.Max(4, (int)MathF.Ceiling((10f + totalRank * 3f) * (1f + friction)));
            int hp = friction > 0.25f ? Math.Max(1, (int)MathF.Ceiling(friction * (8f + totalRank * 2.5f))) : 0;
            return new SynthesisOutcome(true, false, friction >= 1.0f ? "gu_emergent_slag" : "", friction >= 1.0f ? "dao_fracture" : "unstable_dissipation", friction, sea, hp);
        }

        // Emergent synthesis / dynamic Tag Friction backlash when no blueprint matches
        double roll = rng.NextDouble();
        if (friction >= 0.80f)
        {
            int sea = Math.Max(10, (int)MathF.Ceiling(14f + friction * 18f + totalRank * 2f));
            int hp = Math.Max(4, (int)MathF.Ceiling(6f + friction * 16f + totalRank * 2f));
            bool chimera = roll < 0.30;
            return new SynthesisOutcome(false, false, chimera ? "gu_emergent_chimera" : "gu_emergent_slag", chimera ? "chaotic_chimera" : "dao_fracture", friction, sea, hp);
        }

        float emergentChance = Math.Clamp(0.25f + totalRank * 0.05f - friction * 0.15f, 0.05f, 0.65f);
        if (count >= 2 && totalRank >= 2 && roll < emergentChance)
        {
            return new SynthesisOutcome(false, true, "gu_emergent_essence", "emergent_coalescence", friction, Math.Max(4, (int)MathF.Ceiling(8f + friction * 10f)), 0);
        }

        return new SynthesisOutcome(false, false, "gu_emergent_slag", "inert_slag", friction, Math.Max(2, (int)MathF.Ceiling(6f + friction * 12f)), friction > 0.30f ? (int)MathF.Ceiling(friction * 10f) : 0);
    }

    private static int FindBestBlueprint(ReadOnlySpan<DaoTagWeight> active, int totalRank, ReadOnlySpan<SynthesisBlueprint> blueprints)
    {
        int best = -1, bestRank = -1, bestSum = -1;
        for (int i = 0; i < blueprints.Length; i++)
        {
            var bp = blueprints[i];
            if (totalRank < bp.MinTotalRank || bp.RequiredTagWeights is null || bp.RequiredTagWeights.Length == 0) continue;
            bool ok = true;
            int sum = 0;
            for (int r = 0; r < bp.RequiredTagWeights.Length; r++)
            {
                var req = bp.RequiredTagWeights[r];
                if (KillerMovePipelineCompiler.GetTagWeight(active, req.TagId) < req.Weight) { ok = false; break; }
                sum += req.Weight;
            }
            if (ok && (best < 0 || bp.MinTotalRank > bestRank || (bp.MinTotalRank == bestRank && sum > bestSum)))
            {
                best = i; bestRank = bp.MinTotalRank; bestSum = sum;
            }
        }
        return best;
    }
}
