using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// Deep Breath's dials: how long it lasts, how slow the world goes, what fighting gives back, and how it
    /// looks. Edit the asset at Assets/OrbitalAnte/Resources/Config/DeepBreathConfig; if it is missing the
    /// values written here are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Deep Breath Config", fileName = "DeepBreathConfig")]
    public sealed class DeepBreathConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/DeepBreathConfig";

        [Tooltip("seconds: real seconds a full meter lasts. slowTo: world speed while held. easeIn / easeOut: real seconds to slow down and to come back. leastToStart: share of the meter needed to begin. perLimb / perKill: share given back for a limb and for a kill.")]
        public DeepBreathRules rules = new DeepBreathRules();

        [Header("How it looks")]
        [Tooltip("The colour of the meter and of the cool rim round the view while the breath is held.")]
        public Color colour = new Color(0.62f, 0.86f, 1f);
        [Tooltip("How strong the cool rim round the view is at full slow, 0 (none) to 1.")]
        [Range(0f, 1f)] public float rimStrength = 0.4f;

        static DeepBreathConfig _cached;

        public static DeepBreathConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<DeepBreathConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<DeepBreathConfig>();
                _cached.name = "DeepBreathConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
