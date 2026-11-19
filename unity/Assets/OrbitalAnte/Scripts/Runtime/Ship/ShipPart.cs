using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// Marks the colliders of one part of the ship (a leg, a window, the dish...), so bullets and crashes know
    /// which part they struck.
    /// </summary>
    public sealed class ShipPart : MonoBehaviour, IHittable
    {
        public Ship Ship { get; set; }
        public int Index { get; set; }

        public HitOutcome Hit(in HitInfo hit)
        {
            return Ship != null ? Ship.HitPart(Index, hit) : new HitOutcome { Hit = true, Label = "Wreckage" };
        }

        public string DescribeForDebug()
        {
            if (Ship == null) return "Wreckage";
            return Ship.Model.Parts[Index].Name + "  " + Mathf.RoundToInt(Ship.Model.Fraction(Index) * 100f) + "%  " + Ship.Model.State(Index);
        }
    }

    /// <summary>Turns the ship's plain mesh data into Unity meshes and hands out its few materials.</summary>
    public static class ShipMeshes
    {
        static Material _glassBase;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int FrostId = Shader.PropertyToID("_Frost");

        public static void ResetStatics() => _glassBase = null;

        public static bool LinearColours => QualitySettings.activeColorSpace == ColorSpace.Linear;

        /// <param name="bakedLinear">Baked colours (linear light) to use in place of the mesh's paint, or null.</param>
        /// <param name="roundDegrees">Above zero, corners in the same place share their facing unless the surfaces meet at more than this angle, so a panelled curve shades round. Zero keeps every panel flat-edged.</param>
        public static Mesh ToMesh(ShipMesh data, string name, float[] bakedLinear = null, float roundDegrees = 0f)
        {
            int count = data.VertexCount;
            var vertices = new Vector3[count];
            var colors = new Color[count];
            bool linear = LinearColours;
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                vertices[i] = new Vector3(data.Positions[o], data.Positions[o + 1], data.Positions[o + 2]);
                float r, g, b;
                if (bakedLinear != null)
                {
                    r = bakedLinear[o]; g = bakedLinear[o + 1]; b = bakedLinear[o + 2];
                    if (!linear) { r = LightBake.ToScreen(r); g = LightBake.ToScreen(g); b = LightBake.ToScreen(b); }
                }
                else
                {
                    r = data.Colors[o]; g = data.Colors[o + 1]; b = data.Colors[o + 2];
                    if (linear) { r = LightBake.ToLinear(r); g = LightBake.ToLinear(g); b = LightBake.ToLinear(b); }
                }
                colors[i] = new Color(r, g, b, 1f);
            }
            var mesh = new Mesh { name = name };
            if (count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(data.Triangles, 0);
            if (roundDegrees > 0f)
            {
                float[] facing = OrbitalAnte.Look.MeshSmoothing.Normals(data.Positions, data.Triangles, roundDegrees);
                var normals = new Vector3[count];
                for (int i = 0; i < count; i++) normals[i] = new Vector3(facing[i * 3], facing[i * 3 + 1], facing[i * 3 + 2]);
                mesh.SetNormals(normals);
            }
            else
            {
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Writes new baked colours into a mesh made by <see cref="ToMesh"/>.</summary>
        public static void SetBaked(Mesh mesh, float[] bakedLinear)
        {
            int count = bakedLinear.Length / 3;
            var colors = new Color[count];
            bool linear = LinearColours;
            for (int i = 0; i < count; i++)
            {
                float r = bakedLinear[i * 3], g = bakedLinear[i * 3 + 1], b = bakedLinear[i * 3 + 2];
                if (!linear) { r = LightBake.ToScreen(r); g = LightBake.ToScreen(g); b = LightBake.ToScreen(b); }
                colors[i] = new Color(r, g, b, 1f);
            }
            mesh.SetColors(colors);
        }

        /// <summary>A fresh see-through material for one pane (each pane frosts over by itself as it cracks).</summary>
        public static Material Glass(Color tint)
        {
            if (_glassBase == null)
            {
                Shader own = Planets.PlanetMaterials.OwnBody ? Resources.Load<Shader>("Shaders/OA_Glass") : null;
                if (own != null && own.isSupported)
                {
                    _glassBase = new Material(own) { name = "OA_Glass (runtime)" };
                }
                else
                {
                    // The stand-in: a material the setup tool saved with Unity's own see-through settings.
                    _glassBase = Resources.Load<Material>("Materials/OA_Glass");
                    if (_glassBase == null)
                    {
                        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                        if (unlit == null) unlit = Shader.Find("Sprites/Default");
                        _glassBase = new Material(unlit) { name = "OA_Glass (runtime stand-in)" };
                        MakeSeeThrough(_glassBase);
                    }
                }
            }
            var m = new Material(_glassBase);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, tint);
            else m.color = tint;
            return m;
        }

        /// <summary>Switches one of Unity's standard URP materials to see-through. Also used by the setup tool.</summary>
        public static void MakeSeeThrough(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20;
        }

        public static void SetFrost(Material m, float frost)
        {
            if (m != null && m.HasProperty(FrostId)) m.SetFloat(FrostId, frost);
        }
    }
}
