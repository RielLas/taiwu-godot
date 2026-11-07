using System;
using System.Collections.Generic;
using TaiwuGu.Combat;
using TaiwuGu.Map;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 2 / Milestone 2.6 Domain Service: Inter-Mountain & Regional Transit Engine.
    /// Pure C# domain logic managing:
    /// - Intra-regional travel between Qing Mao Mountain strongholds (Gu Yue, Bai Clan, Xiong Ridge).
    /// - Road Ambush mechanics (25% chance) with deferred arrival until combat concludes.
    /// - Five Regions boundary wall traversal with canonical Reverend Insanity aperture backlash.
    /// - Sector state snapshot preservation across mountain transfers.
    /// Strictly decoupled under Constitutional Laws 1, 2, 4, and 6.
    /// </summary>
    public class InterMountainTransitService : IDisposable
    {
        public const int TRAVEL_STONE_COST = 25;
        public const int TRAVEL_AP_COST = 6;
        public const float ROAD_AMBUSH_CHANCE = 0.25f;

        public const int REGIONAL_TRANSIT_STONE_COST = 60;
        public const int REGIONAL_TRANSIT_AP_COST = 15;

        private IGameStateProvider? _provider;
        private GameStateContext _context => _provider!.Current;
        private WorldRegionID _currentRegion = WorldRegionID.SouthernBorder;
        private SouthernMountainID _currentMountain = SouthernMountainID.GuYueVillage;
        private SouthernMountainID? _pendingMountainDestination = null;
        private bool _isTransitModalOpen = false;
        private bool _isRegionalMode = false;

        private readonly Dictionary<SouthernMountainID, MountainSectorData> _mountainDatabase = new();
        private readonly Dictionary<WorldRegionID, RegionalSectorData> _regionalDatabase = new();

        public WorldRegionID CurrentRegion => _currentRegion;
        public SouthernMountainID CurrentMountain => _currentMountain;
        public SouthernMountainID? PendingMountainDestination => _pendingMountainDestination;
        public bool IsTransitModalOpen => _isTransitModalOpen;
        public bool IsRegionalMode => _isRegionalMode;

        public IReadOnlyDictionary<SouthernMountainID, MountainSectorData> MountainDatabase => _mountainDatabase;
        public IReadOnlyDictionary<WorldRegionID, RegionalSectorData> RegionalDatabase => _regionalDatabase;

        public void Initialize(GameStateContext context, IGameStateProvider? provider = null)
        {
            _provider = provider ?? new StaticGameStateProvider(context);
            InitializeDatabases();

            if (_context.WorldMap != null)
            {
                _currentRegion = _context.WorldMap.CurrentRegion;
            }

            GameEventManager.OnMountainTransitRequested += HandleMountainTransitRequested;
            GameEventManager.OnRegionTransitRequested += HandleRegionTransitRequested;
            GameEventManager.OnOpenTransitModalRequested += HandleOpenTransitModalRequested;
            GameEventManager.OnCloseTransitModalRequested += HandleCloseTransitModalRequested;
            GameEventManager.OnCombatConcluded += HandleCombatConcluded;

            Debug.Log($"[InterMountainTransitService] Initialized. Region: {_currentRegion}, Mountain: {_currentMountain}");
        }

        public void Dispose()
        {
            GameEventManager.OnMountainTransitRequested -= HandleMountainTransitRequested;
            GameEventManager.OnRegionTransitRequested -= HandleRegionTransitRequested;
            GameEventManager.OnOpenTransitModalRequested -= HandleOpenTransitModalRequested;
            GameEventManager.OnCloseTransitModalRequested -= HandleCloseTransitModalRequested;
            GameEventManager.OnCombatConcluded -= HandleCombatConcluded;
            _provider = null;
        }

        private void InitializeDatabases()
        {
            _mountainDatabase[SouthernMountainID.GuYueVillage] = new MountainSectorData
            {
                MountainID = SouthernMountainID.GuYueVillage,
                MountainName = "Gu Yue Mountain (古月山寨)",
                RandomSeed = 17881588,
                SectorWidth = 28,
                SectorHeight = 28,
                Biome = BiomeType.BambooMountain,
                Description = "A towering verdant mountain thick with bamboo groves and Moon Orchid caves. Governed by the Gu Yue Clan."
            };

            _mountainDatabase[SouthernMountainID.BaiClanMountain] = new MountainSectorData
            {
                MountainID = SouthernMountainID.BaiClanMountain,
                MountainName = "Bai Clan Mountain (白家山)",
                RandomSeed = 28993412,
                SectorWidth = 34,
                SectorHeight = 24,
                Biome = BiomeType.BambooMountain,
                Description = "Steep limestone ridges enveloped in white mist. Home to the Bai Clan, renowned for bone-path inheritances and pure springs."
            };

            _mountainDatabase[SouthernMountainID.XiongRidge] = new MountainSectorData
            {
                MountainID = SouthernMountainID.XiongRidge,
                MountainName = "Xiong Ridge (熊家寨)",
                RandomSeed = 39012845,
                SectorWidth = 32,
                SectorHeight = 32,
                Biome = BiomeType.BambooMountain,
                Description = "Dense forested peaks populated by black bears and savage swine. Governed by the ferocious Xiong Clan."
            };

            _regionalDatabase[WorldRegionID.SouthernBorder] = new RegionalSectorData
            {
                RegionID = WorldRegionID.SouthernBorder,
                RegionName = "Southern Border (南疆)",
                RandomSeed = 17881588,
                PrimaryBiome = BiomeType.BambooMountain,
                WallType = RegionalWallType.MiasmaWall,
                LoreDescription = "Endless karst mountain ranges, primeval bamboo forests, deep secluded ravines, and poisonous earth miasma."
            };

            _regionalDatabase[WorldRegionID.NorthernPlains] = new RegionalSectorData
            {
                RegionID = WorldRegionID.NorthernPlains,
                RegionName = "Northern Plains (北原)",
                RandomSeed = 55129031,
                PrimaryBiome = BiomeType.GrasslandPlateau,
                WallType = RegionalWallType.LicoriceWall,
                LoreDescription = "Immense roaring grasslands and howling wind steppes. Ruled by the Huang Jin bloodline tribes and fierce blood-path warriors."
            };

            _regionalDatabase[WorldRegionID.WesternDesert] = new RegionalSectorData
            {
                RegionID = WorldRegionID.WesternDesert,
                RegionName = "Western Desert (西漠)",
                RandomSeed = 71839204,
                PrimaryBiome = BiomeType.DesertOasis,
                WallType = RegionalWallType.FeatherWall,
                LoreDescription = "Vast sea of scorching golden sand dunes, ghost illusions, and rich fire-path gemstone veins clustered around flourishing oases."
            };

            _regionalDatabase[WorldRegionID.EasternSea] = new RegionalSectorData
            {
                RegionID = WorldRegionID.EasternSea,
                RegionName = "Eastern Sea (东海)",
                RandomSeed = 82940173,
                PrimaryBiome = BiomeType.AzureArchipelago,
                WallType = RegionalWallType.BlueWaterWall,
                LoreDescription = "Countless celestial islands, deep ocean trenches, bubbling spirit springs, and flourishing maritime trade ports."
            };

            _regionalDatabase[WorldRegionID.CentralContinent] = new RegionalSectorData
            {
                RegionID = WorldRegionID.CentralContinent,
                RegionName = "Central Continent (中洲)",
                RandomSeed = 99482015,
                PrimaryBiome = BiomeType.SpiritLake,
                WallType = RegionalWallType.SaintWall,
                LoreDescription = "The supreme heartland of the mortal world, governed by the Ten Great Ancient Sects and overseen by the eternal Heavenly Court."
            };
        }

        public string GetCurrentSectorKey() => $"Sector_{_currentRegion}_{_currentMountain}";

        public bool CanTravelToMountain(SouthernMountainID target, out string reason)
        {
            reason = string.Empty;
            if (target == _currentMountain)
            {
                reason = "Already at this mountain!";
                return false;
            }

            if (_context?.Cultivator == null)
            {
                reason = "No active cultivator";
                return false;
            }

            if (_context.Cultivator.PrimevalStones < TRAVEL_STONE_COST)
            {
                reason = $"Need {TRAVEL_STONE_COST} Stones (Have {_context.Cultivator.PrimevalStones})";
                return false;
            }

            int ap = _context.Calendar?.ActionPointsRemaining ?? 0;
            if (ap < TRAVEL_AP_COST)
            {
                reason = $"Need {TRAVEL_AP_COST} Days/AP (Have {ap})";
                return false;
            }

            return true;
        }

        public bool CanTravelToRegion(WorldRegionID target, out string reason)
        {
            reason = string.Empty;
            if (target == _currentRegion)
            {
                reason = "Already in this Region!";
                return false;
            }

            if (_context?.Cultivator == null)
            {
                reason = "No active cultivator";
                return false;
            }

            if (_context.Cultivator.PrimevalStones < REGIONAL_TRANSIT_STONE_COST)
            {
                reason = $"Need {REGIONAL_TRANSIT_STONE_COST} Stones (Have {_context.Cultivator.PrimevalStones})";
                return false;
            }

            int ap = _context.Calendar?.ActionPointsRemaining ?? 0;
            if (ap < REGIONAL_TRANSIT_AP_COST)
            {
                reason = $"Need {REGIONAL_TRANSIT_AP_COST} Days/AP (Have {ap})";
                return false;
            }

            return true;
        }

        public void TravelToMountain(SouthernMountainID destination)
        {
            if (!CanTravelToMountain(destination, out string reason))
            {
                Debug.LogWarning($"[InterMountainTransitService] Transit Blocked: {reason}");
                return;
            }

            if (_context?.Cultivator == null) return;

            // Deduct toll costs
            _context.Cultivator.PrimevalStones -= TRAVEL_STONE_COST;
            GameEventManager.TriggerPrimevalStonesChanged(_context.Cultivator.PrimevalStones);
            if (_context.Calendar != null)
            {
                _context.Calendar.ActionPointsRemaining = Math.Max(0, _context.Calendar.ActionPointsRemaining - TRAVEL_AP_COST);
                GameEventManager.TriggerActionPointsChanged(_context.Calendar.ActionPointsRemaining, _context.Calendar.MonthlyAPBudget);
            }
            GameEventManager.RaiseRequestDeductActionPoints(TRAVEL_AP_COST);

            Debug.Log($"[InterMountainTransitService] Commenced mountain transit toward [{destination}]! Spent {TRAVEL_STONE_COST} Stones and {TRAVEL_AP_COST} AP.");

            // Save active sector state before travel
            SaveCurrentSectorMapState();

            // Close transit modal
            HandleCloseTransitModalRequested();

            // 25% Road Ambush check
            bool isAmbushed = Random.Shared.NextDouble() < ROAD_AMBUSH_CHANCE;
            if (isAmbushed)
            {
                _pendingMountainDestination = destination;
                Debug.LogWarning("[InterMountainTransitService] Road ambush triggered! Sector arrival deferred until battle conclusion.");
                GameEventManager.TriggerRoadAmbushOccurred(ArenaEncounterType.WildBoarHunt, new GridCoord(15, 15));
                GameEventManager.RaiseRequestEnterCombat(ArenaEncounterType.WildBoarHunt, new GridCoord(15, 15));
            }
            else
            {
                ExecuteMountainLoad(destination);
            }
        }

        public void TravelToRegion(WorldRegionID destination, bool sealAperture)
        {
            if (!CanTravelToRegion(destination, out string reason))
            {
                Debug.LogWarning($"[InterMountainTransitService] Regional Transit Blocked: {reason}");
                return;
            }

            if (_context?.Cultivator == null) return;

            // Deduct toll costs
            _context.Cultivator.PrimevalStones -= REGIONAL_TRANSIT_STONE_COST;
            GameEventManager.TriggerPrimevalStonesChanged(_context.Cultivator.PrimevalStones);
            if (_context.Calendar != null)
            {
                _context.Calendar.ActionPointsRemaining = Math.Max(0, _context.Calendar.ActionPointsRemaining - REGIONAL_TRANSIT_AP_COST);
                GameEventManager.TriggerActionPointsChanged(_context.Calendar.ActionPointsRemaining, _context.Calendar.MonthlyAPBudget);
            }
            GameEventManager.RaiseRequestDeductActionPoints(REGIONAL_TRANSIT_AP_COST);

            RegionalWallType wall = RegionalWallLaw.GetBoundaryWall(_currentRegion, destination);
            string wallName = RegionalWallLaw.GetWallName(wall);

            Debug.Log($"[InterMountainTransitService] Traversing [{wallName}] toward [{destination}] (Sealed: {sealAperture})");

            ApplyRegionalWallBacklash(wall, sealAperture);
            SaveCurrentSectorMapState();
            HandleCloseTransitModalRequested();

            ExecuteRegionLoad(destination);
        }

        private void ApplyRegionalWallBacklash(RegionalWallType wall, bool isApertureSealed)
        {
            if (_context?.Cultivator == null) return;
            var cult = _context.Cultivator;

            if (isApertureSealed)
            {
                // Canon Restraint Strategy: Suppress primeval sea to 0%, minimal wall shock (-5% durability)
                cult.CurrentEssenceSea = 0f;
                cult.ApertureWallDurability = Math.Max(5f, cult.ApertureWallDurability - (cult.MaxWallDurability * 0.05f));
                GameEventManager.TriggerPrimevalSeaChanged(0f, cult.AptitudePercent);
                GameEventManager.TriggerApertureWallStateChanged(cult.WallWashingProgress, cult.ApertureWallDurability, cult.MaxWallDurability, cult.GetRankDescription());
                Debug.Log("[InterMountainTransitService] [Aperture Restraint] Aperture sealed with Restraint Gu! Avoided catastrophic Dao mark backlash.");
                return;
            }

            switch (cult.Rank)
            {
                case 1:
                    cult.CurrentEssenceSea = Math.Max(0f, cult.CurrentEssenceSea - 10f);
                    GameEventManager.TriggerPrimevalSeaChanged(cult.CurrentEssenceSea, cult.AptitudePercent);
                    break;

                case 2:
                    cult.CurrentEssenceSea = Math.Max(0f, cult.CurrentEssenceSea - 25f);
                    GameEventManager.TriggerPrimevalSeaChanged(cult.CurrentEssenceSea, cult.AptitudePercent);
                    DeductGuSatiety(15f);
                    break;

                case 3:
                    cult.CurrentEssenceSea = Math.Max(0f, cult.CurrentEssenceSea - 50f);
                    GameEventManager.TriggerPrimevalSeaChanged(cult.CurrentEssenceSea, cult.AptitudePercent);
                    DeductGuSatiety(35f);
                    break;

                case 4:
                    cult.CurrentEssenceSea = Math.Max(0f, cult.CurrentEssenceSea - 75f);
                    cult.ApertureWallDurability = Math.Max(10f, cult.ApertureWallDurability - (cult.MaxWallDurability * 0.30f));
                    GameEventManager.TriggerPrimevalSeaChanged(cult.CurrentEssenceSea, cult.AptitudePercent);
                    GameEventManager.TriggerApertureWallStateChanged(cult.WallWashingProgress, cult.ApertureWallDurability, cult.MaxWallDurability, cult.GetRankDescription());
                    DeductGuSatiety(60f);
                    break;

                case 5:
                default:
                    cult.CurrentEssenceSea = 0f;
                    cult.CurrentHealth = Math.Max(10, (int)(cult.CurrentHealth - cult.MaxHealth * 0.50f));
                    cult.ApertureWallDurability = Math.Max(5f, cult.ApertureWallDurability - (cult.MaxWallDurability * 0.60f));
                    GameEventManager.TriggerPrimevalSeaChanged(0f, cult.AptitudePercent);
                    GameEventManager.TriggerApertureWallStateChanged(cult.WallWashingProgress, cult.ApertureWallDurability, cult.MaxWallDurability, cult.GetRankDescription());
                    DeductGuSatiety(85f);
                    Debug.LogError("[InterMountainTransitService] [CATASTROPHIC BACKLASH] Rank 5 Mortal Ceiling crushed by Regional Wall! -50% HP, -60% Durability, 0% Sea.");
                    break;
            }
        }

        private void DeductGuSatiety(float amount)
        {
            if (_context?.GuInventory?.EquippedGu == null) return;
            foreach (var gu in _context.GuInventory.EquippedGu)
            {
                gu.Satiety = Math.Max(0f, gu.Satiety - amount);
                GameEventManager.TriggerGuFed(gu.GuID, false, gu.Satiety);
            }
        }

        private void SaveCurrentSectorMapState()
        {
            if (_context?.WorldMap != null)
            {
                string key = GetCurrentSectorKey();
                _context.WorldMap.SaveCurrentSectorSnapshot(key);
                Debug.Log($"[InterMountainTransitService] Saved sector map snapshot for '{key}'.");
            }
        }

        private void ExecuteMountainLoad(SouthernMountainID destination)
        {
            _currentMountain = destination;
            if (!_mountainDatabase.TryGetValue(destination, out var targetData)) return;

            string destKey = GetCurrentSectorKey();
            if (_context?.WorldMap != null)
            {
                bool loaded = _context.WorldMap.TryLoadSectorSnapshot(destKey);
                if (!loaded)
                {
                    _context.WorldMap.Seed = targetData.RandomSeed;
                    _context.WorldMap.CurrentSectorKey = destKey;
                    _context.WorldMap.GridWidth = targetData.SectorWidth;
                    _context.WorldMap.GridHeight = targetData.SectorHeight;
                    _context.WorldMap.PlayerGridPos = new GridCoord(targetData.SectorWidth / 2, targetData.SectorHeight / 2);
                    _context.WorldMap.InitializeDefaultGrid();
                }
            }

            GameEventManager.TriggerPlayerMoved(new GridCoord(targetData.SectorWidth / 2, targetData.SectorHeight / 2));
            GameEventManager.TriggerMountainChanged(destination, targetData.MountainName);
            GameEventManager.TriggerCultivatorsWandered();

            Debug.Log($"[InterMountainTransitService] Arrived at mountain '{targetData.MountainName}'. Welcome to the new sector!");
        }

        private void ExecuteRegionLoad(WorldRegionID destination)
        {
            _currentRegion = destination;
            if (!_regionalDatabase.TryGetValue(destination, out var regData)) return;

            string destKey = GetCurrentSectorKey();
            if (_context?.WorldMap != null)
            {
                _context.WorldMap.CurrentRegion = destination;
                bool loaded = _context.WorldMap.TryLoadSectorSnapshot(destKey);
                if (!loaded)
                {
                    _context.WorldMap.Seed = regData.RandomSeed;
                    _context.WorldMap.CurrentSectorKey = destKey;
                    _context.WorldMap.InitializeDefaultGrid();
                }
            }

            GameEventManager.TriggerRegionChanged(destination, regData.RegionName);
            Debug.Log($"[InterMountainTransitService] Crossed regional boundary into '{regData.RegionName}'.");
        }

        private void HandleMountainTransitRequested(SouthernMountainID mountain) => TravelToMountain(mountain);
        private void HandleRegionTransitRequested(WorldRegionID reg, bool seal) => TravelToRegion(reg, seal);

        private void HandleOpenTransitModalRequested(bool regionalMode)
        {
            _isTransitModalOpen = true;
            _isRegionalMode = regionalMode;
            GameEventManager.TriggerUIModalActive(true);
            GameEventManager.TriggerTransitModalToggled(true, regionalMode);
        }

        private void HandleCloseTransitModalRequested()
        {
            _isTransitModalOpen = false;
            GameEventManager.TriggerUIModalActive(false);
            GameEventManager.TriggerTransitModalToggled(false, _isRegionalMode);
        }

        private void HandleCombatConcluded(bool won, GridCoord pos, string enemy, ArenaEncounterType encounter)
        {
            // If road ambush concludes, complete deferred arrival
            if (_pendingMountainDestination.HasValue)
            {
                var dest = _pendingMountainDestination.Value;
                _pendingMountainDestination = null;
                Debug.Log($"[InterMountainTransitService] Ambush combat concluded (Victory: {won}). Resuming deferred transit to {dest}.");
                ExecuteMountainLoad(dest);
            }
        }
    }
}
