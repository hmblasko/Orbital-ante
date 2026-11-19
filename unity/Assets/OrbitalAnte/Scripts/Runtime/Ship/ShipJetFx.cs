using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Ship
{
    /// <summary>
    /// The small thrusters' flames: a short bright cone out of each nozzle of the four clusters that is firing.
    /// Which ones fire, and how hard, is <see cref="ShipJets"/>; this only draws it. The big engines' flame for an
    /// upward push is drawn by Launch/LaunchFx.
    ///
    /// Sixteen small cones on the ship, each switched off when its nozzle is idle, so they cost nothing at rest.
    /// They share one mesh and one material.
    /// </summary>
    public sealed class ShipJetFx : MonoBehaviour
    {
        /// <summary>Length of a flame at full strength, in metres.</summary>
        public const float FullLength = 0.95f;
        /// <summary>Radius of a flame at the nozzle's mouth in thick air; in a vacuum it spreads wider.</summary>
        public const float MouthRadius = 0.075f;
        /// <summary>How quickly a flame grows and dies, per second (fast: the real ones are pulses).</summary>
        public const float Response = 18f;

        Ship _ship;
        readonly Transform[] _flames = new Transform[ShipJets.Count];
        readonly float[] _wanted = new float[ShipJets.Count];
        readonly float[] _shown = new float[ShipJets.Count];
        readonly bool[] _working = new bool[ShipJets.Clusters];
        System.Func<int, bool> _isWorking;
        Vector3 _turn;
        Mesh _mesh;
        Material _material;

        public static ShipJetFx Attach(Ship ship)
        {
            var fx = ship.gameObject.AddComponent<ShipJetFx>();
            fx._ship = ship;
            fx.Build();
            return fx;
        }

        void Build()
        {
            _isWorking = cluster => _working[cluster];
            _mesh = ConeMesh(8);
            _material = new Material(PlanetMaterials.VertexColour(new Color(1f, 0.84f, 0.62f), 0f, 2.2f, false));
            _material.name = "OA_ThrusterFlame";
            for (int n = 0; n < ShipJets.Count; n++)
            {
                P3 mouth = ShipJets.Mouth(n);
                P3 way = ShipJets.Way(n);
                var go = new GameObject("JetFlame" + n);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(mouth.X, mouth.Y, mouth.Z);
                // The cone is drawn pointing down its own -Y; turn it to point the way the nozzle does.
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.down, new Vector3(way.X, way.Y, way.Z));
                go.layer = Layers.Default;
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _flames[n] = go.transform;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _ship == null || _ship.Flight == null || _ship.Model == null) return;
            TestCraft flight = _ship.Flight;

            for (int c = 0; c < ShipJets.Clusters; c++)
            {
                int part = _ship.Model.Find(PartKind.Thruster, c);
                _working[c] = part < 0 || _ship.Model.State(part) != PartState.Lost;
            }
            // The turn is read once a physics step and a fast mouse leaves some steps empty; smooth it a little.
            _turn = Vector3.Lerp(_turn, flight.TurnRate, 1f - Mathf.Exp(-25f * dt));
            Vector3 push = flight.Firing * Mathf.Clamp01(_ship.Model.ThrustFactor);
            ShipJets.Drive(new P3(push.x, push.y, push.z), new P3(_turn.x, _turn.y, _turn.z), _isWorking, _wanted);

            float air = _ship.Launch != null ? Mathf.Clamp01(_ship.Launch.Air) : 0f;
            float width = MouthRadius * Mathf.Lerp(1.6f, 1f, air);
            float ease = 1f - Mathf.Exp(-Response * dt);
            for (int n = 0; n < ShipJets.Count; n++)
            {
                _shown[n] = Mathf.Lerp(_shown[n], _wanted[n], ease);
                bool on = _shown[n] > 0.03f;
                Transform flame = _flames[n];
                if (flame.gameObject.activeSelf != on) flame.gameObject.SetActive(on);
                if (!on) continue;
                float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 31f, n * 0.37f);
                float length = FullLength * Mathf.Sqrt(_shown[n]) * flicker;
                flame.localScale = new Vector3(width, length, width);
            }
        }

        static Mesh ConeMesh(int sides)
        {
            var vertices = new Vector3[sides + 1];
            var colors = new Color[sides + 1];
            var triangles = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices[i] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                colors[i] = Color.white;
                triangles[i * 3] = i;
                triangles[i * 3 + 1] = sides;
                triangles[i * 3 + 2] = (i + 1) % sides;
            }
            vertices[sides] = new Vector3(0f, -1f, 0f);
            colors[sides] = new Color(1f, 0.5f, 0.3f, 1f);
            var mesh = new Mesh { name = "ThrusterFlame" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
