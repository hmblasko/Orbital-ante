using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// Tunable numbers for the stand-in test craft. An asset is created at
    /// Assets/OrbitalAnte/Resources/Config/TestCraftConfig. The real ship (Milestones 7 and 10) will have its own.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Test Craft Config", fileName = "TestCraftConfig")]
    public sealed class TestCraftConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/TestCraftConfig";

        [Header("Thrusters")]
        [Tooltip("Push in any direction, in metres per second squared. Earth's gravity is 9.8.")]
        public float thrust = 30f;
        [Tooltip("Thrust is multiplied by this while Boost is held. For crossing between planets.")]
        public float boostMultiplier = 2.5f;
        [Tooltip("How hard Match Velocity brakes, in metres per second squared.")]
        public float matchVelocityThrust = 60f;

        [Header("Autopilot (flies to the locked body and stops above it)")]
        public OrbitalAnte.Navigation.AutopilotRules autopilot = new OrbitalAnte.Navigation.AutopilotRules();

        [Header("Turning")]
        [Tooltip("Degrees turned per unit of mouse movement.")]
        public float mouseSensitivity = 0.08f;
        [Tooltip("Degrees per second at full stick.")]
        public float stickTurnSpeed = 110f;
        [Tooltip("Degrees per second of roll.")]
        public float rollSpeed = 100f;
        [Tooltip("How quickly a spin picked up from a bump is steadied. Higher is steadier.")]
        public float spinDamping = 4f;

        [Header("View")]
        public float fieldOfView = 80f;

        [Header("Start")]
        [Tooltip("The body the run starts in orbit round.")]
        public string startBody = "Earth";
        [Tooltip("Height of the starting orbit above the surface, in metres.")]
        public float startAltitude = 450f;

        [Header("Sound")]
        [Range(0f, 1f)] public float thrusterVolume = 0.5f;

        static TestCraftConfig _cached;

        public static TestCraftConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<TestCraftConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<TestCraftConfig>();
                _cached.name = "TestCraftConfig (defaults)";
            }
            return _cached;
        }
    }
}
