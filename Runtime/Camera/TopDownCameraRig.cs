using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Top-down and isometric camera. Fixed or rotating yaw, follows the character with damping and
    /// look-ahead, supports both perspective and orthographic projections.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Camera/Top Down Rig")]
    public class TopDownCameraRig : CharacterCameraRig
    {
        public enum YawSource
        {
            /// <summary>Yaw follows the look input, the classic twin stick camera.</summary>
            Input = 0,
            /// <summary>Yaw is fixed, an isometric / strategy camera.</summary>
            Fixed = 1,
            /// <summary>Yaw follows the character body.</summary>
            Character = 2
        }

        [Header("Framing")]
        [Tooltip("Camera pitch in degrees. 90 looks straight down.")]
        [Range(10f, 89f)] public float pitch = 55f;

        [Tooltip("Distance from the character to the camera.")]
        [Min(1f)] public float distance = 12f;

        [Min(1f)] public float minDistance = 4f;

        [Min(1f)] public float maxDistance = 30f;

        [Tooltip("Zoom speed in units per scroll notch.")]
        public float zoomSpeed = 2.5f;

        [Min(0f)] public float zoomSharpness = 10f;

        [Header("Rotation")]
        public YawSource yawSource = YawSource.Fixed;

        [Tooltip("Yaw used when the source is Fixed. 45 is a classic isometric angle.")]
        public float fixedYaw = 45f;

        [Tooltip("When true zooming keeps the same framing instead of changing the pitch.")]
        public bool keepPitchWhileZooming = true;

        [Header("Follow")]
        [Tooltip("Offset from the character position, in world space.")]
        public Vector3 targetOffset = new Vector3(0f, 1f, 0f);

        [Tooltip("How quickly the camera catches up with the character.")]
        [Min(0f)] public float followSharpness = 8f;

        [Tooltip("How far ahead of the character the camera leans, per unit of speed.")]
        [Min(0f)] public float lookAheadFactor = 0.25f;

        [Tooltip("Maximum look-ahead distance in meters.")]
        [Min(0f)] public float maxLookAhead = 3f;

        [Header("Projection")]
        [Tooltip("Switch the camera to orthographic while this rig is active.")]
        public bool useOrthographic;

        [Tooltip("Orthographic size, roughly half the visible height in world units.")]
        [Min(0.1f)] public float orthographicSize = 8f;

        [Min(0.1f)] public float minOrthographicSize = 3f;

        [Min(0.1f)] public float maxOrthographicSize = 24f;

        [Tooltip("Zoom changes the orthographic size instead of the distance.")]
        public bool zoomChangesOrthographicSize = true;

        [Tooltip("Optional mouse edge scrolling. Requires a visible cursor.")]
        public bool edgeScrolling;

        [Min(0f)] public float edgeScrollSpeed = 12f;

        [Tooltip("Edge scroll border in pixels.")]
        [Min(1f)] public float edgeScrollBorder = 12f;

        Vector3 _smoothedPosition;
        bool _initialised;
        bool _projectionApplied;
        bool _previousOrthographic;
        float _currentDistance;

        protected override void Awake()
        {
            base.Awake();
            overridePitchLimits = true;
            pitchLimits = new Vector2(Mathf.Max(5f, pitch - 30f), 89f);
            _currentDistance = distance;
        }

        protected override void OnActivate()
        {
            _initialised = false;

            // A top-down camera owns its pitch, so the look input only rotates the yaw.
            overridePitchLimits = true;
            pitchLimits = new Vector2(pitch, pitch);

            if (Context != null && Context.Look != null)
            {
                Context.Look.pitch = pitch;
                Context.Look.Snap();
            }

            Camera cam = Camera;
            if (cam != null && useOrthographic)
            {
                _previousOrthographic = cam.orthographic;
                cam.orthographic = true;
                _projectionApplied = true;
                cam.orthographicSize = orthographicSize;
            }
        }

        protected override void OnDeactivate()
        {
            Camera cam = Camera;
            if (cam != null && _projectionApplied)
            {
                cam.orthographic = _previousOrthographic;
                _projectionApplied = false;
            }
        }

        protected override void ComputeTarget(out Vector3 position, out Quaternion rotation)
        {
            float dt = Time.deltaTime;
            CharacterContext context = Context;

            // ---- zoom
            float zoomInput = 0f;
            if (context != null)
            {
                zoomInput = context.CameraRig != null ? context.CameraRig.ZoomInput : context.Input.zoom;
            }

            if (Mathf.Abs(zoomInput) > 0f)
            {
                if (useOrthographic && zoomChangesOrthographicSize)
                {
                    orthographicSize = Mathf.Clamp(orthographicSize - zoomInput * zoomSpeed, minOrthographicSize, maxOrthographicSize);
                }
                else
                {
                    distance = Mathf.Clamp(distance - zoomInput * zoomSpeed, minDistance, maxDistance);
                }
            }

            _currentDistance = Mathf.Lerp(_currentDistance, distance, CharacterMath.SmoothFactor(Mathf.Max(0.01f, zoomSharpness), dt));

            // ---- yaw
            float yaw;
            switch (yawSource)
            {
                case YawSource.Input:
                    yaw = context != null && context.Look != null ? context.Look.DisplayYaw : fixedYaw;
                    break;
                case YawSource.Character:
                    yaw = transform.eulerAngles.y;
                    break;
                default:
                    yaw = fixedYaw;
                    break;
            }

            rotation = Quaternion.Euler(pitch, yaw, 0f);

            // ---- follow
            Vector3 targetPosition = (context != null ? context.CameraTargetPosition : transform.position) + targetOffset;

            if (context != null && context.Motor != null && lookAheadFactor > 0f)
            {
                Vector3 planarVelocity = context.Motor.PlanarVelocity * lookAheadFactor;
                if (planarVelocity.magnitude > maxLookAhead) planarVelocity = planarVelocity.normalized * maxLookAhead;
                targetPosition += planarVelocity;
            }

            if (edgeScrolling && Application.isPlaying)
            {
                targetPosition += EdgeScrollDelta(dt, yaw);
            }

            if (!_initialised)
            {
                _smoothedPosition = targetPosition;
                _initialised = true;
            }
            else
            {
                _smoothedPosition = Vector3.Lerp(_smoothedPosition, targetPosition, CharacterMath.SmoothFactor(Mathf.Max(0.01f, followSharpness), dt));
            }

            position = _smoothedPosition - rotation * Vector3.forward * _currentDistance;

            Camera cam = Camera;
            if (cam != null && useOrthographic)
            {
                cam.orthographicSize = orthographicSize;
            }
        }

        Vector3 EdgeScrollDelta(float dt, float yaw)
        {
            Vector3 mouse = InputProxy.MousePosition();
            Vector2 delta = Vector2.zero;

            if (mouse.x >= 0f)
            {
                if (mouse.x <= edgeScrollBorder) delta.x = -1f;
                else if (mouse.x >= Screen.width - edgeScrollBorder) delta.x = 1f;

                if (mouse.y <= edgeScrollBorder) delta.y = -1f;
                else if (mouse.y >= Screen.height - edgeScrollBorder) delta.y = 1f;
            }

            if (delta.sqrMagnitude < 0.01f) return Vector3.zero;

            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

            return (right * delta.x + forward * delta.y).normalized * edgeScrollSpeed * dt;
        }

        /// <summary>Sets the yaw of a fixed camera at runtime.</summary>
        public void SetFixedYaw(float newYaw)
        {
            fixedYaw = Mathf.Repeat(newYaw, 360f);
        }

        /// <summary>Rotates a fixed / isometric camera in 90 degree steps, as strategy games do.</summary>
        public void Rotate90(float direction = 1f)
        {
            SetFixedYaw(fixedYaw + 90f * Mathf.Sign(direction));
        }
    }

    /// <summary>Small wrapper so camera code can read the mouse position with or without the new Input System.</summary>
    public static class InputProxy
    {
        /// <summary>Mouse position in pixels, or (-1,-1) when it is not available.</summary>
        public static Vector2 MousePosition()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return new Vector2(-1f, -1f);
#endif
        }
    }
}