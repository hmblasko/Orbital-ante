using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>Tunable on-foot movement and look values. Edit the asset in Resources/Config.</summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Player Config", fileName = "PlayerConfig")]
    public class PlayerConfig : ScriptableObject
    {
        [Header("Movement (m/s)")]
        public float walkSpeed = 4.6f;
        public float sprintSpeed = 7.2f;
        public float crouchSpeed = 2.3f;
        public float groundAcceleration = 55f;
        public float airAcceleration = 10f;
        public float jumpHeight = 1.15f;
        public float gravity = -22f;

        [Header("Body (m)")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.1f;
        public float radius = 0.32f;
        public float eyeBelowTop = 0.14f;
        public float crouchTransitionSpeed = 9f;

        [Header("Look")]
        [Tooltip("Degrees per mouse count.")]
        public float mouseSensitivity = 0.09f;
        [Tooltip("Degrees per second at full stick.")]
        public float stickSensitivity = 170f;
        [Tooltip("1 = linear, 2 = squared (finer aim near centre).")]
        public float stickResponseExponent = 2f;
        public bool invertY = false;
        public float fieldOfView = 80f;

        [Header("Physics fun")]
        [Tooltip("How hard walking into loose physics objects (gibs) shoves them.")]
        public float pushStrength = 1.2f;

        public static PlayerConfig Load()
        {
            var c = Resources.Load<PlayerConfig>("Config/PlayerConfig");
            return c != null ? c : CreateInstance<PlayerConfig>();
        }
    }
}
