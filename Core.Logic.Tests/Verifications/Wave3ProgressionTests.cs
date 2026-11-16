using System;
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
                VitalGuId = "gu_white_boar_01"
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
            Assert.Equal("gu_white_boar_01", vitalGuEntry.Component.GuID);

            var ownerComp = world.Get<OwnerComponent>(vitalGuEntry.Id);
            Assert.NotNull(ownerComp);
            Assert.True(ownerComp.IsEquipped);
            Assert.Equal(0, ownerComp.EquipSlot);
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
            Assert.False(world.IsAlive(gu20));

            // 3. Direct years consumption
            bool consumedDirect = CultivationSystem.ConsumeLifespanGu(world, years: 100);
            Assert.True(consumedDirect);
            Assert.Equal(initialLifespan + 130, cult.LifespanYears);
        }
    }
}
