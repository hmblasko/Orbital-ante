using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Input
{
    /// <summary>
    /// Every game action, built in code so there is a single place to read the defaults.
    /// Each binding is tagged with a group (KeyboardMouse or Gamepad) so the pause menu can
    /// show one tab per device. Player overrides are saved to PlayerPrefs as JSON.
    /// </summary>
    public static class GameInput
    {
        public const string KeyboardMouse = "KeyboardMouse";
        public const string Gamepad = "Gamepad";
        const string PrefsKey = "oa.bindings.v1";

        static InputActionAsset asset;

        public static InputActionMap PlayerMap { get; private set; }
        public static InputActionMap MenuMap { get; private set; }
        public static InputActionMap DebugMap { get; private set; }

        // Player
        public static InputAction Move { get; private set; }
        public static InputAction LookMouse { get; private set; }
        public static InputAction LookStick { get; private set; }
        public static InputAction Jump { get; private set; }
        public static InputAction Crouch { get; private set; }
        public static InputAction Sprint { get; private set; }
        public static InputAction Fire { get; private set; }
        public static InputAction Aim { get; private set; }
        public static InputAction Reload { get; private set; }
        public static InputAction Interact { get; private set; }

        // Menu
        public static InputAction Pause { get; private set; }
        public static InputAction Back { get; private set; }
        public static InputAction UpgradeScreen { get; private set; }

        // Playtest tools
        public static InputAction DebugRespawn { get; private set; }
        public static InputAction DebugWalkMode { get; private set; }
        public static InputAction DebugOverlay { get; private set; }
        public static InputAction DebugSlowMo { get; private set; }

        /// <summary>Actions the player can see but not rebind (mouse look is an axis, not a key).</summary>
        public static readonly HashSet<InputAction> Locked = new HashSet<InputAction>();

        static readonly Dictionary<string, string> displayNames = new Dictionary<string, string>
        {
            { "Move", "Move" }, { "LookMouse", "Look" }, { "LookStick", "Look" },
            { "Jump", "Jump" }, { "Crouch", "Crouch" }, { "Sprint", "Sprint" },
            { "Fire", "Fire" }, { "Aim", "Aim" }, { "Reload", "Reload" }, { "Interact", "Interact" },
            { "Pause", "Pause menu" }, { "Back", "Menu back" }, { "UpgradeScreen", "Upgrade screen" },
            { "DebugRespawn", "[Test] Respawn dummies" }, { "DebugWalkMode", "[Test] Dummy walk mode" },
            { "DebugOverlay", "[Test] Frame-time overlay" },
            { "DebugSlowMo", "[Test] Headshot slow-mo on/off" },
        };

        public static InputActionAsset Asset
        {
            get
            {
                if (asset == null) Build();
                return asset;
            }
        }

        public static string DisplayName(InputAction action) =>
            displayNames.TryGetValue(action.name, out var n) ? n : action.name;

        public static void EnsureCreated()
        {
            if (asset == null) Build();
        }

        static void Build()
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "OrbitalAnteControls";

            PlayerMap = asset.AddActionMap("Player");
            Move = PlayerMap.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouse)
                .With("Down", "<Keyboard>/s", KeyboardMouse)
                .With("Left", "<Keyboard>/a", KeyboardMouse)
                .With("Right", "<Keyboard>/d", KeyboardMouse);
            Move.AddBinding("<Gamepad>/leftStick", groups: Gamepad);

            LookMouse = PlayerMap.AddAction("LookMouse", InputActionType.Value, expectedControlLayout: "Vector2");
            LookMouse.AddBinding("<Mouse>/delta", groups: KeyboardMouse);
            Locked.Add(LookMouse);

            LookStick = PlayerMap.AddAction("LookStick", InputActionType.Value, expectedControlLayout: "Vector2");
            LookStick.AddBinding("<Gamepad>/rightStick", groups: Gamepad);

            Jump = Button(PlayerMap, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Crouch = Button(PlayerMap, "Crouch", "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast");
            Sprint = Button(PlayerMap, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Fire = Button(PlayerMap, "Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            Aim = Button(PlayerMap, "Aim", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            Reload = Button(PlayerMap, "Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
            Interact = Button(PlayerMap, "Interact", "<Keyboard>/e", "<Gamepad>/buttonNorth");

            MenuMap = asset.AddActionMap("Menu");
            Pause = Button(MenuMap, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            Back = Button(MenuMap, "Back", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            UpgradeScreen = Button(MenuMap, "UpgradeScreen", "<Keyboard>/tab", "<Gamepad>/select");

            DebugMap = asset.AddActionMap("Debug");
            DebugRespawn = Button(DebugMap, "DebugRespawn", "<Keyboard>/t", "<Gamepad>/dpad/up");
            DebugWalkMode = Button(DebugMap, "DebugWalkMode", "<Keyboard>/g", "<Gamepad>/dpad/left");
            DebugOverlay = Button(DebugMap, "DebugOverlay", "<Keyboard>/f3", "<Gamepad>/dpad/down");
            DebugSlowMo = Button(DebugMap, "DebugSlowMo", "<Keyboard>/m", "<Gamepad>/dpad/right");

            LoadOverrides();
            asset.Enable();
        }

        static InputAction Button(InputActionMap map, string name, string keyboardPath, string gamepadPath)
        {
            var a = map.AddAction(name, InputActionType.Button);
            a.AddBinding(keyboardPath, groups: KeyboardMouse);
            a.AddBinding(gamepadPath, groups: Gamepad);
            return a;
        }

        public static void SaveOverrides()
        {
            PlayerPrefs.SetString(PrefsKey, Asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        static void LoadOverrides()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                asset.LoadBindingOverridesFromJson(json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OA] Saved bindings could not be loaded, using defaults. " + e.Message);
                PlayerPrefs.DeleteKey(PrefsKey);
            }
        }

        public static bool InGroup(InputBinding binding, string group) =>
            InputBinding.MaskByGroup(group).Matches(binding);

        /// <summary>Removes every override for one device group, then saves.</summary>
        public static void ResetGroup(string group)
        {
            foreach (var map in Asset.actionMaps)
            foreach (var action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite) continue;
                    if (InGroup(b, group)) action.RemoveBindingOverride(i);
                }
            }
            SaveOverrides();
        }

        /// <summary>First binding index of an action in a group (for HUD hints).</summary>
        public static int FirstBindingIndex(InputAction action, string group)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (InGroup(b, group)) return i;
            }
            return -1;
        }
    }
}
