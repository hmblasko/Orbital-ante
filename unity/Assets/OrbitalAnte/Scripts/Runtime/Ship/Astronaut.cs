using OrbitalAnte.Core;
using OrbitalAnte.Player;
using OrbitalAnte.Space;
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
    /// can shake them through a wall. Outside, a simple suit body takes over under the planet's gravity; the
    /// proper suit and jetpack are Milestone 11.
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
        float _pendingYaw;
        bool _jumpQueued;
        Vector3 _up = Vector3.up;
        bool _grounded;

        Transform _strapLeft, _strapRight, _strapLap, _buckle;
        Material _webbing;
        static readonly Vector3 ShoulderLeft = new Vector3(-0.2f, 1.06f, 1.02f);
        static readonly Vector3 ShoulderRight = new Vector3(0.2f, 1.06f, 1.02f);
        static readonly Vector3 HipLeft = new Vector3(-0.3f, 0.52f, 1.22f);
        static readonly Vector3 HipRight = new Vector3(0.3f, 0.52f, 1.22f);
        static readonly Vector3 Lap = new Vector3(0f, 0.6f, 1.44f);

        readonly RaycastHit[] _hits = new RaycastHit[6];

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
            suitGo.SetActive(false);

            a._walker.WalkSpeed = config.walkSpeed;
            a._walker.Gravity = config.cabinGravity;
            a._walker.JumpHeight = config.jumpHeight;

            a.BuildBelt();
            if (config.testPistol) a.BuildPistol();

            // A run starts strapped in, as if you had just finished your checks.
            ship.Belt.StartBuckled();
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
            SetMode(AstronautMode.Outside);
        }

        void BoardLift(Vector3 feetInShip)
        {
            // Keep looking the same way as the view moves from the suit's space to the ship's.
            Vector3 forward = Ship.transform.InverseTransformDirection(transform.forward);
            _yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            transform.SetParent(Ship.transform, false);
            Ship.Hatch.BeginUp(new P3(feetInShip.x, feetInShip.y, feetInShip.z));
            SetMode(AstronautMode.Lift);
            PoseLift();
        }

        static Vector3 V(P3 p) => new Vector3(p.X, p.Y, p.Z);

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (Ship == null || GameInput.Asset == null || GameSession.Paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Prompt = string.Empty;
            PromptProgress = -1f;

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
            yawDelta = 0f;
            Vector2 look = GameInput.Look.ReadValue<Vector2>();
            if (look.sqrMagnitude <= 0f) return;
            InputControl control = GameInput.Look.activeControl;
            if (control != null && control.device is Pointer)
            {
                // Mouse movement is already "distance moved this frame".
                yawDelta = look.x * _look.mouseSensitivity;
                _pitch -= look.y * _look.mouseSensitivity;
            }
            else
            {
                float magnitude = Mathf.Clamp01(look.magnitude);
                float curved = Mathf.Pow(magnitude, Mathf.Max(1f, _look.stickCurve));
                Vector2 shaped = magnitude > 0.0001f ? look / magnitude * curved : Vector2.zero;
                yawDelta = shaped.x * _look.stickYawSpeed * dt;
                _pitch -= shaped.y * _look.stickPitchSpeed * dt;
            }
            _pitch = Mathf.Clamp(_pitch, -88f, 88f);
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
                float bottom = Ship.FindLiftBottom();
                if (float.IsNaN(bottom))
                {
                    Prompt = "The lift is holding you: there is nothing below to stand on.";
                }
                else
                {
                    Ship.Hatch.BeginDown(new P3(_walker.X, 0f, _walker.Z), bottom);
                    SetMode(AstronautMode.Lift);
                    return;
                }
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
                // Measured again now: the ship may have settled or slid while you rode down.
                float bottom = Ship.FindLiftBottom();
                if (float.IsNaN(bottom))
                {
                    // The ground has gone (the ship lifted or rolled). Back up into the cabin.
                    Ship.Hatch.BeginUp(Ship.Hatch.Rider);
                    return;
                }
                Vector3 local = V(Ship.Hatch.Rider);
                local.y = Mathf.Max(local.y, bottom);
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
            ReadLook(dt, out float yawDelta);
            // Turning is applied to the view at once. The suit's body only keeps you upright.
            _pendingYaw += yawDelta;
            transform.localRotation = Quaternion.Euler(0f, _pendingYaw, 0f);
            if (GameInput.Jump.WasPressedThisFrame()) _jumpQueued = true;
            Eye.transform.localPosition = new Vector3(0f, ShipLayout.EyeHeight, 0f);
            Eye.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            Vector3 feetInShip = Quaternion.Inverse(Ship.Body.rotation) * (Suit.position - Ship.Body.position);
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
            if (Mending(dt)) return;

            Vector3 eye = Eye.transform.position, forward = Eye.transform.forward;
            if (LookingAt(V(ShipLayout.OutsideHatchButton), 2.6f, 30f, eye, forward))
            {
                Prompt = Ship.Hatch.WantOpen ? Words(2, null, GameInput.Interact, ": close the hatch") : Words(3, null, GameInput.Interact, ": open the hatch");
                if (GameInput.Interact.WasPressedThisFrame()) ToggleHatch();
            }
            else if (away < _config.remoteHatchRange)
            {
                Prompt = Ship.Hatch.WantOpen ? "Stand under the blue ring to go aboard" : Words(5, null, GameInput.Hatch, ": open the hatch");
            }
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

            // "Up" is away from whatever is pulling. With next to no pull, keep whatever up was.
            Vector3 pull = _suitGravity.FeltGravity;
            float weight = pull.magnitude;
            if (weight > 0.25f) _up = Vector3.Slerp(_up, -pull / weight, 1f - Mathf.Exp(-8f * dt)).normalized;

            Quaternion rotation = Suit.rotation;
            rotation = Quaternion.FromToRotation(rotation * Vector3.up, _up) * rotation;
            Suit.MoveRotation(rotation);
            // Walk the way you are looking.
            rotation = rotation * Quaternion.Euler(0f, _pendingYaw, 0f);

            // What is underfoot, and how it is moving.
            Vector3 groundVelocity = Vector3.zero;
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

            Vector2 move = GameInput.Asset != null && !GameSession.Paused ? Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            Vector3 wish = (rotation * Vector3.right * move.x + rotation * Vector3.forward * move.y) * _config.outsideWalkSpeed;

            Vector3 relative = Suit.linearVelocity - groundVelocity;
            Vector3 vertical = Vector3.Project(relative, _up);
            Vector3 level = relative - vertical;
            if (_grounded)
            {
                level = Vector3.MoveTowards(level, wish, 40f * dt);
                if (_jumpQueued)
                {
                    // The same push off whatever the gravity, so low gravity means high, slow jumps.
                    vertical = _up * Mathf.Sqrt(2f * 9.81f * _config.outsideJumpHeight);
                    _grounded = false;
                }
            }
            else if (weight > 0.25f)
            {
                // A little steering in the air; none at all floating free (that is what the jetpack will be for).
                level = Vector3.MoveTowards(level, wish, 5f * dt);
            }
            _jumpQueued = false;
            Suit.linearVelocity = groundVelocity + level + vertical;
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
}
