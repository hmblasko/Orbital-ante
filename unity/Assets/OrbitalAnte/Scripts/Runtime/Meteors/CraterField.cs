using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Meteors
{
    /// <summary>
    /// The craters strikes leave. Each is a mesh laid over the ground (see <see cref="CraterShape"/>), a child of
    /// its body so it turns with it, drawn with the ground's own shader so it takes the same light, grit and haze,
    /// and solid (a mesh collider) so the player and the rover ride over its rim. A fresh floor glows and cools.
    /// They last the run, up to <see cref="MeteorRules.maxCraters"/>; the oldest goes first. Hidden while their
    /// world's close ground is not shown.
    /// </summary>
    public sealed class CraterField : MonoBehaviour
    {
        static readonly int VertexGlowId = Shader.PropertyToID("_VertexGlow");
        /// <summary>How brightly a fresh floor glows (the shader's "glow where the mesh says so").</summary>
        const float FreshGlow = 6f;

        sealed class Live
        {
            public GameObject Go;
            public Mesh Mesh;
            public Material Hot;
            public float Born;
            public string Body;
            public int Seed;
            public PlanetSurface Surface;
            public MeshRenderer Renderer;
            public MeshCollider Collider;
        }

        readonly List<Live> _live = new List<Live>();
        readonly Dictionary<string, Material> _cool = new Dictionary<string, Material>();
        MeteorRules _rules;
        float _nextLook;

        public int Count => _live.Count;

        public static CraterField Create(Transform parent, MeteorRules rules)
        {
            var go = new GameObject("Craters");
            go.transform.SetParent(parent, false);
            var field = go.AddComponent<CraterField>();
            field._rules = rules;
            CraterRegistry.Clear();
            return field;
        }

        public void Add(PlanetSurface surface, CraterMeshData data, float relief)
        {
            if (surface == null || data == null || data.Corners < 3) return;
            while (_live.Count > 0 && _live.Count >= Mathf.Max(1, _rules.maxCraters)) Remove(0);

            int corners = data.Corners;
            var positions = new Vector3[corners];
            var ups = new List<Vector3>(corners);
            var colours = new Color[corners];
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            // Corners in the body's own space, like the ground tiles', so the shader's close-up grit and stones
            // carry on across the apron without a seam.
            Vector3 origin = SpaceConvert.ToVector3(data.Origin);
            for (int i = 0; i < corners; i++)
            {
                positions[i] = origin + new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]);
                ups.Add(new Vector3(data.Directions[i * 3], data.Directions[i * 3 + 1], data.Directions[i * 3 + 2]));
                // The ground's colours are worked out in linear light by hand (see SkyGlobals.ToLinear), like the tiles'.
                float r = data.Colours[i * 4], g = data.Colours[i * 4 + 1], b = data.Colours[i * 4 + 2];
                if (linear) { r = SkyGlobals.ToLinear(r); g = SkyGlobals.ToLinear(g); b = SkyGlobals.ToLinear(b); }
                colours[i] = new Color(r, g, b, data.Colours[i * 4 + 3]);
            }
            var mesh = new Mesh { name = "Crater" };
            mesh.vertices = positions;
            // The ground's shader reads "up" from the first texture channel.
            mesh.SetUVs(0, ups);
            mesh.colors = colours;
            mesh.triangles = data.Triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string id = surface.Body.Spec.id;
            var go = new GameObject("Crater");
            go.layer = Layers.Default;
            go.transform.SetParent(surface.Body.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material cool = CoolFor(surface, id, relief);
            Material hot = null;
            if (cool.HasProperty(VertexGlowId) && _rules.glowSeconds > 0f)
            {
                hot = new Material(cool) { name = "Crater (hot)" };
                hot.SetFloat(VertexGlowId, FreshGlow);
            }
            renderer.sharedMaterial = hot != null ? hot : cool;
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;

            _live.Add(new Live
            {
                Go = go, Mesh = mesh, Hot = hot, Born = Time.time, Body = id, Seed = data.Site.Seed,
                Surface = surface, Renderer = renderer, Collider = collider,
            });
            CraterRegistry.Add(id, data.Site);
        }

        /// <summary>
        /// The ground tiles' own material where there is one, so a crater takes exactly the same light, grit and
        /// far picture as the ground round it; a material of its own only if the tiles have none yet.
        /// </summary>
        Material CoolFor(PlanetSurface surface, string body, float relief)
        {
            if (surface != null && surface.GroundMaterial != null) return surface.GroundMaterial;
            if (_cool.TryGetValue(body, out Material m) && m != null) return m;
            m = PlanetMaterials.Ground(body + " crater", relief);
            _cool[body] = m;
            return m;
        }

        void Update()
        {
            // A fresh floor glows red, and cools over the glow time.
            for (int i = 0; i < _live.Count; i++)
            {
                Live c = _live[i];
                if (c.Hot == null) continue;
                float k = (Time.time - c.Born) / Mathf.Max(0.1f, _rules.glowSeconds);
                if (k >= 1f)
                {
                    c.Renderer.sharedMaterial = CoolFor(c.Surface, c.Body, c.Hot.HasProperty("_Ground") ? c.Hot.GetFloat("_Ground") : 1f);
                    Destroy(c.Hot);
                    c.Hot = null;
                    continue;
                }
                float flicker = 0.92f + 0.08f * Mathf.PerlinNoise(Time.time * 3f, c.Seed * 0.37f);
                c.Hot.SetFloat(VertexGlowId, FreshGlow * (1f - k) * (1f - k) * flicker);
            }

            if (Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + 0.5f;
            for (int i = 0; i < _live.Count; i++)
            {
                Live c = _live[i];
                if (c.Surface == null) continue;
                bool shown = c.Surface.Near && c.Surface.GroundShown;
                if (c.Renderer.enabled != shown) c.Renderer.enabled = shown;
                if (c.Collider.enabled != c.Surface.Near) c.Collider.enabled = c.Surface.Near;
            }
        }

        void Remove(int index)
        {
            Live c = _live[index];
            _live.RemoveAt(index);
            CraterRegistry.Remove(c.Body, c.Seed);
            if (c.Go != null) Destroy(c.Go);
            if (c.Mesh != null) Destroy(c.Mesh);
            if (c.Hot != null) Destroy(c.Hot);
        }

        public void Clear()
        {
            while (_live.Count > 0) Remove(_live.Count - 1);
            CraterRegistry.Clear();
        }

        void OnDestroy()
        {
            Clear();
            foreach (Material m in _cool.Values) if (m != null) Destroy(m);
            _cool.Clear();
        }
    }
}
