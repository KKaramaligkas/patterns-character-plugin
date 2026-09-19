using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Physics driven motor for characters that must push rigidbodies, be pushed by them, or take part
    /// in ragdoll / vehicle style interactions. Uses the same request bus as the controller based motor,
    /// so abilities are written once and work with either motor.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [DisallowMultipleComponent]
    public class RigidbodyCharacterMotor : MotorBase
    {
        [Header("Rigidbody Motor")]
        [Tooltip("Interpolation is strongly recommended, the motor drives the body from LateUpdate.")]
        public bool forceInterpolation = true;

        [Tooltip("Rotates the body with MoveRotation so physics contacts stay stable.")]
        public bool physicsRotation;

        [Tooltip("Extra downward force applied when grounded, keeps the body glued on slopes.")]
        [Min(0f)] public float groundStickForce = 5f;

        [Tooltip("Below this speed the character is considered standing still and gets damped.")]
        [Min(0f)] public float stopThreshold = 0.15f;

        Rigidbody _body;
        CapsuleCollider _capsule;
        Vector3 _pendingMotion;
        bool _hasPendingMotion;

        public Rigidbody Body
        {
            get
            {
                if (_body == null) _body = GetComponent<Rigidbody>();
                return _body;
            }
        }

        public CapsuleCollider Capsule
        {
            get
            {
                if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
                return _capsule;
            }
        }

        public override float SlopeLimit
        {
            get { return 55f; }
        }

        public override float Height
        {
            get { return Capsule.height; }
            set
            {
                CapsuleCollider capsule = Capsule;
                float newHeight = Mathf.Max(value, capsule.radius * 2f + 0.01f);
                float delta = newHeight - capsule.height;
                Vector3 center = capsule.center;
                center.y += delta * 0.5f;
                capsule.height = newHeight;
                capsule.center = center;
            }
        }

        public override float Radius
        {
            get { return Capsule.radius; }
        }

        public override Vector3 ColliderCenter
        {
            get { return transform.TransformPoint(Capsule.center); }
        }

        public override Vector3 FootPosition
        {
            get
            {
                CapsuleCollider capsule = Capsule;
                Vector3 local = capsule.center + Vector3.down * (capsule.height * 0.5f - capsule.radius);
                return transform.TransformPoint(local);
            }
        }

        protected override void OnMotorAwake()
        {
            _body = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();

            // Gravity is integrated by the motor so that both motors behave identically.
            _body.useGravity = false;
            _body.isKinematic = false;
            if (forceInterpolation) _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        protected override bool ProbeGround(out GroundInfo info)
        {
            info = GroundInfo.None;

            CharacterProfile p = Profile;
            float probeDistance = Mathf.Max(0.02f, p.groundProbeDistance);
            float radius = Mathf.Max(0.01f, Capsule.radius * 0.95f);

            Vector3 bottom = FootPosition;
            Vector3 origin = bottom + Vector3.up * probeDistance;
            float maxDistance = probeDistance + 0.25f;

            RaycastHit hit;
            if (Physics.SphereCast(origin, radius, Vector3.down, out hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                info = BuildGroundInfo(hit);
                info.Distance = Mathf.Max(0f, hit.distance - probeDistance);
                info.IsGrounded = info.Distance <= 0.15f;
                return info.IsGrounded;
            }

            return false;
        }

        protected override void CommitMotion(Vector3 motion, bool skipGroundSnap)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            Vector3 target = motion / dt;

            if (skipGroundSnap && !IsGrounded)
            {
                // Preserve the current vertical speed when the ability manages it (jump, dash).
                target.y = velocity.y;
            }

            Rigidbody body = Body;
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = target;
#else
            body.velocity = target;
#endif

            if (IsGrounded && groundStickForce > 0f)
            {
#if UNITY_6000_0_OR_NEWER
                body.AddForce(Vector3.down * groundStickForce, ForceMode.Force);
#else
                body.AddForce(Vector3.down * groundStickForce, ForceMode.Force);
#endif
            }

            if (physicsRotation)
            {
                body.MoveRotation(Quaternion.Slerp(body.rotation, transform.rotation, CharacterMath.SmoothFactor(20f, dt)));
            }
            else
            {
                // Keep the body aligned with the visual transform without fighting physics.
                transform.rotation = transform.rotation;
            }
        }

        public override void Teleport(Vector3 position, bool keepVelocity = false)
        {
            Teleport(position, transform.rotation, keepVelocity);
        }

        public override void Teleport(Vector3 position, Quaternion rotation, bool keepVelocity = false)
        {
            Rigidbody body = Body;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);

            if (!keepVelocity)
            {
                ClearMotion();
                ClearRequests();
            }
        }

        public override void Resize(float height, float radius, Vector3 center)
        {
            CapsuleCollider capsule = Capsule;
            capsule.height = Mathf.Max(height, radius * 2f + 0.01f);
            capsule.radius = Mathf.Max(0.05f, radius);
            capsule.center = center;
        }

        public override void SetCollisionEnabled(bool enabled)
        {
            Capsule.enabled = enabled;
            if (!enabled)
            {
                _body.detectCollisions = false;
            }
            else
            {
                _body.detectCollisions = true;
            }
        }

        /// <summary>Called by abilities that want to shove the character without a physics force.</summary>
        public void ApplyForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
        {
            Body.AddForce(force, mode);
        }
    }
}