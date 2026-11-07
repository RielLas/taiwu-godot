using System;
using TaiwuGu.Core;

namespace TaiwuGu.Map
{
    public enum SouthernMountainID
    {
        GuYueVillage = 0,       // 古月山寨 (Gu Yue Clan home base)
        BaiClanMountain = 1,    // 白家山 (Limestone karst & springs)
        XiongRidge = 2          // 熊家寨 (Forested bear highlands)
    }

    [Serializable]
    public class MountainSectorData
    {
        public SouthernMountainID MountainID;
        public string MountainName = "";
        public int RandomSeed;
        public int SectorWidth = 30;
        public int SectorHeight = 30;
        public BiomeType Biome;
        public string Description = "";
    }

    [Serializable]
    public class RegionalSectorData
    {
        public WorldRegionID RegionID;
        public string RegionName = "";
        public int RandomSeed;
        public BiomeType PrimaryBiome;
        public RegionalWallType WallType;
        public string LoreDescription = "";
    }

    public enum WorldRegionID
    {
        SouthernBorder = 0,     // 南疆: Endless mountains, karst bamboo forests, miasma
        NorthernPlains = 1,     // 北原: Endless grasslands, fierce gales, blood & wind path
        WesternDesert = 2,      // 西漠: Vast sea of sand, ghost oases, fire & earth path
        EasternSea = 3,         // 东海: Azure archipelago, underwater trenches, water & cloud path
        CentralContinent = 4    // 中洲: Grand fertile heartland, 10 ancient sects, supreme suppression
    }

    public enum RegionalWallType
    {
        MiasmaWall = 0,         // 南疆紫瘴界壁: Corrosive poison mist, eats aperture sea
        LicoriceWall = 1,       // 北原甘草界壁: Green sticky fibrous mist, entangles and slows
        FeatherWall = 2,        // 西漠狂羽界壁: Blistering wind feathers, abrasively cuts membrane
        BlueWaterWall = 3,      // 东海青水界壁: Heavy crushing abyssal currents
        SaintWall = 4           // 中洲圣光界壁: Transcendent holy light, violent Dao repulsion
    }

    public enum BiomeType
    {
        BambooMountain,
        SpiritLake,
        DesertOasis,
        GrasslandPlateau,
        AzureArchipelago
    }

    public enum TilePOIType
    {
        None = 0,
        WayStation = 1,        // Jia Family Caravan Merchant Inn
        SpiritVein = 2,        // Natural Primeval Essence Spring
        WildBeastDen = 3,      // Savage Beast Lair
        AncientGuCave = 4,     // Ancient Cultivator Ruins
        ClanPost = 5,          // Gu Yue Clan Stronghold
        MountainPass = 6,      // Natural Trailhead / Perimeter Crossing to Regional Highway
        AncientPine = 7,       // Scenic Non-Interactive: Thousand-Year Withered Pine (千年古松)
        ShatteredStele = 8,    // Scenic Non-Interactive: Shattered Daoist Stele (残破仙碑)
        BoulderPrecipice = 9   // Impassable Obstacle: Craggy Boulder Precipice (陡峭绝壁)
    }

    [Serializable]
    public struct TileNode
    {
        public Vector2Int GridPos;
        public BiomeType Biome;
        public TilePOIType POI;
        public string? CustomLandmarkId;
        public int DangerRank;
        public bool IsExplored;
        public bool IsVisible;
        public bool IsActive; // True for solid traversable terrain; False for void/chasm
    }

    /// <summary>
    /// Canon Reverend Insanity Regional Wall Backlash Model.
    /// The higher the mortal rank, the more lethal the heaven-and-earth repulsion.
    /// Mortals pass freely; Rank 4-5 face fatal aperture wall rupture, essence combustion, and Gu comas.
    /// </summary>
    public static class RegionalWallLaw
    {
        public static string GetRegionName(WorldRegionID region) => region switch
        {
            WorldRegionID.SouthernBorder   => "Southern Border (南疆)",
            WorldRegionID.NorthernPlains   => "Northern Plains (北原)",
            WorldRegionID.WesternDesert    => "Western Desert (西漠)",
            WorldRegionID.EasternSea       => "Eastern Sea (东海)",
            WorldRegionID.CentralContinent => "Central Continent (中洲)",
            _ => "Southern Border"
        };

        public static BiomeType GetRegionBiome(WorldRegionID region) => region switch
        {
            WorldRegionID.SouthernBorder   => BiomeType.BambooMountain,
            WorldRegionID.NorthernPlains   => BiomeType.GrasslandPlateau,
            WorldRegionID.WesternDesert    => BiomeType.DesertOasis,
            WorldRegionID.EasternSea       => BiomeType.AzureArchipelago,
            WorldRegionID.CentralContinent => BiomeType.SpiritLake,
            _                              => BiomeType.BambooMountain
        };

        public static RegionalWallType GetBoundaryWall(WorldRegionID from, WorldRegionID to)
        {
            return to switch
            {
                WorldRegionID.SouthernBorder   => RegionalWallType.MiasmaWall,
                WorldRegionID.NorthernPlains   => RegionalWallType.LicoriceWall,
                WorldRegionID.WesternDesert    => RegionalWallType.FeatherWall,
                WorldRegionID.EasternSea       => RegionalWallType.BlueWaterWall,
                WorldRegionID.CentralContinent => RegionalWallType.SaintWall,
                _ => RegionalWallType.MiasmaWall
            };
        }

        public static string GetWallName(RegionalWallType wall) => wall switch
        {
            RegionalWallType.MiasmaWall    => "Miasma Regional Wall (紫瘴界壁)",
            RegionalWallType.LicoriceWall  => "Licorice Regional Wall (甘草界壁)",
            RegionalWallType.FeatherWall   => "Feather Regional Wall (狂羽界壁)",
            RegionalWallType.BlueWaterWall => "Blue Water Regional Wall (青水界壁)",
            RegionalWallType.SaintWall     => "Saint Regional Wall (圣光界壁)",
            _ => "Regional Wall"
        };
    }
}
