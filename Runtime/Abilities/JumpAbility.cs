using System;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Jumping with all the quality of life that makes a controller feel good: jump buffering,
    /// coyote time, multiple air jumps and variable jump height.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Jump")]
    public class JumpAbility : InputTriggeredAbility
    {
        [Header("Jump")]
        [Tooltip("Jump height in meters. Negative uses the profile value.")]
        public float jumpHeight = -1f;

        [Tooltip("Multiplies the profile jump height, handy for spring pads and low gravity zones.")]
        [Min(0f)] public float heightMultiplier = 1f;

        [Tooltip("Extra air jumps. Negative uses the profile value.")]
        public int airJumps = -1;

        [Tooltip("Seconds after walking off a ledge during which a jump is still allowed. Negative uses the profile value.")]
        public float coyoteTime = -1f;

        [Tooltip("Seconds a jump press is remembered while airborne. Negative uses the profile value.")]
        public float jumpBuffer = -1f;

        [Header("Feel")]
        [Tooltip("Releasing the jump button early cuts the rise short.")]
        public bool variableHeight = true;

        [Tooltip("Speed multiplier kept from the ground when jumping. 1 keeps everything.")]
        [Range(0f, 1f)] public float horizontalRetention = -1f;

        [Tooltip("When true a jump is refused unless there is headroom.")]
        public bool checkCeiling;

        [Tooltip("Radius used by the ceiling check.")]
        [Min(0.01f)] public float ceilingCheckRadius = 0.3f;

        [Tooltip("Stamina spent per jump. Negative uses the profile value.")]
        public float staminaCost = -1f;

        float _coyoteTimer;
        float _bufferTimer;
        int _airJumpsUsed;
        float _lastJumpTime = -100f;

        /// <summary>True while the character is rising after a jump.</summary>
        public bool IsJumping
        {
            get { return Motor != null && !Motor.IsGrounded && Motor.Velocity.y > 0.05f && Time.time - _lastJumpTime < 1.5f; }
        }

        /// <summary>Air jumps used since the character last touched the ground.</summary>
        public int AirJumpsUsed { get { return _airJumpsUsed; } }

        /// <summary>True while a buffered press is waiting for the ground.</summary>
        public bool HasBufferedJump { get { return _bufferTimer > 0f; } }

        public event Action<bool> Jumped;        // true = air jump
        public event Action<float> JumpCut;      // released rise speed

        protected override void OnBind()
        {
        }

        protected override bool WantsToTrigger()
        {
            // Jump is edge triggered, and the buffer keeps a press alive for a few frames.
            if (Input == null) return false;

            if (Input.ConsumePressed(action))
            {
                _bufferTimer = GetJumpBuffer();
            }

            if (_bufferTimer <= 0f) return false;

            _bufferTimer -= Time.deltaTime;
            return true;
        }

        protected override bool MeetsActivationConditions
        {
            get
            {
                if (Motor == null) return false;
                if (Motor.IsGrounded || _coyoteTimer > 0f) return true;
                return _airJumpsUsed < GetAirJumps();
            }
        }

        protected override void OnTick(float deltaTime)
        {
            if (Motor == null) return;

            if (Motor.IsGrounded)
            {
                _coyoteTimer = GetCoyoteTime();
                _airJumpsUsed = 0;
            }
            else if (_coyoteTimer > 0f)
            {
                _coyoteTimer = Mathf.Max(0f, _coyoteTimer - deltaTime);
            }

            base.OnTick(deltaTime);

            HandleJumpCut();
        }

        void HandleJumpCut()
        {
            if (!variableHeight || Motor == null) return;
            if (Input == null || !Input.jumpReleased) return;
            if (Motor.Velocity.y <= 0f) return;

            CharacterProfile p = Profile;
            float cut = Motor.Velocity.y - p.jumpCutMultiplier;
            if (cut < 0f) cut = 0f;
            Motor.SetVerticalVelocity(cut);
            Motor.ClearExternalVelocity();

            var handler = JumpCut;
            if (handler != null) handler(cut);
        }

        protected override void OnTrigger()
        {
            CharacterProfile p = Profile;

            if (checkCeiling && IsBlockedByCeiling()) return;

            float cost = staminaCost >= 0f ? staminaCost : p.jumpStaminaCost;
            if (cost > 0f && Resources != null && !Resources.TrySpend(costResource, cost)) return;

            bool groundedJump = Motor.IsGrounded || _coyoteTimer > 0f;
            if (!groundedJump) _airJumpsUsed++;

            float height = (jumpHeight > 0f ? jumpHeight : p.jumpHeight) * heightMultiplier;
            float vertical = p.JumpVelocityFor(height, Mathf.Max(0.05f, Motor.persistentGravityScale));

            Motor.Launch(vertical);

            // Horizontal momentum: keep the speed the character had when it jumped.
            float retention = horizontalRetention >= 0f ? horizontalRetention : p.jumpHorizontalRetention;
            if (retention < 1f && Motor.PlanarSpeed > 0f)
            {
                Vector3 planar = Motor.PlanarVelocity * retention;
                Motor.SubmitPlanarRequest(this, motorPriority - 1, planar);
            }

            _coyoteTimer = 0f;
            _bufferTimer = 0f;
            _lastJumpTime = Time.time;

            var handler = Jumped;
            if (handler != null) handler(!groundedJump);
        }

        float GetCoyoteTime()
        {
            return coyoteTime >= 0f ? coyoteTime : Profile.coyoteTime;
        }

        float GetJumpBuffer()
        {
            return jumpBuffer >= 0f ? jumpBuffer : Profile.jumpBuffer;
        }

        int GetAirJumps()
        {
            return airJumps >= 0 ? airJumps : Profile.airJumps;
        }

        bool IsBlockedByCeiling()
        {
            float height = Motor.Height;
            float radius = Mathf.Max(0.05f, ceilingCheckRadius);
            Vector3 origin = Motor.ColliderCenter + Vector3.up * (height * 0.5f - radius);
            float distance = Mathf.Max(0.2f, (jumpHeight > 0f ? jumpHeight : Profile.jumpHeight) * 0.8f);

            // The sweep starts inside the character's own capsule, so own colliders are filtered out.
            return CharacterCollision.SweepHitsBlocker(origin, Vector3.up, radius, distance, ~0, transform);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!checkCeiling) return;
            Vector3 origin = transform.position + Vector3.up * 1f;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(origin, ceilingCheckRadius);
        }
    }
}