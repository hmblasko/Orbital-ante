using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Hands out the materials planets are drawn with. The game has four small shaders of its own (ground, far-off
    /// bodies, the rim of air, water). If any of them is missing or will not run on this machine, or the
    /// "simple shaders" box is ticked on the Planets asset, Unity's standard shaders are used instead: plainer,
    /// but nothing turns pink.
    /// </summary>
    public static class PlanetMaterials
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int FarMapId = Shader.PropertyToID("_FarMap");
        static readonly int FarBlendId = Shader.PropertyToID("_FarBlend");
        static readonly int SunAmountId = Shader.PropertyToID("_SunAmount");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int OutdoorsId = Shader.PropertyToID("_Outdoors");
        static readonly int EmissionId = Shader.PropertyToID("_Emission");
        static readonly int VeilId = Shader.PropertyToID("_Veil");
        static readonly int TwoSidedId = Shader.PropertyToID("_TwoSided");
        static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        static readonly int RippleId = Shader.PropertyToID("_Ripple");
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        static readonly int LimbId = Shader.PropertyToID("_Limb");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int BoostId = Shader.PropertyToID("_Boost");
        static readonly int PaintId = Shader.PropertyToID("_Paint");
        static readonly int PaintScaleId = Shader.PropertyToID("_PaintScale");
        static readonly int BackdropId = Shader.PropertyToID("_Backdrop");
        static readonly int BackdropStrengthId = Shader.PropertyToID("_BackdropStrength");

        static bool _looked;
        static bool _simple;
        static Shader _terrain, _body, _atmosphere, _water, _sky, _model;
        static Material _vertexLit, _vertexUnlit;
        static readonly Dictionary<long, Material> VertexCache = new Dictionary<long, Material>();
        static readonly Dictionary<int, Material> ModelCache = new Dictionary<int, Material>();

        /// <summary>True when the ground is drawn with the game's own shader (far-picture blending, lamp, cave darkness).</summary>
        public static bool OwnTerrain { get { Look(); return _terrain != null; } }
        /// <summary>True when far-off bodies are drawn with the game's own shader (true phases, sky veil).</summary>
        public static bool OwnBody { get { Look(); return _body != null; } }
        public static bool OwnWater { get { Look(); return _water != null; } }

        /// <summary>Call before building a world. <paramref name="simple"/> forces Unity's standard shaders.</summary>
        public static void Configure(bool simple)
        {
            _simple = simple;
            _looked = false;
            ClearVertexCache();
        }

        public static void ResetStatics()
        {
            _simple = false;
            _looked = false;
            _terrain = _body = _atmosphere = _water = _sky = _model = null;
            _vertexLit = _vertexUnlit = null;
            ClearVertexCache();
        }

        static void ClearVertexCache()
        {
            foreach (Material m in VertexCache.Values)
            {
                if (m != null) Object.Destroy(m);
            }
            VertexCache.Clear();
            foreach (Material m in ModelCache.Values)
            {
                if (m != null) Object.Destroy(m);
            }
            ModelCache.Clear();
        }

        public static void SetStrength(Material material, float strength)
        {
            if (material != null) material.SetFloat(StrengthId, strength);
        }

        static void Look()
        {
            if (_looked) return;
            _looked = true;
            _terrain = _simple ? null : Find("OA_Terrain", "OrbitalAnte/Terrain");
            _body = _simple ? null : Find("OA_Body", "OrbitalAnte/Body");
            _atmosphere = _simple ? null : Find("OA_Atmosphere", "OrbitalAnte/Atmosphere");
            _water = _simple ? null : Find("OA_Water", "OrbitalAnte/Water");
            _sky = _simple ? null : Find("OA_Sky", "OrbitalAnte/Sky");
            // Models fall back to the ground's shader (no shine) if theirs cannot be used.
            _model = _simple || _terrain == null ? null : Find("OA_Model", "OrbitalAnte/Model");
            if (!_simple && _terrain != null && _model == null)
            {
                Debug.LogWarning("Orbital Ante: the model shader (OA_Model) could not be used on this machine, so models are drawn with the ground's shader and nothing shines.");
            }
            if (!_simple && (_terrain == null || _body == null || _atmosphere == null || _water == null))
            {
                Debug.LogWarning("Orbital Ante: one or more planet shaders could not be used on this machine, so Unity's standard shaders are standing in for them. " +
                                 "Ground " + Describe(_terrain) + ", far bodies " + Describe(_body) + ", air rim " + Describe(_atmosphere) + ", water " + Describe(_water) + ".");
            }
            if (!_simple && _sky == null)
            {
                Debug.LogWarning("Orbital Ante: the sky shader could not be used on this machine, so the sky is a plain colour.");
            }
        }

        static string Describe(Shader shader) => shader != null ? "ok" : "standing in";

        static Shader Find(string file, string shaderName)
        {
            Shader shader = Resources.Load<Shader>("Shaders/" + file);
            if (shader == null) shader = Shader.Find(shaderName);
            return shader != null && shader.isSupported ? shader : null;
        }

        // ------------------------------------------------------------------ ground, rocks, caves

        /// <summary>
        /// The material for a body's ground tiles. With a far picture, distant ground blends into it, so what you
        /// see from orbit and what you see from a hilltop always agree.
        /// </summary>
        public static Material Ground(string bodyId)
        {
            Look();
            if (_terrain != null)
            {
                return new Material(_terrain) { name = bodyId + " ground" };
            }
            return new Material(VertexLitBase()) { name = bodyId + " ground" };
        }

        public static void SetFarPicture(Material ground, Texture2D picture, float blendStart, float blendEnd)
        {
            if (ground == null || picture == null || !ground.HasProperty(FarMapId)) return;
            ground.SetTexture(FarMapId, picture);
            ground.SetVector(FarBlendId, new Vector4(blendStart, blendEnd, 1f, picture.width));
        }

        /// <summary>
        /// A shared material that shows a mesh's own colours multiplied by <paramref name="tint"/>. Used for rocks,
        /// trees and cave walls. <paramref name="sunlight"/> 0 is for things the Sun never reaches (cave walls).
        /// </summary>
        /// <summary>
        /// For surfaces whose light was worked out beforehand and stored in the mesh colours (the ship's cabin):
        /// shows those colours times <paramref name="brightness"/> and nothing else. No sky light, lamp or fog.
        /// </summary>
        public static Material Baked(float brightness = 1f)
        {
            Material m = new Material(VertexColour(Color.white, 0f, Mathf.Clamp(brightness, 0f, 2f)));
            m.name = "OA_Baked";
            if (m.HasProperty(OutdoorsId)) m.SetFloat(OutdoorsId, 0f);
            return m;
        }

        /// <param name="paintScale">1 for ground and rocks. Larger makes the painted patches smaller, for things held in the hand.</param>
        public static Material VertexColour(Color tint, float sunlight = 1f, float glow = 0f, bool painted = true, float paintScale = 1f)
        {
            Look();
            Color32 c = tint;
            int scaleKey = Mathf.Clamp(Mathf.RoundToInt(paintScale), 1, 63);
            long key = ((long)c.r << 40) | ((long)c.g << 32) | ((long)c.b << 24) | ((long)Mathf.RoundToInt(Mathf.Clamp01(sunlight) * 100f) << 12) | (long)Mathf.RoundToInt(Mathf.Clamp(glow, 0f, 2f) * 100f) | (painted ? 0L : 1L << 50) | ((long)scaleKey << 52);
            if (VertexCache.TryGetValue(key, out Material cached) && cached != null) return cached;

            Material m;
            if (_terrain != null)
            {
                m = new Material(_terrain);
                m.SetColor(BaseColorId, tint);
                m.SetFloat(SunAmountId, sunlight);
                m.SetFloat(GlowId, glow);
                m.SetFloat(PaintId, painted ? 1f : 0f);
                m.SetFloat(PaintScaleId, scaleKey);
            }
            else if (sunlight < 0.5f || glow > 0.3f)
            {
                // No sunlight to switch off in the standard shaders, so cave walls are drawn unlit and dim instead.
                m = new Material(VertexUnlitBase());
                Color dim = glow > 0.3f ? tint : tint * 0.3f;
                dim.a = 1f;
                SetColor(m, dim);
            }
            else
            {
                m = new Material(VertexLitBase());
                SetColor(m, tint);
            }
            m.name = "OA_Vertex_" + ColorUtility.ToHtmlStringRGB(tint);
            VertexCache[key] = m;
            return m;
        }

        /// <summary>
        /// The shared material the game's own models are drawn with: the mesh's colours under the shared light,
        /// like <see cref="VertexColour"/>, plus shine where the model says a surface is smooth or bare metal
        /// (see OA_Model.shader). If that shader cannot be used the ground's stands in, without the shine.
        /// Shared: never change or destroy it.
        /// </summary>
        public static Material Model(float sunlight = 1f, float glow = 0f, float paintScale = 1f)
        {
            Look();
            if (_model == null) return VertexColour(Color.white, sunlight, glow, true, paintScale);
            int scaleKey = Mathf.Clamp(Mathf.RoundToInt(paintScale), 1, 63);
            int key = (Mathf.RoundToInt(Mathf.Clamp01(sunlight) * 100f) << 16) | (Mathf.RoundToInt(Mathf.Clamp(glow, 0f, 2f) * 100f) << 6) | scaleKey;
            if (ModelCache.TryGetValue(key, out Material cached) && cached != null) return cached;
            var m = new Material(_model) { name = "OA_Model" };
            m.SetColor(BaseColorId, Color.white);
            m.SetFloat(SunAmountId, Mathf.Clamp01(sunlight));
            m.SetFloat(GlowId, Mathf.Clamp(glow, 0f, 2f));
            m.SetFloat(PaintId, 1f);
            m.SetFloat(PaintScaleId, scaleKey);
            ModelCache[key] = m;
            return m;
        }

        // Unity's particle shaders are the standard ones that show a mesh's own colours.
        static Material VertexLitBase()
        {
            if (_vertexLit != null) return _vertexLit;
            _vertexLit = Resources.Load<Material>("Materials/OA_VertexLit");
            if (_vertexLit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
                if (shader == null) shader = Shader.Find("Particles/Standard Surface");
                _vertexLit = shader != null ? new Material(shader) { name = "OA_VertexLit (runtime)" } : new Material(Mat.Lit(Color.white, 0.05f));
            }
            return _vertexLit;
        }

        static Material VertexUnlitBase()
        {
            if (_vertexUnlit != null) return _vertexUnlit;
            _vertexUnlit = Resources.Load<Material>("Materials/OA_VertexUnlit");
            if (_vertexUnlit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                _vertexUnlit = shader != null ? new Material(shader) { name = "OA_VertexUnlit (runtime)" } : new Material(Mat.Unlit(Color.white));
            }
            return _vertexUnlit;
        }

        // ------------------------------------------------------------------ far bodies

        /// <summary>
        /// A material for a whole body seen from afar. <paramref name="emission"/> 1 is for the Sun;
        /// <paramref name="veil"/> is how completely a daytime sky hides it.
        /// </summary>
        /// <param name="boost">For things that shine by themselves: how much brighter than white, so they bloom.</param>
        public static Material Body(string name, Texture picture, Color tint, float emission = 0f, float veil = 1f, bool twoSided = false, float boost = 1f)
        {
            Look();
            Material m;
            if (_body != null)
            {
                m = new Material(_body);
                m.SetColor(BaseColorId, tint);
                m.SetFloat(EmissionId, emission);
                m.SetFloat(VeilId, veil);
                m.SetFloat(TwoSidedId, twoSided ? 1f : 0f);
                m.SetFloat(BoostId, Mathf.Clamp(boost, 1f, 12f));
            }
            else
            {
                m = new Material(emission > 0.5f ? Mat.Unlit(tint) : Mat.Lit(tint, 0.05f));
            }
            m.name = name;
            SetPicture(m, picture);
            return m;
        }

        public static void SetPicture(Material material, Texture picture)
        {
            if (material == null || picture == null) return;
            if (material.HasProperty(BaseMapId)) material.SetTexture(BaseMapId, picture);
            else if (material.HasProperty(MainTexId)) material.SetTexture(MainTexId, picture);
        }

        public static void SetColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        /// <summary>The sky ball's material, or null if the sky shader is not available (the camera's plain background stands in).</summary>
        public static Material Sky(float backdropStrength)
        {
            Look();
            if (_sky == null) return null;
            var m = new Material(_sky) { name = "OA_Sky (runtime)" };
            m.SetFloat(BackdropStrengthId, backdropStrength);
            return m;
        }

        public static void SetBackdrop(Material sky, Texture picture)
        {
            if (sky != null && picture != null && sky.HasProperty(BackdropId)) sky.SetTexture(BackdropId, picture);
        }

        /// <summary>The glowing rim of air seen from space, or null if the shader for it is not available.</summary>
        public static Material Atmosphere(string name, Color color, float strength, float limb)
        {
            Look();
            if (_atmosphere == null) return null;
            var m = new Material(_atmosphere) { name = name };
            m.SetColor(BaseColorId, color);
            m.SetFloat(StrengthId, strength);
            m.SetFloat(LimbId, Mathf.Clamp(limb, 0.05f, 0.98f));
            return m;
        }

        // ------------------------------------------------------------------ liquids

        /// <summary>The surface of a sea, lake or pool. <paramref name="glow"/> above zero is for lava.</summary>
        public static Material Water(string name, Color color, Color deep, float opacity, float ripple, float glow = 0f)
        {
            Look();
            Material m;
            if (_water != null)
            {
                m = new Material(_water);
                color.a = opacity;
                m.SetColor(BaseColorId, color);
                m.SetColor(DeepColorId, deep);
                m.SetFloat(RippleId, ripple);
                m.SetFloat(GlowId, glow);
            }
            else if (glow > 0.3f)
            {
                color.a = 1f;
                m = new Material(Mat.Unlit(color));
            }
            else
            {
                // The standard shader stand-in is solid, not see-through: simple and cannot go wrong.
                color.a = 1f;
                m = new Material(Mat.Lit(color, 0.85f));
                if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, 0.85f);
            }
            m.name = name;
            return m;
        }
    }
}
