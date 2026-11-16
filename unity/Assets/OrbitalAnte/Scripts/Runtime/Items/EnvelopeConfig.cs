using UnityEngine;

namespace OrbitalAnte.Items
{
    /// <summary>
    /// The envelope's dials. Edit the asset at Assets/OrbitalAnte/Resources/Config/EnvelopeConfig; if it is missing
    /// the values written here are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Envelope Config", fileName = "EnvelopeConfig")]
    public sealed class EnvelopeConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/EnvelopeConfig";

        [Tooltip("The chance of each rarity when an envelope opens (any numbers; they are shared out as percentages and printed on the envelope).")]
        public PrizeOdds odds = new PrizeOdds();
        [Tooltip("Show the sealed envelope at the start of every run.")]
        public bool showAtRunStart = true;
        [Tooltip("Close the run-start envelope by itself after this many seconds (0 waits for a press).")]
        public float runStartAutoClose = 0f;
        [Tooltip("Offer test envelopes in the collection page (editor and development builds only).")]
        public bool offerTestEnvelopes = true;

        static EnvelopeConfig _cached;

        public static EnvelopeConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<EnvelopeConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<EnvelopeConfig>();
                _cached.name = "EnvelopeConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
