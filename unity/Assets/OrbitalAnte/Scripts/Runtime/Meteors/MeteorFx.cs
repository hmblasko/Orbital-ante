using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using UnityEngine;

namespace OrbitalAnte.Meteors
{
    /// <summary>
    /// Everything a strike looks like: the falling rocks (a glowing head and flame tail in air, a dark rock and a
    /// faint streak in vacuum), the smoke they leave, the flash and fire where a rock bursts or lands, the dust
    /// and grit thrown up, and the rocks that are thrown out and stay lying round the crater.
    ///
    /// All of it rides with the body it is on: the heads and lying rocks are children of the body, and the
    /// particles simulate in a frame that is a child of the body (local space), so the planet's turning and the
    /// world being slid back to Unity's zero point move everything with it. Smoke and dust are solid lumps,
    /// like the rest of the game's smoke. Counts are capped (MeteorConfig.maxParticles, MeteorRules.maxChunks).
    /// Runs after LaunchFx (order 100), so its ground flash is not wiped by the engines' "no flare" each frame.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class MeteorFx : MonoBehaviour
    {
        public const int MaxHeads = 8;

        sealed class Head
        {
            public GameObject Go;
            public Transform Rock, Glow, Tail;
            public MeshRenderer RockRenderer, TailRenderer;
            public bool ShownHot, ShownCool;
            public Vector3 LastPuff;
            public bool HasLast;
        }

        sealed class Chunk
        {
            public GameObject Go;
            public Transform Body;
            public Vector3 Position, Velocity, SpinAxis;
            public float Spin, Size, HotUntil;
            public int Bounces;
            public bool Flying;
            public MeshRenderer Renderer;
            /// <summary>The ground this rock falls back onto (body space), kept per rock: strikes overlap.</summary>
            public System.Func<Vector3, float> Ground;
        }

        struct Smoker
        {
            public Vector3 Local;
            public float Started, Seconds, Size, Carry;
        }

        MeteorConfig _config;
        Mesh[] _rocks;
        Mesh _ball, _lump, _tail, _gritRock;
        ParticleSystem.Particle[] _moving;
        bool _flareLit;
        Color _flareWritten;
        Material _rockMat, _hotRockMat, _headGlowMat, _tailMat, _streakMat, _flashMat, _fireMat, _sparkMat, _smokeMat, _gritMat;
        Transform _root, _rootBody;
        ParticleSystem _fire, _smoke, _dust, _grit;
        ParticleSystem.Particle[] _unused;
        readonly Head[] _heads = new Head[MaxHeads];
        readonly List<Chunk> _chunks = new List<Chunk>();
        readonly List<Smoker> _smokers = new List<Smoker>();
        Transform _flash;
        float _flashStart = -10f, _flashLength = 0.4f, _flashSize = 1f;
        Vector3 _flareWorldLocal;
        float _flareStart = -10f, _flareReach, _flareStrength;
        bool _air;
        float _gravity = 9.8f;
        Color _dustColour = new Color(0.6f, 0.58f, 0.55f);

        public int ChunkCount => _chunks.Count;
        public int ParticleCount => (_fire ? _fire.particleCount : 0) + (_smoke ? _smoke.particleCount : 0) + (_dust ? _dust.particleCount : 0) + (_grit ? _grit.particleCount : 0);

        public static MeteorFx Create(Transform parent, MeteorConfig config)
        {
            var go = new GameObject("MeteorFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<MeteorFx>();
            fx._config = config;
            fx.Build();
            return fx;
        }

        // ------------------------------------------------------------------ building

        void Build()
        {
            _rocks = new[] { RockMesh(1, 1), RockMesh(2, 1), RockMesh(3, 1) };
            // Particles are drawn as meshes built on the CPU every frame, so they get the plain 20-face shapes.
            _gritRock = RockMesh(4, 0);
            _ball = BallMesh(1);
            _lump = BallMesh(0);
            _tail = TailMesh(10);
            // A dark, burnt rock: what a meteorite looks like close to.
            _rockMat = PlanetMaterials.VertexColour(new Color(0.24f, 0.21f, 0.19f), 1f, 0f, true, 8f);
            _hotRockMat = PlanetMaterials.VertexColour(new Color(0.55f, 0.2f, 0.07f), 0.4f, 1.2f, false);
            // Things that shine by themselves are drawn brighter than white so they bloom (see LaunchFx's flame).
            _headGlowMat = PlanetMaterials.VertexColour(new Color(1f, 0.82f, 0.55f), 0f, 2f, false);
            _tailMat = PlanetMaterials.VertexColour(new Color(1f, 0.55f, 0.2f), 0f, 2f, false);
            _streakMat = PlanetMaterials.VertexColour(new Color(0.7f, 0.72f, 0.78f), 0f, 0.7f, false);
            _flashMat = PlanetMaterials.VertexColour(new Color(1f, 0.9f, 0.7f), 0f, 2f, false);
            _fireMat = PlanetMaterials.VertexColour(new Color(1f, 0.5f, 0.16f), 0f, 2f, false);
            _sparkMat = PlanetMaterials.VertexColour(new Color(1f, 0.72f, 0.3f), 0f, 2f, false);
            _smokeMat = PlanetMaterials.VertexColour(new Color(0.34f, 0.32f, 0.31f), 1f, 0f, false);
            _gritMat = PlanetMaterials.VertexColour(new Color(0.22f, 0.2f, 0.18f), 1f, 0f, false);

            for (int i = 0; i < MaxHeads; i++)
            {
                var h = new Head { Go = new GameObject("Meteor" + i) };
                h.Go.transform.SetParent(transform, false);
                h.Rock = Part(h.Go.transform, "Rock", _rocks[i % _rocks.Length], _rockMat);
                h.Glow = Part(h.Go.transform, "Glow", _ball, _headGlowMat);
                h.Tail = Part(h.Go.transform, "Tail", _tail, _tailMat);
                h.RockRenderer = h.Rock.GetComponent<MeshRenderer>();
                h.TailRenderer = h.Tail.GetComponent<MeshRenderer>();
                h.Go.SetActive(false);
                _heads[i] = h;
            }
            _flash = Part(transform, "Flash", _ball, _flashMat);
            _flash.gameObject.SetActive(false);

            _root = new GameObject("MeteorParticles").transform;
            _root.SetParent(transform, false);
            int max = Mathf.Clamp(_config.maxParticles, 50, 1200);
            _fire = NewParticles("Fire", Mathf.Max(20, max * 18 / 100), _fireMat);
            _smoke = NewParticles("Smoke", Mathf.Max(20, max * 38 / 100), _smokeMat);
            _dust = NewParticles("Dust", Mathf.Max(20, max * 30 / 100), _smokeMat);
            _grit = NewParticles("Grit", Mathf.Max(10, max * 14 / 100), _gritMat);
            _grit.GetComponent<ParticleSystemRenderer>().mesh = _gritRock;
            _moving = new ParticleSystem.Particle[Mathf.Max(_fire.main.maxParticles, Mathf.Max(_smoke.main.maxParticles, Mathf.Max(_dust.main.maxParticles, _grit.main.maxParticles)))];

            Curve(_fire, new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));
            Curve(_smoke, new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0f));
            Curve(_dust, new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(0.8f, 0.85f), new Keyframe(1f, 0f));
            Curve(_grit, new Keyframe(0f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0f));
            Drag(_fire, 0.12f);
            Drag(_smoke, 0.08f);
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        ParticleSystem NewParticles(string name, int max, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = max;
            // Strikes are often out of sight when they begin; they must keep moving anyway.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false; // every lump is let out by hand, so the counts follow the rules exactly
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = _lump;
            renderer.sharedMaterial = material;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play(true);
            return system;
        }

        static void Curve(ParticleSystem system, params Keyframe[] keys)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        static void Drag(ParticleSystem system, float dampen)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule slow = system.limitVelocityOverLifetime;
            slow.enabled = dampen > 0f;
            slow.limit = 0.6f;
            slow.dampen = dampen;
        }

        // ------------------------------------------------------------------ a strike begins

        /// <summary>
        /// Gets ready for a strike on a body: the particle frame is put on the target with its up out of the ground.
        /// Smoke still drifting from the last strike is cleared only if it is on another world; on the same world it
        /// is carried over into the new frame, so it stays where it was.
        /// </summary>
        public void Begin(Transform body, Vector3 targetInBody, Vector3 upInBody, bool air, float gravity, Color dust)
        {
            if (_rootBody != body)
            {
                ClearParticles();
                _smokers.Clear();
            }
            bool carry = _rootBody == body && (_smokers.Count > 0 || ParticleCount > 0);
            Matrix4x4 oldFrame = _root.localToWorldMatrix;
            _rootBody = body;
            _root.SetParent(body, false);
            _root.localPosition = targetInBody;
            _root.localRotation = Quaternion.FromToRotation(Vector3.up, upInBody.normalized);
            if (carry) Rebase(_root.worldToLocalMatrix * oldFrame);
            _air = air;
            _gravity = Mathf.Max(0.05f, gravity);
            _dustColour = dust;
            _dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = PlanetMaterials.VertexColour(Rounded(dust), 1f, 0f, false);

            // In air smoke and dust billow and rise; in vacuum dust is thrown in clean arcs and falls straight back.
            ParticleSystem.ForceOverLifetimeModule rise = _smoke.forceOverLifetime;
            rise.y = air ? 0.9f : 0f;
            ParticleSystem.ForceOverLifetimeModule dustFall = _dust.forceOverLifetime;
            dustFall.y = air ? 0.35f : -_gravity;
            Drag(_dust, air ? 0.07f : 0f);
            ParticleSystem.ForceOverLifetimeModule gritFall = _grit.forceOverLifetime;
            gritFall.y = -_gravity;
        }

        /// <summary>Moves live lumps and rising smoke from the old frame into the new one, so nothing jumps.</summary>
        void Rebase(Matrix4x4 oldToNew)
        {
            ParticleSystem[] systems = { _fire, _smoke, _dust, _grit };
            foreach (ParticleSystem system in systems)
            {
                if (system == null || system.particleCount == 0) continue;
                int n = system.GetParticles(_moving);
                for (int i = 0; i < n; i++)
                {
                    _moving[i].position = oldToNew.MultiplyPoint3x4(_moving[i].position);
                    _moving[i].velocity = oldToNew.MultiplyVector(_moving[i].velocity);
                }
                system.SetParticles(_moving, n);
            }
            for (int i = 0; i < _smokers.Count; i++)
            {
                Smoker s = _smokers[i];
                s.Local = oldToNew.MultiplyPoint3x4(s.Local);
                _smokers[i] = s;
            }
        }

        static Color Rounded(Color c) => new Color(Mathf.Round(c.r * 10f) / 10f, Mathf.Round(c.g * 10f) / 10f, Mathf.Round(c.b * 10f) / 10f, 1f);

        // ------------------------------------------------------------------ rocks in the air

        /// <summary>Shows one falling rock this frame, at a place and velocity in the body's space.</summary>
        public void ShowHead(int slot, Transform body, Vector3 position, Vector3 velocity, float rock)
        {
            if (slot < 0 || slot >= MaxHeads) return;
            Head h = _heads[slot];
            if (h.Go.transform.parent != body) h.Go.transform.SetParent(body, false);
            if (!h.Go.activeSelf)
            {
                h.Go.SetActive(true);
                h.HasLast = false;
            }
            float speed = velocity.magnitude;
            Vector3 forward = speed > 0.01f ? velocity / speed : Vector3.down;
            h.Go.transform.localPosition = position;
            h.Go.transform.localRotation = Quaternion.LookRotation(forward, Mathf.Abs(forward.y) < 0.95f ? Vector3.up : Vector3.right);
            h.Rock.localScale = Vector3.one * (rock * 2f);
            h.Rock.localRotation = Quaternion.Euler(Time.time * 140f, Time.time * 90f + slot * 40f, 0f);

            if (_air)
            {
                // The air in front of the rock is squeezed white hot; the flame streams behind.
                float flicker = 0.9f + 0.2f * Mathf.PerlinNoise(Time.time * 19f, slot);
                h.Glow.gameObject.SetActive(true);
                h.Glow.localScale = Vector3.one * (rock * 2.6f * flicker);
                if (!h.ShownHot)
                {
                    h.RockRenderer.sharedMaterial = _hotRockMat;
                    h.TailRenderer.sharedMaterial = _tailMat;
                    h.ShownHot = true;
                    h.ShownCool = false;
                }
                float width = rock * 2.2f * flicker;
                float length = Mathf.Clamp(speed * 0.12f, 4f, 30f) * Mathf.Sqrt(Mathf.Max(0.2f, rock / 0.45f));
                h.Tail.localScale = new Vector3(width, width, length);
                EmitTrail(h, body, position, rock);
            }
            else
            {
                // No air: nothing burns. The sunlit rock and a faint streak, so a player can still see it coming.
                h.Glow.gameObject.SetActive(false);
                if (!h.ShownCool)
                {
                    h.RockRenderer.sharedMaterial = _rockMat;
                    h.TailRenderer.sharedMaterial = _streakMat;
                    h.ShownCool = true;
                    h.ShownHot = false;
                }
                float width = rock * 0.7f;
                h.Tail.localScale = new Vector3(width, width, Mathf.Clamp(speed * 0.06f, 2f, 14f));
            }
        }

        void EmitTrail(Head h, Transform body, Vector3 position, float rock)
        {
            if (!h.HasLast)
            {
                h.LastPuff = position;
                h.HasLast = true;
                return;
            }
            float every = 1f / Mathf.Max(0.01f, _config.trailPuffsPerMetre);
            Vector3 step = position - h.LastPuff;
            float gone = step.magnitude;
            if (gone < every) return;
            int count = Mathf.Min(6, Mathf.FloorToInt(gone / every));
            for (int i = 1; i <= count; i++)
            {
                Vector3 at = h.LastPuff + step * (i * every / gone);
                Vector3 local = BodyToRoot(body, at);
                Emit(_smoke, local + Random.insideUnitSphere * rock, Random.insideUnitSphere * 0.6f, Random.Range(1.4f, 2.4f) * Mathf.Max(0.5f, rock * 2.4f), Random.Range(5f, 8f));
                Emit(_fire, local, Random.insideUnitSphere * 1.5f, rock * Random.Range(2f, 3.2f), Random.Range(0.25f, 0.45f));
            }
            h.LastPuff += step * (count * every / gone);
        }

        /// <summary>The object a falling rock is drawn on, for its sound to follow. Null for a slot out of range.</summary>
        public Transform HeadTransform(int slot) => slot >= 0 && slot < MaxHeads ? _heads[slot].Go.transform : null;

        public void HideHead(int slot)
        {
            if (slot < 0 || slot >= MaxHeads) return;
            Head h = _heads[slot];
            if (h.Go.activeSelf) h.Go.SetActive(false);
            h.HasLast = false;
        }

        public void HideAllHeads()
        {
            for (int i = 0; i < MaxHeads; i++) HideHead(i);
        }

        // ------------------------------------------------------------------ bursts and landings

        /// <summary>A rock bursting apart in the air: a flash, a ball of fire, sparks and a cloud of smoke.</summary>
        public void Burst(Transform body, Vector3 position, Vector3 velocity, float crater)
        {
            Vector3 local = BodyToRoot(body, position);
            Vector3 drift = RootDirection(body, velocity) * 0.25f;
            Flash(body, position, crater * 1.2f, 0.3f);
            int fire = Mathf.RoundToInt(8 + crater * 3f);
            for (int i = 0; i < fire; i++)
                Emit(_fire, local + Random.insideUnitSphere * crater * 0.3f, drift + Random.insideUnitSphere * (6f + crater * 2f), Random.Range(0.8f, 1.6f) * crater * 0.5f, Random.Range(0.4f, 0.9f));
            for (int i = 0; i < 10; i++)
                Emit(_fire, local, drift + Random.onUnitSphere * Random.Range(15f, 32f), Random.Range(0.12f, 0.25f), Random.Range(0.6f, 1.2f));
            int smoke = Mathf.RoundToInt(6 + crater * 2f);
            for (int i = 0; i < smoke; i++)
                Emit(_smoke, local + Random.insideUnitSphere * crater * 0.6f, drift * 0.4f + Random.insideUnitSphere * 3f, Random.Range(2f, 3.6f) * Mathf.Max(0.6f, crater * 0.45f), Random.Range(6f, 10f));
            FlareAt(body, position, crater * 10f, 1.2f);
        }

        /// <summary>
        /// A rock landing: a flash, a fireball (in air), a ring and column of dust, grit sprayed out, rocks
        /// thrown out to lie round the crater, and smoke that keeps rising from it for a while.
        /// <paramref name="ground"/> gives the ground's distance from the body's centre in a direction (body space).
        /// </summary>
        public void Impact(Transform body, Vector3 position, Vector3 up, float crater, bool splash, MeteorRules rules, System.Func<Vector3, float> ground)
        {
            Vector3 local = BodyToRoot(body, position);
            Vector3 upLocal = RootDirection(body, up).normalized;
            Flash(body, position + up * crater * 0.4f, crater * 1.8f, 0.45f);
            FlareAt(body, position + up * crater, crater * 14f, 2f);
            Basis(upLocal, out Vector3 e1, out Vector3 e2);

            if (_air && !splash)
            {
                int fire = Mathf.RoundToInt(10 + crater * 4f);
                for (int i = 0; i < fire; i++)
                {
                    Vector3 v = (upLocal * Random.Range(0.4f, 1.2f) + Random.insideUnitSphere * 0.8f) * (5f + crater * 2.2f);
                    Emit(_fire, local + upLocal * crater * 0.3f + Random.insideUnitSphere * crater * 0.4f, v, Random.Range(0.9f, 1.8f) * crater * 0.45f, Random.Range(0.5f, 1.1f));
                }
            }

            // Dust: a ring rolling out across the ground and a column thrown up.
            int ring = Mathf.RoundToInt(12 + crater * 4f);
            for (int i = 0; i < ring; i++)
            {
                float a = i * Mathf.PI * 2f / ring + Random.Range(-0.2f, 0.2f);
                Vector3 outward = e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a);
                float speed = Random.Range(6f, 12f) * Mathf.Sqrt(crater / 3f);
                Vector3 v = outward * speed + upLocal * (_air ? Random.Range(0.5f, 2f) : Random.Range(1f, 4f));
                Emit(_dust, local + outward * crater * 0.6f + upLocal * 0.4f, v, Random.Range(1.2f, 2.4f) * Mathf.Max(0.6f, crater * 0.4f), Lifetime(v, upLocal, _air ? Random.Range(3f, 6f) : 0f));
            }
            int column = Mathf.RoundToInt(10 + crater * 3f);
            for (int i = 0; i < column; i++)
            {
                Vector3 v = (upLocal * Random.Range(0.7f, 1.3f) + Random.insideUnitSphere * 0.45f) * Random.Range(8f, 18f) * Mathf.Sqrt(crater / 3f);
                Emit(_dust, local + Random.insideUnitSphere * crater * 0.3f, v, Random.Range(1f, 2.2f) * Mathf.Max(0.6f, crater * 0.4f), Lifetime(v, upLocal, _air ? Random.Range(4f, 7f) : 0f));
            }
            if (splash) return;

            // Grit: lots of small hot stones in high arcs, gone where they come back down.
            int grit = Mathf.RoundToInt(16 + crater * 6f);
            for (int i = 0; i < grit; i++)
            {
                Vector3 v = Throw(upLocal, e1, e2, crater, Random.value, Random.value) * Random.Range(0.8f, 1.5f);
                Emit(_grit, local + upLocal * 0.3f, v, Random.Range(0.08f, 0.26f) * Mathf.Max(0.7f, crater / 3f), Lifetime(v, upLocal, 0f));
            }
            for (int i = 0; i < 12; i++)
                Emit(_fire, local + upLocal * 0.3f, Throw(upLocal, e1, e2, crater, Random.value, Random.value) * 1.4f, Random.Range(0.1f, 0.2f), Random.Range(0.5f, 1.1f));

            // Real rocks, thrown out to lie round the crater for good.
            int chunks = EjectaRules.Count(rules, crater);
            for (int i = 0; i < chunks; i++) ThrowChunk(body, position, up, crater, rules, ground);

            _smokers.Add(new Smoker
            {
                Local = local,
                Started = Time.time,
                Seconds = _air ? _config.smokeSeconds : _config.smokeSeconds * 0.35f,
                Size = Mathf.Max(0.7f, crater * 0.45f),
            });
            if (_smokers.Count > 8) _smokers.RemoveAt(0);
        }

        /// <summary>A throw out of the crater: a cone round "up", stronger for bigger blasts.</summary>
        static Vector3 Throw(Vector3 up, Vector3 e1, Vector3 e2, float crater, float roll, float roll2)
        {
            float angle = (float)EjectaRules.Angle(roll) * Mathf.Deg2Rad;
            float bearing = roll2 * Mathf.PI * 2f;
            Vector3 outward = e1 * Mathf.Cos(bearing) + e2 * Mathf.Sin(bearing);
            float speed = (float)EjectaRules.Speed(crater, Random.value);
            return (outward * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * speed;
        }

        /// <summary>How long a thrown lump lives: in vacuum, until it falls back to the ground it left.</summary>
        float Lifetime(Vector3 v, Vector3 up, float inAir)
        {
            if (inAir > 0f) return inAir;
            float rise = Vector3.Dot(v, up);
            return Mathf.Clamp(2f * Mathf.Max(0.5f, rise) / _gravity + 0.1f, 0.4f, 14f);
        }

        void ThrowChunk(Transform body, Vector3 position, Vector3 up, float crater, MeteorRules rules, System.Func<Vector3, float> ground)
        {
            Chunk c = NextChunk(body, rules);
            Basis(up, out Vector3 e1, out Vector3 e2);
            c.Size = (float)EjectaRules.Size(crater, Random.value);
            c.Position = position + up * (c.Size * 0.6f);
            c.Velocity = Throw(up, e1, e2, crater, Random.value, Random.value);
            c.SpinAxis = Random.onUnitSphere;
            c.Spin = Random.Range(180f, 720f);
            c.Bounces = 0;
            c.Flying = true;
            c.HotUntil = Time.time + 12f;
            c.Renderer.sharedMaterial = _hotRockMat;
            c.Go.transform.localScale = Vector3.one * c.Size;
            c.Go.transform.localPosition = c.Position;
            c.Go.SetActive(true);
            c.Ground = ground;
        }

        Chunk NextChunk(Transform body, MeteorRules rules)
        {
            Chunk c;
            if (_chunks.Count >= Mathf.Max(1, rules.maxChunks))
            {
                // The oldest lying rock is the one put back in play.
                c = _chunks[0];
                _chunks.RemoveAt(0);
            }
            else
            {
                c = new Chunk { Go = new GameObject("MeteorRock") };
                c.Go.AddComponent<MeshFilter>().sharedMesh = _rocks[Random.Range(0, _rocks.Length)];
                c.Renderer = c.Go.AddComponent<MeshRenderer>();
                c.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            c.Body = body;
            c.Go.transform.SetParent(body, false);
            _chunks.Add(c);
            return c;
        }

        /// <summary>Thrown rocks fly on the world's own pull (not Unity's) and bounce a few times before they settle.</summary>
        void StepChunks(float dt)
        {
            for (int i = 0; i < _chunks.Count; i++)
            {
                Chunk c = _chunks[i];
                if (c.Go == null) continue;
                if (!c.Flying)
                {
                    if (c.HotUntil > 0f && Time.time > c.HotUntil)
                    {
                        c.HotUntil = 0f;
                        c.Renderer.sharedMaterial = _rockMat;
                    }
                    continue;
                }
                float r = c.Position.magnitude;
                if (r < 1f) { c.Flying = false; continue; }
                Vector3 up = c.Position / r;
                c.Velocity -= up * (_gravity * dt);
                if (_air) c.Velocity *= 1f - 0.05f * dt;
                c.Position += c.Velocity * dt;
                c.Go.transform.localRotation = Quaternion.AngleAxis(c.Spin * dt, c.SpinAxis) * c.Go.transform.localRotation;

                float rNow = c.Position.magnitude;
                float floor = c.Ground != null ? c.Ground(c.Position / rNow) + c.Size * 0.3f : rNow - 1f;
                if (rNow <= floor)
                {
                    Vector3 n = c.Position / rNow;
                    c.Position = n * floor;
                    float down = Vector3.Dot(c.Velocity, n);
                    Vector3 along = c.Velocity - n * down;
                    c.Velocity = along * 0.55f - n * (down * 0.32f);
                    c.Spin *= 0.6f;
                    c.Bounces++;
                    if (c.Bounces == 1 && OnChunkLanded != null) OnChunkLanded(c.Body.TransformPoint(c.Position), c.Size);
                    if (c.Bounces >= 4 || c.Velocity.magnitude < 1.2f)
                    {
                        c.Flying = false;
                        c.Ground = null;
                        c.Position = n * (floor - c.Size * 0.12f);
                    }
                }
                c.Go.transform.localPosition = c.Position;
            }
        }

        /// <summary>Raised when a thrown rock first hits the ground (world position, size), for its sound.</summary>
        public System.Action<Vector3, float> OnChunkLanded;

        // ------------------------------------------------------------------ light

        void Flash(Transform body, Vector3 position, float size, float length)
        {
            _flash.SetParent(body, false);
            _flash.localPosition = position;
            _flash.gameObject.SetActive(true);
            _flashStart = Time.time;
            _flashLength = length;
            _flashSize = size;
        }

        void FlareAt(Transform body, Vector3 position, float reach, float strength)
        {
            _flareBody = body;
            _flareWorldLocal = position;
            _flareStart = Time.time;
            _flareReach = reach;
            _flareStrength = strength;
        }

        Transform _flareBody;
        Transform _litBody;
        Vector3 _litAt;
        float _litReach, _litStrength;

        /// <summary>
        /// The biggest burning rock lights the ground under it as it comes down (worlds with air only), so a strike
        /// is seen coming on the ground round the player whichever way they face. Called each frame it falls.
        /// </summary>
        public void HeadLight(Transform body, Vector3 position, float crater)
        {
            if (!_air) return;
            _litBody = body;
            _litAt = position;
            _litReach = Mathf.Max(30f, crater * 40f);
            _litStrength = 0.9f;
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            StepChunks(dt);
            StepSmokers(dt);

            float k = (Time.time - _flashStart) / Mathf.Max(0.05f, _flashLength);
            if (_flash.gameObject.activeSelf)
            {
                if (k >= 1f) _flash.gameObject.SetActive(false);
                // Swells fast, then shrinks away: an opaque ball reads as a flash when it is gone this quickly.
                else _flash.localScale = Vector3.one * (_flashSize * (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f));
            }

            // What lights the ground this frame: the falling rock's own glow, and the flash of a burst or landing.
            bool wrote = false;
            if (_litBody != null)
            {
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 23f, 0.3f);
                wrote |= Light(_litBody, _litAt, _litReach, new Color(1f, 0.62f, 0.3f) * (_litStrength * flicker));
                _litBody = null;
            }
            float f = (Time.time - _flareStart) / 0.7f;
            if (f < 1f && _flareBody != null)
            {
                float fade = (1f - f) * (1f - f);
                wrote |= Light(_flareBody, _flareWorldLocal, _flareReach, new Color(1f, 0.6f, 0.28f) * (_flareStrength * fade));
            }
            if (!wrote && _flareLit)
            {
                // The launch's effects usually put the flare out every frame, but they do not always run: put
                // our own out once nothing of ours is lit, unless someone else has written it since.
                _flareLit = false;
                if (SkyGlobals.FlareColor == _flareWritten) SkyGlobals.FlareColor = Color.black;
            }
        }

        /// <summary>Hands a light to the ground's flare, unless the launch's own glow is the brighter.</summary>
        bool Light(Transform body, Vector3 local, float reach, Color c)
        {
            if (c.maxColorComponent <= SkyGlobals.FlareColor.maxColorComponent) return false;
            SkyGlobals.FlarePosition = body.TransformPoint(local);
            SkyGlobals.FlareReach = reach;
            SkyGlobals.FlareColor = c;
            _flareWritten = c;
            _flareLit = true;
            return true;
        }

        void StepSmokers(float dt)
        {
            for (int i = _smokers.Count - 1; i >= 0; i--)
            {
                Smoker s = _smokers[i];
                float age = (Time.time - s.Started) / Mathf.Max(0.1f, s.Seconds);
                if (age >= 1f)
                {
                    _smokers.RemoveAt(i);
                    continue;
                }
                // Thick at first, thinning to wisps.
                s.Carry += (1f - age) * (1f - age) * 3.5f * dt;
                while (s.Carry >= 1f)
                {
                    s.Carry -= 1f;
                    Vector3 v = Vector3.up * Random.Range(0.8f, 2.2f) + Random.insideUnitSphere * 0.5f;
                    ParticleSystem into = _air ? _smoke : _dust;
                    Emit(into, s.Local + Random.insideUnitSphere * s.Size + Vector3.up * 0.5f, _air ? v : v * 0.4f, s.Size * Random.Range(1.1f, 2f) * (1f - age * 0.5f), _air ? Random.Range(5f, 8f) : Random.Range(1.5f, 2.5f));
                }
                _smokers[i] = s;
            }
        }

        // ------------------------------------------------------------------ helpers

        Vector3 BodyToRoot(Transform body, Vector3 bodyPoint) => _root.InverseTransformPoint(body.TransformPoint(bodyPoint));

        Vector3 RootDirection(Transform body, Vector3 bodyDirection) => _root.InverseTransformDirection(body.TransformDirection(bodyDirection));

        static void Basis(Vector3 up, out Vector3 e1, out Vector3 e2)
        {
            e1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            e2 = Vector3.Cross(up, e1);
        }

        static void Emit(ParticleSystem system, Vector3 local, Vector3 velocity, float size, float life)
        {
            if (system == null || system.particleCount >= system.main.maxParticles) return;
            var p = new ParticleSystem.EmitParams
            {
                position = local,
                velocity = velocity,
                startSize = size,
                startLifetime = life,
                applyShapeToPosition = false,
            };
            system.Emit(p, 1);
        }

        void ClearParticles()
        {
            if (_fire) _fire.Clear(true);
            if (_smoke) _smoke.Clear(true);
            if (_dust) _dust.Clear(true);
            if (_grit) _grit.Clear(true);
        }

        /// <summary>Takes away the lying rocks on a body (or all of them) and the smoke, as when a run ends.</summary>
        public void Clear()
        {
            HideAllHeads();
            ClearParticles();
            _smokers.Clear();
            for (int i = 0; i < _chunks.Count; i++) if (_chunks[i].Go != null) Destroy(_chunks[i].Go);
            _chunks.Clear();
        }

        void OnDestroy()
        {
            for (int i = 0; i < _chunks.Count; i++) if (_chunks[i].Go != null) Destroy(_chunks[i].Go);
            for (int i = 0; i < _heads.Length; i++) if (_heads[i] != null && _heads[i].Go != null) Destroy(_heads[i].Go);
            if (_flash != null) Destroy(_flash.gameObject);
            if (_root != null) Destroy(_root.gameObject);
            if (_rocks != null) foreach (Mesh m in _rocks) if (m != null) Destroy(m);
            if (_ball != null) Destroy(_ball);
            if (_lump != null) Destroy(_lump);
            if (_gritRock != null) Destroy(_gritRock);
            if (_tail != null) Destroy(_tail);
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>A lumpy rock about one metre across: an icosahedron split once and pushed in and out, with darker pits.</summary>
        static Mesh RockMesh(int seed, int splits)
        {
            BallCorners(out List<Vector3> corners, out List<int> faces, splits);
            var rng = new SeededRandom(seed * 7919 + 13);
            var bumps = new Vector3[5];
            for (int i = 0; i < bumps.Length; i++) bumps[i] = new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f)).normalized;
            var stretch = new Vector3(rng.Range(0.85f, 1.15f), rng.Range(0.75f, 1f), rng.Range(0.9f, 1.2f));
            var positions = new Vector3[faces.Count];
            var colours = new Color[faces.Count];
            var tris = new int[faces.Count];
            var shaped = new Vector3[corners.Count];
            var shade = new float[corners.Count];
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 d = corners[i];
                float r = 1f;
                float pit = 0f;
                for (int b = 0; b < bumps.Length; b++)
                {
                    float dot = Vector3.Dot(d, bumps[b]);
                    r += (b % 2 == 0 ? 0.12f : -0.1f) * Mathf.Max(0f, dot) * Mathf.Max(0f, dot);
                    if (b % 2 == 1) pit += Mathf.Max(0f, dot - 0.6f);
                }
                r += rng.Range(-0.05f, 0.05f);
                shaped[i] = Vector3.Scale(d * r, stretch) * 0.5f;
                shade[i] = Mathf.Clamp01(1f - pit * 1.5f) * rng.Range(0.85f, 1f);
            }
            for (int i = 0; i < faces.Count; i++)
            {
                int c = faces[i];
                positions[i] = shaped[c];
                float s = shade[c];
                colours[i] = new Color(s, s * 0.95f, s * 0.9f, 1f);
                tris[i] = i;
            }
            // Corners are not shared between faces, so the rock shows hard, chipped facets.
            var mesh = new Mesh { name = "MeteorRock" + seed };
            mesh.vertices = positions;
            mesh.colors = colours;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A round ball one metre across (smooth; 80 faces split once, 20 not split), for glows, flashes and lumps of smoke.</summary>
        static Mesh BallMesh(int splits)
        {
            BallCorners(out List<Vector3> corners, out List<int> faces, splits);
            var positions = new Vector3[corners.Count];
            var normals = new Vector3[corners.Count];
            var colours = new Color[corners.Count];
            for (int i = 0; i < corners.Count; i++)
            {
                positions[i] = corners[i] * 0.5f;
                normals[i] = corners[i];
                colours[i] = Color.white;
            }
            var mesh = new Mesh { name = "MeteorBall" + splits };
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.colors = colours;
            mesh.triangles = faces.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>An icosahedron's corners on the unit ball, each face split into four <paramref name="splits"/> times.</summary>
        static void BallCorners(out List<Vector3> corners, out List<int> faces, int splits)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            corners = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < corners.Count; i++) corners[i] = corners[i].normalized;
            faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int s = 0; s < splits; s++)
            {
                var middles = new Dictionary<long, int>();
                var next = new List<int>(faces.Count * 4);
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Middle(corners, middles, a, b), bc = Middle(corners, middles, b, c), ca = Middle(corners, middles, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
        }

        static int Middle(List<Vector3> corners, Dictionary<long, int> middles, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (middles.TryGetValue(key, out int i)) return i;
            corners.Add(((corners[a] + corners[b]) * 0.5f).normalized);
            middles[key] = corners.Count - 1;
            return corners.Count - 1;
        }

        /// <summary>A flame tail: wide and white hot at the rock (the origin), narrowing to a dim point one metre behind (-Z).</summary>
        static Mesh TailMesh(int sides)
        {
            var vertices = new Vector3[sides + 1];
            var colours = new Color[sides + 1];
            // Both sides of each face, so the tail shows whichever way it is seen from.
            var triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices[i] = new Vector3(Mathf.Sin(a) * 0.5f, Mathf.Cos(a) * 0.5f, 0f);
                colours[i] = Color.white;
                triangles[i * 6] = i;
                triangles[i * 6 + 1] = (i + 1) % sides;
                triangles[i * 6 + 2] = sides;
                triangles[i * 6 + 3] = i;
                triangles[i * 6 + 4] = sides;
                triangles[i * 6 + 5] = (i + 1) % sides;
            }
            vertices[sides] = new Vector3(0f, 0f, -1f);
            colours[sides] = new Color(0.8f, 0.35f, 0.2f, 1f);
            // Normals set by hand, out from the axis: the two faces share corners, so worked-out normals would add up
            // to nothing, and a shader normalising a zero normal draws NaN, which bloom spreads into blocks.
            var normals = new Vector3[sides + 1];
            for (int i = 0; i < sides; i++) normals[i] = new Vector3(vertices[i].x, vertices[i].y, 0.25f).normalized;
            normals[sides] = new Vector3(0f, 0f, -1f);
            var mesh = new Mesh { name = "MeteorTail" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
