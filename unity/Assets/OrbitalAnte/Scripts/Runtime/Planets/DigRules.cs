using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>One scoop of ground taken away: a ball of air, in metres from the body's centre (the body's own space).</summary>
    public struct DigCarve
    {
        public Vec3d Centre;
        public double Radius;

        public DigCarve(Vec3d centre, double radius)
        {
            Centre = centre;
            Radius = radius;
        }
    }

    /// <summary>What dug the ground out, for the size of the hole and for tests.</summary>
    public enum DigSource { Shovel, Bullet, Blast }

    /// <summary>Why a scoop was refused, so the shovel can say so rather than sound like rock (constitution rule 3).</summary>
    public enum DigRefusal { None, TooDeep, NearWater, OldCave, Full, AlreadyDug }

    /// <summary>
    /// Digging (Matthew, 17 Nov 2026: "have the ground of the planet be solid and give the player the ability to
    /// shovel holes and dig in the ground ... shoot holes in the ground ... including when enemies shoot at you ...
    /// cave around your enemies").
    ///
    /// The planet's ground stays the height-field it always was until something digs it. Then the ground round the
    /// hole becomes "dug ground": a lattice of boxes (<see cref="ChunkSize"/> on a side, lined up with the body's own
    /// axes, so neighbouring boxes always share their grid) in which the ground is worked out as a solid: below the
    /// ground and not in a cave or a scoop. Each box is drawn by <see cref="DigMesher"/> (surface nets, like the cave
    /// networks). Where boxes stand, the ordinary ground tile and the cave walls leave out their faces
    /// (<see cref="DigCover"/>), and the dug ground is drawn instead; it lies a hair under the old surface
    /// (<see cref="Recess"/>) so that in the strip where both are drawn the old surface wins and no seam shows.
    /// Pure rules, no Unity types.
    /// </summary>
    public static class DigRules
    {
        /// <summary>Distance between the dug ground's grid points, in metres.</summary>
        public const double Cell = 0.4;
        /// <summary>Grid cells along each side of one box of dug ground.</summary>
        public const int ChunkCells = 16;
        public const double ChunkSize = Cell * ChunkCells;
        /// <summary>
        /// Boxes are put round every scoop out to this far beyond it. It must be more than a ground tile's
        /// square (1.7 m at most, across its diagonal) plus <see cref="Clearance"/>, so the ground tile's faces left
        /// standing never reach over a scoop's edge.
        /// </summary>
        public const double Margin = 3.0;
        /// <summary>A ground tile's square is only left out where all four corners are this far inside the boxes.</summary>
        public const double Clearance = 0.3;
        /// <summary>How far under the true surface the dug ground is drawn, in metres (see the class notes).</summary>
        public const double Recess = 0.06;

        /// <summary>The most scoops one body keeps. More are refused (a run would have to empty many guns into one world).</summary>
        public const int MaxCarves = 6000;
        /// <summary>The most boxes of dug ground one body keeps; a scoop that would need more is refused.</summary>
        public const int MaxChunks = 420;
        /// <summary>
        /// Bullets stop digging once a body has this many boxes; the rest are kept for the shovel and blasts, so
        /// a long firefight can never use up the ground you want to dig yourself.
        /// </summary>
        public const int BulletChunks = 250;
        /// <summary>The most boxes a scoop of this kind may bring a body to.</summary>
        public static int ChunkCapFor(DigSource source) => source == DigSource.Bullet ? BulletChunks : MaxChunks;
        /// <summary>No scoop reaches deeper than this under the ground straight above it, in metres.</summary>
        public const double MaxDepth = 24;
        /// <summary>A scoop's lowest point stays this far above a sea's surface.</summary>
        public const double AboveSea = 0.4;

        // --- sizes of the holes -----------------------------------------------------------------------------

        /// <summary>One bite of the shovel: a ball this big ...</summary>
        public const double ShovelRadius = 0.72;
        /// <summary>... with its middle this far past where the blade met the ground, along the way you dig.</summary>
        public const double ShovelBite = 0.20;
        public const double BulletBite = 0.1;
        public const double SmallestBullet = 0.2;
        public const double BiggestBullet = 0.5;
        /// <summary>The biggest blast crater's radius.</summary>
        public const double BiggestBlast = 6.0;

        /// <summary>The hole a bullet of this damage (the gun's torso damage) leaves: a little scoop, more for heavy rounds.</summary>
        public static double BulletRadius(double damage)
        {
            if (double.IsNaN(damage) || damage < 0) damage = 0;
            return Math.Min(BiggestBullet, Math.Max(SmallestBullet, 0.16 + damage * 0.0045));
        }

        /// <summary>A bite of the shovel where the blade met the ground at <paramref name="hit"/>, digging along <paramref name="along"/>.</summary>
        public static DigCarve Shovel(Vec3d hit, Vec3d along) => new DigCarve(hit + Unit(along) * ShovelBite, ShovelRadius);

        /// <summary>How far under the first ball the second one goes when digging sideways, in metres (no feet given).</summary>
        public const double ShovelStep = 0.62;
        /// <summary>
        /// Digging sideways, the lower ball's middle sits this far above the digger's feet, so its floor is exactly at
        /// them and a tunnel neither climbs nor sinks as you go (the feet settle in the bottom of the round floor).
        /// </summary>
        public const double FeetClear = ShovelRadius;
        /// <summary>A hole whose opening is at least this wide (a shovel's or a blast's, never a bullet's) is a way in.</summary>
        public const double WayInRadius = 0.6;

        /// <summary>
        /// Everything one bite takes: digging into a wall (roughly sideways) takes a second ball under the first, so a
        /// tunnel dug at eye height is tall enough to walk along and its floor is near your feet; digging down or up
        /// takes just the one.
        /// </summary>
        public static DigCarve[] ShovelBites(Vec3d hit, Vec3d along) => ShovelBites(hit, along, null);

        /// <param name="feet">
        /// Where the digger stands (body space), if known: the lower ball is then put <see cref="FeetClear"/> above
        /// the feet, under the first ball, so the tunnel's floor stays at the feet. Without it, or when the first
        /// ball is not roughly at standing height over the feet, it goes <see cref="ShovelStep"/> under the first.
        /// </param>
        public static DigCarve[] ShovelBites(Vec3d hit, Vec3d along, Vec3d? feet)
        {
            DigCarve first = Shovel(hit, along);
            Vec3d up = first.Centre.SqrMagnitude > 1e-12 ? first.Centre.Normalized : Vec3d.Up;
            if (Math.Abs(Vec3d.Dot(Unit(along), up)) > 0.7) return new[] { first };
            double step = ShovelStep;
            if (feet.HasValue && Finite(feet.Value))
            {
                double above = Vec3d.Dot(first.Centre - feet.Value, up);
                // Only for a bite at about chest height: digging into a bank well over your head, or a ledge at your
                // knees, keeps the plain shape.
                if (above > FeetClear + 0.3 && above < FeetClear + 1.6) step = above - FeetClear;
            }
            return new[] { first, new DigCarve(first.Centre - up * step, ShovelRadius) };
        }

        public static DigCarve Bullet(Vec3d hit, Vec3d along, double damage) => new DigCarve(hit + Unit(along) * BulletBite, BulletRadius(damage));

        /// <summary>
        /// A blast of this radius at a point on (or over) the ground: a bowl, centred a little under the point so its
        /// lip stands proud rather than leaving a ball-shaped pit.
        /// </summary>
        public static DigCarve Blast(Vec3d point, double radius)
        {
            radius = Math.Max(0.3, Math.Min(BiggestBlast, double.IsNaN(radius) ? 0.3 : radius));
            Vec3d up = point.SqrMagnitude > 1e-12 ? point.Normalized : Vec3d.Up;
            return new DigCarve(point - up * (radius * 0.35), radius);
        }

        static Vec3d Unit(Vec3d v) => v.SqrMagnitude > 1e-12 ? v.Normalized : Vec3d.Zero;

        /// <summary>
        /// Makes a scoop fit the world, or refuses it: never deeper than <see cref="MaxDepth"/> under the ground,
        /// never below <paramref name="floorRadius"/> (the body's safety net), never into a sea, and never near the
        /// old single caves (only cave networks join up with dug ground). Shrinks a scoop that would just reach too
        /// low rather than refusing it outright.
        /// </summary>
        public static bool Fit(PlanetTerrain terrain, double floorRadius, ref DigCarve carve) => Fit(terrain, floorRadius, ref carve, out _);

        public static bool Fit(PlanetTerrain terrain, double floorRadius, ref DigCarve carve, out DigRefusal why)
        {
            why = DigRefusal.TooDeep;
            if (terrain == null || !Finite(carve.Centre) || !(carve.Radius > 0) || carve.Centre.SqrMagnitude < 1) return false;
            Vec3d dir = carve.Centre.Normalized;
            double ground = terrain.Radius + terrain.Height(dir);
            double low = Math.Max(ground - MaxDepth, floorRadius);
            bool sea = false;
            if (terrain.HasSea)
            {
                double seaFloor = terrain.Radius + terrain.SeaLevel + AboveSea;
                if (seaFloor > low) { low = seaFloor; sea = true; }
            }
            double r = carve.Centre.Magnitude;
            // Shrink a scoop whose bottom dips just under the limit; refuse one whose middle is under it.
            if (r - carve.Radius < low) carve.Radius = r - low;
            if (carve.Radius < 0.12) { why = sea ? DigRefusal.NearWater : DigRefusal.TooDeep; return false; }
            // Scoops above the ground cut nothing: a shot that hit a ledge high over the ground still counts, but a
            // blast in mid-air does not.
            if (r - carve.Radius > ground + 40) return false;
            for (int c = 0; c < terrain.Caves.Count; c++)
            {
                Cave cave = terrain.Caves[c];
                if (cave.Network != null) continue;
                if (TerrainCollision.InsideCave(cave, carve.Centre, carve.Radius + Margin + 6)) { why = DigRefusal.OldCave; return false; }
            }
            why = DigRefusal.None;
            return true;
        }

        // --- the lattice of boxes ---------------------------------------------------------------------------

        const int Bits = 21;
        const long Mask = (1L << Bits) - 1;
        const int Offset = 1 << (Bits - 1);

        public static long Key(int x, int y, int z)
        {
            return (((long)(x + Offset) & Mask) << (Bits * 2)) | (((long)(y + Offset) & Mask) << Bits) | ((long)(z + Offset) & Mask);
        }

        public static void Unpack(long key, out int x, out int y, out int z)
        {
            x = (int)((key >> (Bits * 2)) & Mask) - Offset;
            y = (int)((key >> Bits) & Mask) - Offset;
            z = (int)(key & Mask) - Offset;
        }

        public static int ChunkIndex(double metres) => (int)Math.Floor(metres / ChunkSize);

        public static long KeyOf(Vec3d p) => Key(ChunkIndex(p.X), ChunkIndex(p.Y), ChunkIndex(p.Z));

        /// <summary>The low corner of a box, in metres from the body's centre.</summary>
        public static Vec3d ChunkMin(long key)
        {
            Unpack(key, out int x, out int y, out int z);
            return new Vec3d(x * ChunkSize, y * ChunkSize, z * ChunkSize);
        }

        public static Vec3d ChunkCentre(long key) => ChunkMin(key) + new Vec3d(ChunkSize * 0.5, ChunkSize * 0.5, ChunkSize * 0.5);

        /// <summary>Every box that a ball touches, added to <paramref name="into"/>.</summary>
        public static void ChunksTouching(Vec3d centre, double radius, List<long> into)
        {
            int x0 = ChunkIndex(centre.X - radius), x1 = ChunkIndex(centre.X + radius);
            int y0 = ChunkIndex(centre.Y - radius), y1 = ChunkIndex(centre.Y + radius);
            int z0 = ChunkIndex(centre.Z - radius), z1 = ChunkIndex(centre.Z + radius);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                    {
                        // Only boxes the ball really reaches (its nearest point to the middle is within the radius).
                        double dx = Math.Max(Math.Max(x * ChunkSize - centre.X, 0), centre.X - (x + 1) * ChunkSize);
                        double dy = Math.Max(Math.Max(y * ChunkSize - centre.Y, 0), centre.Y - (y + 1) * ChunkSize);
                        double dz = Math.Max(Math.Max(z * ChunkSize - centre.Z, 0), centre.Z - (z + 1) * ChunkSize);
                        if (dx * dx + dy * dy + dz * dz <= radius * radius) into.Add(Key(x, y, z));
                    }
        }

        internal static bool Finite(Vec3d v) => !double.IsNaN(v.X) && !double.IsNaN(v.Y) && !double.IsNaN(v.Z)
                                                && !double.IsInfinity(v.X) && !double.IsInfinity(v.Y) && !double.IsInfinity(v.Z);
    }

    /// <summary>
    /// Everything dug out of one body this run: the scoops and the boxes of dug ground round them. Changed on the
    /// main thread only; background work is handed copies (<see cref="CarvesNear"/>, <see cref="DigCover"/>).
    /// Kept while the player is away from the body, so holes are still there when they come back (for the run).
    /// </summary>
    public sealed class DigField
    {
        readonly List<DigCarve> _carves = new List<DigCarve>();
        // The scoops that reach into (or within two grid cells of) each box: what its shape depends on.
        readonly Dictionary<long, List<int>> _byChunk = new Dictionary<long, List<int>>();
        readonly HashSet<long> _chunks = new HashSet<long>();
        readonly List<long> _scratch = new List<long>(64);

        public int CarveCount => _carves.Count;
        public int ChunkCount => _chunks.Count;
        public IEnumerable<long> Chunks => _chunks;
        public bool HasChunk(long key) => _chunks.Contains(key);
        /// <summary>Goes up by one with every scoop taken.</summary>
        public int Version { get; private set; }
        public DigCarve CarveAt(int i) => _carves[i];

        /// <summary>
        /// Takes a scoop out. <paramref name="reshaped"/> gets every box whose shape changes, and
        /// <paramref name="added"/> every box that did not exist before (both may be given the same list). False if
        /// it was refused: the body is dug out as far as it may be, or the scoop is already inside one taken before.
        /// </summary>
        public bool TryCarve(DigCarve carve, List<long> reshaped, List<long> added) => TryCarve(carve, reshaped, added, DigRules.MaxChunks);

        /// <param name="chunkCap">The most boxes this scoop may bring the body to (<see cref="DigRules.ChunkCapFor"/>).</param>
        public bool TryCarve(DigCarve carve, List<long> reshaped, List<long> added, int chunkCap)
        {
            if (!(carve.Radius > 0) || !DigRules.Finite(carve.Centre)) return false;
            if (_carves.Count >= DigRules.MaxCarves) return false;
            if (Inside(carve)) return false;

            _scratch.Clear();
            DigRules.ChunksTouching(carve.Centre, carve.Radius + DigRules.Margin, _scratch);
            int fresh = 0;
            for (int i = 0; i < _scratch.Count; i++) if (!_chunks.Contains(_scratch[i])) fresh++;
            if (_chunks.Count + fresh > Math.Min(chunkCap, DigRules.MaxChunks)) return false;
            for (int i = 0; i < _scratch.Count; i++)
            {
                if (_chunks.Add(_scratch[i])) added?.Add(_scratch[i]);
            }

            int index = _carves.Count;
            _carves.Add(carve);
            _scratch.Clear();
            DigRules.ChunksTouching(carve.Centre, carve.Radius + DigRules.Cell * 2, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                long key = _scratch[i];
                if (!_byChunk.TryGetValue(key, out List<int> list)) _byChunk[key] = list = new List<int>(4);
                list.Add(index);
                reshaped?.Add(key);
            }
            Version++;
            return true;
        }

        /// <summary>True if a scoop lies wholly inside one already taken (it would change nothing).</summary>
        public bool Inside(DigCarve carve)
        {
            if (!_byChunk.TryGetValue(DigRules.KeyOf(carve.Centre), out List<int> list)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                DigCarve old = _carves[list[i]];
                if (Vec3d.Distance(old.Centre, carve.Centre) + carve.Radius <= old.Radius + 0.02) return true;
            }
            return false;
        }

        /// <summary>
        /// A copy of every scoop that comes within <paramref name="reach"/> of a box, for its shape to be worked out
        /// in the background.
        /// </summary>
        public DigCarve[] CarvesNear(long key, double reach)
        {
            Vec3d min = DigRules.ChunkMin(key) - new Vec3d(reach, reach, reach);
            Vec3d max = DigRules.ChunkMin(key) + new Vec3d(DigRules.ChunkSize + reach, DigRules.ChunkSize + reach, DigRules.ChunkSize + reach);
            var seen = new HashSet<int>();
            var result = new List<DigCarve>();
            int x0 = DigRules.ChunkIndex(min.X), x1 = DigRules.ChunkIndex(max.X);
            int y0 = DigRules.ChunkIndex(min.Y), y1 = DigRules.ChunkIndex(max.Y);
            int z0 = DigRules.ChunkIndex(min.Z), z1 = DigRules.ChunkIndex(max.Z);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!_byChunk.TryGetValue(DigRules.Key(x, y, z), out List<int> list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (!seen.Add(list[i])) continue;
                            DigCarve c = _carves[list[i]];
                            double dx = Math.Max(Math.Max(min.X - c.Centre.X, 0), c.Centre.X - max.X);
                            double dy = Math.Max(Math.Max(min.Y - c.Centre.Y, 0), c.Centre.Y - max.Y);
                            double dz = Math.Max(Math.Max(min.Z - c.Centre.Z, 0), c.Centre.Z - max.Z);
                            if (dx * dx + dy * dy + dz * dz <= c.Radius * c.Radius) result.Add(c);
                        }
                    }
            return result.ToArray();
        }

        /// <summary>True if a point (body space) is in a scoop's air, or within <paramref name="margin"/> of it.</summary>
        public bool InDugAir(Vec3d p, double margin = 0)
        {
            if (_carves.Count == 0) return false;
            margin = Math.Max(0, margin);
            // Scoops are listed in every box they reach (plus two cells), so the box a point is in, and its
            // neighbours when the margin is wide, hold every scoop that can contain it.
            int reach = (int)Math.Ceiling(margin / DigRules.ChunkSize);
            int cx = DigRules.ChunkIndex(p.X), cy = DigRules.ChunkIndex(p.Y), cz = DigRules.ChunkIndex(p.Z);
            for (int x = cx - reach; x <= cx + reach; x++)
                for (int y = cy - reach; y <= cy + reach; y++)
                    for (int z = cz - reach; z <= cz + reach; z++)
                    {
                        if (!_byChunk.TryGetValue(DigRules.Key(x, y, z), out List<int> list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            DigCarve c = _carves[list[i]];
                            double r = c.Radius + margin;
                            if ((p - c.Centre).SqrMagnitude <= r * r) return true;
                        }
                    }
            return false;
        }

        /// <summary>
        /// The way into dug ground near <paramref name="p"/> (body space): of the holes within <paramref name="reach"/>
        /// that break the ground's surface and are wide enough to walk into (<see cref="DigRules.WayInRadius"/>), the
        /// nearest; <paramref name="mouth"/> is the point on the old surface over its middle. For enemies hunting a
        /// player who has dug in: they come to the mouth and shoot down it, rather than standing on the roof.
        /// <paramref name="groundRadius"/> gives the old ground's distance from the centre in a direction.
        /// </summary>
        public bool WayInNear(Vec3d p, double reach, Func<Vec3d, double> groundRadius, out Vec3d mouth)
        {
            mouth = p;
            if (_carves.Count == 0 || groundRadius == null) return false;
            double best = double.MaxValue;
            int r = (int)Math.Ceiling(reach / DigRules.ChunkSize);
            int cx = DigRules.ChunkIndex(p.X), cy = DigRules.ChunkIndex(p.Y), cz = DigRules.ChunkIndex(p.Z);
            for (int x = cx - r; x <= cx + r; x++)
                for (int y = cy - r; y <= cy + r; y++)
                    for (int z = cz - r; z <= cz + r; z++)
                    {
                        // A scoop is listed in its own box first; checking the box its middle is in avoids repeats.
                        if (!_byChunk.TryGetValue(DigRules.Key(x, y, z), out List<int> list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            DigCarve c = _carves[list[i]];
                            if (c.Radius < DigRules.WayInRadius || DigRules.KeyOf(c.Centre) != DigRules.Key(x, y, z)) continue;
                            double d = (c.Centre - p).SqrMagnitude;
                            if (d > reach * reach || d >= best) continue;
                            if (!Opening(c, groundRadius, out Vec3d at)) continue;
                            best = d;
                            mouth = at;
                        }
                    }
            return best < double.MaxValue;
        }

        /// <summary>
        /// True if a scoop breaks out through the old ground: one of six points well inside its ball (over it, and to
        /// all four sides and below, so a tunnel's mouth in a hillside counts) stands in the open air.
        /// <paramref name="at"/> is the ground under that point.
        /// </summary>
        static bool Opening(DigCarve c, Func<Vec3d, double> groundRadius, out Vec3d at)
        {
            at = c.Centre;
            double cr = c.Centre.Magnitude;
            if (cr < 1) return false;
            Vec3d up = c.Centre / cr;
            Vec3d east = Vec3d.Cross(Math.Abs(up.Y) < 0.9 ? Vec3d.Up : Vec3d.Right, up).Normalized;
            Vec3d north = Vec3d.Cross(up, east);
            double reach = c.Radius * 0.8;
            for (int k = 0; k < 6; k++)
            {
                Vec3d way = k == 0 ? up : k == 1 ? east : k == 2 ? -east : k == 3 ? north : k == 4 ? -north : -up;
                Vec3d p = c.Centre + way * reach;
                double r = p.Magnitude;
                double ground = groundRadius(p / r);
                if (r > ground + 0.1)
                {
                    at = p / r * ground;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Forgets everything dug (a new run).</summary>
        public void Clear()
        {
            _carves.Clear();
            _byChunk.Clear();
            _chunks.Clear();
            Version++;
        }
    }

    /// <summary>
    /// Which boxes of dug ground are up and solid: the ordinary ground and cave walls leave out their faces inside
    /// them. Never changed once made (a new one is made when boxes come or go), so it can be handed to background
    /// work.
    /// </summary>
    public sealed class DigCover
    {
        public static readonly DigCover None = new DigCover(new long[0], 0);

        readonly HashSet<long> _ready;
        public int Version { get; }
        public int Count => _ready.Count;
        public bool IsEmpty => _ready.Count == 0;
        /// <summary>A box round every box in the cover (body space), for quick refusals.</summary>
        public Vec3d Min { get; }
        public Vec3d Max { get; }

        public DigCover(IEnumerable<long> ready, int version)
        {
            _ready = new HashSet<long>(ready);
            Version = version;
            Vec3d min = new Vec3d(double.MaxValue, double.MaxValue, double.MaxValue), max = -min;
            foreach (long key in _ready)
            {
                Vec3d a = DigRules.ChunkMin(key);
                Vec3d b = a + new Vec3d(DigRules.ChunkSize, DigRules.ChunkSize, DigRules.ChunkSize);
                min = new Vec3d(Math.Min(min.X, a.X), Math.Min(min.Y, a.Y), Math.Min(min.Z, a.Z));
                max = new Vec3d(Math.Max(max.X, b.X), Math.Max(max.Y, b.Y), Math.Max(max.Z, b.Z));
            }
            Min = min;
            Max = max;
        }

        public bool Contains(long key) => _ready.Contains(key);

        /// <summary>True if any box of the cover reaches within <paramref name="reach"/> of a box from min to max.</summary>
        public bool Touches(Vec3d min, Vec3d max, double reach = 0)
        {
            if (IsEmpty) return false;
            return !(max.X < Min.X - reach || max.Y < Min.Y - reach || max.Z < Min.Z - reach
                     || min.X > Max.X + reach || min.Y > Max.Y + reach || min.Z > Max.Z + reach);
        }

        /// <summary>
        /// True if a point (body space) is inside the boxes with at least <paramref name="clearance"/> of them all
        /// round (a cube of that half-size round it is wholly inside the boxes).
        /// </summary>
        public bool Covers(Vec3d p, double clearance)
        {
            if (IsEmpty) return false;
            if (p.X < Min.X || p.Y < Min.Y || p.Z < Min.Z || p.X > Max.X || p.Y > Max.Y || p.Z > Max.Z) return false;
            double s = DigRules.ChunkSize;
            int cx = DigRules.ChunkIndex(p.X), cy = DigRules.ChunkIndex(p.Y), cz = DigRules.ChunkIndex(p.Z);
            double lx = p.X - cx * s, ly = p.Y - cy * s, lz = p.Z - cz * s;
            int x0 = lx < clearance ? -1 : 0, x1 = lx > s - clearance ? 1 : 0;
            int y0 = ly < clearance ? -1 : 0, y1 = ly > s - clearance ? 1 : 0;
            int z0 = lz < clearance ? -1 : 0, z1 = lz > s - clearance ? 1 : 0;
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                        if (!_ready.Contains(DigRules.Key(cx + x, cy + y, cz + z))) return false;
            return true;
        }
    }

    /// <summary>
    /// The height of the ground exactly as the finest ground tiles draw it (their corners, joined by the same two
    /// triangles per square), so dug ground meets the tiles round it with no step. Keeps the corners it has worked
    /// out; one per background job, never shared between threads.
    /// </summary>
    public sealed class DigGround
    {
        readonly PlanetTerrain _terrain;
        readonly int _side;
        readonly double _minWavelength;
        readonly Dictionary<long, Vec3d> _corners = new Dictionary<long, Vec3d>();

        /// <param name="level">The finest tile level the body draws (PlanetSurface.MaxLevel).</param>
        public DigGround(PlanetTerrain terrain, int level)
        {
            _terrain = terrain;
            _side = (1 << level) * TerrainMesher.Quads;
            _minWavelength = TerrainMesher.CellSize(terrain.Radius, level) * 1.6;
        }

        public PlanetTerrain Terrain => _terrain;
        public double MinWavelength => _minWavelength;

        Vec3d Corner(int face, int i, int j)
        {
            long key = ((long)face << 56) | ((long)(i & 0xFFFFFFF) << 28) | (long)(j & 0xFFFFFFF);
            if (_corners.TryGetValue(key, out Vec3d p)) return p;
            double u = -1 + i * 2.0 / _side, v = -1 + j * 2.0 / _side;
            Vec3d dir = CubeSphere.Direction(face, u, v);
            p = dir * (_terrain.Radius + _terrain.Sample(dir, _minWavelength).Height);
            _corners[key] = p;
            return p;
        }

        /// <summary>Distance from the body's centre to the drawn ground in a direction (a unit vector), in metres.</summary>
        public double RadiusAt(Vec3d direction)
        {
            CubeSphere.Locate(direction, out int face, out double u, out double v);
            double fu = (u + 1) * 0.5 * _side, fv = (v + 1) * 0.5 * _side;
            int i = Math.Min(_side - 1, Math.Max(0, (int)Math.Floor(fu)));
            int j = Math.Min(_side - 1, Math.Max(0, (int)Math.Floor(fv)));
            fu -= i;
            fv -= j;
            // The tile's two triangles per square meet along the diagonal from (i + 1, j) to (i, j + 1). Where the
            // line from the centre meets the flat triangle is exactly where the tile is drawn.
            Vec3d a, b, c;
            if (fu + fv <= 1) { a = Corner(face, i, j); b = Corner(face, i + 1, j); c = Corner(face, i, j + 1); }
            else { a = Corner(face, i + 1, j + 1); b = Corner(face, i, j + 1); c = Corner(face, i + 1, j); }
            Vec3d normal = Vec3d.Cross(b - a, c - a);
            double facing = Vec3d.Dot(normal, direction);
            if (Math.Abs(facing) < 1e-9) return a.Magnitude;
            return Vec3d.Dot(normal, a) / facing;
        }
    }
}
