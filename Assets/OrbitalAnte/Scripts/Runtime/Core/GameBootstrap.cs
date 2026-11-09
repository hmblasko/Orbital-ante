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
            GameInput.EnsureCreated();
            RunSeed.EnsureInitialized();

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
