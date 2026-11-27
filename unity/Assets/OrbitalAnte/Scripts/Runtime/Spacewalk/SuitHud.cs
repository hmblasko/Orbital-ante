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
    /// A bolted plate in the lower left ("Hull stencil", Matthew's pick of 26 Nov 2026, which replaces the visor
    /// arcs of 15 Nov): air as a tank that drains beside its per cent and the time left; the suit's armour as ten
    /// chevrons that go dark one by one; jet fuel as a row of blocks. Your speed and what it is measured against,
    /// the tether and your stance are lines just above the plate. One notice at a time on a strip low in the
    /// middle, with hazard stripes when it is an alarm. The lower corners of the view are shaded like the edge of
    /// a helmet's glass, and the rim of the visor glows on the side a jet is burning.
    /// </summary>
    public sealed class SuitHud : MonoBehaviour
    {
        const float TextInterval = 0.12f;

        Astronaut _astronaut;
        SuitConfig _config;
        Canvas _canvas;
        RectTransform _panel;
        Text _speed, _tether, _notice, _stance;
        int _shownStance = -1;
        // The plate: air on the left, armour and jets on the right.
        RectTransform _plate, _suitColumn, _jetRow, _noticePlate;
        RectTransform _tankFill;
        Image _tankImage;
        Image[] _chevrons, _jetBlocks;
        Image[] _noticeStripes;
        Text _airName, _airNumber, _airLine, _suitLine;
        int _shownAir = -1, _shownPlates = -1, _shownTank = -1;
        string _shownAirName, _shownAirLine, _shownSuitLine;
        bool _noticeAlarm;
        const float TankInner = 84f;
        readonly Image[] _shade = new Image[2];
        Texture2D _shadeTexture;
        Sprite _shadeSprite;
        readonly Image[] _rim = new Image[4];
        Texture2D _rimTexture;
        Sprite _rimSprite;
        float _nextText, _shownUntil;
        int _airSecond = int.MinValue;
        string _airWords = string.Empty;
        int _shownSpeed = -1, _shownLine = -1, _noticeKey = -1, _againstKey = -1;
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
                rt.sizeDelta = new Vector2((sideways ? screen.y : screen.x) * 1.6f, 90f);
                rt.localRotation = Quaternion.Euler(0f, 0f, turn[i]);
                // Coloured once; its brightness is then set on the renderer, which does not rebuild the canvas.
                _rim[i] = UiKit.AddImage(rt, _config.jetColour, _rimSprite);
                _rim[i].type = Image.Type.Simple;
                _rim[i].enabled = false;
            }

            BuildShade();

            // The suit plate, lower left. Measured from its top left corner.
            _plate = HullKit.Plate(_canvas.transform, "SuitPlate", new Vector2(0f, 0f), new Vector2(HullKit.Margin, HullKit.Margin), new Vector2(400f, 122f));

            // Air: a tank that drains, its per cent, and the time left at a resting breath.
            Image tank = HullKit.Box(_plate, "Tank", 18f, 15f, 30f, 92f, HullKit.PlateEdge);
            tank.sprite = UiKit.Rounded;
            tank.type = Image.Type.Sliced;
            RectTransform well = UiKit.NewRect("Well", tank.transform);
            UiKit.Stretch(well);
            well.offsetMin = new Vector2(2f, 2f);
            well.offsetMax = new Vector2(-2f, -2f);
            Image wellImage = UiKit.AddImage(well, HullKit.Well, UiKit.Rounded);
            wellImage.type = Image.Type.Sliced;
            _tankFill = UiKit.NewRect("Air", well);
            _tankFill.anchorMin = new Vector2(0f, 0f);
            _tankFill.anchorMax = new Vector2(1f, 0f);
            _tankFill.pivot = new Vector2(0.5f, 0f);
            _tankFill.offsetMin = new Vector2(2f, 2f);
            _tankFill.offsetMax = new Vector2(-2f, 2f);
            _tankImage = UiKit.AddImage(_tankFill, Color.white, UiKit.Rounded);
            // Marks at a quarter, a half and three quarters, either side of the tank.
            for (int i = 1; i <= 3; i++)
            {
                HullKit.Box(_plate, "Mark", 12f, 15f + 92f * i / 4f - 1f, 5f, 2f, HullKit.PlateEdge);
                HullKit.Box(_plate, "Mark", 49f, 15f + 92f * i / 4f - 1f, 5f, 2f, HullKit.PlateEdge);
            }
            _airName = HullKit.At(HullKit.Mono(_plate, "O2", 14, Color.white), 64f, 22f, 130f, 18f);
            _airNumber = HullKit.At(HullKit.Stencil(_plate, string.Empty, 46, Color.white), 62f, 62f, 140f, 52f);
            _airLine = HullKit.At(HullKit.Mono(_plate, string.Empty, 14, Color.white), 64f, 104f, 140f, 18f);

            // Armour: ten chevrons, and words only once it is a danger. Only shown in the open, where you can be shot.
            _suitColumn = UiKit.NewRect("Armour", _plate);
            UiKit.Stretch(_suitColumn);
            HullKit.At(HullKit.Mono(_suitColumn, "SUIT PLATES", 14, HullKit.Dim), 206f, 22f, 150f, 18f);
            _chevrons = HullKit.Blocks(_suitColumn, "Plate", 206f, 35f, VisorReadout.Plates, 17f, 28f, 0f, HullKit.Chevron);
            _suitLine = HullKit.At(HullKit.Mono(_suitColumn, string.Empty, 14, Color.white), 206f, 77f, 180f, 18f);

            // Jet fuel: a row of blocks under the armour. Not shown while driving (the rover has its own boost bar).
            _jetRow = UiKit.NewRect("Jets", _plate);
            UiKit.Stretch(_jetRow);
            HullKit.At(HullKit.Mono(_jetRow, "JETS", 14, HullKit.Dim), 206f, 104f, 50f, 18f);
            _jetBlocks = HullKit.Blocks(_jetRow, "Jet", 254f, 99f, HullReadout.JetBlocks, 10f, 10f, 3f);
            _plate.gameObject.SetActive(false);

            // Speed, tether and stance: lines just above the plate.
            _panel = UiKit.NewRect("Suit", _canvas.transform);
            UiKit.Pin(_panel, new Vector2(0f, 0f), new Vector2(HullKit.Margin + 2f, 152f), new Vector2(520f, 72f));
            // Shadow only: this line is rewritten several times a second, and an outline draws it four more times.
            _speed = UiKit.Halo(HullKit.Mono(_panel, string.Empty, 17, UiKit.Cream));
            UiKit.Pin((RectTransform)_speed.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(520f, 22f));
            _tether = UiKit.Halo(HullKit.Mono(_panel, string.Empty, 14, UiKit.Muted, TextAnchor.MiddleLeft, false), true);
            UiKit.Pin((RectTransform)_tether.transform, new Vector2(0f, 0f), new Vector2(0f, 24f), new Vector2(520f, 20f));
            // How you are holding yourself on foot, so a sneak (which a part-pushed stick starts by itself) is never
            // a hidden state. Blank when simply standing.
            _stance = UiKit.Halo(HullKit.Mono(_panel, string.Empty, 16, HullKit.Orange), true);
            UiKit.Pin((RectTransform)_stance.transform, new Vector2(0f, 0f), new Vector2(0f, 48f), new Vector2(520f, 22f));

            // The one notice: a strip low in the middle, between the two plates. Hazard stripes mean an alarm.
            _noticePlate = HullKit.Plate(_canvas.transform, "Notice", new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(380f, 46f), false);
            _noticeStripes = new[] { HullKit.HazardStrip(_noticePlate, true, 6f), HullKit.HazardStrip(_noticePlate, false, 6f) };
            _notice = HullKit.Stencil(_noticePlate, string.Empty, 21, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch((RectTransform)_notice.transform);
            _noticePlate.gameObject.SetActive(false);
            _panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// The edge of the helmet's glass: a soft dark shade in each lower corner of the view, so the gauges sit
        /// on something darker than a bright sky. Two corner pieces rather than one full-screen sheet, to keep
        /// the Deck's fill cost small.
        /// </summary>
        void BuildShade()
        {
            const int w = 64, h = 40;
            _shadeTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VisorShade" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Darkest in the lower-left corner of the picture, fading out along an ellipse.
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * w + x] = new Color(0.04f, 0.03f, 0.03f, a * a * (3f - 2f * a));
                }
            }
            _shadeTexture.SetPixels32(px);
            _shadeTexture.Apply(false, true);
            _shadeSprite = Sprite.Create(_shadeTexture, new Rect(0f, 0f, w, h), new Vector2(0f, 0f), 100f, 0, SpriteMeshType.FullRect);
            for (int i = 0; i < 2; i++)
            {
                RectTransform rt = UiKit.NewRect(i == 0 ? "ShadeLeft" : "ShadeRight", _canvas.transform);
                Vector2 corner = new Vector2(i, 0f);
                UiKit.Pin(rt, corner, Vector2.zero, new Vector2(440f, 270f));
                // Both turn about their darkest corner, which sits in the screen's corner; the right one is the
                // left one mirrored about it.
                rt.pivot = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                if (i == 1) rt.localScale = new Vector3(-1f, 1f, 1f);
                _shade[i] = UiKit.AddImage(rt, new Color(1f, 1f, 1f, _config.visorShade), _shadeSprite);
                _shade[i].type = Image.Type.Simple;
                _shade[i].enabled = false;
            }
        }

        void LateUpdate()
        {
            if (_astronaut == null || _panel == null) return;
            SuitInputs s = _astronaut.SuitState;
            bool outside = _astronaut.Mode == AstronautMode.Outside;
            // Driving the rover you are still in the open in the suit: air and armour show, the jet panel does not.
            bool open = _astronaut.InTheOpen;
            float now = Time.unscaledTime;
            // Aboard, it stays a moment while the tanks fill, so you see the suit drink from the ship.
            if (open || _astronaut.SuitTanks.Filling || s.AirLow) _shownUntil = now + 1.5f;
            bool show = !_astronaut.Dead && now < _shownUntil && !OrbitalAnte.Cockpit.CockpitDash.WatchingScreen;
            bool panel = show && _astronaut.Mode != AstronautMode.Driving;
            if (_panel.gameObject.activeSelf != panel) _panel.gameObject.SetActive(panel);
            if (_plate.gameObject.activeSelf != show) _plate.gameObject.SetActive(show);
            // The armour only counts in the open, where you can be shot.
            bool armourShown = show && open && _astronaut.Armour != null;
            if (_suitColumn.gameObject.activeSelf != armourShown) _suitColumn.gameObject.SetActive(armourShown);
            if (_jetRow.gameObject.activeSelf != panel) _jetRow.gameObject.SetActive(panel);
            // Driving, the rover's speed and boost bar have the low middle: the notice strip sits above them.
            float noticeY = _astronaut.Mode == AstronautMode.Driving ? 176f : 104f;
            if (!Mathf.Approximately(_noticePlate.anchoredPosition.y, noticeY)) _noticePlate.anchoredPosition = new Vector2(0f, noticeY);
            bool shade = show && open && _config.visorShade > 0.001f;
            for (int i = 0; i < 2; i++) if (_shade[i].enabled != shade) _shade[i].enabled = shade;

            Rim(outside && show);
            if (!show)
            {
                if (_shownNotice.Length > 0) { _shownNotice = string.Empty; _notice.text = string.Empty; _noticeKey = -1; }
                if (_noticePlate.gameObject.activeSelf) _noticePlate.gameObject.SetActive(false);
                return;
            }

            // Words and sizes are written only when they would look different (every write makes the canvas lay
            // itself out again); colours go on the renderers, which costs it nothing.
            bool blink = Mathf.Repeat(now, 0.8f) < 0.5f;
            if (panel)
                HullKit.Light(_jetBlocks, HullReadout.Blocks(s.Fuel, HullReadout.JetBlocks),
                    s.FuelLow ? (blink ? HullKit.Orange : HullKit.Unlit) : _config.jetColour);

            GaugeTone airTone = VisorReadout.AirTone(s.AirLow, s.AirOut, s.BurningAir);
            int airSecond = s.AirOut ? -1 - Mathf.CeilToInt(Mathf.Max(0f, s.SecondsLeft))
                : s.BurningAir ? int.MaxValue
                : VisorReadout.AirSeconds(s.Air, _config.rules.airMinutes);
            if (airSecond != _airSecond)
            {
                _airSecond = airSecond;
                _airWords = VisorReadout.AirLine(s.Air, _config.rules.airMinutes, s.AirOut, s.SecondsLeft, s.BurningAir).ToUpperInvariant();
            }
            ShowAir(s.Air, airTone, airTone != GaugeTone.Alarm || blink);
            if (armourShown)
            {
                float armour = _astronaut.Armour.Fraction;
                // A fresh hit flashes the plates for a moment.
                bool hit = now - _astronaut.Armour.LastHitUnscaledTime < 0.25f;
                ShowArmour(armour, VisorReadout.SuitTone(armour), !hit || blink);
            }

            int stance = outside ? (int)_astronaut.Stances.Current * 2 + (_astronaut.Sneaking ? 1 : 0) : 0;
            if (stance != _shownStance)
            {
                _shownStance = stance;
                _stance.text = StanceWords(stance);
            }

            if (now < _nextText) return;
            _nextText = now + TextInterval;

            // The words are only built again when something they depend on has changed.
            bool wanted = outside || s.AirOut || s.AirLow;
            int body = s.GroundName != null ? s.GroundName.GetHashCode() : 0;
            int noticeKey = !wanted ? 0 : 1
                | (s.AirOut ? 2 : 0) | (s.AirLow ? 4 : 0) | (s.BurningAir ? 8 : 0) | (s.FuelOut ? 16 : 0) | (s.FuelLow ? 32 : 0)
                | (s.TetherTaut ? 64 : 0) | (s.Matching ? 128 : 0) | (s.MatchTarget == MatchTarget.Ship ? 256 : 0) | (s.AgainstGround ? 512 : 0)
                | (s.AirOut ? Mathf.CeilToInt(Mathf.Max(0f, s.SecondsLeft)) << 10 : 0) | ((body & 0x3FF) << 20);
            if (noticeKey != _noticeKey)
            {
                _noticeKey = noticeKey;
                string notice = wanted ? SuitReadout.Notice(s) : string.Empty;
                _shownNotice = notice;
                _notice.text = notice;
                _noticeAlarm = SuitReadout.IsAlarm(s);
                bool any = notice.Length > 0;
                if (_noticePlate.gameObject.activeSelf != any) _noticePlate.gameObject.SetActive(any);
                if (any)
                {
                    // As wide as its words need, and no wider.
                    // Words too long for the gap between the lower plates drop a size rather than cover their edges.
                    _notice.fontSize = 21;
                    if (_notice.preferredWidth > HullReadout.NoticeMost - HullReadout.NoticePadding) _notice.fontSize = 18;
                    float width = HullReadout.NoticeWidth(_notice.preferredWidth);
                    if (!Mathf.Approximately(_noticePlate.sizeDelta.x, width)) _noticePlate.sizeDelta = new Vector2(width, _noticePlate.sizeDelta.y);
                    for (int i = 0; i < _noticeStripes.Length; i++)
                        if (_noticeStripes[i].enabled != _noticeAlarm) _noticeStripes[i].enabled = _noticeAlarm;
                }
            }
            HullKit.Tint(_notice, _noticeAlarm ? HullKit.Orange : HullKit.Paint);

            bool lines = outside;
            if (_speed.gameObject.activeSelf != lines)
            {
                _speed.gameObject.SetActive(lines);
                _tether.gameObject.SetActive(lines);
            }
            if (!lines) return;

            // Tenths below 10 m/s, where a tenth matters; whole numbers above.
            int speed = s.Speed < 10f ? Mathf.RoundToInt(s.Speed * 10f) : 1000 + Mathf.RoundToInt(s.Speed);
            int againstKey = (s.MatchTarget == MatchTarget.Ship ? 1 : 0) | (s.AgainstGround ? 2 : 0) | (body << 2);
            if (againstKey != _againstKey)
            {
                _againstKey = againstKey;
                _shownAgainst = SuitReadout.SpeedAgainst(s);
                _shownSpeed = -1;
            }
            string against = _shownAgainst;
            if (speed != _shownSpeed)
            {
                _shownSpeed = speed;
                _text.Length = 0;
                if (speed < 1000) _text.Append(speed / 10).Append('.').Append(speed % 10);
                else _text.Append(speed - 1000);
                _text.Append(" m/s ").Append(against);
                _speed.text = _text.ToString();
            }
            SuitRules rules = _config.rules;
            _speed.color = s.Speed >= rules.fatalSpeed * rules.warnShare ? HullKit.Orange : UiKit.Cream;

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
            _tether.color = s.Tethered ? UiKit.Cream : HullKit.Orange;
        }

        /// <summary>The air side of the plate. <paramref name="lit"/> false dims the coloured parts for a blink.</summary>
        void ShowAir(float air, GaugeTone tone, bool lit)
        {
            air = Mathf.Clamp01(float.IsNaN(air) ? 0f : air);
            Color main = tone == GaugeTone.Calm ? VisorGauge.Air : HullKit.Orange;
            if (tone == GaugeTone.Alarm && !lit) main = Color.Lerp(main, Color.black, 0.55f);

            // The tank in whole pixels; anything left still shows a sliver.
            int tank = air <= 0f ? 0 : Mathf.Max(2, Mathf.RoundToInt(TankInner * air));
            if (tank != _shownTank)
            {
                _shownTank = tank;
                _tankFill.sizeDelta = new Vector2(_tankFill.sizeDelta.x, tank);
            }
            HullKit.Tint(_tankImage, main);

            int number = VisorReadout.Percent(air);
            if (number != _shownAir)
            {
                _shownAir = number;
                _airNumber.text = number + "<size=24>%</size>";
            }
            string name = tone == GaugeTone.Alarm ? "O2 LOW" : "O2";
            if (!ReferenceEquals(name, _shownAirName))
            {
                _shownAirName = name;
                _airName.text = name;
            }
            if (!ReferenceEquals(_airWords, _shownAirLine))
            {
                _shownAirLine = _airWords;
                _airLine.text = _airWords;
            }
            HullKit.Tint(_airNumber, tone == GaugeTone.Calm ? HullKit.Paint : main);
            HullKit.Tint(_airName, tone == GaugeTone.Calm ? HullKit.Dim : main);
            HullKit.Tint(_airLine, tone == GaugeTone.Alarm ? HullKit.Paint : HullKit.Sub);
        }

        /// <summary>
        /// The armour side: a chevron goes dark only when its plate is used up, and the last one still on is
        /// orange while it is being worn through.
        /// </summary>
        void ShowArmour(float fraction, GaugeTone tone, bool lit)
        {
            fraction = Mathf.Clamp01(float.IsNaN(fraction) ? 0f : fraction);
            int left = VisorReadout.PlatesLeft(fraction);
            Color main = tone == GaugeTone.Alarm ? HullKit.Orange : HullKit.Paint;
            Color last = VisorReadout.LastPlateWorn(fraction) ? HullKit.Orange : main;
            if (!lit)
            {
                main = Color.Lerp(main, Color.black, 0.55f);
                last = Color.Lerp(last, Color.black, 0.55f);
            }
            HullKit.Light(_chevrons, left, main, last);

            string words = VisorReadout.SuitLine(fraction);
            if (left != _shownPlates || !ReferenceEquals(words, _shownSuitLine))
            {
                _shownPlates = left;
                _shownSuitLine = words;
                _suitLine.text = words.Length > 0 ? words.ToUpperInvariant()
                    : HullReadout.TwoDigits(left) + " OF " + HullReadout.TwoDigits(VisorReadout.Plates);
            }
            HullKit.Tint(_suitLine, tone == GaugeTone.Alarm ? HullKit.Orange : HullKit.Sub);
        }

        static string StanceWords(int stance)
        {
            switch (stance)
            {
                case 1: return "SNEAKING";
                case 2: return "CROUCHED";
                case 3: return "CROUCHED, SNEAKING";
                case 4: case 5: return "PRONE";
                default: return string.Empty;
            }
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
                _rim[i].canvasRenderer.SetAlpha(level);
            }
        }

        void OnDestroy()
        {
            if (_rimSprite != null) Destroy(_rimSprite);
            if (_rimTexture != null) Destroy(_rimTexture);
            if (_shadeSprite != null) Destroy(_shadeSprite);
            if (_shadeTexture != null) Destroy(_shadeTexture);
        }
    }
}
