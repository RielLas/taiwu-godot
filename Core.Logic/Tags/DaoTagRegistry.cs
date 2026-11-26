#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TaiwuGu.Tags;

public readonly record struct DaoTagWeight(ushort TagId, short Weight);

public struct DaoTagVector
{
    public const int MaxCapacity = 16;

    [InlineArray(MaxCapacity)]
    private struct EntryBuffer
    {
        private DaoTagWeight _element0;
    }

    private EntryBuffer _entries;
    private int _count;

    public readonly int Count => _count;
    public readonly bool IsEmpty => _count == 0;

    public readonly DaoTagWeight this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (uint)index < (uint)_count
            ? _entries[index]
            : throw new ArgumentOutOfRangeException(nameof(index));
    }

    public static DaoTagVector Create(ReadOnlySpan<DaoTagWeight> entries)
    {
        var vec = new DaoTagVector();
        for (int i = 0; i < entries.Length; i++)
            vec.AddOrMerge(entries[i].TagId, entries[i].Weight);
        return vec;
    }

    public static DaoTagVector Single(ushort tagId, short weight = 1)
    {
        var vec = new DaoTagVector();
        vec.AddOrMerge(tagId, weight);
        return vec;
    }

    public bool AddOrMerge(ushort tagId, short weight)
    {
        if (tagId == DaoTagRegistry.InvalidTagId || weight == 0) return false;

        for (int i = 0; i < _count; i++)
        {
            if (_entries[i].TagId == tagId)
            {
                int merged = _entries[i].Weight + weight;
                if (merged == 0)
                {
                    int last = _count - 1;
                    _entries[i] = _entries[last];
                    _entries[last] = default;
                    _count = last;
                    return true;
                }
                _entries[i] = new DaoTagWeight(tagId, (short)Math.Clamp(merged, short.MinValue, short.MaxValue));
                return true;
            }
        }

        if (_count >= MaxCapacity) return false;
        _entries[_count++] = new DaoTagWeight(tagId, weight);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool HasTag(ushort tagId)
    {
        if (tagId == DaoTagRegistry.InvalidTagId) return false;
        for (int i = 0; i < _count; i++)
            if (_entries[i].TagId == tagId && _entries[i].Weight > 0) return true;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetWeight(ushort tagId)
    {
        if (tagId == DaoTagRegistry.InvalidTagId) return 0;
        for (int i = 0; i < _count; i++)
            if (_entries[i].TagId == tagId) return _entries[i].Weight;
        return 0;
    }

    public void Clear()
    {
        for (int i = 0; i < _count; i++) _entries[i] = default;
        _count = 0;
    }
}

/// <summary>
/// Interns Dao/Trait/Terrain tag strings to dense 1-based ushort IDs and stores an O(1)
/// zero-allocation conflict/friction matrix (positive = friction/conflict, negative = synergy).
/// </summary>
public sealed class DaoTagRegistry
{
    public const ushort InvalidTagId = 0;
    private const int InitialStride = 64;

    private readonly Dictionary<string, ushort> _nameToId = new(InitialStride, StringComparer.OrdinalIgnoreCase);
    private string[] _idToName = new string[InitialStride];
    private float[] _conflictMatrix = new float[InitialStride * InitialStride];
    private int _stride = InitialStride;
    private ushort _nextId = 1;

    public ushort Wood { get; }
    public ushort Poison { get; }
    public ushort Detox { get; }
    public ushort Blood { get; }
    public ushort Metal { get; }
    public ushort Fire { get; }
    public ushort Earth { get; }
    public ushort Water { get; }
    public ushort Formation { get; }
    public ushort Corrosive { get; }
    public ushort MiasmaDense { get; }
    public ushort KarstPeak { get; }
    public ushort RiverGorge { get; }
    public ushort SealedFormation { get; }

    public DaoTagRegistry()
    {
        _idToName[InvalidTagId] = string.Empty;
        Wood = Register("Wood");
        Poison = Register("Poison");
        Detox = Register("Detox");
        Blood = Register("Blood");
        Metal = Register("Metal");
        Fire = Register("Fire");
        Earth = Register("Earth");
        Water = Register("Water");
        Formation = Register("Formation");
        Corrosive = Register("Corrosive");
        MiasmaDense = Register("Miasma_Dense");
        KarstPeak = Register("Karst_Peak");
        RiverGorge = Register("River_Gorge");
        SealedFormation = Register("Sealed_Formation");

        // Conflict friction weights (>0 = friction/backlash; <0 = stabilizing synergy)
        SetConflictWeight(Water, Fire, 0.45f);
        SetConflictWeight(Fire, Metal, 0.40f);
        SetConflictWeight(Metal, Wood, 0.40f);
        SetConflictWeight(Wood, Earth, 0.35f);
        SetConflictWeight(Earth, Water, 0.35f);
        SetConflictWeight(Detox, Poison, 0.55f);
        SetConflictWeight(Blood, Water, 0.30f);
        SetConflictWeight(Formation, Blood, 0.35f);

        SetConflictWeight(Water, Wood, -0.10f);
        SetConflictWeight(Wood, Poison, -0.08f);
        SetConflictWeight(Poison, Corrosive, -0.08f);
    }

    public ushort Register(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be empty.", nameof(name));
        string trimmed = name.Trim();
        if (_nameToId.TryGetValue(trimmed, out ushort existing)) return existing;

        ushort id = _nextId++;
        if (id >= _stride) Grow(_stride * 2);
        _nameToId[trimmed] = id;
        _idToName[id] = trimmed;
        return id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetId(string name, out ushort id) => _nameToId.TryGetValue(name, out id);

    public ushort GetId(string name) =>
        TryGetId(name, out ushort id) ? id : throw new KeyNotFoundException($"Unknown Dao tag: {name}");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetName(ushort id) => id > 0 && id < _nextId ? _idToName[id] : string.Empty;

    public void SetConflictWeight(ushort tagA, ushort tagB, float friction, bool symmetric = true)
    {
        if (tagA == 0 || tagB == 0 || tagA >= _nextId || tagB >= _nextId) return;
        _conflictMatrix[tagA * _stride + tagB] = friction;
        if (symmetric) _conflictMatrix[tagB * _stride + tagA] = friction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetConflictWeight(ushort tagA, ushort tagB)
    {
        if (tagA == 0 || tagB == 0 || tagA >= _nextId || tagB >= _nextId) return 0f;
        return _conflictMatrix[tagA * _stride + tagB];
    }

    private void Grow(int newStride)
    {
        var newNames = new string[newStride];
        Array.Copy(_idToName, newNames, _idToName.Length);
        var newMatrix = new float[newStride * newStride];
        for (int r = 0; r < _stride; r++)
            Array.Copy(_conflictMatrix, r * _stride, newMatrix, r * newStride, _stride);
        _idToName = newNames;
        _conflictMatrix = newMatrix;
        _stride = newStride;
    }
}
