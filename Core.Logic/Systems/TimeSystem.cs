using System;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing Action Points, 24 Solar Terms rollover, and universal Mortal Lifespan aging.
    /// </summary>
    public static class TimeSystem
    {
        public static bool DeductActionPoints(WorldStore world, int apCost)
        {
            var clock = world.GetSingleton<WorldClockComponent>();
            if (clock == null || apCost <= 0) return true;

            clock.ActionPointsRemaining = Math.Max(0, clock.ActionPointsRemaining - apCost);
            WorldSignals.EmitTopics(DirtyTopic.Calendar);

            if (clock.ActionPointsRemaining <= 0)
            {
                RollOverMonth(world);
            }
            return true;
        }

        public static void RollOverMonth(WorldStore world, IRandomSource? rng = null)
        {
            var clock = world.GetSingleton<WorldClockComponent>();
            if (clock == null) return;

            clock.ActionPointsRemaining = clock.MonthlyAPBudget;
            clock.CurrentSolarTermIndex++;

            bool agedYear = false;
            if (clock.CurrentSolarTermIndex > WorldClockComponent.TERMS_PER_YEAR)
            {
                clock.CurrentSolarTermIndex = 1;
                clock.CurrentYear++;
                agedYear = true;

                // Canon Inviolate Mortal Lifespan Law (寿元天则): ALL cultivators age 1 year at Lichun
                foreach (var (eid, cult) in world.GetAll<CultivatorComponent>())
                {
                    if (!cult.IsAlive) continue;
                    cult.AgeOneYear();

                    if (!cult.IsAlive)
                    {
                        Debug.LogError($"[MORTALITY CEILING] {cult.Name} reached mortal lifespan ceiling ({cult.LifespanYears} yrs)!");
                    }
                }
            }

            // Advance world subsystems synchronized to Solar Terms
            CultivationSystem.RegenerateOnSolarTerm(world);
            GuEcologySystem.ProcessFeedingCycleForWorld(world);
            ForensicsSystem.AdvanceCrimeScenes(world);
            SocialVendettaSystem.TickMonthlyNPCs(world, rng);
            WorldDirectorSystem.TickSolarTerm(world, rng);

            Debug.Log($"[TimeSystem] Month Rollover -> Year {clock.CurrentYear}, {clock.ActiveSolarTerm} ({clock.GetSolarTermName()}) | AP: {clock.ActionPointsRemaining}/{clock.MonthlyAPBudget}");

            DirtyTopic dirty = DirtyTopic.Calendar | DirtyTopic.Aperture | DirtyTopic.Inventory | DirtyTopic.Forensics | DirtyTopic.NPCs;
            if (agedYear) dirty |= DirtyTopic.Cultivator;
            WorldSignals.EmitTopics(dirty);
        }
    }
}
