using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Look;
using OrbitalAnte.Player;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.InputSystem;
using LanderShip = OrbitalAnte.Ship.Ship;

namespace OrbitalAnte.Rover
{
    /// <summary>
    /// The moon rover (Matthew, 16 Nov 2026: "a moon rover vehicle that can run enemies over and splat them and/or
    /// their limbs ... really fast and ... aesthetically pleasing"). The lander carries it on a davit behind its
    /// base, as the Apollo lander carried its rover folded on its side: land, walk round the back, look at it and
    /// press Interact to winch it down; drive it back under the davit and hold Interact to winch it up again.
    ///
    /// Driving is a raycast-wheel vehicle on the game's own gravity (<see cref="GravityBody"/>, so it works on any
    /// world and its rotating ground). Low gravity would leave a fast car skating and floating, so while its wheels
    /// are down it is held to the ground by at least the grip gravity (<see cref="RoverRules.gripGravity"/>) and a
    /// little more the faster it goes; in the air it keeps the world's own long, floaty fall (a little shortened)
    /// and the stick tips it, so jumps are part of the fun. The rules are plain C# in RoverRules.cs.
    ///
    /// People are run over, not driven into: the rover's colliders are on <see cref="Layers.Vehicle"/>, which does
    /// not touch characters' bodies, and every physics step it looks for hitboxes in its path and hurts them by the
    /// closing speed (<see cref="RunOver"/>): limbs it touches come off, and fast enough the whole person goes.
    /// The pieces leave with the rover's speed, through the same gib and gore systems as a gunshot.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class MoonRover : MonoBehaviour
    {
        public enum Where
        {
            /// <summary>Hanging on the lander's davit.</summary>
            Stowed,
            /// <summary>Being winched down to the ground.</summary>
            Lowering,
            /// <summary>On its wheels, under physics.</summary>
            Rolling,
            /// <summary>Parked with nobody in it: fixed to the ground it stands on, costing nothing, until someone gets in.</summary>
            Parked,
            /// <summary>Being winched back up.</summary>
            Lifting,
        }

        public Where Now { get; private set; } = Where.Stowed;
        public Rigidbody Body { get; private set; }
        public GravityBody Gravity { get; private set; }
        public RoverRules Rules { get; private set; }
        public Astronaut Driver { get; private set; }
        public BoostTank Boost { get; } = new BoostTank();
        /// <summary>Speed over the ground, metres per second.</summary>
        public float Speed { get; private set; }
        /// <summary>Speed along the nose over the ground (negative reversing).</summary>
        public float ForwardSpeed { get; private set; }
        /// <summary>How many wheels touched the ground last physics step.</summary>
        public int WheelsDown { get; private set; }
        /// <summary>Upside down or on its side and nearly still: the driver can put it back on its wheels.</summary>
        public bool Overturned { get; private set; }
        /// <summary>The motors' throttle, -1 to 1, for sound and the screen.</summary>
        public float Throttle { get; private set; }
        /// <summary>People hit this run (for the debug line).</summary>
        public int Splats { get; private set; }

        LanderShip _ship;
        Transform _world;
        SolarSystem _system;
        RoverConfig _config;

        readonly Transform[] _corner = new Transform[RoverLayout.Wheels];
        readonly Transform[] _spinner = new Transform[RoverLayout.Wheels];
        readonly Transform[] _arm = new Transform[RoverLayout.Wheels];
        readonly float[] _offset = new float[RoverLayout.Wheels];
        readonly float[] _spin = new float[RoverLayout.Wheels];
        readonly bool[] _down = new bool[RoverLayout.Wheels];
        float _steer, _rearSteer, _still, _upturned, _liftT;
        float _inForward, _inBrake, _inSteer;
        bool _inBoost, _inHandbrake, _inFlip;
        int _parkedOn;
        float _sinceGround;
        /// <summary>How far below the hook the ground was found when lowering started, metres.</summary>
        float _lowerTo;
        const float HookAbove = 0.3f, LowerAtMost = 6f;
        Vector3 _liftFrom;
        Quaternion _liftFromTurn;
        float _baseFov = -1f;

        Collider[] _solid;
        GameObject _driverBox;
        HealthLink _driverLink;
        GameObject _boostGlow;
        Transform[] _cables;
        AudioSource _motor;
        RoverHud _hud;

        static readonly Quaternion StowTurn = Quaternion.Euler(0f, 180f, 0f);

        // ------------------------------------------------------------------ building

        /// <summary>Makes the rover and hangs it on the lander's davit. Null if the config says the lander carries none.</summary>
        public static MoonRover Create(Transform world, LanderShip ship, SolarSystem system)
        {
            RoverConfig config = RoverConfig.Load();
            if (!config.carried || ship == null) return null;

            var go = new GameObject("MoonRover");
            go.layer = Layers.Vehicle;
            var rover = go.AddComponent<MoonRover>();
            rover._ship = ship;
            rover._world = world;
            rover._system = system;
            rover._config = config;
            rover.Rules = config.rules;

            rover.Body = go.AddComponent<Rigidbody>();
            rover.Body.mass = rover.Rules.mass;
            rover.Body.useGravity = false;
            rover.Body.linearDamping = 0f;
            rover.Body.angularDamping = 0.05f;
            rover.Body.maxAngularVelocity = 9f;
            rover.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rover.Gravity = go.AddComponent<GravityBody>();
            // It is ridden and left parked, so the ground under it is kept solid while it is out on its wheels.
            rover.Gravity.NeedsGround = true;
            rover.Gravity.SunkBelow = -0.6f;
            rover.Gravity.PutBackAt = 0.6f;

            rover.BuildLook();
            rover.BuildSolid();
            rover.BuildDriverBox();
            rover.BuildCables();
            rover.BuildSound();
            rover._hud = RoverHud.Build(rover);
            rover.Stow(instant: true);
            return rover;
        }

        void BuildLook()
        {
            var pieces = new Dictionary<string, GameObject>();
            var look = new GameObject("Look");
            look.layer = Layers.Vehicle;
            look.transform.SetParent(transform, false);
            bool modelled = ModelLibrary.Build(RoverLayout.Model, look.transform, pieces);
            if (modelled)
            {
                // The library hands back one copy of the wheel and the arm; each corner gets its own.
                if (pieces.TryGetValue("wheel", out var w)) Destroy(w);
                if (pieces.TryGetValue("arm", out var a)) Destroy(a);
                if (pieces.TryGetValue("boostGlow", out var glow)) { _boostGlow = glow; glow.SetActive(false); }
            }
            else
            {
                StandIn(look.transform);
            }
            Mesh wheel = modelled ? ModelLibrary.PartMesh(RoverLayout.Model, "wheel") : null;
            Mesh arm = modelled ? ModelLibrary.PartMesh(RoverLayout.Model, "arm") : null;
            Material paint = ModelLibrary.SharedMaterial();
            for (int i = 0; i < RoverLayout.Wheels; i++)
            {
                var corner = new GameObject("Wheel" + RoverLayout.WheelNames[i]).transform;
                corner.gameObject.layer = Layers.Vehicle;
                corner.SetParent(transform, false);
                corner.localPosition = V(RoverLayout.Hub(i));
                _corner[i] = corner;
                // The wheel is built with its outside face to the right: the left ones are turned half round.
                var side = new GameObject("Side").transform;
                side.SetParent(corner, false);
                side.localRotation = RoverLayout.Side(i) < 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                var spin = new GameObject("Spin").transform;
                spin.SetParent(side, false);
                _spinner[i] = spin;
                if (wheel != null) Show(spin.gameObject, wheel, paint);
                else
                {
                    var stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(stand.GetComponent<Collider>());
                    stand.transform.SetParent(spin, false);
                    stand.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    stand.transform.localScale = new Vector3(RoverLayout.WheelRadius * 2f, RoverLayout.WheelWidth * 0.5f, RoverLayout.WheelRadius * 2f);
                    stand.GetComponent<Renderer>().sharedMaterial = Mat.Lit(new Color(0.2f, 0.2f, 0.21f));
                }

                var pivot = new GameObject("Arm" + RoverLayout.WheelNames[i]).transform;
                pivot.SetParent(transform, false);
                pivot.localPosition = V(RoverLayout.ArmPivot(i));
                var swing = new GameObject("Swing").transform;
                swing.SetParent(pivot, false);
                swing.localRotation = side.localRotation;
                _arm[i] = swing;
                if (arm != null) Show(swing.gameObject, arm, paint);
            }
            Layers.SetLayerRecursively(look, Layers.Vehicle);
        }

        static void Show(GameObject go, Mesh mesh, Material material)
        {
            go.layer = Layers.Vehicle;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>Plain shapes for when the model file is missing, so the rover still works.</summary>
        static void StandIn(Transform parent)
        {
            Box(parent, RoverLayout.HullCentre, new P3(1.2f, 0.3f, 3.4f), new Color(0.85f, 0.85f, 0.82f));
            Box(parent, new P3(0f, 1.0f, -0.4f), new P3(1.0f, 0.5f, 0.5f), new Color(0.32f, 0.36f, 0.42f));
            Box(parent, new P3(0f, 1.9f, -0.8f), new P3(1.1f, 0.06f, 0.06f), new Color(0.7f, 0.7f, 0.72f));
        }

        static void Box(Transform parent, P3 centre, P3 size, Color colour)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = V(centre);
            box.transform.localScale = V(size);
            box.GetComponent<Renderer>().sharedMaterial = Mat.Lit(colour);
        }

        void BuildSolid()
        {
            var grip = new PhysicsMaterial("Rover") { dynamicFriction = 0.35f, staticFriction = 0.4f, bounciness = 0.1f, frictionCombine = PhysicsMaterialCombine.Average };
            var hull = gameObject.AddComponent<BoxCollider>();
            hull.center = V(RoverLayout.HullCentre);
            hull.size = V(RoverLayout.HullSize);
            hull.sharedMaterial = grip;
            var cage = gameObject.AddComponent<BoxCollider>();
            cage.center = V(RoverLayout.CageCentre);
            cage.size = V(RoverLayout.CageSize);
            cage.sharedMaterial = grip;
            _solid = new Collider[] { hull, cage };
            // Low down: it slides before it rolls over.
            Body.centerOfMass = V(RoverLayout.CentreOfMass);
        }

        void BuildDriverBox()
        {
            // Enemies shoot at the driver; the rover's open frame does not stop their shots. The driver's own body is
            // not here, so a stand-in capsule takes the hits for them.
            _driverBox = new GameObject("DriverHitbox");
            // On the Seat layer, which collides with nothing, and with no rigidbody of its own: it can never push the
            // ship, the rover or anyone, and only enemy bullets see it.
            _driverBox.layer = Layers.Seat;
            _driverBox.transform.SetParent(transform, false);
            var capsule = _driverBox.AddComponent<CapsuleCollider>();
            Vector3 low = V(RoverLayout.DriverLow), high = V(RoverLayout.DriverHigh);
            capsule.center = (low + high) * 0.5f;
            capsule.radius = RoverLayout.DriverRadius;
            capsule.height = Vector3.Distance(low, high) + RoverLayout.DriverRadius * 2f;
            capsule.direction = 1;
            _driverLink = _driverBox.AddComponent<HealthLink>();
            _driverBox.SetActive(false);
        }

        void BuildCables()
        {
            _cables = new Transform[2];
            Material wire = Mat.Unlit(new Color(0.16f, 0.16f, 0.17f));
            for (int i = 0; i < 2; i++)
            {
                var cable = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cable.GetComponent<Collider>());
                cable.name = "DavitCable";
                cable.transform.SetParent(_ship.transform, false);
                cable.GetComponent<Renderer>().sharedMaterial = wire;
                cable.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _cables[i] = cable.transform;
            }
            // The davit itself belongs to the lander.
            var davit = new GameObject("RoverDavit");
            davit.transform.SetParent(_ship.transform, false);
            ModelLibrary.Build(RoverLayout.DavitModel, davit.transform);
        }

        void BuildSound()
        {
            _motor = gameObject.AddComponent<AudioSource>();
            _motor.clip = MotorClip();
            _motor.loop = true;
            _motor.playOnAwake = false;
            _motor.spatialBlend = 0f;
            _motor.volume = 0f;
        }

        /// <summary>
        /// Electric hub motors heard through the seat and the suit (there is no air to carry it on the Moon): a low
        /// hum, a reduction-gear whine above it and a little grit. Two seconds that loop cleanly; pitch follows speed.
        /// A stand-in until the audio pipeline makes the real one.
        /// </summary>
        static AudioClip MotorClip()
        {
            const int rate = 22050, seconds = 2;
            var data = new float[rate * seconds];
            var noise = new System.Random(7);
            float smooth = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.45f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 165f * t) * 0.1f;
                float whine = Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.12f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.05f;
                smooth += ((float)noise.NextDouble() * 2f - 1f - smooth) * 0.08f;
                data[i] = (hum + whine + smooth * 0.25f) * 0.6f;
            }
            var clip = AudioClip.Create("RoverMotor", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static Vector3 V(P3 p) => new Vector3(p.X, p.Y, p.Z);

        // ------------------------------------------------------------------ where it is

        /// <summary>Hangs it on the davit.</summary>
        void Stow(bool instant)
        {
            SetLoose(false);
            transform.SetParent(_ship.transform, true);
            if (instant)
            {
                transform.localPosition = V(RoverLayout.StowInShip);
                transform.localRotation = StowTurn;
            }
            for (int i = 0; i < RoverLayout.Wheels; i++) _offset[i] = -Rules.travelDown;
            Now = Where.Stowed;
            Boost.Refill();
            PoseWheels(0f);
        }

        /// <summary>Under physics (true) or carried by whatever it is fixed to (false).</summary>
        void SetLoose(bool loose)
        {
            Body.isKinematic = !loose;
            Body.interpolation = loose ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            Gravity.enabled = loose;
            // Hanging off the lander it must never push the lander about.
            bool solid = loose || Now == Where.Parked;
            for (int i = 0; i < _solid.Length; i++) _solid[i].enabled = solid;
        }

        bool ShipLanded => _ship != null && _ship.Flight != null && _ship.Flight.Landed;

        /// <summary>Starts winching it down to the ground. Only from the davit, with the lander standing on the ground.</summary>
        public bool Unload()
        {
            if (Now != Where.Stowed || !ShipLanded) return false;
            // Look for the ground from the davit's tip, not from the rover: on a slope the ground behind the lander
            // can stand higher than the rover's middle, and ground is only solid from above. No ground within reach
            // below the hook (or ground above it): it stays on the davit.
            Vector3 shipUp = _ship.transform.up;
            Vector3 hook = _ship.transform.TransformPoint(V(RoverLayout.StowInShip)) + shipUp * HookAbove;
            if (!Physics.Raycast(hook, -shipUp, out RaycastHit ground, HookAbove + LowerAtMost, Layers.WorldMask, QueryTriggerInteraction.Ignore)
                || ground.collider.attachedRigidbody == _ship.Body) return false;
            _lowerTo = ground.distance - HookAbove;
            Now = Where.Lowering;
            _liftT = 0f;
            return true;
        }

        /// <summary>Whether it is parked close enough under the davit, slow enough and empty, to be winched up.</summary>
        public bool CanStow
        {
            get
            {
                if ((Now != Where.Rolling && Now != Where.Parked) || Driver != null || !ShipLanded || Speed > 1f) return false;
                Vector3 off = _ship.transform.InverseTransformPoint(transform.position) - V(RoverLayout.StowInShip);
                off.y = 0f;
                return off.magnitude <= Rules.stowReach;
            }
        }

        /// <summary>Starts winching it back up onto the davit.</summary>
        public bool StartStow()
        {
            if (!CanStow) return false;
            SetLoose(false);
            Now = Where.Lifting;
            for (int i = 0; i < _solid.Length; i++) _solid[i].enabled = false;
            transform.SetParent(_ship.transform, true);
            _liftFrom = transform.localPosition;
            _liftFromTurn = transform.localRotation;
            _liftT = 0f;
            return true;
        }

        void Release()
        {
            // Off the hook: it carries on with the lander's own motion and falls the last few centimetres.
            Vector3 velocity = _ship.Body.GetPointVelocity(transform.position);
            transform.SetParent(_world, true);
            Gravity.Neighbourhood = _ship.Gravity.Neighbourhood;
            Now = Where.Rolling;
            SetLoose(true);
            Body.position = transform.position;
            Body.rotation = transform.rotation;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Vector3.zero;
            Steady();
        }

        bool _steadied;

        /// <summary>
        /// Heavier to roll and pitch than its boxes alone would make it (the batteries and wheels are low and wide),
        /// so a hard turn slides it before it tips. Done once its colliders are live, so Unity has worked them out.
        /// </summary>
        void Steady()
        {
            if (_steadied) return;
            _steadied = true;
            Body.ResetInertiaTensor();
            Body.centerOfMass = V(RoverLayout.CentreOfMass);
            Vector3 inertia = Body.inertiaTensor;
            if (inertia.x > 1f && inertia.z > 1f) Body.inertiaTensor = new Vector3(inertia.x * 1.4f, inertia.y, inertia.z * 1.8f);
        }

        /// <summary>Fixes it to the ground it stands on (it rides the world's spin with it) until someone gets in.</summary>
        void Park()
        {
            // Never while the view's frame follows it (the driver died in the seat): parking switches off its pull,
            // and the frame would lose what it follows.
            if (_system != null && _system.Focus == Gravity) return;
            int body = Gravity.Neighbourhood;
            var surface = OrbitalAnte.Planets.PlanetWorld.Instance != null ? OrbitalAnte.Planets.PlanetWorld.Instance.SurfaceOf(body) : null;
            if (surface == null || surface.Body == null) return;
            _parkedOn = body;
            Now = Where.Parked;
            SetLoose(false);
            transform.SetParent(surface.Body.transform, true);
        }

        void Wake()
        {
            if (Now != Where.Parked) return;
            Vector3 ground = _system != null ? _system.GroundVelocityAt(_parkedOn, transform.position) : Vector3.zero;
            transform.SetParent(_world, true);
            Gravity.Neighbourhood = _parkedOn;
            Now = Where.Rolling;
            SetLoose(true);
            Body.position = transform.position;
            Body.rotation = transform.rotation;
            Body.linearVelocity = ground;
            Body.angularVelocity = Vector3.zero;
            _still = 0f;
        }

        // ------------------------------------------------------------------ the driver

        /// <summary>What a player on foot can do with it from where they are looking.</summary>
        public enum Offer { None, Drive, Unload, LandFirst }

        public Offer OfferTo(Vector3 eye, Vector3 forward)
        {
            if (Driver != null) return Offer.None;
            Vector3 middle = transform.TransformPoint(0f, 1f, 0f);
            Vector3 to = middle - eye;
            float reach = Rules.reach + (Now == Where.Stowed ? 2.2f : 0f);   // it hangs a couple of metres up
            if (to.magnitude > reach || Vector3.Angle(forward, to) > 40f) return Offer.None;
            switch (Now)
            {
                case Where.Stowed: return ShipLanded ? Offer.Unload : Offer.LandFirst;
                case Where.Rolling:
                case Where.Parked: return Offer.Drive;
                default: return Offer.None;
            }
        }

        public void Board(Astronaut driver)
        {
            if (Driver != null || (Now != Where.Rolling && Now != Where.Parked)) return;
            Wake();
            Driver = driver;
            _driverLink.Target = driver.Armour;
            _driverBox.SetActive(true);
            _baseFov = driver.Eye != null ? driver.Eye.fieldOfView : -1f;
            _upturned = 0f;
            if (_motor != null) _motor.Play();
            _hud?.SetShown(true);
        }

        /// <summary>The driver has got out (or died). It coasts to a stop on its brakes.</summary>
        public void Leave()
        {
            if (Driver != null && Driver.Eye != null && _baseFov > 0f) Driver.Eye.fieldOfView = _baseFov;
            Driver = null;
            _driverLink.Target = null;
            _driverBox.SetActive(false);
            _inForward = _inBrake = _inSteer = 0f;
            _inBoost = _inHandbrake = _inFlip = false;
            if (_motor != null) _motor.Stop();
            _hud?.SetShown(false);
        }

        /// <summary>Where the driver's boots go when they climb out, and how fast they are moving then.</summary>
        public Vector3 ExitFeet => transform.TransformPoint(V(RoverLayout.Exit));
        public Vector3 VelocityAt(Vector3 point) => Body.isKinematic ? Vector3.zero : Body.GetPointVelocity(point);

        /// <summary>A knock big enough to hurt reached the driver (see <see cref="RoverHandling.Hurt"/>).</summary>
        void OnCollisionEnter(Collision collision)
        {
            if (Driver == null || Now != Where.Rolling) return;
            int layer = collision.collider.gameObject.layer;
            if (layer == Layers.Gib || layer == Layers.Item) return;
            float knock = collision.impulse.magnitude / Mathf.Max(1f, Body.mass);
            float hurt = RoverHandling.Hurt(Rules, knock, out bool kills);
            if (hurt <= 0f && !kills) return;
            Vector3 from = collision.impulse.sqrMagnitude > 1e-6f ? -collision.impulse.normalized : -transform.forward;
            Driver.RoverKnock(knock, hurt, kills, from, Speed);
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (Driver == null || GameSession.Paused || Driver.Dead || GameInput.Asset == null) return;
            Vector2 move = GameInput.Move.ReadValue<Vector2>();
            float trigger = 0f;
            // A controller's right trigger is the accelerator (it is Fire, so the mouse button is not).
            var fireFrom = GameInput.Fire.activeControl;
            if (fireFrom != null && fireFrom.device is Gamepad) trigger = GameInput.Fire.ReadValue<float>();
            _inForward = Mathf.Clamp01(Mathf.Max(move.y, trigger));
            _inBrake = Mathf.Clamp01(Mathf.Max(-move.y, GameInput.SuitMatch.ReadValue<float>()));
            _inSteer = Mathf.Clamp(move.x, -1f, 1f);
            _inBoost = GameInput.Jump.IsPressed() || GameInput.Sprint.IsPressed();
            _inHandbrake = GameInput.JetDown.IsPressed();
            if (GameInput.Reload.WasPressedThisFrame()) _inFlip = true;
        }

        void LateUpdate()
        {
            if (Now == Where.Stowed || Now == Where.Lowering || Now == Where.Lifting) PoseCables(true);
            else PoseCables(false);
            PoseWheels(Time.deltaTime);

            if (_boostGlow != null && _boostGlow.activeSelf != Boost.Lit) _boostGlow.SetActive(Boost.Lit);
            if (Driver == null) return;
            if (Driver.Eye != null && _baseFov > 0f) Driver.Eye.fieldOfView = _baseFov + (FeelSettings.CameraShake ? RoverHandling.Fov(Rules, Speed) : 0f);
            if (_motor != null)
            {
                float load = Mathf.Max(Mathf.Abs(Throttle), Boost.Lit ? 1f : 0f);
                _motor.pitch = 0.55f + 1.25f * Mathf.Clamp01(Speed / Rules.boostSpeed) + 0.15f * load;
                _motor.volume = _config.motorVolume * (GameSession.Paused ? 0f : 0.35f + 0.65f * Mathf.Clamp01(load * 0.7f + Speed / Rules.topSpeed * 0.5f));
            }
        }

        void PoseCables(bool shown)
        {
            if (_cables == null) return;
            for (int i = 0; i < _cables.Length; i++)
            {
                Transform c = _cables[i];
                if (c.gameObject.activeSelf != shown) c.gameObject.SetActive(shown);
                if (!shown) continue;
                float s = i == 0 ? -1f : 1f;
                // From the winch at the davit's tip down to the top corners of the roll cage.
                Vector3 from = _ship.transform.TransformPoint(new Vector3(s * 0.62f, -1.13f, -3.40f));
                Vector3 to = transform.TransformPoint(new Vector3(-s * 0.40f, 1.94f, -0.82f));
                Vector3 mid = (from + to) * 0.5f;
                Vector3 along = to - from;
                c.position = mid;
                c.rotation = along.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(along.normalized, _ship.transform.forward) : Quaternion.identity;
                // In the lander's units, which are metres (it is never scaled).
                c.localScale = new Vector3(0.02f, 0.02f, along.magnitude);
            }
        }

        void PoseWheels(float dt)
        {
            for (int i = 0; i < RoverLayout.Wheels; i++)
            {
                if (_corner[i] == null) continue;
                bool front = RoverLayout.Front(i);
                float steer = front ? _steer : _rearSteer;
                _corner[i].localPosition = V(RoverLayout.Hub(i)) + Vector3.up * _offset[i];
                _corner[i].localRotation = Quaternion.Euler(0f, steer, 0f);
                // Rolling forward turns a right wheel one way and a left wheel (turned half round) the other.
                float angle = _spin[i] * (RoverLayout.Side(i) < 0 ? -1f : 1f);
                _spinner[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
                if (_arm[i] != null)
                {
                    float swing = Mathf.Asin(Mathf.Clamp(_offset[i] / RoverLayout.ArmLength, -0.9f, 0.9f)) * Mathf.Rad2Deg;
                    _arm[i].localRotation = Quaternion.Euler(0f, RoverLayout.Side(i) < 0 ? 180f : 0f, swing);
                }
            }
        }

        // ------------------------------------------------------------------ every physics step

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            switch (Now)
            {
                case Where.Lowering: StepLowering(dt); break;
                case Where.Lifting: StepLifting(dt); break;
                case Where.Rolling: StepRolling(dt); break;
            }
        }

        void StepLowering(float dt)
        {
            _liftT += dt;
            transform.localPosition += Vector3.down * (Rules.lowerSpeed * dt);
            for (int i = 0; i < RoverLayout.Wheels; i++) _offset[i] = -Rules.travelDown;
            // If the lander lifts off meanwhile, the winch snatches it back up.
            if (!ShipLanded) { Stow(instant: true); return; }
            // Down until the hanging wheels are about to touch the ground found under the hook (they hang
            // travelDown below the rover's origin), then let go for the last few centimetres.
            float lowered = V(RoverLayout.StowInShip).y - transform.localPosition.y;
            if (lowered >= _lowerTo - Rules.travelDown - 0.05f || lowered > LowerAtMost) Release();
        }

        void StepLifting(float dt)
        {
            _liftT = Mathf.Min(1f, _liftT + dt / Mathf.Max(0.3f, Rules.liftSeconds));
            float t = Mathf.SmoothStep(0f, 1f, _liftT);
            Vector3 target = V(RoverLayout.StowInShip);
            transform.localPosition = Vector3.Lerp(_liftFrom, target, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.6f);
            transform.localRotation = Quaternion.Slerp(_liftFromTurn, StowTurn, t);
            for (int i = 0; i < RoverLayout.Wheels; i++) _offset[i] = Mathf.Lerp(_offset[i], -Rules.travelDown, t);
            if (_liftT >= 1f) Stow(instant: true);
        }

        readonly RaycastHit[] _wheelHit = new RaycastHit[RoverLayout.Wheels];
        readonly float[] _load = new float[RoverLayout.Wheels];

        void StepRolling(float dt)
        {
            Vector3 felt = Gravity.FeltGravity;
            float pull = felt.magnitude;
            Vector3 worldUp = pull > 0.05f ? -felt / pull : transform.up;
            int body = Gravity.Neighbourhood;
            Vector3 groundVelocity = _system != null && body > 0 ? _system.GroundVelocityAt(body, Body.worldCenterOfMass) : Vector3.zero;
            Vector3 relative = Body.linearVelocity - groundVelocity;
            // The physics pose, not the drawn one (the body is interpolated, so the transform can be a step behind).
            Quaternion turn = Body.rotation;
            Vector3 where = Body.position;
            Vector3 up = turn * Vector3.up, nose = turn * Vector3.forward;
            ForwardSpeed = Vector3.Dot(relative, nose);
            Speed = relative.magnitude;

            // ---- what the driver asks for
            bool driven = Driver != null && !Driver.Dead;
            float forward = driven ? _inForward : 0f, brake = driven ? _inBrake : 0f, steerIn = driven ? _inSteer : 0f;
            bool braking, reversing = false;
            float throttle;
            if (forward > 0.05f && ForwardSpeed > -0.8f) { throttle = forward; braking = false; }
            else if (brake > 0.05f && ForwardSpeed < 0.8f) { throttle = -brake; braking = false; reversing = true; }
            else if (brake > 0.05f || (forward > 0.05f && ForwardSpeed <= -0.8f)) { throttle = 0f; braking = true; }
            else { throttle = 0f; braking = !driven; }   // nobody in it: the brakes hold it
            Throttle = throttle;
            bool boosting = Boost.Step(dt, driven && _inBoost && !reversing, Rules);
            bool handbrake = driven && _inHandbrake;

            float steerTarget = steerIn * RoverHandling.SteerAngle(Rules, ForwardSpeed);
            _steer = Mathf.MoveTowards(_steer, steerTarget, Rules.steerRate * dt);
            _rearSteer = -_steer * RoverHandling.RearSteer(Rules, ForwardSpeed);
            float motor = RoverHandling.Drive(Rules, throttle, ForwardSpeed, boosting);

            // ---- the wheels: springs, then the tyres
            float cornerMass = Body.mass / RoverLayout.Wheels;
            float reachDown = Rules.travelUp + Rules.travelDown + RoverLayout.WheelRadius;
            int down = 0;
            for (int i = 0; i < RoverLayout.Wheels; i++)
            {
                P3 hub = RoverLayout.Hub(i);
                Vector3 mount = where + turn * new Vector3(hub.X, hub.Y + Rules.travelUp, hub.Z);
                float offset = -Rules.travelDown;
                bool touching = Physics.Raycast(mount, -up, out RaycastHit hit, reachDown, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                if (touching) offset = Mathf.Clamp(Rules.travelUp - (hit.distance - RoverLayout.WheelRadius), -Rules.travelDown, Rules.travelUp);
                float rising = (offset - _offset[i]) / dt;
                _offset[i] = touching ? offset : Mathf.MoveTowards(_offset[i], -Rules.travelDown, 2f * dt);
                _down[i] = touching;
                _wheelHit[i] = hit;
                _load[i] = 0f;
                if (!touching) continue;
                down++;
                Vector3 hubWorld = mount - up * (Rules.travelUp - _offset[i]);
                float push = RoverHandling.Suspension(Rules, cornerMass, offset, rising);
                if (offset >= Rules.travelUp - 0.001f)
                {
                    // Bottomed out on the bump stop: whatever is still coming down stops here.
                    float into = -Vector3.Dot(Body.GetPointVelocity(hubWorld) - groundVelocity, up);
                    if (into > 0f) push += cornerMass * into / dt * 0.5f;
                }
                Body.AddForceAtPosition(up * push, hubWorld);
                _load[i] = push;
            }
            WheelsDown = down;

            // Anti-roll bars, front and back.
            for (int axle = 0; axle < 2; axle++)
            {
                int l = axle * 2, r = axle * 2 + 1;
                if (!_down[l] || !_down[r]) continue;
                float bar = Rules.antiRoll * RoverHandling.Stiffness(Rules, cornerMass) * (_offset[l] - _offset[r]) * 0.1f;
                Body.AddForceAtPosition(up * bar, where + turn * V(RoverLayout.Hub(l)));
                Body.AddForceAtPosition(-up * bar, where + turn * V(RoverLayout.Hub(r)));
                _load[l] = Mathf.Max(0f, _load[l] + bar);
                _load[r] = Mathf.Max(0f, _load[r] - bar);
            }

            for (int i = 0; i < RoverLayout.Wheels; i++)
            {
                if (!_down[i]) continue;
                RaycastHit hit = _wheelHit[i];
                bool front = RoverLayout.Front(i);
                Vector3 normal = hit.normal;
                Vector3 heading = Quaternion.AngleAxis(front ? _steer : _rearSteer, up) * nose;
                Vector3 along = Vector3.ProjectOnPlane(heading, normal);
                if (along.sqrMagnitude < 1e-6f) continue;
                along.Normalize();
                Vector3 side = Vector3.Cross(normal, along);
                Vector3 v = Body.GetPointVelocity(hit.point) - groundVelocity;
                float vAlong = Vector3.Dot(v, along), vSide = Vector3.Dot(v, side);

                float grip = Rules.grip * (front ? 1f : RoverHandling.RearGrip(Rules, ForwardSpeed, handbrake));
                float most = grip * _load[i];
                float slip = Mathf.Atan2(Mathf.Abs(vSide), Mathf.Abs(vAlong) + 1f) * Mathf.Rad2Deg;
                float sideMost = most * RoverHandling.SideGrip(Rules, slip);
                float sideForce = Mathf.Clamp(-vSide * cornerMass / dt, -sideMost, sideMost);

                float alongForce;
                if (braking || (handbrake && !front))
                {
                    float stop = Mathf.Abs(vAlong) * cornerMass / dt;
                    alongForce = -Mathf.Sign(vAlong) * Mathf.Min(Rules.brakeAccel * cornerMass, stop);
                }
                else if (Mathf.Abs(motor) > 0.001f)
                {
                    alongForce = motor * cornerMass;
                }
                else
                {
                    alongForce = -Mathf.Sign(vAlong) * Mathf.Min(Rules.rollingDrag * cornerMass, Mathf.Abs(vAlong) * cornerMass / dt);
                }
                // The tyre can only give so much in all: past it, it spins or skids.
                float total = Mathf.Sqrt(alongForce * alongForce + sideForce * sideForce);
                if (total > most && total > 0f)
                {
                    float k = most / total;
                    alongForce *= k;
                    sideForce *= k;
                }
                // Pushed between the hub and the ground, not at the ground: less leverage to roll it over.
                Vector3 at = hit.point + normal * (RoverLayout.WheelRadius * 0.6f);
                Body.AddForceAtPosition(along * alongForce + side * sideForce, at);
                _spin[i] = Mathf.Repeat(_spin[i] + vAlong / RoverLayout.WheelRadius * Mathf.Rad2Deg * dt, 360f);
            }
            for (int i = 0; i < RoverLayout.Wheels; i++)
            {
                if (_down[i]) continue;
                // Wheels in the air spin on with the motors, or wind down.
                _spin[i] = Mathf.Repeat(_spin[i] + (throttle * 25f + ForwardSpeed) / RoverLayout.WheelRadius * Mathf.Rad2Deg * dt * 0.5f, 360f);
            }

            // ---- held to the ground, or flying
            // Only the right way up: tipped on its side against a cliff, or driven up a cave wall, nothing holds it there.
            float upright = Vector3.Dot(up, worldUp);
            float holdShare = pull > 0.05f ? Mathf.Clamp01((upright - 0.5f) / 0.3f) : 1f;
            _sinceGround = down >= 2 ? 0f : _sinceGround + dt;
            if (down >= 2 || _sinceGround < Rules.holdLinger)
            {
                Body.AddForce(-up * (RoverHandling.Hold(Rules, pull, Speed) * holdShare), ForceMode.Acceleration);
            }
            if (down < 2)
            {
                // Near a world it comes back down sooner than the world alone would bring it.
                if (pull > 0.3f)
                {
                    bool falling = Vector3.Dot(relative, worldUp) < 0f;
                    float wanted = Mathf.Max(falling ? pull * Rules.fallScale : pull, Rules.airGravity);
                    if (wanted > pull) Body.AddForce(-worldUp * (wanted - pull), ForceMode.Acceleration);
                }
                // The stick tips it, and it turns its wheels back toward the ground by itself.
                Vector3 local = new Vector3((forward - brake) * Rules.airTurn, steerIn * Rules.airTurn * 0.6f, -steerIn * Rules.airTurn * 0.35f);
                Body.AddRelativeTorque(local, ForceMode.Acceleration);
                Body.AddTorque(Vector3.Cross(up, worldUp) * Rules.airLevel, ForceMode.Acceleration);
                Vector3 spinning = Body.angularVelocity;
                Body.AddTorque(-spinning * (Rules.airSpin * 0.2f), ForceMode.Acceleration);
            }
            if (boosting)
            {
                // A rocket along the ground, never a rocket into the sky: off the wheels only the push along the
                // ground counts, so the nose pointed up does not launch it.
                Vector3 push = nose * RoverHandling.BoostPush(Rules, ForwardSpeed);
                if (down < 2 && pull > 0.05f) push = Vector3.ProjectOnPlane(push, worldUp);
                Body.AddForce(push, ForceMode.Acceleration);
            }

            // ---- on its back
            bool upsideDown = Vector3.Dot(up, worldUp) < 0.35f && Speed < 3f;
            _upturned = upsideDown ? _upturned + dt : 0f;
            Overturned = _upturned > 1f;
            if (_inFlip)
            {
                _inFlip = false;
                if (Overturned) Flip(worldUp, groundVelocity);
            }

            RunOverPeople(dt, groundVelocity, up, where, turn);

            // ---- parked with nobody in it: let it rest
            if (Driver == null && down >= 3 && Speed < 0.3f)
            {
                _still += dt;
                if (_still > 2f) Park();
            }
            else _still = 0f;
        }

        void Flip(Vector3 worldUp, Vector3 groundVelocity)
        {
            Vector3 heading = Vector3.ProjectOnPlane(transform.forward, worldUp);
            if (heading.sqrMagnitude < 1e-4f) heading = Vector3.ProjectOnPlane(transform.up, worldUp);
            Body.position += worldUp * 1.4f;
            Body.rotation = Quaternion.LookRotation(heading.normalized, worldUp);
            Body.linearVelocity = groundVelocity;
            Body.angularVelocity = Vector3.zero;
            _upturned = 0f;
            Overturned = false;
        }

        // ------------------------------------------------------------------ people in the way

        readonly Collider[] _touched = new Collider[64];
        readonly Dictionary<Mannequin, List<BodyPart>> _people = new Dictionary<Mannequin, List<BodyPart>>();
        readonly Stack<List<BodyPart>> _spareLists = new Stack<List<BodyPart>>();
        readonly Dictionary<int, float> _lastHit = new Dictionary<int, float>();
        float _nextForget;

        void RunOverPeople(float dt, Vector3 groundVelocity, Vector3 up, Vector3 where, Quaternion turn)
        {
            if (Speed < Rules.bumpFrom) return;
            Vector3 lead = (Body.linearVelocity - groundVelocity) * (dt * 0.5f);
            Vector3 centre = where + turn * V(RoverLayout.RamCentre) + lead;
            Vector3 half = V(RoverLayout.RamHalfSize) + Vector3.one * 0.1f;
            // Hitboxes on living bodies, and the loose pieces (they move to the gib layer when they come off).
            int n = Physics.OverlapBoxNonAlloc(centre, half, _touched, turn, (1 << Layers.Hitbox) | (1 << Layers.Gib), QueryTriggerInteraction.Collide);
            if (n == 0) return;

            foreach (var list in _people.Values) { list.Clear(); _spareLists.Push(list); }
            _people.Clear();
            for (int i = 0; i < n; i++)
            {
                if (!_touched[i].TryGetComponent(out BodyPart part)) part = _touched[i].GetComponentInParent<BodyPart>();
                if (part == null || part.Owner == null || part.Owner.Model == null) continue;
                if (!_people.TryGetValue(part.Owner, out var parts))
                {
                    parts = _spareLists.Count > 0 ? _spareLists.Pop() : new List<BodyPart>();
                    _people[part.Owner] = parts;
                }
                if (!parts.Contains(part)) parts.Add(part);
            }

            float now = Time.time;
            if (now > _nextForget) { _lastHit.Clear(); _nextForget = now + 20f; }
            foreach (var pair in _people)
            {
                Mannequin who = pair.Key;
                List<BodyPart> parts = pair.Value;
                if (who == null || parts.Count == 0) continue;
                // Living body first, loose pieces and the dead after: each is its own thing to hit.
                for (int p = 0; p < parts.Count; p++)
                {
                    BodyPart part = parts[p];
                    if (part == null) continue;
                    int group = who.Model.Group(part.Id);
                    int key = who.GetHashCode() * 31 + group;
                    if (_lastHit.TryGetValue(key, out float last) && now - last < Rules.hitAgainAfter) continue;

                    Vector3 at = part.transform.position;
                    Rigidbody loose = part.Hitbox != null ? part.Hitbox.attachedRigidbody : null;
                    Vector3 theirs = loose != null && !loose.isKinematic ? loose.linearVelocity : who.Velocity;
                    Vector3 mine = Body.GetPointVelocity(at);
                    Vector3 closing = mine - theirs;
                    Vector3 away = Vector3.ProjectOnPlane(at - Body.worldCenterOfMass, up);
                    float speed = away.sqrMagnitude > 1e-4f ? Vector3.Dot(closing, away.normalized) : closing.magnitude;
                    // Under the wheels counts too: whatever is beneath it is being driven over, whichever way.
                    if (Vector3.Dot(at - where, up) < 0.6f) speed = Mathf.Max(speed, closing.magnitude * 0.8f);
                    if (speed < Rules.bumpFrom) continue;

                    bool living = group == 0 && who.Alive;
                    RunOverResult result = RunOver.Judge(Rules, speed);
                    if (living && result == RunOverResult.Bump)
                    {
                        // Too slow to break anything: they are shoved aside and stagger, never driven through.
                        _lastHit[key] = now;
                        Vector3 aside = Vector3.ProjectOnPlane(at - where, up);
                        if (aside.sqrMagnitude < 1e-4f) aside = turn * Vector3.forward;
                        Hit(who, part, at, (aside.normalized + up * 0.2f).normalized, Rules.bumpDamage, lethal: false);
                        break;
                    }
                    if (!living && speed < Rules.breakFrom) continue;
                    _lastHit[key] = now;

                    // What comes off leaves with the rover's speed (and a little up), not the walker's.
                    // A little up as well, so they clear the bumper.
                    Vector3 fling = groundVelocity + (mine - groundVelocity) * Rules.fling + up * (Speed * 0.2f);
                    if (group == 0) who.Velocity = fling;
                    Vector3 direction = ((mine - groundVelocity).normalized + up * 0.35f).normalized;

                    if (living && result == RunOverResult.Splat)
                    {
                        BodyPartId target = who.Model.OnBody(BodyPartId.Torso) ? BodyPartId.Torso : BodyPartId.Head;
                        Hit(who, who.Part(target), at, direction, 100000f, lethal: true);
                        Splats++;
                        break;   // nothing left of this one to hit this step
                    }
                    float damage = RunOver.Damage(Rules, speed);
                    if (living)
                    {
                        // Every part of the body the rover is touching takes the blow.
                        for (int q = p; q < parts.Count; q++)
                        {
                            BodyPart other = parts[q];
                            if (other == null || who.Model.Group(other.Id) != 0) continue;
                            Hit(who, other, other.transform.position, direction, damage, lethal: false);
                            if (!who.Alive) break;
                        }
                        break;
                    }
                    Hit(who, part, at, direction, damage, lethal: false);
                }
            }
        }

        void Hit(Mannequin who, BodyPart part, Vector3 at, Vector3 direction, float damage, bool lethal)
        {
            if (part == null) return;
            var hit = new HitInfo { Damage = damage, Point = at, Direction = direction, Normal = -direction, Lethal = lethal };
            HitOutcome outcome = who.ApplyHit(part, hit);
            // It counts as the player's doing (hit markers, the gun ladder's kills, Focus), as long as someone drives.
            outcome.Source = HitSource.Rover;
            if (outcome.Hit && Driver != null) CombatEvents.RaisePlayerHit(outcome);
        }

        // ------------------------------------------------------------------ the rest

        public string DescribeForDebug()
        {
            return "Rover: " + Now + (Driver != null ? " (driven)" : string.Empty) + "  " + Mathf.RoundToInt(Speed * 3.6f) + " km/h  wheels down " + WheelsDown
                   + "  boost " + Mathf.RoundToInt(Boost.Amount * 100f) + "%  people hit " + Splats;
        }

        void OnDestroy()
        {
            if (_cables != null)
                for (int i = 0; i < _cables.Length; i++)
                    if (_cables[i] != null) Destroy(_cables[i].gameObject);
            if (_hud != null) Destroy(_hud.gameObject);
        }
    }
}
