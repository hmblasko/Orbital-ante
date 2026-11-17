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

        static void Set(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
