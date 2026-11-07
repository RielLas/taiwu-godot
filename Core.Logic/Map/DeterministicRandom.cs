using System;

namespace TaiwuGu.Map
{
    /// <summary>
    /// Xorshift32 PRNG.
    /// System.Random's algorithm is not guaranteed stable across .NET versions, which would silently
    /// change a sector's terrain between runtime upgrades and invalidate saved seeds. This generator
    /// is a fixed integer algorithm, so a seed always reproduces the same sector.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            // Zero is a fixed point of xorshift; substitute a non-zero constant.
            _state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }
    }
}
