using System;
using System.Collections.Generic;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing Intra-Regional Mountain Sector Transit and Five Regions Boundary Wall crossings.
    /// </summary>
    public static class TransitSystem
    {
        public const int TRAVEL_STONE_COST = 25;
        public const int TRAVEL_AP_COST = 6;
        public const float ROAD_AMBUSH_CHANCE = 0.25f;

        public const int REGIONAL_TRANSIT_STONE_COST = 60;
        public const int REGIONAL_TRANSIT_AP_COST = 15;

        public static readonly IReadOnlyDictionary<SouthernMountainID, MountainSectorData> MountainDatabase =
            new Dictionary<SouthernMountainID, MountainSectorData>
            {
                [SouthernMountainID.GuYueVillage] = new MountainSectorData
                {
                    MountainID = SouthernMountainID.GuYueVillage,
                    MountainName = "Gu Yue Mountain (古月山寨)",
                    RandomSeed = 17881588,
                    SectorWidth = 30,
                    SectorHeight = 30,
                    Biome = BiomeType.BambooMountain,
                    Description = "A towering verdant mountain thick with bamboo groves and Moon Orchid caves. Governed by the Gu Yue Clan."
                },
                [SouthernMountainID.BaiClanMountain] = new MountainSectorData
                {
                    MountainID = SouthernMountainID.BaiClanMountain,
                    MountainName = "Bai Clan Mountain (白家山)",
                    RandomSeed = 28993412,
                    SectorWidth = 34,
                    SectorHeight = 24,
                    Biome = BiomeType.BambooMountain,
                    Description = "Steep limestone ridges enveloped in white mist. Home to the Bai Clan, renowned for bone-path inheritances and pure springs."
                },
                [SouthernMountainID.XiongRidge] = new MountainSectorData
                {
                    MountainID = SouthernMountainID.XiongRidge,
                    MountainName = "Xiong Ridge (熊家寨)",
                    RandomSeed = 39012845,
                    SectorWidth = 32,
                    SectorHeight = 32,
                    Biome = BiomeType.BambooMountain,
                    Description = "Dense forested peaks populated by black bears and savage swine. Governed by the ferocious Xiong Clan."
                }
            };

        public static readonly IReadOnlyDictionary<WorldRegionID, RegionalSectorData> RegionalDatabase =
            new Dictionary<WorldRegionID, RegionalSectorData>
            {
                [WorldRegionID.SouthernBorder] = new RegionalSectorData
                {
                    RegionID = WorldRegionID.SouthernBorder,
                    RegionName = "Southern Border (南疆)",
                    RandomSeed = 17881588,
                    PrimaryBiome = BiomeType.BambooMountain,
                    WallType = RegionalWallType.MiasmaWall,
                    LoreDescription = "Endless karst mountain ranges, primeval bamboo forests, deep secluded ravines, and poisonous earth miasma."
                },
                [WorldRegionID.NorthernPlains] = new RegionalSectorData
                {
                    RegionID = WorldRegionID.NorthernPlains,
                    RegionName = "Northern Plains (北原)",
                    RandomSeed = 55129031,
                    PrimaryBiome = BiomeType.GrasslandPlateau,
                    WallType = RegionalWallType.LicoriceWall,
                    LoreDescription = "Immense roaring grasslands and howling wind steppes. Ruled by the Huang Jin bloodline tribes and fierce blood-path warriors."
                },
                [WorldRegionID.WesternDesert] = new RegionalSectorData
                {
                    RegionID = WorldRegionID.WesternDesert,
                    RegionName = "Western Desert (西漠)",
                    RandomSeed = 71839204,
                    PrimaryBiome = BiomeType.DesertOasis,
                    WallType = RegionalWallType.FeatherWall,
                    LoreDescription = "Vast sea of scorching golden sand dunes, ghost illusions, and rich fire-path gemstone veins clustered around flourishing oases."
                },
                [WorldRegionID.EasternSea] = new RegionalSectorData
                {
                    RegionID = WorldRegionID.EasternSea,
                    RegionName = "Eastern Sea (东海)",
                    RandomSeed = 82940173,
                    PrimaryBiome = BiomeType.AzureArchipelago,
                    WallType = RegionalWallType.BlueWaterWall,
                    LoreDescription = "Countless celestial islands, deep ocean trenches, bubbling spirit springs, and flourishing maritime trade ports."
                },
                [WorldRegionID.CentralContinent] = new RegionalSectorData
                {
                    RegionID = WorldRegionID.CentralContinent,
                    RegionName = "Central Continent (中洲)",
                    RandomSeed = 99482015,
                    PrimaryBiome = BiomeType.SpiritLake,
                    WallType = RegionalWallType.SaintWall,
                    LoreDescription = "The supreme heartland of the mortal world, governed by the Ten Great Ancient Sects and overseen by the eternal Heavenly Court."
                }
            };

        public static TransitPendingComponent GetOrCreateSession(WorldStore world)
        {
            var s = world.Get<TransitPendingComponent>(world.PlayerEntity);
            if (s == null) s = world.Set(world.PlayerEntity, new TransitPendingComponent());
            return s;
        }

        public static string MakeSectorKey(WorldRegionID region, SouthernMountainID mountain)
            => $"Sector_{region}_{mountain}";

        public static void OpenTransitModal(WorldStore world, bool regionalMode)
        {
            var s = GetOrCreateSession(world);
            s.IsModalOpen = true;
            s.IsRegionalMode = regionalMode;
            WorldSignals.EmitUIModalActive(true);
            WorldSignals.EmitTransitModalToggled(true);
            WorldSignals.EmitTopics(DirtyTopic.Transit);
        }

        public static void CloseTransitModal(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            if (!s.IsModalOpen) return;
            s.IsModalOpen = false;
            WorldSignals.EmitUIModalActive(false);
            WorldSignals.EmitTransitModalToggled(false);
            WorldSignals.EmitTopics(DirtyTopic.Transit);
        }

        public static bool CanTravelToMountain(WorldStore world, SouthernMountainID target, out string reason)
        {
            reason = string.Empty;
            var currentSector = world.GetCurrentSector();
            if (currentSector != null && currentSector.MountainId == target && currentSector.Region == WorldRegionID.SouthernBorder)
            {
                reason = "Already at this mountain!";
                return false;
            }

            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult))
            {
                reason = "No active cultivator";
                return false;
            }

            if (cult.PrimevalStones < TRAVEL_STONE_COST)
            {
                reason = $"Need {TRAVEL_STONE_COST} Stones (Have {cult.PrimevalStones})";
                return false;
            }

            int ap = world.GetSingleton<WorldClockComponent>()?.ActionPointsRemaining ?? 0;
            if (ap < TRAVEL_AP_COST)
            {
                reason = $"Need {TRAVEL_AP_COST} Days/AP (Have {ap})";
                return false;
            }

            return true;
        }

        public static bool CanTravelToRegion(WorldStore world, WorldRegionID target, out string reason)
        {
            reason = string.Empty;
            var currentSector = world.GetCurrentSector();
            if (currentSector != null && currentSector.Region == target)
            {
                reason = "Already in this Region!";
                return false;
            }

            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult))
            {
                reason = "No active cultivator";
                return false;
            }

            if (cult.PrimevalStones < REGIONAL_TRANSIT_STONE_COST)
            {
                reason = $"Need {REGIONAL_TRANSIT_STONE_COST} Stones (Have {cult.PrimevalStones})";
                return false;
            }

            int ap = world.GetSingleton<WorldClockComponent>()?.ActionPointsRemaining ?? 0;
            if (ap < REGIONAL_TRANSIT_AP_COST)
            {
                reason = $"Need {REGIONAL_TRANSIT_AP_COST} Days/AP (Have {ap})";
                return false;
            }

            return true;
        }

        public static bool TravelToMountain(WorldStore world, SouthernMountainID destination, IRandomSource? rngOverride = null)
        {
            if (!CanTravelToMountain(world, destination, out string reason))
            {
                Debug.LogWarning($"[TransitSystem] Mountain transit blocked: {reason}");
                return false;
            }

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            cult.PrimevalStones -= TRAVEL_STONE_COST;
            TimeSystem.DeductActionPoints(world, TRAVEL_AP_COST);

            world.GetCurrentSector()?.CommitTilesToSnapshot();
            CloseTransitModal(world);

            IRandomSource rng = rngOverride ?? new SeededRandom(world.RunSeed + (int)destination);
            if (rng.NextDouble() < ROAD_AMBUSH_CHANCE)
            {
                var s = GetOrCreateSession(world);
                s.PendingMountainDestination = destination;
                WorldSignals.EmitRoadAmbushOccurred(ArenaEncounterType.WildBoarHunt, world.GetPlayerPosition());
                CombatSystem.StartCombat(
                    world,
                    ArenaEncounterType.WildBoarHunt,
                    world.GetPlayerPosition(),
                    isLethal: true,
                    enemyName: "Mountain Road Wild Boars");
                WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Calendar | DirtyTopic.Transit);
                return true;
            }

            LoadMountainSector(world, destination);
            return true;
        }

        public static bool TravelToRegion(WorldStore world, WorldRegionID destination, bool sealAperture)
        {
            if (!CanTravelToRegion(world, destination, out string reason))
            {
                Debug.LogWarning($"[TransitSystem] Regional transit blocked: {reason}");
                return false;
            }

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            cult.PrimevalStones -= REGIONAL_TRANSIT_STONE_COST;
            TimeSystem.DeductActionPoints(world, REGIONAL_TRANSIT_AP_COST);

            WorldRegionID fromRegion = world.GetCurrentSector()?.Region ?? WorldRegionID.SouthernBorder;
            RegionalWallType wall = RegionalWallLaw.GetBoundaryWall(fromRegion, destination);
            ApplyRegionalWallBacklash(world, wall, sealAperture);

            world.GetCurrentSector()?.CommitTilesToSnapshot();
            CloseTransitModal(world);
            LoadRegionSector(world, destination);
            return true;
        }

        public static void CompletePendingAmbushArrival(WorldStore world)
        {
            var s = GetOrCreateSession(world);
            if (!s.PendingMountainDestination.HasValue) return;
            var dest = s.PendingMountainDestination.Value;
            s.PendingMountainDestination = null;
            LoadMountainSector(world, dest);
        }

        public static void ApplyRegionalWallBacklash(WorldStore world, RegionalWallType wall, bool isApertureSealed)
        {
            if (!world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) ||
                !world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
                return;

            if (isApertureSealed)
            {
                ap.CurrentEssenceSea = 0f;
                ap.ApertureWallDurability = MathF.Max(5f, ap.ApertureWallDurability - (ap.MaxWallDurability * 0.05f));
                WorldSignals.EmitTopics(DirtyTopic.Aperture);
                return;
            }

            switch (ap.Rank)
            {
                case 1:
                    ap.CurrentEssenceSea = MathF.Max(0f, ap.CurrentEssenceSea - 10f);
                    break;
                case 2:
                    ap.CurrentEssenceSea = MathF.Max(0f, ap.CurrentEssenceSea - 25f);
                    DeductEquippedGuSatiety(world, 15f);
                    break;
                case 3:
                    ap.CurrentEssenceSea = MathF.Max(0f, ap.CurrentEssenceSea - 50f);
                    DeductEquippedGuSatiety(world, 35f);
                    break;
                case 4:
                    ap.CurrentEssenceSea = MathF.Max(0f, ap.CurrentEssenceSea - 75f);
                    ap.ApertureWallDurability = MathF.Max(10f, ap.ApertureWallDurability - (ap.MaxWallDurability * 0.30f));
                    DeductEquippedGuSatiety(world, 60f);
                    break;
                default:
                    ap.CurrentEssenceSea = 0f;
                    ap.CurrentHealth = MathF.Max(10f, ap.CurrentHealth - ap.MaxHealth * 0.50f);
                    ap.ApertureWallDurability = MathF.Max(5f, ap.ApertureWallDurability - (ap.MaxWallDurability * 0.60f));
                    DeductEquippedGuSatiety(world, 85f);
                    break;
            }

            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Inventory);
        }

        private static void DeductEquippedGuSatiety(WorldStore world, float amount)
        {
            foreach (var (eid, gu) in world.GetOwned<GuWormComponent>(world.PlayerEntity))
            {
                if (world.TryGet<OwnerComponent>(eid, out var owner) && owner.IsEquipped)
                {
                    gu.Satiety = MathF.Max(0f, gu.Satiety - amount);
                }
            }
        }

        public static void LoadSector(WorldStore world, string sectorKey)
        {
            var sector = world.GetSector(sectorKey);
            WorldAtlasRegistry.TryGet(sectorKey, out var bp);

            int width = bp?.Width ?? 30;
            int height = bp?.Height ?? 30;
            int seed = bp?.Seed ?? 17881588;
            var region = bp?.Region ?? WorldRegionID.SouthernBorder;
            var biome = bp?.Biome ?? BiomeType.BambooMountain;
            string displayName = bp?.DisplayName ?? sectorKey;

            if (sector == null)
            {
                EntityId secEntity = world.CreateEntity(sectorKey);
                sector = new SectorGridComponent
                {
                    SectorKey = sectorKey,
                    Seed = seed,
                    Region = region,
                    MountainId = bp?.MountainId ?? SouthernMountainID.GuYueVillage,
                    Biome = biome,
                    DisplayName = displayName,
                    GridWidth = width,
                    GridHeight = height
                };
                sector.HydrateTiles();
                world.Set(secEntity, sector);
            }
            else
            {
                sector.EnsureHydrated();
            }

            GridCoord spawn = new(width / 2, height / 2);
            if (sector.Tiles != null)
            {
                for (int i = 0; i < sector.Tiles.Length; i++)
                {
                    if (sector.Tiles[i].POI == TilePOIType.WayStation || sector.Tiles[i].POI == TilePOIType.ClanPost)
                    {
                        spawn = new GridCoord(sector.Tiles[i].GridPos.X, sector.Tiles[i].GridPos.Y);
                        break;
                    }
                }
            }

            world.CurrentSectorKey = sectorKey;
            world.SetPosition(world.PlayerEntity, spawn, sectorKey);
            WorldFactory.RevealVision(sector, spawn, ExplorationSystem.DEFAULT_SENSORY_RADIUS);

            WorldSignals.EmitTopics(DirtyTopic.All);
        }

        private static void LoadMountainSector(WorldStore world, SouthernMountainID destination)
        {
            string destKey = MakeSectorKey(WorldRegionID.SouthernBorder, destination);
            LoadSector(world, destKey);
        }

        private static void LoadRegionSector(WorldStore world, WorldRegionID destination)
        {
            var regionSectors = WorldAtlasRegistry.GetByRegion(destination);
            string destKey = regionSectors.Count > 0 ? regionSectors[0].SectorKey : MakeSectorKey(destination, SouthernMountainID.GuYueVillage);
            LoadSector(world, destKey);
        }
    }
}
