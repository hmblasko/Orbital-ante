using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Feel;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OrbitalAnte.Fx
{
    /// <summary>
    /// Blood splats using URP Decal Projectors, pooled and capped for the Deck. The oldest splat
    /// is reused when the cap is hit; old splats fade out at the end of their life.
    /// </summary>
    public class DecalFx : MonoBehaviour
    {
        static DecalFx instance;

        GameFeelConfig cfg;
        Material material;
        readonly List<(DecalProjector projector, float born)> decals = new List<(DecalProjector, float)>();
        int next;

        static DecalFx Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("DecalFx").AddComponent<DecalFx>();
                return instance;
            }
        }

        void Awake()
        {
            cfg = GameFeelConfig.Get();
            material = MaterialLibrary.Decal(FxTextures.Splat(cfg.bloodColor, cfg.bloodColorDark));
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static int ActiveCount => instance == null ? 0 : instance.decals.Count;

        /// <summary>Splat on the surface at <paramref name="point"/> facing <paramref name="normal"/>.</summary>
        public static void Splat(Vector3 point, Vector3 normal, float size)
        {
            var i = Instance;
            if (i.material == null) return;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            normal.Normalize();

            DecalProjector p;
            if (i.decals.Count < Mathf.Max(1, i.cfg.maxDecals))
            {
                var go = new GameObject("BloodDecal");
                go.transform.SetParent(i.transform, false);
                p = go.AddComponent<DecalProjector>();
                p.material = i.material;
                i.decals.Add((p, Time.time));
            }
            else
            {
                i.next %= i.decals.Count;
                p = i.decals[i.next].projector;
                i.decals[i.next] = (p, Time.time);
                i.next++;
            }

            float depth = 0.5f;
            p.size = new Vector3(size, size, depth);
            p.pivot = new Vector3(0, 0, depth * 0.5f);
            p.fadeFactor = 1f;
            p.drawDistance = 60f;
            p.transform.position = point + normal * (depth * 0.25f);
            p.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            p.gameObject.SetActive(true);
        }

        /// <summary>Splat on whatever is below a point (used for wounds on standing targets).</summary>
        public static void SplatBelow(Vector3 point, float size, float maxDistance = 3f)
        {
            if (Physics.Raycast(point + Vector3.up * 0.05f, Vector3.down, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic) return; // don't splat on gibs
                Splat(hit.point, hit.normal, size);
            }
        }

        void Update()
        {
            float now = Time.time;
            float life = cfg.decalLifetimeSeconds;
            for (int k = 0; k < decals.Count; k++)
            {
                var (p, born) = decals[k];
                if (p == null || !p.gameObject.activeSelf) continue;
                float age = now - born;
                if (age > life) p.gameObject.SetActive(false);
                else if (age > life - 3f) p.fadeFactor = Mathf.Clamp01((life - age) / 3f);
            }
        }
    }
}
