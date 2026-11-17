using UnityEngine;

namespace OrbitalAnte.Melee
{
    /// <summary>
    /// Dials shared by every melee item (each item's own numbers are its <see cref="MeleeData"/> asset). Edit the
    /// asset at Assets/OrbitalAnte/Resources/Config/MeleeConfig; if it is missing the values written here are used.
    /// The hard limits (the 120 ms freeze cap, the fastest shove) are in <see cref="MeleeRules"/> and are not dials.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Melee Config", fileName = "MeleeConfig")]
    public sealed class MeleeConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/MeleeConfig";

        [Header("Knocked down")]
        [Tooltip("Seconds a knocked-down enemy cannot shoot or walk.")]
        public float knockdownSeconds = MeleeRules.KnockdownSeconds;
        [Tooltip("How fast a shoved enemy slows down as it slides, in m/s each second. Higher stops it sooner.")]
        public float shoveFriction = 6f;
        [Tooltip("The speed a loose limb, a corpse or a gun on the ground is knocked away at by a launching or crushing swing (m/s).")]
        public float looseLaunch = 4f;

        [Header("Zero-G")]
        [Tooltip("The player in the suit, kilograms, against which a struck thing's mass is weighed for the push back.")]
        public float playerMass = 120f;
        [Tooltip("The most a single swing can push a floating player back (m/s). Match Velocity catches you after.")]
        public float pushBackCap = 3f;

        [Header("Swing shape and cues")]
        [Tooltip("How far above and below the crosshair a swing reaches, degrees.")]
        public float verticalHalfAngle = MeleeRules.VerticalHalfAngle;
        [Tooltip("How far off a melee item on the ground can be picked up, metres.")]
        public float pickupRange = 2.6f;
        [Tooltip("Show the small mark under the crosshair when a swing would hit a living enemy.")]
        public bool showInRange = true;

        static MeleeConfig _cached;

        public static MeleeConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<MeleeConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<MeleeConfig>();
                _cached.name = "MeleeConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
