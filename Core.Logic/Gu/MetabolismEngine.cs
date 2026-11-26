#nullable enable
using System;
using System.Collections.Generic;

namespace TaiwuGu.Gu;

public enum StarveBehavior : byte { Hibernate, DrainSea, DrainHp, CannibalizeAperture }

public readonly record struct MetabolismProfile(
    string ProfileId,
    float MonthlyDecay,
    float CombatCastBurn,
    float PenaltyThreshold,
    StarveBehavior UnfedPolicy,
    ushort PreferredFeedTag);

public struct GuMetabolismState
{
    public string GuId;
    public string ProfileId;
    public float HungerRatio; // 0.0 = fully fed, 1.0 = fully starved
    public int Rank;
    public bool IsDormant;
    public bool IsDestroyed;
}

/// <summary>
/// Data-driven Gu metabolism &amp; upkeep engine replacing hardcoded linear hunger enums.
/// Supports Hibernate, DrainSea, DrainHp, and CannibalizeAperture (eats weakest living neighbor before draining host).
/// </summary>
public static class MetabolismEngine
{
    public static void ApplyCombatCastBurn(ref GuMetabolismState state, in MetabolismProfile profile)
    {
        if (state.IsDestroyed || state.IsDormant) return;
        state.HungerRatio = Math.Clamp(state.HungerRatio + Math.Max(0f, profile.CombatCastBurn), 0f, 1.0f);
    }

    public static void FeedGu(ref GuMetabolismState state, float satiationAmount)
    {
        if (state.IsDestroyed) return;
        state.HungerRatio = Math.Clamp(state.HungerRatio - Math.Max(0f, satiationAmount), 0f, 1.0f);
        if (state.HungerRatio < 0.5f) state.IsDormant = false;
    }

    public static void TickMonth(
        Span<GuMetabolismState> roster,
        IReadOnlyDictionary<string, MetabolismProfile> profiles,
        out int seaDrain,
        out int hpDrain,
        out int cannibalizedCount,
        out int hibernatingCount)
    {
        seaDrain = 0;
        hpDrain = 0;
        cannibalizedCount = 0;
        hibernatingCount = 0;

        for (int i = 0; i < roster.Length; i++)
        {
            ref var gu = ref roster[i];
            if (gu.IsDestroyed || !profiles.TryGetValue(gu.ProfileId, out var profile)) continue;

            if (!gu.IsDormant)
                gu.HungerRatio = Math.Clamp(gu.HungerRatio + profile.MonthlyDecay, 0f, 1.0f);

            if (gu.HungerRatio < profile.PenaltyThreshold) continue;

            float excess = Math.Max(0.1f, gu.HungerRatio - profile.PenaltyThreshold);
            int severity = Math.Max(1, (int)MathF.Ceiling(excess * 10f * Math.Max(1, gu.Rank)));

            switch (profile.UnfedPolicy)
            {
                case StarveBehavior.Hibernate:
                    gu.IsDormant = true;
                    hibernatingCount++;
                    break;

                case StarveBehavior.DrainSea:
                    seaDrain += severity * 2;
                    break;

                case StarveBehavior.DrainHp:
                    hpDrain += severity;
                    break;

                case StarveBehavior.CannibalizeAperture:
                    int preyIdx = FindWeakestNeighbor(roster, i);
                    if (preyIdx >= 0)
                    {
                        roster[preyIdx].IsDestroyed = true;
                        gu.HungerRatio = Math.Clamp(gu.HungerRatio - 0.55f, 0f, 1.0f);
                        cannibalizedCount++;
                    }
                    else
                    {
                        hpDrain += severity * 2;
                    }
                    break;
            }
        }
    }

    private static int FindWeakestNeighbor(Span<GuMetabolismState> roster, int predatorIdx)
    {
        int bestIdx = -1;
        int lowestRank = int.MaxValue;
        for (int j = 0; j < roster.Length; j++)
        {
            if (j == predatorIdx || roster[j].IsDestroyed) continue;
            if (roster[j].Rank < lowestRank)
            {
                lowestRank = roster[j].Rank;
                bestIdx = j;
            }
        }
        return bestIdx;
    }
}
