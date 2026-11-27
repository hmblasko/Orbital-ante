using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>What a cave network needs built in the background before it can be shown.</summary>
    public sealed class CaveBuild
    {
        public List<CaveChunk> Chunks;
        public CaveLightVolume Light;
    }

    /// <summary>
    /// A cave network in the world: its blocks of wall (each drawn and solid on its own, so blocks out of view are
    /// skipped), its work lamps, and the box of light the shaders read while the player is near it (see
    /// <see cref="CaveLight"/>). The walls go up one block a frame, so a network arriving is never a hitch, and it
    /// counts as ready (safe for enemies to stand in: they find the floor from the network's own shape) once every
    /// block is drawn. A block is only made solid (one a frame) when something that needs ground (the suit, the
    /// ship) comes within <see cref="SolidReach"/> of it, because cooking a collider is the dearest part of putting
    /// a block up: flying over a cave or passing it far off never pays for one.
    /// A child of the body's detail, in the body's own space.
    /// </summary>
    public sealed class CaveNetworkView : MonoBehaviour
    {
        public CaveNetwork Network { get; private set; }
        public Cave Cave { get; private set; }
        public bool Ready { get; private set; }

        CaveBuild _build;
        int _next;
        Action<Cave> _onReady;
        Material _wall;
        readonly List<Mesh> _meshes = new List<Mesh>();
        Texture3D _light;
        CaveLightVolume _volume;   // kept for the game's own questions (how dark it is where the player stands)
        Matrix4x4 _bodyToVolume;
        Vector3 _boxMin, _boxMax;   // local frame of the network, metres
        const int BlocksPerFrame = 1;
        /// <summary>How near (metres from a block's bounds) something that needs ground must be for the block to be made solid.</summary>
        /// Further than an enemy wakes (70 m) and sees, so rock between a fight and the player is always solid, and
        /// gibs and dropped guns always have a floor.
        public const float SolidReach = 90f;
        readonly List<GameObject> _unsolid = new List<GameObject>();
        float _solidCheckAt;
        PlanetSurface _surface;

        static Mesh _lampBody, _lampGlow;

        /// <summary>The build's colours must already be linear if the project is (done in the background job).</summary>
        public static CaveNetworkView Create(Transform parent, PlanetSurface surface, Cave cave, CaveBuild build, Action<Cave> onReady)
        {
            var go = new GameObject("Cave network " + cave.Formation);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CaveNetworkView>();
            view.Network = cave.Network;
            view.Cave = cave;
            view._build = build;
            view._onReady = onReady;
            view._surface = surface;
            // Lit by the shared light, which the cave's own box of light then darkens where the sky cannot reach.
            // Without the box (or with Unity's standard shaders standing in) nothing would keep the Sun out, so the
            // walls then take almost none of it, as the old single caves do.
            bool boxed = build.Light != null && PlanetMaterials.OwnTerrain;
            view._wall = PlanetMaterials.VertexColour(Color.white, boxed ? 1f : 0.05f);
            view.MakeLight();
            return view;
        }

        /// <summary>Turns the build's colours linear. Called in the background job, not on the main thread.</summary>
        public static void ToLinear(CaveBuild build)
        {
            foreach (CaveChunk chunk in build.Chunks)
            {
                float[] colors = chunk.Shape.Colors;
                for (int i = 0; i < colors.Length; i++) colors[i] = SkyGlobals.ToLinear(colors[i]);
            }
        }

        void Update()
        {
            if (Ready) { MakeSolidNear(); return; }
            if (_build == null) return;
            for (int n = 0; n < BlocksPerFrame && _next < _build.Chunks.Count; n++) MakeBlock(_build.Chunks[_next++]);
            if (_next < _build.Chunks.Count) return;
            MakeLamps();
            Ready = true;
            _build.Chunks = null;
            _onReady?.Invoke(Cave);
        }

        void MakeBlock(CaveChunk chunk)
        {
            ShapeData shape = chunk.Shape;
            int count = shape.VertexCount;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                vertices[i] = new Vector3(shape.Positions[o], shape.Positions[o + 1], shape.Positions[o + 2]);
                normals[i] = new Vector3(shape.Normals[o], shape.Normals[o + 1], shape.Normals[o + 2]);
                colors[i] = new Color(shape.Colors[o], shape.Colors[o + 1], shape.Colors[o + 2], 1f);
            }
            var mesh = new Mesh { name = "Cave " + Cave.Formation };
            if (count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(shape.Triangles, 0);
            mesh.RecalculateBounds();
            _meshes.Add(mesh);

            var go = new GameObject("Cave");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _wall;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _unsolid.Add(go);
            _solidCheckAt = 0f;
        }

        /// <summary>
        /// Gives a collider to one block a frame that something needing ground has come near; when none is near, it
        /// looks again a fifth of a second later. Once solid, a block stays solid.
        /// </summary>
        void MakeSolidNear()
        {
            if (_unsolid.Count == 0 || _surface == null || Time.time < _solidCheckAt) return;
            _solidCheckAt = Time.time + 0.2f;
            int count = _surface.NeedGroundCount;
            if (count == 0) return;
            for (int i = _unsolid.Count - 1; i >= 0; i--)
            {
                GameObject block = _unsolid[i];
                if (block == null) { _unsolid.RemoveAt(i); continue; }
                Mesh mesh = block.GetComponent<MeshFilter>().sharedMesh;
                Bounds bounds = mesh.bounds;
                bounds.Expand(SolidReach * 2f);
                bool near = false;
                for (int g = 0; g < count && !near; g++)
                    near = bounds.Contains(SpaceConvert.ToVector3(_surface.NeedGroundAt(g)));   // both in the body's own space
                if (!near) continue;
                var collider = block.AddComponent<MeshCollider>();
                // Surface nets output has no repeated corners or slivers to clean up.
                collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.UseFastMidphase;
                collider.sharedMesh = mesh;
                _unsolid.RemoveAt(i);
                _solidCheckAt = 0f;   // more may be near: the next one next frame
                return;
            }
        }

        void MakeLamps()
        {
            if (_lampBody == null) _lampBody = ToMesh(CaveProps.WorkLamp(out ShapeData glow), "Cave lamp");
            if (_lampGlow == null) _lampGlow = ToMesh(CaveProps.WorkLampGlow(), "Cave lamp glow");
            Material body = PlanetMaterials.VertexColour(Color.white, 1f);
            Material glowing = PlanetMaterials.VertexColour(Color.white, 0f, 2f, false);
            foreach (CaveLamp lamp in Network.Lamps)
            {
                if (lamp.Crystal) continue;
                Vector3 up = SpaceConvert.ToVector3(lamp.Up);
                Vector3 facing = SpaceConvert.ToVector3(lamp.Facing);
                var go = new GameObject("Cave lamp");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = SpaceConvert.ToVector3(lamp.Position);
                Vector3 ahead = Vector3.ProjectOnPlane(facing, up);
                if (ahead.sqrMagnitude < 1e-6f) ahead = Vector3.ProjectOnPlane(Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.forward, up);
                go.transform.localRotation = Quaternion.LookRotation(ahead.normalized, up);
                go.AddComponent<MeshFilter>().sharedMesh = _lampBody;
                var lampRenderer = go.AddComponent<MeshRenderer>();
                lampRenderer.sharedMaterial = body;
                // Cave darkness comes from the cave's own light box, never from cast shadows.
                lampRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var head = new GameObject("Glow");
                head.transform.SetParent(go.transform, false);
                head.AddComponent<MeshFilter>().sharedMesh = _lampGlow;
                var glowRenderer = head.AddComponent<MeshRenderer>();
                glowRenderer.sharedMaterial = glowing;
                glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static Mesh ToMesh(ShapeData shape, string name)
        {
            int count = shape.VertexCount;
            var vertices = new Vector3[count];
            var colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                vertices[i] = new Vector3(shape.Positions[o], shape.Positions[o + 1], shape.Positions[o + 2]);
                colors[i] = new Color(shape.Colors[o], shape.Colors[o + 1], shape.Colors[o + 2], 1f);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) colors[i] = colors[i].linear;
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(shape.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The box of light as a 3D texture, and the sum from the body's space into it.</summary>
        void MakeLight()
        {
            CaveLightVolume v = _build.Light;
            if (v == null) return;
            // Stored as plain numbers, never sRGB: the light in it is already in the shaders' own units.
            _light = new Texture3D(v.Nx, v.Ny, v.Nz, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                UnityEngine.Experimental.Rendering.TextureCreationFlags.None)
            {
                name = "Cave light " + Cave.Formation, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            _light.SetPixelData(v.Rgba, 0);
            _light.Apply(false, true);
            _volume = v;
            _volume.Rgba = null;   // on the graphics card now
            _build.Light = null;

            CaveNetwork n = Network;
            Vector3 size = new Vector3(v.Nx, v.Ny, v.Nz) * (float)CaveLightVolume.Cell;
            // body point -> local frame (east, up, north) from the network's origin -> 0..1 across the box.
            var toLocal = new Matrix4x4();
            toLocal.SetRow(0, Row(n.East, n.Origin));
            toLocal.SetRow(1, Row(n.Up, n.Origin));
            toLocal.SetRow(2, Row(n.North, n.Origin));
            toLocal.SetRow(3, new Vector4(0, 0, 0, 1));
            Vector3 origin = SpaceConvert.ToVector3(v.LocalOrigin);
            Matrix4x4 toBox = Matrix4x4.Scale(new Vector3(1f / size.x, 1f / size.y, 1f / size.z)) * Matrix4x4.Translate(-origin);
            _bodyToVolume = toBox * toLocal;
            _boxMin = origin;
            _boxMax = origin + size;
        }

        static Vector4 Row(Vec3d axis, Vec3d origin)
        {
            return new Vector4((float)axis.X, (float)axis.Y, (float)axis.Z, (float)-Vec3d.Dot(axis, origin));
        }

        /// <summary>How far a point (world space) is outside this network's box of light, in metres; 0 inside.</summary>
        public float OutsideBy(Vector3 world)
        {
            if (_light == null) return float.MaxValue;
            Vector3 body = transform.InverseTransformPoint(world);
            Vec3d local = Network.ToLocal(SpaceConvert.ToVec3d(body));
            float dx = Mathf.Max(_boxMin.x - (float)local.X, 0f, (float)local.X - _boxMax.x);
            float dy = Mathf.Max(_boxMin.y - (float)local.Y, 0f, (float)local.Y - _boxMax.y);
            float dz = Mathf.Max(_boxMin.z - (float)local.Z, 0f, (float)local.Z - _boxMax.z);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public Texture3D LightTexture => _light;

        /// <summary>How visible someone at a point (world space) is in this cave's light (see CaveLightVolume.Visibility).</summary>
        public float VisibilityAt(Vector3 world)
        {
            if (_volume == null) return 1f;
            Vec3d body = SpaceConvert.ToVec3d(transform.InverseTransformPoint(world));
            _volume.Sample(Network, body, out float sky, out float r, out float g, out float b);
            return CaveLightVolume.Visibility(sky, r, g, b);
        }
        /// <summary>From world space to 0..1 across the box of light, for this frame.</summary>
        public Matrix4x4 WorldToVolume => _bodyToVolume * transform.worldToLocalMatrix;

        void OnEnable() => CaveLight.Add(this);
        void OnDisable() => CaveLight.Remove(this);

        void OnDestroy()
        {
            CaveLight.Remove(this);
            for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) Destroy(_meshes[i]);
            _meshes.Clear();
            if (_light != null) Destroy(_light);
        }
    }

    /// <summary>
    /// Hands the shaders the box of light of the cave network the eye is in or nearest to, once a frame; or tells
    /// them there is none, and then they read nothing. Called by <see cref="PlanetSky"/>.
    /// </summary>
    public static class CaveLight
    {
        static readonly List<CaveNetworkView> Views = new List<CaveNetworkView>();
        static readonly int TextureId = Shader.PropertyToID("_OA_CaveLight");
        static readonly int MatrixId = Shader.PropertyToID("_OA_CaveToVolume");
        static readonly int OnId = Shader.PropertyToID("_OA_CaveOn");
        static Texture3D _blank;
        /// <summary>True while any cave network is built in the world (only on a world the player is near).</summary>
        public static bool AnyBuilt => Views.Count > 0;
        /// <summary>The view whose light is in use this frame, or null.</summary>
        public static CaveNetworkView Current { get; private set; }

        public static void Add(CaveNetworkView view) { if (!Views.Contains(view)) Views.Add(view); }
        public static void Remove(CaveNetworkView view) { Views.Remove(view); if (Current == view) Current = null; }

        /// <summary>Sets the shaders' cave light for an eye in world space.</summary>
        public static void Apply(Vector3 eye)
        {
            CaveNetworkView best = null;
            float nearest = 30f;   // a network further than this outside its box cannot be seen into
            for (int i = 0; i < Views.Count; i++)
            {
                CaveNetworkView v = Views[i];
                if (v == null || v.LightTexture == null) continue;
                float d = v.OutsideBy(eye);
                if (d < nearest) { nearest = d; best = v; }
            }
            Current = best;
            if (best == null)
            {
                Off();
                return;
            }
            Shader.SetGlobalTexture(TextureId, best.LightTexture);
            Shader.SetGlobalMatrix(MatrixId, best.WorldToVolume);
            Shader.SetGlobalVector(OnId, new Vector4(1f, CaveLightVolume.LightScale, 0f, 0f));
        }

        /// <summary>How visible someone at a point is in the light of the cave in use; 1 when there is none.</summary>
        public static float VisibilityAt(Vector3 world) => Current != null ? Current.VisibilityAt(world) : 1f;

        /// <summary>Binds the empty light box before any scene draws, so no shader ever reads an unset 3D texture.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BindBlank() => Off();

        public static void Off()
        {
            if (_blank == null)
            {
                _blank = new Texture3D(1, 1, 1, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                    UnityEngine.Experimental.Rendering.TextureCreationFlags.None) { name = "No cave light" };
                _blank.SetPixel(0, 0, 0, new Color(0f, 0f, 0f, 1f));
                _blank.Apply(false, true);
            }
            Current = null;
            Shader.SetGlobalTexture(TextureId, _blank);
            Shader.SetGlobalVector(OnId, Vector4.zero);
        }
    }
}
