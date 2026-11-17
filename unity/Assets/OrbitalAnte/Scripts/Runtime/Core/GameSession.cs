using System;
using UnityEngine;

namespace OrbitalAnte.Core
{
    public enum GameMode
    {
        MainMenu,
        Playing,
    }

    /// <summary>Which test world a run takes place in.</summary>
    public enum Scenario
    {
        /// <summary>The combat test arena from Milestones 1 to 3.</summary>
        CombatArena = 0,
        /// <summary>Flying the solar system (Milestone 5 onward).</summary>
        SolarSystem = 1,
    }

    /// <summary>Run-wide state: the seed, whether we are in the menu or playing, and whether the game is paused.</summary>
    public static class GameSession
    {
        public const string BuildLabel = "Milestone 7 - ship";

        public static GameMode Mode { get; private set; } = GameMode.MainMenu;
        public static int Seed { get; private set; }
        public static bool Paused { get; private set; }
        public static Scenario Scenario { get; private set; } = Scenario.SolarSystem;

        public static void SetScenario(Scenario scenario) => Scenario = scenario;

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

        /// <summary>
        /// The named random stream for the current run. The same seed and name always give the same numbers,
        /// whatever else is going on. Ask for a stream once and keep it; asking again starts it over.
        /// </summary>
        public static SeededRandom Stream(string name) => SeededRandom.ForStream(Seed, name);

        public static void SetMode(GameMode mode)
        {
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }

        public static void SetPaused(bool paused)
        {
            Paused = paused;
            // Time is only ever set in TimeFlow, so pausing cannot be undone by a slow-mo ending underneath it.
            TimeFlow.Paused = paused;
            AudioListener.pause = paused;
            PauseChanged?.Invoke(paused);
        }

        /// <summary>Clears static state. Needed because Unity can keep statics alive between Play sessions.</summary>
        public static void ResetStatics()
        {
            Mode = GameMode.MainMenu;
            Paused = false;
            TimeFlow.Reset();
            AudioListener.pause = false;
            PauseChanged = null;
            ModeChanged = null;
        }
    }
}
