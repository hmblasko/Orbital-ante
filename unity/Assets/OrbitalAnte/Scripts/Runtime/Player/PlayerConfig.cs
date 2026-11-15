using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// Tunable numbers for the test-arena player. An asset is created at
    /// Assets/OrbitalAnte/Resources/Config/PlayerConfig so the values can be changed in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/PlayerConfig";

        [Header("Movement")]
        public float walkSpeed = 5f;
        public float sprintSpeed = 8f;
        [Tooltip("How quickly the player reaches full speed on the ground, in metres per second squared.")]
        public float groundAcceleration = 60f;
        public float airAcceleration = 14f;
        public float jumpHeight = 1.1f;
        public float gravity = 22f;
        [Tooltip("Seconds after walking off an edge during which a jump still works.")]
        public float coyoteTime = 0.1f;

        [Header("Crouch and lean — initial trials for Fable's feel review")]
        [Tooltip("Trial crouched capsule height; clamped to at least its diameter, with the boots kept in place.")]
        public float crouchHeight = 1.15f;
        public float crouchEyeHeight = 1.02f;
        public float crouchSpeedShare = 0.5f;
        [Tooltip("Trial stance transition speed in metres per second. A blocked stand retries until clear.")]
        public float stanceSpeed = 3.5f;
        [Tooltip("Trial sideways peek distance in metres. Solid geometry always limits it.")]
        public float leanDistance = 0.28f;
        public float leanDegrees = 12f;
        [Tooltip("Trial lean transitions per second.")]
        public float leanSpeed = 7f;
        [Tooltip("Trial head clearance radius. The camera near-plane corners are always protected too.")]
        public float headClearanceRadius = 0.14f;

        [Header("Parkour — initial trials for Fable's feel review")]
        public bool parkourEnabled = true;
        [Tooltip("Trial minimum pull for upright wall-running and climbing; floating controls keep priority below it.")]
        public float parkourMinimumGravity = 1f;
        [Tooltip("Trial side-wall reach, measured beyond the capsule radius.")]
        public float wallReach = 0.22f;
        public float wallMinimumSpeed = 3.5f;
        public float wallRunSeconds = 0.9f;
        [Tooltip("Trial share of gravity while running; no upward boost is applied.")]
        public float wallGravityShare = 0.3f;
        public float wallFallSpeed = 2.5f;
        [Tooltip("Trial maximum absolute dot between forward and the wall normal: smaller means more parallel.")]
        public float wallParallelDot = 0.35f;
        public float wallJumpAwaySpeed = 4f;
        public float wallJumpUpSpeed = 5f;
        [Tooltip("Climbing needs a fresh airborne Jump press and forward input. Keep Jump held to finish; release to drop.")]
        public float climbReach = 0.65f;
        public float climbMinimumRise = 0.45f;
        public float climbMaximumRise = 1.65f;
        public float climbSpeed = 2.8f;
        public float climbSeconds = 1.5f;

        [Header("Looking")]
        [Tooltip("Degrees turned per unit of mouse movement.")]
        public float mouseSensitivity = 0.09f;
        [Tooltip("Degrees per second at full stick deflection.")]
        public float stickYawSpeed = 190f;
        public float stickPitchSpeed = 120f;
        [Tooltip("Above 1 makes small stick movements finer.")]
        public float stickCurve = 1.6f;
        public float fieldOfView = 80f;

        [Header("Health (bare minimum so enemies shooting back can be tested)")]
        public float maxHealth = 100f;
        [Tooltip("Seconds without being hit before health starts coming back.")]
        public float regenDelay = 4f;
        public float regenPerSecond = 18f;
        [Tooltip("Seconds on the floor before getting back up.")]
        public float respawnDelay = 2.2f;

        [Header("Weapons")]
        [Tooltip("How close a dropped weapon must be to pick it up, in metres.")]
        public float pickupRange = 2.6f;
        [Tooltip("How hard a swapped-out weapon is tossed forward, in metres per second.")]
        public float dropTossSpeed = 3.5f;

        static PlayerConfig _cached;

        public static PlayerConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<PlayerConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<PlayerConfig>();
                _cached.name = "PlayerConfig (defaults)";
            }
            return _cached;
        }
    }
}
