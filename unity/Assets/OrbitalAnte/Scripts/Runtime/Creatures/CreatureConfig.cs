using UnityEngine;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// Dials for the alien creatures. Loaded from Resources/Config/CreatureConfig, with built-in defaults if the
    /// asset is missing.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Creature Config", fileName = "CreatureConfig")]
    public sealed class CreatureConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/CreatureConfig";

        [Tooltip("The Strider (the spider crab): pace, sight, its claw strike, hit points, loose pieces.")]
        public StriderRules strider = new StriderRules();

        [Tooltip("How many Striders the combat test arena starts with (0 for none).")]
        [Range(0, 3)] public int arenaStriders = 1;

        static CreatureConfig _cached;

        public static CreatureConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<CreatureConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<CreatureConfig>();
                _cached.name = "CreatureConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
