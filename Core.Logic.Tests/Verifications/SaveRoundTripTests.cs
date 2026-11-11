using System;
using System.IO;
using System.Linq;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Serialization;

namespace TaiwuGu.Tests
{
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
            catch { }
        }

        [Fact]
        public void SaveThenLoad_PreservesWorldComponentsOwnedEntitiesAndEveryTile()
        {
            var world = WorldFactory.CreateDefault(runSeed: 424242);
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;
            var clock = world.GetSingleton<WorldClockComponent>()!;
            var sector = world.GetCurrentSector()!;

            ap.Rank = 2;
            ap.Substage = 3;
            cult.PrimevalStones = 777;
            ap.CurrentEssenceSea = 61.5f;
            clock.CurrentYear = 4;
            clock.CurrentSolarTermIndex = 11;
            clock.ActionPointsRemaining = 17;

            var tiles = sector.EnsureHydrated();
            tiles[4 * sector.GridWidth + 3].IsExplored = true;
            tiles[4 * sector.GridWidth + 3].IsVisible = true;
            tiles[0].CustomLandmarkId = "custom_landmark_probe";

            Assert.True(SaveDiskIO.SaveWorld("round_trip", world));

            var loaded = SaveDiskIO.LoadWorld("round_trip");
            Assert.NotNull(loaded);

            var cultB = loaded!.Get<CultivatorComponent>(loaded.PlayerEntity)!;
            var apB = loaded.Get<ApertureComponent>(loaded.PlayerEntity)!;
            var clockB = loaded.GetSingleton<WorldClockComponent>()!;
            var sectorB = loaded.GetCurrentSector()!;

            Assert.Equal("Gu Yue Fang Yuan", cultB.Name);
            Assert.Equal(2, apB.Rank);
            Assert.Equal(3, apB.Substage);
            Assert.Equal(777, cultB.PrimevalStones);
            Assert.Equal(61.5f, apB.CurrentEssenceSea, 3);
            Assert.Equal(4, clockB.CurrentYear);
            Assert.Equal(11, clockB.CurrentSolarTermIndex);
            Assert.Equal(17, clockB.ActionPointsRemaining);
            Assert.Equal(424242, loaded.RunSeed);
            Assert.Equal(world.GetOwned<GuWormComponent>(world.PlayerEntity).Count(),
                         loaded.GetOwned<GuWormComponent>(loaded.PlayerEntity).Count());

            var tilesB = sectorB.EnsureHydrated();
            Assert.Equal(tiles.Length, tilesB.Length);
            Assert.Equal("custom_landmark_probe", tilesB[0].CustomLandmarkId);

            for (int i = 0; i < tiles.Length; i++)
            {
                TileNode before = tiles[i];
                TileNode after = tilesB[i];
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
        public void SaveThenLoad_TwiceIsStable()
        {
            var world = WorldFactory.CreateDefault();
            Assert.True(SaveDiskIO.SaveWorld("stable_slot", world));
            Assert.True(SaveDiskIO.SaveWorld("stable_slot", world));
            Assert.True(SaveDiskIO.SlotExists("stable_slot"));

            var first = SaveDiskIO.LoadWorld("stable_slot");
            var second = SaveDiskIO.LoadWorld("stable_slot");

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first!.Get<CultivatorComponent>(first.PlayerEntity)!.Name,
                         second!.Get<CultivatorComponent>(second.PlayerEntity)!.Name);
            Assert.Equal(first.GetCurrentSector()!.EnsureHydrated().Length,
                         second.GetCurrentSector()!.EnsureHydrated().Length);
        }
    }
}
