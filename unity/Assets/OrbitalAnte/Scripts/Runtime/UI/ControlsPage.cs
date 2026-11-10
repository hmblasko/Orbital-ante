using System;
using System.Collections;
using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The "Controls" page of the pause menu: one tab for keyboard and mouse, one for the controller.
    /// Pick a row, press a new key or button, and it is rebound and saved. Conflicts offer swap or cancel.
    /// </summary>
    public sealed class ControlsPage
    {
        sealed class Row
        {
            public GameInput.BindingEntry Entry;
            public Button Button;
            public Text Text;
            public Image Disc;
        }

        public GameObject Root { get; private set; }
        public bool IsRebinding { get; private set; }
        /// <summary>True while the swap-or-cancel question is on screen.</summary>
        public bool ConflictOpen => _conflict != null && _conflict.activeSelf;
        /// <summary>Menu input is ignored until this (unscaled) time, so the key just bound does not also click something.</summary>
        public float InputBlockedUntil { get; private set; }

        MonoBehaviour _host;
        Action _onBack;
        string _group = GameInput.KeyboardMouseGroup;
        Button _tabKeyboard;
        Button _tabGamepad;
        RectTransform _list;
        Text _hint;
        readonly List<Row> _rows = new List<Row>();

        GameObject _prompt;
        Text _promptText;
        GameObject _conflict;
        Text _conflictText;
        Button _conflictSwap;
        Button _conflictCancel;
        Action _onSwap;
        Row _lastRow;
        InputActionRebindingExtensions.RebindingOperation _operation;
        PadFamily _shownFamily;

        public GameObject DefaultSelection
        {
            get
            {
                if (ConflictOpen) return (_conflictSwap.gameObject.activeSelf ? _conflictSwap : _conflictCancel).gameObject;
                if (_lastRow != null && _lastRow.Button != null && _lastRow.Button.gameObject.activeInHierarchy && _lastRow.Button.interactable)
                    return _lastRow.Button.gameObject;
                return (_group == GameInput.GamepadGroup ? _tabGamepad : _tabKeyboard).gameObject;
            }
        }

        public void Build(Transform canvas, MonoBehaviour host, Action onBack)
        {
            _host = host;
            _onBack = onBack;

            var page = UiKit.NewRect("ControlsPage", canvas);
            UiKit.Stretch(page);
            Root = page.gameObject;

            var card = UiKit.Card(page, "ControlsCard", 860f);
            UiKit.LayoutLabel(card, "CONTROLS", 34, UiKit.PanelEdge, 44f);

            var tabs = UiKit.NewRect("Tabs", card);
            UiKit.HStack(tabs, 12f).childForceExpandWidth = true;
            UiKit.SetLayout(tabs.gameObject, 52f);
            _tabKeyboard = UiKit.Button(tabs, "Keyboard & Mouse", () => ShowGroup(GameInput.KeyboardMouseGroup), 52f, 24);
            _tabGamepad = UiKit.Button(tabs, "Controller", () => ShowGroup(GameInput.GamepadGroup), 52f, 24);

            _hint = UiKit.LayoutLabel(card, string.Empty, 18, UiKit.Muted, 26f, TextAnchor.MiddleCenter, FontStyle.Normal);

            _list = UiKit.NewRect("Bindings", card);
            var listGroup = UiKit.VStack(_list, 6f, 0);
            listGroup.childForceExpandHeight = false;

            var footer = UiKit.NewRect("Footer", card);
            UiKit.HStack(footer, 12f).childForceExpandWidth = true;
            UiKit.SetLayout(footer.gameObject, 52f);
            UiKit.Button(footer, "Reset to defaults", ResetCurrentGroup, 52f, 22);
            UiKit.Button(footer, "Back", () => _onBack?.Invoke(), 52f, 22);

            BuildPrompt(canvas);
            BuildConflict(canvas);
            Root.SetActive(false);
        }

        void BuildPrompt(Transform canvas)
        {
            var dim = UiKit.NewRect("RebindPrompt", canvas);
            UiKit.Stretch(dim);
            UiKit.AddImage(dim, new Color(0f, 0f, 0f, 0.75f), null, true);
            var card = UiKit.Card(dim, "PromptCard", 640f);
            UiKit.LayoutLabel(card, "REBINDING", 30, UiKit.PanelEdge, 40f);
            _promptText = UiKit.LayoutLabel(card, string.Empty, 24, UiKit.Cream, 130f);
            _prompt = dim.gameObject;
            _prompt.SetActive(false);
        }

        void BuildConflict(Transform canvas)
        {
            var dim = UiKit.NewRect("ConflictDialog", canvas);
            UiKit.Stretch(dim);
            UiKit.AddImage(dim, new Color(0f, 0f, 0f, 0.75f), null, true);
            var card = UiKit.Card(dim, "ConflictCard", 640f);
            UiKit.LayoutLabel(card, "ALREADY IN USE", 30, UiKit.PanelEdge, 40f);
            _conflictText = UiKit.LayoutLabel(card, string.Empty, 24, UiKit.Cream, 110f);
            _conflictSwap = UiKit.Button(card, "Swap them", () =>
            {
                var swap = _onSwap;
                CloseConflict();
                swap?.Invoke();
            });
            _conflictCancel = UiKit.Button(card, "Cancel", CloseConflict);
            _conflict = dim.gameObject;
            _conflict.SetActive(false);
        }

        public void Open()
        {
            Root.SetActive(true);
            // Start on the tab for whatever the player is holding right now.
            bool padInUse = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
            ShowGroup(padInUse ? GameInput.GamepadGroup : _group);
        }

        public void Close()
        {
            CancelRebind();
            _conflict.SetActive(false);
            _prompt.SetActive(false);
            Root.SetActive(false);
        }

        /// <summary>Called every frame while the page is open, so glyphs follow the controller that is plugged in.</summary>
        public void Tick()
        {
            if (_group == GameInput.GamepadGroup && Glyphs.CurrentFamily != _shownFamily && !IsRebinding)
            {
                RefreshTexts();
            }
        }

        void ShowGroup(string group)
        {
            _group = group;
            _lastRow = null;

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Button == null) continue;
                GameObject rowObject = _rows[i].Button.transform.parent.gameObject;
                rowObject.SetActive(false);
                UnityEngine.Object.Destroy(rowObject);
            }
            _rows.Clear();

            List<GameInput.BindingEntry> entries = GameInput.GetEntries(group);
            for (int i = 0; i < entries.Count; i++)
            {
                _rows.Add(BuildRow(entries[i]));
            }

            Tint(_tabKeyboard, group == GameInput.KeyboardMouseGroup);
            Tint(_tabGamepad, group == GameInput.GamepadGroup);
            RefreshTexts();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(DefaultSelection);
            }
        }

        static void Tint(Button tab, bool active)
        {
            var colors = tab.colors;
            colors.normalColor = active ? new Color(0.62f, 0.38f, 0.16f) : UiKit.ButtonColor;
            tab.colors = colors;
        }

        Row BuildRow(GameInput.BindingEntry entry)
        {
            var rowRect = UiKit.NewRect("Row " + entry.Label, _list);
            UiKit.HStack(rowRect, 12f);
            UiKit.SetLayout(rowRect.gameObject, 44f);

            var label = UiKit.Label(rowRect, entry.Label, 22, UiKit.Cream, TextAnchor.MiddleLeft);
            UiKit.SetLayout(label.gameObject, 44f, -1f, 1f);

            var row = new Row { Entry = entry };
            row.Button = UiKit.Button(rowRect, string.Empty, null, 44f, 21);
            UiKit.SetLayout(row.Button.gameObject, 44f, 340f, 0f);
            row.Text = UiKit.ButtonText(row.Button);
            row.Button.interactable = entry.Rebindable;
            row.Button.onClick.AddListener(() => StartRebind(row));

            var disc = UiKit.NewRect("Disc", row.Button.transform);
            disc.anchorMin = disc.anchorMax = new Vector2(0f, 0.5f);
            disc.pivot = new Vector2(0f, 0.5f);
            disc.anchoredPosition = new Vector2(12f, 0f);
            disc.sizeDelta = new Vector2(26f, 26f);
            row.Disc = UiKit.AddImage(disc, Color.white, UiKit.Circle);
            row.Disc.gameObject.SetActive(false);
            return row;
        }

        void RefreshTexts()
        {
            bool pad = _group == GameInput.GamepadGroup;
            _shownFamily = Glyphs.CurrentFamily;
            _hint.text = pad
                ? "Showing " + Glyphs.FamilyName(_shownFamily) + " buttons. Pick an action, then press the new button."
                : "Pick an action, then press the new key or mouse button.";

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                string path = row.Entry.Action.bindings[row.Entry.BindingIndex].effectivePath;
                if (pad)
                {
                    Glyph glyph = Glyphs.ForGamepadPath(path, _shownFamily);
                    row.Text.text = glyph.Text;
                    row.Disc.gameObject.SetActive(glyph.IsFaceButton);
                    row.Disc.color = glyph.Color;
                }
                else
                {
                    row.Text.text = row.Entry.Rebindable ? KeyboardName(path) : "Mouse (fixed)";
                    row.Disc.gameObject.SetActive(false);
                }
            }
        }

        static string KeyboardName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Unbound";
            string readable = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            if (string.IsNullOrEmpty(readable)) readable = path;
            if (path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase)) readable = "Mouse " + readable;
            return readable;
        }

        string DisplayName(string path)
        {
            return _group == GameInput.GamepadGroup
                ? Glyphs.ForGamepadPath(path, Glyphs.CurrentFamily).Text
                : KeyboardName(path);
        }

        // ------------------------------------------------------------------ rebinding

        void StartRebind(Row row)
        {
            if (IsRebinding || !row.Entry.Rebindable) return;

            InputAction action = row.Entry.Action;
            int index = row.Entry.BindingIndex;
            string oldPath = action.bindings[index].effectivePath;
            bool pad = _group == GameInput.GamepadGroup;

            _lastRow = row;
            IsRebinding = true;
            GameInput.UI.Disable();

            _promptText.text = pad
                ? "Press a controller button for\n" + row.Entry.Label + "\n\nMenu/Options cancels. Gives up after 6 seconds."
                : "Press a key or mouse button for\n" + row.Entry.Label + "\n\nEsc cancels. Gives up after 6 seconds.";
            _prompt.SetActive(true);
            _prompt.transform.SetAsLastSibling();

            // An action cannot be rebound while it is switched on. (Gameplay is already off while paused.)
            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();

            var operation = action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .OnMatchWaitForAnother(0.1f)
                .WithTimeout(6f);

            if (pad)
            {
                operation.WithControlsHavingToMatchPath("<Gamepad>")
                    .WithControlsExcluding("<Gamepad>/start")
                    .WithControlsExcluding("<Gamepad>/select")
                    .WithCancelingThrough("<Gamepad>/start");
            }
            else
            {
                operation.WithControlsExcluding("<Gamepad>")
                    .WithCancelingThrough("<Keyboard>/escape");
            }

            operation.OnComplete(op =>
            {
                string newPath = action.bindings[index].effectivePath;
                op.Dispose();
                _operation = null;
                if (wasEnabled) action.Enable();
                EndRebind();
                FinishRebind(row, oldPath, newPath);
            });
            operation.OnCancel(op =>
            {
                op.Dispose();
                _operation = null;
                if (wasEnabled) action.Enable();
                EndRebind();
                RefreshTexts();
            });

            _operation = operation;
            operation.Start();
        }

        void CancelRebind()
        {
            if (_operation != null)
            {
                // Cancel() runs the OnCancel callback above, which cleans up.
                _operation.Cancel();
            }
        }

        void EndRebind()
        {
            IsRebinding = false;
            InputBlockedUntil = Time.unscaledTime + 0.3f;
            _prompt.SetActive(false);
            if (_host != null && _host.isActiveAndEnabled) _host.StartCoroutine(ReenableUi());
            else GameInput.UI.Enable();
        }

        IEnumerator ReenableUi()
        {
            // Wait a moment so the key that was just bound does not also press a menu button.
            yield return new WaitForSecondsRealtime(0.25f);
            GameInput.UI.Enable();
            if (EventSystem.current != null && Root.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(DefaultSelection);
            }
        }

        void FinishRebind(Row row, string oldPath, string newPath)
        {
            InputAction action = row.Entry.Action;
            int index = row.Entry.BindingIndex;
            bool pad = _group == GameInput.GamepadGroup;

            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                RefreshTexts();
                return;
            }

            // The input must belong to the tab being edited.
            bool isPadPath = newPath.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase);
            if (isPadPath != pad)
            {
                SetPath(action, index, oldPath);
                RefreshTexts();
                return;
            }

            // Keys the game keeps for itself (pause, debug overlay) cannot be taken.
            foreach (InputAction reserved in GameInput.SystemMap.actions)
            {
                for (int i = 0; i < reserved.bindings.Count; i++)
                {
                    if (string.Equals(reserved.bindings[i].effectivePath, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        SetPath(action, index, oldPath);
                        RefreshTexts();
                        ShowConflict(DisplayName(newPath) + " is reserved for " + reserved.name + ".\nPick something else.", null);
                        return;
                    }
                }
            }

            // Is another action already using it?
            List<GameInput.BindingEntry> entries = GameInput.GetEntries(_group);
            for (int i = 0; i < entries.Count; i++)
            {
                GameInput.BindingEntry other = entries[i];
                if (other.Action == action && other.BindingIndex == index) continue;
                string otherPath = other.Action.bindings[other.BindingIndex].effectivePath;
                if (!string.Equals(otherPath, newPath, StringComparison.OrdinalIgnoreCase)) continue;

                // Put the old binding back until the player decides.
                SetPath(action, index, oldPath);
                RefreshTexts();
                ShowConflict(
                    DisplayName(newPath) + " is already used by " + other.Label + ".\nSwap the two, or cancel?",
                    () =>
                    {
                        SetPath(action, index, newPath);
                        SetPath(other.Action, other.BindingIndex, oldPath);
                        GameInput.SaveBindings();
                        RefreshTexts();
                    });
                return;
            }

            GameInput.SaveBindings();
            RefreshTexts();
        }

        /// <summary>Sets a binding to a path, storing no override at all when the path is the default.</summary>
        static void SetPath(InputAction action, int index, string path)
        {
            bool wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();
            if (string.Equals(action.bindings[index].path, path, StringComparison.OrdinalIgnoreCase))
                action.RemoveBindingOverride(index);
            else
                action.ApplyBindingOverride(index, path);
            if (wasEnabled) action.Enable();
        }

        void ShowConflict(string message, Action onSwap)
        {
            _onSwap = onSwap;
            _conflictText.text = message;
            _conflictSwap.gameObject.SetActive(onSwap != null);
            Root.SetActive(false);
            _conflict.SetActive(true);
            _conflict.transform.SetAsLastSibling();
            if (EventSystem.current != null)
            {
                // With nothing to swap, the only button is Cancel.
                EventSystem.current.SetSelectedGameObject(DefaultSelection);
            }
        }

        public void CloseConflict()
        {
            _onSwap = null;
            _conflict.SetActive(false);
            Root.SetActive(true);
            RefreshTexts();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(DefaultSelection);
        }

        void ResetCurrentGroup()
        {
            GameInput.ResetGroupToDefaults(_group);
            RefreshTexts();
        }
    }
}
