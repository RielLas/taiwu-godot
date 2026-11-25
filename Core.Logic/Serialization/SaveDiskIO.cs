using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Serialization
{
    /// <summary>
    /// Durable v3.0.0 POOF Save I/O for WorldStore.
    /// Writes atomically via save.json.tmp -> File.Replace -> save.json.bak.
    /// </summary>
    public static class SaveDiskIO
    {
        public const string CURRENT_SAVE_VERSION = "3.0.0";

        public static event Action<string>? OnSaveCompleted;
        public static event Action<string, string>? OnSaveLoadError;

        private static readonly JsonSerializerSettings s_SerializerSettings = new()
        {
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = new KnownComponentsBinder(),
            Formatting = Formatting.Indented
        };

        public static SaveMetadataDTO ExtractMetadata(string slotID, WorldStore store, double playtimeSeconds = 0)
        {
            var cult = store.Get<CultivatorComponent>(store.PlayerEntity);
            var ap = store.Get<ApertureComponent>(store.PlayerEntity);
            var clock = store.GetSingleton<WorldClockComponent>();
            var sector = store.GetCurrentSector();

            return new SaveMetadataDTO
            {
                SlotID = slotID,
                PlayerName = cult?.Name ?? "Gu Yue Fang Yuan",
                CurrentYear = clock?.CurrentYear ?? 1,
                CurrentSolarTerm = clock?.CurrentSolarTermIndex ?? 4,
                SolarTermName = clock?.GetSolarTermName() ?? "Spring Equinox (春分)",
                CurrentMountainID = sector?.DisplayName ?? store.CurrentSectorKey,
                TotalPlaytimeSeconds = playtimeSeconds,
                LastPlayedTimestamp = DateTime.UtcNow.ToString("o"),
                PreviewRank = ap?.Rank ?? 1,
                PreviewSubstage = ap?.Substage ?? 1,
                PreviewEssencePercent = ap?.CurrentEssenceSea ?? 44f,
                PreviewStones = cult?.PrimevalStones ?? 250
            };
        }

        public static bool SaveWorld(string slotID, WorldStore store, double playtimeSeconds = 0)
        {
            if (store == null) return false;
            if (!TryPrepareSave(slotID, store, playtimeSeconds, out _, out string savePath, out string tempPath, out string json))
                return false;

            try
            {
                File.WriteAllText(tempPath, json, Encoding.UTF8);
                CommitAtomicReplace(tempPath, savePath);

                Debug.Log($"[SaveDiskIO] Saved slot '{slotID}' (v{CURRENT_SAVE_VERSION}).");
                OnSaveCompleted?.Invoke(slotID);
                return true;
            }
            catch (Exception ex)
            {
                ReportSaveFailure(slotID, tempPath, ex);
                return false;
            }
        }

        public static async Task<bool> SaveWorldAsync(string slotID, WorldStore store, double playtimeSeconds = 0)
        {
            if (store == null) return false;
            if (!TryPrepareSave(slotID, store, playtimeSeconds, out _, out string savePath, out string tempPath, out string json))
                return false;

            try
            {
                await WriteTextToFileAsync(tempPath, json).ConfigureAwait(false);
                CommitAtomicReplace(tempPath, savePath);

                Debug.Log($"[SaveDiskIO] Async saved slot '{slotID}' (v{CURRENT_SAVE_VERSION}).");
                OnSaveCompleted?.Invoke(slotID);
                return true;
            }
            catch (Exception ex)
            {
                ReportSaveFailure(slotID, tempPath, ex);
                return false;
            }
        }

        public static WorldStore? LoadWorld(string slotID)
        {
            return TryLoadWorldWithMetadata(slotID, out var world, out _) ? world : null;
        }

        public static bool TryLoadWorldWithMetadata(string slotID, out WorldStore? world, out SaveMetadataDTO? metadata)
        {
            world = null;
            metadata = null;
            if (!TryGetSlotDirectory(slotID, out string slotDir)) return false;

            foreach (string path in CandidatePaths(slotDir))
            {
                var env = ReadValidatedEnvelope(path, slotID);
                if (env != null && env.Store != null)
                {
                    world = env.Store;
                    metadata = env.Metadata;
                    return true;
                }
            }

            Debug.LogError($"[SaveDiskIO] No readable v{CURRENT_SAVE_VERSION} save found for slot '{slotID}'.");
            OnSaveLoadError?.Invoke(slotID, "No readable save found.");
            return false;
        }

        public static async Task<WorldStore?> LoadWorldAsync(string slotID)
        {
            if (!TryGetSlotDirectory(slotID, out string slotDir)) return null;

            foreach (string path in CandidatePaths(slotDir))
            {
                if (!File.Exists(path)) continue;
                WorldStore? world = await ReadWorldAsync(path, slotID);
                if (world != null) return world;
            }

            Debug.LogError($"[SaveDiskIO] No readable v{CURRENT_SAVE_VERSION} save found for slot '{slotID}'.");
            OnSaveLoadError?.Invoke(slotID, "No readable save found.");
            return null;
        }

        public static SaveMetadataDTO? LoadMetadata(string slotID)
        {
            if (!TryGetSlotDirectory(slotID, out string slotDir)) return null;

            foreach (string path in CandidatePaths(slotDir))
            {
                WorldSaveEnvelope? env = ReadEnvelope(path);
                if (env?.Metadata != null && env.SaveVersion == CURRENT_SAVE_VERSION)
                    return env.Metadata;
            }
            return null;
        }

        public static async Task<SaveMetadataDTO?> LoadMetadataAsync(string slotID)
        {
            if (!TryGetSlotDirectory(slotID, out string slotDir)) return null;

            foreach (string path in CandidatePaths(slotDir))
            {
                if (!File.Exists(path)) continue;
                WorldSaveEnvelope? env = await ReadEnvelopeAsync(path);
                if (env?.Metadata != null && env.SaveVersion == CURRENT_SAVE_VERSION)
                    return env.Metadata;
            }
            return null;
        }

        public static async Task<List<SaveMetadataDTO>> LoadAllMetadataAsync()
        {
            var list = new List<SaveMetadataDTO>();
            SavePaths.EnsureDirectoryExists(SavePaths.RootSaveDirectory);

            foreach (string dir in Directory.GetDirectories(SavePaths.RootSaveDirectory))
            {
                string slotID = Path.GetFileName(dir);
                if (!SavePaths.IsValidSlotID(slotID)) continue;

                SaveMetadataDTO? metadata = await LoadMetadataAsync(slotID);
                if (metadata != null) list.Add(metadata);
            }

            list.Sort((a, b) => string.Compare(b.LastPlayedTimestamp, a.LastPlayedTimestamp, StringComparison.Ordinal));
            return list;
        }

        public static bool DeleteSlot(string slotID)
        {
            if (!TryGetSlotDirectory(slotID, out string slotDir)) return false;
            if (!Directory.Exists(slotDir)) return false;

            try
            {
                Directory.Delete(slotDir, true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveDiskIO] Failed to delete slot '{slotID}': {ex.Message}");
                return false;
            }
        }

        public static bool SlotExists(string slotID)
        {
            if (!SavePaths.IsValidSlotID(slotID)) return false;
            string slotDir = SavePaths.GetSlotPath(slotID!);
            if (!Directory.Exists(slotDir)) return false;

            foreach (string path in CandidatePaths(slotDir))
            {
                if (File.Exists(path)) return true;
            }
            return false;
        }

        private static bool TryGetSlotDirectory(string slotID, out string slotDir)
        {
            slotDir = "";
            if (!SavePaths.IsValidSlotID(slotID))
            {
                Debug.LogError($"[SaveDiskIO] Rejected invalid slot id '{slotID}'.");
                return false;
            }
            slotDir = SavePaths.GetSlotPath(slotID);
            return true;
        }

        private static bool TryPrepareSave(
            string slotID,
            WorldStore store,
            double playtimeSeconds,
            out string slotDir,
            out string savePath,
            out string tempPath,
            out string json)
        {
            slotDir = savePath = tempPath = json = "";
            if (!TryGetSlotDirectory(slotID, out slotDir)) return false;

            // Ensure every sector's runtime TileNode[] edits & exploration visibility are flushed to snapshots
            foreach (var (_, sector) in store.GetAll<SectorGridComponent>())
            {
                sector.CommitTilesToSnapshot();
            }

            var envelope = new WorldSaveEnvelope
            {
                SaveVersion = CURRENT_SAVE_VERSION,
                Metadata = ExtractMetadata(slotID, store, playtimeSeconds),
                Store = store
            };

            SavePaths.EnsureDirectoryExists(slotDir);
            savePath = Path.Combine(slotDir, SavePaths.SAVE_FILE_NAME);
            tempPath = savePath + SavePaths.TEMP_SUFFIX;
            json = JsonConvert.SerializeObject(envelope, s_SerializerSettings);
            return true;
        }

        private static IEnumerable<string> CandidatePaths(string slotDir)
        {
            string primary = Path.Combine(slotDir, SavePaths.SAVE_FILE_NAME);
            yield return primary;
            yield return primary + SavePaths.BACKUP_SUFFIX;
        }

        private static WorldSaveEnvelope? ReadValidatedEnvelope(string path, string slotID)
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return ParseEnvelope(json, path, slotID);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDiskIO] Failed reading '{path}': {ex.Message}");
                return null;
            }
        }

        private static async Task<WorldStore?> ReadWorldAsync(string path, string slotID)
        {
            try
            {
                string json = await ReadTextFromFileAsync(path);
                return ParseEnvelope(json, path, slotID)?.Store;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDiskIO] Failed reading '{path}': {ex.Message}");
                return null;
            }
        }

        private static WorldSaveEnvelope? ParseEnvelope(string json, string path, string slotID)
        {
            WorldSaveEnvelope? env = JsonConvert.DeserializeObject<WorldSaveEnvelope>(json, s_SerializerSettings);
            if (env == null || env.SaveVersion != CURRENT_SAVE_VERSION || env.Store == null)
            {
                Debug.LogWarning($"[SaveDiskIO] '{path}' invalid or incompatible save version (expected v{CURRENT_SAVE_VERSION}).");
                return null;
            }

            if (env.Store.Get<CultivatorComponent>(env.Store.PlayerEntity) == null)
            {
                Debug.LogWarning($"[SaveDiskIO] '{path}' missing Player cultivator component.");
                return null;
            }

            env.Store.RebuildIndices();
            return env;
        }

        private static WorldSaveEnvelope? ReadEnvelope(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return JsonConvert.DeserializeObject<WorldSaveEnvelope>(File.ReadAllText(path, Encoding.UTF8), s_SerializerSettings);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<WorldSaveEnvelope?> ReadEnvelopeAsync(string path)
        {
            try
            {
                string json = await ReadTextFromFileAsync(path);
                return await Task.Run(() => JsonConvert.DeserializeObject<WorldSaveEnvelope>(json, s_SerializerSettings));
            }
            catch
            {
                return null;
            }
        }

        private static async Task WriteTextToFileAsync(string filePath, string content)
        {
            byte[] encodedText = Encoding.UTF8.GetBytes(content);
            using var sourceStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await sourceStream.WriteAsync(encodedText, 0, encodedText.Length).ConfigureAwait(false);
        }

        private static async Task<string> ReadTextFromFileAsync(string filePath)
        {
            using var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            using var reader = new StreamReader(sourceStream, Encoding.UTF8);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        private static void CommitAtomicReplace(string tempPath, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
                return;
            }
            File.Replace(tempPath, targetPath, targetPath + SavePaths.BACKUP_SUFFIX, ignoreMetadataErrors: true);
        }

        private static void ReportSaveFailure(string slotID, string tempPath, Exception ex)
        {
            Debug.LogError($"[SaveDiskIO] Failed saving slot '{slotID}': {ex.Message}");
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            OnSaveLoadError?.Invoke(slotID, ex.Message);
        }
    }
}
