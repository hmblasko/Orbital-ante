using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using UnityEngine;

namespace OrbitalAnte.Gore
{
    /// <summary>
    /// All the mess: the blood burst when something pops, the spray that trails from fresh wounds, the splats
    /// where chunks land, and the pop sounds. Everything is pooled or capped so a big fight costs the same as a
    /// small one.
    ///
    /// Blood is drawn as chunky opaque droplets rather than transparent sprites: it suits the cartoon look and
    /// is cheap on a Steam Deck.
    /// </summary>
    public sealed class GoreFx : MonoBehaviour
    {
        const int MaxBleeders = 24;

        struct Bleeder
        {
            public Transform Anchor;
            public float StartTime;
            public float Duration;
            public float Carry;
            public Vector3 LastPosition;
        }

        public static GoreFx Instance { get; private set; }
        public GoreConfig Config { get; private set; }
        public DecalPool Decals { get; private set; }
        /// <summary>Droplets alive right now (for tests and the performance readout).</summary>
        public int LiveDroplets => _blood != null ? _blood.particleCount : 0;
        public int ActiveBleeders => _bleeders.Count;

        ParticleSystem _blood;
        GoreAudio _audio;
        readonly List<Bleeder> _bleeders = new List<Bleeder>(MaxBleeders);

        public static GoreFx Create(Transform parent, GoreConfig config)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("GoreFx");
            if (parent != null) go.transform.SetParent(parent, false);
            var fx = go.AddComponent<GoreFx>();
            fx.Initialize(config);
            Instance = fx;
            return fx;
        }

        public static void ResetStatics()
        {
            Instance = null;
            Weight = 1f;
        }

        void Initialize(GoreConfig config)
        {
            Config = config;
            BuildBlood();

            Decals = new DecalPool();
            var decalRoot = new GameObject("Decals").transform;
            decalRoot.SetParent(transform, false);
            Decals.Build(decalRoot, config);

            _audio = new GoreAudio();
            var audioRoot = new GameObject("Audio").transform;
            audioRoot.SetParent(transform, false);
            _audio.Build(audioRoot);

            Mannequin.AnyPartDetached += OnPartDetached;
        }

        void OnDestroy()
        {
            Mannequin.AnyPartDetached -= OnPartDetached;
            if (Instance == this) Instance = null;
        }

        void BuildBlood()
        {
            var go = new GameObject("BloodDroplets");
            go.transform.SetParent(transform, false);
            _blood = go.AddComponent<ParticleSystem>();
            // A new particle system starts out emitting on its own; stop that before configuring it.
            _blood.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _blood.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(Config.maxBloodParticles, 16, 4000);
            main.gravityModifier = GravityScaleFor(0.7f);
            _weightShown = Weight;
            // Made now, not on the first re-centre with blood in the air (Shift).
            _shiftBuffer = new ParticleSystem.Particle[main.maxParticles];
            main.startSpeed = 0f;
            main.startSize = Config.dropletSize;
            main.startLifetime = Config.dropletLife;
            main.startColor = Color.white;

            var emission = _blood.emission;
            emission.enabled = false; // droplets are only ever emitted from code

            var shape = _blood.shape;
            shape.enabled = false;

            // Droplets hit the world, lose most of their life and nearly stop, which reads as a splatter.
            var collision = _blood.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.collidesWith = Layers.WorldMask;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.bounce = 0.08f;
            collision.dampen = 0.7f;
            collision.lifetimeLoss = 0.55f;
            collision.radiusScale = 0.5f;
            collision.sendCollisionMessages = false;

            var size = _blood.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = MakeDropletMesh();
            renderer.sharedMaterial = Mat.Unlit(Config.bloodColor);
            renderer.alignment = ParticleSystemRenderSpace.World;
            // Plain (non-particle) shader, so let Unity batch the droplet meshes itself.
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _blood.Play(true);
        }

        /// <summary>A 20-sided blob: round enough to read as a droplet, cheap enough to draw hundreds.</summary>
        static Mesh MakeDropletMesh()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized * 0.5f;
            int[] tris =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mesh = new Mesh { name = "BloodDroplet" };
            mesh.vertices = v;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ events

        void OnPartDetached(Mannequin mannequin, Detachment detachment, Gib gib, HitInfo hit)
        {
            if (mannequin == null) return;
            BodyPart root = mannequin.Part(detachment.Root);
            Vector3 wound = root != null ? root.transform.position : hit.Point;
            if (root != null && root.ProximalCap != null) wound = root.ProximalCap.transform.position;
            Vector3 direction = hit.Direction.sqrMagnitude > 0.0001f ? hit.Direction.normalized : Vector3.up;

            float scale;
            switch (detachment.Reason)
            {
                case DetachReason.TorsoGib: scale = 2.2f; break;
                case DetachReason.HeadDestroyed: scale = 1.6f; break;
                case DetachReason.Corpse: scale = 0f; break;
                case DetachReason.TorsoDestroyed: scale = 0.7f; break;
                default: scale = 1f; break;
            }

            // The player's own head carries the view: no spray straight into the lens.
            bool lens = mannequin.KeepsHeadClean && detachment.Root == BodyPartId.Head;
            if (lens) scale = 0f;

            if (scale > 0f)
            {
                Burst(wound, direction, Mathf.RoundToInt(Config.burstCount * scale), Config.burstSpeed * Mathf.Lerp(1f, 1.3f, scale - 1f));
            }

            // Both sides of the break keep spraying for a moment: the loose chunk and the stump it left behind.
            if (!lens && root != null && root.ProximalCap != null) AddBleeder(root.ProximalCap.transform, Config.trailDuration);
            if (BodyLayout.HasParent(detachment.Root))
            {
                BodyPart parent = mannequin.Part(BodyLayout.Parent(detachment.Root));
                GameObject stump = parent != null ? parent.ChildCap(detachment.Root) : null;
                if (stump != null) AddBleeder(stump.transform, Config.trailDuration * 1.3f);
            }

            _audio.PlayPop(detachment, wound, Config.popVolume);
        }

        /// <summary>Called by a loose chunk when it hits the world hard enough to leave a mark.</summary>
        public void GibLanded(Vector3 point, Vector3 normal, float impactSpeed, float mass, Collider landedOn = null)
        {
            float size = Config.decalSize * Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(1f, 30f, mass))
                                          * Random.Range(0.8f, 1.25f);
            Decals.Spawn(point, normal, size, StuckMarks.On(landedOn));
            Burst(point + normal * 0.05f, normal, Mathf.Max(3, Config.burstCount / 5), Config.burstSpeed * 0.45f);
            float loudness = Mathf.InverseLerp(Config.decalMinImpactSpeed, 12f, impactSpeed);
            _audio.PlaySplat(point, Config.splatVolume * Mathf.Lerp(0.4f, 1f, loudness));
        }

        // ------------------------------------------------------------------ droplets

        /// <summary>Throws a fan of droplets from a point, biased along a direction.</summary>
        public void Burst(Vector3 position, Vector3 direction, int count, float speed)
        {
            if (_blood == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = (direction * 0.7f + Random.onUnitSphere * 0.75f + Up * 0.45f).normalized
                                   * (speed * Random.Range(0.35f, 1f));
                EmitDroplet(position + Random.insideUnitSphere * 0.04f, velocity, Random.Range(0.6f, 1.35f));
            }
        }

        void EmitDroplet(Vector3 position, Vector3 velocity, float sizeScale)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = Config.dropletSize * sizeScale,
                startLifetime = Config.dropletLife * Random.Range(0.7f, 1.2f),
            };
            _blood.Emit(p, 1);
        }

        void AddBleeder(Transform anchor, float duration)
        {
            if (anchor == null || duration <= 0f) return;
            if (_bleeders.Count >= MaxBleeders) _bleeders.RemoveAt(0);
            _bleeders.Add(new Bleeder
            {
                Anchor = anchor,
                StartTime = Time.time,
                Duration = duration,
                Carry = 0f,
                LastPosition = anchor.position,
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f) UpdateBleeders(dt);
            if (Decals != null) Decals.Tick();
        }

        /// <summary>
        /// How much of the world's pull the player really feels, 0 to 1: 1 standing on the ground, 0 falling freely
        /// (in orbit beside the ship, or drifting on a jetpack). Set by the enemy spawner in the solar system and
        /// left at 1 in the arena. Blood's extra weight and its lean "upward" only apply to the part that is felt:
        /// in free fall a spray drifts as a cloud round the wound, falling with everything else.
        /// </summary>
        public static float Weight = 1f;

        /// <summary>The blood's gravity scale for this weight. The drops fall with the world, plus a felt extra.</summary>
        public static float GravityScaleFor(float extra) => 1f + extra * Mathf.Clamp01(Weight);

        /// <summary>"Up" for blood: away from the pull the player feels, as strongly as it is felt.</summary>
        static Vector3 Up
        {
            get
            {
                Vector3 g = Physics.gravity;
                return g.sqrMagnitude > 0.01f ? -g.normalized * Mathf.Clamp01(Weight) : Vector3.zero;
            }
        }

        float _weightShown = 1f;

        /// <summary>Re-applies the blood's gravity scale when the felt weight has changed enough to matter.</summary>
        void FollowWeight()
        {
            if (_blood == null || Mathf.Abs(Weight - _weightShown) < 0.05f) return;
            _weightShown = Weight;
            var main = _blood.main;
            main.gravityModifier = GravityScaleFor(0.7f);
        }

        void LateUpdate()
        {
            if (Decals != null) Decals.Follow();
            FollowWeight();
        }

        /// <summary>
        /// The world was slid back to Unity's zero point: move the droplets in the air and the wounds' last
        /// positions with it (splats follow what they landed on by themselves).
        /// </summary>
        public void Shift(Vector3 by)
        {
            for (int i = 0; i < _bleeders.Count; i++)
            {
                Bleeder b = _bleeders[i];
                b.LastPosition += by;
                _bleeders[i] = b;
            }
            ShiftParticles(_blood, by, ref _shiftBuffer);
        }

        ParticleSystem.Particle[] _shiftBuffer;

        /// <summary>Moves every live particle of a system by the same amount (only on a re-centre, so rarely).</summary>
        public static void ShiftParticles(ParticleSystem system, Vector3 by, ref ParticleSystem.Particle[] buffer)
        {
            if (system == null || system.particleCount == 0) return;
            int max = system.main.maxParticles;
            if (buffer == null || buffer.Length < max) buffer = new ParticleSystem.Particle[max];
            int n = system.GetParticles(buffer);
            for (int i = 0; i < n; i++) buffer[i].position += by;
            system.SetParticles(buffer, n);
        }

        void UpdateBleeders(float dt)
        {
            float now = Time.time;
            for (int i = _bleeders.Count - 1; i >= 0; i--)
            {
                Bleeder b = _bleeders[i];
                float k = (now - b.StartTime) / b.Duration;
                // A wound stops when its time is up, or when the part it was on has been cleaned away.
                if (k >= 1f || b.Anchor == null || !b.Anchor.gameObject.activeInHierarchy)
                {
                    _bleeders.RemoveAt(i);
                    continue;
                }

                Vector3 position = b.Anchor.position;
                Vector3 anchorVelocity = (position - b.LastPosition) / dt;
                b.LastPosition = position;
                // The world was slid back to Unity's zero point (or the piece was put back on the ground): that jump
                // is not a speed, and must not fire one frame of droplets at kilometres a second.
                if (anchorVelocity.sqrMagnitude > 60f * 60f) anchorVelocity = Vector3.zero;

                // Starts strong and tails off to nothing.
                float rate = Config.trailRate * (1f - k) * (1f - k);
                b.Carry += rate * dt;
                while (b.Carry >= 1f)
                {
                    b.Carry -= 1f;
                    Vector3 velocity = anchorVelocity * 0.35f + Random.onUnitSphere * 1.1f + Up * 0.8f;
                    EmitDroplet(position, velocity, Random.Range(0.45f, 0.9f));
                }
                _bleeders[i] = b;
            }
        }

        /// <summary>Wipes all blood, splats and wounds straight away (used when the arena is reset).</summary>
        public void Clear()
        {
            _bleeders.Clear();
            if (_blood != null) _blood.Clear(true);
            if (Decals != null) Decals.Clear();
        }
    }
}
