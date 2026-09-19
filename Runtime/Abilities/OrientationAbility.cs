using System.Collections.Generic;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Rotates the character body. First person uses FaceCamera, third person uses FaceMovement,
    /// top-down usually uses FaceCamera or FaceInput, tank controls use Manual.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Orientation")]
    public class OrientationAbility : ContinuousAbility
    {
        [Header("Mode")]
        public OrientationMode mode = OrientationMode.FaceMovement;

        [Tooltip("When true the character is only turned while there is movement input.")]
        public bool rotateOnlyWhenMoving = true;

        [Tooltip("Yaw correction applied on top, for models that do not face +Z.")]
        public float yawOffset;

        [Header("Speed")]
        [Tooltip("When true the rotation speed comes from the CharacterProfile.")]
        public bool useProfileRotationSpeed = true;

        [Tooltip("Degrees per second. Only used when the profile value is not used.")]
        [Min(0f)] public float rotationSpeed = 900f;

        [Header("Slopes")]
        [Tooltip("Tilts the body to match the ground normal on walkable slopes.")]
        public bool alignToSlopes;

        [Tooltip("Slopes above this angle are ignored by the alignment.")]
        [Range(0f, 89f)] public float maxSlopeAngle = 30f;

        [Min(0f)] public float slopeAlignSharpness = 10f;

        LocomotionAbility _locomotion;

        struct ModeOverride
        {
            public object Source;
            public int Priority;
            public OrientationMode Mode;
            public float Timestamp;
        }

        readonly List<ModeOverride> _overrides = new List<ModeOverride>(3);

        /// <summary>Yaw currently applied to the body, in degrees.</summary>
        public float Yaw { get; private set; }

        /// <summary>True while the body is still turning towards its target.</summary>
        public bool IsRotating { get; private set; }

        /// <summary>Direction the body is turning towards. Zero when it is settled.</summary>
        public Vector3 TargetDirection { get; private set; }

        protected override void OnBind()
        {
            _locomotion = Context != null ? Context.GetAbility<LocomotionAbility>() : null;
            Yaw = transform.eulerAngles.y;
        }

        /// <summary>Lets another ability (aiming, ladders, climbing) take over the body rotation for a while.</summary>
        public void SetModeOverride(object source, int priority, OrientationMode overrideMode)
        {
            float now = Time.time;
            for (int i = 0; i < _overrides.Count; i++)
            {
                if (_overrides[i].Source == source)
                {
                    ModeOverride existing = _overrides[i];
                    existing.Priority = priority;
                    existing.Mode = overrideMode;
                    existing.Timestamp = now;
                    _overrides[i] = existing;
                    return;
                }
            }

            ModeOverride entry;
            entry.Source = source;
            entry.Priority = priority;
            entry.Mode = overrideMode;
            entry.Timestamp = now;
            _overrides.Add(entry);
        }

        public void ClearModeOverride(object source)
        {
            for (int i = _overrides.Count - 1; i >= 0; i--)
            {
                if (_overrides[i].Source == source) _overrides.RemoveAt(i);
            }
        }

        OrientationMode ResolveMode()
        {
            OrientationMode resolved = mode;
            int bestPriority = int.MinValue;
            float now = Time.time;

            for (int i = _overrides.Count - 1; i >= 0; i--)
            {
                ModeOverride entry = _overrides[i];
                if (now - entry.Timestamp > 0.25f)
                {
                    _overrides.RemoveAt(i);
                    continue;
                }

                if (entry.Priority >= bestPriority)
                {
                    bestPriority = entry.Priority;
                    resolved = entry.Mode;
                }
            }

            return resolved;
        }

        protected override void OnTick(float deltaTime)
        {
            if (Motor == null) return;

            OrientationMode resolvedMode = ResolveMode();
            if (resolvedMode == OrientationMode.Manual) return;

            CharacterProfile p = Profile;
            float speed = useProfileRotationSpeed ? p.rotationSpeed : rotationSpeed;

            Quaternion target = transform.rotation;
            Vector3 direction = Vector3.zero;
            bool hasTarget = false;

            if (resolvedMode == OrientationMode.FaceCamera)
            {
                float yaw = Look != null ? Look.DisplayYaw : transform.eulerAngles.y;
                target = Quaternion.Euler(0f, yaw + yawOffset, 0f);
                direction = target * Vector3.forward;
                hasTarget = true;
            }
            else
            {
                if (resolvedMode == OrientationMode.FaceInput && _locomotion != null)
                {
                    direction = _locomotion.InputDirection;
                }
                else
                {
                    Vector3 planarVelocity = Motor.PlanarVelocity;
                    direction = planarVelocity.sqrMagnitude > 0.01f
                        ? planarVelocity
                        : (_locomotion != null ? _locomotion.MoveDirection : Vector3.zero);
                }

                // The direction is flattened by InputToWorld already, but keep this safe for custom sources.
                direction.y = 0f;

                if (direction.sqrMagnitude > 1e-4f)
                {
                    direction.Normalize();
                    target = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
                    hasTarget = true;
                }
            }

            TargetDirection = hasTarget ? direction : Vector3.zero;

            if (!hasTarget) return;

            bool shouldRotate = !rotateOnlyWhenMoving
                || Motor.PlanarSpeed > 0.1f
                || (_locomotion != null && _locomotion.HasInput)
                || resolvedMode == OrientationMode.FaceCamera;

            if (!shouldRotate) return;

            Quaternion current = transform.rotation;
            Quaternion next;

            if (resolvedMode == OrientationMode.FaceCamera || !p.snappyRotation)
            {
                // Exponential smoothing: snappy at the start, gentle at the end.
                float sharpness = Mathf.Max(1f, speed / 45f);
                next = Quaternion.Slerp(current, target, CharacterMath.SmoothFactor(sharpness, deltaTime));
            }
            else
            {
                next = Quaternion.RotateTowards(current, target, speed * deltaTime);
            }

            if (alignToSlopes && Motor.IsGrounded && Motor.Ground.IsGrounded && Motor.Ground.SlopeAngle <= maxSlopeAngle)
            {
                Quaternion slopeAlignment = Quaternion.FromToRotation(Vector3.up, Motor.Ground.Normal);
                next = Quaternion.Slerp(next, slopeAlignment * next, CharacterMath.SmoothFactor(slopeAlignSharpness, deltaTime));
            }

            IsRotating = Quaternion.Angle(current, next) > 0.05f;
            transform.rotation = next;
            Yaw = next.eulerAngles.y;
        }
    }
}