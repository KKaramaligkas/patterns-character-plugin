using System;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Burst of speed in a direction, on the ground or in the air. Uses the motor impulse channel so the
    /// motion reads as a dash and then hands the remaining speed back to locomotion instead of stopping dead.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Dash")]
    public class DashAbility : InputTriggeredAbility
    {
        [Header("Dash")]
        [Tooltip("Dash speed in m/s. Negative uses the profile value.")]
        public float dashSpeed = -1f;

        [Tooltip("Dash duration in seconds. Negative uses the profile value.")]
        public float duration = -1f;

        [Tooltip("Dash cooldown in seconds. Negative uses the profile value.")]
        public float dashCooldown = -1f;

        [Tooltip("When true the dash follows the movement input, otherwise the character faces forward.")]
        public bool useInputDirection = true;

        [Tooltip("Air dashes allowed before landing. Negative uses the profile value.")]
        public int airDashes = -1;

        [Header("Feel")]
        [Tooltip("Gravity is suspended while dashing.")]
        public bool suspendGravity = true;

        [Tooltip("Fraction of the dash speed kept when the dash ends.")]
        [Range(0f, 1f)] public float exitSpeedRetention = 0.35f;

        [Tooltip("How much of the character's previous velocity is cancelled at the start of the dash.")]
        [Range(0f, 1f)] public float entryVelocityCancel = 1f;

        [Tooltip("Marks the character as invulnerable while dashing (i-frames).")]
        public bool invulnerableWhileDashing;

        [Tooltip("Pushes nearby rigidbodies away on contact.")]
        public bool applyPhysicsForce = true;

        [Tooltip("Impulse applied to rigidbodies hit during the dash.")]
        [Min(0f)] public float physicsForce = 6f;

        [Tooltip("Stamina spent per dash. Negative uses the profile value.")]
        public float staminaCost = -1f;

        Vector3 _direction;
        float _endTime;
        int _airDashesUsed;

        /// <summary>Direction of the dash currently in progress.</summary>
        public Vector3 DashDirection { get { return _direction; } }

        /// <summary>True while dashing.</summary>
        public bool IsDashing { get { return IsActive; } }

        /// <summary>True while the dash cooldown is running.</summary>
        public bool IsOnCooldown { get { return CooldownRemaining > 0f; } }

        public event Action<Vector3> Dashed;
        public event Action<Vector3> DashFinished;

        protected override void OnTick(float deltaTime)
        {
            if (Motor == null) return;

            if (Motor.IsGrounded)
            {
                _airDashesUsed = 0;
            }

            base.OnTick(deltaTime);
        }

        protected override bool CanTrigger()
        {
            if (!base.CanTrigger()) return false;
            if (Motor == null) return false;

            if (!Motor.IsGrounded)
            {
                CharacterProfile p = Profile;
                bool profileAllows = p.dashInAir;
                int allowed = airDashes >= 0 ? airDashes : p.airDashCount;

                if (!profileAllows) return false;
                if (allowed > 0 && _airDashesUsed >= allowed) return false;
            }

            return true;
        }

        protected override void OnTrigger()
        {
            CharacterProfile p = Profile;

            float cost = staminaCost >= 0f ? staminaCost : p.dashStaminaCost;
            if (cost > 0f && Resources != null && !Resources.TrySpend(costResource, cost)) return;

            _direction = ResolveDirection();
            float speed = dashSpeed >= 0f ? dashSpeed : p.dashSpeed;
            float length = duration >= 0f ? duration : p.dashDuration;

            if (entryVelocityCancel > 0f)
            {
                Motor.ClearExternalVelocity();
                Vector3 planar = Vector3.ProjectOnPlane(Motor.MotorVelocity, Vector3.up);
                Motor.SubmitPlanarRequest(this, motorPriority, planar * (1f - entryVelocityCancel));
            }

            Motor.SetExternalVelocity(_direction * speed);

            if (applyPhysicsForce) PushNearbyBodies(speed);

            if (!Motor.IsGrounded) _airDashesUsed++;

            _endTime = Time.time + length;

            var handler = Dashed;
            if (handler != null) handler(_direction);
        }

        protected override void OnActiveTick(float deltaTime)
        {
            if (Motor == null) return;

            CharacterProfile p = Profile;
            float speed = dashSpeed >= 0f ? dashSpeed : p.dashSpeed;
            bool ignoreGravity = suspendGravity && p.dashIgnoresGravity;

            // Keep the impulse alive for the whole dash, then let it decay.
            Motor.SetExternalVelocity(_direction * speed);

            // Lock the horizontal axis to the dash so locomotion cannot fight it.
            SubmitMotion(new Vector3(0f, Motor.Velocity.y, 0f), false, true);

            if (ignoreGravity) Motor.SetGravityScale(0f);

            if (Time.time >= _endTime) FinishActive();
        }

        protected override void OnActiveEnd()
        {
            if (Motor == null) return;

            CharacterProfile p = Profile;
            float speed = dashSpeed >= 0f ? dashSpeed : p.dashSpeed;

            // Hand a slice of the dash speed back so the exit feels continuous.
            Motor.TransferExternalVelocityToMotor(exitSpeedRetention * Mathf.Max(0.2f, Mathf.Min(1f, speed / 20f)));
            Motor.ClearExternalVelocity();

            var handler = DashFinished;
            if (handler != null) handler(_direction);
        }

        Vector3 ResolveDirection()
        {
            Vector3 direction = Vector3.zero;

            if (useInputDirection && Input != null && Input.move.sqrMagnitude > 0.01f)
            {
                bool hasInput;
                Transform cameraTransform = Context != null && Context.CameraRig != null ? Context.CameraRig.ActiveCameraTransform : null;
                direction = CharacterMath.InputToWorld(Input.move, MovementSpace.CameraBasis, transform, cameraTransform, out hasInput);
            }

            if (direction.sqrMagnitude < 1e-4f)
            {
                direction = transform.forward;
            }

            direction.y = 0f;
            return direction.normalized;
        }

        void PushNearbyBodies(float speed)
        {
            if (!applyPhysicsForce || physicsForce <= 0f) return;

            float radius = Mathf.Max(0.4f, Motor.Radius * 1.5f);
            Collider[] hits = Physics.OverlapSphere(Motor.ColliderCenter, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Rigidbody body = hits[i].attachedRigidbody;
                if (body == null || body.isKinematic) continue;
                if (body.transform.IsChildOf(transform)) continue;

                Vector3 push = (body.worldCenterOfMass - Motor.ColliderCenter);
                push.y = 0f;
                if (push.sqrMagnitude < 1e-4f) push = _direction;
                body.AddForce(push.normalized * physicsForce, ForceMode.Impulse);
            }
        }

        protected override void OnDisabled()
        {
            if (Motor != null)
            {
                Motor.ClearExternalVelocity();
            }
        }
    }
}