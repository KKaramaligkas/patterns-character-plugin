using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Ready to use input source built on the classic Input Manager, so a project works with zero setup.
    /// Default bindings: WASD / arrows, mouse look, Space jump, Shift sprint, C crouch, Q dash, E interact,
    /// F fly, Tab camera, mouse wheel zoom, Escape unlocks the cursor.
    /// Projects that only run the new Input System can use the InputSystemSource instead.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Input/Legacy Input Source")]
    public class LegacyInputSource : CharacterInputSourceBehaviour
    {
        [Header("Look")]
        [Tooltip("Mouse look scale in degrees of rotation per pixel of mouse movement.")]
        [Min(0.001f)] public float mouseDegreesPerPixel = 0.12f;

        [Tooltip("Stick look scale in degrees per second at full deflection.")]
        [Min(1f)] public float stickDegreesPerSecond = 220f;

        [Header("Bindings")]
        public KeyCode jumpKey = KeyCode.Space;
        public KeyCode sprintKey = KeyCode.LeftShift;
        public KeyCode crouchKey = KeyCode.C;
        public KeyCode dashKey = KeyCode.LeftControl;
        public KeyCode interactKey = KeyCode.E;
        public KeyCode flyKey = KeyCode.F;
        public KeyCode glideKey = KeyCode.Space;
        public KeyCode toggleCameraKey = KeyCode.Tab;

        [Tooltip("When true the cursor is locked on start and Escape releases it.")]
        public bool lockCursorOnStart = true;

        [Header("Legacy Axes")]
        public string horizontalAxis = "Horizontal";
        public string verticalAxis = "Vertical";
        public string mouseXAxis = "Mouse X";
        public string mouseYAxis = "Mouse Y";
        public string lookXAxis = "Look X";
        public string lookYAxis = "Look Y";
        public string mouseScrollAxis = "Mouse ScrollWheel";

        [Tooltip("Axes that are not part of the default Input Manager setup are skipped instead of throwing.")]
        public bool ignoreMissingAxes = true;

        bool _prevJump, _prevSprint, _prevCrouch, _prevDash, _prevInteract, _prevFly, _prevPrimary, _prevSecondary, _prevCamera;

        float _suppressMouseUntil;

        void Start()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
#endif
        }

        public override string DeviceName
        {
            get { return "Keyboard & Mouse (legacy)"; }
        }

        public override void ResetLook()
        {
            _suppressMouseUntil = Time.unscaledTime + 0.15f;
        }

        public override void Poll(CharacterInputFrame frame, float deltaTime)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            float deadZone = 0.15f;

            // ---- movement
            float horizontal = ReadAxisSafe(horizontalAxis);
            float vertical = ReadAxisSafe(verticalAxis);
            Vector2 move = new Vector2(horizontal, vertical);
            move = ApplyDeadZone(move, deadZone);
            frame.move = move;
            frame.moveMagnitude = Mathf.Min(1f, move.magnitude);

            // ---- look (degrees, camera rigs apply the user sensitivity multiplier on top)
            if (Time.unscaledTime >= _suppressMouseUntil)
            {
                float mouseX = ReadAxisSafe(mouseXAxis);
                float mouseY = ReadAxisSafe(mouseYAxis);
                if (Mathf.Abs(mouseX) > 0f || Mathf.Abs(mouseY) > 0f)
                {
                    frame.look += new Vector2(mouseX, mouseY) * mouseDegreesPerPixel;
                    MarkActive();
                }
            }

            float lookX = ReadAxisSafe(lookXAxis);
            float lookY = ReadAxisSafe(lookYAxis);
            Vector2 stick = ApplyDeadZone(new Vector2(lookX, lookY), deadZone);
            if (stick.sqrMagnitude > 0f)
            {
                frame.look += stick * stickDegreesPerSecond * deltaTime;
                frame.lookFromStick = true;
                MarkActive();
            }

            frame.zoom += ReadAxisSafe(mouseScrollAxis);

            if (move.sqrMagnitude > 0f) MarkActive();

            // ---- buttons
            bool jump = SafeGetKey(jumpKey);
            bool sprint = SafeGetKey(sprintKey);
            bool crouch = SafeGetKey(crouchKey);
            bool dash = SafeGetKey(dashKey);
            bool interact = SafeGetKey(interactKey);
            bool fly = SafeGetKey(flyKey);
            bool primary = SafeGetMouseButton(0);
            bool secondary = SafeGetMouseButton(1);
            bool camera = SafeGetKey(toggleCameraKey);

            bool pressed, released;
            Edge(jump, ref _prevJump, out pressed, out released);
            frame.jumpPressed |= pressed;
            frame.jumpHeld |= jump;
            frame.jumpReleased |= released;

            Edge(sprint, ref _prevSprint, out pressed, out released);
            frame.sprintPressed |= pressed;
            frame.sprintHeld |= sprint;
            frame.sprintReleased |= released;

            Edge(crouch, ref _prevCrouch, out pressed, out released);
            frame.crouchPressed |= pressed;
            frame.crouchHeld |= crouch;
            frame.crouchReleased |= released;

            Edge(dash, ref _prevDash, out pressed, out released);
            frame.dashPressed |= pressed;

            Edge(interact, ref _prevInteract, out pressed, out released);
            frame.interactPressed |= pressed;

            Edge(fly, ref _prevFly, out pressed, out released);
            frame.flyPressed |= pressed;

            Edge(primary, ref _prevPrimary, out pressed, out released);
            frame.primaryPressed |= pressed;
            frame.primaryHeld |= primary;
            frame.primaryReleased |= released;

            Edge(secondary, ref _prevSecondary, out pressed, out released);
            frame.secondaryPressed |= pressed;
            frame.secondaryHeld |= secondary;

            Edge(camera, ref _prevCamera, out pressed, out released);
            frame.toggleCameraPressed |= pressed;

            frame.glideHeld |= SafeGetKey(glideKey);
            frame.cancelPressed |= Input.GetKeyDown(KeyCode.Escape);

            if (jump || sprint || crouch || dash || interact || fly || primary || secondary || camera) MarkActive();
            frame.deviceName = DeviceName;
#else
            frame.deviceName = "Legacy input disabled";
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        float ReadAxisSafe(string axis)
        {
            if (string.IsNullOrEmpty(axis)) return 0f;
            try
            {
                return Input.GetAxisRaw(axis);
            }
            catch (System.Exception)
            {
                if (!ignoreMissingAxes)
                {
                    Debug.LogWarningFormat("[Patterns.Character] Input axis '{0}' is not configured in the Input Manager.", axis);
                }
                return 0f;
            }
        }

        bool SafeGetKey(KeyCode key)
        {
            return key != KeyCode.None && Input.GetKey(key);
        }

        bool SafeGetMouseButton(int button)
        {
            return Input.GetMouseButton(button);
        }
#else
        float ReadAxisSafe(string axis) { return 0f; }
        bool SafeGetKey(KeyCode key) { return false; }
        bool SafeGetMouseButton(int button) { return false; }
#endif
    }
}