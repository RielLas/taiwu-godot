using System;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Single live-world seam: systems, views, and controllers read Current dynamically so loading a save
    /// is an atomic pointer swap with zero stale cached contexts.
    /// </summary>
    public interface IWorldStateProvider
    {
        WorldStore Current { get; }
    }

    public sealed class WorldStateProvider : IWorldStateProvider
    {
        public WorldStateProvider(WorldStore world)
        {
            Current = world ?? throw new ArgumentNullException(nameof(world));
        }

        public WorldStore Current { get; private set; }

        public void Replace(WorldStore world)
        {
            Current = world ?? throw new ArgumentNullException(nameof(world));
        }
    }

    public sealed class StaticWorldStateProvider : IWorldStateProvider
    {
        public StaticWorldStateProvider(WorldStore world)
        {
            Current = world ?? throw new ArgumentNullException(nameof(world));
        }

        public WorldStore Current { get; }
    }
}
