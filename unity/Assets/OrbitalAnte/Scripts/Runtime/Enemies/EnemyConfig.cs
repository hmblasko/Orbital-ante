using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Enemies
{
    /// <summary>
    /// The dials for who lives on the worlds: the kinds of enemy and how thickly they are spread. Loaded from
    /// Resources/Config/EnemyConfig, with built-in defaults if the asset is missing.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Enemy Config", fileName = "EnemyConfig")]
    public sealed class EnemyConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/EnemyConfig";

        [Tooltip("Untick to fly and walk about with nobody shooting at you.")]
        public bool enemiesInTheWorld = true;
        [Tooltip("The kinds of enemy. Each has its own size, pace, toughness, gun and way of fighting.")]
        public List<EnemyTypeSpec> roster = EnemyRoster.Defaults();
        [Tooltip("Which set of built-in kinds the list above came from. An older asset takes the new kinds when the game starts. Set it very high to keep your own list.")]
        public int rosterVersion = EnemyRoster.Version;
        public EnemyFieldRules field = new EnemyFieldRules();
        [Tooltip("How enemies take cover and go round your side. Untick Cover or Flank to compare with the old way of fighting.")]
        public OrbitalAnte.AI.TacticsRules tactics = new OrbitalAnte.AI.TacticsRules();
        [Tooltip("How far an enemy that has not seen you lately sees behind its back, against its sight ahead (1 = all round). Makes a sneak up for a knife takedown possible.")]
        [Range(0.1f, 1f)] public float behindSight = 0.3f;
        [Tooltip("Where Faction A lives (its home world) and what share of camps elsewhere are its squads.")]
        public FactionFieldRules factions = new FactionFieldRules();
        [Tooltip("Faction A's Scout: how long it raises its flare, and how far the flare is heard.")]
        public FlareRules flare = new FlareRules();
        [Tooltip("Faction A's Heavy: how long its gun spins up, how long it fires, how slowly it turns while braced.")]
        public SpinUpRules spinUp = new SpinUpRules();
        [Tooltip("Faction A's Line: its covering fire while a squadmate is out on a flank.")]
        public SuppressRules suppress = new SuppressRules();
        [Tooltip("Tick to give every kind of enemy a jointed body (corpses crumple, lost limbs fold as they fly), not only the kinds marked as jointed in the list above.")]
        public bool jointedBodiesForAll = false;
        [Tooltip("Seconds a dead enemy's body is left lying before it is cleared away.")]
        public float bodySeconds = 25f;

        static EnemyConfig _cached;

        public static EnemyConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<EnemyConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<EnemyConfig>();
                _cached.name = "EnemyConfig (defaults)";
            }
            if (_cached.roster == null || _cached.roster.Count == 0 || _cached.rosterVersion < EnemyRoster.Version)
            {
                _cached.roster = EnemyRoster.Defaults();
                _cached.rosterVersion = EnemyRoster.Version;
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
