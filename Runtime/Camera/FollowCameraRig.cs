using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// 2.5D / side-scroller follow camera. The camera keeps a fixed offset and can be locked to a plane,
    /// so it only follows the axes you care about, with an optional dead zone and look-ahead.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Camera/Follow Rig (2.5D)")]
    public class FollowCameraRig : CharacterCameraRig
    {
        [Header("Framing")]
        [Tooltip("Offset from the character position, in world space.")]
        public Vector3 offset = new Vector3(0f, 2.2f, -8f);

        [Tooltip("Point the camera looks at, relative to the character.")]
        public Vector3 lookAtOffset = new Vector3(0f, 1.2f, 0f);

        [Header("Follow")]
        [Tooltip("Axes the camera follows. Turn off X for a side scroller, or Z for a platform runner.")]
        public Vector3 followAxisMask = new Vector3(1f, 1f, 0f);

        [Tooltip("Distance the character can move before the camera starts following.")]
        [Min(0f)] public float deadZone = 0.3f;

        [Tooltip("How quickly the camera catches up once the character leaves the dead zone.")]
        [Min(0f)] public float followSharpness = 8f;

        [Tooltip("Camera leans ahead of the character, per unit of speed.")]
        [Min(0f)] public float lookAheadFactor = 0.35f;

        [Min(0f)] public float maxLookAhead = 2f;

        [Tooltip("When true the camera keeps its world Y, useful for pure side scrollers.")]
        public bool lockHeight;

        [Header("Rotation")]
        [Tooltip("Look at the target instead of using a fixed rotation.")]
        public bool lookAtTarget = true;

        [Tooltip("Rotation used when look at target is off.")]
        public Vector3 fixedEulerAngles = new Vector3(12f, 0f, 0f);

        [Tooltip("Rotate to follow the character yaw (over the shoulder 2.5D).")]
        public bool followCharacterYaw;

        Vector3 _smoothedPosition;
        bool _initialised;

        protected override void Awake()
        {
            base.Awake();

            // This rig does its own dead zone damping, so the base smoothing is switched off.
            positionSmoothTime = 0f;
        }

        protected override void OnActivate()
        {
            _initialised = false;

            if (lookAtTarget)
            {
                // The follow rig owns its own rotation, so the look input is left alone.
                overridePitchLimits = true;
                pitchLimits = new Vector2(fixedEulerAngles.x, fixedEulerAngles.x);
            }
        }

        protected override void ComputeTarget(out Vector3 position, out Quaternion rotation)
        {
            float dt = Time.deltaTime;
            CharacterContext context = Context;

            Vector3 characterPosition = context != null ? context.CameraTargetPosition : transform.position;

            if (context != null && context.Motor != null && lookAheadFactor > 0f)
            {
                Vector3 velocity = context.Motor.PlanarVelocity * lookAheadFactor;
                if (velocity.magnitude > maxLookAhead) velocity = velocity.normalized * maxLookAhead;
                characterPosition += Vector3.Scale(velocity, followAxisMask);
            }

            Vector3 desired = characterPosition + offset;

            if (lockHeight && _initialised) desired.y = _smoothedPosition.y;

            if (!_initialised)
            {
                _smoothedPosition = desired;
                _initialised = true;
            }
            else
            {
                // Dead zone: the character moves inside a bubble before the camera reacts.
                Vector3 delta = Vector3.Scale(desired - _smoothedPosition, followAxisMask);
                float distance = delta.magnitude;

                if (distance > deadZone)
                {
                    Vector3 target = _smoothedPosition + delta.normalized * (distance - deadZone);
                    _smoothedPosition = Vector3.Lerp(_smoothedPosition, target, CharacterMath.SmoothFactor(Mathf.Max(0.01f, followSharpness), dt));
                }
            }

            position = _smoothedPosition;

            if (followCharacterYaw && context != null)
            {
                rotation = Quaternion.Euler(fixedEulerAngles.x, transform.eulerAngles.y + fixedEulerAngles.y, fixedEulerAngles.z);
            }
            else if (lookAtTarget)
            {
                Vector3 lookTarget = (context != null ? context.CameraTargetPosition : transform.position) + lookAtOffset;
                Vector3 direction = lookTarget - position;
                rotation = direction.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(direction, Vector3.up)
                    : Quaternion.Euler(fixedEulerAngles);
            }
            else
            {
                rotation = Quaternion.Euler(fixedEulerAngles);
            }
        }

        /// <summary>Snaps the camera behind the character without damping.</summary>
        public void Recenter()
        {
            _initialised = false;
        }
    }
}