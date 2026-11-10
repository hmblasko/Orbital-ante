using System;
using UnityEngine;

namespace OrbitalAnte.Core
{
    public enum GameMode
    {
        MainMenu,
        Playing,
    }

    /// <summary>Run-wide state: the seed, whether we are in the menu or playing, and whether the game is paused.</summary>
    public static class GameSession
    {
        public const string BuildLabel = "Milestone 2 - gibs with juice";

        public static GameMode Mode { get; private set; } = GameMode.MainMenu;
        public static int Seed { get; private set; }
        public static bool Paused { get; private set; }

        /// <summary>Raised after the pause state changes.</summary>
        public static event Action<bool> PauseChanged;
        /// <summary>Raised after the mode changes.</summary>
        public static event Action<GameMode> ModeChanged;

        public static int NewRandomSeed()
        {
            // The seed itself is the only thing that is allowed to come from the clock.
            int seed = unchecked(Environment.TickCount * 73856093) ^ (int)DateTime.UtcNow.Ticks;
            seed &= 0x7FFFFFFF;
            return seed % 1000000;
        }

        public static void SetSeed(int seed) => Seed = seed;

        public static void SetMode(GameMode mode)
        {
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }

        public static void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            PauseChanged?.Invoke(paused);
        }

        /// <summary>Clears static state. Needed because Unity can keep statics alive between Play sessions.</summary>
        public static void ResetStatics()
        {
            Mode = GameMode.MainMenu;
            Paused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            PauseChanged = null;
            ModeChanged = null;
        }
    }
}
