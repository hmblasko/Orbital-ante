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

        [Header("Navigation assist (first-pass values for Fable to tune)")]
        [Tooltip("Metres above the body's outer envelope, including the terrain's maximum-height estimate. Autopilot approaches; it does not land.")]
        [Min(10f)] public float autopilotClearance = 150f;
        [Tooltip("Maximum translational acceleration used by autopilot, before damaged-engine limits.")]
        [Min(0f)] public float autopilotAcceleration = 30f;
        [Tooltip("Cruise speed relative to the moving destination, in metres per second.")]
        [Min(1f)] public float autopilotCruiseSpeed = 300f;
        [Tooltip("Seconds used to settle toward the requested relative velocity.")]
        [Min(0.05f)] public float assistResponseTime = 0.5f;
        [Min(0.1f)] public float autopilotArrivalDistance = 3f;
        [Min(0.01f)] public float autopilotArrivalSpeed = 0.5f;
        [Tooltip("Extra clearance around intervening celestial bodies. This is a route refusal check, not obstacle pathfinding.")]
        [Min(0f)] public float autopilotRouteClearance = 20f;
        [Tooltip("Angular forgiveness beyond a planet's visible disc, in degrees, when pressing Target Lock.")]
        [Range(0f, 20f)] public float targetLockCone = 7f;
        [Tooltip("A manual translational input above this cancels an active assist.")]
        [Range(0.01f, 0.5f)] public float assistCancelInput = 0.1f;

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
