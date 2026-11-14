using UnityEngine;

namespace OrbitalAnte.Spacewalk
{
    /// <summary>
    /// The suit's dials: jets, air, tether, and how they look and sound. Edit the asset at
    /// Assets/OrbitalAnte/Resources/Config/SuitConfig; if it is missing the values written here are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Suit Config", fileName = "SuitConfig")]
    public sealed class SuitConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/SuitConfig";

        public SuitRules rules = new SuitRules();

        [Header("Floating")]
        [Tooltip("How fast the roll buttons turn you over when floating, degrees per second.")]
        public float rollSpeed = 80f;

        [Header("Jets you can see")]
        [Tooltip("The colour of the jets. Cold gas, so pale blue, not the engines' orange.")]
        public Color jetColour = new Color(0.62f, 0.86f, 1f);
        [Tooltip("How long a jet's flame is at full push, metres.")]
        public float jetLength = 0.55f;
        [Tooltip("How strongly the jets light the rim of your visor, 0 to 1.")]
        [Range(0f, 1f)] public float visorGlow = 0.55f;
        public Color tetherColour = new Color(0.95f, 0.55f, 0.16f);

        [Header("Sound")]
        [Range(0f, 1f)] public float volume = 0.6f;

        static SuitConfig _cached;

        public static SuitConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<SuitConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<SuitConfig>();
                _cached.name = "SuitConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
