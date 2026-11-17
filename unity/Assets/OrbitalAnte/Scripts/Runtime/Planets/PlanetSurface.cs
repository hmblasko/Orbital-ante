using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// The ground of one body. Far away, the body is a smooth ball wearing a picture of its own surface. Come
    /// close and real ground appears in square tiles that split into finer tiles as you approach, down to
    /// detail you can land and drive on. Near ground is solid; rocks, pools, caves and the sea appear with it.
    ///
    /// All the shapes come from <see cref="PlanetTerrain"/> (pure rules). This class only turns them into
    /// meshes and colliders, a little at a time, on background threads.
    /// </summary>
    public sealed class PlanetSurface : MonoBehaviour
    {
        sealed class TileBuild
        {
            public ChunkData Chunk;
            public List<Cave> RequiredCaves;
            public List<Placement> Scatter;
            public Vec3d[] ScatterPositions;
        }

        sealed class Node
        {
            public int Face, Level, X, Y;
            /// <summary>Middle of the tile, in metres from the body's centre.</summary>
            public Vec3d Centre;
            public Vec3d FootprintCentre;
            public Node[] Children;
            public GameObject Go;
            public Mesh Mesh;
            public MeshCollider Collider;
            public bool Solid;
            public bool HasMesh;
            public List<Cave> RequiredCaves;
            public bool Shown;
            public bool Dead;
            public PlanetJobs.Handle Job;
            public List<Placement> Scatter;
            public Vec3d[] ScatterPositions;
            public bool ScatterOut;
            public int SeenTick;
            /// <summary>The dug-ground cover the tile's shape was last worked out with (finest tiles only).</summary>
            public int DigVersion;
            public PlanetJobs.Handle Rebuild;
            /// <summary>True if the tile's shape leaves squares out for dug ground.</summary>
            public bool DugOut;
            /// <summary>Corners of the box round the tile's mesh (body space), for finding the tiles dug ground touches.</summary>
            public Vec3d BoxMin, BoxMax;
            /// <summary>A rebuilt shape is waiting for its collider to be cooked again (<see cref="GroundCooks"/>).</summary>
            public bool NeedsCook;
        }

        sealed class TerrainBuild
        {
            public PlanetTerrain Terrain;
            public Vec3d[] PlacementPositions;
            public double DeepestCave;
        }

        public CelestialBody Body { get; private set; }
        public PlanetProfile Profile { get; private set; }
        public PlanetSettings Settings { get; private set; }
        /// <summary>The ground's rules. Null until they have been worked out (a moment after the run starts).</summary>
        public PlanetTerrain Terrain { get; private set; }
        public WeatherModel Weather { get; private set; }
        public PlanetFeatures Features { get; private set; }
        /// <summary>Radius of the ground's zero level, in metres. Far smaller than the body for a gas giant.</summary>
        public double SurfaceRadius { get; private set; }
        public double BodyRadius { get; private set; }
        /// <summary>True for a body whose ground is hidden far below its cloud tops.</summary>
        public bool Interior { get; private set; }
        public bool Ready => Terrain != null;
        /// <summary>True while the detailed ground is switched on (the viewer is near).</summary>
        public bool Near { get; private set; }
        /// <summary>True once the detailed ground is actually being drawn.</summary>
        public bool GroundShown { get; private set; }
        public int MaxLevel { get; private set; }
        public int ShownTiles { get; private set; }
        public int SolidTiles { get; private set; }
        public bool PictureReady { get; private set; }
        /// <summary>Positions of the terrain's fixed placements, in metres from the body's centre.</summary>
        public Vec3d[] PlacementPositions { get; private set; }
        /// <summary>The ground dug out of this body this run (shovel, bullets, blasts). Made with the terrain.</summary>
        public PlanetDigging Digging { get; private set; }
        /// <summary>The safety net's radius: nothing is ever dug down to it.</summary>
        public double SafetyRadius => _safetyRadius;
        /// <summary>Where the ground was last built round (the viewer), metres from the body's centre.</summary>
        public Vec3d Viewer => _viewer;
        /// <summary>The parent of the ground tiles (and of the dug ground).</summary>
        public Transform GroundRoot => _tileRoot;

        public Color SkyColor { get; private set; }
        public Color HazeColor { get; private set; }
        public Color SeaColor { get; private set; }
        public Color SeaDeepColor { get; private set; }
        readonly Dictionary<WeatherSpec, Color> _weatherColors = new Dictionary<WeatherSpec, Color>();
        readonly Dictionary<WeatherSpec, Color> _lightningColors = new Dictionary<WeatherSpec, Color>();

        /// <summary>The colour of a kind of weather's rain, snow or dust.</summary>
        public Color ColorOf(WeatherSpec spec)
        {
            if (_weatherColors.TryGetValue(spec, out Color c)) return c;
            c = SpaceConvert.ParseColor(spec.color, HazeColor);
            _weatherColors[spec] = c;
            return c;
        }

        /// <summary>The colour of a kind of weather's lightning. Clear (alpha 0) if it has none.</summary>
        public Color LightningOf(WeatherSpec spec)
        {
            if (_lightningColors.TryGetValue(spec, out Color c)) return c;
            c = SpaceConvert.ParseColor(spec.lightningColor, Color.clear);
            _lightningColors[spec] = c;
            return c;
        }

        int _seed;
        bool _linear;
        Transform _tileRoot;
        Transform _detailRoot;
        Node[] _roots;
        Material _ground;
        Texture2D _picture;
        GameObject _sea;
        Material _seaMaterial;
        readonly Dictionary<Liquid, Material> _poolMaterials = new Dictionary<Liquid, Material>();
        GameObject _airRim;
        Material _airRimMaterial;
        float _airRimStrength, _airRimShown;
        double _airRimShell;
        readonly List<PlanetJobs.Handle> _caveJobs = new List<PlanetJobs.Handle>();
        readonly HashSet<Cave> _readyCaves = new HashSet<Cave>();
        readonly Stack<Node> _spare = new Stack<Node>();
        readonly List<Node> _scatterNodes = new List<Node>();
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        Vec3d _viewer;
        DigCover _digCover = DigCover.None;
        readonly List<CaveNetworkView> _networkViews = new List<CaveNetworkView>();
        float _solidRange;
        int _tick;
        int _bakesLeft;
        double _safetyRadius;
        PlanetJobs.Handle _terrainJob;
        PlanetJobs.Handle _pictureJob;
        bool _pictureHeld;
        bool _pictureStarted;
        Func<float> _bodyPriority;

        // Reused every time a tile is filled, so building ground makes no garbage.
        static readonly List<Vector3> VertexBuffer = new List<Vector3>(1400);
        static readonly List<Vector3> NormalBuffer = new List<Vector3>(1400);
        static readonly List<Vector3> DirectionBuffer = new List<Vector3>(1400);
        static readonly List<Color> ColorBuffer = new List<Color>(1400);
        static Mesh _disc;

        /// <summary>Tests set this to paint tiny pictures, so they do not spend a minute painting nineteen planets.</summary>
        public static int PictureWidthForTests { get; set; }

        public static void ResetStatics()
        {
            _disc = null;
            PictureWidthForTests = 0;
            PlanetDigging.ResetStatics();
        }

        // ------------------------------------------------------------------ building

        public static PlanetSurface Create(CelestialBody body, PlanetProfile profile, PlanetSettings settings, int seed)
        {
            var surface = body.gameObject.AddComponent<PlanetSurface>();
            surface.Body = body;
            surface.Profile = profile;
            surface.Settings = settings;
            surface._seed = seed;
            surface._linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            surface.BodyRadius = body.Spec.radius;
            surface.SurfaceRadius = profile.surfaceRadius > 0 ? profile.surfaceRadius : body.Spec.radius;
            surface.Interior = surface.SurfaceRadius < surface.BodyRadius * 0.97;
            surface.MaxLevel = TileRules.MaxLevel(surface.SurfaceRadius, settings.finestCell);
            surface.SkyColor = SpaceConvert.ParseColor(profile.skyColor, Color.black);
            surface.HazeColor = SpaceConvert.ParseColor(profile.hazeColor, surface.SkyColor);
            surface.SeaColor = SpaceConvert.ParseColor(profile.seaColor, new Color(0.12f, 0.43f, 0.66f));
            surface.SeaDeepColor = SpaceConvert.ParseColor(profile.seaDeepColor, surface.SeaColor * 0.5f);

            surface._tileRoot = NewChild(body.transform, "Ground");
            surface._detailRoot = NewChild(body.transform, "Detail");
            surface._tileRoot.gameObject.SetActive(false);
            surface._detailRoot.gameObject.SetActive(false);
            surface.Features = new PlanetFeatures(surface, NewChild(body.transform, "Features"));

            // A gas giant's cloud tops are not solid: the ball shrinks to the ground far below straight away.
            if (surface.Interior) body.Ball.radius = (float)surface.SurfaceRadius;
            surface.BuildAirRim();
            return surface;
        }

        static Transform NewChild(Transform parent, string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>
        /// Starts working out the ground's rules in the background. <paramref name="priority"/> says how urgent
        /// this body is (distance to the player works well).
        /// </summary>
        public void BeginTerrain(Func<float> priority)
        {
            if (Terrain != null || _terrainJob != null) return;
            _bodyPriority = priority;
            PlanetProfile profile = Profile;
            string id = Body.Spec.id;
            double radius = BodyRadius;
            int seed = _seed;
            _terrainJob = PlanetJobs.Run(() => BuildTerrain(profile, id, radius, seed), OnTerrain, () => priority() - 1e6f);
        }

        /// <summary>Works out the ground's rules right now, on this thread. For the starting planet and for tests.</summary>
        public void BuildTerrainNow(Func<float> priority)
        {
            if (Terrain != null) return;
            _bodyPriority = priority;
            _pictureHeld = true;
            if (_terrainJob != null) _terrainJob.Cancel();
            OnTerrain(BuildTerrain(Profile, Body.Spec.id, BodyRadius, _seed));
        }

        static TerrainBuild BuildTerrain(PlanetProfile profile, string id, double bodyRadius, int seed)
        {
            var terrain = new PlanetTerrain(profile, id, bodyRadius, seed);
            var build = new TerrainBuild { Terrain = terrain, PlacementPositions = new Vec3d[terrain.Placements.Count], DeepestCave = double.MaxValue };
            for (int i = 0; i < build.PlacementPositions.Length; i++) build.PlacementPositions[i] = PositionOf(terrain, terrain.Placements[i]);
            for (int c = 0; c < terrain.Caves.Count; c++)
            {
                Cave cave = terrain.Caves[c];
                for (int k = 0; k < cave.Centres.Length; k++)
                {
                    build.DeepestCave = Math.Min(build.DeepestCave, cave.Centres[k].Magnitude - cave.Radii[k] * 1.3);
                }
            }
            return build;
        }

        /// <summary>Where a placed thing stands, in metres from the body's centre.</summary>
        public static Vec3d PositionOf(PlanetTerrain terrain, in Placement p)
        {
            if (p.Absolute) return p.Position;
            double height = terrain.Height(p.Direction);
            if (p.Floats && terrain.HasSea) height = Math.Max(height, terrain.SeaLevel);
            return p.Direction * (terrain.Radius + height);
        }

        void OnTerrain(TerrainBuild build)
        {
            if (this == null || Terrain != null) return;
            Terrain = build.Terrain;
            PlacementPositions = build.PlacementPositions;
            Weather = new WeatherModel(Profile, Body.Spec.id, SurfaceRadius, _seed);
            // The safety net sits under the lowest ground and the deepest cave.
            _safetyRadius = Math.Min(SurfaceRadius + Terrain.MinHeight, build.DeepestCave) - 30;
            _ground = PlanetMaterials.Ground(Body.Spec.id, Settings.groundRelief);
            _owned.Add(_ground);
            Features.SetTerrain(Terrain, PlacementPositions);
            Digging = new PlanetDigging(this);
            if (!_pictureHeld) BeginPicture();
        }

        /// <summary>
        /// Paints the far-away picture from the real ground, in the background, in slices so other work can run
        /// in between.
        /// </summary>
        public void BeginPicture()
        {
            _pictureHeld = false;
            if (Interior || Terrain == null || _pictureStarted) return;
            _pictureStarted = true;
            int width = Body.Spec.kind == BodyKind.Moon ? Settings.moonPictureWidth : Settings.planetPictureWidth;
            if (PictureWidthForTests > 0) width = PictureWidthForTests;
            width = Mathf.Clamp(Mathf.ClosestPowerOfTwo(width), 64, 4096);
            int height = width / 2;
            var pixels = new byte[width * height * 4];
            PlanetTerrain terrain = Terrain;
            // Small slices: a slice cannot be interrupted, so each must be short enough not to hold up urgent work.
            const int slices = 32;
            int slice = 0;
            Func<float> priority = _bodyPriority ?? (() => 0f);

            void Next()
            {
                int first = height * slice / slices, end = height * (slice + 1) / slices;
                _pictureJob = PlanetJobs.Run(() =>
                {
                    FarMap.Paint(terrain, width, pixels, first, end);
                    return pixels;
                }, _ =>
                {
                    slice++;
                    if (slice < slices) Next();
                    else OnPicture(pixels, width, height);
                }, () => priority() + 500f, true);
            }
            Next();
        }

        void OnPicture(byte[] pixels, int width, int height)
        {
            if (this == null) return;
            _pictureJob = null;
            _picture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = Body.Spec.id + " surface",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            _picture.SetPixelData(pixels, 0);
            _picture.Apply(true, true);
            _owned.Add(_picture);
            Body.SetPicture(_picture);
            // Ground further than this is drawn with the picture's colours, so the two always agree.
            float tile = (float)TerrainMesher.TileSize(SurfaceRadius, MaxLevel);
            PlanetMaterials.SetFarPicture(_ground, _picture, tile * 3f, tile * 12f);
            PictureReady = true;
        }

        void BuildAirRim()
        {
            if (!Profile.hasAtmosphere) return;
            double top = AtmosphereModel.Top(Profile, SurfaceRadius, BodyRadius);
            double shell = Interior ? BodyRadius * 1.025 : BodyRadius + Math.Max(top * 0.5, BodyRadius * 0.02);
            double ratio = BodyRadius / shell;
            float limb = 1f - (float)Math.Sqrt(Math.Max(0, 1 - ratio * ratio));
            Color color = Interior ? HazeColor : SkyColor;
            Material material = PlanetMaterials.Atmosphere(Body.Spec.id + " air", color, 0.6f * (float)Math.Max(0.35, Profile.skyOpacity), limb);
            if (material == null) return;
            _owned.Add(material);
            _airRimMaterial = material;
            _airRimStrength = 0.6f * (float)Math.Max(0.35, Profile.skyOpacity);
            _airRimShown = _airRimStrength;
            _airRimShell = shell;

            _airRim = new GameObject("AirRim");
            _airRim.transform.SetParent(Body.transform, false);
            _airRim.transform.localScale = Vector3.one * (float)shell;
            _airRim.AddComponent<MeshFilter>().sharedMesh = SpaceMeshes.Sphere(64);
            var renderer = _airRim.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ near and far

        /// <summary>Switches the detailed ground on (the viewer has come near) or off (they have left).</summary>
        public void SetNear(bool near)
        {
            if (near == Near || (near && !Ready)) return;
            Near = near;
            if (near)
            {
                _tileRoot.gameObject.SetActive(true);
                _roots = new Node[CubeSphere.Faces];
                for (int f = 0; f < CubeSphere.Faces; f++) _roots[f] = NewNode(f, 0, 0, 0);
                BuildDetail();
                Features.SetActive(true);
                Digging?.Show();
            }
            else
            {
                Digging?.Hide();
                _digCover = DigCover.None;
                for (int f = 0; f < _roots.Length; f++) Release(_roots[f]);
                _roots = null;
                while (_spare.Count > 0) DestroyNode(_spare.Pop());
                _scatterNodes.Clear();
                ClearDetail();
                SetAirRim(_airRimStrength);
                Features.SetActive(false);
                _tileRoot.gameObject.SetActive(false);
                _detailRoot.gameObject.SetActive(false);
                ShowGround(false);
            }
        }

        void SetAirRim(float strength)
        {
            if (_airRimMaterial == null || Mathf.Abs(strength - _airRimShown) < 0.002f) return;
            _airRimShown = strength;
            PlanetMaterials.SetStrength(_airRimMaterial, strength);
            if (_airRim != null) _airRim.SetActive(strength > 0.001f);
        }

        void ShowGround(bool shown)
        {
            GroundShown = shown;
            // The sea, pools and caves appear together with the ground they belong to.
            _detailRoot.gameObject.SetActive(shown);
            // Rocky bodies swap the smooth ball for real ground. A gas giant keeps its cloud tops: the ground is inside.
            if (!Interior)
            {
                if (Body.FarLook != null) Body.FarLook.enabled = !shown;
                Body.Ball.radius = shown ? (float)_safetyRadius : (float)BodyRadius;
            }
            else
            {
                Body.Ball.radius = shown ? (float)_safetyRadius : (float)SurfaceRadius;
            }
        }

        /// <summary>
        /// Call once a frame while near. <paramref name="viewer"/> is the camera and <paramref name="focus"/> the
        /// ship, both in metres from the body's centre in the body's own frame. Ground near the focus is solid.
        /// </summary>
        public void Tick(Vec3d viewer, Vec3d focus, float focusSpeed)
        {
            _onePoint[0] = focus;
            Tick(viewer, _onePoint, 1, focusSpeed);
        }

        /// <summary>The most places at once that can ask for solid ground under them.</summary>
        public const int GroundPointLimit = 4;
        /// <summary>
        /// A place that needs ground splits the tiles it is over (and little else) down to the finest, so what it
        /// stands on has the same shape whether or not the player is there to see it.
        /// </summary>
        const double GroundPointSplit = 0.3;
        readonly Vec3d[] _onePoint = new Vec3d[1];
        readonly Vec3d[] _needGround = new Vec3d[GroundPointLimit];
        readonly Vec3d[] _groundFootprints = new Vec3d[GroundPointLimit];
        readonly int[] _groundFaces = new int[GroundPointLimit];
        readonly int[] _groundX = new int[GroundPointLimit];
        readonly int[] _groundY = new int[GroundPointLimit];
        readonly double[] _groundAltitude = new double[GroundPointLimit];
        int _needGroundCount;
        /// <summary>How many places need solid ground this frame (see <see cref="Tick(Vec3d, Vec3d[], int, float)"/>).</summary>
        public int NeedGroundCount => _needGroundCount;
        /// <summary>One of the places that need solid ground, in the body's own space.</summary>
        public Vec3d NeedGroundAt(int i) => _needGround[i];

        /// <summary>
        /// As above, for several places that need solid ground at once. The first is the focus; the rest are
        /// things left behind that must not sink while the player is away (the parked ship).
        /// </summary>
        public void Tick(Vec3d viewer, Vec3d[] ground, int count, float focusSpeed)
        {
            if (!Near) return;
            _viewer = viewer;
            _needGroundCount = Math.Max(0, Math.Min(count, GroundPointLimit));
            for (int g = 0; g < _needGroundCount; g++)
            {
                _needGround[g] = ground[g];
                _groundAltitude[g] = ground[g].Magnitude - GroundRadius(ground[g].Normalized);
                _groundFootprints[g] = ground[g].Normalized * SurfaceRadius;
                CubeSphere.Locate(ground[g], out _groundFaces[g], out double u, out double v);
                int side = 1 << MaxLevel;
                _groundX[g] = Math.Min(side - 1, Math.Max(0, (int)((u + 1) * 0.5 * side)));
                _groundY[g] = Math.Min(side - 1, Math.Max(0, (int)((v + 1) * 0.5 * side)));
            }
            // Faster means solid ground is needed further ahead.
            _solidRange = Mathf.Min(Settings.solidGroundLimit, Mathf.Max(Settings.solidGroundRange, focusSpeed * 3f));
            _tick++;
            _bakesLeft = 1;
            Features.BeginFrame();
            // The glowing rim is a shell seen from outside. Fade it out on the way down to it, so it does not
            // vanish in one frame when the camera passes through.
            double above = (viewer.Magnitude - _airRimShell) / (_airRimShell * 0.04);
            float fade = (float)Math.Max(0, Math.Min(1, above));
            SetAirRim(_airRimStrength * fade * fade * (3f - 2f * fade));

            bool rootsReady = true;
            for (int f = 0; f < _roots.Length; f++) rootsReady &= ReadyToShow(_roots[f]);
            if (!rootsReady) return;
            if (!GroundShown) ShowGround(true);

            ShownTiles = 0;
            SolidTiles = 0;
            for (int f = 0; f < _roots.Length; f++) Refresh(_roots[f]);

            for (int i = _scatterNodes.Count - 1; i >= 0; i--)
            {
                Node n = _scatterNodes[i];
                // Loose rocks only near the viewer, so the cap is spent on what is at your feet, not on the horizon.
                double away = Vec3d.Distance(n.Centre, _viewer) - TileRules.Reach(SurfaceRadius, n.Level);
                bool wanted = !n.Dead && n.SeenTick == _tick && away < Settings.scatterRange * (n.ScatterOut ? 1.2 : 1.0);
                if (wanted && !n.ScatterOut)
                {
                    // False means this frame's allowance is used up: try again next frame.
                    n.ScatterOut = Features.ShowGroup(n, n.Scatter, n.ScatterPositions);
                }
                else if (!wanted && n.ScatterOut)
                {
                    Features.HideGroup(n);
                    n.ScatterOut = false;
                }
                if (n.Dead) _scatterNodes.RemoveAt(i);
            }

            Features.Tick(viewer);
            CookRebuiltTiles();
            Digging?.Tick(viewer);
        }

        void Refresh(Node n)
        {
            n.SeenTick = _tick;
            bool split = TileRules.WantsSplit(SurfaceRadius, n.Level, MaxLevel, n.Centre, _viewer, Settings.splitDistance, n.Children != null);
            for (int g = 0; !split && g < _needGroundCount; g++)
            {
                split = n.Level < MaxLevel && (ContainsGround(n, g)
                    || TileRules.WantsSplit(SurfaceRadius, n.Level, MaxLevel, n.FootprintCentre, _groundFootprints[g], GroundPointSplit, n.Children != null));
            }
            if (split)
            {
                if (n.Children == null)
                {
                    n.Children = new Node[4];
                    for (int c = 0; c < 4; c++) n.Children[c] = NewNode(n.Face, n.Level + 1, n.X * 2 + (c & 1), n.Y * 2 + (c >> 1));
                }
                bool ready = true;
                for (int c = 0; c < 4; c++) ready &= ReadyToShow(n.Children[c]);
                if (ready)
                {
                    bool wasSolid = n.Shown && n.Solid;
                    // The ground under something is about to change shape (finer, with different heights). A
                    // foot that ends up under the new surface would drop through it, so the world is told and
                    // sets whatever stands here back on top.
                    if (wasSolid && NearGround(n) <= 0 && _reshaped.Count < GroundPointLimit * (TileRules.DeepestLevel + 1) * 4) _reshaped.Add(n);
                    // Prepare replacements before retiring the active support. A mesh being ready alone does
                    // not make a tile solid, and a cave opening also needs its wall collider first.
                    for (int c = 0; c < 4; c++)
                    {
                        // Ground you are standing on must never stop being solid, even for one frame. Only the
                        // pieces right under the ship are done at once; the rest follow one a frame.
                        if (wasSolid && !n.Children[c].Solid && NearGround(n.Children[c]) < 25) MakeSolid(n.Children[c]);
                        Refresh(n.Children[c]);
                    }
                    Show(n, false);
                    return;
                }
            }
            else if (n.Children != null)
            {
                // The parent may never have needed a collider during the approach. Make it ready before
                // releasing finer support, including when the normal one-tile budget has already been spent.
                Show(n, true);
                if (NearGround(n) < 25 && HasSolidDescendant(n)) MakeSolid(n);
                for (int c = 0; c < 4; c++) Release(n.Children[c]);
                n.Children = null;
            }

            Show(n, true);
            ShownTiles++;
            UpdateSolid(n);
            if (n.Solid) SolidTiles++;
        }

        void Show(Node n, bool shown)
        {
            if (n.Shown == shown) return;
            n.Shown = shown;
            n.Go.SetActive(shown);
        }

        /// <summary>
        /// Solid ground reaches this far round a thing left behind (the parked ship). The focus gets the full,
        /// speed-driven range, because it is the one that may be arriving fast.
        /// </summary>
        const double LeftBehindRange = 30;

        bool ContainsGround(Node n, int ground)
        {
            int shift = MaxLevel - n.Level;
            return n.Face == _groundFaces[ground] && n.X == (_groundX[ground] >> shift) && n.Y == (_groundY[ground] >> shift);
        }

        double FootprintDistance(Node n, int ground)
        {
            return Vec3d.Distance(n.FootprintCentre, _groundFootprints[ground]) - TileRules.Reach(SurfaceRadius, n.Level);
        }

        bool ReadyToShow(Node n)
        {
            if (!n.HasMesh) return false;
            if (n.RequiredCaves == null) return true;
            for (int c = 0; c < n.RequiredCaves.Count; c++)
                if (!_readyCaves.Contains(n.RequiredCaves[c])) return false;
            return true;
        }

        static bool HasSolidDescendant(Node n)
        {
            if (n.Children == null) return n.Shown && n.Solid;
            for (int c = 0; c < n.Children.Length; c++)
                if (HasSolidDescendant(n.Children[c])) return true;
            return false;
        }

        void UpdateSolid(Node n)
        {
            bool wanted = false, kept = false, under = false;
            for (int g = 0; g < _needGroundCount; g++)
            {
                double distance = Math.Max(FootprintDistance(n, g), _groundAltitude[g]);
                double range = g == 0 ? _solidRange : LeftBehindRange;
                wanted |= distance <= range;
                kept |= distance <= range * 1.6 + 40;
                under |= distance < 12 || (_groundAltitude[g] <= range && ContainsGround(n, g));
            }
            if (!n.Solid)
            {
                if (!wanted) return;
                // One new solid tile a frame keeps the cost even. Ground right under something is never made to
                // wait its turn: a tile that is late there is a hole to fall through.
                if (!under)
                {
                    if (_bakesLeft <= 0) return;
                    _bakesLeft--;
                }
                MakeSolid(n);
            }
            else if (!kept)
            {
                n.Collider.enabled = false;
                n.Solid = false;
            }
        }

        /// <summary>How far a tile's edge is from the nearest place that needs solid ground (0 or less: under it).</summary>
        double NearGround(Node n)
        {
            double nearest = double.MaxValue;
            for (int g = 0; g < _needGroundCount; g++)
            {
                double range = g == 0 ? _solidRange : LeftBehindRange;
                if (_groundAltitude[g] > range) continue;
                nearest = Math.Min(nearest, FootprintDistance(n, g));
                if (ContainsGround(n, g)) nearest = Math.Min(nearest, 0);
            }
            return nearest;
        }

        readonly List<Node> _reshaped = new List<Node>();

        /// <summary>
        /// True once if solid ground at this point (metres from the body's centre) was swapped for a finer shape
        /// since the last time anyone asked. Call <see cref="ForgetReshaped"/> after asking for every mover.
        /// </summary>
        public bool ReshapedUnder(Vec3d point)
        {
            for (int i = 0; i < _reshaped.Count; i++)
            {
                Node n = _reshaped[i];
                if (TerrainCollision.ContainsDirection(n.Face, n.Level, n.X, n.Y, point)) return true;
            }
            return false;
        }

        public bool AnyReshaped => _reshaped.Count > 0;
        public void ForgetReshaped() => _reshaped.Clear();

        /// <summary>
        /// True if the ground showing under a point is solid and in its finest shape, so its real height is
        /// what <see cref="GroundRadius"/> says. Coarser ground can be metres off.
        /// </summary>
        public bool FineSolidAt(Vec3d point)
        {
            Node n = SupportingNode(point);
            return n != null && n.Shown && n.Solid && n.Level >= MaxLevel;
        }

        public bool SolidAt(Vec3d point)
        {
            Node n = SupportingNode(point);
            return n != null && n.Shown && n.Solid;
        }

        Node SupportingNode(Vec3d point)
        {
            if (!GroundShown || _roots == null || point.SqrMagnitude < 1e-12) return null;
            CubeSphere.Locate(point, out int face, out double u, out double v);
            Node n = _roots[face];
            while (n != null && !n.Shown && n.Children != null)
            {
                int cells = 1 << (n.Level + 1);
                int x = Math.Min(cells - 1, Math.Max(0, (int)((u + 1) * 0.5 * cells)));
                int y = Math.Min(cells - 1, Math.Max(0, (int)((v + 1) * 0.5 * cells)));
                n = n.Children[(x & 1) | ((y & 1) << 1)];
            }
            return n;
        }

        /// <summary>
        /// Makes the ground showing at a point (metres from the body's centre) solid now. Used when something
        /// has just been put back on top of the ground, so it cannot fall straight back in.
        /// </summary>
        public void MakeSolidAt(Vec3d point)
        {
            if (!GroundShown) return;
            for (int f = 0; f < _roots.Length; f++) MakeSolidAt(_roots[f], point);
        }

        void MakeSolidAt(Node n, Vec3d point)
        {
            if (n == null || (!TerrainCollision.ContainsDirection(n.Face, n.Level, n.X, n.Y, point)
                && TerrainCollision.FootprintDistance(SurfaceRadius, n.Face, n.Level, n.X, n.Y, point) > 2)) return;
            if (n.Shown && n.HasMesh) { if (!n.Solid) MakeSolid(n); return; }
            if (n.Children == null) return;
            for (int c = 0; c < 4; c++) MakeSolidAt(n.Children[c], point);
        }

        void MakeSolid(Node n)
        {
            if (n.Collider == null) n.Collider = n.Go.AddComponent<MeshCollider>();
            if (n.Collider.sharedMesh != n.Mesh) n.Collider.sharedMesh = n.Mesh;
            n.Collider.enabled = true;
            n.Solid = true;
        }

        // ------------------------------------------------------------------ tiles

        Node NewNode(int face, int level, int x, int y)
        {
            Node n = _spare.Count > 0 ? _spare.Pop() : new Node();
            n.Face = face;
            n.Level = level;
            n.X = x;
            n.Y = y;
            n.Dead = false;
            n.HasMesh = false;
            n.RequiredCaves = null;
            n.Shown = false;
            n.Solid = false;
            n.Children = null;
            n.Scatter = null;
            n.ScatterPositions = null;
            n.ScatterOut = false;
            n.SeenTick = 0;
            Vec3d dir = TerrainMesher.TileCentre(face, level, x, y);
            n.FootprintCentre = dir * SurfaceRadius;
            n.Centre = dir * (SurfaceRadius + Terrain.CoarseHeight(dir));

            PlanetTerrain terrain = Terrain;
            // Cave mouths are only cut into the two finest levels: coarser ground is too far away to see them.
            bool cut = level >= MaxLevel - 1;
            bool scatter = level == Mathf.Max(0, MaxLevel - 1);
            bool linear = _linear;
            // The finest tiles leave out their squares where dug ground is drawn instead.
            DigCover dig = level >= MaxLevel && !_digCover.IsEmpty ? _digCover : null;
            n.DigVersion = _digCover.Version;
            n.Rebuild = null;
            n.Job = PlanetJobs.Run(() => BuildTile(terrain, face, level, x, y, cut, scatter, linear, dig), build => OnTile(n, build), () => TilePriority(n));
            return n;
        }

        float TilePriority(Node n)
        {
            // Nearest first; among equals, coarser first, so there is always something to show.
            return (float)Vec3d.Distance(n.Centre, _viewer) + n.Level * 2f - 2e6f;
        }

        static TileBuild BuildTile(PlanetTerrain terrain, int face, int level, int x, int y, bool cut, bool scatter, bool linear, DigCover dig)
        {
            var build = new TileBuild { Chunk = TerrainMesher.Build(terrain, face, level, x, y, cut, dig) };
            if (build.Chunk.HasHole)
            {
                build.RequiredCaves = new List<Cave>();
                for (int c = 0; c < terrain.Caves.Count; c++)
                {
                    Cave cave = terrain.Caves[c];
                    for (int m = 0; m < cave.Mouths.Length; m++)
                    {
                        double distance = TerrainCollision.FootprintDistance(terrain.Radius, face, level, x, y, cave.Mouths[m]);
                        if (distance > cave.MouthRadius + build.Chunk.CellSize * 2) continue;
                        build.RequiredCaves.Add(cave);
                        break;
                    }
                }
                // Fail closed if a future hole kind cannot be associated with the current mouth bounds.
                if (build.RequiredCaves.Count == 0) build.RequiredCaves.AddRange(terrain.Caves);
            }
            if (linear)
            {
                float[] colors = build.Chunk.Colors;
                for (int i = 0; i < colors.Length; i++) colors[i] = SkyGlobals.ToLinear(colors[i]);
            }
            if (scatter)
            {
                build.Scatter = new List<Placement>();
                PlanetScatter.ForTile(terrain, face, level, x, y, build.Scatter);
                build.ScatterPositions = new Vec3d[build.Scatter.Count];
                for (int i = 0; i < build.ScatterPositions.Length; i++) build.ScatterPositions[i] = PositionOf(terrain, build.Scatter[i]);
            }
            return build;
        }

        void OnTile(Node n, TileBuild build)
        {
            n.Job = null;
            if (this == null || n.Dead || !Near) return;
            ChunkData chunk = build.Chunk;

            if (n.Go == null)
            {
                n.Go = new GameObject("Tile");
                n.Go.transform.SetParent(_tileRoot, false);
                n.Go.layer = Layers.Default;
                n.Go.AddComponent<DiggableGround>().Surface = this;
                n.Mesh = new Mesh { name = "Ground" };
                n.Go.AddComponent<MeshFilter>().sharedMesh = n.Mesh;
                var renderer = n.Go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _ground;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            n.Go.SetActive(false);
            ApplyMesh(n, chunk);
            n.RequiredCaves = build.RequiredCaves;

            if (build.Scatter != null && build.Scatter.Count > 0)
            {
                n.Scatter = build.Scatter;
                n.ScatterPositions = build.ScatterPositions;
                _scatterNodes.Add(n);
            }
            // Dug ground came or went while this tile was being worked out: work it out again.
            if (n.Level >= MaxLevel && n.DigVersion != _digCover.Version && (chunk.DugOut || TileNear(n, _digCover.Min, _digCover.Max))) RebuildTile(n);
        }

        void ApplyMesh(Node n, ChunkData chunk)
        {
            int count = chunk.VertexCount;
            VertexBuffer.Clear();
            NormalBuffer.Clear();
            DirectionBuffer.Clear();
            ColorBuffer.Clear();
            Vector3 low = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), high = -low;
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                var p = new Vector3(chunk.Positions[o], chunk.Positions[o + 1], chunk.Positions[o + 2]);
                low = Vector3.Min(low, p);
                high = Vector3.Max(high, p);
                VertexBuffer.Add(p);
                NormalBuffer.Add(new Vector3(chunk.Normals[o], chunk.Normals[o + 1], chunk.Normals[o + 2]));
                DirectionBuffer.Add(p.normalized);
                ColorBuffer.Add(new Color(chunk.Colors[o], chunk.Colors[o + 1], chunk.Colors[o + 2], 1f));
            }
            n.Mesh.Clear();
            n.Mesh.SetVertices(VertexBuffer);
            n.Mesh.SetNormals(NormalBuffer);
            n.Mesh.SetUVs(0, DirectionBuffer);
            n.Mesh.SetColors(ColorBuffer);
            n.Mesh.SetTriangles(chunk.Triangles, 0, true);
            n.HasMesh = true;
            n.DugOut = chunk.DugOut;
            n.BoxMin = SpaceConvert.ToVec3d(low);
            n.BoxMax = SpaceConvert.ToVec3d(high);
        }

        // ------------------------------------------------------------------ dug ground

        /// <summary>
        /// Called by <see cref="Digging"/> when boxes of dug ground have come up (drawn and solid) between min and
        /// max: the finest tiles and the cave walls there are worked out again without their faces inside them.
        /// </summary>
        public void OnDigCover(DigCover cover, Vec3d min, Vec3d max)
        {
            _digCover = cover ?? DigCover.None;
            if (_roots == null) return;
            for (int f = 0; f < _roots.Length; f++) RebuildNear(_roots[f], min, max);
            for (int i = _networkViews.Count - 1; i >= 0; i--)
            {
                if (_networkViews[i] == null) { _networkViews.RemoveAt(i); continue; }
                _networkViews[i].OnDigCover(_digCover, min, max);
            }
        }

        void RebuildNear(Node n, Vec3d min, Vec3d max)
        {
            if (n == null || n.Dead || !TileNear(n, min, max)) return;
            if (n.Children != null)
                for (int c = 0; c < 4; c++) RebuildNear(n.Children[c], min, max);
            // Tiles still being worked out check the cover's version when they arrive.
            if (n.Level >= MaxLevel && n.HasMesh) RebuildTile(n);
        }

        bool TileNear(Node n, Vec3d min, Vec3d max)
        {
            if (min.X > max.X) return false;
            // A finest tile with a shape: only if its own box touches the change (the test the tile's builder uses).
            if (n.Level >= MaxLevel && n.HasMesh)
                return !(n.BoxMax.X < min.X - 2 || n.BoxMax.Y < min.Y - 2 || n.BoxMax.Z < min.Z - 2
                         || n.BoxMin.X > max.X + 2 || n.BoxMin.Y > max.Y + 2 || n.BoxMin.Z > max.Z + 2);
            double reach = TerrainMesher.TileSize(SurfaceRadius, n.Level) * 0.8 + DigRules.Margin + 2;
            Vec3d c = n.Centre;
            double dx = Math.Max(Math.Max(min.X - c.X, 0), c.X - max.X);
            double dy = Math.Max(Math.Max(min.Y - c.Y, 0), c.Y - max.Y);
            double dz = Math.Max(Math.Max(min.Z - c.Z, 0), c.Z - max.Z);
            return dx * dx + dy * dy + dz * dz < reach * reach;
        }

        /// <summary>Works a finest tile out again with the cover as it is now, and swaps its shape (and collider) in place.</summary>
        void RebuildTile(Node n)
        {
            if (n.Rebuild != null) n.Rebuild.Cancel();
            PlanetTerrain terrain = Terrain;
            int face = n.Face, level = n.Level, x = n.X, y = n.Y;
            bool linear = _linear;
            DigCover dig = _digCover;
            int version = dig.Version;
            PlanetJobs.Handle handle = null;
            Vec3d centre = n.Centre;
            bool patient = Vec3d.Distance(centre, _viewer) > PlanetDigging.UrgentReach + 40;
            handle = PlanetJobs.Run(() => BuildTile(terrain, face, level, x, y, true, false, linear, dig), build =>
            {
                if (n.Rebuild == handle) n.Rebuild = null;
                if (this == null || n.Dead || !n.HasMesh || !Near) return;
                n.DigVersion = version;
                // Nothing was left out before and nothing is now: the shape is the same, so keep it as it is.
                if (!n.DugOut && !build.Chunk.DugOut) return;
                ApplyMesh(n, build.Chunk);
                n.RequiredCaves = build.RequiredCaves;
                if (n.Collider != null)
                {
                    if (n.Solid)
                    {
                        if (!n.NeedsCook) _cookQueue.Add(n);
                        n.NeedsCook = true;
                    }
                    // A tile that is not solid now must not keep the old shape in its collider: when it is made solid
                    // again (MakeSolid hands the mesh over only if it is a different one) it would be a lid over the hole.
                    else n.Collider.sharedMesh = null;
                }
            }, () => PlanetDigging.Priority(centre, _viewer), patient);
            n.Rebuild = handle;
        }

        readonly List<Node> _cookQueue = new List<Node>();

        /// <summary>Cooks again, within the frame's budget, the colliders of finest tiles rebuilt round dug ground.</summary>
        void CookRebuiltTiles()
        {
            for (int i = 0; i < _cookQueue.Count;)
            {
                Node n = _cookQueue[i];
                if (n.Dead || !n.NeedsCook || n.Collider == null || !n.Solid)
                {
                    // Not solid any more: drop the old shape too (see RebuildTile).
                    if (!n.Dead && n.NeedsCook && n.Collider != null && !n.Solid) n.Collider.sharedMesh = null;
                    n.NeedsCook = false;
                    _cookQueue.RemoveAt(i);
                    continue;
                }
                if (!GroundCooks.Take()) return;
                // Handing the same mesh over again cooks it again.
                n.Collider.sharedMesh = null;
                n.Collider.sharedMesh = n.Mesh;
                n.NeedsCook = false;
                _cookQueue.RemoveAt(i);
                Digging?.WakeIn(n.BoxMin, n.BoxMax);
            }
        }

        /// <summary>True if the ground drawn at a point (metres from the body's centre) is a finest tile, the only kind that leaves holes for dug ground.</summary>
        public bool FinestDrawnAt(Vec3d point)
        {
            Node n = SupportingNode(point);
            return n != null && n.Shown && n.Level >= MaxLevel;
        }

        /// <summary>Takes a tile (and everything under it) out of use and keeps its objects for the next one.</summary>
        void Release(Node n)
        {
            if (n.Children != null)
            {
                for (int c = 0; c < 4; c++) Release(n.Children[c]);
                n.Children = null;
            }
            if (n.Job != null)
            {
                n.Job.Cancel();
                n.Job = null;
            }
            if (n.Rebuild != null)
            {
                n.Rebuild.Cancel();
                n.Rebuild = null;
            }
            n.NeedsCook = false;
            if (n.ScatterOut)
            {
                Features.HideGroup(n);
                n.ScatterOut = false;
            }
            n.Dead = true;
            n.HasMesh = false;
            n.Shown = false;
            n.Solid = false;
            if (n.Go != null)
            {
                n.Go.SetActive(false);
                if (n.Collider != null)
                {
                    n.Collider.enabled = false;
                    n.Collider.sharedMesh = null;
                }
            }
            // A dead node may still be in the scatter list this frame; a fresh object avoids mixing the two up.
            var spare = new Node { Go = n.Go, Mesh = n.Mesh, Collider = n.Collider, Dead = true };
            n.Go = null;
            n.Mesh = null;
            n.Collider = null;
            if (spare.Go != null) _spare.Push(spare);
        }

        static void DestroyNode(Node n)
        {
            if (n.Mesh != null) Destroy(n.Mesh);
            if (n.Go != null) Destroy(n.Go);
        }

        // ------------------------------------------------------------------ sea, pools, caves

        void BuildDetail()
        {
            if (Terrain.HasSea)
            {
                if (_seaMaterial == null)
                {
                    _seaMaterial = PlanetMaterials.Water(Body.Spec.id + " sea", SeaColor, SeaDeepColor, 0.84f, 4f);
                    _owned.Add(_seaMaterial);
                }
                _sea = new GameObject("Sea");
                _sea.transform.SetParent(_detailRoot, false);
                _sea.transform.localScale = Vector3.one * (float)(SurfaceRadius + Terrain.SeaLevel);
                _sea.AddComponent<MeshFilter>().sharedMesh = SpaceMeshes.Sphere(192);
                AddRenderer(_sea, _seaMaterial);
            }

            for (int i = 0; i < Terrain.Ponds.Count; i++) BuildPond(Terrain.Ponds[i]);

            PlanetTerrain terrain = Terrain;
            bool linear = _linear;
            DigCover dig = _digCover;
            for (int i = 0; i < Terrain.Caves.Count; i++)
            {
                Cave cave = Terrain.Caves[i];
                if (cave.Network != null)
                {
                    // A whole network: blocks of wall and a box of light, put up over a few frames once worked out.
                    // Patient and ordered by distance only: a network is seconds of work and is never waited for when
                    // a run starts (the player cannot be inside one yet), so it never holds up the first ground.
                    _caveJobs.Add(PlanetJobs.Run(() =>
                    {
                        var build = new CaveBuild
                        {
                            Chunks = CaveNetworkMesher.Build(cave.Network, terrain, dig),
                            Light = CaveLightVolume.Build(cave.Network, terrain),
                        };
                        if (linear) CaveNetworkView.ToLinear(build);
                        return build;
                    }, build => OnCaveNetwork(cave, build, dig.Version), () => (float)Vec3d.Distance(cave.Centres[0], _viewer), patient: true));
                    continue;
                }
                _caveJobs.Add(PlanetJobs.Run(() =>
                {
                    ShapeData shape = CaveMesher.Build(cave, terrain);
                    if (linear)
                    {
                        for (int k = 0; k < shape.Colors.Length; k++) shape.Colors[k] = SkyGlobals.ToLinear(shape.Colors[k]);
                    }
                    return shape;
                }, shape => OnCave(cave, shape), () => (float)Vec3d.Distance(cave.Centres[0], _viewer) - 1e6f));
            }
        }

        void ClearDetail()
        {
            // Caves still being built for this visit must not turn up during the next one as duplicates.
            for (int i = 0; i < _caveJobs.Count; i++) _caveJobs[i].Cancel();
            _caveJobs.Clear();
            _networkViews.Clear();
            _readyCaves.Clear();
            for (int i = _detailRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = _detailRoot.GetChild(i).gameObject;
                var filter = child.GetComponent<MeshFilter>();
                // Cave walls are one-off meshes; the sea ball and pool disc are shared and must be kept.
                if (filter != null && filter.sharedMesh != null && child.name == "Cave") Destroy(filter.sharedMesh);
                Destroy(child);
            }
            _sea = null;
        }

        Material PoolMaterial(Liquid liquid)
        {
            if (_poolMaterials.TryGetValue(liquid, out Material cached) && cached != null) return cached;
            Color color, deep;
            float glow = 0f, opacity = 0.86f, ripple = 2.2f;
            switch (liquid)
            {
                case Liquid.Lava:
                    color = new Color(1f, 0.42f, 0.08f);
                    deep = new Color(0.75f, 0.12f, 0.02f);
                    glow = 1.3f;
                    opacity = 1f;
                    ripple = 6f;
                    break;
                case Liquid.Methane:
                    color = new Color(0.16f, 0.13f, 0.09f);
                    deep = new Color(0.05f, 0.04f, 0.03f);
                    opacity = 0.93f;
                    break;
                case Liquid.Brine:
                    color = new Color(0.55f, 0.80f, 0.76f);
                    deep = new Color(0.22f, 0.45f, 0.45f);
                    break;
                case Liquid.Sulfur:
                    color = new Color(0.86f, 0.76f, 0.20f);
                    deep = new Color(0.55f, 0.40f, 0.08f);
                    glow = 0.35f;
                    opacity = 1f;
                    ripple = 5f;
                    break;
                default:
                    color = Profile.hasSea ? SeaColor : new Color(0.18f, 0.5f, 0.71f);
                    deep = color * 0.45f;
                    break;
            }
            Material material = PlanetMaterials.Water(liquid + " pool", color, deep, opacity, ripple, glow);
            _owned.Add(material);
            _poolMaterials[liquid] = material;
            return material;
        }

        void BuildPond(Pond pond)
        {
            var go = new GameObject(pond.Liquid + "Pool");
            go.transform.SetParent(_detailRoot, false);
            Vector3 up = SpaceConvert.ToVector3(pond.Direction);
            go.transform.localPosition = up * (float)(SurfaceRadius + pond.Level);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, up);
            go.transform.localScale = Vector3.one * (float)pond.Radius;
            go.AddComponent<MeshFilter>().sharedMesh = Disc();
            AddRenderer(go, PoolMaterial(pond.Liquid));
        }

        void OnCave(Cave cave, ShapeData shape)
        {
            if (this == null || !Near || Terrain == null) return;
            int count = shape.VertexCount;
            var vertices = new Vector3[count];
            var colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                vertices[i] = new Vector3(shape.Positions[o], shape.Positions[o + 1], shape.Positions[o + 2]);
                colors[i] = new Color(shape.Colors[o], shape.Colors[o + 1], shape.Colors[o + 2], 1f);
            }
            var mesh = new Mesh { name = "Cave " + cave.Formation };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(shape.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Cave");
            go.transform.SetParent(_detailRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            // The Sun never reaches in here: the walls show only by the lamp and the little light that leaks in.
            AddRenderer(go, PlanetMaterials.VertexColour(Color.white, 0f));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            _readyCaves.Add(cave);
        }

        void OnCaveNetwork(Cave cave, CaveBuild build, int digVersion)
        {
            if (this == null || !Near || Terrain == null) return;
            // Only counted as there (safe for enemies) once its last block of wall is drawn; blocks are made solid near the player.
            CaveNetworkView view = CaveNetworkView.Create(_detailRoot, this, cave, build, ready =>
            {
                if (this != null && Near) _readyCaves.Add(ready);
            });
            _networkViews.Add(view);
            // Ground was dug near it while its walls were being worked out.
            if (digVersion != _digCover.Version && !_digCover.IsEmpty) view.OnDigCover(_digCover, _digCover.Min, _digCover.Max);
        }

        /// <summary>True once a cave's walls are up (a network's blocks are made solid as the player nears them).</summary>
        public bool CaveReady(Cave cave) => _readyCaves.Contains(cave);

        /// <summary>
        /// For the debug readout: how far it is over the ground to the nearest way into a cave network on this body
        /// (from the last place the ground was built round), or empty if this body has none. Watch it shrink.
        /// </summary>
        public string NearestNetworkForDebug()
        {
            if (Terrain == null || _viewer.SqrMagnitude < 1) return string.Empty;
            Vec3d here = _viewer.Normalized;
            double best = double.MaxValue;
            foreach (Cave cave in Terrain.Caves)
            {
                if (cave.Network == null) continue;
                foreach (CaveOpening o in cave.Network.Openings)
                {
                    double d = Vec3d.Distance(o.Direction, here) * SurfaceRadius;
                    if (d < best) best = d;
                }
            }
            if (best == double.MaxValue) return string.Empty;
            return "   cave way in " + (best < 1000 ? best.ToString("0") + " m" : (best / 1000).ToString("0.0") + " km");
        }

        /// <summary>True once a cave network's walls are up and solid.</summary>
        public bool NetworkReady(CaveNetwork network)
        {
            foreach (Cave cave in _readyCaves) if (cave.Network == network) return true;
            return false;
        }

        static void AddRenderer(GameObject go, Material material)
        {
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>A flat disc of radius 1 facing up, shared by every pool.</summary>
        static Mesh Disc()
        {
            if (_disc != null) return _disc;
            const int around = 40;
            var vertices = new Vector3[around + 1];
            var normals = new Vector3[around + 1];
            var triangles = new int[around * 3];
            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            for (int i = 0; i < around; i++)
            {
                float a = i / (float)around * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                normals[i + 1] = Vector3.up;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % around;
                triangles[i * 3 + 2] = 1 + i;
            }
            _disc = new Mesh { name = "PoolDisc", vertices = vertices, normals = normals, triangles = triangles };
            _disc.RecalculateBounds();
            return _disc;
        }

        // ------------------------------------------------------------------ questions other scripts ask

        /// <summary>Distance from the body's centre to the ground in a direction, in metres.</summary>
        public double GroundRadius(Vec3d direction)
        {
            return Terrain != null ? SurfaceRadius + Terrain.Height(direction) : BodyRadius;
        }

        /// <summary>True if a point (metres from the body's centre) is inside one of the caves.</summary>
        public bool InCave(Vec3d point, double margin = 3)
        {
            if (Terrain == null) return false;
            // Down a dug hole or tunnel counts too: nobody there is "sunk" into the ground and lifted out of it. Tightly,
            // whatever the margin asked for: a wide margin round every bullet hole would turn the safety net off
            // across a whole battlefield.
            if (Digging != null && Digging.Field.InDugAir(point, Math.Min(margin, 0.5))) return true;
            for (int c = 0; c < Terrain.Caves.Count; c++)
            {
                Cave cave = Terrain.Caves[c];
                if (_readyCaves.Contains(cave) && TerrainCollision.InsideCave(cave, point, margin)) return true;
            }
            return false;
        }

        /// <summary>The air (or water) at a point given in metres from the body's centre.</summary>
        public AirSample AirAt(Vec3d point)
        {
            return AtmosphereModel.At(Profile, SurfaceRadius, BodyRadius, point.Magnitude - SurfaceRadius);
        }

        /// <summary>Distance from the body's centre beyond which there is no air at all, in metres.</summary>
        public double AirTop
        {
            get
            {
                if (!Profile.hasAtmosphere) return Profile.hasSea ? SurfaceRadius + Profile.seaLevel : 0;
                double top = SurfaceRadius + AtmosphereModel.Top(Profile, SurfaceRadius, BodyRadius);
                return Profile.cloudDeck ? top + BodyRadius * 0.04 : top;
            }
        }

        /// <summary>
        /// The weather at a point (metres from the body's centre) at a moment. Storms live in the lower air:
        /// they fade out as you climb, and there are none under water.
        /// </summary>
        public WeatherSample WeatherAt(Vec3d point, double time, in AirSample air)
        {
            if (Weather == null || !Weather.Any || !air.InAtmosphere || air.Underwater) return default;
            double altitude = point.Magnitude - SurfaceRadius;
            double top = AtmosphereModel.Top(Profile, SurfaceRadius, BodyRadius);
            double fade = Profile.cloudDeck
                ? (altitude < top ? 1 : 0)
                : 1 - Noise3.Smooth(top * 0.35, top * 0.6, altitude);
            if (fade <= 0) return default;
            WeatherSample sample = Weather.At(point.Normalized, time);
            sample.Intensity *= fade;
            sample.Wind = sample.Wind * fade;
            return sample;
        }

        /// <summary>
        /// Stops and finishes all the ground that is currently wanted. For tests and for the first moment of a
        /// run; never during play.
        /// </summary>
        public void FinishPending()
        {
            PlanetJobs.FinishAll();
        }

        void OnDestroy()
        {
            if (_terrainJob != null) _terrainJob.Cancel();
            if (_pictureJob != null) _pictureJob.Cancel();
            if (_detailRoot != null) ClearDetail();
            if (_roots != null)
            {
                for (int f = 0; f < _roots.Length; f++) Release(_roots[f]);
            }
            while (_spare.Count > 0) DestroyNode(_spare.Pop());
            if (Features != null) Features.Dispose();
            Digging?.Destroy();
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null) Destroy(_owned[i]);
            }
        }
    }
}
