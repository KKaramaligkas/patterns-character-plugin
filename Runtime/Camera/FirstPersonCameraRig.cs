using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Classic first person camera: sits on the character's camera target, takes the rotation straight from
    /// <see cref="CharacterLook"/>, and adds head bob, lean and a field of view kick for speed.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Camera/First Person Rig")]
    public class FirstPersonCameraRig : CharacterCameraRig
    {
        [Header("Head")]
        [Tooltip("Use the character camera target position (moves with crouch and animation).")]
        public bool useCameraTarget = true;

        [Tooltip("Offset from the camera target, in character local space.")]
        public Vector3 eyeOffset = new Vector3(0f, 0.08f, 0.12f);

        [Header("Head Bob")]
        [Tooltip("Vertical bob amplitude in meters.")]
        [Min(0f)] public float bobAmplitude = 0.035f;

        [Tooltip("Lateral bob amplitude in meters.")]
        [Min(0f)] public float bobLateralAmplitude = 0.02f;

        [Tooltip("Bob cycles per meter travelled. Higher is faster.")]
        [Min(0f)] public float bobFrequency = 0.35f;

        [Tooltip("How quickly the bob returns to neutral when standing still.")]
        [Min(0f)] public float bobReturnSharpness = 6f;

        [Tooltip("When true the head only bobs while grounded.")]
        public bool bobOnlyWhenGrounded = true;

        [Header("Feel")]
        [Tooltip("Field of view added at full sprint speed.")]
        public float speedFovKick = 6f;

        [Tooltip("Roll added when strafing sideways, in degrees at full strafe speed.")]
        public float strafeRoll = 1.6f;

        [Tooltip("Extra camera roll while airborne.")]
        public float airborneRoll;

        [Header("Occlusion")]
        [Tooltip("Layers that push the camera forward when something is inside its near plane.")]
        public LayerMask occlusionMask = ~0;

        [Tooltip("Radius used by the occlusion push out.")]
        [Min(0.01f)] public float occlusionRadius = 0.12f;

        public bool preventOcclusion = true;

        [Header("Pivots")]
        [Tooltip("When set the camera rotation is applied to this pivot and the camera only copies it.")]
        public Transform headPivot;

        Vector3 _currentBobOffset;
        float _bobPhase;

        protected override void ComputeTarget(out Vector3 position, out Quaternion rotation)
        {
            CharacterContext context = Context;
            CharacterProfile profile = context != null ? context.Profile : CharacterProfile.Default;

            Vector3 basePosition = useCameraTarget && context != null
                ? context.CameraTargetPosition
                : transform.position;

            float speed = context != null && context.Motor != null ? context.Motor.PlanarSpeed : 0f;
            float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(0.01f, profile.runSpeed));

            // ---- head bob
            bool grounded = context == null || context.Motor == null || context.Motor.IsGrounded;
            bool moving = speed > 0.15f;

            if (moving && (!bobOnlyWhenGrounded || grounded))
            {
                _bobPhase += speed * bobFrequency * Time.deltaTime * Mathf.PI * 2f;
            }

            Vector3 targetBob = Vector3.zero;
            if (moving && (!bobOnlyWhenGrounded || grounded))
            {
                targetBob = new Vector3(
                    Mathf.Sin(_bobPhase) * bobLateralAmplitude,
                    Mathf.Abs(Mathf.Cos(_bobPhase)) * bobAmplitude * 2f - bobAmplitude,
                    0f);
            }

            _currentBobOffset = Vector3.Lerp(_currentBobOffset, targetBob, CharacterMath.SmoothFactor(Mathf.Max(0.01f, bobReturnSharpness), Time.deltaTime));

            // ---- rotation
            Quaternion lookRotation = context != null && context.Look != null
                ? context.Look.CameraRotation
                : transform.rotation;

            float roll = 0f;
            if (context != null && context.Motor != null)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(context.Motor.PlanarVelocity);
                roll = -Mathf.Clamp(localVelocity.x / Mathf.Max(0.01f, profile.runSpeed), -1f, 1f) * strafeRoll;

                if (!grounded) roll += airborneRoll;
            }

            rotation = lookRotation * Quaternion.Euler(roll, 0f, 0f);

            // ---- position
            Vector3 worldOffset = transform.TransformDirection(eyeOffset) + rotation * _currentBobOffset;
            position = basePosition + worldOffset;

            if (preventOcclusion)
            {
                position = CameraObstruction.PushOut(basePosition, position, occlusionRadius, occlusionMask);
            }

            if (headPivot != null)
            {
                headPivot.rotation = rotation;
            }
        }

        protected override float ComputeFov(float baseFov)
        {
            float speedKick = 0f;

            if (Context != null && Context.Motor != null && speedFovKick != 0f)
            {
                CharacterProfile profile = Context.Profile;
                float normalized = Mathf.Clamp01(Context.Motor.PlanarSpeed / Mathf.Max(0.01f, profile.sprintSpeed));
                speedKick = speedFovKick * normalized * normalized;
            }

            return base.ComputeFov(baseFov) + speedKick;
        }
    }
}