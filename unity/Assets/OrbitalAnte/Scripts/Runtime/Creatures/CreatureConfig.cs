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

        [Tooltip("The Roller (the giant isopod): pace on foot, its roll and the warning before it, how long it lies on its back, hit points.")]
        public RollerRules roller = new RollerRules();

        [Tooltip("The Cave hunter (the giant centipede): pace, its weave, the rear and the strike, how long its head lies still, hit points.")]
        public HunterRules hunter = new HunterRules();

        [Tooltip("The Screamer (the cicada): its flight, where it lands, the warning, the scream's reach, cone and hurt, how long it sags afterwards, hit points.")]
        public ScreamerRules screamer = new ScreamerRules();

        [Tooltip("The Ambusher (the bobbit worm): how far it feels footsteps and reaches, the warning, the strike, how long it holds on and drags, tunnelling, hit points.")]
        public AmbusherRules ambusher = new AmbusherRules();

        [Tooltip("How many Striders the combat test arena starts with (0 for none).")]
        [Range(0, 3)] public int arenaStriders = 1;

        [Tooltip("How many Sprinters the combat test arena starts with (0 for none).")]
        [Range(0, 4)] public int arenaSprinters = 2;

        [Tooltip("How many Rollers the combat test arena starts with (0 for none).")]
        [Range(0, 2)] public int arenaRollers = 1;

        [Tooltip("How many Cave hunters the combat test arena starts with (0 for none).")]
        [Range(0, 2)] public int arenaHunters = 1;

        [Tooltip("How many Screamers the combat test arena starts with (0 for none).")]
        [Range(0, 2)] public int arenaScreamers = 1;

        [Tooltip("How many Ambushers the combat test arena starts with (0 for none).")]
        [Range(0, 2)] public int arenaAmbushers = 1;

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
