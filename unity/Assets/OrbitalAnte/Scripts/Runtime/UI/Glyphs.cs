using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace OrbitalAnte.UI
{
    public enum PadFamily
    {
        Xbox,
        PlayStation,
        SteamDeck,
    }

    /// <summary>How to draw one controller input: its name on this controller, and a face-button colour if it has one.</summary>
    public struct Glyph
    {
        public string Text;
        /// <summary>True for the four face buttons, which are drawn as a coloured disc.</summary>
        public bool IsFaceButton;
        public Color Color;
    }

    /// <summary>
    /// Names controller inputs the way the connected controller labels them (Steam Deck, Xbox or PlayStation).
    /// Drawn as text badges for now; proper glyph art can replace the drawing without touching this lookup.
    /// </summary>
    public static class Glyphs
    {
        static readonly Color XboxA = new Color(0.36f, 0.72f, 0.25f);
        static readonly Color XboxB = new Color(0.85f, 0.24f, 0.20f);
        static readonly Color XboxX = new Color(0.20f, 0.48f, 0.86f);
        static readonly Color XboxY = new Color(0.93f, 0.73f, 0.16f);
        static readonly Color PsCross = new Color(0.42f, 0.56f, 0.87f);
        static readonly Color PsCircle = new Color(0.87f, 0.35f, 0.33f);
        static readonly Color PsSquare = new Color(0.82f, 0.50f, 0.74f);
        static readonly Color PsTriangle = new Color(0.30f, 0.72f, 0.62f);
        static readonly Color DeckFace = new Color(0.82f, 0.82f, 0.84f);

        public static PadFamily CurrentFamily
        {
            get
            {
                if (IsSteamDeck()) return PadFamily.SteamDeck;
                Gamepad pad = Gamepad.current;
                if (pad is DualShockGamepad) return PadFamily.PlayStation;
                if (pad != null)
                {
                    string product = (pad.description.product ?? string.Empty).ToLowerInvariant();
                    string maker = (pad.description.manufacturer ?? string.Empty).ToLowerInvariant();
                    if (product.Contains("dualsense") || product.Contains("dualshock") || product.Contains("playstation") || maker.Contains("sony"))
                        return PadFamily.PlayStation;
                }
                return PadFamily.Xbox;
            }
        }

        public static string FamilyName(PadFamily family)
        {
            switch (family)
            {
                case PadFamily.SteamDeck: return "Steam Deck";
                case PadFamily.PlayStation: return "PlayStation";
                default: return "Xbox";
            }
        }

        static bool IsSteamDeck()
        {
            try
            {
                if (Environment.GetEnvironmentVariable("SteamDeck") == "1") return true;
            }
            catch (Exception)
            {
                // Some platforms do not allow reading environment variables. Fall through to the model check.
            }
            string model = SystemInfo.deviceModel ?? string.Empty;
            return model.IndexOf("Jupiter", StringComparison.OrdinalIgnoreCase) >= 0
                || model.IndexOf("Galileo", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Looks up the glyph for a binding path such as "&lt;Gamepad&gt;/buttonSouth".</summary>
        public static Glyph ForGamepadPath(string path, PadFamily family)
        {
            string control = path ?? string.Empty;
            int slash = control.IndexOf('/');
            if (slash >= 0) control = control.Substring(slash + 1);

            bool ps = family == PadFamily.PlayStation;
            bool deck = family == PadFamily.SteamDeck;

            switch (control)
            {
                case "buttonSouth":
                    return Face(ps ? "Cross" : "A", ps ? PsCross : deck ? DeckFace : XboxA);
                case "buttonEast":
                    return Face(ps ? "Circle" : "B", ps ? PsCircle : deck ? DeckFace : XboxB);
                case "buttonWest":
                    return Face(ps ? "Square" : "X", ps ? PsSquare : deck ? DeckFace : XboxX);
                case "buttonNorth":
                    return Face(ps ? "Triangle" : "Y", ps ? PsTriangle : deck ? DeckFace : XboxY);

                case "leftShoulder": return Plain(ps || deck ? "L1" : "LB");
                case "rightShoulder": return Plain(ps || deck ? "R1" : "RB");
                case "leftTrigger": return Plain(ps || deck ? "L2" : "LT");
                case "rightTrigger": return Plain(ps || deck ? "R2" : "RT");
                case "leftStickPress": return Plain(ps || deck ? "L3" : "Left Stick Click");
                case "rightStickPress": return Plain(ps || deck ? "R3" : "Right Stick Click");
                case "leftStick": return Plain("Left Stick");
                case "rightStick": return Plain("Right Stick");
                case "dpad": return Plain("D-Pad");
                case "dpad/up": return Plain("D-Pad Up");
                case "dpad/down": return Plain("D-Pad Down");
                case "dpad/left": return Plain("D-Pad Left");
                case "dpad/right": return Plain("D-Pad Right");
                case "start": return Plain(ps ? "Options" : deck ? "Menu" : "Menu");
                case "select": return Plain(ps ? "Create" : "View");
                case "leftStick/up": return Plain("Left Stick Up");
                case "leftStick/down": return Plain("Left Stick Down");
                case "leftStick/left": return Plain("Left Stick Left");
                case "leftStick/right": return Plain("Left Stick Right");
                case "rightStick/up": return Plain("Right Stick Up");
                case "rightStick/down": return Plain("Right Stick Down");
                case "rightStick/left": return Plain("Right Stick Left");
                case "rightStick/right": return Plain("Right Stick Right");
            }

            // Anything unusual: let the Input System describe it.
            string readable = InputControlPath.ToHumanReadableString(path ?? string.Empty,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            return Plain(string.IsNullOrEmpty(readable) ? "Unbound" : readable);
        }

        static Glyph Face(string text, Color color)
        {
            return new Glyph { Text = text, IsFaceButton = true, Color = color };
        }

        static Glyph Plain(string text)
        {
            return new Glyph { Text = text, IsFaceButton = false, Color = UiKit.Cream };
        }
    }
}
