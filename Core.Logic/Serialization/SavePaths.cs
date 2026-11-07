using System;
using System.IO;

namespace TaiwuGu.Serialization
{
    /// <summary>
    /// File system paths and directory resolver for save data storage.
    /// Provides configurable root directory with zero engine dependencies.
    /// </summary>
    public static class SavePaths
    {
        public const string SAVE_FOLDER_NAME = "Saves";

        /// <summary>One file per slot: metadata and state travel together so they cannot disagree.</summary>
        public const string SAVE_FILE_NAME = "save.json";

        // Legacy two-file layout (before the single-file commit). Read-only compatibility.
        public const string METADATA_FILE_NAME = "metadata.json";
        public const string PAYLOAD_FILE_NAME = "payload.json";

        public const string TEMP_SUFFIX = ".tmp";
        public const string BACKUP_SUFFIX = ".bak";

        public const int MAX_SLOT_ID_LENGTH = 32;

        private static string? _cachedRootSaveDir;

        public static string BaseSaveDirectory
        {
            get => RootSaveDirectory;
            set => RootSaveDirectory = value;
        }

        public static string RootSaveDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_cachedRootSaveDir))
                {
                    _cachedRootSaveDir = ResolveDefaultRootDirectory();
                }
                return _cachedRootSaveDir;
            }
            set => _cachedRootSaveDir = value;
        }

        public static void Initialize(string? customRootPath = null)
        {
            if (!string.IsNullOrEmpty(customRootPath))
            {
                _cachedRootSaveDir = customRootPath;
            }
            else if (string.IsNullOrEmpty(_cachedRootSaveDir))
            {
                _cachedRootSaveDir = ResolveDefaultRootDirectory();
            }
            EnsureDirectoryExists(_cachedRootSaveDir);
        }

        private static string ResolveDefaultRootDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".taiwu_saves", SAVE_FOLDER_NAME);
        }

        /// <summary>
        /// A slot id is a file name: 1–32 characters from [A-Za-z0-9_-]. Anything else (path
        /// separators, dots, control characters) is rejected before it can reach the filesystem.
        /// </summary>
        public static bool IsValidSlotID(string? slotID)
        {
            if (string.IsNullOrEmpty(slotID) || slotID.Length > MAX_SLOT_ID_LENGTH) return false;

            for (int i = 0; i < slotID.Length; i++)
            {
                char c = slotID[i];
                bool allowed = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '_'
                    || c == '-';

                if (!allowed) return false;
            }

            return true;
        }

        /// <summary>Resolves a slot directory. Throws for ids that are not safe file names.</summary>
        public static string GetSlotPath(string slotID)
        {
            if (!IsValidSlotID(slotID))
            {
                throw new ArgumentException(
                    $"Invalid save slot id '{slotID}'. Expected 1-{MAX_SLOT_ID_LENGTH} characters from [A-Za-z0-9_-].",
                    nameof(slotID));
            }

            return Path.Combine(RootSaveDirectory, slotID!);
        }

        public static string GetSavePath(string slotID)
        {
            return Path.Combine(GetSlotPath(slotID), SAVE_FILE_NAME);
        }

        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }

    public static partial class SaveLoadEngine
    {
        public static string RootSaveDirectory
        {
            get => SavePaths.RootSaveDirectory;
            set => SavePaths.RootSaveDirectory = value;
        }

        public static void Initialize(string? customRootPath = null) => SavePaths.Initialize(customRootPath);
        public static string GetSlotPath(string slotID) => SavePaths.GetSlotPath(slotID);
        public static void EnsureDirectoryExists(string path) => SavePaths.EnsureDirectoryExists(path);
    }
}
