using System;
using System.Collections.Generic;

namespace TaiwuGu.Map
{
    /// <summary>
    /// Single authority for sector terrain, POIs, and initial visibility across all 5 regions.
    ///
    /// The renderer must never author tiles. Everything a sector contains is produced here from
    /// (SectorBlueprint, seed), which makes generation deterministic, testable without Godot,
    /// and small enough to persist as a seed plus deltas instead of a full tile array.
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

        /// <summary>Designed legacy starting-sector landmarks for backward compatibility.</summary>
        private static readonly (int x, int y, TilePOIType poi)[] LegacyAnchorPOIs =
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

        /// <summary>
        /// Backward-compatible generator for legacy unit tests and unmapped procedural sectors.
        /// </summary>
        public static TileNode[] Generate(int seed, int width, int height, BiomeType biome)
        {
            var anchors = new List<AnchorPOISpec>();
            foreach (var (x, y, poi) in LegacyAnchorPOIs)
            {
                anchors.Add(new AnchorPOISpec(x, y, poi));
            }

            var bp = new SectorBlueprint
            {
                SectorKey = "legacy_sector",
                DisplayName = "Generated Sector",
                Region = WorldRegionID.SouthernBorder,
                Biome = biome,
                Seed = seed,
                Width = width,
                Height = height,
                DangerRankFloor = 1,
                PrimaryPOIs = anchors
            };

            return Generate(bp, seed);
        }

        /// <summary>
        /// Authoritative generation from a declarative SectorBlueprint with full biome-specific active masking.
        /// </summary>
        public static TileNode[] Generate(SectorBlueprint blueprint, int? seedOverride = null)
        {
            int seed = seedOverride ?? blueprint.Seed;
            int width = Math.Max(1, blueprint.Width);
            int height = Math.Max(1, blueprint.Height);
            BiomeType biome = blueprint.Biome;

            var tiles = new TileNode[width * height];
            var rng = new DeterministicRandom(seed ^ (width * 73856093) ^ (height * 19349663));

            int centerX = width / 2;
            int centerY = height / 2;
            float radialCenterX = (width - 1) * 0.5f;
            float radialCenterY = (height - 1) * 0.5f;
            float maxRadius = MathF.Max(width, height) * 0.5f;

            float phase = rng.NextFloat() * MathF.PI * 2f;
            float wobble = 0.30f + rng.NextFloat() * 0.10f;

            (float x, float y, float r)[]? archipelagoIslands = null;
            if (biome == BiomeType.AzureArchipelago)
            {
                archipelagoIslands = GenerateArchipelagoIslands(centerX, centerY, maxRadius, phase, rng);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = x - radialCenterX;
                    float dy = y - radialCenterY;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);
                    float normalized = maxRadius > 0f ? distance / maxRadius : 0f;

                    bool active = EvaluateActive(
                        biome, x, y, normalized, distance, phase, wobble,
                        radialCenterX, radialCenterY, centerX, centerY, archipelagoIslands);

                    int manhattan = Math.Abs(x - centerX) + Math.Abs(y - centerY);
                    int danger = Math.Clamp(blueprint.DangerRankFloor + (int)MathF.Floor(distance / 6f), blueprint.DangerRankFloor, 5);

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

            // Ensure the anchor start hub is active and reachable
            int hubX = centerX;
            int hubY = centerY;
            if (biome != BiomeType.SpiritLake)
            {
                tiles[centerY * width + centerX].IsActive = true;
            }
            else
            {
                // In Spirit Lake, central basin is lake void. Find closest shore tile as the hub.
                float minDist = float.MaxValue;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (tiles[y * width + x].IsActive)
                        {
                            float d = MathF.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                            if (d < minDist)
                            {
                                minDist = d;
                                hubX = x;
                                hubY = y;
                            }
                        }
                    }
                }
            }

            // Carve routes to primary anchor POIs
            CarveAnchorRoutes(tiles, width, height, hubX, hubY, blueprint.PrimaryPOIs, biome, radialCenterX, radialCenterY, maxRadius);
            CullDisconnected(tiles, width, height, hubX, hubY);
            PlaceMountainPasses(tiles, width, height, hubX, hubY);
            PlaceAnchorPOIs(tiles, width, height, blueprint.PrimaryPOIs);
            ScatterAmbientLandmarks(tiles, width, height, rng);

            return tiles;
        }

        private static bool EvaluateActive(
            BiomeType biome,
            int x, int y,
            float normalized, float distance,
            float phase, float wobble,
            float radialCenterX, float radialCenterY,
            int centerX, int centerY,
            (float x, float y, float r)[]? archipelagoIslands)
        {
            float noise = MathF.Sin(x * 0.22f + phase) * MathF.Cos(y * 0.22f + phase * 0.7f);

            switch (biome)
            {
                case BiomeType.BambooMountain:
                {
                    bool active = normalized <= CORE_RADIUS || (normalized + noise * wobble) <= EDGE_THRESHOLD;
                    // Karst chasm carving along ridge flanks
                    if (active && normalized > 0.30f)
                    {
                        float karst = MathF.Abs(MathF.Sin(x * 0.31f + phase) * MathF.Cos(y * 0.27f - phase));
                        if (karst > 0.58f) active = false;
                    }
                    return active;
                }

                case BiomeType.GrasslandPlateau:
                {
                    // Broad open steppe with higher threshold
                    bool active = normalized <= 0.40f || (normalized + noise * 0.15f) <= 0.96f;
                    // Diagonal wind-ravine fissures with natural bridges
                    if (active && normalized > 0.25f)
                    {
                        float cosA = MathF.Cos(phase);
                        float sinA = MathF.Sin(phase);
                        float distToFissure = MathF.Abs((x - radialCenterX) * cosA + (y - radialCenterY) * sinA + MathF.Sin(y * 0.25f) * 2.5f);
                        if (distToFissure < 1.2f && (Math.Abs(x - centerX) > 3 || Math.Abs(y - centerY) > 3))
                        {
                            active = false;
                        }
                    }
                    return active;
                }

                case BiomeType.DesertOasis:
                {
                    // Inner fertile oasis ring is guaranteed active
                    if (normalized <= 0.28f) return true;
                    // Outer shifting sand dunes with high wobble and erosion
                    return (normalized + noise * 0.38f) <= 0.82f;
                }

                case BiomeType.AzureArchipelago:
                {
                    if (archipelagoIslands == null || archipelagoIslands.Length == 0)
                    {
                        return normalized <= CORE_RADIUS || (normalized + noise * wobble) <= EDGE_THRESHOLD;
                    }
                    for (int i = 0; i < archipelagoIslands.Length; i++)
                    {
                        var island = archipelagoIslands[i];
                        float idx = x - island.x;
                        float idy = y - island.y;
                        if (MathF.Sqrt(idx * idx + idy * idy) <= island.r)
                        {
                            return true;
                        }
                    }
                    return false;
                }

                case BiomeType.SpiritLake:
                {
                    // Central lake basin is water void
                    if (normalized <= 0.22f) return false;
                    // Perimeter shore ring is active
                    return (normalized + noise * 0.18f) <= 0.86f;
                }

                default:
                    return normalized <= CORE_RADIUS || (normalized + noise * wobble) <= EDGE_THRESHOLD;
            }
        }

        private static (float x, float y, float r)[] GenerateArchipelagoIslands(
            int centerX, int centerY, float maxRadius, float phase, DeterministicRandom rng)
        {
            var list = new List<(float x, float y, float r)>
            {
                (centerX, centerY, 4.5f)
            };

            int islandCount = 4 + rng.NextInt(3); // 4 to 6 satellite islands
            for (int i = 0; i < islandCount; i++)
            {
                float angle = phase + (i * MathF.PI * 2f / islandCount) + (rng.NextFloat() * 0.4f - 0.2f);
                float dist = (0.42f + rng.NextFloat() * 0.30f) * maxRadius;
                float ix = centerX + MathF.Cos(angle) * dist;
                float iy = centerY + MathF.Sin(angle) * dist;
                float radius = 3.2f + rng.NextFloat() * 2.0f;
                list.Add((ix, iy, radius));
            }

            return list.ToArray();
        }

        private static void CarveAnchorRoutes(
            TileNode[] tiles, int width, int height, int startX, int startY, IReadOnlyList<AnchorPOISpec> anchors,
            BiomeType biome, float radialCenterX, float radialCenterY, float maxRadius)
        {
            if (anchors == null) return;

            foreach (var anchor in anchors)
            {
                if (anchor.X < 0 || anchor.X >= width || anchor.Y < 0 || anchor.Y >= height) continue;

                int x = startX;
                int y = startY;
                while (x != anchor.X)
                {
                    x += Math.Sign(anchor.X - x);
                    if (ShouldCarve(biome, x, y, radialCenterX, radialCenterY, maxRadius))
                    {
                        tiles[y * width + x].IsActive = true;
                    }
                }
                while (y != anchor.Y)
                {
                    y += Math.Sign(anchor.Y - y);
                    if (ShouldCarve(biome, x, y, radialCenterX, radialCenterY, maxRadius))
                    {
                        tiles[y * width + x].IsActive = true;
                    }
                }
                tiles[anchor.Y * width + anchor.X].IsActive = true;
            }
        }

        private static bool ShouldCarve(BiomeType biome, int x, int y, float radialCenterX, float radialCenterY, float maxRadius)
        {
            if (biome == BiomeType.SpiritLake)
            {
                float dx = x - radialCenterX;
                float dy = y - radialCenterY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (maxRadius > 0f && (dist / maxRadius) <= 0.22f) return false;
            }
            return true;
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
            var candidates = new List<(int x, int y, float distance)>();

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

            var placed = new List<(int x, int y)>();
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

        private static void PlaceAnchorPOIs(TileNode[] tiles, int width, int height, IReadOnlyList<AnchorPOISpec> anchors)
        {
            if (anchors == null) return;

            foreach (var anchor in anchors)
            {
                if (anchor.X < 0 || anchor.X >= width || anchor.Y < 0 || anchor.Y >= height) continue;

                int index = anchor.Y * width + anchor.X;
                if (!tiles[index].IsActive) continue;

                tiles[index].POI = anchor.POI;
                if (!string.IsNullOrEmpty(anchor.CustomLandmarkId))
                {
                    tiles[index].CustomLandmarkId = anchor.CustomLandmarkId;
                }
            }
        }

        private static void ScatterAmbientLandmarks(TileNode[] tiles, int width, int height, DeterministicRandom rng)
        {
            int count = width * height;
            int target = Math.Max(MIN_AMBIENT_LANDMARKS, count / 90 * AMBIENT_LANDMARKS_PER_90_TILES);
            int placed = 0;

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
