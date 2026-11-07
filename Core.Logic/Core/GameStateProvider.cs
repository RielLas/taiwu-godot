using System;

namespace TaiwuGu.Core
{
    /// <summary>
    /// The single live-state seam (Phase 3a).
    ///
    /// Domain services must never cache a <see cref="GameStateContext"/>: they read
    /// <see cref="Current"/> on every call, so loading a save can replace the whole context
    /// atomically while every service keeps ticking against the new truth.
    /// </summary>
    public interface IGameStateProvider
    {
        GameStateContext Current { get; }
    }

    /// <summary>
    /// Owns the one live context. <see cref="Replace"/> is the only way a loaded save becomes live,
    /// which is what makes load atomic: nothing mutates the previous context while it is parsed.
    /// </summary>
    public sealed class GameStateProvider : IGameStateProvider
    {
        public GameStateProvider(GameStateContext context)
        {
            Current = context ?? throw new ArgumentNullException(nameof(context));
        }

        public GameStateContext Current { get; private set; }

        public void Replace(GameStateContext context)
        {
            Current = context ?? throw new ArgumentNullException(nameof(context));
        }
    }

    /// <summary>
    /// Fixed provider for unit tests and tooling that constructs one context and never swaps it.
    /// </summary>
    public sealed class StaticGameStateProvider : IGameStateProvider
    {
        public StaticGameStateProvider(GameStateContext context)
        {
            Current = context ?? throw new ArgumentNullException(nameof(context));
        }

        public GameStateContext Current { get; }
    }
}
