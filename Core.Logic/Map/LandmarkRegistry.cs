using System;
using System.Collections.Generic;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;

namespace TaiwuGu.Map
{
    public enum LandmarkCategory
    {
        Interactive = 0,
        Scenic = 1,
        Hazard = 2,
        Obstacle = 3
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

        public Action<WorldStore, string, GridCoord>? ExecuteAction { get; init; }
        public Action<WorldStore, GridCoord>? DirectInteraction { get; init; }
    }

    /// <summary>
    /// Pure C# Domain Functional Registry for all overworld Landmarks operating directly over WorldStore.
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

        private static bool TrySpendActionAP(WorldStore world, int apCost)
        {
            if (apCost <= 0) return true;
            var clock = world.GetSingleton<WorldClockComponent>();
            if (clock == null || clock.ActionPointsRemaining < apCost)
            {
                Debug.LogWarning($"[LandmarkRegistry] Insufficient Action Points (requires {apCost} AP).");
                return false;
            }
            TimeSystem.DeductActionPoints(world, apCost);
            return true;
        }

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
                BadgeColorHex = "#38BDF8",
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("spirit_vein_absorb", "Absorb Primeval Essence (+15% Sea Capacity)", "Meditate to draw bedrock primeval essence into the aperture sea.", 1, 0)
                },
                ExecuteAction = (world, actionId, pos) =>
                {
                    if (actionId == "spirit_vein_absorb" &&
                        world.TryGet<CultivatorComponent>(world.PlayerEntity, out var cult) &&
                        world.TryGet<ApertureComponent>(world.PlayerEntity, out var ap))
                    {
                        if (!TrySpendActionAP(world, 1)) return;
                        ap.CurrentEssenceSea = MathF.Min(cult.AptitudePercent, ap.CurrentEssenceSea + 15.0f);
                        WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Calendar);
                    }
                },
                DirectInteraction = (world, pos) => WorldSignals.EmitOpenTilePOI(pos)
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
                BadgeColorHex = "#FBBF24",
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("waystation_buy_orchid", "Buy Moon Orchid Petals (10 Stones)", "Purchase fresh moon orchid petals for Moonlight Gu sustenance.", 0, 10),
                    new LandmarkActionDefinition("waystation_buy_boar", "Buy Fresh Boar Meat (10 Stones)", "Purchase raw wild boar meat for Boar Gu sustenance.", 0, 10),
                    new LandmarkActionDefinition("waystation_regional_transit", "Caravan Regional Wall Crossing (商队界壁跨域)", "Hire the caravan to traverse regional boundary walls.", 0, TransitSystem.REGIONAL_TRANSIT_STONE_COST)
                },
                ExecuteAction = (world, actionId, pos) =>
                {
                    var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
                    var inv = world.Get<InventoryComponent>(world.PlayerEntity);
                    if (cult == null || inv == null) return;

                    switch (actionId)
                    {
                        case "waystation_buy_orchid":
                            if (cult.PrimevalStones >= 10)
                            {
                                cult.PrimevalStones -= 10;
                                inv.AddFood(GuDietType.MoonOrchidPetals, 5);
                                WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Inventory);
                            }
                            break;
                        case "waystation_buy_boar":
                            if (cult.PrimevalStones >= 10)
                            {
                                cult.PrimevalStones -= 10;
                                inv.AddFood(GuDietType.FreshBoarFlesh, 5);
                                WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Inventory);
                            }
                            break;
                        case "waystation_regional_transit":
                            WorldSignals.EmitCloseTilePOI();
                            TransitSystem.OpenTransitModal(world, regionalMode: true);
                            break;
                    }
                },
                DirectInteraction = (world, pos) => WorldSignals.EmitOpenTilePOI(pos)
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
                BadgeColorHex = "#34D399",
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("clan_post_rest", "Rest at Clan Pavilion (+5 AP, +35 HP Restored, 1/Term)", "Rest peacefully within the clan boundaries once per Solar Term to recuperate action stamina and heal physical injuries.", 0, 0)
                },
                ExecuteAction = (world, actionId, pos) =>
                {
                    if (actionId == "clan_post_rest")
                    {
                        var clock = world.GetSingleton<WorldClockComponent>();
                        if (clock != null)
                        {
                            if (clock.LastClanRestYear == clock.CurrentYear &&
                                clock.LastClanRestTerm == clock.CurrentSolarTermIndex)
                            {
                                Debug.LogWarning("[LandmarkRegistry] Already rested at Clan Pavilion this Solar Term.");
                                return;
                            }

                            clock.LastClanRestYear = clock.CurrentYear;
                            clock.LastClanRestTerm = clock.CurrentSolarTermIndex;
                            clock.ActionPointsRemaining = Math.Min(clock.MonthlyAPBudget, clock.ActionPointsRemaining + 5);
                        }

                        var aperture = world.Get<ApertureComponent>(world.PlayerEntity);
                        if (aperture != null)
                        {
                            aperture.CurrentHealth = Math.Min(aperture.MaxHealth, aperture.CurrentHealth + 35f);
                        }

                        WorldSignals.EmitTopics(DirtyTopic.Calendar | DirtyTopic.Aperture);
                    }
                },
                DirectInteraction = (world, pos) => WorldSignals.EmitOpenTilePOI(pos)
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
                BadgeColorHex = "#F87171",
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("beast_den_subjugate", "Track Wild Beasts in Lair (Combat Arena)", "Track ferocious beasts lurking inside the lair to harvest strength Gu or food.", 2, 0),
                    new LandmarkActionDefinition("beast_den_combat", "Hunt the Beast Pack (Real-Time Arena)", "Descend into the lair and fight the beast pack in real time.", 1, 0)
                },
                ExecuteAction = (world, actionId, pos) =>
                {
                    if (actionId == "beast_den_subjugate")
                    {
                        if (!TrySpendActionAP(world, 2)) return;
                        WorldSignals.EmitCloseTilePOI();
                        int denSeed = unchecked(world.RunSeed ^ (pos.X * 73856093) ^ (pos.Y * 19349663) ^ ((world.GetSingleton<WorldClockComponent>()?.CurrentSolarTermIndex ?? 1) * 83492791));
                        var denRng = new SeededRandom(denSeed);
                        var boarWild = WildOrganismData.CreateWildOrganism(
                            denRng.NextDouble() < 0.5 ? "gu_white_boar_r1" : "gu_black_boar_r1",
                            WildOrganismTemperament.Ferocious
                        );
                        WildEncounterSystem.OpenEncounter(world, boarWild);
                    }
                    else if (actionId == "beast_den_combat")
                    {
                        if (!TrySpendActionAP(world, 1)) return;
                        WorldSignals.EmitCloseTilePOI();
                        CombatSystem.StartCombat(
                            world,
                            ArenaEncounterType.WildBoarHunt,
                            pos,
                            isLethal: true,
                            enemyName: "Savage Beast Pack (凶兽群)");
                    }
                },
                DirectInteraction = (world, pos) => WorldSignals.EmitOpenTilePOI(pos)
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
                TextureResourcePath = "res://Resources/Landmarks/tile_bamboo_mountain.png",
                BadgeColorHex = "#C084FC",
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("cave_subjugate", "Explore Subterranean Ruins (Demonic Cultivator Trial)", "Descend into the sealed underground cavern in search of ancient legacy Gu.", 2, 0),
                    new LandmarkActionDefinition("cave_combat", "Confront the Cave Cultivator (Real-Time Arena)", "Face the sealed demonic cultivator in a lethal arena duel.", 1, 0)
                },
                ExecuteAction = (world, actionId, pos) =>
                {
                    if (actionId == "cave_subjugate")
                    {
                        if (!TrySpendActionAP(world, 2)) return;
                        WorldSignals.EmitCloseTilePOI();
                        int caveSeed = unchecked(world.RunSeed ^ (pos.X * 39712847) ^ (pos.Y * 61849201) ^ ((world.GetSingleton<WorldClockComponent>()?.CurrentSolarTermIndex ?? 1) * 48618773));
                        var caveRng = new SeededRandom(caveSeed);
                        string[] cavePool = { "gu_liquor_worm_r1", "gu_little_light_r1", "gu_bronze_skin_r1", "gu_white_jade_r1" };
                        string pickedCaveGu = cavePool[caveRng.Next(0, cavePool.Length)];
                        var caveWild = WildOrganismData.CreateWildOrganism(pickedCaveGu, WildOrganismTemperament.Cunning);
                        WildEncounterSystem.OpenEncounter(world, caveWild);
                    }
                    else if (actionId == "cave_combat")
                    {
                        if (!TrySpendActionAP(world, 1)) return;
                        WorldSignals.EmitCloseTilePOI();
                        CombatSystem.StartCombat(
                            world,
                            ArenaEncounterType.DemonicCultivator,
                            pos,
                            isLethal: true,
                            enemyName: "Ancient Cave Demonic Cultivator (洞窟魔修)");
                    }
                },
                DirectInteraction = (world, pos) => WorldSignals.EmitOpenTilePOI(pos)
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
                BadgeColorHex = "#94A3B8",
                AvailableActions = new[]
                {
                    new LandmarkActionDefinition("mountain_sector_transit", "Qing Mao Mountain Transit Station (十里凉亭行驿)", "Cross mountain sectors between Gu Yue, Bai Clan, and Xiong Ridge.", 1, 0)
                },
                ExecuteAction = (world, actionId, pos) =>
                {
                    if (actionId == "mountain_sector_transit")
                    {
                        if (!TrySpendActionAP(world, 1)) return;
                        WorldSignals.EmitCloseTilePOI();
                        TransitSystem.OpenTransitModal(world, regionalMode: false);
                    }
                },
                DirectInteraction = (world, pos) =>
                {
                    WorldSignals.EmitCloseTilePOI();
                    TransitSystem.OpenTransitModal(world, regionalMode: false);
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
                BadgeColorHex = "#16A34A",
                DirectInteraction = (world, pos) =>
                {
                    WorldSignals.EmitScenicToast("Thousand-Year Withered Pine (千年古松)", "松", "An ancient gnarled pine clinging stubbornly to karst rock. Its jade needles whisper in the mountain wind.", pos);
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
                TextureResourcePath = "res://Resources/Landmarks/tile_bamboo_mountain.png",
                BadgeColorHex = "#78716C",
                DirectInteraction = (world, pos) =>
                {
                    WorldSignals.EmitScenicToast("Shattered Daoist Stele (残破仙碑)", "碑", "A weathered megalithic stele fractured by an ancient Gu battle. Pre-antiquity seal calligraphy remains faintly etched.", pos);
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
                TextureResourcePath = "res://Resources/Landmarks/tile_bamboo_mountain.png",
                BadgeColorHex = "#475569"
            });
        }
    }
}
