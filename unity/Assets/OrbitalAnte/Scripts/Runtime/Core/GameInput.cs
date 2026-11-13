using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// All input actions for the game, built in code so there is no asset to wire up by hand.
    ///
    /// Three maps:
    /// - Gameplay: everything the player can rebind in the pause menu.
    /// - System: pause and debug keys. Fixed, so the player can never lock themselves out of the menu.
    /// - UI: menu navigation.
    ///
    /// Select/View on the controller is deliberately left unbound: it is reserved for the end-of-ante upgrade screen.
    /// </summary>
    public static class GameInput
    {
        public const string KeyboardMouseGroup = "KeyboardMouse";
        public const string GamepadGroup = "Gamepad";
        const string PrefsKey = "oa.bindings.v1";

        public static InputActionAsset Asset { get; private set; }
        public static InputActionMap Gameplay { get; private set; }
        public static InputActionMap SystemMap { get; private set; }
        public static InputActionMap UI { get; private set; }

        public static InputAction Move { get; private set; }
        public static InputAction Look { get; private set; }
        public static InputAction Jump { get; private set; }
        public static InputAction Sprint { get; private set; }
        public static InputAction Fire { get; private set; }
        public static InputAction Reload { get; private set; }
        public static InputAction Interact { get; private set; }
        public static InputAction RespawnDummies { get; private set; }
        public static InputAction HoldFire { get; private set; }
        /// <summary>Opens or closes the ship's belly hatch, from inside or from nearby outside.</summary>
        public static InputAction Hatch { get; private set; }

        /// <summary>Flying controls. Rebindable, like Gameplay, and shown in the controls menu while flying.</summary>
        public static InputActionMap Flight { get; private set; }
        public static InputAction FlyMove { get; private set; }
        public static InputAction FlyLook { get; private set; }
        public static InputAction FlyUp { get; private set; }
        public static InputAction FlyDown { get; private set; }
        public static InputAction RollLeft { get; private set; }
        public static InputAction RollRight { get; private set; }
        public static InputAction MatchVelocity { get; private set; }
        public static InputAction Boost { get; private set; }
        public static InputAction ToggleMap { get; private set; }
        public static InputAction Unbuckle { get; private set; }
        public static InputAction FlyHatch { get; private set; }
        public static InputAction LockTarget { get; private set; }
        public static InputAction Autopilot { get; private set; }
        public static InputAction VelocityLock { get; private set; }

        /// <summary>The rebindable map for what the player is doing right now: on foot (Gameplay) or flying.</summary>
        public static InputActionMap Play { get; private set; }

        /// <summary>Chooses which set of play controls is live. The other set is switched off.</summary>
        public static void SetFlying(bool flying)
        {
            if (Asset == null) return;
            bool wasOn = Play != null && Play.enabled;
            if (Play != null) Play.Disable();
            Play = flying ? Flight : Gameplay;
            if (wasOn) Play.Enable();
        }

        /// <summary>True when the controller was touched more recently than the keyboard or mouse.</summary>
        public static bool UsingGamepad
        {
            get
            {
                Gamepad pad = Gamepad.current;
                if (pad == null) return false;
                double other = 0;
                if (Keyboard.current != null) other = Keyboard.current.lastUpdateTime;
                if (Mouse.current != null && Mouse.current.lastUpdateTime > other) other = Mouse.current.lastUpdateTime;
                return pad.lastUpdateTime > other;
            }
        }

        /// <summary>
        /// Effective keys or buttons for the device in use. Optional part names limit a composite to an axis
        /// (for example Up/Down for map zoom); a directly bound stick is still described as a whole.
        /// </summary>
        public static string Describe(InputAction action, string firstPart = null, string secondPart = null)
        {
            return DescribeForGroup(action, UsingGamepad ? GamepadGroup : KeyboardMouseGroup, firstPart, secondPart);
        }

        /// <summary>Explicit-group form for hints that apply to a particular device, such as the fixed F3 debug key.</summary>
        public static string DescribeForGroup(InputAction action, string group, string firstPart = null, string secondPart = null)
        {
            if (action == null) return "Unbound";
            string result = string.Empty;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                // Composite headers have no device group. Their parts do, and can each be rebound.
                if (binding.isComposite || !InGroup(binding, group)) continue;
                if (binding.isPartOfComposite && firstPart != null
                    && !string.Equals(binding.name, firstPart, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(binding.name, secondPart, StringComparison.OrdinalIgnoreCase)) continue;

                string text = action.GetBindingDisplayString(i);
                if (string.IsNullOrEmpty(text)) text = "Unbound";
                result = result.Length == 0 ? text : result + " / " + text;
            }
            return result.Length == 0 ? "Unbound" : result;
        }

        public static InputAction Pause { get; private set; }
        public static InputAction ToggleDebug { get; private set; }

        public static InputAction UiNavigate { get; private set; }
        public static InputAction UiSubmit { get; private set; }
        public static InputAction UiCancel { get; private set; }
        public static InputAction UiPoint { get; private set; }
        public static InputAction UiClick { get; private set; }
        public static InputAction UiScroll { get; private set; }

        /// <summary>One row in the controls menu: a single binding of a single action.</summary>
        public struct BindingEntry
        {
            public InputAction Action;
            public int BindingIndex;
            public string Label;
            /// <summary>False for bindings that make no sense to rebind (mouse look).</summary>
            public bool Rebindable;
        }

        public static void Initialize()
        {
            if (Asset != null) return;

            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Asset.name = "OrbitalAnteInput";

            // ---------- Gameplay (rebindable) ----------
            Gameplay = Asset.AddActionMap("Gameplay");

            Move = Gameplay.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouseGroup)
                .With("Down", "<Keyboard>/s", KeyboardMouseGroup)
                .With("Left", "<Keyboard>/a", KeyboardMouseGroup)
                .With("Right", "<Keyboard>/d", KeyboardMouseGroup);
            Move.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);

            Look = Gameplay.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            Look.AddBinding("<Mouse>/delta", groups: KeyboardMouseGroup);
            Look.AddBinding("<Gamepad>/rightStick", groups: GamepadGroup);

            Jump = AddButton(Gameplay, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Sprint = AddButton(Gameplay, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Fire = AddButton(Gameplay, "Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            Reload = AddButton(Gameplay, "Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
            Interact = AddButton(Gameplay, "Pick Up / Swap Weapon", "<Keyboard>/e", "<Gamepad>/buttonNorth");
            RespawnDummies = AddButton(Gameplay, "Respawn Dummies", "<Keyboard>/t", "<Gamepad>/dpad/up");
            HoldFire = AddButton(Gameplay, "Enemies Hold Fire (toggle)", "<Keyboard>/h", "<Gamepad>/dpad/down");
            Hatch = AddButton(Gameplay, "Ship Hatch", "<Keyboard>/g", "<Gamepad>/dpad/left");

            // ---------- Flight (rebindable) ----------
            Flight = Asset.AddActionMap("Flight");
            FlyMove = Flight.AddAction("Thrust", InputActionType.Value, expectedControlLayout: "Vector2");
            FlyMove.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouseGroup)
                .With("Down", "<Keyboard>/s", KeyboardMouseGroup)
                .With("Left", "<Keyboard>/a", KeyboardMouseGroup)
                .With("Right", "<Keyboard>/d", KeyboardMouseGroup);
            FlyMove.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);
            FlyLook = Flight.AddAction("Steer", InputActionType.Value, expectedControlLayout: "Vector2");
            FlyLook.AddBinding("<Mouse>/delta", groups: KeyboardMouseGroup);
            FlyLook.AddBinding("<Gamepad>/rightStick", groups: GamepadGroup);
            FlyUp = AddButton(Flight, "Thrust Up", "<Keyboard>/space", "<Gamepad>/rightTrigger");
            FlyDown = AddButton(Flight, "Thrust Down", "<Keyboard>/leftCtrl", "<Gamepad>/leftTrigger");
            RollLeft = AddButton(Flight, "Roll Left", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            RollRight = AddButton(Flight, "Roll Right", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            MatchVelocity = AddButton(Flight, "Match Velocity (hold)", "<Keyboard>/f", "<Gamepad>/buttonSouth");
            Boost = AddButton(Flight, "Boost (hold)", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            ToggleMap = AddButton(Flight, "Map", "<Keyboard>/m", "<Gamepad>/dpad/up");
            Unbuckle = AddButton(Flight, "Unbuckle", "<Keyboard>/b", "<Gamepad>/buttonNorth");
            FlyHatch = AddButton(Flight, "Ship Hatch", "<Keyboard>/g", "<Gamepad>/dpad/left");
            LockTarget = AddButton(Flight, "Target Lock (toggle)", "<Keyboard>/t", "<Gamepad>/rightStickPress");
            Autopilot = AddButton(Flight, "Autopilot (toggle)", "<Keyboard>/p", "<Gamepad>/buttonWest");
            VelocityLock = AddButton(Flight, "Velocity Lock (toggle)", "<Keyboard>/v", "<Gamepad>/dpad/down");
            Play = Gameplay;

            // ---------- System (fixed) ----------
            SystemMap = Asset.AddActionMap("System");
            Pause = SystemMap.AddAction("Pause", InputActionType.Button);
            Pause.AddBinding("<Keyboard>/escape", groups: KeyboardMouseGroup);
            Pause.AddBinding("<Gamepad>/start", groups: GamepadGroup);
            ToggleDebug = SystemMap.AddAction("Toggle Debug", InputActionType.Button);
            ToggleDebug.AddBinding("<Keyboard>/f3", groups: KeyboardMouseGroup);

            // ---------- UI ----------
            UI = Asset.AddActionMap("UI");
            UiNavigate = UI.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            UiNavigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            UiNavigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            UiNavigate.AddBinding("<Gamepad>/leftStick");
            UiNavigate.AddBinding("<Gamepad>/dpad");

            UiSubmit = UI.AddAction("Submit", InputActionType.Button);
            UiSubmit.AddBinding("<Keyboard>/enter");
            UiSubmit.AddBinding("<Keyboard>/numpadEnter");
            UiSubmit.AddBinding("<Gamepad>/buttonSouth");

            UiCancel = UI.AddAction("Cancel", InputActionType.Button);
            UiCancel.AddBinding("<Keyboard>/backspace");
            UiCancel.AddBinding("<Gamepad>/buttonEast");

            UiPoint = UI.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            UiPoint.AddBinding("<Mouse>/position");

            UiClick = UI.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button");
            UiClick.AddBinding("<Mouse>/leftButton");

            UiScroll = UI.AddAction("Scroll", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            UiScroll.AddBinding("<Mouse>/scroll");

            LoadBindings();

            SystemMap.Enable();
            UI.Enable();
            // The play controls are switched on by the game when a run starts and off while paused.
        }

        static InputAction AddButton(InputActionMap map, string name, string keyboardPath, string gamepadPath)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboardPath, groups: KeyboardMouseGroup);
            action.AddBinding(gamepadPath, groups: GamepadGroup);
            return action;
        }

        /// <summary>Every binding of the current play controls that belongs to one device group, in menu order.</summary>
        public static List<BindingEntry> GetEntries(string group)
        {
            var entries = new List<BindingEntry>();
            if (Play == null) return entries;

            foreach (var action in Play.actions)
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];
                    if (binding.isComposite) continue;
                    if (!InGroup(binding, group)) continue;

                    string label = action.name;
                    if (binding.isPartOfComposite && !string.IsNullOrEmpty(binding.name))
                    {
                        label = action.name + " " + Capitalize(binding.name);
                    }

                    entries.Add(new BindingEntry
                    {
                        Action = action,
                        BindingIndex = i,
                        Label = label,
                        Rebindable = binding.path != "<Mouse>/delta",
                    });
                }
            }
            return entries;
        }

        public static bool InGroup(InputBinding binding, string group)
        {
            if (string.IsNullOrEmpty(binding.groups)) return false;
            var parts = binding.groups.Split(InputBinding.Separator);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == group) return true;
            }
            return false;
        }

        /// <summary>Puts every binding of the current play controls, for one device group, back to its default.</summary>
        public static void ResetGroupToDefaults(string group)
        {
            if (Play == null) return;
            foreach (var action in Play.actions)
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    if (bindings[i].isComposite) continue;
                    if (!InGroup(bindings[i], group)) continue;
                    action.RemoveBindingOverride(i);
                }
            }
            SaveBindings();
        }

        public static void SaveBindings()
        {
            if (Asset == null) return;
            PlayerPrefs.SetString(PrefsKey, Asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        static void LoadBindings()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                Asset.LoadBindingOverridesFromJson(json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Saved key bindings could not be read and were ignored: " + e.Message);
            }
        }

        static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
