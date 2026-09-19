using System;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// One frame of device-agnostic input. Reused every frame to stay allocation free, therefore
    /// abilities must never store a reference to it.
    /// </summary>
    public sealed class CharacterInputFrame
    {
        public Vector2 move;
        public Vector2 look;
        public float zoom;
        public float moveMagnitude;

        public bool jumpPressed;
        public bool jumpHeld;
        public bool jumpReleased;

        public bool sprintPressed;
        public bool sprintHeld;
        public bool sprintReleased;

        public bool crouchPressed;
        public bool crouchHeld;
        public bool crouchReleased;

        public bool dashPressed;
        public bool glideHeld;

        public bool interactPressed;

        public bool flyPressed;

        public bool primaryPressed;
        public bool primaryHeld;
        public bool primaryReleased;

        public bool secondaryPressed;
        public bool secondaryHeld;

        public bool toggleCameraPressed;

        /// <summary>Set by sources when the look values came from an analogue stick, used for gamepad sensitivity.</summary>
        public bool lookFromStick;

        /// <summary>Cancel / back / escape was pressed. Camera modes use it to release the cursor.</summary>
        public bool cancelPressed;

        /// <summary>True when a virtual device (touch / on-screen stick) produced the movement values.</summary>
        public bool fromVirtualDevice;

        /// <summary>Human readable name of the device that produced this frame.</summary>
        public string deviceName = "None";

        bool[] _consumed = new bool[16];

        public bool HasMovementInput
        {
            get { return moveMagnitude > 0.01f; }
        }

        public bool HasLookInput
        {
            get { return look.sqrMagnitude > 0.0001f || Mathf.Abs(zoom) > 0.0001f; }
        }

        /// <summary>Reads the state of any action without consuming it.</summary>
        public bool IsPressed(CharacterAction action)
        {
            switch (action)
            {
                case CharacterAction.Jump: return jumpPressed;
                case CharacterAction.Sprint: return sprintPressed;
                case CharacterAction.Crouch: return crouchPressed;
                case CharacterAction.Dash: return dashPressed;
                case CharacterAction.Interact: return interactPressed;
                case CharacterAction.Fly: return flyPressed;
                case CharacterAction.PrimaryAction: return primaryPressed;
                case CharacterAction.SecondaryAction: return secondaryPressed;
                case CharacterAction.ToggleCamera: return toggleCameraPressed;
                case CharacterAction.Glide: return glideHeld;
                default: return false;
            }
        }

        /// <summary>Reads the held state of any action.</summary>
        public bool IsHeld(CharacterAction action)
        {
            switch (action)
            {
                case CharacterAction.Jump: return jumpHeld;
                case CharacterAction.Sprint: return sprintHeld;
                case CharacterAction.Crouch: return crouchHeld;
                case CharacterAction.Dash: return dashPressed;
                case CharacterAction.PrimaryAction: return primaryHeld;
                case CharacterAction.SecondaryAction: return secondaryHeld;
                case CharacterAction.Glide: return glideHeld;
                default: return IsPressed(action);
            }
        }

        /// <summary>
        /// Claims an action for this frame. A second ability asking for the same action in the same frame
        /// will get false, which keeps jump, dash and interact from firing at once.
        /// </summary>
        public bool ConsumePressed(CharacterAction action)
        {
            if (!IsPressed(action)) return false;
            int index = (int)action;
            if (index < 0 || index >= _consumed.Length) return true;
            if (_consumed[index]) return false;
            _consumed[index] = true;
            return true;
        }

        /// <summary>Returns true when the action was already claimed this frame.</summary>
        public bool IsConsumed(CharacterAction action)
        {
            int index = (int)action;
            return index >= 0 && index < _consumed.Length && _consumed[index];
        }

        /// <summary>Removes the claim placed on an action by an ability that decided not to act.</summary>
        public void ReleaseClaim(CharacterAction action)
        {
            int index = (int)action;
            if (index >= 0 && index < _consumed.Length) _consumed[index] = false;
        }

        public void Clear()
        {
            move = Vector2.zero;
            look = Vector2.zero;
            zoom = 0f;
            moveMagnitude = 0f;

            jumpPressed = jumpHeld = jumpReleased = false;
            sprintPressed = sprintHeld = sprintReleased = false;
            crouchPressed = crouchHeld = crouchReleased = false;
            dashPressed = glideHeld = false;
            interactPressed = false;
            flyPressed = false;
            primaryPressed = primaryHeld = primaryReleased = false;
            secondaryPressed = secondaryHeld = false;
            toggleCameraPressed = false;
            lookFromStick = false;
            cancelPressed = false;
            fromVirtualDevice = false;

            for (int i = 0; i < _consumed.Length; i++) _consumed[i] = false;
        }
    }

    /// <summary>
    /// Anything that can feed input into a character. Implement this to plug in a new backend
    /// (touch controls, networking, AI, replays) without touching the abilities.
    /// </summary>
    public interface ICharacterInputSource
    {
        /// <summary>Fills the supplied frame with the current device state.</summary>
        void Poll(CharacterInputFrame frame, float deltaTime);

        /// <summary>Higher priority sources win when several are present. The Input System source uses 100.</summary>
        int Priority { get; }

        /// <summary>True while the source has an enabled, usable device.</summary>
        bool IsAvailable { get; }

        /// <summary>True when a device produced meaningful input recently. Used to auto pick between sources.</summary>
        bool IsActive { get; }

        /// <summary>Human readable device name, shown in the inspector and in the UI.</summary>
        string DeviceName { get; }

        /// <summary>Called when the source becomes the one driving the character.</summary>
        void OnActivated();

        /// <summary>Called when another source took over.</summary>
        void OnDeactivated();

        /// <summary>Requests a cursor / look reset (used when the camera mode changes).</summary>
        void ResetLook();
    }

    /// <summary>Convenience base class for MonoBehaviour input sources.</summary>
    public abstract class CharacterInputSourceBehaviour : MonoBehaviour, ICharacterInputSource
    {
        [Tooltip("Higher wins when several sources are present on the character.")]
        [SerializeField] protected int priority;

        public virtual int Priority { get { return priority; } }
        public virtual bool IsAvailable { get { return isActiveAndEnabled; } }
        public virtual bool IsActive { get { return isActiveAndEnabled && Time.unscaledTime - _lastActivity < 3f; } }

        protected float _lastActivity = -100f;

        public virtual string DeviceName
        {
            get { return name; }
        }

        public abstract void Poll(CharacterInputFrame frame, float deltaTime);

        public virtual void OnActivated() { }
        public virtual void OnDeactivated() { }

        public virtual void ResetLook() { }

        protected void MarkActive()
        {
            _lastActivity = Time.unscaledTime;
        }

        /// <summary>Compares an axis value against the profile dead zone and rescales it.</summary>
        protected static float ApplyDeadZone(float value, float deadZone)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude <= deadZone) return 0f;
            return Mathf.Sign(value) * ((magnitude - deadZone) / (1f - deadZone));
        }

        protected static Vector2 ApplyDeadZone(Vector2 value, float deadZone)
        {
            float magnitude = value.magnitude;
            if (magnitude <= deadZone) return Vector2.zero;
            float scaled = (magnitude - deadZone) / (1f - deadZone);
            return value / magnitude * Mathf.Min(1f, scaled);
        }

        /// <summary>Edge detection helper used to build pressed / released flags.</summary>
        protected static void Edge(bool current, ref bool previous, out bool pressed, out bool released)
        {
            pressed = current && !previous;
            released = !current && previous;
            previous = current;
        }

        /// <summary>Axis helper that works whether or not the legacy input manager is enabled.</summary>
        protected static float ReadAxis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }
}