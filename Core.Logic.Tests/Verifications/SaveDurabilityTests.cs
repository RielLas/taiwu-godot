using System;
using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using TaiwuGu.Core;
using TaiwuGu.Serialization;

namespace TaiwuGu.Tests
{
    /// <summary>
    /// Phase 3b: a save is either the previous state or the new one — never neither, never a
    /// half-written mixture. These tests recreate every intermediate state a crash can leave on
    /// disk and assert the loader always recovers.
    /// </summary>
    public class SaveDurabilityTests : IDisposable
    {
        private const string SlotA = "durability_a";
        private const string SlotB = "durability_b";

        private readonly ITestOutputHelper _output;
        private readonly string _originalRoot;
        private readonly string _testRoot;

        public SaveDurabilityTests(ITestOutputHelper output)
        {
            _output = output;
            _originalRoot = SavePaths.RootSaveDirectory;
            _testRoot = Path.Combine(Path.GetTempPath(), "taiwu_durability_" + Guid.NewGuid().ToString("N"));
            SavePaths.RootSaveDirectory = _testRoot;
            SaveLoadEngine.Initialize();
        }

        public void Dispose()
        {
            SavePaths.RootSaveDirectory = _originalRoot;
            try
            {
                if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, true);
            }
            catch
            {
                // Best-effort temp cleanup.
            }
        }

        [Fact]
        public void Save_WritesOneFileAndLeavesNoTempBehind()
        {
            Assert.True(SaveContext(SlotA, 100));

            string slotDir = SavePaths.GetSlotPath(SlotA);
            string[] files = Directory.GetFiles(slotDir);

            Assert.Single(files);
            Assert.Equal(SavePaths.SAVE_FILE_NAME, Path.GetFileName(files[0]));
            Assert.False(File.Exists(SavePaths.GetSavePath(SlotA) + SavePaths.TEMP_SUFFIX));
            Assert.DoesNotContain(files, f => f.EndsWith(SavePaths.METADATA_FILE_NAME, StringComparison.Ordinal));
        }

        [Fact]
        public void CrashBeforeReplace_LeavesPreviousSaveReadable()
        {
            Assert.True(SaveContext(SlotA, 100));
            Assert.True(SaveContext(SlotA, 200));

            // A crash while writing the next save: the temp file exists, the install never happened.
            File.WriteAllText(SavePaths.GetSavePath(SlotA) + SavePaths.TEMP_SUFFIX, "HALF_WRITTEN_CHUNK");

            GameStateContext loaded = SaveLoadEngine.LoadContext(SlotA);

            Assert.NotNull(loaded);
            Assert.Equal(200, loaded!.Cultivator.PrimevalStones);
        }

        [Fact]
        public void MissingPrimary_RecoversThePreviousSaveFromBackup()
        {
            Assert.True(SaveContext(SlotA, 100));
            Assert.True(SaveContext(SlotA, 200));

            // The replace removed the old file and the process died before installing the new one.
            File.Delete(SavePaths.GetSavePath(SlotA));

            GameStateContext loaded = SaveLoadEngine.LoadContext(SlotA);

            Assert.NotNull(loaded);
            Assert.Equal(100, loaded!.Cultivator.PrimevalStones);
        }

        [Fact]
        public void CorruptPrimary_RecoversFromBackup()
        {
            Assert.True(SaveContext(SlotA, 100));
            Assert.True(SaveContext(SlotA, 200));

            File.WriteAllText(SavePaths.GetSavePath(SlotA), "{\"SaveVersion\":\"2.1.0\",\"Context\":null}");

            GameStateContext loaded = SaveLoadEngine.LoadContext(SlotA);

            Assert.NotNull(loaded);
            Assert.Equal(100, loaded!.Cultivator.PrimevalStones);
        }

        [Fact]
        public void FirstEverSave_IsImmediatelyReadable()
        {
            Assert.False(File.Exists(SavePaths.GetSavePath(SlotA)));

            Assert.True(SaveContext(SlotA, 333));

            GameStateContext loaded = SaveLoadEngine.LoadContext(SlotA);
            Assert.NotNull(loaded);
            Assert.Equal(333, loaded!.Cultivator.PrimevalStones);
        }

        [Fact]
        public void TwoSlots_AreIndependentAndKeepTheirOwnBackups()
        {
            Assert.True(SaveContext(SlotA, 111));
            Assert.True(SaveContext(SlotB, 222));
            Assert.True(SaveContext(SlotA, 112));

            Assert.Equal(112, SaveLoadEngine.LoadContext(SlotA)!.Cultivator.PrimevalStones);
            Assert.Equal(222, SaveLoadEngine.LoadContext(SlotB)!.Cultivator.PrimevalStones);
            AssertBackupHoldsStones(SlotA, 111);
            Assert.False(File.Exists(SavePaths.GetSavePath(SlotB) + SavePaths.BACKUP_SUFFIX));
        }

        [Fact]
        public void InvalidSlotIds_AreRejectedEverywhere()
        {
            var ctx = GameStateContext.CreateDefault();

            foreach (string invalid in new[] { "../evil", "..", "a/b", "a\\b", "", "   ", "name with space", new string('x', 33) })
            {
                Assert.False(SavePaths.IsValidSlotID(invalid), $"'{invalid}' must not be a valid slot id.");
                Assert.False(SaveLoadEngine.SaveContext(invalid, ctx), $"Saving to '{invalid}' must be rejected.");
                Assert.False(SaveLoadEngine.SlotExists(invalid));
                Assert.False(SaveLoadEngine.DeleteSlot(invalid));
                Assert.Null(SaveLoadEngine.LoadContext(invalid));
            }

            Assert.Throws<ArgumentException>(() => SavePaths.GetSlotPath("../evil"));
            Assert.True(SavePaths.IsValidSlotID("auto_save-1"));
        }

        [Fact]
        public void Load_ReturnsAFreshContextAndLeavesTheLiveOneUntouched()
        {
            Assert.True(SaveContext(SlotA, 500));

            var live = GameStateContext.CreateDefault();
            var provider = new GameStateProvider(live);

            GameStateContext loaded = SaveLoadEngine.LoadContext(SlotA);
            Assert.NotNull(loaded);
            Assert.NotSame(provider.Current, loaded);

            provider.Replace(loaded!);

            Assert.Same(loaded, provider.Current);
            Assert.Equal(500, provider.Current.Cultivator.PrimevalStones);
            Assert.Equal(250, live.Cultivator.PrimevalStones);
        }

        [Fact]
        public void Save_StaysWithinAPlayableBudget()
        {
            var ctx = GameStateContext.CreateDefault();

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                Assert.True(SaveContext(SlotA, 400 + i));
            }
            stopwatch.Stop();

            double perSaveMs = stopwatch.Elapsed.TotalMilliseconds / 5.0;
            _output.WriteLine($"Synchronous save (serialize + atomic replace) averaged {perSaveMs:F1} ms over 5 runs of a 30x30 sector.");

            Assert.True(perSaveMs < 2000, $"A save took {perSaveMs:F0} ms, which is far outside the expected range.");
        }

        private static bool SaveContext(string slot, int stones)
        {
            var ctx = GameStateContext.CreateDefault();
            ctx.Cultivator.PrimevalStones = stones;
            return SaveLoadEngine.SaveContext(slot, ctx);
        }

        private static void AssertBackupHoldsStones(string slot, int expectedStones)
        {
            string backupPath = SavePaths.GetSavePath(slot) + SavePaths.BACKUP_SUFFIX;
            Assert.True(File.Exists(backupPath), $"Expected a backup at '{backupPath}'.");

            // The backup is the superseded file, not the one the loader prefers.
            Assert.Contains($"\"PrimevalStones\": {expectedStones}", File.ReadAllText(backupPath));
        }
    }
}
