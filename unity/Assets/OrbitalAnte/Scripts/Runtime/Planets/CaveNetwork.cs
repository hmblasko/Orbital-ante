using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>What kind of rock a cave network is cut in. It decides the shapes, the colours and what grows inside.</summary>
    public enum CaveStyle
    {
        /// <summary>Not a network: an ordinary single tunnel (<see cref="CavePath"/>).</summary>
        None,
        /// <summary>Limestone dissolved by water, like Carlsbad or Mammoth Cave: round passages, flowstone, stone spikes.</summary>
        Karst,
        /// <summary>Tunnels left by flowing lava, like those on the Moon, Mars and Hawaii: wide, flat-floored, dark basalt.</summary>
        LavaTube,
        /// <summary>Passages melted through ice: pale blue walls, glowing crystals.</summary>
        Ice,
    }

    /// <summary>A room of a cave network: a squashed, lumpy ball with a level floor.</summary>
    public sealed class CaveChamber
    {
        /// <summary>Middle of the room, metres from the body's centre.</summary>
        public Vec3d Centre;
        /// <summary>"Up" at the room: away from the body's centre.</summary>
        public Vec3d Up;
        /// <summary>The room's two level directions (its long way first).</summary>
        public Vec3d Long, Short;
        /// <summary>Half-lengths: along <see cref="Long"/>, along <see cref="Short"/>, and up.</summary>
        public double LongRadius, ShortRadius, Height;
        /// <summary>How far below the middle the floor is, in metres.</summary>
        public double FloorDrop;
        /// <summary>Which level of the network it is on: 0 nearest the surface.</summary>
        public int Tier;
        /// <summary>True if enemies live here.</summary>
        public bool Camp;
        /// <summary>True if a hole in the roof lets the sky in.</summary>
        public bool Skylight;

        /// <summary>The middle of the floor.</summary>
        public Vec3d Floor => Centre - Up * FloorDrop;
        /// <summary>How far the floor reaches from its middle in a level direction, a little inside the walls.</summary>
        public double FloorReach(Vec3d level)
        {
            double a = Vec3d.Dot(level, Long), b = Vec3d.Dot(level, Short);
            double cut = Math.Sqrt(Math.Max(0, 1 - (FloorDrop * FloorDrop) / (Height * Height)));
            double k = Math.Sqrt(a * a / (LongRadius * LongRadius) + b * b / (ShortRadius * ShortRadius));
            return k > 1e-9 ? cut / k : Math.Min(LongRadius, ShortRadius) * cut;
        }
    }

    /// <summary>A passage of a cave network, as a string of circles. A shaft is too steep to walk.</summary>
    public sealed class CaveTunnel
    {
        public Vec3d[] Points;
        public double[] Radii;
        /// <summary>Rooms at each end; -1 for the surface or a dead end.</summary>
        public int From = -1, To = -1;
        /// <summary>True for a climb (a pit or a chimney): only a jetpack gets you up it.</summary>
        public bool Shaft;
        /// <summary>True if it leads to the surface.</summary>
        public bool Entrance;
        /// <summary>How low the floor is cut, as a fraction of the radius below the middle (a shaft has none).</summary>
        public double FloorCut = 0.55;
    }

    /// <summary>A hole from the network up to the open air.</summary>
    public struct CaveOpening
    {
        /// <summary>Unit direction from the body's centre to the middle of the hole in the ground.</summary>
        public Vec3d Direction;
        /// <summary>Radius of the hole cut in the ground, in metres.</summary>
        public double Radius;
        /// <summary>True for a hole straight down through a room's roof, false for a walk-in entrance.</summary>
        public bool Skylight;
    }

    /// <summary>A light inside a network: a camp's work lamp, or a glowing crystal.</summary>
    public struct CaveLamp
    {
        /// <summary>Where it stands, metres from the body's centre (the foot of a lamp, the middle of a crystal's glow).</summary>
        public Vec3d Position;
        public Vec3d Up;
        public float R, G, B;
        /// <summary>Brightness at the lamp itself, and how far its light reaches through the air of the cave, in metres.</summary>
        public float Strength, Reach;
        /// <summary>True for a crystal's own glow (there is no lamp to draw).</summary>
        public bool Crystal;
        /// <summary>Which room it is in.</summary>
        public int Chamber;
        /// <summary>Which way the lamp head points (level).</summary>
        public Vec3d Facing;
    }

    /// <summary>A cone of rock hanging from a roof or rising from a floor.</summary>
    public struct CaveSpike
    {
        public Vec3d Base;
        /// <summary>Unit direction from the base to the tip.</summary>
        public Vec3d Toward;
        public double Length, Radius;
    }

    /// <summary>
    /// A cave network: rooms on two or three levels joined by winding passages, with side passages that end,
    /// ramps between levels, a pit or chimney you need the jetpack for, and one or two ways in from the surface,
    /// one of which may be a hole in a room's roof that lets the daylight in. Enemies camp in some rooms by the
    /// light of work lamps.
    ///
    /// Pure rules, no Unity types, built once with the planet and never changed afterwards, so it is safe to read
    /// from background threads. The space inside is a signed distance (<see cref="Air"/>: negative in the air of
    /// the cave), which <see cref="CaveNetworkMesher"/> turns into walls and <see cref="CaveLightVolume"/> into
    /// light. Positions are metres from the body's centre.
    /// </summary>
    public sealed class CaveNetwork
    {
        public CaveStyle Style;
        public string Formation;
        /// <summary>Where the network is: the point on the ground over its middle, and the frame there.</summary>
        public Vec3d Origin, East, Up, North;
        public readonly List<CaveChamber> Chambers = new List<CaveChamber>();
        public readonly List<CaveTunnel> Tunnels = new List<CaveTunnel>();
        public readonly List<CaveOpening> Openings = new List<CaveOpening>();
        public readonly List<CaveLamp> Lamps = new List<CaveLamp>();
        public readonly List<CaveSpike> Spikes = new List<CaveSpike>();
        /// <summary>Rock colour (0..1, screen colour).</summary>
        public float R, G, B;
        /// <summary>Floor colour (mud, dust, frost).</summary>
        public float FloorR, FloorG, FloorB;
        /// <summary>How lumpy the walls are, in metres.</summary>
        public double Lumps = 0.7;
        public uint Seed;
        /// <summary>Distance from the body's centre to the lowest ground over the network, in metres.</summary>
        public double GroundLow;

        /// <summary>The box round everything, in the local frame (x east, y up, z north, from <see cref="Origin"/>).</summary>
        public Vec3d LocalMin, LocalMax;

        // ------------------------------------------------------------------ the walk graph

        /// <summary>Points on the floor that enemies walk between, metres from the body's centre.</summary>
        public readonly List<Vec3d> Waypoints = new List<Vec3d>();
        /// <summary>Which waypoints join which, walking.</summary>
        public readonly List<List<int>> Links = new List<List<int>>();
        /// <summary>The room a waypoint is in, or -1 for one in a passage.</summary>
        public readonly List<int> WaypointRoom = new List<int>();

        // ------------------------------------------------------------------ shapes for the distance sum

        struct Piece
        {
            public bool Ball;              // a room (else a stretch of passage)
            public int Index;              // room or tunnel
            public Vec3d A, B;             // the stretch's two ends (a room: A is the middle)
            public double RadiusA, RadiusB;
            public Vec3d Up;
            public double FloorCut;        // a stretch: fraction of the radius below the middle where the floor is; 0 none
            public Vec3d Min, Max;         // bounds, body space, with room for lumps and blending
        }

        Piece[] _pieces = new Piece[0];
        const double Blend = 0.8;

        /// <summary>
        /// The signed distance from a point to the cave's rock: negative in the air of the cave, positive in rock,
        /// roughly in metres. Points far from every room and passage are far into rock.
        /// </summary>
        public double Air(Vec3d p)
        {
            double d = 1e9;
            int nearest = -1;
            double nearestD = 1e9;
            for (int i = 0; i < _pieces.Length; i++)
            {
                ref Piece s = ref _pieces[i];
                if (p.X < s.Min.X || p.Y < s.Min.Y || p.Z < s.Min.Z || p.X > s.Max.X || p.Y > s.Max.Y || p.Z > s.Max.Z) continue;
                double di = s.Ball ? RoomDistance(Chambers[s.Index], p) : StretchDistance(ref s, p);
                if (di < nearestD) { nearestD = di; nearest = i; }
                d = SoftMin(d, di, Blend);
            }
            if (nearest < 0) return 1e9;
            if (d > Lumps * 2 + 2) return d;

            // Lumpy walls, but floors you can walk on: the lumps fade out toward the floor.
            double aboveFloor = AboveFloor(ref _pieces[nearest], p);
            double wall = Noise3.Smooth(0.25, 1.8, aboveFloor);
            double lumps = Noise3.Fbm(p, 5.5, 3, Seed) * Lumps * (0.25 + 0.75 * wall)
                           + Noise3.Perlin(p, 1.7, Seed + 7) * Lumps * 0.3 * wall;
            d += lumps;

            // Stone spikes hang from the roofs and rise from the floors.
            for (int i = 0; i < Spikes.Count; i++)
            {
                CaveSpike k = Spikes[i];
                Vec3d rel = p - k.Base;
                if (rel.SqrMagnitude > (k.Length + 1) * (k.Length + 1)) continue;
                double along = Vec3d.Dot(rel, k.Toward);
                double across = (rel - k.Toward * along).Magnitude;
                double t = Noise3.Clamp01(along / k.Length);
                double radius = k.Radius * (1 - t) * (1 - t * 0.2) + 0.05;
                double cone = Math.Max(across - radius, Math.Max(-along - 0.6, along - k.Length));
                d = SoftMax(d, -cone, 0.5);
            }
            return d;
        }

        /// <summary>True if any room or passage comes near a box (body space).</summary>
        public bool Touches(Vec3d min, Vec3d max)
        {
            for (int i = 0; i < _pieces.Length; i++)
            {
                ref Piece s = ref _pieces[i];
                if (s.Max.X < min.X || s.Max.Y < min.Y || s.Max.Z < min.Z || s.Min.X > max.X || s.Min.Y > max.Y || s.Min.Z > max.Z) continue;
                return true;
            }
            for (int i = 0; i < Spikes.Count; i++)
            {
                Vec3d b = Spikes[i].Base;
                if (b.X > min.X - 6 && b.Y > min.Y - 6 && b.Z > min.Z - 6 && b.X < max.X + 6 && b.Y < max.Y + 6 && b.Z < max.Z + 6) return true;
            }
            return false;
        }

        /// <summary>True if a point is in the air of the cave (within a margin, in metres).</summary>
        public bool InsideAir(Vec3d p, double margin = 0)
        {
            Vec3d local = ToLocal(p);
            if (local.X < LocalMin.X - margin || local.Y < LocalMin.Y - margin || local.Z < LocalMin.Z - margin
                || local.X > LocalMax.X + margin || local.Y > LocalMax.Y + margin || local.Z > LocalMax.Z + margin) return false;
            return Air(p) < margin;
        }

        static double RoomDistance(CaveChamber c, Vec3d p)
        {
            Vec3d rel = p - c.Centre;
            double x = Vec3d.Dot(rel, c.Long) / c.LongRadius, y = Vec3d.Dot(rel, c.Up) / c.Height, z = Vec3d.Dot(rel, c.Short) / c.ShortRadius;
            double k0 = Math.Sqrt(x * x + y * y + z * z);
            double x2 = x / c.LongRadius, y2 = y / c.Height, z2 = z / c.ShortRadius;
            double k1 = Math.Sqrt(x2 * x2 + y2 * y2 + z2 * z2);
            double ball = k1 > 1e-9 ? k0 * (k0 - 1) / k1 : -Math.Min(c.Height, c.ShortRadius);
            // A level floor.
            double floor = -c.FloorDrop - Vec3d.Dot(rel, c.Up);
            return Math.Max(ball, floor);
        }

        static double StretchDistance(ref Piece s, Vec3d p)
        {
            Vec3d ab = s.B - s.A;
            double len2 = ab.SqrMagnitude;
            double t = len2 > 1e-9 ? Noise3.Clamp01(Vec3d.Dot(p - s.A, ab) / len2) : 0;
            Vec3d c = s.A + ab * t;
            double radius = s.RadiusA + (s.RadiusB - s.RadiusA) * t;
            Vec3d rel = p - c;
            // Passages are a little wider than they are tall, as real ones are.
            double up = Vec3d.Dot(rel, s.Up);
            Vec3d level = rel - s.Up * up;
            double tube = Math.Sqrt(level.SqrMagnitude + up * up * 1.25) - radius;
            if (s.FloorCut <= 0) return tube;
            return Math.Max(tube, -FloorHeight(ref s, p));
        }

        /// <summary>
        /// Height of a point over a passage's floor. The floor follows the middle of the passage as it is seen from
        /// above, so on a slope it is still where it was planned under the middle; measured from the nearest point of
        /// the middle instead, it would sink as the point went lower and a ramp would open into a pit.
        /// </summary>
        static double FloorHeight(ref Piece s, Vec3d p)
        {
            Vec3d ab = s.B - s.A;
            Vec3d abLevel = ab - s.Up * Vec3d.Dot(ab, s.Up);
            double len2 = abLevel.SqrMagnitude;
            Vec3d ap = p - s.A;
            // The round end of a stretch reaches a radius past its end. There the floor is the higher of its end's
            // level and its slope carried on: either one alone sinks a step into the next stretch where the slope
            // changes (level going on into the climb, or the climb carried back over the level). Where pieces meet,
            // the lower floor of the two is the one walked on, so taking the higher here never raises a floor.
            double t = len2 > 1e-9 ? Vec3d.Dot(ap, abLevel) / len2 : 0;
            double tc = Noise3.Clamp01(t);
            double radius = s.RadiusA + (s.RadiusB - s.RadiusA) * tc;
            double held = Vec3d.Dot(p - (s.A + ab * tc), s.Up), carried = Vec3d.Dot(p - (s.A + ab * t), s.Up);
            return Math.Min(held, carried) + s.FloorCut * radius;
        }

        double AboveFloor(ref Piece s, Vec3d p)
        {
            if (s.Ball)
            {
                CaveChamber c = Chambers[s.Index];
                return Vec3d.Dot(p - c.Centre, c.Up) + c.FloorDrop;
            }
            if (s.FloorCut <= 0) return 10;
            return FloorHeight(ref s, p);
        }

        static double SoftMin(double a, double b, double k)
        {
            double h = Math.Max(k - Math.Abs(a - b), 0) / k;
            return Math.Min(a, b) - h * h * k * 0.25;
        }

        static double SoftMax(double a, double b, double k) => -SoftMin(-a, -b, k);

        // ------------------------------------------------------------------ frame

        public Vec3d ToLocal(Vec3d p)
        {
            Vec3d rel = p - Origin;
            return new Vec3d(Vec3d.Dot(rel, East), Vec3d.Dot(rel, Up), Vec3d.Dot(rel, North));
        }

        public Vec3d FromLocal(Vec3d local) => Origin + East * local.X + Up * local.Y + North * local.Z;

        // ------------------------------------------------------------------ questions

        /// <summary>
        /// The floor under a point in the cave (or the point itself if it is not in the cave's air): straight down
        /// until the rock. False if there is no floor within <paramref name="reach"/> metres.
        /// </summary>
        public bool FloorBelow(Vec3d p, out Vec3d floor, double reach = 8)
        {
            floor = p;
            if (Air(p) > 0) return false;
            Vec3d down = -p.Normalized;
            double step = 0.4, travelled = 0;
            Vec3d at = p;
            while (travelled < reach)
            {
                Vec3d next = at + down * step;
                if (Air(next) >= 0)
                {
                    // Close in on the floor.
                    double lo = 0, hi = step;
                    for (int i = 0; i < 6; i++)
                    {
                        double mid = (lo + hi) * 0.5;
                        if (Air(at + down * mid) >= 0) hi = mid; else lo = mid;
                    }
                    floor = at + down * lo;
                    return true;
                }
                at = next;
                travelled += step;
            }
            return false;
        }

        /// <summary>
        /// Where feet near a point stand: the floor under it felt for from a little above (higher first, so a foot
        /// just under the floor finds it again; lower if that is in the rock, under a stone or a low lip of roof).
        /// </summary>
        public bool FeetAt(Vec3d p, out Vec3d floor, double reach = 6)
        {
            Vec3d up = p.Normalized;
            if (FloorBelow(p + up * 1.2, out floor, reach)) return true;
            if (FloorBelow(p + up * 0.6, out floor, reach)) return true;
            return FloorBelow(p + up * 0.2, out floor, reach);
        }

        /// <summary>A point on a room's floor: an angle round it and how far out (0 middle, 1 near the wall).</summary>
        public Vec3d RoomFloorPoint(int room, double angle, double fraction)
        {
            CaveChamber c = Chambers[room];
            Vec3d level = c.Long * Math.Cos(angle) + c.Short * Math.Sin(angle);
            return c.Floor + level * (c.FloorReach(level) * Noise3.Clamp01(fraction));
        }

        /// <summary>The waypoint nearest a point that can be walked to from it in a straight line (as far as the rules can tell).</summary>
        public int NearestWaypoint(Vec3d p)
        {
            // Nearest first, stopping at the first that can be walked to: the line test is the dear part, so it is
            // only run on the few waypoints nearer than the answer. Only those within reach are looked at.
            const double Reach = 15 * 15;
            var near = new List<(double d, int i)>();
            int closest = -1;
            double closestD = double.MaxValue;
            for (int i = 0; i < Waypoints.Count; i++)
            {
                double d = (Waypoints[i] - p).SqrMagnitude;
                if (d < closestD) { closestD = d; closest = i; }
                if (d > Reach || Links[i].Count == 0) continue;
                // Not one overhead or underfoot on another level (where passages cross): it cannot be walked to.
                double rise = Math.Abs(Waypoints[i].Magnitude - p.Magnitude);
                double level = Math.Sqrt(Math.Max(0, d - rise * rise));
                if (rise > 0.8 + 0.7 * level) continue;
                near.Add((d, i));
            }
            near.Sort((x, y) => x.d.CompareTo(y.d));
            foreach ((double d, int i) in near)
            {
                if (d <= 4 || ClearBetween(p, Waypoints[i])) return i;
            }
            return closest;
        }

        /// <summary>True if the straight line between two points stays in the cave's air, a metre above a floor all the way.</summary>
        public bool ClearBetween(Vec3d a, Vec3d b)
        {
            // No steeper than a steep path: a straight line down a pit or up a chimney is not a walk.
            Vec3d up = a.Normalized;
            Vec3d d = b - a;
            double rise = Math.Abs(Vec3d.Dot(d, up));
            if (rise > 0.8 + 0.7 * (d - up * Vec3d.Dot(d, up)).Magnitude) return false;
            Vec3d lift = up * 1.0;
            Vec3d from = a + lift, to = b + b.Normalized * 1.0;
            double length = Vec3d.Distance(from, to);
            int steps = Math.Max(3, (int)(length / 0.5));
            for (int i = 1; i < steps; i++)
            {
                Vec3d q = from + (to - from) * (i / (double)steps);
                if (Air(q) > -0.3) return false;
                // Room for a body: nothing at head height either (a stone spike, a low lip of roof).
                if (Air(q + q.Normalized * 0.7) > -0.1) return false;
                // And floor all the way: a line over a pit is not a walk.
                if (Air(q - q.Normalized * 2.4) < 0) return false;
            }
            return true;
        }

        /// <summary>Shortest walk through the network between two waypoints (both included), or null if there is none.</summary>
        public List<int> Route(int from, int to)
        {
            if (from < 0 || to < 0) return null;
            int n = Waypoints.Count;
            var cost = new double[n];
            var back = new int[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) { cost[i] = double.MaxValue; back[i] = -1; }
            cost[from] = 0;
            for (int round = 0; round < n; round++)
            {
                int u = -1;
                double best = double.MaxValue;
                for (int i = 0; i < n; i++) if (!done[i] && cost[i] < best) { best = cost[i]; u = i; }
                if (u < 0) break;
                if (u == to) break;
                done[u] = true;
                foreach (int v in Links[u])
                {
                    double c = cost[u] + Vec3d.Distance(Waypoints[u], Waypoints[v]);
                    if (c < cost[v]) { cost[v] = c; back[v] = u; }
                }
            }
            if (cost[to] == double.MaxValue) return null;
            var path = new List<int>();
            for (int at = to; at >= 0; at = back[at]) path.Add(at);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Where to head next to get from one point to another through the network, walking: the destination
        /// itself if it can be walked to straight, otherwise the next waypoint on the way.
        /// </summary>
        public Vec3d NextStep(Vec3d from, Vec3d destination)
        {
            if (ClearBetween(from, destination)) return destination;
            int b = NearestWaypoint(destination);
            if (b < 0) return destination;
            WalkField field = FieldTo(b);
            // Start from whichever waypoint in reach leaves the shortest walk in all (the walk to it plus its walk
            // on), not merely the nearest: two nearly as near can lead opposite ways round a loop, and a walker
            // between them would go back and forth.
            int start = -1;
            double best = double.MaxValue;
            for (int i = 0; i < Waypoints.Count; i++)
            {
                if (field.Cost[i] == double.MaxValue) continue;
                double d2 = (Waypoints[i] - from).SqrMagnitude;
                if (d2 > 10 * 10) continue;
                double d = Math.Sqrt(d2);
                if (d + field.Cost[i] >= best) continue;
                if (!Reachable(from, Waypoints[i], d2)) continue;
                best = d + field.Cost[i];
                start = i;
            }
            if (start < 0) start = NearestWaypoint(from);
            if (start < 0 || field.Cost[start] == double.MaxValue) return destination;
            var path = new List<int>();
            for (int at = start; at >= 0 && path.Count < 64; at = field.Next[at]) path.Add(at);
            // Waypoints already underfoot are passed; then on to the furthest of the next few in plain view.
            int first = 0;
            while (first < path.Count - 1 && Underfoot(from, Waypoints[path[first]])) first++;
            int next = first;
            for (int i = first + 1; i < path.Count && i < first + 4; i++)
            {
                if (ClearBetween(from, Waypoints[path[i]])) next = i;
            }
            if (next == path.Count - 1 && Underfoot(from, Waypoints[path[next]])) return destination;
            return Waypoints[path[next]];
        }

        /// <summary>A waypoint as good as reached: close by on the level, at much the same height.</summary>
        static bool Underfoot(Vec3d from, Vec3d waypoint)
        {
            Vec3d d = waypoint - from;
            Vec3d up = from.Normalized;
            double rise = Vec3d.Dot(d, up);
            return Math.Abs(rise) < 1.6 && (d - up * rise).SqrMagnitude < 1.5 * 1.5;
        }

        bool Reachable(Vec3d from, Vec3d waypoint, double d2)
        {
            double rise = Math.Abs(waypoint.Magnitude - from.Magnitude);
            double level = Math.Sqrt(Math.Max(0, d2 - rise * rise));
            if (rise > 0.8 + 0.7 * level) return false;
            return d2 <= 1 || ClearBetween(from, waypoint);
        }

        /// <summary>Every waypoint's walk to one waypoint, and its next step on the way there.</summary>
        sealed class WalkField
        {
            public double[] Cost;
            public int[] Next;
        }

        readonly Dictionary<int, WalkField> _fields = new Dictionary<int, WalkField>();
        readonly object _fieldsLock = new object();

        WalkField FieldTo(int goal)
        {
            lock (_fieldsLock)
            {
                if (_fields.TryGetValue(goal, out WalkField known)) return known;
            }
            int n = Waypoints.Count;
            var field = new WalkField { Cost = new double[n], Next = new int[n] };
            var done = new bool[n];
            for (int i = 0; i < n; i++) { field.Cost[i] = double.MaxValue; field.Next[i] = -1; }
            field.Cost[goal] = 0;
            for (int round = 0; round < n; round++)
            {
                int u = -1;
                double least = double.MaxValue;
                for (int i = 0; i < n; i++) if (!done[i] && field.Cost[i] < least) { least = field.Cost[i]; u = i; }
                if (u < 0) break;
                done[u] = true;
                foreach (int v in Links[u])
                {
                    double c = field.Cost[u] + Vec3d.Distance(Waypoints[u], Waypoints[v]);
                    if (c < field.Cost[v]) { field.Cost[v] = c; field.Next[v] = u; }
                }
            }
            lock (_fieldsLock)
            {
                if (_fields.Count > 32) _fields.Clear();   // a camp's few rooms are all most walkers ever head for
                _fields[goal] = field;
            }
            return field;
        }

        // ------------------------------------------------------------------ building

        /// <summary>How far above the sea the lowest air of a network on a world with a sea stays, in metres.</summary>
        public const double DrySpare = 1.5;

        /// <summary>
        /// Plans a network for a formation, at a seeded place on dry, gentle ground. Null if this body has no room
        /// for one or no good place was found.
        /// </summary>
        public static CaveNetwork Build(PlanetTerrain terrain, Formation f, SeededRandom r)
        {
            double bodyRadius = terrain.Radius;
            if (bodyRadius < 100) return null;
            CaveStyle style = f.CaveStyle == CaveStyle.None ? CaveStyle.Karst : f.CaveStyle;
            // Smaller worlds get smaller networks: the curve of the ground would otherwise run into the deep rooms.
            double size = Noise3.Clamp01((bodyRadius - 100) / 300);
            double reach = Noise3.Lerp(44, 58, size) * Noise3.Lerp(0.9, 1.1, r.NextDouble());
            int rooms = (int)Math.Round(Noise3.Lerp(f.MinCount, f.MaxCount, size * 0.6 + r.NextDouble() * 0.4));
            int tiers = size > 0.5 ? 3 : 2;
            double deepest = 13 + 10 * (tiers - 1) + 6;

            for (int attempt = 0; attempt < 400; attempt++)
            {
                Vec3d site = FormationGenerator.PickLocation(terrain, Where.Land, r);
                // A network has to stay above the sea, so on a world with one it goes under high ground: a quick look first.
                if (terrain.HasSea && terrain.Height(site) < terrain.SeaLevel + 30) continue;
                // The middle is deep under the ground, so only its ways in need gentle ground (see AddEntrance).
                if (terrain.InHole(site, reach + 12)) continue;
                // The whole network must be dry, so on a world with a sea it goes under high ground.
                double lowest = LowestGround(terrain, site, reach + 12, out double highest);
                // On a world with a sea every room and passage stays above the sea, so the caves are dry: under low
                // ground the levels are closer together, and fewer.
                int levels = tiers;
                // Levels closer together on small worlds, where there is less room to the side for the ramps.
                double spacing = Noise3.Lerp(7.5, 10, size);
                if (terrain.HasSea)
                {
                    double room = lowest - terrain.SeaLevel - DrySpare - 13 - 4;
                    if (room < 6) continue;
                    if (room < spacing * (tiers - 1)) { levels = 2; spacing = Math.Min(spacing, room); }
                }
                if (highest - lowest > 90) continue;
                var net = new CaveNetwork
                {
                    Style = style, Formation = f.Id, Seed = (uint)r.Next(),
                };
                if (net.Plan(terrain, f, r.Fork(attempt), site, reach, rooms, levels, spacing, lowest, size)) return net;
            }
            return null;
        }

        static double LowestGround(PlanetTerrain terrain, Vec3d site, double reach, out double highest)
        {
            double lowest = double.MaxValue;
            highest = double.MinValue;
            Vec3d e = site.AnyPerpendicular(), n = Vec3d.Cross(site, e).Normalized;
            for (int ring = 0; ring <= 4; ring++)
            {
                double out_ = reach * ring / 4.0;
                int around = ring == 0 ? 1 : 6 * ring;
                for (int i = 0; i < around; i++)
                {
                    double a = i * Math.PI * 2 / around;
                    Vec3d dir = (site * terrain.Radius + (e * Math.Cos(a) + n * Math.Sin(a)) * out_).Normalized;
                    double h = terrain.Height(dir);
                    lowest = Math.Min(lowest, h);
                    highest = Math.Max(highest, h);
                }
            }
            return lowest;
        }

        bool Plan(PlanetTerrain terrain, Formation f, SeededRandom r, Vec3d site, double reach, int rooms, int tiers, double spacing, double lowest, double size)
        {
            double R = terrain.Radius;
            Up = site;
            East = site.AnyPerpendicular().RotatedAround(site, r.NextDouble() * Math.PI * 2);
            North = Vec3d.Cross(site, East).Normalized;
            Origin = site * (R + terrain.Height(site));
            double floorRadius = R + lowest;   // every depth is measured down from the lowest ground over the network
            GroundLow = floorRadius;
            bool lava = Style == CaveStyle.LavaTube, ice = Style == CaveStyle.Ice;
            Lumps = lava ? 0.45 : ice ? 0.55 : 0.75;

            // ---------------------------------------------------------- rooms
            int guard = 0;
            while (Chambers.Count < rooms && guard++ < 400)
            {
                double a = r.NextDouble() * Math.PI * 2, out_ = reach * Math.Sqrt(r.NextDouble()) * 0.85;
                bool great = Chambers.Count == 1;
                double longR = (great ? r.Range(12f, 16f) : r.Range(6.5f, 11f)) * Noise3.Lerp(0.75, 1.0, size);
                double shortR = longR * r.Range(0.6f, 0.9f);
                double height0 = great ? r.Range(6.5f, 8.5f) : r.Range(3.6f, 5.4f);
                if (lava) { longR *= 1.15; height0 *= 0.85; }
                double jitter = r.Range(-1.5f, 1.5f);
                double spin = r.NextDouble() * Math.PI;
                // The level it is on: a seeded first choice, then the others, so a spot that is too close to a room
                // on one level can still take a room on another.
                int firstTier = Chambers.Count == 0 ? 0 : r.Range(0, tiers);
                int triedTiers = Chambers.Count == 0 ? 1 : tiers;
                for (int n = 0; n < triedTiers; n++)
                {
                    int tier = (firstTier + n) % tiers;
                    CaveChamber room = TryRoom(terrain, a, out_, tier, great, longR, shortR, height0, jitter, spin, spacing, lowest, floorRadius);
                    if (room == null) continue;
                    Chambers.Add(room);
                    break;
                }
            }
            if (Chambers.Count < 3) return false;
            bool levelsApart = tiers == 1;
            foreach (CaveChamber c in Chambers) if (c.Tier > 0) levelsApart = true;
            if (!levelsApart) return false;

            // ---------------------------------------------------------- passages between rooms
            // The rooms are joined the cheapest way first (a level walk is cheap, a climb is dear), so every room
            // can be reached; then a few more passages make loops, so there is more than one way round.
            int count = Chambers.Count;
            var joined = new bool[count];
            joined[0] = true;
            var pairs = new HashSet<long>();
            for (int k = 1; k < count; k++)
            {
                // The cheapest join to a room not yet reached that does not run over or under another passage;
                // only if every one would, the cheapest anyway.
                var options = new List<(double cost, int a, int b)>();
                for (int a = 0; a < count; a++)
                {
                    if (!joined[a]) continue;
                    for (int b = 0; b < count; b++)
                        if (!joined[b]) options.Add((JoinCost(Chambers[a], Chambers[b]), a, b));
                }
                options.Sort((x, y) => x.cost.CompareTo(y.cost));
                int made = -1;
                for (int o = 0; o < options.Count && o < 5 && made < 0; o++)
                    if (Join(options[o].a, options[o].b, r, pairs, optional: true)) made = o;
                if (made < 0) { made = 0; Join(options[0].a, options[0].b, r, pairs); }
                joined[options[made].b] = true;
            }
            int loops = Math.Max(1, count / 3);
            // The loops: rooms not yet joined to each other, nearest first (with a shuffle so it is not always the
            // same pair), as long as the passage keeps clear of the others.
            var loopOptions = new List<(double cost, int a, int b)>();
            for (int a = 0; a < count; a++)
                for (int b = a + 1; b < count; b++)
                {
                    if (pairs.Contains(Key(a, b))) continue;
                    if (Vec3d.Distance(Chambers[a].Centre, Chambers[b].Centre) > reach * 1.1) continue;
                    loopOptions.Add((JoinCost(Chambers[a], Chambers[b]) * r.Range(0.8f, 1.25f), a, b));
                }
            loopOptions.Sort((x, y) => x.cost.CompareTo(y.cost));
            for (int o = 0; o < loopOptions.Count && loops > 0; o++)
                if (Join(loopOptions[o].a, loopOptions[o].b, r, pairs, optional: true)) loops--;

            // A pit or chimney: the jetpack's way between levels.
            if (tiers > 1)
            {
                // The pair of rooms most nearly over one another, of those a shaft can join without breaking into
                // a passage on the way.
                var pairsByLevel = new List<(double level, int top, int bottom)>();
                for (int a = 0; a < count; a++)
                    for (int b = 0; b < count; b++)
                    {
                        if (Chambers[b].Tier <= Chambers[a].Tier) continue;
                        double level = LevelDistance(Chambers[a], Chambers[b]);
                        // Not two rooms a passage already joins: its ramp would run down beside the shaft into it.
                        if (level < reach && !pairs.Contains(Key(a, b))) pairsByLevel.Add((level, a, b));
                    }
                pairsByLevel.Sort((x, y) => x.level.CompareTo(y.level));
                for (int k = 0; k < pairsByLevel.Count && k < 6; k++)
                    if (AddShaft(pairsByLevel[k].top, pairsByLevel[k].bottom, r)) break;
            }

            // Side passages that wind off and end.
            int ends = r.Range(2, 5);
            for (int k = 0, made = 0; k < ends * 4 && made < ends; k++)
                if (AddDeadEnd(r.Range(0, count), r)) made++;

            // ---------------------------------------------------------- ways in
            if (!AddEntrance(terrain, r, -1)) return false;
            if (Style == CaveStyle.Karst && count >= 6) AddEntrance(terrain, r, Tunnels[Tunnels.Count - 1].To);
            // A hole in the roof of the great room lets the daylight in.
            if (r.Chance(lava ? 0.85 : 0.6)) AddSkylight(terrain, 1 < count ? 1 : 0, r);

            // No passage may break out of the ground anywhere but its own way in.
            foreach (CaveTunnel t in Tunnels)
            {
                if (t.Entrance) continue;
                for (int k = 0; k < t.Points.Length; k++)
                {
                    Vec3d dir = t.Points[k].Normalized;
                    double highest = R + terrain.Height(dir) - t.Radii[k] - 3.5;
                    if (t.Points[k].Magnitude > highest) t.Points[k] = dir * highest;
                    // And none dips into the sea.
                    double lowestDry = terrain.HasSea ? R + terrain.SeaLevel + DrySpare + t.Radii[k] : 0;
                    if (t.Points[k].Magnitude < lowestDry) t.Points[k] = dir * lowestDry;
                }
            }

            // ---------------------------------------------------------- what is inside
            Colour(terrain, f);
            int camps = 0;
            for (int i = 0; i < count; i++)
            {
                CaveChamber c = Chambers[i];
                // Enemies live in the bigger rooms, never in the one you walk into first, and never under the hole
                // in the roof (dropping in from above is a way in, not a fall into a camp).
                c.Camp = i != EntranceRoom && !c.Skylight && (i == 1 || r.Chance(0.55));
                if (c.Camp) camps++;
            }
            if (camps == 0)
                for (int i = 1; i < count && camps == 0; i++)
                    if (i != EntranceRoom && !Chambers[i].Skylight) { Chambers[i].Camp = true; camps++; }
            for (int i = 0; i < count; i++)
            {
                CaveChamber c = Chambers[i];
                if (c.Camp) AddCampLamps(i, r);
                AddSpikes(i, r);
                if (ice) AddCrystals(terrain, f, i, r);
                else if (Style == CaveStyle.Karst && f.MaxCount > 0) AddFloorStones(terrain, f, i, r);
            }

            Finish();
            BuildWalkGraph();
            return true;
        }

        CaveChamber TryRoom(PlanetTerrain terrain, double a, double out_, int tier, bool great, double longR, double shortR,
            double height0, double jitter, double spin, double spacing, double lowest, double floorRadius)
        {
            bool lava = Style == CaveStyle.LavaTube;
            double height = height0;
            double depth = 13 + spacing * tier + jitter;
            if (great) depth = Math.Max(depth, height + 7);
            // Above the sea, and a full roof over every room.
            double dryDepth = terrain.HasSea ? lowest - terrain.SeaLevel - DrySpare : double.MaxValue;
            double floorDrop = height * (lava ? 0.45 : 0.55);
            if (depth + floorDrop > dryDepth)
            {
                depth = dryDepth - floorDrop;
                height = Math.Min(height, depth - 5);
                floorDrop = height * (lava ? 0.45 : 0.55);
                if (height < 3.2) return null;
            }
            if (depth - height < 5) depth = height + 5;
            Vec3d level = East * (Math.Cos(a) * out_) + North * (Math.Sin(a) * out_);
            Vec3d dir = (Origin + level).Normalized;
            Vec3d centre = dir * (floorRadius - depth);
            Vec3d up = dir;
            Vec3d longWay = (East - up * Vec3d.Dot(East, up)).Normalized.RotatedAround(up, spin);
            var room = new CaveChamber
            {
                Centre = centre, Up = up, Long = longWay, Short = Vec3d.Cross(up, longWay).Normalized,
                LongRadius = longR, ShortRadius = shortR, Height = height, FloorDrop = floorDrop, Tier = tier,
            };
            // Rooms keep apart, so there is rock and a passage between them.
            bool clear = true;
            foreach (CaveChamber other in Chambers)
            {
                double gap = Vec3d.Distance(other.Centre, centre) - other.LongRadius - longR;
                if (gap < 5) { clear = false; break; }
                // On another level, either far enough off to the side for a walkable ramp between them, or
                // nearly over one another (a shaft's way down) with rock between.
                double climb = Math.Abs((other.Centre.Magnitude - other.FloorDrop) - (centre.Magnitude - floorDrop));
                if (climb < 3) continue;
                Vec3d off = other.Centre - centre;
                double levelGap = (off - up * Vec3d.Dot(off, up)).Magnitude - other.LongRadius - longR;
                bool stacked = (off - up * Vec3d.Dot(off, up)).Magnitude < Math.Min(other.ShortRadius, shortR) * 0.6;
                if (stacked && climb - Math.Max(height, other.Height) > 4) continue;
                if (levelGap < climb / 0.45 + 2) { clear = false; break; }
            }
            if (!clear) return null;
            if (terrain.Radius + terrain.Height(dir) - (centre.Magnitude + height) < 5) return null;
            return room;
        }

        int EntranceRoom = -1;

        static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        static double LevelDistance(CaveChamber a, CaveChamber b)
        {
            Vec3d d = b.Centre - a.Centre;
            return (d - a.Up * Vec3d.Dot(d, a.Up)).Magnitude;
        }

        static double JoinCost(CaveChamber a, CaveChamber b)
        {
            double climb = Math.Abs(a.Floor.Magnitude - b.Floor.Magnitude);
            return LevelDistance(a, b) + climb * 2.5;
        }

        /// <summary>A walkable passage from one room's floor to another's: no steeper than a steep path.</summary>
        bool Join(int a, int b, SeededRandom r, HashSet<long> pairs, bool optional = false)
        {
            pairs.Add(Key(a, b));
            CaveChamber ca = Chambers[a], cb = Chambers[b];
            double radius = Style == CaveStyle.LavaTube ? r.Range(3.0f, 4.2f) : r.Range(2.0f, 2.9f);
            double cut = Style == CaveStyle.LavaTube ? 0.45 : 0.55;
            // The passage's middle is held at floor height plus its own floor cut, so its floor meets the rooms'.
            // Each end starts part way out across its room's floor, toward the other room, so the passages do not
            // all meet in the middle of a room (where their floors, blended together, would sink).
            Vec3d start = Toward(ca, cb.Centre, 0.45) + ca.Up * (radius * cut + 0.2);
            Vec3d end = Toward(cb, ca.Centre, 0.45) + cb.Up * (radius * cut + 0.2);
            double level = LevelDistance(ca, cb);
            double climb = end.Magnitude - start.Magnitude;
            // Too steep to walk: it winds further, like a path up a hillside, until it is not. The climb is all
            // made between the rooms' walls (see KeepLevelInRooms), so that is the distance it has.
            double between = Math.Max(6, level - 1.05 * (DomeReach(ca, LevelToward(ca, cb.Centre)) + DomeReach(cb, LevelToward(cb, ca.Centre))));
            double wind = Math.Max(0, Math.Abs(climb) / 0.5 - between);
            CaveTunnel best = null;
            int bestClash = int.MaxValue;
            for (int attempt = 0; attempt < 6 && bestClash > 0; attempt++)
            {
                // Each try winds a little more, in case the last was still too steep.
                var points = Wander(start, end, r, (0.18 + wind / Math.Max(10, level) * 0.5) * (1 + 0.3 * attempt), radius);
                KeepLevelInRooms(points, a, b);
                var tunnel = new CaveTunnel { Points = points, Radii = Radii(points.Length, radius, r), From = a, To = b, FloorCut = cut };
                int clash = Clashes(tunnel) + TooSteep(points);
                if (clash < bestClash) { bestClash = clash; best = tunnel; }
            }
            if (bestClash > 0 && optional) { pairs.Remove(Key(a, b)); return false; }
            Tunnels.Add(best);
            return true;
        }

        /// <summary>
        /// How many of a new passage's points pass over or under another passage or room at a different level, so
        /// close that only a thin skin of rock would be left between them (or a hole where the two run into each
        /// other: a drop in the middle of a walk). Where it meets its own rooms does not count.
        /// </summary>
        int Clashes(CaveTunnel tunnel, bool anyHeight = false)
        {
            int clashes = 0;
            for (int i = 0; i < tunnel.Points.Length; i++)
            {
                Vec3d p = tunnel.Points[i];
                double rp = tunnel.Radii[i];
                // A shaft is checked all the way: it opens a pit wherever it comes up, even in its own rooms.
                if (!anyHeight && NearOwnRoom(tunnel, p, rp)) continue;
                bool clash = false;
                foreach (CaveTunnel other in Tunnels)
                {
                    bool shares = other.From >= 0 && (other.From == tunnel.From || other.From == tunnel.To)
                                  || other.To >= 0 && (other.To == tunnel.From || other.To == tunnel.To);
                    for (int k = 0; k < other.Points.Length && !clash; k++)
                    {
                        Vec3d q = other.Points[k];
                        if (!TooClose(p, rp, tunnel.FloorCut, q, other.Radii[k], other.FloorCut, anyHeight || other.Shaft)) continue;
                        if (shares && !anyHeight && NearOwnRoom(other, q, other.Radii[k])) continue;
                        // Out over a room's floor a passage is part of the room, which can have a pit in it.
                        if (anyHeight && (OverFloor(tunnel.From, q) || OverFloor(tunnel.To, q))) continue;
                        clash = true;
                    }
                    if (clash) break;
                }
                // Nor over or under itself, where it winds back on itself to ease a climb.
                for (int k = 0; k < tunnel.Points.Length && !clash; k++)
                {
                    double reach = rp + tunnel.Radii[k] + 1.5;
                    if (Math.Abs(k - i) * 1.5 < reach * 1.6) continue;
                    Vec3d q = tunnel.Points[k];
                    if (!TooClose(p, rp, tunnel.FloorCut, q, tunnel.Radii[k], tunnel.FloorCut, anyHeight)) continue;
                    if (NearOwnRoom(tunnel, q, tunnel.Radii[k])) continue;
                    clash = true;
                }
                for (int c = 0; c < Chambers.Count && !clash; c++)
                {
                    if (c == tunnel.From || c == tunnel.To) continue;
                    CaveChamber room = Chambers[c];
                    double reach = room.LongRadius + rp + 2.5;
                    if ((room.Centre - p).SqrMagnitude >= reach * reach) continue;
                    // Running into another room at its floor's height just makes one more way into it; over or
                    // under it, it would leave a skin of rock or a hole.
                    double floorHere = p.Magnitude - rp * tunnel.FloorCut;
                    if (anyHeight || Math.Abs(floorHere - room.Floor.Magnitude) > 1.5) clash = true;
                }
                if (clash) clashes++;
            }
            return clashes;
        }

        /// <summary>
        /// True if two stretches of passage at different heights would leave less than a metre and a half of rock
        /// between them, sideways and up and down both (each measured to the tube's real edge: its floor cut below,
        /// its squashed roof above). At much the same height they simply meet, which is a junction, not a clash,
        /// unless one is a shaft.
        /// </summary>
        static bool TooClose(Vec3d p, double rp, double cutP, Vec3d q, double rq, double cutQ, bool anyHeight)
        {
            const double Rock = 1.5;
            Vec3d up = ((p + q) * 0.5).Normalized;
            Vec3d d = q - p;
            double rise = Vec3d.Dot(d, up);
            double across = (d - up * rise).Magnitude;
            if (across - rp - rq >= Rock) return false;
            if (!anyHeight && Math.Abs(rise) < 2.0) return false;
            // q above p: rock from p's roof up to q's floor; q below: from q's roof up to p's floor.
            const double Roof = 0.894;   // the 1.25 squash: a passage's roof is this much of its radius over its middle
            double floorQ = cutQ > 0 ? cutQ * rq : rq, floorP = cutP > 0 ? cutP * rp : rp;
            double gap = rise >= 0 ? rise - Roof * rp - floorQ : -rise - Roof * rq - floorP;
            return gap < Rock;
        }

        /// <summary>How many stretches of a passage climb more steeply than a steep path (judged over three metres).</summary>
        static int TooSteep(Vec3d[] points)
        {
            int steep = 0;
            for (int i = 2; i < points.Length; i++)
            {
                Vec3d d = points[i] - points[i - 2];
                Vec3d up = points[i].Normalized;
                double rise = Vec3d.Dot(d, up);
                double level = (d - up * rise).Magnitude;
                if (Math.Abs(rise) > 0.62 * level + 0.2) steep++;
            }
            return steep;
        }

        /// <summary>True out over the floor of one of a passage's own rooms, where it is part of the room.</summary>
        bool NearOwnRoom(CaveTunnel tunnel, Vec3d p, double radius) => OverFloor(tunnel.From, p, 1.0) || OverFloor(tunnel.To, p, 1.0);

        Vec3d LevelToward(CaveChamber c, Vec3d somewhere)
        {
            Vec3d d = somewhere - c.Centre;
            d = d - c.Up * Vec3d.Dot(d, c.Up);
            return d.SqrMagnitude > 1e-9 ? d.Normalized : c.Long;
        }

        /// <summary>
        /// Holds a passage level while it is still over a room's floor, and makes the whole climb between the rooms'
        /// walls, evenly. Otherwise a ramp would still be high when it came out over the room, and end in a ledge
        /// with a drop to the floor.
        /// </summary>
        void KeepLevelInRooms(Vec3d[] points, int a, int b)
        {
            int n = points.Length;
            double startHeight = points[0].Magnitude, endHeight = points[n - 1].Magnitude;
            int lastInA = 0, firstInB = n - 1;
            while (lastInA < n - 1 && UnderRoof(a, points[lastInA + 1])) lastInA++;
            while (firstInB > lastInA + 1 && UnderRoof(b, points[firstInB - 1])) firstInB--;
            var walked = new double[n];
            for (int i = 1; i < n; i++)
            {
                Vec3d d = points[i] - points[i - 1];
                Vec3d up = points[i].Normalized;
                walked[i] = walked[i - 1] + (d - up * Vec3d.Dot(d, up)).Magnitude;
            }
            double span = Math.Max(1e-6, walked[firstInB] - walked[lastInA]);
            for (int i = 0; i < n; i++)
            {
                double t = Noise3.Clamp01((walked[i] - walked[lastInA]) / span);
                points[i] = points[i].Normalized * (startHeight + (endHeight - startHeight) * t);
            }
        }

        /// <summary>
        /// True anywhere under a room's dome, out to its widest (which is wider than its floor, as the dome's
        /// widest part is above the floor): a passage still sloping there would come out over the floor as a ledge.
        /// </summary>
        static double DomeReach(CaveChamber c, Vec3d level)
        {
            double a = Vec3d.Dot(level, c.Long) / c.LongRadius, b = Vec3d.Dot(level, c.Short) / c.ShortRadius;
            double k = Math.Sqrt(a * a + b * b);
            return k > 1e-9 ? 1 / k : c.LongRadius;
        }

        bool UnderRoof(int room, Vec3d p)
        {
            if (room < 0) return false;
            CaveChamber c = Chambers[room];
            Vec3d d = p - c.Centre;
            double a = Vec3d.Dot(d, c.Long) / c.LongRadius, b = Vec3d.Dot(d, c.Short) / c.ShortRadius;
            return a * a + b * b < 1.05 * 1.05;
        }

        bool OverFloor(int room, Vec3d p, double margin = 0)
        {
            if (room < 0) return false;
            CaveChamber c = Chambers[room];
            Vec3d d = p - c.Centre;
            d = d - c.Up * Vec3d.Dot(d, c.Up);
            double reach = d.SqrMagnitude > 1e-9 ? c.FloorReach(d.Normalized) : 1;
            return d.Magnitude < reach * 0.9 + margin;
        }

        /// <summary>A point on a room's floor part of the way to its wall, in the direction of somewhere else.</summary>
        static Vec3d Toward(CaveChamber c, Vec3d somewhere, double fraction)
        {
            Vec3d d = somewhere - c.Centre;
            d = d - c.Up * Vec3d.Dot(d, c.Up);
            if (d.SqrMagnitude < 1e-6) return c.Floor;
            d = d.Normalized;
            return c.Floor + d * (c.FloorReach(d) * fraction);
        }

        bool AddShaft(int top, int bottom, SeededRandom r)
        {
            CaveChamber ct = Chambers[top], cb = Chambers[bottom];
            double radius = r.Range(2.2f, 3.0f);
            // From the low room's floor straight up into the high room, through its floor.
            // Its round bottom sits on the low room's floor rather than sinking a hollow into it.
            Vec3d bottomPoint = cb.Floor + cb.Up * (radius * 0.95);
            Vec3d level = bottomPoint.Normalized;
            Vec3d topPoint = level * (ct.Floor.Magnitude + 2.0);
            // Joined at the top by a short passage over to the high room if they are not stacked.
            var list = new List<Vec3d>();
            double rise = topPoint.Magnitude - bottomPoint.Magnitude;
            int steps = Math.Max(3, (int)(rise / 1.5));
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                Vec3d p = bottomPoint + (topPoint - bottomPoint) * t;
                p += Ortho(level, Seed + 31, t * 3) * 0.6;
                list.Add(p);
            }
            var shaft = new CaveTunnel { Points = list.ToArray(), Radii = Radii(list.Count, radius, r), From = bottom, To = top, Shaft = true, FloorCut = 0 };
            if (Clashes(shaft, anyHeight: true) > 0) return false;
            CaveTunnel join = null;
            if (LevelDistance(ct, cb) > ct.ShortRadius * 0.6)
            {
                double side = Style == CaveStyle.LavaTube ? 3.2 : 2.4;
                Vec3d start = topPoint - level * 0.6;
                Vec3d end = ct.Floor + ct.Up * (side * 0.55 + 0.2);
                var points = Wander(start, end, r, 0.12, side);
                join = new CaveTunnel { Points = points, Radii = Radii(points.Length, side, r), From = bottom, To = top };
                if (Clashes(join) > 0) return false;
                join.From = -1;
            }
            Tunnels.Add(shaft);
            if (join != null) Tunnels.Add(join);
            return true;
        }

        bool AddDeadEnd(int room, SeededRandom r)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                CaveTunnel tunnel = DeadEnd(room, r);
                if (Clashes(tunnel) > 0) continue;
                Tunnels.Add(tunnel);
                return true;
            }
            return false;
        }

        CaveTunnel DeadEnd(int room, SeededRandom r)
        {
            CaveChamber c = Chambers[room];
            double a = r.NextDouble() * Math.PI * 2;
            Vec3d level = c.Long * Math.Cos(a) + c.Short * Math.Sin(a);
            double radius = r.Range(1.6f, 2.3f);
            Vec3d start = c.Floor + level * (c.FloorReach(level) * 0.5) + c.Up * (radius * 0.55 + 0.2);
            double length = r.Range(14f, 26f);
            Vec3d end = start + level * (c.FloorReach(level) * 0.5 + length);
            // Rising or falling a little, never out of the rock.
            end = end.Normalized * (end.Magnitude + r.Range(-3f, 2f));
            var points = Wander(start, end, r, 0.25, radius);
            var radii = Radii(points.Length, radius, r);
            // It narrows to nothing.
            for (int i = 0; i < radii.Length; i++)
            {
                double t = i / (double)(radii.Length - 1);
                radii[i] *= 1 - 0.7 * Noise3.Smooth(0.6, 1.0, t);
            }
            return new CaveTunnel { Points = points, Radii = radii, From = room, To = -1 };
        }

        /// <summary>A sloping walk-in passage from the surface down to a room on the top level.</summary>
        bool AddEntrance(PlanetTerrain terrain, SeededRandom r, int avoid)
        {
            double R = terrain.Radius;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                // A top-level room near the edge, leading out the way away from the middle.
                int room = -1;
                double far = -1;
                for (int i = 0; i < Chambers.Count; i++)
                {
                    if (i == avoid || Chambers[i].Tier != 0) continue;
                    double d = LevelDistance(Chambers[0], Chambers[i]) + r.NextDouble() * 12;
                    if (d > far) { far = d; room = i; }
                }
                if (room < 0) room = 0;
                CaveChamber c = Chambers[room];
                Vec3d outward = c.Centre - Origin;
                outward = outward - c.Up * Vec3d.Dot(outward, c.Up);
                outward = outward.SqrMagnitude > 1 ? outward.Normalized : c.Long;
                outward = outward.RotatedAround(c.Up, r.Range(-1.1f, 1.1f));
                double radius = Style == CaveStyle.LavaTube ? r.Range(3.4f, 4.2f) : r.Range(2.8f, 3.4f);
                const double Slope = 0.5;
                Vec3d start = c.Floor + c.Up * (radius * 0.55 + 0.2);
                // Walk out until the passage, rising at the slope, meets the ground.
                Vec3d mouthDir = Vec3d.Zero;
                double run = c.FloorReach(outward);
                double climbStart = start.Magnitude;
                bool found = false;
                for (int i = 0; i < 120; i++)
                {
                    run += 0.75;
                    Vec3d dir = (start + outward * run).Normalized;
                    double rise = (run - c.FloorReach(outward)) * Slope;
                    double ground = R + terrain.Height(dir);
                    if (climbStart + rise >= ground - radius * 0.3)
                    {
                        mouthDir = dir;
                        found = true;
                        break;
                    }
                }
                if (!found) continue;
                if (terrain.SlopeAt(mouthDir, 4) > 0.45) continue;
                if (terrain.HasSea && terrain.Height(mouthDir) < terrain.SeaLevel + 3) continue;
                if (terrain.InHole(mouthDir, 10)) continue;
                bool crowded = false;
                foreach (CaveOpening o in Openings) if ((o.Direction - mouthDir).Magnitude * R < 25) crowded = true;
                if (crowded) continue;

                // The passage: from the room, rising steadily, and out a couple of metres above the ground.
                Vec3d end = mouthDir * (R + terrain.Height(mouthDir) + radius * 0.6);
                var points = new List<Vec3d>();
                double length = Vec3d.Distance(start, end);
                int steps = Math.Max(4, (int)(length / 1.5));
                for (int i = 0; i <= steps; i++)
                {
                    double t = i / (double)steps;
                    Vec3d p = start + (end - start) * t;
                    // A gentle bend, none at the ends.
                    p += Ortho((end - start).Normalized, Seed + (uint)attempt, t * 2.2) * (Math.Sin(t * Math.PI) * length * 0.08);
                    points.Add(p);
                }
                Tunnels.Add(new CaveTunnel { Points = points.ToArray(), Radii = Radii(points.Count, radius, r, 0.1), From = room, To = -1, Entrance = true, FloorCut = 0.55 });
                // The hole is long because the passage meets the ground at a slant: it covers the whole of that.
                double slant = radius / Slope * 0.5;
                Vec3d holeMiddle = (mouthDir * R - outward * slant * 0.6).Normalized;
                Openings.Add(new CaveOpening { Direction = holeMiddle, Radius = radius + slant * 0.75 + 0.8, Skylight = false });
                if (EntranceRoom < 0) EntranceRoom = room;
                return true;
            }
            return false;
        }

        void AddSkylight(PlanetTerrain terrain, int room, SeededRandom r)
        {
            double R = terrain.Radius;
            CaveChamber c = Chambers[room];
            Vec3d dir = (c.Centre + c.Long * (c.LongRadius * r.Range(-0.3f, 0.3f))).Normalized;
            if (terrain.SlopeAt(dir, 4) > 0.5) return;
            if (terrain.HasSea && terrain.Height(dir) < terrain.SeaLevel + 3) return;
            foreach (CaveOpening o in Openings) if ((o.Direction - dir).Magnitude * R < 22) return;
            if (terrain.InHole(dir, 10)) return;
            double radius = r.Range(2.6f, 3.8f);
            double top = R + terrain.Height(dir) + 2;
            Vec3d bottom = dir * (c.Centre.Magnitude + c.Height * 0.5);
            var list = new List<Vec3d>();
            int steps = Math.Max(3, (int)((top - bottom.Magnitude) / 1.5));
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                list.Add(dir * Noise3.Lerp(bottom.Magnitude, top, t) + Ortho(dir, Seed + 77, t * 2) * 0.5);
            }
            var radii = Radii(list.Count, radius, r);
            // A collapse: wider where it opens into the room.
            radii[0] *= 1.35;
            Tunnels.Add(new CaveTunnel { Points = list.ToArray(), Radii = radii, From = room, To = -1, Shaft = true, Entrance = true, FloorCut = 0 });
            Openings.Add(new CaveOpening { Direction = dir, Radius = radius + 1.0, Skylight = true });
            c.Skylight = true;
        }

        /// <summary>A curving line between two points: a few bends set by the seed, sampled every metre and a half.</summary>
        Vec3d[] Wander(Vec3d start, Vec3d end, SeededRandom r, double bend, double radius)
        {
            Vec3d along = end - start;
            double length = along.Magnitude;
            Vec3d up = ((start + end) * 0.5).Normalized;
            Vec3d side = Vec3d.Cross(up, along.Normalized).Normalized;
            double s1 = r.Range(-1f, 1f) * bend * length, s2 = r.Range(-1f, 1f) * bend * length;
            double u1 = r.Range(-1f, 1f) * Math.Min(2.5, length * 0.05), u2 = r.Range(-1f, 1f) * Math.Min(2.5, length * 0.05);
            Vec3d p0 = start, p3 = end;
            Vec3d p1 = start + along * 0.33 + side * s1 + up * u1;
            Vec3d p2 = start + along * 0.66 + side * s2 + up * u2;
            int steps = Math.Max(3, (int)(length * (1 + bend) / 1.5));
            var points = new Vec3d[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps, m = 1 - t;
                points[i] = p0 * (m * m * m) + p1 * (3 * m * m * t) + p2 * (3 * m * t * t) + p3 * (t * t * t);
            }
            return points;
        }

        double[] Radii(int count, double radius, SeededRandom r, double vary = 0.18)
        {
            var radii = new double[count];
            uint s = (uint)r.Next();
            for (int i = 0; i < count; i++) radii[i] = radius * (1 + vary * Noise3.Perlin(i * 0.17, 0.3, 0.7, s));
            return radii;
        }

        static Vec3d Ortho(Vec3d axis, uint seed, double t)
        {
            Vec3d a = axis.AnyPerpendicular(), b = Vec3d.Cross(axis, a).Normalized;
            return a * Noise3.Perlin(t, 0.1, 0.2, seed) + b * Noise3.Perlin(t, 5.1, 0.2, seed);
        }

        void AddCampLamps(int room, SeededRandom r)
        {
            CaveChamber c = Chambers[room];
            int lamps = c.LongRadius > 11 ? 2 : 1;
            double start = r.NextDouble() * Math.PI * 2;
            for (int i = 0; i < lamps; i++)
            {
                double a = start + i * Math.PI + r.Range(-0.4f, 0.4f);
                Vec3d foot = RoomFloorPoint(room, a, 0.62);
                Vec3d toMiddle = c.Floor - foot;
                toMiddle = (toMiddle - c.Up * Vec3d.Dot(toMiddle, c.Up));
                Lamps.Add(new CaveLamp
                {
                    // Work lamps: a warm, slightly orange light, like old sodium or halogen site lamps.
                    Position = foot, Up = c.Up, R = 1.0f, G = 0.74f, B = 0.46f, Strength = 2.6f, Reach = 17f,
                    Chamber = room, Facing = toMiddle.SqrMagnitude > 1e-6 ? toMiddle.Normalized : c.Long,
                });
            }
        }

        /// <summary>True if something standing here would be in a passage's way where it opens into a room.</summary>
        bool InWayOfAPassage(Vec3d p, double margin)
        {
            foreach (CaveTunnel t in Tunnels)
            {
                if (t.Shaft) continue;
                for (int i = 0; i < t.Points.Length; i++)
                {
                    double reach = t.Radii[i] + margin;
                    if ((t.Points[i] - p).SqrMagnitude < reach * reach) return true;
                }
            }
            return false;
        }

        void AddSpikes(int room, SeededRandom r)
        {
            CaveChamber c = Chambers[room];
            if (Style == CaveStyle.LavaTube && !r.Chance(0.3)) return;
            int count = Style == CaveStyle.Karst ? r.Range(4, 9) : r.Range(1, 4);
            for (int i = 0; i < count; i++)
            {
                // The middle of the floor is kept clear: it is where people stand and walk through.
                double a = r.NextDouble() * Math.PI * 2, out_ = Noise3.Lerp(0.4, 0.85, Math.Sqrt(r.NextDouble()));
                Vec3d level = c.Long * Math.Cos(a) + c.Short * Math.Sin(a);
                double reachHere = c.FloorReach(level) * out_;
                // How high the roof is over this spot.
                double ka = Vec3d.Dot(level, c.Long) / c.LongRadius, kb = Vec3d.Dot(level, c.Short) / c.ShortRadius;
                double q = reachHere * Math.Sqrt(ka * ka + kb * kb);
                double roof = c.Height * Math.Sqrt(Math.Max(0, 1 - q * q));
                double clear = roof + c.FloorDrop;
                if (clear < 3) continue;
                bool hanging = r.Chance(0.6);
                double length = Math.Min(clear * r.Range(0.25f, 0.45f), 4.5);
                double radius = Math.Max(0.55, length * r.Range(0.22f, 0.32f));
                Vec3d foot = c.Floor + level * reachHere;
                if (!hanging && InWayOfAPassage(foot, 1.2)) continue;
                if (hanging)
                {
                    Spikes.Add(new CaveSpike { Base = foot + c.Up * (clear + 0.6), Toward = -c.Up, Length = length + 0.6, Radius = radius });
                }
                else
                {
                    Spikes.Add(new CaveSpike { Base = foot - c.Up * 0.3, Toward = c.Up, Length = length * 0.8, Radius = radius * 1.2 });
                }
            }
        }

        void AddCrystals(PlanetTerrain terrain, Formation f, int room, SeededRandom r)
        {
            CaveChamber c = Chambers[room];
            int count = r.Range(3, 7);
            for (int i = 0; i < count; i++)
            {
                double a = r.NextDouble() * Math.PI * 2;
                Vec3d at = RoomFloorPoint(room, a, r.Range(0.68f, 0.9f));
                double scale = r.Range(0.9f, 2.6f);
                if (InWayOfAPassage(at, scale * 0.4 + 0.8)) continue;
                terrain.Placements.Add(new Placement
                {
                    Direction = at.Normalized, Absolute = true, Position = at - c.Up * 0.2, Kind = FeatureKind.Crystal, Variant = r.Range(0, 1024),
                    Scale = scale, Yaw = r.NextDouble() * Math.PI * 2, R = 0.62f, G = 0.84f, B = 0.95f,
                });
                // The crystals glow a little: the only light in a room with nobody in it.
                Lamps.Add(new CaveLamp
                {
                    Position = at + c.Up * (scale * 0.5), Up = c.Up, R = 0.35f, G = 0.72f, B = 1.0f, Strength = 0.5f, Reach = 7f,
                    Crystal = true, Chamber = room, Facing = c.Long,
                });
            }
        }

        void AddFloorStones(PlanetTerrain terrain, Formation f, int room, SeededRandom r)
        {
            CaveChamber c = Chambers[room];
            int count = r.Range(2, 6);
            for (int i = 0; i < count; i++)
            {
                double a = r.NextDouble() * Math.PI * 2;
                Vec3d at = RoomFloorPoint(room, a, r.Range(0.68f, 0.92f));
                double t = r.NextDouble();
                if (InWayOfAPassage(at, f.MaxScale * 0.4 + 0.8)) continue;
                terrain.Placements.Add(new Placement
                {
                    Direction = at.Normalized, Absolute = true, Position = at - c.Up * 0.15, Kind = f.Feature, Variant = r.Range(0, 1024),
                    Scale = Noise3.Lerp(f.MinScale, f.MaxScale, t * t), Yaw = r.NextDouble() * Math.PI * 2,
                    R = R * 1.05f, G = G * 1.02f, B = B,
                });
            }
        }

        void Colour(PlanetTerrain terrain, Formation f)
        {
            float[] rock;
            float[] floor;
            switch (Style)
            {
                case CaveStyle.LavaTube:
                    // Basalt: dark grey, a little of the world's own colour in it; dust blown in on the floor.
                    float[] cliff = ColorUtil.Parse(terrain.Profile.cliffColor);
                    rock = new[] { cliff[0] * 0.45f + 0.30f * 0.55f, cliff[1] * 0.45f + 0.29f * 0.55f, cliff[2] * 0.45f + 0.28f * 0.55f };
                    float[] low = ColorUtil.Parse(terrain.Profile.lowColor);
                    floor = new[] { low[0] * 0.7f, low[1] * 0.7f, low[2] * 0.7f };
                    break;
                case CaveStyle.Ice:
                    rock = ColorUtil.Parse(string.IsNullOrEmpty(f.Tint) ? "#9EC2D6" : f.Tint);
                    floor = ColorUtil.Parse("#B9C8CF");
                    break;
                default:
                    // Limestone, greyed and warm; mud and clay on the floor.
                    rock = ColorUtil.Parse(string.IsNullOrEmpty(f.Tint) ? "#958C7E" : f.Tint);
                    floor = ColorUtil.Parse("#62574A");
                    break;
            }
            R = rock[0]; G = rock[1]; B = rock[2];
            FloorR = floor[0]; FloorG = floor[1]; FloorB = floor[2];
        }

        /// <summary>Works out the pieces of the distance sum and the box round them.</summary>
        void Finish()
        {
            var pieces = new List<Piece>();
            double pad = Lumps * 2 + Blend + 1;
            for (int i = 0; i < Chambers.Count; i++)
            {
                CaveChamber c = Chambers[i];
                double reach = Math.Max(c.LongRadius, Math.Max(c.ShortRadius, c.Height)) + pad;
                pieces.Add(new Piece
                {
                    Ball = true, Index = i, A = c.Centre, Up = c.Up,
                    Min = c.Centre - new Vec3d(reach, reach, reach), Max = c.Centre + new Vec3d(reach, reach, reach),
                });
            }
            for (int t = 0; t < Tunnels.Count; t++)
            {
                CaveTunnel tunnel = Tunnels[t];
                for (int k = 0; k + 1 < tunnel.Points.Length; k++)
                {
                    Vec3d a = tunnel.Points[k], b = tunnel.Points[k + 1];
                    double reach = Math.Max(tunnel.Radii[k], tunnel.Radii[k + 1]) * 1.15 + pad;
                    pieces.Add(new Piece
                    {
                        Ball = false, Index = t, A = a, B = b, RadiusA = tunnel.Radii[k], RadiusB = tunnel.Radii[k + 1],
                        Up = ((a + b) * 0.5).Normalized, FloorCut = tunnel.FloorCut,
                        Min = new Vec3d(Math.Min(a.X, b.X) - reach, Math.Min(a.Y, b.Y) - reach, Math.Min(a.Z, b.Z) - reach),
                        Max = new Vec3d(Math.Max(a.X, b.X) + reach, Math.Max(a.Y, b.Y) + reach, Math.Max(a.Z, b.Z) + reach),
                    });
                }
            }
            _pieces = pieces.ToArray();

            Vec3d min = new Vec3d(double.MaxValue, double.MaxValue, double.MaxValue);
            Vec3d max = new Vec3d(double.MinValue, double.MinValue, double.MinValue);
            foreach (Piece s in _pieces)
            {
                // The corners of each piece's box, in the local frame.
                for (int c = 0; c < 8; c++)
                {
                    Vec3d corner = new Vec3d((c & 1) == 0 ? s.Min.X : s.Max.X, (c & 2) == 0 ? s.Min.Y : s.Max.Y, (c & 4) == 0 ? s.Min.Z : s.Max.Z);
                    Vec3d l = ToLocal(corner);
                    min = new Vec3d(Math.Min(min.X, l.X), Math.Min(min.Y, l.Y), Math.Min(min.Z, l.Z));
                    max = new Vec3d(Math.Max(max.X, l.X), Math.Max(max.Y, l.Y), Math.Max(max.Z, l.Z));
                }
            }
            LocalMin = min;
            LocalMax = max;
        }

        /// <summary>
        /// Points enemies walk between: the middle of each room's floor, a ring round it, and points every few
        /// metres along each walkable passage, joined where they can be walked between.
        /// </summary>
        void BuildWalkGraph()
        {
            for (int i = 0; i < Chambers.Count; i++)
            {
                // The middle and a ring round it, so passages that open at the walls are joined up. A point over a
                // hole in the floor (the top of a shaft) is left out, and two are joined only where the floor
                // between them can be walked.
                int first = Waypoints.Count;
                TryRoomWaypoint(Chambers[i].Floor, i);
                for (int k = 0; k < 6; k++) TryRoomWaypoint(RoomFloorPoint(i, k * Math.PI / 3, 0.55), i);
                for (int a = first; a < Waypoints.Count; a++)
                    for (int b = a + 1; b < Waypoints.Count; b++)
                        if (ClearBetween(Waypoints[a], Waypoints[b])) Link(a, b);
            }
            foreach (CaveTunnel t in Tunnels)
            {
                if (t.Shaft) continue;
                int previous = -1, firstOfTunnel = -1;
                for (int k = 0; k < t.Points.Length; k += 2)
                {
                    // Every three metres or so; the point between if that one cannot be stood on.
                    if (!PassageFoot(t, k, out Vec3d foot) && !(k + 1 < t.Points.Length && PassageFoot(t, k + 1, out foot))) continue;
                    int w = AddWaypoint(foot, -1);
                    // Only the next one along, and over a gap (where a stretch could not be stood on) only if it can
                    // be walked straight across.
                    if (previous >= 0)
                    {
                        double gap = Vec3d.Distance(Waypoints[previous], foot);
                        if (gap < 5.5 || gap < 10 && ClearBetween(Waypoints[previous], foot)) Link(previous, w);
                    }
                    if (firstOfTunnel < 0) firstOfTunnel = w;
                    previous = w;
                }
                // Each end joins the nearest point of the room it opens into.
                if (previous < 0) continue;
                if (t.From >= 0) JoinToRoom(firstOfTunnel, t.From);
                if (t.To >= 0) JoinToRoom(previous, t.To);
            }
        }

        void TryRoomWaypoint(Vec3d onFloor, int room)
        {
            Vec3d up = onFloor.Normalized;
            if (!FloorBelow(onFloor + up * 1.0, out Vec3d floor, 2.5)) return;
            // Room to stand: not up against a stone spike or under a low lip of roof.
            if (Air(floor + up * 1.0) > -0.5 || Air(floor + up * 1.7) > -0.3) return;
            AddWaypoint(floor + up * 0.1, room);
        }

        int AddWaypoint(Vec3d p, int room)
        {
            Waypoints.Add(p);
            Links.Add(new List<int>());
            WaypointRoom.Add(room);
            return Waypoints.Count - 1;
        }

        void Link(int a, int b)
        {
            if (a == b || a < 0 || b < 0) return;
            if (!Links[a].Contains(b)) Links[a].Add(b);
            if (!Links[b].Contains(a)) Links[b].Add(a);
        }

        bool Linked(int a, int b) => Links[a].Contains(b);

        /// <summary>Where someone would stand on a passage's floor under one of its points, if there is room to.</summary>
        bool PassageFoot(CaveTunnel t, int k, out Vec3d foot)
        {
            Vec3d p = t.Points[k];
            Vec3d up = p.Normalized;
            foot = p - up * (t.FloorCut * t.Radii[k] - 0.1);
            if (Air(foot + up * 0.8) > -0.2) return false;
            // On the real floor, which the blending and the lumps move from where the plan put it.
            if (!FloorBelow(foot + up * 1.0, out Vec3d floor, 4.5)) return false;
            foot = floor + up * 0.1;
            return Air(foot + up * 1.0) < -0.5 && Air(foot + up * 1.7) < -0.3;
        }

        /// <summary>A passage's end waypoint to the nearest of a room's, if it can be walked there.</summary>
        void JoinToRoom(int w, int room)
        {
            int nearest = NearestOfRoom(room, Waypoints[w]);
            if (nearest < 0) return;
            double d = Vec3d.Distance(Waypoints[w], Waypoints[nearest]);
            if (d < 3 || d < 12 && ClearBetween(Waypoints[w], Waypoints[nearest])) Link(w, nearest);
        }

        int NearestOfRoom(int room, Vec3d p)
        {
            int best = -1;
            double bestD = double.MaxValue;
            for (int i = 0; i < Waypoints.Count; i++)
            {
                if (WaypointRoom[i] != room) continue;
                double d = (Waypoints[i] - p).SqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // ------------------------------------------------------------------ as a cave

        /// <summary>
        /// The network as one of the planet's caves, so everything that already knows about caves (holes in the
        /// ground, "in a cave", the safety net under the ground) knows about it too. Its circles are the rooms and
        /// the passages; its first is the main way in.
        /// </summary>
        public Cave AsCave()
        {
            var centres = new List<Vec3d>();
            var radii = new List<double>();
            foreach (CaveTunnel t in Tunnels)
            {
                if (!t.Entrance || t.Shaft) continue;
                centres.Add(t.Points[t.Points.Length - 1]);
                radii.Add(t.Radii[t.Radii.Length - 1]);
                break;
            }
            foreach (CaveChamber c in Chambers)
            {
                centres.Add(c.Centre);
                radii.Add(Math.Min(c.Height, c.ShortRadius));
            }
            foreach (CaveTunnel t in Tunnels)
            {
                for (int k = 0; k < t.Points.Length; k += 2)
                {
                    centres.Add(t.Points[k]);
                    radii.Add(t.Radii[k]);
                }
            }
            var mouths = new Vec3d[Openings.Count];
            double mouth = 0;
            for (int i = 0; i < Openings.Count; i++)
            {
                mouths[i] = Openings[i].Direction;
                mouth = Math.Max(mouth, Openings[i].Radius);
            }
            return new Cave
            {
                Centres = centres.ToArray(), Radii = radii.ToArray(), Mouths = mouths, MouthRadius = mouth,
                R = R, G = G, B = B, Formation = Formation, Network = this,
            };
        }
    }
}

namespace OrbitalAnte.Planets
{
    /// <summary>What one cave network may cost (see "Cave networks" in docs/performance_budget.md).</summary>
    public static class CaveBudget
    {
        /// <summary>Wall triangles of one network. Only the blocks in view are drawn.</summary>
        public const int MostTriangles = 140000;
        /// <summary>
        /// Size of one network's light box, in bytes. A quarter over one megabyte: the box reaches up to the
        /// ground at each way in, and a ridge or rock tower standing there (19 Nov 2026) makes it a little taller.
        /// </summary>
        public const int MostLightBytes = 5 << 18;
    }
}
