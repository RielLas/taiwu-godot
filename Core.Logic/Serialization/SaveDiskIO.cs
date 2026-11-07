using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TaiwuGu.Core;

namespace TaiwuGu.Serialization
{
    /// <summary>
    /// Durable save I/O.
    ///
    /// One file per slot (<c>save.json</c>) holds both metadata and state, so the two can never
    /// disagree. Writes go to a temp file and are installed with <see cref="File.Replace(string, string, string)"/>,
    /// a single filesystem operation: after a crash the slot holds either the previous save or the
    /// new one — never a truncated mix, and never nothing. The superseded bytes land in
    /// <c>save.json.bak</c> and are used when the primary file is missing or unreadable.
    ///
    /// Slots written by the older two-file layout (<c>payload.json</c> + <c>metadata.json</c>) are
    /// still readable; they are simply rewritten as <c>save.json</c> on the next save.
    /// </summary>
    public static class SaveDiskIO
    {
        // 2.1.0: sector snapshots store seed + tile patches instead of a cloned TileNode[].
        public const string CURRENT_SAVE_VERSION = "2.1.0";

        public static event Action<string> OnSaveCompleted;
        public static event Action<string, string> OnSaveLoadError;

        #region Synchronous Operations (tests, headless verification, tooling)

        public static bool SaveContext(string slotID, GameStateContext ctx, double playtimeSeconds = 0)
        {
            if (ctx == null) return false;

            SavePayloadDTO? payload = SavePayloadMapper.ExtractPayload(slotID, ctx, playtimeSeconds);
            if (payload == null) return false;

            if (!TryPrepareSave(slotID, payload, out _, out string savePath, out string tempPath, out string json))
            {
                return false;
            }

            try
            {
                File.WriteAllText(tempPath, json, Encoding.UTF8);
                CommitAtomicReplace(tempPath, savePath);

                Debug.Log($"[SaveDiskIO] Saved slot '{slotID}'.");
                OnSaveCompleted?.Invoke(slotID);
                return true;
            }
            catch (Exception ex)
            {
                ReportSaveFailure(slotID, tempPath, ex);
                return false;
            }
        }

        public static GameStateContext LoadContext(string slotID)
        {
            if (!TryGetSlotDirectory(slotID, out string slotDir)) return null;

            foreach (string path in CandidatePaths(slotDir))
            {
                GameStateContext? context = ReadContext(path, slotID);
                if (context != null) return context;
            }

            Debug.LogError($"[SaveDiskIO] No readable save found for slot '{slotID}'.");
            OnSaveLoadError?.Invoke(slotID, "No readable save found.");
            return null;
        }

        public static SaveMetadataDTO LoadMetadata(string slotID)
        {
            if (!TryGetSlotDirectory(slotID, out string slotDir)) return null;

            foreach (string path in CandidatePaths(slotDir))
            {
                SavePayloadDTO payload = ReadPayload(path);
                if (payload?.Metadata != null) return payload.Metadata;
            }

            return ReadLegacyMetadata(slotDir);
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

        #endregion

        #region Asynchronous Operations (overworld save without blocking the frame)

        public static async Task<bool> SaveContextAsync(string slotID, GameStateContext ctx, double playtimeSeconds = 0)
        {
            if (ctx == null) return false;

            // Serialization stays on the calling thread: it walks the live graph, which only the
            // main thread may enumerate. Only the disk write is asynchronous.
            SavePayloadDTO? payload = SavePayloadMapper.ExtractPayload(slotID, ctx, playtimeSeconds);
            if (payload == null) return false;

            if (!TryPrepareSave(slotID, payload, out _, out string savePath, out string tempPath, out string json))
            {
                return false;
            }

            try
            {
                await WriteTextToFileAsync(tempPath, json);
                CommitAtomicReplace(tempPath, savePath);

                Debug.Log($"[SaveDiskIO] Saved slot '{slotID}'.");
                OnSaveCompleted?.Invoke(slotID);
                return true;
            }
            catch (Exception ex)
            {
                ReportSaveFailure(slotID, tempPath, ex);
                return false;
            }
        }

        public static async Task<GameStateContext> LoadContextAsync(string slotID)
        {
            if (!TryGetSlotDirectory(slotID, out string slotDir)) return null;

            foreach (string path in CandidatePaths(slotDir))
            {
                if (!File.Exists(path)) continue;

                GameStateContext? context = await ReadContextAsync(path, slotID);
                if (context != null) return context;
            }

            Debug.LogError($"[SaveDiskIO] No readable save found for slot '{slotID}'.");
            OnSaveLoadError?.Invoke(slotID, "No readable save found.");
            return null;
        }

        public static async Task<SaveMetadataDTO> LoadMetadataAsync(string slotID)
        {
            if (!TryGetSlotDirectory(slotID, out string slotDir)) return null;

            foreach (string path in CandidatePaths(slotDir))
            {
                if (!File.Exists(path)) continue;

                SavePayloadDTO payload = await ReadPayloadAsync(path);
                if (payload?.Metadata != null) return payload.Metadata;
            }

            return ReadLegacyMetadata(slotDir);
        }

        public static async Task<List<SaveMetadataDTO>> LoadAllMetadataAsync()
        {
            var list = new List<SaveMetadataDTO>();
            SavePaths.EnsureDirectoryExists(SavePaths.RootSaveDirectory);

            foreach (string dir in Directory.GetDirectories(SavePaths.RootSaveDirectory))
            {
                string slotID = Path.GetFileName(dir);
                if (!SavePaths.IsValidSlotID(slotID)) continue;

                SaveMetadataDTO metadata = await LoadMetadataAsync(slotID);
                if (metadata != null) list.Add(metadata);
            }

            list.Sort((a, b) => string.Compare(b.LastPlayedTimestamp, a.LastPlayedTimestamp, StringComparison.Ordinal));
            return list;
        }

        #endregion

        #region Internal File Helpers

        private static bool TryGetSlotDirectory(string slotID, out string slotDir)
        {
            slotDir = "";
            if (!SavePaths.IsValidSlotID(slotID))
            {
                Debug.LogError($"[SaveDiskIO] Rejected invalid slot id '{slotID}'.");
                return false;
            }

            slotDir = SavePaths.GetSlotPath(slotID!);
            return true;
        }

        private static bool TryPrepareSave(
            string slotID,
            SavePayloadDTO payload,
            out string slotDir,
            out string savePath,
            out string tempPath,
            out string json)
        {
            slotDir = savePath = tempPath = json = "";

            if (!TryGetSlotDirectory(slotID, out slotDir)) return false;

            if (payload.Metadata == null) payload.Metadata = new SaveMetadataDTO();
            payload.Metadata.SlotID = slotID;
            payload.Metadata.LastPlayedTimestamp = DateTime.UtcNow.ToString("o");

            SavePaths.EnsureDirectoryExists(slotDir);

            savePath = Path.Combine(slotDir, SavePaths.SAVE_FILE_NAME);
            tempPath = savePath + SavePaths.TEMP_SUFFIX;
            json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            return true;
        }

        /// <summary>Primary, its backup, then the legacy two-file layout's payload and its backup.</summary>
        private static IEnumerable<string> CandidatePaths(string slotDir)
        {
            string primary = Path.Combine(slotDir, SavePaths.SAVE_FILE_NAME);
            yield return primary;
            yield return primary + SavePaths.BACKUP_SUFFIX;

            string legacy = Path.Combine(slotDir, SavePaths.PAYLOAD_FILE_NAME);
            yield return legacy;
            yield return legacy + SavePaths.BACKUP_SUFFIX;
        }

        private static GameStateContext? ReadContext(string path, string slotID)
        {
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return BuildContext(json, path, slotID);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDiskIO] Failed reading '{path}': {ex.Message}");
                return null;
            }
        }

        private static async Task<GameStateContext> ReadContextAsync(string path, string slotID)
        {
            try
            {
                string json = await ReadTextFromFileAsync(path);
                return BuildContext(json, path, slotID);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDiskIO] Failed reading '{path}': {ex.Message}");
                return null;
            }
        }

        private static GameStateContext? BuildContext(string json, string path, string slotID)
        {
            SavePayloadDTO? payload = JsonConvert.DeserializeObject<SavePayloadDTO>(json);

            if (payload?.Context?.Cultivator == null)
            {
                Debug.LogWarning($"[SaveDiskIO] '{path}' is incomplete; trying the next candidate.");
                return null;
            }

            if (!SavePayloadMapper.MigratePayload(payload, slotID)) return null;

            return SavePayloadMapper.CreateContextFromPayload(payload);
        }

        private static SavePayloadDTO? ReadPayload(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                return JsonConvert.DeserializeObject<SavePayloadDTO>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDiskIO] Failed reading payload '{path}': {ex.Message}");
                return null;
            }
        }

        private static async Task<SavePayloadDTO?> ReadPayloadAsync(string path)
        {
            try
            {
                string json = await ReadTextFromFileAsync(path);
                return await Task.Run(() => JsonConvert.DeserializeObject<SavePayloadDTO>(json));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDiskIO] Failed reading payload '{path}': {ex.Message}");
                return null;
            }
        }

        private static SaveMetadataDTO? ReadLegacyMetadata(string slotDir)
        {
            string legacyMetaPath = Path.Combine(slotDir, SavePaths.METADATA_FILE_NAME);
            if (!File.Exists(legacyMetaPath)) return null;

            try
            {
                return JsonConvert.DeserializeObject<SaveMetadataDTO>(File.ReadAllText(legacyMetaPath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDiskIO] Legacy metadata unreadable in '{slotDir}': {ex.Message}");
                return null;
            }
        }

        public static bool MigratePayload(SavePayloadDTO payload, string slotID = "")
        {
            return SavePayloadMapper.MigratePayload(payload, slotID);
        }

        internal static void RaiseSaveLoadError(string slotID, string error)
        {
            OnSaveLoadError?.Invoke(slotID, error);
        }

        private static async Task WriteTextToFileAsync(string filePath, string content)
        {
            byte[] encodedText = Encoding.UTF8.GetBytes(content);
            using (FileStream sourceStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.WriteAsync(encodedText, 0, encodedText.Length).ConfigureAwait(false);
            }
        }

        private static async Task<string> ReadTextFromFileAsync(string filePath)
        {
            using (FileStream sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var reader = new StreamReader(sourceStream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        private static void CommitAtomicReplace(string tempPath, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
                return;
            }

            // Single filesystem operation: the target always contains either the old or the new
            // bytes. The superseded file is kept as .bak for recovery.
            File.Replace(tempPath, targetPath, targetPath + SavePaths.BACKUP_SUFFIX, ignoreMetadataErrors: true);
        }

        private static void ReportSaveFailure(string slotID, string tempPath, Exception ex)
        {
            Debug.LogError($"[SaveDiskIO] Failed saving slot '{slotID}': {ex.Message}\n{ex.StackTrace}");
            CleanTempFile(tempPath);
            OnSaveLoadError?.Invoke(slotID, ex.Message);
        }

        private static void CleanTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // A leftover temp file is harmless: the loader ignores it.
            }
        }

        #endregion
    }

    public static partial class SaveLoadEngine
    {
        public static event Action<string> OnSaveCompleted
        {
            add => SaveDiskIO.OnSaveCompleted += value;
            remove => SaveDiskIO.OnSaveCompleted -= value;
        }

        public static event Action<string, string> OnSaveLoadError
        {
            add => SaveDiskIO.OnSaveLoadError += value;
            remove => SaveDiskIO.OnSaveLoadError -= value;
        }

        public static bool SaveContext(string slotID, GameStateContext ctx, double playtimeSeconds = 0)
            => SaveDiskIO.SaveContext(slotID, ctx, playtimeSeconds);

        public static GameStateContext LoadContext(string slotID)
            => SaveDiskIO.LoadContext(slotID);

        public static bool DeleteSlot(string slotID)
            => SaveDiskIO.DeleteSlot(slotID);

        public static bool SlotExists(string slotID)
            => SaveDiskIO.SlotExists(slotID);

        public static Task<bool> SaveContextAsync(string slotID, GameStateContext ctx, double playtimeSeconds = 0)
            => SaveDiskIO.SaveContextAsync(slotID, ctx, playtimeSeconds);

        public static Task<GameStateContext> LoadContextAsync(string slotID)
            => SaveDiskIO.LoadContextAsync(slotID);

        public static SaveMetadataDTO LoadMetadata(string slotID)
            => SaveDiskIO.LoadMetadata(slotID);

        public static Task<SaveMetadataDTO> LoadMetadataAsync(string slotID)
            => SaveDiskIO.LoadMetadataAsync(slotID);

        public static Task<List<SaveMetadataDTO>> LoadAllMetadataAsync()
            => SaveDiskIO.LoadAllMetadataAsync();

        public static void Initialize() { }
    }
}
