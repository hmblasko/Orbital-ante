using UnityEngine;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// The gun ladder's dials: which guns, in what order, and how many kills each one takes. Edit the asset at
    /// Assets/OrbitalAnte/Resources/Config/GunLadderConfig; if it is missing the values written here are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Gun Ladder Config", fileName = "GunLadderConfig")]
    public sealed class GunLadderConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/GunLadderConfig";

        [Tooltip("enabled: untick to start every run with the rifle and keep it (no ladder). rungs: the guns in the order they are handed out, weakest first. Weapon Id is the gun's file name in Resources/Weapons (Pistol, Smg, Rifle, Shotgun, Sniper). Kills To Next is how many kills that gun takes before the next one; the last gun keeps it to the end of the run.")]
        public GunLadderRules rules = new GunLadderRules();

        [Header("How it looks")]
        [Tooltip("The colour of the ladder readout and of the banner when a new gun is handed over.")]
        public Color colour = new Color(1f, 0.78f, 0.36f);
        [Tooltip("Real seconds the 'new gun' banner stays on screen.")]
        public float bannerSeconds = 2.2f;

        static GunLadderConfig _cached;

        public static GunLadderConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<GunLadderConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<GunLadderConfig>();
                _cached.name = "GunLadderConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
