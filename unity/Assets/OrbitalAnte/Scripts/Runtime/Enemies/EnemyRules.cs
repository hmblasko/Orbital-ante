using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Enemies
{
    /// <summary>
    /// One kind of enemy: how big, how quick, how tough, what it carries and how it likes to fight. Plain
    /// numbers, no engine. The names are placeholders and belong to no faction.
    /// </summary>
    [Serializable]
    public sealed class EnemyTypeSpec
    {
        public string id = "Trooper";
        /// <summary>Height against the ordinary enemy (1).</summary>
        public float scale = 1f;
        /// <summary>Walking and running speed against the ordinary enemy.</summary>
        public float speed = 1f;
        /// <summary>
        /// How much the torso and head take against the ordinary enemy. Limbs stay easy to shoot off on every
        /// type (never inflated health: a tough enemy is tough in the middle, and still loses arms).
        /// </summary>
        public float toughness = 1f;
        /// <summary>The gun it carries (a weapon's id).</summary>
        public string weapon = "Rifle";
        public float engageRange = 45f;
        /// <summary>Seconds between seeing the player and the first shot. This is the tell: never instant.</summary>
        public float reactionTime = 0.6f;
        /// <summary>Degrees of aim error on top of the gun's own.</summary>
        public float aimError = 3f;
        public float keepFar = 18f, keepNear = 7f;
        public bool rushes, holdsStill;
        /// <summary>How often this type turns up, against the others.</summary>
        public float share = 1f;
    }

    /// <summary>The dials of how thickly the worlds are populated.</summary>
    [Serializable]
    public sealed class EnemyFieldRules
    {
        /// <summary>One camp for every this many square metres of ground.</summary>
        public float areaPerCamp = 14000f;
        public int leastCamps = 4, mostCamps = 70;
        public int squadLeast = 3, squadMost = 5;
        /// <summary>How far from its camp a squad strolls, in metres.</summary>
        public float roam = 14f;
        /// <summary>No camp nearer than this to the launch pad, in metres over the ground.</summary>
        public float padClear = 170f;
        /// <summary>No two camps nearer than this, in metres over the ground.</summary>
        public float campSpacing = 70f;
        /// <summary>A camp wakes when the player is this near (metres over the ground) and sleeps beyond the second.</summary>
        public float wakeRange = 150f, sleepRange = 230f;
        /// <summary>The most enemies alive at once. The Steam Deck's limit, not the design's.</summary>
        public int liveCap = 12;
    }

    public struct EnemyCamp
    {
        /// <summary>Which way from the middle of the body the camp is (length 1).</summary>
        public Vec3d Direction;
        /// <summary>Index into the roster for each member.</summary>
        public int[] Members;
        public int Seed;
    }

    public static class EnemyRoster
    {
        /// <summary>The four kinds asked for on 14 Nov 2026: each with its own size, pace, toughness and gun.</summary>
        public static List<EnemyTypeSpec> Defaults()
        {
            return new List<EnemyTypeSpec>
            {
                // The ordinary one: walks, takes cover of distance, fires bursts from a rifle.
                new EnemyTypeSpec { id = "Trooper", share = 4f },
                // Small and quick: runs straight in and sprays from close up. Falls apart fast.
                new EnemyTypeSpec { id = "Runner", scale = 0.85f, speed = 1.5f, toughness = 0.7f, weapon = "Smg", engageRange = 30f,
                                    reactionTime = 0.45f, aimError = 5f, keepFar = 9f, keepNear = 3f, rushes = true, share = 3f },
                // Big and slow: walks you down with a blast that only matters close. Hard to stop in the middle.
                new EnemyTypeSpec { id = "Bruiser", scale = 1.3f, speed = 0.6f, toughness = 2.2f, weapon = "Shotgun", engageRange = 24f,
                                    reactionTime = 0.9f, aimError = 2f, keepFar = 8f, keepNear = 2f, share = 2f },
                // Tall and still: stands off and takes one slow, painful shot. The long wait before it is your warning.
                new EnemyTypeSpec { id = "Marksman", scale = 1.1f, speed = 0.9f, toughness = 0.8f, weapon = "Sniper", engageRange = 110f,
                                    reactionTime = 1.6f, aimError = 0.6f, keepFar = 200f, keepNear = 25f, holdsStill = true, share = 1f },
            };
        }

        /// <summary>Picks a type by its share. <paramref name="roll"/> is 0 to 1.</summary>
        public static int Pick(IReadOnlyList<EnemyTypeSpec> roster, double roll)
        {
            double total = 0;
            for (int i = 0; i < roster.Count; i++) total += Math.Max(0f, roster[i].share);
            if (total <= 0) return 0;
            double at = roll * total;
            for (int i = 0; i < roster.Count; i++)
            {
                at -= Math.Max(0f, roster[i].share);
                if (at < 0) return i;
            }
            return roster.Count - 1;
        }
    }

    /// <summary>
    /// Where the enemies are on one world, for one seed: camps scattered evenly over the dry ground, each a
    /// small mixed squad. The same seed always gives the same camps. Plain C#, no engine.
    /// </summary>
    public static class EnemyField
    {
        public static int CampCount(EnemyFieldRules rules, double radius)
        {
            double area = 4 * Math.PI * radius * radius;
            int wanted = (int)Math.Round(area / Math.Max(100f, rules.areaPerCamp));
            return Math.Max(rules.leastCamps, Math.Min(rules.mostCamps, wanted));
        }

        /// <param name="radius">The body's ground radius, metres.</param>
        /// <param name="dry">True if a direction is on dry ground (null: everywhere is).</param>
        /// <param name="keepClear">A direction to stay away from (the launch pad), or zero for none.</param>
        public static List<EnemyCamp> Build(EnemyFieldRules rules, IReadOnlyList<EnemyTypeSpec> roster, SeededRandom random,
                                            double radius, Func<Vec3d, bool> dry, Vec3d keepClear)
        {
            var camps = new List<EnemyCamp>();
            int count = CampCount(rules, radius);
            // On a small world the spacing asked for may not fit: never more than a third of the way round.
            double spacing = Math.Min(rules.campSpacing, radius * Math.PI / 3) / radius;
            double clear = keepClear.SqrMagnitude > 0.5 ? rules.padClear / radius : -1;
            int tries = count * 30;
            while (camps.Count < count && tries-- > 0)
            {
                Vec3d dir = RandomDirection(random);
                if (clear > 0 && Angle(dir, keepClear) < clear) continue;
                if (dry != null && !dry(dir)) continue;
                bool crowded = false;
                for (int i = 0; i < camps.Count && !crowded; i++) crowded = Angle(dir, camps[i].Direction) < spacing;
                if (crowded) continue;

                int size = random.Range(rules.squadLeast, Math.Max(rules.squadLeast, rules.squadMost) + 1);
                var members = new int[size];
                for (int m = 0; m < size; m++) members[m] = EnemyRoster.Pick(roster, random.NextDouble());
                camps.Add(new EnemyCamp { Direction = dir, Members = members, Seed = random.Next() });
            }
            return camps;
        }

        /// <summary>Metres over the ground between two directions on a ball of this radius.</summary>
        public static double GroundDistance(Vec3d a, Vec3d b, double radius) => Angle(a, b) * radius;

        static double Angle(Vec3d a, Vec3d b) => Math.Acos(Math.Max(-1, Math.Min(1, Vec3d.Dot(a, b))));

        static Vec3d RandomDirection(SeededRandom random)
        {
            // Even over the ball: height is even, and so is the angle round.
            double y = random.NextDouble() * 2 - 1;
            double around = random.NextDouble() * Math.PI * 2;
            double r = Math.Sqrt(Math.Max(0, 1 - y * y));
            return new Vec3d(r * Math.Cos(around), y, r * Math.Sin(around));
        }
    }
}
