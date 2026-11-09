using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrbitalAnte.Core
{
    public static class SceneFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string TestArenaScene = "TestArena";

        static void ResetTime()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public static void StartRun(bool newSeed)
        {
            if (newSeed) RunSeed.NewRandom();
            else RunSeed.EnsureInitialized();
            ResetTime();
            SceneManager.LoadScene(TestArenaScene);
        }

        /// <summary>Picks the saved run back up (same seed). Falls back to a new run if there is none.</summary>
        public static void ContinueRun()
        {
            var run = Save.SaveSystem.Current.run;
            if (!run.active)
            {
                StartRun(newSeed: true);
                return;
            }
            RunSeed.Set(run.seed);
            StartRun(newSeed: false);
        }

        public static void ToMainMenu()
        {
            ResetTime();
            SceneManager.LoadScene(MainMenuScene);
        }

        public static void QuitToDesktop()
        {
            ResetTime();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
