using System;
using System.Reflection;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Gore
{
    /// <summary>
    /// A fixed ring of blood splats. When the ring is full the oldest splat is reused, so the count never grows.
    ///
    /// Two ways of drawing them:
    /// - URP Decal Projectors, which wrap over whatever they land on. Needs the Decal renderer feature and a decal
    ///   material, both created by Orbital Ante > Set Up Project.
    /// - Flat splat meshes laid on the surface. Cheaper, and works with any renderer setup. Used automatically
    ///   when projectors are not available.
    ///
    /// The projector component is reached by name instead of by a compile-time reference, so the game still
    /// compiles and runs if URP's decal API changes or is missing.
    /// </summary>
    public sealed class DecalPool
    {
        const float FadeSeconds = 1.2f;
        const float ProjectorDepth = 0.5f;
        const string ProjectorTypeName =
            "UnityEngine.Rendering.Universal.DecalProjector, Unity.RenderPipelines.Universal.Runtime";

        GoreConfig _cfg;
        Transform[] _slots;
        float[] _bornAt;
        float[] _size;
        bool[] _live;
        int _next;
        int _spawned;

        bool _projectors;
        bool _projectorsCanScale;
        PropertyInfo _projectorSize;
        Component[] _projectorComponents;

        /// <summary>True when splats are drawn with URP decal projectors, false for flat meshes.</summary>
        public bool UsingProjectors => _projectors;
        public int LiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _live.Length; i++) if (_live[i]) n++;
                return n;
            }
        }

        public void Build(Transform parent, GoreConfig cfg)
        {
            _cfg = cfg;
            int count = Mathf.Clamp(cfg.maxDecals, 1, 256);
            _slots = new Transform[count];
            _bornAt = new float[count];
            _size = new float[count];
            _live = new bool[count];

            bool wantProjectors = cfg.decalMode == DecalMode.ProjectorOnly
                                  || (cfg.decalMode == DecalMode.Auto && cfg.decalProjectorsReady);
            if (wantProjectors && TryBuildProjectors(parent, count)) return;
            BuildMeshes(parent, count);
        }

        // ------------------------------------------------------------------ projector backend

        bool TryBuildProjectors(Transform parent, int count)
        {
            try
            {
                Type type = Type.GetType(ProjectorTypeName);
                if (type == null) return false;

                Material source = Resources.Load<Material>("Materials/OA_Decal");
                if (source == null)
                {
                    Shader shader = Shader.Find("Shader Graphs/Decal");
                    if (shader == null) return false;
                    source = new Material(shader);
                }

                var material = new Material(source) { name = "OA_BloodDecal (runtime)" };
                Texture2D texture = MakeSplatTexture(_cfg.bloodColor * 0.72f);
                if (material.HasProperty("Base_Map")) material.SetTexture("Base_Map", texture);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);

                PropertyInfo materialProp = type.GetProperty("material");
                _projectorSize = type.GetProperty("size");
                PropertyInfo pivotProp = type.GetProperty("pivot");
                PropertyInfo scaleModeProp = type.GetProperty("scaleMode");
                PropertyInfo drawDistanceProp = type.GetProperty("drawDistance");
                if (materialProp == null || _projectorSize == null) return false;

                _projectorComponents = new Component[count];
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject("BloodDecal");
                    go.transform.SetParent(parent, false);
                    go.SetActive(false);
                    Component projector = go.AddComponent(type);
                    materialProp.SetValue(projector, material);
                    // A unit box that reaches forward from the object; the object's scale then sets its real size.
                    _projectorSize.SetValue(projector, Vector3.one);
                    if (pivotProp != null) pivotProp.SetValue(projector, new Vector3(0f, 0f, 0.5f));
                    if (drawDistanceProp != null) drawDistanceProp.SetValue(projector, 60f);
                    if (scaleModeProp != null)
                    {
                        // 1 = inherit the size from the object's scale, which lets a splat shrink away cheaply.
                        scaleModeProp.SetValue(projector, Enum.ToObject(scaleModeProp.PropertyType, 1));
                        _projectorsCanScale = true;
                    }
                    _projectorComponents[i] = projector;
                    _slots[i] = go.transform;
                }

                _projectors = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Blood decals: URP decal projectors are not available (" + e.Message +
                                 "). Using flat splat meshes instead.");
                // Throw away anything half-built.
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null) UnityEngine.Object.Destroy(_slots[i].gameObject);
                    _slots[i] = null;
                }
                _projectors = false;
                return false;
            }
        }

        // ------------------------------------------------------------------ mesh backend

        void BuildMeshes(Transform parent, int count)
        {
            Material material = Mat.Unlit(_cfg.bloodColor * 0.72f);
            Mesh[] shapes = { MakeSplatMesh(101), MakeSplatMesh(202), MakeSplatMesh(303) };

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("BloodSplat");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = shapes[i % shapes.Length];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
                _slots[i] = go.transform;
            }
            _projectors = false;
        }

        // ------------------------------------------------------------------ use

        public void Spawn(Vector3 point, Vector3 normal, float size)
        {
            if (_slots == null) return;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            normal.Normalize();

            int index = _next;
            _next = (_next + 1) % _slots.Length;
            Transform slot = _slots[index];
            if (slot == null) return;

            float spin = UnityEngine.Random.Range(0f, 360f);
            _spawned++;

            if (_projectors)
            {
                // A projector looks along its forward axis, so it sits just off the surface looking in.
                slot.position = point + normal * (ProjectorDepth * 0.5f);
                slot.rotation = Quaternion.LookRotation(-normal, PerpendicularTo(normal)) * Quaternion.Euler(0f, 0f, spin);
                if (_projectorsCanScale)
                {
                    slot.localScale = new Vector3(size, size, ProjectorDepth);
                }
                else
                {
                    _projectorSize.SetValue(_projectorComponents[index], new Vector3(size, size, ProjectorDepth));
                }
            }
            else
            {
                // A tiny, slightly different lift for each splat stops overlapping splats flickering.
                float lift = 0.006f + (_spawned % 16) * 0.0005f;
                slot.position = point + normal * lift;
                slot.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, spin, 0f);
                slot.localScale = new Vector3(size, 1f, size);
            }

            _bornAt[index] = Time.time;
            _size[index] = size;
            _live[index] = true;
            slot.gameObject.SetActive(true);
        }

        public void Tick()
        {
            if (_slots == null) return;
            float now = Time.time;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_live[i]) continue;
                float age = now - _bornAt[i];
                if (age >= _cfg.decalLife)
                {
                    _live[i] = false;
                    if (_slots[i] != null) _slots[i].gameObject.SetActive(false);
                    continue;
                }

                float remaining = _cfg.decalLife - age;
                if (remaining < FadeSeconds && _slots[i] != null)
                {
                    float k = Mathf.Max(0.02f, remaining / FadeSeconds) * _size[i];
                    if (_projectors)
                    {
                        if (_projectorsCanScale) _slots[i].localScale = new Vector3(k, k, ProjectorDepth);
                    }
                    else
                    {
                        _slots[i].localScale = new Vector3(k, 1f, k);
                    }
                }
            }
        }

        public void Clear()
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                _live[i] = false;
                if (_slots[i] != null) _slots[i].gameObject.SetActive(false);
            }
        }

        static Vector3 PerpendicularTo(Vector3 normal)
        {
            Vector3 reference = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
            return Vector3.Cross(normal, reference).normalized;
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>Radius of an irregular blob at a given angle, between roughly 0.25 and 0.5.</summary>
        static float BlobRadius(float angle, float a, float b, float c)
        {
            float wobble = 0.5f * Mathf.Sin(angle * 3f + a) + 0.3f * Mathf.Sin(angle * 5f + b) + 0.2f * Mathf.Sin(angle * 8f + c);
            return 0.38f + 0.11f * wobble;
        }

        /// <summary>A flat irregular splat about one unit across, lying in the XZ plane, visible from both sides.</summary>
        static Mesh MakeSplatMesh(int seed)
        {
            var rng = new System.Random(seed);
            float a = (float)rng.NextDouble() * 6.28f, b = (float)rng.NextDouble() * 6.28f, c = (float)rng.NextDouble() * 6.28f;

            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();

            AddFan(vertices, triangles, Vector3.zero, 22, angle => BlobRadius(angle, a, b, c));

            // A few satellite droplets flung off the main splat.
            int droplets = 4 + rng.Next(3);
            for (int d = 0; d < droplets; d++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distance = 0.5f + (float)rng.NextDouble() * 0.22f;
                float radius = 0.035f + (float)rng.NextDouble() * 0.05f;
                var centre = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                AddFan(vertices, triangles, centre, 8, _ => radius);
            }

            var mesh = new Mesh { name = "BloodSplat" + seed };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddFan(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles,
            Vector3 centre, int segments, Func<float, float> radiusAt)
        {
            int start = vertices.Count;
            vertices.Add(centre);
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float r = radiusAt(angle);
                vertices.Add(centre + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
            }
            for (int i = 0; i < segments; i++)
            {
                int current = start + 1 + i;
                int next = start + 1 + (i + 1) % segments;
                // Both windings, so the splat shows whichever way the surface faces.
                triangles.Add(start); triangles.Add(next); triangles.Add(current);
                triangles.Add(start); triangles.Add(current); triangles.Add(next);
            }
        }

        /// <summary>The same kind of splat as a texture with a soft transparent edge, for decal projectors.</summary>
        static Texture2D MakeSplatTexture(Color color)
        {
            const int size = 128;
            var rng = new System.Random(404);
            float a = (float)rng.NextDouble() * 6.28f, b = (float)rng.NextDouble() * 6.28f, c = (float)rng.NextDouble() * 6.28f;

            int droplets = 6;
            var dropCentres = new Vector2[droplets];
            var dropRadii = new float[droplets];
            for (int d = 0; d < droplets; d++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distance = 0.36f + (float)rng.NextDouble() * 0.09f;
                dropCentres[d] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                dropRadii[d] = 0.02f + (float)rng.NextDouble() * 0.03f;
            }

            var pixels = new Color32[size * size];
            const float edge = 0.02f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // -0.5..0.5 across the texture; the main blob is scaled to leave room for droplets.
                    var p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                    float angle = Mathf.Atan2(p.y, p.x);
                    float inside = BlobRadius(angle, a, b, c) * 0.7f - p.magnitude;
                    float alpha = Mathf.Clamp01(inside / edge);
                    for (int d = 0; d < droplets; d++)
                    {
                        float drop = dropRadii[d] - (p - dropCentres[d]).magnitude;
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(drop / edge));
                    }
                    Color pixel = color;
                    pixel.a = alpha;
                    pixels[y * size + x] = pixel;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "BloodSplatTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
