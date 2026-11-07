using System;
using System.Collections.Generic;
using Xunit;
using TaiwuGu.Map;

namespace TaiwuGu.Tests
{
    public class SectorGeneratorTests
    {
        private const int Width = 30;
        private const int Height = 30;

        [Fact]
        public void Generate_SameSeed_ProducesIdenticalTiles()
        {
            TileNode[] first = SectorGenerator.Generate(424242, Width, Height, BiomeType.BambooMountain);
            TileNode[] second = SectorGenerator.Generate(424242, Width, Height, BiomeType.BambooMountain);

            Assert.Equal(first.Length, second.Length);

            for (int i = 0; i < first.Length; i++)
            {
                Assert.Equal(first[i].Biome, second[i].Biome);
                Assert.Equal(first[i].POI, second[i].POI);
                Assert.Equal(first[i].DangerRank, second[i].DangerRank);
                Assert.Equal(first[i].IsActive, second[i].IsActive);
                Assert.Equal(first[i].IsExplored, second[i].IsExplored);
                Assert.Equal(first[i].IsVisible, second[i].IsVisible);
                Assert.Equal(first[i].CustomLandmarkId, second[i].CustomLandmarkId);
            }
        }

        [Fact]
        public void Generate_DifferentSeed_ProducesDifferentTerrain()
        {
            TileNode[] a = SectorGenerator.Generate(1111, Width, Height, BiomeType.BambooMountain);
            TileNode[] b = SectorGenerator.Generate(9999, Width, Height, BiomeType.BambooMountain);

            int differences = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].IsActive != b[i].IsActive || a[i].POI != b[i].POI) differences++;
            }

            Assert.True(differences > 0, "Different seeds must not produce an identical sector.");
        }

        [Fact]
        public void Generate_AllTraversableTiles_AreConnectedToTheCentre()
        {
            TileNode[] tiles = SectorGenerator.Generate(17881588, Width, Height, BiomeType.BambooMountain);

            int activeCount = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i].IsActive) activeCount++;
            }

            Assert.True(activeCount > 0);
            Assert.Equal(activeCount, CountReachableFromCentre(tiles));
        }

        [Fact]
        public void Generate_PlacesTheDesignedStartingLandmarksOnWalkableTiles()
        {
            TileNode[] tiles = SectorGenerator.Generate(17881588, Width, Height, BiomeType.BambooMountain);

            Assert.Equal(TilePOIType.ClanPost, tiles[15 * Width + 15].POI);
            Assert.Equal(TilePOIType.WayStation, tiles[15 * Width + 19].POI);
            Assert.Equal(TilePOIType.SpiritVein, tiles[17 * Width + 14].POI);

            Assert.True(tiles[15 * Width + 15].IsActive);
            Assert.True(tiles[15 * Width + 15].IsExplored);
            Assert.True(tiles[15 * Width + 15].IsVisible);
        }

        [Fact]
        public void Generate_NeverPlacesAPoiOnVoid()
        {
            foreach (int seed in new[] { 1, 17881588, 424242, -7 })
            {
                TileNode[] tiles = SectorGenerator.Generate(seed, Width, Height, BiomeType.BambooMountain);

                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i].POI != TilePOIType.None)
                    {
                        Assert.True(tiles[i].IsActive, $"seed {seed}: POI {tiles[i].POI} placed on an inactive tile at index {i}.");
                    }
                }
            }
        }

        [Fact]
        public void Generate_ScalesToDifferentSectorSizes()
        {
            foreach (var (w, h) in new[] { (28, 28), (34, 24), (32, 32) })
            {
                TileNode[] tiles = SectorGenerator.Generate(17881588, w, h, BiomeType.BambooMountain);

                Assert.Equal(w * h, tiles.Length);

                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i].POI != TilePOIType.None)
                    {
                        Assert.True(tiles[i].IsActive);
                    }
                }
            }
        }

        [Fact]
        public void Generate_UsesTheRequestedBiome()
        {
            TileNode[] tiles = SectorGenerator.Generate(17881588, Width, Height, BiomeType.DesertOasis);

            for (int i = 0; i < tiles.Length; i++)
            {
                Assert.Equal(BiomeType.DesertOasis, tiles[i].Biome);
            }
        }

        private static int CountReachableFromCentre(TileNode[] tiles)
        {
            int start = 15 * Width + 15;
            var seen = new bool[tiles.Length];
            var queue = new Queue<int>();
            seen[start] = true;
            queue.Enqueue(start);
            int visited = 1;

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % Width;
                int y = index / Width;

                Visit(x + 1, y);
                Visit(x - 1, y);
                Visit(x, y + 1);
                Visit(x, y - 1);
            }

            return visited;

            void Visit(int x, int y)
            {
                if (x < 0 || x >= Width || y < 0 || y >= Height) return;

                int index = y * Width + x;
                if (seen[index] || !tiles[index].IsActive) return;

                seen[index] = true;
                visited++;
                queue.Enqueue(index);
            }
        }
    }
}
