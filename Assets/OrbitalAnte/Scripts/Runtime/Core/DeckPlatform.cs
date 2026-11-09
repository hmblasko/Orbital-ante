using UnityEngine;

namespace OrbitalAnte.Core
{
    public static class DeckPlatform
    {
        static bool? isDeck;

        /// <summary>Steam sets SteamDeck=1 for games launched on a Deck.</summary>
        public static bool IsSteamDeck
        {
            get
            {
                if (isDeck == null)
                {
                    string env = System.Environment.GetEnvironmentVariable("SteamDeck");
                    isDeck = env == "1" || SystemInfo.operatingSystem.Contains("SteamOS");
                }
                return isDeck.Value;
            }
        }

        public const int TargetWidth = 1280;
        public const int TargetHeight = 800;

        /// <summary>Frame pacing defaults. Deck runs vsync at 60; everything else matches.</summary>
        public static void ApplyFramePacing()
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
    }
}
