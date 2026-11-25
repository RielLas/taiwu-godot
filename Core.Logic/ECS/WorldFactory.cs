using System;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Psychology;
using TaiwuGu.Systems;

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
            return CreateFromConfig(new NewRunConfig
            {
                RunSeed = runSeed,
                PlayerName = "Gu Yue Fang Yuan",
                OriginClan = "Gu Yue Clan (古月氏)",
                StartingSectorKey = DefaultSectorKey,
                AptitudePercent = 44.0f,
                Disposition = TaiwuDisposition.Egoist,
                VitalGuId = "gu_moonlight_r1"
            }, playerStart);
        }

        public static WorldStore CreateFromConfig(NewRunConfig config, GridCoord? playerStart = null)
        {
            config ??= new NewRunConfig();
            SaveSystem.PlaytimeSeconds = 0.0;

            int runSeed = config.RunSeed;
            string requestedSectorKey = !string.IsNullOrWhiteSpace(config.StartingSectorKey) ? config.StartingSectorKey : DefaultSectorKey;
            var startBp = WorldAtlasRegistry.TryGet(requestedSectorKey, out var foundBp) && foundBp != null
                ? foundBp
                : WorldAtlasRegistry.Get(DefaultSectorKey);

            var store = new WorldStore
            {
                RunSeed = runSeed,
                CurrentSectorKey = startBp.SectorKey
            };

            // 1. World Singleton Entity (Clock + Kinship/Bounty Ledger)
            EntityId worldId = store.CreateEntity("world_singleton");
            store.WorldEntityId = worldId.Value;
            store.Set(worldId, new WorldClockComponent());

            var kinship = new KinshipLedgerComponent();
            kinship.AddEdge("npc_enforcer_01", "npc_elder_yan", KinshipRelationshipType.Parent, 1.8f, 1.5f);
            kinship.AddEdge("npc_rogue_demon", "npc_rogue_brother", KinshipRelationshipType.SwornSibling, 1.2f, 1.0f);
            kinship.AddEdge("npc_chi_disciple", "npc_mo_elder", KinshipRelationshipType.Parent, 1.9f, 1.5f);
            kinship.AddEdge("npc_bai_scout", "npc_bai_elder", KinshipRelationshipType.Parent, 1.7f, 1.5f);
            kinship.AddEdge("npc_xiong_hunter", "npc_xiong_warrior", KinshipRelationshipType.SwornSibling, 1.6f, 1.2f);
            kinship.AddEdge("npc_xiong_warrior", "npc_xiong_hunter", KinshipRelationshipType.SwornSibling, 1.6f, 1.2f);
            store.Set(worldId, kinship);

            // 2. Starting Sector Map Entity
            EntityId sectorEntity = store.CreateEntity(startBp.SectorKey);
            var sector = new SectorGridComponent
            {
                SectorKey = startBp.SectorKey,
                Seed = startBp.Seed,
                Region = startBp.Region,
                MountainId = startBp.MountainId ?? SouthernMountainID.GuYueVillage,
                Biome = startBp.Biome,
                DisplayName = startBp.DisplayName,
                GridWidth = startBp.Width,
                GridHeight = startBp.Height
            };
            sector.HydrateTiles();
            store.Set(sectorEntity, sector);

            // 3. Player Cultivator Entity
            GridCoord startPos = playerStart ?? new GridCoord(15, 15);
            EntityId playerId = store.CreateEntity("player_fang_yuan");
            store.PlayerId = playerId.Value;
            store.Set(playerId, new PlayerTagComponent());
            store.Set(playerId, new PositionComponent(startPos, startBp.SectorKey));

            AptitudeGrade grade = DeriveAptitudeGrade(config.AptitudePercent);
            float sea = Math.Clamp(config.AptitudePercent, 20f, 100f);

            store.Set(playerId, new CultivatorComponent
            {
                ExternalId = "player_fang_yuan",
                Name = string.IsNullOrWhiteSpace(config.PlayerName) ? "Gu Yue Fang Yuan" : config.PlayerName,
                Clan = string.IsNullOrWhiteSpace(config.OriginClan) ? "Gu Yue Clan (古月氏)" : config.OriginClan,
                Disposition = config.Disposition,
                Aptitude = grade,
                AptitudePercent = sea,
                Age = 15,
                LifespanYears = 100,
                PrimevalStones = 250
            });
            store.Set(playerId, new ApertureComponent
            {
                Rank = 1,
                Substage = 1,
                CurrentEssenceSea = sea,
                CurrentNurtureProgress = 0f,
                WallWashingProgress = 0f,
                MaxWallDurability = 100f,
                ApertureWallDurability = 100f,
                MaxHealth = 100f,
                CurrentHealth = 100f
            });
            store.Set(playerId, new DaoMarksComponent
            {
                Ledger = new DaoMarksLedger { HumanPathMarks = ApertureComponent.DeriveInnateHumanPathMarks(sea) }
            });

            var inv = new InventoryComponent();
            inv.AddFood(new InventoryItem("food_moon_orchid", "Moon Orchid Petals (月兰花瓣)", GuDietType.MoonOrchidPetals, 12, 3));
            inv.AddFood(new InventoryItem("food_boar_meat", "Fresh Boar Flesh (新鲜野猪肉)", GuDietType.FreshBoarFlesh, 8, 2));
            inv.AddFood(new InventoryItem("food_vintage_wine", "Green Bamboo Wine (青竹老酒)", GuDietType.VintageWine, 3, 10));
            store.Set(playerId, inv);

            // Starter Gu Entities owned by Player
            string vitalId = !string.IsNullOrWhiteSpace(config.VitalGuId) ? config.VitalGuId : "gu_moonlight_r1";
            GuWormComponent? vitalGu = null;
            if (GuDatabase.TryGet(vitalId, out var def) && def != null)
            {
                vitalGu = GuDatabase.CreateGuWormComponent(vitalId, isWild: false);
            }
            vitalGu ??= new GuWormComponent
            {
                GuID = vitalId,
                Name = vitalId.Contains("boar", StringComparison.OrdinalIgnoreCase) ? "White Boar Gu (白豕蛊)" : "Moonlight Gu (月光蛊)",
                Rank = 1,
                Slot = vitalId.Contains("boar", StringComparison.OrdinalIgnoreCase) ? GuSlotCategory.Support : GuSlotCategory.Attack,
                Diet = vitalId.Contains("boar", StringComparison.OrdinalIgnoreCase) ? GuDietType.FreshBoarFlesh : GuDietType.MoonOrchidPetals,
                BaseEssenceCost = 10f,
                Satiety = 100f
            };
            vitalGu.IsVitalGu = true;
            SpawnGuForOwner(store, playerId, vitalGu, isEquipped: true, equipSlot: 0);

            if (!vitalId.Contains("boar", StringComparison.OrdinalIgnoreCase))
            {
                var secondaryBoar = GuDatabase.CreateGuWormComponent("gu_white_boar_r1", isWild: false) ?? new GuWormComponent
                {
                    GuID = "gu_white_boar_r1",
                    Name = "White Boar Gu (白豕蛊)",
                    Rank = 1,
                    Slot = GuSlotCategory.Support,
                    Diet = GuDietType.FreshBoarFlesh,
                    BaseEssenceCost = 15f,
                    Satiety = 100f,
                    IsVitalGu = false
                };
                secondaryBoar.IsVitalGu = false;
                SpawnGuForOwner(store, playerId, secondaryBoar, isEquipped: true, equipSlot: 1);
            }
            else
            {
                var secondaryMoon = GuDatabase.CreateGuWormComponent("gu_moonlight_r1", isWild: false) ?? new GuWormComponent
                {
                    GuID = "gu_moonlight_r1",
                    Name = "Moonlight Gu (月光蛊)",
                    Rank = 1,
                    Slot = GuSlotCategory.Attack,
                    Diet = GuDietType.MoonOrchidPetals,
                    BaseEssenceCost = 10f,
                    Satiety = 100f,
                    IsVitalGu = false
                };
                secondaryMoon.IsVitalGu = false;
                SpawnGuForOwner(store, playerId, secondaryMoon, isEquipped: true, equipSlot: 1);
            }

            // 4. Seed 6 Autonomous Faction Entities
            SeedFactions(store);

            // 5. Seed Initial Living NPC Cultivators
            SeedNPCs(store);

            // 6. Initialize starting vision fog of war
            RevealVision(sector, startPos, 3);
            sector.CommitTilesToSnapshot();

            return store;
        }

        public static AptitudeGrade DeriveAptitudeGrade(float aptitudePercent)
        {
            if (aptitudePercent >= 80f) return AptitudeGrade.A_Grade;
            if (aptitudePercent >= 60f) return AptitudeGrade.B_Grade;
            if (aptitudePercent >= 40f) return AptitudeGrade.C_Grade;
            return AptitudeGrade.D_Grade;
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
            foreach (var bp in WorldAtlasRegistry.GetAll())
            {
                foreach (var npc in bp.InitialResidents)
                {
                    EntityId id = store.CreateEntity(npc.ExternalId);
                    store.Set(id, new PositionComponent(npc.X, npc.Y, bp.SectorKey));
                    store.Set(id, new CultivatorComponent
                    {
                        ExternalId = npc.ExternalId,
                        Name = npc.Name,
                        Clan = npc.Clan,
                        HomeSectorKey = bp.SectorKey,
                        Disposition = npc.Disposition,
                        Aptitude = AptitudeGrade.C_Grade,
                        AptitudePercent = 45f,
                        Age = npc.Age,
                        LifespanYears = npc.LifespanYears,
                        PrimevalStones = npc.PrimevalStones,
                        SoulTabletBound = npc.SoulTabletBound,
                        Favorability = npc.Favorability,
                        PersonalityTrait = npc.PersonalityTrait
                    });
                    store.Set(id, new ApertureComponent
                    {
                        Rank = npc.Rank,
                        Substage = npc.Substage,
                        CurrentEssenceSea = 45f,
                        MaxWallDurability = ApertureComponent.GetMaxWallDurabilityForRank(npc.Rank),
                        ApertureWallDurability = ApertureComponent.GetMaxWallDurabilityForRank(npc.Rank)
                    });
                    store.Set(id, new InventoryComponent());
                }
            }
        }
    }
}
