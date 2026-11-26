#nullable enable
using System;
using System.Runtime.CompilerServices;
using TaiwuGu.Tags;

namespace TaiwuGu.Gu;

public enum NodeRole : byte { Delivery, Payload, Amplifier, CostMitigator }

public enum ModifierOpCode : ushort
{
    Projectile,
    NovaAura,
    DashTrail,
    SelfBuff,
    ConvertDamageTag,
    ApplyStatus,
    ScalePower,
    ScaleChannelTime,
    ScaleSeaCost
}

public readonly record struct ModifierNodeSpec(
    NodeRole Role,
    ModifierOpCode OpCode,
    ushort ParamTagA,
    ushort ParamTagB,
    float Magnitude);

public readonly record struct KillerMoveCompiled(
    ModifierOpCode Delivery,
    ushort DamageTagId,
    float PowerMultiplier,
    float ChannelSeconds,
    int SeaCost,
    float InstabilityScore,
    int BacklashDamage);

/// <summary>
/// Zero-GC compiler folding any Core + Support Gu modifier nodes into a value-type KillerMoveCompiled.
/// Resonance &amp; Backlash emerge automatically from Tag Friction across slotted nodes.
/// </summary>
public static class KillerMovePipelineCompiler
{
    private const int MaxTags = 32;
    private const float BacklashThreshold = 0.35f;

    public static KillerMoveCompiled Compile(
        ReadOnlySpan<ModifierNodeSpec> nodes,
        ReadOnlySpan<DaoTagWeight> slottedTags,
        DaoTagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        Span<DaoTagWeight> agg = stackalloc DaoTagWeight[MaxTags];
        int tagCount = AggregateTags(slottedTags, agg);
        ReadOnlySpan<DaoTagWeight> activeTags = agg.Slice(0, tagCount);

        ModifierOpCode delivery = ModifierOpCode.Projectile;
        bool hasDelivery = false;
        ushort damageTag = FindDominantTag(activeTags);

        float power = 1.0f;
        float channel = 0.65f + Math.Max(0, nodes.Length - 1) * 0.12f;
        float seaCost = 18.0f + Math.Max(0, nodes.Length - 1) * 4.0f;
        float structuralInstability = nodes.Length > 4 ? (nodes.Length - 4) * 0.08f : 0f;

        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            switch (n.OpCode)
            {
                case ModifierOpCode.Projectile:
                case ModifierOpCode.NovaAura:
                case ModifierOpCode.DashTrail:
                case ModifierOpCode.SelfBuff:
                    if (!hasDelivery)
                    {
                        delivery = n.OpCode;
                        hasDelivery = true;
                        float scale = n.Magnitude > 0f ? Math.Clamp(n.Magnitude, 0.5f, 3.0f) : 1.0f;
                        power *= n.OpCode == ModifierOpCode.NovaAura ? scale * 1.15f : scale;
                        if (n.OpCode == ModifierOpCode.NovaAura) { channel += 0.25f; seaCost += 8f; }
                        else if (n.OpCode == ModifierOpCode.DashTrail) { channel *= 0.80f; seaCost += 4f; }
                    }
                    else
                    {
                        structuralInstability += 0.25f;
                        seaCost += 6f;
                    }
                    break;

                case ModifierOpCode.ConvertDamageTag:
                    if (n.ParamTagA != 0)
                    {
                        damageTag = n.ParamTagA;
                        power *= 1.0f + Math.Clamp(n.Magnitude, -0.5f, 2.0f);
                    }
                    break;

                case ModifierOpCode.ApplyStatus:
                    power *= 1.0f + Math.Clamp(n.Magnitude, 0f, 3.0f) * 0.20f;
                    seaCost += 3.0f + Math.Clamp(n.Magnitude, 0f, 3.0f) * 2.0f;
                    break;

                case ModifierOpCode.ScalePower:
                    power *= Math.Clamp(n.Magnitude, 0.10f, 4.0f);
                    break;

                case ModifierOpCode.ScaleChannelTime:
                    channel *= Math.Clamp(n.Magnitude, 0.20f, 3.0f);
                    break;

                case ModifierOpCode.ScaleSeaCost:
                    seaCost *= Math.Clamp(n.Magnitude, 0.20f, 3.0f);
                    break;
            }
        }

        float tagFriction = ComputePairwiseFriction(activeTags, registry);
        float instability = MathF.Round(MathF.Max(0f, tagFriction + structuralInstability), 4);
        power = Math.Clamp(power, 0.15f, 12.0f);
        channel = Math.Clamp(channel, 0.10f, 8.0f);
        int finalSea = Math.Max(1, (int)MathF.Ceiling(seaCost));

        int backlash = instability > BacklashThreshold
            ? Math.Max(1, (int)MathF.Ceiling((instability - BacklashThreshold) * 24.0f * MathF.Max(1.0f, power * 0.5f)))
            : 0;

        return new KillerMoveCompiled(delivery, damageTag, power, channel, finalSea, instability, backlash);
    }

    internal static int AggregateTags(ReadOnlySpan<DaoTagWeight> src, Span<DaoTagWeight> dst)
    {
        int count = 0;
        for (int i = 0; i < src.Length; i++)
        {
            var e = src[i];
            if (e.TagId == 0 || e.Weight <= 0) continue;
            int idx = -1;
            for (int j = 0; j < count; j++) if (dst[j].TagId == e.TagId) { idx = j; break; }
            if (idx >= 0) dst[idx] = new DaoTagWeight(e.TagId, (short)Math.Clamp(dst[idx].Weight + e.Weight, short.MinValue, short.MaxValue));
            else if (count < dst.Length) dst[count++] = e;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ushort FindDominantTag(ReadOnlySpan<DaoTagWeight> tags)
    {
        ushort best = 0;
        short bestW = 0;
        for (int i = 0; i < tags.Length; i++)
            if (tags[i].Weight > bestW) { bestW = tags[i].Weight; best = tags[i].TagId; }
        return best;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetTagWeight(ReadOnlySpan<DaoTagWeight> tags, ushort tagId)
    {
        for (int i = 0; i < tags.Length; i++) if (tags[i].TagId == tagId) return tags[i].Weight;
        return 0;
    }

    internal static float ComputePairwiseFriction(ReadOnlySpan<DaoTagWeight> tags, DaoTagRegistry registry)
    {
        float friction = 0f;
        for (int i = 0; i < tags.Length; i++)
        {
            for (int j = i + 1; j < tags.Length; j++)
            {
                float w = registry.GetConflictWeight(tags[i].TagId, tags[j].TagId);
                if (w > 0f) friction += w * MathF.Sqrt(tags[i].Weight * tags[j].Weight);
            }
        }
        return friction;
    }
}
