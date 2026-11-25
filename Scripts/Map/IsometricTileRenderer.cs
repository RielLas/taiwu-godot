using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Systems;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Phase 2: Isometric Tile Renderer (Godot 2D).
    /// Pure presentation layer visual renderer decoupled from overworld data and business rules.
    /// Generates combined 2.5D dimetric diamond mesh (W=4.8, H=2.4) with extruded ink-stone cliff bevels (+Y downward)
    /// and reacts to Fog of War events to dynamically update vertex luminance.
    /// Strictly obeys Constitutional Law 1 (Observer Mandate) and Halal geometric compliance.
    /// </summary>
    public partial class IsometricTileRenderer : Node2D
    {
        public const int GRID_SIZE = 30;
        public const float TILE_HALF_WIDTH = 2.4f;
        public const float TILE_HALF_HEIGHT = 1.2f;
        public const float TILE_CLIFF_THICKNESS = 0.28f;

        // Normalized Taiwu V2 2:1 Isometric Tile Specs (512x384 canvas, 512x256 diamond, 128 headroom)
        public const float V2_CANVAS_WIDTH = 512f;
        public const float V2_CANVAS_HEIGHT = 384f;
        public const float V2_DIAMOND_WIDTH = 512f;
        public const float V2_DIAMOND_HEIGHT = 256f;
        public const float V2_HEADROOM_HEIGHT = 128f;
        public const float V2_WORLD_SCALE = (TILE_HALF_WIDTH * 2f) / V2_DIAMOND_WIDTH; // 4.8f / 512f = 0.009375f
        public static readonly Vector2 V2_SPRITE_OFFSET = new(0f, -64f);

        [Export] public int GridWidth { get; set; } = 30;
        [Export] public int GridHeight { get; set; } = 30;
        [Export] public bool GenerateMeshVisuals { get; set; } = true;
        [Export] public bool UseV2SpriteTiles { get; set; } = true;

        protected readonly List<Vector2I> activeGridCoords = new();
        private readonly List<int> activeTileVertexCounts = new();
        private readonly List<(bool left, bool right)> activeTileSkirtFlags = new();

        protected MeshInstance2D? tileMeshInstance;
        protected ShaderMaterial? sharedTileMaterial;
        protected ArrayMesh? combinedMesh;

        protected Node2D? _v2TileLayer;
        protected readonly Dictionary<Vector2I, Sprite2D> _v2TileSprites = new();
        protected readonly Stack<Sprite2D> _v2SpritePool = new();

        private Texture2D? _v2TexMeadow;
        private Texture2D? _v2TexBamboo;
        private Texture2D? _v2TexClan;
        private Texture2D? _v2TexCave;

        private readonly List<Vector2> _batchedVerts = new(900 * 8);
        private readonly List<Vector2> _batchedUvs = new(900 * 8);
        private readonly List<Color> _batchedCols = new(900 * 8);
        private readonly List<int> _batchedTris = new(900 * 12);

        private Vector2[] _cachedVertsArray = System.Array.Empty<Vector2>();
        private Vector2[] _cachedUvsArray = System.Array.Empty<Vector2>();
        private Color[] _cachedColsArray = System.Array.Empty<Color>();
        private int[] _cachedTrisArray = System.Array.Empty<int>();
        private readonly Godot.Collections.Array _cachedSurfaceArrays = new();

        protected Vector2I? selectedTargetPos = null;
        protected readonly HashSet<Vector2I> previewPathTiles = new();

        public override void _Ready()
        {
            WorldSignals.OnTopicsChanged += HandleTopicsChanged;
            WorldSignals.OnTargetTileSelected += HandleTargetTileSelected;

            if (GenerateMeshVisuals)
            {
                RebuildTileMesh();
            }
        }

        public override void _ExitTree()
        {
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
            WorldSignals.OnTargetTileSelected -= HandleTargetTileSelected;

            if (combinedMesh != null)
            {
                combinedMesh.Dispose();
                combinedMesh = null;
            }
            _cachedSurfaceArrays.Clear();

            ClearV2Tiles();
            if (_v2TileLayer != null)
            {
                if (GodotObject.IsInstanceValid(_v2TileLayer))
                {
                    _v2TileLayer.QueueFree();
                }
                _v2TileLayer = null;
            }
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if ((topics & DirtyTopic.Fog) != 0)
            {
                UpdateFogMeshColors();
            }
        }

        protected virtual void HandleTargetTileSelected(GridCoord? targetPos, List<GridCoord>? path)
        {
            selectedTargetPos = targetPos.HasValue ? targetPos.Value.ToGodot() : null;
            previewPathTiles.Clear();

            if (path != null && targetPos.HasValue)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    if (path[i] != targetPos.Value)
                    {
                        previewPathTiles.Add(path[i].ToGodot());
                    }
                }
            }

            UpdateFogMeshColors();
        }

        public virtual void RebuildTileMesh()
        {
            if (UseV2SpriteTiles)
            {
                if (tileMeshInstance != null)
                {
                    tileMeshInstance.Visible = false;
                }
                RebuildV2Tiles();
            }
            else
            {
                if (_v2TileLayer != null)
                {
                    _v2TileLayer.Visible = false;
                }
                if (GenerateMeshVisuals)
                {
                    if (tileMeshInstance != null)
                    {
                        tileMeshInstance.Visible = true;
                    }
                    BuildProceduralDiamondMesh();
                }
            }
        }

        public void BuildProceduralDiamondMesh()
        {
            EnsureTileMeshInstance();

            activeGridCoords.Clear();
            activeTileVertexCounts.Clear();
            activeTileSkirtFlags.Clear();
            _batchedVerts.Clear();
            _batchedUvs.Clear();
            _batchedCols.Clear();
            _batchedTris.Clear();

            int maxDiag = (GridWidth - 1) + (GridHeight - 1);
            for (int diag = 0; diag <= maxDiag; diag++)
            {
                int startX = Math.Max(0, diag - (GridHeight - 1));
                int endX = Math.Min(GridWidth - 1, diag);
                for (int x = startX; x <= endX; x++)
                {
                    int y = diag - x;
                    var tileInfo = GetTileDisplayInfo(x, y);

                    // Southern / foreground neighbors check for 2.5D stone cliff extrusion (+Y down)
                    // Bottom-Left (South-West) neighbor is at (x, y + 1)
                    // Bottom-Right (South-East) neighbor is at (x + 1, y)
                    // Only active mountain tiles drop cliff skirts down to void / edge
                    bool exposeBottomLeft = tileInfo.active && ((y + 1 >= GridHeight) || !GetTileDisplayInfo(x, y + 1).active);
                    bool exposeBottomRight = tileInfo.active && ((x + 1 >= GridWidth) || !GetTileDisplayInfo(x + 1, y).active);

                    activeGridCoords.Add(new Vector2I(x, y));
                    Vector2 pos = GridToWorld(new Vector2I(x, y));

                    AppendSeamlessDiamondData(
                        _batchedVerts,
                        _batchedUvs,
                        _batchedCols,
                        _batchedTris,
                        pos,
                        tileInfo.biome,
                        tileInfo.poi,
                        tileInfo.visible,
                        tileInfo.explored,
                        tileInfo.active,
                        x,
                        y,
                        exposeBottomLeft,
                        exposeBottomRight
                    );
                }
            }

            combinedMesh ??= new ArrayMesh();
            combinedMesh.ClearSurfaces();

            _cachedVertsArray = _batchedVerts.ToArray();
            _cachedUvsArray = _batchedUvs.ToArray();
            _cachedColsArray = _batchedCols.ToArray();
            _cachedTrisArray = _batchedTris.ToArray();

            _cachedSurfaceArrays.Resize((int)Mesh.ArrayType.Max);
            _cachedSurfaceArrays[(int)Mesh.ArrayType.Vertex] = _cachedVertsArray;
            _cachedSurfaceArrays[(int)Mesh.ArrayType.TexUV] = _cachedUvsArray;
            _cachedSurfaceArrays[(int)Mesh.ArrayType.Color] = _cachedColsArray;
            _cachedSurfaceArrays[(int)Mesh.ArrayType.Index] = _cachedTrisArray;

            combinedMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, _cachedSurfaceArrays);

            if (tileMeshInstance != null)
            {
                tileMeshInstance.Mesh = combinedMesh;
            }
        }

        private void EnsureTileMeshInstance()
        {
            if (tileMeshInstance == null)
            {
                tileMeshInstance = new MeshInstance2D { Name = "TileMeshInstance" };
                AddChild(tileMeshInstance);
            }

            if (sharedTileMaterial == null)
            {
                var shader = ResourceLoader.Load<Shader>("res://Resources/Shaders/taiwu_isometric_tile_ink.gdshader");
                sharedTileMaterial = new ShaderMaterial
                {
                    Shader = shader
                };

                var grassTex = ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/tile_base_grass.png");
                if (grassTex != null)
                {
                    tileMeshInstance.Texture = grassTex;
                }

                var fogTex = ResourceLoader.Load<Texture2D>("res://Resources/Fog/fog_ink_clouds.png");
                if (fogTex != null)
                {
                    sharedTileMaterial.SetShaderParameter("fog_texture", fogTex);
                }
                sharedTileMaterial.SetShaderParameter("fog_scroll_speed", new Vector2(0.006f, 0.003f));
                sharedTileMaterial.SetShaderParameter("fog_color", new Color(0.031f, 0.051f, 0.059f, 1.0f));
                sharedTileMaterial.SetShaderParameter("dao_mist_color", new Color(0.18f, 0.28f, 0.26f, 0.65f));

                tileMeshInstance.Material = sharedTileMaterial;
            }
        }

        private void AppendSeamlessDiamondData(
            List<Vector2> allVerts,
            List<Vector2> allUvs,
            List<Color> allCols,
            List<int> allTris,
            Vector2 worldPos,
            BiomeType biome,
            TilePOIType poi,
            bool visible,
            bool explored,
            bool active,
            int gx,
            int gy,
            bool extrudeLeftSkirt,
            bool extrudeRightSkirt)
        {
            float hw = TILE_HALF_WIDTH;
            float hh = TILE_HALF_HEIGHT;
            float cliff = TILE_CLIFF_THICKNESS;

            Color faceColor = EvaluateTileColor(biome, poi, visible, explored, active, gx, gy);
            Color leftCliffColor = new(faceColor.R * 0.48f, faceColor.G * 0.48f, faceColor.B * 0.52f, faceColor.A);
            Color rightCliffColor = new(faceColor.R * 0.34f, faceColor.G * 0.34f, faceColor.B * 0.38f, faceColor.A);

            int startV = allVerts.Count;

            // In Godot 2D: -Y is North/Up, +Y is South/Down
            allVerts.Add(worldPos + new Vector2(0f, -hh)); // North
            allVerts.Add(worldPos + new Vector2(hw, 0f));  // East
            allVerts.Add(worldPos + new Vector2(0f, hh));  // South
            allVerts.Add(worldPos + new Vector2(-hw, 0f)); // West

            allUvs.Add(new Vector2(0.5f, 0.022f));
            allUvs.Add(new Vector2(0.995f, 0.5f));
            allUvs.Add(new Vector2(0.5f, 0.978f));
            allUvs.Add(new Vector2(0.005f, 0.5f));

            allCols.Add(faceColor);
            allCols.Add(faceColor);
            allCols.Add(faceColor);
            allCols.Add(faceColor);

            allTris.Add(startV);     allTris.Add(startV + 1); allTris.Add(startV + 2);
            allTris.Add(startV);     allTris.Add(startV + 2); allTris.Add(startV + 3);

            int currentCount = 4;

            // Inverted Tile Bevels (+Y Downward):
            // Left skirt: West (-hw, 0) to South (0, hh), extrudes downward along +Y by cliff
            if (extrudeLeftSkirt)
            {
                int bIdx = allVerts.Count;
                allVerts.Add(worldPos + new Vector2(-hw, 0f));
                allVerts.Add(worldPos + new Vector2(0f, hh));
                allVerts.Add(worldPos + new Vector2(0f, hh + cliff));
                allVerts.Add(worldPos + new Vector2(-hw, cliff));

                allUvs.Add(new Vector2(0f, 0.5f));
                allUvs.Add(new Vector2(0.5f, 1f));
                allUvs.Add(new Vector2(0.5f, 1f));
                allUvs.Add(new Vector2(0f, 0.5f));

                allCols.Add(leftCliffColor);
                allCols.Add(leftCliffColor);
                allCols.Add(leftCliffColor);
                allCols.Add(leftCliffColor);

                allTris.Add(bIdx); allTris.Add(bIdx + 1); allTris.Add(bIdx + 2);
                allTris.Add(bIdx); allTris.Add(bIdx + 2); allTris.Add(bIdx + 3);
                currentCount += 4;
            }

            // Right skirt: South (0, hh) to East (hw, 0), extrudes downward along +Y by cliff
            if (extrudeRightSkirt)
            {
                int bIdx = allVerts.Count;
                allVerts.Add(worldPos + new Vector2(0f, hh));
                allVerts.Add(worldPos + new Vector2(hw, 0f));
                allVerts.Add(worldPos + new Vector2(hw, cliff));
                allVerts.Add(worldPos + new Vector2(0f, hh + cliff));

                allUvs.Add(new Vector2(0.5f, 1f));
                allUvs.Add(new Vector2(1f, 0.5f));
                allUvs.Add(new Vector2(1f, 0.5f));
                allUvs.Add(new Vector2(0.5f, 1f));

                allCols.Add(rightCliffColor);
                allCols.Add(rightCliffColor);
                allCols.Add(rightCliffColor);
                allCols.Add(rightCliffColor);

                allTris.Add(bIdx); allTris.Add(bIdx + 1); allTris.Add(bIdx + 2);
                allTris.Add(bIdx); allTris.Add(bIdx + 2); allTris.Add(bIdx + 3);
                currentCount += 4;
            }

            activeTileVertexCounts.Add(currentCount);
            activeTileSkirtFlags.Add((extrudeLeftSkirt, extrudeRightSkirt));
        }

        /// <summary>
        /// Computes organic 1-tile transition ring fog factors by probing 8-neighbor visibility and exploration states.
        /// Zero heap allocation (uses stackalloc).
        /// </summary>
        protected (float visibleFactor, float exploredFactor) ComputeFogTransitionFactors(int gx, int gy, bool isVisible, bool isExplored)
        {
            float visWeight = isVisible ? 1.0f : 0f;
            float expWeight = isExplored ? 1.0f : 0f;
            float totalWeight = 1.0f;

            ReadOnlySpan<(int dx, int dy, float w)> offsets = stackalloc (int, int, float)[]
            {
                ( 1,  0, 0.25f),
                (-1,  0, 0.25f),
                ( 0,  1, 0.25f),
                ( 0, -1, 0.25f),
                ( 1,  1, 0.125f),
                (-1, -1, 0.125f),
                ( 1, -1, 0.125f),
                (-1,  1, 0.125f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int nx = gx + offsets[i].dx;
                int ny = gy + offsets[i].dy;
                if (!IsWithinBounds(new Vector2I(nx, ny))) continue;

                var nInfo = GetTileDisplayInfo(nx, ny);
                if (!nInfo.active) continue;

                float w = offsets[i].w;
                totalWeight += w;
                if (nInfo.visible) visWeight += w;
                if (nInfo.explored) expWeight += w;
            }

            return (visWeight / totalWeight, expWeight / totalWeight);
        }

        public Color EvaluateTileColor(BiomeType biome, TilePOIType poi, bool visible, bool explored, bool active = true, int gx = 0, int gy = 0)
        {
            if (!active)
            {
                // Inactive void boundary / abyss: Seamless rolling mist
                return new Color(0.031f, 0.051f, 0.059f, 0.0f);
            }

            // Deterministic hash-based variation replacing Perlin noise
            float hash = MathF.Sin(gx * 12.9898f + gy * 78.233f) * 43758.5453f;
            float noise = hash - MathF.Floor(hash);
            float variation = 0.94f + noise * 0.12f;

            // Five Regions Biome-driven ink-wash mineral pigment palette
            Color biomeTint = biome switch
            {
                BiomeType.BambooMountain   => new Color(0.68f, 0.94f, 0.72f, 1f), // Southern Border: Verdant bamboo jade
                BiomeType.GrasslandPlateau => new Color(0.92f, 0.85f, 0.45f, 1f), // Northern Plains: Golden ochre steppe
                BiomeType.DesertOasis      => new Color(0.95f, 0.64f, 0.32f, 1f), // Western Desert: Warm cinnabar amber
                BiomeType.AzureArchipelago => new Color(0.28f, 0.76f, 0.95f, 1f), // Eastern Sea: Deep mineral azurite
                BiomeType.SpiritLake       => new Color(0.80f, 0.58f, 0.95f, 1f), // Central Continent: Ethereal violet mist
                _                          => Colors.White
            };

            Color baseColor = new(
                Math.Clamp(biomeTint.R * variation, 0f, 1f),
                Math.Clamp(biomeTint.G * variation, 0f, 1f),
                Math.Clamp(biomeTint.B * variation, 0f, 1f),
                1f
            );

            var (visFactor, expFactor) = ComputeFogTransitionFactors(gx, gy, visible, explored);

            if (!explored)
            {
                if (expFactor > 0.01f)
                {
                    // 1-tile organic transition ring around IsExplored
                    float ringT = Math.Clamp(expFactor * 2.5f, 0f, 1f);
                    float alpha = Godot.Mathf.Lerp(0.20f, 0.38f, ringT);
                    float rMul = Godot.Mathf.Lerp(0.35f, 0.48f, ringT);
                    float gMul = Godot.Mathf.Lerp(0.40f, 0.52f, ringT);
                    float bMul = Godot.Mathf.Lerp(0.36f, 0.49f, ringT);
                    return new Color(baseColor.R * rMul, baseColor.G * gMul, baseColor.B * bMul, alpha);
                }

                // Deep unexplored: Dim organic mountain landscape under gentle Dao mist (Alpha=0.20)
                return new Color(baseColor.R * 0.35f, baseColor.G * 0.40f, baseColor.B * 0.36f, 0.20f);
            }
            else if (!visible)
            {
                if (visFactor > 0.01f)
                {
                    // 1-tile organic transition ring around IsVisible
                    float visT = Math.Clamp(visFactor * 2.5f, 0f, 1f);
                    float alpha = Godot.Mathf.Lerp(0.55f, 0.85f, visT);
                    float rMul = Godot.Mathf.Lerp(0.60f, 0.85f, visT);
                    float gMul = Godot.Mathf.Lerp(0.65f, 0.88f, visT);
                    float bMul = Godot.Mathf.Lerp(0.62f, 0.86f, visT);
                    baseColor = new Color(baseColor.R * rMul, baseColor.G * gMul, baseColor.B * bMul, alpha);
                }
                else if (expFactor < 0.95f)
                {
                    // Explored boundary bordering unexplored fog
                    float borderT = Math.Clamp((1.0f - expFactor) * 2.0f, 0f, 1f);
                    float alpha = Godot.Mathf.Lerp(0.55f, 0.44f, borderT);
                    float rMul = Godot.Mathf.Lerp(0.60f, 0.50f, borderT);
                    float gMul = Godot.Mathf.Lerp(0.65f, 0.54f, borderT);
                    float bMul = Godot.Mathf.Lerp(0.62f, 0.51f, borderT);
                    baseColor = new Color(baseColor.R * rMul, baseColor.G * gMul, baseColor.B * bMul, alpha);
                }
                else
                {
                    // Standard explored out of sight: Half-lit mountain (Alpha=0.55)
                    baseColor = new Color(baseColor.R * 0.60f, baseColor.G * 0.65f, baseColor.B * 0.62f, 0.55f);
                }
            }
            else
            {
                if (visFactor < 0.95f)
                {
                    // Visible edge bordering fog: softly transition into Dao mist
                    float edgeT = Math.Clamp((1.0f - visFactor) * 1.5f, 0f, 1f);
                    float alpha = Godot.Mathf.Lerp(1.0f, 0.92f, edgeT);
                    float rMul = Godot.Mathf.Lerp(1.0f, 0.95f, edgeT);
                    float gMul = Godot.Mathf.Lerp(1.0f, 0.96f, edgeT);
                    float bMul = Godot.Mathf.Lerp(1.0f, 0.95f, edgeT);
                    baseColor = new Color(baseColor.R * rMul, baseColor.G * gMul, baseColor.B * bMul, alpha);
                }
                else
                {
                    // Fully visible core: Lush, vibrant verdant bamboo jade (Alpha=1.0)
                    baseColor = new Color(baseColor.R, baseColor.G, baseColor.B, 1.0f);
                }
            }

            Vector2I currentCoord = new(gx, gy);
            if (selectedTargetPos.HasValue && selectedTargetPos.Value == currentCoord)
            {
                // Soft jade-celadon highlight (#34D399 / #67E8F9) without heavy yellow fill
                baseColor = new Color(
                    Math.Clamp(baseColor.R * 0.85f + 0.05f, 0f, 1f),
                    Math.Clamp(baseColor.G * 1.15f + 0.12f, 0f, 1f),
                    Math.Clamp(baseColor.B * 1.10f + 0.12f, 0f, 1f),
                    baseColor.A
                );
            }
            else if (previewPathTiles.Contains(currentCoord))
            {
                baseColor = new Color(
                    Math.Clamp(baseColor.R * 0.50f, 0f, 1f),
                    Math.Clamp(baseColor.G * 1.25f + 0.25f, 0f, 1f),
                    Math.Clamp(baseColor.B * 0.70f + 0.10f, 0f, 1f),
                    baseColor.A
                );
            }

            return baseColor;
        }

        public virtual void UpdateFogMeshColors()
        {
            if (UseV2SpriteTiles)
            {
                UpdateFogV2Tiles();
                return;
            }

            if (combinedMesh == null) return;

            int totalVerts = _batchedVerts.Count;
            if (totalVerts == 0) return;

            if (_cachedColsArray.Length != totalVerts)
            {
                _cachedVertsArray = _batchedVerts.ToArray();
                _cachedUvsArray = _batchedUvs.ToArray();
                _cachedColsArray = _batchedCols.ToArray();
                _cachedTrisArray = _batchedTris.ToArray();

                _cachedSurfaceArrays.Resize((int)Mesh.ArrayType.Max);
                _cachedSurfaceArrays[(int)Mesh.ArrayType.Vertex] = _cachedVertsArray;
                _cachedSurfaceArrays[(int)Mesh.ArrayType.TexUV] = _cachedUvsArray;
                _cachedSurfaceArrays[(int)Mesh.ArrayType.Color] = _cachedColsArray;
                _cachedSurfaceArrays[(int)Mesh.ArrayType.Index] = _cachedTrisArray;
            }

            int vertexCursor = 0;
            for (int i = 0; i < activeGridCoords.Count; i++)
            {
                Vector2I coord = activeGridCoords[i];
                int vCount = (i < activeTileVertexCounts.Count) ? activeTileVertexCounts[i] : 4;

                var tileInfo = GetTileDisplayInfo(coord.X, coord.Y);
                Color topColor = EvaluateTileColor(tileInfo.biome, tileInfo.poi, tileInfo.visible, tileInfo.explored, tileInfo.active, coord.X, coord.Y);

                // First 4 vertices are always the top diamond surface
                for (int v = 0; v < 4 && (vertexCursor + v) < totalVerts; v++)
                {
                    _batchedCols[vertexCursor + v] = topColor;
                    _cachedColsArray[vertexCursor + v] = topColor;
                }

                // Remaining vertices belong to the 2.5D cliff skirts with directional shading
                if (vCount > 4 && i < activeTileSkirtFlags.Count)
                {
                    var (hasLeft, hasRight) = activeTileSkirtFlags[i];
                    int cursor = vertexCursor + 4;
                    if (hasLeft)
                    {
                        Color leftCliffColor = new(topColor.R * 0.48f, topColor.G * 0.48f, topColor.B * 0.52f, topColor.A);
                        for (int v = 0; v < 4 && (cursor + v) < totalVerts; v++)
                        {
                            _batchedCols[cursor + v] = leftCliffColor;
                            _cachedColsArray[cursor + v] = leftCliffColor;
                        }
                        cursor += 4;
                    }
                    if (hasRight)
                    {
                        Color rightCliffColor = new(topColor.R * 0.34f, topColor.G * 0.34f, topColor.B * 0.38f, topColor.A);
                        for (int v = 0; v < 4 && (cursor + v) < totalVerts; v++)
                        {
                            _batchedCols[cursor + v] = rightCliffColor;
                            _cachedColsArray[cursor + v] = rightCliffColor;
                        }
                        cursor += 4;
                    }
                }

                vertexCursor += vCount;
            }

            // Dynamically refresh vertex colors on the combined mesh surface without heap allocations
            _cachedSurfaceArrays[(int)Mesh.ArrayType.Color] = _cachedColsArray;
            combinedMesh.ClearSurfaces();
            combinedMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, _cachedSurfaceArrays);
        }

        protected virtual (BiomeType biome, TilePOIType poi, bool visible, bool explored, bool active) GetTileDisplayInfo(int x, int y)
        {
            int distance = Math.Abs(x - GridWidth / 2) + Math.Abs(y - GridHeight / 2);
            return (BiomeType.BambooMountain, TilePOIType.None, distance <= 4, distance <= 5, true);
        }

        public Vector2 GridToWorld(Vector2I gridPos)
        {
            return ExplorationSystem.GridToWorld(gridPos.ToGridCoord()).ToGodot();
        }

        public Vector2I WorldToGrid(Vector2 worldPos)
        {
            return ExplorationSystem.WorldToGrid(worldPos.ToWorldVec2()).ToGodot();
        }

        public bool IsWithinBounds(Vector2I pos)
        {
            return pos.X >= 0 && pos.X < GridWidth && pos.Y >= 0 && pos.Y < GridHeight;
        }

        #region Normalized Taiwu V2 Sprite Tile System

        protected void EnsureV2TileLayer()
        {
            YSortEnabled = true;
            if (_v2TileLayer == null || !GodotObject.IsInstanceValid(_v2TileLayer))
            {
                _v2TileLayer = GetNodeOrNull<Node2D>("V2TileLayer");
                if (_v2TileLayer == null)
                {
                    _v2TileLayer = new Node2D
                    {
                        Name = "V2TileLayer",
                        YSortEnabled = true,
                        ZIndex = 0
                    };
                    AddChild(_v2TileLayer);
                }
            }
            _v2TileLayer.YSortEnabled = true;
        }

        protected void LoadV2Textures()
        {
            _v2TexMeadow ??= ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/v2_test/tile_meadow_01.png");
            _v2TexBamboo ??= ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/v2_test/tile_bamboo_crag.png");
            _v2TexClan   ??= ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/v2_test/poi_clan_stronghold.png");
            _v2TexCave   ??= ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/v2_test/poi_ancient_gu_cave.png");
        }

        public virtual Texture2D? GetV2TextureForTile(BiomeType biome, TilePOIType poi)
        {
            LoadV2Textures();
            if (poi == TilePOIType.ClanPost)
            {
                return _v2TexClan ?? _v2TexMeadow;
            }
            if (poi == TilePOIType.AncientGuCave)
            {
                return _v2TexCave ?? _v2TexBamboo ?? _v2TexMeadow;
            }
            if (biome == BiomeType.BambooMountain)
            {
                return _v2TexBamboo ?? _v2TexMeadow;
            }
            return _v2TexMeadow;
        }

        public void RebuildV2Tiles()
        {
            EnsureV2TileLayer();
            LoadV2Textures();

            if (_v2TileLayer == null) return;
            _v2TileLayer.Visible = true;

            var activeCoords = new HashSet<Vector2I>();

            int maxDiag = (GridWidth - 1) + (GridHeight - 1);
            for (int diag = 0; diag <= maxDiag; diag++)
            {
                int startX = Math.Max(0, diag - (GridHeight - 1));
                int endX = Math.Min(GridWidth - 1, diag);
                for (int x = startX; x <= endX; x++)
                {
                    int y = diag - x;
                    var tileInfo = GetTileDisplayInfo(x, y);
                    if (!tileInfo.active) continue;

                    Vector2I coord = new(x, y);
                    activeCoords.Add(coord);

                    Texture2D? tex = GetV2TextureForTile(tileInfo.biome, tileInfo.poi);
                    if (tex == null) continue;

                    if (!_v2TileSprites.TryGetValue(coord, out var sp) || !GodotObject.IsInstanceValid(sp))
                    {
                        sp = null;
                        while (_v2SpritePool.Count > 0)
                        {
                            var candidate = _v2SpritePool.Pop();
                            if (GodotObject.IsInstanceValid(candidate))
                            {
                                sp = candidate;
                                break;
                            }
                        }

                        if (sp == null)
                        {
                            sp = new Sprite2D();
                            _v2TileLayer.AddChild(sp);
                        }

                        _v2TileSprites[coord] = sp;
                    }

                    sp.Name = $"Tile_{x}_{y}";
                    sp.Texture = tex;
                    sp.Centered = true;
                    sp.Offset = V2_SPRITE_OFFSET;
                    sp.Scale = new Vector2(V2_WORLD_SCALE, V2_WORLD_SCALE);
                    sp.Position = GridToWorld(coord);
                    sp.YSortEnabled = true;
                }
            }

            List<Vector2I>? toRecycle = null;
            foreach (var (coord, sp) in _v2TileSprites)
            {
                if (!activeCoords.Contains(coord))
                {
                    toRecycle ??= new List<Vector2I>();
                    toRecycle.Add(coord);
                    if (GodotObject.IsInstanceValid(sp))
                    {
                        sp.Visible = false;
                        _v2SpritePool.Push(sp);
                    }
                }
            }

            if (toRecycle != null)
            {
                for (int i = 0; i < toRecycle.Count; i++)
                {
                    _v2TileSprites.Remove(toRecycle[i]);
                }
            }

            UpdateFogV2Tiles();
        }

        public void UpdateFogV2Tiles()
        {
            if (_v2TileLayer == null || !_v2TileLayer.Visible) return;

            foreach (var (coord, sp) in _v2TileSprites)
            {
                if (!GodotObject.IsInstanceValid(sp)) continue;

                var tileInfo = GetTileDisplayInfo(coord.X, coord.Y);
                if (!tileInfo.active)
                {
                    sp.Visible = false;
                    continue;
                }

                if (tileInfo.visible)
                {
                    sp.Visible = true;
                    if (selectedTargetPos.HasValue && selectedTargetPos.Value == coord)
                    {
                        sp.Modulate = new Color(1.18f, 1.15f, 1.05f, 1f);
                    }
                    else if (previewPathTiles.Contains(coord))
                    {
                        sp.Modulate = new Color(1.10f, 1.08f, 1.02f, 1f);
                    }
                    else
                    {
                        sp.Modulate = Colors.White;
                    }
                }
                else if (tileInfo.explored)
                {
                    sp.Visible = true;
                    sp.Modulate = new Color(0.42f, 0.44f, 0.48f, 0.95f);
                }
                else
                {
                    sp.Visible = false;
                }
            }
        }

        private void ClearV2Tiles()
        {
            foreach (var sp in _v2TileSprites.Values)
            {
                if (GodotObject.IsInstanceValid(sp))
                {
                    sp.QueueFree();
                }
            }
            _v2TileSprites.Clear();

            while (_v2SpritePool.Count > 0)
            {
                var sp = _v2SpritePool.Pop();
                if (GodotObject.IsInstanceValid(sp))
                {
                    sp.QueueFree();
                }
            }
        }

        #endregion
    }
}
