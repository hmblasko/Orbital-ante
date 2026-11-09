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
