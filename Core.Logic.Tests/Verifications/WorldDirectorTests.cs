using System;
using System.IO;
using System.Linq;
using Xunit;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Psychology;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class WorldDirectorTests : IDisposable
    {
        public WorldDirectorTests()
        {
            WorldSignals.ClearAllSubscriptions();
            EnsureGuDatabase();
        }

        public void Dispose()
        {
            WorldSignals.ClearAllSubscriptions();
        }

        [Fact]
        public void WorldDirector_ForeshadowedToActiveToExpired_RestoresSectorTileCleanly()
        {
            var world = WorldFactory.CreateDefault(runSeed: 42);
            var eventComp = world.GetOrCreateSingleton<WorldEventComponent>();
            eventComp.NextEventCountdownTerms = 1;

            var sector = world.GetCurrentSector();
            Assert.NotNull(sector);

            // 1. Advance to countdown trigger: Calamity scheduled as Foreshadowed with full duration (not decremented on spawn tick)
            WorldDirectorSystem.TickSolarTerm(world);
            Assert.Single(eventComp.ActiveEvents);
            var ev = eventComp.ActiveEvents[0];
            Assert.Equal(WorldCalamityPhase.Foreshadowed, ev.Phase);
            Assert.Equal(WorldDirectorSystem.DEFAULT_FORESHADOW_DURATION_TERMS, ev.RemainingTerms);

            TilePOIType initialPOI = sector!.GetTile(ev.TargetCoord.X, ev.TargetCoord.Y).POI;
            string? initialLandmark = sector.GetTile(ev.TargetCoord.X, ev.TargetCoord.Y).CustomLandmarkId;

            // 2. Advance 1 term: Foreshadowed countdown ticks (2 -> 1)
            WorldDirectorSystem.TickSolarTerm(world);
            Assert.Equal(WorldCalamityPhase.Foreshadowed, ev.Phase);
            Assert.Equal(1, ev.RemainingTerms);

            // 3. Advance 1 term: Foreshadowed -> Active transition
            // AncientGuCave placed on target tile, previous POI cached
            WorldDirectorSystem.TickSolarTerm(world);
            Assert.Equal(WorldCalamityPhase.Active, ev.Phase);
            Assert.Equal(WorldDirectorSystem.DEFAULT_ACTIVE_CALAMITY_DURATION_TERMS, ev.RemainingTerms);
            Assert.Equal(initialPOI, ev.PreviousPOI);
            Assert.Equal(initialLandmark, ev.PreviousCustomLandmarkId);
            Assert.Equal(TilePOIType.AncientGuCave, sector.GetTile(ev.TargetCoord.X, ev.TargetCoord.Y).POI);

            // 4. Advance 1 term: Active countdown ticks (2 -> 1)
            WorldDirectorSystem.TickSolarTerm(world);
            Assert.Equal(WorldCalamityPhase.Active, ev.Phase);
            Assert.Equal(1, ev.RemainingTerms);
            Assert.Equal(TilePOIType.AncientGuCave, sector.GetTile(ev.TargetCoord.X, ev.TargetCoord.Y).POI);

            // 5. Advance 1 term: Active -> Expired transition
            // Previous tile POI cleanly restored and event removed from ActiveEvents
            WorldDirectorSystem.TickSolarTerm(world);
            Assert.Equal(initialPOI, sector.GetTile(ev.TargetCoord.X, ev.TargetCoord.Y).POI);
            Assert.Equal(initialLandmark, sector.GetTile(ev.TargetCoord.X, ev.TargetCoord.Y).CustomLandmarkId);
            Assert.DoesNotContain(ev, eventComp.ActiveEvents);
        }

        [Fact]
        public void WorldDirector_DeterministicReplay_MatchesAcrossSameRunSeed()
        {
            var worldA = WorldFactory.CreateDefault(runSeed: 98765);
            var worldB = WorldFactory.CreateDefault(runSeed: 98765);

            // Simulate 24 solar terms (1 full calendar year)
            for (int term = 0; term < 24; term++)
            {
                TimeSystem.RollOverMonth(worldA);
                TimeSystem.RollOverMonth(worldB);
            }

            var eventCompA = worldA.GetSingleton<WorldEventComponent>();
            var eventCompB = worldB.GetSingleton<WorldEventComponent>();
            Assert.NotNull(eventCompA);
            Assert.NotNull(eventCompB);
            Assert.Equal(eventCompA!.NextEventCountdownTerms, eventCompB!.NextEventCountdownTerms);
            Assert.Equal(eventCompA.ActiveEvents.Count, eventCompB.ActiveEvents.Count);

            for (int i = 0; i < eventCompA.ActiveEvents.Count; i++)
            {
                var evA = eventCompA.ActiveEvents[i];
                var evB = eventCompB.ActiveEvents[i];

                Assert.Equal(evA.EventId, evB.EventId);
                Assert.Equal(evA.Kind, evB.Kind);
                Assert.Equal(evA.Phase, evB.Phase);
                Assert.Equal(evA.SectorKey, evB.SectorKey);
                Assert.Equal(evA.TargetCoord, evB.TargetCoord);
                Assert.Equal(evA.PreviousPOI, evB.PreviousPOI);
                Assert.Equal(evA.RemainingTerms, evB.RemainingTerms);
                Assert.Equal(evA.Title, evB.Title);
            }

            var trackerA = worldA.GetSingleton<BountyTrackerComponent>();
            var trackerB = worldB.GetSingleton<BountyTrackerComponent>();
            Assert.NotNull(trackerA);
            Assert.NotNull(trackerB);
            Assert.Equal(trackerA!.Hunters.Count, trackerB!.Hunters.Count);

            for (int i = 0; i < trackerA.Hunters.Count; i++)
            {
                var hA = trackerA.Hunters[i];
                var hB = trackerB.Hunters[i];

                Assert.Equal(hA.IssuingFaction, hB.IssuingFaction);
                Assert.Equal(hA.HunterName, hB.HunterName);
                Assert.Equal(hA.CurrentSectorKey, hB.CurrentSectorKey);
                Assert.Equal(hA.CurrentCoord, hB.CurrentCoord);
                Assert.Equal(hA.State, hB.State);
                Assert.Equal(hA.BribeCostStones, hB.BribeCostStones);
                Assert.Equal(hA.ThreatRank, hB.ThreatRank);
            }
        }

        [Fact]
        public void Forensics_FrameRivalClan_TriggersInterClanSkirmish()
        {
            var world = WorldFactory.CreateDefault(runSeed: 777);
            GridCoord crimePos = new GridCoord(16, 16);
            ForensicsSystem.SpawnCrimeScene(world, crimePos, "Elder Gu Yue", CrimeSignature.CleanIncisions, terms: 3);

            bool tampered = ForensicsSystem.TamperCrimeScene(world, crimePos, CrimeTamperChoice.FrameRivalClan);
            Assert.True(tampered);

            var eventComp = world.GetSingleton<WorldEventComponent>();
            Assert.NotNull(eventComp);

            var skirmish = eventComp!.ActiveEvents.Find(e => e.Kind == WorldCalamityKind.InterClanSkirmish);
            Assert.NotNull(skirmish);
            Assert.Equal(WorldCalamityPhase.Active, skirmish!.Phase);
            Assert.Equal(world.CurrentSectorKey, skirmish.SectorKey);
            Assert.Contains("Inter-Clan Skirmish", skirmish.Title);
            Assert.Contains("XiongClan", skirmish.Title);

            var xiong = world.GetFaction(FactionID.XiongClan);
            Assert.NotNull(xiong);
            Assert.True(xiong!.ReputationScore <= -30);
        }

        [Fact]
        public void BountyHunter_SpawnsOnHostileReputation_AndStepsTowardPlayer()
        {
            var world = WorldFactory.CreateDefault(runSeed: 555);
            var tieFaction = world.GetFaction(FactionID.TieClan);
            Assert.NotNull(tieFaction);
            tieFaction!.ReputationScore = -60; // Hostile tier
            Assert.Equal(FactionStandingTier.Hostile, tieFaction.GetStandingTier());

            var pPos = world.Get<PositionComponent>(world.PlayerEntity);
            Assert.NotNull(pPos);
            pPos!.Coord = new GridCoord(15, 15);

            // Tick solar term to spawn hunter
            WorldDirectorSystem.TickSolarTerm(world);

            var tracker = world.GetSingleton<BountyTrackerComponent>();
            Assert.NotNull(tracker);
            Assert.NotEmpty(tracker!.Hunters);

            var hunter = tracker.Hunters[0];
            Assert.Equal(BountyHunterState.Pursuing, hunter.State);
            Assert.Equal(FactionID.TieClan, hunter.IssuingFaction);

            var coordAfterSpawn = hunter.CurrentCoord;
            int dist0 = Math.Abs(hunter.CurrentCoord.X - pPos.Coord.X) + Math.Abs(hunter.CurrentCoord.Y - pPos.Coord.Y);
            Assert.True(dist0 > 0, "Hunter must spawn at non-zero distance from player.");

            // Advance another solar term; hunter must step 1-2 tiles closer to player
            WorldDirectorSystem.TickSolarTerm(world);
            var coordAfterTick2 = hunter.CurrentCoord;
            int dist1 = Math.Abs(hunter.CurrentCoord.X - pPos.Coord.X) + Math.Abs(hunter.CurrentCoord.Y - pPos.Coord.Y);
            Assert.True(dist1 < dist0, $"Hunter did not step toward player: spawnCoord={coordAfterSpawn}, tick2Coord={coordAfterTick2}, playerPos={pPos.Coord}, dist0={dist0}, dist1={dist1}, state={hunter.State}");
        }

        [Fact]
        public void BountyHunter_BribeAndCombatVictory_ResolvesTracker()
        {
            // Subtest 1: Bribing hunter
            var worldBribe = WorldFactory.CreateDefault(runSeed: 444);
            var xiong = worldBribe.GetFaction(FactionID.XiongClan);
            Assert.NotNull(xiong);
            xiong!.ReputationScore = -70;

            WorldDirectorSystem.TickSolarTerm(worldBribe);
            var trackerBribe = worldBribe.GetSingleton<BountyTrackerComponent>();
            Assert.NotNull(trackerBribe);
            Assert.NotEmpty(trackerBribe!.Hunters);
            var hunterBribe = trackerBribe.Hunters[0];

            var cultBribe = worldBribe.Get<CultivatorComponent>(worldBribe.PlayerEntity)!;
            cultBribe.PrimevalStones = 500;
            int stonesBefore = cultBribe.PrimevalStones;

            bool bribed = WorldDirectorSystem.TryBribeHunter(worldBribe, new EntityId(hunterBribe.HunterEntityId));
            Assert.True(bribed);
            Assert.Equal(BountyHunterState.Bribed, hunterBribe.State);
            Assert.Equal(stonesBefore - hunterBribe.BribeCostStones, cultBribe.PrimevalStones);
            Assert.False(WorldDirectorSystem.TryBribeHunter(worldBribe, new EntityId(hunterBribe.HunterEntityId)));

            // Subtest 2: Combat victory / defeat resolution
            var worldFight = WorldFactory.CreateDefault(runSeed: 333);
            var bai = worldFight.GetFaction(FactionID.BaiClan);
            Assert.NotNull(bai);
            bai!.ReputationScore = -80;

            WorldDirectorSystem.TickSolarTerm(worldFight);
            var trackerFight = worldFight.GetSingleton<BountyTrackerComponent>();
            Assert.NotNull(trackerFight);
            Assert.NotEmpty(trackerFight!.Hunters);
            var hunterFight = trackerFight.Hunters[0];
            Assert.Equal(BountyHunterState.Pursuing, hunterFight.State);

            bool defeated = WorldDirectorSystem.ResolveHunterDefeated(worldFight, new EntityId(hunterFight.HunterEntityId));
            Assert.True(defeated);
            Assert.Equal(BountyHunterState.Defeated, hunterFight.State);

            // Subtest 3: NPC mortality triggers Defeated on next tick
            var worldKill = WorldFactory.CreateDefault(runSeed: 222);
            var tie = worldKill.GetFaction(FactionID.TieClan);
            Assert.NotNull(tie);
            tie!.ReputationScore = -75;

            WorldDirectorSystem.TickSolarTerm(worldKill);
            var trackerKill = worldKill.GetSingleton<BountyTrackerComponent>()!;
            var hunterKill = trackerKill.Hunters[0];
            Assert.Equal(BountyHunterState.Pursuing, hunterKill.State);

            var hunterCult = worldKill.Get<CultivatorComponent>(new EntityId(hunterKill.HunterEntityId))!;
            hunterCult.Age = hunterCult.LifespanYears; // Mark dead
            Assert.False(hunterCult.IsAlive);

            WorldDirectorSystem.TickSolarTerm(worldKill);
            Assert.Equal(BountyHunterState.Defeated, hunterKill.State);
        }

        [Fact]
        public void Exploration_WildGuPool_RespectsSectorNativePathsAndMaxRank()
        {
            var world = WorldFactory.CreateDefault(runSeed: 101);

            // 1. Gu Yue Village: Native Paths = Moon, Light, Food; MaxWildGuRank = 1
            string guYueKey = "Sector_SouthernBorder_GuYueVillage";
            Assert.True(WorldAtlasRegistry.TryGet(guYueKey, out var guYueBp));
            Assert.NotNull(guYueBp);

            var rng = new SeededRandom(12345);
            for (int i = 0; i < 60; i++)
            {
                string pickedId = ExplorationSystem.PickWildGuForSector(world, guYueKey, rng);
                var def = GuDatabase.Get(pickedId);
                Assert.NotNull(def);
                Assert.True(def!.Rank <= guYueBp!.MaxWildGuRank, $"Gu '{def.Id}' rank {def.Rank} exceeds sector max {guYueBp.MaxWildGuRank}");
                Assert.Contains(guYueBp.NativeGuPaths, p => string.Equals(p, def.Path, StringComparison.OrdinalIgnoreCase));
            }

            // 2. Bai Clan Mountain: Native Paths = Bone, Ice, Light; MaxWildGuRank = 2
            string baiKey = "Sector_SouthernBorder_BaiClanMountain";
            Assert.True(WorldAtlasRegistry.TryGet(baiKey, out var baiBp));
            Assert.NotNull(baiBp);

            for (int i = 0; i < 60; i++)
            {
                string pickedId = ExplorationSystem.PickWildGuForSector(world, baiKey, rng);
                var def = GuDatabase.Get(pickedId);
                Assert.NotNull(def);
                Assert.True(def!.Rank <= baiBp!.MaxWildGuRank, $"Gu '{def.Id}' rank {def.Rank} exceeds sector max {baiBp.MaxWildGuRank}");
                Assert.Contains(baiBp.NativeGuPaths, p => string.Equals(p, def.Path, StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public void Caravan_EnsureStock_ResetsOnSectorChange()
        {
            var world = WorldFactory.CreateDefault(runSeed: 888);
            world.CurrentSectorKey = "Sector_SouthernBorder_GuYueVillage";

            var session1 = CaravanEconomySystem.EnsureStock(world);
            Assert.NotNull(session1);
            Assert.True(session1.HasStock);
            Assert.Equal("Sector_SouthernBorder_GuYueVillage", session1.SectorKey);
            int seed1 = session1.StockSeed;
            var stock1Ids = session1.Stock.Select(s => s.Id).ToList();

            // Calling EnsureStock again within the same sector preserves cached session
            var sessionSame = CaravanEconomySystem.EnsureStock(world);
            Assert.Same(session1, sessionSame);
            Assert.Equal(seed1, sessionSame.StockSeed);

            // Change sector to Bai Clan Mountain
            world.CurrentSectorKey = "Sector_SouthernBorder_BaiClanMountain";
            var session2 = CaravanEconomySystem.EnsureStock(world);
            Assert.NotNull(session2);
            Assert.Equal("Sector_SouthernBorder_BaiClanMountain", session2.SectorKey);
            Assert.NotEqual(seed1, session2.StockSeed);
            Assert.True(session2.HasStock);

            var stock2Ids = session2.Stock.Select(s => s.Id).ToList();
            Assert.False(stock1Ids.SequenceEqual(stock2Ids), "Caravan stock must regenerate uniquely when switching sectors.");
        }

        private static void EnsureGuDatabase()
        {
            if (GuDatabase.IsLoaded) return;

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Resources", "Data", "gu_worms_database.json");
                if (File.Exists(candidate))
                {
                    Assert.True(GuDatabase.Initialize(File.ReadAllText(candidate)));
                    return;
                }
                dir = dir.Parent;
            }

            throw new InvalidOperationException("Unable to locate Resources/Data/gu_worms_database.json from the test output directory.");
        }
    }
}
