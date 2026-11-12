using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Things that rise out of the ground: steam from hot springs, geysers that erupt and rest, smoke from
    /// volcanic vents, lava fountains, the black smoke of sea-floor vents, and the dark geysers of an ice moon.
    /// Only the few nearest the viewer are drawn. Each puff is a small solid lump that shrinks away, in keeping
    /// with the game's solid, cartoon look (and far cheaper to draw than see-through smoke).
    /// </summary>
    public sealed class PlumeFx : MonoBehaviour
    {
        const int MaxActive = 5;
        const float Range = 650f;

        sealed class Emitter
        {
            public GameObject Go;
            public ParticleSystem System;
            public ParticleSystemRenderer Renderer;
            public PlumeSite Site;
            public PlanetSurface Surface;
            public float Clock;
        }

        PlanetWorld _world;
        readonly List<Emitter> _emitters = new List<Emitter>();
        readonly List<PlumeSite> _nearest = new List<PlumeSite>();
        readonly List<float> _nearestDistance = new List<float>();
        float _nextLook;

        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _emitters.Count; i++) if (_emitters[i].Site != null) n++;
                return n;
            }
        }

        public void Build(PlanetWorld world)
        {
            _world = world;
            for (int i = 0; i < MaxActive; i++) _emitters.Add(NewEmitter(i));
        }

        Emitter NewEmitter(int index)
        {
            var e = new Emitter { Go = new GameObject("Plume" + (index + 1)) };
            e.Go.transform.SetParent(transform, false);
            e.System = e.Go.AddComponent<ParticleSystem>();
            e.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = e.System.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.gravityModifier = 0f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.ShapeModule shape = e.System.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            ParticleSystem.SizeOverLifetimeModule size = e.System.sizeOverLifetime;
            size.enabled = true;
            // Swell quickly, then shrink away to nothing.
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.25f), new Keyframe(0.25f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f)));
            ParticleSystem.ForceOverLifetimeModule force = e.System.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            e.Renderer = e.Go.GetComponent<ParticleSystemRenderer>();
            e.Renderer.renderMode = ParticleSystemRenderMode.Mesh;
            e.Renderer.mesh = FeatureMeshes.Get(FeatureKind.Boulder, index);
            e.Renderer.alignment = ParticleSystemRenderSpace.World;
            e.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            e.Renderer.receiveShadows = false;
            e.Go.SetActive(false);
            return e;
        }

        void Update()
        {
            if (_world == null || _world.Surfaces == null || _world.System == null) return;
            Camera cam = _world.System.ViewCamera;
            if (cam == null) return;

            if (Time.unscaledTime >= _nextLook)
            {
                _nextLook = Time.unscaledTime + 0.5f;
                Choose(cam.transform.position);
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < _emitters.Count; i++)
            {
                Emitter e = _emitters[i];
                if (e.Site == null) continue;
                // Geysers erupt for a few seconds, then rest.
                if (e.Site.Kind == PlumeKind.Geyser || e.Site.Kind == PlumeKind.DarkGeyser)
                {
                    e.Clock += dt;
                    float cycle = e.Site.Kind == PlumeKind.Geyser ? 14f : 22f;
                    bool erupting = Mathf.Repeat(e.Clock, cycle) < cycle * 0.4f;
                    ParticleSystem.EmissionModule emission = e.System.emission;
                    emission.enabled = erupting;
                }
            }
        }

        /// <summary>Works out which plumes are nearest and gives each one an emitter.</summary>
        void Choose(Vector3 eye)
        {
            _nearest.Clear();
            _nearestDistance.Clear();
            PlanetSurface surface = null;
            for (int b = 0; b < _world.Surfaces.Length; b++)
            {
                PlanetSurface s = _world.Surfaces[b];
                if (s == null || !s.GroundShown || s.Terrain.Plumes.Count == 0) continue;
                Vec3d viewer = SpaceConvert.ToVec3d(s.Body.transform.InverseTransformPoint(eye));
                for (int i = 0; i < s.Terrain.Plumes.Count; i++)
                {
                    PlumeSite site = s.Terrain.Plumes[i];
                    Vec3d at = site.Direction * (s.SurfaceRadius + site.Height);
                    float d = (float)Vec3d.Distance(at, viewer);
                    if (d > Range) continue;
                    int slot = _nearest.Count;
                    while (slot > 0 && _nearestDistance[slot - 1] > d) slot--;
                    if (slot >= MaxActive) continue;
                    _nearest.Insert(slot, site);
                    _nearestDistance.Insert(slot, d);
                    if (_nearest.Count > MaxActive)
                    {
                        _nearest.RemoveAt(MaxActive);
                        _nearestDistance.RemoveAt(MaxActive);
                    }
                }
                // Only one body can be this close at a time.
                surface = s;
                break;
            }

            // Stop emitters whose plume is no longer among the nearest.
            for (int i = 0; i < _emitters.Count; i++)
            {
                Emitter e = _emitters[i];
                if (e.Site == null) continue;
                if (surface != e.Surface || !_nearest.Contains(e.Site))
                {
                    e.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    e.Go.SetActive(false);
                    e.Site = null;
                    e.Surface = null;
                }
            }
            // Start one for each newly near plume.
            for (int n = 0; n < _nearest.Count; n++)
            {
                bool running = false;
                Emitter free = null;
                for (int i = 0; i < _emitters.Count; i++)
                {
                    if (_emitters[i].Site == _nearest[n]) running = true;
                    else if (_emitters[i].Site == null && free == null) free = _emitters[i];
                }
                if (!running && free != null) Launch(free, surface, _nearest[n]);
            }
        }

        void Launch(Emitter e, PlanetSurface s, PlumeSite site)
        {
            e.Site = site;
            e.Surface = s;
            // Each geyser keeps its own time, so a field of them does not erupt in step.
            e.Clock = (float)(Noise3.Hash01((int)(site.Direction.X * 100000), (int)(site.Direction.Z * 100000), 5, 77u) * 20.0);
            Transform t = e.Go.transform;
            t.SetParent(s.Body.transform, false);
            Vector3 up = SpaceConvert.ToVector3(site.Direction);
            t.localPosition = up * (float)(s.SurfaceRadius + site.Height + 0.3);
            // The emitter points "forward", so forward is aimed straight up out of the ground.
            t.localRotation = Quaternion.LookRotation(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right);

            float gravity = (float)(s.Body.Spec.surfaceGravity * s.SurfaceRadius / s.BodyRadius);
            float scale = Mathf.Lerp(0.6f, 1.2f, (float)s.Terrain.Scale);
            ParticleSystem.MainModule main = e.System.main;
            main.customSimulationSpace = s.Body.transform;
            ParticleSystem.EmissionModule emission = e.System.emission;
            emission.enabled = true;
            ParticleSystem.ShapeModule shape = e.System.shape;
            ParticleSystem.ForceOverLifetimeModule force = e.System.forceOverLifetime;
            force.x = 0f;
            force.y = 0f;
            force.z = 0f;

            Color color;
            float glow = 0f;
            switch (site.Kind)
            {
                case PlumeKind.Geyser:
                    color = new Color(0.88f, 0.94f, 1f);
                    main.startLifetime = 3.2f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(14f * scale, 22f * scale);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.9f * scale, 2.2f * scale);
                    emission.rateOverTime = 13f;
                    shape.angle = 5f;
                    shape.radius = 0.4f;
                    force.z = -gravity;
                    break;
                case PlumeKind.DarkGeyser:
                    // Thin air and weak gravity: a tall dark column that leans over with the wind high up.
                    color = new Color(0.16f, 0.14f, 0.15f);
                    main.startLifetime = 7f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(11f, 15f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3f);
                    emission.rateOverTime = 6f;
                    shape.angle = 3f;
                    shape.radius = 0.5f;
                    force.z = -gravity * 0.6f;
                    force.x = 1.6f;
                    break;
                case PlumeKind.Smoke:
                    color = new Color(0.22f, 0.2f, 0.19f);
                    main.startLifetime = 7f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(2f * scale, 5f * scale);
                    emission.rateOverTime = 5f;
                    shape.angle = 9f;
                    shape.radius = 1.2f;
                    force.x = 0.5f;
                    break;
                case PlumeKind.Fountain:
                    color = new Color(1f, 0.45f, 0.08f);
                    glow = 1.2f;
                    main.startLifetime = 3f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(9f * scale, 17f * scale);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
                    emission.rateOverTime = 14f;
                    shape.angle = 16f;
                    shape.radius = 0.6f;
                    force.z = -gravity;
                    break;
                case PlumeKind.BlackSmoker:
                    color = new Color(0.07f, 0.07f, 0.08f);
                    main.startLifetime = 6f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
                    emission.rateOverTime = 6f;
                    shape.angle = 7f;
                    shape.radius = 0.3f;
                    break;
                default:
                    color = new Color(0.93f, 0.95f, 0.96f);
                    main.startLifetime = 5f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.2f * scale, 3.2f * scale);
                    emission.rateOverTime = 5f;
                    shape.angle = 12f;
                    shape.radius = 0.8f;
                    force.x = 0.35f;
                    break;
            }
            e.Renderer.sharedMaterial = PlanetMaterials.VertexColour(color, 1f, glow);
            e.Go.SetActive(true);
            e.System.Play(true);
        }

        void OnDestroy()
        {
            // Emitters are parented to bodies while running; make sure none is left behind.
            for (int i = 0; i < _emitters.Count; i++)
            {
                if (_emitters[i].Go != null) Destroy(_emitters[i].Go);
            }
        }
    }
}
