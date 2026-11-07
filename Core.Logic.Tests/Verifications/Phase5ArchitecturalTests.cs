using System;
using System.Collections.Generic;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Combat;
using TaiwuGu.Psychology;

namespace TaiwuGu.Tests
{
    public class Phase5ArchitecturalTests
    {
        [Fact]
        public void Test01_CombatArenaIsolationAndCoordinates()
        {
            var stage = new DedicatedBattleStage();
            var cultivator = new CultivatorData("Fang Yuan", AptitudeGrade.C_Grade, 44f);

            Assert.Equal(new Vector3(2000f, 2000f, 0f), FullscreenCombatViewport.ArenaOrigin);
            Assert.False(stage.IsArenaActive);

            stage.EnterBattleStage(cultivator, new List<GuWormData>(), ArenaEncounterType.WildBoarHunt, new Vector2Int(5, 5));
            Assert.True(stage.IsArenaActive);

            stage.ExitBattleStage();
            Assert.False(stage.IsArenaActive);
        }

        [Fact]
        public void Test02_InputSubjugationAndCombatHandover()
        {
            GameEventManager.ClearAllSubscriptions();

            var router = new TaiwuInputRouter();
            router.OnEnable();
            router.SetInputActive(true);

            var stage = new DedicatedBattleStage();
            stage.OnEnable();

            try
            {
                Assert.Equal(InputControlMode.Overworld, router.ControlMode);

                GameEventManager.TriggerBattleStageStateChanged(true);
                Assert.Equal(InputControlMode.CombatArena, router.ControlMode);

                Vector2 testDir = new Vector2(1f, 0f);
                GameEventManager.RaiseRequestCombatMove(testDir);
                Assert.Equal(testDir, stage.CurrentMoveIntent);

                GameEventManager.TriggerBattleStageStateChanged(false);
                Assert.Equal(InputControlMode.Overworld, router.ControlMode);
            }
            finally
            {
                stage.OnDisable();
                router.OnDisable();
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test03_CombatExecutionAndCoordinationRewards()
        {
            GameEventManager.ClearAllSubscriptions();

            var ctx = GameStateContext.CreateDefault();
            using var timeService = new CalendarTimeService();
            timeService.Initialize(ctx);
            using var combatService = new CombatCoordinationService();
            combatService.Initialize(ctx);

            try
            {
                int initialStones = ctx.Cultivator.PrimevalStones;
                int initialAP = ctx.Calendar.ActionPointsRemaining;
                var boarMeatItem = ctx.GuInventory.FoodStockpile.Find(f => f.DietType == GuDietType.FreshBoarFlesh);
                int initialBoarMeat = boarMeatItem != null ? boarMeatItem.Quantity : 0;

                GameEventManager.TriggerCombatConcluded(true, new Vector2Int(15, 15), "Frenzied Razor Boar (狂暴野彘)", ArenaEncounterType.WildBoarHunt);

                Assert.Equal(initialAP - 1, ctx.Calendar.ActionPointsRemaining);
                Assert.Equal(initialStones + 15, ctx.Cultivator.PrimevalStones);

                int newBoarMeat = boarMeatItem != null ? boarMeatItem.Quantity : 0;
                Assert.Equal(initialBoarMeat + 2, newBoarMeat);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test04_CrimeSceneGenerationOnLethalCombat()
        {
            GameEventManager.ClearAllSubscriptions();

            var ctx = GameStateContext.CreateDefault();
            using var forensicsService = new CrimeAndForensicsService();
            forensicsService.Initialize(ctx);

            try
            {
                Assert.Empty(ctx.Forensics.ActiveCrimeScenes);

                Vector2Int fightPos = new Vector2Int(12, 18);
                GameEventManager.TriggerCombatConcluded(true, fightPos, "Frenzied Razor Boar", ArenaEncounterType.WildBoarHunt);

                Assert.Single(ctx.Forensics.ActiveCrimeScenes);

                var scene = ctx.Forensics.ActiveCrimeScenes[0];
                Assert.Equal(fightPos, scene.GridPos);
                Assert.Equal(CrimeSignature.BeastRemains, scene.Signature);
                Assert.Equal(2, scene.LingeringSoulQiTerms);
                Assert.False(scene.IsTamperedClean);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test05_TheThreeForensicTamperingChoices()
        {
            GameEventManager.ClearAllSubscriptions();

            var ctx = GameStateContext.CreateDefault();
            using var timeService = new CalendarTimeService();
            timeService.Initialize(ctx);
            using var forensicsService = new CrimeAndForensicsService();
            forensicsService.Initialize(ctx);

            try
            {
                ctx.Calendar.ActionPointsRemaining = 30;

                // 1. Cleanse
                Vector2Int pos1 = new Vector2Int(5, 5);
                forensicsService.SpawnCrimeScene(pos1, "Elder Gu Yue", CrimeSignature.CleanIncisions, terms: 2);
                GameEventManager.RaiseRequestTamperCrimeEvidence(pos1, CrimeTamperChoice.Cleanse);

                var scene1 = ctx.Forensics.ActiveCrimeScenes.Find(c => c.GridPos == pos1);
                Assert.NotNull(scene1);
                Assert.True(scene1.IsTamperedClean);
                Assert.Equal(0, scene1.LingeringSoulQiTerms);
                Assert.Equal(29, ctx.Calendar.ActionPointsRemaining);

                // 2. Frame Wild Beasts
                Vector2Int pos2 = new Vector2Int(6, 6);
                forensicsService.SpawnCrimeScene(pos2, "Rival Scout", CrimeSignature.CleanIncisions, terms: 2);
                GameEventManager.RaiseRequestTamperCrimeEvidence(pos2, CrimeTamperChoice.FrameWildBeasts);

                var scene2 = ctx.Forensics.ActiveCrimeScenes.Find(c => c.GridPos == pos2);
                Assert.NotNull(scene2);
                Assert.Equal(CrimeSignature.FramedWildBeast, scene2.Signature);
                Assert.Equal("Wild Boar Pack (野彘群)", scene2.FramedFaction);
                Assert.Equal(28, ctx.Calendar.ActionPointsRemaining);

                // 3. Frame Rival Clan
                Vector2Int pos3 = new Vector2Int(7, 7);
                forensicsService.SpawnCrimeScene(pos3, "Caravan Guard", CrimeSignature.CleanIncisions, terms: 2);
                GameEventManager.RaiseRequestTamperCrimeEvidence(pos3, CrimeTamperChoice.FrameRivalClan);

                var scene3 = ctx.Forensics.ActiveCrimeScenes.Find(c => c.GridPos == pos3);
                Assert.NotNull(scene3);
                Assert.Equal(CrimeSignature.FramedRivalClan, scene3.Signature);
                Assert.Equal("Xiong Clan (熊家寨)", scene3.FramedFaction);
                Assert.Equal(27, ctx.Calendar.ActionPointsRemaining);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test06_TemporalDissipationOverTwoSolarTerms()
        {
            GameEventManager.ClearAllSubscriptions();

            var ctx = GameStateContext.CreateDefault();
            using var timeService = new CalendarTimeService();
            timeService.Initialize(ctx);
            using var forensicsService = new CrimeAndForensicsService();
            forensicsService.Initialize(ctx);

            try
            {
                Vector2Int pos = new Vector2Int(20, 20);
                forensicsService.SpawnCrimeScene(pos, "Nameless Cultivator", CrimeSignature.CleanIncisions, terms: 2);

                Assert.NotNull(forensicsService.GetActiveCrimeSceneAt(pos));

                // Advance Term 1
                timeService.RollOverMonth();
                var sceneAfterTerm1 = ctx.Forensics.ActiveCrimeScenes.Find(c => c.GridPos == pos);
                Assert.NotNull(sceneAfterTerm1);
                Assert.Equal(1, sceneAfterTerm1.LingeringSoulQiTerms);

                // Advance Term 2
                timeService.RollOverMonth();
                var sceneAfterTerm2 = forensicsService.GetActiveCrimeSceneAt(pos);
                Assert.Null(sceneAfterTerm2);
                Assert.DoesNotContain(ctx.Forensics.ActiveCrimeScenes, c => c.GridPos == pos);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }
    }
}
