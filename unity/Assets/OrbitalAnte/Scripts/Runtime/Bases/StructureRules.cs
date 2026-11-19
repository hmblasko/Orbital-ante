using System;
using System.Collections.Generic;
using OrbitalAnte.Look;

namespace OrbitalAnte.Bases
{
    /// <summary>
    /// The structures of a base model (a living pod, a mast, a heap of crates) as the file holds them, numbered
    /// from 0. Written by tools/blender/build_bases.py; the piece and point names are its "Sii..." names. Plain
    /// rules, no Unity types.
    /// </summary>
    public static class BaseStructures
    {
        /// <summary>The structure's own pieces after its tag ("S03" + one of these). The whole thing is the bare tag.</summary>
        public const string Glow = "Glow", Hit = "Hit", Inside = "In", Door = "Door", Bit = "Bit", Ruin = "Ruin", RuinHit = "RuinHit";
        /// <summary>Its points after its tag: the middle of its footprint, an air station's face, where its door's middle is shut and open.</summary>
        public const string Mid = "Mid", Air = "Air", DoorShut = "DoorShut", DoorOpen = "DoorOpen";
        /// <summary>What cannot be broken: the landing circle, spoil heaps, cables.</summary>
        public const string Ground = "Ground", GroundGlow = "GroundGlow", GroundHit = "GroundHit";
        /// <summary>The most structures one base may have, and the most pieces one structure breaks into.</summary>
        public const int MaxStructures = 60, MaxBits = 16;

        public static string Tag(int index) => "S" + index.ToString("00");

        /// <summary>How many structures a base model has: they are numbered without gaps, each with its middle point.</summary>
        public static int Count(ModelFile file)
        {
            if (file == null) return 0;
            int n = 0;
            while (n < MaxStructures && file.TrySocket(Tag(n) + Mid, out _)) n++;
            return n;
        }

        /// <summary>How many pieces a structure breaks into (0 if it cannot break).</summary>
        public static int Bits(ModelFile file, int index)
        {
            if (file == null) return 0;
            string tag = Tag(index) + Bit;
            int n = 0;
            while (n < MaxBits && file.Part(tag + n) != null) n++;
            return n;
        }

        /// <summary>A structure can be walked into when it has a room and a door.</summary>
        public static bool Enterable(ModelFile file, int index) =>
            file != null && file.Part(Tag(index) + Inside) != null && file.TrySocket(Tag(index) + DoorShut, out _);
    }

    /// <summary>The dials for breaking buildings. Plain numbers so they can be tuned and tested anywhere.</summary>
    [Serializable]
    public sealed class StructureRules
    {
        /// <summary>Toughness: a base amount plus so much per metre of the structure's size (the cube root of its solid volume).</summary>
        public float baseToughness = 60f, toughnessPerMetre = 140f, mostToughness = 1600f;
        /// <summary>A ship or other heavy thing driving into a structure: nothing below this speed (m/s straight in; a careful landing on a roof does nothing)...</summary>
        public float ramSafeSpeed = 5f;
        /// <summary>...and above it, this much damage per (m/s over)^2 per tonne.</summary>
        public float ramDamage = 3.2f;
        /// <summary>How far a door opens from, metres from its middle, and how long it takes to slide, seconds.</summary>
        public float doorReach = 3.2f, doorSeconds = 0.9f;
        /// <summary>
        /// One air station holds this many full suit tanks (one: a find worth a detour, not a tap; oxygen tanks are
        /// Phase 2's job), and fills a suit from empty in this many seconds.
        /// A station that has been emptied stays empty: it is a find, not a tap (constitution 2 and 5).
        /// </summary>
        public float stationTanks = 1f, stationFillSeconds = 6f;
        /// <summary>The rooms are drawn only this near (metres), with a little slack so they do not flicker at the edge.</summary>
        public float insideDrawDistance = 45f;
        /// <summary>The most loose building pieces in the world at once; the oldest are made still when more fall.</summary>
        public int mostLoosePieces = 48;
        /// <summary>
        /// The most settled pieces one structure keeps (the oldest go first), and the size (metres) below which a
        /// settled piece keeps no solid shape: rubble you walk through costs the planet's physics nothing.
        /// </summary>
        public int mostSettledPieces = 16;
        public float solidPieceSize = 0.6f;
        /// <summary>Doors and rooms are only looked after within this distance of a structure, metres.</summary>
        public float lookAfterWithin = 60f;
        /// <summary>A loose piece is made still (part of the ground) after this long, or once it has stopped moving.</summary>
        public float pieceSettleSeconds = 9f, pieceRestSpeed = 0.25f;
        /// <summary>The outward kick a falling structure gives its pieces, metres per second.</summary>
        public float fallKick = 2.2f;

        /// <summary>
        /// The share of a bullet's damage a building you can walk into (or anything tough) takes: bullets chip
        /// walls, they do not fell them in two seconds. Props (crates, lamps, masts) take it all. Rams always count in full.
        /// </summary>
        public float buildingBulletShare = 0.35f, toughAbove = 900f;
        /// <summary>Warnings as it weakens: a creak and dust at the first share worn, smoke and a groan at the second.</summary>
        public float creakAt = 0.5f, groanAt = 0.8f;
        /// <summary>One knock counts once: several touches within this many seconds are the same knock.</summary>
        public float ramRepeatSeconds = 0.25f;

        /// <summary>How much of a bullet's damage this structure takes.</summary>
        public float BulletShare(bool enterable, float toughness) => enterable || toughness >= toughAbove ? buildingBulletShare : 1f;

        /// <summary>Which warning a hit that took it from worn <paramref name="before"/> to <paramref name="after"/> earns: 0 none, 1 creak, 2 groan.</summary>
        public int Warning(float before, float after)
        {
            if (before < groanAt && after >= groanAt && after < 1f) return 2;
            if (before < creakAt && after >= creakAt && after < 1f) return 1;
            return 0;
        }

        /// <summary>How much damage a structure takes before it falls, from the size of its solid shape (metres).</summary>
        public float Toughness(float sizeX, float sizeY, float sizeZ)
        {
            double volume = Math.Max(0.01, (double)Math.Max(0.05f, sizeX) * Math.Max(0.05f, sizeY) * Math.Max(0.05f, sizeZ));
            float metres = (float)Math.Pow(volume, 1.0 / 3.0);
            return Math.Min(mostToughness, baseToughness + toughnessPerMetre * metres);
        }

        /// <summary>Damage from a heavy thing (mass in kg) striking at this speed straight into the structure.</summary>
        public float RamDamage(float speed, float mass)
        {
            float over = speed - ramSafeSpeed;
            if (over <= 0f || mass <= 0f) return 0f;
            return ramDamage * over * over * (mass / 1000f);
        }
    }

    /// <summary>How much one structure can still take. Plain C#.</summary>
    public sealed class StructureHealth
    {
        public float Max { get; }
        public float Left { get; private set; }
        public bool Fallen => Left <= 0f;
        /// <summary>0 untouched, 1 fallen: for creaks and the debug readout.</summary>
        public float Worn => Max > 0f ? 1f - Left / Max : 1f;

        public StructureHealth(float toughness)
        {
            Max = Math.Max(1f, toughness);
            Left = Max;
        }

        /// <summary>Takes damage. True only on the hit that brings it down.</summary>
        public bool Take(float damage)
        {
            if (Fallen || damage <= 0f || float.IsNaN(damage)) return false;
            Left = Math.Max(0f, Left - damage);
            return Fallen;
        }
    }

    /// <summary>
    /// Which structures of which bases have fallen this run. A base is put away when the player leaves and
    /// built again when they come back; what fell stays fallen (shown as its ruin), and an emptied air station
    /// stays empty. Plain C#.
    /// </summary>
    public sealed class BaseWrecks
    {
        readonly HashSet<long> _fallen = new HashSet<long>();
        readonly Dictionary<long, float> _air = new Dictionary<long, float>();

        /// <summary>One number for a base: its seed and where it stands.</summary>
        public static long Key(BaseSite site)
        {
            unchecked
            {
                long h = site.Seed;
                h = h * 1000003L ^ (long)Math.Round(site.Up.X * 1e6);
                h = h * 1000003L ^ (long)Math.Round(site.Up.Y * 1e6);
                h = h * 1000003L ^ (long)Math.Round(site.Up.Z * 1e6);
                return h;
            }
        }

        static long Of(long site, int structure) { unchecked { return site * 131L + structure; } }

        public bool IsDown(long site, int structure) => _fallen.Contains(Of(site, structure));
        public void Fell(long site, int structure) => _fallen.Add(Of(site, structure));
        public int FallenCount => _fallen.Count;

        /// <summary>The air left in a structure's station, in full suit tanks (its full store if untouched).</summary>
        public float AirLeft(long site, int structure, float full) => _air.TryGetValue(Of(site, structure), out float left) ? left : full;
        public void SetAir(long site, int structure, float left) => _air[Of(site, structure)] = Math.Max(0f, left);
    }

    /// <summary>A sliding door: open while someone is near and the building still stands. Plain C#.</summary>
    public static class DoorRule
    {
        /// <summary>Moves a door's openness (0 shut, 1 open) toward what it wants, at the door's speed.</summary>
        public static float Step(float open, bool wantOpen, float dt, float seconds)
        {
            float rate = dt / Math.Max(0.05f, seconds);
            float target = wantOpen ? 1f : 0f;
            if (open < target) return Math.Min(target, open + rate);
            return Math.Max(target, open - rate);
        }

        /// <summary>Eased, so the door starts and stops gently.</summary>
        public static float Eased(float open)
        {
            float t = open < 0f ? 0f : open > 1f ? 1f : open;
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>Filling the suit at a base's air station. Plain C#.</summary>
    public static class AirStationRule
    {
        /// <summary>
        /// How much air (in full suit tanks) moves into the suit this step: no more than the suit has room for,
        /// the station holds, or the station's flow allows.
        /// </summary>
        public static float Flow(float suitAir, float stationLeft, float dt, float fillSeconds)
        {
            if (dt <= 0f) return 0f;
            float room = Math.Max(0f, 1f - suitAir);
            float rate = dt / Math.Max(0.1f, fillSeconds);
            return Math.Max(0f, Math.Min(room, Math.Min(stationLeft, rate)));
        }
    }
}
