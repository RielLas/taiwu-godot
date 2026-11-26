#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TaiwuGu.Tags;

namespace TaiwuGu.Meta;

public sealed record LegacyRunSnapshot(
    int PeakRank,
    int TotalDaoMarks,
    int MonthsSurvived,
    int PeakNetWorth,
    bool ClearedTrial,
    IReadOnlyList<string> DiscoveredBlueprints,
    IReadOnlyList<DaoTagWeight> RunDaoTagWeights);

public sealed class NewRunMetaConfig
{
    public string SelectedOriginId { get; set; } = LegacyArchive.DefaultOriginId;
    public int StartingBonusStones { get; set; }
    public int StartingBonusDaoMarks { get; set; }
    public List<string> UnlockedOriginIds { get; } = new();
    public List<string> KnownBlueprintIds { get; } = new();
    public List<DaoTagWeight> InheritedDaoTagWeights { get; } = new();
}

/// <summary>
/// Second-run carryover archive (Spring Autumn Cicada fragments, synthesis blueprints,
/// inherited Dao tag weights, unlocked NewRunConfig origins) with atomic temp+replace persistence.
/// </summary>
public sealed class LegacyArchive
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultOriginId = "mortal_wanderer";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public int CicadaFragments { get; set; }
    public List<string> DiscoveredSynthesisBlueprints { get; set; } = new();
    public List<DaoTagWeight> InheritedDaoTagWeights { get; set; } = new();
    public List<string> UnlockedOriginIds { get; set; } = new() { DefaultOriginId };
    public int TotalRunsArchived { get; set; }

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static LegacyArchive CreateDefault() => new();

    public static LegacyArchive ExtractFromRun(LegacyArchive? prior, LegacyRunSnapshot run)
    {
        var baseArc = prior ?? CreateDefault();
        int gained = Math.Max(1, run.PeakRank * 2 + run.TotalDaoMarks / 10 + run.MonthsSurvived / 3 + (run.ClearedTrial ? 12 : 0));

        var bps = new HashSet<string>(baseArc.DiscoveredSynthesisBlueprints, StringComparer.Ordinal);
        foreach (var b in run.DiscoveredBlueprints) if (!string.IsNullOrWhiteSpace(b)) bps.Add(b);

        var origins = new HashSet<string>(baseArc.UnlockedOriginIds, StringComparer.Ordinal) { DefaultOriginId };
        if (run.TotalDaoMarks >= 25) origins.Add("blood_sect_exile");
        if (run.PeakNetWorth >= 1500) origins.Add("caravan_heir");
        if (run.PeakRank >= 5 || run.ClearedTrial) origins.Add("cicada_vessel");

        var merged = new Dictionary<ushort, int>();
        foreach (var w in baseArc.InheritedDaoTagWeights) merged[w.TagId] = (int)(w.Weight * 0.75f);
        foreach (var w in run.RunDaoTagWeights) merged[w.TagId] = merged.GetValueOrDefault(w.TagId) + Math.Max(1, w.Weight / 4);

        return new LegacyArchive
        {
            SchemaVersion = CurrentSchemaVersion,
            CicadaFragments = baseArc.CicadaFragments + gained,
            DiscoveredSynthesisBlueprints = bps.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            InheritedDaoTagWeights = merged.OrderBy(kv => kv.Key).Select(kv => new DaoTagWeight(kv.Key, (short)Math.Clamp(kv.Value, 1, 20))).ToList(),
            UnlockedOriginIds = origins.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            TotalRunsArchived = baseArc.TotalRunsArchived + 1
        };
    }

    public static void SaveAtomic(string path, LegacyArchive archive)
    {
        string full = Path.GetFullPath(path);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string tmp = $"{full}.tmp.{Guid.NewGuid():N}";
        string bak = $"{full}.bak";
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(archive, Opts));
        using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(true);
        }
        if (File.Exists(full)) File.Replace(tmp, full, bak, true);
        else File.Move(tmp, full, true);
    }

    public static LegacyArchive LoadAtomic(string path)
    {
        foreach (var candidate in new[] { Path.GetFullPath(path), Path.GetFullPath(path) + ".bak" })
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                var parsed = JsonSerializer.Deserialize<LegacyArchive>(File.ReadAllText(candidate, Encoding.UTF8), Opts);
                if (parsed is not null && parsed.SchemaVersion == CurrentSchemaVersion) return parsed;
            }
            catch { }
        }
        return CreateDefault();
    }

    public static void ApplyToNewRunConfig(LegacyArchive archive, NewRunMetaConfig cfg, string? requestedOrigin = null, int spendFragments = 0)
    {
        cfg.SelectedOriginId = !string.IsNullOrWhiteSpace(requestedOrigin) && archive.UnlockedOriginIds.Contains(requestedOrigin)
            ? requestedOrigin : DefaultOriginId;
        cfg.UnlockedOriginIds.Clear(); cfg.UnlockedOriginIds.AddRange(archive.UnlockedOriginIds);
        cfg.KnownBlueprintIds.Clear(); cfg.KnownBlueprintIds.AddRange(archive.DiscoveredSynthesisBlueprints);
        cfg.InheritedDaoTagWeights.Clear(); cfg.InheritedDaoTagWeights.AddRange(archive.InheritedDaoTagWeights);
        int spent = Math.Clamp(spendFragments, 0, archive.CicadaFragments);
        cfg.StartingBonusStones = spent * 15 + (cfg.SelectedOriginId == "caravan_heir" ? 120 : 0);
        cfg.StartingBonusDaoMarks = spent / 2 + (cfg.SelectedOriginId == "blood_sect_exile" ? 4 : 0);
    }
}
