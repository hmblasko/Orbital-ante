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
        public WalkerRules strider = new WalkerRules();

        [Tooltip("The Sprinter (the camel spider): pace, its dash and pause, its lunging bite, hit points.")]
        public WalkerRules sprinter = WalkerRules.ForSprinter();

        [Tooltip("How many Striders the combat test arena starts with (0 for none).")]
        [Range(0, 3)] public int arenaStriders = 1;

        [Tooltip("How many Sprinters the combat test arena starts with (0 for none).")]
        [Range(0, 4)] public int arenaSprinters = 2;

        public WalkerRules RulesFor(WalkerLayout kind) => kind == WalkerLayout.Sprinter ? sprinter : strider;

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
