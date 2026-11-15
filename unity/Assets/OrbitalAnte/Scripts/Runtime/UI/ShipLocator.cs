using OrbitalAnte.Core;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Keeps the player's own ship findable after leaving it, including through terrain and behind the view.
    /// The geometry and spacing are plain stand-ins for the lead designer's HUD presentation review.
    /// </summary>
    public sealed class ShipLocator : MonoBehaviour
    {
        const float TextInterval = 0.25f;
        // Trial Deck spacing: the whole label fits inside the view; the lower suit readout stays clear.
        const float SideMargin = 104f, TopMargin = 104f, BottomMargin = 216f;
        static readonly Vector2 LabelSize = new Vector2(184f, 32f);

        Astronaut _astronaut;
        Ship.Ship _homeShip;
        SolarSystemWorld _world;
        SolarSystem _system;
        Canvas _canvas;
        RectTransform _canvasRect, _marker, _labelRect;
        CanvasRenderer _shape;
        Text _label;
        Mesh _targetMesh, _pointerMesh;
        ShipLocatorDistance _shownDistance;
        float _nextText;
        bool _edge, _unavailable;

        public Ship.Ship TrackedShip => _homeShip;
        public bool Shown => _canvas != null && _canvas.gameObject.activeSelf;

        public static ShipLocator Attach(Astronaut astronaut)
        {
            if (astronaut == null) return null;
            ShipLocator locator = astronaut.GetComponent<ShipLocator>();
            if (locator != null) return locator;
            locator = astronaut.gameObject.AddComponent<ShipLocator>();
            locator._astronaut = astronaut;
            locator._homeShip = astronaut.Ship;
            // Look these up once. Boarding another ship must not change which ship the locator remembers.
            locator._world = astronaut.GetComponentInParent<SolarSystemWorld>();
            locator._system = locator._world != null ? locator._world.System : SolarSystem.Instance;
            locator.Build();
            return locator;
        }

        /// <summary>An explicit ownership change; boarding somebody else's cabin does not retarget the locator.</summary>
        public void Track(Ship.Ship ship)
        {
            _homeShip = ship;
            _shownDistance = default;
            _nextText = 0f;
        }

        void Build()
        {
            _canvas = UiKit.CreateCanvas("ShipLocator", transform, 9);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;
            _canvasRect = (RectTransform)_canvas.transform;
            _marker = UiKit.NewRect("ShipBearing", _canvas.transform);
            UiKit.Pin(_marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            _shape = _marker.gameObject.AddComponent<CanvasRenderer>();
            _shape.materialCount = 1;
            _shape.SetMaterial(Graphic.defaultGraphicMaterial, 0);
            _shape.SetTexture(Texture2D.whiteTexture);
            _targetMesh = MakeShape(false);
            _pointerMesh = MakeShape(true);
            _shape.SetMesh(_targetMesh);

            _labelRect = UiKit.NewRect("ShipDistance", _canvas.transform);
            UiKit.Pin(_labelRect, new Vector2(0.5f, 0.5f), Vector2.zero, LabelSize);
            UiKit.AddImage(_labelRect, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.88f), UiKit.Rounded);
            _label = UiKit.Label(_labelRect, string.Empty, 18, UiKit.Cream);
            UiKit.Stretch((RectTransform)_label.transform);
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _canvas.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_canvas == null) return;
            bool show = _astronaut != null && !_astronaut.Dead && _astronaut.Eye != null
                && _astronaut.Eye.isActiveAndEnabled && GameSession.Mode == GameMode.Playing && !GameSession.Paused
                && !OrbitalAnte.Cockpit.CockpitDash.WatchingScreen && (_world == null || _world.Map == null || !_world.Map.IsOpen);
            if (show)
            {
                // The actual cabin parent is authoritative if the player boards a different ship later.
                bool inOwnShip = _homeShip != null && _astronaut.transform.IsChildOf(_homeShip.transform)
                    && _astronaut.Mode != AstronautMode.Outside;
                show = !inOwnShip && _astronaut.Mode != AstronautMode.Lift && _astronaut.Mode != AstronautMode.Seated;
            }
            if (_canvas.gameObject.activeSelf != show) _canvas.gameObject.SetActive(show);
            if (!show) return;

            Rect view = _astronaut.Eye.rect;
            Rect canvas = _canvasRect.rect;
            float width = canvas.width * view.width, height = canvas.height * view.height;
            Vector2 viewCentre = new Vector2((view.center.x - 0.5f) * canvas.width, (view.center.y - 0.5f) * canvas.height);
            bool available = _homeShip != null && _homeShip.isActiveAndEnabled && _homeShip.Gravity != null
                && _homeShip.Gravity.isActiveAndEnabled && _homeShip.Gravity.Body != null
                && _system != null && _system.Frame != null && _system.States != null;
            if (!available)
            {
                Unavailable(viewCentre, height);
                return;
            }

            Transform eye = _astronaut.Eye.transform;
            // Both positions use the rendered transforms: mixing the latest physics position with interpolated
            // eyes would invent a separation when the ship and suit share a high velocity. Convert through the
            // current double-precision frame, then treat the difference as a direction across origin shifts.
            Vec3d target = _system.TruePosition(_homeShip.Gravity.transform.position);
            Vec3d observer = _system.TruePosition(eye.position);
            Vec3d localDelta = _system.Frame.ToLocalDirection(target - observer, _system.States);
            Vec3d eyeDelta = new Vec3d(Vec3d.Dot(localDelta, SpaceConvert.ToVec3d(eye.right)),
                Vec3d.Dot(localDelta, SpaceConvert.ToVec3d(eye.up)), Vec3d.Dot(localDelta, SpaceConvert.ToVec3d(eye.forward)));
            ShipLocatorPoint point = ShipLocatorMath.Project(eyeDelta, _astronaut.Eye.fieldOfView, width, height,
                SideMargin, SideMargin, BottomMargin, TopMargin);
            ShipLocatorDistance distance = ShipLocatorMath.Distance(Vec3d.Distance(observer, target));
            if (!point.Valid || !distance.Valid)
            {
                Unavailable(viewCentre, height);
                return;
            }

            if (_unavailable)
            {
                _unavailable = false;
                _marker.gameObject.SetActive(true);
                _labelRect.sizeDelta = LabelSize;
                _shownDistance = default;
                _nextText = 0f;
            }
            bool edge = !point.AtTarget;
            if (_edge != edge)
            {
                _edge = edge;
                _shape.SetMesh(edge ? _pointerMesh : _targetMesh);
            }
            Vector2 at = viewCentre + new Vector2((float)point.X, (float)point.Y);
            _marker.anchoredPosition = at;
            _marker.localRotation = edge ? Quaternion.Euler(0f, 0f, (float)point.Angle) : Quaternion.identity;
            // At the lower edge the words sit above the pointer, clear of the suit's injury readout.
            _labelRect.anchoredPosition = at + new Vector2(0f, point.Y < 0 ? 32f : -32f);

            float now = Time.unscaledTime;
            if (now < _nextText) return;
            _nextText = now + TextInterval;
            if (!_shownDistance.Equals(distance))
            {
                _shownDistance = distance;
                _label.text = "SHIP  " + distance;
            }
        }

        void Unavailable(Vector2 viewCentre, float height)
        {
            if (!_unavailable)
            {
                _unavailable = true;
                _marker.gameObject.SetActive(false);
                _labelRect.sizeDelta = new Vector2(280f, 32f);
                _label.text = "SHIP SIGNAL UNAVAILABLE";
            }
            // A status, never a marker at a remembered or invented position.
            _labelRect.anchoredPosition = viewCentre + new Vector2(0f, height * 0.5f - TopMargin);
        }

        static Mesh MakeShape(bool pointer)
        {
            Vector3[] points = pointer
                ? new[] { new Vector3(11, 0, 0), new Vector3(-5, 10, 0), new Vector3(-9, 7, 0), new Vector3(3, 0, 0), new Vector3(-9, -7, 0), new Vector3(-5, -10, 0) }
                : new[] { new Vector3(0, 11, 0), new Vector3(11, 0, 0), new Vector3(0, -11, 0), new Vector3(-11, 0, 0),
                    new Vector3(0, 7, 0), new Vector3(7, 0, 0), new Vector3(0, -7, 0), new Vector3(-7, 0, 0) };
            int[] faces = pointer ? new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 }
                : new[] { 0, 1, 4, 1, 5, 4, 1, 2, 5, 2, 6, 5, 2, 3, 6, 3, 7, 6, 3, 0, 7, 0, 4, 7 };
            var vertices = new Vector3[points.Length * 2];
            var colours = new Color32[vertices.Length];
            var triangles = new int[faces.Length * 2];
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < points.Length; i++)
            {
                vertices[i] = points[i] + new Vector3(1.5f, -1.5f, 0f);
                vertices[i + points.Length] = points[i];
                colours[i] = new Color(0f, 0f, 0f, 0.9f);
                colours[i + points.Length] = UiKit.Cream;
            }
            for (int i = 0; i < faces.Length; i++)
            {
                triangles[i] = faces[i];
                triangles[i + faces.Length] = faces[i] + points.Length;
            }
            var mesh = new Mesh { name = pointer ? "ShipLocatorPointer" : "ShipLocatorTarget" };
            mesh.vertices = vertices;
            mesh.colors32 = colours;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDisable()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            // Only the two meshes belong to this component. UiKit's sprites and the UI material are shared.
            if (_targetMesh != null) Destroy(_targetMesh);
            if (_pointerMesh != null) Destroy(_pointerMesh);
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
