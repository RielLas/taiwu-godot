using System;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class RefinementAndDeterminismTests : IDisposable
    {
        public RefinementAndDeterminismTests() => WorldSignals.ClearAllSubscriptions();
        public void Dispose() => WorldSignals.ClearAllSubscriptions();

        private static void TickFor(WorldStore world, float seconds)
        {
            int steps = (int)MathF.Round(seconds / RefinementSystem.MAX_TICK_SECONDS);
            for (int i = 0; i < steps; i++) RefinementSystem.Tick(world, RefinementSystem.MAX_TICK_SECONDS);
        }

        [Fact]
        public void FuelValidation_AndPhase1CleanFail_WorkAccurately()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;

            RefinementSystem.OpenCauldron(world);
            var cauldron = RefinementSystem.GetOrCreateSession(world);
            Assert.Equal(RefinementPhase.Idle, cauldron.CurrentPhase);
            Assert.Equal(11, cauldron.CalculateMinStonesRequired());
            Assert.True(RefinementSystem.CanInitiateRefinement(world, out _));

            cult.PrimevalStones = 5;
            Assert.False(RefinementSystem.CanInitiateRefinement(world, out _));
            cult.PrimevalStones = 250;

            Assert.True(RefinementSystem.InitiateRefinement(world));
            Assert.Equal(RefinementPhase.Phase1_Extraction, cauldron.CurrentPhase);

            float startSea = ap.CurrentEssenceSea;
            RefinementSystem.PulseHeat(world);
            Assert.True(MathF.Abs(cauldron.CauldronTemperature - 62f) <= 0.01f);
            Assert.True(MathF.Abs(ap.CurrentEssenceSea - (startSea - 2.5f)) <= 0.01f);

            cauldron.CauldronTemperature = 10f;
            TickFor(world, 1.0f);
            Assert.Equal(RefinementPhase.Phase1_Extraction, cauldron.CurrentPhase);

            TickFor(world, 0.6f);
            Assert.Equal(RefinementPhase.Failure, cauldron.CurrentPhase);
            Assert.Equal(RefinementFailureTier.Tier1_CleanFail, cauldron.LastFailureTier);
            Assert.NotNull(GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_white_boar_r1"));
        }

        [Fact]
        public void FullThreePhaseCondensationTriumph_TransformsPrimaryAndConsumesSecondary()
        {
            var world = WorldFactory.CreateDefault();
            var cauldron = RefinementSystem.GetOrCreateSession(world);
            RefinementSystem.OpenCauldron(world);
            RefinementSystem.InitiateRefinement(world);

            for (float t = 0; t < 4.1f; t += 0.1f)
            {
                cauldron.CauldronTemperature = 50f;
                RefinementSystem.Tick(world, 0.1f);
            }
            Assert.Equal(RefinementPhase.Phase2_WillFusion, cauldron.CurrentPhase);

            RefinementSystem.AdjustWillFocus(world, 0.15f);
            Assert.True(MathF.Abs(cauldron.MasterWillFocus - 0.65f) <= 0.01f);

            cauldron.WildWillFrequency = 0.65f;
            cauldron.WildWillResistance = 50f;
            cauldron.WillAlignmentProgress = 0f;
            RefinementSystem.SuppressWill(world);
            Assert.True(cauldron.WillAlignmentProgress >= 17.9f);

            cauldron.WillAlignmentProgress = 60f;
            cauldron.WildWillResistance = 10f;
            for (float t = 0; t < 5.0f; t += 0.1f)
            {
                cauldron.MasterWillFocus = cauldron.WildWillFrequency;
                RefinementSystem.Tick(world, 0.1f);
            }
            Assert.Equal(RefinementPhase.Phase3_Condensation, cauldron.CurrentPhase);

            TickFor(world, 0.5f);
            RefinementSystem.CrushStone(world);
            Assert.Equal(1, cauldron.StonesCrushed);

            TickFor(world, 1.0f);
            RefinementSystem.CrushStone(world);
            TickFor(world, 1.0f);
            RefinementSystem.CrushStone(world);
            RefinementSystem.CrushStone(world);
            TickFor(world, 1.1f);

            Assert.Equal(RefinementPhase.Success, cauldron.CurrentPhase);
            Assert.Null(GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_white_boar_r1"));
            var resultId = GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_moonglow_r2");
            Assert.NotNull(resultId);
            Assert.Equal(2, world.Get<GuWormComponent>(resultId!.Value)!.Rank);
        }

        [Fact]
        public void VitalGuImmunity_OnCatastrophicBacklash_EntersTraumaSleepInsteadOfPerishing()
        {
            var world = WorldFactory.CreateDefault();
            var cauldron = RefinementSystem.GetOrCreateSession(world);
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;

            cauldron.IsActive = true;
            cauldron.CurrentPhase = RefinementPhase.Phase2_WillFusion;
            cauldron.WildWillResistance = 99f;
            cauldron.MasterWillFocus = 0.1f;
            cauldron.WildWillFrequency = 0.8f;

            RefinementSystem.Tick(world, 0.2f);
            Assert.Equal(RefinementPhase.Failure, cauldron.CurrentPhase);
            Assert.Equal(RefinementFailureTier.Tier3_CatastrophicBacklash, cauldron.LastFailureTier);

            Assert.Null(GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_white_boar_r1"));
            var vitalId = GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_moonlight_r1");
            Assert.NotNull(vitalId);
            var vitalGu = world.Get<GuWormComponent>(vitalId!.Value)!;
            Assert.True(vitalGu.IsInTraumaSleep);
            Assert.True(vitalGu.Satiety <= 0.01f);
            Assert.True(ap.CurrentEssenceSea <= 0.01f);
        }

        [Fact]
        public void Determinism_And_SimulationLoopDeltaClamp_PreventHitchSkipping()
        {
            WorldStore CreateInPhase1(int seed)
            {
                var w = WorldFactory.CreateDefault(runSeed: seed);
                var c = RefinementSystem.GetOrCreateSession(w);
                c.IsActive = true;
                c.CurrentPhase = RefinementPhase.Phase1_Extraction;
                c.CauldronTemperature = 50f;
                c.Phase1Timer = 3.95f;
                return w;
            }

            var w1 = CreateInPhase1(1234);
            var w2 = CreateInPhase1(1234);
            var w3 = CreateInPhase1(9999);

            RefinementSystem.Tick(w1, 0.1f);
            RefinementSystem.Tick(w2, 0.1f);
            RefinementSystem.Tick(w3, 0.1f);

            Assert.Equal(RefinementSystem.GetOrCreateSession(w1).WildWillFrequency,
                         RefinementSystem.GetOrCreateSession(w2).WildWillFrequency);
            Assert.NotEqual(RefinementSystem.GetOrCreateSession(w1).WildWillFrequency,
                            RefinementSystem.GetOrCreateSession(w3).WildWillFrequency);

            // Hitch-sized 10s frame through SimulationLoop.Tick is clamped to 0.1s
            var hitchWorld = WorldFactory.CreateDefault();
            var hitchCauldron = RefinementSystem.GetOrCreateSession(hitchWorld);
            hitchCauldron.IsActive = true;
            hitchCauldron.CurrentPhase = RefinementPhase.Phase2_WillFusion;
            hitchCauldron.Phase2Timer = 0.6f;

            SimulationLoop.Tick(hitchWorld, 10f);
            Assert.Equal(RefinementPhase.Phase2_WillFusion, hitchCauldron.CurrentPhase);
            Assert.True(hitchCauldron.Phase2Timer <= 0.71f);
        }
    }
}
