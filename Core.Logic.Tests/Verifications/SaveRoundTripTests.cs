using System;
using System.IO;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Serialization;

namespace TaiwuGu.Tests
{
    /// <summary>
    /// Guards the save pipeline the headless verification harness relies on.
    /// If this fails, <c>--verify</c> will fail too, and the cause is serialization, not the scene.
    /// </summary>
    public class SaveRoundTripTests : IDisposable
    {
        private readonly string _originalRoot;
        private readonly string _testRoot;

        public SaveRoundTripTests()
        {
            _originalRoot = SavePaths.RootSaveDirectory;
            _testRoot = Path.Combine(Path.GetTempPath(), "taiwu_save_tests_" + Guid.NewGuid().ToString("N"));
            SavePaths.RootSaveDirectory = _testRoot;
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
                // Temp cleanup is best effort; a locked file must not fail the suite.
            }
        }

        [Fact]
        public void SaveThenLoad_PreservesCultivatorCalendarInventoryAndEveryTile()
        {
            var ctx = GameStateContext.CreateDefault();
            ctx.Cultivator.Rank = 2;
            ctx.Cultivator.Substage = 3;
            ctx.Cultivator.PrimevalStones = 777;
            ctx.Cultivator.CurrentEssenceSea = 61.5f;
            ctx.Calendar.CurrentYear = 4;
            ctx.Calendar.CurrentSolarTermIndex = 11;
            ctx.Calendar.ActionPointsRemaining = 17;
            ctx.WorldMap.SetExplored(3, 4, true);
            ctx.WorldMap.SetVisible(3, 4, true);

            Assert.NotNull(ctx.WorldMap.Tiles);

            // A struct field that only survives if TileNode serializes field-by-field.
            ctx.WorldMap.Tiles![0].CustomLandmarkId = "custom_landmark_probe";

            Assert.True(SaveLoadEngine.SaveContext("round_trip", ctx));

            var loaded = SaveLoadEngine.LoadContext("round_trip");
            Assert.NotNull(loaded);

            Assert.Equal("Gu Yue Fang Yuan", loaded!.Cultivator.Name);
            Assert.Equal(2, loaded.Cultivator.Rank);
            Assert.Equal(3, loaded.Cultivator.Substage);
            Assert.Equal(777, loaded.Cultivator.PrimevalStones);
            Assert.Equal(61.5f, loaded.Cultivator.CurrentEssenceSea, 3);
            Assert.Equal(4, loaded.Calendar.CurrentYear);
            Assert.Equal(11, loaded.Calendar.CurrentSolarTermIndex);
            Assert.Equal(17, loaded.Calendar.ActionPointsRemaining);
            Assert.Equal(ctx.GuInventory.EquippedGu.Count, loaded.GuInventory.EquippedGu.Count);

            Assert.NotNull(loaded.WorldMap.Tiles);
            Assert.Equal(ctx.WorldMap.Tiles.Length, loaded.WorldMap.Tiles!.Length);
            Assert.Equal("custom_landmark_probe", loaded.WorldMap.Tiles[0].CustomLandmarkId);

            for (int i = 0; i < ctx.WorldMap.Tiles.Length; i++)
            {
                TileNode before = ctx.WorldMap.Tiles[i];
                TileNode after = loaded.WorldMap.Tiles[i];

                Assert.Equal(before.POI, after.POI);
                Assert.Equal(before.Biome, after.Biome);
                Assert.Equal(before.DangerRank, after.DangerRank);
                Assert.Equal(before.IsExplored, after.IsExplored);
                Assert.Equal(before.IsVisible, after.IsVisible);
                Assert.Equal(before.IsActive, after.IsActive);
                Assert.Equal(before.GridPos.X, after.GridPos.X);
                Assert.Equal(before.GridPos.Y, after.GridPos.Y);
                Assert.Equal(before.CustomLandmarkId, after.CustomLandmarkId);
            }
        }

        [Fact]
        public void SaveThenLoad_TwiceIsStableAndWritesRecoverableSlot()
        {
            var ctx = GameStateContext.CreateDefault();

            Assert.True(SaveLoadEngine.SaveContext("stable_slot", ctx));
            Assert.True(SaveLoadEngine.SaveContext("stable_slot", ctx));

            Assert.True(SaveLoadEngine.SlotExists("stable_slot"));

            var first = SaveLoadEngine.LoadContext("stable_slot");
            var second = SaveLoadEngine.LoadContext("stable_slot");

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first!.Cultivator.Name, second!.Cultivator.Name);
            Assert.Equal(first.WorldMap.Tiles!.Length, second.WorldMap.Tiles!.Length);
            Assert.Equal(first.WorldMap.PlayerGridPos, second.WorldMap.PlayerGridPos);
        }
    }
}
