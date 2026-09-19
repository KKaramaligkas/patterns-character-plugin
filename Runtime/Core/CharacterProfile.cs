using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// All tuning values a character needs, as a data asset. The plugin works with no profile assigned
    /// (falling back to <see cref="Default"/>) so a project can be set up in seconds and tuned later.
    /// </summary>
    [CreateAssetMenu(menuName = "Patterns/Character/Character Profile", fileName = "CharacterProfile")]
    public class CharacterProfile : ScriptableObject
    {
        [Header("Locomotion - Speeds (m/s)")]
        [Min(0f)] public float walkSpeed = 2.2f;
        [Min(0f)] public float runSpeed = 5f;
        [Min(0f)] public float sprintSpeed = 8.5f;
        [Min(0f)] public float crouchSpeed = 1.2f;
        [Min(0f)] public float swimSpeed = 3f;
        [Min(0f)] public float flySpeed = 9f;
        [Min(0f)] public float glideSpeed = 6f;

        [Header("Locomotion - Feels")]
        [Tooltip("How fast the character reaches its target speed when moving.")]
        [Min(0f)] public float acceleration = 18f;
        [Tooltip("How fast the character slows down when the input stops.")]
        [Min(0f)] public float deceleration = 22f;
        [Range(0f, 1f)] public float airControl = 0.45f;
        [Tooltip("Degrees per second used to rotate the body towards the target direction.")]
        [Min(0f)] public float rotationSpeed = 900f;
        [Tooltip("When true the body rotates with a snappy curve instead of a constant angular speed.")]
        public bool snappyRotation = true;
        [Range(0f, 1f)] public float deadZone = 0.15f;

        [Header("Gravity & Ground")]
        public float gravity = -24f;
        [Min(0f)] public float maxFallSpeed = 45f;
        [Tooltip("Downward correction applied when the character walks off a small ledge while grounded.")]
        [Min(0f)] public float groundSnapDistance = 0.35f;
        [Tooltip("Distance below the capsule used to look for the ground surface.")]
        [Min(0.01f)] public float groundProbeDistance = 0.25f;
        [Tooltip("Allow the character to slide down slopes steeper than the controller slope limit.")]
        public bool slideOnSteepSlopes = true;
        [Min(0f)] public float slopeSlideSpeed = 12f;

        [Header("Jump")]
        [Min(0f)] public float jumpHeight = 1.25f;
        [Tooltip("Extra jumps available while airborne. 0 = single jump, 1 = double jump.")]
        [Min(0)] public int airJumps = 1;
        [Min(0f)] public float coyoteTime = 0.12f;
        [Min(0f)] public float jumpBuffer = 0.15f;
        [Tooltip("When true, releasing jump early cuts the remaining upward velocity.")]
        public bool variableJumpHeight = true;
        [Tooltip("Multiplier applied to the upward velocity when jump is released early.")]
        [Min(0f)] public float jumpCutMultiplier = 2.5f;
        [Tooltip("Horizontal speed kept while jumping from a run.")]
        [Range(0f, 1f)] public float jumpHorizontalRetention = 0.9f;
        [Min(0f)] public float jumpStaminaCost = 10f;

        [Header("Sprint")]
        [Min(0f)] public float sprintStaminaDrain = 18f;
        [Min(0f)] public float sprintMinStamina = 5f;
        [Tooltip("When true sprinting is only allowed while pushing the stick forward.")]
        public bool sprintOnlyForward = true;
        [Tooltip("Delay before sprinting starts after the key/button is held.")]
        [Min(0f)] public float sprintDelay = 0f;

        [Header("Crouch")]
        [Min(0.1f)] public float crouchHeight = 1.1f;
        [Min(0.1f)] public float standHeight = 1.8f;
        [Min(0f)] public float crouchTransitionSpeed = 6f;
        [Tooltip("When true the crouch action toggles instead of being held.")]
        public bool crouchIsToggle;
        [Tooltip("Allows standing up as soon as the ceiling check is clear even if crouch is still held.")]
        public bool autoStandWhenBlocked = true;

        [Header("Dash")]
        [Min(0f)] public float dashSpeed = 16f;
        [Min(0f)] public float dashDuration = 0.18f;
        [Min(0f)] public float dashCooldown = 0.6f;
        [Min(0f)] public float dashStaminaCost = 20f;
        public bool dashInAir = true;
        [Tooltip("Number of dashes allowed before touching the ground again. 0 = unlimited.")]
        [Min(0)] public int airDashCount = 1;
        [Tooltip("When true the character ignores gravity during the dash.")]
        public bool dashIgnoresGravity = true;

        [Header("Swim")]
        [Min(0f)] public float buoyancy = 4f;
        [Range(0f, 1f)] public float waterDrag = 0.35f;
        [Min(0f)] public float waterExitImpulse = 3f;

        [Header("Glide")]
        [Range(0f, 1f)] public float glideFallSpeedMultiplier = 0.18f;
        [Min(0f)] public float glideStaminaDrain = 6f;

        [Header("Camera")]
        [Tooltip("User facing multiplier on top of the scale defined by the input source.")]
        [Min(0f)] public float mouseSensitivity = 1f;
        [Min(0f)] public float gamepadSensitivity = 1f;
        public bool invertY;
        public bool invertX;
        [Range(0f, 90f)] public float minPitch = -80f;
        [Range(0f, 90f)] public float maxPitch = 80f;
        [Min(0f)] public float cameraYawSharpness = 40f;
        [Min(0f)] public float cameraPitchSharpness = 40f;

        [Header("Resources")]
        [Min(1f)] public float maxHealth = 100f;
        [Min(1f)] public float maxStamina = 100f;
        [Min(0f)] public float staminaRegenPerSecond = 22f;
        [Min(0f)] public float staminaRegenDelay = 0.75f;

        static CharacterProfile _default;

        /// <summary>A shared, in-memory profile used when no asset is assigned. Never written to disk.</summary>
        public static CharacterProfile Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<CharacterProfile>();
                    _default.name = "CharacterProfile (Runtime Default)";
                    _default.hideFlags = HideFlags.HideAndDontSave;
                }
                return _default;
            }
        }

        /// <summary>Converts a jump height (in meters) into the initial vertical velocity required.</summary>
        public float JumpVelocityFor(float height, float gravityScale = 1f)
        {
            float g = Mathf.Abs(gravity * (gravityScale == 0f ? 1f : gravityScale));
            return Mathf.Sqrt(2f * g * Mathf.Max(0f, height));
        }

        /// <summary>Speed associated with a locomotion tier.</summary>
        public float SpeedFor(LocomotionTier tier)
        {
            switch (tier)
            {
                case LocomotionTier.Walk: return walkSpeed;
                case LocomotionTier.Run: return runSpeed;
                case LocomotionTier.Sprint: return sprintSpeed;
                case LocomotionTier.Crouch: return crouchSpeed;
                case LocomotionTier.Swim: return swimSpeed;
                case LocomotionTier.Fly: return flySpeed;
                case LocomotionTier.Glide: return glideSpeed;
                default: return 0f;
            }
        }
    }

    /// <summary>Locomotion tiers a character can be in. Also used for animation blend trees.</summary>
    public enum LocomotionTier
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Sprint = 3,
        Crouch = 4,
        Swim = 5,
        Fly = 6,
        Glide = 7
    }
}