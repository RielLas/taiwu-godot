using System;
using System.Threading.Tasks;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Serialization;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Direct POOF Save/Load operations coordinating SaveDiskIO + WorldStateProvider atomic swaps.
    /// </summary>
    public static class SaveSystem
    {
        private static bool _isSaving = false;
        public static bool IsSaving => _isSaving;
        public static double PlaytimeSeconds { get; set; } = 0;

        public static void TickPlaytime(float dt)
        {
            if (dt > 0f) PlaytimeSeconds += dt;
        }

        public static async Task<bool> SaveAsync(string slotID, WorldStore world)
        {
            if (!SavePaths.IsValidSlotID(slotID))
            {
                WorldSignals.EmitGameSaved(slotID ?? "", false);
                return false;
            }

            if (_isSaving)
            {
                Debug.LogWarning($"[SaveSystem] Save to '{slotID}' ignored: another save is in flight.");
                return false;
            }

            _isSaving = true;
            try
            {
                bool ok = await SaveDiskIO.SaveWorldAsync(slotID, world, PlaytimeSeconds);
                WorldSignals.EmitGameSaved(slotID, ok);
                return ok;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Save to '{slotID}' threw: {ex.Message}");
                WorldSignals.EmitGameSaved(slotID, false);
                return false;
            }
            finally
            {
                _isSaving = false;
            }
        }

        public static bool LoadAndSwap(string slotID, WorldStateProvider provider)
        {
            if (!SavePaths.IsValidSlotID(slotID))
            {
                WorldSignals.EmitGameLoaded(slotID ?? "", false);
                return false;
            }

            WorldStore? loaded = SaveDiskIO.LoadWorld(slotID);
            if (loaded == null)
            {
                WorldSignals.EmitGameLoaded(slotID, false);
                return false;
            }

            provider.Replace(loaded);
            var meta = SaveDiskIO.LoadMetadata(slotID);
            if (meta != null) PlaytimeSeconds = meta.TotalPlaytimeSeconds;

            WorldSignals.EmitTopics(DirtyTopic.All);
            WorldSignals.EmitGameLoaded(slotID, true);
            return true;
        }

        public static bool DeleteSlot(string slotID)
        {
            bool ok = SaveDiskIO.DeleteSlot(slotID);
            WorldSignals.EmitSaveDeleted(slotID, ok);
            return ok;
        }
    }
}
