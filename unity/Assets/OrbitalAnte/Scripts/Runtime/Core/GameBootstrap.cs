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
        Space.SolarSystemWorld _space;
        TimeFx _timeFx;
        Perf.PerfMonitor _perf;
        double _unsavedSeconds;

        public TestArena Arena => _arena;
        public Space.SolarSystemWorld Space => _space;

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
            Weapons.Weapon.ResetStatics();
            Weapons.WeaponFx.ResetStatics();
            AI.CombatWorld.Reset();
            Content.ContentDatabase.ResetStatics();
            Save.SaveSystem.ResetStatics();
            Perf.PerfMonitor.ResetStatics();
            OrbitalAnte.Space.SolarSystem.ResetStatics();
            OrbitalAnte.Space.CelestialBody.ResetStatics();
            Planets.PlanetWorld.ResetStatics();
            Planets.PlanetJobs.ResetStatics();
            Planets.PlanetMaterials.ResetStatics();
            Planets.PlanetSurface.ResetStatics();
            Planets.FeatureMeshes.ResetStatics();
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

            Perf.PerformanceBudget.Load().Apply();
            Layers.Configure();
            GameInput.Initialize();
            Save.SaveSystem.Load();
            Content.ContentDatabase.Load();
            _perf = gameObject.AddComponent<Perf.PerfMonitor>();
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
            _mainMenu.PlayRequested = scenario => StartRun(GameSession.NewRandomSeed(), scenario);
            _mainMenu.ReplayRequested = StartRun;
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
            bool wasPlaying = _world != null;
            DestroyWorld();
            if (wasPlaying)
            {
                BankPlayTime();
                Save.SaveSystem.Save();
            }
            GameSession.SetPaused(false);
            GameSession.SetMode(GameMode.MainMenu);
            GameInput.Play.Disable();

            _menuCamera.gameObject.SetActive(true);
            _menuListener.enabled = true;
            _hud.SetVisible(false);
            _mainMenu.SetVisible(true);
            SetCursorLocked(false);
        }

        /// <summary>Restarts in the same test world.</summary>
        public void StartRun(int seed) => StartRun(seed, GameSession.Scenario);

        /// <summary>Tears down whatever is running and starts a fresh run from a seed.</summary>
        public void StartRun(int seed, Scenario scenario)
        {
            DestroyWorld();
            GameInput.Play.Disable();
            GameSession.SetScenario(scenario);
            GameInput.SetFlying(scenario == Scenario.SolarSystem);
            GameSession.SetSeed(seed);
            GameSession.SetPaused(false);

            Save.SaveData save = Save.SaveSystem.Data;
            save.hasLastSeed = true;
            save.lastSeed = seed;
            save.lastScenario = (int)scenario;
            save.runsStarted++;
            BankPlayTime();
            Save.SaveSystem.Save();

            // The menu camera and its listener go off first, so there is only ever one audio listener.
            _menuListener.enabled = false;
            _menuCamera.gameObject.SetActive(false);
            _mainMenu.SetVisible(false);

            _world = new GameObject("World");
            if (scenario == Scenario.SolarSystem)
            {
                _space = _world.AddComponent<Space.SolarSystemWorld>();
                _space.Build(seed);
                _hud.SetArenaMode(false);
                _hud.ExtraDebug = _space.DescribeForDebug;
            }
            else
            {
                _arena = _world.AddComponent<TestArena>();
                _arena.Build(seed);
                _hud.SetArenaMode(true);
                _hud.Weapons = _arena.Weapons;
                _hud.Health = _arena.Health;
            }
            _hud.SetVisible(true);

            GameSession.SetMode(GameMode.Playing);
            GameInput.Play.Enable();
            SetCursorLocked(true);
        }

        void DestroyWorld()
        {
            if (_timeFx != null) _timeFx.Clear();
            if (_world == null) return;
            if (_perf != null) _perf.LogAndReset("seed " + GameSession.Seed);
            // Destroy is delayed to the end of the frame, so switch the old world off right now as well.
            _world.SetActive(false);
            Destroy(_world);
            _world = null;
            _arena = null;
            _space = null;
            if (_hud != null)
            {
                _hud.ExtraDebug = null;
                _hud.Weapons = null;
                _hud.Health = null;
            }
        }

        void Update()
        {
            if (GameSession.Mode == GameMode.Playing && !GameSession.Paused) _unsavedSeconds += Time.unscaledDeltaTime;
        }

        /// <summary>Moves time played since the last save into the save data.</summary>
        void BankPlayTime()
        {
            Save.SaveSystem.Data.secondsPlayed += _unsavedSeconds;
            _unsavedSeconds = 0;
        }

        void OnApplicationQuit()
        {
            if (_perf != null && _world != null) _perf.LogAndReset("seed " + GameSession.Seed);
            BankPlayTime();
            Save.SaveSystem.Save();
        }

        void OnPauseChanged(bool paused)
        {
            if (GameSession.Mode != GameMode.Playing) return;
            if (paused) GameInput.Play.Disable();
            else GameInput.Play.Enable();
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
