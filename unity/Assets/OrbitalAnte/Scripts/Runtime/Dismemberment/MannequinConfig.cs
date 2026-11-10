using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// Tunable numbers for the breakable test mannequin. An asset is created at
    /// Assets/OrbitalAnte/Resources/Config/MannequinConfig so the values can be changed in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Mannequin Config", fileName = "MannequinConfig")]
    public sealed class MannequinConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/MannequinConfig";

        [Header("Hit points (the test rifle does 34 per hit)")]
        [Tooltip("Destroying the head is the only way to kill.")]
        public float headHp = 90f;
        public float torsoHp = 240f;
        public float upperArmHp = 60f;
        public float forearmHp = 45f;
        public float handHp = 30f;
        public float thighHp = 68f;
        public float shinHp = 50f;
        public float footHp = 30f;

        [Header("Movement speed in metres per second")]
        public float walkSpeed = 2.0f;
        public float sprintSpeed = 4.6f;
        public float oneLegSpeed = 1.2f;
        public float crawlSpeed = 0.65f;
        public float wriggleSpeed = 0.28f;
        public float headRollSpeed = 0.5f;
        [Tooltip("How quickly a character turns to face where it is going, in degrees per second.")]
        public float turnSpeed = 220f;

        [Header("Gib launch (deliberately exaggerated)")]
        [Tooltip("Speed added along the bullet direction, in metres per second.")]
        public float gibLaunchSpeed = 7.5f;
        [Tooltip("Extra upward speed so parts arc instead of skidding.")]
        public float gibLift = 3.2f;
        [Tooltip("Random spin, in radians per second.")]
        public float gibSpin = 16f;
        [Tooltip("How hard a bullet shoves a gib that is already loose.")]
        public float gibPushSpeed = 3.5f;
        [Tooltip("Outward speed for limbs when the torso is destroyed.")]
        public float torsoBurstSpeed = 5f;
        [Tooltip("Seconds before a loose part is cleaned up. Milestone 2 replaces this with pooling and a fade.")]
        public float gibLifetime = 30f;

        [Header("Colours")]
        public Color torsoColor = new Color(0.93f, 0.45f, 0.13f);
        public Color headColor = new Color(0.96f, 0.93f, 0.85f);
        public Color upperLimbColor = new Color(0.80f, 0.78f, 0.72f);
        public Color lowerLimbColor = new Color(0.52f, 0.55f, 0.60f);
        public Color extremityColor = new Color(0.20f, 0.22f, 0.26f);
        public Color visorColor = new Color(0.10f, 0.12f, 0.16f);
        public Color woundColor = new Color(0.72f, 0.06f, 0.08f);

        public float HpFor(BodyPartId id)
        {
            switch (id)
            {
                case BodyPartId.Head: return headHp;
                case BodyPartId.Torso: return torsoHp;
                case BodyPartId.LeftUpperArm:
                case BodyPartId.RightUpperArm: return upperArmHp;
                case BodyPartId.LeftForearm:
                case BodyPartId.RightForearm: return forearmHp;
                case BodyPartId.LeftHand:
                case BodyPartId.RightHand: return handHp;
                case BodyPartId.LeftThigh:
                case BodyPartId.RightThigh: return thighHp;
                case BodyPartId.LeftShin:
                case BodyPartId.RightShin: return shinHp;
                default: return footHp;
            }
        }

        public float[] BuildHpTable()
        {
            var table = new float[BodyLayout.Count];
            for (int i = 0; i < BodyLayout.Count; i++) table[i] = Mathf.Max(1f, HpFor((BodyPartId)i));
            return table;
        }

        static MannequinConfig _cached;

        /// <summary>The config asset if it exists, otherwise built-in defaults.</summary>
        public static MannequinConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<MannequinConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<MannequinConfig>();
                _cached.name = "MannequinConfig (defaults)";
            }
            return _cached;
        }
    }
}
