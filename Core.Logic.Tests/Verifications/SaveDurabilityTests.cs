using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using TaiwuGu.Entities;
using TaiwuGu.Serialization;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
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
            SavePaths.Initialize(_testRoot);
            WorldSignals.ClearAllSubscriptions();
        }

        public void Dispose()
        {
            WorldSignals.ClearAllSubscriptions();
            SavePaths.RootSaveDirectory = _originalRoot;
            try
            {
                if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, true);
            }
            catch { }
        }

        [Fact]
        public void Save_WritesOneFileAndLeavesNoTempBehind()
        {
            Assert.True(SaveWorldWithStones(SlotA, 100));

            string slotDir = SavePaths.GetSlotPath(SlotA);
            string[] files = Directory.GetFiles(slotDir);

            Assert.Single(files);
            Assert.Equal(SavePaths.SAVE_FILE_NAME, Path.GetFileName(files[0]));
            Assert.False(File.Exists(SavePaths.GetSavePath(SlotA) + SavePaths.TEMP_SUFFIX));
        }

        [Fact]
        public void CrashBeforeReplace_LeavesPreviousSaveReadable()
        {
            Assert.True(SaveWorldWithStones(SlotA, 100));
            Assert.True(SaveWorldWithStones(SlotA, 200));

            File.WriteAllText(SavePaths.GetSavePath(SlotA) + SavePaths.TEMP_SUFFIX, "HALF_WRITTEN_CHUNK");

            var loaded = SaveDiskIO.LoadWorld(SlotA);
            Assert.NotNull(loaded);
            Assert.Equal(200, loaded!.Get<CultivatorComponent>(loaded.PlayerEntity)!.PrimevalStones);
        }

        [Fact]
        public void MissingPrimary_And_CorruptPrimary_RecoverFromBackup()
        {
            Assert.True(SaveWorldWithStones(SlotA, 100));
            Assert.True(SaveWorldWithStones(SlotA, 200));

            File.Delete(SavePaths.GetSavePath(SlotA));
            var fromBackup = SaveDiskIO.LoadWorld(SlotA);
            Assert.NotNull(fromBackup);
            Assert.Equal(100, fromBackup!.Get<CultivatorComponent>(fromBackup.PlayerEntity)!.PrimevalStones);

            File.WriteAllText(SavePaths.GetSavePath(SlotA), "CORRUPTED_GARBAGE{{{");
            var recoveredCorrupt = SaveDiskIO.LoadWorld(SlotA);
            Assert.NotNull(recoveredCorrupt);
            Assert.Equal(100, recoveredCorrupt!.Get<CultivatorComponent>(recoveredCorrupt.PlayerEntity)!.PrimevalStones);
        }

        [Fact]
        public void TwoSlots_AreIndependentAndKeepTheirOwnBackups()
        {
            Assert.True(SaveWorldWithStones(SlotA, 111));
            Assert.True(SaveWorldWithStones(SlotB, 222));
            Assert.True(SaveWorldWithStones(SlotA, 112));

            Assert.Equal(112, SaveDiskIO.LoadWorld(SlotA)!.Get<CultivatorComponent>(SaveDiskIO.LoadWorld(SlotA)!.PlayerEntity)!.PrimevalStones);
            Assert.Equal(222, SaveDiskIO.LoadWorld(SlotB)!.Get<CultivatorComponent>(SaveDiskIO.LoadWorld(SlotB)!.PlayerEntity)!.PrimevalStones);

            string backupPath = SavePaths.GetSavePath(SlotA) + SavePaths.BACKUP_SUFFIX;
            Assert.True(File.Exists(backupPath));
            Assert.Contains("\"PrimevalStones\": 111", File.ReadAllText(backupPath));
            Assert.False(File.Exists(SavePaths.GetSavePath(SlotB) + SavePaths.BACKUP_SUFFIX));
        }

        [Fact]
        public void InvalidSlotIds_AreRejectedEverywhere()
        {
            var world = WorldFactory.CreateDefault();
            foreach (string invalid in new[] { "../evil", "..", "a/b", "a\\b", "", "   ", "name with space", new string('x', 33) })
            {
                Assert.False(SavePaths.IsValidSlotID(invalid));
                Assert.False(SaveDiskIO.SaveWorld(invalid, world));
                Assert.False(SaveDiskIO.SlotExists(invalid));
                Assert.False(SaveDiskIO.DeleteSlot(invalid));
                Assert.Null(SaveDiskIO.LoadWorld(invalid));
            }
            Assert.Throws<ArgumentException>(() => SavePaths.GetSlotPath("../evil"));
            Assert.True(SavePaths.IsValidSlotID("auto_save-1"));
        }

        [Fact]
        public async Task SaveSystem_AsyncSaveAndAtomicProviderSwap_WorksCleanly()
        {
            var world = WorldFactory.CreateDefault();
            world.Get<CultivatorComponent>(world.PlayerEntity)!.PrimevalStones = 450;

            bool savedEvent = false;
            WorldSignals.OnGameSaved += (slot, ok) => { if (slot == SlotB && ok) savedEvent = true; };

            Assert.True(await SaveSystem.SaveAsync(SlotB, world));
            Assert.True(savedEvent);

            var provider = new WorldStateProvider(WorldFactory.CreateDefault());
            var originalLive = provider.Current;
            originalLive.Get<CultivatorComponent>(originalLive.PlayerEntity)!.PrimevalStones = 99999;

            Assert.True(SaveSystem.LoadAndSwap(SlotB, provider));
            Assert.NotSame(originalLive, provider.Current);
            Assert.Equal(450, provider.Current.Get<CultivatorComponent>(provider.Current.PlayerEntity)!.PrimevalStones);
            Assert.Equal(99999, originalLive.Get<CultivatorComponent>(originalLive.PlayerEntity)!.PrimevalStones);
        }

        [Fact]
        public void Save_StaysWithinPlayableBudget()
        {
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++) Assert.True(SaveWorldWithStones(SlotA, 400 + i));
            stopwatch.Stop();

            double perSaveMs = stopwatch.Elapsed.TotalMilliseconds / 5.0;
            _output.WriteLine($"Synchronous ECS save averaged {perSaveMs:F1} ms.");
            Assert.True(perSaveMs < 2000);
        }

        private static bool SaveWorldWithStones(string slot, int stones)
        {
            var world = WorldFactory.CreateDefault();
            world.Get<CultivatorComponent>(world.PlayerEntity)!.PrimevalStones = stones;
            return SaveDiskIO.SaveWorld(slot, world);
        }
    }
}
