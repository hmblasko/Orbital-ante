using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// One of the two gauges marked on the lower visor (option A, "Visor arcs", of the 15 Nov 2026 mockups):
    /// an arc that curls round the outside of a big number, with a small name above and a line of words under.
    ///
    /// The left one is a smooth arc that fills from the bottom up, with tick marks every tenth (air). The right
    /// one is cut into <see cref="VisorReadout.Plates"/> plates that go dark one by one (the suit's armour).
    /// Numbers and words are written only when they change; colours are set on the renderer, which does not
    /// make the canvas lay itself out again.
    /// </summary>
    public sealed class VisorGauge
    {
        /// <summary>Inner edge of the arc, in the 1280 x 800 reference canvas.</summary>
        public const float Radius = 74f;
        /// <summary>How far round the arc reaches, degrees.</summary>
        public const float Span = 150f;
        /// <summary>Outer edge of the arc. The ring sprite's band is an eighth of that (about 10 px here).</summary>
        const float Outer = Radius + 10f;
        const float PlateGap = 2.2f;

        public static readonly Color Air = new Color(0.59f, 0.84f, 0.96f);
        public static readonly Color Warn = new Color(0.94f, 0.59f, 0.20f);
        static readonly Color Empty = new Color(0f, 0f, 0f, 0.5f);

        public RectTransform Root { get; private set; }
        readonly bool _left;
        readonly Color _tint;
        Image _back, _fill;
        Image[] _plates;
        Text _name, _number, _unit, _line;
        int _shownNumber = -1;
        string _shownLine;

        VisorGauge(bool left, Color tint)
        {
            _left = left;
            _tint = tint;
        }

        /// <summary>
        /// Builds a gauge in a lower corner of <paramref name="parent"/>. <paramref name="left"/>: lower left with a
        /// smooth arc; otherwise lower right with plates. <paramref name="inset"/> is the distance of the arc's
        /// middle from that side of the screen.
        /// </summary>
        public static VisorGauge Build(Transform parent, bool left, string name, Color tint, float inset = 300f, float lift = 110f)
        {
            var g = new VisorGauge(left, tint);
            g.Root = UiKit.NewRect(left ? "AirGauge" : "SuitGauge", parent);
            UiKit.Pin(g.Root, new Vector2(left ? 0f : 1f, 0f), Vector2.zero, new Vector2(2f * (Radius + 20f), 2f * (Radius + 20f)));
            // Placed by its middle (the arc's centre), not by a corner.
            g.Root.pivot = new Vector2(0.5f, 0.5f);
            g.Root.anchoredPosition = new Vector2(left ? inset : -inset, lift);

            // An arc starts on the screen's lower edge side and runs up round the outside: clockwise on the left
            // (from about seven o'clock, through nine, to eleven), anticlockwise on the right.
            float start = left ? 255f : 285f;
            if (left)
            {
                g._back = Arc(g.Root, "Back", start, Span, true, Empty);
                g._fill = Arc(g.Root, "Fill", start, Span, true, Color.white);
                for (int i = 0; i <= 10; i++) Tick(g.Root, start - Span * i / 10f, i % 5 == 0);
            }
            else
            {
                g._plates = new Image[VisorReadout.Plates];
                float step = Span / VisorReadout.Plates;
                for (int i = 0; i < g._plates.Length; i++)
                    g._plates[i] = Arc(g.Root, "Plate" + i, start + step * i, step - PlateGap, false, Color.white);
            }

            // Words: on the inside of the arc, so they lean toward the middle of the screen.
            TextAnchor across = left ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
            float x = left ? -38f : 38f;
            Vector2 pivot = new Vector2(left ? 0f : 1f, 1f);
            g._name = Place(UiKit.Label(g.Root, name, 15, tint, across), x, 52f, 140f, 20f, pivot);
            // White underneath: their colours are set on the renderer, which multiplies with this.
            g._number = Place(UiKit.Label(g.Root, string.Empty, 40, Color.white, across), x, 34f, 140f, 48f, pivot);
            g._line = Place(UiKit.Label(g.Root, string.Empty, 17, Color.white, across, FontStyle.Normal), x, -18f, 190f, 22f, pivot);
            if (left)
            {
                // The per cent sign sits after the number, smaller; moved along when the number's width changes.
                g._unit = Place(UiKit.Label(g.Root, "%", 20, UiKit.Muted, TextAnchor.UpperLeft), x, 22f, 30f, 26f, pivot);
            }
            UiKit.Halo(g._name);
            UiKit.Halo(g._number, true);
            UiKit.Halo(g._line);
            if (g._unit != null) UiKit.Halo(g._unit);
            return g;
        }

        /// <summary>Sets a colour on the renderer only when it differs, so an unchanged gauge costs the canvas nothing.</summary>
        static void Tint(Graphic g, Color c)
        {
            if (g.canvasRenderer.GetColor() != c) g.canvasRenderer.SetColor(c);
        }

        static Text Place(Text t, float x, float y, float w, float h, Vector2 pivot)
        {
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>A piece of ring from <paramref name="from"/> degrees (anticlockwise from the right, as Unity counts) round by <paramref name="sweep"/>.</summary>
        static Image Arc(RectTransform parent, string name, float from, float sweep, bool clockwise, Color colour)
        {
            RectTransform rt = UiKit.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(2f * Outer, 2f * Outer);
            // A filled image starts at its "right" and turns; turning the whole rect puts that start where wanted.
            rt.localRotation = Quaternion.Euler(0f, 0f, from);
            Image img = UiKit.AddImage(rt, colour, UiKit.Ring);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Right;
            img.fillClockwise = clockwise;
            img.fillAmount = sweep / 360f;
            return img;
        }

        static void Tick(RectTransform parent, float angle, bool major)
        {
            RectTransform rt = UiKit.NewRect("Tick", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            float a = angle * Mathf.Deg2Rad;
            float from = Outer + 3f;
            rt.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * from;
            rt.sizeDelta = new Vector2(major ? 8f : 5f, 2f);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            UiKit.AddImage(rt, new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.8f));
        }

        public void SetActive(bool on)
        {
            if (Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on);
        }

        public bool Active => Root.gameObject.activeSelf;

        /// <summary>
        /// Shows a share (0 to 1). <paramref name="lit"/> false makes the coloured parts dim for a blink without
        /// touching the layout.
        /// </summary>
        public void Show(float fraction, string line, GaugeTone tone, bool lit = true)
        {
            fraction = Mathf.Clamp01(float.IsNaN(fraction) ? 0f : fraction);
            Color main = tone == GaugeTone.Alarm ? UiKit.Danger : _tint;
            if (!lit) main = Color.Lerp(main, Color.black, 0.55f);

            if (_fill != null)
            {
                // Steps of half a per cent: finer changes are not worth rebuilding the arc for.
                // Rounded up, like the number, so a last scrap still shows on the arc.
                float amount = Mathf.Ceil(fraction * 200f - 1e-3f) / 200f * (Span / 360f);
                if (!Mathf.Approximately(_fill.fillAmount, amount)) _fill.fillAmount = amount;
                Tint(_fill, tone == GaugeTone.Warn ? Warn : main);
            }
            if (_plates != null)
            {
                int left = VisorReadout.PlatesLeft(fraction);
                bool worn = VisorReadout.LastPlateWorn(fraction);
                for (int i = 0; i < _plates.Length; i++)
                {
                    Color c = i >= left ? Empty
                        : tone == GaugeTone.Alarm ? main
                        : i == left - 1 && worn ? Warn
                        : UiKit.Cream;
                    if (!lit && i < left) c = Color.Lerp(c, Color.black, 0.55f);
                    Tint(_plates[i], c);
                }
            }

            int number = VisorReadout.Percent(fraction);
            if (number != _shownNumber)
            {
                _shownNumber = number;
                _number.text = number.ToString();
                if (_unit != null)
                {
                    var unit = (RectTransform)_unit.transform;
                    var at = ((RectTransform)_number.transform).anchoredPosition;
                    unit.anchoredPosition = new Vector2(at.x + _number.preferredWidth + 3f, at.y - 14f);
                }
            }
            Tint(_number, tone == GaugeTone.Alarm && lit ? UiKit.Danger : UiKit.Cream);
            if (!ReferenceEquals(line, _shownLine) && line != _shownLine)
            {
                _shownLine = line;
                _line.text = line;
            }
            Tint(_line, tone == GaugeTone.Alarm ? Color.Lerp(UiKit.Danger, Color.white, 0.3f) : UiKit.Muted);
        }
    }
}
