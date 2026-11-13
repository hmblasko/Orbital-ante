using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The live map: the solar system seen from above, drawn fresh every frame from the same numbers that move
    /// the real bodies, so it is always right. The game keeps running behind it.
    /// </summary>
    public sealed class SystemMap : MonoBehaviour
    {
        /// <summary>The drawing surface. Unity asks it for its shapes, and it asks the map.</summary>
        sealed class MapGraphic : Graphic
        {
            public SystemMap Owner;

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                if (Owner != null) Owner.Draw(vh);
            }
        }

        const int OrbitSegments = 72;
        const float MinMetresAcross = 600f;
        const float MaxMetresAcross = 320000f;

        static readonly Color OrbitColor = new Color(0.72f, 0.66f, 0.58f, 0.35f);
        static readonly Color MoonOrbitColor = new Color(0.72f, 0.66f, 0.58f, 0.22f);
        static readonly Color YouColor = new Color(0.45f, 0.95f, 0.6f);

        SolarSystem _system;
        TestCraft _craft;
        Canvas _canvas;
        MapGraphic _graphic;
        RectTransform _area;
        Text _title;
        Text _help;
        Text _scaleText;
        Text _youLabel;
        readonly Dictionary<int, Text> _labels = new Dictionary<int, Text>();

        /// <summary>-1 centres on the craft, otherwise a body index.</summary>
        int _centre;
        float _metresAcross = 270000f;
        float _scaleBarMetres;
        float _shownScaleBar = -1f;
        float _nextHelp;
        int _titleFor = int.MinValue;

        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        public void Build(SolarSystem system, TestCraft craft)
        {
            _system = system;
            _craft = craft;
            _canvas = UiKit.CreateCanvas("SystemMap", transform, 11);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            var backdrop = UiKit.NewRect("Backdrop", _canvas.transform);
            UiKit.Stretch(backdrop);
            UiKit.AddImage(backdrop, new Color(0.05f, 0.045f, 0.07f, 0.93f));

            _area = UiKit.NewRect("Map", _canvas.transform);
            UiKit.Stretch(_area);
            _graphic = _area.gameObject.AddComponent<MapGraphic>();
            _graphic.Owner = this;
            _graphic.raycastTarget = false;

            _title = UiKit.Label(_canvas.transform, string.Empty, 28, UiKit.PanelEdge, TextAnchor.UpperLeft);
            UiKit.Pin((RectTransform)_title.transform, new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(900f, 40f));
            _help = UiKit.Label(_canvas.transform, string.Empty, 18, UiKit.Muted, TextAnchor.LowerCenter, FontStyle.Normal);
            UiKit.Pin((RectTransform)_help.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1200f, 28f));
            _scaleText = UiKit.Label(_canvas.transform, string.Empty, 18, UiKit.Cream, TextAnchor.LowerLeft, FontStyle.Normal);
            UiKit.Pin((RectTransform)_scaleText.transform, new Vector2(0f, 0f), new Vector2(24f, 64f), new Vector2(300f, 24f));
            _youLabel = NewLabel("YOU", 16, YouColor);

            _canvas.gameObject.SetActive(false);
        }

        Text NewLabel(string text, int size, Color color)
        {
            Text label = UiKit.Label(_area, text, size, color, TextAnchor.MiddleLeft, FontStyle.Normal);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(220f, 22f);
            return label;
        }

        /// <summary>Asked before the map opens. If it says no, the map stays shut (and shuts if it was open).</summary>
        public System.Func<bool> CanOpen;
        /// <summary>The words to show when the map will not open.</summary>
        public System.Func<string> WhyNot;
        /// <summary>The last refusal, and when (unscaled time), for whoever shows notices.</summary>
        public string Refused { get => _refused; private set { _refused = value; RefusedTime = Time.unscaledTime; } }
        public float RefusedTime { get; private set; } = -100f;
        string _refused = string.Empty;

        public void SetOpen(bool open)
        {
            if (_canvas == null || open == IsOpen) return;
            _canvas.gameObject.SetActive(open);
            if (_craft != null) _craft.ControlsLocked = open;
            if (open)
            {
                _nextHelp = 0f;
                // Open on wherever is most useful: the planet you are at, or the whole system if you are between.
                int home = _system.PlanetOf(_system.Frame.Anchor);
                Centre(home);
            }
        }

        void Centre(int body)
        {
            _centre = body;
            if (body == 0) _metresAcross = OutermostOrbit() * 2.25f;
            else if (body > 0) _metresAcross = (float)_system.Model.InfluenceRadius(body) * 2.4f;
            else _metresAcross = 3000f;
            _metresAcross = Mathf.Clamp(_metresAcross, MinMetresAcross, MaxMetresAcross);
        }

        float OutermostOrbit()
        {
            double far = 1000;
            SolarSystemModel model = _system.Model;
            for (int i = 1; i < model.Count; i++)
            {
                if (model.ParentOf(i) == 0 && model.Spec(i).orbitRadius > far) far = model.Spec(i).orbitRadius;
            }
            return (float)far;
        }

        void Update()
        {
            if (_system == null || _system.Model == null || GameInput.Asset == null || GameSession.Paused) return;

            if (GameInput.ToggleMap.WasPressedThisFrame())
            {
                if (!IsOpen && CanOpen != null && !CanOpen()) Refused = WhyNot != null ? WhyNot() : string.Empty;
                else SetOpen(!IsOpen);
            }
            else if (IsOpen && CanOpen != null && !CanOpen()) SetOpen(false);
            if (!IsOpen) return;

            // Zoom with forward and back. Each second of holding halves or doubles the view, roughly.
            float zoom = GameInput.FlyMove.ReadValue<Vector2>().y;
            if (Mathf.Abs(zoom) > 0.15f)
            {
                _metresAcross = Mathf.Clamp(_metresAcross * Mathf.Exp(-zoom * 1.6f * Time.unscaledDeltaTime), MinMetresAcross, MaxMetresAcross);
            }

            // The match-velocity button steps the centre: whole system, the planet you are at, you.
            if (GameInput.MatchVelocity.WasPressedThisFrame())
            {
                int home = _system.PlanetOf(_system.Frame.Anchor);
                if (_centre == 0) Centre(home != 0 ? home : -1);
                else if (_centre > 0) Centre(-1);
                else Centre(0);
            }

            if (_centre != _titleFor)
            {
                _titleFor = _centre;
                _title.text = "MAP   centred on " + (_centre < 0 ? "you" : _system.Model.Spec(_centre).displayName);
            }
            if (Time.unscaledTime >= _nextHelp)
            {
                // Looked up now and then, not every frame: it only changes when a control is rebound.
                _nextHelp = Time.unscaledTime + 2f;
                _help.text = (GameInput.UsingGamepad ? "Zoom (up / down): " : "Zoom: ") + GameInput.Describe(GameInput.FlyMove, "Up", "Down") + "     Centre: " + Key(GameInput.MatchVelocity) + "     Close: " + Key(GameInput.ToggleMap) +
                             "     Green ring: the neighbourhood you are in";
            }
            UpdateLabels();
            _graphic.SetVerticesDirty();
        }

        static string Key(UnityEngine.InputSystem.InputAction action) => GameInput.Describe(action);

        // ------------------------------------------------------------------ where things go on the map

        float PixelsPerMetre => _area.rect.height / _metresAcross;

        Vec3d CentreTrue()
        {
            return _centre >= 0 ? _system.States[_centre].Position : _system.TruePosition(_craft.transform.position);
        }

        /// <summary>Looking straight down on the solar system: true X runs across the map and true Z runs up it.</summary>
        Vector2 ToMap(Vec3d truePosition, Vec3d centre)
        {
            float scale = PixelsPerMetre;
            return new Vector2((float)(truePosition.X - centre.X) * scale, (float)(truePosition.Z - centre.Z) * scale);
        }

        static float MinimumDot(BodyKind kind)
        {
            switch (kind)
            {
                case BodyKind.Star: return 8f;
                case BodyKind.GasGiant: return 5.5f;
                case BodyKind.Planet: return 4.5f;
                case BodyKind.Moon: return 2.8f;
                default: return 1.3f;
            }
        }

        bool OrbitWorthDrawing(int body, out float pixels)
        {
            pixels = (float)_system.Model.Spec(body).orbitRadius * PixelsPerMetre;
            return pixels > (_system.Model.ParentOf(body) == 0 ? 10f : 18f);
        }

        void UpdateLabels()
        {
            SolarSystemModel model = _system.Model;
            Vec3d centre = CentreTrue();
            Rect rect = _area.rect;
            for (int i = 0; i < model.Count; i++)
            {
                BodySpec spec = model.Spec(i);
                if (spec.kind == BodyKind.Asteroid) continue;
                Vector2 at = ToMap(_system.States[i].Position, centre);
                bool show = (i == 0 || i == _centre || OrbitWorthDrawing(i, out _)) && rect.Contains(at);
                if (!_labels.TryGetValue(i, out Text label))
                {
                    if (!show) continue;
                    bool major = spec.kind != BodyKind.Moon;
                    label = NewLabel(spec.displayName, major ? 18 : 15, major ? UiKit.Cream : UiKit.Muted);
                    _labels[i] = label;
                }
                if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
                if (!show) continue;
                float dot = Mathf.Max((float)spec.radius * PixelsPerMetre, MinimumDot(spec.kind));
                ((RectTransform)label.transform).anchoredPosition = at + new Vector2(dot + 6f, 0f);
            }

            Vector2 you = ToMap(_system.TruePosition(_craft.transform.position), centre);
            ((RectTransform)_youLabel.transform).anchoredPosition = you + new Vector2(14f, -12f);

            // A scale bar in a round number of metres, about a fifth of the screen wide.
            float target = rect.width * 0.2f / PixelsPerMetre;
            float pow = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(target)));
            float lead = target / pow;
            _scaleBarMetres = (lead >= 5f ? 5f : lead >= 2f ? 2f : 1f) * pow;
            if (!Mathf.Approximately(_scaleBarMetres, _shownScaleBar))
            {
                _shownScaleBar = _scaleBarMetres;
                _scaleText.text = SpaceConvert.Distance(_scaleBarMetres);
            }
        }

        // ------------------------------------------------------------------ drawing

        void Draw(VertexHelper vh)
        {
            if (_system == null || _system.Model == null || _craft == null) return;
            SolarSystemModel model = _system.Model;
            BodyState[] states = _system.States;
            Vec3d centre = CentreTrue();
            Rect rect = _area.rect;
            Rect loose = new Rect(rect.xMin - 40f, rect.yMin - 40f, rect.width + 80f, rect.height + 80f);

            // Orbits first, so the bodies sit on top of them.
            for (int i = 1; i < model.Count; i++)
            {
                if (model.Spec(i).kind == BodyKind.Asteroid) continue;
                if (!OrbitWorthDrawing(i, out _)) continue;
                int parent = model.ParentOf(i);
                Color32 color = parent == 0 ? OrbitColor : MoonOrbitColor;
                Vec3d[] outline = model.OrbitOutline(i, OrbitSegments);
                Vector2 previous = ToMap(states[parent].Position + outline[0], centre);
                for (int s = 1; s <= OrbitSegments; s++)
                {
                    Vector2 next = ToMap(states[parent].Position + outline[s], centre);
                    if (loose.Contains(previous) || loose.Contains(next)) Line(vh, previous, next, 1.5f, color);
                    previous = next;
                }
            }

            float scale = PixelsPerMetre;

            // The neighbourhood you are in, as a faint ring: inside it, only this body and its moons pull on you,
            // and your speed is measured against it. Drawn so the rule is something you can see.
            int anchor = _system.Frame.Anchor;
            if (anchor != 0)
            {
                float reach = (float)model.InfluenceRadius(anchor) * scale;
                if (reach > 12f && reach < rect.width * 3f)
                {
                    Ring(vh, ToMap(states[anchor].Position, centre), reach, 1.5f, new Color(YouColor.r, YouColor.g, YouColor.b, 0.28f));
                }
            }

            for (int i = 0; i < model.Count; i++)
            {
                BodySpec spec = model.Spec(i);
                Vector2 at = ToMap(states[i].Position, centre);
                float radius = Mathf.Max((float)spec.radius * scale, MinimumDot(spec.kind));
                if (!loose.Contains(at) && radius < 60f) continue;
                Color color = _system.Bodies[i].MapColor;
                color.a = 1f;
                if (spec.kind == BodyKind.Asteroid)
                {
                    Quad(vh, at, radius, new Color(0.62f, 0.57f, 0.5f, 0.9f));
                    continue;
                }
                if (spec.ringOuterRadius > 0)
                {
                    Ring(vh, at, Mathf.Max((float)spec.ringOuterRadius * scale, radius + 3f), 1.5f, new Color(color.r, color.g, color.b, 0.7f));
                }
                Disc(vh, at, radius, color, radius > 40f ? 48 : 20);
            }

            // Scale bar, bottom left.
            Vector2 barStart = new Vector2(rect.xMin + 24f, rect.yMin + 58f);
            Vector2 barEnd = barStart + new Vector2(_scaleBarMetres * scale, 0f);
            Line(vh, barStart, barEnd, 2f, UiKit.Cream);
            Line(vh, barStart + new Vector2(0f, -4f), barStart + new Vector2(0f, 4f), 2f, UiKit.Cream);
            Line(vh, barEnd + new Vector2(0f, -4f), barEnd + new Vector2(0f, 4f), 2f, UiKit.Cream);

            // You: an arrowhead pointing the way the nose points, with a line showing which way you are drifting.
            Vector2 you = ToMap(_system.TruePosition(_craft.transform.position), centre);
            Vec3d nose = _system.Frame.ToTrueDirection(SpaceConvert.ToVec3d(_craft.transform.forward), states);
            var heading = new Vector2((float)nose.X, (float)nose.Z);
            heading = heading.sqrMagnitude > 0.0004f ? heading.normalized : Vector2.up;
            Vec3d drift = _system.Frame.ToTrueDirection(SpaceConvert.ToVec3d(_craft.Body.linearVelocity), states);
            var driftFlat = new Vector2((float)drift.X, (float)drift.Z);
            if (driftFlat.magnitude > 1f) Line(vh, you, you + driftFlat.normalized * 26f, 1.5f, new Color(YouColor.r, YouColor.g, YouColor.b, 0.6f));
            Vector2 side = new Vector2(-heading.y, heading.x);
            Triangle(vh, you + heading * 11f, you - heading * 7f + side * 6.5f, you - heading * 7f - side * 6.5f, YouColor);
        }

        static void Vert(VertexHelper vh, Vector2 position, Color32 color)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = position;
            v.color = color;
            vh.AddVert(v);
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
        {
            Vector2 along = b - a;
            if (along.sqrMagnitude < 0.0001f) return;
            Vector2 side = new Vector2(-along.y, along.x).normalized * (width * 0.5f);
            int start = vh.currentVertCount;
            Vert(vh, a - side, color);
            Vert(vh, a + side, color);
            Vert(vh, b + side, color);
            Vert(vh, b - side, color);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        static void Quad(VertexHelper vh, Vector2 centre, float half, Color32 color)
        {
            int start = vh.currentVertCount;
            Vert(vh, centre + new Vector2(-half, -half), color);
            Vert(vh, centre + new Vector2(-half, half), color);
            Vert(vh, centre + new Vector2(half, half), color);
            Vert(vh, centre + new Vector2(half, -half), color);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int start = vh.currentVertCount;
            Vert(vh, a, color);
            Vert(vh, b, color);
            Vert(vh, c, color);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        static void Disc(VertexHelper vh, Vector2 centre, float radius, Color32 color, int segments)
        {
            int start = vh.currentVertCount;
            Vert(vh, centre, color);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vert(vh, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color);
            }
            for (int i = 0; i < segments; i++) vh.AddTriangle(start, start + 1 + i, start + 2 + i);
        }

        static void Ring(VertexHelper vh, Vector2 centre, float radius, float width, Color32 color)
        {
            const int segments = 40;
            Vector2 previous = centre + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector2 next = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Line(vh, previous, next, width, color);
                previous = next;
            }
        }
    }
}
