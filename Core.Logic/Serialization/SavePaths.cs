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
        public const string METADATA_FILE_NAME = "metadata.json";
        public const string PAYLOAD_FILE_NAME = "payload.json";
        public const string TEMP_SUFFIX = ".tmp";
        public const string BACKUP_SUFFIX = ".bak";

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

        public static string GetSlotPath(string slotID)
        {
            return Path.Combine(RootSaveDirectory, slotID);
        }

        public static string GetMetadataPath(string slotID)
        {
            return Path.Combine(GetSlotPath(slotID), METADATA_FILE_NAME);
        }

        public static string GetPayloadPath(string slotID)
        {
            return Path.Combine(GetSlotPath(slotID), PAYLOAD_FILE_NAME);
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
