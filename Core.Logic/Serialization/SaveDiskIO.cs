using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using TaiwuGu.Core;

namespace TaiwuGu.Serialization
{
    /// <summary>
    /// Disk I/O subsystem providing atomic temp-write-replace protocol, backup recovery,
    /// Newtonsoft.Json serialization, and non-crashing save version compatibility checking.
    /// Engine-agnostic implementation for Core.Logic.
    /// </summary>
    public static class SaveDiskIO
    {
        public const string CURRENT_SAVE_VERSION = "2.0.0";

        public static event Action<string> OnSaveCompleted;
        public static event Action<string, string> OnSaveLoadError;

        #region Synchronous Operations (Direct / Verification / Edit Mode)

        public static bool SaveContext(string slotID, GameStateContext ctx, double playtimeSeconds = 0)
        {
            if (string.IsNullOrEmpty(slotID) || ctx == null) return false;

            string slotDir = SavePaths.GetSlotPath(slotID);
            SavePaths.EnsureDirectoryExists(slotDir);

            string metaPath = Path.Combine(slotDir, SavePaths.METADATA_FILE_NAME);
            string metaTempPath = metaPath + SavePaths.TEMP_SUFFIX;
            string payloadPath = Path.Combine(slotDir, SavePaths.PAYLOAD_FILE_NAME);
            string payloadTempPath = payloadPath + SavePaths.TEMP_SUFFIX;

            try
            {
                SavePayloadDTO payload = SavePayloadMapper.ExtractPayload(slotID, ctx, playtimeSeconds);
                SaveMetadataDTO metadata = payload.Metadata;

                string metaJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                string payloadJson = JsonConvert.SerializeObject(payload, Formatting.Indented);

                File.WriteAllText(payloadTempPath, payloadJson, Encoding.UTF8);
                File.WriteAllText(metaTempPath, metaJson, Encoding.UTF8);

                CommitAtomicReplace(payloadTempPath, payloadPath);
                CommitAtomicReplace(metaTempPath, metaPath);

                Debug.Log($"[SaveDiskIO] Synchronous atomic save succeeded for slot '{slotID}'.");
                OnSaveCompleted?.Invoke(slotID);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveDiskIO] Critical failure saving slot '{slotID}': {ex.Message}\n{ex.StackTrace}");
                CleanTempFile(metaTempPath);
                CleanTempFile(payloadTempPath);
                OnSaveLoadError?.Invoke(slotID, ex.Message);
                return false;
            }
        }

        public static GameStateContext LoadContext(string slotID)
        {
            string slotDir = SavePaths.GetSlotPath(slotID);
            string payloadPath = Path.Combine(slotDir, SavePaths.PAYLOAD_FILE_NAME);
            string backupPath = payloadPath + SavePaths.BACKUP_SUFFIX;

            if (!File.Exists(payloadPath))
            {
                if (File.Exists(backupPath))
                {
                    Debug.LogWarning($"[SaveDiskIO] Primary payload missing. Attempting restoration from backup '{backupPath}'...");
                    payloadPath = backupPath;
                }
                else
                {
                    Debug.LogError($"[SaveDiskIO] No save payload found for slot '{slotID}'.");
                    return null;
                }
            }

            try
            {
                string json = File.ReadAllText(payloadPath, Encoding.UTF8);
                SavePayloadDTO payload = JsonConvert.DeserializeObject<SavePayloadDTO>(json);

                if (payload == null || payload.Context == null || payload.Context.Cultivator == null)
                {
                    if (payloadPath != backupPath && File.Exists(backupPath))
                    {
                        Debug.LogWarning($"[SaveDiskIO] Payload corrupt, recovering from backup '{backupPath}'...");
                        json = File.ReadAllText(backupPath, Encoding.UTF8);
                        payload = JsonConvert.DeserializeObject<SavePayloadDTO>(json);
                    }
                    if (payload == null || payload.Context == null || payload.Context.Cultivator == null)
                    {
                        throw new InvalidDataException("Deserialized payload state is null or incomplete.");
                    }
                }

                if (!MigratePayload(payload, slotID))
                {
                    return null;
                }
                return SavePayloadMapper.CreateContextFromPayload(payload);
            }
            catch (Exception ex)
            {
                if (payloadPath != backupPath && File.Exists(backupPath))
                {
                    try
                    {
                        Debug.LogWarning($"[SaveDiskIO] Primary load threw exception, falling back to backup '{backupPath}'...");
                        string json = File.ReadAllText(backupPath, Encoding.UTF8);
                        SavePayloadDTO payload = JsonConvert.DeserializeObject<SavePayloadDTO>(json);
                        if (payload != null && payload.Context != null && payload.Context.Cultivator != null)
                        {
                            if (!MigratePayload(payload, slotID))
                            {
                                return null;
                            }
                            return SavePayloadMapper.CreateContextFromPayload(payload);
                        }
                    }
                    catch { }
                }

                Debug.LogError($"[SaveDiskIO] Corrupted payload detected in slot '{slotID}': {ex.Message}");
                OnSaveLoadError?.Invoke(slotID, $"Corrupted Save: {ex.Message}");
                return null;
            }
        }

        public static bool DeleteSlot(string slotID)
        {
            string slotDir = SavePaths.GetSlotPath(slotID);
            if (Directory.Exists(slotDir))
            {
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
            return false;
        }

        public static bool SlotExists(string slotID)
        {
            string slotDir = SavePaths.GetSlotPath(slotID);
            string payloadPath = Path.Combine(slotDir, SavePaths.PAYLOAD_FILE_NAME);
            string metaPath = Path.Combine(slotDir, SavePaths.METADATA_FILE_NAME);
            return Directory.Exists(slotDir) && (File.Exists(payloadPath) || File.Exists(payloadPath + SavePaths.BACKUP_SUFFIX)) && File.Exists(metaPath);
        }

        #endregion

        #region Asynchronous Operations (Non-Blocking Overworld Saving)

        public static async Task<bool> SaveContextAsync(string slotID, GameStateContext ctx, double playtimeSeconds = 0)
        {
            if (string.IsNullOrEmpty(slotID) || ctx == null) return false;
            SavePayloadDTO payload = SavePayloadMapper.ExtractPayload(slotID, ctx, playtimeSeconds);
            return await SaveGameAsync(slotID, payload.Metadata, payload);
        }

        public static async Task<GameStateContext> LoadContextAsync(string slotID)
        {
            SavePayloadDTO payload = await LoadPayloadAsync(slotID);
            if (payload == null) return null;
            return SavePayloadMapper.CreateContextFromPayload(payload);
        }

        public static async Task<bool> SaveGameAsync(string slotID, SaveMetadataDTO metadata, SavePayloadDTO payload)
        {
            if (string.IsNullOrEmpty(slotID))
            {
                Debug.LogError("[SaveDiskIO] SlotID cannot be null or empty.");
                return false;
            }

            string slotDir = SavePaths.GetSlotPath(slotID);
            SavePaths.EnsureDirectoryExists(slotDir);

            string metaPath = Path.Combine(slotDir, SavePaths.METADATA_FILE_NAME);
            string metaTempPath = metaPath + SavePaths.TEMP_SUFFIX;
            string payloadPath = Path.Combine(slotDir, SavePaths.PAYLOAD_FILE_NAME);
            string payloadTempPath = payloadPath + SavePaths.TEMP_SUFFIX;

            try
            {
                metadata.SlotID = slotID;
                metadata.LastPlayedTimestamp = DateTime.UtcNow.ToString("o");
                payload.Metadata = metadata;

                // Snapshot JSON synchronously on the main thread before asynchronous file I/O
                // to prevent background thread enumeration of live mutated lists.
                string metaJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                string payloadJson = JsonConvert.SerializeObject(payload, Formatting.Indented);

                await WriteTextToFileAsync(payloadTempPath, payloadJson);
                await WriteTextToFileAsync(metaTempPath, metaJson);

                CommitAtomicReplace(payloadTempPath, payloadPath);
                CommitAtomicReplace(metaTempPath, metaPath);

                Debug.Log($"[SaveDiskIO] Atomic save succeeded for slot '{slotID}'.");
                OnSaveCompleted?.Invoke(slotID);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveDiskIO] Critical failure saving slot '{slotID}': {ex.Message}\n{ex.StackTrace}");
                CleanTempFile(metaTempPath);
                CleanTempFile(payloadTempPath);
                OnSaveLoadError?.Invoke(slotID, ex.Message);
                return false;
            }
        }

        public static SaveMetadataDTO LoadMetadata(string slotID)
        {
            string slotDir = SavePaths.GetSlotPath(slotID);
            string metaPath = Path.Combine(slotDir, SavePaths.METADATA_FILE_NAME);

            if (!File.Exists(metaPath)) return null;

            try
            {
                string json = File.ReadAllText(metaPath);
                return JsonConvert.DeserializeObject<SaveMetadataDTO>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDiskIO] Metadata corrupted for slot '{slotID}': {ex.Message}");
                return null;
            }
        }

        public static async Task<SaveMetadataDTO> LoadMetadataAsync(string slotID)
        {
            string slotDir = SavePaths.GetSlotPath(slotID);
            string metaPath = Path.Combine(slotDir, SavePaths.METADATA_FILE_NAME);

            if (!File.Exists(metaPath)) return null;

            try
            {
                string json = await ReadTextFromFileAsync(metaPath);
                return await Task.Run(() => JsonConvert.DeserializeObject<SaveMetadataDTO>(json));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveDiskIO] Metadata corrupted for slot '{slotID}': {ex.Message}");
                return null;
            }
        }

        public static async Task<List<SaveMetadataDTO>> LoadAllMetadataAsync()
        {
            List<SaveMetadataDTO> list = new List<SaveMetadataDTO>();
            SavePaths.EnsureDirectoryExists(SavePaths.RootSaveDirectory);

            string[] directories = Directory.GetDirectories(SavePaths.RootSaveDirectory);
            foreach (var dir in directories)
            {
                string slotID = Path.GetFileName(dir);
                SaveMetadataDTO meta = await LoadMetadataAsync(slotID);
                if (meta != null)
                {
                    list.Add(meta);
                }
            }

            list.Sort((a, b) => string.Compare(b.LastPlayedTimestamp, a.LastPlayedTimestamp, StringComparison.Ordinal));
            return list;
        }

        public static async Task<SavePayloadDTO> LoadPayloadAsync(string slotID)
        {
            string slotDir = SavePaths.GetSlotPath(slotID);
            string payloadPath = Path.Combine(slotDir, SavePaths.PAYLOAD_FILE_NAME);
            string backupPath = payloadPath + SavePaths.BACKUP_SUFFIX;

            if (!File.Exists(payloadPath))
            {
                if (File.Exists(backupPath))
                {
                    Debug.LogWarning($"[SaveDiskIO] Primary payload missing. Attempting restoration from backup '{backupPath}'...");
                    payloadPath = backupPath;
                }
                else
                {
                    Debug.LogError($"[SaveDiskIO] No save payload found for slot '{slotID}'.");
                    return null;
                }
            }

            try
            {
                string json = await ReadTextFromFileAsync(payloadPath);
                SavePayloadDTO payload = await Task.Run(() => JsonConvert.DeserializeObject<SavePayloadDTO>(json));

                if (payload == null || payload.Context == null || payload.Context.Cultivator == null)
                {
                    if (payloadPath != backupPath && File.Exists(backupPath))
                    {
                        Debug.LogWarning($"[SaveDiskIO] Primary payload corrupt, falling back to backup '{backupPath}'...");
                        json = await ReadTextFromFileAsync(backupPath);
                        payload = await Task.Run(() => JsonConvert.DeserializeObject<SavePayloadDTO>(json));
                    }
                    if (payload == null || payload.Context == null || payload.Context.Cultivator == null)
                    {
                        throw new InvalidDataException("Deserialized payload state is null or incomplete.");
                    }
                }

                if (!MigratePayload(payload, slotID))
                {
                    return null;
                }
                return payload;
            }
            catch (Exception ex)
            {
                if (payloadPath != backupPath && File.Exists(backupPath))
                {
                    try
                    {
                        Debug.LogWarning($"[SaveDiskIO] Exception loading primary, falling back to backup '{backupPath}'...");
                        string json = await ReadTextFromFileAsync(backupPath);
                        SavePayloadDTO payload = await Task.Run(() => JsonConvert.DeserializeObject<SavePayloadDTO>(json));
                        if (payload != null && payload.Context != null && payload.Context.Cultivator != null)
                        {
                            if (!MigratePayload(payload, slotID))
                            {
                                return null;
                            }
                            return payload;
                        }
                    }
                    catch { }
                }

                Debug.LogError($"[SaveDiskIO] Corrupted payload detected in slot '{slotID}': {ex.Message}");
                OnSaveLoadError?.Invoke(slotID, $"Corrupted Save: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Internal Atomic File Helpers & Version Validation

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
            using (StreamReader reader = new StreamReader(sourceStream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        private static void CommitAtomicReplace(string tempPath, string targetPath)
        {
            string backupPath = targetPath + SavePaths.BACKUP_SUFFIX;

            if (File.Exists(targetPath))
            {
                File.Copy(targetPath, backupPath, true);
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);
        }

        private static void CleanTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch { }
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

        public static Task<bool> SaveGameAsync(string slotID, SaveMetadataDTO metadata, SavePayloadDTO payload)
            => SaveDiskIO.SaveGameAsync(slotID, metadata, payload);

        public static SaveMetadataDTO LoadMetadata(string slotID)
            => SaveDiskIO.LoadMetadata(slotID);

        public static Task<SaveMetadataDTO> LoadMetadataAsync(string slotID)
            => SaveDiskIO.LoadMetadataAsync(slotID);

        public static Task<List<SaveMetadataDTO>> LoadAllMetadataAsync()
            => SaveDiskIO.LoadAllMetadataAsync();

        public static void Initialize() { }
    }
}
