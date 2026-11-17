using System;

namespace OrbitalAnte.Cockpit
{
    /// <summary>The dash designs on offer. Matthew picks; the choice is on the CockpitConfig asset and can be flipped in play.</summary>
    public enum DashStyle
    {
        /// <summary>
        /// A dark glass screen set in the console: the direction ball, speed, height, the little ship and the
        /// camera, in warm line work with hardly a word. Air and fuel are not on it: they are real gauges on
        /// the console beside it (<see cref="ConsoleGauges"/>).
        /// </summary>
        FlightDeck,
        /// <summary>Dark panel, chunky blocks and bubbles, big bright numbers. Loudest and quickest to read.</summary>
        Arcade,
        /// <summary>No panel at all: thin amber lines floating over the desk. Least in the way.</summary>
        Glass,
        /// <summary>Black screen, green text. The old-school one.</summary>
        GreenScreen,
    }

    /// <summary>
    /// Paints each dash design into a <see cref="DashDrawing"/>. Plain C#, no Unity types.
    ///
    /// The three spare designs show the same five things in the same places, so switching between them never
    /// moves where you look: air and fuel on the left, the direction ball and speed in the middle, the little
    /// ship showing your push on the right, and the camera window at the far right. Fuel is always a bar of
    /// some kind and air is always something round, so the two are never mistaken.
    ///
    /// The Flight deck design (the one Matthew chose) is the screen of the modelled console. It leaves air and
    /// fuel to the console's own gauges and says as little as it can in words: numbers, the name of the body
    /// the speed is measured against, which camera, and the one notice.
    /// </summary>
    public static class DashDesigns
    {
        public const int Count = 4;

        public static string Name(DashStyle style)
        {
            switch (style)
            {
                case DashStyle.FlightDeck: return "Flight deck";
                case DashStyle.Arcade: return "Arcade";
                case DashStyle.Glass: return "Glass";
                default: return "Green screen";
            }
        }

        struct Look
        {
            public DashColor Panel, Edge, Face, Ink, Dim, Accent, Warn, Fuel, Air, Flame;
            public bool HasPanel;
        }

        static Look LookOf(DashStyle style)
        {
            switch (style)
            {
                case DashStyle.FlightDeck:
                    return new Look
                    {
                        HasPanel = true, Panel = new DashColor(0.045f, 0.065f, 0.08f), Edge = new DashColor(0.20f, 0.27f, 0.30f),
                        Face = new DashColor(0.02f, 0.03f, 0.04f), Ink = new DashColor(0.98f, 0.90f, 0.72f), Dim = new DashColor(0.46f, 0.54f, 0.55f),
                        Accent = new DashColor(1f, 0.64f, 0.18f), Warn = new DashColor(1f, 0.30f, 0.20f),
                        Fuel = new DashColor(0.96f, 0.60f, 0.12f), Air = new DashColor(0.42f, 0.78f, 0.95f), Flame = new DashColor(1f, 0.66f, 0.2f),
                    };
                case DashStyle.Arcade:
                    return new Look
                    {
                        HasPanel = true, Panel = new DashColor(0.10f, 0.12f, 0.22f), Edge = new DashColor(0.98f, 0.55f, 0.12f),
                        Face = new DashColor(0.05f, 0.06f, 0.12f), Ink = new DashColor(1f, 0.95f, 0.55f), Dim = new DashColor(0.42f, 0.46f, 0.66f),
                        Accent = new DashColor(0.25f, 0.92f, 0.85f), Warn = new DashColor(1f, 0.25f, 0.35f),
                        Fuel = new DashColor(0.45f, 0.95f, 0.35f), Air = new DashColor(0.45f, 0.75f, 1f), Flame = new DashColor(1f, 0.5f, 0.15f),
                    };
                case DashStyle.Glass:
                    return new Look
                    {
                        HasPanel = false, Panel = new DashColor(0f, 0f, 0f, 0f), Edge = new DashColor(1f, 0.72f, 0.3f, 0.55f),
                        Face = new DashColor(0.05f, 0.04f, 0.02f, 0.35f), Ink = new DashColor(1f, 0.78f, 0.38f), Dim = new DashColor(1f, 0.72f, 0.3f, 0.6f),
                        Accent = new DashColor(0.55f, 0.95f, 0.9f), Warn = new DashColor(1f, 0.35f, 0.25f),
                        Fuel = new DashColor(1f, 0.78f, 0.38f), Air = new DashColor(0.55f, 0.95f, 0.9f), Flame = new DashColor(1f, 0.9f, 0.6f),
                    };
                default:
                    var green = new DashColor(0.30f, 1f, 0.42f);
                    return new Look
                    {
                        HasPanel = true, Panel = new DashColor(0.01f, 0.03f, 0.015f), Edge = new DashColor(0.12f, 0.42f, 0.18f),
                        Face = new DashColor(0.01f, 0.05f, 0.02f), Ink = green, Dim = new DashColor(0.14f, 0.5f, 0.2f),
                        Accent = green, Warn = green, Fuel = green, Air = green, Flame = green,
                    };
            }
        }

        public static void Paint(DashStyle style, DashReadings r, DashDrawing d)
        {
            d.Clear();
            Look k = LookOf(style);
            const float W = DashDrawing.Width, H = DashDrawing.Height;

            if (style == DashStyle.FlightDeck)
            {
                FlightDeck(k, r, d);
                return;
            }
            if (k.HasPanel)
            {
                d.Panel(0f, 0f, W, H, style == DashStyle.GreenScreen ? 6f : 26f, k.Edge);
                d.Panel(5f, 5f, W - 10f, H - 10f, style == DashStyle.GreenScreen ? 4f : 22f, k.Panel);
            }
            Air(style, k, r, d);
            Fuel(style, k, r, d);
            Ball(style, k, r, d, 400f, 158f, 104f);
            Speed(style, k, r, d);
            Pusher(style, k, r, d, 722f, 150f, 62f);
            Camera(style, k, r, d);

            // The notice is always the last word, and always there (blank when there is nothing to say), so a
            // blinking warning changes one word on the real dash and not every word after it.
            bool urgent = r.FuelOut || r.OxygenLow || r.FuelLow || r.SinkTooFast || r.Notice == "NOT BUCKLED IN";
            float noticeSize = r.Notice.Length > 24 ? 16f : 20f;
            if (r.Notice.Length > 0 && style == DashStyle.GreenScreen && urgent && r.Blink)
            {
                // One colour only, so a warning is shown the old way: the block lit and the letters dark.
                d.Rect(250f, 9f, 300f, 27f, k.Ink);
                d.Text(400f, 22f, noticeSize, r.Notice, k.Panel);
            }
            else d.Text(400f, 22f, noticeSize, !urgent || r.Blink || style == DashStyle.GreenScreen ? r.Notice : "", urgent ? k.Warn : k.Accent);
        }

        // ------------------------------------------------------------------ the Flight deck screen

        /// <summary>
        /// The chosen design. Always the same seven words in the same order (blank when there is nothing to
        /// say): speed, its unit, the body it is measured against, height, climb or sink, the camera's name,
        /// and the notice. Fuel and air are pictures (a tube and a column of bubbles), not words.
        /// </summary>
        static void FlightDeck(Look k, DashReadings r, DashDrawing d)
        {
            const float W = DashDrawing.Width, H = DashDrawing.Height;
            d.Panel(0f, 0f, W, H, 20f, k.Face);
            d.Panel(4f, 4f, W - 8f, H - 8f, 17f, k.Panel);
            // Fine rules across the glass, like the graticule of an old scope.
            DashColor rule = k.Edge.Fade(0.55f);
            for (int i = 1; i < 6; i++) d.Rect(14f, H * i / 6f - 0.5f, W - 28f, 1f, rule);
            for (int i = 1; i < 20; i++) d.Rect(W * i / 20f - 0.5f, 10f, 1f, i % 5 == 0 ? 12f : 6f, rule);

            Pusher(DashStyle.FlightDeck, k, r, d, 118f, 172f, 60f);
            Ball(DashStyle.FlightDeck, k, r, d, 392f, 164f, 112f);
            // Fuel and air stand either side of the ball, so both are read looking straight ahead. The console's
            // own tube and dial say the same thing larger, but they are off to the sides and need the model.
            DeckFuel(k, r, d, 245f);
            DeckAir(k, r, d, 536f);

            // Speed: the one big number, with the body it is measured against named under a small world.
            const float sx = 668f;
            d.Panel(sx - 108f, 150f, 216f, 86f, 12f, k.Edge);
            d.Panel(sx - 105f, 153f, 210f, 80f, 10f, k.Face);
            d.Text(sx - 12f, 196f, 46f, r.SpeedText, k.Ink);
            d.Text(sx + 98f, 168f, 14f, r.SpeedUnit, k.Dim, DashAlign.Right, false);
            bool against = r.Against.Length > 0;
            if (against)
            {
                d.Disc(sx - 96f, 258f, 7f, k.Accent, 14);
                d.Arc(sx - 96f, 258f, 10f, 11.5f, 200f, 340f, k.Accent, 8);
            }
            d.Text(sx - 78f, 258f, 16f, r.Against, k.Dim, DashAlign.Left);

            // Height over a strip of ground, and an arrow that leans the way you are going: up or down.
            bool near = r.AltitudeText.Length > 0;
            if (near)
            {
                d.Rect(sx - 104f, 100f, 30f, 2.5f, k.Dim);
                for (int i = 0; i < 4; i++) d.Line(sx - 100f + i * 8f, 100f, sx - 106f + i * 8f, 92f, 1.5f, k.Dim);
                d.Line(sx - 89f, 104f, sx - 89f, 124f, 2f, k.Ink);
                d.Triangle(sx - 94f, 120f, sx - 84f, 120f, sx - 89f, 128f, k.Ink);
                float tilt = Math.Max(-1f, Math.Min(1f, r.Vertical / 30f));
                DashColor arrow = r.SinkTooFast ? k.Warn : tilt < -0.5f ? k.Accent : k.Ink;
                float tip = 68f + 14f * tilt + (tilt >= 0f ? 4f : -4f);
                d.Triangle(sx - 100f, 68f, sx - 78f, 68f, sx - 89f, tip, arrow);
            }
            d.Text(sx - 64f, 110f, 22f, r.AltitudeText, k.Ink, DashAlign.Left);
            d.Text(sx - 64f, 68f, 20f, r.VerticalText, r.SinkTooFast ? k.Warn : k.Ink, DashAlign.Left);

            // The camera: a window with a lit corner bracket at each corner, and its name.
            const float x = 822f, y = 100f, w = 160f, h = 100f;
            d.Panel(x - 5f, y - 5f, w + 10f, h + 10f, 8f, k.Edge);
            d.Rect(x, y, w, h, new DashColor(0f, 0f, 0f));
            d.CameraX = x; d.CameraY = y; d.CameraWidth = w; d.CameraHeight = h;
            for (int i = 0; i < 4; i++)
            {
                float bx = i % 2 == 0 ? x - 10f : x + w + 10f, by = i < 2 ? y - 10f : y + h + 10f;
                float dx = i % 2 == 0 ? 16f : -16f, dy = i < 2 ? 16f : -16f;
                d.Line(bx, by, bx + dx, by, 2.5f, k.Accent);
                d.Line(bx, by, bx, by + dy, 2.5f, k.Accent);
            }
            d.Text(x + w * 0.5f, 232f, 15f, r.CameraName, k.Dim);

            bool urgent = r.FuelOut || r.OxygenLow || r.FuelLow || r.SinkTooFast || r.Notice == "NOT BUCKLED IN";
            // A warning flag either end of the bottom edge: lit with the notice, dim between blinks, gone when all is well.
            // (Always drawn, so a blink changes colours and never the number of corners.)
            DashColor flag = !urgent ? k.Panel : r.Blink ? k.Warn : k.Warn.Fade(0.25f);
            d.Triangle(20f, 14f, 44f, 14f, 32f, 36f, flag);
            d.Triangle(W - 44f, 14f, W - 20f, 14f, W - 32f, 36f, flag);
            d.Text(520f, 24f, r.Notice.Length > 24 ? 17f : 21f, !urgent || r.Blink ? r.Notice : "", urgent ? k.Warn : k.Accent);
        }

        /// <summary>
        /// Fuel on the Flight deck screen: a slim glass tube with a drop over it. The long red notch is where
        /// "low" begins. Always the same number of corners, so the picture only changes when the level does.
        /// </summary>
        static void DeckFuel(Look k, DashReadings r, DashDrawing d, float cx)
        {
            const float y = 64f, w = 20f, h = 168f;
            float level = Math.Max(0f, Math.Min(1f, r.Fuel));
            bool warn = r.FuelLow || r.FuelOut;
            DashColor colour = warn ? (r.Blink ? k.Warn : DashColor.Mix(k.Warn, k.Face, 0.5f)) : k.Fuel;
            float x = cx - w * 0.5f;
            d.Panel(x - 4f, y - 4f, w + 8f, h + 8f, 13f, warn && r.Blink ? k.Warn : k.Edge);
            d.Panel(x, y, w, h, 10f, k.Face);
            float tall = (h - 6f) * level;
            d.Panel(x + 3f, y + 3f, w - 6f, tall, 7f, colour);
            d.Rect(x + 5f, y + 12f, 2.5f, h - 24f, new DashColor(1f, 1f, 1f, 0.14f));
            for (int i = 1; i < 5; i++)
            {
                bool low = i == 1;
                d.Rect(x + w + 6f, y + 3f + (h - 6f) * i / 5f - 1f, low ? 11f : 6f, 2f, low ? k.Warn : k.Dim);
            }
            // The drop.
            float top = y + h + 14f;
            d.Disc(cx, top + 6f, 6.5f, colour, 12);
            d.Triangle(cx - 5.8f, top + 9f, cx + 5.8f, top + 9f, cx, top + 21f, colour);
        }

        /// <summary>
        /// Air on the Flight deck screen: five bubbles in a column. They shrink away from the top as the air
        /// goes; the last one is ringed red, because that is the "low" fifth.
        /// </summary>
        static void DeckAir(Look k, DashReadings r, DashDrawing d, float cx)
        {
            const float bottom = 80f, step = 38f, size = 12.5f;
            float level = Math.Max(0f, Math.Min(1f, r.Oxygen));
            bool warn = r.OxygenLow;
            DashColor colour = warn ? (r.Blink ? k.Warn : DashColor.Mix(k.Warn, k.Face, 0.5f)) : k.Air;
            for (int i = 0; i < 5; i++)
            {
                float cy = bottom + step * i;
                float fill = Math.Max(0f, Math.Min(1f, level * 5f - i));
                // The low bubble's ring is a quiet red until the air really is low: a mark, not an alarm.
                DashColor ring = i > 0 ? k.Edge : warn ? (r.Blink ? k.Warn : DashColor.Mix(k.Warn, k.Face, 0.5f)) : DashColor.Mix(k.Warn, k.Edge, 0.55f);
                d.Ring(cx, cy, size + 1.5f, size + 4f, ring, 20);
                d.Disc(cx, cy, size + 1.5f, k.Face, 20);
                // The area of the bubble follows the air in it, so half a bubble looks like half.
                float radius = size * (float)Math.Sqrt(fill);
                d.Disc(cx, cy, radius, colour, 16);
                // A glint, as long as there is bubble enough to carry one.
                d.Disc(cx - radius * 0.35f, cy + radius * 0.35f, radius * 0.22f, new DashColor(1f, 1f, 1f, 0.3f), 8);
            }
        }

        // ------------------------------------------------------------------ air: always something round

        static void Air(DashStyle style, Look k, DashReadings r, DashDrawing d)
        {
            const float cx = 78f, cy = 150f;
            float level = r.Oxygen;
            DashColor colour = r.OxygenLow && r.Blink ? k.Warn : k.Air;
            switch (style)
            {
                case DashStyle.FlightDeck:
                    // A round dial: needle sweeps from empty (lower left) round to full (lower right).
                    d.Disc(cx, cy, 58f, k.Edge);
                    d.Disc(cx, cy, 53f, k.Face);
                    d.Arc(cx, cy, 40f, 47f, 225f, 225f - 270f * 0.2f, k.Warn, 6);
                    d.Arc(cx, cy, 40f, 47f, 225f - 270f * 0.2f, -45f, k.Air, 20);
                    for (int i = 0; i <= 10; i++)
                    {
                        double a = (225f - 27f * i) * Math.PI / 180.0;
                        float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                        d.Line(cx + c * (i % 5 == 0 ? 30f : 35f), cy + s * (i % 5 == 0 ? 30f : 35f), cx + c * 39f, cy + s * 39f, 2f, k.Ink);
                    }
                    d.Needle(cx, cy, 44f, 225f - 270f * level, 7f, k.Ink);
                    d.Disc(cx, cy, 6f, k.Accent, 12);
                    break;
                case DashStyle.Arcade:
                    // Five bubbles in a tank; they pop from the top as the air goes.
                    d.Panel(cx - 34f, cy - 80f, 68f, 160f, 30f, k.Face);
                    for (int i = 0; i < 5; i++)
                    {
                        float fill = Math.Max(0f, Math.Min(1f, level * 5f - i));
                        float by = cy - 60f + i * 30f;
                        d.Ring(cx, by, 10f, 13f, k.Dim, 16);
                        if (fill > 0.02f) d.Disc(cx, by, 4f + 8f * fill, colour, 16);
                    }
                    d.Text(cx, cy - 100f, 15f, r.OxygenLabel, k.Ink);
                    break;
                case DashStyle.Glass:
                    // A thin ring that empties anticlockwise, with the number inside.
                    d.Ring(cx, cy, 49f, 52f, k.Dim);
                    d.Arc(cx, cy, 44f, 52f, 90f, 90f - 360f * level, colour, 40);
                    d.Text(cx, cy - 2f, 26f, r.OxygenText, k.Ink);
                    d.Text(cx, cy - 22f, 12f, "%", k.Dim, DashAlign.Centre, false);
                    break;
                default:
                    // Twelve marks round a circle, like a clock running down.
                    for (int i = 0; i < 12; i++)
                    {
                        double a = (90f - 30f * i) * Math.PI / 180.0;
                        bool lit = i < (int)Math.Ceiling(level * 12f - 0.001f);
                        float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                        d.Line(cx + c * 34f, cy + s * 34f, cx + c * 52f, cy + s * 52f, lit ? 7f : 2f, lit ? colour : k.Dim);
                    }
                    d.Text(cx, cy - 2f, 22f, r.OxygenText, k.Ink);
                    break;
            }
            DashColor airLabel = r.OxygenLow ? k.Warn : style == DashStyle.FlightDeck ? k.Face : k.Accent;
            d.Text(cx, 276f, 15f, r.OxygenLow && !r.Blink ? "" : "AIR", airLabel);
            if (style == DashStyle.FlightDeck) d.Text(cx, 72f, 15f, r.OxygenLabel, k.Face);
        }

        // ------------------------------------------------------------------ fuel: always a bar

        static void Fuel(DashStyle style, Look k, DashReadings r, DashDrawing d)
        {
            const float x = 172f, y = 64f, w = 46f, h = 190f;
            float level = r.Fuel;
            DashColor colour = r.FuelLow ? (r.Blink ? k.Warn : DashColor.Mix(k.Warn, k.Face, 0.5f)) : k.Fuel;
            switch (style)
            {
                case DashStyle.FlightDeck:
                    // A glass tube of orange fuel with marks up the side.
                    d.Panel(x - 4f, y - 4f, w + 8f, h + 8f, 14f, k.Edge);
                    d.Panel(x, y, w, h, 11f, k.Face);
                    if (level > 0.005f) d.Panel(x + 5f, y + 5f, w - 10f, Math.Max(10f, (h - 10f) * level), 7f, colour);
                    d.Rect(x + 9f, y + 12f, 5f, h - 24f, new DashColor(1f, 1f, 1f, 0.16f));
                    for (int i = 1; i < 4; i++) d.Rect(x + w + 6f, y + h * i / 4f - 1f, i == 2 ? 12f : 7f, 2f, k.Face);
                    d.Text(x + w + 22f, y + h - 8f, 13f, "F", k.Face, DashAlign.Left);
                    d.Text(x + w + 22f, y + 8f, 13f, "E", k.Warn, DashAlign.Left);
                    break;
                case DashStyle.Arcade:
                    // Ten chunky blocks, green at the top down to red at the bottom.
                    for (int i = 0; i < 10; i++)
                    {
                        bool lit = level * 10f > i + 0.001f;
                        // The whole stack takes the colour of how much is left, so a full tank is all green.
                        DashColor block = level <= 0.2f ? k.Warn : level <= 0.4f ? k.Ink : k.Fuel;
                        if (r.FuelLow && !r.Blink) block = k.Dim;
                        d.Panel(x, y + i * 19f, w + 10f, 15f, 4f, lit ? block : k.Face);
                    }
                    break;
                case DashStyle.Glass:
                    // A thin outline that fills from the bottom.
                    d.Rect(x + 11f, y, 3f, h, k.Dim);
                    d.Rect(x + 32f, y, 3f, h, k.Dim);
                    d.Rect(x + 11f, y, 24f, 3f, k.Dim);
                    d.Rect(x + 11f, y + h - 3f, 24f, 3f, k.Dim);
                    d.Rect(x + 17f, y + 5f, 12f, (h - 10f) * level, colour);
                    for (int i = 1; i < 4; i++) d.Rect(x + 36f, y + h * i / 4f - 1f, 8f, 2f, k.Dim);
                    break;
                default:
                    // FUEL [##########] turned on its end.
                    for (int i = 0; i < 10; i++)
                    {
                        bool lit = level * 10f > i + 0.001f;
                        if (lit) d.Rect(x + 6f, y + i * 19f, w - 6f, 13f, colour);
                        else d.Rect(x + 6f + (w - 6f) * 0.5f - 3f, y + i * 19f + 5f, 6f, 3f, k.Dim);
                    }
                    d.Rect(x, y - 6f, 3f, h + 6f, k.Dim);
                    d.Rect(x + w + 3f, y - 6f, 3f, h + 6f, k.Dim);
                    break;
            }
            float mid = x + w * 0.5f + (style == DashStyle.Arcade ? 5f : 0f);
            d.Text(mid, 276f, 15f, "FUEL", r.FuelLow ? k.Warn : style == DashStyle.FlightDeck ? k.Face : k.Accent);
            d.Text(mid, 42f, 15f, r.FuelLabel, style == DashStyle.FlightDeck ? k.Face : k.Ink);
        }

        // ------------------------------------------------------------------ the direction ball

        /// <summary>
        /// The middle of the ball is your nose. The dot is where you are really going: dead centre means
        /// straight ahead, at the rim means sideways. A hollow dot means that motion is behind you.
        /// </summary>
        static void Ball(DashStyle style, Look k, DashReadings r, DashDrawing d, float cx, float cy, float radius)
        {
            if (style == DashStyle.FlightDeck) d.Disc(cx, cy, radius + 7f, k.Edge, 40);
            if (style == DashStyle.Arcade) d.Disc(cx, cy, radius + 5f, k.Accent, 40);
            if (style == DashStyle.Glass || style == DashStyle.GreenScreen) d.Ring(cx, cy, radius - 3f, radius, k.Ink, 48);
            if (style != DashStyle.GreenScreen) d.Disc(cx, cy, radius - (style == DashStyle.Glass ? 2f : 0f), k.Face, 40);

            d.Ring(cx, cy, radius * 0.5f - 1f, radius * 0.5f, k.Dim, 36);
            for (int i = 0; i < 4; i++)
            {
                double a = i * Math.PI * 0.5;
                float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                d.Line(cx + c * radius * 0.82f, cy + s * radius * 0.82f, cx + c * (radius - 3f), cy + s * (radius - 3f), 3f, k.Ink);
            }
            // The nose: a small open cross.
            d.Line(cx - 14f, cy, cx - 5f, cy, 2.5f, k.Ink);
            d.Line(cx + 5f, cy, cx + 14f, cy, 2.5f, k.Ink);
            d.Line(cx, cy + 5f, cx, cy + 14f, 2.5f, k.Ink);

            if (r.Moving)
            {
                // Straight on to the ball: left and right, up and down, as the pilot sees them.
                float reach = radius - 14f;
                float px = cx + r.DriftX * reach, py = cy + r.DriftY * reach;
                d.Line(cx, cy, px, py, 2f, k.Accent.Fade(0.45f));
                if (r.DriftZ >= 0f) d.Disc(px, py, 10f, k.Accent, 18);
                else
                {
                    d.Ring(px, py, 7f, 11f, k.Accent, 18);
                    if (style != DashStyle.FlightDeck) d.Text(cx, cy + radius * 0.62f, 13f, "GOING BACKWARD", k.Accent);
                }
            }
            else if (style != DashStyle.FlightDeck) d.Text(cx, cy + radius * 0.62f, 13f, "HOLDING STILL", k.Dim);
        }

        static void Speed(DashStyle style, Look k, DashReadings r, DashDrawing d)
        {
            const float x = 520f;
            if (style == DashStyle.GreenScreen)
            {
                d.Text(x, 250f, 17f, r.SpeedText, k.Ink, DashAlign.Left);
                d.Text(x + 62f, 250f, 14f, r.SpeedUnit, k.Ink, DashAlign.Left);
                d.Text(x, 228f, 13f, r.SpeedLabel, k.Dim, DashAlign.Left);
                if (r.AltitudeText.Length > 0)
                {
                    d.Text(x, 196f, 13f, "ALT", k.Dim, DashAlign.Left);
                    d.Text(x + 40f, 196f, 17f, r.AltitudeText, k.Ink, DashAlign.Left);
                    d.Text(x, 168f, 13f, "V/S", k.Dim, DashAlign.Left);
                    d.Text(x + 40f, 168f, 17f, r.VerticalText, k.Ink, DashAlign.Left);
                }
                return;
            }
            if (style == DashStyle.FlightDeck)
            {
                d.Panel(x - 4f, 196f, 118f, 62f, 8f, k.Edge);
                d.Panel(x, 200f, 110f, 54f, 6f, k.Face);
            }
            if (style == DashStyle.Arcade) d.Panel(x - 2f, 198f, 114f, 58f, 10f, k.Face);
            d.Text(x + 55f, 230f, style == DashStyle.Arcade ? 34f : 30f, r.SpeedText, k.Ink);
            d.Text(x + 55f, 207f, 12f, r.SpeedUnit, style == DashStyle.FlightDeck ? k.Dim : k.Accent, DashAlign.Centre, false);
            DashColor label = style == DashStyle.FlightDeck ? k.Face : k.Accent;
            d.Text(x + 55f, 276f, 13f, r.SpeedLabel, label);
            if (r.AltitudeText.Length > 0)
            {
                DashColor value = style == DashStyle.FlightDeck ? k.Face : k.Ink;
                d.Text(x, 168f, 12f, "HEIGHT", label, DashAlign.Left);
                d.Text(x, 148f, 18f, r.AltitudeText, value, DashAlign.Left);
                d.Text(x, 120f, 12f, r.Vertical >= 0f ? "CLIMB" : "SINK", label, DashAlign.Left);
                d.Text(x, 100f, 18f, r.VerticalText, value, DashAlign.Left);
                // A little arrow, so climbing or sinking is read without reading.
                float tilt = Math.Max(-1f, Math.Min(1f, r.Vertical / 30f));
                d.Triangle(x + 84f, 104f, x + 106f, 104f, x + 95f, 104f + 18f * tilt + (tilt >= 0f ? 4f : -4f), tilt < -0.5f ? k.Warn : label);
            }
        }

        // ------------------------------------------------------------------ the little ship that shows your push

        static void Project(float cx, float cy, float s, float x, float y, float z, out float px, out float py)
        {
            // Seen from behind, above and a little to one side: the nose points away from you, up the screen.
            px = cx + (x * 0.95f + z * 0.42f) * s;
            py = cy + (y * 0.9f + z * 0.36f - x * 0.08f) * s;
        }

        static void Edge3(DashDrawing d, float cx, float cy, float s, float ax, float ay, float az, float bx, float by, float bz, float width, DashColor c)
        {
            Project(cx, cy, s, ax, ay, az, out float x0, out float y0);
            Project(cx, cy, s, bx, by, bz, out float x1, out float y1);
            d.Line(x0, y0, x1, y1, width, c);
        }

        /// <summary>A jet of flame leaving the little ship from a point, along a direction, as long as the push is hard.</summary>
        static void Jet(DashDrawing d, float cx, float cy, float s, float ox, float oy, float oz, float dx, float dy, float dz, float amount, DashColor c)
        {
            if (amount < 0.04f) return;
            Project(cx, cy, s, ox, oy, oz, out float x0, out float y0);
            Project(cx, cy, s, ox + dx * amount, oy + dy * amount, oz + dz * amount, out float x1, out float y1);
            float lx = x1 - x0, ly = y1 - y0;
            float length = (float)Math.Sqrt(lx * lx + ly * ly);
            if (length < 0.5f) return;
            float nx = -ly / length * s * 0.16f, ny = lx / length * s * 0.16f;
            d.Triangle(x0 - nx, y0 - ny, x1, y1, x0 + nx, y0 + ny, c);
            d.Triangle(x0 - nx * 0.5f, y0 - ny * 0.5f, x0 + lx * 0.55f, y0 + ly * 0.55f, x0 + nx * 0.5f, y0 + ny * 0.5f, new DashColor(1f, 1f, 1f, 0.75f * c.A));
        }

        static void Pusher(DashStyle style, Look k, DashReadings r, DashDrawing d, float cx, float cy, float s)
        {
            if (style == DashStyle.FlightDeck) { d.Disc(cx, cy - 12f, 100f, k.Edge, 40); d.Disc(cx, cy - 12f, 95f, k.Face, 40); }
            if (style == DashStyle.Arcade) d.Panel(cx - 82f, cy - 82f, 164f, 164f, 18f, k.Face);
            if (style == DashStyle.Glass) d.Ring(cx, cy, 81f, 84f, k.Dim, 40);
            cy -= 6f;

            float boost = r.Boost ? 1.45f : 1f;
            // Flames leave the side opposite to the way you are pushed. Drawn first where they are behind the hull.
            DashColor flame = k.Flame;
            if (r.ThrustZ > 0f) Jet(d, cx, cy, s, 0f, 0.25f, -0.5f, 0f, 0f, -1f, r.ThrustZ * 0.9f * boost, flame);
            if (r.ThrustY < 0f) Jet(d, cx, cy, s, 0f, 0.92f, 0f, 0f, 1f, 0f, -r.ThrustY * 0.8f * boost, flame);

            DashColor hull = k.Ink;
            const int sides = 8;
            float w = style == DashStyle.GreenScreen ? 2f : 2.6f;
            for (int i = 0; i < sides; i++)
            {
                double a = (i + 0.5) * Math.PI * 2.0 / sides, b = (i + 1.5) * Math.PI * 2.0 / sides;
                float ax = (float)Math.Cos(a), az = (float)Math.Sin(a), bx = (float)Math.Cos(b), bz = (float)Math.Sin(b);
                Edge3(d, cx, cy, s, ax * 0.5f, 0f, az * 0.5f, bx * 0.5f, 0f, bz * 0.5f, w, hull);
                Edge3(d, cx, cy, s, ax * 0.5f, 0.5f, az * 0.5f, bx * 0.5f, 0.5f, bz * 0.5f, w, hull);
                Edge3(d, cx, cy, s, ax * 0.5f, 0f, az * 0.5f, ax * 0.5f, 0.5f, az * 0.5f, w, hull);
                Edge3(d, cx, cy, s, ax * 0.3f, 0.92f, az * 0.3f, bx * 0.3f, 0.92f, bz * 0.3f, w, hull);
                Edge3(d, cx, cy, s, ax * 0.5f, 0.5f, az * 0.5f, ax * 0.3f, 0.92f, az * 0.3f, w, hull);
            }
            for (int i = 0; i < 4; i++)
            {
                double a = (i + 0.5) * Math.PI * 0.5;
                float lx = (float)Math.Cos(a), lz = (float)Math.Sin(a);
                Edge3(d, cx, cy, s, lx * 0.5f, 0.1f, lz * 0.5f, lx * 1.12f, -0.72f, lz * 1.12f, w, hull);
                Edge3(d, cx, cy, s, lx * 0.99f, -0.72f, lz * 0.99f, lx * 1.25f, -0.72f, lz * 1.25f, w + 1.5f, hull);
            }
            // The nose: a window, so you can tell front from back.
            Edge3(d, cx, cy, s, -0.16f, 0.66f, 0.42f, 0.16f, 0.66f, 0.42f, w + 2.5f, k.Accent);

            if (r.ThrustY > 0f) Jet(d, cx, cy, s, 0f, -0.05f, 0f, 0f, -1f, 0f, r.ThrustY * 0.95f * boost, flame);
            if (r.ThrustX > 0f) Jet(d, cx, cy, s, -0.5f, 0.25f, 0f, -1f, 0f, 0f, r.ThrustX * 0.8f * boost, flame);
            if (r.ThrustX < 0f) Jet(d, cx, cy, s, 0.5f, 0.25f, 0f, 1f, 0f, 0f, -r.ThrustX * 0.8f * boost, flame);
            if (r.ThrustZ < 0f) Jet(d, cx, cy, s, 0f, 0.25f, 0.5f, 0f, 0f, 1f, -r.ThrustZ * 0.9f * boost, flame);

            // The Flight deck screen says neither in words: the flames show the push, and a boost makes them longer.
            if (style == DashStyle.FlightDeck) return;
            d.Text(cx, 276f, 15f, style == DashStyle.GreenScreen ? "THRUST" : "PUSH", k.Accent);
            d.Text(cx, 40f, 15f, r.Boost && r.Blink ? "BOOST" : "", k.Warn);
        }

        static void Camera(DashStyle style, Look k, DashReadings r, DashDrawing d)
        {
            const float x = 822f, y = 96f, w = 160f, h = 100f;
            d.Panel(x - 5f, y - 5f, w + 10f, h + 10f, style == DashStyle.GreenScreen ? 2f : 10f, k.Edge);
            d.Rect(x, y, w, h, new DashColor(0f, 0f, 0f, style == DashStyle.Glass ? 0.5f : 1f));
            d.CameraX = x; d.CameraY = y; d.CameraWidth = w; d.CameraHeight = h;
            DashColor label = style == DashStyle.FlightDeck ? k.Face : k.Accent;
            // One caption above (which camera) and one below (the button that puts it on the whole view).
            d.Text(x + w * 0.5f, 222f, 14f, r.CameraName, style == DashStyle.FlightDeck ? k.Face : k.Ink);
            d.Text(x + w * 0.5f, 68f, 13f, r.ScreenHint, label);
        }
    }
}
