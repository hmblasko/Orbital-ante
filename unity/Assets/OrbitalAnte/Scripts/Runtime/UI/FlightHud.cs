using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Navigation;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// What you see from the seat while flying: a name and distance on every world in view, and a row of instruments
    /// along the dash (Matthew, 20 Nov 2026: the stats should be "super interesting", simple, sharp, "not like AI made
    /// it" and easily visible; he was shown option B, a dash strip under a row of warning lamps).
    ///
    /// Top middle: the world you are near, big, and how high you are in words. Along the dash: five readings, each a
    /// big number with a small unit and one short note, always in the same place (speed, height, pull, orbit, wind).
    /// Above them: lamps for Boost, Match, Lock, Auto, the orbit's path, Pull and Brake, which flash as they come on;
    /// one line of advice above the lamps when there is something to do. The list of keys fades after you sit down.
    /// The rules (splitting numbers, the flash, the orbit bar) are <see cref="DashStrip"/>.
    /// </summary>
    public sealed class FlightHud : MonoBehaviour
    {
        sealed class Marker
        {
            public RectTransform Root;
            public Image Dot;
            public Text Label;
            public Color Plain;
            public long ShownKey = long.MinValue;
        }

        sealed class Cell
        {
            public Text Name, Number, Unit, Note;
            public string ShownNumber = string.Empty;
        }

        sealed class Lamp
        {
            public Image Glass;
            public Text Word;
            public bool On;
            public float OnSince;
            public int Shown = -1;
            public string ShownWord = string.Empty;
            public Color Lit;
        }

        const int CellCount = 5;
        const float NumberBox = 96f;
        const float StripWidth = 1140f;
        const float StripHeight = 96f;
        const float StripBottom = 76f;
        const float LampWidth = 96f;
        const float LampGap = 8f;
        static readonly Color LampOffGlass = new Color(0.04f, 0.035f, 0.03f, 0.5f);
        static readonly Color LampOffWord = new Color(0.965f, 0.918f, 0.824f, 0.35f);
        static readonly Color LampInk = new Color(0.08f, 0.05f, 0.03f, 1f);

        SolarSystem _system;
        TestCraft _craft;
        Canvas _canvas;
        RectTransform _canvasRect;
        Text _where;
        Text _whereNote;
        Text _state;
        Text _help;
        RectTransform _drift;
        Text _driftLabel;
        readonly Cell[] _cells = new Cell[CellCount];
        readonly Lamp[] _lamps = new Lamp[7];
        RectTransform _orbitBar, _orbitFill, _orbitNotch;
        readonly Dictionary<int, Marker> _markers = new Dictionary<int, Marker>();
        // Markers move every frame, so they get a canvas of their own and the steady readouts are left alone.
        Canvas _markerCanvas;
        RectTransform _markerRect;
        // Words are refreshed a few times a second, not every frame: nobody can read faster, and building text
        // every frame leaves a steady trickle of rubbish for the game to clear up. Numbers roll every frame, but
        // their text is only rebuilt when the shown digits change.
        const float TextInterval = 0.125f;
        float _nextText;
        float _nextHelp;
        bool _refreshText;
        float _seatedAt;
        double _shownSpeed = double.NaN, _shownHeight = double.NaN;
        double _trueSpeed, _trueHeight;
        long _speedKey = long.MinValue, _heightKey = long.MinValue;
        bool _heightKnown;
        OrbitKind _orbitKind;
        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
        static readonly Color TargetColor = new Color(0.51f, 0.84f, 0.98f);

        public void Build(SolarSystem system, TestCraft craft)
        {
            _system = system;
            _craft = craft;
            _canvas = UiKit.CreateCanvas("FlightHUD", transform, 9);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;
            _canvasRect = (RectTransform)_canvas.transform;
            _markerCanvas = UiKit.CreateCanvas("FlightMarkers", transform, 8);
            _markerCanvas.GetComponent<GraphicRaycaster>().enabled = false;
            _markerRect = (RectTransform)_markerCanvas.transform;
            _seatedAt = Time.unscaledTime;

            // Aiming mark: a small open cross, so the thing you are pointing at is not hidden behind it.
            for (int i = 0; i < 4; i++)
            {
                var tick = UiKit.NewRect("AimTick", _canvas.transform);
                float angle = 90f * i;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = dir * 11f;
                tick.sizeDelta = new Vector2(8f, 2f);
                tick.localRotation = Quaternion.Euler(0f, 0f, angle);
                UiKit.AddImage(tick, new Color(0.965f, 0.918f, 0.824f, 0.85f));
            }

            Font numbers = UiKit.NumberFont, labels = UiKit.LabelFont;

            // Top middle: the world, big, and the height in words under it.
            _where = UiKit.Halo(UiKit.DashLabel(_canvas.transform, string.Empty, 40, UiKit.DashAmber, numbers, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_where.transform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(900f, 46f));
            _whereNote = UiKit.Halo(UiKit.DashLabel(_canvas.transform, string.Empty, 14, UiKit.DashCream, labels, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_whereNote.transform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(900f, 20f));

            BuildStrip(numbers, labels);
            BuildLamps(labels);

            _state = UiKit.Halo(UiKit.DashLabel(_canvas.transform, string.Empty, 15, UiKit.DashAmber, labels, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_state.transform, new Vector2(0.5f, 0f), new Vector2(0f, StripBottom + StripHeight + 52f), new Vector2(1100f, 22f));

            _targetLine = UiKit.Halo(UiKit.DashLabel(_canvas.transform, string.Empty, 15, UiKit.DashBlue, labels, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_targetLine.transform, new Vector2(0.5f, 0f), new Vector2(0f, StripBottom + StripHeight + 78f), new Vector2(1100f, 22f));

            _help = UiKit.Halo(UiKit.DashLabel(_canvas.transform, string.Empty, 13, UiKit.DashDim, labels, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_help.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1240f, 22f));

            // Drift marker: a ring with a dot, placed where your velocity points.
            // On the markers' canvas, which is rebuilt every frame anyway, so its moving does not rebuild the readings.
            _drift = UiKit.NewRect("DriftMarker", _markerCanvas.transform);
            _drift.anchorMin = _drift.anchorMax = _drift.pivot = new Vector2(0.5f, 0.5f);
            _drift.sizeDelta = new Vector2(26f, 26f);
            UiKit.AddImage(_drift, new Color(0.45f, 0.95f, 0.6f, 0.35f), UiKit.Circle);
            var core = UiKit.NewRect("Core", _drift);
            UiKit.Pin(core, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
            UiKit.AddImage(core, new Color(0.45f, 0.95f, 0.6f, 1f), UiKit.Circle);
            _driftLabel = UiKit.Halo(UiKit.DashLabel(_drift, string.Empty, 12, new Color(0.45f, 0.95f, 0.6f), labels, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_driftLabel.transform, new Vector2(0.5f, 0f), new Vector2(0f, -20f), new Vector2(200f, 18f));
        }

        void BuildStrip(Font numbers, Font labels)
        {
            // A see-through dark plate along the dash with an amber rule on top: a row of real instruments.
            var strip = UiKit.NewRect("DashStrip", _canvas.transform);
            UiKit.Pin(strip, new Vector2(0.5f, 0f), new Vector2(0f, StripBottom), new Vector2(StripWidth, StripHeight));
            UiKit.AddImage(strip, UiKit.DashPlate);
            var rule = UiKit.NewRect("Rule", strip);
            UiKit.Pin(rule, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(StripWidth, 2f));
            UiKit.AddImage(rule, UiKit.DashAmber);

            string[] names = { "SPEED", "HEIGHT", "PULL", "ORBIT", "WIND" };
            float cellWidth = StripWidth / CellCount;
            for (int i = 0; i < CellCount; i++)
            {
                var cell = UiKit.NewRect("Cell " + names[i], strip);
                cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(0f, 1f);
                cell.anchoredPosition = new Vector2(i * cellWidth, 0f);
                cell.sizeDelta = new Vector2(cellWidth, StripHeight);
                if (i > 0)
                {
                    var divider = UiKit.NewRect("Divider", cell);
                    UiKit.Pin(divider, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1f, StripHeight - 28f));
                    UiKit.AddImage(divider, new Color(0.965f, 0.918f, 0.824f, 0.35f));
                }
                var c = new Cell
                {
                    Name = UiKit.DashLabel(cell, names[i], 12, UiKit.DashDim, labels),
                    Number = UiKit.DashLabel(cell, string.Empty, 46, UiKit.DashCream, numbers),
                    Unit = UiKit.DashLabel(cell, string.Empty, 12, UiKit.DashDim, labels),
                    Note = UiKit.DashLabel(cell, string.Empty, 12, UiKit.DashDim, labels),
                };
                UiKit.Pin((RectTransform)c.Name.transform, new Vector2(0f, 1f), new Vector2(20f, -8f), new Vector2(cellWidth - 30f, 18f));
                // Digits are right-aligned in a fixed box and the unit sits at a fixed place after it, so a rolling
                // number never pushes its unit about (the number face's digits are not all one width).
                c.Number.alignment = TextAnchor.MiddleRight;
                UiKit.Pin((RectTransform)c.Number.transform, new Vector2(0f, 1f), new Vector2(14f, -24f), new Vector2(NumberBox, 52f));
                UiKit.Pin((RectTransform)c.Unit.transform, new Vector2(0f, 1f), new Vector2(14f + NumberBox + 6f, -53f), new Vector2(70f, 18f));
                UiKit.Pin((RectTransform)c.Note.transform, new Vector2(0f, 0f), new Vector2(20f, 6f), new Vector2(cellWidth - 30f, 18f));
                _cells[i] = c;

                if (i == 3)
                {
                    // Orbit bar: filled to your speed across the world, the notch is what a circle at this height needs.
                    _orbitBar = UiKit.NewRect("OrbitBar", cell);
                    UiKit.Pin(_orbitBar, new Vector2(1f, 1f), new Vector2(-16f, -42f), new Vector2(84f, 6f));
                    UiKit.AddImage(_orbitBar, new Color(0.965f, 0.918f, 0.824f, 0.22f));
                    _orbitFill = UiKit.NewRect("Fill", _orbitBar);
                    _orbitFill.anchorMin = Vector2.zero;
                    _orbitFill.anchorMax = new Vector2(0f, 1f);
                    _orbitFill.pivot = new Vector2(0f, 0.5f);
                    _orbitFill.offsetMin = _orbitFill.offsetMax = Vector2.zero;
                    UiKit.AddImage(_orbitFill, Color.white);
                    _orbitNotch = UiKit.NewRect("Notch", _orbitBar);
                    _orbitNotch.anchorMin = _orbitNotch.anchorMax = new Vector2(0f, 0.5f);
                    _orbitNotch.pivot = new Vector2(0.5f, 0.5f);
                    _orbitNotch.sizeDelta = new Vector2(2f, 16f);
                    UiKit.AddImage(_orbitNotch, UiKit.DashCream);
                }
            }
        }

        void BuildLamps(Font labels)
        {
            float total = _lamps.Length * LampWidth + (_lamps.Length - 1) * LampGap;
            for (int i = 0; i < _lamps.Length; i++)
            {
                var rt = UiKit.NewRect("Lamp " + (DashLamp)i, _canvas.transform);
                float x = -total * 0.5f + LampWidth * 0.5f + i * (LampWidth + LampGap);
                UiKit.Pin(rt, new Vector2(0.5f, 0f), new Vector2(x, StripBottom + StripHeight + 10f), new Vector2(LampWidth, 28f));
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(x, StripBottom + StripHeight + 10f);
                var lamp = new Lamp { Glass = UiKit.AddImage(rt, LampOffGlass) };
                var edge = rt.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0.965f, 0.918f, 0.824f, 0.28f);
                edge.effectDistance = new Vector2(1f, -1f);
                lamp.Word = UiKit.DashLabel(rt, DashStrip.LampWord((DashLamp)i), 14, LampOffWord, labels, TextAnchor.MiddleCenter);
                UiKit.Stretch((RectTransform)lamp.Word.transform);
                _lamps[i] = lamp;
            }
        }

        public void SetVisible(bool visible)
        {
            bool was = _canvas != null && _canvas.gameObject.activeSelf;
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
            if (_markerCanvas != null) _markerCanvas.gameObject.SetActive(visible);
            // Sitting down (or coming back from the map) shows the keys again for a while.
            if (visible && !was) _seatedAt = Time.unscaledTime;
        }

        void LateUpdate()
        {
            if (_system == null || _system.Model == null || _craft == null || !_canvas.gameObject.activeSelf) return;
            _refreshText = Time.unscaledTime >= _nextText;
            if (_refreshText)
            {
                _nextText = Time.unscaledTime + TextInterval;
                UpdateReadouts();
            }
            UpdateHelp();
            RollNumbers();
            UpdateLamps();
            UpdateDriftMarker();
            UpdateBodyMarkers();
            // When the target's own marker is not drawn (too big, off screen, or out of the marked set), its distance
            // and closing speed move to a fixed line above the advice, so they never vanish on the way in.
            string line = _craft.Target >= 0 && !_targetMarked ? _targetLineWords : string.Empty;
            SetText(_targetLine, line);
        }

        /// <summary>
        /// Height for the top line and the height cell. Over real ground it is the height above the ground straight
        /// below (or above the sea), which is the number you need for landing; otherwise the height above the body's
        /// round surface.
        /// </summary>
        void ReadHeight(int anchor, Vector3 position, out double metres, out bool known, out string note)
        {
            known = true;
            Planets.PlanetWorld planets = Planets.PlanetWorld.Instance;
            if (planets != null)
            {
                Planets.GroundReading ground = planets.Read(anchor, position);
                if (ground.Known)
                {
                    if (ground.Underwater)
                    {
                        metres = -ground.AboveSea;
                        note = "UNDER WATER";
                        return;
                    }
                    if (ground.InCave)
                    {
                        metres = System.Math.Max(0, ground.AboveGround);
                        note = "IN A CAVE";
                        return;
                    }
                    metres = System.Math.Max(0, ground.OverSea ? ground.AboveSea : ground.AboveGround);
                    note = ground.InsideClouds ? "IN THE CLOUDS" : ground.OverSea ? "ABOVE THE SEA" : "ABOVE GROUND";
                    return;
                }
            }
            metres = _system.AltitudeAbove(anchor, position);
            note = "ABOVE THE SURFACE";
        }

        void UpdateReadouts()
        {
            SolarSystemModel model = _system.Model;
            int anchor = _system.Frame.Anchor;
            Vector3 position = _craft.transform.position;
            float speed = _craft.Body.linearVelocity.magnitude;
            float pull = _craft.Gravity.FeltGravity.magnitude;
            float thrust = _craft.Config.thrust;
            int target = _craft.Target;

            // Top middle: where you are.
            string heightNote = string.Empty;
            if (anchor == 0)
            {
                SetText(_where, "OPEN SPACE");
                SetText(_whereNote, "FAR FROM ANY WORLD");
                _heightKnown = false;
            }
            else
            {
                SetText(_where, model.Spec(anchor).displayName.ToUpperInvariant());
                ReadHeight(anchor, position, out _trueHeight, out _heightKnown, out heightNote);
                DashStrip.SplitDistance(_trueHeight, out string hn, out string hu);
                SetText(_whereNote, hn + " " + hu + " " + heightNote);
            }

            // Speed.
            _trueSpeed = speed;
            string against = anchor == 0 ? "THE SUN" : model.Spec(anchor).displayName.ToUpperInvariant() + (_system.Frame.Rotating ? " GROUND" : string.Empty);
            SetCell(0, null, null, "VS " + against, DashTone.Plain);

            // Height.
            if (_heightKnown) SetCell(1, null, null, heightNote, DashTone.Plain);
            else SetCell(1, "-", string.Empty, "OPEN SPACE", DashTone.Plain);

            // Pull against what the thrusters can push.
            bool heavy = pull > thrust && !_craft.Boosting && !_craft.Landed && !_craft.Grounded;
            SetCell(2, pull.ToString("0.0", Inv), "M/S\u00B2", "THRUST " + thrust.ToString("0", Inv), heavy ? DashTone.Act : DashTone.Plain);

            // Orbit.
            _orbitKind = OrbitKind.None;
            if (anchor > 0 && (_craft.Landed || _craft.Grounded))
            {
                // Standing on the ground, the spin of the world reads as a "falling" path: say landed instead.
                SetCell(3, "-", string.Empty, "LANDED", DashTone.Plain);
                _orbitBar.gameObject.SetActive(false);
            }
            else if (anchor > 0) UpdateOrbit(model, anchor, position);
            else
            {
                SetCell(3, "-", string.Empty, "NO WORLD TO CIRCLE", DashTone.Plain);
                _orbitBar.gameObject.SetActive(false);
            }

            // What the surroundings are doing to you, so nothing slows or pushes the craft without saying so.
            Planets.PlanetWorld planets = Planets.PlanetWorld.Instance;
            string windNote = "NO AIR";
            DashTone windTone = DashTone.Plain;
            float wind = 0f;
            if (planets != null && planets.Sky != null && planets.Sky.Surface != null)
            {
                Planets.PlanetSky sky = planets.Sky;
                if (sky.Weather.Intensity > 0.05) wind = (float)sky.Weather.Wind.Magnitude;
                if (sky.Air.Underwater) { windNote = "WATER SLOWS YOU"; windTone = DashTone.Watch; }
                else if (sky.Air.Drag > 0.005) { windNote = wind > 0f ? "THICK AIR, CARRIES YOU" : "THICK AIR SLOWS YOU"; windTone = DashTone.Watch; }
                else windNote = wind > 0f ? "CARRIES YOU SIDEWAYS" : "STILL AIR";
            }
            DashStrip.SplitSpeed(wind, out string wn, out string wu);
            SetCell(4, wn, wu, windNote, windTone);

            UpdateTargetWords(model, target);
            UpdateAdvice(model, target, pull, thrust);
        }

        void UpdateOrbit(SolarSystemModel model, int anchor, Vector3 position)
        {
            // The orbit aid (Matthew, 16 Nov 2026): what speed across the world's face holds a circle at this height,
            // how fast you are really going across it, and where the path you are coasting on goes. Measured against
            // the world's centre, not its spinning ground, because that is what an orbit is.
            Vec3d local = SpaceConvert.ToVec3d(position);
            BodyState[] states = _system.States;
            Vec3d offset = _system.Frame.ToTruePosition(local, states) - states[anchor].Position;
            Vec3d velocity = _system.Frame.ToTrueVelocity(local, SpaceConvert.ToVec3d(_craft.Body.linearVelocity), states) - states[anchor].Velocity;
            OrbitReading orbit = OrbitPath.Read(model, anchor, offset, velocity);
            _orbitKind = orbit.Kind;

            DashStrip.SplitSpeed(orbit.SideSpeed, out string n, out string u);
            DashStrip.SplitSpeed(orbit.CircleSpeed, out string cn, out string cu);
            string note;
            if (orbit.Kind == OrbitKind.Orbit || orbit.Kind == OrbitKind.Tugged)
            {
                DashStrip.SplitDistance(orbit.LowHeight, out string ln, out string lu);
                DashStrip.SplitDistance(orbit.HighHeight, out string hn, out string hu);
                note = "LOW " + ln + " " + lu + "  HIGH " + hn + " " + hu;
            }
            else note = "NEEDS " + cn + " " + cu;
            SetCell(3, n, u, note, orbit.Kind == OrbitKind.None ? DashTone.Plain : DashStrip.OrbitTone(orbit.Kind));

            DashStrip.OrbitBar(orbit.SideSpeed, orbit.CircleSpeed, out float fill, out float notch);
            bool show = orbit.CircleSpeed > 0.0;
            if (_orbitBar.gameObject.activeSelf != show) _orbitBar.gameObject.SetActive(show);
            if (!show) return;
            float width = _orbitBar.sizeDelta.x;
            _orbitFill.sizeDelta = new Vector2(width * fill, 0f);
            _orbitFill.GetComponent<Image>().color = UiKit.ToneColor(orbit.Kind == OrbitKind.None ? DashTone.Watch : DashStrip.OrbitTone(orbit.Kind));
            _orbitNotch.anchoredPosition = new Vector2(width * notch, 0f);
        }

        void UpdateTargetWords(SolarSystemModel model, int target)
        {
            // The target's numbers live on its marker (see UpdateBodyMarkers), or on a fixed line when its marker is
            // not drawn. The words are only rebuilt when the shown numbers change.
            if (target < 0)
            {
                _targetWords = string.Empty;
                _targetLineWords = string.Empty;
                _targetKey = long.MinValue;
                return;
            }
            float closing = _craft.ClosingSpeed;
            long key = DashStrip.DistanceKey(_craft.TargetDistance) * 31 + DashStrip.SpeedKey(Mathf.Abs(closing)) * 2 + (closing >= 0f ? 1 : 0) + (long)target * 1000003;
            if (key == _targetKey) return;
            _targetKey = key;
            DashStrip.SplitDistance(_craft.TargetDistance, out string dn, out string du);
            DashStrip.SplitSpeed(Mathf.Abs(closing), out string sn, out string su);
            _targetWords = dn + " " + du + "   " + (closing >= 0f ? "CLOSING " : "PULLING AWAY ") + sn + " " + su;
            _targetLineWords = "[ " + model.Spec(target).displayName.ToUpperInvariant() + " ]   " + _targetWords;
        }

        long _targetKey = long.MinValue;
        string _targetWords = string.Empty;
        string _targetLineWords = string.Empty;
        bool _targetMarked;
        Text _targetLine;

        void UpdateAdvice(SolarSystemModel model, int target, float pull, float thrust)
        {
            // One line of plain advice, most urgent first. The lamps say what is on; this says what to do.
            _mustBrake = target >= 0 && _craft.ClosingSpeed > 30f && _craft.StoppingDistance > _craft.TargetDistance * 0.8f;
            string words = string.Empty;
            Color colour = UiKit.DashAmber;
            if (_craft.PilotOn)
            {
                int key = (int)_craft.Order.Phase * 10000 + (int)Mathf.Clamp(_craft.Order.SecondsLeft, 0f, 5999f);
                if (key != _pilotKey)
                {
                    _pilotKey = key;
                    _pilotWords = Autopilot.Words(_craft.Order.Phase, _craft.Order.SecondsLeft) + "     TOUCH THE THRUST TO TAKE OVER";
                }
                words = _pilotWords;
            }
            else if (Time.unscaledTime - _craft.LastStopTime < 4f && _craft.LastStopWords.Length > 0)
            {
                words = _craft.LastStopWords.ToUpperInvariant();
                colour = _craft.LastStop == PilotStop.Arrived || _craft.LastStop == PilotStop.PilotTookOver ? UiKit.DashAmber : UiKit.DashRed;
            }
            else if (_craft.MatchingVelocity)
            {
                string who = model.Spec(_craft.MatchBody).displayName.ToUpperInvariant();
                words = _craft.RelativeVelocity().magnitude > 0.5f ? "MATCHING VELOCITY WITH " + who : "VELOCITY MATCHED WITH " + who;
            }
            else if (_mustBrake)
            {
                words = "BRAKE NOW: AIM AT " + model.Spec(target).displayName.ToUpperInvariant() + " AND HOLD " + _keyMatch;
                colour = UiKit.DashRed;
            }
            else if (pull > thrust && !_craft.Boosting && !_craft.Landed && !_craft.Grounded)
            {
                words = "PULL IS STRONGER THAN YOUR THRUSTERS: HOLD " + _keyBoost;
                colour = UiKit.DashRed;
            }
            else if (target < 0 && _craft.NearNose >= 0)
            {
                // Nothing dead ahead, but Lock would still take this one: say so, so a rough aim is enough.
                words = "NEAREST THE NOSE: " + model.Spec(_craft.NearNose).displayName.ToUpperInvariant() + "    " + _keyLock + " LOCKS IT";
                colour = UiKit.DashCream;
            }
            SetText(_state, words);
            if (_state.color != colour) _state.color = colour;
        }

        bool _mustBrake;
        string _keyMatch = "MATCH", _keyBoost = "BOOST", _keyLock = "LOCK";

        void UpdateHelp()
        {
            float alpha = DashStrip.KeysAlpha(Time.unscaledTime - _seatedAt);
            Color c = UiKit.DashDim;
            c.a = alpha;
            if (_help.color != c) _help.color = c;
            if (_help.enabled != alpha > 0f) _help.enabled = alpha > 0f;
            if (GameInput.Asset == null || Time.unscaledTime < _nextHelp) return;
            // The key names only change when a control is rebound, so look them up now and then (the advice line
            // uses the same names, so it builds no strings for them).
            _nextHelp = Time.unscaledTime + 2f;
            _keyMatch = Key(GameInput.MatchVelocity);
            _keyBoost = Key(GameInput.Boost);
            _keyLock = Key(GameInput.LockTarget);
            if (alpha <= 0f) return;
            bool pad = GameInput.UsingGamepad;
            SetText(_help, ((pad ? "L STICK" : "W A S D") + " THRUST    " + Key(GameInput.FlyUp) + " / " + Key(GameInput.FlyDown) + " UP / DOWN    " +
                            (pad ? "R STICK LOOK (CLICK TO TURN)    " : "HOLD " + Key(GameInput.FlySteer) + " TURN    ") +
                            Key(GameInput.RollLeft) + " / " + Key(GameInput.RollRight) + " ROLL    " +
                            Key(GameInput.Boost) + " BOOST    " + Key(GameInput.MatchVelocity) + " MATCH    " + Key(GameInput.LockTarget) + " LOCK    " +
                            Key(GameInput.Autopilot) + " AUTOPILOT    " + Key(GameInput.ToggleMap) + " MAP").ToUpperInvariant());
        }

        void UpdateLamps()
        {
            bool thrusting = _craft.ThrustInput.sqrMagnitude > 0.01f;
            float pull = _craft.Gravity.FeltGravity.magnitude;
            SetLamp(DashLamp.Boost, _craft.Boosting && thrusting, null);
            SetLamp(DashLamp.Match, _craft.MatchingVelocity, null);
            SetLamp(DashLamp.Lock, _craft.LockedTarget >= 0, null);
            SetLamp(DashLamp.Auto, _craft.PilotOn, null);
            bool onGround = _craft.Landed || _craft.Grounded;
            bool orbitOn = _orbitKind != OrbitKind.None && _system.Frame.Anchor > 0 && !onGround;
            SetLamp(DashLamp.Orbit, orbitOn, orbitOn ? DashStrip.OrbitWord(_orbitKind) : DashStrip.LampWord(DashLamp.Orbit));
            SetLamp(DashLamp.Pull, pull > _craft.Config.thrust && !_craft.Boosting && !onGround, null);
            SetLamp(DashLamp.Brake, _mustBrake, null);
        }

        void SetLamp(DashLamp which, bool on, string word)
        {
            Lamp lamp = _lamps[(int)which];
            float now = Time.unscaledTime;
            if (on && !lamp.On) lamp.OnSince = now;
            lamp.On = on;
            if (word != null && word != lamp.ShownWord)
            {
                lamp.ShownWord = word;
                lamp.Word.text = word;
                lamp.Shown = -1;
            }
            bool lit = DashStrip.LampLit(on, now - lamp.OnSince);
            Color glow = UiKit.ToneColor(DashStrip.LampTone(which, _orbitKind));
            int state = lit ? 1 : 0;
            if (state == lamp.Shown && (!lit || glow == lamp.Lit)) return;
            lamp.Shown = state;
            lamp.Lit = glow;
            if (lit)
            {
                glow.a = 0.92f;
                lamp.Glass.color = glow;
                lamp.Word.color = LampInk;
            }
            else
            {
                lamp.Glass.color = LampOffGlass;
                lamp.Word.color = LampOffWord;
            }
        }

        void RollNumbers()
        {
            // Speed and height roll toward the truth, so a change is seen as movement, not a flicker of digits.
            float dt = Time.unscaledDeltaTime;
            // Digits are only rebuilt when what they show changes (an integer key per shown value), so the strip
            // makes no new strings frame to frame while a number holds still on screen.
            _shownSpeed = DashStrip.RollToward(_shownSpeed, _trueSpeed, dt);
            long speedKey = DashStrip.SpeedKey(_shownSpeed);
            if (speedKey != _speedKey)
            {
                _speedKey = speedKey;
                DashStrip.SplitSpeed(_shownSpeed, out string sn, out string su);
                SetNumber(0, sn, su);
            }
            if (_heightKnown)
            {
                _shownHeight = DashStrip.RollToward(_shownHeight, _trueHeight, dt);
                long heightKey = DashStrip.DistanceKey(_shownHeight);
                if (heightKey != _heightKey)
                {
                    _heightKey = heightKey;
                    DashStrip.SplitDistance(_shownHeight, out string hn, out string hu);
                    SetNumber(1, hn, hu);
                }
            }
            else
            {
                _shownHeight = double.NaN;
                _heightKey = long.MinValue;
            }
        }

        void SetCell(int index, string number, string unit, string note, DashTone tone)
        {
            Cell cell = _cells[index];
            if (number != null) SetNumber(index, number, unit);
            SetText(cell.Note, note);
            Color numberColour = UiKit.ToneColor(tone);
            if (cell.Number.color != numberColour) cell.Number.color = numberColour;
            Color noteColour = tone == DashTone.Plain ? UiKit.DashDim : numberColour;
            if (cell.Note.color != noteColour) cell.Note.color = noteColour;
        }

        void SetNumber(int index, string number, string unit)
        {
            Cell cell = _cells[index];
            if (number == cell.ShownNumber && cell.Unit.text == unit) return;
            cell.ShownNumber = number;
            cell.Number.text = number;
            cell.Unit.text = unit;
        }

        static void SetText(Text label, string text)
        {
            if (label.text != text) label.text = text;
        }

        int _pilotKey = -1;
        string _pilotWords = string.Empty;

        static string Key(UnityEngine.InputSystem.InputAction action) => GameInput.Describe(action).ToUpperInvariant();

        void UpdateDriftMarker()
        {
            Vector3 velocity = _craft.Body.linearVelocity;
            float speed = velocity.magnitude;
            Camera cam = _craft.Eye;
            if (speed < 1f)
            {
                _drift.gameObject.SetActive(false);
                return;
            }
            // Show where you are heading; if that is behind you, show where you are coming from instead.
            Vector3 view = cam.WorldToViewportPoint(cam.transform.position + velocity / speed * 100f);
            bool ahead = view.z > 0f;
            if (!ahead) view = cam.WorldToViewportPoint(cam.transform.position - velocity / speed * 100f);
            bool onScreen = view.z > 0f && view.x > 0.02f && view.x < 0.98f && view.y > 0.04f && view.y < 0.96f;
            _drift.gameObject.SetActive(onScreen);
            if (!onScreen) return;
            _drift.anchoredPosition = ToCanvas(view);
            if (_refreshText) _driftLabel.text = ahead ? "HEADING" : "COMING FROM";
        }

        Vector2 ToCanvas(Vector3 viewport)
        {
            Rect rect = _canvasRect.rect;
            return new Vector2((viewport.x - 0.5f) * rect.width, (viewport.y - 0.5f) * rect.height);
        }

        void UpdateBodyMarkers()
        {
            _targetMarked = false;
            SolarSystemModel model = _system.Model;
            Camera cam = _craft.Eye;
            Vector3 eye = cam.transform.position;
            int anchor = _system.Frame.Anchor;
            int home = _system.PlanetOf(anchor);
            float pixelsPerRadian = _markerRect.rect.height / (cam.fieldOfView * Mathf.Deg2Rad);
            int target = _craft.Target;

            for (int i = 0; i < model.Count; i++)
            {
                BodySpec spec = model.Spec(i);
                Vector3 centre = _system.Bodies[i].transform.position;
                float distance = Vector3.Distance(eye, centre);
                float surfaceDistance = Mathf.Max(0f, distance - (float)spec.radius);

                // Keep the screen readable: planets always, moons of the planet you are at, rocks only when close.
                bool relevant;
                if (spec.kind == BodyKind.Asteroid) relevant = surfaceDistance < 2500f;
                else if (spec.kind == BodyKind.Moon) relevant = home != 0 && _system.PlanetOf(i) == home;
                else relevant = true;

                Vector3 view = relevant ? cam.WorldToViewportPoint(centre) : Vector3.zero;
                float size = distance > 1f ? Mathf.Asin(Mathf.Clamp01((float)spec.radius / distance)) * pixelsPerRadian : 9999f;
                bool show = relevant && view.z > 0f && view.x > 0f && view.x < 1f && view.y > 0f && view.y < 1f
                            && size < _markerRect.rect.height * 0.3f;

                if (i == target) _targetMarked = show;
                if (!_markers.TryGetValue(i, out Marker marker))
                {
                    if (!show) continue;
                    marker = CreateMarker(spec, _system.Bodies[i].MapColor);
                    _markers[i] = marker;
                }
                if (marker.Root.gameObject.activeSelf != show) marker.Root.gameObject.SetActive(show);
                if (!show) continue;

                marker.Root.anchoredPosition = ToCanvas(view);
                // A dot for things too small to see; once the body itself is visible the dot gets out of the way.
                marker.Dot.enabled = size < 5f;
                ((RectTransform)marker.Label.transform).anchoredPosition = new Vector2(0f, -(Mathf.Max(size, 5f) + 6f));
                if (_refreshText)
                {
                    bool aimed = i == target;
                    marker.Label.color = aimed ? TargetColor : marker.Plain;
                    // Only rebuild the words when what they show changes (the rounded distance, aimed or not, the
                    // target line), so a still sky makes no new strings.
                    long key = DashStrip.DistanceKey(surfaceDistance) * 4 + (aimed ? (_targetWords.Length > 0 ? 2 : 1) : 0);
                    if (aimed && _targetWords.Length > 0) key ^= (long)_targetWords.GetHashCode() << 20;
                    if (key == marker.ShownKey) continue;
                    marker.ShownKey = key;
                    DashStrip.SplitDistance(surfaceDistance, out string dn, out string du);
                    // The aimed body carries the numbers you steer by: how far, and how fast it is coming.
                    string text = aimed && _targetWords.Length > 0
                        ? "[ " + spec.displayName.ToUpperInvariant() + " ]\n" + _targetWords
                        : aimed ? "[ " + spec.displayName.ToUpperInvariant() + "  " + dn + " " + du + " ]"
                        : spec.displayName.ToUpperInvariant() + "  " + dn + " " + du;
                    if (marker.Label.text != text) marker.Label.text = text;
                }
            }
        }

        Marker CreateMarker(BodySpec spec, Color color)
        {
            var root = UiKit.NewRect("Marker " + spec.id, _markerCanvas.transform);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(8f, 8f);
            root.SetAsFirstSibling();
            Color bright = Color.Lerp(color, Color.white, 0.35f);
            bright.a = 1f;
            var marker = new Marker { Root = root, Dot = UiKit.AddImage(root, bright, UiKit.Circle) };
            bool major = spec.kind != BodyKind.Moon && spec.kind != BodyKind.Asteroid;
            marker.Label = UiKit.Halo(UiKit.DashLabel(root, string.Empty, major ? 14 : 12, major ? UiKit.DashCream : UiKit.DashDim,
                UiKit.LabelFont, TextAnchor.UpperCenter));
            marker.Plain = marker.Label.color;
            var labelRect = (RectTransform)marker.Label.transform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.sizeDelta = new Vector2(320f, 20f);
            return marker;
        }
    }
}
