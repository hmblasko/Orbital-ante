using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// The starter ship: a small lander modelled on the Apollo Lunar Module. This builds it (hull, cabin,
    /// windows, legs, colliders), and turns the rules in <see cref="ShipModel"/>, <see cref="HatchLift"/> and
    /// <see cref="SeatBelt"/> into things you can see, hear and bump into.
    ///
    /// Everything on the ship is placed in the ship's own space (see <see cref="ShipLayout"/>), so the cabin is
    /// the right way up for the person inside whatever the ship is doing.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class Ship : MonoBehaviour
    {
        public ShipConfig Config { get; private set; }
        public Rigidbody Body { get; private set; }
        public GravityBody Gravity { get; private set; }
        /// <summary>The flying controls (thrust, steering, match velocity). Only live while someone is buckled in.</summary>
        public TestCraft Flight { get; private set; }
        public ShipModel Model { get; private set; }
        public HatchLift Hatch { get; private set; }
        public SeatBelt Belt { get; private set; }
        public ShipAudio Sounds { get; private set; }
        /// <summary>True once the cabin is wearing its properly baked light (false while a stand-in is showing).</summary>
        public bool LightBaked { get; private set; }

        /// <summary>A part was hurt: its index and what happened.</summary>
        public event Action<int, DamageResult> PartDamaged;
        public event Action<int> PartRepaired;

        GameObject[] _partRoot;
        Vector3[] _pivot;
        List<Vector3>[] _anchors;
        Renderer[][] _partRenderers;
        MaterialPropertyBlock _block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Windows
        sealed class Pane
        {
            public WindowSpec Spec;
            public int Part;
            public GameObject Glass;
            public Collider[] Colliders;
            public Material Material;
            public MeshFilter Cracks;
            public readonly List<P3> Hits = new List<P3>();
            public int ShownStage = -1;
            public int ShownHits = -1;
        }
        Pane[] _panes;

        // Hatch
        Transform _doorLeft, _doorRight, _lever;
        Renderer _ring, _beam;
        Material _ringMaterial;
        bool _wasShut = true;

        // Baked light
        List<ShipPiece> _pieces;
        Mesh[] _pieceMeshes;
        PlanetJobs.Handle _bakeJob;

        AudioSource _voice, _thump, _hum, _tick;
        float _nextSpark;
        readonly RaycastHit[] _rayHits = new RaycastHit[8];
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

        // ------------------------------------------------------------------ building

        public static Ship Create(Transform parent, ShipConfig config, TestCraftConfig flightConfig)
        {
            var go = new GameObject("Ship");
            go.transform.SetParent(parent, false);
            go.layer = Layers.Default;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(100f, config.mass);
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Planets are moved by script; only the "speculative" kind of fast-collision checking sees those.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var ship = go.AddComponent<Ship>();
            ship.Config = config;
            ship.Body = rb;
            ship.Model = new ShipModel(config.damage);
            ship.Hatch = new HatchLift { DoorSeconds = config.doorSeconds, LiftSpeed = config.liftSpeed, Reach = ReachOf(config) };
            ship.Belt = new SeatBelt { BuckleSeconds = config.buckleSeconds, UnbuckleSeconds = config.unbuckleSeconds };
            ship.Sounds = ShipAudio.Build();
            ship.Build();

            // Low and central, so it sits steadily on its legs.
            rb.centerOfMass = new Vector3(0f, -1.0f, 0f);
            ship.Gravity = go.AddComponent<GravityBody>();
            // The ground under the ship stays solid even when the pilot has walked off. The cabin floor (the
            // ship's origin) cannot honestly be this close to the ground however the ship is lying, so nearer
            // than this means it has sunk in, and it is stood back up on its feet.
            ship.Gravity.NeedsGround = true;
            ship.Gravity.SunkBelow = 0.6f;
            ship.Gravity.PutBackAt = -ShipLayout.GroundY + 0.3f;
            ship.Flight = TestCraft.Attach(go, flightConfig);
            ship.Flight.Unmanned = true;
            return ship;
        }

        static Vector3 V(P3 p) => new Vector3(p.X, p.Y, p.Z);

        void Build()
        {
            _block = new MaterialPropertyBlock();
            int count = Model.Count;
            _partRoot = new GameObject[count];
            _pivot = new Vector3[count];
            _anchors = new List<Vector3>[count];
            _partRenderers = new Renderer[count][];

            Material outside = PlanetMaterials.VertexColour(Color.white, 1f, 0f);
            Material baked = PlanetMaterials.Baked(1f);
            Material glowing = PlanetMaterials.Baked(1.6f);
            _owned.Add(baked);
            _owned.Add(glowing);
            var grip = new PhysicsMaterial("ShipHull") { bounciness = Config.bounce, dynamicFriction = Config.grip, staticFriction = Config.grip };
            _owned.Add(grip);

            // ---- cabin
            int cabin = Model.IndexOf("cabin");
            GameObject cabinGo = NewPart(cabin, new Vector3(0f, 1.2f, 0f));
            AddVisual(cabinGo, ShipShell.Hull(), "Hull", outside, HullRoundDegrees);
            AddVisual(cabinGo, ShipProps.HullDetails(), "HullDetails", outside);
            AddConvex(cabinGo, ShipShell.CabinCollision(), "CabinCollision", grip);
            _anchors[cabin].Add(new Vector3(0f, 1.25f, -2.3f));
            _anchors[cabin].Add(new Vector3(0f, 0.7f, 2.75f));
            _anchors[cabin].Add(new Vector3(0f, 0.7f, -2.75f));
            _anchors[cabin].Add(new Vector3(2.75f, 0.5f, 0f));
            _anchors[cabin].Add(new Vector3(-2.75f, 0.5f, 0f));

            // ---- base
            int baseIndex = Model.IndexOf("base");
            GameObject baseGo = NewPart(baseIndex, new Vector3(0f, -0.9f, 0f));
            AddFitting(baseGo, "base", ShipProps.Base, "Base", outside);
            AddConvex(baseGo, ShipShell.BaseCollision(), "BaseCollision", grip);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                _anchors[baseIndex].Add(new Vector3(Mathf.Sin(a) * ShipLayout.BaseInradius, -0.9f, Mathf.Cos(a) * ShipLayout.BaseInradius));
            }

            // ---- legs
            for (int i = 0; i < ShipLayout.Legs; i++)
            {
                int part = Model.Find(PartKind.Leg, i);
                Vector3 root = V(ShipLayout.LegRoot(i)), foot = V(ShipLayout.Footpad(i));
                GameObject leg = NewPart(part, root);
                int legIndex = i;
                AddFitting(leg, "leg" + i, () => ShipProps.Leg(legIndex), "Leg", outside);
                AddCapsule(leg, root, foot + Vector3.up * 0.35f, 0.14f, grip);
                var pad = leg.AddComponent<SphereCollider>();
                pad.center = foot + Vector3.up * ShipLayout.FootpadRadius;
                pad.radius = ShipLayout.FootpadRadius;
                pad.sharedMaterial = grip;
                _anchors[part].Add(Vector3.Lerp(root, foot, 0.55f));
            }

            // ---- engines
            int engines = Model.Find(PartKind.Engine);
            GameObject enginesGo = NewPart(engines, new Vector3(0f, ShipLayout.BellyY, 0f));
            AddFitting(enginesGo, "engines", ShipProps.Engines, "Engines", outside);
            for (int i = 0; i < 4; i++)
            {
                Vector3 top = V(ShipLayout.Engine(i));
                Vector3 exit = new Vector3(top.x, ShipLayout.EngineExitY, top.z);
                AddCapsule(enginesGo, top, exit, ShipLayout.EngineExitRadius * 0.8f, grip);
                _anchors[engines].Add(exit);
            }

            // ---- thrusters
            for (int i = 0; i < 4; i++)
            {
                int part = Model.Find(PartKind.Thruster, i);
                Vector3 at = V(ShipLayout.Thruster(i));
                GameObject go = NewPart(part, at * 0.88f);
                int thrusterIndex = i;
                AddFitting(go, "thruster" + i, () => ShipProps.Thruster(thrusterIndex), "Thruster", outside);
                var ball = go.AddComponent<SphereCollider>();
                ball.center = at;
                ball.radius = 0.3f;
                ball.sharedMaterial = grip;
                _anchors[part].Add(at);
            }

            // ---- dish
            int dish = Model.Find(PartKind.Dish);
            GameObject dishGo = NewPart(dish, V(ShipLayout.DishMastFoot));
            AddFitting(dishGo, "dish", ShipProps.Dish, "Dish", outside);
            var dishBall = dishGo.AddComponent<SphereCollider>();
            dishBall.center = V(ShipLayout.DishCentre);
            dishBall.radius = ShipLayout.DishRadius * 0.9f;
            dishBall.sharedMaterial = grip;
            _anchors[dish].Add(V(ShipLayout.DishCentre));

            // ---- windows
            _panes = new Pane[ShipLayout.Windows.Length];
            Material crackMaterial = Mat.Unlit(new Color(0.93f, 0.97f, 1f));
            for (int i = 0; i < ShipLayout.Windows.Length; i++)
            {
                WindowSpec spec = ShipLayout.Windows[i];
                int part = Model.IndexOf(spec.Id);
                ShipLayout.WindowCentre(spec, out P3 centre, out _);
                GameObject go = NewPart(part, V(centre));

                var pane = new Pane { Spec = spec, Part = part };
                pane.Material = ShipMeshes.Glass(Config.glassTint);
                _owned.Add(pane.Material);
                pane.Glass = AddVisual(go, ShipShell.Glass(spec), "Glass", pane.Material);

                // A wide window bends round the cabin, so its glass is covered by a row of boxes, not one.
                pane.Colliders = new Collider[ShipShell.GlassBoxCount(spec)];
                for (int c = 0; c < pane.Colliders.Length; c++)
                {
                    ShipShell.GlassBox(spec, c, out P3 boxCentre, out _, out P3 up, out P3 outward, out P3 half);
                    var boxGo = new GameObject("GlassBox");
                    boxGo.transform.SetParent(go.transform, false);
                    boxGo.transform.localPosition = V(boxCentre);
                    boxGo.transform.localRotation = Quaternion.LookRotation(V(outward), V(up));
                    boxGo.layer = Layers.Default;
                    var box = boxGo.AddComponent<BoxCollider>();
                    box.size = new Vector3(half.X * 2f, half.Y * 2f, half.Z * 2f);
                    pane.Colliders[c] = box;
                }

                var cracks = new GameObject("Cracks");
                cracks.transform.SetParent(go.transform, false);
                pane.Cracks = cracks.AddComponent<MeshFilter>();
                var crackRenderer = cracks.AddComponent<MeshRenderer>();
                crackRenderer.sharedMaterial = crackMaterial;
                crackRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cracks.SetActive(false);

                _panes[i] = pane;
                _anchors[part].Add(V(centre));
                // The glass is not tinted when the part is hurt; the cracks say it all.
                _partRenderers[part] = new Renderer[0];
            }

            // ---- the cabin's inside, with its baked light
            _pieces = ShipAssembly.InteriorPieces();
            List<float[]> light = LoadBakedLight();
            LightBaked = light != null;
            if (light == null) light = ShipAssembly.FlatLight(_pieces);
            _pieceMeshes = new Mesh[_pieces.Count];
            var inside = new GameObject("Cabin").transform;
            inside.SetParent(transform, false);

            int hatch = Model.Find(PartKind.Hatch);
            GameObject hatchGo = NewPart(hatch, Vector3.zero);
            _partRenderers[hatch] = new Renderer[0];
            _anchors[hatch].Add(V(ShipLayout.HatchLever));
            _anchors[hatch].Add(V(ShipLayout.OutsideHatchButton));

            for (int i = 0; i < _pieces.Count; i++)
            {
                ShipPiece piece = _pieces[i];
                Transform home = inside;
                if (piece.Name.StartsWith("HatchDoor")) home = hatchGo.transform;
                var go = new GameObject(piece.Name);
                go.transform.SetParent(home, false);
                go.transform.localPosition = V(piece.Offset);
                Mesh mesh = ShipMeshes.ToMesh(piece.Mesh, piece.Name, light[i]);
                _owned.Add(mesh);
                _pieceMeshes[i] = mesh;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = baked;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (piece.Name == "HatchDoor0") _doorLeft = go.transform;
                else if (piece.Name == "HatchDoor1") _doorRight = go.transform;
                else if (piece.Name == "Lever") _lever = go.transform;
            }

            // ---- the blue ring and the lift's beam
            var ringGo = new GameObject("EntryRing");
            ringGo.transform.SetParent(transform, false);
            Mesh ringMesh = ShipMeshes.ToMesh(ShipProps.Ring(), "EntryRing");
            _owned.Add(ringMesh);
            ringGo.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            _ringMaterial = new Material(glowing) { name = "EntryRing" };
            _owned.Add(_ringMaterial);
            _ring = ringGo.AddComponent<MeshRenderer>();
            _ring.sharedMaterial = _ringMaterial;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var beam = new MeshBuilder();
            beam.Color("#39B8FF");
            float bottom = ShipLayout.BellyY - ReachOf(Config);
            beam.Lathe(P3.Zero, new[] { ShipLayout.ShaftRadius * 0.96f, ShipLayout.ShaftRadius * 0.96f }, new[] { bottom, ShipLayout.BellyY }, 28);
            var beamGo = new GameObject("LiftBeam");
            beamGo.transform.SetParent(transform, false);
            Mesh beamMesh = ShipMeshes.ToMesh(beam.Finish(), "LiftBeam");
            _owned.Add(beamMesh);
            beamGo.AddComponent<MeshFilter>().sharedMesh = beamMesh;
            Material beamMaterial = ShipMeshes.Glass(new Color(0.22f, 0.72f, 1f, 0.07f));
            _owned.Add(beamMaterial);
            _beam = beamGo.AddComponent<MeshRenderer>();
            _beam.sharedMaterial = beamMaterial;
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.enabled = false;

            // ---- sound
            // Its own source, because its pitch climbs as a repair goes and must not bend other sounds.
            _tick = gameObject.AddComponent<AudioSource>();
            _tick.playOnAwake = false;
            _tick.spatialBlend = 0f;
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 1f;
            _voice.rolloffMode = AudioRolloffMode.Linear;
            _voice.minDistance = 5f;
            _voice.maxDistance = 70f;
            // Knocks are pitched by how hard they are, so they get their own source and bend nothing else.
            _thump = gameObject.AddComponent<AudioSource>();
            _thump.playOnAwake = false;
            _thump.spatialBlend = 1f;
            _thump.rolloffMode = AudioRolloffMode.Linear;
            _thump.minDistance = 5f;
            _thump.maxDistance = 70f;
            _hum = gameObject.AddComponent<AudioSource>();
            _hum.playOnAwake = false;
            _hum.clip = Sounds.LiftHum;
            _hum.loop = true;
            _hum.spatialBlend = 1f;
            _hum.rolloffMode = AudioRolloffMode.Linear;
            _hum.minDistance = 4f;
            _hum.maxDistance = 40f;
            _hum.volume = 0f;
            _scrape = gameObject.AddComponent<AudioSource>();
            _scrape.playOnAwake = false;
            _scrape.clip = Sounds.Scrape;
            _scrape.loop = true;
            _scrape.spatialBlend = 1f;
            _scrape.rolloffMode = AudioRolloffMode.Linear;
            _scrape.minDistance = 5f;
            _scrape.maxDistance = 70f;
            _scrape.volume = 0f;

            for (int i = 0; i < count; i++) RefreshPart(i);
            PoseHatch();
        }

        GameObject NewPart(int index, Vector3 pivot)
        {
            var go = new GameObject(Model.Parts[index].Id);
            go.transform.SetParent(transform, false);
            go.layer = Layers.Default;
            var part = go.AddComponent<ShipPart>();
            part.Ship = this;
            part.Index = index;
            _partRoot[index] = go;
            _pivot[index] = pivot;
            _anchors[index] = new List<Vector3>();
            return go;
        }

        /// <summary>The model file holding the Blender-made outside fittings (tools/blender/build_ship.py).</summary>
        public const string FittingsModel = "Lander";
        /// <summary>The cabin's panelled dome shades round; edges sharper than this stay crisp.</summary>
        const float HullRoundDegrees = 42f;

        /// <summary>
        /// An outside fitting: the Blender-made piece if the model file has it, otherwise the shape the game
        /// builds itself. Both are in ship space and fit the same colliders.
        /// </summary>
        GameObject AddFitting(GameObject parent, string piece, System.Func<ShipMesh> standIn, string name, Material material)
        {
            Mesh shared = OrbitalAnte.Look.ModelLibrary.PartMesh(FittingsModel, piece);
            if (shared == null) return AddVisual(parent, standIn(), name, material);
            // The library owns this mesh (wreckage and a restarted run use it too), so it is not added to _owned.
            return AddRenderer(parent, shared, name, material);
        }

        GameObject AddVisual(GameObject parent, ShipMesh data, string name, Material material, float roundDegrees = 0f)
        {
            Mesh mesh = ShipMeshes.ToMesh(data, name, null, roundDegrees);
            _owned.Add(mesh);
            return AddRenderer(parent, mesh, name, material);
        }

        GameObject AddRenderer(GameObject parent, Mesh mesh, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.layer = Layers.Default;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var part = parent.GetComponent<ShipPart>();
            if (part != null)
            {
                Renderer[] old = _partRenderers[part.Index] ?? new Renderer[0];
                var grown = new Renderer[old.Length + 1];
                old.CopyTo(grown, 0);
                grown[old.Length] = renderer;
                _partRenderers[part.Index] = grown;
            }
            return go;
        }

        void AddConvex(GameObject parent, ShipMesh data, string name, PhysicsMaterial material)
        {
            Mesh mesh = ShipMeshes.ToMesh(data, name);
            _owned.Add(mesh);
            var collider = parent.AddComponent<MeshCollider>();
            collider.convex = true;
            collider.sharedMesh = mesh;
            collider.sharedMaterial = material;
        }

        static void AddCapsule(GameObject parent, Vector3 from, Vector3 to, float radius, PhysicsMaterial material)
        {
            var go = new GameObject("Strut");
            go.transform.SetParent(parent.transform, false);
            go.layer = Layers.Default;
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = radius;
            capsule.height = (to - from).magnitude + radius * 2f;
            capsule.sharedMaterial = material;
        }

        // ------------------------------------------------------------------ baked light

        List<float[]> LoadBakedLight()
        {
            var file = Resources.Load<TextAsset>(ShipAssembly.BakeResource);
            return file != null ? ShipAssembly.Unpack(file.bytes, _pieces) : null;
        }

        /// <summary>
        /// If the baked lighting file was missing or did not fit this cabin, works the light out now on a
        /// background thread and swaps it in when ready. Until then the cabin shows a flat stand-in.
        /// </summary>
        public void BakeLightIfNeeded()
        {
            if (LightBaked || _bakeJob != null) return;
            Debug.LogWarning("Orbital Ante: the ship's baked lighting file is missing or out of date, so the cabin's light is being worked out now. " +
                             "It looks flat for a few seconds, then correct. Tell Claude so the file can be rebuilt.");
            List<ShipPiece> pieces = _pieces;
            var settings = new LightBake.Settings { Paths = Mathf.Clamp(Config.fallbackBakePaths, 8, 512), Threads = 1 };
            _bakeJob = PlanetJobs.Run(() => ShipAssembly.Bake(pieces, settings), ApplyBakedLight, () => -5e5f, true);
        }

        void ApplyBakedLight(List<float[]> light)
        {
            if (this == null || light == null) return;
            for (int i = 0; i < _pieceMeshes.Length; i++) ShipMeshes.SetBaked(_pieceMeshes[i], light[i]);
            LightBaked = true;
        }

        // ------------------------------------------------------------------ damage

        /// <summary>A bullet (or anything else that says how hard it hits) struck a part.</summary>
        public HitOutcome HitPart(int index, in HitInfo hit)
        {
            // A shot fired in the cabin that misses the glass has hit the cabin wall, whatever the physics says:
            // the hull's collision shape is solid, so from inside a ray leaves it without touching it and would
            // otherwise strike your own legs or dish.
            if (CrewAboard && Model.Parts[index].Kind != PartKind.Window) index = Model.IndexOf("cabin");
            DamageResult result = Model.Damage(index, hit.Damage);
            ShowDamage(index, result, hit.Point, hit.Normal, bullet: true);
            return new HitOutcome
            {
                Hit = true,
                Severed = result.FellOff,
                Juice = result.FellOff ? 0.7f : result.Broke ? 0.5f : 0f,
                Label = Model.Parts[index].Name,
            };
        }

        /// <summary>Hurts a part and shows it. For crashes and tests.</summary>
        public DamageResult DamagePart(int index, float amount, Vector3 worldPoint)
        {
            DamageResult result = Model.Damage(index, amount);
            Vector3 away = (worldPoint - transform.position).normalized;
            ShowDamage(index, result, worldPoint, away, bullet: false);
            return result;
        }

        void ShowDamage(int index, DamageResult result, Vector3 point, Vector3 normal, bool bullet)
        {
            if (result.Dealt <= 0f) return;
            PartSpec spec = Model.Parts[index];
            WeaponFx fx = WeaponFx.Instance;

            if (spec.Kind == PartKind.Window)
            {
                Pane pane = _panes[spec.Index];
                if (pane.Hits.Count < 6) pane.Hits.Add(ToShip(point));
                if (result.Broke)
                {
                    Play(Sounds.GlassShatter, 1f);
                    if (fx != null) fx.Sparks(point, normal, 26);
                }
                else
                {
                    Play(Sounds.GlassCrack, 0.8f);
                    if (fx != null) fx.Sparks(point, normal, 4);
                }
            }
            else
            {
                if (fx != null) fx.Sparks(point, normal, result.Broke || result.FellOff ? 22 : bullet ? 6 : 12);
                if (result.FellOff) Play(Sounds.TearOff, 1f);
                else if (result.Broke) Play(Sounds.Crunch, 1f);
                else if (bullet && fx != null) fx.Audio.PlayClang(point);
            }

            if (result.FellOff) Detach(index);
            RefreshPart(index);
            PartDamaged?.Invoke(index, result);
        }

        P3 ToShip(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return new P3(local.x, local.y, local.z);
        }

        /// <summary>Sets how a part looks to match its state: sound, scuffed and dark, hanging broken, or gone.</summary>
        void RefreshPart(int index)
        {
            GameObject go = _partRoot[index];
            if (go == null) return;
            PartSpec spec = Model.Parts[index];
            PartState state = Model.State(index);
            if (state == PartState.Lost) return;

            if (spec.Kind == PartKind.Window)
            {
                Pane pane = _panes[spec.Index];
                int stage = Model.CrackStage(index);
                bool gone = stage >= 4;
                pane.Glass.SetActive(!gone);
                // With the glass gone, shots and everything else pass straight through the hole.
                for (int c = 0; c < pane.Colliders.Length; c++) pane.Colliders[c].enabled = !gone;
                ShipMeshes.SetFrost(pane.Material, stage >= 3 ? 1f : stage == 2 ? 0.5f : stage == 1 ? 0.15f : 0f);
                if (stage == 0) pane.Hits.Clear();
                // Only when what would be drawn has changed: the crack lines are thousands of triangles.
                if (stage != pane.ShownStage || pane.Hits.Count != pane.ShownHits)
                {
                    pane.ShownStage = stage;
                    pane.ShownHits = pane.Hits.Count;
                    Mesh old = pane.Cracks.sharedMesh;
                    if (old != null) Destroy(old);
                    pane.Cracks.sharedMesh = null;
                    bool show = stage > 0 && stage < 4 && pane.Hits.Count > 0;
                    if (show)
                    {
                        ShipMesh cracks = ShipShell.Cracks(pane.Spec, pane.Hits, stage);
                        show = cracks.TriangleCount > 0;
                        if (show) pane.Cracks.sharedMesh = ShipMeshes.ToMesh(cracks, "Cracks");
                    }
                    pane.Cracks.gameObject.SetActive(show);
                }
                return;
            }

            float shade = state == PartState.Sound ? 1f : state == PartState.Damaged ? 0.78f : 0.5f;
            Renderer[] renderers = _partRenderers[index];
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (shade >= 0.999f) { renderers[i].SetPropertyBlock(null); continue; }
                    _block.SetColor(BaseColorId, new Color(shade, shade * 0.97f, shade * 0.94f, 1f));
                    renderers[i].SetPropertyBlock(_block);
                }
            }

            // Broken things hang at an angle, so you can see what is wrong from across the landing site.
            Quaternion sag = Quaternion.identity;
            if (state == PartState.Broken && spec.CanFallOff)
            {
                float degrees = spec.Kind == PartKind.Dish ? 28f : spec.Kind == PartKind.Thruster ? 22f : spec.Kind == PartKind.Leg ? 7f : 4f;
                Vector3 outward = new Vector3(_pivot[index].x, 0f, _pivot[index].z);
                Vector3 axis = outward.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, outward.normalized) : Vector3.right;
                sag = Quaternion.AngleAxis(degrees, axis);
            }
            go.transform.localRotation = sag;
            go.transform.localPosition = _pivot[index] - sag * _pivot[index];
        }

        /// <summary>Tears a part off for good. It becomes loose wreckage with its own momentum.</summary>
        void Detach(int index)
        {
            GameObject go = _partRoot[index];
            if (go == null) return;
            _partRoot[index] = null;
            var part = go.GetComponent<ShipPart>();
            if (part != null) part.Ship = null;

            Vector3 velocity = Body.GetPointVelocity(go.transform.TransformPoint(_pivot[index]));
            Vector3 outward = transform.TransformDirection(_pivot[index].sqrMagnitude > 0.01f ? _pivot[index].normalized : Vector3.down);
            go.transform.SetParent(transform.parent, true);
            Layers.SetLayerRecursively(go, Layers.Gib);
            // It starts touching the ship; without this the physics would fling it away like a cannon shot.
            Collider[] loose = go.GetComponentsInChildren<Collider>(true);
            Collider[] mine = GetComponentsInChildren<Collider>(true);
            for (int a = 0; a < loose.Length; a++)
            {
                for (int b = 0; b < mine.Length; b++) Physics.IgnoreCollision(loose[a], mine[b], true);
            }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 60f;
            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.linearVelocity = velocity + outward * 2.5f;
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2.5f;
            go.AddComponent<GravityBody>();
            // Left far behind, it is tidied away: the ground is only solid near you, so it would fall for ever.
            go.AddComponent<Wreckage>().Home = this;
        }

        float _lastCrash = -10f, _lastCrashSpeed, _lastGroan = -10f, _slide, _stopHeard = -100f;
        AudioSource _scrape;

        /// <summary>How fast the hull is sliding over what it touches, metres per second. Drives the scrape.</summary>
        public float SlideSpeed => _slide;

        // The ship's velocity at the end of the last physics step, and the part of this step's change of
        // velocity that the things it touched account for.
        Vector3 _velocityBefore, _felt;

        void FixedUpdate()
        {
            // Runs before the physics step, so the collision messages of the step before have all arrived.
            // The blow is told here, once, after every surface touched in that step has been counted.
            float knock = _felt.magnitude - 0.5f;
            if (knock > 0f)
            {
                if (knock > HardestKnock) HardestKnock = knock;
                // The surfaces pushed the ship this way, so the blow came from the other way.
                if (Crew != null) Crew.Knock(knock, transform.InverseTransformDirection(-_felt.normalized));
            }
            _felt = Vector3.zero;
            if (Body != null) _velocityBefore = Body.linearVelocity;
        }

        /// <summary>
        /// What the person inside feels: how much the ship's own velocity changed across this physics step,
        /// measured straight into each surface it touched. Two touches on the same surface count once; a
        /// floor and a wall in the same step both count. Told once per step (see FixedUpdate).
        /// A ship resting on spinning ground changes velocity a little every step; that is not a knock.
        /// </summary>
        void FeelKnock(Collision collision)
        {
            if (Body == null || collision.contactCount == 0) return;
            Vector3 normal = collision.GetContact(0).normal;
            // Only what the surfaces counted so far have not already explained.
            Vector3 left = Body.linearVelocity - _velocityBefore - _felt;
            _felt += normal * Vector3.Dot(left, normal);
        }

        void OnCollisionStay(Collision collision)
        {
            if (collision.contactCount == 0) return;
            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic && other.mass < 300f) return;
            FeelKnock(collision);
            Vector3 normal = collision.GetContact(0).normal;
            Vector3 along = collision.relativeVelocity - normal * Vector3.Dot(collision.relativeVelocity, normal);
            float speed = along.magnitude;
            if (speed > _slide) _slide = speed;
        }

        /// <summary>The sounds that follow from flying, not from a single event: the scrape of a skid, and the autopilot letting go.</summary>
        void FlightSounds(float dt)
        {
            if (_scrape != null)
            {
                // Metal dragged over ground: louder and a little higher the faster it slides. Fades in a moment.
                float want = Mathf.Clamp01((_slide - 1.5f) / 16f);
                _scrape.volume = Mathf.MoveTowards(_scrape.volume, want * Config.volume, dt * 4f);
                _scrape.pitch = 0.8f + 0.5f * want;
                if (_scrape.volume > 0.001f && !_scrape.isPlaying) _scrape.Play();
                else if (_scrape.volume <= 0.001f && _scrape.isPlaying) _scrape.Stop();
                _slide = Mathf.MoveTowards(_slide, 0f, dt * 60f);
            }
            if (Flight != null && Flight.LastStopTime != _stopHeard)
            {
                _stopHeard = Flight.LastStopTime;
                // Letting go on its own in mid-flight is heard as well as read; finishing is a quiet click.
                if (OrbitalAnte.Navigation.Autopilot.IsAlarm(Flight.LastStop)) Play(Sounds.Deny, 1f);
                else if (Flight.LastStop == OrbitalAnte.Navigation.PilotStop.Arrived) Play(Sounds.Lever, 0.8f);
            }
        }
        /// <summary>The player, when they belong to this ship. Told about every knock.</summary>
        public Astronaut Crew { get; set; }
        /// <summary>The hardest knock so far (change in the ship's velocity, metres per second), for tests and the debug line.</summary>
        public float HardestKnock { get; private set; }

        void OnCollisionEnter(Collision collision)
        {
            if (Model == null || collision.contactCount == 0) return;
            // Small loose things (wreckage, a person) do not dent a spacecraft.
            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic && other.mass < 300f) return;

            // Only the speed straight into the surface counts. Sliding along the ground is not a crash.
            ContactPoint contact = collision.GetContact(0);
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            // The same knock often arrives as several touches (four footpads, two pieces of ground). Count it once.
            if (Time.time - _lastCrash < 0.2f && speed <= _lastCrashSpeed) speed = 0f;
            FeelKnock(collision);

            float damage = Model.ImpactDamage(speed);
            if (damage > 0f) { _lastCrash = Time.time; _lastCrashSpeed = speed; }
            // The sound of a knock: a dull thump through the hull, deeper and louder the harder it is, and the
            // structure groaning afterwards if it was a real blow. No stings, no music.
            float loud = OrbitalAnte.Navigation.CrashModel.Loudness(speed);
            if (loud > 0.02f) Play(Sounds.Thud, damage > 0f ? 1f : loud * 0.6f, OrbitalAnte.Navigation.CrashModel.Pitch(speed));
            if (damage <= 0f) return;
            // One groan at a time: a tumbling crash must not pile them up.
            if (speed > Model.Rules.safeImpactSpeed * 2f && Time.time - _lastGroan > 1f)
            {
                _lastGroan = Time.time;
                Play(Sounds.Groan, Mathf.Clamp01(speed / 25f), 1f);
            }
            if (Flight != null && Flight.PilotOn) Flight.StopPilot(OrbitalAnte.Navigation.PilotStop.Knocked);

            ShipPart struck = contact.thisCollider != null ? contact.thisCollider.GetComponentInParent<ShipPart>() : null;
            int index = struck != null && struck.Ship == this ? struck.Index : Model.IndexOf("base");
            PartSpec spec = Model.Parts[index];
            DamagePart(index, damage, contact.point);
            if (spec.Kind != PartKind.Hull)
            {
                // What sticks out takes the blow, but some of it always reaches the body of the ship.
                bool high = transform.InverseTransformPoint(contact.point).y > 0f;
                DamagePart(Model.IndexOf(high ? "cabin" : "base"), damage * 0.35f, contact.point);
            }
        }

        // ------------------------------------------------------------------ repair

        /// <summary>
        /// The damaged part someone at <paramref name="eye"/> is looking at and close enough to mend, or -1.
        /// </summary>
        /// <param name="inside">
        /// True when the mender is standing in the cabin. From there you can reach the windows, the hatch and the
        /// cabin wall, and nothing else: the legs, engines, thrusters and dish are on the other side of the hull.
        /// </param>
        public int FindRepairTarget(Vector3 eye, Vector3 forward, bool inside = false)
        {
            int best = -1;
            float reach = inside ? Config.repairReach : Config.spacewalkRepairReach;
            float bestAngle = Config.repairAim;
            for (int i = 0; i < Model.Count; i++)
            {
                if (!Model.NeedsRepair(i)) continue;
                if (inside)
                {
                    PartSpec spec = Model.Parts[i];
                    if (spec.Kind != PartKind.Window && spec.Kind != PartKind.Hatch && spec.Id != "cabin") continue;
                }
                List<Vector3> anchors = _anchors[i];
                for (int k = 0; k < anchors.Count; k++)
                {
                    Vector3 to = transform.TransformPoint(anchors[k]) - eye;
                    float distance = to.magnitude;
                    if (distance > reach) continue;
                    // Right up against it, anywhere roughly in front counts.
                    float angle = distance < 0.4f ? 0f : Vector3.Angle(forward, to);
                    if (angle < bestAngle) { bestAngle = angle; best = i; }
                }
            }
            return best;
        }

        /// <summary>Where a part is, for pointing the player at it. World space.</summary>
        public Vector3 PartPosition(int index) => transform.TransformPoint(_anchors[index].Count > 0 ? _anchors[index][0] : _pivot[index]);

        float _lastTick;

        /// <summary>Mends a part for this frame. Returns true at the moment it is finished.</summary>
        public bool Repair(int index, float dt)
        {
            if (!Model.NeedsRepair(index)) return false;
            if (Time.time - _lastTick > 0.16f)
            {
                _lastTick = Time.time;
                _tick.pitch = 0.9f + Model.RepairProgress(index) * 0.5f;
                _tick.PlayOneShot(Sounds.RepairTick, 0.5f * Config.volume);
            }
            if (!Model.Repair(index, dt)) return false;
            Play(Sounds.RepairDone, 1f);
            RefreshPart(index);
            PartRepaired?.Invoke(index);
            return true;
        }

        // ------------------------------------------------------------------ hatch and lift

        /// <summary>Opens or closes the hatch. Returns false, with a refusing buzz, if it cannot move.</summary>
        public bool ToggleHatch()
        {
            if (!Hatch.Toggle(Model.HatchWorks))
            {
                Play(Sounds.Deny, 0.8f);
                return false;
            }
            Play(Sounds.Lever, 0.8f);
            Play(Sounds.HatchMove, 0.9f);
            return true;
        }

        /// <summary>
        /// How far down the lift can set someone down: the ground under the belly in ship space, or NaN when
        /// there is nothing within its reach to stand on.
        /// </summary>
        /// <summary>
        /// How far below the belly the lift reaches: the dial, but never less than the ground under the
        /// footpads plus a margin, so an old config asset cannot leave the lift short of the ground.
        /// </summary>
        public static float ReachOf(ShipConfig config)
        {
            float needed = ShipLayout.BellyClearance + 1.5f;
            return config == null ? needed : Mathf.Max(config.liftReach, needed);
        }

        public float FindLiftBottom()
        {
            Vector3 from = transform.TransformPoint(new Vector3(0f, ShipLayout.BellyY, 0f));
            int hits = Physics.RaycastNonAlloc(from, -transform.up, _rayHits, ReachOf(Config), Layers.WorldMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                if (_rayHits[i].rigidbody == Body) continue;
                if (_rayHits[i].distance < nearest) nearest = _rayHits[i].distance;
            }
            return nearest == float.MaxValue ? float.NaN : ShipLayout.BellyY - nearest;
        }

        void PoseHatch()
        {
            float open = Hatch.Openness;
            float eased = open * open * (3f - 2f * open);
            // The doors drop a little, then slide apart under the floor.
            float drop = Mathf.Clamp01(open * 4f) * 0.075f;
            float slide = eased * (ShipLayout.ShaftRadius + 0.06f);
            if (_doorLeft != null) _doorLeft.localPosition = new Vector3(-slide, -drop, 0f);
            if (_doorRight != null) _doorRight.localPosition = new Vector3(slide, -drop, 0f);
            if (_lever != null)
            {
                float lean = Mathf.Lerp(-32f, 32f, Hatch.WantOpen ? 1f : 0f);
                Quaternion target = Quaternion.Euler(lean, 0f, 0f);
                _lever.localRotation = Quaternion.RotateTowards(_lever.localRotation, target, 400f * Time.deltaTime);
            }
            if (_ringMaterial != null)
            {
                // The ring is always lit, so you can find the way in; brighter while the lift is on.
                float glow = Mathf.Lerp(0.9f, 1.7f, eased) * (Model.HatchWorks ? 1f : 0.35f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 9f)));
                _ringMaterial.SetFloat(GlowId, glow);
            }
            if (_beam != null) _beam.enabled = open > 0.5f;
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Model == null) return;

            Belt.Tick(dt);
            Hatch.Tick(dt, Model.HatchWorks);
            LastRideFinished = Hatch.LastFinished;
            PoseHatch();

            bool shut = Hatch.Shut;
            if (shut && !_wasShut) Play(Sounds.HatchThunk, 0.9f);
            if (!shut && _wasShut && !Hatch.WantOpen) { }
            _wasShut = shut;

            float hum = Hatch.Ride != LiftRide.None ? 0.55f : Hatch.Open ? 0.12f : 0f;
            _hum.volume = Mathf.MoveTowards(_hum.volume, hum * Config.volume, dt * 2f);
            if (_hum.volume > 0.001f && !_hum.isPlaying) _hum.Play();
            else if (_hum.volume <= 0.001f && _hum.isPlaying) _hum.Stop();

            FlightSounds(Time.deltaTime);
            if (Flight != null)
            {
                Flight.Unmanned = !Belt.Buckled || (Crew != null && Crew.Dead);
                Flight.LiftScale = Model.ThrustFactor * Mathf.Clamp01(LiftGate);
                Flight.SideScale = Model.TurnFactor;
                Flight.PushScale = Mathf.Clamp01(FuelGate);
            }

            for (int i = 0; i < Model.Count; i++) Model.RestRepair(i, RepairingPart == i ? 0f : dt);
            RepairingPart = -1;

            // Broken parts spit the odd spark, so damage keeps announcing itself.
            if (Time.time >= _nextSpark)
            {
                _nextSpark = Time.time + UnityEngine.Random.Range(0.7f, 2.2f);
                WeaponFx fx = WeaponFx.Instance;
                if (fx != null)
                {
                    int start = UnityEngine.Random.Range(0, Model.Count);
                    for (int k = 0; k < Model.Count; k++)
                    {
                        int i = (start + k) % Model.Count;
                        if (Model.State(i) != PartState.Broken || Model.Parts[i].Kind == PartKind.Window) continue;
                        Vector3 at = PartPosition(i);
                        fx.Sparks(at, (at - transform.position).normalized, 5);
                        break;
                    }
                }
            }
        }

        /// <summary>Set by whoever is mending, each frame they mend, so that part's progress does not slip back.</summary>
        public int RepairingPart { get; set; } = -1;

        /// <summary>
        /// The share of the engines' push that reaches the ship, 0 to 1. Less than 1 during the build-up before
        /// lift-off. Set each frame by the launch effects; 1 when there are none.
        /// </summary>
        public float LiftGate { get; set; } = 1f;
        /// <summary>The share of all push the fuel allows: 1 with fuel in the tank, the weak reserve when it is dry (see Cockpit/ShipSupplies). Steering is never cut.</summary>
        public float FuelGate { get; set; } = 1f;
        /// <summary>The engine effects riding on this ship, if it has them (see Launch/LaunchFx).</summary>
        public OrbitalAnte.Launch.LaunchFx Launch { get; set; }

        /// <summary>True while the player is on their feet in the cabin. Set by the astronaut.</summary>
        public bool CrewAboard
        {
            get => _crewAboard;
            set
            {
                _crewAboard = value;
                if (value) Boarded = this;
                else if (Boarded == this) Boarded = null;
            }
        }
        bool _crewAboard;

        /// <summary>The ship the player is standing in, if any (for shots fired inside it).</summary>
        public static Ship Boarded { get; private set; }

        /// <summary>
        /// For a shot fired from inside the cabin: where it meets the solid hull, unless it leaves through glass
        /// (the window's own collider decides that) or down the open hatch well. The hull's collision shape is
        /// solid, so a ray starting inside it would otherwise pass through the wall as if it were not there.
        /// </summary>
        public bool InteriorWall(Vector3 origin, Vector3 direction, out float distance, out Vector3 point, out Vector3 normal)
        {
            distance = 0f; point = origin; normal = -direction;
            if (Model == null) return false;
            Vector3 o = transform.InverseTransformPoint(origin);
            Vector3 d = transform.InverseTransformDirection(direction);
            if (!ShipLayout.CabinExit(new P3(o.x, o.y, o.z), new P3(d.x, d.y, d.z), out float along, out P3 at,
                    out ShipLayout.CabinExitKind kind, out int window)) return false;
            if (kind == ShipLayout.CabinExitKind.Window) return false;
            if (kind == ShipLayout.CabinExitKind.Shaft && !Hatch.Shut) return false;
            point = transform.TransformPoint(V(at));
            distance = Vector3.Distance(origin, point);
            Vector3 inward = kind == ShipLayout.CabinExitKind.Wall
                ? -new Vector3((float)at.X, 0f, (float)at.Z).normalized
                : Vector3.up;
            if (kind == ShipLayout.CabinExitKind.Wall && at.Y > ShipLayout.WallTop) inward = -V(at).normalized;
            normal = transform.TransformDirection(inward);
            return true;
        }

        /// <summary>The cabin's part, for hits on the inside of the hull.</summary>
        public int CabinPart => Model.IndexOf("cabin");

        /// <summary>The lift ride that finished during this frame's update, if any.</summary>
        public LiftRide LastRideFinished { get; private set; }

        void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            // Anything pitched goes to the knock source, so the ordinary voice always plays at its true pitch.
            AudioSource source = pitch != 1f ? _thump : _voice;
            if (clip == null || source == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume * Config.volume);
        }

        /// <summary>Plays one of the ship's sounds from the ship (for the seat and belt, which the player drives).</summary>
        public void PlaySound(AudioClip clip, float volume = 1f) => Play(clip, volume);

        public string DescribeForDebug()
        {
            int worst = Model.WorstPart();
            return "Ship: hatch " + (Hatch.Open ? "open" : Hatch.Shut ? "shut" : "moving") + "   belt " + Belt.State +
                   "   light " + (LightBaked ? "baked" : "stand-in") +
                   "   thrust " + Mathf.RoundToInt(Model.ThrustFactor * 100f) + "%  turning " + Mathf.RoundToInt(Model.TurnFactor * 100f) + "%" +
                   (worst >= 0 ? "   worst: " + Model.Parts[worst].Name + " " + Mathf.RoundToInt(Model.Fraction(worst) * 100f) + "%" : "   no damage");
        }

        void OnDestroy()
        {
            if (Boarded == this) Boarded = null;
            if (_bakeJob != null) _bakeJob.Cancel();
            if (_panes != null)
            {
                for (int i = 0; i < _panes.Length; i++)
                {
                    if (_panes[i] != null && _panes[i].Cracks != null && _panes[i].Cracks.sharedMesh != null) Destroy(_panes[i].Cracks.sharedMesh);
                }
            }
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null) Destroy(_owned[i]);
            }
            if (Sounds != null) Sounds.Dispose();
        }
    }

    /// <summary>A part that has come off the ship. Removed once it is far from both the ship and the player.</summary>
    public sealed class Wreckage : MonoBehaviour
    {
        const float FarAway = 400f;
        public Ship Home { get; set; }
        float _nextCheck;

        void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 2f;
            Vector3 here = transform.position;
            Camera eye = Home != null && Home.Flight != null ? Home.Flight.Eye : null;
            bool nearShip = Home != null && (Home.transform.position - here).sqrMagnitude < FarAway * FarAway;
            bool nearYou = eye != null && (eye.transform.position - here).sqrMagnitude < FarAway * FarAway;
            if (!nearShip && !nearYou) Destroy(gameObject);
        }

        void OnDestroy()
        {
            // Shapes the ship built itself belong to the ship and are freed with it; Blender-made fittings
            // belong to the model library and outlive it.
        }
    }
}
