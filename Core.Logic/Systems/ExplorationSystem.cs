using System;
using System.Collections.Generic;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing isometric coordinate conversion, fog of war, tile inspection, POI action dispatch, and wilderness exploration rolls.
    /// </summary>
    public static class ExplorationSystem
    {
        public const float TILE_HALF_WIDTH = 2.4f;
        public const float TILE_HALF_HEIGHT = 1.2f;
        public const int DEFAULT_SENSORY_RADIUS = 3;

        public static WorldVec2 GridToWorld(GridCoord gridPos)
        {
            float wx = (gridPos.X - gridPos.Y) * TILE_HALF_WIDTH;
            float wy = (gridPos.X + gridPos.Y) * TILE_HALF_HEIGHT;
            return new WorldVec2(wx, wy);
        }

        public static GridCoord WorldToGrid(WorldVec2 worldPos)
        {
            float gx = (worldPos.X / TILE_HALF_WIDTH + worldPos.Y / TILE_HALF_HEIGHT) * 0.5f;
            float gy = (worldPos.Y / TILE_HALF_HEIGHT - worldPos.X / TILE_HALF_WIDTH) * 0.5f;
            return new GridCoord((int)MathF.Round(gx), (int)MathF.Round(gy));
        }

        public static void MovePlayer(WorldStore world, GridCoord targetPos, int sensoryRadius = DEFAULT_SENSORY_RADIUS, IRandomSource? rng = null, bool evaluateWilderness = true, bool openInteractivePOI = true)
        {
            var sector = world.GetCurrentSector();
            if (sector == null || !sector.IsWithinBounds(targetPos)) return;

            world.SetPosition(world.PlayerEntity, targetPos, sector.SectorKey);
            TimeSystem.DeductActionPoints(world, 1);

            WorldFactory.RevealVision(sector, targetPos, sensoryRadius);
            WorldSignals.EmitTopics(DirtyTopic.PlayerPos | DirtyTopic.Fog | DirtyTopic.NPCs);

            if (!evaluateWilderness) return;

            TileNode tile = sector.GetTile(targetPos.X, targetPos.Y);
            LandmarkDefinition? def = LandmarkRegistry.Resolve(tile);
            bool hasCrime = ForensicsSystem.GetActiveCrimeSceneAt(world, targetPos).HasValue;
            bool hasNpcs = SocialVendettaSystem.GetAliveNPCsAt(world, targetPos).Count > 0;

            if (def != null && def.Category == LandmarkCategory.Scenic)
            {
                WorldSignals.EmitScenicToast(def.Name, def.SealText, def.LoreDescription, targetPos);
            }
            else if (openInteractivePOI && (tile.POI != TilePOIType.None || hasCrime || hasNpcs))
            {
                if (def?.DirectInteraction != null)
                {
                    def.DirectInteraction(world, targetPos);
                }
                else
                {
                    WorldSignals.EmitOpenTilePOI(targetPos);
                }
            }
            else
            {
                EvaluateWildernessExploration(world, targetPos, tile, rng);
            }
        }

        public static void InspectTile(WorldStore world, GridCoord pos)
        {
            var sector = world.GetCurrentSector();
            if (sector == null || !sector.IsWithinBounds(pos)) return;

            TileNode tile = sector.GetTile(pos.X, pos.Y);
            LandmarkDefinition? def = LandmarkRegistry.Resolve(tile);
            bool hasCrime = ForensicsSystem.GetActiveCrimeSceneAt(world, pos).HasValue;
            bool hasNpcs = SocialVendettaSystem.GetAliveNPCsAt(world, pos).Count > 0;

            if (def != null)
            {
                if (def.Category == LandmarkCategory.Scenic)
                {
                    WorldSignals.EmitScenicToast(def.Name, def.SealText, def.LoreDescription, pos);
                    return;
                }

                if (def.Category == LandmarkCategory.Interactive)
                {
                    if (def.DirectInteraction != null)
                    {
                        def.DirectInteraction(world, pos);
                        return;
                    }
                    WorldSignals.EmitOpenTilePOI(pos);
                    return;
                }
            }

            if (hasCrime || hasNpcs)
            {
                WorldSignals.EmitOpenTilePOI(pos);
            }
        }

        public static void ExecutePOIAction(WorldStore world, GridCoord pos, string actionId)
        {
            var sector = world.GetCurrentSector();
            if (sector == null) return;

            TileNode tile = sector.GetTile(pos.X, pos.Y);
            LandmarkDefinition? def = LandmarkRegistry.Resolve(tile);
            if (def?.ExecuteAction != null)
            {
                def.ExecuteAction(world, actionId, pos);
            }
        }

        public static void UpdateVision(WorldStore world, GridCoord center, int radius = DEFAULT_SENSORY_RADIUS)
        {
            var sector = world.GetCurrentSector();
            if (sector == null) return;
            WorldFactory.RevealVision(sector, center, radius);
            WorldSignals.EmitTopics(DirtyTopic.Fog);
        }

        private static void EvaluateWildernessExploration(WorldStore world, GridCoord pos, TileNode tile, IRandomSource? rngOverride)
        {
            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) ||
                !world.TryGet<ApertureComponent>(world.PlayerEntity, out var aperture))
                return;

            IRandomSource rng = rngOverride ?? new SeededRandom(world.RunSeed ^ (pos.X * 73856093) ^ (pos.Y * 19349663) ^ (world.GetSingleton<WorldClockComponent>()?.ActionPointsRemaining ?? 0));

            // 1. Terrain Hazard (~2% + 1% * DangerRank)
            float hazardRoll = (float)rng.NextDouble();
            float hazardThreshold = 0.02f + (0.01f * Math.Max(0, tile.DangerRank));
            if (hazardRoll < hazardThreshold)
            {
                string hazardName = rng.NextDouble() < 0.5
                    ? "Poison Bamboo Spores (竹海瘴毒)"
                    : "Craggy Mountain Rockfall (崩岩暗石)";

                int hpDmg = rng.Next(4, 9);
                float seaDmg = (float)(rng.NextDouble() * 3.0 + 2.0);

                aperture.CurrentHealth = MathF.Max(1f, aperture.CurrentHealth - hpDmg);
                aperture.CurrentEssenceSea = MathF.Max(0f, aperture.CurrentEssenceSea - seaDmg);

                WorldSignals.EmitTopics(DirtyTopic.Aperture);
                WorldSignals.EmitTerrainHazardTriggered(pos, hazardName, hpDmg, seaDmg);
                Debug.Log($"[ExplorationSystem] Hazard '{hazardName}' at {pos}: -{hpDmg} HP, -{seaDmg:F1}% Sea.");
            }

            // 2. Wild Beast Ambush (~3% if DangerRank >= 1)
            if (tile.DangerRank >= 1 && rng.NextDouble() < 0.03f)
            {
                string beastName = "Frenzied Razor Boar (狂躁獠牙野猪)";
                WorldSignals.EmitWildBeastAmbushed(pos, beastName);
                CombatSystem.StartCombat(world, ArenaEncounterType.WildBoarHunt, pos, isLethal: true, enemyName: beastName);
                return;
            }

            // 3. Wild Gu Discovery (Miniscule chance: ~0.5%)
            if (rng.NextDouble() < 0.005f)
            {
                string pickedGu = PickWildGuForSector(world, world.CurrentSectorKey, rng);
                var temp = (WildOrganismTemperament)rng.Next(0, 3);
                var organism = WildOrganismData.CreateWildOrganism(pickedGu, temp);
                WildEncounterSystem.OpenEncounter(world, organism);
            }
        }

        public static string PickWildGuForSector(WorldStore world, string sectorKey, IRandomSource rng)
        {
            string sKey = string.IsNullOrEmpty(sectorKey) ? world.CurrentSectorKey : sectorKey;
            if (WorldAtlasRegistry.TryGet(sKey, out var bp) && bp != null && GuDatabase.IsLoaded)
            {
                var candidates = new List<string>();
                var fallbackCandidates = new List<string>();

                foreach (var def in GuDatabase.GetAll())
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                    if (def.Rank > bp.MaxWildGuRank) continue;

                    fallbackCandidates.Add(def.Id);

                    if (bp.NativeGuPaths != null && bp.NativeGuPaths.Count > 0)
                    {
                        for (int i = 0; i < bp.NativeGuPaths.Count; i++)
                        {
                            if (string.Equals(bp.NativeGuPaths[i], def.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                candidates.Add(def.Id);
                                break;
                            }
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    return candidates[rng.Next(0, candidates.Count)];
                }
                if (fallbackCandidates.Count > 0)
                {
                    return fallbackCandidates[rng.Next(0, fallbackCandidates.Count)];
                }
            }

            string[] defaultPool = { "gu_moonlight_r1", "gu_white_boar_r1", "gu_little_light_r1", "gu_bronze_skin_r1", "gu_liquor_worm_r1" };
            return defaultPool[rng.Next(0, defaultPool.Length)];
        }
    }
}
