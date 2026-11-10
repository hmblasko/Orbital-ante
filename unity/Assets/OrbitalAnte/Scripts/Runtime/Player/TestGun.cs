using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// Bare-minimum hitscan rifle so the breakable characters can be tested in Milestone 1.
    /// Milestone 3 replaces it with data-driven weapons (recoil, reload, ammo, muzzle flash, impacts).
    /// </summary>
    public sealed class TestGun : MonoBehaviour
    {
        const int TracerCount = 6;
        const float TracerLife = 0.06f;

        PlayerConfig _config;
        Camera _eye;
        Transform _muzzle;
        Transform _model;
        Vector3 _modelRestPosition;
        float _nextShotTime;
        float _kick;
        System.Random _rng = new System.Random(12345);

        readonly LineRenderer[] _tracers = new LineRenderer[TracerCount];
        readonly float[] _tracerDieAt = new float[TracerCount];
        int _nextTracer;

        /// <summary>What the crosshair is resting on, for the debug readout. Null when aiming at nothing hittable.</summary>
        public IHittable AimedAt { get; private set; }

        public void Initialize(PlayerConfig config, Camera eye)
        {
            _config = config;
            _eye = eye;
            BuildModel();
            BuildTracers();
        }

        void BuildModel()
        {
            // A plain placeholder shape. Real weapon models arrive in Milestone 3.
            var model = new GameObject("TestRifleModel");
            _model = model.transform;
            _model.SetParent(_eye.transform, false);
            _modelRestPosition = new Vector3(0.21f, -0.19f, 0.42f);
            _model.localPosition = _modelRestPosition;

            Material dark = Mat.Lit(new Color(0.16f, 0.17f, 0.2f), 0.5f);
            Material wood = Mat.Lit(new Color(0.55f, 0.33f, 0.16f), 0.3f);
            AddBlock(_model, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.07f, 0.34f), Quaternion.identity, dark);
            AddBlock(_model, PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0.30f), new Vector3(0.022f, 0.14f, 0.022f), Quaternion.Euler(90f, 0f, 0f), dark);
            AddBlock(_model, PrimitiveType.Cube, new Vector3(0f, -0.075f, -0.10f), new Vector3(0.045f, 0.11f, 0.05f), Quaternion.Euler(18f, 0f, 0f), wood);
            AddBlock(_model, PrimitiveType.Cube, new Vector3(0f, -0.085f, 0.04f), new Vector3(0.04f, 0.13f, 0.06f), Quaternion.Euler(-14f, 0f, 0f), dark);

            var muzzle = new GameObject("Muzzle");
            _muzzle = muzzle.transform;
            _muzzle.SetParent(_model, false);
            _muzzle.localPosition = new Vector3(0f, 0.012f, 0.45f);
        }

        static void AddBlock(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.layer = Layers.Player;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildTracers()
        {
            Material material = Mat.Unlit(new Color(1f, 0.85f, 0.35f));
            for (int i = 0; i < TracerCount; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = 0.02f;
                line.endWidth = 0.008f;
                line.sharedMaterial = material;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _tracers[i] = line;
            }
        }

        void Update()
        {
            if (_config == null) return;

            // Tracers fade on unscaled time so they never get stuck on screen while paused.
            for (int i = 0; i < TracerCount; i++)
            {
                if (_tracers[i].enabled && Time.unscaledTime >= _tracerDieAt[i]) _tracers[i].enabled = false;
            }

            if (GameSession.Paused) return;

            UpdateAim();

            if (GameInput.Fire.IsPressed() && Time.time >= _nextShotTime)
            {
                _nextShotTime = Time.time + 1f / Mathf.Max(0.1f, _config.testRifleShotsPerSecond);
                Shoot();
            }

            // Simple visual kick that settles back.
            _kick = Mathf.MoveTowards(_kick, 0f, Time.deltaTime * 6f);
            _model.localPosition = _modelRestPosition + new Vector3(0f, 0f, -0.05f * _kick);
            _model.localRotation = Quaternion.Euler(-4f * _kick, 0f, 0f);
        }

        void UpdateAim()
        {
            AimedAt = null;
            var ray = new Ray(_eye.transform.position, _eye.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, _config.testRifleRange, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                AimedAt = hit.collider.GetComponentInParent<IHittable>();
            }
        }

        void Shoot()
        {
            _kick = 1f;

            // Small random aim error, as a cone around the crosshair.
            float spread = _config.testRifleSpread;
            float yaw = ((float)_rng.NextDouble() * 2f - 1f) * spread;
            float pitch = ((float)_rng.NextDouble() * 2f - 1f) * spread;
            Vector3 direction = _eye.transform.rotation * Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

            Vector3 origin = _eye.transform.position;
            Vector3 end = origin + direction * _config.testRifleRange;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, _config.testRifleRange, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var target = hit.collider.GetComponentInParent<IHittable>();
                if (target != null)
                {
                    var info = new HitInfo
                    {
                        Damage = _config.testRifleDamage,
                        Point = hit.point,
                        Direction = direction,
                        Normal = hit.normal,
                    };
                    HitOutcome outcome = target.Hit(info);
                    if (outcome.Hit) CombatEvents.RaisePlayerHit(outcome);
                }
            }

            ShowTracer(_muzzle.position, end);
        }

        void ShowTracer(Vector3 from, Vector3 to)
        {
            LineRenderer line = _tracers[_nextTracer];
            _tracerDieAt[_nextTracer] = Time.unscaledTime + TracerLife;
            _nextTracer = (_nextTracer + 1) % TracerCount;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.enabled = true;
        }
    }
}
