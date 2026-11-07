using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Map;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Projects <see cref="WorldMapState"/> into the isometric mesh.
    ///
    /// This class never authors tiles. Terrain, POIs, obstacles and visibility all belong to the
    /// domain (SectorGenerator / ExplorationService), so what the player sees and what the simulation
    /// and save file contain cannot drift apart.
    /// </summary>
    public partial class IsometricTileManager : IsometricTileRenderer
    {
        public const float TILE_THICKNESS = 0.25f;

        private TaiwuGridRenderer? gridRenderer;

        private static WorldMapState? MapState => MapContextProvider.WorldMapResolver?.Invoke();

        public override void _Ready()
        {
            GameEventManager.OnGameLoaded += HandleGameLoaded;

            SyncDimensionsFromState();
            base._Ready();
            EnsureGridRenderer();
        }

        public override void _ExitTree()
        {
            GameEventManager.OnGameLoaded -= HandleGameLoaded;
            base._ExitTree();
        }

        public override void RebuildTileMesh()
        {
            base.RebuildTileMesh();
            gridRenderer?.GenerateGridLines();
        }

        private void EnsureGridRenderer()
        {
            if (gridRenderer == null)
            {
                gridRenderer = new TaiwuGridRenderer { Name = "TaiwuGridRenderer" };
                AddChild(gridRenderer);
            }
            gridRenderer.GenerateGridLines();
        }

        private void HandleGameLoaded(string slotID, bool success)
        {
            if (!success) return;

            WorldMapState? map = MapState;
            if (map != null)
            {
                SyncFromStateContext(map);
            }
        }

        /// <summary>Re-reads grid dimensions and re-projects the mesh after state changes (load, transit).</summary>
        public void SyncFromStateContext(WorldMapState mapState)
        {
            if (mapState == null) return;

            GridWidth = mapState.GridWidth > 0 ? mapState.GridWidth : GridWidth;
            GridHeight = mapState.GridHeight > 0 ? mapState.GridHeight : GridHeight;

            if (GenerateMeshVisuals)
            {
                RebuildTileMesh();
            }
            UpdateFogMeshColors();
        }

        private void SyncDimensionsFromState()
        {
            WorldMapState? map = MapState;
            if (map == null) return;

            GridWidth = map.GridWidth > 0 ? map.GridWidth : GridWidth;
            GridHeight = map.GridHeight > 0 ? map.GridHeight : GridHeight;
        }

        protected override (BiomeType biome, TilePOIType poi, bool visible, bool explored, bool active) GetTileDisplayInfo(int x, int y)
        {
            WorldMapState? map = MapState;
            if (map?.Tiles == null)
            {
                // No state means no world. Never invent tiles: that was the source of the old desync.
                return (BiomeType.BambooMountain, TilePOIType.None, false, false, false);
            }

            TileNode tile = map.GetTile(x, y);
            return (tile.Biome, tile.POI, tile.IsVisible, tile.IsExplored, tile.IsActive);
        }

        public bool IsTileWalkable(Vector2I pos)
        {
            WorldMapState? map = MapState;
            return map != null && map.IsWalkable(pos.ToGridCoord());
        }

        public TileNode GetTile(int x, int y)
        {
            WorldMapState? map = MapState;
            if (map == null) return default;
            return map.GetTile(x, y);
        }
    }
}
