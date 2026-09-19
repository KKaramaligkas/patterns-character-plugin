using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Orbiting third person camera with zoom, shoulder offset for aiming and obstruction handling.
    /// This is the rig to use for action, adventure and shooter characters.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Camera/Third Person Rig")]
    public class ThirdPersonCameraRig : CharacterCameraRig
    {
        [Header("Orbit")]
        [Tooltip("Distance between the orbit centre and the camera.")]
        [Min(0.2f)] public float distance = 4.5f;

        [Min(0.2f)] public float minDistance = 1.2f;

        [Min(0.2f)] public float maxDistance = 10f;

        [Tooltip("Mouse wheel zoom speed in units per scroll notch. Negative uses the switcher value.")]
        public float zoomSpeed = -1f;

        [Tooltip("How quickly the zoom is applied.")]
        [Min(0f)] public float zoomSharpness = 10f;

        [Header("Target")]
        [Tooltip("Offset from the character camera target, in world space. (0, 1.4, 0) is a good start.")]
        public Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);

        [Tooltip("How quickly the orbit centre follows the character. Higher is tighter.")]
        [Min(0f)] public float followSmoothTime = 0.12f;

        [Tooltip("Maximum speed the orbit centre can travel, useful to avoid whipping after a teleport.")]
        [Min(0f)] public float maxFollowSpeed = 0f;

        [Header("Framing")]
        [Tooltip("Sideways offset used to place the character off centre.")]
        public Vector3 shoulderOffset = new Vector3(0.4f, 0f, 0f);

        [Tooltip("Only apply the shoulder offset while aiming.")]
        public bool shoulderOnlyWhenAiming;

        [Tooltip("Pitch added permanently, useful to look slightly down on the character.")]
        public float pitchOffset;

        [Header("Obstruction")]
        [Tooltip("Layers the camera collides with.")]
        public LayerMask collisionMask = ~0;

        [Min(0.02f)] public float collisionRadius = 0.28f;

        [Min(0f)] public float collisionBuffer = 0.15f;

        [Tooltip("Pull the camera in when geometry blocks the view.")]
        public bool preventClipping = true;

        [Tooltip("Camera never gets closer than this, even when the view is blocked.")]
        [Min(0f)] public float minimumObstructedDistance = 0.6f;

        [Tooltip("Distance the obstruction correction moves per second, avoids popping.")]
        [Min(0f)] public float obstructionSharpness = 18f;

        float _currentDistance;
        float _obstructedDistance = -1f;
        Vector3 _pivot;
        Vector3 _pivotVelocity;
        bool _pivotInitialised;

        protected override void Awake()
        {
            base.Awake();
            _currentDistance = distance;
        }

        protected override void OnActivate()
        {
            _pivotInitialised = false;
            _obstructedDistance = -1f;
        }

        protected override void ComputeTarget(out Vector3 position, out Quaternion rotation)
        {
            float dt = Time.deltaTime;
            CharacterContext context = Context;

            Vector3 pivotTarget = (context != null ? context.CameraTargetPosition : transform.position) + targetOffset;

            if (!_pivotInitialised)
            {
                _pivot = pivotTarget;
                _pivotVelocity = Vector3.zero;
                _pivotInitialised = true;
            }
            else if (followSmoothTime <= 0f)
            {
                _pivot = pivotTarget;
            }
            else
            {
                _pivot = Vector3.SmoothDamp(_pivot, pivotTarget, ref _pivotVelocity, followSmoothTime, Mathf.Infinity, dt);

                if (maxFollowSpeed > 0f)
                {
                    Vector3 delta = _pivot - pivotTarget;
                    float maxStep = maxFollowSpeed * dt;
                    if (delta.magnitude > maxStep * 8f)
                    {
                        _pivot = pivotTarget;
                        _pivotVelocity = Vector3.zero;
                    }
                }
            }

            // ---- zoom
            float zoomInput = 0f;
            if (context != null)
            {
                zoomInput = context.CameraRig != null ? context.CameraRig.ZoomInput : context.Input.zoom;
            }

            if (Mathf.Abs(zoomInput) > 0f)
            {
                float speed = zoomSpeed >= 0f ? zoomSpeed : 1f;
                distance = Mathf.Clamp(distance - zoomInput * speed, minDistance, maxDistance);
            }

            _currentDistance = Mathf.Lerp(_currentDistance, distance, CharacterMath.SmoothFactor(Mathf.Max(0.01f, zoomSharpness), dt));

            float effectiveDistance = _currentDistance * Mathf.Lerp(1f, aimDistanceScale, aimRatio);
            effectiveDistance = Mathf.Clamp(effectiveDistance, 0.2f, maxDistance * 1.5f);

            // ---- rotation
            Quaternion lookRotation = context != null && context.Look != null
                ? context.Look.CameraRotation
                : transform.rotation;

            rotation = lookRotation * Quaternion.Euler(pitchOffset, 0f, 0f);

            // ---- framing offset
            Vector3 shoulder = rotation * shoulderOffset;
            if (shoulderOnlyWhenAiming) shoulder *= aimRatio;
            shoulder = Vector3.Lerp(shoulder, rotation * aimShoulderOffset, aimRatio);

            Vector3 orbitCentre = _pivot + shoulder;
            Vector3 direction = rotation * Vector3.back;
            Vector3 desired = orbitCentre + direction * effectiveDistance;

            // ---- obstruction
            if (preventClipping && collisionMask.value != 0)
            {
                Transform ignoreRoot = context != null ? context.transform : null;
                float allowed = CameraObstruction.Resolve(orbitCentre, desired, collisionRadius, collisionMask, collisionBuffer, ignoreRoot);
                allowed = Mathf.Max(minimumObstructedDistance, allowed);

                if (_obstructedDistance < 0f)
                {
                    _obstructedDistance = allowed;
                }
                else
                {
                    // Snap in immediately so geometry never clips, ease back out.
                    float sharpness = allowed < _obstructedDistance ? 60f : Mathf.Max(0.01f, obstructionSharpness);
                    _obstructedDistance = Mathf.Lerp(_obstructedDistance, allowed, CharacterMath.SmoothFactor(sharpness, dt));
                }

                allowed = Mathf.Min(_obstructedDistance, effectiveDistance);
                position = orbitCentre + direction * allowed;
            }
            else
            {
                position = desired;
            }
        }

        /// <summary>Snaps the zoom back to the default distance.</summary>
        public void ResetZoom()
        {
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            _currentDistance = distance;
        }
    }
}