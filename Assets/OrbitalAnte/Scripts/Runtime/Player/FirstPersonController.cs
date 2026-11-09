using OrbitalAnte.Input;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// Ground first-person controller for the test arena. Flight, jetpack and planet gravity
    /// replace parts of this in Milestones 10-11.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public static FirstPersonController Instance { get; private set; }

        public PlayerConfig Config;
        public Camera Camera { get; private set; }
        public Transform Head { get; private set; }
        /// <summary>Between the head and the camera; camera shake moves only this.</summary>
        public Transform ShakeRig { get; private set; }

        CharacterController cc;
        Vector3 velocity;
        float yaw;
        float pitch;
        float currentHeight;
        bool crouching;

        public Vector3 Velocity => velocity;

        public static FirstPersonController Create(PlayerConfig config, Vector3 position, float yawDegrees)
        {
            var go = new GameObject("Player");
            go.transform.position = position;
            var cc = go.AddComponent<CharacterController>();
            cc.height = config.standHeight;
            cc.radius = config.radius;
            cc.center = new Vector3(0, config.standHeight * 0.5f, 0);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;

            var fpc = go.AddComponent<FirstPersonController>();
            fpc.Config = config;
            fpc.yaw = yawDegrees;

            var head = new GameObject("Head").transform;
            head.SetParent(go.transform, false);
            head.localPosition = new Vector3(0, config.standHeight - config.eyeBelowTop, 0);
            fpc.Head = head;

            var shake = new GameObject("ShakeRig").transform;
            shake.SetParent(head, false);
            fpc.ShakeRig = shake;
            OrbitalAnte.Feel.GameFeel.ShakeTarget = shake;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(shake, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 2000f;
            cam.fieldOfView = config.fieldOfView;
            camGo.AddComponent<AudioListener>();
            fpc.Camera = cam;
            return fpc;
        }

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
            if (Config == null) Config = PlayerConfig.Load();
            currentHeight = Config.standHeight;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (PauseMenu.IsPaused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateLook(Time.unscaledDeltaTime); // aim never freezes during hitstop
            UpdateCrouch(dt);
            UpdateMove(dt);
        }

        void UpdateLook(float dt)
        {
            Vector2 mouse = GameInput.LookMouse.ReadValue<Vector2>() * Config.mouseSensitivity;
            Vector2 stick = GameInput.LookStick.ReadValue<Vector2>();
            float mag = stick.magnitude;
            if (mag > 0.0001f)
            {
                float curved = Mathf.Pow(Mathf.Clamp01(mag), Config.stickResponseExponent);
                stick = stick / mag * curved * Config.stickSensitivity * dt;
            }
            Vector2 look = mouse + stick;
            yaw += look.x;
            pitch += (Config.invertY ? 1f : -1f) * look.y;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Head.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        void UpdateCrouch(float dt)
        {
            bool wantCrouch = GameInput.Crouch.IsPressed();
            if (!wantCrouch && crouching && !HasHeadroom(Config.standHeight)) wantCrouch = true;
            crouching = wantCrouch;

            float target = crouching ? Config.crouchHeight : Config.standHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, target, Config.crouchTransitionSpeed * dt);
            cc.height = currentHeight;
            cc.center = new Vector3(0, currentHeight * 0.5f, 0);
            Head.localPosition = new Vector3(0, currentHeight - Config.eyeBelowTop, 0);
        }

        bool HasHeadroom(float height)
        {
            Vector3 bottom = transform.position + Vector3.up * Config.radius;
            float distance = height - Config.radius * 2f;
            return !Physics.SphereCast(bottom, Config.radius * 0.95f, Vector3.up, out _, distance,
                ~0, QueryTriggerInteraction.Ignore);
        }

        void UpdateMove(float dt)
        {
            Vector2 input = Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f);
            bool sprint = GameInput.Sprint.IsPressed() && !crouching && input.y > 0.1f;
            float speed = crouching ? Config.crouchSpeed : sprint ? Config.sprintSpeed : Config.walkSpeed;

            Vector3 wish = (transform.forward * input.y + transform.right * input.x) * speed;
            Vector3 horizontal = new Vector3(velocity.x, 0, velocity.z);
            float accel = cc.isGrounded ? Config.groundAcceleration : Config.airAcceleration;
            horizontal = Vector3.MoveTowards(horizontal, wish, accel * dt);

            if (cc.isGrounded)
            {
                if (velocity.y < 0f) velocity.y = -2f;
                if (GameInput.Jump.WasPressedThisFrame() && !crouching)
                    velocity.y = Mathf.Sqrt(2f * Config.jumpHeight * -Config.gravity);
            }
            velocity.y += Config.gravity * dt;

            velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
            var flags = cc.Move(velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.rigidbody;
            if (rb == null || rb.isKinematic) return;
            Vector3 push = new Vector3(hit.moveDirection.x, 0.15f, hit.moveDirection.z);
            rb.AddForceAtPosition(push * Config.pushStrength, hit.point, ForceMode.Impulse);
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
