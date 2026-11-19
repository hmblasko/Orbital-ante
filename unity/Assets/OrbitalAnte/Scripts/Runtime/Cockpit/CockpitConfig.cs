using UnityEngine;

namespace OrbitalAnte.Cockpit
{
    public enum DashPlacement
    {
        /// <summary>A real panel standing on the desk in front of the seat.</summary>
        OnDesk,
        /// <summary>A strip along the bottom of the screen. For when the desk panel is too small to read.</summary>
        OnScreen,
    }

    /// <summary>The dials of the cockpit: which dash, where it sits, fuel and air, and the ship's cameras.</summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Cockpit Config", fileName = "CockpitConfig")]
    public sealed class CockpitConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/CockpitConfig";

        [Header("The dash")]
        [Tooltip("Which design shows when a run starts. Flip through them in play with the Next Dash Design button.")]
        public DashStyle style = DashStyle.FlightDeck;
        public DashPlacement placement = DashPlacement.OnDesk;
        [Tooltip("Where the middle of the desk panel is, in ship space (metres; +Y up, +Z nose).")]
        public Vector3 deskPosition = new Vector3(0f, 0.9f, 1.94f);
        [Tooltip("How wide the desk panel is, in metres. Its height follows.")]
        public float deskWidth = 1.1f;
        [Tooltip("How far the desk panel leans back, in degrees.")]
        public float deskLean = 24f;
        [Tooltip("On-screen placement only: the strip's width as a share of the screen's.")]
        [Range(0.3f, 1f)] public float screenShare = 0.62f;
        [Tooltip("How many times a second the dash is redrawn. Needles ease between.")]
        [Range(10, 60)] public int redrawsPerSecond = 30;

        [Header("Fuel and air")]
        public SupplyRules supplies = new SupplyRules();

        [Header("Cameras")]
        [Tooltip("Size of the picture on the dash's little camera screen.")]
        public int feedWidth = 240;
        public int feedHeight = 150;
        [Tooltip("The dash's little camera screen is refreshed once every this many frames.")]
        [Range(1, 12)] public int feedEveryFrames = 6;
        [Tooltip("Seconds for your head to tip down to the screen before the camera takes over the view.")]
        public float lookDownSeconds = 0.28f;
        [Tooltip("How far your head tips, in degrees.")]
        public float lookDownDegrees = 26f;
        [Tooltip("How far the dash's little camera screen can see, in metres. The full view, when you look down, sees everything.")]
        public float feedFarClip = 30000f;
        public float landingFieldOfView = 82f;
        public float poleFieldOfView = 66f;

        static CockpitConfig _cached;

        public static CockpitConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<CockpitConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<CockpitConfig>();
                _cached.name = "CockpitConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
