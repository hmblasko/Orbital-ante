using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Input;
using OrbitalAnte.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Pause menu: Resume, Controls (rebinding per device), Restart, Quit. Pauses time and audio
    /// and works with mouse, keyboard or controller. Pause button in a sub-page goes back a page.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        Canvas canvas;
        GameObject backdrop;
        readonly Stack<GameObject> pages = new Stack<GameObject>();
        float savedTimeScale = 1f;
        bool rebinding;

        // Controls page
        GameObject controlsPage;
        RectTransform bindingList;
        Text deviceNote;
        Button kbTab, padTab, glyphButton;
        string currentGroup = GameInput.KeyboardMouse;
        Text seedLabel;

        public static PauseMenu Create()
        {
            var c = UIFactory.CreateCanvas("PauseMenu", 100);
            var menu = c.gameObject.AddComponent<PauseMenu>();
            menu.canvas = c;
            menu.Build();
            return menu;
        }

        void OnDestroy()
        {
            if (IsPaused)
            {
                IsPaused = false;
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }

        void Build()
        {
            UIFactory.EnsureEventSystem();
            backdrop = UIFactory.Image("Backdrop", canvas.transform, UITheme.Backdrop).gameObject;
            UIFactory.Stretch((RectTransform)backdrop.transform);
            backdrop.SetActive(false);
        }

        void Update()
        {
            if (rebinding) return;
            if (GameInput.Pause.WasPressedThisFrame())
            {
                if (!IsPaused) Open();
                else GoBack();
            }
            else if (IsPaused && GameInput.Back.WasPressedThisFrame())
            {
                GoBack();
            }
            else if (!IsPaused && GameInput.UpgradeScreen.WasPressedThisFrame())
            {
                Hud.Instance?.Notify("Upgrade screen arrives with the ante loop (Phase 2)", NoticeKind.System);
            }
        }

        // ---- open / close / pages ------------------------------------------------------------

        public void Open()
        {
            if (IsPaused) return;
            IsPaused = true;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            FirstPersonController.SetCursorLocked(false);
            backdrop.SetActive(true);
            Push(BuildMainPage());
        }

        public void Resume()
        {
            while (pages.Count > 0) Destroy(pages.Pop());
            backdrop.SetActive(false);
            IsPaused = false;
            Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
            AudioListener.pause = false;
            FirstPersonController.SetCursorLocked(true);
            UIFactory.Select(null);
        }

        void GoBack()
        {
            if (pages.Count <= 1)
            {
                Resume();
                return;
            }
            Destroy(pages.Pop());
            var top = pages.Peek();
            top.SetActive(true);
            SelectFirstIn(top);
        }

        void Push(GameObject page)
        {
            if (pages.Count > 0) pages.Peek().SetActive(false);
            pages.Push(page);
            page.SetActive(true);
            SelectFirstIn(page);
        }

        static void SelectFirstIn(GameObject page)
        {
            var first = page.GetComponentInChildren<Selectable>();
            UIFactory.Select(first != null ? first.gameObject : null);
        }

        RectTransform NewPage(string name, Vector2 size)
        {
            var card = UIFactory.Card(name, canvas.transform, size);
            return card;
        }

        // ---- main page -------------------------------------------------------------------

        GameObject BuildMainPage()
        {
            var card = NewPage("MainPage", new Vector2(460, 470));
            UIFactory.Label(card, "PAUSED", UITheme.TitleSize, UITheme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold)
                .rectTransform.localRotation = Quaternion.Euler(0, 0, -3f);
            UIFactory.Layout(card.GetChild(card.childCount - 1).gameObject, 64);
            seedLabel = UIFactory.Label(card, $"seed #{RunSeed.Current}", UITheme.SmallSize, UITheme.TextDim, TextAnchor.MiddleCenter);
            UIFactory.Layout(seedLabel.gameObject, 22);
            UIFactory.Button(card, "Resume", Resume);
            UIFactory.Button(card, "Controls", () => Push(BuildControlsPage()));
            UIFactory.Button(card, "Restart", () => Push(BuildRestartPage()));
            UIFactory.Button(card, "Quit", () => Push(BuildQuitPage()));
            return card.parent.gameObject;
        }

        GameObject BuildRestartPage()
        {
            var card = NewPage("RestartPage", new Vector2(520, 380));
            UIFactory.Header(card, "RESTART THE RUN?");
            var note = UIFactory.Label(card, "Your progress in this run will be lost.", UITheme.BodySize, UITheme.Text, TextAnchor.MiddleCenter);
            UIFactory.Layout(note.gameObject, 34);
            UIFactory.Button(card, $"Same seed (#{RunSeed.Current})", () => SceneFlow.StartRun(newSeed: false));
            UIFactory.Button(card, "New seed", () => SceneFlow.StartRun(newSeed: true));
            UIFactory.Button(card, "Cancel", GoBack);
            return card.parent.gameObject;
        }

        GameObject BuildQuitPage()
        {
            var card = NewPage("QuitPage", new Vector2(480, 330));
            UIFactory.Header(card, "QUIT");
            UIFactory.Button(card, "Quit to main menu", () => Push(BuildConfirm("Quit to the main menu?",
                "This run will end.", "Yes, main menu", SceneFlow.ToMainMenu)));
            UIFactory.Button(card, "Quit to desktop", () => Push(BuildConfirm("Quit to desktop?",
                "This run will end and the game will close.", "Yes, quit game", SceneFlow.QuitToDesktop)));
            UIFactory.Button(card, "Back", GoBack);
            return card.parent.gameObject;
        }

        GameObject BuildConfirm(string title, string message, string yesLabel, Action yes, string noLabel = "No, go back", Action no = null)
        {
            var card = NewPage("Confirm", new Vector2(540, 300));
            UIFactory.Header(card, title);
            var note = UIFactory.Label(card, message, UITheme.BodySize, UITheme.Text, TextAnchor.MiddleCenter);
            UIFactory.Layout(note.gameObject, 56);
            // "No" first so a stray press of the confirm button never quits by accident.
            UIFactory.Button(card, noLabel, () => { GoBack(); no?.Invoke(); });
            UIFactory.Button(card, yesLabel, () => yes());
            return card.parent.gameObject;
        }

        // ---- controls page ---------------------------------------------------------------

        GameObject BuildControlsPage()
        {
            var card = NewPage("ControlsPage", new Vector2(760, 720));
            UIFactory.Header(card, "CONTROLS");

            var tabs = UIFactory.Row(card, 46);
            kbTab = UIFactory.Button(tabs.transform, "Keyboard & mouse", () => ShowGroup(GameInput.KeyboardMouse));
            padTab = UIFactory.Button(tabs.transform, "Controller", () => ShowGroup(GameInput.Gamepad));

            var noteRow = UIFactory.Row(card, 34);
            deviceNote = UIFactory.Label(noteRow.transform, "", UITheme.SmallSize, UITheme.TextDim, TextAnchor.MiddleLeft);
            glyphButton = UIFactory.Button(noteRow.transform, "Glyphs: Xbox", () =>
            {
                InputGlyphs.CycleManualOverride();
                RefreshRows();
            }, 34, UITheme.SmallSize);
            UIFactory.Layout(glyphButton.gameObject, 34, 220, 0);

            bindingList = UIFactory.ScrollList(card, 430, out _);

            var bottom = UIFactory.Row(card, 50);
            UIFactory.Button(bottom.transform, "Reset to defaults", () =>
            {
                string g = currentGroup;
                Push(BuildConfirm("Reset these controls?",
                    g == GameInput.Gamepad ? "Controller bindings go back to default." : "Keyboard & mouse bindings go back to default.",
                    "Yes, reset", () => { GameInput.ResetGroup(g); GoBack(); RefreshRows(); }));
            });
            UIFactory.Button(bottom.transform, "Back", GoBack);

            controlsPage = card.parent.gameObject;
            ShowGroup(InputGlyphs.UsingGamepad() ? GameInput.Gamepad : GameInput.KeyboardMouse, select: false);
            return controlsPage;
        }

        void ShowGroup(string group, bool select = true)
        {
            currentGroup = group;
            RefreshRows();
            SetTabLook(kbTab, group == GameInput.KeyboardMouse);
            SetTabLook(padTab, group == GameInput.Gamepad);
            if (select)
            {
                var first = bindingList.GetComponentInChildren<Button>();
                if (first != null) UIFactory.Select(first.gameObject);
            }
        }

        static void SetTabLook(Button b, bool active)
        {
            var t = UIFactory.ButtonText(b);
            t.color = active ? UITheme.Accent : UITheme.TextDim;
            t.fontSize = active ? UITheme.BodySize + 2 : UITheme.BodySize;
        }

        struct Row
        {
            public InputAction Action;
            public int BindingIndex;
            public string Label;
        }

        List<Row> CollectRows(string group)
        {
            var rows = new List<Row>();
            foreach (var map in GameInput.Asset.actionMaps)
            foreach (var action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite || !GameInput.InGroup(b, group)) continue;
                    string label = GameInput.DisplayName(action);
                    if (b.isPartOfComposite) label += " " + b.name.ToLowerInvariant();
                    rows.Add(new Row { Action = action, BindingIndex = i, Label = label });
                }
            }
            return rows;
        }

        void RefreshRows()
        {
            if (bindingList == null) return;
            for (int i = bindingList.childCount - 1; i >= 0; i--) Destroy(bindingList.GetChild(i).gameObject);

            bool pad = currentGroup == GameInput.Gamepad;
            deviceNote.text = pad
                ? $"Detected: {InputGlyphs.FamilyName(InputGlyphs.Detect())}. Select a row, then press the new button (Start cancels)."
                : "Select a row, then press the new key or mouse button (Esc cancels).";
            glyphButton.gameObject.SetActive(pad);
            UIFactory.ButtonText(glyphButton).text = "Glyphs: " + InputGlyphs.FamilyName(InputGlyphs.Family);

            foreach (var row in CollectRows(currentGroup))
            {
                var h = UIFactory.Row(bindingList, 40);
                var label = UIFactory.Label(h.transform, row.Label, UITheme.BodySize, UITheme.Text);
                UIFactory.Layout(label.gameObject, 40, 330, 1);
                string value = InputGlyphs.BindingLabel(row.Action, row.BindingIndex, currentGroup);
                bool locked = GameInput.Locked.Contains(row.Action);
                Button btn = null;
                var r = row;
                btn = UIFactory.Button(h.transform, locked ? value + "  (fixed)" : value,
                    locked ? (Action)null : () => StartRebind(r, btn), 40);
                UIFactory.Layout(btn.gameObject, 40, 300, 0);
                if (locked) btn.interactable = false;
            }
        }

        // ---- rebinding -------------------------------------------------------------------

        void StartRebind(Row row, Button button)
        {
            if (rebinding) return;
            rebinding = true;
            UIFactory.ButtonText(button).text = currentGroup == GameInput.Gamepad ? "Press a button..." : "Press a key...";
            StartCoroutine(RebindWhenReleased(row, button));
        }

        /// <summary>
        /// Waits until the button that opened the rebind is let go, otherwise the rebind would
        /// grab that same button straight away.
        /// </summary>
        System.Collections.IEnumerator RebindWhenReleased(Row row, Button button)
        {
            float giveUp = Time.unscaledTime + 1.5f;
            while (AnyMenuButtonHeld() && Time.unscaledTime < giveUp) yield return null;
            yield return null;
            BeginRebind(row, button);
        }

        static bool AnyMenuButtonHeld()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            return (kb != null && kb.anyKey.isPressed) ||
                   (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed)) ||
                   (pad != null && (pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.startButton.isPressed));
        }

        void BeginRebind(Row row, Button button)
        {
            var action = row.Action;
            int index = row.BindingIndex;
            string group = currentGroup;
            string previousOverride = action.bindings[index].overridePath;
            string previousEffective = action.bindings[index].effectivePath;

            GameInput.Asset.Disable();

            var op = action.PerformInteractiveRebinding(index)
                .WithMagnitudeHavingToBeGreaterThan(0.5f)
                .OnMatchWaitForAnother(0.1f);
            if (group == GameInput.Gamepad)
            {
                op.WithControlsHavingToMatchPath("<Gamepad>")
                    .WithCancelingThrough("<Gamepad>/start");
            }
            else
            {
                op.WithControlsHavingToMatchPath("<Keyboard>")
                    .WithControlsHavingToMatchPath("<Mouse>")
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/delta")
                    .WithControlsExcluding("<Mouse>/scroll")
                    .WithControlsExcluding("<Keyboard>/anyKey")
                    .WithCancelingThrough("<Keyboard>/escape");
            }

            op.OnCancel(o =>
            {
                o.Dispose();
                EndRebind(button);
            });
            op.OnComplete(o =>
            {
                o.Dispose();
                string newPath = action.bindings[index].effectivePath;
                if (FindConflict(action, index, group, newPath, out var other, out int otherIndex))
                {
                    string keyName = InputGlyphs.BindingLabel(action, index, group);
                    string otherName = GameInput.DisplayName(other) + (other.bindings[otherIndex].isPartOfComposite
                        ? " " + other.bindings[otherIndex].name.ToLowerInvariant() : "");
                    EndRebind(button, refresh: false);
                    Push(BuildConfirm("Already in use",
                        $"{keyName} is already used by {otherName}. Swap the two?",
                        "Swap them",
                        () =>
                        {
                            other.ApplyBindingOverride(otherIndex, previousEffective);
                            GameInput.SaveOverrides();
                            GoBack();
                            RefreshRows();
                        },
                        "Cancel",
                        () =>
                        {
                            if (string.IsNullOrEmpty(previousOverride)) action.RemoveBindingOverride(index);
                            else action.ApplyBindingOverride(index, previousOverride);
                            GameInput.SaveOverrides();
                            RefreshRows();
                        }));
                    return;
                }
                GameInput.SaveOverrides();
                EndRebind(button);
            });
            op.Start();
        }

        void EndRebind(Button button, bool refresh = true)
        {
            GameInput.Asset.Enable();
            rebinding = false;
            if (!refresh) return;
            int rowIndex = button != null ? button.transform.parent.GetSiblingIndex() : 0;
            RefreshRows();
            // Old rows are destroyed at the end of this frame, so re-select next frame.
            StartCoroutine(SelectRowNextFrame(rowIndex));
        }

        System.Collections.IEnumerator SelectRowNextFrame(int rowIndex)
        {
            yield return null;
            if (bindingList == null) yield break;
            if (rowIndex >= bindingList.childCount) rowIndex = bindingList.childCount - 1;
            if (rowIndex < 0) yield break;
            var b = bindingList.GetChild(rowIndex).GetComponentInChildren<Button>();
            if (b != null && b.interactable) UIFactory.Select(b.gameObject);
        }

        /// <summary>
        /// Two bindings clash if they share a device group and a control and could be active at
        /// the same time: same action map, or either one is Pause (always live), or a test key
        /// against a player action.
        /// </summary>
        static bool FindConflict(InputAction action, int index, string group, string path, out InputAction other, out int otherIndex)
        {
            other = null;
            otherIndex = -1;
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var map in GameInput.Asset.actionMaps)
            foreach (var a in map.actions)
            {
                bool related = a.actionMap == action.actionMap || a == GameInput.Pause || action == GameInput.Pause ||
                               (a.actionMap == GameInput.DebugMap && action.actionMap == GameInput.PlayerMap) ||
                               (a.actionMap == GameInput.PlayerMap && action.actionMap == GameInput.DebugMap);
                if (!related) continue;
                for (int i = 0; i < a.bindings.Count; i++)
                {
                    if (a == action && i == index) continue;
                    var b = a.bindings[i];
                    if (b.isComposite || !GameInput.InGroup(b, group)) continue;
                    if (string.Equals(b.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        other = a;
                        otherIndex = i;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
