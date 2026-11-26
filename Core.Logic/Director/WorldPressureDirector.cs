#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaiwuGu.Tags;

namespace TaiwuGu.Director;

public readonly record struct WorldPressureVector(int PlayerNetWorth, int TotalDaoMarks, int BountyHeat, float AvgGuHunger);

public readonly record struct DirectorChoiceSpec(
    string Label,
    PredicateRule Gate,
    float SuccessProb,
    int StoneDelta,
    int HpDelta,
    int SeaDelta,
    int BountyDelta,
    string GrantGuId,
    ushort DaoTagGain,
    int DaoMarkDelta);

public readonly record struct DirectorEventSpec(
    string EventId,
    string DeckId,
    string Title,
    string Vignette,
    float MinPressure,
    float TensionCost,
    PredicateRule Eligibility,
    DirectorChoiceSpec[] Choices);

public sealed record DirectorDeckEnvelope(int SchemaVersion, string DeckId, DirectorEventSpec[] Events);

/// <summary>
/// Pressure-budgeting, biome-agnostic encounter &amp; calamity director.
/// Swapping the loaded JSON deck turns Southern Border into Northern Plains with zero C# edits.
/// </summary>
public sealed class WorldPressureDirector
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly List<DirectorEventSpec> _deck = new();
    private readonly List<DirectorEventSpec> _scratch = new(16);
    private readonly Dictionary<string, int> _cooldowns = new(StringComparer.Ordinal);
    private int _step;

    public string ActiveDeckId { get; private set; } = string.Empty;
    public float CurrentTension { get; private set; }
    public int LoadedEventCount => _deck.Count;

    public WorldPressureDirector(float initialTension = 15f) => CurrentTension = Math.Clamp(initialTension, 0f, 120f);

    public void LoadDeckFromJson(string json, bool preserveTension = true)
    {
        var env = JsonSerializer.Deserialize<DirectorDeckEnvelope>(json, JsonOpts)
            ?? throw new InvalidDataException("Invalid DirectorDeckEnvelope JSON.");
        _deck.Clear();
        ActiveDeckId = env.DeckId;
        foreach (var ev in env.Events) _deck.Add(string.IsNullOrEmpty(ev.DeckId) ? ev with { DeckId = env.DeckId } : ev);
        if (!preserveTension) { CurrentTension = 0f; _cooldowns.Clear(); _step = 0; }
    }

    public float ComputeScalarPressure(in WorldPressureVector v)
    {
        float raw = Math.Max(0, v.PlayerNetWorth) * 0.008f
                  + Math.Max(0, v.TotalDaoMarks) * 0.65f
                  + Math.Max(0, v.BountyHeat) * 1.40f
                  + Math.Clamp(v.AvgGuHunger, 0f, 100f) * 0.45f;
        return Math.Clamp(raw, 0f, 100f);
    }

    public bool TrySelectEvent(in WorldPressureVector pressureVec, in WorldQueryCtx ctx, Random rng, out DirectorEventSpec chosen)
    {
        _step++;
        float pressure = ComputeScalarPressure(in pressureVec);
        CurrentTension = Math.Min(120f, CurrentTension + pressure * 0.35f);

        _scratch.Clear();
        for (int i = 0; i < _deck.Count; i++)
        {
            var c = _deck[i];
            if (pressure < c.MinPressure || CurrentTension < c.TensionCost) continue;
            if (_cooldowns.TryGetValue(c.EventId, out int last) && (_step - last) <= 3) continue;
            var elig = c.Eligibility;
            if (!PredicateEvaluator.Evaluate(in elig, in ctx)) continue;
            _scratch.Add(c);
        }

        if (_scratch.Count == 0) { chosen = default; return false; }
        chosen = _scratch[rng.Next(_scratch.Count)];
        CurrentTension = Math.Max(0f, CurrentTension - chosen.TensionCost);
        _cooldowns[chosen.EventId] = _step;
        return true;
    }
}
