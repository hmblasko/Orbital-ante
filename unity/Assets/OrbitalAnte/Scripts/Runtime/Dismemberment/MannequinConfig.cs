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
        public float gibLaunchSpeed = 9f;
        [Tooltip("Extra upward speed so parts arc instead of skidding.")]
        public float gibLift = 4f;
        [Tooltip("Random spin, in radians per second.")]
        public float gibSpin = 22f;
        [Tooltip("How hard a bullet shoves a gib that is already loose.")]
        public float gibPushSpeed = 3.5f;
        [Tooltip("Outward speed for limbs when the torso is destroyed.")]
        public float torsoBurstSpeed = 5f;

        [Header("Combat (how armed characters fight)")]
        [Tooltip("How far away a character will start shooting at the player, in metres.")]
        public float engageRange = 45f;
        [Tooltip("Seconds between first seeing the player and the first shot. Never instant.")]
        public float reactionTime = 0.6f;
        [Tooltip("Aim error added on top of the gun's own spread, in degrees. Higher is kinder to the player.")]
        public float aimError = 3f;
        [Tooltip("Shortest and longest burst from an automatic gun, in seconds.")]
        public Vector2 burstSeconds = new Vector2(0.45f, 0.9f);
        [Tooltip("Shortest and longest pause between bursts or single shots, in seconds.")]
        public Vector2 burstPause = new Vector2(0.6f, 1.3f);
        [Tooltip("Further than this from the player, it works its way closer between bursts, in metres.")]
        public float keepFar = 18f;
        [Tooltip("Nearer than this, it backs off between bursts, in metres.")]
        public float keepNear = 7f;
        [Tooltip("Runs, not walks, when it shifts position in a fight.")]
        public bool rushes = false;
        [Tooltip("Stands its ground and shoots: never shifts position while it can see the player.")]
        public bool holdsStill = false;
        [Tooltip("How far an unarmed character will go to fetch a dropped gun, in metres.")]
        public float weaponSeekRange = 30f;
        [Tooltip("How close it must get to pick the gun up, in metres.")]
        public float pickupReach = 1.1f;
        [Tooltip("The toggle from the design: can a gun be reloaded with only one hand? Off means a one-handed character is stuck once the magazine is empty.")]
        public bool oneHandedReloadAllowed = true;
        [Tooltip("How fast a gun flies when it is shot out of someone's hands, in metres per second.")]
        public float disarmKnockSpeed = 6f;

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
