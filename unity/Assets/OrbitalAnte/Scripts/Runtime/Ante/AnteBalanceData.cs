using UnityEngine;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The asset that holds the ante and point numbers so they can be changed in the Inspector
    /// (Assets/OrbitalAnte/Resources/Config/AnteBalance). If the asset does not exist the code defaults in
    /// <see cref="AnteBalance"/> are used, so nothing breaks without it.
    ///
    /// Nothing in the game reads this yet: the ante loop is Milestone 14, after the Phase 1 sign-off.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Ante Balance", fileName = "AnteBalance")]
    public sealed class AnteBalanceData : ScriptableObject
    {
        public const string ResourcePath = "Config/AnteBalance";

        [Tooltip("Provisional starting values from the point-system handoff of 13 Nov 2026. Not balanced by play yet.")]
        public AnteBalance balance = new AnteBalance();

        static AnteBalanceData _cached;

        public static AnteBalanceData Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<AnteBalanceData>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<AnteBalanceData>();
                _cached.name = "AnteBalance (defaults)";
            }
            return _cached;
        }
    }
}
