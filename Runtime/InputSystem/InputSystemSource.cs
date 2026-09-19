#if PATTERNS_INPUTSYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace Patterns.Character.InputSystem
{
    /// <summary>
    /// New Input System source. Works with zero setup: when no action asset is assigned it reads the
    /// keyboard, mouse and gamepad directly, which is enough to play immediately. Assign an
    /// <see cref="InputActionAsset"/> to drive the character from your own actions instead.
    ///
    /// Expected action names inside the asset: Move, Look, Jump, Sprint, Crouch, Dash, Interact, Fly,
    /// Glide, Primary, Secondary, ToggleCamera, Zoom, Cancel.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Input/Input System Source")]
    public class InputSystemSource : CharacterInputSourceBehaviour
    {
        [Header("Actions Asset (optional)")]
        [Tooltip("Leave empty to use the built in keyboard, mouse and gamepad bindings.")]
        public InputActionAsset actions;

        [Tooltip("Action map inside the asset that holds the character actions.")]
        public string actionMapName = "Player";

        [Header("Built In Bindings")]
        public Key jumpKey = Key.Space;
        public Key sprintKey = Key.LeftShift;
        public Key crouchKey = Key.C;
        public Key dashKey = Key.LeftCtrl;
        public Key interactKey = Key.E;
        public Key flyKey = Key.F;
        public Key toggleCameraKey = Key.Tab;

        [Header("Look")]
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [Min(0.001f)] public float mouseDegreesPerPixel = 0.12f;

        [Tooltip("Degrees per second at full stick deflection.")]
        [Min(1f)] public float stickDegreesPerSecond = 220f;

        [Range(0f, 0.9f)] public float stickDeadZone = 0.15f;

        [Header("Cursor")]
        public bool lockCursorOnStart = true;

        [SerializeField] int priorityValue = 100;

        InputActionMap _map;
        InputAction _move, _look, _jump, _sprint, _crouch, _dash, _interact, _fly, _glide, _primary, _secondary, _toggleCamera, _zoom, _cancel;

        bool _prevJump, _prevSprint, _prevCrouch, _prevDash, _prevInteract, _prevFly, _prevPrimary, _prevSecondary, _prevCamera, _prevCancel;

        float _suppressMouseUntil;
        bool _actionsReady;

        public override int Priority { get { return priorityValue; } }

        public override string DeviceName
        {
            get
            {
                if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame) return "Gamepad";
                if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return "Keyboard & Mouse";
                return "Input System";
            }
        }

        void Start()
        {
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnEnable()
        {
            EnsureActions();
            if (_map != null) _map.Enable();
        }

        void OnDisable()
        {
            if (_map != null) _map.Disable();
        }

        void EnsureActions()
        {
            if (_actionsReady) return;
            _actionsReady = true;

            if (actions == null || string.IsNullOrEmpty(actionMapName)) return;

            _map = actions.FindActionMap(actionMapName, false);
            if (_map == null)
            {
                Debug.LogWarningFormat("[Patterns.Character] Action map '{0}' was not found in the actions asset.", actionMapName);
                return;
            }

            _move = _map.FindAction("Move", false);
            _look = _map.FindAction("Look", false);
            _jump = _map.FindAction("Jump", false);
            _sprint = _map.FindAction("Sprint", false);
            _crouch = _map.FindAction("Crouch", false);
            _dash = _map.FindAction("Dash", false);
            _interact = _map.FindAction("Interact", false);
            _fly = _map.FindAction("Fly", false);
            _glide = _map.FindAction("Glide", false);
            _primary = _map.FindAction("Primary", false);
            _secondary = _map.FindAction("Secondary", false);
            _toggleCamera = _map.FindAction("ToggleCamera", false);
            _zoom = _map.FindAction("Zoom", false);
            _cancel = _map.FindAction("Cancel", false);
        }

        public override void ResetLook()
        {
            _suppressMouseUntil = Time.unscaledTime + 0.15f;
        }

        public override void Poll(CharacterInputFrame frame, float deltaTime)
        {
            if (_map != null) PollFromActions(frame, deltaTime);
            else PollFromDevices(frame, deltaTime);
        }

        // ---------------------------------------------------------------- action asset

        void PollFromActions(CharacterInputFrame frame, float deltaTime)
        {
            if (_move != null)
            {
                Vector2 move = ApplyDeadZone(_move.ReadValue<Vector2>(), stickDeadZone);
                frame.move += move;
                if (move.sqrMagnitude > 0f) MarkActive();
            }

            if (_look != null)
            {
                Vector2 look = _look.ReadValue<Vector2>();
                frame.look += look;
                if (Gamepad.current != null && Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.0001f)
                {
                    frame.lookFromStick = true;
                }
                if (look.sqrMagnitude > 0f) MarkActive();
            }

            if (_zoom != null) frame.zoom += _zoom.ReadValue<float>();

            Edge(_jump != null && _jump.IsPressed() && _jump.triggered, ref _prevJump, frame, ActionKind.Jump);
            Edge(_sprint != null && _sprint.IsPressed(), ref _prevSprint, frame, ActionKind.Sprint);
            Edge(_crouch != null && _crouch.IsPressed(), ref _prevCrouch, frame, ActionKind.Crouch);
            Edge(_dash != null && _dash.triggered, ref _prevDash, frame, ActionKind.Dash);
            Edge(_interact != null && _interact.triggered, ref _prevInteract, frame, ActionKind.Interact);
            Edge(_fly != null && _fly.triggered, ref _prevFly, frame, ActionKind.Fly);
            Edge(_primary != null && _primary.IsPressed(), ref _prevPrimary, frame, ActionKind.Primary);
            Edge(_secondary != null && _secondary.IsPressed(), ref _prevSecondary, frame, ActionKind.Secondary);
            Edge(_toggleCamera != null && _toggleCamera.triggered, ref _prevCamera, frame, ActionKind.ToggleCamera);
            Edge(_cancel != null && _cancel.triggered, ref _prevCancel, frame, ActionKind.Cancel);

            if (_glide != null && _glide.IsPressed()) frame.glideHeld = true;

            frame.moveMagnitude = Mathf.Min(1f, frame.move.magnitude);
            frame.deviceName = DeviceName;
        }

        enum ActionKind { Jump, Sprint, Crouch, Dash, Interact, Fly, Primary, Secondary, ToggleCamera, Cancel }

        void Edge(bool value, ref bool previous, CharacterInputFrame frame, ActionKind kind)
        {
            bool pressed = value && !previous;
            bool released = !value && previous;
            previous = value;

            switch (kind)
            {
                case ActionKind.Jump:
                    frame.jumpPressed |= pressed;
                    frame.jumpHeld |= value;
                    frame.jumpReleased |= released;
                    break;
                case ActionKind.Sprint:
                    frame.sprintPressed |= pressed;
                    frame.sprintHeld |= value;
                    frame.sprintReleased |= released;
                    break;
                case ActionKind.Crouch:
                    frame.crouchPressed |= pressed;
                    frame.crouchHeld |= value;
                    frame.crouchReleased |= released;
                    break;
                case ActionKind.Dash:
                    frame.dashPressed |= pressed;
                    break;
                case ActionKind.Interact:
                    frame.interactPressed |= pressed;
                    break;
                case ActionKind.Fly:
                    frame.flyPressed |= pressed;
                    break;
                case ActionKind.Primary:
                    frame.primaryPressed |= pressed;
                    frame.primaryHeld |= value;
                    frame.primaryReleased |= released;
                    break;
                case ActionKind.Secondary:
                    frame.secondaryPressed |= pressed;
                    frame.secondaryHeld |= value;
                    break;
                case ActionKind.ToggleCamera:
                    frame.toggleCameraPressed |= pressed;
                    break;
                case ActionKind.Cancel:
                    frame.cancelPressed |= pressed;
                    break;
            }

            if (pressed || released) MarkActive();
        }

        // ---------------------------------------------------------------- direct devices

        void PollFromDevices(CharacterInputFrame frame, float deltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;

            Vector2 move = Vector2.zero;
            float vertical = 0f;
            float horizontal = 0f;

            GamepadStick(gamepad, ref horizontal, ref vertical);
            KeyboardAxis(keyboard, ref horizontal, ref vertical);

            move = ApplyDeadZone(new Vector2(horizontal, vertical), stickDeadZone);
            frame.move += move;
            if (move.sqrMagnitude > 0f) MarkActive();

            // ---- look
            if (mouse != null && Time.unscaledTime >= _suppressMouseUntil)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0f)
                {
                    frame.look += delta * mouseDegreesPerPixel;
                    MarkActive();
                }
            }

            if (gamepad != null)
            {
                Vector2 stick = ApplyDeadZone(gamepad.rightStick.ReadValue(), stickDeadZone);
                if (stick.sqrMagnitude > 0f)
                {
                    frame.look += stick * stickDegreesPerSecond * deltaTime;
                    frame.lookFromStick = true;
                    MarkActive();
                }
            }

            if (mouse != null)
            {
                frame.zoom += mouse.scroll.ReadValue().y / 120f;
            }

            // ---- buttons
            bool jump = (keyboard != null && keyboard[jumpKey].isPressed) || (gamepad != null && gamepad.buttonSouth.isPressed);
            bool sprint = (keyboard != null && keyboard[sprintKey].isPressed) || (gamepad != null && gamepad.leftStickButton.isPressed);
            bool crouch = (keyboard != null && keyboard[crouchKey].isPressed) || (gamepad != null && gamepad.buttonEast.isPressed);
            bool dash = (keyboard != null && keyboard[dashKey].wasPressedThisFrame) || (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);
            bool interact = (keyboard != null && keyboard[interactKey].wasPressedThisFrame) || (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame);
            bool fly = keyboard != null && keyboard[flyKey].wasPressedThisFrame;
            bool primary = (mouse != null && mouse.leftButton.isPressed) || (gamepad != null && gamepad.rightTrigger.isPressed);
            bool secondary = (mouse != null && mouse.rightButton.isPressed) || (gamepad != null && gamepad.leftTrigger.isPressed);
            bool camera = (keyboard != null && keyboard[toggleCameraKey].wasPressedThisFrame) || (gamepad != null && gamepad.selectButton.wasPressedThisFrame);

            Edge(jump, ref _prevJump, frame, ActionKind.Jump);
            Edge(sprint, ref _prevSprint, frame, ActionKind.Sprint);
            Edge(crouch, ref _prevCrouch, frame, ActionKind.Crouch);

            if (dash || !dash) { } // dash uses an edge of its own because it fires once per press
            bool dashPressed = dash && !_prevDash;
            _prevDash = dash;
            frame.dashPressed |= dashPressed;

            bool interactPressed = interact && !_prevInteract;
            _prevInteract = interact;
            frame.interactPressed |= interactPressed;

            bool flyPressed = fly && !_prevFly;
            _prevFly = fly;
            frame.flyPressed |= flyPressed;

            Edge(primary, ref _prevPrimary, frame, ActionKind.Primary);
            Edge(secondary, ref _prevSecondary, frame, ActionKind.Secondary);
            Edge(camera, ref _prevCamera, frame, ActionKind.ToggleCamera);

            bool cancel = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool cancelPressed = cancel && !_prevCancel;
            _prevCancel = cancel;
            frame.cancelPressed |= cancelPressed;

            // Glide shares the jump button when held in the air, which most games do.
            if (jump) frame.glideHeld = true;

            frame.moveMagnitude = Mathf.Min(1f, frame.move.magnitude);
            frame.deviceName = DeviceName;
        }

        static void GamepadStick(Gamepad gamepad, ref float horizontal, ref float vertical)
        {
            if (gamepad == null) return;
            Vector2 stick = gamepad.leftStick.ReadValue();
            horizontal += stick.x;
            vertical += stick.y;
        }

        static void KeyboardAxis(Keyboard keyboard, ref float horizontal, ref float vertical)
        {
            if (keyboard == null) return;

            horizontal += ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
            vertical += ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
            horizontal += ReadAxis(keyboard.leftArrowKey.isPressed, keyboard.rightArrowKey.isPressed);
            vertical += ReadAxis(keyboard.downArrowKey.isPressed, keyboard.upArrowKey.isPressed);
        }
    }
}
#endif