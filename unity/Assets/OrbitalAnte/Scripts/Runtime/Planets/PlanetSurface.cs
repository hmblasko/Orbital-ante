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
            public List<Placement> Scatter;
            public Vec3d[] ScatterPositions;
        }

        sealed class Node
        {
            public int Face, Level, X, Y;
            /// <summary>Middle of the tile, in metres from the body's centre.</summary>
            public Vec3d Centre;
            public Node[] Children;
            public GameObject Go;
            public Mesh Mesh;
            public MeshCollider Collider;
            public bool Solid;
            public bool HasMesh;
            public bool Shown;
            public bool Dead;
            public PlanetJobs.Handle Job;
            public List<Placement> Scatter;
            public Vec3d[] ScatterPositions;
            public bool ScatterOut;
            public int SeenTick;
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
        readonly Stack<Node> _spare = new Stack<Node>();
        readonly List<Node> _scatterNodes = new List<Node>();
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        Vec3d _viewer;
        Vec3d _focus;
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
            _ground = PlanetMaterials.Ground(Body.Spec.id);
            _owned.Add(_ground);
            Features.SetTerrain(Terrain, PlacementPositions);
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
            }
            else
            {
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
            if (!Near) return;
            _viewer = viewer;
            _focus = focus;
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
            for (int f = 0; f < _roots.Length; f++) rootsReady &= _roots[f].HasMesh;
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
        }

        void Refresh(Node n)
        {
            n.SeenTick = _tick;
            bool split = TileRules.WantsSplit(SurfaceRadius, n.Level, MaxLevel, n.Centre, _viewer, Settings.splitDistance, n.Children != null);
            if (split)
            {
                if (n.Children == null)
                {
                    n.Children = new Node[4];
                    for (int c = 0; c < 4; c++) n.Children[c] = NewNode(n.Face, n.Level + 1, n.X * 2 + (c & 1), n.Y * 2 + (c >> 1));
                }
                bool ready = true;
                for (int c = 0; c < 4; c++) ready &= n.Children[c].HasMesh;
                if (ready)
                {
                    bool wasSolid = n.Shown && n.Solid;
                    Show(n, false);
                    for (int c = 0; c < 4; c++)
                    {
                        // Ground you are standing on must never stop being solid, even for one frame. Only the
                        // pieces right under the ship are done at once; the rest follow one a frame.
                        if (wasSolid && !n.Children[c].Solid && NearFocus(n.Children[c], 25)) MakeSolid(n.Children[c]);
                        Refresh(n.Children[c]);
                    }
                    return;
                }
            }
            else if (n.Children != null)
            {
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

        void UpdateSolid(Node n)
        {
            double distance = Vec3d.Distance(n.Centre, _focus) - TileRules.Reach(SurfaceRadius, n.Level);
            if (!n.Solid)
            {
                if (distance > _solidRange) return;
                // One new solid tile a frame keeps the cost even (one extra if the ship is right on top of it).
                if (_bakesLeft <= (distance < 12 ? -1 : 0)) return;
                _bakesLeft--;
                MakeSolid(n);
            }
            else if (distance > _solidRange * 1.6f + 40)
            {
                n.Collider.enabled = false;
                n.Solid = false;
            }
        }

        bool NearFocus(Node n, double margin)
        {
            return Vec3d.Distance(n.Centre, _focus) - TileRules.Reach(SurfaceRadius, n.Level) < margin;
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
            n.Shown = false;
            n.Solid = false;
            n.Children = null;
            n.Scatter = null;
            n.ScatterPositions = null;
            n.ScatterOut = false;
            n.SeenTick = 0;
            Vec3d dir = TerrainMesher.TileCentre(face, level, x, y);
            n.Centre = dir * (SurfaceRadius + Terrain.CoarseHeight(dir));

            PlanetTerrain terrain = Terrain;
            // Cave mouths are only cut into the two finest levels: coarser ground is too far away to see them.
            bool cut = level >= MaxLevel - 1;
            bool scatter = level == Mathf.Max(0, MaxLevel - 1);
            bool linear = _linear;
            n.Job = PlanetJobs.Run(() => BuildTile(terrain, face, level, x, y, cut, scatter, linear), build => OnTile(n, build), () => TilePriority(n));
            return n;
        }

        float TilePriority(Node n)
        {
            // Nearest first; among equals, coarser first, so there is always something to show.
            return (float)Vec3d.Distance(n.Centre, _viewer) + n.Level * 2f - 2e6f;
        }

        static TileBuild BuildTile(PlanetTerrain terrain, int face, int level, int x, int y, bool cut, bool scatter, bool linear)
        {
            var build = new TileBuild { Chunk = TerrainMesher.Build(terrain, face, level, x, y, cut) };
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
                n.Mesh = new Mesh { name = "Ground" };
                n.Go.AddComponent<MeshFilter>().sharedMesh = n.Mesh;
                var renderer = n.Go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _ground;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            n.Go.SetActive(false);

            int count = chunk.VertexCount;
            VertexBuffer.Clear();
            NormalBuffer.Clear();
            DirectionBuffer.Clear();
            ColorBuffer.Clear();
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                var p = new Vector3(chunk.Positions[o], chunk.Positions[o + 1], chunk.Positions[o + 2]);
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

            if (build.Scatter != null && build.Scatter.Count > 0)
            {
                n.Scatter = build.Scatter;
                n.ScatterPositions = build.ScatterPositions;
                _scatterNodes.Add(n);
            }
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
            for (int i = 0; i < Terrain.Caves.Count; i++)
            {
                Cave cave = Terrain.Caves[i];
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
            for (int c = 0; c < Terrain.Caves.Count; c++)
            {
                Cave cave = Terrain.Caves[c];
                // The mouths are funnels wider than the tunnel itself.
                for (int m = 0; m < cave.Mouths.Length; m++)
                {
                    Vec3d mouth = cave.Mouths[m] * point.Magnitude;
                    double reach = cave.MouthRadius + CaveMesher.CollarReach + margin;
                    if ((mouth - point).SqrMagnitude < reach * reach) return true;
                }
                for (int k = 0; k < cave.Centres.Length; k++)
                {
                    double reach = cave.Radii[k] * 1.2 + margin;
                    if ((cave.Centres[k] - point).SqrMagnitude < reach * reach) return true;
                }
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
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null) Destroy(_owned[i]);
            }
        }
    }
}
