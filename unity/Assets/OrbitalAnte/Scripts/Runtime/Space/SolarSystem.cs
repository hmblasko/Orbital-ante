using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// The living solar system. Every physics step it moves each body to where the rules say it is, pulls every
    /// loose object with gravity, and keeps the point of view (see <see cref="ReferenceFrame"/>) on whichever
    /// body the player is near, so the ground under them stands still.
    ///
    /// Runs before other scripts each physics step, so everyone else sees this step's positions.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class SolarSystem : MonoBehaviour
    {
        /// <summary>The player is brought back to Unity's zero point after drifting this far from it (metres).</summary>
        public const float RecenterDistance = 2000f;
        const float StarDistance = 285000f;
        public const float FarClip = 300000f;

        struct Snapshot
        {
            public Vec3d Position;
            public Vec3d Velocity;
            public Vec3d Spin;
        }

        public static SolarSystem Instance { get; private set; }

        public SolarSystemModel Model { get; private set; }
        public ReferenceFrame Frame { get; private set; }
        /// <summary>Every body's true state at <see cref="SimTime"/>.</summary>
        public BodyState[] States { get; private set; }
        public CelestialBody[] Bodies { get; private set; }
        /// <summary>Seconds since the run began, in simulation time.</summary>
        public double SimTime { get; private set; }
        /// <summary>The object the point of view follows: the ship, or the player on foot.</summary>
        public GravityBody Focus { get; set; }
        /// <summary>The camera the sky and sunlight are arranged around.</summary>
        public Camera ViewCamera { get; set; }
        public Light SunLight { get; private set; }
        /// <summary>How many times the player has been brought back to Unity's zero point. For the debug readout.</summary>
        public int RecenterCount { get; private set; }

        /// <summary>
        /// Raised when everything in the world is slid sideways to bring the player back near zero. The argument
        /// is how far everything moved. Effects that leave things behind in the world (trails, smoke) listen
        /// for this so they slide along too.
        /// </summary>
        public event Action<Vector3> WorldShifted;

        readonly List<GravityBody> _movers = new List<GravityBody>();
        readonly List<Snapshot> _snapshots = new List<Snapshot>();
        Transform _stars;

        public static void ResetStatics() => Instance = null;

        public IReadOnlyList<GravityBody> Movers => _movers;

        // ------------------------------------------------------------------ building

        /// <param name="paintedLater">
        /// Ids of bodies whose far-away picture will be painted in the background (see PlanetSurface). They start
        /// as plain-coloured balls. Null for none.
        /// </param>
        public void Build(SolarSystemData data, int seed, ICollection<string> paintedLater = null)
        {
            Instance = this;
            Model = new SolarSystemModel(data.BuildBodies(seed), seed);
            Frame = new ReferenceFrame(Model);
            States = Model.NewStateBuffer();
            SimTime = 0;
            Model.Evaluate(SimTime, States);

            var bodyRoot = new GameObject("Bodies").transform;
            bodyRoot.SetParent(transform, false);
            Bodies = new CelestialBody[Model.Count];
            for (int i = 0; i < Model.Count; i++)
            {
                BodySpec spec = Model.Spec(i);
                Bodies[i] = CelestialBody.Create(bodyRoot, i, spec, paintedLater != null && paintedLater.Contains(spec.id));
            }

            BuildSky();
            PlaceBodies(true);
        }

        void BuildSky()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // A little fill light, so the night side of a planet is dark but not a black hole.
            RenderSettings.ambientLight = SkyGlobals.SpaceAmbient;
            // Remembered once only: on a restart the new solar system is built while the old one still exists,
            // and what it would see here is the old one's space settings, not the originals.
            if (!_skySaved)
            {
                _oldFog = RenderSettings.fog;
                _oldSkybox = RenderSettings.skybox;
                _oldReflection = RenderSettings.reflectionIntensity;
                _skySaved = true;
            }
            RenderSettings.fog = false;
            SkyGlobals.Reset();
            // Space has no sky to reflect. Left on, planets would pick up a bluish sheen on their night sides.
            _changedSky = true;
            RenderSettings.skybox = null;
            RenderSettings.reflectionIntensity = 0f;

            var lightGo = new GameObject("Sunlight");
            lightGo.transform.SetParent(transform, false);
            SunLight = lightGo.AddComponent<Light>();
            SunLight.type = LightType.Directional;
            SunLight.color = SkyGlobals.SunColor;
            SunLight.intensity = SkyGlobals.SunIntensity;
            // No shadow maps yet: hills and night come from which way the ground faces the Sun. The ship
            // milestone decides whether its interior needs real shadows.
            SunLight.shadows = LightShadows.None;

            _stars = new GameObject("Stars").transform;
            _stars.SetParent(transform, false);
            AddStarLayer("WhiteStars", 1100, new Color(0.92f, 0.94f, 1f), 4101);
            AddStarLayer("WarmStars", 320, new Color(1f, 0.82f, 0.62f), 4102);
            AddStarLayer("BlueStars", 260, new Color(0.65f, 0.78f, 1f), 4103);
            // The sky itself, behind the stars. It turns with them, so the galaxy stays fixed in the sky.
            OrbitalAnte.Look.SkyDome.Build(_stars, 20000f, true);
        }

        void AddStarLayer(string layerName, int count, Color color, int seed)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(_stars, false);
            // Sizes chosen to be about one and a half to three pixels across on a Steam Deck screen. Any smaller and the
            // faint ones blink as you turn.
            go.AddComponent<MeshFilter>().sharedMesh = SpaceMeshes.StarField(count, StarDistance, 760f, 1500f, seed);
            var renderer = go.AddComponent<MeshRenderer>();
            // With the game's own shader, stars fade into a daytime sky by themselves (the sky veil).
            renderer.sharedMaterial = PlanetMaterials.OwnBody ? PlanetMaterials.Body(layerName, null, color, 1f, 1f, false, OrbitalAnte.Look.LookConfig.Load().starBrightness) : Mat.Unlit(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Shows or hides the stars. Only needed when Unity's standard shaders are standing in for the game's
        /// own: those cannot fade stars into a daytime sky, so the stars are simply switched off there.
        /// </summary>
        public void SetStarsVisible(bool visible)
        {
            if (_stars != null && _stars.gameObject.activeSelf != visible) _stars.gameObject.SetActive(visible);
        }

        static Material _oldSkybox;
        static bool _oldFog;
        static float _oldReflection;
        static bool _skySaved;
        bool _changedSky;

        void OnDestroy()
        {
            // Put the sky settings back, so the combat arena looks the same whichever test world ran first.
            // Only the last solar system standing puts things back; a newer one is still using them.
            if (_changedSky && _skySaved && (Instance == this || Instance == null))
            {
                RenderSettings.skybox = _oldSkybox;
                RenderSettings.reflectionIntensity = _oldReflection;
                RenderSettings.fog = _oldFog;
                _skySaved = false;
                _oldSkybox = null;
            }
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ loose objects

        public void Register(GravityBody mover)
        {
            if (mover != null && !_movers.Contains(mover)) _movers.Add(mover);
        }

        public void Unregister(GravityBody mover)
        {
            _movers.Remove(mover);
            if (Focus == mover) Focus = null;
        }

        /// <summary>Puts a loose object at a true position with a true velocity (both measured from the Sun).</summary>
        public void Place(GravityBody mover, Vec3d truePosition, Vec3d trueVelocity)
        {
            Register(mover);
            Vector3 position = SpaceConvert.ToVector3(Frame.ToLocalPosition(truePosition, States));
            mover.transform.position = position;
            mover.Body.position = position;
            mover.Body.linearVelocity = SpaceConvert.ToVector3(Frame.ToLocalVelocity(truePosition, trueVelocity, States));
        }

        /// <summary>
        /// Puts a loose object in a circular orbit round a body, <paramref name="altitude"/> metres above its
        /// surface, on the side given by <paramref name="direction"/> (a true direction from the body's centre).
        /// </summary>
        public void PlaceInOrbit(GravityBody mover, int body, double altitude, Vec3d direction)
        {
            double distance = Model.Spec(body).radius + altitude;
            Vec3d axis = Model.SpinAxis(body);
            Vec3d outward = (direction - axis * Vec3d.Dot(direction, axis)).Normalized;
            if (outward.SqrMagnitude < 0.5) outward = axis.AnyPerpendicular();
            Vec3d along = Vec3d.Cross(axis, outward);
            Vec3d position = States[body].Position + outward * distance;
            Vec3d velocity = States[body].Velocity + along * Model.CircularOrbitSpeed(body, distance);
            Place(mover, position, velocity);

            // Face the way it is going, feet toward the body.
            Vector3 forward = SpaceConvert.ToVector3(Frame.ToLocalDirection(along, States));
            Vector3 up = SpaceConvert.ToVector3(Frame.ToLocalDirection(outward, States));
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            mover.transform.rotation = rotation;
            mover.Body.rotation = rotation;
        }

        /// <summary>Makes the point of view follow the focus right now and puts it at Unity's zero point.</summary>
        public void SnapToFocus()
        {
            if (Focus == null) return;
            UpdateFrame(true);
            PlaceBodies(true);
            Physics.SyncTransforms();
        }

        // ------------------------------------------------------------------ the step

        void FixedUpdate()
        {
            if (Model == null) return;
            Step(Time.fixedDeltaTime);
        }

        /// <summary>Advances the solar system by one physics step. Public so tests can drive it directly.</summary>
        public void Step(float dt)
        {
            // States holds this moment (the start of the step), which is also where the loose objects are.
            if (Focus != null && UpdateFrame(false)) PlaceBodies(true);
            ApplyGravity(dt);

            // Then the bodies are sent to where they will be when the step ends, as the loose objects will be.
            SimTime += dt;
            Model.Evaluate(SimTime, States);
            PlaceBodies(false);
        }

        /// <summary>
        /// Chooses the anchor for the focus and recentres if it has wandered far from zero.
        /// Returns true if bodies must be re-placed immediately (the world was slid).
        /// </summary>
        bool UpdateFrame(bool forceRecenter)
        {
            // Note where everything truly is and how it is truly moving, before the point of view changes.
            _snapshots.Clear();
            for (int i = 0; i < _movers.Count; i++)
            {
                Rigidbody rb = _movers[i].Body;
                Vec3d local = SpaceConvert.ToVec3d(rb.position);
                _snapshots.Add(new Snapshot
                {
                    Position = Frame.ToTruePosition(local, States),
                    Velocity = Frame.ToTrueVelocity(local, SpaceConvert.ToVec3d(rb.linearVelocity), States),
                    Spin = Frame.ToTrueSpin(SpaceConvert.ToVec3d(rb.angularVelocity), States),
                });
            }

            Vector3 focusLocal = Focus.Body.position;
            Vec3d focusTrue = Frame.ToTruePosition(SpaceConvert.ToVec3d(focusLocal), States);
            bool changed = Frame.Follow(focusTrue, States);
            bool recenter = forceRecenter || focusLocal.sqrMagnitude > RecenterDistance * RecenterDistance;
            if (recenter) Frame.Recenter(SpaceConvert.ToVec3d(focusLocal));
            if (!changed && !recenter) return false;

            // Same true motion, described from the new point of view.
            for (int i = 0; i < _movers.Count; i++)
            {
                Rigidbody rb = _movers[i].Body;
                Snapshot s = _snapshots[i];
                if (recenter)
                {
                    Vector3 position = SpaceConvert.ToVector3(Frame.ToLocalPosition(s.Position, States));
                    // Setting both stops the renderer from smearing the object across the jump.
                    _movers[i].transform.position = position;
                    rb.position = position;
                }
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = SpaceConvert.ToVector3(Frame.ToLocalVelocity(s.Position, s.Velocity, States));
                    rb.angularVelocity = SpaceConvert.ToVector3(Frame.ToLocalSpin(s.Spin, States));
                }
            }

            if (recenter)
            {
                RecenterCount++;
                WorldShifted?.Invoke(-focusLocal);
            }
            return recenter;
        }

        void ApplyGravity(float dt)
        {
            for (int i = 0; i < _movers.Count; i++)
            {
                GravityBody mover = _movers[i];
                Rigidbody rb = mover.Body;
                if (rb == null || rb.isKinematic) continue;
                Vec3d local = SpaceConvert.ToVec3d(rb.position);
                Vec3d velocity = SpaceConvert.ToVec3d(rb.linearVelocity);
                Vec3d truePosition = Frame.ToTruePosition(local, States);
                // The focus's neighbourhood is the anchor by definition; other objects work out their own.
                mover.Neighbourhood = mover == Focus
                    ? Frame.Anchor
                    : Model.NeighbourhoodOf(truePosition, States, mover.Neighbourhood);
                Vec3d pull = Model.AccelerationAt(truePosition, States, mover.Neighbourhood);
                Vector3 felt = SpaceConvert.ToVector3(Frame.ToLocalAccelerationForStep(local, velocity, pull, States, dt));
                mover.FeltGravity = felt;
                rb.AddForce(felt, ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// Puts every body where it belongs. <paramref name="teleport"/> jumps them there (used when the point of
        /// view changes); otherwise they glide, so they draw smoothly between physics steps.
        /// </summary>
        void PlaceBodies(bool teleport)
        {
            QuatD inverse = Frame.Orientation(States).Inverse;
            Vec3d anchor = States[Frame.Anchor].Position;
            Vec3d origin = Frame.Origin;
            for (int i = 0; i < Bodies.Length; i++)
            {
                Vector3 position = SpaceConvert.ToVector3(inverse.Rotate(States[i].Position - anchor) - origin);
                Quaternion rotation = SpaceConvert.ToQuaternion((inverse * Model.Orientation(i, States[i])).Normalized);
                Rigidbody rb = Bodies[i].Body;
                if (teleport)
                {
                    Bodies[i].transform.SetPositionAndRotation(position, rotation);
                    rb.position = position;
                    rb.rotation = rotation;
                }
                else
                {
                    rb.MovePosition(position);
                    rb.MoveRotation(rotation);
                }
            }
        }

        // ------------------------------------------------------------------ sky and sunlight

        void LateUpdate()
        {
            if (Model == null) return;
            Camera cam = ViewCamera;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;

            // The stars never move in truth, so in Unity's coordinates they turn opposite to the point of view.
            _stars.SetPositionAndRotation(eye, SpaceConvert.ToQuaternion(Frame.Orientation(States).Inverse));

            if (cam != null)
            {
                float pixelsPerRadian = cam.pixelHeight / (cam.fieldOfView * Mathf.Deg2Rad);
                for (int i = 0; i < Bodies.Length; i++)
                {
                    if (Bodies[i].Spec.kind == BodyKind.Asteroid) continue;
                    float distance = Vector3.Distance(eye, Bodies[i].transform.position);
                    float radius = (float)Bodies[i].Spec.radius;
                    Bodies[i].SetDetailFor(distance > radius ? Mathf.Asin(radius / distance) * pixelsPerRadian : 100000f);
                }
            }

            Vector3 fromSun = eye - Bodies[0].transform.position;
            if (fromSun.sqrMagnitude > 1f) SunLight.transform.rotation = Quaternion.LookRotation(fromSun.normalized);
            SkyGlobals.SetSun(Bodies[0].transform.position);
        }

        // ------------------------------------------------------------------ questions other scripts ask

        /// <summary>Where a point in Unity's coordinates truly is, measured from the Sun.</summary>
        public Vec3d TruePosition(Vector3 local) => Frame.ToTruePosition(SpaceConvert.ToVec3d(local), States);

        /// <summary>Height above a body's surface for a point in Unity's coordinates.</summary>
        public double AltitudeAbove(int body, Vector3 local)
        {
            return Vec3d.Distance(TruePosition(local), States[body].Position) - Model.Spec(body).radius;
        }

        /// <summary>A point in Unity's coordinates measured from a body's centre in the body's own turning frame (its north pole is +Y).</summary>
        public Vec3d BodyLocal(int body, Vector3 local)
        {
            Vec3d offset = TruePosition(local) - States[body].Position;
            return Model.Orientation(body, States[body]).Inverse.Rotate(offset);
        }

        /// <summary>
        /// How fast the ground (or air) of a body is moving at a point, in Unity's coordinates: the body's own
        /// travel plus its spin. Standing still on a planet means moving at exactly this velocity.
        /// </summary>
        public Vector3 GroundVelocityAt(int body, Vector3 local)
        {
            Vec3d truePosition = TruePosition(local);
            Vec3d spin = Model.SpinAxis(body) * Model.SpinRate(body);
            Vec3d trueVelocity = States[body].Velocity + Vec3d.Cross(spin, truePosition - States[body].Position);
            return SpaceConvert.ToVector3(Frame.ToLocalVelocity(truePosition, trueVelocity, States));
        }

        /// <summary>How fast a body is moving in Unity's coordinates right now.</summary>
        public Vector3 LocalVelocityOf(int body)
        {
            return SpaceConvert.ToVector3(Frame.ToLocalVelocity(States[body].Position, States[body].Velocity, States));
        }

        /// <summary>
        /// How fast a body's centre is moving as seen from a point in Unity's coordinates. Where the point of view
        /// is turning (close to a spinning body) a far body's centre appears to sweep round; measured at your own
        /// place, two things with the same true velocity read as zero against each other, which is what matching means.
        /// </summary>
        public Vector3 VelocityOfSeenFrom(int body, Vector3 local)
        {
            return SpaceConvert.ToVector3(Frame.ToLocalVelocity(TruePosition(local), States[body].Velocity, States));
        }

        /// <summary>The planet-level body a body belongs to: itself for a planet, its planet for a moon.</summary>
        public int PlanetOf(int body)
        {
            while (body > 0 && Model.ParentOf(body) > 0) body = Model.ParentOf(body);
            return body;
        }
    }
}
