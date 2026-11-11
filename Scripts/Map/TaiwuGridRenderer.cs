using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Systems;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Phase 2: Renders muted ink-wash isometric tile borders (Godot 2D).
    /// Outlines all diamond tiles with authentic parchment borders (#2B261F, ~15-20% alpha).
    /// Gives every diamond tile crisp tactile definition matching Unity.
    /// Strictly obeys Constitutional Law 1 (Observer Mandate) and Halal geometric compliance.
    /// </summary>
    public partial class TaiwuGridRenderer : Node2D
    {
        [Export] public Color GridBorderColor { get; set; } = new Color(0.168f, 0.149f, 0.121f, 0.18f); // #2B261F at 18% alpha
        [Export] public bool ShowGrid { get; set; } = true;

        private IsometricTileManager? _tileManager;
        private MeshInstance2D? _gridMeshInstance;
        private ArrayMesh? _gridMesh;

        public override void _Ready()
        {
            ZIndex = 1;
            _tileManager = GetParent() as IsometricTileManager;
            if (_tileManager != null && ShowGrid)
            {
                GenerateGridLines();
            }
        }

        public override void _ExitTree()
        {
            if (_gridMesh != null)
            {
                _gridMesh.Dispose();
                _gridMesh = null;
            }
        }

        public void Initialize(IsometricTileManager manager)
        {
            _tileManager = manager;
            if (ShowGrid)
            {
                GenerateGridLines();
            }
        }

        public void GenerateGridLines()
        {
            if (!ShowGrid) return;
            if (_tileManager == null)
            {
                _tileManager = GetParent() as IsometricTileManager;
            }

            int width = _tileManager != null ? _tileManager.GridWidth : 30;
            int height = _tileManager != null ? _tileManager.GridHeight : 30;

            float hw = IsometricTileRenderer.TILE_HALF_WIDTH;
            float hh = IsometricTileRenderer.TILE_HALF_HEIGHT;

            List<Vector2> verts = new(width * height * 4);
            List<Color> cols = new(width * height * 4);
            List<int> indices = new(width * height * 8);

            int vIdx = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Only draw grid on active (traversable) tiles
                    if (_tileManager != null && !_tileManager.IsTileWalkable(new Vector2I(x, y)))
                    {
                        continue;
                    }

                    Vector2 center = _tileManager != null 
                        ? _tileManager.GridToWorld(new Vector2I(x, y)) 
                        : ExplorationSystem.GridToWorld(new GridCoord(x, y)).ToGodot();

                    int startV = vIdx;

                    // Diamond vertices: North, East, South, West
                    // Godot 2D: -Y is North, +Y is South
                    verts.Add(center + new Vector2(0f, -hh)); // 0: North
                    verts.Add(center + new Vector2(hw, 0f));  // 1: East
                    verts.Add(center + new Vector2(0f, hh));  // 2: South
                    verts.Add(center + new Vector2(-hw, 0f)); // 3: West

                    cols.Add(GridBorderColor);
                    cols.Add(GridBorderColor);
                    cols.Add(GridBorderColor);
                    cols.Add(GridBorderColor);

                    vIdx += 4;

                    // Line 0 -> 1 (North to East)
                    indices.Add(startV);
                    indices.Add(startV + 1);

                    // Line 1 -> 2 (East to South)
                    indices.Add(startV + 1);
                    indices.Add(startV + 2);

                    // Line 2 -> 3 (South to West)
                    indices.Add(startV + 2);
                    indices.Add(startV + 3);

                    // Line 3 -> 0 (West to North)
                    indices.Add(startV + 3);
                    indices.Add(startV);
                }
            }

            if (verts.Count == 0) return;

            if (_gridMeshInstance == null)
            {
                _gridMeshInstance = new MeshInstance2D { Name = "GridLinesMesh" };
                AddChild(_gridMeshInstance);
            }

            _gridMesh ??= new ArrayMesh();
            _gridMesh.ClearSurfaces();

            var surfaceArrays = new Godot.Collections.Array();
            surfaceArrays.Resize((int)Mesh.ArrayType.Max);
            surfaceArrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
            surfaceArrays[(int)Mesh.ArrayType.Color] = cols.ToArray();
            surfaceArrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

            _gridMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, surfaceArrays);
            _gridMeshInstance.Mesh = _gridMesh;
        }

        public void SetGridVisibility(bool visible)
        {
            ShowGrid = visible;
            Visible = visible;
        }
    }
}
