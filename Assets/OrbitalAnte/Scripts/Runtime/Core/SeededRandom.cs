using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Small deterministic RNG (SplitMix64). Same seed gives the same sequence on every
    /// platform, unlike UnityEngine.Random whose state is global. Milestone 4 builds the
    /// full seed pipeline on top of this.
    /// </summary>
    public sealed class SeededRandom
    {
        ulong state;

        public SeededRandom(ulong seed) { state = seed; }

        public ulong NextULong()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) * (1f / (1 << 24));

        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextULong() % (ulong)(maxExclusive - min));
        }

        public Vector2 InsideUnitCircle()
        {
            float a = NextFloat() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(NextFloat());
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }
    }
}
