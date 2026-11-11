using System;
using System.Linq;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class BreakthroughAndEncounterTests : IDisposable
    {
        public BreakthroughAndEncounterTests() => WorldSignals.ClearAllSubscriptions();
        public void Dispose() => WorldSignals.ClearAllSubscriptions();

        [Fact]
        public void Breakthrough_HarmonicResonanceAndRelicBypass_PromoteMajorRank()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;

            // Requires Substage 4
            Assert.False(BreakthroughSystem.OpenSession(world));

            ap.Substage = 4;
            ap.CurrentEssenceSea = cult.AptitudePercent;
            Assert.True(BreakthroughSystem.OpenSession(world));

            var session = BreakthroughSystem.GetOrCreateSession(world);
            Assert.Equal(BreakthroughState.PoundingInSession, session.State);
            Assert.Equal(100f, session.MembraneCurrentHP);

            // Harmonic hit (Oscillation sine near 1.0 -> SessionTime where Sin(t * 2pi * 1.2) = 1.0)
            session.SessionTime = 1f / (4f * session.OscillationFrequency);
            bool? wasHarmonic = null;
            WorldSignals.OnBreakthroughWaveImpact += (harm, disp, dmg) => wasHarmonic = harm;

            BreakthroughSystem.ReleaseEssenceWave(world, 1.0f);
            Assert.True(wasHarmonic);
            Assert.True(session.MembraneCurrentHP < 100f);

            // Relic Gu bypass promotes Rank 1 -> Rank 2
            int resolvedRank = 0;
            WorldSignals.OnBreakthroughResolved += (st, rank) => resolvedRank = rank;
            Assert.True(BreakthroughSystem.TryUseRelicGuBypass(world));
            Assert.Equal(2, ap.Rank);
            Assert.Equal(1, ap.Substage);
            Assert.Equal(2, resolvedRank);
        }

        [Fact]
        public void Breakthrough_TurbulenceRupture_CrashesSeaAndResealsMembrane()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;

            ap.Substage = 4;
            ap.CurrentEssenceSea = 40f;
            cult.PrimevalStones = 20;

            Assert.True(BreakthroughSystem.OpenSession(world));
            var session = BreakthroughSystem.GetOrCreateSession(world);
            session.CurrentTurbulence = 95f;

            BreakthroughSystem.CrushStone(world);
            Assert.Equal(BreakthroughState.FailedRupture, session.State);
            Assert.Equal(0f, ap.CurrentEssenceSea);
            Assert.Equal(ap.MaxWallDurability, ap.ApertureWallDurability);
        }

        [Fact]
        public void WildEncounter_RankDifferentialWindows_AndInstantOverwrite()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;

            ap.Rank = 3;
            var docOrganism = new WildOrganismData("gu_test_r1", "Test Moonlight Gu", 1, GuSlotCategory.Attack, GuDietType.MoonOrchidPetals, 10f, WildOrganismTemperament.Docile, "Lore");
            WildEncounterSystem.OpenEncounter(world, docOrganism);

            Assert.True(WildEncounterSystem.GetOrCreateSession(world).IsOpen);
            Assert.Equal(2, WildEncounterSystem.CalculateRankDifferential(world));
            Assert.Equal(0.35f, WildEncounterSystem.GetHarmonicTolerance(world));
            Assert.False(WildEncounterSystem.IsInstantRefineAvailable(world));

            ap.Rank = 4;
            Assert.Equal(3, WildEncounterSystem.CalculateRankDifferential(world));
            Assert.True(WildEncounterSystem.IsInstantRefineAvailable(world));

            int initialGu = world.GetOwned<GuWormComponent>(world.PlayerEntity).Count();
            WildEncounterSystem.InstantSubjugate(world);

            Assert.Equal(SubjugationState.Success, WildEncounterSystem.GetOrCreateSession(world).State);
            Assert.Equal(initialGu + 1, world.GetOwned<GuWormComponent>(world.PlayerEntity).Count());
            Assert.NotNull(GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, "gu_test_r1"));
        }

        [Fact]
        public void WildEncounter_AlignedPulse_SuppressesFeralResistance()
        {
            var world = WorldFactory.CreateDefault();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var ap = world.Get<ApertureComponent>(world.PlayerEntity)!;

            ap.Rank = 2;
            cult.AptitudePercent = 80f;
            ap.CurrentEssenceSea = 60f;

            var wildTarget = new WildOrganismData("gu_light_r2", "Little Light Gu", 2, GuSlotCategory.Movement, GuDietType.MoonOrchidPetals, 10f, WildOrganismTemperament.Docile, "Light Lore");
            WildEncounterSystem.OpenEncounter(world, wildTarget);

            var s = WildEncounterSystem.GetOrCreateSession(world);
            float initialResist = s.FeralWillResistance;

            WildEncounterSystem.SetNeedle(world, s.FeralWillFrequency);
            WildEncounterSystem.PulseEssence(world);

            Assert.True(s.FeralWillResistance < initialResist);
            Assert.True(s.PlayerWillAlignment > 0f);
        }
    }
}
