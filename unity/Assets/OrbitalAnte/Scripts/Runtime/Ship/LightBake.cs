using System;
using System.Collections.Generic;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// Bakes the cabin's lighting. It follows light the way it really travels: out of each lamp, onto the
    /// surfaces it can see, and on from those surfaces to others, bounce after bounce (this is "path tracing").
    /// The answer is worked out once and stored as a colour at every corner of the cabin's meshes, so while
    /// playing the soft, bounced light costs nothing at all.
    ///
    /// Only the cabin's own lamps are baked. Sunlight through the windows changes from moment to moment and
    /// is not part of this.
    ///
    /// The same input always gives the same output. No Unity types; safe on a background thread.
    /// </summary>
    public sealed class LightBake
    {
        /// <summary>Surfaces glowing at least this much are lamps and light the room; fainter ones only show lit themselves.</summary>
        public const float CastsLightFrom = 1.3f;

        public sealed class Settings
        {
            /// <summary>Bounced-light paths followed from each corner. More is smoother and slower.</summary>
            public int Paths = 64;
            /// <summary>Straight-line checks to the lamps from each corner.</summary>
            public int LampChecks = 24;
            /// <summary>How many times light may bounce.</summary>
            public int Bounces = 3;
            /// <summary>The typical lit wall ends up this bright on screen (0 to 1).</summary>
            public float TargetBrightness = 0.5f;
            /// <summary>The least light anything gets, so deep corners are dim rather than pure black.</summary>
            public float Floor = 0.02f;
            public ulong Seed = 20261112;
            /// <summary>How many times neighbouring corners are averaged together, to smooth away speckle.</summary>
            public int Smoothing = 3;
            /// <summary>Processor cores to use: 0 for all of them, 1 to stay on the calling thread.</summary>
            public int Threads = 0;
        }

        // Every triangle of every mesh, flattened.
        readonly List<ShipMesh> _meshes = new List<ShipMesh>();
        P3[] _a, _edge1, _edge2, _normal;
        P3[] _albedo;       // linear, per triangle (the average of its corners)
        P3[] _emit;         // linear light given off, per triangle
        float[] _area;
        int[] _meshOf, _corner;   // which mesh each triangle came from, and its three corners
        int _count;

        // The lamps: triangles that give off light, with a running total for picking one in proportion.
        int[] _lamps;
        float[] _lampSum;
        float _lampTotal;

        // The search tree.
        struct Node { public P3 Min, Max; public int Left, Right, First, Count; }
        Node[] _nodes;
        int _nodeCount;
        int[] _order;

        public int TriangleCount => _count;

        public void Add(ShipMesh mesh) => _meshes.Add(mesh);

        /// <summary>Screen colour (0 to 1) to linear light.</summary>
        public static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4);
        public static float ToScreen(float c) => c <= 0.0031308f ? Math.Max(0f, c) * 12.92f : 1.055f * (float)Math.Pow(c, 1.0 / 2.4) - 0.055f;
        static P3 ToLinear(P3 c) => new P3(ToLinear(c.X), ToLinear(c.Y), ToLinear(c.Z));

        // ------------------------------------------------------------------ set-up

        void Prepare()
        {
            _count = 0;
            for (int m = 0; m < _meshes.Count; m++) _count += _meshes[m].TriangleCount;
            _a = new P3[_count]; _edge1 = new P3[_count]; _edge2 = new P3[_count]; _normal = new P3[_count];
            _albedo = new P3[_count]; _emit = new P3[_count]; _area = new float[_count];
            _meshOf = new int[_count]; _corner = new int[_count * 3];
            var lamps = new List<int>();
            int t = 0;
            for (int m = 0; m < _meshes.Count; m++)
            {
                ShipMesh mesh = _meshes[m];
                for (int k = 0; k < mesh.Triangles.Length; k += 3, t++)
                {
                    int i0 = mesh.Triangles[k], i1 = mesh.Triangles[k + 1], i2 = mesh.Triangles[k + 2];
                    P3 a = mesh.Position(i0), b = mesh.Position(i1), c = mesh.Position(i2);
                    _a[t] = a; _edge1[t] = b - a; _edge2[t] = c - a;
                    _meshOf[t] = m; _corner[t * 3] = i0; _corner[t * 3 + 1] = i1; _corner[t * 3 + 2] = i2;
                    P3 cross = P3.Cross(_edge1[t], _edge2[t]);
                    _area[t] = cross.Length * 0.5f;
                    _normal[t] = cross.Unit();
                    P3 paint = (ToLinear(mesh.Color(i0)) + ToLinear(mesh.Color(i1)) + ToLinear(mesh.Color(i2))) * (1f / 3f);
                    float glow = (mesh.Glow[i0] + mesh.Glow[i1] + mesh.Glow[i2]) / 3f;
                    _albedo[t] = paint * 0.85f;
                    // Faint glows (a screen, a pilot light, a sticker star) show lit but light nothing else:
                    // they are tiny, and a tiny lamp close to a wall bakes as coloured speckle.
                    bool casts = glow >= CastsLightFrom;
                    _emit[t] = casts ? paint * glow : P3.Zero;
                    if (casts && _area[t] > 1e-8f) lamps.Add(t);
                }
            }
            _lamps = lamps.ToArray();
            _lampSum = new float[_lamps.Length];
            _lampTotal = 0f;
            for (int i = 0; i < _lamps.Length; i++)
            {
                P3 e = _emit[_lamps[i]];
                _lampTotal += _area[_lamps[i]] * (e.X + e.Y + e.Z);
                _lampSum[i] = _lampTotal;
            }
            BuildTree();
        }

        void BuildTree()
        {
            _order = new int[_count];
            for (int i = 0; i < _count; i++) _order[i] = i;
            _nodes = new Node[Math.Max(1, _count * 2)];
            _nodeCount = 0;
            if (_count > 0) Split(0, _count);
        }

        P3 Centre(int t) => _a[t] + (_edge1[t] + _edge2[t]) * (1f / 3f);

        int Split(int first, int count)
        {
            int index = _nodeCount++;
            P3 min = new P3(float.MaxValue, float.MaxValue, float.MaxValue), max = new P3(float.MinValue, float.MinValue, float.MinValue);
            P3 cmin = min, cmax = max;
            for (int i = first; i < first + count; i++)
            {
                int t = _order[i];
                Grow(ref min, ref max, _a[t]);
                Grow(ref min, ref max, _a[t] + _edge1[t]);
                Grow(ref min, ref max, _a[t] + _edge2[t]);
                P3 c = Centre(t);
                Grow(ref cmin, ref cmax, c);
            }
            var node = new Node { Min = min, Max = max, Left = -1, Right = -1, First = first, Count = count };
            if (count > 4)
            {
                P3 size = cmax - cmin;
                int axis = size.X > size.Y ? (size.X > size.Z ? 0 : 2) : (size.Y > size.Z ? 1 : 2);
                float middle = (Axis(cmin, axis) + Axis(cmax, axis)) * 0.5f;
                int i = first, j = first + count - 1;
                while (i <= j)
                {
                    if (Axis(Centre(_order[i]), axis) < middle) i++;
                    else { int swap = _order[i]; _order[i] = _order[j]; _order[j] = swap; j--; }
                }
                int left = i - first;
                if (left == 0 || left == count) left = count / 2;
                node.Count = 0;
                _nodes[index] = node;
                int l = Split(first, left);
                int r = Split(first + left, count - left);
                node.Left = l; node.Right = r;
            }
            _nodes[index] = node;
            return index;
        }

        static float Axis(P3 p, int axis) => axis == 0 ? p.X : axis == 1 ? p.Y : p.Z;

        static void Grow(ref P3 min, ref P3 max, P3 p)
        {
            if (p.X < min.X) min.X = p.X; if (p.Y < min.Y) min.Y = p.Y; if (p.Z < min.Z) min.Z = p.Z;
            if (p.X > max.X) max.X = p.X; if (p.Y > max.Y) max.Y = p.Y; if (p.Z > max.Z) max.Z = p.Z;
        }

        // ------------------------------------------------------------------ rays

        static bool HitsBox(in Node n, P3 origin, P3 inverse, float limit)
        {
            float t0 = 0f, t1 = limit;
            float a = (n.Min.X - origin.X) * inverse.X, b = (n.Max.X - origin.X) * inverse.X;
            if (a > b) { float s = a; a = b; b = s; }
            if (a > t0) t0 = a; if (b < t1) t1 = b;
            if (t0 > t1) return false;
            a = (n.Min.Y - origin.Y) * inverse.Y; b = (n.Max.Y - origin.Y) * inverse.Y;
            if (a > b) { float s = a; a = b; b = s; }
            if (a > t0) t0 = a; if (b < t1) t1 = b;
            if (t0 > t1) return false;
            a = (n.Min.Z - origin.Z) * inverse.Z; b = (n.Max.Z - origin.Z) * inverse.Z;
            if (a > b) { float s = a; a = b; b = s; }
            if (a > t0) t0 = a; if (b < t1) t1 = b;
            return t0 <= t1;
        }

        /// <summary>
        /// Finds the first triangle a ray meets. Returns its index, or -1. With <paramref name="any"/> it stops
        /// at the first thing in the way (used to ask "can this point see that lamp?").
        /// </summary>
        public int Trace(P3 origin, P3 direction, float limit, out float distance, bool any = false)
        {
            return Trace(origin, direction, limit, out distance, out _, out _, any);
        }

        /// <summary>As above, and also says where on the triangle the ray landed (for blending its corners).</summary>
        public int Trace(P3 origin, P3 direction, float limit, out float distance, out float hitU, out float hitV, bool any = false)
        {
            hitU = hitV = 0f;
            distance = limit;
            int best = -1;
            if (_nodeCount == 0) return -1;
            P3 inverse = new P3(Safe(direction.X), Safe(direction.Y), Safe(direction.Z));
            Span<int> stack = stackalloc int[64];
            int top = 0;
            stack[top++] = 0;
            while (top > 0)
            {
                ref Node n = ref _nodes[stack[--top]];
                if (!HitsBox(n, origin, inverse, distance)) continue;
                if (n.Left >= 0)
                {
                    if (top < 62) { stack[top++] = n.Left; stack[top++] = n.Right; }
                    continue;
                }
                for (int i = n.First; i < n.First + n.Count; i++)
                {
                    int t = _order[i];
                    P3 p = P3.Cross(direction, _edge2[t]);
                    float det = P3.Dot(_edge1[t], p);
                    if (det > -1e-9f && det < 1e-9f) continue;
                    float inv = 1f / det;
                    P3 s = origin - _a[t];
                    float u = P3.Dot(s, p) * inv;
                    if (u < 0f || u > 1f) continue;
                    P3 q = P3.Cross(s, _edge1[t]);
                    float v = P3.Dot(direction, q) * inv;
                    if (v < 0f || u + v > 1f) continue;
                    float d = P3.Dot(_edge2[t], q) * inv;
                    if (d > 1e-4f && d < distance)
                    {
                        distance = d;
                        best = t;
                        hitU = u; hitV = v;
                        if (any) return best;
                    }
                }
            }
            return best;
        }

        static float Safe(float v) => Math.Abs(v) < 1e-12f ? 1e12f : 1f / v;

        /// <summary>Builds the search tree without baking, for callers that only want to cast rays.</summary>
        public void PrepareForRays() => Prepare();

        public void TriangleSource(int t, out int mesh, out int i0, out int i1, out int i2)
        {
            mesh = _meshOf[t]; i0 = _corner[t * 3]; i1 = _corner[t * 3 + 1]; i2 = _corner[t * 3 + 2];
        }

        public P3 TriangleNormal(int t) => _normal[t];
        public P3 TriangleAlbedo(int t) => _albedo[t];
        public P3 TriangleEmission(int t) => _emit[t];

        // ------------------------------------------------------------------ light

        struct Rng
        {
            ulong _s;
            public Rng(ulong seed) { _s = seed * 0x9E3779B97F4A7C15UL + 0x1234567UL; }
            public float Next()
            {
                _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17;
                return (_s >> 40) / 16777216f;
            }
        }

        /// <summary>Light arriving at a point straight from the lamps, from one randomly chosen spot on one lamp.</summary>
        P3 FromLamps(P3 point, P3 normal, ref Rng rng)
        {
            if (_lamps.Length == 0 || _lampTotal <= 0f) return P3.Zero;
            float pick = rng.Next() * _lampTotal;
            int lo = 0, hi = _lamps.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_lampSum[mid] < pick) lo = mid + 1; else hi = mid;
            }
            int t = _lamps[lo];
            float u = rng.Next(), v = rng.Next();
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            P3 spot = _a[t] + _edge1[t] * u + _edge2[t] * v;
            P3 to = spot - point;
            float distance = to.Length;
            if (distance < 1e-4f) return P3.Zero;
            P3 dir = to * (1f / distance);
            float facing = P3.Dot(normal, dir);
            float lampFacing = -P3.Dot(_normal[t], dir);
            if (facing <= 0f || lampFacing <= 0f) return P3.Zero;
            int blocker = Trace(point + normal * 0.004f, dir, distance - 0.006f, out _, any: true);
            if (blocker >= 0 && blocker != t) return P3.Zero;
            P3 e = _emit[t];
            // Chance of having picked this spot, per square metre.
            float chance = (_area[t] * (e.X + e.Y + e.Z) / _lampTotal) / _area[t];
            // Very close lamps would otherwise give wild values at a few corners.
            float spread = Math.Max(distance * distance, 0.02f);
            return e * (facing * lampFacing / (spread * chance));
        }

        static P3 Scatter(P3 normal, ref Rng rng)
        {
            // A random direction away from the surface, favouring straight out (as matt surfaces do).
            float u = rng.Next(), v = rng.Next();
            float r = (float)Math.Sqrt(u);
            double a = 2.0 * Math.PI * v;
            P3 side = Math.Abs(normal.Y) < 0.9f ? P3.Cross(normal, P3.Up).Unit() : P3.Cross(normal, new P3(1, 0, 0)).Unit();
            P3 other = P3.Cross(normal, side);
            return (side * (r * (float)Math.Cos(a)) + other * (r * (float)Math.Sin(a)) + normal * (float)Math.Sqrt(Math.Max(0f, 1f - u))).Unit();
        }

        /// <summary>All the light arriving at a point facing <paramref name="normal"/>: straight from lamps, and bounced.</summary>
        P3 Arriving(P3 point, P3 normal, Settings settings, ref Rng rng)
        {
            P3 direct = P3.Zero;
            for (int i = 0; i < settings.LampChecks; i++) direct += FromLamps(point, normal, ref rng);
            if (settings.LampChecks > 0) direct *= 1f / settings.LampChecks;

            P3 bounced = P3.Zero;
            for (int i = 0; i < settings.Paths; i++)
            {
                P3 p = point, n = normal;
                P3 carry = new P3(1f, 1f, 1f);
                for (int bounce = 0; bounce < settings.Bounces; bounce++)
                {
                    P3 dir = Scatter(n, ref rng);
                    int t = Trace(p + n * 0.004f, dir, 100f, out float distance);
                    if (t < 0) break;                                   // out of a window: nothing comes back
                    if (P3.Dot(_normal[t], dir) > 0f) break;            // the back of something
                    p = p + n * 0.004f + dir * distance;
                    n = _normal[t];
                    carry = carry * _albedo[t];
                    // The surface we landed on passes on the lamp light falling on it.
                    bounced += carry * FromLamps(p, n, ref rng) * (1f / (float)Math.PI);
                    if (carry.X + carry.Y + carry.Z < 0.02f) break;
                }
            }
            if (settings.Paths > 0) bounced *= (float)Math.PI / settings.Paths;
            return direct + bounced;
        }

        /// <summary>
        /// How open to the sky each corner is: 1 with nothing overhead, toward 0 tucked under or between things.
        /// For things that stand outdoors under a moving Sun, where the Sun itself cannot be baked but the soft
        /// shade in nooks and under platforms can. One number per corner, one array per mesh. Always the same answer.
        /// </summary>
        /// <param name="rays">Directions tried per corner.</param>
        /// <param name="reach">Things further away than this (metres) cast no shade.</param>
        public List<float[]> SkyShade(int rays = 24, float reach = 12f, ulong seed = 8)
        {
            Prepare();
            var result = new List<float[]>();
            for (int m = 0; m < _meshes.Count; m++)
            {
                ShipMesh mesh = _meshes[m];
                var n = new P3[mesh.VertexCount];
                for (int k = 0; k < mesh.Triangles.Length; k += 3)
                {
                    int i0 = mesh.Triangles[k], i1 = mesh.Triangles[k + 1], i2 = mesh.Triangles[k + 2];
                    P3 cross = P3.Cross(mesh.Position(i1) - mesh.Position(i0), mesh.Position(i2) - mesh.Position(i0));
                    n[i0] += cross; n[i1] += cross; n[i2] += cross;
                }
                var open = new float[mesh.VertexCount];
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    P3 normal = n[i].Unit();
                    P3 side = P3.Cross(normal, Math.Abs(normal.Y) < 0.9f ? P3.Up : new P3(1, 0, 0)).Unit();
                    P3 other = P3.Cross(normal, side);
                    P3 from = mesh.Position(i) + normal * 0.02f;
                    var rng = new Rng(seed + (ulong)m * 104729UL + (ulong)i * 7919UL);
                    int clear = 0;
                    for (int r = 0; r < rays; r++)
                    {
                        // More rays near straight out than along the surface, as light itself arrives.
                        float u = rng.Next(), v = rng.Next();
                        float radius = (float)Math.Sqrt(u);
                        double angle = 2.0 * Math.PI * v;
                        P3 dir = side * (radius * (float)Math.Cos(angle)) + other * (radius * (float)Math.Sin(angle)) + normal * (float)Math.Sqrt(Math.Max(0f, 1f - u));
                        if (Trace(from, dir, reach, out _, any: true) < 0) clear++;
                    }
                    open[i] = clear / (float)Math.Max(1, rays);
                }
                result.Add(open);
            }
            return result;
        }

        /// <summary>
        /// Works out the baked colour of every corner of every mesh added, in the order they were added.
        /// The colours are linear light, 0 to 1, already including each surface's own paint.
        /// </summary>
        public List<float[]> Bake(Settings settings = null, Action<float> progress = null)
        {
            settings = settings ?? new Settings();
            Prepare();

            // Corners in the same place facing the same way share one answer, so seams do not show as noise.
            var shared = new Dictionary<long, int>();
            var sharedPoint = new List<P3>();
            var sharedNormal = new List<P3>();
            var slot = new List<int[]>();

            for (int m = 0; m < _meshes.Count; m++)
            {
                ShipMesh mesh = _meshes[m];
                var n = new P3[mesh.VertexCount];
                for (int k = 0; k < mesh.Triangles.Length; k += 3)
                {
                    int i0 = mesh.Triangles[k], i1 = mesh.Triangles[k + 1], i2 = mesh.Triangles[k + 2];
                    P3 cross = P3.Cross(mesh.Position(i1) - mesh.Position(i0), mesh.Position(i2) - mesh.Position(i0));
                    n[i0] += cross; n[i1] += cross; n[i2] += cross;
                }
                var slots = new int[mesh.VertexCount];
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    n[i] = n[i].Unit();
                    P3 p = mesh.Position(i);
                    long key = Quantise(p.X) * 73856093L ^ Quantise(p.Y) * 19349663L ^ Quantise(p.Z) * 83492791L
                               ^ (long)Math.Round(n[i].X * 3) * 1009L ^ (long)Math.Round(n[i].Y * 3) * 9176L ^ (long)Math.Round(n[i].Z * 3) * 31337L;
                    if (!shared.TryGetValue(key, out int s))
                    {
                        s = sharedPoint.Count;
                        shared[key] = s;
                        sharedPoint.Add(p);
                        sharedNormal.Add(n[i]);
                    }
                    slots[i] = s;
                }
                slot.Add(slots);
            }

            // The slow part: follow the light for every shared corner. Each corner has its own run of random
            // numbers, so the answer is the same however many cores share the work.
            int count = sharedPoint.Count;
            var sharedLight = new P3[count];
            int finished = 0;
            void One(int s)
            {
                var rng = new Rng(settings.Seed + (ulong)s * 7919UL);
                sharedLight[s] = Arriving(sharedPoint[s], sharedNormal[s], settings, ref rng);
                if (progress != null)
                {
                    int f = System.Threading.Interlocked.Increment(ref finished);
                    if ((f & 255) == 0) progress(f / (float)count);
                }
            }
            if (settings.Threads == 1)
            {
                for (int s = 0; s < count; s++) One(s);
            }
            else
            {
                var options = new System.Threading.Tasks.ParallelOptions();
                if (settings.Threads > 1) options.MaxDegreeOfParallelism = settings.Threads;
                System.Threading.Tasks.Parallel.For(0, count, options, One);
            }

            // Smooth: each corner leans toward the average of the corners it shares a triangle edge with,
            // as long as they face much the same way (so light does not leak round sharp edges).
            if (settings.Smoothing > 0)
            {
                var neighbours = new List<int>[count];
                for (int m = 0; m < _meshes.Count; m++)
                {
                    ShipMesh mesh = _meshes[m];
                    for (int k = 0; k < mesh.Triangles.Length; k += 3)
                    {
                        for (int e = 0; e < 3; e++)
                        {
                            int a = slot[m][mesh.Triangles[k + e]], b = slot[m][mesh.Triangles[k + (e + 1) % 3]];
                            if (a == b || P3.Dot(sharedNormal[a], sharedNormal[b]) < 0.8f) continue;
                            (neighbours[a] ?? (neighbours[a] = new List<int>())).Add(b);
                            (neighbours[b] ?? (neighbours[b] = new List<int>())).Add(a);
                        }
                    }
                }
                var next = new P3[count];
                for (int pass = 0; pass < settings.Smoothing; pass++)
                {
                    for (int s = 0; s < count; s++)
                    {
                        if (neighbours[s] == null) { next[s] = sharedLight[s]; continue; }
                        P3 sum = P3.Zero;
                        for (int k = 0; k < neighbours[s].Count; k++) sum += sharedLight[neighbours[s][k]];
                        next[s] = sharedLight[s] * 0.5f + sum * (0.5f / neighbours[s].Count);
                    }
                    P3[] swap = sharedLight; sharedLight = next; next = swap;
                }
            }

            // Set the exposure so a typical lit surface comes out at the wanted brightness.
            var brightness = new List<float>();
            for (int m = 0; m < _meshes.Count; m++)
            {
                ShipMesh mesh = _meshes[m];
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    if (mesh.Glow[i] > 0.01f) continue;
                    P3 l = sharedLight[slot[m][i]];
                    brightness.Add((l.X + l.Y + l.Z) / 3f);
                }
            }
            brightness.Sort();
            float typical = brightness.Count > 0 ? brightness[(int)(brightness.Count * 0.7f)] : 1f;
            float target = ToLinear(settings.TargetBrightness) / 0.6f;   // for a surface that reflects 60%
            float exposure = typical > 1e-6f ? target / typical : 1f;
            Exposure = exposure;

            var result = new List<float[]>();
            for (int m = 0; m < _meshes.Count; m++)
            {
                ShipMesh mesh = _meshes[m];
                var colors = new float[mesh.VertexCount * 3];
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    P3 paint = ToLinear(mesh.Color(i));
                    P3 c;
                    if (mesh.Glow[i] > 0.01f)
                    {
                        c = paint * Math.Min(2f, 0.6f + mesh.Glow[i] * 0.35f);
                    }
                    else
                    {
                        P3 light = sharedLight[slot[m][i]] * exposure + new P3(settings.Floor, settings.Floor, settings.Floor);
                        c = paint * light;
                    }
                    colors[i * 3] = Shoulder(c.X);
                    colors[i * 3 + 1] = Shoulder(c.Y);
                    colors[i * 3 + 2] = Shoulder(c.Z);
                }
                result.Add(colors);
            }
            return result;
        }

        /// <summary>The brightness scale the last bake settled on.</summary>
        public float Exposure { get; private set; } = 1f;

        static long Quantise(float v) => (long)Math.Round(v * 2000.0);

        /// <summary>Rolls very bright values off gently instead of clipping them flat.</summary>
        static float Shoulder(float v)
        {
            if (v <= 0.6f) return Math.Max(0f, v);
            float over = v - 0.6f;
            return 0.6f + 0.4f * (over / (over + 0.4f));
        }
    }
}
