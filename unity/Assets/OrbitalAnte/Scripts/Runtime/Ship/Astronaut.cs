using System;
using OrbitalAnte.Core;
using OrbitalAnte.Player;
using OrbitalAnte.Space;
using OrbitalAnte.Spacewalk;
using OrbitalAnte.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Ship
{
    public enum AstronautMode
    {
        /// <summary>In the pilot's seat: sitting down, strapped in, or getting up.</summary>
        Seated,
        /// <summary>On foot inside the cabin, held to its floor by the ship's own gravity.</summary>
        Aboard,
        /// <summary>Riding the gravity lift up or down the shaft.</summary>
        Lift,
        /// <summary>On foot outside the ship.</summary>
        Outside,
    }

    /// <summary>
    /// The player, in first person, in and around the ship: walking about the cabin, buckling into the seat,
    /// riding the lift, stepping outside and mending things.
    ///
    /// Inside the ship the player is not a physics object at all. They are carried in the ship's own space by
    /// <see cref="InteriorWalker"/>, which is why nothing the ship does (spinning, crashing, flying at speed)
    /// can shake them through a wall. Outside, the suit's body takes over: it walks under a planet's pull,
    /// flies on its jets off the ground, and floats free (no "down" at all) where nothing pulls. The suit's
    /// rules are in <c>Spacewalk/SuitRules.cs</c>.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class Astronaut : MonoBehaviour
    {
        public Camera Eye { get; private set; }
        public Ship Ship { get; private set; }
        public AstronautMode Mode { get; private set; }
        /// <summary>Degrees the seated pilot's head is tipped down toward the dash's camera screen (set by the cockpit).</summary>
        public float ScreenGlance { get; set; }
        public PlayerWeapons Weapons { get; private set; }
        /// <summary>The suit's physics body, used only outside.</summary>
        public Rigidbody Suit { get; private set; }

        /// <summary>What the player can do right now, for the HUD ("E: buckle in"). Empty when nothing.</summary>
        public string Prompt { get; private set; } = string.Empty;
        /// <summary>0 to 1 while mending; -1 when not mending.</summary>
        public float PromptProgress { get; private set; } = -1f;
        /// <summary>A short message that should be shown for a few seconds, and when it was raised (unscaled time).</summary>
        public string Notice { get; private set; } = string.Empty;
        public float NoticeTime { get; private set; } = -100f;

        ShipConfig _config;
        PlayerConfig _look;
        SolarSystem _system;
        Transform _world;
        GravityBody _suitGravity;
        readonly InteriorWalker _walker = new InteriorWalker();

        float _yaw, _pitch;
        Vector3 _standFrom;
        float _yawFrom, _pitchFrom;

        // ---- knocks
        /// <summary>What knocks do to the view and to the person. Rules in <c>Navigation/CrashRules.cs</c>.</summary>
        public OrbitalAnte.Navigation.CrashModel Crash { get; private set; }
        public bool Dead { get; private set; }
        public string DeathCause { get; private set; } = string.Empty;
        /// <summary>True for a moment after being thrown about the cabin: you cannot act.</summary>
        public bool Dazed => _dazed > 0f;
        /// <summary>Called once, a moment after the pilot dies, with the cause in plain words. Set by whoever shows the screen.</summary>
        public static Action<string> OnDeath;
        public static void ResetStatics() => OnDeath = null;
        float _dazed, _deadFor;
        bool _posed, _deathTold;
        float _pendingYaw;
        bool _jumpQueued;
        Vector3 _up = Vector3.up;
        bool _grounded;

        // ---- the suit (rules in Spacewalk/SuitRules.cs)
        public SuitConfig SuitDials { get; private set; }
        public SuitSupplies SuitTanks { get; private set; }
        public JetGlow Jets { get; } = new JetGlow();
        public Posture Posture { get; private set; }
        public bool Tethered { get; private set; }
        public bool TetherTaut { get; private set; }
        /// <summary>Where the tether is fixed to the ship, in the world.</summary>
        public Vector3 TetherAnchor => Ship.transform.TransformPoint(V(ShipLayout.TetherAnchor));
        /// <summary>Which way a slack tether hangs: the way things fall, or away from the ship's belly when nothing pulls.</summary>
        public Vector3 TetherHangs => _suitGravity.FeltGravity.sqrMagnitude > 0.06f ? _suitGravity.FeltGravity.normalized : -Ship.transform.up;
        /// <summary>What the suit's readout shows. Filled in every physics step while outside.</summary>
        public SuitInputs SuitState;
        /// <summary>True while the suit has a danger to report (for the warning tone).</summary>
        public bool SuitAlarm { get; private set; }
        /// <summary>How full the ship's air tank is, 0 to 1. Set by the cockpit, which keeps the ship's supplies.</summary>
        public float ShipAir { get; set; } = 1f;
        /// <summary>Hands over (and forgets) the share of the ship's air the suit has drawn since last asked.</summary>
        public float TakeAirDrawn() { float drawn = _airDrawn; _airDrawn = 0f; return drawn; }
        /// <summary>True once for each time a jet lit from cold (the jets tick with the physics, the sound with the frame).</summary>
        public bool TakeJetPuff() { bool puff = _jetPuff; _jetPuff = false; return puff; }
        bool _jetPuff, _matchWasHeld;
        MatchTarget _matchTarget;
        SuitFx _fx;
        float _airDrawn, _jetPush, _rollEase;
        Quaternion _viewLocal = Quaternion.identity;
        bool _mendingNow;
        Vector3 _jetLocal;
        static readonly Vector3 EyeLocal = new Vector3(0f, ShipLayout.EyeHeight, 0f);

        Transform _strapLeft, _strapRight, _strapLap, _buckle;
        Material _webbing;
        static readonly Vector3 ShoulderLeft = new Vector3(-0.2f, 1.06f, 1.02f);
        static readonly Vector3 ShoulderRight = new Vector3(0.2f, 1.06f, 1.02f);
        static readonly Vector3 HipLeft = new Vector3(-0.3f, 0.52f, 1.22f);
        static readonly Vector3 HipRight = new Vector3(0.3f, 0.52f, 1.22f);
        static readonly Vector3 Lap = new Vector3(0f, 0.6f, 1.44f);

        readonly RaycastHit[] _hits = new RaycastHit[6];
        readonly Collider[] _overlaps = new Collider[6];

        // ------------------------------------------------------------------ building

        public static Astronaut Create(Transform world, Ship ship, ShipConfig config, SolarSystem system)
        {
            var go = new GameObject("Astronaut");
            go.layer = Layers.Player;
            var a = go.AddComponent<Astronaut>();
            a._config = config;
            a._look = PlayerConfig.Load();
            a._system = system;
            a._world = world;
            a.Ship = ship;

            var eyeGo = new GameObject("Eye");
            eyeGo.layer = Layers.Player;
            eyeGo.transform.SetParent(go.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, ShipLayout.EyeHeight, 0f);
            a.Eye = eyeGo.AddComponent<Camera>();
            a.Eye.fieldOfView = config.fieldOfView;
            // Close enough that the cabin wall never cuts into view when you stand against it.
            a.Eye.nearClipPlane = 0.12f;
            a.Eye.farClipPlane = SolarSystem.FarClip;
            a.Eye.clearFlags = CameraClearFlags.SolidColor;
            a.Eye.backgroundColor = Color.black;
            eyeGo.AddComponent<AudioListener>();

            var suitGo = new GameObject("SuitBody");
            suitGo.layer = Layers.Player;
            suitGo.transform.SetParent(world, false);
            a.Suit = suitGo.AddComponent<Rigidbody>();
            a.Suit.mass = 110f;
            a.Suit.useGravity = false;
            a.Suit.linearDamping = 0f;
            a.Suit.angularDamping = 0f;
            a.Suit.freezeRotation = true;
            a.Suit.interpolation = RigidbodyInterpolation.Interpolate;
            a.Suit.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var capsule = suitGo.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, ShipLayout.PersonHeight * 0.5f, 0f);
            capsule.height = ShipLayout.PersonHeight;
            capsule.radius = ShipLayout.PersonRadius;
            // Slippery, so the suit slides along walls instead of sticking to them. Walking speed is set directly.
            capsule.sharedMaterial = new PhysicsMaterial("Suit") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            a._suitGravity = suitGo.AddComponent<GravityBody>();
            suitGo.AddComponent<SuitBody>().Owner = a;
            suitGo.SetActive(false);
            a.SuitDials = SuitConfig.Load();
            a.SuitTanks = new SuitSupplies(a.SuitDials.rules);
            a._fx = SuitFx.Build(a, a.SuitDials, world);

            a._walker.WalkSpeed = config.walkSpeed;
            a._walker.Gravity = config.cabinGravity;
            a._walker.JumpHeight = config.jumpHeight;

            a.BuildBelt();
            if (config.testPistol) a.BuildPistol();

            // A run starts strapped in, as if you had just finished your checks.
            ship.Belt.StartBuckled();
            ship.Crew = a;
            a.Crash = new OrbitalAnte.Navigation.CrashModel(config.crash);
            a.EnterSeat(instant: true);
            return a;
        }

        void BuildBelt()
        {
            Material webbing = Planets.PlanetMaterials.Baked(0.55f);
            _webbing = webbing;
            Mesh strap = ShipMeshes.ToMesh(ShipProps.BeltStrap(), "BeltStrap");
            Mesh buckle = ShipMeshes.ToMesh(ShipProps.BeltBuckle(), "BeltBuckle");
            _strapLeft = BeltPiece("StrapLeft", strap, webbing);
            _strapRight = BeltPiece("StrapRight", strap, webbing);
            _strapLap = BeltPiece("StrapLap", strap, webbing);
            _buckle = BeltPiece("Buckle", buckle, webbing);
        }

        Transform BeltPiece(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Ship.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        void BuildPistol()
        {
            Weapons = Eye.gameObject.AddComponent<PlayerWeapons>();
            Weapons.Initialize(_look, Eye, null, null, _world);
            Weapons.Equip(Weapon.Create(WeaponData.Pistol, _world, true));
        }

        // ------------------------------------------------------------------ changing mode

        void SetMode(AstronautMode mode)
        {
            Mode = mode;
            bool outside = mode == AstronautMode.Outside;
            Ship.CrewAboard = mode == AstronautMode.Aboard;
            if (!outside && Suit.gameObject.activeSelf) Suit.gameObject.SetActive(false);
            if (_system != null) _system.Focus = outside ? _suitGravity : Ship.Gravity;

            bool armed = mode == AstronautMode.Aboard || mode == AstronautMode.Outside;
            if (Weapons != null)
            {
                Weapons.enabled = armed;
                if (Weapons.Current != null)
                {
                    Weapons.Current.gameObject.SetActive(armed);
                    // Inside the hull the Sun cannot reach the gun: it takes the cabin's even light instead.
                    OrbitalAnte.Look.ModelLibrary.SetIndoors(Weapons.Current.gameObject, !outside);
                }

            }
            GameInput.SetFlying(mode == AstronautMode.Seated && Ship.Belt.Buckled);
        }

        void EnterSeat(bool instant)
        {
            transform.SetParent(Ship.transform, false);
            _standFrom = instant ? V(ShipLayout.SeatedEye) : transform.localPosition + Vector3.up * ShipLayout.EyeHeight;
            _yawFrom = Mathf.DeltaAngle(0f, _yaw);
            _pitchFrom = _pitch;
            SetMode(AstronautMode.Seated);
            if (!instant)
            {
                Ship.Belt.Begin();
                Ship.PlaySound(Ship.Sounds.BeltRelease, 0.4f);
            }
            PoseSeated();
        }

        void StandUp()
        {
            // Step out to the left of the seat, facing forward.
            _walker.Place(-0.78f, 1.0f);
            _yaw = 0f;
            _pitch = 0f;
            SetMode(AstronautMode.Aboard);
            PoseAboard();
        }

        void GoOutside(Vector3 feet, Vector3 velocity)
        {
            _up = Ship.transform.up;
            Vector3 forward = Vector3.ProjectOnPlane(Eye.transform.forward, _up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(Ship.transform.forward, _up);
            Suit.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(forward.normalized, _up));
            Suit.gameObject.SetActive(true);
            Suit.position = feet;
            Suit.rotation = Suit.transform.rotation;
            Suit.linearVelocity = velocity;
            transform.SetParent(Suit.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            _pendingYaw = 0f;
            _rollEase = 0f;
            Posture = Posture.Upright;
            Jets.Reset();
            SetMode(AstronautMode.Outside);
            // Stepping out of a ship that is not standing on anything, the line goes on by itself.
            bool landed = Ship.Flight != null && Ship.Flight.Landed;
            if (!landed && !Tethered) SetTether(true, quiet: false);
        }

        /// <summary>Puts the player outside at a place and velocity, as if the lift had just let them go. For tests.</summary>
        public void StepOutsideForTests(Vector3 feet, Vector3 velocity) => GoOutside(feet, velocity);
        /// <summary>Clips the tether on or off without the range check or the sound. For tests.</summary>
        public void SetTetherForTests(bool on) => SetTether(on, quiet: true);

        void SetTether(bool on, bool quiet)
        {
            if (Tethered == on) return;
            Tethered = on;
            TetherTaut = false;
            if (!quiet && _fx != null) _fx.Play(on ? _fx.Sounds.Clip : _fx.Sounds.Unclip, 0.9f);
        }

        void BoardLift(Vector3 feetInShip)
        {
            // Keep looking the same way as the view moves from the suit's space to the ship's.
            Vector3 forward = Ship.transform.InverseTransformDirection(transform.forward);
            _yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            transform.SetParent(Ship.transform, false);
            Ship.Hatch.BeginUp(new P3(feetInShip.x, feetInShip.y, feetInShip.z));
            // The line reels itself in as the lift takes you.
            SetTether(false, quiet: !Tethered);
            Jets.Reset();
            _pitch = Posture == Posture.Free ? 0f : _pitch;
            SetMode(AstronautMode.Lift);
            PoseLift();
        }

        static Vector3 V(P3 p) => new Vector3(p.X, p.Y, p.Z);

        // ------------------------------------------------------------------ every frame

        /// <summary>
        /// The ship took a knock. <paramref name="knock"/> is how much its velocity changed (metres per
        /// second); <paramref name="from"/> is the direction the blow came from, in the ship's own directions.
        /// </summary>
        public void Knock(float knock, Vector3 from)
        {
            if (Dead || Crash == null || Mode == AstronautMode.Outside) return;
            bool buckled = Mode == AstronautMode.Seated && Ship.Belt.Buckled;
            // A blow from below or from ahead throws the head forward; from the side, sideways.
            var fate = Crash.Hit(knock, buckled, from.x, Mathf.Clamp(from.z - from.y, -1f, 1f));
            if (fate == OrbitalAnte.Navigation.PilotFate.Thrown) _dazed = Mathf.Max(_dazed, Crash.Rules.dazedSeconds);
            else if (fate == OrbitalAnte.Navigation.PilotFate.Killed) Die(OrbitalAnte.Navigation.CrashModel.Cause(Crash.LastKnock, buckled));
        }

        /// <summary>Ends the run. Always first person: the view slumps where you were, then the screen says why.</summary>
        public void Die(string cause)
        {
            if (Dead) return;
            Dead = true;
            DeathCause = cause ?? string.Empty;
            _deadFor = 0f;
            Prompt = string.Empty;
            PromptProgress = -1f;
        }

        void PoseOnly()
        {
            switch (Mode)
            {
                case AstronautMode.Seated: PoseSeated(); break;
                case AstronautMode.Aboard: PoseAboard(); break;
                case AstronautMode.Lift: PoseLift(); break;
                case AstronautMode.Outside: PoseOutside(); break;
            }
        }

        void LateUpdate()
        {
            // Only ever on top of a pose made this frame, or the kick would pile up on itself.
            if (!_posed || Eye == null || Crash == null) return;
            _posed = false;
            bool shake = FeelSettings.CameraShake;
            float pitch = shake ? Crash.KickPitch : 0f, roll = shake ? Crash.KickRoll : 0f;
            float slump = 0f;
            if (Dead) slump = Mathf.SmoothStep(0f, 1f, _deadFor / 1.1f);
            else if (_dazed > 0f) slump = 0.35f * Mathf.Sin(Mathf.Clamp01(_dazed / Mathf.Max(0.1f, Crash.Rules.dazedSeconds)) * Mathf.PI);
            if (slump > 0f)
            {
                // Going down: the head drops and rolls to one side. Not a comfort setting; it is what happened.
                pitch += 38f * slump;
                roll += 62f * slump;
                Eye.transform.localPosition += Vector3.down * (0.55f * slump);
            }
            if (pitch != 0f || roll != 0f) Eye.transform.localRotation *= Quaternion.Euler(pitch, 0f, roll);
        }

        void Update()
        {
            if (Ship == null || GameInput.Asset == null || GameSession.Paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Prompt = string.Empty;
            PromptProgress = -1f;

            if (Crash != null) Crash.Tick(dt);
            if (Dead)
            {
                _deadFor += dt;
                PoseOnly();
                _posed = true;
                if (!_deathTold && _deadFor > 1.8f)
                {
                    _deathTold = true;
                    OnDeath?.Invoke(DeathCause);
                }
                return;
            }
            if (_dazed > 0f)
            {
                _dazed -= dt;
                if (Mode == AstronautMode.Aboard)
                {
                    // Thrown about the cabin: for a moment you can do nothing but watch.
                    PoseOnly();
                    _posed = true;
                    PoseBelt();
                    return;
                }
                // In the seat or the lift the daze is only felt (the slump): the belt and the lift ride must
                // keep running, or a knock half way through either would leave you stuck there.
            }
            _posed = true;
            TickSuit(dt);
            if (Dead) { _posed = false; return; }

            switch (Mode)
            {
                case AstronautMode.Seated: UpdateSeated(); break;
                case AstronautMode.Aboard: UpdateAboard(dt); break;
                case AstronautMode.Lift: UpdateLift(); break;
                case AstronautMode.Outside: UpdateOutside(dt); break;
            }
            PoseBelt();
        }

        void Say(string notice)
        {
            Notice = notice;
            NoticeTime = Time.unscaledTime;
        }

        void ToggleHatch()
        {
            if (!Ship.ToggleHatch())
            {
                Say(Ship.Hatch.Ride != LiftRide.None ? "The hatch will not close on someone in the lift." : "The hatch is broken. Mend it first.");
            }
        }

        void UpdateSeated()
        {
            SeatBelt belt = Ship.Belt;
            if (belt.Clicked)
            {
                Ship.PlaySound(Ship.Sounds.BeltClick, 1f);
                Ship.PlaySound(Ship.Sounds.SeatHiss, 0.9f);
                GameInput.SetFlying(true);
            }
            if (belt.State == BeltState.Free)
            {
                StandUp();
                return;
            }
            if (belt.Buckled)
            {
                var launch = Ship.Launch;
                Prompt = launch != null && launch.Settled && launch.Model.State == OrbitalAnte.Launch.EngineState.Off
                    ? Words(6, "Hold ", GameInput.FlyUp, ": lift off")
                    : Words(1, null, GameInput.Unbuckle, ": unbuckle");
                if (GameInput.FlyHatch.WasPressedThisFrame()) ToggleHatch();
                if (GameInput.Unbuckle.WasPressedThisFrame() && belt.Release())
                {
                    Ship.PlaySound(Ship.Sounds.BeltRelease, 1f);
                    GameInput.SetFlying(false);
                    _yawFrom = 0f;
                    _pitchFrom = 0f;
                    _standFrom = new Vector3(-0.78f, ShipLayout.EyeHeight, 1.0f);
                }
            }
            PoseSeated();
        }

        void PoseSeated()
        {
            SeatBelt belt = Ship.Belt;
            float sit = belt.Sit;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            // A glance down at your lap while the belt goes across, then eyes front.
            float glance = belt.State == BeltState.Buckling ? Mathf.Sin(Mathf.Clamp01((belt.Progress - 0.2f) / 0.75f) * Mathf.PI) * _config.buckleGlance : 0f;
            float yaw = Mathf.LerpAngle(_yawFrom, 0f, sit);
            float pitch = Mathf.Lerp(_pitchFrom, 0f, sit) + glance + ScreenGlance;
            Eye.transform.localPosition = Vector3.Lerp(_standFrom, V(ShipLayout.SeatedEye), sit);
            Eye.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        void ReadLook(float dt, out float yawDelta)
        {
            ReadLookRaw(dt, out yawDelta, out float pitchDelta);
            _pitch = Mathf.Clamp(_pitch + pitchDelta, -88f, 88f);
        }

        /// <summary>How far the player turned their head this frame, in degrees (pitch positive is downward).</summary>
        void ReadLookRaw(float dt, out float yawDelta, out float pitchDelta)
        {
            yawDelta = 0f;
            pitchDelta = 0f;
            Vector2 look = GameInput.Look.ReadValue<Vector2>();
            if (look.sqrMagnitude <= 0f) return;
            InputControl control = GameInput.Look.activeControl;
            if (control != null && control.device is Pointer)
            {
                // Mouse movement is already "distance moved this frame".
                yawDelta = look.x * _look.mouseSensitivity;
                pitchDelta = -look.y * _look.mouseSensitivity;
            }
            else
            {
                float magnitude = Mathf.Clamp01(look.magnitude);
                float curved = Mathf.Pow(magnitude, Mathf.Max(1f, _look.stickCurve));
                Vector2 shaped = magnitude > 0.0001f ? look / magnitude * curved : Vector2.zero;
                yawDelta = shaped.x * _look.stickYawSpeed * dt;
                pitchDelta = -shaped.y * _look.stickPitchSpeed * dt;
            }
        }

        void UpdateAboard(float dt)
        {
            ReadLook(dt, out float yawDelta);
            _yaw += yawDelta;

            Vector2 move = Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f);
            float rad = _yaw * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad), cos = Mathf.Cos(rad);
            float wishX = move.x * cos + move.y * sin;
            float wishZ = -move.x * sin + move.y * cos;
            bool liftOn = Ship.Hatch.Open && Ship.Hatch.WantOpen;
            _walker.Step(dt, wishX, wishZ, GameInput.Jump.WasPressedThisFrame(), liftOn);
            PoseAboard();

            if (_walker.OverOpenWell)
            {
                // With ground below, the lift sets you down on it. With none (in flight), it lowers you clear
                // of the belly and lets go: the suit's jets and the tether take it from there.
                float bottom = Ship.FindLiftBottom();
                if (float.IsNaN(bottom)) bottom = ShipLayout.BellyY - ShipLayout.PersonHeight - 0.35f;
                Ship.Hatch.BeginDown(new P3(_walker.X, 0f, _walker.Z), bottom);
                SetMode(AstronautMode.Lift);
                return;
            }

            if (GameInput.Hatch.WasPressedThisFrame()) ToggleHatch();

            // The lever and the seat come first, so a cracked window nearby never swallows the button.
            Vector3 eye = Eye.transform.position, forward = Eye.transform.forward;
            if (LookingAt(V(ShipLayout.HatchLever), 1.8f, 26f, eye, forward))
            {
                Prompt = Ship.Hatch.WantOpen ? Words(2, null, GameInput.Interact, ": close the hatch") : Words(3, null, GameInput.Interact, ": open the hatch");
                if (GameInput.Interact.WasPressedThisFrame()) ToggleHatch();
                return;
            }
            Vector3 seat = V(ShipLayout.SeatPosition);
            float toSeat = new Vector2(_walker.X - seat.x, _walker.Z - seat.z).magnitude;
            if (toSeat < _config.seatReach && LookingAt(seat + Vector3.up * 0.75f, 3f, 42f, eye, forward))
            {
                Prompt = Words(4, null, GameInput.Interact, ": buckle in");
                if (GameInput.Interact.WasPressedThisFrame()) EnterSeat(instant: false);
                return;
            }
            Mending(dt);
        }

        void PoseAboard()
        {
            transform.localPosition = new Vector3(_walker.X, _walker.Y, _walker.Z);
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            Eye.transform.localPosition = new Vector3(0f, ShipLayout.EyeHeight, 0f);
            Eye.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void UpdateLift()
        {
            ReadLook(Time.deltaTime, out float yawDelta);
            _yaw += yawDelta;
            PoseLift();
            LiftRide finished = Ship.LastRideFinished;
            if (finished == LiftRide.Up)
            {
                _walker.Place(0f, 0f);
                SetMode(AstronautMode.Aboard);
                PoseAboard();
            }
            else if (finished == LiftRide.Down)
            {
                // Measured again now: the ship may have settled or slid while you rode down. With no ground
                // there at all, you are let go where the lift stopped.
                float bottom = Ship.FindLiftBottom();
                Vector3 local = V(Ship.Hatch.Rider);
                if (!float.IsNaN(bottom)) local.y = Mathf.Max(local.y, bottom);
                Vector3 feet = Ship.Body.position + Ship.Body.rotation * local;
                GoOutside(feet, Ship.Body.GetPointVelocity(feet));
            }
        }

        void PoseLift()
        {
            transform.localPosition = V(Ship.Hatch.Rider);
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            Eye.transform.localPosition = new Vector3(0f, ShipLayout.EyeHeight, 0f);
            Eye.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void UpdateOutside(float dt)
        {
            ReadLookRaw(dt, out float yawDelta, out float pitchDelta);
            if (Posture == Posture.Free)
            {
                // Floating: there is no "up", so the head turns any way at all, and can roll over.
                float roll = (GameInput.SuitRollLeft.IsPressed() ? 1f : 0f) - (GameInput.SuitRollRight.IsPressed() ? 1f : 0f);
                _viewLocal = _viewLocal * Quaternion.Euler(pitchDelta, yawDelta, roll * SuitDials.rollSpeed * dt);
            }
            else
            {
                // Turning is applied to the view at once. The suit's body only keeps you upright.
                _pendingYaw += yawDelta;
                _pitch = Mathf.Clamp(_pitch + pitchDelta, -88f, 88f);
                _rollEase = Mathf.LerpAngle(_rollEase, 0f, 1f - Mathf.Exp(-5f * dt));
            }
            if (GameInput.Jump.WasPressedThisFrame()) _jumpQueued = true;
            PoseOutside();

            // Floating, there are no "feet": the lift takes you by where your middle is.
            Vector3 feetWorld = Posture == Posture.Free
                ? Suit.position + Suit.rotation * (Vector3.up * (ShipLayout.PersonHeight * 0.5f)) - Ship.Body.rotation * (Vector3.up * (ShipLayout.PersonHeight * 0.5f))
                : Suit.position;
            Vector3 feetInShip = Quaternion.Inverse(Ship.Body.rotation) * (feetWorld - Ship.Body.position);
            if (Ship.Hatch.CanPickUp(new P3(feetInShip.x, feetInShip.y, feetInShip.z)))
            {
                BoardLift(feetInShip);
                return;
            }

            float away = Vector3.Distance(Suit.position, Ship.transform.position);
            if (away < _config.remoteHatchRange)
            {
                if (GameInput.Hatch.WasPressedThisFrame()) ToggleHatch();
            }
            if (GameInput.TetherClip.WasPressedThisFrame()) ToggleTether();
            // While you mend, the jets hold you beside the ship by themselves (see FixedUpdate).
            _mendingNow = false;
            if (Mending(dt))
            {
                _mendingNow = GameInput.Interact.IsPressed();
                return;
            }

            Vector3 eye = Eye.transform.position, forward = Eye.transform.forward;
            bool flying = !_grounded;
            if (LookingAt(V(ShipLayout.OutsideHatchButton), 2.6f, 30f, eye, forward))
            {
                Prompt = Ship.Hatch.WantOpen ? Words(2, null, GameInput.Interact, ": close the hatch") : Words(3, null, GameInput.Interact, ": open the hatch");
                if (GameInput.Interact.WasPressedThisFrame()) ToggleHatch();
            }
            else if (flying && SuitState.Speed > 1.5f && !SuitState.Matching)
            {
                // The one thing worth knowing when you are drifting: how to stop.
                Prompt = SuitState.MatchTarget == MatchTarget.Ship
                    ? Words(7, "Hold ", GameInput.SuitMatch, ": match the ship")
                    : Words(8, "Hold ", GameInput.SuitMatch, ": stop against the ground");
            }
            else if (flying && !Tethered && SuitState.TetherInReach)
            {
                Prompt = Words(9, null, GameInput.TetherClip, ": clip the tether on");
            }
            else if (away < _config.remoteHatchRange)
            {
                Prompt = Ship.Hatch.WantOpen
                    ? (flying ? "Fly under the blue ring to go aboard" : "Stand under the blue ring to go aboard")
                    : Words(5, null, GameInput.Hatch, ": open the hatch");
            }
        }

        /// <summary>Puts the view where the suit's state says it is. Upright: turn and nod. Floating: any way up, turning about the eyes.</summary>
        void PoseOutside()
        {
            if (Posture == Posture.Free)
            {
                transform.localRotation = _viewLocal;
                transform.localPosition = EyeLocal - _viewLocal * EyeLocal;
                Eye.transform.localPosition = EyeLocal;
                Eye.transform.localRotation = Quaternion.identity;
            }
            else
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.Euler(0f, _pendingYaw, 0f);
                Eye.transform.localPosition = EyeLocal;
                Eye.transform.localRotation = Quaternion.Euler(_pitch, 0f, _rollEase);
            }
        }

        void ToggleTether()
        {
            if (Tethered)
            {
                SetTether(false, quiet: false);
                Say("Tether off. Nothing is holding you now.");
                return;
            }
            float distance = Vector3.Distance(Suit.worldCenterOfMass, TetherAnchor);
            if (Tether.InReach(SuitDials.rules, distance))
            {
                SetTether(true, quiet: false);
                Say("Tether on: " + Mathf.RoundToInt(SuitDials.rules.tetherLength) + " m of line.");
            }
            else
            {
                Say("Too far from the ship to clip on (" + Mathf.RoundToInt(distance) + " m; the line is " + Mathf.RoundToInt(SuitDials.rules.tetherLength) + " m).");
            }
        }

        /// <summary>The suit's air and jet fuel, every frame, wherever you are.</summary>
        void TickSuit(float dt)
        {
            bool outside = Mode == AstronautMode.Outside;
            float push = outside ? _jetPush : 0f;
            _airDrawn += SuitTanks.Tick(dt, outside || ShipAir <= 0f, !outside, push, ShipAir);
            SuitState.Air = SuitTanks.Air;
            SuitState.Fuel = SuitTanks.Fuel;
            SuitState.AirLow = SuitTanks.AirLow;
            SuitState.AirOut = SuitTanks.AirOut;
            SuitState.FuelLow = SuitTanks.FuelLow;
            SuitState.FuelOut = SuitTanks.FuelOut;
            SuitState.BurningAir = SuitTanks.BurningAir;
            SuitState.SecondsLeft = SuitTanks.SecondsLeft;
            SuitState.Tethered = Tethered;
            SuitState.TetherTaut = TetherTaut;
            SuitState.Floating = outside && Posture == Posture.Free;
            if (!outside)
            {
                SuitState.Matching = false;
                SuitState.TetherTaut = false;
                // Aboard, an empty jet tank is no danger: it is filling.
                SuitState.FuelOut = false;
                SuitState.FuelLow = false;
            }
            SuitAlarm = SuitReadout.IsAlarm(SuitState);
            if (SuitTanks.Suffocated) Die(SuitKnock.NoAir);
        }

        /// <summary>The suit's body hit something at this speed (metres per second, straight into it).</summary>
        public void SuitHit(float speed, Vector3 from)
        {
            if (Dead || Mode != AstronautMode.Outside) return;
            SuitKnock.Result result = SuitKnock.For(SuitDials.rules, speed);
            if (speed > 2.5f && _fx != null) _fx.Play(_fx.Sounds.Bump, Mathf.Clamp01(speed / SuitDials.rules.hurtSpeed));
            if (result == SuitKnock.Result.Nothing) return;
            // The view takes the blow the same way it does in the cabin.
            Vector3 local = Eye.transform.InverseTransformDirection(from);
            if (Crash != null) Crash.Hit(speed, false, local.x, Mathf.Clamp(local.z - local.y, -1f, 1f));
            if (result == SuitKnock.Result.Killed) Die(SuitKnock.Cause(speed));
            else _dazed = Mathf.Max(_dazed, 0.8f);
        }

        /// <summary>Looks for a damaged part in front of the player and mends it while the button is held.</summary>
        bool Mending(float dt)
        {
            int part = Ship.FindRepairTarget(Eye.transform.position, Eye.transform.forward, Mode == AstronautMode.Aboard);
            if (part < 0) return false;
            Prompt = Words(100 + part, "Hold ", GameInput.Interact, ": mend the ", Ship.Model.Parts[part].Name);
            PromptProgress = Ship.Model.RepairProgress(part);
            if (GameInput.Interact.IsPressed())
            {
                Ship.RepairingPart = part;
                if (Ship.Repair(part, dt)) Say(Ship.Model.Parts[part].Name + " mended.");
                PromptProgress = Ship.Model.NeedsRepair(part) ? Ship.Model.RepairProgress(part) : -1f;
            }
            return true;
        }

        bool LookingAt(Vector3 shipPoint, float reach, float degrees, Vector3 eye, Vector3 forward)
        {
            Vector3 to = Ship.transform.TransformPoint(shipPoint) - eye;
            return to.magnitude <= reach && Vector3.Angle(forward, to) <= degrees;
        }

        static string Key(InputAction action) => GameInput.Describe(action);

        // Prompts are shown every frame but only change when a key is rebound or the controller is picked up,
        // so the finished words are kept for a second at a time and no text is built on most frames.
        readonly System.Collections.Generic.Dictionary<int, string> _words = new System.Collections.Generic.Dictionary<int, string>();
        float _wordsUntil;

        string Words(int id, string before, InputAction action, string after, string name = null)
        {
            if (Time.unscaledTime > _wordsUntil)
            {
                _words.Clear();
                _wordsUntil = Time.unscaledTime + 1f;
            }
            if (_words.TryGetValue(id, out string words)) return words;
            words = before + Key(action) + after + (name != null ? name.ToLowerInvariant() : string.Empty);
            _words[id] = words;
            return words;
        }

        // ------------------------------------------------------------------ on foot outside

        void FixedUpdate()
        {
            if (Mode != AstronautMode.Outside || Ship == null) return;
            float dt = Time.fixedDeltaTime;
            SuitRules rules = SuitDials.rules;

            // What is pulling, how hard, and how high above its ground you are.
            Vector3 pull = _suitGravity.FeltGravity;
            float weight = pull.magnitude;
            int body = _system != null ? _suitGravity.Neighbourhood : -1;
            bool hasBody = body >= 0 && _system != null && body < _system.Bodies.Length;
            float radius = hasBody ? (float)_system.Model.Spec(body).radius : 1f;
            float height = hasBody ? HeightAbove(body) : float.MaxValue;

            Posture next = SuitPosture.Next(rules, Posture, weight, height, radius, _grounded);
            if (next != Posture) SwitchPosture(next, pull, weight);

            // The directions the jets push in: your own right, up and forward.
            Quaternion frame;
            Vector3 groundVelocity = Vector3.zero;
            if (Posture == Posture.Upright)
            {
                // "Up" is away from whatever is pulling.
                if (weight > 0.05f) _up = Vector3.Slerp(_up, -pull / weight, 1f - Mathf.Exp(-8f * dt)).normalized;
                Quaternion rotation = Suit.rotation;
                rotation = Quaternion.FromToRotation(rotation * Vector3.up, _up) * rotation;
                Suit.MoveRotation(rotation);
                // Walk, and fly, the way you are looking.
                frame = rotation * Quaternion.Euler(0f, _pendingYaw, 0f);

                // What is underfoot, and how it is moving.
                _grounded = false;
                Vector3 from = Suit.position + _up * 0.5f;
                int hits = Physics.SphereCastNonAlloc(from, 0.25f, -_up, _hits, 0.36f, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                for (int i = 0; i < hits; i++)
                {
                    if (_hits[i].rigidbody == Suit || _hits[i].distance >= nearest) continue;
                    nearest = _hits[i].distance;
                    _grounded = true;
                    groundVelocity = _hits[i].rigidbody != null ? _hits[i].rigidbody.GetPointVelocity(_hits[i].point) : Vector3.zero;
                }
            }
            else
            {
                // Floating, but something may still be pulling you gently onto ground: feel for it along the
                // pull, so touching down stands you up wherever the ground is.
                _grounded = false;
                if (weight > rules.freePull)
                {
                    Vector3 down = pull / weight;
                    int hits = Physics.SphereCastNonAlloc(Suit.worldCenterOfMass, 0.25f, down, _hits, ShipLayout.PersonHeight * 0.5f + 0.15f, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < hits; i++)
                    {
                        if (_hits[i].rigidbody != Suit) _grounded = true;
                    }
                }
                frame = Suit.rotation * _viewLocal;
            }

            // A blow that winds you costs you the controls for as long as it lasts.
            bool live = GameInput.Asset != null && !GameSession.Paused && !Dead && !Dazed;
            Vector2 move = live ? Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            bool jetUp = live && GameInput.Jump.IsPressed(), jetDown = live && GameInput.JetDown.IsPressed();
            bool matchHeld = live && (GameInput.SuitMatch.IsPressed() || _mendingNow);

            // What Match Velocity would match, and your speed against it (shown on the suit's readout).
            Vector3 toShip = Ship.Body.worldCenterOfMass - Suit.worldCenterOfMass;
            float shipDistance = toShip.magnitude;
            float offShip = shipDistance > 0.5f ? Vector3.Angle(Eye.transform.forward, toShip) : 0f;
            // Chosen when the button goes down and kept while it is held, so turning your head cannot change it.
            if (!matchHeld || !_matchWasHeld) _matchTarget = _mendingNow ? MatchTarget.Ship : Jetpack.Choose(rules, shipDistance, offShip);
            _matchWasHeld = matchHeld;
            MatchTarget target = _matchTarget;
            bool againstGround = hasBody && height < Mathf.Max(GroundMatchLeast, radius * GroundMatchShare);
            // (Mending a tumbling ship, you are held against the part of it beside you, not its middle.)
            Vector3 reference = target == MatchTarget.Ship
                ? (_mendingNow ? Ship.Body.GetPointVelocity(Suit.worldCenterOfMass) : Ship.Body.linearVelocity)
                : !hasBody ? Vector3.zero
                : againstGround ? _system.GroundVelocityAt(body, Suit.position) : _system.LocalVelocityOf(body);
            Vector3 relative = Suit.linearVelocity - reference;

            Vector3 jets = Vector3.zero;
            float factor = SuitTanks.PushFactor;
            if (_grounded)
            {
                // On your feet: walking, and the push off for a jump. No jets, no fuel.
                Vector3 wish = (frame * Vector3.right * move.x + frame * Vector3.forward * move.y) * _config.outsideWalkSpeed;
                Vector3 onGround = Suit.linearVelocity - groundVelocity;
                Vector3 vertical = Vector3.Project(onGround, _up);
                Vector3 level = Vector3.MoveTowards(onGround - vertical, wish, 40f * dt);
                if (_jumpQueued)
                {
                    // The same push off whatever the gravity, so low gravity means high, slow jumps.
                    vertical = _up * Mathf.Sqrt(2f * 9.81f * _config.outsideJumpHeight);
                    _grounded = false;
                }
                Suit.linearVelocity = groundVelocity + level + vertical;
            }
            else
            {
                if (matchHeld)
                {
                    P3 push = Jetpack.Match(rules, new P3(relative.x, relative.y, relative.z), dt, factor);
                    jets = new Vector3(push.X, push.Y, push.Z);
                }
                else
                {
                    // Off the ground the stick is the jets, at once and every time. Nothing else moves you:
                    // let go and you keep exactly the speed you had.
                    float fine = live && GameInput.Sprint.IsPressed() ? rules.fineShare : 1f;
                    P3 wish = new P3(move.x, (jetUp ? 1f : 0f) - (jetDown ? 1f : 0f), move.y) * fine;
                    P3 push = Jetpack.Push(rules, wish, factor);
                    jets = frame * new Vector3(push.X, push.Y, push.Z);
                }
                if (jets.sqrMagnitude > 0f) Suit.AddForce(jets, ForceMode.Acceleration);
            }
            _jumpQueued = false;

            // The nozzles that are lit, and what the push costs.
            float full = Mathf.Max(0.1f, rules.jetPush);
            _jetLocal = Quaternion.Inverse(frame) * jets / full;
            _jetPush = Mathf.Clamp01(_jetLocal.magnitude);
            Jets.Tick(dt, new P3(_jetLocal.x, _jetLocal.y, _jetLocal.z));
            if (Jets.Lit) _jetPuff = true;

            // The tether: stops you at its length, and nothing more.
            Vector3 anchor = TetherAnchor;
            float fromAnchor = Vector3.Distance(Suit.worldCenterOfMass, anchor);
            TetherTaut = false;
            if (Tethered)
            {
                Vector3 offset = Suit.worldCenterOfMass - anchor;
                Vector3 against = Suit.linearVelocity - Ship.Body.GetPointVelocity(anchor);
                P3 change = Tether.Pull(rules, new P3(offset.x, offset.y, offset.z), new P3(against.x, against.y, against.z), out bool taut, out P3 back);
                if (taut)
                {
                    Suit.linearVelocity += new Vector3(change.X, change.Y, change.Z);
                    // Only a real overrun is put right by moving you (a fast drift can pass the end in one
                    // step). Small ones are left alone: moving the body every step would shake the view.
                    if (back.Length > 0.05f) Suit.position += new Vector3(back.X, back.Y, back.Z);
                    // Coming up short on the line at speed is a blow like any other, though the harness
                    // spreads it: it can wind you, never kill you.
                    float jolt = change.Length;
                    if (jolt > rules.hurtSpeed) SuitHit(Mathf.Min(jolt, rules.fatalSpeed - 0.5f), -offset.normalized);
                }
                // Held taut for as long as you hang at the end of it, not only on the step it catches you.
                TetherTaut = taut || offset.magnitude > rules.tetherLength - 0.05f;
            }

            SuitState.MatchTarget = target;
            SuitState.Matching = matchHeld && !_grounded && relative.sqrMagnitude > 0.04f;
            SuitState.Speed = relative.magnitude;
            SuitState.AgainstGround = againstGround;
            SuitState.GroundName = hasBody ? BodyName(body) : string.Empty;
            SuitState.ShipDistance = fromAnchor;
            SuitState.TetherInReach = Tether.InReach(rules, fromAnchor);
        }

        // The real ground can be far below a body's nominal radius (gas giants), so the planets are asked. Looked
        // up a few times a second and carried between by how far you have risen or sunk.
        float _heightRead, _heightSphere;
        int _heightBody = -1, _heightSteps;

        float HeightAbove(int body)
        {
            float sphere = (float)_system.AltitudeAbove(body, Suit.position);
            if (body != _heightBody || --_heightSteps <= 0)
            {
                _heightBody = body;
                _heightSteps = 6;
                _heightSphere = sphere;
                Planets.PlanetWorld planets = Planets.PlanetWorld.Instance;
                Planets.GroundReading reading = planets != null ? planets.Read(body, Suit.position) : default(Planets.GroundReading);
                _heightRead = reading.Known ? (float)reading.AboveGround : sphere;
            }
            return _heightRead + (sphere - _heightSphere);
        }

        // Close above a body, speed is measured against the ground turning under you (the same rule the ship uses).
        const float GroundMatchLeast = 300f, GroundMatchShare = 0.5f;

        string[] _bodyNames;
        string BodyName(int body)
        {
            if (_bodyNames == null)
            {
                _bodyNames = new string[_system.Bodies.Length];
                for (int i = 0; i < _bodyNames.Length; i++) _bodyNames[i] = _system.Bodies[i].Spec.id.ToUpperInvariant();
            }
            return _bodyNames[body];
        }

        /// <summary>
        /// Changes between standing head-up and floating free without the view jumping: the way you are
        /// looking is kept exactly, and any tilt left over is eased out.
        /// </summary>
        void SwitchPosture(Posture next, Vector3 pull, float weight)
        {
            if (next == Posture.Free)
            {
                _viewLocal = Quaternion.Euler(0f, _pendingYaw, 0f) * Quaternion.Euler(_pitch, 0f, _rollEase);
                _rollEase = 0f;
            }
            else
            {
                Quaternion view = Suit.rotation * _viewLocal;
                Vector3 eye = Suit.position + Suit.rotation * EyeLocal;
                Vector3 up = weight > 0.01f ? -pull / weight : view * Vector3.up;
                Vector3 forward = view * Vector3.forward;
                Vector3 flat = Vector3.ProjectOnPlane(forward, up);
                // Looking straight up or down, the top of your head says which way is "ahead".
                if (flat.sqrMagnitude < 0.01f) flat = Vector3.ProjectOnPlane(view * (Vector3.Dot(forward, up) > 0f ? Vector3.down : Vector3.up), up);
                Quaternion upright = Quaternion.LookRotation(flat.normalized, up);
                _pendingYaw = 0f;
                _pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(Vector3.Dot(forward, up), -1f, 1f)) * Mathf.Rad2Deg, -88f, 88f);
                Quaternion left = Quaternion.Inverse(upright * Quaternion.Euler(_pitch, 0f, 0f)) * view;
                _rollEase = Mathf.DeltaAngle(0f, left.eulerAngles.z);
                _up = up;
                // The body turns about your eyes, so what you see does not move.
                Vector3 feet = eye - upright * EyeLocal;
                // If standing up would put the body through something (the hull beside you), wait until it is clear.
                if (!_grounded)
                {
                    int touching = Physics.OverlapCapsuleNonAlloc(feet + upright * (Vector3.up * 0.35f), feet + upright * (Vector3.up * (ShipLayout.PersonHeight - 0.35f)), 0.28f, _overlaps, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < touching; i++)
                    {
                        if (_overlaps[i].attachedRigidbody != Suit) return;
                    }
                }
                Suit.transform.SetPositionAndRotation(feet, upright);
                Suit.position = feet;
                Suit.rotation = upright;
            }
            Posture = next;
            // What "forward" means has just changed, so say so.
            Say(next == Posture.Free ? Words(10, "Floating free: no up or down. Roll with ", GameInput.SuitRollLeft, " and " + Key(GameInput.SuitRollRight) + ".") : "Gravity has you: head up.");
            PoseOutside();
        }

        // ------------------------------------------------------------------ the belt you can see

        void PoseBelt()
        {
            bool seated = Mode == AstronautMode.Seated;
            float draw = seated ? Ship.Belt.Draw : 0f;
            bool show = draw > 0.01f;
            if (_buckle.gameObject.activeSelf != show)
            {
                _buckle.gameObject.SetActive(show);
                _strapLeft.gameObject.SetActive(show);
                _strapRight.gameObject.SetActive(show);
                _strapLap.gameObject.SetActive(show);
            }
            if (!show) return;
            // The buckle is drawn from the right hip up and across to the middle of the lap.
            Vector3 buckle = Vector3.Lerp(HipRight, Lap, draw) + Vector3.up * (Mathf.Sin(draw * Mathf.PI) * 0.1f);
            _buckle.localPosition = buckle;
            _buckle.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            Stretch(_strapRight, ShoulderRight, buckle, 1f);
            Stretch(_strapLap, HipRight, buckle, 1f);
            // The other shoulder strap and lap strap come across to meet it at the end.
            Stretch(_strapLeft, ShoulderLeft, Vector3.Lerp(ShoulderLeft + Vector3.down * 0.15f, buckle, draw), 1f);
        }

        /// <summary>Lays a strap (built one unit long along its own X) between two points in the ship's space.</summary>
        static void Stretch(Transform strap, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            if (length < 0.001f) { strap.localScale = new Vector3(0.001f, thickness, thickness); return; }
            strap.localPosition = from;
            // The strap's flat face should look roughly toward the wearer's chest (the ship's nose direction).
            strap.localRotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.forward).normalized, Vector3.Cross(along, Vector3.Cross(along, Vector3.forward)).normalized) * Quaternion.Euler(0f, -90f, 0f);
            strap.localScale = new Vector3(length, thickness, thickness);
        }

        public string DescribeForDebug()
        {
            return "You: " + Mode + (Mode == AstronautMode.Outside ? (_grounded ? " (on the ground)" : " (in the air)") : string.Empty);
        }

        void OnDestroy()
        {
            if (Suit != null)
            {
                var capsule = Suit.GetComponent<CapsuleCollider>();
                if (capsule != null && capsule.sharedMaterial != null) Destroy(capsule.sharedMaterial);
                Destroy(Suit.gameObject);
            }
            if (_webbing != null) Destroy(_webbing);
            if (_strapLeft != null) Destroy(_strapLeft.GetComponent<MeshFilter>().sharedMesh);
            if (_buckle != null) Destroy(_buckle.GetComponent<MeshFilter>().sharedMesh);
        }
    }

    /// <summary>Sits on the suit's physics body and passes on what it runs into.</summary>
    public sealed class SuitBody : MonoBehaviour
    {
        public Astronaut Owner { get; set; }

        void OnCollisionEnter(Collision collision)
        {
            if (Owner == null || collision.contactCount == 0) return;
            // Small loose things (a gib, a dropped gun) do not hurt.
            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic && other.mass < 20f) return;
            Vector3 normal = collision.GetContact(0).normal;
            Owner.SuitHit(Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal)), -normal);
        }
    }
}
