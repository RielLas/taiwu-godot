using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TaiwuGu.Core;
using TaiwuGu.Map;

namespace TaiwuGu.Entities
{
    [Serializable]
    public class TilePatch
    {
        public int Index { get; set; }
        public BiomeType Biome { get; set; }
        public TilePOIType POI { get; set; }
        public string? CustomLandmarkId { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Compact ECS sector map component. Persists Seed + sparse Patches + Exploration bitlists
    /// while deriving the 30x30 (or sector-sized) TileNode[] grid deterministically on load.
    /// </summary>
    [Serializable]
    public class SectorGridComponent
    {
        public string SectorKey { get; set; } = "Sector_SouthernBorder_GuYueVillage";
        public int Seed { get; set; } = 17881588;
        public WorldRegionID Region { get; set; } = WorldRegionID.SouthernBorder;
        public SouthernMountainID MountainId { get; set; } = SouthernMountainID.GuYueVillage;
        public string DisplayName { get; set; } = "Gu Yue Mountain (古月山寨)";
        public int GridWidth { get; set; } = 30;
        public int GridHeight { get; set; } = 30;

        public List<TilePatch> Patches { get; set; } = new();
        public List<int> ExploredTileIndices { get; set; } = new();
        public List<int> VisibleTileIndices { get; set; } = new();

        [JsonIgnore]
        public TileNode[]? Tiles { get; set; }

        public TileNode[] EnsureHydrated()
        {
            if (Tiles == null || Tiles.Length != GridWidth * GridHeight)
            {
                HydrateTiles();
            }
            return Tiles!;
        }

        public void HydrateTiles()
        {
            Tiles = SectorGenerator.Generate(Seed, GridWidth, GridHeight, RegionalWallLaw.GetRegionBiome(Region));

            if (Patches != null)
            {
                foreach (var patch in Patches)
                {
                    if (patch.Index < 0 || patch.Index >= Tiles.Length) continue;
                    Tiles[patch.Index].Biome = patch.Biome;
                    Tiles[patch.Index].POI = patch.POI;
                    Tiles[patch.Index].CustomLandmarkId = patch.CustomLandmarkId;
                    Tiles[patch.Index].IsActive = patch.IsActive;
                }
            }

            for (int i = 0; i < Tiles.Length; i++)
            {
                Tiles[i].IsExplored = false;
                Tiles[i].IsVisible = false;
            }

            if (ExploredTileIndices != null)
            {
                foreach (int index in ExploredTileIndices)
                {
                    if (index >= 0 && index < Tiles.Length) Tiles[index].IsExplored = true;
                }
            }

            if (VisibleTileIndices != null)
            {
                foreach (int index in VisibleTileIndices)
                {
                    if (index >= 0 && index < Tiles.Length) Tiles[index].IsVisible = true;
                }
            }
        }

        public void CommitTilesToSnapshot()
        {
            if (Tiles == null) return;

            TileNode[] baseline = SectorGenerator.Generate(Seed, GridWidth, GridHeight, RegionalWallLaw.GetRegionBiome(Region));
            var explored = new List<int>();
            var visible = new List<int>();
            var patches = new List<TilePatch>();

            int count = Math.Min(Tiles.Length, baseline.Length);
            for (int i = 0; i < count; i++)
            {
                TileNode current = Tiles[i];
                if (current.IsExplored) explored.Add(i);
                if (current.IsVisible) visible.Add(i);

                TileNode generated = baseline[i];
                bool changed = current.Biome != generated.Biome
                    || current.POI != generated.POI
                    || current.IsActive != generated.IsActive
                    || !string.Equals(current.CustomLandmarkId, generated.CustomLandmarkId, StringComparison.Ordinal);

                if (changed)
                {
                    patches.Add(new TilePatch
                    {
                        Index = i,
                        Biome = current.Biome,
                        POI = current.POI,
                        CustomLandmarkId = current.CustomLandmarkId,
                        IsActive = current.IsActive
                    });
                }
            }

            ExploredTileIndices = explored;
            VisibleTileIndices = visible;
            Patches = patches;
        }

        public bool IsWithinBounds(GridCoord pos)
            => pos.X >= 0 && pos.X < GridWidth && pos.Y >= 0 && pos.Y < GridHeight;

        public TileNode GetTile(int x, int y)
        {
            TileNode[] tiles = EnsureHydrated();
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return default;
            return tiles[y * GridWidth + x];
        }

        public void SetTile(int x, int y, TileNode node)
        {
            TileNode[] tiles = EnsureHydrated();
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;
            tiles[y * GridWidth + x] = node;
        }

        public void SetExplored(int x, int y, bool explored)
        {
            TileNode[] tiles = EnsureHydrated();
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;
            tiles[y * GridWidth + x].IsExplored = explored;
        }

        public void SetVisible(int x, int y, bool visible)
        {
            TileNode[] tiles = EnsureHydrated();
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;
            tiles[y * GridWidth + x].IsVisible = visible;
        }

        public bool IsWalkable(GridCoord pos)
        {
            if (!IsWithinBounds(pos)) return false;
            TileNode tile = GetTile(pos.X, pos.Y);
            if (!tile.IsActive) return false;
            return LandmarkRegistry.IsTraversable(tile.POI, tile.CustomLandmarkId);
        }
    }
}
