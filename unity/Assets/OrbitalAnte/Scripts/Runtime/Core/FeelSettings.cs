using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Player-facing comfort toggles for game feel, changed from the pause menu and remembered between sessions.
    /// </summary>
    public static class FeelSettings
    {
        const string HitstopKey = "oa.feel.hitstop";
        const string ShakeKey = "oa.feel.shake";
        const string SlowMoKey = "oa.feel.headshotSlowMo";

        public static bool Hitstop
        {
            get => PlayerPrefs.GetInt(HitstopKey, 1) != 0;
            set => Set(HitstopKey, value);
        }

        public static bool CameraShake
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) != 0;
            set => Set(ShakeKey, value);
        }

        public static bool HeadshotSlowMo
        {
            get => PlayerPrefs.GetInt(SlowMoKey, 1) != 0;
            set => Set(SlowMoKey, value);
        }

        const string OverlayKey = "oa.hud.performanceOverlay";
        static int _overlay = -1;

        /// <summary>
        /// The performance overlay in the top left (frame times, counts, the Deck budget). Off unless asked for
        /// (Matthew, 17 Nov 2026: the screen had too many stats); F3 or the Run Stats page in the pause menu turns it
        /// on, and the choice is remembered. Read every frame, so it is kept in memory after the first read.
        /// </summary>
        public static bool PerformanceOverlay
        {
            get
            {
                if (_overlay < 0) _overlay = PlayerPrefs.GetInt(OverlayKey, 0);
                return _overlay != 0;
            }
            set
            {
                _overlay = value ? 1 : 0;
                Set(OverlayKey, value);
            }
        }

        static void Set(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
