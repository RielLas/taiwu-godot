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
    public class Wave3ProgressionTests : IDisposable
    {
        public Wave3ProgressionTests() => WorldSignals.ClearAllSubscriptions();
        public void Dispose() => WorldSignals.ClearAllSubscriptions();

        private static void EnsureGuDatabaseLoaded()
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

        [Fact]
        public void WorldFactory_CreateFromConfig_SeedsCustomRunCorrectly()
        {
            var config = new NewRunConfig
            {
                RunSeed = 999,
                PlayerName = "Bai Ning Bing",
                OriginClan = "Bai Clan (白家寨)",
                StartingSectorKey = "Sector_SouthernBorder_BaiClanMountain",
                AptitudePercent = 95.0f,
                Disposition = TaiwuDisposition.Rebellious,
                VitalGuId = "gu_white_boar_r1"
            };

            var world = WorldFactory.CreateFromConfig(config);

            Assert.NotNull(world);
            Assert.Equal(999, world.RunSeed);
            Assert.Equal("Sector_SouthernBorder_BaiClanMountain", world.CurrentSectorKey);

            // Cultivator Component verification
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            Assert.NotNull(cult);
            Assert.Equal("Bai Ning Bing", cult.Name);
            Assert.Equal("Bai Clan (白家寨)", cult.Clan);
            Assert.Equal(TaiwuDisposition.Rebellious, cult.Disposition);
            Assert.Equal(AptitudeGrade.A_Grade, cult.Aptitude);
            Assert.Equal(95.0f, cult.AptitudePercent);
            Assert.Equal(100, cult.LifespanYears);

            // Aperture Component verification
            var aperture = world.Get<ApertureComponent>(world.PlayerEntity);
            Assert.NotNull(aperture);
            Assert.Equal(95.0f, aperture.CurrentEssenceSea);

            // Position Component verification
            var pos = world.Get<PositionComponent>(world.PlayerEntity);
            Assert.NotNull(pos);
            Assert.Equal("Sector_SouthernBorder_BaiClanMountain", pos.SectorKey);

            // Starter Gu and Vital Gu verification
            var ownedGus = world.GetOwned<GuWormComponent>(world.PlayerEntity).ToList();
            Assert.NotEmpty(ownedGus);

            var vitalGuEntry = ownedGus.FirstOrDefault(g => g.Component.IsVitalGu);
            Assert.NotNull(vitalGuEntry.Component);
            Assert.Equal("gu_white_boar_r1", vitalGuEntry.Component.GuID);

            var ownerComp = world.Get<OwnerComponent>(vitalGuEntry.Id);
            Assert.NotNull(ownerComp);
            Assert.True(ownerComp.IsEquipped);
            Assert.Equal(0, ownerComp.EquipSlot);
        }

        [Fact]
        public void GuDatabase_LoadsAll85Entries_AndCreateFromConfigUsesCanonicalVitalGuIds()
        {
            EnsureGuDatabaseLoaded();
            Assert.Equal(85, GuDatabase.Count);

            foreach (var def in GuDatabase.GetAll())
            {
                Assert.False(string.IsNullOrWhiteSpace(def.Id));
                if (def.RefinementPaths == null) continue;
                foreach (var path in def.RefinementPaths)
                {
                    if (!string.IsNullOrEmpty(path.ResultGuId))
                    {
                        Assert.True(GuDatabase.TryGet(path.ResultGuId, out var res) && res != null,
                            $"Dangling refinement target '{path.ResultGuId}' on '{def.Id}'.");
                    }
                    foreach (var mat in path.RequiredMaterials)
                    {
                        Assert.True(GuDatabase.TryGet(mat, out var matDef) && matDef != null,
                            $"Dangling refinement material '{mat}' on '{def.Id}'.");
                    }
                }
            }

            var defaultWorld = WorldFactory.CreateDefault(runSeed: 123);
            var owned = defaultWorld.GetOwned<GuWormComponent>(defaultWorld.PlayerEntity).Select(x => x.Component).ToList();
            Assert.Contains(owned, g => g.IsVitalGu && g.GuID == "gu_moonlight_r1");
            Assert.Contains(owned, g => !g.IsVitalGu && g.GuID == "gu_white_boar_r1");
            Assert.NotNull(GuDatabase.Get("gu_moonlight_r1"));
            Assert.NotNull(GuDatabase.Get("gu_white_boar_r1"));
        }

        [Fact]
        public void BreakthroughSystem_TryStartSovereignTrial_RequiresPeakRank5AndStartsCombat()
        {
            var world = WorldFactory.CreateDefault(runSeed: 505);
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;

            // Rank 4 Peak cannot start Sovereign Trial
            ap.Rank = 4;
            ap.Substage = 4;
            Assert.False(BreakthroughSystem.TryStartSovereignTrial(world));

            // Rank 5 Upper Stage (Substage 3) cannot start Sovereign Trial
            ap.Rank = 5;
            ap.Substage = 3;
            Assert.False(BreakthroughSystem.TryStartSovereignTrial(world));

            // Peak Rank 5 (Substage 4) enters SovereignTrial encounter
            ap.Rank = 5;
            ap.Substage = 4;
            Assert.True(BreakthroughSystem.TryStartSovereignTrial(world));

            var combatSession = world.GetSingleton<CombatSessionComponent>();
            Assert.NotNull(combatSession);
            Assert.True(combatSession!.InBattle);
            Assert.Equal(ArenaEncounterType.SovereignTrial, combatSession.EncounterType);
            Assert.True(combatSession.Simulation.IsActive);
            Assert.Contains(combatSession.Simulation.EntityBuffer,
                e => e.Active && e.Team == CombatEntityTeam.Hostile && e.Archetype == CombatEntityArchetype.SovereignElder);
        }

        [Fact]
        public void DaoMarksLedger_DamageMitigationAndAmplification_ScalesAccurately()
        {
            var ledger = new DaoMarksLedger
            {
                HumanPathMarks = 4,
                MoonPathMarks = 100,
                StrengthPathMarks = 50,
                BloodPathMarks = 30
            };

            // Amplification: 1.0f + marks * 0.001f
            Assert.Equal(1.004f, ledger.GetPathAmplificationMultiplier(DaoPathType.HumanPath), 4);
            Assert.Equal(1.100f, ledger.GetPathAmplificationMultiplier(DaoPathType.MoonPath), 4);
            Assert.Equal(1.050f, ledger.GetPathAmplificationMultiplier(DaoPathType.StrengthPath), 4);
            Assert.Equal(1.030f, ledger.GetPathAmplificationMultiplier(DaoPathType.BloodPath), 4);

            // Adding marks
            ledger.AddMarks(DaoPathType.MoonPath, 200);
            Assert.Equal(1.300f, ledger.GetPathAmplificationMultiplier(DaoPathType.MoonPath), 4);

            // Mitigation: 1.0f - Clamp(Strength * 0.002f + Human * 0.001f, 0, 0.50f)
            // Reduction = 50 * 0.002 (0.10) + 4 * 0.001 (0.004) = 0.104
            // Multiplier = 1.0 - 0.104 = 0.896
            Assert.Equal(0.896f, ledger.GetDamageMitigationMultiplier(), 3);

            // Capped at 50% max mitigation
            ledger.AddMarks(DaoPathType.StrengthPath, 500);
            Assert.Equal(0.500f, ledger.GetDamageMitigationMultiplier(), 3);
        }

        [Fact]
        public void KillerMove_Slot5_SynthesizesAndChannelsCorrectly()
        {
            var world = WorldFactory.CreateDefault();
            var simulation = CombatSystem.GetOrCreateArena(world);
            simulation.InitializeArena(
                world,
                ArenaEncounterType.DemonicCultivator,
                new GridCoord(15, 15),
                targetNpcId: null,
                isLethal: true,
                enemyName: "Test Cultivator",
                rng: new SeededRandom(42));

            // Verify Slot 5 is populated with Killer Move
            Assert.True(simulation.PlayerSkills[5].IsPopulated);
            Assert.Equal("killer_move_resonance", simulation.PlayerSkills[5].GuId);
            Assert.Equal(GuSkillEffectKind.AoEPulse, simulation.PlayerSkills[5].EffectKind);
            Assert.Equal(35f, simulation.PlayerSkills[5].BaseBeuCost);

            ref var player = ref simulation.EntityBuffer[0];
            Assert.Equal(0f, player.ChannelTimer);

            // Cast Killer Move
            bool casted = simulation.TryCastSkill(world, 5);
            Assert.True(casted);
            Assert.Equal(1.0f, player.ChannelTimer, 2);
            Assert.Equal(0f, player.VelX);
            Assert.Equal(0f, player.VelY);

            // Cannot cast while already channeling
            Assert.False(simulation.TryCastSkill(world, 0));
        }

        [Fact]
        public void KillerMove_FullChannelCompletion_DetonatesAndDamagesHostile()
        {
            var world = WorldFactory.CreateDefault(runSeed: 88);
            var simulation = CombatSystem.GetOrCreateArena(world);
            simulation.InitializeArena(
                world,
                ArenaEncounterType.DemonicCultivator,
                new GridCoord(15, 15),
                targetNpcId: null,
                isLethal: true,
                enemyName: "Target Cultivator",
                rng: new SeededRandom(88));

            ref var player = ref simulation.EntityBuffer[0];
            int hostileIdx = -1;
            for (int i = 1; i < CombatArenaSimulation.MAX_ENTITIES; i++)
            {
                if (simulation.EntityBuffer[i].Active && simulation.EntityBuffer[i].Team == CombatEntityTeam.Hostile)
                {
                    hostileIdx = i;
                    break;
                }
            }
            Assert.True(hostileIdx > 0);

            // Position hostile safely within AoE burst range without interrupting player channel
            ref var enemy = ref simulation.EntityBuffer[hostileIdx];
            enemy.PosX = player.PosX + 2.5f;
            enemy.PosY = player.PosY;
            enemy.MoveSpeed = 0f;
            enemy.AttackCooldown = 10f;
            enemy.MaxHealth = 500f;
            enemy.Health = 500f;

            Assert.True(simulation.TryCastSkill(world, 5));
            Assert.True(player.ChannelTimer > 0.9f);

            // Step through the full 1.0s channel duration
            for (int step = 0; step < 12; step++)
            {
                simulation.Step(world, 0.1f);
            }

            Assert.Equal(0f, player.ChannelTimer);
            Assert.True(simulation.EntityBuffer[hostileIdx].Health < 500f,
                "Completed Killer Move [R] channel must detonate and damage hostiles within burst radius.");
        }

        [Fact]
        public void CombatArenaSimulation_DaoMarksLedger_ScalesLiveDamageInStep()
        {
            float RunSingleProjectileDamage(int moonMarks)
            {
                var world = WorldFactory.CreateDefault(runSeed: 77);
                var dao = world.Get<DaoMarksComponent>(world.PlayerEntity)!;
                dao.Ledger.MoonPathMarks = moonMarks;

                var simulation = CombatSystem.GetOrCreateArena(world);
                simulation.InitializeArena(
                    world,
                    ArenaEncounterType.DemonicCultivator,
                    new GridCoord(15, 15),
                    targetNpcId: null,
                    isLethal: true,
                    enemyName: "Dummy Cultivator",
                    rng: new SeededRandom(77));

                ref var player = ref simulation.EntityBuffer[0];
                player.AimX = 1f;
                player.AimY = 0f;

                int hostileIdx = -1;
                for (int i = 1; i < CombatArenaSimulation.MAX_ENTITIES; i++)
                {
                    if (simulation.EntityBuffer[i].Active && simulation.EntityBuffer[i].Team == CombatEntityTeam.Hostile)
                    {
                        hostileIdx = i;
                        break;
                    }
                }
                Assert.True(hostileIdx > 0);

                ref var enemy = ref simulation.EntityBuffer[hostileIdx];
                enemy.PosX = player.PosX + 2.0f;
                enemy.PosY = player.PosY;
                enemy.MoveSpeed = 0f;
                enemy.AttackCooldown = 10f;
                enemy.MaxHealth = 500f;
                enemy.Health = 500f;

                Assert.True(simulation.TryCastSkill(world, 0));
                for (int step = 0; step < 20; step++)
                {
                    simulation.Step(world, 0.05f);
                }

                return 500f - simulation.EntityBuffer[hostileIdx].Health;
            }

            float baseDamage = RunSingleProjectileDamage(moonMarks: 0);
            float amplifiedDamage = RunSingleProjectileDamage(moonMarks: 500);

            Assert.True(baseDamage > 0f);
            Assert.True(amplifiedDamage > baseDamage * 1.4f,
                $"Expected 500 MoonPathMarks (~1.5x) to scale live combat damage (base={baseDamage}, amp={amplifiedDamage}).");
        }

        [Fact]
        public void OverworldHealing_ClanPostRestAndSolarTermRollover_RestoreHealthSafely()
        {
            var world = WorldFactory.CreateDefault(runSeed: 321);
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;
            var clock = world.GetSingleton<WorldClockComponent>()!;

            ap.MaxHealth = 100f;
            ap.CurrentHealth = 40f;
            clock.ActionPointsRemaining = 10;

            var clanDef = LandmarkRegistry.GetByPOI(TilePOIType.ClanPost)!;

            // First rest in Solar Term restores +35 HP and +5 AP
            clanDef.ExecuteAction!(world, "clan_post_rest", new GridCoord(15, 15));
            Assert.Equal(75f, ap.CurrentHealth, 2);
            Assert.Equal(15, clock.ActionPointsRemaining);

            // Second rest in the same Solar Term is blocked (prevents infinite AP/HP spam)
            clanDef.ExecuteAction!(world, "clan_post_rest", new GridCoord(15, 15));
            Assert.Equal(75f, ap.CurrentHealth, 2);
            Assert.Equal(15, clock.ActionPointsRemaining);

            // Solar Term rollover passively restores +15% MaxHealth (+15 HP -> 90 HP)
            TimeSystem.RollOverMonth(world);
            Assert.Equal(90f, ap.CurrentHealth, 2);
        }

        [Fact]
        public void KillerMove_ChannelInterruption_AppliesResonanceBacklashStunAndSatietyLoss()
        {
            var world = WorldFactory.CreateDefault();
            var simulation = CombatSystem.GetOrCreateArena(world);
            simulation.InitializeArena(
                world,
                ArenaEncounterType.WildBoarHunt,
                new GridCoord(15, 15),
                targetNpcId: null,
                isLethal: true,
                enemyName: "Ambush Boars",
                rng: new SeededRandom(55));

            // Record initial satiety for equipped Gu
            var equippedGus = world.GetOwned<GuWormComponent>(world.PlayerEntity)
                .Where(g => world.Get<OwnerComponent>(g.Id)?.IsEquipped == true)
                .Select(g => g.Component)
                .ToList();
            Assert.NotEmpty(equippedGus);
            foreach (var g in equippedGus) g.Satiety = 100f;

            // Start Killer Move Channel
            Assert.True(simulation.TryCastSkill(world, 5));
            ref var player = ref simulation.EntityBuffer[0];
            Assert.True(player.ChannelTimer > 0f);

            float initialHealth = player.Health;

            // Spawn hostile projectile directly on top of player to trigger hit and channel interruption
            ref var proj = ref simulation.ProjectileBuffer[0];
            proj.Active = true;
            proj.OwnerTeam = CombatEntityTeam.Hostile;
            proj.PosX = player.PosX;
            proj.PosY = player.PosY;
            proj.VelX = 0f;
            proj.VelY = 0f;
            proj.Damage = 10f;
            proj.Radius = 0.5f;
            proj.RemainingRange = 10f;

            simulation.Step(world, 0.016f);

            // Channel should be broken
            Assert.Equal(0f, player.ChannelTimer);
            // Stun timer should be 1.5s
            Assert.True(player.StunTimer > 1.0f);
            // Resonance backlash self-damage (25f) + projectile damage (10f)
            Assert.True(player.Health <= initialHealth - 30f);
            // Equipped Gu must lose 20 satiety
            foreach (var g in equippedGus)
            {
                Assert.Equal(80f, g.Satiety, 1);
            }
        }

        [Fact]
        public void CultivationSystem_ConsumeLifespanGu_IncrementsLifespanYearsAndDestroysEntity()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            Assert.NotNull(cult);
            int initialLifespan = cult.LifespanYears;

            // 1. Consume using catalog ID
            EntityId gu10 = WorldFactory.SpawnGuForOwner(world, world.PlayerEntity, new GuWormComponent
            {
                GuID = "gu_lifespan_10",
                Name = "Ten-Year Lifespan Gu (十年寿蛊)",
                Rank = 4,
                Slot = GuSlotCategory.Support,
                Diet = GuDietType.WillowLeafDew,
                BaseEssenceCost = 0f
            }, isEquipped: false);

            bool consumed10 = CultivationSystem.ConsumeLifespanGu(world, gu10);
            Assert.True(consumed10);
            Assert.Equal(initialLifespan + 10, cult.LifespanYears);
            Assert.Equal(10, cult.LifespanGuConsumed);
            Assert.False(world.IsAlive(gu10));

            // 2. Consume 20-year Lifespan Gu
            EntityId gu20 = WorldFactory.SpawnGuForOwner(world, world.PlayerEntity, new GuWormComponent
            {
                GuID = "gu_lifespan_20",
                Name = "Twenty-Year Lifespan Gu (二十年寿蛊)",
                Rank = 5,
                Slot = GuSlotCategory.Support,
                Diet = GuDietType.WillowLeafDew,
                BaseEssenceCost = 0f
            }, isEquipped: false);

            bool consumed20 = CultivationSystem.ConsumeLifespanGu(world, "gu_lifespan_20");
            Assert.True(consumed20);
            Assert.Equal(initialLifespan + 30, cult.LifespanYears);
            Assert.Equal(30, cult.LifespanGuConsumed);
            Assert.False(world.IsAlive(gu20));

            // 3. Direct years consumption (rejects <= 0)
            Assert.False(CultivationSystem.ConsumeLifespanGu(world, years: 0));
            Assert.False(CultivationSystem.ConsumeLifespanGu(world, years: -5));
            bool consumedDirect = CultivationSystem.ConsumeLifespanGu(world, years: 100);
            Assert.True(consumedDirect);
            Assert.Equal(initialLifespan + 130, cult.LifespanYears);
            Assert.Equal(130, cult.LifespanGuConsumed);
        }
    }
}
