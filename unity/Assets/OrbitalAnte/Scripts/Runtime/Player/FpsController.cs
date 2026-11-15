using OrbitalAnte.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// First-person walking controller for the test arena. Keyboard and mouse or gamepad.
    /// (Planet gravity, the suit and the jetpack replace this in later milestones.)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-240)] // The current eye pose must exist before PlayerWeapons.Update casts a shot.
    public sealed class FpsController : MonoBehaviour
    {
        public Camera Eye { get; private set; }
        public PlayerConfig Config { get; private set; }
        /// <summary>Optional camera shake to layer on top of the view.</summary>
        public CameraShake Shake { get; set; }

        CharacterController _controller;
        Vector3 _horizontalVelocity;
        float _verticalVelocity;
        float _yaw;
        float _pitch;
        float _lastGroundedTime = -10f;
        float _recoilReturnPitch;
        float _recoilReturnYaw;
        float _recoilRecovery = 14f;
        float _standingHeight, _standingEye, _standingStep, _lean, _visibleLean;
        Vector3 _standingCenter;
        readonly CameraClearance _clearance = new CameraClearance();
        readonly ParkourMotor _parkour = new ParkourMotor();
        public bool Crouched => _controller != null && _controller.height < _standingHeight - 0.001f;
        public bool WallRunning => _parkour.WallRunning;
        public bool Climbing => _parkour.Climbing;

        /// <summary>When true the player cannot move or look (used for the moment after being shot down).</summary>
        public bool Frozen { get; set; }
        /// <summary>Current movement in metres per second. Things the player drops inherit it.</summary>
        public Vector3 Velocity => _horizontalVelocity + Vector3.up * _verticalVelocity;

        public void Initialize(PlayerConfig config, Camera eye)
        {
            Config = config;
            Eye = eye;
            _controller = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
            _pitch = 0f;
            _standingHeight = _controller.height;
            _standingCenter = _controller.center;
            _standingStep = _controller.stepOffset;
            _standingEye = Eye.transform.localPosition.y;
        }

        void Update()
        {
            if (Config == null || GameSession.Paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Frozen)
            {
                _parkour.Stop();
                _horizontalVelocity = Vector3.zero;
                return;
            }

            Look(dt);
            SettleRecoil(dt);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            UpdateStance(TimeFlow.PlayerDelta(dt));
            Move(dt);
            ApplyBasePose();
        }

        /// <summary>
        /// Kicks the view, the way a gun does. Part of each kick settles back by itself; the rest stays, so
        /// holding the trigger makes the gun climb and the player has to pull it back down.
        /// </summary>
        /// <param name="pitchUp">Degrees the view jumps upward.</param>
        /// <param name="yaw">Degrees sideways (positive is right).</param>
        /// <param name="returnFraction">0 to 1: how much of the kick comes back by itself.</param>
        /// <param name="recoverySpeed">How fast it comes back, in degrees per second.</param>
        public void AddRecoil(float pitchUp, float yaw, float returnFraction, float recoverySpeed)
        {
            float before = _pitch;
            _pitch = Mathf.Clamp(_pitch - pitchUp, -88f, 88f);
            float applied = before - _pitch;
            _yaw += yaw;

            returnFraction = Mathf.Clamp01(returnFraction);
            _recoilReturnPitch += applied * returnFraction;
            _recoilReturnYaw += yaw * returnFraction;
            _recoilRecovery = Mathf.Max(1f, recoverySpeed);
        }

        void SettleRecoil(float dt)
        {
            float step = _recoilRecovery * dt;
            if (_recoilReturnPitch != 0f)
            {
                float next = Mathf.MoveTowards(_recoilReturnPitch, 0f, step);
                _pitch = Mathf.Clamp(_pitch + (_recoilReturnPitch - next), -88f, 88f);
                _recoilReturnPitch = next;
            }
            if (_recoilReturnYaw != 0f)
            {
                float next = Mathf.MoveTowards(_recoilReturnYaw, 0f, step);
                _yaw -= _recoilReturnYaw - next;
                _recoilReturnYaw = next;
            }
        }

        /// <summary>Moves the player somewhere else in one go and stops them dead (used when respawning).</summary>
        public void Teleport(Vector3 position, float yawDegrees)
        {
            // A CharacterController ignores direct position changes unless it is switched off first.
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _yaw = yawDegrees;
            _pitch = 0f;
            _recoilReturnPitch = 0f;
            _recoilReturnYaw = 0f;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _parkour.Reset();
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _controller.height = _standingHeight;
            _controller.center = _standingCenter;
            _controller.stepOffset = _standingStep;
            _lean = _visibleLean = 0f;
            Eye.transform.localPosition = new Vector3(0f, _standingEye, 0f);
            Eye.transform.localRotation = Quaternion.identity;
        }

        void Look(float dt)
        {
            Vector2 look = GameInput.Look.ReadValue<Vector2>();
            if (look.sqrMagnitude <= 0f) return;

            InputControl control = GameInput.Look.activeControl;
            bool fromPointer = control != null && control.device is Pointer;
            if (fromPointer)
            {
                // Mouse movement is already "distance moved this frame", so it must not be scaled by time.
                _yaw += look.x * Config.mouseSensitivity;
                _pitch -= look.y * Config.mouseSensitivity;
            }
            else
            {
                float magnitude = Mathf.Clamp01(look.magnitude);
                float curved = Mathf.Pow(magnitude, Mathf.Max(1f, Config.stickCurve));
                Vector2 shaped = magnitude > 0.0001f ? look / magnitude * curved : Vector2.zero;
                // Your own head does not slow when the world does (hitstop, Deep Breath).
                float own = TimeFlow.PlayerDelta(dt);
                _yaw += shaped.x * Config.stickYawSpeed * own;
                _pitch -= shaped.y * Config.stickPitchSpeed * own;
            }

            _pitch = Mathf.Clamp(_pitch, -88f, 88f);
        }

        void LateUpdate()
        {
            if (Eye == null || GameSession.Paused || Frozen) return;
            // Recoil may have arrived after Update; rebuild once, then layer the final shake on top.
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            ApplyBasePose();
            // Applied every frame so camera shake can ride on top of where the player is looking.
            Quaternion shake = Shake != null ? Shake.Offset : Quaternion.identity;
            Eye.transform.localRotation *= shake;
        }

        void UpdateStance(float dt)
        {
            float crouch = StanceRules.CrouchHeight(Config.crouchHeight, _standingHeight, _controller.radius);
            bool wantCrouch = GameInput.JetDown.IsPressed() && (_controller.isGrounded || Climbing);
            float wanted = wantCrouch ? crouch : _standingHeight;
            float next = Mathf.MoveTowards(_controller.height, wanted, StanceRules.NonNegative(Config.stanceSpeed) * dt);
            Vector3 feet = transform.TransformPoint(_standingCenter - Vector3.up * (_standingHeight * 0.5f));
            if (!Mathf.Approximately(next, _controller.height) &&
                (next < _controller.height || _parkour.CanOccupy(feet, transform.up, next, _controller.radius, transform, _controller.skinWidth)))
            {
                _controller.height = next;
                _controller.center = _standingCenter + Vector3.up * ((next - _standingHeight) * 0.5f);
                _controller.stepOffset = Mathf.Min(_standingStep, next * 0.5f);
            }
            int lean = StanceRules.Lean(GameInput.SuitRollLeft.IsPressed(), GameInput.SuitRollRight.IsPressed());
            _lean = Mathf.MoveTowards(_lean, lean, StanceRules.NonNegative(Config.leanSpeed) * dt);
        }

        void ApplyBasePose()
        {
            float crouch = StanceRules.CrouchHeight(Config.crouchHeight, _standingHeight, _controller.radius);
            float eyeHeight = StanceRules.EyeHeight(_controller.height, _standingHeight, _standingEye, crouch, Config.crouchEyeHeight);
            Vector3 feet = transform.TransformPoint(_standingCenter - Vector3.up * (_standingHeight * 0.5f));
            float radius = CameraClearance.Radius(Eye, Config.headClearanceRadius);
            Vector3 centre = feet + transform.up * (_controller.height * 0.5f);
            Vector3 uprightEye = _clearance.Clip(centre, feet + transform.up * eyeHeight, radius, transform);
            float distance = StanceRules.NonNegative(Config.leanDistance);
            Vector3 eye = _clearance.Clip(uprightEye, uprightEye + transform.right * (_lean * distance), radius, transform);
            _visibleLean = distance > 0f ? Vector3.Dot(eye - uprightEye, transform.right) / distance : 0f;
            Eye.transform.position = eye;
            Eye.transform.localRotation = Quaternion.Euler(_pitch, 0f, -_visibleLean * StanceRules.NonNegative(Config.leanDegrees));
        }

        void Move(float dt)
        {
            Vector2 input = Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f);
            bool grounded = _controller.isGrounded;
            bool jump = GameInput.Jump.WasPressedThisFrame();
            bool down = GameInput.JetDown.IsPressed();
            if (grounded && !Climbing)
            {
                _lastGroundedTime = Time.time;
                _parkour.State.Grounded();
            }
            _parkour.Tick(dt);
            Vector3 feet = transform.TransformPoint(_standingCenter - Vector3.up * (_standingHeight * 0.5f));
            bool climbAllowed = Config.parkourEnabled && ParkourRules.GravityAllows(Config.gravity, Config.parkourMinimumGravity);
            bool parkourAllowed = climbAllowed && !down && !Crouched;
            if (Climbing && (!climbAllowed || !GameInput.Jump.IsPressed()))
            {
                Vector3 release = _parkour.ClimbExitVelocity;
                _parkour.Stop();
                _horizontalVelocity = Vector3.ProjectOnPlane(release, Vector3.up);
                _verticalVelocity = release.y;
            }
            if (!Climbing && climbAllowed && !grounded && jump && input.y > 0.3f)
                _parkour.TryBeginClimb(feet, Vector3.up, transform.forward, _controller.height, _controller.radius, transform, Velocity, Config, _controller.skinWidth);
            if (Climbing)
            {
                _lastGroundedTime = -10f;
                if (_parkour.TryClimbStep(feet, Vector3.up, _controller.height, _controller.radius, transform, Config, dt, out Vector3 climb, _controller.skinWidth))
                {
                    Vector3 before = transform.position;
                    _controller.Move(climb);
                    Vector3 actual = transform.position - before;
                    _horizontalVelocity = Vector3.ProjectOnPlane(actual, Vector3.up) / dt;
                    _verticalVelocity = actual.y / dt;
                    // A controller collision can shorten a query-approved step. Do not continue driving into it.
                    if ((actual - climb).sqrMagnitude > 0.0004f)
                    {
                        Vector3 release = _parkour.ClimbExitVelocity;
                        _parkour.Stop();
                        _horizontalVelocity = Vector3.ProjectOnPlane(release, Vector3.up);
                        _verticalVelocity = release.y;
                    }
                    return;
                }
                Vector3 exit = _parkour.ClimbExitVelocity;
                _horizontalVelocity = Vector3.ProjectOnPlane(exit, Vector3.up);
                _verticalVelocity = exit.y;
            }

            bool sprinting = !Crouched && GameInput.Sprint.IsPressed() && input.y > 0.3f;
            float speed = sprinting ? Config.sprintSpeed : Config.walkSpeed;
            if (Crouched) speed *= Mathf.Clamp01(StanceRules.NonNegative(Config.crouchSpeedShare));
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;

            float acceleration = grounded ? Config.groundAcceleration : Config.airAcceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, wish, acceleration * dt);

            if (grounded && _verticalVelocity < 0f) _verticalVelocity = -2f;

            bool canJump = Time.time - _lastGroundedTime <= Config.coyoteTime;
            if (canJump && jump)
            {
                _verticalVelocity = Mathf.Sqrt(2f * Config.gravity * Config.jumpHeight);
                _lastGroundedTime = -10f;
            }

            _verticalVelocity -= Config.gravity * dt;

            Vector3 velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _parkour.TryWallRun(feet, Vector3.up, transform.forward, _controller.height, _controller.radius,
                transform, Config, parkourAllowed, grounded, sprinting, input.y, Crouched, jump, Config.gravity, dt, ref velocity);
            _horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            _verticalVelocity = velocity.y;
            CollisionFlags flags = _controller.Move(velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;
        }
    }
}
