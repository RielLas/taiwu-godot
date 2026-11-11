using System;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Psychology;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Seeds a canonical fresh Qing Mao Mountain ECS world (Fang Yuan, starter Gu, rations, sectors, 6 factions, living NPCs, and kinship graph).
    /// </summary>
    public static class WorldFactory
    {
        public const string DefaultSectorKey = "Sector_SouthernBorder_GuYueVillage";

        public static WorldStore CreateDefault(int runSeed = 1, GridCoord? playerStart = null)
        {
            var store = new WorldStore
            {
                RunSeed = runSeed,
                CurrentSectorKey = DefaultSectorKey
            };

            // 1. World Singleton Entity (Clock + Kinship/Bounty Ledger)
            EntityId worldId = store.CreateEntity("world_singleton");
            store.WorldEntityId = worldId.Value;
            store.Set(worldId, new WorldClockComponent());

            var kinship = new KinshipLedgerComponent();
            kinship.AddEdge("npc_enforcer_01", "npc_elder_yan", KinshipRelationshipType.Parent, 1.8f, 1.5f);
            kinship.AddEdge("npc_rogue_01", "npc_rogue_brother", KinshipRelationshipType.SwornSibling, 1.2f, 1.0f);
            kinship.AddEdge("npc_chi_disciple", "npc_mo_elder", KinshipRelationshipType.Parent, 1.9f, 1.5f);
            kinship.AddEdge("npc_bai_scout", "npc_bai_elder", KinshipRelationshipType.Parent, 1.7f, 1.5f);
            kinship.AddEdge("npc_xiong_hunter", "npc_xiong_warrior", KinshipRelationshipType.SwornSibling, 1.6f, 1.2f);
            kinship.AddEdge("npc_xiong_warrior", "npc_xiong_hunter", KinshipRelationshipType.SwornSibling, 1.6f, 1.2f);
            store.Set(worldId, kinship);

            // 2. Starting Sector Map Entity
            EntityId sectorEntity = store.CreateEntity(DefaultSectorKey);
            var sector = new SectorGridComponent
            {
                SectorKey = DefaultSectorKey,
                Seed = 17881588,
                Region = WorldRegionID.SouthernBorder,
                MountainId = SouthernMountainID.GuYueVillage,
                DisplayName = "Gu Yue Mountain (古月山寨)",
                GridWidth = 30,
                GridHeight = 30
            };
            sector.HydrateTiles();
            store.Set(sectorEntity, sector);

            // 3. Player Cultivator Entity (Gu Yue Fang Yuan)
            GridCoord startPos = playerStart ?? new GridCoord(15, 15);
            EntityId playerId = store.CreateEntity("player_fang_yuan");
            store.PlayerId = playerId.Value;
            store.Set(playerId, new PlayerTagComponent());
            store.Set(playerId, new PositionComponent(startPos, DefaultSectorKey));
            store.Set(playerId, new CultivatorComponent
            {
                ExternalId = "player_fang_yuan",
                Name = "Gu Yue Fang Yuan",
                Clan = "Gu Yue Clan (古月氏)",
                Disposition = TaiwuDisposition.Egoist,
                Aptitude = AptitudeGrade.C_Grade,
                AptitudePercent = 44.0f,
                Age = 15,
                LifespanYears = 100,
                PrimevalStones = 250
            });
            store.Set(playerId, new ApertureComponent
            {
                Rank = 1,
                Substage = 1,
                CurrentEssenceSea = 44.0f,
                CurrentNurtureProgress = 0f,
                WallWashingProgress = 0f,
                MaxWallDurability = 100f,
                ApertureWallDurability = 100f,
                MaxHealth = 100f,
                CurrentHealth = 100f
            });
            store.Set(playerId, new DaoMarksComponent
            {
                Ledger = new DaoMarksLedger { HumanPathMarks = ApertureComponent.DeriveInnateHumanPathMarks(44.0f) }
            });

            var inv = new InventoryComponent();
            inv.AddFood(new InventoryItem("food_moon_orchid", "Moon Orchid Petals (月兰花瓣)", GuDietType.MoonOrchidPetals, 12, 3));
            inv.AddFood(new InventoryItem("food_boar_meat", "Fresh Boar Flesh (新鲜野猪肉)", GuDietType.FreshBoarFlesh, 8, 2));
            inv.AddFood(new InventoryItem("food_vintage_wine", "Green Bamboo Wine (青竹老酒)", GuDietType.VintageWine, 3, 10));
            store.Set(playerId, inv);

            // Starter Gu Entities owned by Player
            SpawnGuForOwner(store, playerId, new GuWormComponent
            {
                GuID = "gu_moonlight_01",
                Name = "Moonlight Gu (月光蛊)",
                Rank = 1,
                Slot = GuSlotCategory.Attack,
                Diet = GuDietType.MoonOrchidPetals,
                BaseEssenceCost = 10f,
                Satiety = 100f,
                IsVitalGu = true
            }, isEquipped: true, equipSlot: 0);

            SpawnGuForOwner(store, playerId, new GuWormComponent
            {
                GuID = "gu_white_boar_01",
                Name = "White Boar Gu (白豕蛊)",
                Rank = 1,
                Slot = GuSlotCategory.Support,
                Diet = GuDietType.FreshBoarFlesh,
                BaseEssenceCost = 15f,
                Satiety = 100f,
                IsVitalGu = false
            }, isEquipped: true, equipSlot: 1);

            // 4. Seed 6 Autonomous Faction Entities
            SeedFactions(store);

            // 5. Seed Initial Living NPC Cultivators
            SeedNPCs(store);

            // 6. Initialize starting vision fog of war
            RevealVision(sector, startPos, 3);
            sector.CommitTilesToSnapshot();

            return store;
        }

        public static EntityId SpawnGuForOwner(WorldStore store, EntityId ownerId, GuWormComponent gu, bool isEquipped, int equipSlot = -1)
        {
            EntityId guEntity = store.CreateEntity();
            store.Set(guEntity, gu);
            store.Set(guEntity, new OwnerComponent(ownerId.Value, isEquipped, equipSlot));
            return guEntity;
        }

        public static void RevealVision(SectorGridComponent sector, GridCoord center, int radius)
        {
            TileNode[] tiles = sector.EnsureHydrated();
            for (int i = 0; i < tiles.Length; i++) tiles[i].IsVisible = false;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) <= radius)
                    {
                        int tx = center.X + dx;
                        int ty = center.Y + dy;
                        if (tx >= 0 && tx < sector.GridWidth && ty >= 0 && ty < sector.GridHeight)
                        {
                            int idx = ty * sector.GridWidth + tx;
                            tiles[idx].IsVisible = true;
                            tiles[idx].IsExplored = true;
                        }
                    }
                }
            }
        }

        private static void SeedFactions(WorldStore store)
        {
            FactionComponent[] factions =
            {
                new(FactionID.GuYueClan, "Gu Yue Clan", "古月氏", "【古】", 100, "Sector_SouthernBorder_GuYueVillage", "Righteous bloodline clan commanding Qing Mao Mountain. Revering filial bloodlines and moon path heritage."),
                new(FactionID.BaiClan, "Bai Clan", "白家寨", "【白】", -10, "Sector_SouthernBorder_BaiClanMountain", "Ancient limestone karst clan famed for bone-path secret arts, secluded springs, and prideful descendants."),
                new(FactionID.XiongClan, "Xiong Clan", "熊家寨", "【熊】", -10, "Sector_SouthernBorder_XiongRidge", "Fierce mountain stronghold honoring strength-path brute force, bear hunting, and survival of the fittest."),
                new(FactionID.JiaConsortium, "Jia Consortium", "贾家商会", "【贾】", 25, "Sector_SouthernBorder_GuYueVillage", "Pan-regional merchant caravan syndicate. Neutral arbiters of commerce, food rations, and currency exchange."),
                new(FactionID.TieClan, "Tie Clan", "铁家神捕", "【铁】", 0, "Sector_SouthernBorder_GuYueVillage", "Iron-masked righteous enforcers who uphold absolute cosmic justice, hunt demonic fiends, and issue bounties."),
                new(FactionID.DemonicRogues, "Demonic Rogues", "散修魔修", "【魔】", -25, "Sector_SouthernBorder_GuYueVillage", "Lawless outlaws, demonic vagabonds, and contraband smugglers lurking in deep ravines and wilderness crevices.")
            };

            foreach (var f in factions)
            {
                EntityId fid = store.CreateEntity($"faction_{f.ID}");
                store.Set(fid, f);
            }
        }

        private static void SeedNPCs(WorldStore store)
        {
            void AddNpc(string extId, string name, string clan, int rank, int sub, TaiwuDisposition disp, int x, int y,
                int age, int lifespan, int stones, bool soulTablet, int favor, string trait = "Gangzheng")
            {
                EntityId id = store.CreateEntity(extId);
                store.Set(id, new PositionComponent(x, y, DefaultSectorKey));
                store.Set(id, new CultivatorComponent
                {
                    ExternalId = extId,
                    Name = name,
                    Clan = clan,
                    Disposition = disp,
                    Aptitude = AptitudeGrade.C_Grade,
                    AptitudePercent = 45f,
                    Age = age,
                    LifespanYears = lifespan,
                    PrimevalStones = stones,
                    SoulTabletBound = soulTablet,
                    Favorability = favor,
                    PersonalityTrait = trait
                });
                store.Set(id, new ApertureComponent
                {
                    Rank = rank,
                    Substage = sub,
                    CurrentEssenceSea = 45f,
                    MaxWallDurability = ApertureComponent.GetMaxWallDurabilityForRank(rank),
                    ApertureWallDurability = ApertureComponent.GetMaxWallDurabilityForRank(rank)
                });
                store.Set(id, new InventoryComponent());
            }

            AddNpc("npc_chi_disciple", "Gu Yue Chi Cheng (古月赤成)", "Gu Yue Clan (古月氏)", 1, 2, TaiwuDisposition.Rebellious, 15, 14, 16, 100, 85, true, 10);
            AddNpc("npc_mo_elder", "Gu Yue Mo Chen (古月漠尘)", "Gu Yue Clan (古月氏)", 3, 1, TaiwuDisposition.Pragmatic, 14, 15, 48, 120, 420, true, 0, "Huduan");
            AddNpc("npc_elder_yan", "Elder Gu Yue Yan (古月阎长老)", "Gu Yue Clan (古月氏)", 2, 2, TaiwuDisposition.Righteous, 15, 15, 42, 110, 260, true, 15, "Huduan");
            AddNpc("npc_enforcer_01", "Gu Yue Clan Enforcer (古月刑堂修士)", "Gu Yue Clan (古月氏)", 1, 4, TaiwuDisposition.Righteous, 15, 17, 24, 100, 110, true, 0);
            AddNpc("npc_bai_scout", "Bai Ning Bing (白凝冰)", "Bai Clan (白家寨)", 2, 3, TaiwuDisposition.Rebellious, 14, 16, 16, 100, 310, true, -20);
            AddNpc("npc_xiong_warrior", "Xiong Li (熊力)", "Xiong Clan (熊家寨)", 2, 2, TaiwuDisposition.Righteous, 14, 17, 22, 105, 190, true, -10);
            AddNpc("npc_rogue_demon", "Scarred Vulture (苍疤鹫)", "Demonic Rogues (散修魔修)", 2, 1, TaiwuDisposition.Egoist, 18, 14, 35, 95, 160, false, -50);
            AddNpc("npc_tie_detective", "Tie Ruo Nan (铁若男)", "Tie Clan (铁家)", 2, 1, TaiwuDisposition.Righteous, 13, 14, 17, 110, 240, true, 20);
            AddNpc("npc_jia_steward", "Jia Fu (贾富)", "Jia Consortium (贾家商会)", 2, 4, TaiwuDisposition.Benevolent, 13, 15, 38, 100, 950, false, 30);
        }
    }
}
