using System.Collections.Generic;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Turns input into movement. Every other movement ability modifies locomotion instead of fighting
    /// it: sprinting, crouching, aiming and swimming push tier requests which locomotion resolves by
    /// priority, so the character never receives two conflicting speeds in the same frame.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Locomotion")]
    public class LocomotionAbility : ContinuousAbility
    {
        [Header("Movement Space")]
        [Tooltip("CameraYaw for third person, CharacterYaw for first person, CameraBasis for top-down and isometric.")]
        public MovementSpace movementSpace = MovementSpace.CameraYaw;

        [Tooltip("When false the character cannot accelerate while airborne.")]
        public bool allowAirControl = true;

        [Tooltip("When true the character keeps the speed it had when it left the ground (jump momentum).")]
        public bool preserveAirMomentum = true;

        [Header("Speed")]
        [Tooltip("Tier used when nothing else requests one. Sprint raises it, crouch lowers it.")]
        public LocomotionTier defaultTier = LocomotionTier.Run;

        [Tooltip("Multiplies whatever tier speed is resolved. Aiming and status effects tweak this.")]
        [Min(0f)] public float speedMultiplier = 1f;

        [Tooltip("Input below this value is ignored, useful for virtual sticks.")]
        [Range(0f, 0.9f)] public float inputThreshold = 0.05f;

        [Header("Animation")]
        [Tooltip("Speed that maps to 1.0 on the animation speed parameter.")]
        [Min(0.01f)] public float animationReferenceSpeed = 5f;

        LocomotionTier _tier = LocomotionTier.Idle;
        float _tierSpeedMultiplier = 1f;
        float _airMomentumSpeed;
        Transform _fallbackCamera;

        readonly List<TierRequest> _tierRequests = new List<TierRequest>(6);

        struct TierRequest
        {
            public object Source;
            public int Priority;
            public LocomotionTier Tier;
            public float SpeedMultiplier;
            public float Timestamp;
        }

        /// <summary>World space direction the character wants to move in. Zero when idle.</summary>
        public Vector3 MoveDirection { get; private set; }

        /// <summary>Input direction in world space before acceleration, used by animation and orientation.</summary>
        public Vector3 InputDirection { get; private set; }

        /// <summary>Resolved locomotion tier for this frame.</summary>
        public LocomotionTier Tier { get { return _tier; } }

        /// <summary>Speed the character is aiming for, in units per second.</summary>
        public float TargetSpeed { get; private set; }

        /// <summary>Actual planar speed reported by the motor.</summary>
        public float CurrentSpeed
        {
            get { return Motor != null ? Motor.PlanarSpeed : 0f; }
        }

        /// <summary>0..1 speed mapped for blend trees. 1 equals <see cref="animationReferenceSpeed"/>.</summary>
        public float NormalizedSpeed
        {
            get { return Mathf.Clamp01(CurrentSpeed / Mathf.Max(0.01f, animationReferenceSpeed)); }
        }

        /// <summary>Signed forward speed, useful for strafe blend trees.</summary>
        public float LocalForwardSpeed { get; private set; }

        /// <summary>Signed right speed, useful for strafe blend trees.</summary>
        public float LocalRightSpeed { get; private set; }

        /// <summary>True when there is movement input above the threshold.</summary>
        public bool HasInput { get; private set; }

        public bool IsMoving
        {
            get { return CurrentSpeed > 0.1f; }
        }

        protected override void OnBind()
        {
            if (executionOrder == 0)
            {
                executionOrder = 0; // Locomotion always resolves first.
            }
            motorPriority = 0;
        }

        /// <summary>
        /// Pushes a locomotion tier for as long as the caller keeps calling it (typically every tick).
        /// Requests expire automatically so a disabled ability can never leave the character stuck in a tier.
        /// </summary>
        public void RequestTier(object source, int priority, LocomotionTier tier, float speedMultiplier = 1f)
        {
            float now = Time.time;
            for (int i = 0; i < _tierRequests.Count; i++)
            {
                if (_tierRequests[i].Source == source)
                {
                    TierRequest existing = _tierRequests[i];
                    existing.Priority = priority;
                    existing.Tier = tier;
                    existing.SpeedMultiplier = speedMultiplier;
                    existing.Timestamp = now;
                    _tierRequests[i] = existing;
                    return;
                }
            }

            TierRequest request;
            request.Source = source;
            request.Priority = priority;
            request.Tier = tier;
            request.SpeedMultiplier = speedMultiplier;
            request.Timestamp = now;
            _tierRequests.Add(request);
        }

        /// <summary>Removes the tier request of a caller, for example when flight is switched off.</summary>
        public void ClearTierRequest(object source)
        {
            for (int i = _tierRequests.Count - 1; i >= 0; i--)
            {
                if (_tierRequests[i].Source == source) _tierRequests.RemoveAt(i);
            }
        }

        LocomotionTier ResolveTier(out float multiplier)
        {
            LocomotionTier tier = defaultTier;
            multiplier = 1f;
            int bestPriority = int.MinValue;
            float now = Time.time;

            for (int i = _tierRequests.Count - 1; i >= 0; i--)
            {
                TierRequest request = _tierRequests[i];

                // Expire stale requests so a destroyed ability cannot pin the character in a tier.
                if (now - request.Timestamp > 0.25f)
                {
                    _tierRequests.RemoveAt(i);
                    continue;
                }

                if (request.Priority >= bestPriority)
                {
                    bestPriority = request.Priority;
                    tier = request.Tier;
                    multiplier = request.SpeedMultiplier;
                }
            }

            return tier;
        }

        Transform ResolveCameraTransform()
        {
            if (Context != null && Context.CameraRig != null)
            {
                Transform active = Context.CameraRig.ActiveCameraTransform;
                if (active != null) return active;
            }

            if (_fallbackCamera == null && Camera.main != null) _fallbackCamera = Camera.main.transform;
            return _fallbackCamera;
        }

        protected override void OnTick(float deltaTime)
        {
            if (Motor == null) return;

            CharacterProfile p = Profile;
            float deadZone = Mathf.Max(p.deadZone, inputThreshold);

            Vector2 rawInput = Input != null ? Input.move : Vector2.zero;
            if (rawInput.magnitude < deadZone) rawInput = Vector2.zero;

            bool hasInput;
            Vector3 desiredDirection = CharacterMath.InputToWorld(rawInput, movementSpace, transform, ResolveCameraTransform(), out hasInput);

            HasInput = hasInput;
            InputDirection = desiredDirection;

            float tierMultiplier;
            LocomotionTier resolved = ResolveTier(out tierMultiplier);

            bool wantsToMove = hasInput || Motor.PlanarSpeed > 0.1f;
            _tier = wantsToMove ? resolved : LocomotionTier.Idle;
            _tierSpeedMultiplier = tierMultiplier;

            float tierSpeed = p.SpeedFor(resolved);
            TargetSpeed = tierSpeed * _tierSpeedMultiplier * speedMultiplier;

            // Jump momentum: when leaving the ground the last ground speed is kept as the air target.
            if (!Motor.IsGrounded && preserveAirMomentum)
            {
                if (Motor.AirTime < 0.05f) _airMomentumSpeed = Mathf.Max(_airMomentumSpeed, Motor.PlanarSpeed);
                if (!hasInput && _airMomentumSpeed > 0f)
                {
                    Vector3 momentumDirection = Motor.PlanarVelocity.sqrMagnitude > 0.01f
                        ? Motor.PlanarVelocity.normalized
                        : transform.forward;
                    desiredDirection = momentumDirection;
                    TargetSpeed = Mathf.Max(TargetSpeed, _airMomentumSpeed);
                }
            }
            else
            {
                _airMomentumSpeed = Motor.PlanarSpeed;
            }

            if (!Motor.IsGrounded && !allowAirControl)
            {
                TargetSpeed = Motor.PlanarSpeed;
                desiredDirection = Motor.PlanarVelocity.sqrMagnitude > 0.01f ? Motor.PlanarVelocity.normalized : desiredDirection;
            }

            if (!hasInput && _airMomentumSpeed <= 0f)
            {
                TargetSpeed = 0f;
                desiredDirection = Vector3.zero;
            }

            if (desiredDirection.sqrMagnitude > 1e-5f)
            {
                MoveDirection = desiredDirection.normalized;
            }
            else if (Motor.PlanarSpeed > 0.1f)
            {
                MoveDirection = Motor.PlanarVelocity.normalized;
            }
            else
            {
                MoveDirection = Vector3.zero;
            }

            Vector3 targetVelocity = MoveDirection * TargetSpeed;

            // On slopes the movement follows the surface so the character does not bounce.
            if (Motor.IsGrounded && Motor.Ground.IsGrounded && Motor.Ground.SlopeAngle > 1f && Motor.Ground.SlopeAngle <= Motor.SlopeLimit)
            {
                targetVelocity = Vector3.ProjectOnPlane(targetVelocity, Motor.Ground.Normal);
            }

            SubmitMotion(targetVelocity, true, false, false);

            // Local space speeds for strafe blend trees.
            Vector3 local = transform.InverseTransformDirection(Motor.PlanarVelocity);
            LocalForwardSpeed = local.z;
            LocalRightSpeed = local.x;
        }
    }
}