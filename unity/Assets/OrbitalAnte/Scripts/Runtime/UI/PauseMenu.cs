using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The pause menu: Resume, Controls (rebinding), Audio (volumes), Feel (hitstop, shake and slow-mo toggles), Restart (same seed or new seed) and Quit (menu or desktop).
    /// Esc on keyboard or Start/Menu on a controller opens it. It pauses game time and audio, and every page
    /// works with keyboard, mouse or controller.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        enum Page
        {
            None,
            Main,
            Controls,
            Feel,
            Audio,
            Restart,
            Quit,
            Confirm,
        }

        /// <summary>Restart the run. The argument is true to keep the current seed.</summary>
        public Action<bool> RestartRequested;
        public Action QuitToMenuRequested;
        public Action QuitToDesktopRequested;

        Canvas _canvas;
        readonly Dictionary<Page, GameObject> _pages = new Dictionary<Page, GameObject>();
        readonly Dictionary<Page, GameObject> _defaults = new Dictionary<Page, GameObject>();
        Page _current = Page.None;
        ControlsPage _controls;

        Text _seedLabel;
        Button _sameSeedButton;
        Text _confirmText;
        Action _confirmYes;
        Page _confirmReturn = Page.Main;

        public bool IsOpen => _current != Page.None;

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("PauseMenu", uiRoot, 100);

            var dim = UiKit.NewRect("Dim", _canvas.transform);
            UiKit.Stretch(dim);
            UiKit.AddImage(dim, new Color(0.05f, 0.04f, 0.04f, 0.72f), null, true);

            BuildMain();
            BuildFeel();
            BuildAudio();
            BuildRestart();
            BuildQuit();
            BuildConfirm();

            _controls = new ControlsPage();
            _controls.Build(_canvas.transform, this, () => Show(Page.Main));
            _pages[Page.Controls] = _controls.Root;

            _canvas.gameObject.SetActive(false);
        }

        RectTransform NewPage(Page page, string title, float width)
        {
            var root = UiKit.NewRect(page + "Page", _canvas.transform);
            UiKit.Stretch(root);
            var card = UiKit.Card(root, page + "Card", width);
            var heading = UiKit.LayoutLabel(card, title, 40, UiKit.PanelEdge, 54f);
            // A slight tilt keeps it from feeling like a spreadsheet.
            heading.transform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
            _pages[page] = root.gameObject;
            root.gameObject.SetActive(false);
            return card;
        }

        void BuildMain()
        {
            var card = NewPage(Page.Main, "PAUSED", 520f);
            _defaults[Page.Main] = UiKit.Button(card, "Resume", Resume).gameObject;
            UiKit.Button(card, "Controls", () => Show(Page.Controls));
            UiKit.Button(card, "Audio", () => Show(Page.Audio));
            UiKit.Button(card, "Feel", () => Show(Page.Feel));
            UiKit.Button(card, "Restart", () => Show(Page.Restart));
            UiKit.Button(card, "Quit", () => Show(Page.Quit));
            _seedLabel = UiKit.LayoutLabel(card, string.Empty, 17, UiKit.Muted, 26f, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        /// <summary>Comfort toggles for the punchy stuff. Each button flips one setting and shows its state.</summary>
        void BuildFeel()
        {
            var card = NewPage(Page.Feel, "FEEL", 560f);
            UiKit.LayoutLabel(card, "Turn off anything that bothers you. Saved between sessions.", 18, UiKit.Cream, 30f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _defaults[Page.Feel] = Toggle(card, "Hitstop on limb pops", () => FeelSettings.Hitstop, v => FeelSettings.Hitstop = v).gameObject;
            Toggle(card, "Camera shake", () => FeelSettings.CameraShake, v => FeelSettings.CameraShake = v);
            Toggle(card, "Slow-mo on headshot kills", () => FeelSettings.HeadshotSlowMo, v => FeelSettings.HeadshotSlowMo = v);
            UiKit.Button(card, "Back", () => Show(Page.Main));
        }

        /// <summary>Four volumes. Each button steps its volume up by a tenth and wraps round to silent, so it works the same with mouse, keys or a controller.</summary>
        void BuildAudio()
        {
            var card = NewPage(Page.Audio, "AUDIO", 560f);
            UiKit.LayoutLabel(card, "Press a volume to turn it up a step; after 10 it goes back to 0. Saved between sessions.", 17, UiKit.Cream, 48f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _defaults[Page.Audio] = Volume(card, "Master volume", Audio.AudioVolume.Master).gameObject;
            Volume(card, "Music", Audio.AudioVolume.Music);
            Volume(card, "Effects", Audio.AudioVolume.Effects);
            Volume(card, "Radio and voices", Audio.AudioVolume.Dialogue);
            UiKit.Button(card, "Back", () => Show(Page.Main));
        }

        static Button Volume(Transform parent, string label, Audio.AudioVolume which)
        {
            Button button = null;
            string Describe()
            {
                int step = Mathf.RoundToInt(Audio.AudioPrefs.Get(which) * 10f);
                return label + ":  " + new string('|', step) + new string('.', 10 - step) + "  " + step;
            }
            button = UiKit.Button(parent, string.Empty, () =>
            {
                int step = Mathf.RoundToInt(Audio.AudioPrefs.Get(which) * 10f) + 1;
                if (step > 10) step = 0;
                Audio.AudioPrefs.Set(which, step / 10f);
                UiKit.ButtonText(button).text = Describe();
            });
            UiKit.ButtonText(button).text = Describe();
            return button;
        }

        static Button Toggle(Transform parent, string label, Func<bool> get, Action<bool> set)
        {
            Button button = null;
            button = UiKit.Button(parent, string.Empty, () =>
            {
                set(!get());
                UiKit.ButtonText(button).text = label + ": " + (get() ? "ON" : "OFF");
            });
            UiKit.ButtonText(button).text = label + ": " + (get() ? "ON" : "OFF");
            return button;
        }

        void BuildRestart()
        {
            var card = NewPage(Page.Restart, "RESTART?", 560f);
            UiKit.LayoutLabel(card, "This ends the current run and starts a fresh one.", 20, UiKit.Cream, 34f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _sameSeedButton = UiKit.Button(card, "Restart with the same seed", () => { Close(); RestartRequested?.Invoke(true); });
            UiKit.Button(card, "Restart with a new seed", () => { Close(); RestartRequested?.Invoke(false); });
            _defaults[Page.Restart] = UiKit.Button(card, "Cancel", () => Show(Page.Main)).gameObject;
        }

        void BuildQuit()
        {
            var card = NewPage(Page.Quit, "QUIT?", 560f);
            UiKit.Button(card, "Quit to main menu", () => Confirm(
                "Quit to the main menu?\nThe current run will end.", Page.Quit,
                () => { Close(); QuitToMenuRequested?.Invoke(); }));
            UiKit.Button(card, "Quit to desktop", () => Confirm(
                "Quit to the desktop?\nThe current run will end.", Page.Quit,
                () => { Close(); QuitToDesktopRequested?.Invoke(); }));
            _defaults[Page.Quit] = UiKit.Button(card, "Back", () => Show(Page.Main)).gameObject;
        }

        void BuildConfirm()
        {
            var card = NewPage(Page.Confirm, "ARE YOU SURE?", 560f);
            _confirmText = UiKit.LayoutLabel(card, string.Empty, 22, UiKit.Cream, 80f, TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Button(card, "Yes", () =>
            {
                var yes = _confirmYes;
                _confirmYes = null;
                yes?.Invoke();
            });
            // "No" is the default so a stray button press never quits.
            _defaults[Page.Confirm] = UiKit.Button(card, "No", () => Show(_confirmReturn)).gameObject;
        }

        void Confirm(string message, Page returnTo, Action onYes)
        {
            _confirmText.text = message;
            _confirmReturn = returnTo;
            _confirmYes = onYes;
            Show(Page.Confirm);
        }

        // ------------------------------------------------------------------ open / close

        public void Open()
        {
            if (IsOpen) return;
            GameSession.SetPaused(true);
            _canvas.gameObject.SetActive(true);
            _seedLabel.text = "Seed " + GameSession.Seed + "   |   " + GameSession.BuildLabel;
            UiKit.ButtonText(_sameSeedButton).text = "Restart with the same seed (" + GameSession.Seed + ")";
            Show(Page.Main);
        }

        public void Resume()
        {
            Close();
        }

        /// <summary>Closes the menu and un-pauses.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            if (_current == Page.Controls) _controls.Close();
            foreach (var pair in _pages) pair.Value.SetActive(false);
            _current = Page.None;
            _canvas.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            GameSession.SetPaused(false);
        }

        void Show(Page page)
        {
            if (_current == Page.Controls && page != Page.Controls) _controls.Close();
            foreach (var pair in _pages) pair.Value.SetActive(pair.Key == page);
            _current = page;

            if (page == Page.Controls)
            {
                _controls.Open();
            }
            else if (EventSystem.current != null && _defaults.TryGetValue(page, out GameObject first))
            {
                EventSystem.current.SetSelectedGameObject(first);
            }
        }

        void Back()
        {
            switch (_current)
            {
                case Page.Main:
                    Resume();
                    break;
                case Page.Controls:
                    if (_controls.ConflictOpen) _controls.CloseConflict();
                    else Show(Page.Main);
                    break;
                case Page.Confirm:
                    Show(_confirmReturn);
                    break;
                default:
                    Show(Page.Main);
                    break;
            }
        }

        void Update()
        {
            if (GameSession.Mode != GameMode.Playing || GameInput.Asset == null) return;
            if (_controls.IsRebinding || Time.unscaledTime < _controls.InputBlockedUntil) return;

            bool pausePressed = GameInput.Pause.WasPressedThisFrame();

            if (!IsOpen)
            {
                if (pausePressed) Open();
                return;
            }

            if (pausePressed || GameInput.UiCancel.WasPressedThisFrame())
            {
                Back();
                return;
            }

            if (_current == Page.Controls) _controls.Tick();
            KeepSomethingSelected();
        }

        /// <summary>
        /// Clicking empty space with the mouse clears the selection, which would leave a controller with nothing
        /// to move from. As soon as the player navigates again, put the selection back.
        /// </summary>
        void KeepSomethingSelected()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;
            GameObject selected = es.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) return;

            bool navigating = GameInput.UiNavigate.ReadValue<Vector2>().sqrMagnitude > 0.25f
                              || GameInput.UiSubmit.WasPressedThisFrame();
            if (!navigating) return;

            GameObject fallback = _current == Page.Controls
                ? _controls.DefaultSelection
                : (_defaults.TryGetValue(_current, out GameObject first) ? first : null);
            if (fallback != null) es.SetSelectedGameObject(fallback);
        }
    }
}
