using System;

namespace OrbitalAnte.Ship
{
    /// <summary>How a ship paint is laid on the cabin's hull panels.</summary>
    public enum HullPaintStyle
    {
        /// <summary>The paint on every light panel, a darker shade of it on the grey ones, the accent where the gold panels were.</summary>
        Plain = 0,
        /// <summary>As Plain, with the band of panels round the foot of the hull in the accent.</summary>
        Band = 1,
        /// <summary>The roof in the paint and the walls below it in the accent.</summary>
        TwoTone = 2,
    }

    /// <summary>
    /// A ship paint from the Locker (Ralph loop pass 4, 28 Nov 2026): the colours the lander's cabin hull is built
    /// with in place of its own white, grey and gold panels. The dark panels, the skirt into the base and the bands
    /// round the windows stay dark, so the windows and the shape still read. Only the outside hull is painted; the
    /// cabin inside (whose light is baked) and the Blender-made fittings (base, legs, engines, thrusters, dish) are
    /// not. Plain C#.
    /// </summary>
    public sealed class HullPaint
    {
        public readonly P3 Main;
        public readonly P3 Accent;
        public readonly HullPaintStyle Style;

        public HullPaint(P3 main, P3 accent, HullPaintStyle style)
        {
            Main = Clamp(main);
            Accent = Clamp(accent);
            Style = style;
        }

        /// <summary>A darker (below 1) or lighter shade of a colour, kept inside 0 to 1.</summary>
        public static P3 Shade(P3 c, float by) => Clamp(c * by);

        static P3 Clamp(P3 c) => new P3(Math.Max(0f, Math.Min(1f, c.X)), Math.Max(0f, Math.Min(1f, c.Y)), Math.Max(0f, Math.Min(1f, c.Z)));

        /// <summary>
        /// The colour of one kind of hull panel under this paint. <paramref name="roofOdd"/> picks the roof's second
        /// shade, so the dome keeps its panelled look. Dark panels are never asked for: they stay dark.
        /// </summary>
        public P3 Panel(HullPanelKind kind, bool roofOdd)
        {
            switch (kind)
            {
                case HullPanelKind.Band:
                    return Style == HullPaintStyle.Band ? Accent : Shade(Main, 0.7f);
                case HullPanelKind.Roof:
                    return roofOdd ? Shade(Main, 0.94f) : Main;
                case HullPanelKind.WallLight:
                    return Style == HullPaintStyle.TwoTone ? Accent : Main;
                case HullPanelKind.WallGrey:
                    return Style == HullPaintStyle.TwoTone ? Shade(Accent, 0.86f) : Shade(Main, 0.84f);
                case HullPanelKind.WallGold:
                    return Style == HullPaintStyle.TwoTone ? Main : Accent;
                case HullPanelKind.Cap:
                    return Main;
                default:
                    return Main;
            }
        }
    }

    /// <summary>The kinds of panel the cabin's hull is made of, for <see cref="HullPaint.Panel"/>. Dark panels are never painted.</summary>
    public enum HullPanelKind
    {
        /// <summary>The band of grey panels round the foot of the hull.</summary>
        Band = 0,
        /// <summary>The panels of the dome above the walls.</summary>
        Roof = 1,
        WallLight = 2,
        WallGrey = 3,
        WallGold = 4,
        WallDark = 5,
        /// <summary>The round cap on top.</summary>
        Cap = 6,
    }
}
