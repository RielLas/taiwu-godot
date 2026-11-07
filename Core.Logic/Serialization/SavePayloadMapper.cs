using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Psychology;

namespace TaiwuGu.Serialization
{
    /// <summary>
    /// Pure POCO GameStateContext <-> SavePayloadDTO mapper (Constitutional Law 4).
    /// Decoupled from disk I/O and engine scene lifecycles.
    /// </summary>
    public static class SavePayloadMapper
    {
        public static SaveMetadataDTO? ExtractMetadata(string slotID, GameStateContext? ctx, double playtimeSeconds = 0)
        {
            if (ctx == null) return null;
            return new SaveMetadataDTO
            {
                SlotID = slotID,
                PlayerName = ctx.Cultivator?.Name ?? "Fang Yuan",
                CurrentYear = ctx.Calendar?.CurrentYear ?? 1,
                CurrentSolarTerm = ctx.Calendar?.CurrentSolarTermIndex ?? 4,
                SolarTermName = ctx.Calendar?.GetSolarTermName() ?? "Spring Equinox (春分)",
                CurrentMountainID = ctx.WorldMap?.CurrentSectorKey ?? ctx.WorldMap?.CurrentRegion.ToString() ?? "Qing Mao Mountain (青茅山)",
                TotalPlaytimeSeconds = playtimeSeconds,
                LastPlayedTimestamp = DateTime.UtcNow.ToString("o"),
                PreviewRank = ctx.Cultivator?.Rank ?? 1,
                PreviewSubstage = ctx.Cultivator?.Substage ?? 1,
                PreviewEssencePercent = ctx.Cultivator?.CurrentEssenceSea ?? 44f,
                PreviewStones = ctx.Cultivator?.PrimevalStones ?? 250
            };
        }

        public static SavePayloadDTO? ExtractPayload(string slotID, GameStateContext? ctx, double playtimeSeconds = 0)
        {
            if (ctx == null) return null;

            var meta = ExtractMetadata(slotID, ctx, playtimeSeconds);
            return new SavePayloadDTO
            {
                SaveVersion = SaveDiskIO.CURRENT_SAVE_VERSION,
                Metadata = meta ?? new SaveMetadataDTO(),
                Context = ctx
            };
        }

        public static bool MigratePayload(SavePayloadDTO? payload, string slotID = "")
        {
            if (payload == null) return false;

            string version = payload.SaveVersion;

            if (version == SaveDiskIO.CURRENT_SAVE_VERSION)
            {
                if (payload.Context == null)
                {
                    payload.Context = GameStateContext.CreateDefault();
                }
                return true;
            }

            if (string.IsNullOrEmpty(version) || version == "1.0.0" || version == "2.0.0")
            {
                if (payload.Context == null)
                {
                    payload.Context = GameStateContext.CreateDefault();
                }

                MigrateSectorSnapshots(payload.Context);

                payload.SaveVersion = SaveDiskIO.CURRENT_SAVE_VERSION;
                Debug.Log($"[SavePayloadMapper] Migrated payload from version '{(string.IsNullOrEmpty(version) ? "unversioned" : version)}' to '{SaveDiskIO.CURRENT_SAVE_VERSION}'.");
                return true;
            }

            Debug.LogError($"[SaveDiskIO] Unsupported save version '{version}' in slot '{slotID}'. Expected '{SaveDiskIO.CURRENT_SAVE_VERSION}'. Save rejected.");
            SaveDiskIO.RaiseSaveLoadError(slotID, $"Unsupported save version: {version}");
            return false;
        }

        /// <summary>
        /// 2.0 payloads stored each visited sector as a cloned TileNode[]. Convert that array into the
        /// sparse patch list the current loader regenerates against. Without this, revisiting a sector
        /// saved by 2.0 would silently discard every authored change it contained.
        /// </summary>
        private static void MigrateSectorSnapshots(GameStateContext? context)
        {
            var snapshots = context?.WorldMap?.SectorSnapshots;
            if (snapshots == null) return;

            foreach (var pair in snapshots)
            {
                SectorMapSnapshot? snapshot = pair.Value;
                if (snapshot?.LegacyTiles == null) continue;

                int width = snapshot.GridWidth > 0 ? snapshot.GridWidth : SectorGenerator.DEFAULT_WIDTH;
                int height = snapshot.GridHeight > 0 ? snapshot.GridHeight : SectorGenerator.DEFAULT_HEIGHT;

                TileNode[] baseline = SectorGenerator.Generate(
                    snapshot.Seed,
                    width,
                    height,
                    RegionalWallLaw.GetRegionBiome(snapshot.Region));

                var patches = new List<TilePatch>();
                var explored = new List<int>();
                var visible = new List<int>();

                int count = Math.Min(snapshot.LegacyTiles.Length, Math.Min(baseline.Length, width * height));
                for (int i = 0; i < count; i++)
                {
                    TileNode saved = snapshot.LegacyTiles[i];

                    if (saved.IsExplored) explored.Add(i);
                    if (saved.IsVisible) visible.Add(i);

                    TileNode generated = baseline[i];
                    bool changed = saved.Biome != generated.Biome
                        || saved.POI != generated.POI
                        || saved.IsActive != generated.IsActive
                        || !string.Equals(saved.CustomLandmarkId, generated.CustomLandmarkId, StringComparison.Ordinal);

                    if (!changed) continue;

                    patches.Add(new TilePatch
                    {
                        Index = i,
                        Biome = saved.Biome,
                        POI = saved.POI,
                        CustomLandmarkId = saved.CustomLandmarkId,
                        IsActive = saved.IsActive
                    });
                }

                snapshot.Patches = patches;
                snapshot.ExploredTileIndices = explored;
                snapshot.VisibleTileIndices = visible;
                snapshot.LegacyTiles = null;

                Debug.Log($"[SavePayloadMapper] Migrated sector '{pair.Key}' from a 2.0 tile array to {patches.Count} patch(es).");
            }
        }

        public static GameStateContext? CreateContextFromPayload(SavePayloadDTO? payload)
        {
            if (payload == null) return null;
            if (!MigratePayload(payload))
            {
                return null;
            }
            if (payload.Context != null)
            {
                if (payload.Context.WorldMap?.Tiles == null || payload.Context.WorldMap.Tiles.Length == 0)
                {
                    payload.Context.WorldMap?.InitializeDefaultGrid();
                }
                return payload.Context;
            }
            return GameStateContext.CreateDefault();
        }
    }

    public static partial class SaveLoadEngine
    {
        public static SaveMetadataDTO? ExtractMetadata(string slotID, GameStateContext? ctx, double playtimeSeconds = 0)
            => SavePayloadMapper.ExtractMetadata(slotID, ctx, playtimeSeconds);

        public static SavePayloadDTO? ExtractPayload(string slotID, GameStateContext? ctx, double playtimeSeconds = 0)
            => SavePayloadMapper.ExtractPayload(slotID, ctx, playtimeSeconds);

        public static GameStateContext? CreateContextFromPayload(SavePayloadDTO? payload)
            => SavePayloadMapper.CreateContextFromPayload(payload);

        public static bool MigratePayload(SavePayloadDTO? payload, string slotID = "")
            => SavePayloadMapper.MigratePayload(payload, slotID);
    }
}
