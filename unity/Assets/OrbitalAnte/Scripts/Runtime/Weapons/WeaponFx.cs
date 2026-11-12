using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Weapons
{
    /// <summary>
    /// The visible and audible side of shooting, shared by the player and the enemies: tracers, muzzle flashes,
    /// sparks and marks where bullets land, and the gun sounds. Everything is pooled and capped.
    /// </summary>
    public sealed class WeaponFx : MonoBehaviour
    {
        const int TracerCount = 16;
        const float TracerLife = 0.06f;
        const int FlashCount = 6;
        const float FlashLife = 0.045f;
        const int MarkCount = 48;
        const float MarkLife = 20f;

        public static WeaponFx Instance { get; private set; }
        public WeaponAudio Audio { get; private set; }

        readonly LineRenderer[] _tracers = new LineRenderer[TracerCount];
        readonly float[] _tracerDieAt = new float[TracerCount];
        int _nextTracer;

        readonly Transform[] _flashes = new Transform[FlashCount];
        readonly Light[] _flashLights = new Light[FlashCount];
        readonly float[] _flashDieAt = new float[FlashCount];
        int _nextFlash;

        readonly Transform[] _marks = new Transform[MarkCount];
        readonly float[] _markDieAt = new float[MarkCount];
        int _nextMark;

        ParticleSystem _sparks;

        /// <summary>Bullet marks showing right now (for tests).</summary>
        public int LiveMarks
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MarkCount; i++) if (_marks[i] != null && _marks[i].gameObject.activeSelf) n++;
                return n;
            }
        }

        public static WeaponFx Create(Transform parent)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("WeaponFx");
            if (parent != null) go.transform.SetParent(parent, false);
            var fx = go.AddComponent<WeaponFx>();
            fx.Build();
            Instance = fx;
            return fx;
        }

        /// <summary>The current effects object, creating a bare one if the scene has none (tests, odd scenes).</summary>
        public static WeaponFx GetOrCreate()
        {
            if (Instance == null) Create(null);
            return Instance;
        }

        public static void ResetStatics() => Instance = null;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Build()
        {
            for (int i = 0; i < TracerCount; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.startWidth = 0.022f;
                line.endWidth = 0.008f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _tracers[i] = line;
            }

            Material flashMaterial = Mat.Unlit(new Color(1f, 0.86f, 0.42f));
            for (int i = 0; i < FlashCount; i++)
            {
                // Two squashed blobs at right angles read as a star-shaped flash from any direction.
                var root = new GameObject("MuzzleFlash").transform;
                root.SetParent(transform, false);
                AddFlashBlob(root, new Vector3(1f, 0.35f, 1.5f), flashMaterial);
                AddFlashBlob(root, new Vector3(0.35f, 1f, 1.5f), flashMaterial);
                var light = root.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.8f, 0.45f);
                light.range = 7f;
                light.intensity = 3f;
                light.shadows = LightShadows.None;
                root.gameObject.SetActive(false);
                _flashes[i] = root;
                _flashLights[i] = light;
            }

            Material markMaterial = Mat.Unlit(new Color(0.07f, 0.07f, 0.08f));
            Mesh disc = MakeDisc();
            for (int i = 0; i < MarkCount; i++)
            {
                var go = new GameObject("BulletMark");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = disc;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = markMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);
                _marks[i] = go.transform;
            }

            BuildSparks();

            Audio = new WeaponAudio();
            var audioRoot = new GameObject("Audio").transform;
            audioRoot.SetParent(transform, false);
            Audio.Build(audioRoot);
        }

        static void AddFlashBlob(Transform parent, Vector3 scale, Material material)
        {
            var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = blob.GetComponent<Collider>();
            if (col != null) Destroy(col);
            blob.transform.SetParent(parent, false);
            blob.transform.localScale = scale;
            var renderer = blob.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void BuildSparks()
        {
            var go = new GameObject("ImpactSparks");
            go.transform.SetParent(transform, false);
            _sparks = go.AddComponent<ParticleSystem>();
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _sparks.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.gravityModifier = 1.2f;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.startLifetime = 0.3f;

            var emission = _sparks.emission;
            emission.enabled = false;
            var shape = _sparks.shape;
            shape.enabled = false;

            var size = _sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
            renderer.sharedMaterial = Mat.Unlit(new Color(1f, 0.8f, 0.45f));
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _sparks.Play(true);
        }

        static Mesh MakeDisc()
        {
            const int segments = 10;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 6];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = 1 + i;
                int b = 1 + (i + 1) % segments;
                // Both windings so the mark shows whichever way the surface faces.
                triangles[i * 6 + 0] = 0; triangles[i * 6 + 1] = b; triangles[i * 6 + 2] = a;
                triangles[i * 6 + 3] = 0; triangles[i * 6 + 4] = a; triangles[i * 6 + 5] = b;
            }
            var mesh = new Mesh { name = "BulletMark" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ use

        /// <summary>Everything that happens at the gun when it fires: flash, light, tracer and sound.</summary>
        public void Shot(WeaponData data, Vector3 muzzle, Vector3 end)
        {
            LineRenderer line = _tracers[_nextTracer];
            _tracerDieAt[_nextTracer] = Time.unscaledTime + TracerLife;
            _nextTracer = (_nextTracer + 1) % TracerCount;
            line.sharedMaterial = Mat.Unlit(data.tracerColor);
            line.SetPosition(0, muzzle);
            line.SetPosition(1, end);
            line.enabled = true;

            Transform flash = _flashes[_nextFlash];
            _flashDieAt[_nextFlash] = Time.unscaledTime + FlashLife;
            _nextFlash = (_nextFlash + 1) % FlashCount;
            Vector3 along = end - muzzle;
            flash.position = muzzle;
            if (along.sqrMagnitude > 0.0001f) flash.rotation = Quaternion.LookRotation(along) * Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            flash.localScale = Vector3.one * (data.muzzleFlashSize * Random.Range(0.8f, 1.2f));
            flash.gameObject.SetActive(true);

            Audio.PlayShot(data, muzzle);
        }

        /// <summary>A bullet hitting the world: sparks and a dark mark.</summary>
        public void Impact(Vector3 point, Vector3 normal)
        {
            Sparks(point, normal, 7);

            Transform mark = _marks[_nextMark];
            _markDieAt[_nextMark] = Time.time + MarkLife;
            _nextMark = (_nextMark + 1) % MarkCount;
            mark.position = point + normal * 0.004f;
            mark.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            float size = Random.Range(0.05f, 0.075f);
            mark.localScale = new Vector3(size, 1f, size);
            mark.gameObject.SetActive(true);
        }

        /// <summary>A spray of sparks, used for world hits and for bullets striking a gun.</summary>
        public void Sparks(Vector3 point, Vector3 normal, int count)
        {
            if (_sparks == null) return;
            for (int i = 0; i < count; i++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = point + normal * 0.02f,
                    velocity = (normal * 0.9f + Random.onUnitSphere * 0.8f).normalized * Random.Range(1.5f, 5f),
                    startSize = Random.Range(0.015f, 0.04f),
                    startLifetime = Random.Range(0.15f, 0.4f),
                };
                _sparks.Emit(p, 1);
            }
        }

        void Update()
        {
            // Unscaled time, so nothing gets stuck on screen during hitstop or a pause.
            float now = Time.unscaledTime;
            for (int i = 0; i < TracerCount; i++)
            {
                if (_tracers[i].enabled && now >= _tracerDieAt[i]) _tracers[i].enabled = false;
            }
            for (int i = 0; i < FlashCount; i++)
            {
                if (_flashes[i].gameObject.activeSelf && now >= _flashDieAt[i]) _flashes[i].gameObject.SetActive(false);
            }
            float gameNow = Time.time;
            for (int i = 0; i < MarkCount; i++)
            {
                if (_marks[i].gameObject.activeSelf && gameNow >= _markDieAt[i]) _marks[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Removes every tracer, flash and mark straight away (used when the arena is reset).</summary>
        public void Clear()
        {
            for (int i = 0; i < TracerCount; i++) _tracers[i].enabled = false;
            for (int i = 0; i < FlashCount; i++) _flashes[i].gameObject.SetActive(false);
            for (int i = 0; i < MarkCount; i++) _marks[i].gameObject.SetActive(false);
            if (_sparks != null) _sparks.Clear(true);
        }
    }
}
