using System;
using TaiwuGu.Map;

namespace TaiwuGu.Core
{
    /// <summary>
    /// All simulation randomness goes through this seam. The stream is seeded from
    /// <see cref="GameStateContext.RunSeed"/> and persisted with the save, so replaying the same run
    /// (or re-running a test) produces the same rolls. <c>Random.Shared</c> cannot do that: its
    /// algorithm is not fixed across runtimes and it cannot be saved.
    /// </summary>
    public interface IRandomSource
    {
        double NextDouble();
        int Next();
        int Next(int maxExclusive);
        int Next(int minInclusive, int maxExclusive);
    }

    /// <summary>Xorshift32-backed source; see <see cref="DeterministicRandom"/> for the algorithm rationale.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        private readonly DeterministicRandom _rng;

        public SeededRandom(int seed)
        {
            _rng = new DeterministicRandom(seed);
        }

        public double NextDouble() => _rng.NextFloat();

        public int Next() => (int)(_rng.NextUInt() & 0x7FFFFFFF);

        public int Next(int maxExclusive) => _rng.NextInt(maxExclusive);

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + _rng.NextInt(maxExclusive - minInclusive);
        }
    }
}
