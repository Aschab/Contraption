using System.Collections.Generic;

namespace Contraption.Core
{
    // SplitMix64. Deterministic across platforms; the only RNG allowed in gameplay code.
    public sealed class Rng
    {
        private ulong _state;

        public Rng(ulong seed) => _state = seed;

        public ulong NextULong()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextULong() % (ulong)(maxExclusive - minInclusive));
        }

        public int RangeInclusive(int min, int max) => Range(min, max + 1);

        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public bool Chance(double p) => NextDouble() < p;

        public int Sign() => Chance(0.5) ? 1 : -1;

        public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
