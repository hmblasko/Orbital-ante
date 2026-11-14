using OrbitalAnte.Core;
using OrbitalAnte.Navigation;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// How the ship flies: real inertia (nothing slows you down unless you thrust the other way), Match
    /// Velocity, target lock and the autopilot. It began as a bare test craft for judging the solar system and
    /// kept the name; it is attached to the ship and only answers a pilot who is buckled in. The rules it
    /// follows for the lock and the autopilot are plain C# in <c>Navigation/Autopilot.cs</c>.
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
        public bool ControlsLocked { get; set; }
        /// <summary>Set by the ship while nobody is buckled into the seat: the controls do nothing.</summary>
        public bool Unmanned { get; set; }
        /// <summary>How much of full up-and-down thrust is available (damaged engines give less). 0 to 1.</summary>
        public float LiftScale { get; set; } = 1f;
        /// <summary>The share of all push (and of the match-velocity brake) that fuel allows, 0 to 1. Turning is not touched: you can always steer.</summary>
        public float PushScale { get; set; } = 1f;
        /// <summary>How much of full turning and sideways thrust is available (damaged thrusters give less). 0 to 1.</summary>
        public float SideScale { get; set; } = 1f;
        bool Locked => ControlsLocked || Unmanned;

        /// <summary>Set by whoever builds the craft, so it can ask where bodies are and how they are moving.</summary>
        public SolarSystem System { get; set; }
        /// <summary>The body the nose is pointing at, or -1. Match Velocity matches this body when there is one.</summary>
        public int Target { get; private set; } = -1;
        /// <summary>The body under the nose right now, or -1.</summary>
        public int UnderNose { get; private set; } = -1;
        /// <summary>The body the pilot has locked on to, or -1. A lock stays wherever the nose points.</summary>
        public int LockedTarget { get; private set; } = -1;

        // ---- autopilot
        public AutopilotRules PilotRules => Config != null && Config.autopilot != null ? Config.autopilot : _fallbackRules;
        readonly AutopilotRules _fallbackRules = new AutopilotRules();
        public bool PilotOn { get; private set; }
        public PilotOrder Order { get; private set; }
        /// <summary>Why the autopilot last let go, and when (unscaled time), so the dash and HUD can say so.</summary>
        public PilotStop LastStop { get; private set; }
        public float LastStopTime { get; private set; } = -100f;
        public string LastStopWords { get; private set; } = string.Empty;
        /// <summary>The push the autopilot is using as a multiple of the ship's ordinary push, for the fuel sums. 0 when off.</summary>
        public float PilotPushShare { get; private set; }
        /// <summary>Set by the ship: standing still on the ground. The autopilot will not start from there.</summary>
        public bool Landed { get; set; }
        /// <summary>Set by the ship: touching the ground at all, moving or not.</summary>
        public bool Grounded { get; set; }
        /// <summary>The body nearest the nose within a wide cone, or -1. What Lock takes when nothing is dead ahead, so a rough aim is enough.</summary>
        public int NearNose { get; private set; } = -1;
        const float LockCone = 25f;
        float _nextPathCheck, _shownSeconds = -1f, _phaseHeld;
        PilotPhase _shownPhase;
        Vec3d[] _pathPositions;
        double[] _pathRadii;
        /// <summary>Speed toward the target in metres per second. Negative when moving apart.</summary>
        public float ClosingSpeed { get; private set; }
        /// <summary>Distance to the target's surface in metres.</summary>
        public float TargetDistance { get; private set; }
        /// <summary>How far the craft would travel before Match Velocity finished matching the target, in metres.</summary>
        public float StoppingDistance { get; private set; }

        /// <summary>The body Match Velocity is measured against: the target if there is one, otherwise the nearby body.</summary>
        public int MatchBody => Target >= 0 ? Target : System != null ? System.Frame.Anchor : 0;

        /// <summary>True when speed is being measured against the ground beneath (close above a body), not the body's centre.</summary>
        public bool AgainstGround { get; private set; }
        /// <summary>Below this height above a body (metres, or this share of its radius if that is more) speed is measured against its ground.</summary>
        public const float GroundMatchLeast = 300f, GroundMatchShare = 0.5f;

        /// <summary>Locks on to what is under the nose, or lets go (see <see cref="TargetLock.Press"/>).</summary>
        public void PressLock()
        {
            int before = LockedTarget;
            LockedTarget = TargetLock.Press(LockedTarget, UnderNose >= 0 ? UnderNose : NearNose);
            if (!PilotOn || LockedTarget == before) return;
            // Locking a different body while the autopilot is flying sends it there instead. Letting go of
            // the lock altogether hands the ship back.
            if (LockedTarget < 0) { StopPilot(PilotStop.PilotTookOver); return; }
            int blocker = FindBlocker();
            if (blocker >= 0) { StopPilot(PilotStop.Blocked, blocker); return; }
            _shownSeconds = -1f;
            _shownPhase = PilotPhase.Burning;
            _phaseHeld = 1f;
            _nextPathCheck = 0.5f;
        }

        /// <summary>Starts the autopilot toward the locked body, or says plainly why it will not.</summary>
        public bool StartPilot()
        {
            if (PilotOn) return true;
            if (Unmanned) return Refuse(PilotStop.Unbuckled);
            if (LockedTarget < 0 || System == null) return Refuse(PilotStop.NoTarget);
            if (Landed) return Refuse(PilotStop.OnGround);
            if (PushScale < 0.5f) return Refuse(PilotStop.NoFuel);
            int blocker = FindBlocker();
            if (blocker >= 0) return Refuse(PilotStop.Blocked, blocker);
            PilotOn = true;
            _shownSeconds = -1f;
            _shownPhase = PilotPhase.Burning;
            _phaseHeld = 1f;
            LastStop = PilotStop.None;
            LastStopWords = string.Empty;
            _nextPathCheck = 0.5f;
            return true;
        }

        bool Refuse(PilotStop why, int blocker = -1)
        {
            StopPilot(why, blocker);
            return false;
        }

        public void StopPilot(PilotStop why, int blocker = -1)
        {
            PilotOn = false;
            PilotPushShare = 0f;
            Order = default;
            LastStop = why;
            LastStopTime = Time.unscaledTime;
            string name = blocker >= 0 && System != null ? System.Model.Spec(blocker).displayName.ToUpperInvariant() : null;
            LastStopWords = Autopilot.Describe(why, name);
        }

        int FindBlocker()
        {
            int count = System.Bodies.Length;
            if (_pathPositions == null || _pathPositions.Length != count)
            {
                _pathPositions = new Vec3d[count];
                _pathRadii = new double[count];
            }
            Vector3 here = Body.position;
            for (int i = 0; i < count; i++)
            {
                _pathPositions[i] = SpaceConvert.ToVec3d(System.Bodies[i].transform.position - here);
                // Rocks are small and drift; the autopilot does not plan round them. Watch the window.
                _pathRadii[i] = System.Bodies[i].Spec.kind == BodyKind.Asteroid ? 0.0 : System.Bodies[i].Spec.radius;
            }
            return Autopilot.Blocker(PilotRules, _pathPositions, _pathRadii, count, LockedTarget);
        }

        const float TargetCone = 7f;

        Vector2 _lookPending;
        AudioSource _thrusterSound;
        /// <summary>The craft's own code-made rumble, and only that: the ship carries other sounds on the same object.</summary>
        public AudioSource ThrusterSound => _thrusterSound;
        float _thrusterLevel;

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
            if (GameInput.Asset == null) return;
            if (GameSession.Paused)
            {
                // Whatever was held when the menu opened is forgotten, so letting go while paused is honoured
                // and the first physics step after resuming cannot use a stale push.
                ThrustInput = Vector3.zero;
                _lookPending = Vector2.zero;
                Boosting = false;
                MatchingVelocity = false;
                return;
            }

            if (Locked)
            {
                ThrustInput = Vector3.zero;
                _lookPending = Vector2.zero;
                Boosting = false;
                MatchingVelocity = false;
                // Nobody in the seat ends the autopilot. Reading the map does not: it flies on while you look.
                if (PilotOn && Unmanned) StopPilot(PilotStop.Unbuckled);
            }
            else
            {
                Vector2 move = GameInput.FlyMove.ReadValue<Vector2>();
                float lift = (GameInput.FlyUp.IsPressed() ? 1f : 0f) - (GameInput.FlyDown.IsPressed() ? 1f : 0f);
                ThrustInput = Vector3.ClampMagnitude(new Vector3(move.x, lift, move.y), 1f);
                Boosting = GameInput.Boost.IsPressed();
                MatchingVelocity = GameInput.MatchVelocity.IsPressed();

                if (GameInput.LockTarget != null && GameInput.LockTarget.WasPressedThisFrame()) PressLock();
                if (GameInput.Autopilot != null && GameInput.Autopilot.WasPressedThisFrame())
                {
                    if (PilotOn) StopPilot(PilotStop.PilotTookOver);
                    else
                    {
                        // One press is enough: with nothing locked yet, it locks what the nose is on and goes.
                        if (LockedTarget < 0 && (UnderNose >= 0 || NearNose >= 0)) LockedTarget = UnderNose >= 0 ? UnderNose : NearNose;
                        StartPilot();
                    }
                }
                else if (PilotOn && (ThrustInput.sqrMagnitude > 0.04f || MatchingVelocity))
                {
                    // Any push from the pilot and the ship is theirs again, at once.
                    StopPilot(PilotStop.PilotTookOver);
                }
                if (PilotOn)
                {
                    // Shown on the dash as the jets the autopilot is firing.
                    Vector3 shown = transform.InverseTransformDirection(SpaceConvert.ToVector3(Order.Push));
                    ThrustInput = shown.sqrMagnitude > 1e-6f ? shown.normalized * Order.Effort : Vector3.zero;
                    Boosting = false;
                }

                // Mouse movement arrives as "how far since last frame"; a stick arrives as "how far it is pushed".
                Vector2 look = GameInput.FlyLook.ReadValue<Vector2>();
                var control = GameInput.FlyLook.activeControl;
                bool mouse = control != null && control.device is UnityEngine.InputSystem.Pointer;
                if (mouse) _lookPending += look * Config.mouseSensitivity;
                else _lookPending += look * (Config.stickTurnSpeed * Time.unscaledDeltaTime);
            }

            if (!Locked) UpdateTarget();

            float target = ThrustInput.magnitude * (Boosting || PilotOn ? 1f : 0.55f);
            if (MatchingVelocity && RelativeVelocity().sqrMagnitude > 0.04f) target = Mathf.Max(target, 0.8f);
            _thrusterLevel = Mathf.MoveTowards(_thrusterLevel, target, Time.unscaledDeltaTime * 5f);
            _thrusterSound.volume = _thrusterLevel * Config.thrusterVolume;
            _thrusterSound.pitch = 0.8f + _thrusterLevel * 0.5f;
        }

        /// <summary>The craft's velocity measured against the body Match Velocity would match.</summary>
        public Vector3 RelativeVelocity()
        {
            Vector3 velocity = Body.linearVelocity;
            AgainstGround = false;
            if (System != null && Target >= 0)
            {
                // Close above a body, what matters is the ground beneath, which a spinning body carries round
                // with it. Matching that is what lets you set down on a spinning moon without skidding.
                BodySpec spec = System.Bodies[Target].Spec;
                double height = System.AltitudeAbove(Target, Body.position);
                AgainstGround = height < global::System.Math.Max(GroundMatchLeast, spec.radius * GroundMatchShare);
                velocity -= AgainstGround ? System.GroundVelocityAt(Target, Body.position) : System.LocalVelocityOf(Target);
            }
            return velocity;
        }

        /// <summary>
        /// Picks the body the nose is pointing at. Pointing anywhere on a big planet's disc counts, and a small
        /// far-off one only needs to be within a few degrees.
        /// </summary>
        void UpdateTarget()
        {
            Target = -1;
            UnderNose = -1;
            NearNose = -1;
            float near = LockCone;
            if (System == null || System.Model == null || Eye == null) return;
            Vector3 eye = Eye.transform.position;
            // The ship's nose, not the pilot's eyes: a glance down at the camera screen, a launch shake or a knock
            // to the head must never change what the ship is aimed at or what Match Velocity matches.
            Vector3 forward = transform.forward;
            float best = TargetCone;
            for (int i = 0; i < System.Bodies.Length; i++)
            {
                BodySpec spec = System.Bodies[i].Spec;
                Vector3 to = System.Bodies[i].transform.position - eye;
                float distance = to.magnitude;
                float radius = (float)spec.radius;
                if (distance <= radius) continue;
                // Rocks only count when close; otherwise the belt would steal the target from the planet behind it.
                if (spec.kind == BodyKind.Asteroid && distance - radius > 4000f) continue;
                // Among bodies under the nose, the nearer one wins.
                float score = (float)TargetLock.Score(Vector3.Angle(forward, to), distance, radius);
                if (score < best)
                {
                    best = score;
                    UnderNose = i;
                }
                if (score < near)
                {
                    near = score;
                    NearNose = i;
                }
            }
            if (LockedTarget >= System.Bodies.Length) LockedTarget = -1;
            Target = TargetLock.Target(LockedTarget, UnderNose);

            if (Target < 0)
            {
                ClosingSpeed = 0f;
                TargetDistance = 0f;
                StoppingDistance = 0f;
                return;
            }
            Vector3 toTarget = System.Bodies[Target].transform.position - eye;
            TargetDistance = Mathf.Max(0f, toTarget.magnitude - (float)System.Bodies[Target].Spec.radius);
            Vector3 relative = RelativeVelocity();
            ClosingSpeed = Vector3.Dot(relative, toTarget.normalized);
            StoppingDistance = relative.sqrMagnitude / (2f * Mathf.Max(1f, Config.matchVelocityThrust));
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // Turning is direct: the nose goes where you point it. Any spin picked up from a bump is steadied.
            float roll = 0f;
            if (!Locked && GameInput.Asset != null)
            {
                roll = (GameInput.RollLeft.IsPressed() ? 1f : 0f) - (GameInput.RollRight.IsPressed() ? 1f : 0f);
            }
            float turning = Mathf.Clamp01(SideScale);
            Quaternion turn = Quaternion.Euler(-_lookPending.y * turning, _lookPending.x * turning, roll * Config.rollSpeed * turning * dt);
            _lookPending = Vector2.zero;
            // Standing on the ground with the engines idle, nothing turns the ship: a nudge of the stick or the
            // mouse must not twist its feet against the pad. Any push (the thrusters firing) and it steers again.
            bool planted = Grounded && ThrustInput.sqrMagnitude < 0.0025f && !MatchingVelocity;
            if (PilotOn && !Unmanned) FlyByPilot(dt);
            else if (!Locked && !planted)
            {
                // Only a pilot steadies the ship. Left alone it tumbles, tips and comes to rest like any other object.
                Body.MoveRotation(Body.rotation * turn);
                Body.angularVelocity = Vector3.Lerp(Body.angularVelocity, Vector3.zero, 1f - Mathf.Exp(-Config.spinDamping * dt));
            }

            float power = Config.thrust * (Boosting ? Config.boostMultiplier : 1f);
            if (PilotOn) return;
            if (ThrustInput.sqrMagnitude > 0.0001f)
            {
                // The main engines lift; the small thrusters push sideways and fore and aft.
                Vector3 push = new Vector3(ThrustInput.x * SideScale, ThrustInput.y * LiftScale, ThrustInput.z * SideScale);
                Body.AddRelativeForce(push * (power * PushScale), ForceMode.Acceleration);
            }

            if (MatchingVelocity)
            {
                // Matches the body under the nose. With nothing under the nose, velocity here is already measured
                // against the nearby body (see ReferenceFrame), so matching it is simply coming to a stop.
                Vector3 velocity = RelativeVelocity();
                float speed = velocity.magnitude;
                if (speed > 0.02f)
                {
                    float brake = Mathf.Min(Config.matchVelocityThrust * Mathf.Min(LiftScale, SideScale) * PushScale, speed / dt);
                    Body.AddForce(-velocity / speed * brake, ForceMode.Acceleration);
                }
            }
        }
    
        void FlyByPilot(float dt)
        {
            if (System == null || LockedTarget < 0) { StopPilot(PilotStop.NoTarget); return; }
            if (PushScale < 0.5f) { StopPilot(PilotStop.NoFuel); return; }
            _nextPathCheck -= dt;
            if (_nextPathCheck <= 0f)
            {
                _nextPathCheck = 0.5f;
                int blocker = FindBlocker();
                if (blocker >= 0) { StopPilot(PilotStop.Blocked, blocker); return; }
            }

            // Whatever weakens the ship in the pilot's hands weakens it in the autopilot's: damage and an empty tank.
            float share = Mathf.Clamp01(PushScale) * Mathf.Clamp01(Mathf.Min(LiftScale, SideScale));
            CelestialBody body = System.Bodies[LockedTarget];
            Vector3 to = body.transform.position - Body.position;
            Vector3 relative = Body.linearVelocity - System.LocalVelocityOf(LockedTarget);
            PilotOrder order = Autopilot.Fly(PilotRules, SpaceConvert.ToVec3d(to), SpaceConvert.ToVec3d(relative), body.Spec.radius,
                SpaceConvert.ToVec3d(Gravity != null ? Gravity.FeltGravity : Vector3.zero), share);
            // What is shown is steadier than what is flown: the time only counts down, and the word for what it
            // is doing holds for a moment, so the dash and HUD are not rewritten every frame.
            if (order.SecondsLeft > _shownSeconds && _shownSeconds >= 0f) order.SecondsLeft = _shownSeconds;
            _shownSeconds = order.SecondsLeft;
            _phaseHeld += dt;
            if (order.Phase != _shownPhase && _phaseHeld >= 0.3f) { _shownPhase = order.Phase; _phaseHeld = 0f; }
            order.Phase = _shownPhase;
            Order = order;
            if (order.Arrived) { StopPilot(PilotStop.Arrived); return; }

            Body.AddForce(SpaceConvert.ToVector3(order.Push), ForceMode.Acceleration);
            PilotPushShare = order.Effort * PilotRules.push * share * Mathf.Max(1f, PilotRules.fuelSurcharge) / Mathf.Max(1f, Config.thrust);

            // The nose swings round to the target, so you watch it come up through the window. Bumps are steadied.
            if (to.sqrMagnitude > 1f)
            {
                Quaternion want = Quaternion.LookRotation(to.normalized, Body.rotation * Vector3.up);
                Body.MoveRotation(Quaternion.RotateTowards(Body.rotation, want, PilotRules.turnSpeed * Mathf.Clamp01(SideScale) * dt));
            }
            Body.angularVelocity = Vector3.Lerp(Body.angularVelocity, Vector3.zero, 1f - Mathf.Exp(-Config.spinDamping * dt));
            _lookPending = Vector2.zero;
        }

        void OnDestroy()
        {
            // The rumble is made afresh for every run.
            if (_thrusterSound != null && _thrusterSound.clip != null) Destroy(_thrusterSound.clip);
        }
    }
}
