using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Overlap helpers that ignore the character's own colliders. Any check that asks "is there room
    /// here?" must skip the body being measured, otherwise the answer is always no.
    /// </summary>
    public static class CharacterCollision
    {
        static readonly Collider[] Buffer = new Collider[24];

        /// <summary>True when nothing except the character occupies the capsule between the two points.</summary>
        public static bool CapsuleIsClear(Vector3 pointA, Vector3 pointB, float radius, LayerMask mask, Transform self,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore)
        {
            if (mask.value == 0) return true;

            int count = Physics.OverlapCapsuleNonAlloc(pointA, pointB, Mathf.Max(0.01f, radius), Buffer, mask, triggerInteraction);

            for (int i = 0; i < count; i++)
            {
                Collider collider = Buffer[i];
                if (collider == null) continue;
                if (self != null && collider.transform.IsChildOf(self)) continue;

                return false;
            }

            return true;
        }

        /// <summary>True when nothing except the character occupies the sphere.</summary>
        public static bool SphereIsClear(Vector3 center, float radius, LayerMask mask, Transform self,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore)
        {
            if (mask.value == 0) return true;

            int count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0.01f, radius), Buffer, mask, triggerInteraction);

            for (int i = 0; i < count; i++)
            {
                Collider collider = Buffer[i];
                if (collider == null) continue;
                if (self != null && collider.transform.IsChildOf(self)) continue;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Finds the first blocking collider along a sphere sweep, ignoring the character's own colliders.
        /// Used by the ceiling check where the sweep may start inside the body.
        /// </summary>
        public static bool SweepHitsBlocker(Vector3 origin, Vector3 direction, float radius, float distance,
            LayerMask mask, Transform self, QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore)
        {
            if (mask.value == 0) return false;

            int count = Physics.SphereCastNonAlloc(origin, Mathf.Max(0.01f, radius), direction, Hits, Mathf.Max(0f, distance), mask, triggerInteraction);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider == null) continue;
                if (self != null && hit.collider.transform.IsChildOf(self)) continue;
                return true;
            }

            return false;
        }

        static readonly RaycastHit[] Hits = new RaycastHit[24];
    }
}