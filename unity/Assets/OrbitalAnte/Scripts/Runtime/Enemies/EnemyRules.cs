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
        /// <summary>The body model it is drawn with (empty: the ordinary one).</summary>
        public string model = "";
        /// <summary>Height against the ordinary enemy (1).</summary>
        public float scale = 1f;
        /// <summary>Walking and running speed against the ordinary enemy.</summary>
        public float speed = 1f;
        /// <summary>
        /// How much the torso and head take against the ordinary enemy. Limbs stay easy to shoot off on every
        /// type (never inflated health: a tough enemy is tough in the middle, and still loses arms).
        /// </summary>
        public float toughness = 1f;
        /// <summary>How tough the head is. 0 means the same as <see cref="toughness"/>. Kept lower on a tough body so ending it is never a slog.</summary>
        public float headToughness;
        /// <summary>The gun it carries (a weapon's id).</summary>
        public string weapon = "Rifle";
        public float engageRange = 45f;
        /// <summary>Seconds between seeing the player and the first shot. This is the tell: never instant.</summary>
        public float reactionTime = 0.6f;
        /// <summary>Degrees of aim error on top of the gun's own.</summary>
        public float aimError = 3f;
        public float keepFar = 18f, keepNear = 7f;
        public bool rushes, holdsStill;
        /// <summary>Keeps walking at the player until it is inside <see cref="keepFar"/>, instead of side-stepping at range.</summary>
        public bool closesIn;
        /// <summary>Width against height (1: as modelled). Only for a kind with no body model of its own.</summary>
        public float width = 1f;
        /// <summary>The colour of its chest on the stand-in shapes, 0 to 1 each. All zero: the ordinary colour.</summary>
        public float red, green, blue;
        /// <summary>Seconds a bright line shows where it is aiming before every shot (0: none). Only for slow, heavy single shots.</summary>
        public float aimTell;
        /// <summary>The most of this type in one camp (0: no limit).</summary>
        public int mostPerCamp;
        /// <summary>How many of this type always stand guard at a medium and at a large base. A bigger base is a different fight, not tougher enemies.</summary>
        public int atMediumBase, atLargeBase;
        /// <summary>Never among the drawn guards of a small base (the heavy types are kept for the bigger ones).</summary>
        public bool notAtSmallBase;
        /// <summary>How often this type turns up, against the others.</summary>
        public float share = 1f;
    }

    /// <summary>The dials of how thickly the worlds are populated.</summary>
    [Serializable]
    public sealed class EnemyFieldRules
    {
        /// <summary>One camp for every this many square metres of ground.</summary>
        public float areaPerCamp = 14000f;
        public int leastCamps = 1, mostCamps = 120;
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
        /// <summary>How far from the body's centre the camp stands, metres. 0: on the ground (a base's guards stand on its floor).</summary>
        public double HomeRadius;
    }

    public static class EnemyRoster
    {
        /// <summary>The four kinds asked for on 14 Nov 2026: each with its own size, pace, toughness and gun.</summary>
        /// <summary>Raise this when the defaults change, so a config asset made earlier takes the new roster.</summary>
        public const int Version = 2;

        public static List<EnemyTypeSpec> Defaults()
        {
            return new List<EnemyTypeSpec>
            {
                // The ordinary one: walks, takes cover of distance, fires bursts from a rifle.
                new EnemyTypeSpec { id = "Trooper", share = 4f },
                // Small and quick: runs straight in and sprays from close up. Falls apart fast.
                new EnemyTypeSpec { id = "Runner", model = "EnemyRunner", scale = 0.85f, speed = 1.5f, toughness = 0.7f, weapon = "Smg", engageRange = 30f,
                                    reactionTime = 0.45f, aimError = 5f, keepFar = 9f, keepNear = 3f, rushes = true, closesIn = true, share = 3f },
                // Big and slow: walks you down with a blast that only matters close. Hard to stop in the middle.
                new EnemyTypeSpec { id = "Bruiser", model = "EnemyBruiser", scale = 1.3f, speed = 0.8f, toughness = 2.2f, headToughness = 1.5f, weapon = "Shotgun", engageRange = 20f,
                                    reactionTime = 0.9f, aimError = 2f, keepFar = 10f, keepNear = 2f, closesIn = true, share = 2f, atMediumBase = 1, atLargeBase = 2, notAtSmallBase = true },
                // Tall, thin and still: stands off and takes one slow, painful shot. A bright line shows where it is
                // aiming for a second before every shot, and there is never more than one in a camp.
                new EnemyTypeSpec { id = "Marksman", model = "EnemyMarksman", scale = 1.2f, speed = 0.9f, toughness = 0.8f, weapon = "Sniper", engageRange = 110f,
                                    reactionTime = 1.6f, aimError = 0.6f, keepFar = 200f, keepNear = 25f, holdsStill = true, share = 1f,
                                    aimTell = 1f, mostPerCamp = 1, atLargeBase = 1, notAtSmallBase = true },
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
                for (int m = 0; m < size; m++)
                {
                    int type = EnemyRoster.Pick(roster, random.NextDouble());
                    // Some types are only fair in ones (the Marksman): past its limit the ordinary type stands in.
                    int most = roster[type].mostPerCamp;
                    if (most > 0)
                    {
                        int already = 0;
                        for (int k = 0; k < m; k++) if (members[k] == type) already++;
                        if (already >= most) type = 0;
                    }
                    members[m] = type;
                }
                camps.Add(new EnemyCamp { Direction = dir, Members = members, Seed = random.Next() });
            }
            return camps;
        }

        /// <summary>
        /// Puts guards at a base: a squad of the given size standing on its floor. Any ordinary camp that would
        /// crowd it is taken away, so a base is one fight and not two on top of each other.
        /// </summary>
        public static void AddGarrison(List<EnemyCamp> camps, EnemyFieldRules rules, IReadOnlyList<EnemyTypeSpec> roster, SeededRandom random,
                                       double radius, Vec3d direction, double homeRadius, int guards, int baseSize = 0)
        {
            double spacing = Math.Min(rules.campSpacing, radius * Math.PI / 3) / radius;
            for (int i = camps.Count - 1; i >= 0; i--)
            {
                if (camps[i].HomeRadius <= 0 && Angle(camps[i].Direction, direction) < spacing) camps.RemoveAt(i);
            }
            var members = new int[Math.Max(1, guards)];
            // The ones a base of this size always has come first; the rest are drawn.
            int placed = 0;
            for (int t = 0; t < roster.Count && baseSize > 0; t++)
            {
                int always = baseSize >= 2 ? roster[t].atLargeBase : roster[t].atMediumBase;
                for (int k = 0; k < always && placed < members.Length; k++) members[placed++] = t;
            }
            for (int m = placed; m < members.Length; m++)
            {
                int type = EnemyRoster.Pick(roster, random.NextDouble());
                int most = roster[type].mostPerCamp;
                if (most > 0)
                {
                    int already = 0;
                    for (int k = 0; k < m; k++) if (members[k] == type) already++;
                    if (already >= most) type = 0;
                }
                if (baseSize == 0 && roster[type].notAtSmallBase) type = 0;
                members[m] = type;
            }
            camps.Add(new EnemyCamp { Direction = direction.Normalized, Members = members, Seed = random.Next(), HomeRadius = homeRadius });
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
