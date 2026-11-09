using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Feel;
using UnityEngine;

namespace OrbitalAnte.Fx
{
    /// <summary>
    /// Blood bursts at wounds and short trailing sprays on flying gibs. One shared burst system
    /// (world space, so it can be moved and re-emitted) plus a small pool of trail emitters.
    /// </summary>
    public class BloodFx : MonoBehaviour
    {
        static BloodFx instance;

        GameFeelConfig cfg;
        ParticleSystem burst;
        readonly List<Trail> trails = new List<Trail>();

        class Trail
        {
            public ParticleSystem System;
            public Transform Target;
            public float Until;
        }

        static BloodFx Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("BloodFx").AddComponent<BloodFx>();
                return instance;
            }
        }

        void Awake()
        {
            cfg = GameFeelConfig.Get();
            burst = CreateSystem("Burst", false);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Spray from a wound. <paramref name="outward"/> is the direction the blood flies.</summary>
        public static void Burst(Vector3 point, Vector3 outward, int count)
        {
            if (count <= 0) return;
            var i = Instance;
            if (outward.sqrMagnitude < 0.0001f) outward = Vector3.up;
            i.burst.transform.SetPositionAndRotation(point, Quaternion.LookRotation(outward.normalized));
            i.burst.Emit(count);
        }

        /// <summary>Short spray that follows a flying gib.</summary>
        public static void TrailOn(Transform target)
        {
            if (target == null) return;
            var i = Instance;
            Trail t = null;
            foreach (var tr in i.trails)
                if (tr.Target == null || Time.time > tr.Until) { t = tr; break; }
            if (t == null)
            {
                if (i.trails.Count >= i.cfg.maxTrails)
                {
                    t = i.trails[0]; // steal the oldest
                    i.trails.RemoveAt(0);
                    i.trails.Add(t);
                }
                else
                {
                    t = new Trail { System = i.CreateSystem("Trail", true) };
                    i.trails.Add(t);
                }
            }
            t.Target = target;
            t.Until = Time.time + i.cfg.trailSeconds;
            t.System.transform.position = target.position;
            var em = t.System.emission;
            em.enabled = true;
            if (!t.System.isPlaying) t.System.Play();
        }

        void LateUpdate()
        {
            foreach (var t in trails)
            {
                bool active = t.Target != null && t.Target.gameObject.activeInHierarchy && Time.time < t.Until;
                var em = t.System.emission;
                if (!active)
                {
                    if (em.enabled) em.enabled = false;
                    continue;
                }
                t.System.transform.position = t.Target.position;
                float left = (t.Until - Time.time) / Mathf.Max(0.01f, cfg.trailSeconds);
                em.rateOverTime = cfg.trailRate * Mathf.Clamp01(left);
            }
        }

        ParticleSystem CreateSystem(string name, bool continuous)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = continuous;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSpeed = continuous ? new ParticleSystem.MinMaxCurve(0.2f, 1.2f) : new ParticleSystem.MinMaxCurve(1.5f, 7.5f);
            main.startSize = continuous ? new ParticleSystem.MinMaxCurve(0.03f, 0.07f) : new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(cfg.bloodColor, cfg.bloodColorDark);
            main.gravityModifier = 1.5f;
            main.maxParticles = continuous ? 120 : 600;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = continuous ? cfg.trailRate : 0f;
            emission.enabled = continuous;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = continuous ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = continuous ? 0.05f : 0.03f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = MaterialLibrary.Particle(FxTextures.Dot);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }
    }
}
