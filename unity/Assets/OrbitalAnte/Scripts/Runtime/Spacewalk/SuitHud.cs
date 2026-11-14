using System.Text;
using OrbitalAnte.Ship;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Spacewalk
{
    /// <summary>
    /// The suit's readout, shown only outside the ship (and for a moment aboard, while the tanks fill).
    ///
    /// Bottom left, small: air as a ring, jet fuel as an upright bar (the same shapes as the ship's dash, so
    /// each is known without reading), your speed and what it is measured against, and the tether. One notice
    /// at a time above them. The rim of the visor glows on the side a jet is burning.
    /// </summary>
    public sealed class SuitHud : MonoBehaviour
    {
        const float TextInterval = 0.12f;

        Astronaut _astronaut;
        SuitConfig _config;
        Canvas _canvas;
        RectTransform _panel;
        Image _airRing, _fuelFill;
        Text _airText, _speed, _tether, _notice;
        readonly Image[] _rim = new Image[4];
        Texture2D _rimTexture;
        Sprite _rimSprite;
        float _nextText, _shownUntil;
        int _shownAir = -1, _shownSpeed = -1, _shownLine = -1;
        string _shownAgainst = string.Empty, _shownNotice = string.Empty;
        readonly StringBuilder _text = new StringBuilder(48);

        public bool Shown => _panel != null && _panel.gameObject.activeSelf;
        public string NoticeShown => _shownNotice;

        public void Build(Astronaut astronaut, SuitConfig config)
        {
            _astronaut = astronaut;
            _config = config;
            _canvas = UiKit.CreateCanvas("SuitHUD", transform, 9);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            // The visor's rim: a soft band of jet light along each edge of the view.
            _rimTexture = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VisorRim" };
            var pixels = new Color32[32];
            for (int i = 0; i < 32; i++)
            {
                float t = 1f - i / 31f;
                pixels[i] = new Color(1f, 1f, 1f, t * t * t);
            }
            _rimTexture.SetPixels32(pixels);
            _rimTexture.Apply(false, true);
            _rimSprite = Sprite.Create(_rimTexture, new Rect(0f, 0f, 1f, 32f), new Vector2(0.5f, 0f), 32f);
            // Bottom, top, left, right. The sprite is brightest along its own bottom edge.
            float[] turn = { 0f, 180f, -90f, 90f };
            Vector2[] edge = { new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f) };
            for (int i = 0; i < 4; i++)
            {
                RectTransform rt = UiKit.NewRect("Rim" + i, _canvas.transform);
                bool sideways = i >= 2;
                // Fixed by the middle of its bright edge to the middle of a screen edge, then turned to face inward.
                rt.anchorMin = rt.anchorMax = edge[i];
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = Vector2.zero;
                Vector2 screen = UiKit.ReferenceResolution;
                // Long enough to span the screen whichever way the canvas is scaled.
                rt.sizeDelta = new Vector2((sideways ? screen.y : screen.x) * 1.6f, 150f);
                rt.localRotation = Quaternion.Euler(0f, 0f, turn[i]);
                _rim[i] = UiKit.AddImage(rt, Color.clear, _rimSprite);
                _rim[i].type = Image.Type.Simple;
                _rim[i].enabled = false;
            }

            _panel = UiKit.NewRect("Suit", _canvas.transform);
            UiKit.Pin(_panel, new Vector2(0f, 0f), new Vector2(26f, 26f), new Vector2(330f, 86f));

            // Air: a ring that empties clockwise, with the percent in the middle.
            RectTransform ringBack = UiKit.NewRect("AirBack", _panel);
            UiKit.Pin(ringBack, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(76f, 76f));
            UiKit.AddImage(ringBack, new Color(0f, 0f, 0f, 0.55f), UiKit.Circle);
            RectTransform ring = UiKit.NewRect("Air", ringBack);
            UiKit.Stretch(ring);
            ring.offsetMin = new Vector2(4f, 4f);
            ring.offsetMax = new Vector2(-4f, -4f);
            _airRing = UiKit.AddImage(ring, UiKit.Cream, UiKit.Circle);
            _airRing.type = Image.Type.Filled;
            _airRing.fillMethod = Image.FillMethod.Radial360;
            _airRing.fillOrigin = (int)Image.Origin360.Top;
            _airRing.fillClockwise = false;
            RectTransform hole = UiKit.NewRect("Hole", ringBack);
            UiKit.Stretch(hole);
            hole.offsetMin = new Vector2(13f, 13f);
            hole.offsetMax = new Vector2(-13f, -13f);
            UiKit.AddImage(hole, new Color(0.10f, 0.09f, 0.08f, 0.95f), UiKit.Circle);
            _airText = UiKit.Label(ringBack, string.Empty, 17, UiKit.Cream);
            UiKit.Stretch((RectTransform)_airText.transform);
            Text airName = UiKit.Label(_panel, "AIR", 13, UiKit.Muted);
            UiKit.Pin((RectTransform)airName.transform, new Vector2(0f, 1f), new Vector2(0f, 14f), new Vector2(76f, 16f));

            // Jet fuel: an upright bar that drains downward.
            RectTransform barBack = UiKit.NewRect("JetsBack", _panel);
            UiKit.Pin(barBack, new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(20f, 76f));
            UiKit.AddImage(barBack, new Color(0f, 0f, 0f, 0.55f), UiKit.Rounded);
            RectTransform fill = UiKit.NewRect("Jets", barBack);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = new Vector2(3f, 3f);
            fill.offsetMax = new Vector2(-3f, -3f);
            _fuelFill = UiKit.AddImage(fill, _config.jetColour);
            _fuelFill.type = Image.Type.Filled;
            _fuelFill.fillMethod = Image.FillMethod.Vertical;
            _fuelFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            // A filled image needs a sprite to cut; any plain one will do.
            _fuelFill.sprite = UiKit.Rounded;
            Text jetName = UiKit.Label(_panel, "JETS", 13, UiKit.Muted);
            UiKit.Pin((RectTransform)jetName.transform, new Vector2(0f, 1f), new Vector2(78f, 14f), new Vector2(44f, 16f));

            _speed = Shadowed(UiKit.Label(_panel, string.Empty, 20, UiKit.Cream, TextAnchor.MiddleLeft));
            UiKit.Pin((RectTransform)_speed.transform, new Vector2(0f, 0.5f), new Vector2(124f, 16f), new Vector2(330f, 26f));
            _tether = Shadowed(UiKit.Label(_panel, string.Empty, 16, UiKit.Muted, TextAnchor.MiddleLeft));
            UiKit.Pin((RectTransform)_tether.transform, new Vector2(0f, 0.5f), new Vector2(124f, -12f), new Vector2(330f, 22f));

            _notice = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 24, UiKit.PanelEdge));
            UiKit.Pin((RectTransform)_notice.transform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 32f));
            _panel.gameObject.SetActive(false);
        }

        static Text Shadowed(Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        void LateUpdate()
        {
            if (_astronaut == null || _panel == null) return;
            SuitInputs s = _astronaut.SuitState;
            bool outside = _astronaut.Mode == AstronautMode.Outside;
            float now = Time.unscaledTime;
            // Aboard, it stays a moment while the tanks fill, so you see the suit drink from the ship.
            if (outside || _astronaut.SuitTanks.Filling || s.AirLow) _shownUntil = now + 1.5f;
            bool show = !_astronaut.Dead && now < _shownUntil && !OrbitalAnte.Cockpit.CockpitDash.WatchingScreen;
            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);

            Rim(outside && show);
            if (!show)
            {
                if (_shownNotice.Length > 0) { _shownNotice = string.Empty; _notice.text = string.Empty; }
                return;
            }

            _airRing.fillAmount = s.Air;
            _fuelFill.fillAmount = s.Fuel;
            bool blink = Mathf.Repeat(now, 0.8f) < 0.5f;
            _airRing.color = s.AirOut || s.AirLow ? (blink ? UiKit.Danger : UiKit.Cream) : UiKit.Cream;
            _fuelFill.color = s.FuelLow ? (blink ? UiKit.Danger : _config.jetColour) : _config.jetColour;

            if (now < _nextText) return;
            _nextText = now + TextInterval;

            int air = Mathf.CeilToInt(s.Air * 100f);
            if (air != _shownAir)
            {
                _shownAir = air;
                _airText.text = air.ToString();
            }

            string notice = outside ? SuitReadout.Notice(s) : s.AirOut || s.AirLow ? SuitReadout.Notice(s) : string.Empty;
            if (notice != _shownNotice)
            {
                _shownNotice = notice;
                _notice.text = notice;
                _notice.color = SuitReadout.IsAlarm(s) ? UiKit.Danger : UiKit.PanelEdge;
            }

            bool lines = outside;
            if (_speed.gameObject.activeSelf != lines)
            {
                _speed.gameObject.SetActive(lines);
                _tether.gameObject.SetActive(lines);
            }
            if (!lines) return;

            // Tenths below 10 m/s, where a tenth matters; whole numbers above.
            int speed = s.Speed < 10f ? Mathf.RoundToInt(s.Speed * 10f) : 1000 + Mathf.RoundToInt(s.Speed);
            string against = SuitReadout.SpeedAgainst(s);
            if (speed != _shownSpeed || against != _shownAgainst)
            {
                _shownSpeed = speed;
                _shownAgainst = against;
                _text.Length = 0;
                if (speed < 1000) _text.Append(speed / 10).Append('.').Append(speed % 10);
                else _text.Append(speed - 1000);
                _text.Append(" m/s ").Append(against);
                _speed.text = _text.ToString();
            }
            SuitRules rules = _config.rules;
            _speed.color = s.Speed >= rules.fatalSpeed * rules.warnShare ? UiKit.Danger : UiKit.Cream;

            int metres = Mathf.RoundToInt(s.ShipDistance);
            int line = s.Tethered ? metres : s.TetherInReach ? -1 : -2 - metres;
            if (line != _shownLine)
            {
                _shownLine = line;
                _text.Length = 0;
                if (s.Tethered) _text.Append("TETHER ").Append(metres).Append(" / ").Append(Mathf.RoundToInt(rules.tetherLength)).Append(" m");
                else if (s.TetherInReach) _text.Append("TETHER OFF");
                else _text.Append("TETHER OFF  SHIP ").Append(metres).Append(" m");
                _tether.text = _text.ToString();
            }
            _tether.color = s.Tethered ? UiKit.Cream : UiKit.PanelEdge;
        }

        // Bottom, top, left, right: lit by the nozzle whose flame is on that side. A push forward (the flames
        // are behind you) lights all four a little.
        static readonly int[] RimNozzle = { JetGlow.Down, JetGlow.Up, JetGlow.Left, JetGlow.Right };

        void Rim(bool on)
        {
            JetGlow jets = _astronaut.Jets;
            float behind = on ? jets.Level(JetGlow.Back) * 0.3f : 0f;
            for (int i = 0; i < 4; i++)
            {
                float level = on ? Mathf.Max(jets.Level(RimNozzle[i]), behind) * _config.visorGlow : 0f;
                bool lit = level > 0.01f;
                if (_rim[i].enabled != lit) _rim[i].enabled = lit;
                if (!lit) continue;
                Color c = _config.jetColour;
                c.a = level;
                _rim[i].color = c;
            }
        }

        void OnDestroy()
        {
            if (_rimSprite != null) Destroy(_rimSprite);
            if (_rimTexture != null) Destroy(_rimTexture);
        }
    }
}
