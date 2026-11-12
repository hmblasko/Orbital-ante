using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// A bare first-person flying test craft, here only so the solar system can be flown through and judged.
    /// It is not the ship: no cockpit, no damage, no fuel, no autopilot. Those are Milestones 7 to 10.
    /// What it does have is the part that matters for judging the solar system: real inertia. Nothing slows you
    /// down unless you thrust the other way (or hold Match Velocity, which does that for you).
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
        /// <summary>How much of full turning and sideways thrust is available (damaged thrusters give less). 0 to 1.</summary>
        public float SideScale { get; set; } = 1f;
        bool Locked => ControlsLocked || Unmanned;

        /// <summary>Set by whoever builds the craft, so it can ask where bodies are and how they are moving.</summary>
        public SolarSystem System { get; set; }
        /// <summary>The body the nose is pointing at, or -1. Match Velocity matches this body when there is one.</summary>
        public int Target { get; private set; } = -1;
        /// <summary>Speed toward the target in metres per second. Negative when moving apart.</summary>
        public float ClosingSpeed { get; private set; }
        /// <summary>Distance to the target's surface in metres.</summary>
        public float TargetDistance { get; private set; }
        /// <summary>How far the craft would travel before Match Velocity finished matching the target, in metres.</summary>
        public float StoppingDistance { get; private set; }

        /// <summary>The body Match Velocity is measured against: the target if there is one, otherwise the nearby body.</summary>
        public int MatchBody => Target >= 0 ? Target : System != null ? System.Frame.Anchor : 0;

        const float TargetCone = 7f;

        Vector2 _lookPending;
        AudioSource _thrusterSound;
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
            if (GameInput.Asset == null || GameSession.Paused) return;

            if (Locked)
            {
                ThrustInput = Vector3.zero;
                _lookPending = Vector2.zero;
                Boosting = false;
                MatchingVelocity = false;
            }
            else
            {
                Vector2 move = GameInput.FlyMove.ReadValue<Vector2>();
                float lift = (GameInput.FlyUp.IsPressed() ? 1f : 0f) - (GameInput.FlyDown.IsPressed() ? 1f : 0f);
                ThrustInput = Vector3.ClampMagnitude(new Vector3(move.x, lift, move.y), 1f);
                Boosting = GameInput.Boost.IsPressed();
                MatchingVelocity = GameInput.MatchVelocity.IsPressed();

                // Mouse movement arrives as "how far since last frame"; a stick arrives as "how far it is pushed".
                Vector2 look = GameInput.FlyLook.ReadValue<Vector2>();
                var control = GameInput.FlyLook.activeControl;
                bool mouse = control != null && control.device is UnityEngine.InputSystem.Pointer;
                if (mouse) _lookPending += look * Config.mouseSensitivity;
                else _lookPending += look * (Config.stickTurnSpeed * Time.unscaledDeltaTime);
            }

            if (!Locked) UpdateTarget();

            float target = ThrustInput.magnitude * (Boosting ? 1f : 0.55f);
            if (MatchingVelocity && RelativeVelocity().sqrMagnitude > 0.04f) target = Mathf.Max(target, 0.8f);
            _thrusterLevel = Mathf.MoveTowards(_thrusterLevel, target, Time.unscaledDeltaTime * 5f);
            _thrusterSound.volume = _thrusterLevel * Config.thrusterVolume;
            _thrusterSound.pitch = 0.8f + _thrusterLevel * 0.5f;
        }

        /// <summary>The craft's velocity measured against the body Match Velocity would match.</summary>
        public Vector3 RelativeVelocity()
        {
            Vector3 velocity = Body.linearVelocity;
            if (System != null && Target >= 0) velocity -= System.LocalVelocityOf(Target);
            return velocity;
        }

        /// <summary>
        /// Picks the body the nose is pointing at. Pointing anywhere on a big planet's disc counts, and a small
        /// far-off one only needs to be within a few degrees.
        /// </summary>
        void UpdateTarget()
        {
            Target = -1;
            if (System == null || System.Model == null || Eye == null) return;
            Vector3 eye = Eye.transform.position;
            Vector3 forward = Eye.transform.forward;
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
                float off = Vector3.Angle(forward, to) - Mathf.Asin(radius / distance) * Mathf.Rad2Deg;
                if (off < 0f) off = 0f;
                // Among bodies under the nose, the nearer one wins.
                float score = off + distance * 1e-7f;
                if (score < best)
                {
                    best = score;
                    Target = i;
                }
            }

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
            if (!Locked)
            {
                // Only a pilot steadies the ship. Left alone it tumbles, tips and comes to rest like any other object.
                Body.MoveRotation(Body.rotation * turn);
                Body.angularVelocity = Vector3.Lerp(Body.angularVelocity, Vector3.zero, 1f - Mathf.Exp(-Config.spinDamping * dt));
            }

            float power = Config.thrust * (Boosting ? Config.boostMultiplier : 1f);
            if (ThrustInput.sqrMagnitude > 0.0001f)
            {
                // The main engines lift; the small thrusters push sideways and fore and aft.
                Vector3 push = new Vector3(ThrustInput.x * SideScale, ThrustInput.y * LiftScale, ThrustInput.z * SideScale);
                Body.AddRelativeForce(push * power, ForceMode.Acceleration);
            }

            if (MatchingVelocity)
            {
                // Matches the body under the nose. With nothing under the nose, velocity here is already measured
                // against the nearby body (see ReferenceFrame), so matching it is simply coming to a stop.
                Vector3 velocity = RelativeVelocity();
                float speed = velocity.magnitude;
                if (speed > 0.02f)
                {
                    float brake = Mathf.Min(Config.matchVelocityThrust * Mathf.Min(LiftScale, SideScale), speed / dt);
                    Body.AddForce(-velocity / speed * brake, ForceMode.Acceleration);
                }
            }
        }
    
        void OnDestroy()
        {
            // The rumble is made afresh for every run.
            if (_thrusterSound != null && _thrusterSound.clip != null) Destroy(_thrusterSound.clip);
        }
    }
}
