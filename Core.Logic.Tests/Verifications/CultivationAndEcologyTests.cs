using System;
using System.Linq;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class CultivationAndEcologyTests : IDisposable
    {
        public CultivationAndEcologyTests() => WorldSignals.ClearAllSubscriptions();
        public void Dispose() => WorldSignals.ClearAllSubscriptions();

        [Fact]
        public void CultivationMath_DensityScaling_AndStoneRecovery_MatchCanon()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;

            // Rank 1 Initial (Aptitude 44%): 15.4 / (44 * 1) * 100 = 35.0%
            Assert.Equal(1.0, ApertureComponent.CalculateDensityCoefficient(1, 1));
            Assert.Equal(80.0, ApertureComponent.CalculateDensityCoefficient(2, 1));
            Assert.Equal(6400.0 * 8.0, ApertureComponent.CalculateDensityCoefficient(3, 4));

            float recovery = ap.CalculateStoneRecoveryPercentage(cult.AptitudePercent);
            Assert.True(MathF.Abs(recovery - 35.0f) <= 0.1f);

            ap.CurrentEssenceSea = 5.0f;
            int initialStones = cult.PrimevalStones;
            Assert.True(CultivationSystem.ConsumeStone(world));
            Assert.Equal(initialStones - 1, cult.PrimevalStones);
            Assert.True(MathF.Abs(ap.CurrentEssenceSea - 40.0f) <= 0.5f);

            Assert.True(CultivationSystem.TryNurtureSubstage(world));
            Assert.Equal(2, ap.Substage);

            float newRecovery = ap.CalculateStoneRecoveryPercentage(cult.AptitudePercent);
            Assert.True(MathF.Abs(newRecovery - 17.5f) <= 0.1f);
        }

        [Fact]
        public void TimeSystem_APRollover_AndInviolateMortalLifespanAging_Work()
        {
            var world = WorldFactory.CreateDefault();
            var clock = world.GetSingleton<WorldClockComponent>()!;
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;

            int initialAge = cult.Age;
            Assert.Equal(4, clock.CurrentSolarTermIndex);
            Assert.Equal(30, clock.ActionPointsRemaining);

            // Deduct 30 AP -> triggers rollover to term 5
            TimeSystem.DeductActionPoints(world, 30);
            Assert.Equal(5, clock.CurrentSolarTermIndex);
            Assert.Equal(30, clock.ActionPointsRemaining);

            // Roll through remaining terms of Year 1 until Lichun rollover into Year 2
            for (int i = 0; i < 20; i++) TimeSystem.RollOverMonth(world);
            Assert.Equal(2, clock.CurrentYear);
            Assert.Equal(initialAge + 1, cult.Age);
        }

        [Fact]
        public void GuEcology_FeedingStarvationAndVitalGuImmunity_WorkDeterministically()
        {
            var world = WorldFactory.CreateDefault();
            var inv = world.Get<InventoryComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;

            int initialPetals = inv.GetFoodQuantity(GuDietType.MoonOrchidPetals);

            // Term 1 rollover feeds Rank 1 Moonlight Gu from stockpile
            TimeSystem.RollOverMonth(world);
            int consumed = (GuDatabase.TryGet("gu_moonlight_r1", out var moonDef) && moonDef?.Diet != null)
                ? Math.Max(1, moonDef.Diet.RationAmount)
                : 1;
            Assert.Equal(initialPetals - consumed, inv.GetFoodQuantity(GuDietType.MoonOrchidPetals));

            // Clear stockpile and give non-vital Boar Gu + vital Moonlight Gu predatory starvation pressure
            inv.FoodStockpile.Clear();
            EntityId moonId = (GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_moonlight_r1")
                ?? GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_moonlight_01"))!.Value;
            EntityId boarId = (GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_white_boar_r1")
                ?? GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_white_boar_01"))!.Value;

            var moonGu = world.Get<GuWormComponent>(moonId)!;
            var boarGu = world.Get<GuWormComponent>(boarId)!;
            boarGu.IsPredatory = true;

            // Default fallback degradation without DTO loaded is 30f per cycle -> 70 -> 40 (Hungry) -> 10 (Starving) -> 0 (Hibernating)
            GuEcologySystem.ProcessFeedingCycleForWorld(world);
            Assert.Equal(GuStarvationStage.Healthy, moonGu.GetStarvationStage());
            GuEcologySystem.ProcessFeedingCycleForWorld(world);
            Assert.Equal(GuStarvationStage.Hungry, moonGu.GetStarvationStage());
            GuEcologySystem.ProcessFeedingCycleForWorld(world);
            Assert.Equal(GuStarvationStage.Starving, moonGu.GetStarvationStage());

            float seaBeforeBacklash = ap.CurrentEssenceSea = cult.AptitudePercent;
            GuEcologySystem.ProcessFeedingCycleForWorld(world);
            Assert.Equal(GuStarvationStage.Hibernating, moonGu.GetStarvationStage());
            Assert.True(ap.CurrentEssenceSea < seaBeforeBacklash, "Predatory hibernating Gu must drain host primeval sea.");

            // Two more missed cycles destroy non-vital Boar Gu, while Vital Moonlight Gu stays alive
            GuEcologySystem.ProcessFeedingCycleForWorld(world);
            GuEcologySystem.ProcessFeedingCycleForWorld(world);

            Assert.False(world.IsAlive(boarId));
            Assert.True(world.IsAlive(moonId));
        }
    }
}
