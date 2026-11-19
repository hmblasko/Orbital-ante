using UnityEngine;

namespace OrbitalAnte.Gore
{
    public enum DecalMode
    {
        /// <summary>URP decal projectors when the project is set up for them, flat splat meshes otherwise.</summary>
        Auto,
        /// <summary>Always use flat splat meshes. Cheapest, and works with any renderer setup.</summary>
        MeshOnly,
        /// <summary>Always use URP decal projectors (needs the Decal renderer feature).</summary>
        ProjectorOnly,
    }

    /// <summary>
    /// Tunable numbers for gibs, blood, decals, hitstop, camera shake and pop sounds. An asset is created at
    /// Assets/OrbitalAnte/Resources/Config/GoreConfig so the values can be changed in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Gore Config", fileName = "GoreConfig")]
    public sealed class GoreConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/GoreConfig";

        [Header("Gibs (performance budget)")]
        [Tooltip("Most loose chunks alive at once, fading ones included (each is a rigidbody). Near the cap the oldest fades out early; at the cap one is removed straight away.")]
        public int maxActiveGibs = 40;
        [Tooltip("How many of the cap's places are kept for chunks that are fading out, so overflow fades rather than pops. Trial value for the lead designer.")]
        public int overflowFadeSlots = 4;
        [Tooltip("Seconds a loose chunk stays before it fades.")]
        public float gibLifetime = 30f;
        [Tooltip("Seconds the fade-out takes.")]
        public float gibFadeTime = 1.5f;
        [Tooltip("Faster fade used when the cap pushes an old chunk out.")]
        public float overflowFadeTime = 0.3f;
        [Tooltip("How many gib containers to create up front so none are created mid-fight.")]
        public int poolPrewarm = 24;
        [Range(0f, 1f)] public float gibBounciness = 0.55f;
        [Range(0f, 1f)] public float gibFriction = 0.45f;

        [Header("Blood particles")]
        public Color bloodColor = new Color(0.78f, 0.04f, 0.06f);
        public int maxBloodParticles = 500;
        [Tooltip("Droplets in the burst when a limb pops.")]
        public int burstCount = 22;
        public float burstSpeed = 4.5f;
        public float dropletSize = 0.07f;
        public float dropletLife = 1.1f;
        [Tooltip("Seconds a fresh wound keeps spraying.")]
        public float trailDuration = 1.4f;
        [Tooltip("Droplets per second from a fresh wound, tailing off to nothing.")]
        public float trailRate = 45f;

        [Header("Blood decals")]
        public DecalMode decalMode = DecalMode.Auto;
        public int maxDecals = 48;
        public float decalLife = 25f;
        public float decalSize = 0.55f;
        [Tooltip("How many splats one chunk can leave as it bounces.")]
        public int decalsPerGib = 3;
        [Tooltip("Slowest impact that still leaves a splat, in metres per second.")]
        public float decalMinImpactSpeed = 2f;
        [Tooltip("Set by Orbital Ante > Set Up Project once the Decal renderer feature and decal material exist.")]
        public bool decalProjectorsReady;

        [Header("Hitstop (seconds of real time)")]
        public float limbHitstop = 0.05f;
        public float torsoHitstop = 0.07f;
        public float killHitstop = 0.08f;
        [Tooltip("Game speed during hitstop. Near zero reads as a freeze.")]
        public float hitstopTimeScale = 0.02f;

        [Header("Camera shake")]
        public float shakeLimb = 0.35f;
        public float shakeBig = 0.7f;
        public float shakeMaxDegrees = 1.6f;
        [Tooltip("How quickly shake dies away, per second.")]
        public float shakeDecay = 2.8f;

        [Header("Headshot slow-mo")]
        public float headshotSlowMoScale = 0.3f;
        [Tooltip("Seconds of real time.")]
        public float headshotSlowMoDuration = 0.45f;

        [Header("Sound")]
        [Range(0f, 1f)] public float popVolume = 0.85f;
        [Range(0f, 1f)] public float splatVolume = 0.35f;

        static GoreConfig _cached;

        public static GoreConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<GoreConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<GoreConfig>();
                _cached.name = "GoreConfig (defaults)";
            }
            return _cached;
        }
    }
}
