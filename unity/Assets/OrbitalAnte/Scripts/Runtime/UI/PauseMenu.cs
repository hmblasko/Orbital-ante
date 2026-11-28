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
            Died,
            /// <summary>Between antes: the ante just cleared, and the way on (ante tasks, 20 Nov 2026).</summary>
            Intermission,
            /// <summary>A page of choices the caller fills (the ante break's perk, shop and stakes pages, 20 Nov 2026).</summary>
            Choices,
        }

        /// <summary>One button on a choices page.</summary>
        public struct MenuChoice
        {
            public string Label;
            public Action OnPick;
            public bool Enabled;

            public MenuChoice(string label, Action onPick, bool enabled = true)
            {
                Label = label;
                OnPick = onPick;
                Enabled = enabled;
            }
        }

        /// <summary>Restart the run. The argument is true to keep the current seed.</summary>
        public Action<bool> RestartRequested;
        public Action QuitToMenuRequested;
        public Action QuitToDesktopRequested;
        /// <summary>True when the current ante's target is met and it may be ended early (Hybrid). Shows the button.</summary>
        public Func<bool> CanEndAnte;
        /// <summary>Ends the current ante now. Only ever reached through its confirm page.</summary>
        public Action EndAnte;
        /// <summary>Leaves the between-antes page and starts the next ante.</summary>
        public Action StartNextAnte;

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
        /// <summary>The game's pause menu, once built (the star chart asks whether it is open before un-pausing).</summary>
        public static PauseMenu Instance { get; private set; }

        public void Build(Transform uiRoot)
        {
            Instance = this;
            _canvas = UiKit.CreateCanvas("PauseMenu", uiRoot, 100);

            var dim = UiKit.NewRect("Dim", _canvas.transform);
            UiKit.Stretch(dim);
            UiKit.AddImage(dim, new Color(0.05f, 0.04f, 0.04f, 0.72f), null, true);

            BuildMain();
            BuildFeel();
            BuildAudio();
            BuildRestart();
            BuildQuit();
            BuildDied();
            BuildIntermission();
            BuildChoices();
            BuildConfirm();

            _controls = new ControlsPage();
            _controls.Build(_canvas.transform, this, () => Show(Page.Main));
            _pages[Page.Controls] = _controls.Root;

            _canvas.gameObject.SetActive(false);
        }

        Text _lastHeading;

        RectTransform NewPage(Page page, string title, float width)
        {
            var root = UiKit.NewRect(page + "Page", _canvas.transform);
            UiKit.Stretch(root);
            var card = UiKit.Card(root, page + "Card", width);
            var heading = UiKit.Heading(card, title, 40, 54f);
            _lastHeading = heading;
            _pages[page] = root.gameObject;
            root.gameObject.SetActive(false);
            return card;
        }

        void BuildMain()
        {
            var card = NewPage(Page.Main, "PAUSED", 520f);
            _defaults[Page.Main] = UiKit.Button(card, "Resume", Resume).gameObject;
            // Hybrid antes: once the target is met you may move on early. A confirmed action, never Select.
            _endAnteButton = UiKit.Button(card, "Bank this ante now", () => Confirm(
                "Target met. End this ante now and bank it?\nAnything you would still score this ante is given up.", Page.Main,
                () => { Close(); EndAnte?.Invoke(); }));
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
            Toggle(card, "Aim assist (controller)", () => FeelSettings.AimAssist, v => FeelSettings.AimAssist = v);
            Toggle(card, "Tips for new players", () => FeelSettings.GuideTips, v => FeelSettings.GuideTips = v);
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
            _restartNote = UiKit.LayoutLabel(card, RestartWords, 20, UiKit.Cream, 34f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _sameSeedButton = UiKit.Button(card, "Restart with the same seed", () => { Close(); RestartRequested?.Invoke(true); });
            UiKit.Button(card, "Restart with a new seed", () => { Close(); RestartRequested?.Invoke(false); });
            _defaults[Page.Restart] = UiKit.Button(card, "Cancel", () => Show(Page.Main)).gameObject;
        }

        Text _restartNote;
        const string RestartWords = "This ends the current run and starts a fresh one.";

        /// <summary>
        /// The restart page says when a starting kit comes along again (goal unlocks, 27 Nov 2026): a restart starts
        /// with the kit chosen at New run, which is changed only there.
        /// </summary>
        void SetRestartWords()
        {
            if (_restartNote == null) return;
            Ante.StartingKit kit = GameSession.Scenario == Scenario.SolarSystem ? Ante.UnlockBook.ChosenKit(Save.SaveSystem.Data) : null;
            bool withKit = kit != null && kit.perkIds.Length > 0;
            _restartNote.text = withKit ? RestartWords + " It starts with the " + kit.displayName + " again (change it at New run)." : RestartWords;
            UiKit.SetLayout(_restartNote.gameObject, withKit ? 58f : 34f);
        }

        Text _diedCause;
        Text _diedTitle;
        Text _diedRecap;
        Button _diedSameSeed;
        Button _endAnteButton;
        Text _interTitle, _interBody;
        Button _interGo;

        void BuildIntermission()
        {
            var card = NewPage(Page.Intermission, "ANTE CLEARED", 640f);
            _interTitle = _lastHeading;
            _interBody = UiKit.LayoutLabel(card, string.Empty, 19, UiKit.Cream, 210f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _interGo = UiKit.Button(card, "Start the next ante", () => { Close(); StartNextAnte?.Invoke(); });
            _defaults[Page.Intermission] = _interGo.gameObject;
            UiKit.Button(card, "Restart", () => Show(Page.Restart));
            UiKit.Button(card, "Quit", () => Show(Page.Quit));
        }

        /// <summary>Opens the between-antes page: what was cleared and what comes next. The game stays paused on it.</summary>
        public void ShowIntermission(string title, string body, string goLabel)
        {
            if (!IsOpen)
            {
                GameSession.SetPaused(true);
                _canvas.gameObject.SetActive(true);
            }
            if (_interTitle != null) _interTitle.text = title;
            _interBody.text = body ?? string.Empty;
            UiKit.ButtonText(_interGo).text = goLabel;
            Show(Page.Intermission);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _diedAt = Time.unscaledTime;
            _selectHold.Start(DiedHold);
        }

        // ------------------------------------------------------------------ choices page (ante break)

        const int ChoiceSlots = 7;
        /// <summary>A choice button's height. A full page of seven uses less, so the card still fits 1280 x 800.</summary>
        public const float ChoiceButtonHeight = 70f;
        Text _choiceTitle, _choiceBody;
        readonly Button[] _choiceButtons = new Button[ChoiceSlots];
        readonly Action[] _choiceActions = new Action[ChoiceSlots];
        Action _choiceBack;

        void BuildChoices()
        {
            var card = NewPage(Page.Choices, "CHOOSE", 760f);
            _choiceTitle = _lastHeading;
            _choiceBody = UiKit.LayoutLabel(card, string.Empty, 18, UiKit.Cream, 120f, TextAnchor.MiddleCenter, FontStyle.Normal);
            // A body cut to the room left on a small screen (MenuFit.Fit) shrinks its words rather than spilling.
            ShrinkToFit(_choiceBody, 14, 18);
            for (int i = 0; i < ChoiceSlots; i++)
            {
                int slot = i;
                Button b = UiKit.Button(card, string.Empty, () => _choiceActions[slot]?.Invoke(), ChoiceButtonHeight, 19);
                Text t = UiKit.ButtonText(b);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                // Two-line rows (the scratch ticket and its odds) need 54 units at 19 px; shorter buttons shrink the words.
                ShrinkToFit(t, 13, 19);
                ((RectTransform)t.transform).offsetMin = new Vector2(16f, 4f);
                ((RectTransform)t.transform).offsetMax = new Vector2(-16f, -4f);
                _choiceButtons[i] = b;
            }
        }

        /// <summary>
        /// Shows a page of up to seven choices, the first enabled one selected for the controller. The game stays
        /// paused. <paramref name="onBack"/> is what B / Esc does there (nothing if null). Each pick runs its action;
        /// the action decides which page comes next.
        /// </summary>
        /// <param name="keepSlot">After a pick that stays on the same page (buying in the shop), the slot to keep the
        /// cursor on, or the nearest enabled one; -1 selects the first enabled.</param>
        /// <param name="holdSelection">Select nothing for a moment, so a button still held or mashed from the fight
        /// cannot pick something unread (the first page of a break, the perk cards).</param>
        public void ShowChoices(string title, string body, IList<MenuChoice> choices, Action onBack = null, float bodyHeight = 120f,
            int keepSlot = -1, bool holdSelection = false, float buttonHeight = ChoiceButtonHeight)
        {
            if (!IsOpen) holdSelection = true;
            if (!IsOpen)
            {
                GameSession.SetPaused(true);
                _canvas.gameObject.SetActive(true);
            }
            if (_choiceTitle != null) _choiceTitle.text = title;
            _choiceBody.text = body ?? string.Empty;
            // The body grows to its words, measured by the label's own text generator; the buttons give up height
            // before the card runs off the screen, then the body is cut and its words shrink (UI/MenuFit).
            float wanted = string.IsNullOrEmpty(body) ? 0f : Mathf.Max(bodyHeight, WordsHeight(_choiceBody, body, ChoiceBodyWidth, MenuFit.CharsPerLine));
            int rows = choices == null ? 0 : Math.Min(choices.Count, ChoiceSlots);
            MenuFit.Fit(rows, wanted, buttonHeight, CanvasHeight, out float shownBody, out buttonHeight);
            UiKit.SetLayout(_choiceBody.gameObject, shownBody);
            _choiceBack = onBack;
            GameObject first = null;
            for (int i = 0; i < ChoiceSlots; i++)
            {
                bool used = choices != null && i < choices.Count;
                Button b = _choiceButtons[i];
                b.gameObject.SetActive(used);
                _choiceActions[i] = used ? choices[i].OnPick : null;
                if (!used) continue;
                UiKit.ButtonText(b).text = choices[i].Label;
                UiKit.SetLayout(b.gameObject, buttonHeight);
                b.interactable = choices[i].Enabled;
                if (first == null && choices[i].Enabled) first = b.gameObject;
            }
            if (keepSlot >= 0 && choices != null)
            {
                for (int d = 0; d < ChoiceSlots; d++)
                {
                    int up = keepSlot - d, down = keepSlot + d;
                    if (down < choices.Count && down < ChoiceSlots && choices[down].Enabled) { first = _choiceButtons[down].gameObject; break; }
                    if (up >= 0 && up < choices.Count && choices[up].Enabled) { first = _choiceButtons[up].gameObject; break; }
                }
            }
            _defaults[Page.Choices] = first;
            Show(Page.Choices);
            if (holdSelection)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                _diedAt = Time.unscaledTime;
                _selectHold.Start(DiedHold);
            }
        }

        /// <summary>
        /// The menu canvas's height in layout units (800 on the Deck; about 759 on a 16:9 screen), from the screen and
        /// the scaler's settings (UiKit.CreateCanvas), since the canvas's rect may still be unscaled the frame it is
        /// first switched on.
        /// </summary>
        static float CanvasHeight => MenuFit.CanvasHeight(Screen.width, Screen.height, UiKit.ReferenceResolution.x, UiKit.ReferenceResolution.y, 0.5f);

        /// <summary>The choices page's body width (its 760 card less the padding) and the death page's recap width (620 less it).</summary>
        const float ChoiceBodyWidth = 704f, RecapWidth = 564f;

        /// <summary>
        /// How tall a label's words come out at this width, from the label's own text generator at its full size
        /// (Unity review, 27 Nov 2026: counting characters over-counted lines and shrank the break's buttons). The
        /// character-count estimate when the generator gives nothing usable.
        /// </summary>
        static float WordsHeight(Text label, string text, float width, int charsPerLine)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float measured = 0f;
            if (label != null && label.font != null)
            {
                TextGenerationSettings settings = label.GetGenerationSettings(new Vector2(width, 0f));
                // Measured at the label's own size: best fit would shrink it to nothing in a box of no height.
                settings.resizeTextForBestFit = false;
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                float perUnit = label.pixelsPerUnit;
                measured = label.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / (perUnit > 0f ? perUnit : 1f);
            }
            return measured > 1f && measured < 4000f ? Mathf.Ceil(measured) + 4f : MenuFit.BodyHeight(text, 0f, 22f, charsPerLine);
        }

        /// <summary>Best fit between two sizes: the words keep their size unless the box is too small for them.</summary>
        static void ShrinkToFit(Text label, int least, int most)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = least;
            label.resizeTextMaxSize = most;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>Restarts the run (true keeps the seed), from a page outside the menu's own (the ante break).</summary>
        public void RequestRestart(bool sameSeed) { Close(); RestartRequested?.Invoke(sameSeed); }

        public void RequestQuitToMenu() { Close(); QuitToMenuRequested?.Invoke(); }

        /// <summary>The run has ended without a death (the ante timer ran out short of the target).</summary>
        public void ShowRunOver(string title, string cause) => ShowRunOver(title, cause, null);

        public void ShowRunOver(string title, string cause, string recap)
        {
            Playtest.PlaytestHooks.RunOver(title, cause);
            ShowDeath(cause, recap);
            if (_diedTitle != null) _diedTitle.text = title;
        }

        /// <summary>
        /// Shown when the pilot dies. Says what killed them in plain words, so the lesson is never hidden, and
        /// offers the same choices as Restart. There is no way back into a run that has ended.
        /// </summary>
        void BuildDied()
        {
            var card = NewPage(Page.Died, "THAT WAS FATAL", 620f);
            _diedTitle = _lastHeading;
            _diedCause = UiKit.LayoutLabel(card, string.Empty, 21, UiKit.Cream, 84f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _diedRecap = UiKit.LayoutLabel(card, string.Empty, 18, UiKit.Muted, 0f, TextAnchor.MiddleCenter, FontStyle.Normal);
            ShrinkToFit(_diedRecap, 14, 18);
            _diedRecap.gameObject.SetActive(false);
            _diedSameSeed = UiKit.Button(card, "Try again with the same seed", () => { Close(); RestartRequested?.Invoke(true); });
            _defaults[Page.Died] = _diedSameSeed.gameObject;
            UiKit.Button(card, "Try again with a new seed", () => { Close(); RestartRequested?.Invoke(false); });
            UiKit.Button(card, "Quit to main menu", () => { Close(); QuitToMenuRequested?.Invoke(); });
        }

        /// <summary>Opens the menu on the page for a run that has ended in the pilot's death.</summary>
        public void ShowDeath(string cause) => ShowDeath(cause, null);

        /// <summary>
        /// The death page with a run recap under the cause (20 Nov 2026): how far the run got, how close it came,
        /// the build and any new record. No recap (the arena) leaves the page as it was.
        /// </summary>
        public void ShowDeath(string cause, string recap)
        {
            bool hasRecap = !string.IsNullOrEmpty(recap);
            _diedRecap.gameObject.SetActive(hasRecap);
            _diedRecap.text = hasRecap ? recap : string.Empty;
            // The recap grows with its lines (unlocks add some), up to what the page has room for: the heading, the
            // cause, three 56-unit buttons and the gaps take 422 of the screen; past that its words shrink.
            float recapRoom = Mathf.Min(300f, CanvasHeight - 2f * MenuFit.Margin - 422f);
            UiKit.SetLayout(_diedRecap.gameObject, hasRecap
                ? Mathf.Min(recapRoom, Mathf.Max(150f, WordsHeight(_diedRecap, recap, RecapWidth, MenuFit.RecapCharsPerLine)))
                : 0f);
            if (!IsOpen)
            {
                GameSession.SetPaused(true);
                _canvas.gameObject.SetActive(true);
            }
            _diedCause.text = cause ?? string.Empty;
            if (_diedTitle != null) _diedTitle.text = "THAT WAS FATAL";
            UiKit.ButtonText(_diedSameSeed).text = "Try again with the same seed (" + GameSession.Seed + ")";
            Show(Page.Died);
            // Nothing is chosen for a moment, so a button still held from the crash cannot skip the page.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _diedAt = Time.unscaledTime;
            _selectHold.Start(DiedHold);
        }

        float _diedAt = -10f;
        const float DiedHold = 0.8f;
        /// <summary>Selects the page's default by itself once the hold is over and the confirm button is at rest.</summary>
        readonly SelectionHold _selectHold = new SelectionHold();

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
            SetRestartWords();
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
            _selectHold.Cancel();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            GameSession.SetPaused(false);
        }

        void Show(Page page)
        {
            // The plain pause pages draw the frozen world at half rate (PerformanceBudget.halfRateWhenPaused); the
            // pages meant to look good (the ante break, its choices, the death recap) keep full rate.
            bool plain = page == Page.Main || page == Page.Controls || page == Page.Feel || page == Page.Audio
                         || page == Page.Restart || page == Page.Quit || page == Page.Confirm;
            Perf.PerformanceBudget.Load().ApplyPaused(plain && GameSession.Mode == GameMode.Playing);
            if (_current == Page.Controls && page != Page.Controls) _controls.Close();
            foreach (var pair in _pages) pair.Value.SetActive(pair.Key == page);
            _current = page;
            // Whether the ante can be banked is asked every time the main page shows, not only when the menu opens.
            if (page == Page.Main && _endAnteButton != null) _endAnteButton.gameObject.SetActive(CanEndAnte != null && CanEndAnte());

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
                case Page.Choices:
                    _choiceBack?.Invoke();
                    break;
                case Page.Died:
                case Page.Intermission:
                    // The run is over, or waits for the next ante: there is nothing to go back to.
                    break;
                default:
                    Show(Page.Main);
                    break;
            }
        }

        /// <summary>
        /// Leaving the game (alt-tab, the Steam button's overlay, the Deck put to sleep) opens the pause menu, so
        /// nobody comes back to a lost fight (24 Nov 2026 refinement). Not in the editor, where clicking the
        /// Inspector would pause every time. Nothing opens over a page that already pauses the game.
        /// </summary>
        void OnApplicationFocus(bool focused)
        {
            if (!focused) PauseForAway();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) PauseForAway();
        }

        void PauseForAway()
        {
            if (Application.isEditor || _canvas == null) return;
            if (GameSession.Mode != GameMode.Playing || GameSession.Paused || IsOpen || StarChartView.Showing) return;
            Open();
        }

        void Update()
        {
            if (GameSession.Mode != GameMode.Playing || GameInput.Asset == null) return;
            if (_controls.IsRebinding || Time.unscaledTime < _controls.InputBlockedUntil) return;
            // The upgrade screen handles its own Back; the press that closed it must not open this menu too.
            if (StarChartView.Showing || StarChartView.ClosedFrame == Time.frameCount) return;

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
            if (selected != null && selected.activeInHierarchy) { _selectHold.Cancel(); return; }
            bool heldPage = _current == Page.Died || _current == Page.Intermission || _current == Page.Choices;
            if (!heldPage) _selectHold.Cancel();
            // One press from failing to trying again: the default selects itself once the hands are still.
            if (_selectHold.Step(Time.unscaledDeltaTime, GameInput.UiSubmit.IsPressed())
                && _defaults.TryGetValue(_current, out GameObject ready) && ready != null && ready.activeInHierarchy)
            {
                es.SetSelectedGameObject(ready);
                return;
            }
            if (heldPage && Time.unscaledTime - _diedAt < DiedHold) return;

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
