using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Input
{
    public enum ControllerFamily { Xbox, PlayStation, SteamDeck }

    /// <summary>
    /// Text glyphs for bindings. Sprite glyphs are an art asset still to make; the text uses
    /// the real labels printed on each controller so the player can always find the button.
    /// </summary>
    public static class InputGlyphs
    {
        static ControllerFamily? manualOverride;

        static readonly Dictionary<string, string> xbox = new Dictionary<string, string>
        {
            { "buttonSouth", "A" }, { "buttonEast", "B" }, { "buttonWest", "X" }, { "buttonNorth", "Y" },
            { "leftShoulder", "LB" }, { "rightShoulder", "RB" }, { "leftTrigger", "LT" }, { "rightTrigger", "RT" },
            { "start", "Menu" }, { "select", "View" }, { "leftStickPress", "LS click" }, { "rightStickPress", "RS click" },
            { "leftStick", "Left stick" }, { "rightStick", "Right stick" },
        };

        static readonly Dictionary<string, string> playstation = new Dictionary<string, string>
        {
            { "buttonSouth", "Cross" }, { "buttonEast", "Circle" }, { "buttonWest", "Square" }, { "buttonNorth", "Triangle" },
            { "leftShoulder", "L1" }, { "rightShoulder", "R1" }, { "leftTrigger", "L2" }, { "rightTrigger", "R2" },
            { "start", "Options" }, { "select", "Share" }, { "leftStickPress", "L3" }, { "rightStickPress", "R3" },
            { "leftStick", "Left stick" }, { "rightStick", "Right stick" },
        };

        static readonly Dictionary<string, string> deck = new Dictionary<string, string>
        {
            { "buttonSouth", "A" }, { "buttonEast", "B" }, { "buttonWest", "X" }, { "buttonNorth", "Y" },
            { "leftShoulder", "L1" }, { "rightShoulder", "R1" }, { "leftTrigger", "L2" }, { "rightTrigger", "R2" },
            { "start", "Menu" }, { "select", "View" }, { "leftStickPress", "L3" }, { "rightStickPress", "R3" },
            { "leftStick", "Left stick" }, { "rightStick", "Right stick" },
        };

        public static ControllerFamily Family => manualOverride ?? Detect();

        public static void CycleManualOverride()
        {
            var f = Family;
            manualOverride = (ControllerFamily)(((int)f + 1) % 3);
        }

        public static ControllerFamily Detect()
        {
            if (DeckPlatform.IsSteamDeck) return ControllerFamily.SteamDeck;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad is UnityEngine.InputSystem.DualShock.DualShockGamepad) return ControllerFamily.PlayStation;
            return ControllerFamily.Xbox;
        }

        public static string FamilyName(ControllerFamily f) =>
            f == ControllerFamily.SteamDeck ? "Steam Deck" : f == ControllerFamily.PlayStation ? "PlayStation" : "Xbox";

        /// <summary>True when a gamepad was touched more recently than the keyboard or mouse.</summary>
        public static bool UsingGamepad()
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad == null) return false;
            double kb = Keyboard.current != null ? Keyboard.current.lastUpdateTime : 0;
            double mouse = Mouse.current != null ? Mouse.current.lastUpdateTime : 0;
            return pad.lastUpdateTime > System.Math.Max(kb, mouse);
        }

        public static string BindingLabel(InputAction action, int bindingIndex, string group)
        {
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "-";
            string display = action.GetBindingDisplayString(bindingIndex, out string layout, out string controlPath);
            if (group == GameInput.Gamepad && !string.IsNullOrEmpty(controlPath))
            {
                var table = Family == ControllerFamily.PlayStation ? playstation
                    : Family == ControllerFamily.SteamDeck ? deck : xbox;
                if (table.TryGetValue(controlPath, out var label)) return label;
                if (controlPath.StartsWith("dpad/")) return "D-pad " + Capitalise(controlPath.Substring(5));
                if (controlPath == "dpad") return "D-pad";
            }
            return string.IsNullOrEmpty(display) ? "-" : display;
        }

        /// <summary>Label for the device the player is using right now.</summary>
        public static string HintFor(InputAction action)
        {
            string group = UsingGamepad() ? GameInput.Gamepad : GameInput.KeyboardMouse;
            return BindingLabel(action, GameInput.FirstBindingIndex(action, group), group);
        }

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
