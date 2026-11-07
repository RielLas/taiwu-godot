using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Psychology;
using TaiwuGu.Serialization;

namespace TaiwuGu.Tests
{
    public class Phase6ArchitecturalTests
    {
        private const string TEST_SLOT_1 = "Phase6_TestSlot_Fidelity";
        private const string TEST_SLOT_2 = "Phase6_TestSlot_Atomic";
        private const string TEST_SLOT_3 = "Phase6_TestSlot_Coordination";

        public Phase6ArchitecturalTests()
        {
            SavePaths.BaseSaveDirectory = Path.Combine(Path.GetTempPath(), "TaiwuTests_Saves");
            SaveLoadEngine.Initialize();
        }

        [Fact]
        public void Test01_StateMappingFidelity()
        {
            var original = GameStateContext.CreateDefault();
            original.Cultivator.Name = "Fang Yuan (Demon)";
            original.Cultivator.Rank = 2;
            original.Cultivator.Substage = 3;
            original.Cultivator.PrimevalStones = 740;
            original.Cultivator.CurrentEssenceSea = 85.5f;

            original.Calendar.CurrentYear = 3;
            original.Calendar.CurrentSolarTermIndex = 6;
            original.Calendar.ActionPointsRemaining = 17;

            original.WorldMap.PlayerGridPos = new Vector2Int(18, 22);
            original.WorldMap.SetExplored(18, 22, true);

            original.GuInventory.FoodStockpile.Add(new InventoryItem("food_test_meat", "Wolf Meat", GuDietType.FreshBoarFlesh, 25, 4));
            original.Forensics.ActiveCrimeScenes.Add(new CrimeSceneData(
                new Vector2Int(18, 22),
                "Tie Clan Investigator",
                CrimeSignature.CleanIncisions,
                2
            )
            {
                IsTamperedClean = true
            });

            SavePayloadDTO payload = SaveLoadEngine.ExtractPayload("Slot_Test01", original, 1234.5);
            GameStateContext hydrated = SaveLoadEngine.CreateContextFromPayload(payload);

            Assert.Equal("Fang Yuan (Demon)", hydrated.Cultivator.Name);
            Assert.Equal(2, hydrated.Cultivator.Rank);
            Assert.Equal(3, hydrated.Cultivator.Substage);
            Assert.Equal(740, hydrated.Cultivator.PrimevalStones);
            Assert.True(Mathf.Abs(hydrated.Cultivator.CurrentEssenceSea - 85.5f) <= 0.01f);

            Assert.Equal(3, hydrated.Calendar.CurrentYear);
            Assert.Equal(6, hydrated.Calendar.CurrentSolarTermIndex);
            Assert.Equal(17, hydrated.Calendar.ActionPointsRemaining);

            Assert.Equal(new Vector2Int(18, 22), hydrated.WorldMap.PlayerGridPos);

            Assert.NotEmpty(hydrated.GuInventory.FoodStockpile);
            Assert.Equal(25, hydrated.GuInventory.FoodStockpile.Find(f => f.ItemID == "food_test_meat")?.Quantity);

            Assert.NotEmpty(hydrated.Forensics.ActiveCrimeScenes);
            Assert.Equal("Tie Clan Investigator", hydrated.Forensics.ActiveCrimeScenes[0].VictimName);
        }

        [Fact]
        public void Test02_AtomicSerializationProtocol()
        {
            try
            {
                SaveLoadEngine.DeleteSlot(TEST_SLOT_2);

                var ctx1 = GameStateContext.CreateDefault();
                ctx1.Cultivator.PrimevalStones = 300;

                bool saved = SaveLoadEngine.SaveContext(TEST_SLOT_2, ctx1);
                Assert.True(saved);

                string slotDir = SaveLoadEngine.GetSlotPath(TEST_SLOT_2);
                string savePath = Path.Combine(slotDir, SavePaths.SAVE_FILE_NAME);
                string tempPath = savePath + ".tmp";
                string bakPath = savePath + ".bak";

                Assert.True(File.Exists(savePath));
                Assert.False(File.Exists(tempPath));

                // Second Save (Triggers Backup Creation)
                var ctx2 = GameStateContext.CreateDefault();
                ctx2.Cultivator.PrimevalStones = 600;
                SaveLoadEngine.SaveContext(TEST_SLOT_2, ctx2);

                Assert.True(File.Exists(bakPath));

                string curJson = File.ReadAllText(savePath);
                string bakJson = File.ReadAllText(bakPath);

                Assert.Contains("\"PrimevalStones\": 600", curJson);
                Assert.Contains("\"PrimevalStones\": 300", bakJson);
            }
            finally
            {
                SaveLoadEngine.DeleteSlot(TEST_SLOT_2);
            }
        }

        [Fact]
        public void Test03_CorruptedPayloadFallback()
        {
            try
            {
                SaveLoadEngine.DeleteSlot(TEST_SLOT_2);

                var ctx1 = GameStateContext.CreateDefault();
                ctx1.Cultivator.PrimevalStones = 100;
                SaveLoadEngine.SaveContext(TEST_SLOT_2, ctx1);

                var ctx2 = GameStateContext.CreateDefault();
                ctx2.Cultivator.PrimevalStones = 500;
                SaveLoadEngine.SaveContext(TEST_SLOT_2, ctx2);

                string slotDir = SaveLoadEngine.GetSlotPath(TEST_SLOT_2);
                string savePath = Path.Combine(slotDir, SavePaths.SAVE_FILE_NAME);

                File.WriteAllText(savePath, "CORRUPTED_GARBAGE_JSON{{{---NOT_VALID");

                GameStateContext recovered = SaveLoadEngine.LoadContext(TEST_SLOT_2);
                Assert.NotNull(recovered);
                Assert.Equal(100, recovered.Cultivator.PrimevalStones);
            }
            finally
            {
                SaveLoadEngine.DeleteSlot(TEST_SLOT_2);
            }
        }

        [Fact]
        public void Test04_PowerLossInterruptionSimulation()
        {
            try
            {
                SaveLoadEngine.DeleteSlot(TEST_SLOT_2);

                var ctx = GameStateContext.CreateDefault();
                ctx.Cultivator.PrimevalStones = 888;
                SaveLoadEngine.SaveContext(TEST_SLOT_2, ctx);

                string slotDir = SaveLoadEngine.GetSlotPath(TEST_SLOT_2);
                string tempPayloadPath = Path.Combine(slotDir, SavePaths.SAVE_FILE_NAME + ".tmp");

                File.WriteAllText(tempPayloadPath, "HALF_WRITTEN_CHUNK...");

                GameStateContext loaded = SaveLoadEngine.LoadContext(TEST_SLOT_2);
                Assert.NotNull(loaded);
                Assert.Equal(888, loaded.Cultivator.PrimevalStones);

                ctx.Cultivator.PrimevalStones = 999;
                SaveLoadEngine.SaveContext(TEST_SLOT_2, ctx);

                Assert.False(File.Exists(tempPayloadPath));
            }
            finally
            {
                SaveLoadEngine.DeleteSlot(TEST_SLOT_2);
            }
        }

        [Fact]
        public void Test05_SaveCoordinationServiceCQRS()
        {
            SaveCoordinationService service = null;
            try
            {
                SaveLoadEngine.DeleteSlot(TEST_SLOT_3);

                var ctx = GameStateContext.CreateDefault();
                ctx.Cultivator.PrimevalStones = 450;
                ctx.Cultivator.Rank = 1;
                ctx.Cultivator.Substage = 4;

                service = new SaveCoordinationService();
                service.Initialize(ctx);

                bool saveEventFired = false;
                using var saveCompleted = new System.Threading.ManualResetEventSlim(false);
                Action<string, bool> saveHandler = (slot, success) =>
                {
                    if (slot == TEST_SLOT_3 && success)
                    {
                        saveEventFired = true;
                        saveCompleted.Set();
                    }
                };
                GameEventManager.OnGameSaved += saveHandler;

                GameEventManager.RaiseRequestSaveGame(TEST_SLOT_3);
                Assert.True(saveCompleted.Wait(TimeSpan.FromSeconds(15)), "Asynchronous save did not complete.");
                GameEventManager.OnGameSaved -= saveHandler;

                Assert.True(saveEventFired);

                ctx.Cultivator.PrimevalStones = 99999;
                ctx.Cultivator.Rank = 5;

                int broadcastStones = -1;
                Action<int> stonesHandler = stones => broadcastStones = stones;
                GameEventManager.OnPrimevalStonesChanged += stonesHandler;

                bool loadEventFired = false;
                Action<string, bool> loadHandler = (slot, success) =>
                {
                    if (slot == TEST_SLOT_3 && success) loadEventFired = true;
                };
                GameEventManager.OnGameLoaded += loadHandler;

                GameEventManager.RaiseRequestLoadGame(TEST_SLOT_3);

                GameEventManager.OnPrimevalStonesChanged -= stonesHandler;
                GameEventManager.OnGameLoaded -= loadHandler;

                Assert.True(loadEventFired);
                // The load replaced the live context wholesale; the pre-load object is now detached.
                Assert.Equal(450, service.Context.Cultivator.PrimevalStones);
                Assert.Equal(1, service.Context.Cultivator.Rank);
                Assert.Equal(450, broadcastStones);
            }
            finally
            {
                service?.Dispose();
                SaveLoadEngine.DeleteSlot(TEST_SLOT_3);
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public async Task Test06_MetadataBrowsingAndDeletion()
        {
            string slotAlpha = "Slot_Phase6_Alpha";
            string slotBeta = "Slot_Phase6_Beta";

            try
            {
                SaveLoadEngine.DeleteSlot(slotAlpha);
                SaveLoadEngine.DeleteSlot(slotBeta);

                var ctxA = GameStateContext.CreateDefault();
                ctxA.Cultivator.Name = "Alpha Master";
                ctxA.Cultivator.Rank = 2;
                ctxA.Cultivator.PrimevalStones = 210;
                SaveLoadEngine.SaveContext(slotAlpha, ctxA);

                var ctxB = GameStateContext.CreateDefault();
                ctxB.Cultivator.Name = "Beta Elder";
                ctxB.Cultivator.Rank = 4;
                ctxB.Cultivator.PrimevalStones = 1500;
                SaveLoadEngine.SaveContext(slotBeta, ctxB);

                var metaA = SaveLoadEngine.LoadMetadata(slotAlpha);
                var metaB = await SaveLoadEngine.LoadMetadataAsync(slotBeta);

                Assert.NotNull(metaA);
                Assert.Equal("Alpha Master", metaA.PlayerName);
                Assert.Equal(2, metaA.PreviewRank);
                Assert.Equal(210, metaA.PreviewStones);

                Assert.NotNull(metaB);
                Assert.Equal("Beta Elder", metaB.PlayerName);
                Assert.Equal(4, metaB.PreviewRank);
                Assert.Equal(1500, metaB.PreviewStones);

                Assert.True(SaveLoadEngine.SlotExists(slotAlpha));
                Assert.True(SaveLoadEngine.SlotExists(slotBeta));

                bool deleted = SaveLoadEngine.DeleteSlot(slotAlpha);
                Assert.True(deleted);
                Assert.False(SaveLoadEngine.SlotExists(slotAlpha));
            }
            finally
            {
                SaveLoadEngine.DeleteSlot(slotAlpha);
                SaveLoadEngine.DeleteSlot(slotBeta);
            }
        }
    }
}
