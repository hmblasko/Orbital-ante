using UnityEngine;

namespace OrbitalAnte.Rover
{
    /// <summary>
    /// The moon rover's dials. Edit the asset at Assets/OrbitalAnte/Resources/Config/RoverConfig; if it is missing
    /// the values written in <see cref="RoverRules"/> are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Rover Config", fileName = "RoverConfig")]
    public sealed class RoverConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/RoverConfig";

        [Tooltip("Off: the lander carries no rover.")]
        public bool carried = true;

        public RoverRules rules = new RoverRules();

        [Header("Sound")]
        [Tooltip("The motors' whine heard through the seat while you drive.")]
        [Range(0f, 1f)] public float motorVolume = 0.35f;

        static RoverConfig _cached;

        public static RoverConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<RoverConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<RoverConfig>();
                _cached.name = "RoverConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
