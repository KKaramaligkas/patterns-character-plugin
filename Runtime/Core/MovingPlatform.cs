using UnityEngine;

namespace Patterns.Character
{
    /// <summary>Moving platform a character can ride. Attach to any moving collider.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Platform velocity is inherited by characters standing on it.")]
        public bool inheritVelocity = true;

        [Tooltip("Rotation is only inherited when true (still experimental on kinematic rigidbodies).")]
        public bool inheritRotation;

        [Tooltip("Extra speed multiplier applied to the inherited motion, useful for conveyors.")]
        [Min(0f)] public float speedMultiplier = 1f;

        Rigidbody _body;
        Vector3 _lastPosition;
        Quaternion _lastRotation;
        bool _hasLast;

        public Vector3 DeltaPosition { get; private set; }
        public Quaternion DeltaRotation { get; private set; } = Quaternion.identity;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            Capture();
        }

        void OnEnable()
        {
            Capture();
        }

        void FixedUpdate()
        {
            if (_body != null) Capture();
        }

        void LateUpdate()
        {
            if (_body == null) Capture();
        }

        void Capture()
        {
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;

            if (_hasLast)
            {
                DeltaPosition = position - _lastPosition;
                DeltaRotation = rotation * Quaternion.Inverse(_lastRotation);
                if (speedMultiplier != 1f) DeltaPosition *= speedMultiplier;
            }
            else
            {
                DeltaPosition = Vector3.zero;
                DeltaRotation = Quaternion.identity;
            }

            _lastPosition = position;
            _lastRotation = rotation;
            _hasLast = true;
        }
    }
}