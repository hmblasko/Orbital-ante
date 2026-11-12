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
        }

        void Update()
        {
            if (Config == null || GameSession.Paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Frozen)
            {
                _horizontalVelocity = Vector3.zero;
                return;
            }

            Look(dt);
            SettleRecoil(dt);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            Move(dt);
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
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
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
                _yaw += shaped.x * Config.stickYawSpeed * dt;
                _pitch -= shaped.y * Config.stickPitchSpeed * dt;
            }

            _pitch = Mathf.Clamp(_pitch, -88f, 88f);
        }

        void LateUpdate()
        {
            if (Eye == null || GameSession.Paused) return;
            // Applied every frame so camera shake can ride on top of where the player is looking.
            Quaternion shake = Shake != null ? Shake.Offset : Quaternion.identity;
            Eye.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f) * shake;
        }

        void Move(float dt)
        {
            Vector2 input = Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f);
            bool grounded = _controller.isGrounded;
            if (grounded) _lastGroundedTime = Time.time;

            bool sprinting = GameInput.Sprint.IsPressed() && input.y > 0.3f;
            float speed = sprinting ? Config.sprintSpeed : Config.walkSpeed;
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;

            float acceleration = grounded ? Config.groundAcceleration : Config.airAcceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, wish, acceleration * dt);

            if (grounded && _verticalVelocity < 0f) _verticalVelocity = -2f;

            bool canJump = Time.time - _lastGroundedTime <= Config.coyoteTime;
            if (canJump && GameInput.Jump.WasPressedThisFrame())
            {
                _verticalVelocity = Mathf.Sqrt(2f * Config.gravity * Config.jumpHeight);
                _lastGroundedTime = -10f;
            }

            _verticalVelocity -= Config.gravity * dt;

            Vector3 velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            CollisionFlags flags = _controller.Move(velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;
        }
    }
}
