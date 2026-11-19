using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Look;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using UnityEngine;

namespace OrbitalAnte.Cockpit
{
    /// <summary>
    /// The modelled console on the desk and its moving parts: the fuel tube's liquid, the air dial's needle,
    /// the five picture lamps and the throttle's lever. The shapes come from the Console model (tools/blender/build_console.py);
    /// what they show comes from <see cref="ConsoleGauges"/>.
    ///
    /// The body is drawn with the cabin's baked light when the cabin is showing its shipped light (the bake
    /// tool lights a copy of it in LanderCabin), and with the even indoor light otherwise. The moving parts
    /// shine by themselves, so they need no baked light and read in a dark cabin.
    /// </summary>
    public sealed class ConsoleInstruments
    {
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly GameObject[] _lamps = new GameObject[ConsoleGauges.LampCount];
        readonly bool[] _lampShown = new bool[ConsoleGauges.LampCount];
        Material _bodyMaterial, _glowMaterial;
        Transform _fuel, _needle, _throttle;
        Vector3 _needleAxis, _throttleTravel;
        float _needleHalf, _fuelShown = -1f, _airShown = -1f, _throttleShown = -1f;

        /// <summary>Where the model says the middle of the screen is, in ship space.</summary>
        public Vector3 ScreenPlace { get; private set; }
        public bool HasScreenPlace { get; private set; }

        /// <summary>Null if the Console model is missing: the screen then stands on the bare desk as before.</summary>
        public static ConsoleInstruments Build(Transform ship, bool cabinLit)
        {
            ModelFile model = ModelLibrary.Find(ConsoleGauges.Model);
            if (model == null) return null;
            var made = new ConsoleInstruments();

            // The body: the lit copy only fits a cabin showing its shipped light.
            Mesh body = cabinLit ? ModelLibrary.PartMesh(ShipAssembly.BakedFittingsModel, ConsoleGauges.Body) : null;
            bool lit = body != null;
            if (!lit) body = ModelLibrary.PartMesh(ConsoleGauges.Model, ConsoleGauges.Body);
            if (body != null)
            {
                GameObject go = made.Piece(ConsoleGauges.Body, body, ship);
                var r = go.GetComponent<MeshRenderer>();
                // Baked() hands back a material of our own, to be destroyed with the console; the shared ones are not ours.
                if (lit) made._bodyMaterial = PlanetMaterials.Baked(1f);
                r.sharedMaterial = lit ? made._bodyMaterial : ModelLibrary.SharedMaterial();
                // The plain copy stands inside the hull: give it the even indoor light, not the Sun.
                if (!lit) ModelLibrary.SetIndoors(go, true);
            }
            // The throttle's lever: lit like the body it stands on, and slid along its slot. A lit body with no lit
            // lever (an old baked file) gets no lever at all sooner than a flat-lit one standing on a lit console.
            Mesh lever = lit ? ModelLibrary.PartMesh(ShipAssembly.BakedFittingsModel, ConsoleGauges.ThrottlePiece) : ModelLibrary.PartMesh(ConsoleGauges.Model, ConsoleGauges.ThrottlePiece);
            if (lever != null && model.TrySocket(ConsoleGauges.ThrottleIdle, out ModelSocket idle) && model.TrySocket(ConsoleGauges.ThrottleFull, out ModelSocket full))
            {
                GameObject go = made.Piece(ConsoleGauges.ThrottlePiece, lever, ship);
                go.GetComponent<MeshRenderer>().sharedMaterial = lit ? made._bodyMaterial : ModelLibrary.SharedMaterial();
                if (!lit) ModelLibrary.SetIndoors(go, true);
                made._throttle = go.transform;
                made._throttleTravel = V(full) - V(idle);
            }
            if (model.TrySocket(ConsoleGauges.DashSocket, out ModelSocket dash))
            {
                made.ScreenPlace = V(dash);
                made.HasScreenPlace = true;
            }

            // The parts that shine: their own colours, brighter than white so they bloom, and nothing from the sky
            // (they are inside the hull). Baked() hands back a material of our own, destroyed with the console.
            Material glow = made._glowMaterial = PlanetMaterials.Baked(2f);

            // Fuel: the liquid is modelled full. It hangs from a pivot at the empty end that is squeezed along the tube.
            Mesh fuel = ModelLibrary.PartMesh(ConsoleGauges.Model, ConsoleGauges.FuelPiece);
            if (fuel != null && model.TrySocket(ConsoleGauges.FuelBase, out ModelSocket fuelBase) && model.TrySocket(ConsoleGauges.FuelTop, out ModelSocket fuelTop))
            {
                Vector3 from = V(fuelBase), along = V(fuelTop) - from;
                if (along.sqrMagnitude > 1e-8f)
                {
                    made._fuel = made.Pivot("FuelPivot", ship, from, Quaternion.LookRotation(along.normalized, Vector3.up), ConsoleGauges.FuelPiece, fuel, glow);
                }
            }

            // Air: the needle is modelled pointing at empty and turns about the dial's own axis.
            Mesh needle = ModelLibrary.PartMesh(ConsoleGauges.Model, ConsoleGauges.NeedlePiece);
            if (needle != null && model.TrySocket(ConsoleGauges.AirDial, out ModelSocket dial) && model.TrySocket(ConsoleGauges.AirDialOut, out ModelSocket dialOut)
                && model.TrySocket(ConsoleGauges.AirEmpty, out ModelSocket empty) && model.TrySocket(ConsoleGauges.AirHalf, out ModelSocket half))
            {
                Vector3 pivot = V(dial);
                made._needleAxis = (V(dialOut) - pivot).normalized;
                // Which way round the needle goes is read from the model (empty, then half), never assumed.
                made._needleHalf = Vector3.SignedAngle(V(empty) - pivot, V(half) - pivot, made._needleAxis);
                made._needle = made.Pivot("NeedlePivot", ship, pivot, Quaternion.identity, ConsoleGauges.NeedlePiece, needle, glow);
            }

            for (int i = 0; i < ConsoleGauges.LampCount; i++)
            {
                string name = ConsoleGauges.PieceOf((ConsoleLamp)i);
                Mesh mesh = ModelLibrary.PartMesh(ConsoleGauges.Model, name);
                if (mesh == null) continue;
                GameObject go = made.Piece(name, mesh, ship);
                go.GetComponent<MeshRenderer>().sharedMaterial = glow;
                go.SetActive(false);
                made._lamps[i] = go;
            }
            return made;
        }

        /// <summary>Moves the instruments to what the gauges say. Cheap to call every frame: nothing is touched unless it changed.</summary>
        public void Show(ConsoleGauges gauges)
        {
            if (gauges == null) return;
            if (_fuel != null && Mathf.Abs(gauges.FuelLevel - _fuelShown) > 0.0005f)
            {
                _fuelShown = gauges.FuelLevel;
                _fuel.localScale = new Vector3(1f, 1f, Mathf.Clamp(_fuelShown, ConsoleGauges.FuelSliver, 1f));
            }
            if (_needle != null && Mathf.Abs(gauges.AirTurn - _airShown) > 0.0005f)
            {
                _airShown = gauges.AirTurn;
                _needle.localRotation = Quaternion.AngleAxis(_needleHalf * 2f * Mathf.Clamp01(_airShown), _needleAxis);
            }
            if (_throttle != null && Mathf.Abs(gauges.Throttle - _throttleShown) > 0.0005f)
            {
                _throttleShown = gauges.Throttle;
                _throttle.localPosition = _throttleTravel * Mathf.Clamp01(_throttleShown);
            }
            for (int i = 0; i < _lamps.Length; i++)
            {
                if (_lamps[i] == null || _lampShown[i] == gauges.Lamps[i]) continue;
                _lampShown[i] = gauges.Lamps[i];
                _lamps[i].SetActive(_lampShown[i]);
            }
        }

        public void SetShown(bool shown)
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] == null) continue;
                var r = _objects[i].GetComponent<MeshRenderer>();
                if (r != null) r.enabled = shown;
            }
        }

        public void Destroy()
        {
            for (int i = 0; i < _objects.Count; i++) if (_objects[i] != null) Object.Destroy(_objects[i]);
            _objects.Clear();
            if (_bodyMaterial != null) Object.Destroy(_bodyMaterial);
            if (_glowMaterial != null) Object.Destroy(_glowMaterial);
            _bodyMaterial = null;
            _glowMaterial = null;
        }

        /// <summary>A pivot at a place in ship space with the piece hung under it so the piece stays where the model put it.</summary>
        Transform Pivot(string name, Transform ship, Vector3 at, Quaternion facing, string piece, Mesh mesh, Material material)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(ship, false);
            pivot.transform.localPosition = at;
            pivot.transform.localRotation = facing;
            _objects.Add(pivot);
            GameObject go = Piece(piece, mesh, pivot.transform);
            Quaternion back = Quaternion.Inverse(facing);
            go.transform.localRotation = back;
            go.transform.localPosition = back * -at;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return pivot.transform;
        }

        GameObject Piece(string name, Mesh mesh, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = Layers.Default;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;      // library meshes are shared: never destroyed here
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _objects.Add(go);
            return go;
        }

        static Vector3 V(ModelSocket s) => new Vector3(s.X, s.Y, s.Z);
    }
}
