using UnityEngine;

namespace Patterns.Character
{
    /// <summary>Shared helpers for keeping cameras out of geometry.</summary>
    public static class CameraObstruction
    {
        /// <summary>
        /// Casts from the orbit centre towards the desired camera position and returns the distance the
        /// camera may travel. Returns the full distance when nothing is in the way.
        /// </summary>
        public static float Resolve(Vector3 origin, Vector3 desiredPosition, float radius, LayerMask mask,
            float buffer = 0.15f, Transform ignoreRoot = null, QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore)
        {
            Vector3 delta = desiredPosition - origin;
            float distance = delta.magnitude;
            if (distance < 1e-4f) return 0f;

            Vector3 direction = delta / distance;
            RaycastHit hit;

            if (Physics.SphereCast(origin, Mathf.Max(0.01f, radius), direction, out hit, distance, mask, triggerInteraction))
            {
                if (ignoreRoot != null && hit.collider != null && hit.collider.transform.IsChildOf(ignoreRoot))
                {
                    // Ignore our own body: sweep again from just past the hit.
                    Vector3 restart = origin + direction * (hit.distance + radius * 2f);
                    Vector3 remaining = desiredPosition - restart;
                    RaycastHit second;
                    if (Physics.SphereCast(restart, radius, remaining.normalized, out second, remaining.magnitude, mask, triggerInteraction))
                    {
                        return Mathf.Max(0f, (hit.distance + radius * 2f) + second.distance - buffer);
                    }
                    return distance;
                }

                return Mathf.Max(0f, hit.distance - buffer);
            }

            return distance;
        }

        /// <summary>
        /// Checks whether the camera position is inside geometry after a teleport or a fast move and
        /// pushes it back towards the orbit centre.
        /// </summary>
        public static Vector3 PushOut(Vector3 origin, Vector3 position, float radius, LayerMask mask)
        {
            float allowed = Resolve(origin, position, radius, mask, 0.05f);
            Vector3 delta = position - origin;
            float distance = delta.magnitude;
            if (distance < 1e-4f || allowed >= distance) return position;

            return origin + delta.normalized * allowed;
        }
    }
}