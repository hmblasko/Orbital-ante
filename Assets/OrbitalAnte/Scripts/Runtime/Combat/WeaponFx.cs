using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>Tracers and bullet impact sparks, pooled.</summary>
    public class WeaponFx : MonoBehaviour
    {
        static WeaponFx instance;
        LineRenderer[] tracers;
        float[] tracerUntil;
        int nextTracer;
        ParticleSystem sparks;

        static WeaponFx Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("WeaponFx").AddComponent<WeaponFx>();
                return instance;
            }
        }

        void Awake()
        {
            tracers = new LineRenderer[16];
            tracerUntil = new float[tracers.Length];
            for (int i = 0; i < tracers.Length; i++)
            {
                var go = new GameObject("Tracer" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.03f;
                lr.endWidth = 0.012f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                tracers[i] = lr;
            }
            sparks = CreateSparks();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color)
        {
            var i = Instance;
            var lr = i.tracers[i.nextTracer];
            i.tracerUntil[i.nextTracer] = Time.unscaledTime + 0.045f;
            i.nextTracer = (i.nextTracer + 1) % i.tracers.Length;
            lr.sharedMaterial = MaterialLibrary.Unlit(color);
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.enabled = true;
        }

        public static void Impact(Vector3 point, Vector3 normal)
        {
            var i = Instance;
            i.sparks.transform.SetPositionAndRotation(point + normal * 0.01f, Quaternion.LookRotation(normal));
            i.sparks.Emit(10);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < tracers.Length; i++)
                if (tracers[i].enabled && now > tracerUntil[i]) tracers[i].enabled = false;
        }

        ParticleSystem CreateSparks()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.55f), new Color(0.75f, 0.72f, 0.68f));
            main.gravityModifier = 0.8f;
            main.maxParticles = 300;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.01f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Particle(OrbitalAnte.Fx.FxTextures.Dot);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
    }
}
