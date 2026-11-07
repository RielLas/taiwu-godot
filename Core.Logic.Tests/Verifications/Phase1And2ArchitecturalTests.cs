using System;
using System.Reflection;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.UI;

namespace TaiwuGu.Tests
{
    public class Phase1And2ArchitecturalTests
    {
        [Fact]
        public void Test01_StateCentralizationAndTruth()
        {
            var ctx = GameStateContext.CreateDefault();

            // Cultivator State assertions
            Assert.Equal("Gu Yue Fang Yuan", ctx.Cultivator.Name);
            Assert.Equal(AptitudeGrade.C_Grade, ctx.Cultivator.Aptitude);
            Assert.True(Mathf.Abs(ctx.Cultivator.AptitudePercent - 44.0f) <= 0.01f);
            Assert.Equal(1, ctx.Cultivator.Rank);
            Assert.Equal(1, ctx.Cultivator.Substage);
            Assert.Equal(250, ctx.Cultivator.PrimevalStones);

            // Gu Inventory State assertions
            Assert.Equal(2, ctx.GuInventory.EquippedGu.Count);
            Assert.True(ctx.GuInventory.FoodStockpile.Count >= 3);

            // Calendar State assertions
            Assert.Equal(1, ctx.Calendar.CurrentYear);
            Assert.Equal(4, ctx.Calendar.CurrentSolarTermIndex);
            Assert.Equal(30, ctx.Calendar.ActionPointsRemaining);
        }

        [Fact]
        public void Test02_CalendarAPProgressionAndMonthRollover()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new CalendarTimeService();
            service.Initialize(ctx);

            int lastReportedAP = -1;
            int lastReportedMaxAP = -1;
            int lastReportedYear = -1;
            SolarTerm lastReportedTerm = SolarTerm.Lichun_BeginningOfSpring;

            Action<int, int> apHandler = (ap, max) => { lastReportedAP = ap; lastReportedMaxAP = max; };
            Action<int, SolarTerm, string> termHandler = (yr, term, name) => { lastReportedYear = yr; lastReportedTerm = term; };

            GameEventManager.OnActionPointsChanged += apHandler;
            GameEventManager.OnSolarTermAdvanced += termHandler;

            try
            {
                // Step 1: Move 1 step -> AP should decrease from 30 to 29
                GameEventManager.TriggerPlayerMoved(new Vector2Int(15, 16));

                Assert.Equal(29, ctx.Calendar.ActionPointsRemaining);
                Assert.Equal(29, lastReportedAP);

                // Step 2: Move remaining 29 steps (total 30 steps taken)
                for (int i = 0; i < 29; i++)
                {
                    GameEventManager.TriggerPlayerMoved(new Vector2Int(15, 16 + i));
                }

                // AP reached 0 -> Rollover must have fired!
                // Term should advance from 4 (Chunfen) to 5 (Qingming)
                // AP should refill to 30
                Assert.Equal(5, ctx.Calendar.CurrentSolarTermIndex);
                Assert.Equal(30, ctx.Calendar.ActionPointsRemaining);
                Assert.Equal(30, lastReportedAP);
                Assert.Equal(SolarTerm.Qingming_PureBrightness, lastReportedTerm);
            }
            finally
            {
                GameEventManager.OnActionPointsChanged -= apHandler;
                GameEventManager.OnSolarTermAdvanced -= termHandler;
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test03_PassMonthCommandIntent()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new CalendarTimeService();
            service.Initialize(ctx);

            try
            {
                // Starting at term 4 (Chunfen) with 30 AP
                GameEventManager.RaiseRequestAdvanceSolarTerm();

                Assert.Equal(5, ctx.Calendar.CurrentSolarTermIndex);
                Assert.Equal(30, ctx.Calendar.ActionPointsRemaining);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test04_GuLifecycleFeedingAndCadence()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var calendarService = new CalendarTimeService();
            using var guService = new GuLifecycleService();

            calendarService.Initialize(ctx);
            guService.Initialize(ctx);

            try
            {
                // Initial: Moonlight Gu (Rank 1, Diet: MoonOrchidPetals, 12 in stockpile)
                int initialPetals = ctx.GuInventory.GetFoodQuantity(GuDietType.MoonOrchidPetals);

                // Advance 1 Solar Term (Chunfen 4 -> Qingming 5)
                GameEventManager.RaiseRequestAdvanceSolarTerm();

                int postPetals = ctx.GuInventory.GetFoodQuantity(GuDietType.MoonOrchidPetals);
                Assert.Equal(initialPetals - 1, postPetals);

                // Now test starvation: Wipe food stockpile
                ctx.GuInventory.FoodStockpile.Clear();

                var moonlightGu = ctx.GuInventory.FindGu("gu_moonlight_01");
                Assert.NotNull(moonlightGu);

                // Advance month without food -> drops by -30% to 70%
                GameEventManager.RaiseRequestAdvanceSolarTerm();
                Assert.True(Mathf.Abs(moonlightGu.Satiety - 70f) <= 1.0f);
                Assert.Equal(GuStarvationStage.Healthy, moonlightGu.GetStarvationStage());

                // Advance another term without food -> drops to 40% (Hungry)
                GameEventManager.RaiseRequestAdvanceSolarTerm();
                Assert.Equal(GuStarvationStage.Hungry, moonlightGu.GetStarvationStage());

                // Advance another term without food -> drops to 10% (Starving)
                GameEventManager.RaiseRequestAdvanceSolarTerm();
                Assert.Equal(GuStarvationStage.Starving, moonlightGu.GetStarvationStage());

                // Advance another term without food -> drops to 0% (Hibernating)
                GameEventManager.RaiseRequestAdvanceSolarTerm();
                Assert.Equal(GuStarvationStage.Hibernating, moonlightGu.GetStarvationStage());
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test05_CultivationServiceFormulasAndBreaks()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new CultivationService();
            service.Initialize(ctx);

            try
            {
                // Fang Yuan: Rank 1 Substage 1 (Initial), Aptitude = 44%
                // Recovery% = (15.4 / (44 * 1)) * 100 = 35.0%
                float recovery = ctx.Cultivator.CalculateStoneRecoveryPercentage();
                Assert.True(Mathf.Abs(recovery - 35.0f) <= 0.1f);

                ctx.Cultivator.CurrentEssenceSea = 5.0f;
                int initialStones = ctx.Cultivator.PrimevalStones;

                GameEventManager.RaiseRequestConsumeStone();

                Assert.Equal(initialStones - 1, ctx.Cultivator.PrimevalStones);
                Assert.True(Mathf.Abs(ctx.Cultivator.CurrentEssenceSea - 40.0f) <= 0.5f);

                // Substage 1 -> 2
                GameEventManager.RaiseRequestNurtureSubstage();
                Assert.Equal(2, ctx.Cultivator.Substage);

                float newRecovery = ctx.Cultivator.CalculateStoneRecoveryPercentage();
                Assert.True(Mathf.Abs(newRecovery - 17.5f) <= 0.1f);
            }
            finally
            {
                GameEventManager.ClearAllSubscriptions();
            }
        }

        [Fact]
        public void Test06_ConstitutionalObserverDecoupling()
        {
            var hudType = typeof(TaiwuHUDView);
            var fields = hudType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var f in fields)
            {
                Assert.False(
                    f.FieldType == typeof(CalendarTimeService) ||
                    f.FieldType == typeof(GuLifecycleService) ||
                    f.FieldType == typeof(CultivationService) ||
                    f.FieldType == typeof(GameStateContext),
                    $"TaiwuHUDView has forbidden direct reference to {f.FieldType.Name}!"
                );
            }
        }

        [Fact]
        public void Test07_IsometricCliffExposureAndCoordinateOrientation()
        {
            // Center tile
            var center = new Vector2Int(15, 15);
            var centerWorld = ExplorationService.GridToWorld(center);

            // In Godot 2D, +Y points DOWN (foreground / southern).
            // (x, y + 1) is South-West: wx decreases (-hw), wy increases (+hh, down-screen).
            var southWest = new Vector2Int(15, 16);
            var southWestWorld = ExplorationService.GridToWorld(southWest);
            Assert.True(southWestWorld.y > centerWorld.y, "South-West neighbor must have larger Y (downward/foreground)");
            Assert.True(southWestWorld.x < centerWorld.x, "South-West neighbor must have smaller X (leftward)");

            // (x + 1, y) is South-East: wx increases (+hw), wy increases (+hh, down-screen).
            var southEast = new Vector2Int(16, 15);
            var southEastWorld = ExplorationService.GridToWorld(southEast);
            Assert.True(southEastWorld.y > centerWorld.y, "South-East neighbor must have larger Y (downward/foreground)");
            Assert.True(southEastWorld.x > centerWorld.x, "South-East neighbor must have larger X (rightward)");

            // (x - 1, y) is North-West (background): wy decreases (-hh, up-screen).
            var northWest = new Vector2Int(14, 15);
            var northWestWorld = ExplorationService.GridToWorld(northWest);
            Assert.True(northWestWorld.y < centerWorld.y, "North-West neighbor must have smaller Y (upward/background)");

            // (x, y - 1) is North-East (background): wy decreases (-hh, up-screen).
            var northEast = new Vector2Int(15, 14);
            var northEastWorld = ExplorationService.GridToWorld(northEast);
            Assert.True(northEastWorld.y < centerWorld.y, "North-East neighbor must have smaller Y (upward/background)");
        }
    }
}
