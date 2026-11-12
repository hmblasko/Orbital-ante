using System;
using System.Collections.Generic;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// The game's random number generator. Everything that should be the same for the same run seed draws from
    /// one of these, never from the clock or from Unity's global random.
    ///
    /// Why not System.Random: its number sequence is not guaranteed to be the same on every platform and
    /// runtime version, and a run seed must mean the same thing on a Steam Deck as on a desktop.
    ///
    /// Streams: each system asks for its own named stream ("arena.mannequins", "mars.rocks"). A stream depends
    /// only on the run seed and its name, so adding a new system, or changing how many numbers one system
    /// draws, never shifts what any other system gets.
    ///
    /// No Unity types, so it can be unit tested anywhere.
    /// </summary>
    public sealed class SeededRandom
    {
        ulong _state;
        readonly ulong _origin;

        public SeededRandom(ulong seed)
        {
            _origin = seed;
            _state = seed;
        }

        public SeededRandom(int seed) : this(Mix((ulong)(uint)seed + 0x9E3779B97F4A7C15UL))
        {
        }

        /// <summary>The stream called <paramref name="name"/> for a run seed.</summary>
        public static SeededRandom ForStream(int runSeed, string name)
        {
            return new SeededRandom(Mix((ulong)(uint)runSeed * 0xD6E8FEB86659FD93UL ^ Hash(name)));
        }

        /// <summary>
        /// A new independent generator derived from this one's starting point and a name. It does not depend on
        /// how many numbers have been drawn from this one so far.
        /// </summary>
        public SeededRandom Fork(string name)
        {
            return new SeededRandom(Mix(_origin ^ Hash(name)));
        }

        /// <summary>Same idea as <see cref="Fork(string)"/>, for numbered things (the 7th asteroid, the 3rd enemy).</summary>
        public SeededRandom Fork(int index)
        {
            return new SeededRandom(Mix(_origin ^ ((ulong)(uint)index + 1UL) * 0xA24BAED4963EE407UL));
        }

        // SplitMix64: small, fast, well tested, and identical everywhere because it is only integer maths.
        static ulong Mix(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        // FNV-1a, so a stream name always maps to the same number on every platform.
        static ulong Hash(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                if (text != null)
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        hash ^= text[i];
                        hash *= 1099511628211UL;
                    }
                }
                return hash;
            }
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                return Mix(_state);
            }
        }

        /// <summary>A whole number from 0 up to (not including) int.MaxValue.</summary>
        public int Next()
        {
            return (int)(NextUInt64() >> 33);
        }

        /// <summary>A whole number from <paramref name="minInclusive"/> up to (not including) <paramref name="maxExclusive"/>.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return (int)((long)minInclusive + (long)(NextUInt64() % span));
        }

        /// <summary>A number from 0 up to (not including) 1.</summary>
        public double NextDouble()
        {
            return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>A number from 0 up to (not including) 1.</summary>
        public float NextFloat()
        {
            return (NextUInt64() >> 40) * (1f / 16777216f);
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>True with the given probability (0 = never, 1 = always).</summary>
        public bool Chance(double probability)
        {
            return NextDouble() < probability;
        }

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Nothing to pick from.", nameof(items));
            return items[Range(0, items.Count)];
        }

        /// <summary>Puts a list in a random order.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            if (items == null) return;
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }
        }
    }
}
