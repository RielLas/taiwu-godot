#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TaiwuGu.Tags;

namespace TaiwuGu.World;

public readonly record struct TerrainRuleProfile(
    ushort TerrainTagId,
    string SpriteLookupKey,
    PredicateRule PassabilityRule,
    bool BlocksVision,
    string BlockedReasonKey)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPassable(in WorldQueryCtx ctx)
    {
        var rule = PassabilityRule;
        return PredicateEvaluator.Evaluate(in rule, in ctx);
    }
}

public readonly record struct ResolvedTileSprite(string SpritePath, int VariantIndex, bool FlipH);

/// <summary>
/// Data-driven biome tag-to-sprite lookup table (zero hardcoded asset paths in C#).
/// Uses deterministic coordinate hashing (x * 73856093 ^ y * 19349663) for weighted variant &amp; flip_h selection.
/// </summary>
public sealed class BiomeSpriteTable
{
    private readonly record struct VariantEntry(string Path, float Weight, bool AllowFlipH);

    private readonly Dictionary<string, VariantEntry[]> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public string BiomeId { get; private set; } = string.Empty;
    public int EntryCount => _byKey.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint HashCoords(int x, int y)
    {
        unchecked { return (uint)((x * 73856093) ^ (y * 19349663)); }
    }

    public static BiomeSpriteTable LoadFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var table = new BiomeSpriteTable
        {
            BiomeId = root.TryGetProperty("biomeId", out var b) ? b.GetString() ?? "" : ""
        };
        string assetRoot = root.TryGetProperty("assetRoot", out var ar) ? ar.GetString() ?? "" : "";

        if (root.TryGetProperty("tagSprites", out var spritesObj))
        {
            foreach (var prop in spritesObj.EnumerateObject())
            {
                var variants = prop.Value.GetProperty("variants");
                var list = new List<VariantEntry>();
                foreach (var v in variants.EnumerateArray())
                {
                    string file = v.GetProperty("file").GetString() ?? "";
                    float weight = v.TryGetProperty("weight", out var w) ? w.GetSingle() : 1.0f;
                    bool flip = v.TryGetProperty("allowFlipH", out var fl) && fl.GetBoolean();
                    list.Add(new VariantEntry(assetRoot + file, Math.Max(0.01f, weight), flip));
                }
                table._byKey[prop.Name] = list.ToArray();
            }
        }
        return table;
    }

    public bool TryResolve(string lookupKey, int x, int y, out ResolvedTileSprite resolved)
    {
        if (!_byKey.TryGetValue(lookupKey, out var bucket) || bucket.Length == 0)
        {
            resolved = default;
            return false;
        }

        uint h = HashCoords(x, y);
        float totalW = 0f;
        for (int i = 0; i < bucket.Length; i++) totalW += bucket[i].Weight;

        float pick = ((h & 0xFFFF) / 65536.0f) * totalW;
        float acc = 0f;
        int chosenIdx = bucket.Length - 1;
        for (int i = 0; i < bucket.Length; i++)
        {
            acc += bucket[i].Weight;
            if (pick <= acc) { chosenIdx = i; break; }
        }

        var entry = bucket[chosenIdx];
        bool flipH = entry.AllowFlipH && ((h >> 16) & 1) == 1;
        resolved = new ResolvedTileSprite(entry.Path, chosenIdx, flipH);
        return true;
    }
}
