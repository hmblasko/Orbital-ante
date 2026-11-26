using System;
using System.Reflection;
using OrbitalAnte.Planets;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitalAnte.Look
{
    /// <summary>
    /// Switches the Sun's cast shadows on and off (the Look asset's "Sun Shadows") and gives URP the settings they
    /// are drawn with. URP is reached by name, as in PostFx, so the game still compiles where the package's
    /// version differs. The game's own shaders read the shadow picture in OA_SunShade (OA_Look.hlsl) and draw into
    /// it with their ShadowCaster passes; Unity's standard shaders, when they stand in, use it by themselves.
    /// Orbital Ante > Set Up Project saves the same settings into the pipeline asset, which a built game needs.
    /// </summary>
    public static class SunShadows
    {
        /// <summary>What happened the last time the settings were applied, for the F3 readout and for reports.</summary>
        public static string Status { get; private set; } = "not set";

        public static bool Wanted(LookConfig look) => look != null && look.realLight && look.sunShadows;

        /// <summary>
        /// Says whether one thing casts a shadow: only when it is meant to (<paramref name="should"/>), Sun Shadows
        /// is on, and the material it wears can draw itself into the shadow picture (the game's own ground and
        /// model shaders can; a stand-in or glowing material may not). Everything on a world is made with casting
        /// off, so a thing casts only where the code that makes it calls this. What casts is listed in
        /// docs/performance_budget.md ("Sun shadows"); small plants, glowing pieces, cave walls, the cabin and task
        /// views never do.
        /// </summary>
        public static void Cast(Renderer renderer, bool should)
        {
            if (renderer == null) return;
            bool on = should && Wanted(LookConfig.Load()) && CanCast(renderer.sharedMaterial);
            renderer.shadowCastingMode = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
            // URP's own shaders obey this; the game's shaders read the shadow picture whatever it says.
            renderer.receiveShadows = on || renderer.receiveShadows;
        }

        /// <summary>True if a material has a pass that draws into the shadow picture.</summary>
        public static bool CanCast(Material material)
        {
            return material != null && material.FindPass("ShadowCaster") >= 0;
        }

        public static void Apply(Light sun, LookConfig look)
        {
            if (sun == null) return;
            bool on = Wanted(look);
            sun.shadows = on ? LightShadows.Soft : LightShadows.None;
            if (!on)
            {
                Status = "off (Look asset)";
                return;
            }
            // How dark a shadow is belongs to the game's shaders (Shadow Darkness), so the light itself casts full ones.
            sun.shadowStrength = 1f;
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                Status = "on, no render pipeline (run Orbital Ante > Set Up Project)";
                return;
            }
            int set = 0;
            set += Set(pipeline, "supportsMainLightShadows", true) ? 1 : 0;
            set += Set(pipeline, "supportsSoftShadows", true) ? 1 : 0;
            set += Set(pipeline, "shadowDistance", SunShadowRules.Distance(look.shadowDistance)) ? 1 : 0;
            set += Set(pipeline, "shadowCascadeCount", SunShadowRules.Cascades) ? 1 : 0;
            set += Set(pipeline, "cascade2Split", SunShadowRules.NearShare) ? 1 : 0;
            set += Set(pipeline, "mainLightShadowmapResolution", SunShadowRules.Resolution) ? 1 : 0;
            // The cheapest soft edge (four reads a dot). Not counted: an older URP has no such setting.
            Set(pipeline, "softShadowQuality", SunShadowRules.SoftQuality);
            Status = set == 6 ? "on" : "on, " + (6 - set) + " of 6 settings could not be set (shadows may be coarse or missing)";
        }

        /// <summary>Sets a property by name, whether or not its setter is public. False if it is not there or refuses.</summary>
        static bool Set(object target, string name, object value)
        {
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                PropertyInfo property = target.GetType().GetProperty(name, flags);
                if (property == null) return false;
                MethodInfo setter = property.GetSetMethod(true);
                if (setter == null) return false;
                object current = property.GetValue(target, null);
                object wanted = property.PropertyType.IsEnum ? Enum.ToObject(property.PropertyType, value) : Convert.ChangeType(value, property.PropertyType);
                if (Equals(current, wanted)) return true;
                setter.Invoke(target, new[] { wanted });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
