using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// Stand-in hitscan used only to playtest Milestone 1. Milestone 3 replaces it with the
    /// real weapon data (AK spoof and test pistol).
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Debug Gun Config", fileName = "DebugGunConfig")]
    public class DebugGunConfig : ScriptableObject
    {
        public float damagePerHit = 34f;
        public float roundsPerMinute = 600f;
        public float range = 250f;
        public float forceScale = 1f;
        [Tooltip("Shove for plain physics objects that are not body parts.")]
        public float propImpulse = 4f;
        public Color tracerColor = new Color(1f, 0.85f, 0.45f);

        public static DebugGunConfig Load()
        {
            var c = Resources.Load<DebugGunConfig>("Config/DebugGunConfig");
            return c != null ? c : CreateInstance<DebugGunConfig>();
        }
    }
}
