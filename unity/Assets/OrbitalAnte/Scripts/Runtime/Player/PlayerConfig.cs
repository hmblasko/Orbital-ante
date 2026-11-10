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
