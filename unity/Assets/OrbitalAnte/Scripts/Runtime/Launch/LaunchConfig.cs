using UnityEngine;

namespace OrbitalAnte.Launch
{
    /// <summary>
    /// The launch's tunable numbers. Edit the asset at Assets/OrbitalAnte/Resources/Config/LaunchConfig;
    /// if it is missing the values written here are used.
    /// </summary>
    public sealed class LaunchConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/LaunchConfig";

        [Header("Where a run starts")]
        [Tooltip("Ticked: you start on the launch pad on Earth. Unticked: in orbit, as before Milestone 8.")]
        public bool startOnPad = true;

        [Header("Ignition and lift-off")]
        public LaunchRules rules = new LaunchRules();

        [Header("Shake (first person, view only)")]
        [Tooltip("How fast the shake judders.")]
        public float shakeSpeed = 21f;

        [Header("Fire, smoke and grit")]
        [Tooltip("Length of each engine's flame at full burn, in metres.")]
        public float flameLength = 3.4f;
        [Tooltip("How brightly the engines light the ground under the ship.")]
        public float groundLight = 2.4f;
        [Tooltip("How far that light reaches, in metres.")]
        public float groundLightReach = 45f;
        [Tooltip("Most smoke puffs alive at once (Steam Deck budget).")]
        public int maxSmoke = 110;
        [Tooltip("Most flying bits of grit alive at once.")]
        public int maxDebris = 60;

        [Header("Sound")]
        [Range(0f, 1f)] public float roarVolume = 0.85f;
        [Range(0f, 1f)] public float crackleVolume = 0.55f;
        [Range(0f, 1f)] public float ignitionVolume = 0.9f;

        [Header("The base")]
        [Tooltip("Seconds the service arm takes to swing clear.")]
        public float armSeconds = 0.9f;
        [Tooltip("How strongly the pad's lamps and floodlights glow.")]
        public float lampGlow = 2.6f;

        static LaunchConfig _cached;

        public static LaunchConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<LaunchConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<LaunchConfig>();
                _cached.name = "LaunchConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
