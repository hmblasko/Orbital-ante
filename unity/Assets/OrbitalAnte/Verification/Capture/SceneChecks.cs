using System;
using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Verification.Capture
{
    [Serializable]
    public sealed class Finding
    {
        /// <summary>"error" (a verified defect), "warning" (needs a look) or "info".</summary>
        public string severity;
        public string check;
        public string where;
        public string detail;
    }

    /// <summary>
    /// Mechanical checks on whatever is in the scene right now: the defects that can be detected without
    /// judgment. Each finding names the object, so the Critic can go and look at it.
    /// </summary>
    public static class SceneChecks
    {
        public static List<Finding> Run(string label)
        {
            var found = new List<Finding>();
            void Add(string severity, string check, UnityEngine.Object obj, string detail)
                => found.Add(new Finding { severity = severity, check = check, where = label + ": " + PathOf(obj), detail = detail });

            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (Component c in t.GetComponents<Component>())
                    if (c == null) { Add("error", "missing_script", t, "A component's script is missing."); break; }
                Vector3 p = t.position;
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
                    Add("error", "invalid_transform", t, "Position is not a number.");
            }

            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled) continue;
                Material[] materials = r.sharedMaterials;
                if (materials.Length == 0 && !(r is ParticleSystemRenderer)) Add("error", "missing_material", r, "Renderer has no material.");
                foreach (Material m in materials)
                {
                    if (m == null) { Add("error", "missing_material", r, "A material slot is empty (draws pink)."); continue; }
                    if (m.shader == null) Add("error", "missing_shader", r, "Material '" + m.name + "' has no shader.");
                    else if (m.shader.name == "Hidden/InternalErrorShader") Add("error", "shader_error", r, "Material '" + m.name + "' uses Unity's error shader (its own shader failed or is missing).");
                    else if (!m.shader.isSupported) Add("error", "shader_unsupported", r, "Shader '" + m.shader.name + "' is not supported on this graphics device.");
                }
                if (r is SkinnedMeshRenderer skin)
                {
                    if (skin.sharedMesh == null) Add("error", "missing_mesh", r, "Skinned renderer has no mesh.");
                    else
                    {
                        if (skin.bones == null || skin.bones.Length != skin.sharedMesh.bindposes.Length)
                            Add("error", "broken_rig_binding", r, "Bone count does not match the mesh's bind poses.");
                        else foreach (Transform bone in skin.bones)
                            if (bone == null) { Add("error", "broken_rig_binding", r, "A bone reference is missing."); break; }
                    }
                }
                Bounds b = r.bounds;
                if (float.IsNaN(b.size.x) || float.IsNaN(b.size.y) || float.IsNaN(b.size.z)) Add("error", "invalid_bounds", r, "Renderer bounds are not a number.");
            }

            foreach (MeshFilter f in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var renderer = f.GetComponent<MeshRenderer>();
                if (f.sharedMesh == null && renderer != null && renderer.enabled) Add("error", "missing_mesh", f, "Mesh filter has no mesh but its renderer is on.");
            }

            foreach (Collider c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c is MeshCollider mc && mc.enabled && mc.sharedMesh == null) Add("error", "collider_without_mesh", c, "Mesh collider has no mesh.");
                Vector3 s = c.transform.lossyScale;
                if ((c is BoxCollider || c is SphereCollider || c is CapsuleCollider) && (s.x < 0f || s.y < 0f || s.z < 0f))
                    Add("warning", "negative_scale_collider", c, "Collider on a negatively scaled object; physics may behave oddly.");
            }

            foreach (Animator a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (a.avatar != null && !a.avatar.isValid) Add("error", "invalid_avatar", a, "The avatar is not valid.");
                if (!a.enabled) { Add("info", "animator_disabled", a, "Animator is off; motion comes from code."); continue; }
                if (a.runtimeAnimatorController == null) { Add("error", "missing_animation_controller", a, "Animator is on but has no controller."); continue; }
                foreach (AnimationClip clip in a.runtimeAnimatorController.animationClips)
                    if (clip == null) { Add("error", "missing_animation_clip", a, "The controller refers to a clip that is missing."); break; }
                foreach (AnimatorControllerParameter p in a.parameters)
                    if (string.IsNullOrEmpty(p.name)) Add("warning", "unnamed_animator_parameter", a, "A parameter has no name.");
                if (a.applyRootMotion) Add("info", "root_motion_on", a, "Root motion is applied. Confirm that is intended for this character.");
            }

            int listeners = 0;
            foreach (AudioListener l in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.enabled) listeners++;
            if (listeners > 1) found.Add(new Finding { severity = "error", check = "audio_listeners", where = label, detail = listeners + " audio listeners are on at once." });

            int cameras = 0;
            foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (c.enabled && c.targetTexture == null) cameras++;
            if (cameras > 1) found.Add(new Finding { severity = "warning", check = "cameras", where = label, detail = cameras + " cameras are drawing to the screen at once." });
            return found;
        }

        public static string PathOf(UnityEngine.Object obj)
        {
            Transform t = obj is Component c ? c.transform : obj is GameObject g ? g.transform : null;
            if (t == null) return obj != null ? obj.name : "(none)";
            string path = t.name;
            for (int depth = 0; t.parent != null && depth < 6; depth++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
