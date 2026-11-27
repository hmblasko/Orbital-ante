using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    [Serializable]
    public sealed class SeaFieldRules
    {
        /// <summary>Untick to leave the seas empty.</summary>
        public bool creaturesInTheSeas = true;
        /// <summary>One haunt for every this many square metres of the whole world (sea or not; places on land are thrown away).</summary>
        public float areaPerHaunt = 450000f;
        public int leastHaunts = 1, mostHaunts = 30;
        /// <summary>A big swimmer only lives over water at least this deep, in metres.</summary>
        public float minDepth = 16f;
        /// <summary>No two haunts nearer than this, in metres.</summary>
        public float spacing = 240f;
        /// <summary>A haunt's animal is there when the player is this near (metres), and put away beyond the second.</summary>
        public float wakeRange = 170f, sleepRange = 260f;
        /// <summary>The most big swimmers present at once. The Steam Deck's limit, not the design's.</summary>
        public int liveCap = 2;
    }

    public struct SeaHaunt
    {
        /// <summary>Which way from the middle of the world the haunt is (length 1).</summary>
        public Vec3d Direction;
        /// <summary>How far under the surface of the sea its animal lives, metres.</summary>
        public double Under;
        /// <summary>How deep the sea is there, metres.</summary>
        public double Depth;
        public int Seed;
    }

    /// <summary>
    /// Where the big swimmers live on a world with a sea: seeded places over deep water, well apart, the same for a
    /// seed every time. Plain C#, no Unity types.
    /// </summary>
    public static class SeaField
    {
        public static int HauntCount(SeaFieldRules rules, double radius)
        {
            double area = 4 * Math.PI * radius * radius;
            int wanted = (int)Math.Round(area / Math.Max(1000f, rules.areaPerHaunt));
            return Math.Max(rules.leastHaunts, Math.Min(rules.mostHaunts, wanted));
        }

        /// <summary>How far under the surface an animal lives over water of a depth: about the middle, never too near the top or the bottom.</summary>
        public static double UnderFor(double depth) => Math.Max(5.0, Math.Min(depth * 0.5, 14.0));

        /// <param name="radius">The radius of the sea's surface, metres.</param>
        /// <param name="depthAt">How deep the sea is in a direction, metres (0 or less: land).</param>
        public static List<SeaHaunt> Build(SeaFieldRules rules, SeededRandom random, double radius, Func<Vec3d, double> depthAt)
        {
            var haunts = new List<SeaHaunt>();
            if (rules == null || random == null || depthAt == null || radius <= 0) return haunts;
            int count = HauntCount(rules, radius);
            // On a small world the spacing asked for may not fit: never more than a third of the way round.
            double spacing = Math.Min(rules.spacing, radius * Math.PI / 3) / radius;
            int tries = count * 40;
            while (haunts.Count < count && tries-- > 0)
            {
                Vec3d dir = RandomDirection(random);
                int seed = random.Next();
                double depth = depthAt(dir);
                if (depth < rules.minDepth) continue;
                bool crowded = false;
                for (int i = 0; i < haunts.Count && !crowded; i++) crowded = Angle(dir, haunts[i].Direction) < spacing;
                if (crowded) continue;
                haunts.Add(new SeaHaunt { Direction = dir, Depth = depth, Under = UnderFor(depth), Seed = seed });
            }
            return haunts;
        }

        static double Angle(Vec3d a, Vec3d b) => Math.Acos(Math.Max(-1, Math.Min(1, Vec3d.Dot(a, b))));

        static Vec3d RandomDirection(SeededRandom random)
        {
            // Even over the ball: height even, angle even.
            double y = random.NextDouble() * 2 - 1, a = random.NextDouble() * Math.PI * 2, r = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
            return new Vec3d(r * Math.Cos(a), y, r * Math.Sin(a));
        }
    }
}
