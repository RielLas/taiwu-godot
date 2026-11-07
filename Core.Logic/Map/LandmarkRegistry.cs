using System;
using System.Collections.Generic;
using TaiwuGu.Core;

namespace TaiwuGu.Map
{
    public enum LandmarkCategory
    {
        Interactive = 0,    // Interactive POI (caravan barter, primeval spring, beast tracking, etc.)
        Scenic = 1,         // Atmospheric non-interactive landmark (ancient pine, shattered stele); inspect flavor only
        Hazard = 2,         // Passive terrain hazard (poison spores, craggy rockfall); rolls damage when stepped
        Obstacle = 3        // Impassable terrain obstacle (boulder precipice, deep chasm); unwalkable in pathfinder
    }

    public record LandmarkActionDefinition(
        string ActionId,
        string Label,
        string Description,
        int APCost = 0,
        int PrimevalStoneCost = 0
    );

    public record LandmarkDefinition
    {
        public string Id { get; init; } = "";
        public TilePOIType LegacyPoi { get; init; } = TilePOIType.None;
        public string Name { get; init; } = "";
        public string SealText { get; init; } = "";
        public LandmarkCategory Category { get; init; } = LandmarkCategory.Interactive;
        public string LoreDescription { get; init; } = "";
        public bool IsTraversable { get; init; } = true;
        public int MovementAPCost { get; init; } = 2;
        public string TextureResourcePath { get; init; } = "";
        public string BadgeColorHex { get; init; } = "#FFFFFF";
        public IReadOnlyList<LandmarkActionDefinition> AvailableActions { get; init; } = Array.Empty<LandmarkActionDefinition>();

        // Functional Recall: Invoked when player triggers an action button
        public Action<GameStateContext, string, Vector2Int>? ExecuteAction { get; init; }

        // Functional Recall: Invoked when player clicks directly to interact with landmark
        public Action<GameStateContext, Vector2Int>? DirectInteraction { get; init; }
    }

    /// <summary>
    /// Pure C# Domain Functional Registry for all overworld Landmarks.
    /// Provides data-driven recall for interactions, scenic lore, and pathfinding constraints.
    /// Eliminates hardcoded switch cases in UI views and domain services.
    /// </summary>
    public static class LandmarkRegistry
    {
        private static readonly Dictionary<string, LandmarkDefinition> s_DefinitionsById = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<TilePOIType, LandmarkDefinition> s_DefinitionsByPOI = new();

        static LandmarkRegistry()
        {
            InitializeDefaultRegistry();
        }

        public static void Register(LandmarkDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            s_DefinitionsById[def.Id] = def;
            if (def.LegacyPoi != TilePOIType.None)
            {
                s_DefinitionsByPOI[def.LegacyPoi] = def;
            }
        }

        public static LandmarkDefinition? Get(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            s_DefinitionsById.TryGetValue(id, out var def);
            return def;
        }

        public static LandmarkDefinition? GetByPOI(TilePOIType poi)
        {
            if (poi == TilePOIType.None) return null;
            s_DefinitionsByPOI.TryGetValue(poi, out var def);
            return def;
        }

        public static LandmarkDefinition? Resolve(TileNode tile)
        {
            if (!string.IsNullOrEmpty(tile.CustomLandmarkId) && s_DefinitionsById.TryGetValue(tile.CustomLandmarkId, out var customDef))
            {
                return customDef;
            }
            return GetByPOI(tile.POI);
        }

        public static bool IsInteractive(TilePOIType poi, string? customId = null)
        {
            var def = Get(customId) ?? GetByPOI(poi);
            return def != null && def.Category == LandmarkCategory.Interactive;
        }

        public static bool IsScenic(TilePOIType poi, string? customId = null)
        {
            var def = Get(customId) ?? GetByPOI(poi);
            return def != null && def.Category == LandmarkCategory.Scenic;
        }

        public static bool IsTraversable(TilePOIType poi, string? customId = null)
        {
            var def = Get(customId) ?? GetByPOI(poi);
            if (def != null) return def.IsTraversable;
            return poi != TilePOIType.BoulderPrecipice;
        }

        public static IReadOnlyCollection<LandmarkDefinition> GetAll() => s_DefinitionsById.Values;

        private static void InitializeDefaultRegistry()
        {
            s_DefinitionsById.Clear();
            s_DefinitionsByPOI.Clear();

            // 1. Natural Primeval Essence Spring (天然元泉)
            Register(new LandmarkDefinition
            {
                Id = "spirit_vein",
                LegacyPoi = TilePOIType.SpiritVein,
                Name = "Natural Primeval Spring (天然元泉)",
                SealText = "泉",
                Category = LandmarkCategory.Interactive,
                LoreDescription = "A subterranean vein where pure primeval essence wells up from the bedrock. Ambient spiritual energy condenses into iridescent morning mist.",
                IsTraversable = true,
                MovementAPCost = 2,
                TextureResourcePath = "res://Resources/Landmarks/poi_primeval_spring.png",
                BadgeColorHex = "#38BDF8", // Cyan
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("spirit_vein_absorb", "Absorb Primeval Essence (+15% Sea Capacity)", "Meditate to draw bedrock primeval essence into the aperture sea.", 1, 0)
                },
                ExecuteAction = (context, actionId, pos) =>
                {
                    if (actionId == "spirit_vein_absorb")
                    {
                        float currentSea = context.Cultivator.CurrentEssenceSea;
                        float capacity = context.Cultivator.AptitudePercent;
                        context.Cultivator.CurrentEssenceSea = MathF.Min(capacity, currentSea + 15.0f);
                        GameEventManager.TriggerPrimevalSeaChanged(context.Cultivator.CurrentEssenceSea, capacity);
                    }
                },
                DirectInteraction = (context, pos) =>
                {
                    GameEventManager.TriggerPOIArrived(TilePOIType.SpiritVein, pos, "Natural Primeval Spring (天然元泉)", "A subterranean vein where pure primeval essence wells up from the bedrock.", 1);
                }
            });

            // 2. Jia Family Caravan Merchant Inn (贾家商队驿站)
            Register(new LandmarkDefinition
            {
                Id = "way_station",
                LegacyPoi = TilePOIType.WayStation,
                Name = "Jia Family Caravan Inn (贾家商队驿站)",
                SealText = "商",
                Category = LandmarkCategory.Interactive,
                LoreDescription = "A bustling seasonal caravan encampment operated by the Jia Family merchants. Foreign mortal delicacies, spirit teas, and vital Gu rations are traded freely.",
                IsTraversable = true,
                MovementAPCost = 2,
                TextureResourcePath = "res://Resources/Landmarks/poi_merchant_inn.png",
                BadgeColorHex = "#FBBF24", // Brass Amber
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("waystation_buy_orchid", "Buy Moon Orchid Petals (10 Stones)", "Purchase fresh moon orchid petals for Moonlight Gu sustenance.", 0, 10),
                    new LandmarkActionDefinition("waystation_buy_boar", "Buy Fresh Boar Meat (10 Stones)", "Purchase raw wild boar meat for Boar Gu sustenance.", 0, 10),
                    new LandmarkActionDefinition("waystation_regional_transit", "Caravan Regional Wall Crossing (商队界壁跨域)", "Hire the caravan to traverse regional boundary walls.", 0, 50)
                },
                ExecuteAction = (context, actionId, pos) =>
                {
                    switch (actionId)
                    {
                        case "waystation_buy_orchid":
                            if (context.Cultivator.PrimevalStones >= 10)
                            {
                                context.Cultivator.PrimevalStones -= 10;
                                context.GuInventory.AddFood(GuDietType.MoonOrchidPetals, 5);
                                GameEventManager.TriggerPrimevalStonesChanged(context.Cultivator.PrimevalStones);
                            }
                            break;
                        case "waystation_buy_boar":
                            if (context.Cultivator.PrimevalStones >= 10)
                            {
                                context.Cultivator.PrimevalStones -= 10;
                                context.GuInventory.AddFood(GuDietType.FreshBoarFlesh, 5);
                                GameEventManager.TriggerPrimevalStonesChanged(context.Cultivator.PrimevalStones);
                            }
                            break;
                        case "waystation_regional_transit":
                            GameEventManager.TriggerPOIModalToggled(false);
                            GameEventManager.RaiseRequestOpenTransitModal(true);
                            break;
                    }
                },
                DirectInteraction = (context, pos) =>
                {
                    GameEventManager.TriggerPOIArrived(TilePOIType.WayStation, pos, "Jia Family Caravan Inn (贾家商队驿站)", "A bustling seasonal caravan encampment operated by the Jia Family merchants.", 1);
                }
            });

            // 3. Gu Yue Clan Stronghold (古月山寨)
            Register(new LandmarkDefinition
            {
                Id = "clan_post",
                LegacyPoi = TilePOIType.ClanPost,
                Name = "Gu Yue Clan Stronghold (古月山寨)",
                SealText = "寨",
                Category = LandmarkCategory.Interactive,
                LoreDescription = "The ancestral stronghold of the Gu Yue clan. Verdant bamboo pavilions terrace up the steep mountain face, protected by clan elders and spirit watchtowers.",
                IsTraversable = true,
                MovementAPCost = 1,
                TextureResourcePath = "res://Resources/Landmarks/poi_clan_village.png",
                BadgeColorHex = "#34D399", // Emerald Viridian
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("clan_post_rest", "Rest at Clan Pavilion (+5 AP Restored)", "Rest peacefully within the clan boundaries to recuperate action stamina.", 0, 0)
                },
                ExecuteAction = (context, actionId, pos) =>
                {
                    if (actionId == "clan_post_rest")
                    {
                        int currentAP = context.Calendar.ActionPointsRemaining;
                        int budget = context.Calendar.MonthlyAPBudget;
                        context.Calendar.ActionPointsRemaining = Math.Min(budget, currentAP + 5);
                        GameEventManager.TriggerActionPointsChanged(context.Calendar.ActionPointsRemaining, budget);
                    }
                },
                DirectInteraction = (context, pos) =>
                {
                    GameEventManager.TriggerPOIArrived(TilePOIType.ClanPost, pos, "Gu Yue Clan Stronghold (古月山寨)", "The ancestral stronghold of the Gu Yue clan.", 1);
                }
            });

            // 4. Savage Beast Lair (凶兽巢穴)
            Register(new LandmarkDefinition
            {
                Id = "wild_beast_den",
                LegacyPoi = TilePOIType.WildBeastDen,
                Name = "Savage Beast Lair (凶兽巢穴)",
                SealText = "兽",
                Category = LandmarkCategory.Interactive,
                LoreDescription = "A shadowed hollow strewn with splintered bones and wild boar footprints. Low territorial growls reverberate through the damp air.",
                IsTraversable = true,
                MovementAPCost = 2,
                TextureResourcePath = "res://Resources/Landmarks/tile_bamboo_mountain.png",
                BadgeColorHex = "#F87171", // Crimson
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("beast_den_subjugate", "Track Wild Beasts in Lair (Combat Arena)", "Track ferocious beasts lurking inside the lair to harvest strength Gu or food.", 2, 0)
                },
                ExecuteAction = (context, actionId, pos) =>
                {
                    if (actionId == "beast_den_subjugate")
                    {
                        GameEventManager.TriggerPOIModalToggled(false);
                        var boarWild = WildOrganismData.CreateWildOrganism(
                            Random.Shared.NextDouble() < 0.5 ? "gu_white_boar_r1" : "gu_black_boar_r1",
                            WildOrganismTemperament.Ferocious
                        );
                        GameEventManager.RaiseRequestOpenWildGuEncounter(boarWild);
                    }
                },
                DirectInteraction = (context, pos) =>
                {
                    GameEventManager.TriggerPOIArrived(TilePOIType.WildBeastDen, pos, "Savage Beast Lair (凶兽巢穴)", "A shadowed hollow strewn with splintered bones and wild boar footprints.", 2);
                }
            });

            // 5. Ancient Cultivator Ruins (上古蛊师遗迹)
            Register(new LandmarkDefinition
            {
                Id = "ancient_gu_cave",
                LegacyPoi = TilePOIType.AncientGuCave,
                Name = "Ancient Cultivator Ruins (上古蛊师遗迹)",
                SealText = "洞",
                Category = LandmarkCategory.Interactive,
                LoreDescription = "Weathered megaliths inscribed with pre-antiquity dao formations. A subterranean cave descent hums with sealed alchemical resonance.",
                IsTraversable = true,
                MovementAPCost = 2,
                TextureResourcePath = "res://Resources/Tokens/landmark_stone_stele.png",
                BadgeColorHex = "#C084FC", // Purple Daoist
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("cave_subjugate", "Explore Subterranean Ruins (Demonic Cultivator Trial)", "Descend into the sealed underground cavern in search of ancient legacy Gu.", 2, 0)
                },
                ExecuteAction = (context, actionId, pos) =>
                {
                    if (actionId == "cave_subjugate")
                    {
                        GameEventManager.TriggerPOIModalToggled(false);
                        string[] cavePool = new[] { "gu_liquor_worm_r1", "gu_little_light_r1", "gu_bronze_skin_r1", "gu_white_jade_r1" };
                        string pickedCaveGu = cavePool[Random.Shared.Next(0, cavePool.Length)];
                        var caveWild = WildOrganismData.CreateWildOrganism(pickedCaveGu, WildOrganismTemperament.Cunning);
                        GameEventManager.RaiseRequestOpenWildGuEncounter(caveWild);
                    }
                },
                DirectInteraction = (context, pos) =>
                {
                    GameEventManager.TriggerPOIArrived(TilePOIType.AncientGuCave, pos, "Ancient Cultivator Ruins (上古蛊师遗迹)", "Weathered megaliths inscribed with pre-antiquity dao formations.", 2);
                }
            });

            // 6. Mountain Pass Trailhead (青矛山道隘口)
            Register(new LandmarkDefinition
            {
                Id = "mountain_pass",
                LegacyPoi = TilePOIType.MountainPass,
                Name = "Mountain Pass Trailhead (青矛山道隘口)",
                SealText = "隘",
                Category = LandmarkCategory.Interactive,
                LoreDescription = "A narrow wind-swept gap cutting through Qing Mao Mountain's outer ridge. Natural boundary trail connecting to the broader Southern Border.",
                IsTraversable = true,
                MovementAPCost = 2,
                TextureResourcePath = "res://Resources/Landmarks/tile_bamboo_mountain.png",
                BadgeColorHex = "#94A3B8", // Slate
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("mountain_sector_transit", "Qing Mao Mountain Transit Station (十里凉亭行驿)", "Cross mountain sectors between Gu Yue, Bai Clan, and Xiong Ridge.", 1, 0)
                },
                ExecuteAction = (context, actionId, pos) =>
                {
                    if (actionId == "mountain_sector_transit")
                    {
                        GameEventManager.TriggerPOIModalToggled(false);
                        GameEventManager.RaiseRequestOpenTransitModal(false);
                    }
                },
                DirectInteraction = (context, pos) =>
                {
                    GameEventManager.TriggerPOIModalToggled(false);
                    GameEventManager.RaiseRequestOpenTransitModal(false);
                }
            });

            // 7. Scenic Non-Interactive: Thousand-Year Withered Pine (千年古松)
            Register(new LandmarkDefinition
            {
                Id = "ancient_pine",
                LegacyPoi = TilePOIType.AncientPine,
                Name = "Thousand-Year Withered Pine (千年古松)",
                SealText = "松",
                Category = LandmarkCategory.Scenic,
                LoreDescription = "An ancient gnarled pine clinging stubbornly to karst rock. Its jade needles whisper in the mountain wind, witnessing centuries of mortal ambition.",
                IsTraversable = true,
                MovementAPCost = 2,
                TextureResourcePath = "res://Resources/Landmarks/tile_bamboo_mountain.png",
                BadgeColorHex = "#16A34A", // Pine Green
                DirectInteraction = (context, pos) =>
                {
                    GameEventManager.TriggerTileInspected("Thousand-Year Withered Pine (千年古松)", "松", "An ancient gnarled pine clinging stubbornly to karst rock. Its jade needles whisper in the mountain wind.", pos);
                }
            });

            // 8. Scenic Non-Interactive: Shattered Daoist Stele (残破仙碑)
            Register(new LandmarkDefinition
            {
                Id = "shattered_stele",
                LegacyPoi = TilePOIType.ShatteredStele,
                Name = "Shattered Daoist Stele (残破仙碑)",
                SealText = "碑",
                Category = LandmarkCategory.Scenic,
                LoreDescription = "A weathered megalithic stele fractured by an ancient Gu battle. Pre-antiquity seal calligraphy remains faintly etched across its mossy surface.",
                IsTraversable = true,
                MovementAPCost = 2,
                TextureResourcePath = "res://Resources/Tokens/landmark_stone_stele.png",
                BadgeColorHex = "#78716C", // Stone Grey
                DirectInteraction = (context, pos) =>
                {
                    GameEventManager.TriggerTileInspected("Shattered Daoist Stele (残破仙碑)", "碑", "A weathered megalithic stele fractured by an ancient Gu battle. Pre-antiquity seal calligraphy remains faintly etched.", pos);
                }
            });

            // 9. Impassable Terrain Obstacle: Craggy Boulder Precipice (陡峭绝壁)
            Register(new LandmarkDefinition
            {
                Id = "boulder_precipice",
                LegacyPoi = TilePOIType.BoulderPrecipice,
                Name = "Craggy Boulder Precipice (陡峭绝壁)",
                SealText = "障",
                Category = LandmarkCategory.Obstacle,
                LoreDescription = "A sheer vertical cliff of jagged ironstone. Unclimbable by foot; mortal aperture cultivators must detour around its natural barrier.",
                IsTraversable = false,
                MovementAPCost = 999,
                TextureResourcePath = "res://Resources/Tokens/landmark_stone_stele.png",
                BadgeColorHex = "#475569" // Dark Slate
            });
        }
    }
}
