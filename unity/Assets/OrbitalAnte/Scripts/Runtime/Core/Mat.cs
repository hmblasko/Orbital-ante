using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Hands out simple flat-colour materials for placeholder geometry. Materials are shared per colour so
    /// the renderer can batch them. Works in URP and falls back to the built-in pipeline if URP is not set up.
    /// </summary>
    public static class Mat
    {
        static Material _litBase;
        static Material _unlitBase;
        static readonly Dictionary<long, Material> LitCache = new Dictionary<long, Material>();
        static readonly Dictionary<long, Material> UnlitCache = new Dictionary<long, Material>();

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

        static bool UsingUrp => GraphicsSettings.currentRenderPipeline != null;

        public static Material Lit(Color color, float smoothness = 0.2f)
        {
            long key = Key(color, smoothness);
            if (LitCache.TryGetValue(key, out var cached) && cached != null) return cached;

            if (_litBase == null)
            {
                _litBase = Resources.Load<Material>("Materials/OA_Lit");
                if (_litBase == null)
                {
                    var shader = UsingUrp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    _litBase = new Material(shader) { name = "OA_Lit (runtime)" };
                }
            }

            var m = new Material(_litBase) { name = "OA_Lit_" + ColorUtility.ToHtmlStringRGB(color) };
            ApplyColor(m, color);
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, smoothness);
            if (m.HasProperty(GlossinessId)) m.SetFloat(GlossinessId, smoothness);
            m.enableInstancing = true;
            LitCache[key] = m;
            return m;
        }

        public static Material Unlit(Color color)
        {
            long key = Key(color, 0f);
            if (UnlitCache.TryGetValue(key, out var cached) && cached != null) return cached;

            if (_unlitBase == null)
            {
                _unlitBase = Resources.Load<Material>("Materials/OA_Unlit");
                if (_unlitBase == null)
                {
                    var shader = UsingUrp ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
                    if (shader == null) shader = Shader.Find("Unlit/Color");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    _unlitBase = new Material(shader) { name = "OA_Unlit (runtime)" };
                }
            }

            var m = new Material(_unlitBase) { name = "OA_Unlit_" + ColorUtility.ToHtmlStringRGB(color) };
            ApplyColor(m, color);
            UnlitCache[key] = m;
            return m;
        }

        static void ApplyColor(Material m, Color color)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
        }

        static long Key(Color color, float smoothness)
        {
            Color32 c = color;
            long rgba = ((long)c.r << 24) | ((long)c.g << 16) | ((long)c.b << 8) | c.a;
            long s = (long)Mathf.RoundToInt(Mathf.Clamp01(smoothness) * 100f);
            return (rgba << 8) | s;
        }
    }
}
