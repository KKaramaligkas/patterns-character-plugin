using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Trigger volume a character can enter: water, ladders, wind, lava, custom zones.
    /// Attach to any trigger collider. Abilities such as <see cref="SwimAbility"/> and
    /// <see cref="LadderAbility"/> listen for these.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Patterns/Character/Character Volume")]
    public class CharacterVolume : MonoBehaviour
    {
        public VolumeType type = VolumeType.Water;

        [Tooltip("Direction of the flow or current inside the volume.")]
        public Vector3 flowDirection = Vector3.zero;

        [Tooltip("Strength of the flow in units per second.")]
        [Min(0f)] public float flowStrength;

        [Tooltip("Buoyancy multiplier, 2 makes the character float up quickly.")]
        [Min(0f)] public float buoyancyMultiplier = 1f;

        [Tooltip("Extra drag applied to characters inside, 0 keeps full momentum.")]
        [Range(0f, 1f)] public float drag;

        [Tooltip("Damage per second applied while inside the volume. 0 = harmless.")]
        [Min(0f)] public float damagePerSecond;

        [Tooltip("Surface height in world space, used by swim to know where the water line is. 0 uses the top of the collider.")]
        public float surfaceHeight;

        Collider _collider;

        public Collider Collider
        {
            get
            {
                if (_collider == null) _collider = GetComponent<Collider>();
                return _collider;
            }
        }

        public float SurfaceHeight
        {
            get
            {
                if (surfaceHeight > 0f) return surfaceHeight;
                Collider col = Collider;
                return col != null ? col.bounds.max.y : transform.position.y;
            }
        }

        public Vector3 Flow
        {
            get { return flowStrength > 0f ? flowDirection.normalized * flowStrength : Vector3.zero; }
        }

        void Reset()
        {
            Collider col = Collider;
            if (col != null) col.isTrigger = true;
        }

        void OnValidate()
        {
            Collider col = Collider;
            if (col != null && !col.isTrigger)
            {
                // Volumes must not block the character.
                col.isTrigger = true;
            }
        }

        void OnDrawGizmos()
        {
            if (flowStrength <= 0f) return;
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.85f);
            Gizmos.DrawRay(transform.position, Flow);
        }
    }
}