using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Crouch / slide under obstacles. Resizes the collision capsule, lowers the camera target and
    /// requests the crouch locomotion tier. Standing up is refused while something is overhead.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Crouch")]
    public class CrouchAbility : ContinuousAbility
    {
        [Header("Input")]
        public CharacterAction action = CharacterAction.Crouch;

        [Tooltip("When true the profile decides whether crouch is a toggle or held.")]
        public bool useProfileToggleSetting = true;

        [Tooltip("Toggle behaviour used when the profile setting is ignored.")]
        public bool toggle;

        [Header("Output")]
        public int tierPriority = 200;
        public LocomotionTier tier = LocomotionTier.Crouch;

        [Tooltip("Extra speed multiplier applied on top of the crouch tier speed.")]
        [Min(0f)] public float speedMultiplier = 1f;

        [Header("Body")]
        [Tooltip("Crouch height in meters. Negative uses the profile value.")]
        public float crouchHeight = -1f;

        [Tooltip("Standing height in meters. Negative uses the profile value.")]
        public float standHeight = -1f;

        [Tooltip("How fast the capsule grows and shrinks.")]
        [Min(0f)] public float transitionSpeed = -1f;

        [Tooltip("Lowers the camera target while crouching so the camera follows the body.")]
        public bool lowerCameraTarget = true;

        [Tooltip("Moves the camera target down only partially, 1 = all the way down.")]
        [Range(0f, 1f)] public float cameraTargetCrouchRatio = 0.8f;

        LocomotionAbility _locomotion;
        Transform _cameraTarget;
        float _cameraTargetStandY;
        float _standHeight;
        float _crouchHeight;
        bool _crouchRequested;
        bool _externalRequest;
        bool _toggleState;

        /// <summary>True when the character is crouched (or crouching down right now).</summary>
        public bool IsCrouching { get; private set; }

        /// <summary>0 = fully standing, 1 = fully crouched.</summary>
        public float CrouchRatio { get; private set; }

        /// <summary>True when something above the character blocks standing up.</summary>
        public bool BlockedFromStanding { get; private set; }

        protected override void OnBind()
        {
            _locomotion = Context != null ? Context.GetAbility<LocomotionAbility>() : null;
            _cameraTarget = Context != null ? Context.CameraTarget : null;
            if (_cameraTarget != null) _cameraTargetStandY = _cameraTarget.localPosition.y;

            CharacterProfile p = Profile;
            _standHeight = standHeight > 0f ? standHeight : p.standHeight;
            _crouchHeight = crouchHeight > 0f ? crouchHeight : p.crouchHeight;

            if (Motor != null) _standHeight = Mathf.Max(_standHeight, Motor.Height);
        }

        protected override void OnTick(float deltaTime)
        {
            if (Motor == null) return;

            CharacterProfile p = Profile;
            float speed = transitionSpeed >= 0f ? transitionSpeed : p.crouchTransitionSpeed;
            bool wantsToggle = useProfileToggleSetting ? p.crouchIsToggle : toggle;

            CharacterInputFrame input = Input;
            if (input != null)
            {
                if (wantsToggle)
                {
                    if (input.ConsumePressed(action)) _toggleState = !_toggleState;
                }
                else
                {
                    // Held mode: the button state is the request, the press is still claimed so no
                    // other ability acts on the same input.
                    input.ConsumePressed(action);
                    _toggleState = input.IsHeld(action) || input.IsPressed(action) || _externalRequest;
                }
            }

            _crouchRequested = _toggleState;

            BlockedFromStanding = !Motor.CanFit(_standHeight);

            bool wantCrouch = _crouchRequested || BlockedFromStanding;

            float targetHeight = wantCrouch ? _crouchHeight : _standHeight;
            float newHeight = Mathf.MoveTowards(Motor.Height, targetHeight, speed * deltaTime);
            if (!Mathf.Approximately(newHeight, Motor.Height))
            {
                Motor.Height = newHeight;
            }

            float range = Mathf.Max(0.01f, _standHeight - _crouchHeight);
            CrouchRatio = Mathf.Clamp01((_standHeight - Motor.Height) / range);
            IsCrouching = wantCrouch || CrouchRatio > 0.01f;

            // Keep the character glued to the ground while the capsule shrinks.
            if (IsCrouching && Motor.IsGrounded) Motor.SetGravityScale(2f);

            if (IsCrouching && _locomotion != null)
            {
                _locomotion.RequestTier(this, tierPriority, tier, speedMultiplier);
            }
            else if (_locomotion != null)
            {
                _locomotion.ClearTierRequest(this);
            }

            if (lowerCameraTarget && _cameraTarget != null)
            {
                float ratio = CrouchRatio * cameraTargetCrouchRatio;
                Vector3 local = _cameraTarget.localPosition;
                local.y = Mathf.Lerp(local.y, Mathf.Lerp(_cameraTargetStandY, _cameraTargetStandY * 0.55f, ratio), CharacterMath.SmoothFactor(12f, deltaTime));
                _cameraTarget.localPosition = local;
            }
        }

        /// <summary>Forces crouch on or off from code (cutscenes, going through a vent).</summary>
        public void SetCrouch(bool value)
        {
            _crouchRequested = value;
            _toggleState = value;
            _externalRequest = value;
        }

        protected override void OnDisabled()
        {
            _crouchRequested = false;
            _toggleState = false;
            _externalRequest = false;
            if (_locomotion != null) _locomotion.ClearTierRequest(this);
        }
    }
}