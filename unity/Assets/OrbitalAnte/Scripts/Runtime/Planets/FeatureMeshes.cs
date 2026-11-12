using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Turns the shapes from <see cref="FeatureShapes"/> into Unity meshes, and keeps each one so it is only
    /// built once. Every rock or tree of the same kind and variant shares one mesh.
    ///
    /// The meshes carry vertex colours and no texture coordinates. The material that draws them must use a
    /// shader that reads vertex colours (the stock URP "Lit" shader does not).
    /// </summary>
    public static class FeatureMeshes
    {
        static Mesh[] _cache;

        static readonly int KindCount = System.Enum.GetValues(typeof(FeatureKind)).Length;

        /// <summary>The shared mesh for one kind and variant. 1 unit tall; scale it by the height in metres.</summary>
        public static Mesh Get(FeatureKind kind, int variant)
        {
            int v = ((variant % FeatureShapes.Variants) + FeatureShapes.Variants) % FeatureShapes.Variants;
            int kinds = KindCount;
            if (_cache == null || _cache.Length != kinds * FeatureShapes.Variants) _cache = new Mesh[kinds * FeatureShapes.Variants];

            int slot = (int)kind * FeatureShapes.Variants + v;
            if (slot < 0 || slot >= _cache.Length) slot = v;   // An unknown kind falls back to the first one.

            // "== null" is also true for a mesh Unity has destroyed (after leaving play mode, for example).
            Mesh cached = _cache[slot];
            if (cached != null) return cached;

            Mesh mesh = Build(kind, v);
            _cache[slot] = mesh;
            return mesh;
        }

        static Mesh Build(FeatureKind kind, int variant)
        {
            ShapeData data = FeatureShapes.Build(kind, variant);
            int count = data.VertexCount;
            var vertices = new Vector3[count];
            var colors = new Color[count];
            // See SkyGlobals.Linear: stored colours are converted when the project draws in linear colour.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]);
                float r = Mathf.Clamp01(data.Colors[i * 3]), g = Mathf.Clamp01(data.Colors[i * 3 + 1]), b = Mathf.Clamp01(data.Colors[i * 3 + 2]);
                colors[i] = linear ? new Color(SkyGlobals.ToLinear(r), SkyGlobals.ToLinear(g), SkyGlobals.ToLinear(b), 1f) : new Color(r, g, b, 1f);
            }

            // At most 720 triangles x 3 corners = 2,160 vertices, well inside the 16-bit index limit.
            var mesh = new Mesh { name = kind + "_" + variant };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(data.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void ResetStatics() => _cache = null;
    }
}
