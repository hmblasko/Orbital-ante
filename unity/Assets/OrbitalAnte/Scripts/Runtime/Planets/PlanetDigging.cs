using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Marks a collider as a body's diggable ground (ground tiles, dug ground, cave walls), so a shovel, a bullet or a
    /// blast that strikes it knows which body to dig.
    /// </summary>
    public sealed class DiggableGround : MonoBehaviour
    {
        public PlanetSurface Surface;
    }

    /// <summary>
    /// The dug ground of one body, in the world (see <see cref="DigRules"/> for how it works). Owned by its
    /// <see cref="PlanetSurface"/>, which ticks it while the body is near. It keeps what has been dug for the run
    /// (<see cref="Field"/>); its boxes are objects only while the body is near.
    ///
    /// Order matters, so nobody ever sees or falls through a gap: a new box is worked out in the background, drawn,
    /// and made solid; only then is it added to the cover, and only then do the ground tiles and cave walls round it
    /// leave out their faces. A box that is reshaped is drawn in its new shape as soon as it is ready and made solid
    /// in its new shape a frame or two later (colliders are cooked a few a frame), so for that moment the old floor
    /// is still there: a lid that lags, never a hole that drops you.
    /// A box's collider is always on while the body is near; only its drawing is switched by distance.
    /// </summary>
    public sealed class PlanetDigging
    {
        sealed class View
        {
            public long Key;
            public Vec3d Centre;
            public GameObject Go;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public MeshCollider Collider;
            public bool Dirty = true;
            public bool Ready;
            public bool Cook;
            public PlanetJobs.Handle Job;
        }

        /// <summary>The most boxes worked out at once in the background.</summary>
        const int MostJobs = 3;
        /// <summary>How often the cover is made again at most, in seconds, so the tiles round a firefight are not rebuilt every frame.</summary>
        const float CoverEvery = 0.2f;
        /// <summary>Boxes whose drawing is checked against the ground's detail each frame (in turn).</summary>
        const int VisibleChecksPerFrame = 40;
        /// <summary>Within this far of the viewer, dug work goes ahead of ordinary ground tiles; beyond it, behind them and never on the last free core.</summary>
        public const float UrgentReach = 15f;
        /// <summary>The layers of things that can lie loose on the ground: woken when it changes under them.</summary>
        const int LooseMask = (1 << Layers.Default) | (1 << Layers.Hitbox) | (1 << Layers.Gib) | (1 << Layers.CharacterBody) | (1 << Layers.Item);

        readonly PlanetSurface _surface;
        readonly Dictionary<long, View> _views = new Dictionary<long, View>();
        readonly List<long> _reshaped = new List<long>(64);
        readonly List<long> _added = new List<long>(64);
        readonly List<View> _waiting = new List<View>(64);
        readonly List<View> _order = new List<View>(64);
        readonly CaveNetwork[] _networks;
        readonly Collider[] _woken = new Collider[64];
        readonly Comparison<View> _nearestFirst;
        Vec3d _sortFrom;
        int _visibleNext;
        float _coverAt;
        Material _material;
        int _jobs;
        int _coverVersion;
        bool _coverStale;
        Vec3d _changedMin, _changedMax;
        bool _shown;

        /// <summary>Everything dug on this body this run.</summary>
        public DigField Field { get; } = new DigField();
        /// <summary>The boxes that are up and solid (the ground tiles and cave walls leave their faces out inside them).</summary>
        public DigCover Cover { get; private set; } = DigCover.None;
        /// <summary>Boxes shown as objects now, for the debug readout.</summary>
        public int ShownBoxes => _views.Count;
        /// <summary>Scoops refused since the run began (the body is dug out as far as it may be, or the spot cannot be dug).</summary>
        public int Refused { get; private set; }
        /// <summary>Why the last scoop asked for was refused (None if it was taken).</summary>
        public DigRefusal LastRefusal { get; private set; }

        static readonly List<PlanetDigging> Active = new List<PlanetDigging>();

        public PlanetDigging(PlanetSurface surface)
        {
            _surface = surface;
            _changedMin = new Vec3d(double.MaxValue, double.MaxValue, double.MaxValue);
            _changedMax = -_changedMin;
            var networks = new List<CaveNetwork>();
            foreach (Cave cave in surface.Terrain.Caves) if (cave.Network != null) networks.Add(cave.Network);
            _networks = networks.ToArray();
            _nearestFirst = (a, b) => (a.Centre - _sortFrom).SqrMagnitude.CompareTo((b.Centre - _sortFrom).SqrMagnitude);
        }

        /// <summary>
        /// Digs a scoop out of the ground (body space). False if it was refused (too deep, under the sea, next to an
        /// old cave, or the body is dug out as far as it may be).
        /// </summary>
        public bool Carve(DigCarve carve, DigSource source = DigSource.Shovel)
        {
            LastRefusal = DigRefusal.TooDeep;
            if (_surface.Terrain == null) return false;
            if (!DigRules.Fit(_surface.Terrain, _surface.SafetyRadius + 6, ref carve, out DigRefusal why))
            {
                LastRefusal = why;
                Refused++;
                return false;
            }
            _reshaped.Clear();
            _added.Clear();
            if (!Field.TryCarve(carve, _reshaped, _added, DigRules.ChunkCapFor(source)))
            {
                if (Field.Inside(carve)) LastRefusal = DigRefusal.AlreadyDug;
                else
                {
                    LastRefusal = DigRefusal.Full;
                    Refused++;
                }
                return false;
            }
            LastRefusal = DigRefusal.None;
            if (!_shown) return true;
            for (int i = 0; i < _added.Count; i++) AddView(_added[i]);
            for (int i = 0; i < _reshaped.Count; i++)
                if (_views.TryGetValue(_reshaped[i], out View v)) v.Dirty = true;
            return true;
        }

        void AddView(long key)
        {
            if (_views.ContainsKey(key)) return;
            var v = new View { Key = key, Centre = DigRules.ChunkCentre(key) };
            _views[key] = v;
            _order.Add(v);
        }

        /// <summary>The body has come near: its boxes are built again (what was dug is still there).</summary>
        public void Show()
        {
            if (_shown) return;
            _shown = true;
            if (!Active.Contains(this)) Active.Add(this);
            foreach (long key in Field.Chunks) AddView(key);
        }

        /// <summary>The body is left behind: its boxes go, the record of what was dug stays.</summary>
        public void Hide()
        {
            _shown = false;
            Active.Remove(this);
            foreach (View v in _views.Values)
            {
                if (v.Job != null) v.Job.Cancel();
                Release(v);
            }
            _views.Clear();
            _order.Clear();
            _waiting.Clear();
            _jobs = 0;
            Cover = DigCover.None;
            _coverStale = false;
        }

        public void Destroy()
        {
            Hide();
            if (_material != null) UnityEngine.Object.Destroy(_material);
            _material = null;
        }

        static void Release(View v)
        {
            if (v.Mesh != null) UnityEngine.Object.Destroy(v.Mesh);
            if (v.Go != null) UnityEngine.Object.Destroy(v.Go);
            v.Mesh = null;
            v.Go = null;
            v.Renderer = null;
            v.Collider = null;
        }

        /// <summary>Once a frame while the body is near, from <see cref="PlanetSurface.Tick(Vec3d, Vec3d[], int, float)"/>.</summary>
        public void Tick(Vec3d viewer)
        {
            if (!_shown) return;
            StartJobs(viewer);
            CookColliders(viewer);
            if (_coverStale && Time.time >= _coverAt)
            {
                _coverStale = false;
                _coverAt = Time.time + CoverEvery;
                var ready = new List<long>(_views.Count);
                foreach (View v in _views.Values) if (v.Ready) ready.Add(v.Key);
                Cover = new DigCover(ready, ++_coverVersion);
                _surface.OnDigCover(Cover, _changedMin, _changedMax);
                _changedMin = new Vec3d(double.MaxValue, double.MaxValue, double.MaxValue);
                _changedMax = -_changedMin;
            }
            // Drawn only where the finest ground is (the only tiles that leave a hole for it): from far off the coarse
            // ground covers the spot and dug ground would only poke through it. Only the drawing is switched: the
            // collider stays on, so a finest tile that comes in with its hole already cut always has a floor under it.
            // A few boxes a frame, in turn, so the check never makes a spike.
            for (int n = 0; n < VisibleChecksPerFrame && _order.Count > 0; n++)
            {
                if (_visibleNext >= _order.Count) _visibleNext = 0;
                View v = _order[_visibleNext++];
                if (v.Renderer == null) continue;
                bool want = Fine(v);
                if (v.Renderer.enabled != want) v.Renderer.enabled = want;
            }
        }

        bool Fine(View v)
        {
            Vec3d min = DigRules.ChunkMin(v.Key);
            if (_surface.FinestDrawnAt(v.Centre)) return true;
            // Far from any finest tile (they are all within a few hundred metres of the viewer): no need to try the corners.
            if ((v.Centre - _surface.Viewer).SqrMagnitude > 600.0 * 600.0) return false;
            for (int c = 0; c < 8; c++)
            {
                Vec3d corner = min + new Vec3d((c & 1) * DigRules.ChunkSize, ((c >> 1) & 1) * DigRules.ChunkSize, ((c >> 2) & 1) * DigRules.ChunkSize);
                if (_surface.FinestDrawnAt(corner)) return true;
            }
            return false;
        }

        void StartJobs(Vec3d viewer)
        {
            if (_jobs >= MostJobs) return;
            _waiting.Clear();
            foreach (View v in _views.Values) if (v.Dirty && v.Job == null) _waiting.Add(v);
            if (_waiting.Count == 0) return;
            // The nearest first: the hole you are digging matters more than one shot into a far hillside.
            _sortFrom = viewer;
            _waiting.Sort(_nearestFirst);
            PlanetTerrain terrain = _surface.Terrain;
            int level = _surface.MaxLevel;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int i = 0; i < _waiting.Count && _jobs < MostJobs; i++)
            {
                View v = _waiting[i];
                v.Dirty = false;
                Vec3d centre = v.Centre;
                bool patient = (centre - viewer).Magnitude > UrgentReach + DigRules.ChunkSize;
                _jobs++;
                v.Job = PlanetJobs.Run(BoxWork(v.Key, Field.CarvesNear(v.Key, 9), terrain, level, _networks, linear),
                    data => OnBuilt(v, data), () => Priority(centre, _surface.Viewer), patient);
            }
        }

        /// <summary>
        /// Where dug work stands in the queue: right at the front near the viewer (the hole you are digging), behind
        /// the ordinary ground beyond <see cref="UrgentReach"/>, so a firefight far off never holds up the ground
        /// you are about to land on. Shared by the tiles and cave walls rebuilt round dug ground.
        /// </summary>
        public static float Priority(Vec3d at, Vec3d viewer)
        {
            float d = (float)(at - viewer).Magnitude;
            return d < UrgentReach ? d - 3e6f : d - 1e6f;
        }

        static Func<DigChunkData> BoxWork(long key, DigCarve[] carves, PlanetTerrain terrain, int level, CaveNetwork[] networks, bool linear)
        {
            return () =>
            {
                try
                {
                    DigChunkData data = DigMesher.Build(key, carves, new DigGround(terrain, level), networks);
                    if (linear && data.Colors != null)
                        for (int k = 0; k < data.Colors.Length; k++) data.Colors[k] = SkyGlobals.ToLinear(data.Colors[k]);
                    return data;
                }
                catch (Exception e)
                {
                    // A box that cannot be worked out is left empty (the ground there stays as it was) rather than
                    // holding a worker's place for good.
                    UnityEngine.Debug.LogWarning("Dug ground: box could not be worked out: " + e.Message);
                    return new DigChunkData { Key = key };
                }
            };
        }

        static readonly List<Vector3> Vertices = new List<Vector3>(4096);
        static readonly List<Vector3> Normals = new List<Vector3>(4096);
        static readonly List<Vector3> Ups = new List<Vector3>(4096);
        static readonly List<Color> Colors = new List<Color>(4096);

        void OnBuilt(View v, DigChunkData data)
        {
            v.Job = null;
            _jobs = Math.Max(0, _jobs - 1);
            if (!_shown || !_views.TryGetValue(v.Key, out View current) || current != v) return;
            if (data.Empty)
            {
                Release(v);
                MarkReady(v);
                return;
            }
            if (v.Go == null)
            {
                v.Go = new GameObject("Dug ground");
                v.Go.transform.SetParent(_surface.GroundRoot, false);
                v.Go.layer = Layers.Default;
                v.Go.AddComponent<DiggableGround>().Surface = _surface;
                v.Mesh = new Mesh { name = "Dug ground" };
                v.Go.AddComponent<MeshFilter>().sharedMesh = v.Mesh;
                var renderer = v.Go.AddComponent<MeshRenderer>();
                if (_material == null) _material = PlanetMaterials.DugGround(_surface.Body.Spec.id, _surface.Settings.groundRelief);
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = Fine(v);
                v.Renderer = renderer;
            }
            int count = data.VertexCount;
            Vertices.Clear(); Normals.Clear(); Ups.Clear(); Colors.Clear();
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                var p = new Vector3(data.Positions[o], data.Positions[o + 1], data.Positions[o + 2]);
                Vertices.Add(p);
                Normals.Add(new Vector3(data.Normals[o], data.Normals[o + 1], data.Normals[o + 2]));
                Ups.Add(p.normalized);
                Colors.Add(new Color(data.Colors[o], data.Colors[o + 1], data.Colors[o + 2], data.Sky[i]));
            }
            v.Mesh.Clear();
            v.Mesh.SetVertices(Vertices);
            v.Mesh.SetNormals(Normals);
            v.Mesh.SetUVs(0, Ups);
            v.Mesh.SetColors(Colors);
            v.Mesh.SetTriangles(data.Triangles, 0, true);
            v.Cook = true;
        }

        void CookColliders(Vec3d viewer)
        {
            while (true)
            {
                View best = null;
                double nearest = double.MaxValue;
                foreach (View v in _views.Values)
                {
                    if (!v.Cook) continue;
                    double d = (v.Centre - viewer).SqrMagnitude;
                    if (d < nearest) { nearest = d; best = v; }
                }
                if (best == null) return;
                if (!GroundCooks.Take()) return;
                best.Cook = false;
                if (best.Go == null) continue;
                if (best.Collider == null)
                {
                    best.Collider = best.Go.AddComponent<MeshCollider>();
                    best.Collider.cookingOptions = GroundCooks.Options;
                }
                // Handing the same mesh over again cooks it again.
                best.Collider.sharedMesh = null;
                best.Collider.sharedMesh = best.Mesh;
                WakeIn(DigRules.ChunkMin(best.Key), DigRules.ChunkMin(best.Key) + new Vec3d(DigRules.ChunkSize, DigRules.ChunkSize, DigRules.ChunkSize));
                MarkReady(best);
            }
        }

        void MarkReady(View v)
        {
            if (v.Ready) return;
            v.Ready = true;
            _coverStale = true;
            Vec3d min = DigRules.ChunkMin(v.Key), max = min + new Vec3d(DigRules.ChunkSize, DigRules.ChunkSize, DigRules.ChunkSize);
            if (_changedMin.X > _changedMax.X) { _changedMin = min; _changedMax = max; return; }
            _changedMin = new Vec3d(Math.Min(_changedMin.X, min.X), Math.Min(_changedMin.Y, min.Y), Math.Min(_changedMin.Z, min.Z));
            _changedMax = new Vec3d(Math.Max(_changedMax.X, max.X), Math.Max(_changedMax.Y, max.Y), Math.Max(_changedMax.Z, max.Z));
        }

        /// <summary>
        /// Anything asleep on ground that just changed shape between two body-space corners is woken, so it falls into
        /// the new hole. Called when a box is made solid and when a ground tile is made solid again without its faces.
        /// </summary>
        public void WakeIn(Vec3d min, Vec3d max)
        {
            Transform t = _surface.transform;
            Vector3 centre = t.TransformPoint(SpaceConvert.ToVector3((min + max) * 0.5));
            Vector3 half = SpaceConvert.ToVector3((max - min) * 0.5) + Vector3.one * 0.5f;
            int found = Physics.OverlapBoxNonAlloc(centre, half, _woken, t.rotation, LooseMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
            {
                Rigidbody rb = _woken[i].attachedRigidbody;
                if (rb != null && !rb.isKinematic && rb.IsSleeping()) rb.WakeUp();
            }
        }

        // ------------------------------------------------------------------ questions

        /// <summary>
        /// True if a world-space point is down a dug tunnel: in dug air and with ground over it (a pit open to the sky
        /// does not count). Enemies see someone there only from closer, as in a dark cave.
        /// </summary>
        public static bool Underground(Vector3 world)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                PlanetDigging d = Active[i];
                if (d._surface == null || d.Field.CarveCount == 0) continue;
                Vec3d p = SpaceConvert.ToVec3d(d._surface.transform.InverseTransformPoint(world));
                if (!d.Field.InDugAir(p, 0.6)) continue;
                double r = p.Magnitude;
                if (r < 1e-3 || d._surface.GroundRadius(p / r) - r < 1.2) continue;
                Vector3 up = (world - d._surface.transform.position).normalized;
                if (Physics.Raycast(world + up * 0.3f, up, 30f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        /// <summary>The body whose dug ground is shown nearest a world-space point, or null (for blasts, which strike no collider).</summary>
        public static PlanetSurface SurfaceNear(Vector3 world, float reach)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                PlanetSurface s = Active[i]._surface;
                if (s == null || s.Terrain == null) continue;
                Vec3d p = SpaceConvert.ToVec3d(s.transform.InverseTransformPoint(world));
                double r = p.Magnitude;
                if (r < 1e-3) continue;
                if (r - s.GroundRadius(p / r) < reach) return s;
            }
            return null;
        }

        static Vector3 _wayFrom;
        static Vector3 _wayTo;
        static bool _wayFound;
        static float _wayAt = -10f;

        /// <summary>
        /// Where to go to get at someone at a world point who has dug in: false if they are not down a tunnel (see
        /// <see cref="Underground"/>) or no way in wide enough to walk into is within 30 m; otherwise
        /// <paramref name="mouth"/> is the tunnel's mouth on the surface. Enemies hunting a dug-in player go there
        /// and shoot down it, rather than standing on the roof over them. Kept for a quarter of a second, as every
        /// enemy asks about the same player.
        /// </summary>
        public static bool WayInFor(Vector3 world, out Vector3 mouth)
        {
            float now = Time.time;
            if (now - _wayAt < 0.25f && (world - _wayFrom).sqrMagnitude < 0.25f)
            {
                mouth = _wayTo;
                return _wayFound;
            }
            _wayAt = now;
            _wayFrom = world;
            _wayFound = false;
            _wayTo = world;
            if (Underground(world))
            {
                for (int i = 0; i < Active.Count && !_wayFound; i++)
                {
                    PlanetSurface s = Active[i]._surface;
                    if (s == null) continue;
                    Vec3d p = SpaceConvert.ToVec3d(s.transform.InverseTransformPoint(world));
                    if (Active[i].Field.WayInNear(p, 30, s.GroundRadius, out Vec3d m))
                    {
                        _wayFound = true;
                        _wayTo = s.transform.TransformPoint(SpaceConvert.ToVector3(m));
                    }
                }
            }
            mouth = _wayTo;
            return _wayFound;
        }

        public static void ResetStatics()
        {
            Active.Clear();
            _wayAt = -10f;
            GroundCooks.Reset();
        }
    }

    /// <summary>
    /// One budget a frame for cooking the colliders of changed ground (dug boxes, ground tiles rebuilt round them,
    /// cave walls rebuilt round them), as cooking runs on the main thread. Work that finds the budget spent waits
    /// for the next frame.
    /// </summary>
    public static class GroundCooks
    {
        /// <summary>Colliders cooked a frame at most, across all three kinds.</summary>
        public const int PerFrame = 2;
        /// <summary>Static ground: quick to cook, cleaned of slivers (surface nets can make near-flat triangles).</summary>
        public const MeshColliderCookingOptions Options = MeshColliderCookingOptions.UseFastMidphase | MeshColliderCookingOptions.EnableMeshCleaning;

        static int _frame = -1;
        static int _used;

        /// <summary>True (and counted) if a collider may be cooked this frame.</summary>
        public static bool Take()
        {
            int f = Time.frameCount;
            if (f != _frame) { _frame = f; _used = 0; }
            if (_used >= PerFrame) return false;
            _used++;
            return true;
        }

        public static void Reset()
        {
            _frame = -1;
            _used = 0;
        }
    }
}
