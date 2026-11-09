using UnityEngine;

namespace OrbitalAnte.Combat
{
    public enum OneHandReload
    {
        [Tooltip("One hand can still reload, just slower.")]
        SlowReload,
        [Tooltip("Reloading needs two hands: a one-handed shooter is stuck with what's in the magazine.")]
        NoReload,
    }

    /// <summary>Rules shared by every weapon. Edit the asset in Resources/Config.</summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Combat Rules", fileName = "CombatRulesConfig")]
    public class CombatRulesConfig : ScriptableObject
    {
        [Header("Disarming")]
        public OneHandReload oneHandReload = OneHandReload.SlowReload;
        [Tooltip("Speed a gun is knocked away at when it is shot out of someone's hands (m/s).")]
        public float knockAwaySpeed = 6f;
        [Tooltip("How far a player can reach to pick up a gun (m).")]
        public float playerPickupRange = 2.6f;

        [Header("Test dummies with guns")]
        public float dummyRange = 30f;
        public float dummyExtraSpread = 2.5f;
        public int dummyBurstMin = 3;
        public int dummyBurstMax = 6;
        public float dummyPauseMin = 0.8f;
        public float dummyPauseMax = 1.8f;
        public float dummyFireRateScale = 0.6f;
        [Tooltip("How far an unarmed dummy will go to pick up a dropped gun (m).")]
        public float dummyPickupRadius = 10f;
        public float dummyAttackStopDistance = 9f;
        [Tooltip("A dummy only fires when the target is within this many degrees of where it faces.")]
        public float dummyFireConeDegrees = 30f;

        static CombatRulesConfig cached;

        public static CombatRulesConfig Get()
        {
            if (cached == null)
            {
                cached = Resources.Load<CombatRulesConfig>("Config/CombatRulesConfig");
                if (cached == null) cached = CreateInstance<CombatRulesConfig>();
            }
            return cached;
        }
    }
}
