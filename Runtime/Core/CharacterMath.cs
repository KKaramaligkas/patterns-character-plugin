using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Helper maths shared by motors, abilities and camera rigs. Frame-rate independent and allocation free.
    /// </summary>
    public static class CharacterMath
    {
        public static readonly Vector3 FlatForward = new Vector3(0f, 0f, 1f);

        /// <summary>Moves <paramref name="current"/> towards <paramref name="target"/> at a maximum rate.</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            float delta = target - current;
            if (Mathf.Abs(delta) <= maxDelta) return target;
            return current + Mathf.Sign(delta) * maxDelta;
        }

        /// <summary>Applies acceleration towards a target speed and deceleration when the target is slower or zero.</summary>
        public static float ApproachSpeed(float currentSpeed, float targetSpeed, float acceleration, float deceleration, float dt)
        {
            float rate = Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed) ? acceleration : deceleration;
            return MoveTowards(currentSpeed, targetSpeed, Mathf.Max(0f, rate) * dt);
        }

        /// <summary>Moves a velocity vector towards a target velocity with separate accel / decel rates.</summary>
        public static Vector3 ApproachVelocity(Vector3 current, Vector3 target, float acceleration, float deceleration, float dt)
        {
            float rate = (target - current).sqrMagnitude > current.sqrMagnitude
                ? acceleration
                : deceleration;

            Vector3 delta = target - current;
            float maxDelta = Mathf.Max(0f, rate) * dt;
            float magnitude = delta.magnitude;
            if (magnitude <= maxDelta || magnitude <= 1e-5f) return target;
            return current + delta / magnitude * maxDelta;
        }

        /// <summary>Framerate independent exponential smoothing factor for Lerp / Slerp (1 - e^(-k*dt)).</summary>
        public static float SmoothFactor(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * dt);
        }

        /// <summary>Framerate independent follow for a transform, using an exponential curve.</summary>
        public static Vector3 SmoothDampVector(Vector3 current, Vector3 target, float sharpness, float dt)
        {
            return Vector3.Lerp(current, target, SmoothFactor(sharpness, dt));
        }

        /// <summary>Dampled rotation that keeps the up axis with the world.</summary>
        public static Quaternion SmoothLookRotation(Transform transform, Vector3 forward, Vector3 up, float sharpness, float dt)
        {
            forward = Vector3.ProjectOnPlane(forward, up);
            if (forward.sqrMagnitude < 1e-6f) return transform.rotation;
            Quaternion target = Quaternion.LookRotation(forward.normalized, up);
            return Quaternion.Slerp(transform.rotation, target, SmoothFactor(sharpness, dt));
        }

        /// <summary>Clamps a pitch angle to the -90 / 90 range.</summary>
        public static float ClampPitch(float pitch)
        {
            return Mathf.Clamp(pitch > 180f ? pitch - 360f : pitch, -89.9f, 89.9f);
        }

        /// <summary>Converts a movement space and a 2D input into a world space direction.</summary>
        public static Vector3 InputToWorld(Vector2 input, MovementSpace space, Transform character, Transform camera, out bool hasInput)
        {
            hasInput = input.sqrMagnitude > 1e-4f;
            if (!hasInput) return Vector3.zero;

            // The plane movement happens on. Characters on slopes follow their own up vector.
            Vector3 planeNormal = character != null && Vector3.Dot(character.up, Vector3.up) > 0.35f
                ? character.up.normalized
                : Vector3.up;

            Vector3 direction;
            if (space == MovementSpace.WorldXY)
            {
                direction = new Vector3(input.x, input.y, 0f);
            }
            else
            {
                Vector3 forward;
                if (space == MovementSpace.CharacterYaw && character != null)
                {
                    forward = Vector3.ProjectOnPlane(character.forward, planeNormal);
                }
                else if (space == MovementSpace.World || camera == null)
                {
                    forward = Vector3.ProjectOnPlane(Vector3.forward, planeNormal);
                }
                else
                {
                    // CameraYaw and CameraBasis both resolve through the camera, CameraBasis additionally
                    // aligns the plane with the character up so near vertical cameras stay stable.
                    forward = Vector3.ProjectOnPlane(camera.forward,
                        space == MovementSpace.CameraBasis ? planeNormal : Vector3.up);
                    if (forward.sqrMagnitude < 1e-5f) forward = Vector3.ProjectOnPlane(camera.up, planeNormal);
                }

                if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(Vector3.forward, planeNormal);
                if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
                forward.Normalize();

                Vector3 right = Vector3.Cross(planeNormal, forward);
                if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
                right.Normalize();

                direction = right * input.x + forward * input.y;
            }

            float magnitude = direction.magnitude;
            if (magnitude > 1f) direction /= magnitude;
            return direction;
        }

        /// <summary>Projects a direction onto the plane described by a normal, keeping a sane magnitude.</summary>
        public static Vector3 ProjectOnPlaneSafe(Vector3 direction, Vector3 normal)
        {
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
            Vector3 projected = Vector3.ProjectOnPlane(direction, normal.normalized);
            return projected;
        }

        /// <summary>Angular difference in degrees between two directions.</summary>
        public static float Angle(Vector3 a, Vector3 b)
        {
            if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f) return 0f;
            return Vector3.Angle(a.normalized, b.normalized);
        }

        /// <summary>Returns true when a value (0..1) crosses the supplied threshold this frame.</summary>
        public static bool Stepped(ref float previous, float current, float threshold)
        {
            bool crossed = previous < threshold && current >= threshold;
            previous = current;
            return crossed;
        }
    }
}