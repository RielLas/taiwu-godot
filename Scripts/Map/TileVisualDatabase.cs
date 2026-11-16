using System;
using System.Collections.Generic;
using Godot;
using TaiwuGu.Map;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Pure data container for landmark and biome visuals, matching Unity TileVisualDatabase.cs.
    /// Provides textures, badge colors, footprint dimensions, and pivots for all TilePOITypes without hardcoded shortcuts.
    /// </summary>
    [GlobalClass]
    public partial class TileVisualDatabase : Resource
    {
        public class POIVisualEntry
        {
            public TilePOIType POIType { get; set; }
            public Texture2D? DioramaTexture { get; set; }
            public Color BadgeColor { get; set; } = Colors.White;
            public Color Tint { get; set; } = Colors.White;
            public float TargetFootprintWidth { get; set; } = 4.8f;
            public float ContentWidth { get; set; } = 800f;
            public Vector2 FootprintPivot { get; set; } = new Vector2(0.5f, 0.55f);
        }

        private readonly Dictionary<TilePOIType, POIVisualEntry> _registry = new();

        public Texture2D? SelectionDiamondSprite { get; set; }
        public Color SelectionGlowColor { get; set; } = new Color(0.984f, 0.749f, 0.141f, 1f); // Golden Amber #FBBF24

        public void RegisterEntry(POIVisualEntry entry)
        {
            _registry[entry.POIType] = entry;
        }

        public POIVisualEntry? GetEntry(TilePOIType poi)
        {
            _registry.TryGetValue(poi, out var entry);
            return entry;
        }

        public Texture2D? GetTexture(TilePOIType poi)
        {
            return _registry.TryGetValue(poi, out var entry) ? entry.DioramaTexture : null;
        }

        public Color GetBadgeColor(TilePOIType poi, Color defaultColor)
        {
            if (_registry.TryGetValue(poi, out var entry) && entry.BadgeColor.A > 0.01f)
            {
                return entry.BadgeColor;
            }
            return defaultColor;
        }

        /// <summary>
        /// Creates the canonical default visual database populated with all 6 Southern Border landmark visual assets.
        /// </summary>
        public static TileVisualDatabase CreateDefaultDatabase()
        {
            var db = new TileVisualDatabase();

            // 1. Clan Stronghold (Gu Yue Clan Village)
            var clanVillageTex = ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/poi_clan_village.png");
            db.RegisterEntry(new POIVisualEntry
            {
                POIType = TilePOIType.ClanPost,
                DioramaTexture = clanVillageTex,
                BadgeColor = new Color(0.20f, 0.78f, 0.55f, 1f), // Viridian Green
                Tint = Colors.White,
                TargetFootprintWidth = 5.0f,
                ContentWidth = 929f,
                FootprintPivot = new Vector2(0.5034f, 0.5438f)
            });

            // 2. Jia Family Caravan Merchant Inn
            var merchantInnTex = ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/poi_merchant_inn.png");
            db.RegisterEntry(new POIVisualEntry
            {
                POIType = TilePOIType.WayStation,
                DioramaTexture = merchantInnTex,
                BadgeColor = new Color(0.98f, 0.75f, 0.14f, 1f), // Brass Amber
                Tint = Colors.White,
                TargetFootprintWidth = 4.8f,
                ContentWidth = 765f,
                FootprintPivot = new Vector2(0.5039f, 0.6038f)
            });

            // 3. Natural Primeval Essence Spring
            var springTex = ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/poi_primeval_spring.png");
            db.RegisterEntry(new POIVisualEntry
            {
                POIType = TilePOIType.SpiritVein,
                DioramaTexture = springTex,
                BadgeColor = new Color(0.22f, 0.74f, 0.97f, 1f), // Spring Azure
                Tint = Colors.White,
                TargetFootprintWidth = 4.8f,
                ContentWidth = 822f,
                FootprintPivot = new Vector2(0.5039f, 0.5528f)
            });

            // 4. Ancient Cultivator Ruins / Ancient Gu Cave
            var bambooTex = ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/tile_bamboo_mountain.png");
            db.RegisterEntry(new POIVisualEntry
            {
                POIType = TilePOIType.AncientGuCave,
                DioramaTexture = bambooTex,
                BadgeColor = new Color(0.66f, 0.44f, 0.88f, 1f), // Mystic Daoist Violet
                Tint = new Color(0.85f, 0.75f, 0.95f, 1f),
                TargetFootprintWidth = 4.8f,
                ContentWidth = 734f,
                FootprintPivot = new Vector2(0.4971f, 0.4919f)
            });

            // 5. Savage Beast Lair
            db.RegisterEntry(new POIVisualEntry
            {
                POIType = TilePOIType.WildBeastDen,
                DioramaTexture = bambooTex,
                BadgeColor = new Color(0.94f, 0.38f, 0.28f, 1f), // Primal Crimson
                Tint = new Color(0.92f, 0.84f, 0.80f, 1f),
                TargetFootprintWidth = 4.8f,
                ContentWidth = 734f,
                FootprintPivot = new Vector2(0.4971f, 0.4919f)
            });

            // 6. Mountain Pass Trailhead
            db.RegisterEntry(new POIVisualEntry
            {
                POIType = TilePOIType.MountainPass,
                DioramaTexture = bambooTex,
                BadgeColor = new Color(0.70f, 0.80f, 0.90f, 1f), // Misty Slate
                Tint = new Color(0.88f, 0.92f, 0.95f, 1f),
                TargetFootprintWidth = 4.8f,
                ContentWidth = 734f,
                FootprintPivot = new Vector2(0.4971f, 0.4919f)
            });

            // 7. Scenic: Thousand-Year Withered Pine (千年古松)
            db.RegisterEntry(new POIVisualEntry
            {
                POIType = TilePOIType.AncientPine,
                DioramaTexture = bambooTex,
                BadgeColor = new Color(0.09f, 0.64f, 0.29f, 1f), // Pine Jade
                Tint = new Color(0.85f, 0.95f, 0.88f, 1f),
                TargetFootprintWidth = 4.8f,
                ContentWidth = 734f,
                FootprintPivot = new Vector2(0.4971f, 0.4919f)
            });

            // 8. Scenic: Shattered Daoist Stele (残破仙碑)
            db.RegisterEntry(new POIVisualEntry
            {
                POIType = TilePOIType.ShatteredStele,
                DioramaTexture = bambooTex,
                BadgeColor = new Color(0.55f, 0.53f, 0.50f, 1f), // Ancient Stone Grey
                Tint = new Color(0.75f, 0.75f, 0.75f, 1f),
                TargetFootprintWidth = 4.0f,
                ContentWidth = 600f,
                FootprintPivot = new Vector2(0.5f, 0.5f)
            });

            // 9. Obstacle: Craggy Boulder Precipice (陡峭绝壁)
            db.RegisterEntry(new POIVisualEntry
            {
                POIType = TilePOIType.BoulderPrecipice,
                DioramaTexture = bambooTex,
                BadgeColor = new Color(0.28f, 0.33f, 0.41f, 1f), // Precipice Slate
                Tint = new Color(0.55f, 0.58f, 0.65f, 1f),
                TargetFootprintWidth = 4.2f,
                ContentWidth = 600f,
                FootprintPivot = new Vector2(0.5f, 0.5f)
            });

            return db;
        }
    }
}
