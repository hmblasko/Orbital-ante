using System;

namespace OrbitalAnte.Cockpit
{
    /// <summary>The dash designs on offer. Matthew picks; the choice is on the CockpitConfig asset and can be flipped in play.</summary>
    public enum DashStyle
    {
        /// <summary>Cream enamel panel, round dials with needles, a glass fuel tube. Old spacecraft, warm.</summary>
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
    /// Every design shows the same five things in the same places, so switching design never moves where you
    /// look: air and fuel on the left, the direction ball and speed in the middle, the little ship showing your
    /// push on the right, and the camera window at the far right. What changes is the shape of each gauge.
    /// Fuel is always a bar of some kind and air is always something round, so the two are never mistaken.
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
                        HasPanel = true, Panel = new DashColor(0.89f, 0.85f, 0.74f), Edge = new DashColor(0.55f, 0.50f, 0.42f),
                        Face = new DashColor(0.13f, 0.13f, 0.14f), Ink = new DashColor(0.96f, 0.93f, 0.84f), Dim = new DashColor(0.55f, 0.53f, 0.48f),
                        Accent = new DashColor(0.95f, 0.52f, 0.14f), Warn = new DashColor(0.90f, 0.22f, 0.16f),
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

            if (k.HasPanel)
            {
                d.Panel(0f, 0f, W, H, style == DashStyle.GreenScreen ? 6f : 26f, k.Edge);
                d.Panel(5f, 5f, W - 10f, H - 10f, style == DashStyle.GreenScreen ? 4f : 22f, k.Panel);
            }
            if (style == DashStyle.FlightDeck)
            {
                // Four screws, because a real panel is held on by something.
                for (int i = 0; i < 4; i++)
                {
                    float sx = i % 2 == 0 ? 20f : W - 20f, sy = i < 2 ? 20f : H - 20f;
                    d.Disc(sx, sy, 7f, k.Edge, 12);
                    d.Line(sx - 4f, sy - 4f, sx + 4f, sy + 4f, 2f, k.Panel);
                }
                // A little brass plate. You fly for nobody.
                d.Text(575f, 56f, 12f, "PROPERTY OF NOBODY", k.Edge, DashAlign.Centre, false);
            }

            Air(style, k, r, d);
            Fuel(style, k, r, d);
            Ball(style, k, r, d, 400f, 158f, 104f);
            Speed(style, k, r, d);
            Pusher(style, k, r, d, 722f, 150f, 62f);
            Camera(style, k, r, d);

            // The notice is always the last word, and always there (blank when there is nothing to say), so a
            // blinking warning changes one word on the real dash and not every word after it.
            bool urgent = r.FuelOut || r.OxygenLow || r.FuelLow;
            float noticeSize = r.Notice.Length > 24 ? 16f : 20f;
            if (r.Notice.Length > 0 && style == DashStyle.GreenScreen && urgent && r.Blink)
            {
                // One colour only, so a warning is shown the old way: the block lit and the letters dark.
                d.Rect(250f, 9f, 300f, 27f, k.Ink);
                d.Text(400f, 22f, noticeSize, r.Notice, k.Panel);
            }
            else d.Text(400f, 22f, noticeSize, !urgent || r.Blink || style == DashStyle.GreenScreen ? r.Notice : "", urgent ? k.Warn : k.Accent);
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
                    d.Text(cx, cy + radius * 0.62f, 13f, "GOING BACKWARD", k.Accent);
                }
            }
            else d.Text(cx, cy + radius * 0.62f, 13f, "HOLDING STILL", style == DashStyle.FlightDeck ? k.Ink.Fade(0.6f) : k.Dim);
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
            if (style == DashStyle.FlightDeck) { d.Disc(cx, cy, 86f, k.Edge, 36); d.Disc(cx, cy, 80f, k.Face, 36); }
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
                Edge3(d, cx, cy, s, lx * 0.5f, 0.1f, lz * 0.5f, lx * 0.95f, -0.42f, lz * 0.95f, w, hull);
                Edge3(d, cx, cy, s, lx * 0.82f, -0.42f, lz * 0.82f, lx * 1.08f, -0.42f, lz * 1.08f, w + 1.5f, hull);
            }
            // The nose: a window, so you can tell front from back.
            Edge3(d, cx, cy, s, -0.16f, 0.66f, 0.42f, 0.16f, 0.66f, 0.42f, w + 2.5f, k.Accent);

            if (r.ThrustY > 0f) Jet(d, cx, cy, s, 0f, -0.05f, 0f, 0f, -1f, 0f, r.ThrustY * 0.95f * boost, flame);
            if (r.ThrustX > 0f) Jet(d, cx, cy, s, -0.5f, 0.25f, 0f, -1f, 0f, 0f, r.ThrustX * 0.8f * boost, flame);
            if (r.ThrustX < 0f) Jet(d, cx, cy, s, 0.5f, 0.25f, 0f, 1f, 0f, 0f, -r.ThrustX * 0.8f * boost, flame);
            if (r.ThrustZ < 0f) Jet(d, cx, cy, s, 0f, 0.25f, 0.5f, 0f, 0f, 1f, -r.ThrustZ * 0.9f * boost, flame);

            DashColor label = style == DashStyle.FlightDeck ? k.Face : k.Accent;
            d.Text(cx, 276f, 15f, style == DashStyle.GreenScreen ? "THRUST" : "PUSH", label);
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
