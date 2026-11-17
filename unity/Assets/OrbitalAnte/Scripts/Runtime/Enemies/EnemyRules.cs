using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;

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
        /// <summary>0 to 1, how far it drops into the tactical hunch while fighting (knees bent, low, gun up). The big ones walk you down more upright.</summary>
        public float hunch = 1f;
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
        /// <summary>How often this type turns up, against the others of its faction.</summary>
        public float share = 1f;
        /// <summary>The faction it belongs to (<see cref="Factions"/>; empty: none). A camp is all one faction.</summary>
        public string faction = Factions.None;
        /// <summary>What it does that nobody else does (Faction A's tiers).</summary>
        public FactionAbility ability = FactionAbility.None;
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
        /// <summary>For a camp inside a cave network: the network, and the room it lives in. Null for any other camp.</summary>
        public CaveNetwork Cave;
        public int Room;
        /// <summary>The faction every member belongs to (empty: none).</summary>
        public string Faction;
    }

    public static class EnemyRoster
    {
        /// <summary>Raise this when the defaults change, so a config asset made earlier takes the new roster.</summary>
        public const int Version = 4;

        /// <summary>Faction A's three tiers (Milestone 13), by id.</summary>
        public const string ScoutA = "ScoutA", LineA = "LineA", HeavyA = "HeavyA";

        public static List<EnemyTypeSpec> Defaults()
        {
            return new List<EnemyTypeSpec>
            {
                // The ordinary one: walks, takes cover of distance, fires bursts from a rifle.
                new EnemyTypeSpec { id = "Trooper", share = 4f },
                // Small and quick: runs straight in and sprays from close up. Falls apart fast.
                new EnemyTypeSpec { id = "Runner", model = "EnemyRunner", scale = 0.85f, speed = 1.5f, toughness = 0.7f, weapon = "Smg", engageRange = 30f,
                                    reactionTime = 0.45f, aimError = 5f, keepFar = 9f, keepNear = 3f, rushes = true, closesIn = true, hunch = 0.8f, share = 3f },
                // Big and slow: walks you down with a blast that only matters close. Hard to stop in the middle.
                new EnemyTypeSpec { id = "Bruiser", model = "EnemyBruiser", scale = 1.3f, speed = 0.8f, toughness = 2.2f, headToughness = 1.5f, weapon = "Shotgun", engageRange = 20f,
                                    reactionTime = 0.9f, aimError = 2f, keepFar = 10f, keepNear = 2f, closesIn = true, hunch = 0.35f, share = 2f, atMediumBase = 1, atLargeBase = 2, notAtSmallBase = true },
                // Tall, thin and still: stands off and takes one slow, painful shot. A bright line shows where it is
                // aiming for a second before every shot, and there is never more than one in a camp.
                new EnemyTypeSpec { id = "Marksman", model = "EnemyMarksman", scale = 1.2f, speed = 0.9f, toughness = 0.8f, weapon = "Sniper", engageRange = 110f,
                                    reactionTime = 1.6f, aimError = 0.6f, keepFar = 200f, keepNear = 25f, holdsStill = true, hunch = 0.3f, share = 1f,
                                    aimTell = 1f, mostPerCamp = 1, atLargeBase = 1, notAtSmallBase = true },

                // Faction A (Soviet-spacesuit inspired; placeholder, no name). Three tiers of clearly different
                // size, each with one thing it does that the others do not. Limbs come off as easily as on
                // anyone: the tiers differ in size, gun, ability and how they fight, never in limb health.
                // They wear the unaligned kinds' bodies as stand-ins until their own suits are modelled.

                // Scout: small and quick, goes round the side first, and signals: on first sight it raises its
                // free arm (1 s, the hand glows) and fires a flare that rouses its own side near by. Take the hand,
                // the forearm or the gun, or the Scout, during the raise and no flare goes up.
                new EnemyTypeSpec { id = ScoutA, faction = Factions.A, ability = FactionAbility.Flare, model = "EnemyRunner", scale = 0.85f, speed = 1.4f,
                                    toughness = 0.7f, weapon = "SmgA", engageRange = 32f, reactionTime = 0.5f, aimError = 4f, keepFar = 10f, keepNear = 4f,
                                    rushes = true, closesIn = true, hunch = 0.8f, share = 3f, mostPerCamp = 2 },
                // Line: the backbone. Fights from cover with a rifle, and while a squadmate is round the side it
                // lays down long, wide covering fire: the long burst means someone is coming.
                new EnemyTypeSpec { id = LineA, faction = Factions.A, ability = FactionAbility.Suppress, weapon = "RifleA", share = 4f },
                // Heavy: big and planted. Before every burst it braces and spins its gun up for 1.2 s (the cell
                // glows brighter, it whines), then fires for 3 s, turning no faster than 20 degrees a second (circle it from close in, or break line of sight).
                // Circle it, take a leg (it drops and the spin stops), or shoot the gun out of its hands.
                new EnemyTypeSpec { id = HeavyA, faction = Factions.A, ability = FactionAbility.SpinUp, model = "EnemyBruiser", scale = 1.3f, speed = 0.75f,
                                    toughness = 2f, headToughness = 1.4f, weapon = "HeavyGunA", engageRange = 40f, reactionTime = 0.9f, aimError = 2.5f,
                                    keepFar = 32f, keepNear = 6f, closesIn = true, hunch = 0.35f, share = 1f, mostPerCamp = 1,
                                    atMediumBase = 1, atLargeBase = 2, notAtSmallBase = true },
            };
        }

        /// <summary>The first type of a faction: the one that stands in when a type is past its limit in a camp.</summary>
        public static int FirstOf(IReadOnlyList<EnemyTypeSpec> roster, string faction)
        {
            // The faction's commonest type, so a camp past a limit is filled with its backbone.
            int best = -1;
            for (int i = 0; i < roster.Count; i++)
            {
                if (!Factions.Same(roster[i].faction, faction) || roster[i].mostPerCamp > 0 || roster[i].notAtSmallBase) continue;
                if (best < 0 || roster[i].share > roster[best].share) best = i;
            }
            if (best >= 0) return best;
            for (int i = 0; i < roster.Count; i++) if (Factions.Same(roster[i].faction, faction)) return i;
            return 0;
        }

        /// <summary>True if the roster has any type of this faction.</summary>
        public static bool Has(IReadOnlyList<EnemyTypeSpec> roster, string faction)
        {
            for (int i = 0; i < roster.Count; i++) if (Factions.Same(roster[i].faction, faction)) return true;
            return false;
        }

        /// <summary>Picks a type of one faction by its share. <paramref name="roll"/> is 0 to 1.</summary>
        public static int Pick(IReadOnlyList<EnemyTypeSpec> roster, double roll, string faction)
        {
            double total = 0;
            for (int i = 0; i < roster.Count; i++) if (Factions.Same(roster[i].faction, faction)) total += Math.Max(0f, roster[i].share);
            if (total <= 0) return FirstOf(roster, faction);
            double at = roll * total;
            int last = -1;
            for (int i = 0; i < roster.Count; i++)
            {
                if (!Factions.Same(roster[i].faction, faction)) continue;
                last = i;
                at -= Math.Max(0f, roster[i].share);
                if (at < 0) return i;
            }
            return last >= 0 ? last : 0;
        }

        /// <summary>
        /// Which faction a camp on this world belongs to. Every camp on a faction's home world is the faction's;
        /// elsewhere a seeded share are. With no faction rules (or a roster with no faction types) nothing is
        /// drawn at all, so worlds stay exactly as they were.
        /// </summary>
        public static string CampFaction(IReadOnlyList<EnemyTypeSpec> roster, FactionFieldRules factions, string bodyId, SeededRandom random)
        {
            if (factions == null || !Has(roster, Factions.A)) return Factions.None;
            if (!string.IsNullOrEmpty(bodyId) && bodyId == factions.homeA) return Factions.A;
            return random.NextDouble() < factions.shareA ? Factions.A : Factions.None;
        }

        /// <summary>Fills a camp of one faction, keeping each type within its per-camp limit.</summary>
        internal static void Fill(int[] members, int from, IReadOnlyList<EnemyTypeSpec> roster, SeededRandom random, string faction, bool smallBase)
        {
            int stand = FirstOf(roster, faction);
            for (int m = from; m < members.Length; m++)
            {
                int type = Pick(roster, random.NextDouble(), faction);
                // Some types are only fair in ones (the Marksman, the Heavy): past the limit the faction's backbone stands in.
                int most = roster[type].mostPerCamp;
                if (most > 0)
                {
                    int already = 0;
                    for (int k = 0; k < m; k++) if (members[k] == type) already++;
                    if (already >= most) type = stand;
                }
                if (smallBase && roster[type].notAtSmallBase) type = stand;
                members[m] = type;
            }
        }

        /// <summary>Picks an unaligned type by its share. <paramref name="roll"/> is 0 to 1.</summary>
        public static int Pick(IReadOnlyList<EnemyTypeSpec> roster, double roll) => Pick(roster, roll, Factions.None);
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
        /// <param name="factions">Where the factions live (null: every camp unaligned, as before factions).</param>
        /// <param name="bodyId">The world's id, to know a faction's home.</param>
        public static List<EnemyCamp> Build(EnemyFieldRules rules, IReadOnlyList<EnemyTypeSpec> roster, SeededRandom random,
                                            double radius, Func<Vec3d, bool> dry, Vec3d keepClear,
                                            FactionFieldRules factions = null, string bodyId = null)
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
                string faction = EnemyRoster.CampFaction(roster, factions, bodyId, random);
                EnemyRoster.Fill(members, 0, roster, random, faction, false);
                camps.Add(new EnemyCamp { Direction = dir, Members = members, Seed = random.Next(), Faction = faction });
            }
            return camps;
        }

        /// <summary>
        /// Puts guards at a base: a squad of the given size standing on its floor. Any ordinary camp that would
        /// crowd it is taken away, so a base is one fight and not two on top of each other.
        /// </summary>
        public static void AddGarrison(List<EnemyCamp> camps, EnemyFieldRules rules, IReadOnlyList<EnemyTypeSpec> roster, SeededRandom random,
                                       double radius, Vec3d direction, double homeRadius, int guards, int baseSize = 0,
                                       FactionFieldRules factions = null, string bodyId = null)
        {
            double spacing = Math.Min(rules.campSpacing, radius * Math.PI / 3) / radius;
            for (int i = camps.Count - 1; i >= 0; i--)
            {
                if (camps[i].HomeRadius <= 0 && Angle(camps[i].Direction, direction) < spacing) camps.RemoveAt(i);
            }
            var members = new int[Math.Max(1, guards)];
            string faction = EnemyRoster.CampFaction(roster, factions, bodyId, random);
            // The ones a base of this size always has come first; the rest are drawn.
            int placed = 0;
            for (int t = 0; t < roster.Count && baseSize > 0; t++)
            {
                if (!Factions.Same(roster[t].faction, faction)) continue;
                int always = baseSize >= 2 ? roster[t].atLargeBase : roster[t].atMediumBase;
                for (int k = 0; k < always && placed < members.Length; k++) members[placed++] = t;
            }
            // Past a type's limit or at a small base: the faction's backbone stands in. The "always" guards are
            // already counted, so a drawn one never takes a type over its limit.
            EnemyRoster.Fill(members, placed, roster, random, faction, baseSize == 0);
            camps.Add(new EnemyCamp { Direction = direction.Normalized, Members = members, Seed = random.Next(), HomeRadius = homeRadius, Faction = faction });
        }

        /// <summary>
        /// Camps in the rooms of a cave network that the network marks as lived in (Matthew, 16 Nov 2026: caves
        /// "where enemies will reside"): two to four in a room, five in a great hall, by the light of their lamps.
        /// Their home is the middle of the room's floor; they never wander out of it on their own.
        /// </summary>
        public static void AddCaveCamps(List<EnemyCamp> camps, IReadOnlyList<EnemyTypeSpec> roster, SeededRandom random, CaveNetwork network,
                                        FactionFieldRules factions = null, string bodyId = null)
        {
            if (network == null) return;
            for (int room = 0; room < network.Chambers.Count; room++)
            {
                CaveChamber c = network.Chambers[room];
                if (!c.Camp) continue;
                int size = c.LongRadius > 11 ? random.Range(4, 6) : random.Range(2, 5);
                var members = new int[size];
                string faction = EnemyRoster.CampFaction(roster, factions, bodyId, random);
                EnemyRoster.Fill(members, 0, roster, random, faction, false);
                Vec3d floor = c.Floor;
                camps.Add(new EnemyCamp
                {
                    Direction = floor.Normalized, Members = members, Seed = random.Next(), HomeRadius = floor.Magnitude,
                    Cave = network, Room = room, Faction = faction,
                });
            }
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
