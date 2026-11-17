using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using UnityEngine;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// The ship's floodlights: two lamps on the front of the base that light the ground and anything ahead, so the
    /// ship can see in dark places. When they are on is <see cref="ShipLights"/>; this shows it.
    ///
    /// The game's own shaders are not lit by Unity's lights (see SkyGlobals), so the beam is handed to them as one
    /// cone (<see cref="SkyGlobals.SetBeam"/>, drawn by OA_Beam in OA_Look.hlsl): no extra pass, no shadow map, and
    /// nothing at all while the lights are off. A real Unity spot light stands in only when the "simple shaders"
    /// fallback is in use, the same way the player's lamp does.
    /// </summary>
    public sealed class ShipLightFx : MonoBehaviour
    {
        public static readonly Color Warm = new Color(1f, 0.95f, 0.86f);

        Ship _ship;
        FloodRules _rules;
        readonly ShipLights _lights = new ShipLights();
        System.Func<float> _darkness;
        GameObject[] _faces;
        MeshRenderer[] _faceRenderers;
        Material _faceLit, _faceOut;
        Mesh _faceMesh;
        Light _spot;
        bool _shownOn;

        public ShipLights Lights => _lights;

        /// <summary><paramref name="darkness"/> says how dark it is near the ground right now, 0 to 1.</summary>
        public static ShipLightFx Attach(Ship ship, System.Func<float> darkness)
        {
            var fx = ship.gameObject.AddComponent<ShipLightFx>();
            fx._ship = ship;
            fx._rules = ship.Config != null && ship.Config.floodlights != null ? ship.Config.floodlights : new FloodRules();
            fx._darkness = darkness;
            fx.Build();
            return fx;
        }

        void Build()
        {
            _faceMesh = DiscMesh(14);
            _faceLit = new Material(PlanetMaterials.VertexColour(Warm, 0f, 2f, false)) { name = "OA_FloodlightLit" };
            _faceOut = new Material(PlanetMaterials.VertexColour(new Color(0.32f, 0.33f, 0.34f), 0.4f, 0f, false)) { name = "OA_FloodlightOut" };
            _faces = new GameObject[ShipLights.Lamps.Length];
            _faceRenderers = new MeshRenderer[_faces.Length];
            Vector3 way = V(ShipLights.Direction(_rules));
            for (int i = 0; i < _faces.Length; i++)
            {
                var go = new GameObject("Floodlight" + i);
                go.layer = Layers.Default;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = V(ShipLights.Lamps[i]);
                // Flat on the front face, looking ahead (tilted with the beam its lower edge would sink into the base).
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * ShipLights.LampRadius;
                go.AddComponent<MeshFilter>().sharedMesh = _faceMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _faceOut;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _faces[i] = go;
                _faceRenderers[i] = r;
            }

            var spotGo = new GameObject("FloodlightStandIn");
            spotGo.transform.SetParent(transform, false);
            spotGo.transform.localPosition = V(ShipLights.Origin);
            spotGo.transform.localRotation = Quaternion.LookRotation(way, Vector3.up);
            _spot = spotGo.AddComponent<Light>();
            _spot.type = LightType.Spot;
            _spot.color = Warm;
            _spot.shadows = LightShadows.None;
            _spot.enabled = false;
        }

        /// <summary>The pilot's button: the other way from now, and kept so. Returns true if they are now on.</summary>
        public bool Press() => _lights.Press();

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_ship == null || GameSession.Paused) return;

            Astronaut crew = _ship.Crew;
            bool atControls = crew != null && !crew.Dead && crew.Mode == AstronautMode.Seated;
            if (atControls && GameInput.Floodlights != null && GameInput.Floodlights.WasPressedThisFrame())
                crew.Tell(Press() ? "Floodlights on." : "Floodlights off.");

            float dark = _darkness != null ? Mathf.Clamp01(_darkness()) : 0f;
            _lights.Step(dt, dark, true, _rules);
            float level = _lights.Level;

            bool on = level > 0.01f;
            if (on != _shownOn)
            {
                _shownOn = on;
                for (int i = 0; i < _faceRenderers.Length; i++)
                    if (_faceRenderers[i] != null) _faceRenderers[i].sharedMaterial = on ? _faceLit : _faceOut;
            }
        }

        void LateUpdate()
        {
            if (_ship == null) return;
            float level = _lights.Level;
            Vector3 origin = transform.TransformPoint(V(ShipLights.Origin));
            Vector3 way = transform.TransformDirection(V(ShipLights.Direction(_rules)));
            // Seen from further off than the beam reaches, nothing the eye can see is lit by it: hand the shaders
            // nothing, so they skip it (a parked ship left lit costs nothing while you walk away).
            Camera eye = _ship.Crew != null ? _ship.Crew.Eye : null;
            bool inReach = eye == null || (eye.transform.position - origin).sqrMagnitude < (_rules.reach + 30f) * (_rules.reach + 30f);
            float strength = inReach ? _rules.brightness * level : 0f;
            // Late, after the ship has moved this frame, so the beam never lags behind it.
            SkyGlobals.SetBeam(origin, way, _rules.reach, _rules.innerDegrees, _rules.outerDegrees, Warm, strength);

            bool standIn = !PlanetMaterials.OwnTerrain && level > 0.01f;
            if (_spot.enabled != standIn) _spot.enabled = standIn;
            if (standIn)
            {
                _spot.range = _rules.reach;
                _spot.spotAngle = _rules.outerDegrees * 2f;
                _spot.innerSpotAngle = _rules.innerDegrees * 2f;
                // Unity's lights fall off with the square of distance, so it takes far more to reach the ground ahead.
                _spot.intensity = _rules.brightness * level * 40f;
            }
        }

        static Vector3 V(P3 p) => new Vector3(p.X, p.Y, p.Z);

        /// <summary>A flat round face of radius 1 looking along +Z, brightest in the middle.</summary>
        static Mesh DiscMesh(int sides)
        {
            var vertices = new Vector3[sides + 1];
            var colors = new Color[sides + 1];
            var normals = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            vertices[sides] = Vector3.zero;
            colors[sides] = Color.white;
            normals[sides] = Vector3.forward;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                colors[i] = new Color(0.8f, 0.78f, 0.72f, 1f);
                normals[i] = Vector3.forward;
                // Wound so the face is seen from in front (+Z): Unity shows the side Cross(b - a, c - a) points to.
                triangles[i * 3] = sides;
                triangles[i * 3 + 1] = i;
                triangles[i * 3 + 2] = (i + 1) % sides;
            }
            var mesh = new Mesh { name = "FloodlightFace" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            // Never leave the world lit by a ship that is gone.
            SkyGlobals.SetBeam(Vector3.zero, Vector3.forward, 1f, 20f, 40f, Color.black, 0f);
            if (_faceLit != null) Destroy(_faceLit);
            if (_faceOut != null) Destroy(_faceOut);
            if (_faceMesh != null) Destroy(_faceMesh);
        }
    }
}
