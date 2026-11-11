using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Projects SectorGridComponent directly from WorldStore into the isometric mesh.
    /// Zero state duplication: terrain, POIs, obstacles, and visibility live strictly inside SectorGridComponent.
    /// </summary>
    public partial class IsometricTileManager : IsometricTileRenderer
    {
        public const float TILE_THICKNESS = 0.25f;

        private TaiwuGridRenderer? _gridRenderer;
        private IWorldStateProvider? _worldProvider;

        private SectorGridComponent? CurrentSector => _worldProvider?.Current.GetCurrentSector();

        public override void _Ready()
        {
            WorldSignals.OnTopicsChanged += HandleTileTopicsChanged;
            SyncDimensionsFromState();
            base._Ready();
            EnsureGridRenderer();
        }

        public override void _ExitTree()
        {
            WorldSignals.OnTopicsChanged -= HandleTileTopicsChanged;
            base._ExitTree();
        }

        public void Initialize(IWorldStateProvider provider)
        {
            _worldProvider = provider;
            SyncFromWorld();
        }

        public override void RebuildTileMesh()
        {
            base.RebuildTileMesh();
            _gridRenderer?.GenerateGridLines();
        }

        private void EnsureGridRenderer()
        {
            if (_gridRenderer == null)
            {
                _gridRenderer = new TaiwuGridRenderer { Name = "TaiwuGridRenderer" };
                AddChild(_gridRenderer);
            }
            _gridRenderer.GenerateGridLines();
        }

        private void HandleTileTopicsChanged(DirtyTopic topics)
        {
            if ((topics & DirtyTopic.Map) != 0)
            {
                SyncFromWorld();
            }
        }

        public void SyncFromWorld()
        {
            SectorGridComponent? sector = CurrentSector;
            if (sector == null) return;

            GridWidth = sector.GridWidth > 0 ? sector.GridWidth : GridWidth;
            GridHeight = sector.GridHeight > 0 ? sector.GridHeight : GridHeight;

            if (GenerateMeshVisuals)
            {
                RebuildTileMesh();
            }
            UpdateFogMeshColors();
        }

        private void SyncDimensionsFromState()
        {
            SectorGridComponent? sector = CurrentSector;
            if (sector == null) return;

            GridWidth = sector.GridWidth > 0 ? sector.GridWidth : GridWidth;
            GridHeight = sector.GridHeight > 0 ? sector.GridHeight : GridHeight;
        }

        protected override (BiomeType biome, TilePOIType poi, bool visible, bool explored, bool active) GetTileDisplayInfo(int x, int y)
        {
            SectorGridComponent? sector = CurrentSector;
            if (sector == null)
            {
                return (BiomeType.BambooMountain, TilePOIType.None, false, false, false);
            }

            TileNode tile = sector.GetTile(x, y);
            return (tile.Biome, tile.POI, tile.IsVisible, tile.IsExplored, tile.IsActive);
        }

        public bool IsTileWalkable(Vector2I pos)
        {
            SectorGridComponent? sector = CurrentSector;
            return sector != null && sector.IsWalkable(pos.ToGridCoord());
        }

        public TileNode GetTile(int x, int y)
        {
            SectorGridComponent? sector = CurrentSector;
            return sector != null ? sector.GetTile(x, y) : default;
        }
    }
}
