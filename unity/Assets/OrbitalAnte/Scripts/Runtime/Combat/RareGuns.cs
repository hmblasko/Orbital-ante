using System;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// The rules for rare guns (plain C#, no engine types):
    /// - A gun with a backfire chance blows up on a shot when a fair roll from 0 to 1 comes in under it. Each shot
    ///   is its own roll: nothing builds up, nothing is owed, so 1 in 1,000 means exactly that every time.
    /// - Where they lie: at most one per world, at the first of its bases whose seed says so (about one base in
    ///   <see cref="BasesPerGun"/> qualifies, so a world with few bases often has none). The same seed always
    ///   gives the same base, and a gun taken from it never comes back that run.
    /// </summary>
    public static class RareGuns
    {
        /// <summary>About one base in this many holds the rare gun at its first loot point.</summary>
        public const int BasesPerGun = 6;

        /// <summary>True when a shot with this roll (0 to 1) blows the gun up.</summary>
        public static bool Backfires(double roll, float chance)
        {
            if (!(chance > 0f) || double.IsNaN(roll)) return false;
            return roll < Math.Min(1f, chance);
        }

        /// <summary>The chance that at least one of this many shots blows up, for the readout.</summary>
        public static double ChanceWithin(int shots, float chance)
        {
            if (shots <= 0 || !(chance > 0f)) return 0;
            return 1.0 - Math.Pow(1.0 - Math.Min(1f, chance), shots);
        }

        /// <summary>"1 in 1,000" for 0.001: the chance in words, rounded to a whole number of shots.</summary>
        public static string OneIn(float chance)
        {
            if (!(chance > 0f)) return "never";
            double n = Math.Round(1.0 / Math.Min(1f, chance));
            return "1 in " + n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The one base on a world that holds the rare gun: the first whose seed qualifies, or -1 for none.</summary>
        public static int PickBase(System.Collections.Generic.IList<int> baseSeeds)
        {
            if (baseSeeds == null) return -1;
            for (int i = 0; i < baseSeeds.Count; i++) if (BaseHoldsOne(baseSeeds[i])) return i;
            return -1;
        }

        /// <summary>Whether the base with this seed could hold the rare gun. Spread evenly over seeds, never random at runtime.</summary>
        public static bool BaseHoldsOne(int baseSeed)
        {
            // A mixing step so neighbouring seeds do not fall into a pattern.
            uint h = (uint)baseSeed * 2246822519u;
            h ^= h >> 15;
            h *= 3266489917u;
            h ^= h >> 13;
            return h % BasesPerGun == 0;
        }
    }
}
