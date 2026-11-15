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
        [Tooltip("Seconds a dead enemy's body is left lying before it is cleared away.")]
        public float bodySeconds = 25f;
        [Header("NPC ships — trial settings for Fable")]
        public bool enemyShips = true;
        [Range(0, 2)] public int shipCap = 2;
        public EnemyShipTuning shipFlight = new EnemyShipTuning();
        public float shipHoverSeconds = 4f;
        public float crewOutsideSeconds = 18f;
        public float crewCabinSeconds = 12f;
        public float boardingSeekRange = 12f;

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
            if (_cached.shipFlight == null) _cached.shipFlight = new EnemyShipTuning();
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
