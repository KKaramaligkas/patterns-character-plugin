using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Gliding / parachute / bird flight. While airborne and falling, holding the glide action slows the
    /// descent and raises the locomotion tier so the character moves further per second.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Glide")]
    public class GlideAbility : ContinuousAbility
    {
        [Header("Trigger")]
        public CharacterAction action = CharacterAction.Glide;

        [Tooltip("When false gliding starts automatically as soon as the character is falling.")]
        public bool requiresHolding = true;

        [Tooltip("Only glide while moving downwards.")]
        public bool onlyWhenFalling = true;

        [Tooltip("Minimum height above the ground before gliding can start, avoids clipping the floor.")]
        [Min(0f)] public float minimumGroundClearance = 0.75f;

        [Tooltip("Stops gliding once the character is this fast downwards, useful as a safety cap.")]
        public float maximumFallSpeed = 30f;

        [Header("Output")]
        public int tierPriority = 150;
        public LocomotionTier tier = LocomotionTier.Glide;

        [Tooltip("Fall speed multiplier. 0.15 means 15% of the normal fall speed.")]
        [Range(0.01f, 1f)] public float fallSpeedMultiplier = -1f;

        [Tooltip("Extra speed multiplier applied to the glide tier.")]
        [Min(0f)] public float speedMultiplier = 1f;

        [Tooltip("When true gliding drains stamina and stops when empty.")]
        public bool useStamina = true;

        [Header("Feel")]
        [Tooltip("Sharpness used to blend into and out of the reduced fall speed.")]
        [Min(0f)] public float blendSharpness = 6f;

        float _currentMultiplier = 1f;

        /// <summary>True while gliding.</summary>
        public bool IsGliding { get; private set; }

        /// <summary>0..1 blend between normal falling and gliding, for VFX and animation.</summary>
        public float GlideRatio { get; private set; }

        protected override void OnTick(float deltaTime)
        {
            if (Motor == null) return;

            CharacterProfile p = Profile;
            bool wantsGlide;

            if (requiresHolding)
            {
                wantsGlide = Input != null && Input.IsHeld(action);
            }
            else
            {
                wantsGlide = true;
            }

            bool canGlide = !Motor.IsGrounded;
            if (onlyWhenFalling && Motor.Velocity.y > 0f) canGlide = false;
            if (Motor.Velocity.y < -maximumFallSpeed) canGlide = false;

            if (minimumGroundClearance > 0f && canGlide)
            {
                float clearance = GroundClearance();
                if (clearance >= 0f && clearance < minimumGroundClearance) canGlide = false;
            }

            if (canGlide && useStamina && drainPerSecond > 0f)
            {
                canGlide = TryDrain(deltaTime);
            }

            IsGliding = wantsGlide && canGlide;

            float target = 1f;
            if (IsGliding)
            {
                target = fallSpeedMultiplier >= 0f ? fallSpeedMultiplier : p.glideFallSpeedMultiplier;
            }

            _currentMultiplier = Mathf.Lerp(_currentMultiplier, target, CharacterMath.SmoothFactor(Mathf.Max(0.01f, blendSharpness), deltaTime));
            GlideRatio = Mathf.Clamp01(1f - _currentMultiplier);

            if (_currentMultiplier < 0.999f)
            {
                Motor.SetFallSpeedLimit(p.maxFallSpeed * _currentMultiplier);
            }

            LocomotionAbility locomotion = Context != null ? Context.GetAbility<LocomotionAbility>() : null;
            if (locomotion != null)
            {
                if (IsGliding) locomotion.RequestTier(this, tierPriority, tier, speedMultiplier);
                else locomotion.ClearTierRequest(this);
            }
        }

        float GroundClearance()
        {
            Vector3 origin = Motor.FootPosition + Vector3.up * 0.1f;
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.distance;
            }
            return -1f;
        }

        protected override void OnDisabled()
        {
            IsGliding = false;
            GlideRatio = 0f;
            _currentMultiplier = 1f;

            LocomotionAbility locomotion = Context != null ? Context.GetAbility<LocomotionAbility>() : null;
            if (locomotion != null) locomotion.ClearTierRequest(this);
        }
    }
}