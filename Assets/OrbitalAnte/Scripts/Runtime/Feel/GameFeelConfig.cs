using UnityEngine;

namespace OrbitalAnte.Feel
{
    /// <summary>Juice settings: hitstop, shake, slow-mo, blood. Edit the asset in Resources/Config.</summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Game Feel Config", fileName = "GameFeelConfig")]
    public class GameFeelConfig : ScriptableObject
    {
        [Header("Hitstop (shooter freezes briefly when a limb pops)")]
        [Range(0f, 0.12f)] public float limbHitstopSeconds = 0.05f;
        [Range(0f, 0.12f)] public float killHitstopSeconds = 0.075f;
        [Tooltip("Time scale during hitstop. 0.02 = near freeze.")]
        public float hitstopTimeScale = 0.02f;

        [Header("Camera shake")]
        public bool cameraShake = true;
        [Tooltip("Degrees of shake at full trauma.")]
        public float shakeMaxDegrees = 2.2f;
        public float shakeMaxOffset = 0.025f;
        public float shakeFrequency = 22f;
        [Tooltip("Trauma lost per second (higher = shorter shakes).")]
        public float shakeDecay = 2.6f;
        public float limbPopTrauma = 0.32f;
        public float killTrauma = 0.5f;
        public float shotTrauma = 0.06f;

        [Header("Headshot slow-mo (optional, toggle in the test arena)")]
        public bool headshotSlowMo = false;
        public float slowMoTimeScale = 0.3f;
        public float slowMoSeconds = 0.6f;

        [Header("Blood")]
        public Color bloodColor = new Color(0.62f, 0.03f, 0.05f);
        public Color bloodColorDark = new Color(0.36f, 0.01f, 0.03f);
        public int burstParticlesOnPop = 46;
        public int burstParticlesOnHit = 9;
        public float trailSeconds = 1.6f;
        public float trailRate = 55f;
        public int maxTrails = 14;

        [Header("Blood decals")]
        public int maxDecals = 64;
        public float decalLifetimeSeconds = 45f;
        public float decalMinSize = 0.35f;
        public float decalMaxSize = 1.1f;
        [Tooltip("Gib impact speed (m/s) needed to leave a splat.")]
        public float splatImpactSpeed = 2.5f;
        public float splatCooldownSeconds = 0.25f;

        [Header("Gib fade")]
        [Tooltip("Seconds at the end of a gib's life where it shrinks away.")]
        public float gibFadeSeconds = 1.2f;
        [Tooltip("Pooled part meshes kept per body part type.")]
        public int poolPerPart = 24;

        static GameFeelConfig cached;

        public static GameFeelConfig Get()
        {
            if (cached == null)
            {
                cached = Resources.Load<GameFeelConfig>("Config/GameFeelConfig");
                if (cached == null) cached = CreateInstance<GameFeelConfig>();
            }
            return cached;
        }
    }
}
