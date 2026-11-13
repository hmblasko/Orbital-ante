using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// What you see while flying the test craft: a name and distance on every world in view, which body you are
    /// near and how high, your speed measured against it, the pull you feel, and a marker showing which way you
    /// are actually drifting (in space that is often not the way you are pointing).
    /// This is a plain stand-in. The proper cockpit dash is Milestone 9.
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
        Text _where;
        Text _readout;
        Text _state;
        Text _help;
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
        readonly InputHintRefresh _helpRefresh = new InputHintRefresh();
        bool _refreshText;
        static readonly Color TargetColor = new Color(0.45f, 0.85f, 1f);

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

            _where = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 26, UiKit.PanelEdge));
            UiKit.Pin((RectTransform)_where.transform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(900f, 34f));

            _readout = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 22, UiKit.Cream, TextAnchor.LowerLeft));
            UiKit.Pin((RectTransform)_readout.transform, new Vector2(0f, 0f), new Vector2(22f, 52f), new Vector2(760f, 110f));

            _state = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 24, UiKit.PanelEdge));
            UiKit.Pin((RectTransform)_state.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1000f, 32f));

            _help = Shadowed(UiKit.Label(_canvas.transform, string.Empty, 16, UiKit.Muted, TextAnchor.LowerLeft, FontStyle.Normal));
            UiKit.Pin((RectTransform)_help.transform, new Vector2(0f, 0f), new Vector2(22f, 10f), new Vector2(1230f, 40f));

            // Drift marker: a ring with a dot, placed where your velocity points.
            _drift = UiKit.NewRect("DriftMarker", _canvas.transform);
            _drift.anchorMin = _drift.anchorMax = _drift.pivot = new Vector2(0.5f, 0.5f);
            _drift.sizeDelta = new Vector2(26f, 26f);
            UiKit.AddImage(_drift, new Color(0.45f, 0.95f, 0.6f, 0.35f), UiKit.Circle);
            var core = UiKit.NewRect("Core", _drift);
            UiKit.Pin(core, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
            UiKit.AddImage(core, new Color(0.45f, 0.95f, 0.6f, 1f), UiKit.Circle);
            _driftLabel = Shadowed(UiKit.Label(_drift, string.Empty, 15, new Color(0.45f, 0.95f, 0.6f)));
            UiKit.Pin((RectTransform)_driftLabel.transform, new Vector2(0.5f, 0f), new Vector2(0f, -20f), new Vector2(200f, 20f));
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
            UpdateDriftMarker();
            UpdateBodyMarkers();
        }

        /// <summary>
        /// Height in plain words. Over real ground it is the height above the ground straight below (or above
        /// the sea), which is the number you need for landing; otherwise the height above the body's round surface.
        /// </summary>
        string DescribeHeight(int anchor, Vector3 position)
        {
            Planets.PlanetWorld planets = Planets.PlanetWorld.Instance;
            if (planets != null)
            {
                Planets.GroundReading ground = planets.Read(anchor, position);
                if (ground.Known)
                {
                    if (ground.Underwater) return SpaceConvert.Distance(-ground.AboveSea) + " under water   " + SpaceConvert.Distance(System.Math.Max(0, ground.AboveGround)) + " above the sea bed";
                    if (ground.InCave) return "in a cave";
                    string label = ground.OverSea ? " above the sea" : " above the ground";
                    double height = System.Math.Max(0, ground.OverSea ? ground.AboveSea : ground.AboveGround);
                    return SpaceConvert.Distance(height) + label + (ground.InsideClouds ? "   (inside the clouds)" : ground.UnderClouds ? "   (under the clouds)" : string.Empty);
                }
            }
            return SpaceConvert.Distance(_system.AltitudeAbove(anchor, position)) + " up";
        }

        void UpdateReadouts()
        {
            SolarSystemModel model = _system.Model;
            int anchor = _system.Frame.Anchor;
            Vector3 position = _craft.transform.position;
            float speed = _craft.Body.linearVelocity.magnitude;
            float pull = _craft.Gravity.FeltGravity.magnitude;
            int target = _craft.Target;

            if (anchor == 0)
            {
                _where.text = "OPEN SPACE";
            }
            else
            {
                _where.text = model.Spec(anchor).displayName.ToUpperInvariant() + "   " + DescribeHeight(anchor, position);
            }

            string against = anchor == 0 ? "the Sun" : model.Spec(anchor).displayName + (_system.Frame.Rotating ? "'s ground" : string.Empty);
            string text =
                "Speed  " + FormatSpeed(speed) + "   against " + against + "\n" +
                "Pull   " + pull.ToString("0.0") + " m/s\u00B2";
            if (target >= 0)
            {
                float closing = _craft.ClosingSpeed;
                text += "\nAimed at " + model.Spec(target).displayName + "   " + SpaceConvert.Distance(_craft.TargetDistance) + "   " +
                        (closing >= 0f ? "closing " : "pulling away ") + FormatSpeed(Mathf.Abs(closing));
            }
            // What the surroundings are doing to you, so nothing slows or pushes the craft without saying so.
            Planets.PlanetWorld planets = Planets.PlanetWorld.Instance;
            if (planets != null && planets.Sky != null && planets.Sky.Surface != null)
            {
                Planets.PlanetSky sky = planets.Sky;
                if (sky.Air.Underwater) text += "\nUnder water: it slows you and holds you up";
                else if (sky.Air.Drag > 0.005) text += "\nThick air: it slows you down";
                if (sky.Weather.Intensity > 0.05) text += "\nWind   " + FormatSpeed((float)sky.Weather.Wind.Magnitude) + "   it carries you sideways";
            }
            _readout.text = text;

            // One line of plain advice, most urgent first.
            bool mustBrake = target >= 0 && _craft.ClosingSpeed > 30f && _craft.StoppingDistance > _craft.TargetDistance * 0.8f;
            if (_craft.MatchingVelocity)
            {
                string who = model.Spec(_craft.MatchBody).displayName;
                _state.text = _craft.RelativeVelocity().magnitude > 0.5f ? "MATCHING VELOCITY WITH " + who.ToUpperInvariant() : "VELOCITY MATCHED WITH " + who.ToUpperInvariant();
                _state.color = UiKit.PanelEdge;
            }
            else if (mustBrake)
            {
                _state.text = "BRAKE NOW:  aim at " + model.Spec(target).displayName + " and hold Match Velocity";
                _state.color = UiKit.Danger;
            }
            else if (pull > _craft.Config.thrust && !_craft.Boosting)
            {
                _state.text = "PULL IS STRONGER THAN YOUR THRUSTERS:  use Boost";
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

            if (GameInput.Asset != null && _helpRefresh.TryRefresh(Time.unscaledTimeAsDouble, GameInput.BindingRevision, GameInput.UsingGamepad))
            {
                // Rebinding refreshes on this text tick; device-only changes are bounded by the shared gate.
                _help.text = "Thrust: " + Key(GameInput.FlyMove) + ", " + Key(GameInput.FlyUp) + " up, " + Key(GameInput.FlyDown) + " down     Roll: " +
                             Key(GameInput.RollLeft) + " / " + Key(GameInput.RollRight) + "     Boost: " + Key(GameInput.Boost) +
                             "     Match velocity with what you aim at: hold " + Key(GameInput.MatchVelocity) + "     Map: " + Key(GameInput.ToggleMap) +
                             "     Pause: " + Key(GameInput.Pause);
            }
        }

        static string Key(UnityEngine.InputSystem.InputAction action) => GameInput.Describe(action);

        static string FormatSpeed(float metresPerSecond)
        {
            return metresPerSecond < 100f ? metresPerSecond.ToString("0.0") + " m/s" : Mathf.RoundToInt(metresPerSecond) + " m/s";
        }

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
