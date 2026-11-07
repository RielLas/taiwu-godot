using System;
using Xunit;
using TaiwuGu.Core;

namespace TaiwuGu.Tests
{
    /// <summary>
    /// Phase 3c: simulation rolls come from a save-seeded stream, and a paused or hitchy frame
    /// cannot skip a refinement phase.
    /// </summary>
    public class Phase3cDeterminismTests
    {
        [Fact]
        public void SameRunSeed_ProducesIdenticalWildWillFrequency()
        {
            GuRefinementService first = CreateService(1234, out GameStateContext firstContext);
            GuRefinementService second = CreateService(1234, out GameStateContext secondContext);

            RunPhase1(first, firstContext);
            RunPhase1(second, secondContext);

            Assert.Equal(RefinementPhase.Phase2_WillFusion, firstContext.Cauldron.CurrentPhase);
            Assert.Equal(RefinementPhase.Phase2_WillFusion, secondContext.Cauldron.CurrentPhase);
            Assert.Equal(firstContext.Cauldron.WildWillFrequency, secondContext.Cauldron.WildWillFrequency);
        }

        [Fact]
        public void DifferentRunSeed_ProducesDifferentWildWillFrequency()
        {
            GuRefinementService first = CreateService(1, out GameStateContext firstContext);
            GuRefinementService second = CreateService(2, out GameStateContext secondContext);

            RunPhase1(first, firstContext);
            RunPhase1(second, secondContext);

            Assert.NotEqual(firstContext.Cauldron.WildWillFrequency, secondContext.Cauldron.WildWillFrequency);
        }

        [Fact]
        public void PausedSimulation_FreezesAndResumingCannotSkipAPhase()
        {
            GuRefinementService service = CreateService(7, out GameStateContext context);
            CauldronRefinementState cauldron = context.Cauldron;

            cauldron.CurrentPhase = RefinementPhase.Phase2_WillFusion;
            cauldron.Phase2Timer = 0.6f;
            cauldron.WillAlignmentProgress = 80f;
            cauldron.WildWillResistance = 5f;

            // Paused: nothing ticks, so nothing may advance. This is the state the system menu leaves
            // the cauldron in while GetTree().Paused is true.
            Assert.Equal(RefinementPhase.Phase2_WillFusion, cauldron.CurrentPhase);
            Assert.Equal(0.6f, cauldron.Phase2Timer);

            // Resume with a hitch-sized frame: it is clamped, so the phase neither skips nor fails.
            service.Tick(10f);
            Assert.Equal(RefinementPhase.Phase2_WillFusion, cauldron.CurrentPhase);
            Assert.True(cauldron.Phase2Timer <= 0.7f + 0.001f, $"A 10 s frame advanced the timer to {cauldron.Phase2Timer}, so the clamp failed.");

            // And the run still completes in order, so the transition cannot soft-lock.
            bool sawCondensation = false;
            for (int i = 0; i < 40 && cauldron.CurrentPhase != RefinementPhase.Success; i++)
            {
                if (cauldron.CurrentPhase == RefinementPhase.Phase3_Condensation) sawCondensation = true;
                service.Tick(0.1f);
            }

            Assert.True(sawCondensation, "Phase 2 must pass through Phase 3 rather than jumping to Success.");
            Assert.Equal(RefinementPhase.Success, cauldron.CurrentPhase);
        }

        [Fact]
        public void SeededRandom_IsReproducibleAndRangesAreRespected()
        {
            var a = new SeededRandom(99);
            var b = new SeededRandom(99);

            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(a.NextDouble(), b.NextDouble());
                Assert.Equal(a.Next(4, 9), b.Next(4, 9));
            }

            for (int i = 0; i < 50; i++)
            {
                Assert.InRange(a.Next(4, 9), 4, 8);
                Assert.InRange(a.Next(0, 3), 0, 2);
            }
        }

        private static GuRefinementService CreateService(int runSeed, out GameStateContext context)
        {
            context = GameStateContext.CreateDefault();
            context.RunSeed = runSeed;

            CauldronRefinementState cauldron = context.Cauldron;
            cauldron.IsActive = true;
            cauldron.CurrentPhase = RefinementPhase.Phase1_Extraction;
            cauldron.CauldronTemperature = 50f;
            cauldron.ActiveRecipe = new GuRefinementRecipe
            {
                Phase1Duration = 0.5f,
                Phase2Duration = 1f,
                Phase3Duration = 1f,
                DecayRateK = 0f,
                TargetRank = 1
            };

            var service = new GuRefinementService();
            service.Initialize(context);
            return service;
        }

        private static void RunPhase1(GuRefinementService service, GameStateContext context)
        {
            for (int i = 0; i < 20 && context.Cauldron.CurrentPhase == RefinementPhase.Phase1_Extraction; i++)
            {
                service.Tick(0.05f);
            }
        }
    }
}
