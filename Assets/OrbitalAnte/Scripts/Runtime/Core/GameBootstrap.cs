using OrbitalAnte.Arena;
using OrbitalAnte.Input;
using UnityEngine;

namespace OrbitalAnte.Core
{
    public enum BootMode
    {
        MainMenu,
        TestArena,
    }

    /// <summary>
    /// The only object saved in each scene. Everything else is built in code from data, so
    /// scenes never drift out of sync with the scripts.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public BootMode Mode = BootMode.TestArena;

        void Awake()
        {
            DeckPlatform.ApplyFramePacing();
            Perf.PerfMonitor.EnsureCreated();
            GameInput.EnsureCreated();
            RunSeed.EnsureInitialized();

            // Preferences the player has chosen carry over between sessions.
            var profile = Save.SaveSystem.Current.profile;
            if (profile.headshotSlowMoChosen) Feel.GameFeel.HeadshotSlowMo = profile.headshotSlowMo;

            switch (Mode)
            {
                case BootMode.MainMenu:
                    gameObject.AddComponent<MainMenuScreen>();
                    break;
                case BootMode.TestArena:
                    gameObject.AddComponent<ArenaDirector>();
                    break;
            }
        }
    }
}
