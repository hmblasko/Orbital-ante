using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Look;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Launch
{
    /// <summary>
    /// The launch base, standing on its planet: a raised concrete pad with a blast dish, a service tower whose arm
    /// swings clear at ignition, floodlights, a water tower and a crew hut. No flags. It is a child of the planet,
    /// so it turns and travels with it.
    ///
    /// Each piece is drawn from the Blender model (Resources/Models/LaunchPad) when there is one, and from the
    /// code-built stand-in (<see cref="LaunchShapes"/>) when there is not. Both follow <see cref="LaunchLayout"/>.
    /// </summary>
    public sealed class LaunchBase : MonoBehaviour
    {
        public LaunchSpot Spot { get; private set; }
        public PlanetSurface Surface { get; private set; }
        public LaunchConfig Config { get; private set; }
        /// <summary>The ship whose engines the service arm watches. May be null.</summary>
        public LaunchFx Engines { get; set; }
        /// <summary>0 with the arm against the ship, 1 swung clear.</summary>
        public float ArmSwing { get; private set; }
        /// <summary>True when the pieces came from the Blender model rather than the stand-in shapes.</summary>
        public bool UsesModel { get; private set; }

        Transform _arm;
        Material _material, _glowMaterial;
        bool _shadeWanted;
        float _armShown = -1f;
        readonly List<Mesh> _owned = new List<Mesh>();
        readonly List<ShipMesh> _standIns = new List<ShipMesh>();
        readonly List<Mesh> _standInMeshes = new List<Mesh>();
        PlanetJobs.Handle _shadeJob;
        float _quietFor;

        /// <summary>Builds the base on a body whose ground has been worked out.</summary>
        public static LaunchBase Create(PlanetSurface surface, LaunchSpot spot, LaunchConfig config)
        {
            var go = new GameObject("LaunchBase");
            go.transform.SetParent(surface.Body.transform, false);
            go.transform.localPosition = SpaceConvert.ToVector3(spot.PadCentre);
            go.transform.localRotation = Quaternion.LookRotation(SpaceConvert.ToVector3(spot.North), SpaceConvert.ToVector3(spot.Up));
            go.layer = Layers.Default;
            var b = go.AddComponent<LaunchBase>();
            b.Spot = spot;
            b.Surface = surface;
            b.Config = config;
            b.Build();
            return b;
        }

        /// <summary>Where the ship's own origin goes when it stands on the pad, in metres from the body's centre (the body's own turning frame).</summary>
        public Vec3d ShipSpot => Spot.ToBody(0, LaunchLayout.ShipHeightOnPad + 0.03, 0);

        void Build()
        {
            float depth = (float)Spot.MoundHeight + 1.5f;

            // Shared look with every other model, plus lamps that glow where the mesh marks them.
            _material = new Material(PlanetMaterials.VertexColour(Color.white, 1f, 0f, true, ModelLibrary.PropPaintScale));
            _material.name = "OA_LaunchBase";
            if (_material.HasProperty("_VertexGlow")) _material.SetFloat("_VertexGlow", Mathf.Max(0f, Config.lampGlow));

            List<KeyValuePair<string, ShipMesh>> fixedPieces = null;
            for (int i = 0; i < LaunchLayout.Pieces.Length; i++)
            {
                string piece = LaunchLayout.Pieces[i];
                bool arm = piece == LaunchLayout.Arm;
                Mesh mesh = ModelLibrary.PartMesh(LaunchLayout.Model, piece);
                // The mound's skirt has to reach this particular ground, so the model's mound is used only if it is deep enough.
                if (mesh != null && piece == LaunchLayout.Mound && -mesh.bounds.min.y < depth - 0.01f) mesh = null;
                if (mesh != null) UsesModel = true;
                else
                {
                    ShipMesh shape;
                    if (arm) shape = LaunchShapes.Arm();
                    else
                    {
                        if (fixedPieces == null) fixedPieces = LaunchShapes.Fixed(depth);
                        shape = Find(fixedPieces, piece);
                    }
                    if (shape == null) continue;
                    mesh = ToMesh(shape, piece, null);
                    _owned.Add(mesh);
                    if (!arm)
                    {
                        _standIns.Add(shape);
                        _standInMeshes.Add(mesh);
                    }
                }

                var go = new GameObject(piece);
                go.transform.SetParent(transform, false);
                go.layer = Layers.Default;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (arm)
                {
                    _arm = go.transform;
                    _arm.localPosition = V(LaunchLayout.ArmPivot);
                }

                // The model's lit parts ride on their owner (so the arm's lamp swings with the arm).
                Mesh lit = ModelLibrary.PartMesh(LaunchLayout.Model, piece + LaunchLayout.GlowSuffix);
                if (lit == null) continue;
                if (_glowMaterial == null)
                {
                    _glowMaterial = new Material(PlanetMaterials.VertexColour(Color.white, 0f, Mathf.Max(0f, Config.lampGlow), false));
                    _glowMaterial.name = "OA_LaunchBaseGlow";
                }
                var glow = new GameObject(piece + LaunchLayout.GlowSuffix);
                glow.transform.SetParent(go.transform, false);
                glow.layer = Layers.Default;
                glow.AddComponent<MeshFilter>().sharedMesh = lit;
                var gr = glow.AddComponent<MeshRenderer>();
                gr.sharedMaterial = _glowMaterial;
                gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                gr.receiveShadows = false;
            }

            BuildColliders(depth);

            // Nothing natural grows through the concrete.
            Surface.Features.KeepClear(Spot.PadCentre, LaunchSite.Footprint);

            // Soft shade where the stand-in shapes hide each other from the sky (under the tank, between the
            // tower's legs). Worked out off the main thread; the base shows unshaded for a moment first.
            // Asked for on the first frame, not here: the start of a run waits for every job then running, and this one can wait.
            _shadeWanted = _standIns.Count > 0;
        }

        static ShipMesh Find(List<KeyValuePair<string, ShipMesh>> pieces, string name)
        {
            for (int i = 0; i < pieces.Count; i++) if (pieces[i].Key == name) return pieces[i].Value;
            return null;
        }

        static List<float[]> Shade(List<ShipMesh> shapes)
        {
            var bake = new LightBake();
            for (int i = 0; i < shapes.Count; i++) bake.Add(shapes[i]);
            return bake.SkyShade(12, 10f);
        }

        void ApplyShade(List<float[]> open)
        {
            _shadeJob = null;
            if (this == null || open == null) return;
            for (int i = 0; i < _standInMeshes.Count && i < open.Count; i++)
            {
                if (_standInMeshes[i] == null) continue;
                Mesh fresh = ToMesh(_standIns[i], _standInMeshes[i].name, open[i]);
                _standInMeshes[i].SetColors(fresh.colors);
                Destroy(fresh);
            }
        }

        /// <summary>
        /// A stand-in shape as a Unity mesh. Paint goes in the colour; how much a corner glows by itself goes in
        /// the colour's fourth number (1 is no glow), which is where the shader looks for lamps.
        /// </summary>
        static Mesh ToMesh(ShipMesh data, string name, float[] open)
        {
            int count = data.VertexCount;
            var vertices = new Vector3[count];
            var colors = new Color[count];
            bool linear = ShipMeshes.LinearColours;
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                vertices[i] = new Vector3(data.Positions[o], data.Positions[o + 1], data.Positions[o + 2]);
                float glow = Mathf.Clamp01(data.Glow[i]);
                // Never fully dark: the sky still finds its way in.
                float shade = open != null && glow <= 0.01f ? 0.5f + 0.5f * open[i] : 1f;
                float r = data.Colors[o] * shade, g = data.Colors[o + 1] * shade, b = data.Colors[o + 2] * shade;
                if (linear) { r = LightBake.ToLinear(r); g = LightBake.ToLinear(g); b = LightBake.ToLinear(b); }
                colors[i] = new Color(r, g, b, 1f - glow);
            }
            var mesh = new Mesh { name = name };
            if (count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(data.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void BuildColliders(float depth)
        {
            // Simple solids, whichever look is showing: what you can land on and bump into never depends on the art.
            Mesh mound = ShipMeshes.ToMesh(LaunchShapes.MoundCollision(depth), "MoundCollision");
            _owned.Add(mound);
            var moundGo = Child("MoundCollision");
            moundGo.AddComponent<MeshCollider>().sharedMesh = mound;

            var tower = Child("TowerCollision").AddComponent<BoxCollider>();
            tower.center = V(LaunchLayout.TowerFoot) + Vector3.up * (LaunchLayout.TowerHeight * 0.5f);
            tower.size = new Vector3(LaunchLayout.TowerHalf * 2f, LaunchLayout.TowerHeight, LaunchLayout.TowerHalf * 2f);

            var hut = Child("HutCollision").AddComponent<BoxCollider>();
            hut.center = V(LaunchLayout.HutCentre) + Vector3.up * LaunchLayout.HutHalf.Y;
            hut.size = V(LaunchLayout.HutHalf) * 2f;

            var tank = Child("WaterTowerCollision").AddComponent<CapsuleCollider>();
            tank.center = V(LaunchLayout.WaterTowerFoot) + Vector3.up * (LaunchLayout.WaterTowerHeight * 0.5f);
            tank.radius = LaunchLayout.WaterTowerRadius;
            tank.height = LaunchLayout.WaterTowerHeight;
            tank.direction = 1;

            for (int i = 0; i < LaunchLayout.Masts; i++)
            {
                var mast = Child("MastCollision" + i).AddComponent<CapsuleCollider>();
                mast.center = V(LaunchLayout.Mast(i)) + Vector3.up * (LaunchLayout.MastHeight * 0.5f);
                mast.radius = 0.2f;
                mast.height = LaunchLayout.MastHeight;
                mast.direction = 1;
            }
        }

        GameObject Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = Layers.Default;
            return go;
        }

        static Vector3 V(P3 p) => new Vector3(p.X, p.Y, p.Z);

        /// <summary>True when a point (Unity's coordinates) is over the pad and close above it.</summary>
        public bool OverPad(Vector3 world, float reach = 12f)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return new Vector2(local.x, local.z).magnitude < LaunchLayout.PadRadius && local.y > -1f && local.y < reach;
        }

        void Update()
        {
            if (_shadeWanted)
            {
                _shadeWanted = false;
                List<ShipMesh> shapes = _standIns;
                _shadeJob = PlanetJobs.Run(() => Shade(shapes), ApplyShade, () => -4e5f, true);
            }
            if (_arm == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // The arm is against the ship only while a ship sits quietly on the pad. It swings clear the moment the
            // engines light and comes back a couple of seconds after things have gone quiet again.
            bool shipHere = Engines != null && Engines.OnGround && OverPad(Engines.transform.position);
            bool burning = Engines != null && Engines.Model.State != EngineState.Off;
            if (shipHere && !burning) _quietFor += dt;
            else _quietFor = 0f;
            bool home = Engines == null || (shipHere && !burning && (_quietFor > 2f || ArmSwing <= 0.001f));
            float target = home ? 0f : 1f;
            ArmSwing = Mathf.MoveTowards(ArmSwing, target, dt / Mathf.Max(0.1f, Config.armSeconds));
            // Ease in and out, so it reads as heavy steel on a hinge.
            if (ArmSwing == _armShown) return;
            _armShown = ArmSwing;
            float eased = ArmSwing * ArmSwing * (3f - 2f * ArmSwing);
            _arm.localRotation = Quaternion.Euler(0f, -LaunchLayout.ArmSwingDegrees * eased, 0f);
        }

        void OnDestroy()
        {
            if (_shadeJob != null) _shadeJob.Cancel();
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null) Destroy(_owned[i]);
            if (_material != null) Destroy(_material);
            if (_glowMaterial != null) Destroy(_glowMaterial);
        }
    }
}
