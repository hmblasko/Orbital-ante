using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>The creatures that live on the ground of the worlds (the swimmers live in the seas: <see cref="SeaField"/>).</summary>
    public enum LandKind { Strider, Sprinter, Roller, Screamer }

    [Serializable]
    public sealed class LandFieldRules
    {
        /// <summary>Untick to leave the worlds' ground to the camps.</summary>
        public bool creaturesOnLand = true;
        /// <summary>One haunt for every this many square metres of the whole world (places in the sea, too steep, or too near a camp are thrown away).</summary>
        public float areaPerHaunt = 80000f;
        public int leastHaunts = 1, mostHaunts = 30;
        /// <summary>No land creatures on a world smaller than this (metres of radius): Phobos, Deimos and Enceladus are too small for a 7 m crab.</summary>
        public float leastRadius = 80f;
        /// <summary>No two haunts nearer than this, metres over the ground.</summary>
        public float spacing = 140f;
        /// <summary>No haunt nearer the launch pad than this: the first minutes of a run are the player's.</summary>
        public float padClear = 250f;
        /// <summary>No haunt nearer a camp or a base than this, so a creature's fight and a camp's never start on top of each other.</summary>
        public float campClear = 80f;
        /// <summary>A haunt only on ground no steeper than this (rise over run).</summary>
        public float hauntSlope = 0.35f;
        /// <summary>A haunt's animal is there when the player is this near (metres over the ground), and put away beyond the second.</summary>
        public float wakeRange = 110f, sleepRange = 180f;
        /// <summary>
        /// Never woken nearer the player than this (metres over the ground), so none is seen to appear beside them: an
        /// ante that starts beside a haunt it has just filled, or a landing on top of one, leaves it until they have gone.
        /// </summary>
        public float wakeNoNearer = 70f;
        /// <summary>A haunt has room to fight round it: the ground this far off (metres) is no more than roomRise above or below it.</summary>
        public float roomAround = 15f, roomRise = 5f;
        /// <summary>The most land creatures present at once. The Steam Deck's limit, not the design's.</summary>
        public int liveCap = 2;
        /// <summary>
        /// More of them each ante: this share of the haunts is lived in at the first ante, and every one from
        /// <see cref="allFromAnte"/>, in even steps between (a haunt lived in at one ante is lived in at every later one).
        /// </summary>
        public float firstAnteShare = 0.5f;
        public int allFromAnte = 3;
        /// <summary>The steepest ground each kind walks over (rise over run); steeper is a wall to it. The Roller's ball rolls up less than it walks.</summary>
        public float striderSlope = 0.6f, sprinterSlope = 0.7f, rollerSlope = 0.6f, rollSlope = 0.45f, screamerSlope = 0.7f;
        /// <summary>
        /// Something standing on the ground higher than this is a wall to a kind on a world (metres): a shin for the
        /// small ones; the Strider's body rides 2 m up on long legs, so it steps over what would stop the others.
        /// </summary>
        public float stepOver = 0.3f, striderStepOver = 1.2f;

        public float StepOverFor(LandKind kind) => kind == LandKind.Strider ? striderStepOver : stepOver;

        public float SlopeFor(LandKind kind)
        {
            switch (kind)
            {
                case LandKind.Strider: return striderSlope;
                case LandKind.Sprinter: return sprinterSlope;
                case LandKind.Roller: return rollerSlope;
                default: return screamerSlope;
            }
        }
    }

    public struct LandHaunt
    {
        /// <summary>Which way from the middle of the world the haunt is (length 1).</summary>
        public Vec3d Direction;
        public LandKind Kind;
        /// <summary>The haunt's own lot, 0 to 1, from its seed: it is lived in at an ante whose share is above it (<see cref="LandField.Inhabited"/>).</summary>
        public double Lot;
        public int Seed;
    }

    /// <summary>How often each kind turns up on a world: weights, 0 for never there.</summary>
    public struct LandMix
    {
        public int Strider, Sprinter, Roller, Screamer;

        public LandMix(int strider, int sprinter, int roller, int screamer)
        {
            Strider = strider;
            Sprinter = sprinter;
            Roller = roller;
            Screamer = screamer;
        }

        public static readonly LandMix None = new LandMix(0, 0, 0, 0);

        public int Total => Math.Max(0, Strider) + Math.Max(0, Sprinter) + Math.Max(0, Roller) + Math.Max(0, Screamer);
        public bool Any => Total > 0;

        public int Of(LandKind kind)
        {
            switch (kind)
            {
                case LandKind.Strider: return Math.Max(0, Strider);
                case LandKind.Sprinter: return Math.Max(0, Sprinter);
                case LandKind.Roller: return Math.Max(0, Roller);
                default: return Math.Max(0, Screamer);
            }
        }

        /// <summary>A haunt's kind, by the weights, from the haunt's own seed (so it draws nothing more from the world's stream).</summary>
        public LandKind Pick(int seed)
        {
            int total = Total;
            if (total <= 0) return LandKind.Roller;
            int roll = (int)(((uint)seed >> 4) % (uint)total);
            for (int k = 0; k < 4; k++)
            {
                roll -= Of((LandKind)k);
                if (roll < 0) return (LandKind)k;
            }
            return LandKind.Roller;
        }
    }

    /// <summary>
    /// Which land creatures live on which world, from where the creature brief says each animal lives: the Strider
    /// on open ground, the Sprinter in deserts, the Roller anywhere, the Screamer among trees and cliffs, and it flies
    /// and screams through the air, so only on a world with air. Each kind of world is led by its own animal, so what
    /// lives where can be learned at a glance (constitution rule 7; the design review of pass 8): the airless, cold
    /// moons by the Strider, the hot, dry worlds by the Sprinter, the worlds under air by the Screamer, and the Roller
    /// on every one, never more than a third of the haunts. Plain C#.
    /// </summary>
    public static class LandHabitats
    {
        /// <summary>
        /// A world's mix. None on the Sun or the asteroids, on the smallest moons (see <see cref="LandFieldRules.leastRadius"/>),
        /// or on Neptune, whose ground is the bottom of its ocean (its sea has the swimmers).
        /// </summary>
        public static LandMix For(string bodyId)
        {
            switch (bodyId)
            {
                // Hot and dry: the desert spiders' worlds. Mars and Venus have air, so a few cicadas too.
                case "Mercury":
                case "Io": return new LandMix(1, 3, 1, 0);
                case "Venus": return new LandMix(0, 3, 2, 1);
                case "Mars": return new LandMix(1, 3, 1, 1);
                // Airless, cold, open ground: the crabs' worlds.
                case "Moon": return new LandMix(3, 0, 1, 0);
                case "Europa":
                case "Ganymede":
                case "Callisto":
                case "Titania":
                case "Triton": return new LandMix(2, 0, 1, 0);
                // Under air: trees and cliffs on Earth, dunes and lakes on Titan, towers in the deep air inside the gas giants.
                case "Earth": return new LandMix(1, 0, 2, 3);
                case "Titan":
                case "Jupiter":
                case "Saturn":
                case "Uranus": return new LandMix(0, 0, 1, 2);
                default: return LandMix.None;
            }
        }

        /// <summary>The kind most of a mix's haunts are, or null if none leads (a tie, or no animals).</summary>
        public static LandKind? Leader(LandMix mix)
        {
            LandKind? best = null;
            int most = 0;
            bool tied = false;
            for (int k = 0; k < 4; k++)
            {
                int n = mix.Of((LandKind)k);
                if (n > most) { most = n; best = (LandKind)k; tied = false; }
                else if (n == most && n > 0) tied = true;
            }
            return tied ? null : best;
        }

        /// <summary>The model a kind is drawn with, which is also its name in its credit and its aim line.</summary>
        public static string ModelOf(LandKind kind)
        {
            switch (kind)
            {
                case LandKind.Strider: return WalkerLayout.Strider.Model;
                case LandKind.Sprinter: return WalkerLayout.Sprinter.Model;
                case LandKind.Roller: return RollerLayout.Model;
                default: return ScreamerLayout.Model;
            }
        }

        /// <summary>True for the kinds that fly, which need air to fly in.</summary>
        public static bool Flies(LandKind kind) => kind == LandKind.Screamer;
    }

    /// <summary>
    /// Where the land creatures live on one world, for one seed: haunts scattered over dry, gentle ground, well apart,
    /// clear of the launch pad, the camps and the bases; the same for a seed every time. Plain C#, no Unity types.
    /// </summary>
    public static class LandField
    {
        public static int HauntCount(LandFieldRules rules, double radius, LandMix mix)
        {
            if (rules == null || !rules.creaturesOnLand || !mix.Any || radius < rules.leastRadius) return 0;
            double area = 4 * Math.PI * radius * radius;
            int wanted = (int)Math.Round(area / Math.Max(1000f, rules.areaPerHaunt));
            return Math.Max(rules.leastHaunts, Math.Min(rules.mostHaunts, wanted));
        }

        /// <summary>The share of a world's haunts lived in at an ante (0 before the first).</summary>
        public static double ShareAt(LandFieldRules rules, int ante)
        {
            if (rules == null || ante < 1) return 0;
            int all = Math.Max(1, rules.allFromAnte);
            if (ante >= all) return 1;
            double first = Math.Max(0, Math.Min(1, rules.firstAnteShare));
            return first + (1 - first) * (ante - 1) / (all - 1);
        }

        /// <summary>Whether a haunt's animal is there at an ante. Once it is, it is at every later ante.</summary>
        public static bool Inhabited(LandFieldRules rules, in LandHaunt haunt, int ante) => haunt.Lot < ShareAt(rules, ante);

        /// <summary>A haunt's lot from its seed, 0 to 1 (bits of the seed the kind does not use).</summary>
        public static double LotOf(int seed) => (((uint)seed >> 16) & 0x3FF) / 1024.0;

        /// <param name="radius">The world's ground radius, metres.</param>
        /// <param name="fits">True if a direction is ground a haunt can be on (dry, gentle, not a cave); null: everywhere is.</param>
        /// <param name="keepClear">The launch pad's direction, or zero for none.</param>
        /// <param name="camps">The directions of the world's camps and bases (null: none).</param>
        public static List<LandHaunt> Build(LandFieldRules rules, SeededRandom random, double radius, LandMix mix,
                                            Func<Vec3d, bool> fits, Vec3d keepClear, IReadOnlyList<Vec3d> camps)
        {
            var haunts = new List<LandHaunt>();
            if (random == null || radius <= 0) return haunts;
            int count = HauntCount(rules, radius, mix);
            if (count <= 0) return haunts;
            // On a small world the distances asked for may not fit: never more than a third of the way round apart,
            // never clear of camps by more than a sixth, so a small world full of camps still has room.
            double apart = Math.Cos(Math.Min(rules.spacing, radius * Math.PI / 3) / radius);
            double pad = keepClear.SqrMagnitude > 0.5 ? Math.Cos(Math.Min(rules.padClear, radius * Math.PI / 2) / radius) : 2;
            double clear = Math.Cos(Math.Min(rules.campClear, radius * Math.PI / 6) / radius);
            Vec3d padDirection = keepClear.SqrMagnitude > 0.5 ? keepClear.Normalized : Vec3d.Zero;
            int tries = count * 40;
            while (haunts.Count < count && tries-- > 0)
            {
                Vec3d dir = RandomDirection(random);
                // The seed is drawn whatever becomes of the place, so the stream goes the same way for every outcome.
                int seed = random.Next();
                if (Vec3d.Dot(dir, padDirection) > pad) continue;
                bool crowded = false;
                for (int i = 0; i < haunts.Count && !crowded; i++) crowded = Vec3d.Dot(dir, haunts[i].Direction) > apart;
                for (int i = 0; camps != null && i < camps.Count && !crowded; i++) crowded = Vec3d.Dot(dir, camps[i]) > clear;
                if (crowded) continue;
                if (fits != null && !fits(dir)) continue;
                haunts.Add(new LandHaunt { Direction = dir, Seed = seed, Kind = mix.Pick(seed), Lot = LotOf(seed) });
            }
            return haunts;
        }

        static Vec3d RandomDirection(SeededRandom random)
        {
            // Even over the ball: height even, angle even.
            double y = random.NextDouble() * 2 - 1, a = random.NextDouble() * Math.PI * 2, r = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
            return new Vec3d(r * Math.Cos(a), y, r * Math.Sin(a));
        }
    }

    /// <summary>
    /// What became of a step a creature tried to take: taken; stopped by something solid standing in the way (a rock,
    /// a tree, a crate, a base's floor, a wall); or refused by the ground itself (steeper than it takes, the sea's edge).
    /// A rolling ball crashes into a wall, but the ground only ends its roll.
    /// </summary>
    public enum StepResult { Taken, Wall, Ground }

    /// <summary>
    /// How a land creature keeps to the ground of a world: which steps it refuses, and which way it stands. Plain C#.
    /// (In the practice range none of this is used: the ground there is flat and the old rule stands.)
    /// </summary>
    public static class LandStep
    {
        /// <summary>
        /// A step that climbs or drops more steeply than a creature can take is a wall to it: rise is along "up" and run
        /// along the ground, both in metres. A step too short to measure counts as a millimetre, so a creature creeping
        /// slowly is held to the same slope.
        /// </summary>
        public static bool TooSteep(double rise, double run, double steepest)
            => steepest > 0 && Math.Abs(rise) > steepest * Math.Max(run, 1e-3);

        /// <summary>
        /// Which way is up for ground that runs <paramref name="along"/> a body (back to front) and <paramref name="across"/>
        /// it (left to right): the up of that plane, on the same side as <paramref name="upright"/>.
        /// </summary>
        public static Vec3d PlaneUp(Vec3d along, Vec3d across, Vec3d upright)
        {
            Vec3d n = Vec3d.Cross(along, across);
            if (n.SqrMagnitude < 1e-12) return upright.Normalized;
            n = n.Normalized;
            return Vec3d.Dot(n, upright) < 0 ? -n : n;
        }

        /// <summary>The up a creature stands by on a slope: the ground's own up, but never more than <paramref name="most"/> radians from upright.</summary>
        public static Vec3d Lean(Vec3d upright, Vec3d ground, double most)
        {
            Vec3d u = upright.Normalized, g = ground.Normalized;
            if (g.SqrMagnitude < 0.5) return u;
            double angle = Math.Acos(Math.Max(-1, Math.Min(1, Vec3d.Dot(u, g))));
            if (angle <= Math.Max(0, most)) return g;
            Vec3d axis = Vec3d.Cross(u, g);
            if (axis.SqrMagnitude < 1e-12) return u;
            return u.RotatedAround(axis.Normalized, Math.Max(0, most));
        }
    }
    /// <summary>
    /// Whether a creature of the land could still answer the player (from the design review of pass 8): false once it
    /// has been kept from them for <see cref="KeptSeconds"/> (after them, out of its reach, its steps refused and no
    /// ground gained: on a ledge or a tower it cannot climb, across a drop, at the sea's edge, behind a rock), and true
    /// again the moment it gains ground or they come within reach. A kill it could not answer pays nothing, as for a
    /// swimmer killed from the shore, and the note says why. Kiting is answering: a creature that follows the player
    /// gains ground on them however fast they back away. A second in which no step was refused and no ground gained
    /// (on its back, out of breath, warning, dragging itself without legs) neither counts nor clears, so a creature's
    /// own opening never takes its pay away. Plain C#.
    /// </summary>
    public sealed class LandReach
    {
        /// <summary>Kept from the player this long, it can no longer answer them.</summary>
        public const double KeptSeconds = 4.0;
        /// <summary>How often it is judged (seconds), and the least ground gained in that time that counts (metres).</summary>
        public const double Window = 1.0, GainLeast = 0.5;
        /// <summary>Within its strike's reach and this much more along the ground (metres), and its climb above or below, it can answer.</summary>
        public const double Margin = 3.0;

        double _kept, _window, _gained;
        bool _refused;

        public bool InReach => _kept < KeptSeconds;

        /// <summary>Seconds it has been kept from the player so far.</summary>
        public double KeptFor => _kept;

        /// <param name="after">After the player and able to hurt them.</param>
        /// <param name="refused">A step it took toward somewhere was refused this frame (too steep, a wall, the sea).</param>
        /// <param name="across">How far the player is along the ground (metres).</param>
        /// <param name="rise">How far the player is above it, below negative (metres).</param>
        /// <param name="gained">How far it went toward the player since the last step (metres; away is negative).</param>
        /// <param name="reach">How far along the ground its strike reaches (metres).</param>
        /// <param name="climb">How far above or below it its strike reaches (metres).</param>
        public void Step(bool after, bool refused, double across, double rise, double gained, double reach, double climb, double dt)
        {
            if (!after || (across <= reach + Margin && Math.Abs(rise) <= climb))
            {
                Reset();
                return;
            }
            _window += Math.Max(0.0, dt);
            _gained += gained;
            _refused |= refused;
            if (_window < Window) return;
            if (_gained >= GainLeast) _kept = 0;
            else if (_refused) _kept += _window;
            _window = 0;
            _gained = 0;
            _refused = false;
        }

        public void Reset()
        {
            _kept = 0;
            _window = 0;
            _gained = 0;
            _refused = false;
        }
    }
}
