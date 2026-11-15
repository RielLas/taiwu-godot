using System;
using System.IO;
using System.Linq;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Serialization;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class WorldAtlasTests : IDisposable
    {
        private readonly string _originalRoot;
        private readonly string _testRoot;

        public WorldAtlasTests()
        {
            WorldSignals.ClearAllSubscriptions();
            _originalRoot = SavePaths.RootSaveDirectory;
            _testRoot = Path.Combine(Path.GetTempPath(), "taiwu_atlas_tests_" + Guid.NewGuid().ToString("N"));
            SavePaths.RootSaveDirectory = _testRoot;
        }

        public void Dispose()
        {
            SavePaths.RootSaveDirectory = _originalRoot;
            WorldSignals.ClearAllSubscriptions();
            try
            {
                if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, true);
            }
            catch { }
        }

        [Fact]
        public void WorldAtlasRegistry_RegistersAllSeventeenCanonicalSectors()
        {
            Assert.Equal(17, WorldAtlasRegistry.Count);

            var southern = WorldAtlasRegistry.GetByRegion(WorldRegionID.SouthernBorder);
            var northern = WorldAtlasRegistry.GetByRegion(WorldRegionID.NorthernPlains);
            var western = WorldAtlasRegistry.GetByRegion(WorldRegionID.WesternDesert);
            var eastern = WorldAtlasRegistry.GetByRegion(WorldRegionID.EasternSea);
            var central = WorldAtlasRegistry.GetByRegion(WorldRegionID.CentralContinent);

            Assert.Equal(4, southern.Count);
            Assert.Equal(3, northern.Count);
            Assert.Equal(3, western.Count);
            Assert.Equal(3, eastern.Count);
            Assert.Equal(4, central.Count);
        }

        [Fact]
        public void AllSeventeenSectors_GenerateDeterministically_AndWithoutDisconnectedTiles()
        {
            foreach (var bp in WorldAtlasRegistry.GetAll())
            {
                TileNode[] tiles1 = SectorGenerator.Generate(bp);
                TileNode[] tiles2 = SectorGenerator.Generate(bp);

                Assert.Equal(bp.Width * bp.Height, tiles1.Length);
                Assert.Equal(tiles1.Length, tiles2.Length);

                int activeCount = 0;
                for (int i = 0; i < tiles1.Length; i++)
                {
                    Assert.Equal(tiles1[i].IsActive, tiles2[i].IsActive);
                    Assert.Equal(tiles1[i].POI, tiles2[i].POI);
                    Assert.Equal(tiles1[i].DangerRank, tiles2[i].DangerRank);
                    Assert.Equal(tiles1[i].Biome, bp.Biome);
                    Assert.True(tiles1[i].DangerRank >= bp.DangerRankFloor);

                    if (tiles1[i].IsActive) activeCount++;
                    if (tiles1[i].POI != TilePOIType.None)
                    {
                        Assert.True(tiles1[i].IsActive, $"{bp.SectorKey}: POI {tiles1[i].POI} on inactive tile at index {i}.");
                    }
                }

                Assert.True(activeCount > 50, $"{bp.SectorKey} has insufficient active tiles: {activeCount}");

                // Ensure primary POIs from blueprint were placed
                foreach (var anchor in bp.PrimaryPOIs)
                {
                    int idx = anchor.Y * bp.Width + anchor.X;
                    Assert.True(tiles1[idx].IsActive, $"{bp.SectorKey}: Primary POI {anchor.POI} at ({anchor.X},{anchor.Y}) was culled!");
                    Assert.Equal(anchor.POI, tiles1[idx].POI);
                }
            }
        }

        [Fact]
        public void Biomes_GenerateDistinctTerrainTopologies()
        {
            var bamboo = WorldAtlasRegistry.Get("Sector_SouthernBorder_GuYueVillage");
            var steppe = WorldAtlasRegistry.Get("Sector_NorthernPlains_RoyalCourtSteppe");
            var oasis = WorldAtlasRegistry.Get("Sector_WesternDesert_MoFamilyOasis");
            var archipelago = WorldAtlasRegistry.Get("Sector_EasternSea_RisingDragonPort");
            var lake = WorldAtlasRegistry.Get("Sector_CentralContinent_SpiritAffinityPavilion");

            var bambooTiles = SectorGenerator.Generate(bamboo);
            var steppeTiles = SectorGenerator.Generate(steppe);
            var oasisTiles = SectorGenerator.Generate(oasis);
            var archipelagoTiles = SectorGenerator.Generate(archipelago);
            var lakeTiles = SectorGenerator.Generate(lake);

            int bambooActive = bambooTiles.Count(t => t.IsActive);
            int steppeActive = steppeTiles.Count(t => t.IsActive);
            int oasisActive = oasisTiles.Count(t => t.IsActive);
            int archipelagoActive = archipelagoTiles.Count(t => t.IsActive);
            int lakeActive = lakeTiles.Count(t => t.IsActive);

            // Archipelagos should have lower active density than vast steppes
            Assert.True(steppeActive > archipelagoActive, $"Steppe ({steppeActive}) should have more active land than Archipelago ({archipelagoActive})");

            // Spirit Lake center should be water void (inactive)
            int lakeCenterX = lake.Width / 2;
            int lakeCenterY = lake.Height / 2;
            Assert.False(lakeTiles[lakeCenterY * lake.Width + lakeCenterX].IsActive, "Spirit Lake center must be water void (inactive).");

            // Oasis center should be active fertile basin
            int oasisCenterX = oasis.Width / 2;
            int oasisCenterY = oasis.Height / 2;
            Assert.True(oasisTiles[oasisCenterY * oasis.Width + oasisCenterX].IsActive, "Desert Oasis center must be active fertile basin.");
        }

        [Fact]
        public void SpatialIndex_IsSectorScoped_IsolatingIdenticalCoordsAcrossSectors()
        {
            var world = new WorldStore();

            // Spawn two entities at identical coordinates (15, 15) in different sectors
            var e1 = world.CreateEntity("entity_guyue_15_15");
            world.Set(e1, new PositionComponent(15, 15, "Sector_SouthernBorder_GuYueVillage"));

            var e2 = world.CreateEntity("entity_baiclan_15_15");
            world.Set(e2, new PositionComponent(15, 15, "Sector_SouthernBorder_BaiClanMountain"));

            // Query Gu Yue sector
            var guyueEntities = world.GetEntitiesAt(new GridCoord(15, 15), "Sector_SouthernBorder_GuYueVillage").ToList();
            Assert.Contains(e1, guyueEntities);
            Assert.DoesNotContain(e2, guyueEntities);

            // Query Bai Clan sector
            var baiclanEntities = world.GetEntitiesAt(new GridCoord(15, 15), "Sector_SouthernBorder_BaiClanMountain").ToList();
            Assert.Contains(e2, baiclanEntities);
            Assert.DoesNotContain(e1, baiclanEntities);

            // Move e1 to (16, 16) in Gu Yue
            world.SetPosition(e1, new GridCoord(16, 16), "Sector_SouthernBorder_GuYueVillage");
            Assert.Empty(world.GetEntitiesAt(new GridCoord(15, 15), "Sector_SouthernBorder_GuYueVillage"));
            Assert.Single(world.GetEntitiesAt(new GridCoord(16, 16), "Sector_SouthernBorder_GuYueVillage"));
            Assert.Single(world.GetEntitiesAt(new GridCoord(15, 15), "Sector_SouthernBorder_BaiClanMountain"));
        }

        [Fact]
        public void WorldFactory_SeedsNPCsIntoRespectiveHomeSectors()
        {
            var world = WorldFactory.CreateDefault();

            // Verify Bai Ning Bing is seeded in Bai Clan Mountain, NOT Gu Yue Village
            var bnbId = world.FindByExternalId("npc_bai_scout");
            Assert.NotEqual(EntityId.None, bnbId);
            var bnbPos = world.Get<PositionComponent>(bnbId)!;
            var bnbCult = world.Get<CultivatorComponent>(bnbId)!;
            Assert.Equal("Sector_SouthernBorder_BaiClanMountain", bnbPos.SectorKey);
            Assert.Equal("Sector_SouthernBorder_BaiClanMountain", bnbCult.HomeSectorKey);

            // Verify Xiong Li is in Xiong Ridge
            var xiongId = world.FindByExternalId("npc_xiong_warrior");
            Assert.NotEqual(EntityId.None, xiongId);
            var xiongPos = world.Get<PositionComponent>(xiongId)!;
            Assert.Equal("Sector_SouthernBorder_XiongRidge", xiongPos.SectorKey);

            // Verify Gu Yue elders are in Gu Yue Village
            var moElderId = world.FindByExternalId("npc_mo_elder");
            Assert.NotEqual(EntityId.None, moElderId);
            var moPos = world.Get<PositionComponent>(moElderId)!;
            Assert.Equal("Sector_SouthernBorder_GuYueVillage", moPos.SectorKey);

            // Verify spatial query for Gu Yue Village does NOT return Bai Ning Bing
            var guyueNPCs = world.GetEntitiesAt(bnbPos.Coord, "Sector_SouthernBorder_GuYueVillage").ToList();
            Assert.DoesNotContain(bnbId, guyueNPCs);

            var baiclanNPCs = world.GetEntitiesAt(bnbPos.Coord, "Sector_SouthernBorder_BaiClanMountain").ToList();
            Assert.Contains(bnbId, baiclanNPCs);
        }

        [Fact]
        public void MonthlyNPCWander_PreservesSectorBoundsAndSectorKeys()
        {
            var world = WorldFactory.CreateDefault();

            var bnbId = world.FindByExternalId("npc_bai_scout");
            var xiongId = world.FindByExternalId("npc_xiong_warrior");

            // Tick a month
            SocialVendettaSystem.TickMonthlyNPCs(world, new SeededRandom(42));

            var bnbPos = world.Get<PositionComponent>(bnbId)!;
            var xiongPos = world.Get<PositionComponent>(xiongId)!;

            // Sectors must remain intact
            Assert.Equal("Sector_SouthernBorder_BaiClanMountain", bnbPos.SectorKey);
            Assert.Equal("Sector_SouthernBorder_XiongRidge", xiongPos.SectorKey);

            // Coordinates must be within their sector bounds
            var baiBp = WorldAtlasRegistry.Get("Sector_SouthernBorder_BaiClanMountain");
            Assert.InRange(bnbPos.Coord.X, 0, baiBp.Width - 1);
            Assert.InRange(bnbPos.Coord.Y, 0, baiBp.Height - 1);

            var xiongBp = WorldAtlasRegistry.Get("Sector_SouthernBorder_XiongRidge");
            Assert.InRange(xiongPos.Coord.X, 0, xiongBp.Width - 1);
            Assert.InRange(xiongPos.Coord.Y, 0, xiongBp.Height - 1);
        }

        [Fact]
        public void TransitSystem_LoadSector_LoadsOuterRegionSectorsCorrectly()
        {
            var world = WorldFactory.CreateDefault();

            // Transition to Northern Plains
            TransitSystem.LoadSector(world, "Sector_NorthernPlains_RoyalCourtSteppe");

            Assert.Equal("Sector_NorthernPlains_RoyalCourtSteppe", world.CurrentSectorKey);
            var currentSec = world.GetCurrentSector()!;
            Assert.NotNull(currentSec);
            Assert.Equal(WorldRegionID.NorthernPlains, currentSec.Region);
            Assert.Equal(BiomeType.GrasslandPlateau, currentSec.Biome);

            var playerPos = world.GetPlayerPosition();
            Assert.InRange(playerPos.X, 0, currentSec.GridWidth - 1);
            Assert.InRange(playerPos.Y, 0, currentSec.GridHeight - 1);
            Assert.True(currentSec.IsWalkable(playerPos));

            // Transition to Eastern Sea Archipelago
            TransitSystem.LoadSector(world, "Sector_EasternSea_RisingDragonPort");

            Assert.Equal("Sector_EasternSea_RisingDragonPort", world.CurrentSectorKey);
            var seaSec = world.GetCurrentSector()!;
            Assert.Equal(WorldRegionID.EasternSea, seaSec.Region);
            Assert.Equal(BiomeType.AzureArchipelago, seaSec.Biome);
        }

        [Fact]
        public void MultiSectorSaveLoad_PreservesAllVisitedSectorsAndIndices()
        {
            var world = WorldFactory.CreateDefault();

            // Visit Bai Clan Mountain and Northern Plains
            TransitSystem.LoadSector(world, "Sector_SouthernBorder_BaiClanMountain");
            TransitSystem.LoadSector(world, "Sector_NorthernPlains_RoyalCourtSteppe");

            Assert.True(SaveDiskIO.SaveWorld("multi_sector_test", world));
            var loaded = SaveDiskIO.LoadWorld("multi_sector_test");
            Assert.NotNull(loaded);

            Assert.Equal("Sector_NorthernPlains_RoyalCourtSteppe", loaded!.CurrentSectorKey);
            Assert.NotNull(loaded.GetSector("Sector_SouthernBorder_GuYueVillage"));
            Assert.NotNull(loaded.GetSector("Sector_SouthernBorder_BaiClanMountain"));
            Assert.NotNull(loaded.GetSector("Sector_NorthernPlains_RoyalCourtSteppe"));

            // Verify spatial index after load is correctly isolated
            var bnbId = loaded.FindByExternalId("npc_bai_scout");
            var bnbPos = loaded.Get<PositionComponent>(bnbId)!;
            Assert.Equal("Sector_SouthernBorder_BaiClanMountain", bnbPos.SectorKey);

            var entitiesAtBnb = loaded.GetEntitiesAt(bnbPos.Coord, "Sector_SouthernBorder_BaiClanMountain").ToList();
            Assert.Contains(bnbId, entitiesAtBnb);

            var entitiesAtGuYue = loaded.GetEntitiesAt(bnbPos.Coord, "Sector_SouthernBorder_GuYueVillage").ToList();
            Assert.DoesNotContain(bnbId, entitiesAtGuYue);
        }
    }
}
