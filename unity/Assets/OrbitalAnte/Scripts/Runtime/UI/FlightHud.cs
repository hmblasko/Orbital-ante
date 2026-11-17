using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// What you see while flying from the seat, in four places (clean-up of 17 Nov 2026; Matthew: too many stats in
    /// plain white text):
    /// - top middle: the world you are near and your height, a big number with its words under it;
    /// - lower left: your speed and what it is measured against, the orbit aid as a bar (only while an orbit is
    ///   what you are doing, <see cref="HudRules.ShowOrbit"/>), and one quiet line for wind, thick air or water;
    /// - lower right: what you are aimed at or locked on, its distance and how fast you close on it;
    /// - above the middle of the bottom: one notice, most urgent first (unchanged).
    /// The pull you feel is in the pause menu's Run Stats (a warning still shows here when it beats your thrusters),
    /// and the list of flying keys shows for the first seconds in the seat, then fades.
    /// Every world in view carries its name and distance, and a marker shows which way you are drifting.
    /// </summary>
    public sealed class FlightHud : MonoBehaviour
    {
        sealed class Marker
        {
            public RectTransform Root;
            public Image Dot;
            public Text Label;
            public Color Plain;
        }

        SolarSystem _system;
        TestCraft _craft;
        Canvas _canvas;
        RectTransform _canvasRect;
        Text _whereName, _whereHeight, _whereWords;
        // Lower left.
        RectTransform _speedGroup, _orbitRow, _orbitFill;
        Text _speedLabel, _speed, _speedUnit, _against, _orbitWords, _env, _sink;
        // Height above what you would land on, or below zero where it is not known (open space, a cave).
        double _groundHeight = -1;
        bool _sinkTooFast;
        float _safeSink;
        // Lower right.
        RectTransform _targetGroup, _targetTag;
        Image _targetTagBack;
        Text _targetTagText, _targetName, _targetDistance, _targetUnit, _targetWords;
        Text _state;
        Text _help;
        float _helpShown;
        RectTransform _drift;
        Text _driftLabel;
        readonly Dictionary<int, Marker> _markers = new Dictionary<int, Marker>();
        // Markers move every frame, so they get a canvas of their own and the steady readouts are left alone.
        Canvas _markerCanvas;
        RectTransform _markerRect;
        // Words are refreshed a few times a second, not every frame: nobody can read faster, and building text
        // every frame leaves a steady trickle of rubbish for the game to clear up.
        const float TextInterval = 0.125f;
        float _nextText;
        float _nextHelp;
        bool _refreshText;
        static readonly Color TargetColor = new Color(0.45f, 0.85f, 1f);
        static readonly Color BarBack = new Color(0f, 0f, 0f, 0.5f);

        /// <summary>The pull you feel right now, in m/s², for Run Stats.</summary>
        public float Pull { get; private set; }
        /// <summary>The last orbit reading (Kind None away from any world), for Run Stats.</summary>
        public Navigation.OrbitReading Orbit { get; private set; }

        public void Build(SolarSystem system, TestCraft craft)
        {
            _system = system;
            _craft = craft;
            Ship.Ship ship = craft.GetComponent<Ship.Ship>();
            _safeSink = ship != null && ship.Model != null ? ship.Model.Rules.safeImpactSpeed : 4f;
            _canvas = UiKit.CreateCanvas("FlightHUD", transform, 9);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;
            _canvasRect = (RectTransform)_canvas.transform;
            _markerCanvas = UiKit.CreateCanvas("FlightMarkers", transform, 8);
            _markerCanvas.GetComponent<GraphicRaycaster>().enabled = false;
            _markerRect = (RectTransform)_markerCanvas.transform;

            // Aiming mark: a small open cross, so the thing you are pointing at is not hidden behind it.
            for (int i = 0; i < 4; i++)
            {
                var tick = UiKit.NewRect("AimTick", _canvas.transform);
                float angle = 90f * i;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = dir * 13f;
                tick.sizeDelta = new Vector2(10f, 2f);
                tick.localRotation = Quaternion.Euler(0f, 0f, angle);
                UiKit.AddImage(tick, new Color(0.96f, 0.91f, 0.80f, 0.8f));
            }

            // Where: the world's name, small and amber, over the height as a big number, over its words.
            // Labels are muted; amber is kept for "act soon" and red for "act now".
            _whereName = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 15, UiKit.Muted));
            UiKit.Pin((RectTransform)_whereName.transform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(600f, 20f));
            _whereHeight = UiKit.Halo(UiKit.Label(_canvas.transform, string.Empty, 30, UiKit.Cream), true);
            UiKit.Pin((RectTransform)_whereHeight.transform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 38f));
            _whereWords = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 14, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal));
            UiKit.Pin((RectTransform)_whereWords.transform, new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(700f, 20f));

            BuildSpeedGroup();
            BuildTargetGroup();

            _state = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 22, UiKit.PanelEdge));
            UiKit.Pin((RectTransform)_state.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1000f, 32f));

            _help = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 15, UiKit.Muted, TextAnchor.LowerCenter, FontStyle.Normal));
            UiKit.Pin((RectTransform)_help.transform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(1240f, 22f));

            // Drift marker: a ring with a dot, placed where your velocity points. It moves every frame, so it lives on
            // the markers' canvas, not with the steady readouts.
            _drift = UiKit.NewRect("DriftMarker", _markerCanvas.transform);
            _drift.anchorMin = _drift.anchorMax = _drift.pivot = new Vector2(0.5f, 0.5f);
            _drift.sizeDelta = new Vector2(26f, 26f);
            UiKit.AddImage(_drift, new Color(0.45f, 0.95f, 0.6f, 0.35f), UiKit.Circle);
            var core = UiKit.NewRect("Core", _drift);
            UiKit.Pin(core, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
            UiKit.AddImage(core, new Color(0.45f, 0.95f, 0.6f, 1f), UiKit.Circle);
            _driftLabel = Shadowed(UiKit.Label(_drift, string.Empty, 15, new Color(0.45f, 0.95f, 0.6f)));
            UiKit.Pin((RectTransform)_driftLabel.transform, new Vector2(0.5f, 0f), new Vector2(0f, -20f), new Vector2(200f, 20f));
        }

        /// <summary>A label in a group, pinned by its lower corner on the group's side; its height is set later by <see cref="Stack"/>.</summary>
        static Text GroupLabel(RectTransform group, bool right, int size, Color colour, FontStyle style = FontStyle.Bold, float width = 420f)
        {
            Text t = Shadowed(UiKit.Label(group, string.Empty, size, colour, right ? TextAnchor.LowerRight : TextAnchor.LowerLeft, style));
            Vector2 corner = new Vector2(right ? 1f : 0f, 0f);
            UiKit.Pin((RectTransform)t.transform, corner, Vector2.zero, new Vector2(width, size + 8f));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void BuildSpeedGroup()
        {
            _speedGroup = UiKit.NewRect("Speed", _canvas.transform);
            UiKit.Pin(_speedGroup, new Vector2(0f, 0f), new Vector2(40f, 34f), new Vector2(460f, 220f));

            _env = GroupLabel(_speedGroup, false, 14, UiKit.Muted, FontStyle.Normal);
            _orbitWords = GroupLabel(_speedGroup, false, 15, UiKit.PanelEdge, FontStyle.Normal);

            // The orbit aid: ORBIT, then a bar of your speed across with a notch where a circle's speed is.
            _orbitRow = UiKit.NewRect("Orbit", _speedGroup);
            UiKit.Pin(_orbitRow, new Vector2(0f, 0f), Vector2.zero, new Vector2(280f, 18f));
            Text orbitName = Shadowed(UiKit.Label(_orbitRow, "ORBIT", 13, UiKit.Muted, TextAnchor.MiddleLeft));
            UiKit.Pin((RectTransform)orbitName.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(60f, 18f));
            RectTransform back = UiKit.NewRect("Bar", _orbitRow);
            UiKit.Pin(back, new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(200f, 6f));
            UiKit.AddImage(back, BarBack, UiKit.Rounded);
            _orbitFill = UiKit.NewRect("Fill", back);
            _orbitFill.anchorMin = new Vector2(0f, 0f);
            _orbitFill.anchorMax = new Vector2(0f, 1f);
            _orbitFill.pivot = new Vector2(0f, 0.5f);
            _orbitFill.offsetMin = Vector2.zero;
            _orbitFill.offsetMax = Vector2.zero;
            UiKit.AddImage(_orbitFill, UiKit.PanelEdge);
            RectTransform notch = UiKit.NewRect("Circle", back);
            UiKit.Pin(notch, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2f, 16f));
            notch.anchoredPosition = new Vector2(200f * HudRules.CircleMark - 1f, 0f);
            UiKit.AddImage(notch, UiKit.Cream);

            _sink = GroupLabel(_speedGroup, false, 15, UiKit.Cream, FontStyle.Normal);
            _against = GroupLabel(_speedGroup, false, 15, UiKit.Muted, FontStyle.Normal);
            _speed = UiKit.Halo(GroupLabel(_speedGroup, false, 44, UiKit.Cream), true);
            _speedUnit = GroupLabel(_speedGroup, false, 18, UiKit.Muted, FontStyle.Bold, 80f);
            _speedLabel = GroupLabel(_speedGroup, false, 13, UiKit.Muted);
            _speedLabel.text = "SPEED";
        }

        void BuildTargetGroup()
        {
            _targetGroup = UiKit.NewRect("Target", _canvas.transform);
            UiKit.Pin(_targetGroup, new Vector2(1f, 0f), new Vector2(-40f, 34f), new Vector2(460f, 220f));

            _targetWords = GroupLabel(_targetGroup, true, 15, UiKit.Muted, FontStyle.Normal);
            _targetDistance = UiKit.Halo(GroupLabel(_targetGroup, true, 40, UiKit.Cream, FontStyle.Bold, 300f), true);
            _targetUnit = GroupLabel(_targetGroup, true, 18, UiKit.Muted, FontStyle.Bold, 60f);
            _targetName = GroupLabel(_targetGroup, true, 20, TargetColor);

            // A small filled tag: LOCKED (bright), AIMED or NEAREST (quiet).
            _targetTag = UiKit.NewRect("Tag", _targetGroup);
            UiKit.Pin(_targetTag, new Vector2(1f, 0f), Vector2.zero, new Vector2(80f, 18f));
            _targetTagBack = UiKit.AddImage(_targetTag, TargetColor, UiKit.Rounded);
            _targetTagText = UiKit.Label(_targetTag, string.Empty, 12, UiKit.Ink);
            UiKit.Stretch((RectTransform)_targetTagText.transform);
            _targetGroup.gameObject.SetActive(false);
        }

        /// <summary>Stacks the shown rows of a group upward from its bottom, so a hidden row leaves no gap.</summary>
        static float Stack(float y, RectTransform row, float height, float gap = 4f)
        {
            if (!row.gameObject.activeSelf) return y;
            row.anchoredPosition = new Vector2(row.anchoredPosition.x, y);
            return y + height + gap;
        }

        static void Show(Component c, bool on)
        {
            if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }

        static Text Shadowed(Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
            if (_markerCanvas != null) _markerCanvas.gameObject.SetActive(visible);
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
            UpdateDriftMarker();
            UpdateBodyMarkers();
        }

        /// <summary>
        /// Height as a number and its words. Over real ground it is the height above the ground straight below (or
        /// above the sea), which is the number you need for landing; otherwise the height above the body's round surface.
        /// </summary>
        void DescribeHeight(int anchor, Vector3 position, out string number, out string words)
        {
            Planets.PlanetWorld planets = Planets.PlanetWorld.Instance;
            if (planets != null)
            {
                Planets.GroundReading ground = planets.Read(anchor, position);
                if (ground.Known)
                {
                    if (ground.Underwater)
                    {
                        number = StatsFormat.Distance(-ground.AboveSea);
                        words = "under water   " + StatsFormat.Distance(System.Math.Max(0, ground.AboveGround)) + " above the sea bed";
                        return;
                    }
                    if (ground.InCave)
                    {
                        number = "IN A CAVE";
                        words = string.Empty;
                        return;
                    }
                    double height = System.Math.Max(0, ground.OverSea ? ground.AboveSea : ground.AboveGround);
                    // Measured to the feet, as the dash does, for the sink warning.
                    _groundHeight = System.Math.Max(0, height + OrbitalAnte.Ship.ShipLayout.GroundY);
                    number = StatsFormat.Distance(height);
                    words = (ground.OverSea ? "above the sea" : "above the ground") +
                            (ground.InsideClouds ? "   inside the clouds" : ground.UnderClouds ? "   under the clouds" : string.Empty);
                    return;
                }
            }
            number = StatsFormat.Distance(_system.AltitudeAbove(anchor, position));
            words = "up";
        }

        /// <summary>Your height in words, for Run Stats ("1.2 km above the ground").</summary>
        public string HeightWords { get; private set; } = string.Empty;
        /// <summary>The world you are near, or "Open space", for Run Stats.</summary>
        public string NearWords { get; private set; } = string.Empty;
        /// <summary>Your speed and what it is against, for Run Stats.</summary>
        public string SpeedWords { get; private set; } = string.Empty;

        static void SetText(Text t, string words)
        {
            if (t.text != words) t.text = words;
        }

        void UpdateReadouts()
        {
            SolarSystemModel model = _system.Model;
            int anchor = _system.Frame.Anchor;
            Vector3 position = _craft.transform.position;
            float speed = _craft.Body.linearVelocity.magnitude;
            float pull = _craft.Gravity.FeltGravity.magnitude;
            Pull = pull;
            int target = _craft.Target;

            // Where.
            _groundHeight = -1;
            if (anchor == 0)
            {
                SetText(_whereName, "OPEN SPACE");
                SetText(_whereHeight, string.Empty);
                SetText(_whereWords, string.Empty);
                NearWords = "Open space";
                HeightWords = string.Empty;
            }
            else
            {
                string name = model.Spec(anchor).displayName;
                DescribeHeight(anchor, position, out string number, out string words);
                SetText(_whereName, name.ToUpperInvariant());
                SetText(_whereHeight, number);
                SetText(_whereWords, words);
                NearWords = name;
                // The first part of the words only ("above the ground", not the clouds as well).
                int gap = words.IndexOf("   ", System.StringComparison.Ordinal);
                string first = gap >= 0 ? words.Substring(0, gap) : words;
                HeightWords = first.Length > 0 && number != "IN A CAVE" ? number + " " + first : number;
            }

            // Speed, lower left.
            string against = anchor == 0 ? "the Sun" : model.Spec(anchor).displayName + (_system.Frame.Rotating ? "'s ground" : string.Empty);
            string speedWords = StatsFormat.Speed(speed);
            int cut = speedWords.LastIndexOf(' ');
            SetText(_speed, speedWords.Substring(0, cut));
            SetText(_speedUnit, speedWords.Substring(cut + 1));
            SetText(_against, "against " + against);
            SpeedWords = speedWords;

            // The orbit aid.
            Orbit = anchor > 0 ? ReadOrbit(anchor, position) : default;
            Navigation.OrbitReading orbit = Orbit;
            bool orbitShown = anchor > 0 && HudRules.ShowOrbit(orbit.Kind, orbit.SideSpeed, orbit.CircleSpeed);
            Show(_orbitRow, orbitShown);
            Show(_orbitWords, orbitShown);
            if (orbitShown)
            {
                _orbitFill.sizeDelta = new Vector2(200f * HudRules.OrbitBar(orbit.SideSpeed, orbit.CircleSpeed), 0f);
                int parent = model.ParentOf(anchor);
                SetText(_orbitWords, HudRules.OrbitWords(orbit.Kind, orbit.LowHeight, orbit.HighHeight, parent == 0 ? "Sun" : model.Spec(parent).displayName));
                _orbitWords.color = HudRules.OrbitTone(orbit.Kind) == StatTone.Plain ? UiKit.Cream : UiKit.PanelEdge;
            }

            // What the surroundings are doing to you, so nothing slows or pushes the craft without saying so.
            string env = string.Empty;
            Planets.PlanetWorld planets = Planets.PlanetWorld.Instance;
            if (planets != null && planets.Sky != null && planets.Sky.Surface != null)
            {
                Planets.PlanetSky sky = planets.Sky;
                if (sky.Air.Underwater) env = "Under water: it slows you and holds you up";
                else if (sky.Air.Drag > 0.005) env = "Thick air slows you down";
                if (sky.Weather.Intensity > 0.05) env += (env.Length > 0 ? "   " : string.Empty) + "Wind " + StatsFormat.Speed((float)sky.Weather.Wind.Magnitude) + " carries you sideways";
            }
            SetText(_env, env);
            Show(_env, env.Length > 0);

            // Climb or sink, near the ground: the number a landing needs (the dash shows it too, but the eye is here).
            float vertical = 0f;
            if (anchor > 0 && _groundHeight >= 0)
            {
                Vector3 up = (position - _system.Bodies[anchor].transform.position).normalized;
                vertical = Vector3.Dot(_craft.Body.linearVelocity, up);
            }
            _sinkTooFast = HudRules.SinkTooFast(_groundHeight, vertical, _safeSink, Cockpit.DashModel.LandingWarningSeconds);
            bool sink = HudRules.ShowSink(_groundHeight, vertical);
            Show(_sink, sink);
            if (sink)
            {
                SetText(_sink, HudRules.SinkWords(vertical));
                _sink.color = _sinkTooFast ? UiKit.Danger : UiKit.Cream;
            }

            float y = Stack(0f, (RectTransform)_env.transform, 18f, 2f);
            y = Stack(y, (RectTransform)_orbitWords.transform, 19f, 2f);
            y = Stack(y, _orbitRow, 18f, 8f);
            y = Stack(y, (RectTransform)_sink.transform, 19f, 2f);
            y = Stack(y, (RectTransform)_against.transform, 19f, 0f);
            float speedY = y;
            y = Stack(y, (RectTransform)_speed.transform, 48f, 0f);
            ((RectTransform)_speedUnit.transform).anchoredPosition = new Vector2(_speed.preferredWidth + 6f, speedY + 8f);
            Stack(y, (RectTransform)_speedLabel.transform, 16f);

            UpdateTarget(model, target);

            // One line of plain advice, most urgent first.
            bool mustBrake = target >= 0 && _craft.ClosingSpeed > 30f && _craft.StoppingDistance > _craft.TargetDistance * 0.8f;
            if (_craft.PilotOn)
            {
                int key = (int)_craft.Order.Phase * 10000 + (int)Mathf.Clamp(_craft.Order.SecondsLeft, 0f, 5999f);
                if (key != _pilotKey)
                {
                    _pilotKey = key;
                    _pilotWords = Navigation.Autopilot.Words(_craft.Order.Phase, _craft.Order.SecondsLeft) + "     touch the thrust to take over";
                }
                _state.text = _pilotWords;
                _state.color = UiKit.PanelEdge;
            }
            else if (Time.unscaledTime - _craft.LastStopTime < 4f && _craft.LastStopWords.Length > 0)
            {
                _state.text = _craft.LastStopWords;
                _state.color = _craft.LastStop == Navigation.PilotStop.Arrived || _craft.LastStop == Navigation.PilotStop.PilotTookOver ? UiKit.PanelEdge : UiKit.Danger;
            }
            else if (_craft.MatchingVelocity)
            {
                string who = model.Spec(_craft.MatchBody).displayName;
                _state.text = _craft.RelativeVelocity().magnitude > 0.5f ? "MATCHING VELOCITY WITH " + who.ToUpperInvariant() : "VELOCITY MATCHED WITH " + who.ToUpperInvariant();
                _state.color = UiKit.PanelEdge;
            }
            else if (mustBrake)
            {
                _state.text = "BRAKE NOW:  aim at " + model.Spec(target).displayName + " and hold Match Velocity (" + Key(GameInput.MatchVelocity) + ")";
                _state.color = UiKit.Danger;
            }
            else if (_sinkTooFast)
            {
                // The same moment the dash's own warning comes on, with the key that saves it.
                _state.text = "COMING DOWN TOO FAST:  thrust up (" + Key(GameInput.FlyUp) + ")";
                _state.color = UiKit.Danger;
            }
            else if (pull > _craft.Config.thrust && !_craft.Boosting)
            {
                _state.text = "PULL IS STRONGER THAN YOUR THRUSTERS:  use Boost (" + Key(GameInput.Boost) + ")";
                _state.color = UiKit.Danger;
            }
            else if (_craft.Boosting && _craft.ThrustInput.sqrMagnitude > 0.01f)
            {
                _state.text = "BOOST";
                _state.color = UiKit.PanelEdge;
            }
            else
            {
                _state.text = string.Empty;
            }
            _mustBrake = mustBrake;
        }

        bool _mustBrake;

        /// <summary>Lower right: what the nose is on. Locked, aimed, or the one Lock would take.</summary>
        void UpdateTarget(SolarSystemModel model, int target)
        {
            int shown = target >= 0 ? target : _craft.NearNose;
            Show(_targetGroup, shown >= 0);
            if (shown < 0) return;

            bool locked = target >= 0 && _craft.LockedTarget >= 0;
            string tag = target < 0 ? "NEAREST" : locked ? "LOCKED" : "AIMED";
            SetText(_targetTagText, tag);
            _targetTagBack.color = locked ? TargetColor : new Color(UiKit.Muted.r, UiKit.Muted.g, UiKit.Muted.b, 0.85f);
            _targetTag.sizeDelta = new Vector2(_targetTagText.preferredWidth + 16f, 18f);
            SetText(_targetName, model.Spec(shown).displayName);
            _targetName.color = target >= 0 ? TargetColor : UiKit.Cream;

            bool distance = target >= 0;
            Show(_targetDistance, distance);
            Show(_targetUnit, distance);
            if (distance)
            {
                StatsFormat.Distance(_craft.TargetDistance, out string number, out string unit);
                SetText(_targetDistance, number);
                SetText(_targetUnit, unit);
                float closing = _craft.ClosingSpeed;
                SetText(_targetWords, (closing >= 0f ? "closing " : "pulling away ") + StatsFormat.Speed(Mathf.Abs(closing)));
                _targetWords.color = _mustBrake ? UiKit.Danger : UiKit.Muted;
            }
            else
            {
                // Nothing dead ahead, but Lock would still take this one: say so, so a rough aim is enough.
                SetText(_targetWords, Key(GameInput.LockTarget) + " locks it");
                _targetWords.color = UiKit.Muted;
            }

            float y = Stack(0f, (RectTransform)_targetWords.transform, 19f, 2f);
            if (distance)
            {
                Stack(y + 6f, (RectTransform)_targetUnit.transform, 20f);
                float unitWidth = _targetUnit.preferredWidth;
                y = Stack(y, (RectTransform)_targetDistance.transform, 44f, 0f);
                ((RectTransform)_targetDistance.transform).anchoredPosition =
                    new Vector2(-unitWidth - 6f, ((RectTransform)_targetDistance.transform).anchoredPosition.y);
            }
            y = Stack(y, (RectTransform)_targetName.transform, 24f, 4f);
            Stack(y, _targetTag, 18f);
        }

        /// <summary>The flying keys: up for the first seconds in the seat each run, then faded (all of them are in the pause menu's Controls).</summary>
        void UpdateHelp()
        {
            if (!GameSession.Paused) _helpShown += Time.unscaledDeltaTime;
            float alpha = HudRules.HelpAlpha(_helpShown);
            Show(_help, alpha > 0f);
            if (alpha <= 0f) return;
            _help.canvasRenderer.SetAlpha(alpha);
            if (GameInput.Asset != null && Time.unscaledTime >= _nextHelp)
            {
                // The key names only change when a control is rebound, so look them up now and then.
                _nextHelp = Time.unscaledTime + 2f;
                bool pad = GameInput.UsingGamepad;
                _help.text = "Thrusters: " + (pad ? "left stick, " : "W A S D, ") + Key(GameInput.FlyUp) + " up, " + Key(GameInput.FlyDown) + " down     " +
                             (pad ? "Right stick looks around (click it to turn the ship), roll " : "Turn the ship: hold " + Key(GameInput.FlySteer) + ", roll ") +
                             Key(GameInput.RollLeft) + " / " + Key(GameInput.RollRight) +
                             "     Boost: " + Key(GameInput.Boost) + "     Match velocity: hold " + Key(GameInput.MatchVelocity) +
                             "     Lock: " + Key(GameInput.LockTarget) + "     Autopilot: " + Key(GameInput.Autopilot) + "     Map: " + Key(GameInput.ToggleMap);
            }
        }

        /// <summary>
        /// The orbit aid (Matthew, 16 Nov 2026): what speed across the world's face holds a circle at this height,
        /// how fast you are really going across it, and where the path you are coasting on goes. Measured against
        /// the world's centre, not its spinning ground, because that is what an orbit is.
        /// </summary>
        Navigation.OrbitReading ReadOrbit(int anchor, Vector3 position)
        {
            Vec3d local = SpaceConvert.ToVec3d(position);
            BodyState[] states = _system.States;
            Vec3d offset = _system.Frame.ToTruePosition(local, states) - states[anchor].Position;
            Vec3d velocity = _system.Frame.ToTrueVelocity(local, SpaceConvert.ToVec3d(_craft.Body.linearVelocity), states) - states[anchor].Velocity;
            return Navigation.OrbitPath.Read(_system.Model, anchor, offset, velocity);
        }

        int _pilotKey = -1;
        string _pilotWords = string.Empty;

        static string Key(UnityEngine.InputSystem.InputAction action) => GameInput.Describe(action);

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
            if (_refreshText) _driftLabel.text = ahead ? "heading" : "coming from";
        }

        Vector2 ToCanvas(Vector3 viewport)
        {
            Rect rect = _canvasRect.rect;
            return new Vector2((viewport.x - 0.5f) * rect.width, (viewport.y - 0.5f) * rect.height);
        }

        void UpdateBodyMarkers()
        {
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
                ((RectTransform)marker.Label.transform).anchoredPosition = new Vector2(0f, -(Mathf.Max(size, 5f) + 14f));
                if (_refreshText)
                {
                    bool aimed = i == target;
                    marker.Label.color = aimed ? TargetColor : marker.Plain;
                    marker.Label.text = aimed
                        ? "[ " + spec.displayName + "  " + SpaceConvert.Distance(surfaceDistance) + " ]"
                        : spec.displayName + "  " + SpaceConvert.Distance(surfaceDistance);
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
            marker.Label = Shadowed(UiKit.Label(root, string.Empty, major ? 17 : 14, major ? UiKit.Cream : UiKit.Muted,
                TextAnchor.MiddleCenter, FontStyle.Normal));
            marker.Plain = marker.Label.color;
            var labelRect = (RectTransform)marker.Label.transform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(260f, 22f);
            return marker;
        }
    }
}
