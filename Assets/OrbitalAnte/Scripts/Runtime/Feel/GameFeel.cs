using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Feel
{
    /// <summary>
    /// Owns the time scale while the game is not paused (hitstop and slow-mo) and drives
    /// trauma-based camera shake on the player's shake rig. Uses unscaled time throughout so
    /// a hitstop never freezes its own timer.
    /// </summary>
    public class GameFeel : MonoBehaviour
    {
        static GameFeel instance;

        GameFeelConfig cfg;
        float baseFixedDelta;
        float hitstopUntil;
        float slowMoUntil;
        float trauma;
        float seed;
        bool ownsTimeScale;

        public static Transform ShakeTarget;

        static GameFeel Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("GameFeel").AddComponent<GameFeel>();
                return instance;
            }
        }

        public static GameFeelConfig Config => GameFeelConfig.Get();

        public static void EnsureCreated() => _ = Instance;

        void Awake()
        {
            cfg = GameFeelConfig.Get();
            baseFixedDelta = Time.fixedDeltaTime;
            seed = Random.value * 100f;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            Time.fixedDeltaTime = baseFixedDelta;
            if (!PauseMenu.IsPaused) Time.timeScale = 1f;
        }

        public static void Hitstop(float seconds)
        {
            if (seconds <= 0f) return;
            var i = Instance;
            i.hitstopUntil = Mathf.Max(i.hitstopUntil, Time.unscaledTime + seconds);
        }

        public static void SlowMo(float seconds)
        {
            var i = Instance;
            i.slowMoUntil = Mathf.Max(i.slowMoUntil, Time.unscaledTime + seconds);
        }

        public static void AddTrauma(float amount)
        {
            var i = Instance;
            i.trauma = Mathf.Clamp01(i.trauma + amount);
        }

        static bool? headshotSlowMo;

        /// <summary>Runtime toggle, starts from the config value (toggling never edits the asset).</summary>
        public static bool HeadshotSlowMo
        {
            get => headshotSlowMo ?? GameFeelConfig.Get().headshotSlowMo;
            set => headshotSlowMo = value;
        }

        void Update()
        {
            if (PauseMenu.IsPaused)
            {
                ownsTimeScale = false;
                return;
            }
            float now = Time.unscaledTime;
            float scale = 1f;
            if (now < hitstopUntil) scale = cfg.hitstopTimeScale;
            else if (now < slowMoUntil) scale = cfg.slowMoTimeScale;

            if (scale != 1f || ownsTimeScale || Time.timeScale != 1f)
            {
                Time.timeScale = scale;
                // Keep physics smooth in slow-mo; during a near-freeze physics simply waits.
                Time.fixedDeltaTime = scale >= 0.1f ? baseFixedDelta * scale : baseFixedDelta;
                ownsTimeScale = scale != 1f;
            }
        }

        void LateUpdate()
        {
            var target = ShakeTarget;
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            trauma = Mathf.Max(0f, trauma - cfg.shakeDecay * dt);
            if (!cfg.cameraShake || trauma <= 0f || PauseMenu.IsPaused)
            {
                target.localRotation = Quaternion.identity;
                target.localPosition = Vector3.zero;
                return;
            }
            float shake = trauma * trauma;
            float t = Time.unscaledTime * cfg.shakeFrequency;
            float Noise(float o) => Mathf.PerlinNoise(seed + o, t) * 2f - 1f;
            target.localRotation = Quaternion.Euler(
                Noise(0f) * cfg.shakeMaxDegrees * shake,
                Noise(10f) * cfg.shakeMaxDegrees * shake,
                Noise(20f) * cfg.shakeMaxDegrees * 0.6f * shake);
            target.localPosition = new Vector3(Noise(30f), Noise(40f), 0f) * cfg.shakeMaxOffset * shake;
        }
    }
}
