using UnityEngine;

namespace OrbitalAnte.Meteors
{
    /// <summary>
    /// The meteor strikes' dials. Edit the asset at Assets/OrbitalAnte/Resources/Config/MeteorConfig; if it is
    /// missing the values written in <see cref="MeteorRules"/> are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Meteor Config", fileName = "MeteorConfig")]
    public sealed class MeteorConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/MeteorConfig";

        public MeteorRules rules = new MeteorRules();

        [Header("Look")]
        [Tooltip("Smoke puffs left along the path, per metre of fall (on worlds with air).")]
        [Range(0f, 0.3f)] public float trailPuffsPerMetre = 0.08f;
        [Tooltip("The most smoke, fire, dust and grit lumps alive at once, all strikes together.")]
        [Range(50, 1200)] public int maxParticles = 700;
        [Tooltip("How long the smoke over a crater keeps rising, in seconds.")]
        [Range(0f, 60f)] public float smokeSeconds = 18f;

        [Header("Sound")]
        [Range(0f, 1f)] public float volume = 1f;

        static MeteorConfig _cached;

        public static MeteorConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<MeteorConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<MeteorConfig>();
                _cached.name = "MeteorConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
