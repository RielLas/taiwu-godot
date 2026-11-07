using System;
using Xunit;
using TaiwuGu.Core;

namespace TaiwuGu.Tests
{
    public class Phase4ArchitecturalTests
    {
        /// <summary>Drive the cauldron at realistic frame steps; each tick is clamped to 0.1 s.</summary>
        private static void TickFor(GuRefinementService service, float seconds)
        {
            int steps = (int)MathF.Round(seconds / GuRefinementService.MAX_TICK_SECONDS);
            for (int i = 0; i < steps; i++) service.Tick(GuRefinementService.MAX_TICK_SECONDS);
        }

        [Fact]
        public void Test01_CauldronStateCentralizationAndFuelCheck()
        {
            var ctx = GameStateContext.CreateDefault();
            Assert.NotNull(ctx.Cauldron);
            Assert.Equal(RefinementPhase.Idle, ctx.Cauldron.CurrentPhase);

            var recipe = ctx.Cauldron.ActiveRecipe;
            Assert.NotNull(recipe);
            Assert.Equal("gu_moonlight_01", recipe.PrimaryGuID);
            Assert.Equal("gu_white_boar_01", recipe.SecondaryGuID);

            int minStones = ctx.Cauldron.CalculateMinStonesRequired();
            Assert.Equal(11, minStones);

            using var service = new GuRefinementService();
            service.Initialize(ctx);

            Assert.True(service.CanInitiateRefinement(out string reason));

            ctx.Cultivator.PrimevalStones = 5;
            Assert.False(service.CanInitiateRefinement(out reason));

            ctx.Cultivator.PrimevalStones = 250;
            ctx.Cultivator.CurrentEssenceSea = 15f;
            Assert.False(service.CanInitiateRefinement(out reason));
        }

        [Fact]
        public void Test02_Phase1ThermalEquilibriumAndCleanFail()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new GuRefinementService();
            service.Initialize(ctx);

            GameEventManager.RaiseRequestOpenCauldron();
            Assert.True(ctx.Cauldron.IsActive);

            GameEventManager.RaiseRequestInitiateRefinement();
            Assert.Equal(RefinementPhase.Phase1_Extraction, ctx.Cauldron.CurrentPhase);

            float startSea = ctx.Cultivator.CurrentEssenceSea;
            GameEventManager.RaiseRequestCauldronPulseHeat();
            Assert.True(Mathf.Abs(ctx.Cauldron.CauldronTemperature - 62f) <= 0.01f);
            Assert.True(Mathf.Abs(ctx.Cultivator.CurrentEssenceSea - (startSea - 2.5f)) <= 0.01f);

            ctx.Cauldron.CauldronTemperature = 10f;
            TickFor(service, 1.0f);
            Assert.Equal(RefinementPhase.Phase1_Extraction, ctx.Cauldron.CurrentPhase);

            TickFor(service, 0.6f);
            Assert.Equal(RefinementPhase.Failure, ctx.Cauldron.CurrentPhase);
            Assert.Equal(RefinementFailureTier.Tier1_CleanFail, ctx.Cauldron.LastFailureTier);

            GameEventManager.ClearAllSubscriptions();
        }

        [Fact]
        public void Test03_Phase2WillFusionAndActiveSuppression()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new GuRefinementService();
            service.Initialize(ctx);

            GameEventManager.RaiseRequestOpenCauldron();
            GameEventManager.RaiseRequestInitiateRefinement();

            for (float t = 0; t < 4.1f; t += 0.1f)
            {
                ctx.Cauldron.CauldronTemperature = 50f;
                service.Tick(0.1f);
            }
            Assert.Equal(RefinementPhase.Phase2_WillFusion, ctx.Cauldron.CurrentPhase);

            GameEventManager.RaiseRequestCauldronAdjustWillFocus(0.15f);
            Assert.True(Mathf.Abs(ctx.Cauldron.MasterWillFocus - 0.65f) <= 0.01f);

            ctx.Cauldron.WildWillFrequency = 0.65f;
            ctx.Cauldron.WildWillResistance = 50f;
            ctx.Cauldron.WillAlignmentProgress = 0f; // isolate the suppression strike from the transition frame
            float seaBefore = ctx.Cultivator.CurrentEssenceSea;
            GameEventManager.RaiseRequestCauldronSuppressWill();

            Assert.True(Mathf.Abs(ctx.Cauldron.WillAlignmentProgress - 18f) <= 0.01f);
            Assert.True(Mathf.Abs(ctx.Cauldron.WildWillResistance - 28f) <= 0.01f);
            Assert.True(Mathf.Abs(ctx.Cultivator.CurrentEssenceSea - (seaBefore - 1.5f)) <= 0.01f);

            ctx.Cauldron.WillAlignmentProgress = 60f;
            ctx.Cauldron.WildWillResistance = 10f;
            for (float t = 0; t < 5.0f; t += 0.1f)
            {
                ctx.Cauldron.MasterWillFocus = ctx.Cauldron.WildWillFrequency;
                service.Tick(0.1f);
            }

            Assert.Equal(RefinementPhase.Phase3_Condensation, ctx.Cauldron.CurrentPhase);

            GameEventManager.ClearAllSubscriptions();
        }

        [Fact]
        public void Test04_Phase3CondensationAndStoneCrushingTriumph()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new GuRefinementService();
            service.Initialize(ctx);

            ctx.Cauldron.IsActive = true;
            ctx.Cauldron.CurrentPhase = RefinementPhase.Phase3_Condensation;
            ctx.Cauldron.NascentStability = 100f;
            ctx.Cauldron.Phase3Timer = 0f;
            ctx.Cultivator.PrimevalStones = 250;

            TickFor(service, 0.5f);
            Assert.True(Mathf.Abs(ctx.Cauldron.NascentStability - 82f) <= 0.01f);

            GameEventManager.RaiseRequestCauldronCrushStone();
            Assert.Equal(249, ctx.Cultivator.PrimevalStones);
            Assert.Equal(1, ctx.Cauldron.StonesCrushed);
            Assert.True(Mathf.Abs(ctx.Cauldron.NascentStability - 96f) <= 0.01f);

            TickFor(service, 1.0f);
            GameEventManager.RaiseRequestCauldronCrushStone();

            TickFor(service, 1.0f);
            GameEventManager.RaiseRequestCauldronCrushStone();
            GameEventManager.RaiseRequestCauldronCrushStone();

            TickFor(service, 1.1f);
            Assert.Equal(RefinementPhase.Success, ctx.Cauldron.CurrentPhase);

            var secGu = ctx.GuInventory.EquippedGu.Find(g => g.GuID == "gu_white_boar_01");
            Assert.Null(secGu);

            var priGu = ctx.GuInventory.EquippedGu.Find(g => g.GuID == "gu_moonglow_beast_02");
            Assert.NotNull(priGu);
            Assert.Equal(2, priGu.Rank);
            Assert.True(priGu.Satiety >= 99f);

            GameEventManager.ClearAllSubscriptions();
        }

        [Fact]
        public void Test05_VitalGuImmunityVsAuxiliaryFragility()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            using var service = new GuRefinementService();
            service.Initialize(ctx);

            ctx.Cauldron.IsActive = true;
            ctx.Cauldron.CurrentPhase = RefinementPhase.Phase2_WillFusion;
            ctx.Cauldron.WildWillResistance = 99f;
            ctx.Cauldron.MasterWillFocus = 0.1f;
            ctx.Cauldron.WildWillFrequency = 0.8f;

            service.Tick(0.2f);
            Assert.Equal(RefinementPhase.Failure, ctx.Cauldron.CurrentPhase);
            Assert.Equal(RefinementFailureTier.Tier3_CatastrophicBacklash, ctx.Cauldron.LastFailureTier);

            var secGu = ctx.GuInventory.EquippedGu.Find(g => g.GuID == "gu_white_boar_01");
            Assert.Null(secGu);

            var priGu = ctx.GuInventory.EquippedGu.Find(g => g.GuID == "gu_moonlight_01");
            Assert.NotNull(priGu);
            Assert.True(priGu.IsInTraumaSleep);
            Assert.True(priGu.Satiety <= 0.01f);
            Assert.True(ctx.Cultivator.CurrentEssenceSea <= 0.01f);

            GameEventManager.ClearAllSubscriptions();
        }
    }
}
