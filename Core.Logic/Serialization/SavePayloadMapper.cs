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
                payload.SaveVersion = SaveDiskIO.CURRENT_SAVE_VERSION;
                Debug.Log($"[SavePayloadMapper] Migrated payload from version '{(string.IsNullOrEmpty(version) ? "unversioned" : version)}' to '{SaveDiskIO.CURRENT_SAVE_VERSION}'.");
                return true;
            }

            Debug.LogError($"[SaveDiskIO] Unsupported save version '{version}' in slot '{slotID}'. Expected '{SaveDiskIO.CURRENT_SAVE_VERSION}'. Save rejected.");
            SaveDiskIO.RaiseSaveLoadError(slotID, $"Unsupported save version: {version}");
            return false;
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
