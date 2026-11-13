using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// First-person flight controller shared by the test craft and the ship. Manual flight keeps real inertia;
    /// target lock, velocity matching and autopilot apply bounded thruster acceleration rather than setting velocity.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TestCraft : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public GravityBody Gravity { get; private set; }
        /// <summary>The camera used to work out what the nose is pointing at. The ship sets this to the pilot's eyes.</summary>
        public Camera Eye { get; set; }
        public TestCraftConfig Config { get; private set; }

        /// <summary>The push being asked for right now, in the craft's own axes, each from -1 to 1.</summary>
        public Vector3 ThrustInput { get; private set; }
        public bool Boosting { get; private set; }
        public bool MatchingVelocity { get; private set; }
        /// <summary>Set by the map while it is open, so flying controls do nothing behind it.</summary>
        public bool ControlsLocked
        {
            get => _controlsLocked;
            set { _controlsLocked = value; if (value) SuspendControls("Assist canceled: map open."); }
        }
        /// <summary>Set by the ship while nobody is buckled into the seat: the controls do nothing.</summary>
        public bool Unmanned
        {
            get => _unmanned;
            set { _unmanned = value; if (value) SuspendControls("Assist canceled: pilot left the seat."); }
        }
        /// <summary>How much of full up-and-down thrust is available (damaged engines give less). 0 to 1.</summary>
        public float LiftScale { get; set; } = 1f;
        /// <summary>How much of full turning and sideways thrust is available (damaged thrusters give less). 0 to 1.</summary>
        public float SideScale { get; set; } = 1f;
        bool Locked => ControlsLocked || Unmanned;

        /// <summary>Set by whoever builds the craft, so it can ask where bodies are and how they are moving.</summary>
        public SolarSystem System { get; set; }
        /// <summary>The explicitly locked body, or -1. Looking away never changes it.</summary>
        public int Target => _navigation.Target;
        /// <summary>The candidate under the aiming mark, separate from the persistent lock.</summary>
        public int AimedTarget { get; private set; } = -1;
        public FlightAssistMode AssistMode => _navigation.Mode;
        public bool VelocityLocked => _navigation.Mode == FlightAssistMode.VelocityLock;
        public bool AutopilotActive => _navigation.Mode == FlightAssistMode.Autopilot;
        public bool AutopilotArrived => _navigation.Arrived;
        public AutopilotPhase ApproachPhase { get; private set; }
        public string NavigationNotice { get; private set; } = string.Empty;
        public float NavigationNoticeTime { get; private set; } = -100f;
        /// <summary>Speed toward the target in metres per second. Negative when moving apart.</summary>
        public float ClosingSpeed { get; private set; }
        /// <summary>Distance to the target's surface in metres.</summary>
        public float TargetDistance { get; private set; }
        /// <summary>How far the craft would travel before Match Velocity finished matching the target, in metres.</summary>
        public float StoppingDistance { get; private set; }

        /// <summary>The body Match Velocity is measured against: the target if there is one, otherwise the nearby body.</summary>
        public int MatchBody => _navigation.ReferenceBody >= 0 ? _navigation.ReferenceBody
            : _heldMatchBody >= 0 ? _heldMatchBody : Target >= 0 ? Target : System != null && System.Frame != null ? System.Frame.Anchor : 0;
        /// <summary>The shared speed/drift reference follows ground spin only close to the selected body.</summary>
        public bool MatchGround => BodyExists(MatchBody) && NearGround(MatchBody, System.TruePosition(Body.position), System.States);

        readonly FlightNavigation _navigation = new FlightNavigation();
        bool _controlsLocked, _unmanned;
        Vector3 _manualThrust;
        Vector3 _assistThrust;
        int _heldMatchBody = -1;
        bool _matchNeedsRelease;

        Vector2 _lookPending;
        AudioSource _thrusterSound;
        /// <summary>The craft's own code-made rumble, and only that: the ship carries other sounds on the same object.</summary>
        public AudioSource ThrusterSound => _thrusterSound;
        float _thrusterLevel;

        void OnEnable() => GameSession.PauseChanged += OnPauseChanged;

        void OnDisable()
        {
            GameSession.PauseChanged -= OnPauseChanged;
            SuspendControls("Assist canceled: flight controls disabled.");
        }

        void OnPauseChanged(bool paused)
        {
            if (paused) ClearInput();
        }

        void SuspendControls(string reason)
        {
            if (_navigation.Mode != FlightAssistMode.Manual) CancelAssist(reason);
            AimedTarget = -1;
            ClearInput();
        }

        public static TestCraft Create(Transform parent, TestCraftConfig config)
        {
            var go = new GameObject("TestCraft");
            go.transform.SetParent(parent, false);
            go.layer = Layers.Player;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 800f;
            rb.linearDamping = 0f;      // Space: nothing slows you down.
            rb.angularDamping = 0f;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Fast enough to pass through a small moon between two physics steps unless this is on. It has to be the
            // "speculative" kind: planets are moved by script (kinematic), and the other kinds ignore those.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var hull = go.AddComponent<SphereCollider>();
            hull.radius = 1.6f;
            var material = new PhysicsMaterial("TestCraftHull") { bounciness = 0.15f, dynamicFriction = 0.9f, staticFriction = 0.9f };
            hull.material = material;

            var craft = go.AddComponent<TestCraft>();
            craft.Config = config;
            craft.Body = rb;
            craft.Gravity = go.AddComponent<GravityBody>();

            var eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(go.transform, false);
            craft.Eye = eyeGo.AddComponent<Camera>();
            craft.Eye.fieldOfView = config.fieldOfView;
            craft.Eye.nearClipPlane = 0.2f;
            craft.Eye.farClipPlane = SolarSystem.FarClip;
            craft.Eye.clearFlags = CameraClearFlags.SolidColor;
            craft.Eye.backgroundColor = Color.black;
            eyeGo.AddComponent<AudioListener>();

            craft.BuildSound();
            return craft;
        }

        /// <summary>
        /// Adds the flying controls to something that already has a body and its own shape (the ship). It brings
        /// no camera or collider of its own.
        /// </summary>
        public static TestCraft Attach(GameObject go, TestCraftConfig config)
        {
            var craft = go.AddComponent<TestCraft>();
            craft.Config = config;
            craft.Body = go.GetComponent<Rigidbody>();
            craft.Gravity = go.GetComponent<GravityBody>();
            if (craft.Gravity == null) craft.Gravity = go.AddComponent<GravityBody>();
            craft.BuildSound();
            return craft;
        }

        void BuildSound()
        {
            // A soft rumble made from filtered noise, as a placeholder for the real thruster sound.
            const int rate = 22050;
            var data = new float[rate * 2];
            var rng = new SeededRandom(77);
            float low = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                low += (rng.Range(-1f, 1f) - low) * 0.06f;
                data[i] = low * 2.2f;
            }
            // Blend the end into the start so the loop has no click.
            const int blend = 2000;
            for (int i = 0; i < blend; i++)
            {
                float t = i / (float)blend;
                data[data.Length - blend + i] = Mathf.Lerp(data[data.Length - blend + i], data[i], t);
            }
            var clip = AudioClip.Create("ThrusterRumble", data.Length, 1, rate, false);
            clip.SetData(data, 0);

            _thrusterSound = gameObject.AddComponent<AudioSource>();
            _thrusterSound.clip = clip;
            _thrusterSound.loop = true;
            _thrusterSound.spatialBlend = 0f;
            _thrusterSound.volume = 0f;
            _thrusterSound.playOnAwake = false;
            _thrusterSound.Play();
        }

        void Update()
        {
            if (GameInput.Asset == null || Config == null) return;
            ValidateNavigation();
            if (GameSession.Paused)
            {
                ClearInput();
                return;
            }
            if (Locked)
            {
                if (_navigation.Mode != FlightAssistMode.Manual) CancelAssist(ControlsLocked ? "Assist canceled: map open." : "Assist canceled: pilot left the seat.");
                AimedTarget = -1;
                ClearInput();
            }
            else
            {
                UpdateAimedTarget();
                Vector2 move = GameInput.FlyMove.ReadValue<Vector2>();
                float lift = (GameInput.FlyUp.IsPressed() ? 1f : 0f) - (GameInput.FlyDown.IsPressed() ? 1f : 0f);
                _manualThrust = Vector3.ClampMagnitude(new Vector3(move.x, lift, move.y), 1f);
                bool manual = _manualThrust.magnitude > Mathf.Max(0.01f, Config.assistCancelInput);
                if (GameInput.LockTarget.WasPressedThisFrame()) ToggleTargetLock();
                if (GameInput.Autopilot.WasPressedThisFrame()) ToggleAutopilot();
                if (GameInput.VelocityLock.WasPressedThisFrame()) ToggleVelocityLock();
                if (manual && _navigation.Mode != FlightAssistMode.Manual) CancelAssist("Assist canceled: manual thrust.");

                bool matchPressed = GameInput.MatchVelocity.IsPressed();
                if (!matchPressed) _matchNeedsRelease = false;
                bool held = !manual && matchPressed && !_matchNeedsRelease;
                if (held && _navigation.Mode == FlightAssistMode.Autopilot) CancelAssist("Autopilot canceled: matching velocity.");
                if (held && _heldMatchBody < 0) _heldMatchBody = Target >= 0 ? Target : AimedTarget >= 0 ? AimedTarget
                    : System != null && System.Frame != null ? System.Frame.Anchor : -1;
                if (!held) _heldMatchBody = -1;
                MatchingVelocity = held || VelocityLocked;
                Boosting = _navigation.Mode == FlightAssistMode.Manual && !held && GameInput.Boost.IsPressed();
                ThrustInput = _navigation.Mode != FlightAssistMode.Manual || held ? _assistThrust : _manualThrust;

                // Steering stays manual during translational autopilot; it never snaps the first-person camera.
                Vector2 look = GameInput.FlyLook.ReadValue<Vector2>();
                var control = GameInput.FlyLook.activeControl;
                bool mouse = control != null && control.device is UnityEngine.InputSystem.Pointer;
                if (mouse) _lookPending += look * Config.mouseSensitivity;
                else _lookPending += look * (Config.stickTurnSpeed * Time.unscaledDeltaTime);
            }
            UpdateTargetReadouts();
            float target = ThrustInput.magnitude * (Boosting ? 1f : 0.55f);
            if (MatchingVelocity && RelativeVelocity().sqrMagnitude > 0.04f) target = Mathf.Max(target, 0.8f);
            _thrusterLevel = Mathf.MoveTowards(_thrusterLevel, target, Time.unscaledDeltaTime * 5f);
            _thrusterSound.volume = _thrusterLevel * Config.thrusterVolume;
            _thrusterSound.pitch = 0.8f + _thrusterLevel * 0.5f;
        }

        void ClearInput()
        {
            _manualThrust = Vector3.zero;
            _assistThrust = Vector3.zero;
            ThrustInput = Vector3.zero;
            _lookPending = Vector2.zero;
            _heldMatchBody = -1;
            Boosting = false;
            MatchingVelocity = false;
        }

        void Say(string message)
        {
            NavigationNotice = message;
            NavigationNoticeTime = Time.unscaledTime;
        }

        void CancelAssist(string message)
        {
            _navigation.CancelAssist();
            _assistThrust = Vector3.zero;
            ThrustInput = Vector3.zero;
            MatchingVelocity = false;
            Boosting = false;
            ApproachPhase = AutopilotPhase.None;
            Say(message);
        }

        bool BodyExists(int body) => System != null && System.Model != null && System.Frame != null
            && body >= 0 && body < System.Model.Count;

        bool DestinationExists(int body) => BodyExists(body) && System.Model.Spec(body).kind != BodyKind.Star
            && System.Model.Spec(body).kind != BodyKind.Asteroid;

        void ValidateNavigation()
        {
            int oldTarget = Target;
            FlightAssistMode oldMode = AssistMode;
            _navigation.Validate(System != null && System.Model != null ? System.Model.Count : 0);
            if ((Target >= 0 && !DestinationExists(Target))) _navigation.ToggleTarget(-1);
            if (oldTarget != Target || oldMode != AssistMode)
            {
                _assistThrust = Vector3.zero;
                ThrustInput = Vector3.zero;
                ApproachPhase = AutopilotPhase.None;
                Say("Navigation canceled: target is no longer available.");
            }
            if (!BodyExists(_heldMatchBody)) _heldMatchBody = -1;
        }

        /// <summary>Same entry point used by the input action and future cockpit controls.</summary>
        public bool ToggleTargetLock()
        {
            if (Locked || GameSession.Paused) return false;
            UpdateAimedTarget();
            _navigation.ToggleTarget(AimedTarget);
            // Explicitly changing/clearing a target cancels the captured hold too. A still-held button must
            // be released before it can capture another body, including after pause or opening the map.
            if (_heldMatchBody >= 0 || (GameInput.Asset != null && GameInput.MatchVelocity.IsPressed())) _matchNeedsRelease = true;
            _heldMatchBody = -1;
            _assistThrust = Vector3.zero;
            ThrustInput = Vector3.zero;
            MatchingVelocity = false;
            Boosting = false;
            ApproachPhase = AutopilotPhase.None;
            Say(Target >= 0 ? "Target locked: " + System.Model.Spec(Target).displayName
                : AimedTarget < 0 ? "Target cleared. Aim at a planet or moon to lock." : "Target cleared.");
            UpdateTargetReadouts();
            return Target >= 0;
        }

        public bool ToggleAutopilot()
        {
            if (Locked || GameSession.Paused) return false;
            if (AutopilotActive) { CancelAssist("Autopilot canceled."); return false; }
            if (!DestinationExists(Target)) { Say("Lock a planet or moon before engaging autopilot."); return false; }
            if (!TargetTerrainReady()) { Say("Autopilot unavailable: destination terrain is still loading."); return false; }
            if (!(AvailableAuthority(true) > 0)) { Say("Autopilot unavailable: no thruster authority."); return false; }
            if (RouteBlocked(out int blocker)) { Say("Autopilot blocked by " + System.Model.Spec(blocker).displayName + ". Fly clear first."); return false; }
            _heldMatchBody = -1;
            _navigation.ToggleAutopilot();
            Say("Autopilot to " + System.Model.Spec(Target).displayName + ". Manual thrust cancels.");
            return true;
        }

        public bool ToggleVelocityLock()
        {
            if (Locked || GameSession.Paused) return false;
            if (VelocityLocked) { CancelAssist("Velocity lock released."); return false; }
            ValidateNavigation();
            if (System == null || System.Model == null || System.Frame == null || !(AvailableAuthority(false) > 0))
            { Say("Velocity lock unavailable: navigation or thrusters unavailable."); return false; }
            _heldMatchBody = -1;
            bool enabled = _navigation.ToggleVelocityLock(System.Frame.Anchor);
            if (enabled) Say("Velocity locked to " + System.Model.Spec(MatchBody).displayName + ". Manual thrust cancels.");
            return enabled;
        }

        /// <summary>Velocity relative to the selected body, evaluated at the craft rather than the body's centre.</summary>
        public Vector3 RelativeVelocity()
        {
            if (!BodyExists(MatchBody)) return Body != null ? Body.linearVelocity : Vector3.zero;
            Vec3d position = System.TruePosition(Body.position);
            Vec3d velocity = System.Frame.ToTrueVelocity(SpaceConvert.ToVec3d(Body.position), SpaceConvert.ToVec3d(Body.linearVelocity), System.States);
            ReferenceMotion(MatchBody, position, velocity, System.States, out Vec3d reference, out _);
            return SpaceConvert.ToVector3(System.Frame.ToLocalDirection(velocity - reference, System.States));
        }

        void ReferenceMotion(int body, Vec3d position, Vec3d craftVelocity, BodyState[] states, out Vec3d velocity, out Vec3d acceleration)
        {
            BodyState state = states[body];
            velocity = state.Velocity;
            acceleration = state.Acceleration;
            if (NearGround(body, position, states))
            {
                // This choice depends on distance, not the current rendering frame. Evaluate both velocities
                // at the craft: the body's centre has a different apparent speed in a rotating frame.
                Vec3d spin = System.Model.SpinAxis(body) * System.Model.SpinRate(body);
                if (states == System.States)
                {
                    Vector3 localGround = System.GroundVelocityAt(body, Body.position);
                    velocity = System.Frame.ToTrueVelocity(SpaceConvert.ToVec3d(Body.position), SpaceConvert.ToVec3d(localGround), states);
                }
                else velocity += Vec3d.Cross(spin, position - state.Position);
                // Derivative of the ground-velocity field along the craft's actual path, including sideslip.
                acceleration += Vec3d.Cross(spin, craftVelocity - state.Velocity);
            }
        }

        bool NearGround(int body, Vec3d position, BodyState[] states)
        {
            double nearRadius = System.Model.LockRadius(body);
            return nearRadius > 0 && (position - states[body].Position).Magnitude <= nearRadius;
        }

        void UpdateAimedTarget()
        {
            AimedTarget = -1;
            if (System == null || System.Model == null || Eye == null || Config == null) return;
            Vector3 eye = Eye.transform.position;
            Vector3 forward = Eye.transform.forward;
            float cone = Mathf.Max(0f, Config.targetLockCone);
            float best = float.PositiveInfinity;
            for (int i = 0; i < System.Bodies.Length; i++)
            {
                if (!DestinationExists(i)) continue;
                Vector3 to = System.Bodies[i].transform.position - eye;
                float distance = to.magnitude;
                float radius = (float)System.Model.Spec(i).radius;
                if (distance < 0.001f) continue;
                float off = Mathf.Max(0f, Vector3.Angle(forward, to) - Mathf.Asin(Mathf.Clamp01(radius / distance)) * Mathf.Rad2Deg);
                if (off > cone) continue;
                float score = off + distance * 1e-7f;
                if (score >= best) continue;
                best = score;
                AimedTarget = i;
            }
            if (AimedTarget >= 0)
            {
                // A moon or the Sun directly in front cannot be selected through. This guard is conservative at silhouettes.
                Vec3d from = SpaceConvert.ToVec3d(eye);
                Vec3d to = SpaceConvert.ToVec3d(System.Bodies[AimedTarget].transform.position);
                double targetDistance = (to - from).Magnitude;
                for (int i = 0; i < System.Bodies.Length; i++)
                {
                    if (i == AimedTarget) continue;
                    Vec3d centre = SpaceConvert.ToVec3d(System.Bodies[i].transform.position);
                    if ((centre - from).Magnitude >= targetDistance) continue;
                    double radius = SolidEnvelope(i);
                    // A pilot below a gas giant's cloud radius, or below the conservative height of distant
                    // mountains, can still look out of that envelope. Looking through the body remains blocked.
                    if ((from - centre).SqrMagnitude <= radius * radius && Vec3d.Dot(centre - from, to - from) <= 0) continue;
                    if (FlightGuidance.SegmentBlocked(from, to, centre, radius))
                    { AimedTarget = -1; break; }
                }
            }
        }

        void UpdateTargetReadouts()
        {
            if (!DestinationExists(Target))
            {
                ClosingSpeed = TargetDistance = StoppingDistance = 0f;
                return;
            }
            Vec3d offset = System.States[Target].Position - System.TruePosition(Body.position);
            TargetDistance = Mathf.Max(0f, (float)(offset.Magnitude - System.Model.Spec(Target).radius));
            Vector3 localOffset = SpaceConvert.ToVector3(System.Frame.ToLocalDirection(offset, System.States));
            Vector3 relative = RelativeVelocity();
            ClosingSpeed = Vector3.Dot(relative, localOffset.normalized);
            float authority = AvailableAuthority(AutopilotActive);
            StoppingDistance = authority > 0 ? relative.sqrMagnitude / (2f * authority) : float.PositiveInfinity;
        }

        float AvailableAuthority(bool autopilot)
        {
            if (Config == null) return 0f;
            float scale = Mathf.Clamp01(Mathf.Min(LiftScale, SideScale));
            float authority = (autopilot ? Mathf.Min(Config.autopilotAcceleration, Config.thrust) : Config.matchVelocityThrust) * scale;
            return FlightGuidance.Finite(authority) ? Mathf.Max(0f, authority) : 0f;
        }

        bool TargetTerrainReady()
        {
            var planets = OrbitalAnte.Planets.PlanetWorld.Instance;
            var surface = planets != null && planets.System == System ? planets.SurfaceOf(Target) : null;
            return surface == null || surface.Ready;
        }

        double SolidEnvelope(int body)
        {
            double radius = System.Model.Spec(body).radius;
            var planets = OrbitalAnte.Planets.PlanetWorld.Instance;
            var surface = planets != null && planets.System == System ? planets.SurfaceOf(body) : null;
            if (surface != null && surface.Ready)
                radius = surface.SurfaceRadius + global::System.Math.Max(surface.Terrain.MaxHeight, surface.Terrain.HasSea ? surface.Terrain.SeaLevel : 0);
            return radius;
        }

        double Envelope(int body) => global::System.Math.Max(System.Model.Spec(body).radius, SolidEnvelope(body));

        double StandOffRadius() => Envelope(Target) + Mathf.Max(10f, Config.autopilotClearance);

        bool RouteBlocked(out int blocker, BodyState[] states = null)
        {
            blocker = -1;
            if (!DestinationExists(Target)) return false;
            if (states == null) states = System.States;
            Vec3d start = System.Frame.ToTruePosition(SpaceConvert.ToVec3d(Body.position), states);
            Vec3d to = states[Target].Position - start;
            Vec3d end = states[Target].Position - to.Normalized * StandOffRadius();
            for (int i = 0; i < System.Model.Count; i++)
            {
                if (i == Target) continue;
                if (FlightGuidance.SegmentBlocked(start, end, states[i].Position, Envelope(i) + Mathf.Max(0f, Config.autopilotRouteClearance)))
                { blocker = i; return true; }
            }
            return false;
        }

        void FixedUpdate()
        {
            if (Config == null || Body == null || GameSession.Paused) return;
            float dt = Time.fixedDeltaTime;
            if (!(dt > 0f)) return;
            ValidateNavigation();
            if (Locked)
            {
                if (_navigation.Mode != FlightAssistMode.Manual) CancelAssist("Assist canceled: flight controls unavailable.");
                ClearInput();
                return;
            }
            float roll = GameInput.Asset != null
                ? (GameInput.RollLeft.IsPressed() ? 1f : 0f) - (GameInput.RollRight.IsPressed() ? 1f : 0f) : 0f;
            float turning = Mathf.Clamp01(SideScale);
            Quaternion turn = Quaternion.Euler(-_lookPending.y * turning, _lookPending.x * turning, roll * Config.rollSpeed * turning * dt);
            _lookPending = Vector2.zero;
            Body.MoveRotation(Body.rotation * turn);
            Body.angularVelocity = Vector3.Lerp(Body.angularVelocity, Vector3.zero, 1f - Mathf.Exp(-Config.spinDamping * dt));

            if (_navigation.Mode != FlightAssistMode.Manual || _heldMatchBody >= 0)
            {
                ApplyAssist(dt);
                return;
            }
            _assistThrust = Vector3.zero;
            ThrustInput = _manualThrust;
            float power = Config.thrust * (Boosting ? Config.boostMultiplier : 1f);
            if (_manualThrust.sqrMagnitude > 0.0001f)
            {
                Vector3 push = new Vector3(_manualThrust.x * Mathf.Clamp01(SideScale), _manualThrust.y * Mathf.Clamp01(LiftScale), _manualThrust.z * Mathf.Clamp01(SideScale));
                Body.AddRelativeForce(push * power, ForceMode.Acceleration);
            }
        }

        void ApplyAssist(float dt)
        {
            bool auto = AutopilotActive;
            int reference = MatchBody;
            if (!BodyExists(reference)) { CancelAssist("Assist canceled: reference unavailable."); _heldMatchBody = -1; return; }
            BodyState[] states = System.PhysicsStates;
            if (auto && !TargetTerrainReady())
            { CancelAssist("Autopilot stopped: destination terrain is unavailable."); return; }
            if (auto && RouteBlocked(out int blocker, states))
            { CancelAssist("Autopilot stopped: " + System.Model.Spec(blocker).displayName + " blocks the route. Fly clear manually."); return; }
            Vec3d localPosition = SpaceConvert.ToVec3d(Body.position);
            Vec3d position = System.Frame.ToTruePosition(localPosition, states);
            Vec3d velocity = System.Frame.ToTrueVelocity(localPosition, SpaceConvert.ToVec3d(Body.linearVelocity), states);
            ReferenceMotion(reference, position, velocity, states, out Vec3d referenceVelocity, out Vec3d referenceAcceleration);
            int neighbourhood = System.Model.NeighbourhoodOf(position, states, Gravity.Neighbourhood);
            Vec3d disturbance = System.Model.AccelerationAt(position, states, neighbourhood) - referenceAcceleration;
            double authority = AvailableAuthority(auto);
            FlightCommand command = auto
                ? FlightGuidance.Approach(states[Target].Position - position, velocity - referenceVelocity, disturbance,
                    StandOffRadius(), authority, Config.autopilotCruiseSpeed, Config.assistResponseTime,
                    Config.autopilotArrivalDistance, Config.autopilotArrivalSpeed, dt)
                : FlightGuidance.Match(velocity - referenceVelocity, disturbance, authority, Config.assistResponseTime, dt);
            if (!command.Valid)
            {
                CancelAssist(command.InsufficientThrust ? "Assist canceled: not enough thrust to counter the pull." : "Assist canceled: thrusters or navigation unavailable.");
                _heldMatchBody = -1;
                MatchingVelocity = false;
                return;
            }
            ApproachPhase = command.Phase;
            Vector3 localAcceleration = SpaceConvert.ToVector3(System.Frame.ToLocalDirection(command.Acceleration, states));
            Body.AddForce(localAcceleration, ForceMode.Acceleration);
            _assistThrust = Vector3.ClampMagnitude(Quaternion.Inverse(Body.rotation) * localAcceleration / Mathf.Max(1f, auto ? Config.autopilotAcceleration : Config.matchVelocityThrust), 1f);
            ThrustInput = _assistThrust;
            if (auto && command.Phase == AutopilotPhase.Arrived)
            {
                _navigation.CompleteApproach();
                MatchingVelocity = true;
                Say("Arrived at " + System.Model.Spec(Target).displayName + ". Velocity lock engaged; fly the landing manually.");
            }
        }

        void OnDestroy()
        {
            // The rumble is made afresh for every run.
            if (_thrusterSound != null && _thrusterSound.clip != null) Destroy(_thrusterSound.clip);
        }
    }
}
