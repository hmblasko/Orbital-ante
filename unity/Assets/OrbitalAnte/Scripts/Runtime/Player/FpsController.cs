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

            Look(dt);
            Move(dt);
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
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
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
