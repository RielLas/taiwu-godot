using System;
using System.Collections.Generic;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Map;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Phase 2: Isometric Tile Manager (Godot 2D).
    /// Synchronizes visual rendering with procedural tile state and reacts to Fog of War events.
    /// Supports dynamic Width x Height and organic Perlin island masks with natural missing edges.
    /// </summary>
    public partial class IsometricTileManager : IsometricTileRenderer
    {
        public const float TILE_THICKNESS = 0.25f;

        private TileNode[,]? instanceGrid;
        private TaiwuGridRenderer? gridRenderer;

        public override void _Ready()
        {
            EnsureGridAllocated(GridWidth, GridHeight);
            GenerateOrganicRegionInstance(seed: 17881588, width: GridWidth, height: GridHeight);
            GameEventManager.OnGameLoaded += HandleGameLoaded;

            base._Ready();
            EnsureGridRenderer();
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

        public override void _ExitTree()
        {
            GameEventManager.OnGameLoaded -= HandleGameLoaded;
            base._ExitTree();
        }

        private void EnsureGridAllocated(int width, int height)
        {
            GridWidth = width;
            GridHeight = height;
            if (instanceGrid == null || instanceGrid.GetLength(0) != width || instanceGrid.GetLength(1) != height)
            {
                instanceGrid = new TileNode[width, height];
            }
        }

        private void HandleGameLoaded(string slotID, bool success)
        {
            if (!success) return;
            var mapState = MapContextProvider.WorldMapResolver?.Invoke();
            if (mapState != null)
            {
                SyncFromStateContext(mapState);
            }
        }

        protected override (BiomeType biome, TilePOIType poi, bool visible, bool explored, bool active) GetTileDisplayInfo(int x, int y)
        {
            if (instanceGrid != null && x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
            {
                var t = instanceGrid[x, y];
                return (t.Biome, t.POI, t.IsVisible, t.IsExplored, t.IsActive);
            }
            return base.GetTileDisplayInfo(x, y);
        }

        public bool IsTileWalkable(Vector2I pos)
        {
            if (!IsWithinBounds(pos) || instanceGrid == null) return false;
            var tile = instanceGrid[pos.X, pos.Y];
            if (!tile.IsActive) return false;
            return LandmarkRegistry.IsTraversable(tile.POI, tile.CustomLandmarkId);
        }

        public void GenerateRegionInstance(int seed, BiomeType primaryBiome = BiomeType.BambooMountain)
        {
            GenerateOrganicRegionInstance(seed, GridWidth, GridHeight, primaryBiome);
        }

        public void GenerateOrganicRegionInstance(int seed, int width, int height, BiomeType primaryBiome = BiomeType.BambooMountain)
        {
            EnsureGridAllocated(width, height);
            Random rng = new(seed);

            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;
            float maxRadius = MathF.Max(width, height) * 0.5f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float dist = MathF.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                    float normalizedDist = dist / maxRadius;

                    // Pseudo-perlin / sinusoidal noise for ragged mountain edge silhouette
                    float n1 = MathF.Sin(x * 0.22f + (seed % 1000) * 0.05f) * MathF.Cos(y * 0.22f + (seed % 1000) * 0.05f);
                    float edgeValue = normalizedDist + n1 * 0.35f;

                    // Core center is always solid; outer boundaries create natural chasms and missing edges
                    bool isActive = (normalizedDist <= 0.35f) || (edgeValue <= 0.88f);
                    int danger = Math.Clamp((int)MathF.Round(dist / 3f) + 1, 1, 5);

                    instanceGrid![x, y] = new TileNode
                    {
                        GridPos = new GridCoord(x, y),
                        Biome = primaryBiome,
                        POI = TilePOIType.None,
                        DangerRank = danger,
                        IsExplored = (dist <= 4.5f),
                        IsVisible = (dist <= 3.5f),
                        IsActive = isActive
                    };
                }
            }

            // Cull disconnected island fragments so all traversable land is contiguous
            CullDisconnectedIslands((int)MathF.Round(centerX), (int)MathF.Round(centerY), width, height);

            // Populate landmarks & perimeter Mountain Passes
            PopulatePOIs(width, height);

            if (GenerateMeshVisuals)
            {
                RebuildTileMesh();
            }
        }

        private void CullDisconnectedIslands(int startX, int startY, int width, int height)
        {
            if (instanceGrid == null) return;
            startX = Math.Clamp(startX, 0, width - 1);
            startY = Math.Clamp(startY, 0, height - 1);

            instanceGrid[startX, startY].IsActive = true;

            bool[,] reachable = new bool[width, height];
            Queue<Vector2I> queue = new();
            queue.Enqueue(new Vector2I(startX, startY));
            reachable[startX, startY] = true;

            Vector2I[] dirs = new Vector2I[]
            {
                new Vector2I(1, 0),
                new Vector2I(-1, 0),
                new Vector2I(0, 1),
                new Vector2I(0, -1)
            };

            while (queue.Count > 0)
            {
                Vector2I curr = queue.Dequeue();
                for (int i = 0; i < dirs.Length; i++)
                {
                    Vector2I next = curr + dirs[i];
                    if (next.X >= 0 && next.X < width && next.Y >= 0 && next.Y < height)
                    {
                        if (!reachable[next.X, next.Y] && instanceGrid[next.X, next.Y].IsActive)
                        {
                            reachable[next.X, next.Y] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (instanceGrid[x, y].IsActive && !reachable[x, y])
                    {
                        instanceGrid[x, y].IsActive = false;
                    }
                }
            }
        }

        public void SyncFromStateContext(WorldMapState mapState)
        {
            if (mapState == null || mapState.Tiles == null) return;

            int w = mapState.GridWidth > 0 ? mapState.GridWidth : 30;
            int h = mapState.GridHeight > 0 ? mapState.GridHeight : 30;
            EnsureGridAllocated(w, h);

            bool hasAnyActive = false;
            for (int i = 0; i < mapState.Tiles.Length; i++)
            {
                if (mapState.Tiles[i].IsActive)
                {
                    hasAnyActive = true;
                    break;
                }
            }

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    TileNode node = mapState.GetTile(x, y);
                    if (!hasAnyActive)
                    {
                        node.IsActive = true;
                    }
                    instanceGrid![x, y] = node;
                }
            }

            if (GenerateMeshVisuals)
            {
                RebuildTileMesh();
            }
            UpdateFogMeshColors();
        }

        public void SyncToStateContext(WorldMapState mapState)
        {
            if (mapState == null || instanceGrid == null) return;
            mapState.GridWidth = GridWidth;
            mapState.GridHeight = GridHeight;
            mapState.Tiles = new TileNode[GridWidth * GridHeight];
            mapState.ExploredTileIndices.Clear();
            mapState.VisibleTileIndices.Clear();

            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    TileNode node = instanceGrid[x, y];
                    mapState.SetTile(x, y, node);
                    int idx = y * GridWidth + x;
                    if (node.IsExplored) mapState.ExploredTileIndices.Add(idx);
                    if (node.IsVisible) mapState.VisibleTileIndices.Add(idx);
                }
            }
        }

        private void PopulatePOIs(int width, int height)
        {
            if (instanceGrid == null) return;

            int cx = Math.Clamp(width / 2, 0, width - 1);
            int cy = Math.Clamp(height / 2, 0, height - 1);

            // 1. Clan Stronghold (Gu Yue Clan) at or near center
            if (instanceGrid[cx, cy].IsActive)
            {
                instanceGrid[cx, cy].POI = TilePOIType.ClanPost;
            }

            // 2. Caravan Way Station
            int wsX = Math.Clamp(cx + 3, 0, width - 1);
            if (instanceGrid[wsX, cy].IsActive)
            {
                instanceGrid[wsX, cy].POI = TilePOIType.WayStation;
            }

            // 3. Perimeter Mountain Passes / Trailheads
            List<Vector2I> candidates = new();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!instanceGrid[x, y].IsActive || instanceGrid[x, y].POI != TilePOIType.None) continue;

                    bool nearVoid = (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                        || !instanceGrid[Math.Max(0, x - 1), y].IsActive
                        || !instanceGrid[Math.Min(width - 1, x + 1), y].IsActive
                        || !instanceGrid[x, Math.Max(0, y - 1)].IsActive
                        || !instanceGrid[x, Math.Min(height - 1, y + 1)].IsActive;

                    if (nearVoid)
                    {
                        candidates.Add(new Vector2I(x, y));
                    }
                }
            }

            candidates.Sort((a, b) =>
            {
                float da = MathF.Sqrt((a.X - cx) * (a.X - cx) + (a.Y - cy) * (a.Y - cy));
                float db = MathF.Sqrt((b.X - cx) * (b.X - cx) + (b.Y - cy) * (b.Y - cy));
                return db.CompareTo(da);
            });

            int passesPlaced = 0;
            for (int i = 0; i < candidates.Count && passesPlaced < 3; i++)
            {
                Vector2I pos = candidates[i];
                bool tooClose = false;
                for (int p = 0; p < i; p++)
                {
                    if (candidates[p] != pos && instanceGrid[candidates[p].X, candidates[p].Y].POI == TilePOIType.MountainPass)
                    {
                        float d = MathF.Sqrt((pos.X - candidates[p].X) * (pos.X - candidates[p].X) + (pos.Y - candidates[p].Y) * (pos.Y - candidates[p].Y));
                        if (d < 6f)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }

                if (!tooClose)
                {
                    instanceGrid[pos.X, pos.Y].POI = TilePOIType.MountainPass;
                    passesPlaced++;
                }
            }

            // 4. Interactive, Scenic, and Obstacle Landmarks scattered across active tiles
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!instanceGrid[x, y].IsActive || instanceGrid[x, y].POI != TilePOIType.None) continue;

                    int h = (x * 37 + y * 17) % 100;
                    if (h == 7 || h == 29)
                    {
                        instanceGrid[x, y].POI = TilePOIType.SpiritVein;
                    }
                    else if (h == 41 || h == 83)
                    {
                        instanceGrid[x, y].POI = TilePOIType.AncientGuCave;
                    }
                    else if (h == 13 || h == 67)
                    {
                        instanceGrid[x, y].POI = TilePOIType.WildBeastDen;
                    }
                    else if (h == 23 || h == 77)
                    {
                        instanceGrid[x, y].POI = TilePOIType.AncientPine;
                    }
                    else if (h == 31 || h == 89)
                    {
                        instanceGrid[x, y].POI = TilePOIType.ShatteredStele;
                    }
                    else if (h == 53 && (x > 3 && x < width - 4 && y > 3 && y < height - 4))
                    {
                        // Craggy boulder precipice obstacle
                        instanceGrid[x, y].POI = TilePOIType.BoulderPrecipice;
                    }
                }
            }
        }

        protected override void HandleFogOfWarUpdated(GridCoord center, int radius)
        {
            if (instanceGrid == null) return;

            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    instanceGrid[x, y].IsVisible = false;
                }
            }

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) <= radius)
                    {
                        int tx = center.X + dx;
                        int ty = center.Y + dy;
                        if (tx >= 0 && tx < GridWidth && ty >= 0 && ty < GridHeight)
                        {
                            instanceGrid[tx, ty].IsVisible = true;
                            instanceGrid[tx, ty].IsExplored = true;
                        }
                    }
                }
            }

            base.HandleFogOfWarUpdated(center, radius);
        }

        public TileNode GetTile(int x, int y)
        {
            if (instanceGrid != null && x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                return instanceGrid[x, y];
            return default;
        }

        public void SetExplored(int x, int y, bool explored)
        {
            if (instanceGrid != null && x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                instanceGrid[x, y].IsExplored = explored;
        }

        public void SetVisible(int x, int y, bool visible)
        {
            if (instanceGrid != null && x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                instanceGrid[x, y].IsVisible = visible;
        }

        public void SetBiome(int x, int y, BiomeType biome)
        {
            if (instanceGrid != null && x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
                instanceGrid[x, y].Biome = biome;
        }
    }
}
