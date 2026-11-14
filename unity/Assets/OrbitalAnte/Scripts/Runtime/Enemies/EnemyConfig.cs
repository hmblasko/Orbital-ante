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
        public EnemyFieldRules field = new EnemyFieldRules();
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
            if (_cached.roster == null || _cached.roster.Count == 0) _cached.roster = EnemyRoster.Defaults();
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
