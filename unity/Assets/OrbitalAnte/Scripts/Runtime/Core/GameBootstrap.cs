using OrbitalAnte.Arena;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Starts the game. One of these lives in the scene (the setup tool puts it there); if a scene has none,
    /// one is created automatically, so pressing Play in any scene works.
    ///
    /// It owns the menus and builds or tears down the world. Restart and "quit to menu" rebuild the world in
    /// place rather than reloading a scene.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        Transform _uiRoot;
        MainMenu _mainMenu;
        PauseMenu _pauseMenu;
        Hud _hud;
        Camera _menuCamera;
        AudioListener _menuListener;
        GameObject _world;
        TestArena _arena;
        TimeFx _timeFx;

        public TestArena Arena => _arena;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Unity can keep static values between Play sessions; start clean every time.
            Instance = null;
            GameSession.ResetStatics();
            CombatEvents.ResetStatics();
            Dismemberment.Mannequin.ResetStatics();
            Dismemberment.GibManager.ResetStatics();
            Gore.GoreFx.ResetStatics();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Instance != null) return;
            // Automated tests build their own little worlds and do not want the whole game on top.
            if (SceneManager.GetActiveScene().name.StartsWith("InitTestScene")) return;
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Application.targetFrameRate = -1;
            Layers.Configure();
            GameInput.Initialize();
            SilenceSceneLeftovers();
            BuildUi();
            _timeFx = gameObject.AddComponent<TimeFx>();

            GameSession.PauseChanged += OnPauseChanged;
            ShowMainMenu();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                GameSession.PauseChanged -= OnPauseChanged;
                Instance = null;
            }
        }

        /// <summary>
        /// A scene made with Unity's default template has its own camera and light. The game builds its own,
        /// so anything already in the scene is switched off to avoid doubles.
        /// </summary>
        void SilenceSceneLeftovers()
        {
            foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.gameObject.SetActive(false);
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.gameObject.SetActive(false);
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) listener.enabled = false;
            foreach (EventSystem es in FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) es.gameObject.SetActive(false);
        }

        void BuildUi()
        {
            _uiRoot = new GameObject("UI").transform;
            _uiRoot.SetParent(transform, false);

            // Event system wired to our own UI actions, so menus work with mouse, keyboard and controller.
            var esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(_uiRoot, false);
            esGo.AddComponent<EventSystem>();
            var module = esGo.AddComponent<InputSystemUIInputModule>();
            module.actionsAsset = GameInput.Asset;
            module.move = InputActionReference.Create(GameInput.UiNavigate);
            module.submit = InputActionReference.Create(GameInput.UiSubmit);
            module.cancel = InputActionReference.Create(GameInput.UiCancel);
            module.point = InputActionReference.Create(GameInput.UiPoint);
            module.leftClick = InputActionReference.Create(GameInput.UiClick);
            module.scrollWheel = InputActionReference.Create(GameInput.UiScroll);
            GameInput.UI.Enable();

            // A plain camera so the title screen has a backdrop.
            var camGo = new GameObject("MenuCamera");
            camGo.transform.SetParent(transform, false);
            _menuCamera = camGo.AddComponent<Camera>();
            _menuCamera.clearFlags = CameraClearFlags.SolidColor;
            _menuCamera.backgroundColor = new Color(0.07f, 0.06f, 0.10f);
            _menuCamera.cullingMask = 0;
            _menuListener = camGo.AddComponent<AudioListener>();

            var hudGo = new GameObject("HudController");
            hudGo.transform.SetParent(_uiRoot, false);
            _hud = hudGo.AddComponent<Hud>();
            _hud.Build(_uiRoot);

            var menuGo = new GameObject("MainMenuController");
            menuGo.transform.SetParent(_uiRoot, false);
            _mainMenu = menuGo.AddComponent<MainMenu>();
            _mainMenu.Build(_uiRoot);
            _mainMenu.PlayRequested = () => StartRun(GameSession.NewRandomSeed());
            _mainMenu.QuitRequested = QuitToDesktop;

            var pauseGo = new GameObject("PauseMenuController");
            pauseGo.transform.SetParent(_uiRoot, false);
            _pauseMenu = pauseGo.AddComponent<PauseMenu>();
            _pauseMenu.Build(_uiRoot);
            _pauseMenu.RestartRequested = sameSeed => StartRun(sameSeed ? GameSession.Seed : GameSession.NewRandomSeed());
            _pauseMenu.QuitToMenuRequested = ShowMainMenu;
            _pauseMenu.QuitToDesktopRequested = QuitToDesktop;
        }

        // ------------------------------------------------------------------ state changes

        public void ShowMainMenu()
        {
            DestroyWorld();
            GameSession.SetPaused(false);
            GameSession.SetMode(GameMode.MainMenu);
            GameInput.Gameplay.Disable();

            _menuCamera.gameObject.SetActive(true);
            _menuListener.enabled = true;
            _hud.SetVisible(false);
            _mainMenu.SetVisible(true);
            SetCursorLocked(false);
        }

        /// <summary>Tears down whatever is running and starts a fresh run from a seed.</summary>
        public void StartRun(int seed)
        {
            DestroyWorld();
            GameSession.SetSeed(seed);
            GameSession.SetPaused(false);

            // The menu camera and its listener go off first, so there is only ever one audio listener.
            _menuListener.enabled = false;
            _menuCamera.gameObject.SetActive(false);
            _mainMenu.SetVisible(false);

            _world = new GameObject("World");
            _arena = _world.AddComponent<TestArena>();
            _arena.Build(seed);

            _hud.Gun = _arena.Gun;
            _hud.SetVisible(true);

            GameSession.SetMode(GameMode.Playing);
            GameInput.Gameplay.Enable();
            SetCursorLocked(true);
        }

        void DestroyWorld()
        {
            if (_timeFx != null) _timeFx.Clear();
            if (_world == null) return;
            // Destroy is delayed to the end of the frame, so switch the old world off right now as well.
            _world.SetActive(false);
            Destroy(_world);
            _world = null;
            _arena = null;
            if (_hud != null) _hud.Gun = null;
        }

        void OnPauseChanged(bool paused)
        {
            if (GameSession.Mode != GameMode.Playing) return;
            if (paused) GameInput.Gameplay.Disable();
            else GameInput.Gameplay.Enable();
            SetCursorLocked(!paused);
        }

        void OnApplicationFocus(bool focused)
        {
            // Coming back to the window while playing: take the mouse again.
            if (focused && GameSession.Mode == GameMode.Playing && !GameSession.Paused) SetCursorLocked(true);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void QuitToDesktop()
        {
            GameSession.SetPaused(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
