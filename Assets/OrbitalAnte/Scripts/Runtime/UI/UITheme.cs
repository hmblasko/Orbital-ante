using UnityEngine;

namespace OrbitalAnte.UI
{
    /// <summary>Goofy-but-readable palette: deep space navy, cream text, rocket orange.</summary>
    public static class UITheme
    {
        public static readonly Color Backdrop = new Color(0.03f, 0.04f, 0.09f, 0.72f);
        public static readonly Color Card = new Color(0.11f, 0.14f, 0.23f, 0.97f);
        public static readonly Color CardEdge = new Color(0.95f, 0.55f, 0.16f, 1f);
        public static readonly Color Text = new Color(0.97f, 0.93f, 0.85f, 1f);
        public static readonly Color TextDim = new Color(0.72f, 0.72f, 0.78f, 1f);
        public static readonly Color Accent = new Color(0.95f, 0.55f, 0.16f, 1f);
        public static readonly Color Teal = new Color(0.25f, 0.72f, 0.69f, 1f);
        public static readonly Color Danger = new Color(0.85f, 0.28f, 0.23f, 1f);
        public static readonly Color Button = new Color(0.19f, 0.24f, 0.38f, 1f);
        public static readonly Color ButtonHover = new Color(0.27f, 0.33f, 0.50f, 1f);
        public static readonly Color ButtonSelected = new Color(0.95f, 0.55f, 0.16f, 1f);
        public static readonly Color ButtonPressed = new Color(1f, 0.75f, 0.35f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.16f, 0.18f, 0.24f, 0.8f);

        public const int TitleSize = 46;
        public const int HeaderSize = 30;
        public const int BodySize = 22;
        public const int SmallSize = 17;
    }
}
