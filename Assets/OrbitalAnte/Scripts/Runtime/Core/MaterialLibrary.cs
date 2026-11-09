using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Runtime materials made from two template materials that the setup script saves in
    /// Resources/Materials, so the URP shaders are always included in builds.
    /// </summary>
    public static class MaterialLibrary
    {
        static Material litTemplate;
        static Material unlitTemplate;
        static readonly Dictionary<Color, Material> lit = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> unlit = new Dictionary<Color, Material>();

        static Material LitTemplate
        {
            get
            {
                if (litTemplate == null)
                {
                    litTemplate = Resources.Load<Material>("Materials/OA_Lit");
                    if (litTemplate == null)
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Lit");
                        if (shader == null) shader = Shader.Find("Standard");
                        litTemplate = new Material(shader);
                        Debug.LogWarning("[OA] OA_Lit template missing. Run Orbital Ante > Run Project Setup.");
                    }
                }
                return litTemplate;
            }
        }

        static Material UnlitTemplate
        {
            get
            {
                if (unlitTemplate == null)
                {
                    unlitTemplate = Resources.Load<Material>("Materials/OA_Unlit");
                    if (unlitTemplate == null)
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Unlit");
                        if (shader == null) shader = Shader.Find("Sprites/Default");
                        unlitTemplate = new Material(shader);
                    }
                }
                return unlitTemplate;
            }
        }

        public static Material Lit(Color color, float smoothness = 0.25f)
        {
            if (lit.TryGetValue(color, out var m) && m != null) return m;
            m = new Material(LitTemplate) { name = "OA_Lit_" + ColorUtility.ToHtmlStringRGB(color) };
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            lit[color] = m;
            return m;
        }

        public static Material Unlit(Color color)
        {
            if (unlit.TryGetValue(color, out var m) && m != null) return m;
            m = new Material(UnlitTemplate) { name = "OA_Unlit_" + ColorUtility.ToHtmlStringRGB(color) };
            m.color = color;
            unlit[color] = m;
            return m;
        }

        static Material particle;
        static Material decal;
        static bool warnedDecal;

        /// <summary>Alpha-clipped particle material (template OA_Particle from setup).</summary>
        public static Material Particle(Texture2D tex)
        {
            if (particle != null) return particle;
            var template = Resources.Load<Material>("Materials/OA_Particle");
            if (template == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                template = new Material(shader);
                Debug.LogWarning("[OA] OA_Particle template missing. Run Orbital Ante > Run Project Setup.");
            }
            particle = new Material(template) { name = "OA_Particle_Runtime" };
            particle.mainTexture = tex;
            return particle;
        }

        /// <summary>URP decal material with the given texture, or null if decals are not set up.</summary>
        public static Material Decal(Texture2D tex)
        {
            if (decal != null) return decal;
            var template = Resources.Load<Material>("Materials/OA_Decal");
            if (template == null)
            {
                if (!warnedDecal) Debug.LogWarning("[OA] OA_Decal template missing, blood decals are off. Run Orbital Ante > Run Project Setup.");
                warnedDecal = true;
                return null;
            }
            decal = new Material(template) { name = "OA_Decal_Runtime" };
            if (decal.HasProperty("Base_Map")) decal.SetTexture("Base_Map", tex);
            else decal.mainTexture = tex;
            return decal;
        }

        /// <summary>Lit material with a texture, not cached (used for the arena floor).</summary>
        public static Material LitTextured(Texture2D tex, Vector2 tiling, float smoothness = 0.15f)
        {
            var m = new Material(LitTemplate) { name = "OA_LitTextured" };
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            m.color = Color.white;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }
    }

    public static class PrimitiveMeshes
    {
        static readonly Dictionary<PrimitiveType, Mesh> cache = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh Get(PrimitiveType type)
        {
            if (cache.TryGetValue(type, out var mesh) && mesh != null) return mesh;
            var temp = GameObject.CreatePrimitive(type);
            mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            cache[type] = mesh;
            return mesh;
        }

        /// <summary>Visual-only mesh object (no collider).</summary>
        public static GameObject CreateVisual(string name, PrimitiveType type, Transform parent, Material material,
            Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = Get(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
