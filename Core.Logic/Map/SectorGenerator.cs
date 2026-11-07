using System;

namespace TaiwuGu.Map
{
    /// <summary>
    /// Single authority for sector terrain, POIs and initial visibility.
    ///
    /// The renderer must never author tiles. Everything a sector contains is produced here from
    /// (seed, width, height, biome), which makes generation deterministic, testable without Godot,
    /// and small enough to persist as a seed plus deltas instead of a full tile array.
    ///
    /// Starting-sector anchor POIs are hand-placed so the designed opening area (clan stronghold,
    /// caravan, springs, caves, dens) stays stable; the surrounding terrain and the scattered
    /// landmarks are seed-driven.
    /// </summary>
    public static class SectorGenerator
    {
        public const int DEFAULT_WIDTH = 30;
        public const int DEFAULT_HEIGHT = 30;

        private const float CORE_RADIUS = 0.35f;
        private const float EDGE_THRESHOLD = 0.88f;
        private const int INITIAL_EXPLORED_RADIUS = 5;
        private const int INITIAL_VISIBLE_RADIUS = 4;
        private const int AMBIENT_LANDMARKS_PER_90_TILES = 1;
        private const int MIN_AMBIENT_LANDMARKS = 6;
        private const int MAX_MOUNTAIN_PASSES = 3;
        private const float MOUNTAIN_PASS_SPACING = 6f;

        /// <summary>Designed starting-sector landmarks. Coordinates are absolute and clamped to the sector.</summary>
        private static readonly (int x, int y, TilePOIType poi)[] AnchorPOIs =
        {
            (15, 15, TilePOIType.ClanPost),
            (19, 15, TilePOIType.WayStation),
            (14, 17, TilePOIType.SpiritVein),
            (10, 12, TilePOIType.SpiritVein),
            (21, 20, TilePOIType.SpiritVein),
            (6, 22, TilePOIType.AncientGuCave),
            (23, 8, TilePOIType.AncientGuCave),
            (5, 7, TilePOIType.AncientGuCave),
            (25, 24, TilePOIType.AncientGuCave),
            (11, 25, TilePOIType.AncientGuCave),
            (18, 11, TilePOIType.WildBeastDen),
            (9, 19, TilePOIType.WildBeastDen),
            (22, 17, TilePOIType.WildBeastDen),
            (13, 7, TilePOIType.WildBeastDen),
            (26, 12, TilePOIType.WildBeastDen),
            (4, 15, TilePOIType.WildBeastDen),
        };

        private static readonly TilePOIType[] AmbientLandmarkPool =
        {
            TilePOIType.SpiritVein,
            TilePOIType.AncientGuCave,
            TilePOIType.WildBeastDen,
            TilePOIType.AncientPine,
            TilePOIType.AncientPine,
            TilePOIType.ShatteredStele,
            TilePOIType.BoulderPrecipice,
        };

        public static TileNode[] Generate(int seed, int width, int height, BiomeType biome)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            var tiles = new TileNode[width * height];
            var rng = new DeterministicRandom(seed ^ (width * 73856093) ^ (height * 19349663));

            int centerX = width / 2;
            int centerY = height / 2;
            float radialCenterX = (width - 1) * 0.5f;
            float radialCenterY = (height - 1) * 0.5f;
            float maxRadius = MathF.Max(width, height) * 0.5f;

            // Seed-derived edge silhouette: same seed, same coastline; different seed, different island.
            float phase = rng.NextFloat() * MathF.PI * 2f;
            float wobble = 0.30f + rng.NextFloat() * 0.10f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = x - radialCenterX;
                    float dy = y - radialCenterY;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);
                    float normalized = maxRadius > 0f ? distance / maxRadius : 0f;

                    float noise = MathF.Sin(x * 0.22f + phase) * MathF.Cos(y * 0.22f + phase * 0.7f);
                    bool active = normalized <= CORE_RADIUS || (normalized + noise * wobble) <= EDGE_THRESHOLD;

                    int manhattan = Math.Abs(x - centerX) + Math.Abs(y - centerY);
                    int danger = Math.Clamp((int)MathF.Round(distance / 3f) + 1, 1, 5);

                    tiles[y * width + x] = new TileNode
                    {
                        GridPos = new Vector2Int(x, y),
                        Biome = biome,
                        POI = TilePOIType.None,
                        DangerRank = danger,
                        IsExplored = active && manhattan <= INITIAL_EXPLORED_RADIUS,
                        IsVisible = active && manhattan <= INITIAL_VISIBLE_RADIUS,
                        IsActive = active
                    };
                }
            }

            tiles[centerY * width + centerX].IsActive = true;

            // Anchors must exist and be reachable, or the designed opening sector loses its landmarks.
            CarveAnchorRoutes(tiles, width, height, centerX, centerY);
            CullDisconnected(tiles, width, height, centerX, centerY);
            PlaceMountainPasses(tiles, width, height, centerX, centerY);
            PlaceAnchorPOIs(tiles, width, height);
            ScatterAmbientLandmarks(tiles, width, height, rng);

            return tiles;
        }

        private static void CarveAnchorRoutes(TileNode[] tiles, int width, int height, int centerX, int centerY)
        {
            foreach (var anchor in AnchorPOIs)
            {
                if (anchor.x < 0 || anchor.x >= width || anchor.y < 0 || anchor.y >= height) continue;

                int x = centerX;
                int y = centerY;
                while (x != anchor.x)
                {
                    x += Math.Sign(anchor.x - x);
                    tiles[y * width + x].IsActive = true;
                }
                while (y != anchor.y)
                {
                    y += Math.Sign(anchor.y - y);
                    tiles[y * width + x].IsActive = true;
                }
                tiles[anchor.y * width + anchor.x].IsActive = true;
            }
        }

        private static void CullDisconnected(TileNode[] tiles, int width, int height, int startX, int startY)
        {
            int count = width * height;
            var reachable = new bool[count];
            var queue = new int[count];
            int head = 0;
            int tail = 0;

            int startIndex = startY * width + startX;
            reachable[startIndex] = true;
            queue[tail++] = startIndex;

            ReadOnlySpan<int> dx = stackalloc int[] { 1, -1, 0, 0 };
            ReadOnlySpan<int> dy = stackalloc int[] { 0, 0, 1, -1 };

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width;
                int y = index / width;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dx[d];
                    int ny = y + dy[d];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                    int neighbour = ny * width + nx;
                    if (reachable[neighbour] || !tiles[neighbour].IsActive) continue;

                    reachable[neighbour] = true;
                    queue[tail++] = neighbour;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (tiles[i].IsActive && !reachable[i])
                {
                    tiles[i].IsActive = false;
                    tiles[i].IsExplored = false;
                    tiles[i].IsVisible = false;
                }
            }
        }

        private static void PlaceMountainPasses(TileNode[] tiles, int width, int height, int centerX, int centerY)
        {
            var candidates = new System.Collections.Generic.List<(int x, int y, float distance)>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!tiles[y * width + x].IsActive) continue;

                    bool touchesVoid = x == 0 || x == width - 1 || y == 0 || y == height - 1
                        || !tiles[y * width + (x - 1)].IsActive
                        || !tiles[y * width + (x + 1)].IsActive
                        || !tiles[(y - 1) * width + x].IsActive
                        || !tiles[(y + 1) * width + x].IsActive;

                    if (!touchesVoid) continue;

                    float dx = x - centerX;
                    float dy = y - centerY;
                    candidates.Add((x, y, MathF.Sqrt(dx * dx + dy * dy)));
                }
            }

            candidates.Sort((a, b) => b.distance.CompareTo(a.distance));

            var placed = new System.Collections.Generic.List<(int x, int y)>();
            foreach (var candidate in candidates)
            {
                if (placed.Count >= MAX_MOUNTAIN_PASSES) break;

                bool tooClose = false;
                foreach (var existing in placed)
                {
                    float dx = candidate.x - existing.x;
                    float dy = candidate.y - existing.y;
                    if (MathF.Sqrt(dx * dx + dy * dy) < MOUNTAIN_PASS_SPACING)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) continue;

                tiles[candidate.y * width + candidate.x].POI = TilePOIType.MountainPass;
                placed.Add((candidate.x, candidate.y));
            }
        }

        private static void PlaceAnchorPOIs(TileNode[] tiles, int width, int height)
        {
            foreach (var anchor in AnchorPOIs)
            {
                if (anchor.x < 0 || anchor.x >= width || anchor.y < 0 || anchor.y >= height) continue;

                int index = anchor.y * width + anchor.x;
                if (!tiles[index].IsActive) continue;

                tiles[index].POI = anchor.poi;
            }
        }

        private static void ScatterAmbientLandmarks(TileNode[] tiles, int width, int height, DeterministicRandom rng)
        {
            int count = width * height;
            int target = Math.Max(MIN_AMBIENT_LANDMARKS, count / 90 * AMBIENT_LANDMARKS_PER_90_TILES);
            int placed = 0;

            // Bounded attempts keep this O(n) even on a crowded or tiny sector.
            int attempts = target * 12;
            for (int i = 0; i < attempts && placed < target; i++)
            {
                int index = rng.NextInt(count);
                TileNode tile = tiles[index];

                if (!tile.IsActive || tile.POI != TilePOIType.None) continue;

                tiles[index].POI = AmbientLandmarkPool[rng.NextInt(AmbientLandmarkPool.Length)];
                placed++;
            }
        }
    }
}
