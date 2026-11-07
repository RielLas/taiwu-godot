using System;
using TaiwuGu.Map;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 3: Domain Service for Overworld Exploration & Fog of War.
    /// Pure C# domain logic managing grid calculations, sensory radius, POI detection, and POI actions.
    /// </summary>
    public class ExplorationService : IDisposable
    {
        public const float TILE_HALF_WIDTH = 2.4f;
        public const float TILE_HALF_HEIGHT = 1.2f;

        private GameStateContext? _context;
        private int _sensoryRadius = 3;

        public GameStateContext? Context => _context;
        public int SensoryRadius => _sensoryRadius;

        public void Initialize(GameStateContext context, int baseSensoryRadius = 3)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _sensoryRadius = baseSensoryRadius;

            if (_context.WorldMap.Tiles == null || _context.WorldMap.Tiles.Length == 0)
            {
                _context.WorldMap.InitializeDefaultGrid();
            }

            UpdateVisionInternal(_context.WorldMap.PlayerGridPos, _sensoryRadius);

            GameEventManager.OnPlayerMoved         += HandlePlayerMoved;
            GameEventManager.OnPOIActionRequested += HandlePOIActionRequested;
            GameEventManager.OnInspectTileRequested += HandleInspectTileRequested;

            Debug.Log($"[ExplorationService] Initialized. Grid: {_context.WorldMap.GridWidth}x{_context.WorldMap.GridHeight} | Player at: {_context.WorldMap.PlayerGridPos} | Vision: {_sensoryRadius}");
        }

        public void Dispose()
        {
            GameEventManager.OnPlayerMoved         -= HandlePlayerMoved;
            GameEventManager.OnPOIActionRequested -= HandlePOIActionRequested;
            GameEventManager.OnInspectTileRequested -= HandleInspectTileRequested;
            _context = null;
        }

        public void BroadcastCurrentState()
        {
            if (_context == null) return;
            GameEventManager.TriggerFogOfWarUpdated(_context.WorldMap.PlayerGridPos, _sensoryRadius);
        }

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

        public bool IsWithinBounds(Vector2Int pos)
        {
            if (_context == null) return false;
            return _context.WorldMap.IsWithinBounds(pos);
        }

        public TileNode GetTile(int x, int y)
        {
            if (_context == null) return default;
            return _context.WorldMap.GetTile(x, y);
        }

        private void HandlePlayerMoved(Vector2Int newGridPos)
        {
            if (_context == null) return;

            _context.WorldMap.PlayerGridPos = newGridPos;
            UpdateVisionInternal(newGridPos, _sensoryRadius);
            GameEventManager.TriggerFogOfWarUpdated(newGridPos, _sensoryRadius);

            TileNode tile = _context.WorldMap.GetTile(newGridPos.x, newGridPos.y);
            var def = LandmarkRegistry.Resolve(tile);
            var crimeScene = _context.Forensics?.ActiveCrimeScenes?.Find(c => c.GridPos == newGridPos && !c.IsTamperedClean && c.LingeringSoulQiTerms > 0);
            bool hasCultivators = _context.LivingNPCs != null && _context.LivingNPCs.Exists(n => n.IsAlive && n.CurrentTilePos == newGridPos);

            if (def != null && def.Category == LandmarkCategory.Scenic)
            {
                GameEventManager.TriggerTileInspected(def.Name, def.SealText, def.LoreDescription, newGridPos);
            }
            else if (tile.POI != TilePOIType.None || crimeScene != null || hasCultivators)
            {
                string title = def != null 
                    ? def.Name 
                    : (tile.POI != TilePOIType.None ? GetPOITitle(tile.POI) : (hasCultivators ? "Wilderness Gathering (荒野聚落)" : "Wilderness Crime Scene (案发死地)"));
                string desc = def != null 
                    ? def.LoreDescription 
                    : (tile.POI != TilePOIType.None ? GetPOIDescription(tile.POI) : (hasCultivators ? "Fellow wandering cultivators rest and convene under the mountain canopy." : "Morbid blood stains and lingering spiritual residue mar the secluded wilderness floor."));
                GameEventManager.TriggerPOIArrived(tile.POI, newGridPos, title, desc, tile.DangerRank);
            }
            else
            {
                // Wilderness Tile: Evaluate low-probability random terrain hazard, beast ambush, and wild Gu encounter (~4% per directive)
                EvaluateWildernessExploration(newGridPos, tile);
            }
        }

        public void UpdateVision(Vector2Int center, int radius)
        {
            UpdateVisionInternal(center, radius);
            GameEventManager.TriggerFogOfWarUpdated(center, radius);
        }

        private void UpdateVisionInternal(Vector2Int center, int radius)
        {
            if (_context == null || _context.WorldMap.Tiles == null) return;

            int width = _context.WorldMap.GridWidth;
            int height = _context.WorldMap.GridHeight;

            for (int i = 0; i < _context.WorldMap.VisibleTileIndices.Count; i++)
            {
                int idx = _context.WorldMap.VisibleTileIndices[i];
                if (idx >= 0 && idx < _context.WorldMap.Tiles.Length)
                {
                    _context.WorldMap.Tiles[idx].IsVisible = false;
                }
            }
            _context.WorldMap.VisibleTileIndices.Clear();

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) <= radius)
                    {
                        Vector2Int target = center + new Vector2Int(dx, dy);
                        if (_context.WorldMap.IsWithinBounds(target))
                        {
                            int idx = target.y * width + target.x;
                            _context.WorldMap.Tiles[idx].IsVisible = true;
                            _context.WorldMap.Tiles[idx].IsExplored = true;

                            if (!_context.WorldMap.VisibleTileIndices.Contains(idx))
                                _context.WorldMap.VisibleTileIndices.Add(idx);

                            if (!_context.WorldMap.ExploredTileIndices.Contains(idx))
                                _context.WorldMap.ExploredTileIndices.Add(idx);
                        }
                    }
                }
            }
        }

        private void HandleInspectTileRequested(Vector2Int pos)
        {
            if (_context == null) return;
            TileNode tile = _context.WorldMap.GetTile(pos.x, pos.y);
            var def = LandmarkRegistry.Resolve(tile);
            var crimeScene = _context.Forensics?.ActiveCrimeScenes?.Find(c => c.GridPos == pos && !c.IsTamperedClean && c.LingeringSoulQiTerms > 0);
            bool hasCultivators = _context.LivingNPCs != null && _context.LivingNPCs.Exists(n => n.IsAlive && n.CurrentTilePos == pos);

            if (def != null)
            {
                if (def.Category == LandmarkCategory.Scenic)
                {
                    GameEventManager.TriggerTileInspected(def.Name, def.SealText, def.LoreDescription, pos);
                    return;
                }

                if (def.Category == LandmarkCategory.Interactive)
                {
                    if (def.DirectInteraction != null)
                    {
                        def.DirectInteraction(_context, pos);
                        return;
                    }
                    GameEventManager.TriggerPOIArrived(tile.POI, pos, def.Name, def.LoreDescription, tile.DangerRank);
                    return;
                }
            }

            if (crimeScene != null || hasCultivators)
            {
                string title = hasCultivators ? "Wilderness Gathering (荒野聚落)" : "Wilderness Crime Scene (案发死地)";
                string desc = hasCultivators ? "Fellow wandering cultivators rest and convene under the mountain canopy." : "Morbid blood stains and lingering spiritual residue mar the secluded wilderness floor.";
                GameEventManager.TriggerPOIArrived(TilePOIType.None, pos, title, desc, tile.DangerRank);
            }
        }

        private void HandlePOIActionRequested(TilePOIType poi, Vector2Int pos, string actionId)
        {
            if (_context == null) return;

            Debug.Log($"[ExplorationService] POI Action requested: {actionId} at {pos} ({poi})");
            var def = LandmarkRegistry.GetByPOI(poi);
            if (def?.ExecuteAction != null)
            {
                def.ExecuteAction(_context, actionId, pos);
            }
        }

        private void EvaluateWildernessExploration(Vector2Int pos, TileNode tile)
        {
            if (_context?.Cultivator == null) return;

            // 1. Terrain Hazard Check (~3% base chance, scaled slightly by DangerRank)
            float hazardRoll = (float)Random.Shared.NextDouble();
            float hazardThreshold = 0.02f + (0.01f * Math.Max(0, tile.DangerRank));
            if (hazardRoll < hazardThreshold)
            {
                string hazardName = Random.Shared.NextDouble() < 0.5
                    ? "Poison Bamboo Spores (竹海瘴毒)"
                    : "Craggy Mountain Rockfall (崩岩暗石)";

                int hpDmg = Random.Shared.Next(4, 9);
                float seaDmg = (float)(Random.Shared.NextDouble() * 3.0 + 2.0); // 2-5% sea leakage

                _context.Cultivator.CurrentHealth = Math.Max(1, _context.Cultivator.CurrentHealth - hpDmg);
                _context.Cultivator.CurrentEssenceSea = Math.Max(0f, _context.Cultivator.CurrentEssenceSea - seaDmg);

                GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
                GameEventManager.TriggerTerrainHazardTriggered(pos, hazardName, hpDmg, seaDmg);
                Debug.Log($"[ExplorationService] Stumbled into terrain hazard '{hazardName}' at {pos}! Suffered -{hpDmg} HP and -{seaDmg:F1}% Essence Sea.");
            }

            // 2. Wild Beast Ambush Check (~3% chance in hazardous areas, tile danger >= 1)
            if (tile.DangerRank >= 1)
            {
                float ambushRoll = (float)Random.Shared.NextDouble();
                if (ambushRoll < 0.03f)
                {
                    string beastName = "Frenzied Razor Boar (狂躁獠牙野猪)";
                    Debug.Log($"[ExplorationService] Wild beast ambush triggered at {pos} by {beastName}!");
                    GameEventManager.TriggerWildBeastAmbushed(pos, beastName);
                    GameEventManager.RaiseRequestEnterCombat(TaiwuGu.Combat.ArenaEncounterType.WildBoarHunt, pos);
                    return;
                }
            }

            // 3. Low Random Wild Gu Discovery (~4% chance per directive)
            float wildGuRoll = (float)Random.Shared.NextDouble();
            if (wildGuRoll < 0.04f)
            {
                string[] wildPool = new[]
                {
                    "gu_moonlight_r1",
                    "gu_white_boar_r1",
                    "gu_little_light_r1",
                    "gu_bronze_skin_r1",
                    "gu_liquor_worm_r1"
                };
                string pickedGu = wildPool[Random.Shared.Next(0, wildPool.Length)];
                WildOrganismTemperament temp = (WildOrganismTemperament)Random.Shared.Next(0, 3);
                var organism = WildOrganismData.CreateWildOrganism(pickedGu, temp);

                Debug.Log($"[ExplorationService] Spotted wild Gu organism in the wilderness: {organism.Name} ({temp}) at {pos}!");
                GameEventManager.RaiseRequestOpenWildGuEncounter(organism);
            }
        }

        public static string GetPOITitle(TilePOIType poi)
        {
            var def = LandmarkRegistry.GetByPOI(poi);
            if (def != null) return def.Name;
            return "Wilderness Node (荒野)";
        }

        public static string GetPOIDescription(TilePOIType poi)
        {
            var def = LandmarkRegistry.GetByPOI(poi);
            if (def != null) return def.LoreDescription;
            return "Untamed wilderness blanketed by bamboo groves and mist.";
        }
    }
}
