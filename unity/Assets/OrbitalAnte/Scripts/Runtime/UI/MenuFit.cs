using System;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// How a page of the pause menu's choices fits the screen (Ralph loop pass 1, 27 Nov 2026). Plain C#.
    ///
    /// The choices page is a card of a heading, a body and up to seven buttons, stacked with fixed gaps. A break
    /// with all seven rows was 774 units tall, six rows at full height 804: over the Deck's 800, and well over the
    /// 759 units a 16:9 monitor gives the same canvas (it is scaled half by width, half by height). Now the body
    /// can grow with its words (a goal unlocked at the break adds a line) and the buttons give up height to keep
    /// the whole card on the screen, down to a floor that still reads.
    /// </summary>
    public static class MenuFit
    {
        /// <summary>The card's fixed parts, from <c>PauseMenu.NewPage</c> and <c>UiKit.Card</c>.</summary>
        public const float Heading = 54f, Gap = 12f, Padding = 56f;
        /// <summary>Kept clear above and below the card.</summary>
        public const float Margin = 12f;
        /// <summary>No button is drawn shorter than this.</summary>
        public const float LeastButton = 44f;

        /// <summary>How tall the whole card is.</summary>
        public static float CardHeight(int rows, float bodyHeight, float buttonHeight)
        {
            rows = Math.Max(0, rows);
            return Heading + Math.Max(0f, bodyHeight) + rows * buttonHeight + Gap * (rows + 1) + Padding;
        }

        /// <summary>The tallest buttons, up to <paramref name="preferred"/>, that keep the card on a screen this tall.</summary>
        public static float ButtonHeight(int rows, float bodyHeight, float preferred, float screenHeight)
        {
            if (rows <= 0) return preferred;
            if (!(screenHeight > 0f)) screenHeight = 800f;
            float room = screenHeight - 2f * Margin - CardHeight(rows, bodyHeight, 0f);
            float each = (float)Math.Floor(room / rows);
            return Math.Max(LeastButton, Math.Min(preferred, each));
        }

        /// <summary>Lines the text takes at about <paramref name="charsPerLine"/> characters a line, wrapped lines counted.</summary>
        public static int Lines(string text, int charsPerLine)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            charsPerLine = Math.Max(1, charsPerLine);
            int lines = 0;
            foreach (string line in text.Split('\n'))
                lines += Math.Max(1, (line.Length + charsPerLine - 1) / charsPerLine);
            return lines;
        }

        /// <summary>A body tall enough for its lines, and never shorter than <paramref name="least"/>.</summary>
        public static float BodyHeight(string text, float least, float lineHeight, int charsPerLine)
        {
            return Math.Max(least, Lines(text, charsPerLine) * lineHeight);
        }
    }
}
