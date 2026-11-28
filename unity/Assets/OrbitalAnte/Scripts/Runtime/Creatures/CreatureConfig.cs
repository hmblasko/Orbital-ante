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
        [Tooltip("The Blind stalker (from the olm): what it hears, how fast it comes, its warning and lunge.")]
        public StalkerRules stalker = new StalkerRules();

        [Tooltip("The Drifting maw (the megamouth shark): how it cruises, the warning, how far and how hard it draws you in, the hold and the bites, hit points.")]
        public MawRules maw = new MawRules();
        [Tooltip("The Pack biter (the Goliath tigerfish): how a pack rings you, the warning, the dart and its bite, the gap between turns, hit points.")]
        public BiterRules biter = new BiterRules();
        [Tooltip("The big swimmers in the seas: how many haunts a world has, how deep the water must be, when one is woken, how many at once. Untick the first box to leave the seas empty.")]
        public SeaFieldRules seas = new SeaFieldRules();
        [Tooltip("The creatures of the land on the worlds (the Strider, the Sprinter, the Roller and the Screamer): how many haunts a world has, how far they keep from the camps and the launch pad, how many are lived in at each ante, when one is woken, how many at once, how steep each kind walks. Untick the first box to leave the ground to the camps.")]
        public LandFieldRules land = new LandFieldRules();

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

        [Tooltip("How many Blind stalkers the combat test arena starts with (0 for none).")]
        [Range(0, 2)] public int arenaStalkers = 1;

        [Tooltip("How many Drifting maws the combat test arena starts with (0 for none). There is no water there: it swims in the air just over the ground.")]
        [Range(0, 1)] public int arenaMaws = 1;

        [Tooltip("How many packs of Pack biters the combat test arena starts with (0 for none). They swim in the air just over the ground.")]
        [Range(0, 2)] public int arenaBiterPacks = 1;

        [Tooltip("How many fish in each of the arena's packs.")]
        [Range(1, 6)] public int arenaPackSize = 3;

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
