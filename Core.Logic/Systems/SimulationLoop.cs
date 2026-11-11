using System;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Unified simulation tick runner for active timed ECS minigame sessions.
    /// </summary>
    public static class SimulationLoop
    {
        public const float MAX_DELTA = 0.1f;

        public static void Tick(WorldStore world, float deltaTime, IRandomSource? rngOverride = null)
        {
            float dt = MathF.Min(deltaTime, MAX_DELTA);
            if (dt <= 0f) return;

            SaveSystem.TickPlaytime(dt);
            RefinementSystem.Tick(world, dt, rngOverride);
            BreakthroughSystem.Tick(world, dt);
            WildEncounterSystem.Tick(world, dt);
        }
    }
}
