using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Serialization;

namespace TaiwuGu.Tests
{
    /// <summary>
    /// Save version 2.1 replaced each sector snapshot's cloned TileNode[] with seed + patches.
    /// A 2.0 file must be converted on load, or changes saved in a sector the player is not
    /// standing in would vanish the next time it is entered.
    /// </summary>
    public class LegacySaveMigrationTests : IDisposable
    {
        private const string Slot = "legacy_20_slot";
        private const int Width = 30;
        private const int Height = 30;

        private readonly string _originalRoot;
        private readonly string _testRoot;

        public LegacySaveMigrationTests()
        {
            _originalRoot = SavePaths.RootSaveDirectory;
            _testRoot = Path.Combine(Path.GetTempPath(), "taiwu_legacy_" + Guid.NewGuid().ToString("N"));
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
        public void TwoPointZeroPayload_ConvertsSectorTileArraysToPatches()
        {
            var context = GameStateContext.CreateDefault();
            context.WorldMap.SaveCurrentSectorSnapshot("Sector_Current");

            TileNode[] legacyTiles = (TileNode[])context.WorldMap.Tiles!.Clone();
            legacyTiles[7 * Width + 3].POI = TilePOIType.AncientGuCave;
            legacyTiles[7 * Width + 3].IsExplored = true;
            legacyTiles[7 * Width + 3].IsVisible = true;
            legacyTiles[8 * Width + 4].IsActive = false;

            WriteLegacyPayload(context, legacyTiles);

            GameStateContext? loaded = SaveLoadEngine.LoadContext(Slot);
            Assert.NotNull(loaded);

            // The snapshot is regenerated from its seed, so only the migrated patches can carry the
            // authored changes back in.
            Assert.True(loaded!.WorldMap.TryLoadSectorSnapshot("Sector_Legacy"));
            Assert.Equal(TilePOIType.AncientGuCave, loaded.WorldMap.GetTile(3, 7).POI);
            Assert.False(loaded.WorldMap.GetTile(4, 8).IsActive);
            Assert.True(loaded.WorldMap.GetTile(3, 7).IsExplored);
            Assert.Equal(new Vector2Int(2, 3), loaded.WorldMap.PlayerGridPos);
        }

        [Fact]
        public void MigratedPayload_IsRewrittenWithPatchesAndNoLegacyTiles()
        {
            var context = GameStateContext.CreateDefault();
            TileNode[] legacyTiles = (TileNode[])context.WorldMap.Tiles!.Clone();
            legacyTiles[7 * Width + 3].POI = TilePOIType.AncientGuCave;

            WriteLegacyPayload(context, legacyTiles);

            GameStateContext? loaded = SaveLoadEngine.LoadContext(Slot);
            Assert.NotNull(loaded);
            Assert.True(loaded!.WorldMap.TryLoadSectorSnapshot("Sector_Legacy"));

            Assert.True(SaveLoadEngine.SaveContext(Slot, loaded));

            JObject resaved = JObject.Parse(File.ReadAllText(SavePaths.GetSavePath(Slot)));
            Assert.Equal(SaveDiskIO.CURRENT_SAVE_VERSION, (string?)resaved["SaveVersion"]);

            JObject? snapshot = (JObject?)resaved["Context"]?["WorldMap"]?["SectorSnapshots"]?["Sector_Legacy"];
            Assert.NotNull(snapshot);
            Assert.Null(snapshot!["Tiles"]);
            Assert.NotNull(snapshot["Patches"]);

            JArray? patches = (JArray?)snapshot["Patches"];
            Assert.Contains(patches!, p => (int?)p["Index"] == 7 * Width + 3);
        }

        private static void WriteLegacyPayload(GameStateContext context, TileNode[] legacyTiles)
        {
            SavePayloadDTO payload = SaveLoadEngine.ExtractPayload(Slot, context)!;
            JObject root = JObject.Parse(JsonConvert.SerializeObject(payload));

            root["SaveVersion"] = "2.0.0";

            var legacySnapshot = new JObject
            {
                ["SectorKey"] = "Sector_Legacy",
                ["Seed"] = context.WorldMap.Seed,
                ["Region"] = (int)context.WorldMap.CurrentRegion,
                ["GridWidth"] = Width,
                ["GridHeight"] = Height,
                // Serialize the real type so the key names always match what the loader expects.
                ["PlayerGridPos"] = JToken.FromObject(new Vector2Int(2, 3)),
                ["ExploredTileIndices"] = new JArray(),
                ["VisibleTileIndices"] = new JArray(),
                ["Tiles"] = JToken.FromObject(legacyTiles)
            };

            root["Context"]!["WorldMap"]!["SectorSnapshots"] = new JObject { ["Sector_Legacy"] = legacySnapshot };

            SavePaths.EnsureDirectoryExists(SavePaths.GetSlotPath(Slot));
            File.WriteAllText(SavePaths.GetSavePath(Slot), root.ToString());
        }
    }
}
