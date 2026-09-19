using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Holding sprint raises the locomotion tier and drains stamina. Sprinting can be restricted to
    /// grounded, forward-only movement, which is what third person games usually want.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Sprint")]
    public class SprintAbility : ContinuousAbility
    {
        [Header("Trigger")]
        public CharacterAction action = CharacterAction.Sprint;

        [Tooltip("Only allow sprinting while the stick / keys point forward.")]
        public bool requiresForward = true;

        [Tooltip("How aligned with the camera forward the input must be. 1 = perfectly forward.")]
        [Range(-1f, 1f)] public float forwardThreshold = 0.35f;

        [Tooltip("Only allow sprinting while grounded.")]
        public bool requiresGrounded = true;

        [Tooltip("Delay in seconds between holding sprint and actually sprinting.")]
        [Min(0f)] public float startDelay = 0.15f;

        [Header("Output")]
        [Tooltip("Priority of the tier request. Crouch and swim use higher numbers so they always win.")]
        public int tierPriority = 100;

        public LocomotionTier tier = LocomotionTier.Sprint;

        [Tooltip("Extra multiplier applied on top of the tier speed.")]
        [Min(0f)] public float speedMultiplier = 1f;

        [Header("Stamina")]
        [Tooltip("When true the character can keep sprinting until the stamina pool is empty.")]
        public bool useStamina = true;

        [Tooltip("Stamina drain per second while sprinting. Negative value uses the profile setting.")]
        public float staminaDrainPerSecond = -1f;

        [Tooltip("Stamina required to start a sprint.")]
        [Min(0f)] public float minimumStamina = -1f;

        LocomotionAbility _locomotion;
        float _heldFor;

        /// <summary>True while the character is actually sprinting this frame.</summary>
        public bool IsSprinting { get; private set; }

        /// <summary>True while the sprint button is held, even if the sprint is blocked.</summary>
        public bool SprintHeld { get; private set; }

        public bool IsExhausted { get; private set; }

        protected override void OnBind()
        {
            _locomotion = Context != null ? Context.GetAbility<LocomotionAbility>() : null;
        }

        protected override void OnTick(float deltaTime)
        {
            CharacterProfile p = Profile;
            CharacterInputFrame input = Input;

            SprintHeld = input != null && input.IsHeld(action);

            if (!SprintHeld)
            {
                _heldFor = 0f;
                StopSprinting();
                return;
            }

            _heldFor += deltaTime;

            if (!tier.Equals(LocomotionTier.Sprint) && _heldFor < startDelay)
            {
                StopSprinting();
                return;
            }

            if (requiresGrounded && Motor != null && !Motor.IsGrounded)
            {
                StopSprinting();
                return;
            }

            if (requiresForward && _locomotion != null && _locomotion.HasInput)
            {
                Vector3 reference = Look != null ? Look.YawForward : transform.forward;
                Vector3 moveDirection = _locomotion.InputDirection.sqrMagnitude > 1e-5f
                    ? _locomotion.InputDirection.normalized
                    : Vector3.zero;

                if (Vector3.Dot(moveDirection, reference) < forwardThreshold)
                {
                    StopSprinting();
                    return;
                }
            }

            if (useStamina && Resources != null)
            {
                float minimal = minimumStamina >= 0f ? minimumStamina : p.sprintMinStamina;
                bool hasEnough = Resources.Has(costResource, minimal);

                if (!IsSprinting && !hasEnough)
                {
                    IsExhausted = true;
                    StopSprinting();
                    return;
                }

                float drain = staminaDrainPerSecond >= 0f ? staminaDrainPerSecond : p.sprintStaminaDrain;
                if (drain > 0f && !Resources.Drain(costResource, drain * deltaTime))
                {
                    IsExhausted = true;
                    StopSprinting();
                    return;
                }
            }

            IsExhausted = false;
            IsSprinting = true;

            if (_locomotion != null)
            {
                _locomotion.RequestTier(this, tierPriority, tier, speedMultiplier);
            }
        }

        void StopSprinting()
        {
            IsSprinting = false;
            if (_locomotion != null) _locomotion.ClearTierRequest(this);
        }

        protected override void OnDisabled()
        {
            StopSprinting();
        }
    }
}