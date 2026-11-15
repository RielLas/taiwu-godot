using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Psychology;

namespace TaiwuGu.Map
{
    public sealed record AnchorPOISpec(
        int X,
        int Y,
        TilePOIType POI,
        string? CustomLandmarkId = null
    );

    public sealed record NpcSeedSpec(
        string ExternalId,
        string Name,
        string Clan,
        FactionID? Faction,
        int Rank,
        int Substage,
        TaiwuDisposition Disposition,
        int X,
        int Y,
        int Age,
        int LifespanYears,
        int PrimevalStones,
        bool SoulTabletBound,
        int Favorability,
        string PersonalityTrait = "Gangzheng"
    );

    public sealed record SectorBlueprint
    {
        public required string SectorKey { get; init; }
        public required string DisplayName { get; init; }
        public required WorldRegionID Region { get; init; }
        public SouthernMountainID? MountainId { get; init; }
        public required BiomeType Biome { get; init; }
        public required int Seed { get; init; }
        public int Width { get; init; } = 30;
        public int Height { get; init; } = 30;
        public int DangerRankFloor { get; init; } = 1; // 1..5
        public FactionID? NativeFaction { get; init; }
        public IReadOnlyList<AnchorPOISpec> PrimaryPOIs { get; init; } = Array.Empty<AnchorPOISpec>();
        public IReadOnlyDictionary<TilePOIType, int> SecondaryPOIBudget { get; init; }
            = new Dictionary<TilePOIType, int>();
        public IReadOnlyList<string> NativeGuPaths { get; init; } = Array.Empty<string>();
        public int MaxWildGuRank { get; init; } = 1;
        public IReadOnlyList<string> LocalBeastVarieties { get; init; } = Array.Empty<string>();
        public IReadOnlyList<NpcSeedSpec> InitialResidents { get; init; } = Array.Empty<NpcSeedSpec>();
    }

    /// <summary>
    /// Authoritative, immutable registry for all 17 canonical sectors across the Five Regions.
    /// Pure domain POCO container without Godot engine dependencies.
    /// </summary>
    public static class WorldAtlasRegistry
    {
        private static readonly Dictionary<string, SectorBlueprint> _sectors = new(StringComparer.Ordinal);
        private static readonly Dictionary<WorldRegionID, List<SectorBlueprint>> _regionIndex = new();

        static WorldAtlasRegistry()
        {
            RegisterAllSectors();
        }

        public static SectorBlueprint Get(string sectorKey)
        {
            if (_sectors.TryGetValue(sectorKey, out var bp)) return bp;
            throw new KeyNotFoundException($"[WorldAtlasRegistry] Unknown sector key: '{sectorKey}'");
        }

        public static bool TryGet(string sectorKey, out SectorBlueprint? blueprint)
        {
            if (!string.IsNullOrEmpty(sectorKey) && _sectors.TryGetValue(sectorKey, out var bp))
            {
                blueprint = bp;
                return true;
            }
            blueprint = null;
            return false;
        }

        public static IReadOnlyList<SectorBlueprint> GetByRegion(WorldRegionID region)
        {
            return _regionIndex.TryGetValue(region, out var list) ? list : Array.Empty<SectorBlueprint>();
        }

        public static IReadOnlyCollection<SectorBlueprint> GetAll() => _sectors.Values;

        public static int Count => _sectors.Count;

        private static void Register(SectorBlueprint bp)
        {
            _sectors[bp.SectorKey] = bp;
            if (!_regionIndex.TryGetValue(bp.Region, out var list))
            {
                list = new List<SectorBlueprint>();
                _regionIndex[bp.Region] = list;
            }
            list.Add(bp);
        }

        private static void RegisterAllSectors()
        {
            // =========================================================================
            // 1. Southern Border (南疆) — 4 Sectors
            // =========================================================================
            Register(new SectorBlueprint
            {
                SectorKey = "Sector_SouthernBorder_GuYueVillage",
                DisplayName = "Gu Yue Mountain (古月山寨)",
                Region = WorldRegionID.SouthernBorder,
                MountainId = SouthernMountainID.GuYueVillage,
                Biome = BiomeType.BambooMountain,
                Seed = 17881588,
                Width = 30,
                Height = 30,
                DangerRankFloor = 1,
                NativeFaction = FactionID.GuYueClan,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(15, 15, TilePOIType.ClanPost),
                    new AnchorPOISpec(19, 15, TilePOIType.WayStation),
                    new AnchorPOISpec(14, 17, TilePOIType.SpiritVein),
                    new AnchorPOISpec(10, 12, TilePOIType.SpiritVein),
                    new AnchorPOISpec(21, 20, TilePOIType.SpiritVein),
                    new AnchorPOISpec(6, 22, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(23, 8, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(5, 7, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(18, 11, TilePOIType.WildBeastDen),
                    new AnchorPOISpec(9, 19, TilePOIType.WildBeastDen),
                    new AnchorPOISpec(22, 17, TilePOIType.WildBeastDen)
                },
                NativeGuPaths = new[] { "Moon", "Light", "Food" },
                MaxWildGuRank = 1,
                LocalBeastVarieties = new[] { "WildBoar", "MountainWolf" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_chi_disciple", "Gu Yue Chi Cheng (古月赤成)", "Gu Yue Clan (古月氏)", FactionID.GuYueClan, 1, 2, TaiwuDisposition.Rebellious, 15, 14, 16, 100, 85, true, 10),
                    new NpcSeedSpec("npc_mo_elder", "Gu Yue Mo Chen (古月漠尘)", "Gu Yue Clan (古月氏)", FactionID.GuYueClan, 3, 1, TaiwuDisposition.Pragmatic, 14, 15, 48, 120, 420, true, 0, "Huduan"),
                    new NpcSeedSpec("npc_elder_yan", "Elder Gu Yue Yan (古月阎长老)", "Gu Yue Clan (古月氏)", FactionID.GuYueClan, 2, 2, TaiwuDisposition.Righteous, 15, 15, 42, 110, 260, true, 15, "Huduan"),
                    new NpcSeedSpec("npc_enforcer_01", "Gu Yue Clan Enforcer (古月刑堂修士)", "Gu Yue Clan (古月氏)", FactionID.GuYueClan, 1, 4, TaiwuDisposition.Righteous, 15, 17, 24, 100, 110, true, 0),
                    new NpcSeedSpec("npc_tie_detective", "Tie Ruo Nan (铁若男)", "Tie Clan (铁家)", FactionID.TieClan, 2, 1, TaiwuDisposition.Righteous, 13, 14, 17, 110, 240, true, 20),
                    new NpcSeedSpec("npc_jia_steward", "Jia Fu (贾富)", "Jia Consortium (贾家商会)", FactionID.JiaConsortium, 2, 4, TaiwuDisposition.Benevolent, 13, 15, 38, 100, 950, false, 30)
                }
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_SouthernBorder_BaiClanMountain",
                DisplayName = "Bai Clan Mountain (白家山)",
                Region = WorldRegionID.SouthernBorder,
                MountainId = SouthernMountainID.BaiClanMountain,
                Biome = BiomeType.BambooMountain,
                Seed = 28993412,
                Width = 34,
                Height = 24,
                DangerRankFloor = 2,
                NativeFaction = FactionID.BaiClan,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(17, 12, TilePOIType.ClanPost),
                    new AnchorPOISpec(15, 12, TilePOIType.WayStation),
                    new AnchorPOISpec(12, 10, TilePOIType.SpiritVein),
                    new AnchorPOISpec(22, 15, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(8, 18, TilePOIType.WildBeastDen)
                },
                NativeGuPaths = new[] { "Bone", "Water", "Qi" },
                MaxWildGuRank = 2,
                LocalBeastVarieties = new[] { "LimestoneWhiteSnake", "MountainApe" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_bai_scout", "Bai Ning Bing (白凝冰)", "Bai Clan (白家寨)", FactionID.BaiClan, 2, 3, TaiwuDisposition.Rebellious, 17, 13, 16, 100, 310, true, -20),
                    new NpcSeedSpec("npc_bai_elder", "Elder Bai Mo (白墨长老)", "Bai Clan (白家寨)", FactionID.BaiClan, 3, 2, TaiwuDisposition.Pragmatic, 17, 12, 52, 120, 480, true, -15)
                }
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_SouthernBorder_XiongRidge",
                DisplayName = "Xiong Ridge (熊家寨)",
                Region = WorldRegionID.SouthernBorder,
                MountainId = SouthernMountainID.XiongRidge,
                Biome = BiomeType.BambooMountain,
                Seed = 39012845,
                Width = 32,
                Height = 32,
                DangerRankFloor = 2,
                NativeFaction = FactionID.XiongClan,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(16, 16, TilePOIType.ClanPost),
                    new AnchorPOISpec(18, 16, TilePOIType.WayStation),
                    new AnchorPOISpec(12, 14, TilePOIType.WildBeastDen),
                    new AnchorPOISpec(20, 18, TilePOIType.SpiritVein),
                    new AnchorPOISpec(24, 22, TilePOIType.AncientGuCave)
                },
                NativeGuPaths = new[] { "Strength", "Earth", "Wood" },
                MaxWildGuRank = 2,
                LocalBeastVarieties = new[] { "BlackBear", "ArmoredBoar" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_xiong_warrior", "Xiong Li (熊力)", "Xiong Clan (熊家寨)", FactionID.XiongClan, 2, 2, TaiwuDisposition.Righteous, 16, 17, 22, 105, 190, true, -10),
                    new NpcSeedSpec("npc_xiong_hunter", "Xiong Ba (熊霸)", "Xiong Clan (熊家寨)", FactionID.XiongClan, 1, 4, TaiwuDisposition.Righteous, 15, 16, 25, 100, 140, true, -10)
                }
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_SouthernBorder_QingMaoGorge",
                DisplayName = "Qing Mao Gorge (青茅幽峡)",
                Region = WorldRegionID.SouthernBorder,
                Biome = BiomeType.BambooMountain,
                Seed = 44102938,
                Width = 30,
                Height = 30,
                DangerRankFloor = 3,
                NativeFaction = FactionID.DemonicRogues,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(15, 15, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(4, 15, TilePOIType.MountainPass),
                    new AnchorPOISpec(22, 12, TilePOIType.WildBeastDen),
                    new AnchorPOISpec(10, 20, TilePOIType.SpiritVein)
                },
                NativeGuPaths = new[] { "Moon", "Recon", "Transformation" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "GorgeBatSwarm", "ShadowPanther" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_rogue_demon", "Scarred Vulture (苍疤鹫)", "Demonic Rogues (散修魔修)", FactionID.DemonicRogues, 2, 1, TaiwuDisposition.Egoist, 15, 14, 35, 95, 160, false, -50),
                    new NpcSeedSpec("npc_rogue_brother", "Blood Falcon (血隼)", "Demonic Rogues (散修魔修)", FactionID.DemonicRogues, 2, 2, TaiwuDisposition.Egoist, 16, 15, 32, 90, 180, false, -45)
                }
            });

            // =========================================================================
            // 2. Northern Plains (北原) — 3 Sectors
            // =========================================================================
            Register(new SectorBlueprint
            {
                SectorKey = "Sector_NorthernPlains_RoyalCourtSteppe",
                DisplayName = "Royal Court Steppe (王庭大草原)",
                Region = WorldRegionID.NorthernPlains,
                Biome = BiomeType.GrasslandPlateau,
                Seed = 55129031,
                Width = 32,
                Height = 32,
                DangerRankFloor = 3,
                NativeFaction = FactionID.TieClan,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(16, 16, TilePOIType.ClanPost),
                    new AnchorPOISpec(20, 16, TilePOIType.WayStation),
                    new AnchorPOISpec(12, 12, TilePOIType.SpiritVein),
                    new AnchorPOISpec(24, 20, TilePOIType.AncientGuCave)
                },
                NativeGuPaths = new[] { "Metal", "Wind", "Strength" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "SteppeWolf", "GoldenEagle" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_plains_leader", "Ge Guang (葛光)", "Ge Tribe (葛家部族)", FactionID.TieClan, 3, 1, TaiwuDisposition.Righteous, 16, 15, 24, 110, 350, true, 10)
                }
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_NorthernPlains_CrescentLakeCamp",
                DisplayName = "Crescent Lake Camp (新月湖大营)",
                Region = WorldRegionID.NorthernPlains,
                Biome = BiomeType.SpiritLake,
                Seed = 56291042,
                Width = 30,
                Height = 30,
                DangerRankFloor = 3,
                NativeFaction = FactionID.JiaConsortium,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(15, 18, TilePOIType.WayStation),
                    new AnchorPOISpec(15, 8, TilePOIType.SpiritVein),
                    new AnchorPOISpec(22, 15, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(8, 22, TilePOIType.WildBeastDen)
                },
                NativeGuPaths = new[] { "Water", "Food", "Recon" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "LakeCroc", "CrescentHeron" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_plains_merchant", "Jia San (贾三)", "Jia Consortium (贾家商会)", FactionID.JiaConsortium, 2, 3, TaiwuDisposition.Pragmatic, 15, 17, 40, 100, 600, false, 20)
                }
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_NorthernPlains_GaleRavine",
                DisplayName = "Gale Ravine (罡风裂谷)",
                Region = WorldRegionID.NorthernPlains,
                Biome = BiomeType.GrasslandPlateau,
                Seed = 57401928,
                Width = 30,
                Height = 30,
                DangerRankFloor = 4,
                NativeFaction = FactionID.DemonicRogues,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(15, 15, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(20, 10, TilePOIType.WildBeastDen),
                    new AnchorPOISpec(5, 20, TilePOIType.MountainPass)
                },
                NativeGuPaths = new[] { "Wind", "Lightning", "Space" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "GaleWolfKing", "ThunderFalcon" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_wind_demon", "Wind Demon Dong Po (东坡风魔)", "Demonic Rogues (散修魔修)", FactionID.DemonicRogues, 3, 3, TaiwuDisposition.Rebellious, 15, 14, 45, 115, 520, false, -40)
                }
            });

            // =========================================================================
            // 3. Western Desert (西漠) — 3 Sectors
            // =========================================================================
            Register(new SectorBlueprint
            {
                SectorKey = "Sector_WesternDesert_MoFamilyOasis",
                DisplayName = "Mo Family Oasis (莫家绿洲)",
                Region = WorldRegionID.WesternDesert,
                Biome = BiomeType.DesertOasis,
                Seed = 71839204,
                Width = 32,
                Height = 32,
                DangerRankFloor = 3,
                NativeFaction = FactionID.JiaConsortium,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(16, 16, TilePOIType.WayStation),
                    new AnchorPOISpec(14, 18, TilePOIType.SpiritVein),
                    new AnchorPOISpec(18, 14, TilePOIType.ClanPost)
                },
                NativeGuPaths = new[] { "Earth", "Food", "Qi" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "DuneCamel", "SandScorpion" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_desert_steward", "Mo Yan (莫言)", "Mo Clan Oasis (莫家绿洲)", FactionID.JiaConsortium, 3, 2, TaiwuDisposition.Benevolent, 16, 15, 46, 120, 750, true, 25)
                }
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_WesternDesert_CrimsonDuneRuins",
                DisplayName = "Crimson Dune Ruins (赤沙遗迹)",
                Region = WorldRegionID.WesternDesert,
                Biome = BiomeType.DesertOasis,
                Seed = 72948102,
                Width = 30,
                Height = 30,
                DangerRankFloor = 4,
                NativeFaction = FactionID.DemonicRogues,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(15, 15, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(10, 12, TilePOIType.ShatteredStele),
                    new AnchorPOISpec(20, 18, TilePOIType.WildBeastDen)
                },
                NativeGuPaths = new[] { "Light", "Metal", "Space" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "DunePhantom", "CrimsonSandViper" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_desert_rogue", "Ghost Blade Sha (鬼刃沙)", "Demonic Rogues (散修魔修)", FactionID.DemonicRogues, 3, 4, TaiwuDisposition.Egoist, 15, 14, 39, 105, 480, false, -35)
                }
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_WesternDesert_ScorchedVeinBasin",
                DisplayName = "Scorched Vein Basin (焦脉天坑)",
                Region = WorldRegionID.WesternDesert,
                Biome = BiomeType.DesertOasis,
                Seed = 73059218,
                Width = 30,
                Height = 30,
                DangerRankFloor = 4,
                NativeFaction = null,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(15, 15, TilePOIType.SpiritVein),
                    new AnchorPOISpec(10, 20, TilePOIType.WildBeastDen),
                    new AnchorPOISpec(25, 10, TilePOIType.MountainPass)
                },
                NativeGuPaths = new[] { "Earth", "Light", "Transformation" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "FireLizardKing", "SunBeetle" },
                InitialResidents = Array.Empty<NpcSeedSpec>()
            });

            // =========================================================================
            // 4. Eastern Sea (东海) — 3 Sectors
            // =========================================================================
            Register(new SectorBlueprint
            {
                SectorKey = "Sector_EasternSea_RisingDragonPort",
                DisplayName = "Rising Dragon Port (起龙港)",
                Region = WorldRegionID.EasternSea,
                Biome = BiomeType.AzureArchipelago,
                Seed = 82940173,
                Width = 34,
                Height = 34,
                DangerRankFloor = 3,
                NativeFaction = FactionID.JiaConsortium,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(17, 17, TilePOIType.WayStation),
                    new AnchorPOISpec(20, 16, TilePOIType.ClanPost),
                    new AnchorPOISpec(14, 18, TilePOIType.SpiritVein)
                },
                NativeGuPaths = new[] { "Water", "Wind", "Food" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "SeaTurtle", "AzureGull" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_sea_merchant", "Song He (宋河)", "Eastern Sea Commerce (东海商盟)", FactionID.JiaConsortium, 3, 1, TaiwuDisposition.Pragmatic, 17, 16, 42, 115, 800, true, 20)
                }
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_EasternSea_CoralTrenchAtoll",
                DisplayName = "Coral Trench Atoll (珊瑚海沟环礁)",
                Region = WorldRegionID.EasternSea,
                Biome = BiomeType.AzureArchipelago,
                Seed = 83192041,
                Width = 32,
                Height = 32,
                DangerRankFloor = 4,
                NativeFaction = null,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(16, 16, TilePOIType.SpiritVein),
                    new AnchorPOISpec(22, 12, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(10, 20, TilePOIType.WildBeastDen)
                },
                NativeGuPaths = new[] { "Water", "Lightning", "Recon" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "ElectricEel", "CoralBeast" },
                InitialResidents = Array.Empty<NpcSeedSpec>()
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_EasternSea_BlueMistIsle",
                DisplayName = "Blue Mist Isle (青雾孤岛)",
                Region = WorldRegionID.EasternSea,
                Biome = BiomeType.AzureArchipelago,
                Seed = 84203915,
                Width = 30,
                Height = 30,
                DangerRankFloor = 4,
                NativeFaction = FactionID.DemonicRogues,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(15, 15, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(18, 12, TilePOIType.WildBeastDen),
                    new AnchorPOISpec(6, 22, TilePOIType.MountainPass)
                },
                NativeGuPaths = new[] { "Water", "Qi", "Space" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "MistKraken", "AbyssalShark" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_sea_hermit", "Old Man Blue Mist (青雾上人)", "Demonic Rogues (散修魔修)", FactionID.DemonicRogues, 3, 3, TaiwuDisposition.Rebellious, 15, 16, 68, 130, 600, false, -25)
                }
            });

            // =========================================================================
            // 5. Central Continent (中洲) — 4 Sectors
            // =========================================================================
            Register(new SectorBlueprint
            {
                SectorKey = "Sector_CentralContinent_SpiritAffinityPavilion",
                DisplayName = "Spirit Affinity Pavilion (灵缘斋山门)",
                Region = WorldRegionID.CentralContinent,
                Biome = BiomeType.SpiritLake,
                Seed = 99482015,
                Width = 32,
                Height = 32,
                DangerRankFloor = 4,
                NativeFaction = FactionID.TieClan,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(16, 22, TilePOIType.ClanPost),
                    new AnchorPOISpec(16, 10, TilePOIType.WayStation),
                    new AnchorPOISpec(22, 16, TilePOIType.SpiritVein)
                },
                NativeGuPaths = new[] { "Qi", "Light", "Moon" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "SpiritCrane", "JadeLion" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_central_elder", "Fairy Qing Yue (清月仙子)", "Spirit Affinity House (灵缘斋)", FactionID.TieClan, 3, 4, TaiwuDisposition.Righteous, 16, 21, 30, 130, 900, true, 20)
                }
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_CentralContinent_HeavenlyLotusShore",
                DisplayName = "Heavenly Lotus Shore (天莲水榭)",
                Region = WorldRegionID.CentralContinent,
                Biome = BiomeType.SpiritLake,
                Seed = 98571029,
                Width = 30,
                Height = 30,
                DangerRankFloor = 4,
                NativeFaction = null,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(15, 20, TilePOIType.SpiritVein),
                    new AnchorPOISpec(15, 8, TilePOIType.ShatteredStele),
                    new AnchorPOISpec(22, 15, TilePOIType.AncientGuCave)
                },
                NativeGuPaths = new[] { "Wood", "Water", "Qi" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "LotusCarp", "CelestialDeer" },
                InitialResidents = Array.Empty<NpcSeedSpec>()
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_CentralContinent_HuImmortalLand",
                DisplayName = "Hu Immortal Blessed Land (狐仙福地外围)",
                Region = WorldRegionID.CentralContinent,
                Biome = BiomeType.GrasslandPlateau,
                Seed = 97660142,
                Width = 32,
                Height = 32,
                DangerRankFloor = 5,
                NativeFaction = null,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(16, 16, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(20, 20, TilePOIType.SpiritVein),
                    new AnchorPOISpec(12, 12, TilePOIType.ShatteredStele)
                },
                NativeGuPaths = new[] { "Space", "Strength", "Transformation" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "FoxPack", "CelestialCrane" },
                InitialResidents = Array.Empty<NpcSeedSpec>()
            });

            Register(new SectorBlueprint
            {
                SectorKey = "Sector_CentralContinent_LuoPoValley",
                DisplayName = "Luo Po Valley (落魄谷)",
                Region = WorldRegionID.CentralContinent,
                Biome = BiomeType.BambooMountain,
                Seed = 96759281,
                Width = 30,
                Height = 30,
                DangerRankFloor = 5,
                NativeFaction = FactionID.DemonicRogues,
                PrimaryPOIs = new[]
                {
                    new AnchorPOISpec(15, 15, TilePOIType.AncientGuCave),
                    new AnchorPOISpec(18, 12, TilePOIType.BoulderPrecipice),
                    new AnchorPOISpec(12, 18, TilePOIType.WildBeastDen)
                },
                NativeGuPaths = new[] { "Qi", "Recon", "Bone" },
                MaxWildGuRank = 3,
                LocalBeastVarieties = new[] { "SoulVulture", "NetherHound" },
                InitialResidents = new[]
                {
                    new NpcSeedSpec("npc_luopo_warden", "Ghost King Yan (阎罗鬼王)", "Demonic Rogues (散修魔修)", FactionID.DemonicRogues, 3, 4, TaiwuDisposition.Egoist, 15, 14, 58, 140, 1100, false, -60)
                }
            });
        }
    }
}
