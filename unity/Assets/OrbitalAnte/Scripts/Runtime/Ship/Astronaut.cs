using System;
using OrbitalAnte.AI;
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
        /// <summary>In the driver's seat of the moon rover (Rover/MoonRover), out in the open in the suit.</summary>
        Driving,
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
        /// <summary>The melee slot (a found item, separate from the guns), next to the guns on the eye.</summary>
        public OrbitalAnte.Melee.PlayerMelee Melee { get; private set; }
        /// <summary>How much more the suit can take from being shot. Mends by itself when left alone.</summary>
        public PlayerHealth Armour { get; private set; }
        /// <summary>The suit's physics body, used only outside.</summary>
        public Rigidbody Suit { get; private set; }
        /// <summary>The moon rover the lander carries, or null if it carries none. Set when the world is built.</summary>
        public OrbitalAnte.Rover.MoonRover Rover { get; set; }
        /// <summary>Outside in the suit, on foot or driving: where you breathe the suit's air and can be shot.</summary>
        public bool InTheOpen => Mode == AstronautMode.Outside || Mode == AstronautMode.Driving;

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
        // Where the head is turned while buckled in, measured from straight ahead.
        float _seatYaw, _seatPitch;
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
        /// <summary>Walking pace outside as a share of normal, for carrying a crate of salvage (the ante tasks set it).</summary>
        public static float CarryPace = 1f;
        public static void ResetStatics() { OnDeath = null; CarryPace = 1f; Current = null; }
        /// <summary>The player of the run going now, or null (the arena, the title screen).</summary>
        public static Astronaut Current { get; private set; }
        /// <summary>The player is dead or dying (the burst plays before the death page): nothing may be banked or picked.</summary>
        public static bool PlayerDown => Current != null && Current.Dead;
        /// <summary>Strapped into the seat, where Select is the floodlights and the Gameplay map is off.</summary>
        public static bool PlayerSeated => Current != null && Current.Mode == AstronautMode.Seated;
        /// <summary>Inside the ship (seat, cabin or lift), where the ship's damage list fills the top right.</summary>
        public static bool PlayerInShip => Current != null && (Current.Mode == AstronautMode.Seated || Current.Mode == AstronautMode.Aboard || Current.Mode == AstronautMode.Lift);
        /// <summary>Outside and off the ground: on the jets, jumping, floating. (For the style kills.)</summary>
        public bool Airborne => Mode == AstronautMode.Outside && !_grounded;
        float _dazed, _deadFor;
        bool _posed, _deathTold;
        // The way the last blow pushed (world), and the burst of the body when you die (null if it could not be made).
        Vector3 _lastBlow;
        PlayerBurstFx _burst;
        float _pendingYaw;
        // A press kept for a moment, and ground that counts for a moment after the feet leave it (Spacewalk/JumpGrace).
        readonly JumpGrace _jumpGrace = new JumpGrace();
        Vector3 _lastGroundVelocity;
        // On foot: standing, crouched or prone, and the quiet sneak (see Spacewalk/Stances.cs).
        public StanceState Stances { get; } = new StanceState();
        public bool Sneaking { get; private set; }
        /// <summary>
        /// Out on foot and keeping hidden: crouched, prone, sneaking, or in a cave dark enough to halve how far you
        /// are seen. A long quiet never sends a squad to a hiding player (Enemies/LullRules).
        /// </summary>
        public bool Hiding { get; private set; }
        CapsuleCollider _body;
        float _walkingAt, _airborneFor;
        Func<Stance, bool> _roomFor;
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
        [System.NonSerialized] public SuitInputs SuitState;
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
        /// <summary>The speed Match Velocity would match, kept from the last physics step (a shot's drift cap reads it).</summary>
        Vector3 _matchReference;
        SuitFx _fx;
        float _airDrawn, _jetPush, _rollEase;
        Quaternion _viewLocal = Quaternion.identity;
        // The gun's kick on the view (PlayerWeapons kicks it; ReadLookRaw turns the head by it).
        readonly OrbitalAnte.Player.ViewRecoil _recoil = new OrbitalAnte.Player.ViewRecoil();
        // Controller aim assist (Player/AimAssist): only ever slows the sticks over an enemy.
        readonly AimAssist _aimAssist = new AimAssist();
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
            Current = a;
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
            a._body = capsule;
            a._roomFor = a.RoomFor;
            capsule.center = new Vector3(0f, ShipLayout.PersonHeight * 0.5f, 0f);
            capsule.height = ShipLayout.PersonHeight;
            capsule.radius = ShipLayout.PersonRadius;
            // Slippery, so the suit slides along walls instead of sticking to them. Walking speed is set directly.
            capsule.sharedMaterial = new PhysicsMaterial("Suit") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            a._suitGravity = suitGo.AddComponent<GravityBody>();
            suitGo.AddComponent<SuitBody>().Owner = a;
            // The suit can be shot. It takes what the test player takes and mends after a few quiet seconds.
            a.Armour = suitGo.AddComponent<PlayerHealth>();
            a.Armour.Initialize(a._look);
            a.Armour.Damaged += (amount, direction) => a._lastBlow = direction;
            a.Armour.Died += () => a.Die(a.Mode == AstronautMode.Driving ? "Killed while driving the rover." : "Shot down outside the ship.");
            // Measured from the boots. The boots are the lowest point of the suit's capsule and never honestly
            // below the ground (on a slope they stand a little above it), and the ground checked is the real,
            // built one, so a quarter of a metre deep is already sunk (about where the check for ground under the
            // feet stops finding it). You are put back standing just on it, not dropped from a height. (It was
            // knee deep when the check used the ideal ground, which could be a metre off.)
            a._suitGravity.NeedsGround = true;
            a._suitGravity.SunkBelow = -0.25f;
            a._suitGravity.PutBackAt = 0.2f;
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
            // The small kick of the view when a limb pops (Milestone 2), as in the arena; Pause > Feel switches it off.
            _hitShake = Eye.gameObject.AddComponent<CameraShake>();
            Weapons.Initialize(_look, Eye, null, Armour, _world);
            // The real game has no arena controller, so the gun's kick goes to the astronaut's own head.
            Weapons.Recoil = _recoil;
            // The gun ladder's first gun (the rifle when the ladder is switched off). Kills climb it.
            Weapons.EquipIssued(Weapon.Create(OrbitalAnte.Combat.GunLadderFx.StartingGun(), _world, true));
            // A rare gun that blows up ends the run, like any other death, with the cause on the death page.
            Weapons.Backfired += Die;
            Weapons.CarriedVelocity = () => Mode == AstronautMode.Outside ? Suit.linearVelocity
                : Ship != null ? Ship.Body.GetPointVelocity(Eye.transform.position) : Vector3.zero;
            Weapons.UpNow = () => Mode == AstronautMode.Outside ? Suit.rotation * Vector3.up
                : Ship != null ? Ship.transform.up : Vector3.up;
            Weapons.InShip = () => Mode != AstronautMode.Outside;
            // Floating free, a shot pushes you back like a melee hit does (Player/ZeroGKick). Only the suit's speed.
            Weapons.FloatingFree = () => Mode == AstronautMode.Outside && Posture == Posture.Free;
            Weapons.ShooterVelocity = () => Suit != null ? Suit.linearVelocity - _matchReference : Vector3.zero;
            Weapons.PushBack = push => { if (Mode == AstronautMode.Outside && Suit != null) Suit.linearVelocity += push; };

            // The melee slot: empty until an item is found. Floating free, a hit pushes you back (by the struck
            // mass against yours, capped; only ever the suit's speed, never a turn of the view).
            Melee = Eye.gameObject.AddComponent<OrbitalAnte.Melee.PlayerMelee>();
            Melee.Initialize(Eye, Weapons, _world);
            Melee.CarriedVelocity = Weapons.CarriedVelocity;
            Melee.UpNow = Weapons.UpNow;
            Melee.InShip = Weapons.InShip;
            Melee.FloatingFree = () => Mode == AstronautMode.Outside && Posture == Posture.Free;
            Melee.PushBack = push => { if (Mode == AstronautMode.Outside && Suit != null) Suit.linearVelocity += push; };
            Melee.CanAct = () => !Dead && !Dazed;
        }

        // ------------------------------------------------------------------ changing mode

        void SetMode(AstronautMode mode)
        {
            Mode = mode;
            // Sitting down or taking the wheel ends any kick still settling (no gun in those hands).
            if (mode == AstronautMode.Seated || mode == AstronautMode.Driving) { _recoil.Clear(); _aimAssist.Clear(); }
            bool outside = mode == AstronautMode.Outside;
            Ship.CrewAboard = mode == AstronautMode.Aboard;
            if (!outside && Suit.gameObject.activeSelf) Suit.gameObject.SetActive(false);
            if (_system != null) _system.Focus = outside ? _suitGravity : mode == AstronautMode.Driving && Rover != null ? Rover.Gravity : Ship.Gravity;

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
                // A gun handed over later (the gun ladder) takes the same light.
                Weapons.Indoors = !outside;
            }
            if (Melee != null)
            {
                Melee.enabled = armed;
                if (Melee.Held != null)
                {
                    Melee.Held.gameObject.SetActive(armed);
                    OrbitalAnte.Look.ModelLibrary.SetIndoors(Melee.Held.gameObject, !outside);
                }

            }
            GameInput.SetFlying(mode == AstronautMode.Seated && Ship.Belt.Buckled);
            if (!outside && SuitDials != null) { Stances.Reset(); FitBody(); Sneaking = false; Hiding = false; }
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
            _seatYaw = 0f;
            _seatPitch = 0f;
            Ship.Flight.PilotLook = Vector3.zero;
            SetMode(AstronautMode.Aboard);
            PoseAboard();
        }

        /// <summary><paramref name="roverUp"/>: climbing out of the rover, which way is up there (away from the
        /// world, never the rover's own up: it may be on its side).</summary>
        void GoOutside(Vector3 feet, Vector3 velocity, Vector3? roverUp = null)
        {
            bool fromRover = roverUp.HasValue;
            _up = fromRover ? roverUp.Value : Ship.transform.up;
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
            // (Not climbing out of the rover, which may be a long way from the ship.)
            if (!fromRover && !landed && !Tethered) SetTether(true, quiet: false);
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
            // Safe aboard: the suit is patched up (it cannot mend itself while it is put away).
            if (Armour != null && !Dead) Armour.Revive();
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
            if (Dead || Crash == null || InTheOpen) return;
            bool buckled = Mode == AstronautMode.Seated && Ship.Belt.Buckled;
            // A blow from below or from ahead throws the head forward; from the side, sideways.
            var fate = Crash.Hit(knock, buckled, from.x, Mathf.Clamp(from.z - from.y, -1f, 1f));
            // The blow came from there, so it threw you the other way.
            _lastBlow = -Ship.transform.TransformDirection(from);
            if (fate == OrbitalAnte.Navigation.PilotFate.Thrown) _dazed = Mathf.Max(_dazed, Crash.Rules.dazedSeconds);
            else if (fate == OrbitalAnte.Navigation.PilotFate.Killed) Die(OrbitalAnte.Navigation.CrashModel.Cause(Crash.LastKnock, buckled));
        }

        /// <summary>
        /// Ends the run. Always first person: your body bursts (see <see cref="DeathBurst"/>) and the view rides
        /// your head as it flies; then the screen says why. If the body cannot be made the view slumps instead.
        /// </summary>
        public void Die(string cause)
        {
            if (Dead) return;
            Dead = true;
            DeathCause = cause ?? string.Empty;
            _deadFor = 0f;
            Prompt = string.Empty;
            PromptProgress = -1f;
            Burst();
            if (Mode == AstronautMode.Driving && Rover != null) Rover.Leave();
        }

        void Burst()
        {
            if (Eye == null || Ship == null) return;
            try
            {
                bool outside = Mode == AstronautMode.Outside;
                bool driving = Mode == AstronautMode.Driving && Rover != null;
                // In the lift you may be in the shaft or under the belly, not in the cabin: the cage is only for the cabin.
                bool inCabin = !outside && !driving && Ship.transform.InverseTransformPoint(Eye.transform.position).y > 1f;
                Vector3 up = outside ? Suit.rotation * Vector3.up : driving ? Rover.transform.up : Ship.transform.up;
                Vector3 facing = Vector3.ProjectOnPlane(Eye.transform.forward, up);
                if (facing.sqrMagnitude < 1e-4f) facing = Vector3.ProjectOnPlane(Eye.transform.up, up);
                Quaternion rotation = Quaternion.LookRotation(facing.normalized, up);
                // The body is put so its head is where your eye was (see PlayerBurstFx.Begin), whether you stood or sat.
                // Crouched or lying down outside, the body stands on the ground under you instead (a standing body
                // put with its head at a low eye would start in the ground), and the view goes up with the head.
                bool low = outside && Stances.Current != Stance.Stand;
                Vector3 feet = low ? Suit.position : Eye.transform.position - up * ShipLayout.EyeHeight;
                Vector3 velocity = outside ? Suit.linearVelocity : driving ? Rover.VelocityAt(Eye.transform.position) : Ship.Body.GetPointVelocity(Eye.transform.position);
                Vector3 blow = _lastBlow.sqrMagnitude > 1e-6f ? _lastBlow : -Eye.transform.forward;

                // The gun goes too: outside it is flung away and keeps your speed; inside it is put out of sight
                // (dropped there it would start inside the hull's solid shapes).
                if (Weapons != null)
                {
                    Weapon held = Weapons.Current;
                    if (held != null)
                    {
                        if (outside)
                        {
                            // It leaves with the suit's speed already (PlayerWeapons.CarriedVelocity).
                            Weapons.DropCurrent();
                        }
                        else held.gameObject.SetActive(false);
                    }
                    Weapons.enabled = false;
                }
                // A melee item is lost when you die.
                if (Melee != null)
                {
                    Melee.LoseHeld();
                    Melee.enabled = false;
                }
                GravityBody pull = outside ? _suitGravity : driving ? Rover.Gravity : Ship.Gravity;
                _burst = PlayerBurstFx.Begin(_world, feet, rotation, velocity, blow, Eye, inCabin ? Ship : null,
                    () => pull != null ? pull.FeltGravity : Physics.gravity, headToEye: !low);
            }
            catch (Exception e)
            {
                // Never let the death itself fail: fall back to the slump.
                Debug.LogWarning("Orbital Ante: the player's body could not burst (" + e.Message + "); slumping instead.");
                _burst = null;
            }
        }

        void PoseOnly()
        {
            switch (Mode)
            {
                case AstronautMode.Seated: PoseSeated(); break;
                case AstronautMode.Aboard: PoseAboard(); break;
                case AstronautMode.Lift: PoseLift(); break;
                case AstronautMode.Outside: PoseOutside(); break;
                case AstronautMode.Driving: PoseDriving(); break;
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
            if (Dead && _burst != null) return;   // the view is on the flying head
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
            // Laid on last, over the pose made this frame, so it never piles up. Only where you can shoot.
            if (_hitShake != null && (Mode == AstronautMode.Outside || Mode == AstronautMode.Aboard)) Eye.transform.localRotation *= _hitShake.Offset;
        }

        CameraShake _hitShake;

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
                // Riding the head, the view is placed by the burst; otherwise it slumps where you were.
                if (_burst == null)
                {
                    PoseOnly();
                    _posed = true;
                }
                if (!_deathTold && _deadFor > (_burst != null ? DeathBurst.Hold : 1.8f))
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
                    // A gun's kick from this moment is lost in the tumble, not saved up for when you get up.
                    _recoil.Clear();
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
                case AstronautMode.Driving: UpdateDriving(dt); break;
            }
            PoseBelt();
        }

        /// <summary>Shows a notice, the way the cabin's own controls do (for the ship's fittings).</summary>
        public void Tell(string notice) => Say(notice);

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
            SeatLook(belt.Buckled);
            PoseSeated();
            // Head turned well away from the nose: Lock takes what is in front of your eyes, not the nose.
            bool away = belt.Buckled && (Mathf.Abs(_seatYaw) > 10f || Mathf.Abs(_seatPitch) > 10f);
            Ship.Flight.PilotLook = away ? Eye.transform.forward : Vector3.zero;
        }

        /// <summary>
        /// Buckled in, the mouse or right stick turns your head, not the ship: the ship is moved by its
        /// thrusters. Holding Turn Ship hands the mouse to the ship and brings your eyes back to the front, so
        /// you see where the nose is going. Looking down at the camera screen does the same.
        /// </summary>
        void SeatLook(bool buckled)
        {
            float dt = Time.unscaledDeltaTime;
            bool free = buckled && !Dead && !GameSession.Paused && GameInput.Asset != null
                        && !Ship.Flight.Steering && !Ship.Flight.ControlsLocked && Mathf.Abs(ScreenGlance) < 0.5f;
            if (!free)
            {
                float ease = 1f - Mathf.Exp(-6f * dt);
                _seatYaw = Mathf.Lerp(_seatYaw, 0f, ease);
                _seatPitch = Mathf.Lerp(_seatPitch, 0f, ease);
                return;
            }
            Vector2 look = GameInput.FlyLook.ReadValue<Vector2>();
            if (look.sqrMagnitude <= 0f) return;
            InputControl control = GameInput.FlyLook.activeControl;
            if (control != null && control.device is Pointer)
            {
                _seatYaw += look.x * _look.mouseSensitivity;
                _seatPitch -= look.y * _look.mouseSensitivity;
            }
            else
            {
                float magnitude = Mathf.Clamp01(look.magnitude);
                float curved = Mathf.Pow(magnitude, Mathf.Max(1f, _look.stickCurve));
                Vector2 shaped = magnitude > 0.0001f ? look / magnitude * curved : Vector2.zero;
                _seatYaw += shaped.x * _look.stickYawSpeed * dt;
                _seatPitch -= shaped.y * _look.stickPitchSpeed * dt;
            }
            // As far as a belted-in neck goes: over either shoulder, up at the roof, down at your knees.
            _seatYaw = Mathf.Clamp(_seatYaw, -SeatLookYaw, SeatLookYaw);
            _seatPitch = Mathf.Clamp(_seatPitch, -SeatLookUp, SeatLookDown);
        }

        public const float SeatLookYaw = 125f, SeatLookUp = 70f, SeatLookDown = 60f;
        /// <summary>How far the head is turned from straight ahead in the seat, in degrees (x right, y down).</summary>
        public Vector2 SeatLookNow => new Vector2(_seatYaw, _seatPitch);

        void PoseSeated()
        {
            SeatBelt belt = Ship.Belt;
            float sit = belt.Sit;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            // A glance down at your lap while the belt goes across, then eyes front.
            float glance = belt.State == BeltState.Buckling ? Mathf.Sin(Mathf.Clamp01((belt.Progress - 0.2f) / 0.75f) * Mathf.PI) * _config.buckleGlance : 0f;
            float yaw = Mathf.LerpAngle(_yawFrom, 0f, sit) + _seatYaw * sit;
            float pitch = Mathf.Lerp(_pitchFrom, 0f, sit) + glance + ScreenGlance + _seatPitch * sit;
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
            // A gun's kick turns the head first (the rifle climbs while the trigger is held; Part B Milestone 3).
            _recoil.Step(dt, out float kickUp, out float kickRight);
            yawDelta = kickRight;
            pitchDelta = -kickUp;
            Vector2 look = GameInput.Look.ReadValue<Vector2>();
            // A stick let go starts its next push with no slowdown left over.
            if (look.sqrMagnitude <= 0f) { _aimAssist.Clear(); return; }
            InputControl control = GameInput.Look.activeControl;
            if (control != null && control.device is Pointer)
            {
                // Mouse movement is already "distance moved this frame".
                yawDelta += look.x * _look.mouseSensitivity;
                pitchDelta += -look.y * _look.mouseSensitivity;
            }
            else
            {
                float magnitude = Mathf.Clamp01(look.magnitude);
                float curved = Mathf.Pow(magnitude, Mathf.Max(1f, _look.stickCurve));
                Vector2 shaped = magnitude > 0.0001f ? look / magnitude * curved : Vector2.zero;
                // Your own head does not slow when the world does (hitstop, Deep Breath).
                float own = TimeFlow.PlayerDelta(dt);
                // Over an enemy the sticks turn a little slower, so a thumb can stop on it (never a pull).
                float assist = AimAssistScale(own);
                yawDelta += shaped.x * _look.stickYawSpeed * own * assist;
                pitchDelta += -shaped.y * _look.stickPitchSpeed * own * assist;
            }
        }

        /// <summary>What the sticks' turning is multiplied by this frame: below 1 only over an enemy, with a gun out.</summary>
        float AimAssistScale(float dt)
        {
            AimAssistRules rules = _look.aimAssist.Safe();
            bool armed = Weapons != null && Weapons.enabled && Weapons.Current != null;
            bool on = rules.enabled && FeelSettings.AimAssist && armed && (Mode == AstronautMode.Aboard || Mode == AstronautMode.Outside);
            float closeness = on ? AimAssistProbe.BestCloseness(Eye.transform, Weapons.Current.Data.range, rules) : 0f;
            return _aimAssist.Step(dt, closeness, rules);
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
            // A heavy melee item (the mailbox) slows your walk aboard too.
            _walker.WalkSpeed = _config.walkSpeed * (Melee != null ? Melee.WalkSlow : 1f);
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

            // The hatch and the seat come first, so a cracked window nearby never swallows the button.
            // No lever: look at the hatch in the floor and press the button.
            Vector3 eye = Eye.transform.position, forward = Eye.transform.forward;
            if (LookingAt(Vector3.zero, 3.2f, 24f, eye, forward))
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
            UpdateStance(dt);
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
            // Look at the hatch in the belly (or the button beside it) and press the button.
            if (LookingAt(new Vector3(0f, ShipLayout.BellyY, 0f), HatchLookReach, 24f, eye, forward) || LookingAt(V(ShipLayout.OutsideHatchButton), 2.6f, 30f, eye, forward))
            {
                Prompt = Ship.Hatch.WantOpen ? Words(2, null, GameInput.Interact, ": close the hatch") : Words(3, null, GameInput.Interact, ": open the hatch");
                if (GameInput.Interact.WasPressedThisFrame()) ToggleHatch();
            }
            else if (WorldOffers.Ask(eye, forward, dt, out string offered, out float held))
            {
                // Something of the world's (a crate, a supply pod) took the prompt and the press.
                Prompt = offered;
                if (held >= 0f) PromptProgress = held;
            }
            else if (!flying && RoverOffered(eye, forward, dt))
            {
                // The rover: get in, winch it down from the lander, or (held) winch it back up.
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

        // ------------------------------------------------------------------ the moon rover

        float _roverHeld = -1f;
        /// <summary>Seconds Interact is held at the rover, parked under the davit, to winch it up instead of getting in.</summary>
        const float StowHold = 0.6f;

        /// <summary>Offers what the rover allows from where you are looking, and does it. True when the rover took the prompt.</summary>
        bool RoverOffered(Vector3 eye, Vector3 forward, float dt)
        {
            if (Rover == null) { _roverHeld = -1f; return false; }
            OrbitalAnte.Rover.MoonRover.Offer offer = Rover.OfferTo(eye, forward);
            switch (offer)
            {
                case OrbitalAnte.Rover.MoonRover.Offer.Unload:
                    Prompt = Words(20, null, GameInput.Interact, ": winch the rover down");
                    if (GameInput.Interact.WasPressedThisFrame())
                        Say(Rover.Unload() ? "Lowering the rover." : "No level ground under the rover to lower it onto.");
                    return true;
                case OrbitalAnte.Rover.MoonRover.Offer.LandFirst:
                    Prompt = "The rover can only be lowered with the lander standing on the ground";
                    return true;
                case OrbitalAnte.Rover.MoonRover.Offer.Drive:
                    bool canStow = Rover.CanStow;
                    Prompt = canStow ? Words(22, null, GameInput.Interact, ": drive the rover (hold: winch it up onto the lander)")
                                     : Words(21, null, GameInput.Interact, ": drive the rover");
                    if (GameInput.Interact.WasPressedThisFrame()) _roverHeld = 0f;
                    if (_roverHeld < 0f) return true;
                    if (!canStow) { _roverHeld = -1f; EnterRover(); return true; }
                    if (GameInput.Interact.IsPressed())
                    {
                        _roverHeld += dt;
                        PromptProgress = Mathf.Clamp01(_roverHeld / StowHold);
                        if (_roverHeld >= StowHold)
                        {
                            _roverHeld = -1f;
                            if (Rover.StartStow()) Say("Winching the rover up.");
                        }
                    }
                    else
                    {
                        // Let go before the hold: a press, so get in.
                        _roverHeld = -1f;
                        EnterRover();
                    }
                    return true;
                default:
                    _roverHeld = -1f;
                    return false;
            }
        }

        void EnterRover()
        {
            if (Rover == null) return;
            Rover.Board(this);
            if (Rover.Driver != this) return;
            // A line clipped to the lander would only drag the rover back.
            if (Tethered) SetTether(false, quiet: false);
            _jetPush = 0f;
            Jets.Reset();
            _seatYaw = 0f;
            _seatPitch = 8f;
            transform.SetParent(Rover.transform, false);
            SetMode(AstronautMode.Driving);
            PoseDriving();
            Say(Key(GameInput.Move) + ": drive.  " + Key(GameInput.Jump) + ": boost.  " + Key(GameInput.JetDown) + ": handbrake.  " + Key(GameInput.Interact) + ": get out.");
        }

        void UpdateDriving(float dt)
        {
            if (Rover == null || Rover.Driver != this) { LeaveRover(); return; }
            // The suit body is put away while you drive, so its armour does not tick by itself.
            if (Armour != null && !Armour.isActiveAndEnabled) Armour.Regenerate();
            ReadLookRaw(dt, out float yawDelta, out float pitchDelta);
            float limit = Rover.Rules.lookAround;
            _seatYaw = Mathf.Clamp(_seatYaw + yawDelta, -limit, limit);
            _seatPitch = Mathf.Clamp(_seatPitch + pitchDelta, -70f, 75f);
            PoseDriving();

            // A rover is easy to see and hear coming (its lamps, its dust, its motors in the ground).
            CombatWorld.PlayerSeenFrom = OrbitalAnte.Planets.CaveLight.VisibilityAt(transform.position + Rover.transform.up * 1.5f);
            CombatWorld.PlayerHeardFrom = 1.5f;
            CombatWorld.PlayerAimBelowEye = 0.3f;

            bool slow = Rover.Speed < Rover.Rules.leaveBelow;
            if (Rover.Overturned) Prompt = Words(24, null, GameInput.Reload, ": put the rover back on its wheels");
            else if (slow) Prompt = Words(23, null, GameInput.Interact, ": get out");
            if (GameInput.Interact.WasPressedThisFrame())
            {
                if (slow) { LeaveRover(); return; }
                Say("Slow down to climb out.");
            }
            if (GameInput.Hatch.WasPressedThisFrame() && Vector3.Distance(Rover.transform.position, Ship.transform.position) < _config.remoteHatchRange) ToggleHatch();
        }

        void PoseDriving()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            Eye.transform.localPosition = V(OrbitalAnte.Rover.RoverLayout.Eye);
            Eye.transform.localRotation = Quaternion.Euler(_seatPitch, _seatYaw, 0f);
        }

        /// <summary>Climbs out beside the driver's seat, onto the ground, moving as the rover was.</summary>
        void LeaveRover()
        {
            if (Rover == null) return;
            // Up is away from the world, not the rover's own up: it may be tipped over when you climb out.
            Vector3 felt = Rover.Gravity != null ? Rover.Gravity.FeltGravity : Vector3.zero;
            Vector3 up = felt.sqrMagnitude > 0.01f ? -felt.normalized : Rover.transform.up;
            // Out beside the seat, measured along the ground, then down onto it from above the rover's side.
            Vector3 middle = Rover.transform.position;
            Vector3 aside = Vector3.ProjectOnPlane(Rover.ExitFeet - middle, up);
            if (aside.sqrMagnitude < 0.25f) aside = Vector3.ProjectOnPlane(-Rover.transform.right, up).normalized * 1.75f;
            Vector3 feet = middle + aside;
            if (Physics.Raycast(feet + up * 2.5f, -up, out RaycastHit ground, 5f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) feet = ground.point + up * 0.05f;
            Vector3 velocity = Rover.VelocityAt(feet);
            if (Rover.Driver == this) Rover.Leave();
            _pitch = Mathf.Clamp(_seatPitch, -60f, 60f);
            GoOutside(feet, velocity, up);
        }

        /// <summary>The rover hit something hard enough to hurt (see RoverHandling.Hurt). <paramref name="from"/> is where the blow came from.</summary>
        public void RoverKnock(float knock, float damage, bool kills, Vector3 from, float speed)
        {
            if (Dead || Mode != AstronautMode.Driving) return;
            _lastBlow = -from;
            // The view takes the blow the way it does in the seat of the lander, belted in.
            Vector3 local = Eye.transform.InverseTransformDirection(from);
            if (Crash != null) Crash.Hit(knock, true, local.x, Mathf.Clamp(local.z - local.y, -1f, 1f));
            if (kills)
            {
                Die("Crashed the rover at " + Mathf.RoundToInt(speed * 3.6f) + " km/h.");
                return;
            }
            if (damage > 0f && Armour != null) Armour.TakeDamage(damage, -from);
        }

        /// <summary>
        /// Standing, crouching, prone and sneaking on foot, and how easy that makes you to see and hear. The stance
        /// button is Jets Down: on the ground the jets have nothing to do.
        /// </summary>
        void UpdateStance(float dt)
        {
            StanceRules rules = SuitDials.stances;
            // A moment off the ground (a bump, a step down) does not stand you up; a real fall or a jump does.
            _airborneFor = _grounded ? 0f : _airborneFor + dt;
            bool onFoot = Posture == Posture.Upright && _airborneFor < 0.3f;
            bool held = GameInput.JetDown.IsPressed();
            Stance before = Stances.Current;
            if (Stances.Step(dt, held, onFoot, _roomFor, rules))
            {
                FitBody();
                if (onFoot) Say(Stances.Current == Stance.Crouch ? "Crouching: slower, quieter, harder to see."
                    : Stances.Current == Stance.Prone ? "Lying flat: very slow, very hard to see."
                    : "Standing.");
            }
            else if (Stances.Blocked) Say("No room to get up here.");

            if (GameInput.Jump.WasPressedThisFrame())
            {
                // A jump from a crouch stands you up and goes; from prone you only come up to a crouch.
                if (Stances.RiseToJump(_roomFor)) _jumpGrace.Press(Time.time);
                if (Stances.Current != before) FitBody();
            }

            // Sneaking: hold Sneak, or push a controller's stick only part of the way.
            Vector2 move = GameInput.Move.ReadValue<Vector2>();
            var from = GameInput.Move.activeControl;
            bool pad = from != null && from.device is UnityEngine.InputSystem.Gamepad;
            float push = move.magnitude;
            Sneaking = onFoot && ((GameInput.Sneak != null && GameInput.Sneak.IsPressed()) || (pad && push > 0.12f && push < 0.6f));

            bool moving = onFoot && _walkingAt > 0.15f;
            bool shot = Weapons != null && Time.time - Weapons.LastShotTime < rules.shotLoudFor;
            // In the dark of a cave network you are picked out only from closer.
            float dark = OrbitalAnte.Planets.CaveLight.VisibilityAt(transform.position + transform.up);
            CombatWorld.PlayerSeenFrom = StanceState.SeenFrom(Stances.Current, moving || !onFoot, rules) * dark;
            Hiding = onFoot && (Stances.Current != Stance.Stand || Sneaking || dark < 0.7f);
            CombatWorld.PlayerHeardFrom = StanceState.HeardFrom(Stances.Current, moving, Sneaking, _jetPush > 0.05f, shot, rules);
            // The laser sword's hum carries whatever stance you are in: no sneaking with it.
            if (Melee != null && Melee.isActiveAndEnabled) CombatWorld.PlayerHeardFrom = Mathf.Max(CombatWorld.PlayerHeardFrom, Melee.Noise);
            CombatWorld.PlayerAimBelowEye = Mathf.Min(0.3f, Stances.Eye * 0.5f);
        }

        /// <summary>The suit's collider as tall as the stance, feet where they were.</summary>
        void FitBody()
        {
            if (_body == null) return;
            float height = Mathf.Max(StanceState.HeightOf(Stances.Current, SuitDials.stances), ShipLayout.PersonRadius * 2f + 0.01f);
            _body.height = height;
            _body.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>Whether there is room above you to be this tall (nothing solid in the way).</summary>
        bool RoomFor(Stance stance)
        {
            if (Suit == null) return true;
            float height = StanceState.HeightOf(stance, SuitDials.stances);
            float r = ShipLayout.PersonRadius * 0.9f;
            Vector3 up = Suit.rotation * Vector3.up;
            Vector3 low = Suit.position + up * (r + 0.15f), high = Suit.position + up * Mathf.Max(r + 0.15f, height - r);
            // Rock and walls, and enemies' bodies: you do not stand up into somebody.
            return !Physics.CheckCapsule(low, high, r, Layers.WorldMask | (1 << Layers.CharacterBody), QueryTriggerInteraction.Ignore);
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
                Eye.transform.localPosition = new Vector3(0f, Stances.Eye > 0f ? Stances.Eye : ShipLayout.EyeHeight, 0f);
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
            bool outside = InTheOpen;
            float push = Mode == AstronautMode.Outside ? _jetPush : 0f;
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
            if (from.sqrMagnitude > 1e-6f) _lastBlow = -from.normalized;
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

        /// <summary>
        /// How far from the middle of the belly you can look at the hatch and open it: anywhere under the ship
        /// between its legs, however tall they are.
        /// </summary>
        const float HatchLookReach = ShipLayout.BellyClearance + 0.9f;

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
            // The suit coasts on with nobody in it: no jets, no posture, just what pulls it.
            if (Dead) return;
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
                // From inside the body, never above it: lying flat under a low roof, a cast started over the body's
                // top would begin inside the rock and call it ground. It reaches the same depth below the feet.
                float castFrom = Mathf.Min(0.5f, (_body != null ? _body.height : 1.8f) - 0.26f);
                Vector3 from = Suit.position + _up * castFrom;
                int hits = Physics.SphereCastNonAlloc(from, 0.25f, -_up, _hits, 0.36f - (0.5f - castFrom), Layers.WorldMask, QueryTriggerInteraction.Ignore);
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
            // On your feet Jets Down is the stance button: a step that loses the ground for a moment must not fire
            // the jets (and make you loud) while you are lying down.
            if (Posture == Posture.Upright && _airborneFor < 0.3f) jetDown = false;
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
                : againstGround ? _system.GroundVelocityAt(body, Suit.position) : _system.VelocityOfSeenFrom(body, Suit.position);
            Vector3 relative = Suit.linearVelocity - reference;
            _matchReference = reference;

            Vector3 jets = Vector3.zero;
            float factor = SuitTanks.PushFactor;
            // A jump goes on the first step with footing inside the grace (a press just before landing, or just after
            // stepping off an edge). Upright on foot only; floating free, Jump is the jets as always.
            bool upright = Posture == Posture.Upright;
            bool jumpNow = _jumpGrace.Take(Time.time, _grounded && upright) && upright && live;
            float jumpSpeed = Mathf.Sqrt(2f * 9.81f * _config.outsideJumpHeight);
            if (_grounded)
            {
                _lastGroundVelocity = groundVelocity;
                // On your feet: walking, and the push off for a jump. No jets, no fuel.
                float pace = StanceState.SpeedOf(Stances.Current, Sneaking, SuitDials.stances) * (Melee != null ? Melee.WalkSlow : 1f) * Mathf.Clamp(CarryPace, 0.2f, 1f);
                Vector3 wish = (frame * Vector3.right * move.x + frame * Vector3.forward * move.y) * (_config.outsideWalkSpeed * pace * Combat.RunEffects.Mods.Move);
                Vector3 onGround = Suit.linearVelocity - groundVelocity;
                Vector3 vertical = Vector3.Project(onGround, _up);
                Vector3 level = Vector3.MoveTowards(onGround - vertical, wish, 40f * dt);
                if (jumpNow)
                {
                    // The same push off whatever the gravity, so low gravity means high, slow jumps.
                    vertical = _up * jumpSpeed;
                    _grounded = false;
                }
                Suit.linearVelocity = groundVelocity + level + vertical;
                _walkingAt = level.magnitude;
            }
            else
            {
                if (jumpNow)
                {
                    // Just off an edge (coyote time): the same push off, measured against the ground just left.
                    Vector3 rel = Suit.linearVelocity - _lastGroundVelocity;
                    Suit.linearVelocity += _up * jumpSpeed - Vector3.Project(rel, _up);
                }
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
            if (Current == this) Current = null;
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
