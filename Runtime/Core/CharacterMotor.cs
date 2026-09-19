using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// CharacterController based motor. Collision, slopes and steps are handled by the controller,
    /// this class adds gravity, ground probing, ground snapping, platform inheritance and impulses.
    /// This is the motor to use for first person, third person, top-down and 2.5D characters.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class CharacterMotor : MotorBase
    {
        [Header("Character Controller")]
        [Tooltip("When true the controller settings are kept in sync with the profile.")]
        public bool syncControllerSettings = true;

        [Tooltip("Extra sink tolerance used to keep the character glued to slopes and steps.")]
        [Min(0f)] public float snapTolerance = 0.05f;

        [Tooltip("Layers the ceiling check uses when standing up from a crouch.")]
        public LayerMask obstructionMask = ~0;

        CharacterController _controller;
        float _originalSkinWidth;

        public CharacterController Controller
        {
            get
            {
                if (_controller == null) _controller = GetComponent<CharacterController>();
                return _controller;
            }
        }

        public override float SlopeLimit
        {
            get { return Controller.slopeLimit; }
        }

        public override float Height
        {
            get { return Controller.height; }
            set
            {
                CharacterController controller = Controller;
                float previous = controller.height;
                float newHeight = Mathf.Max(value, controller.radius * 2f + 0.01f);
                if (Mathf.Approximately(previous, newHeight)) return;

                Vector3 center = controller.center;
                float delta = newHeight - previous;
                center.y += delta * 0.5f;
                controller.height = newHeight;
                controller.center = center;
            }
        }

        public override float Radius
        {
            get { return Controller.radius; }
        }

        public override Vector3 ColliderCenter
        {
            get { return transform.TransformPoint(Controller.center); }
        }

        public override Vector3 FootPosition
        {
            get
            {
                CharacterController controller = Controller;
                Vector3 local = controller.center + Vector3.down * (controller.height * 0.5f - controller.radius);
                return transform.TransformPoint(local);
            }
        }

        protected override void OnMotorAwake()
        {
            _controller = GetComponent<CharacterController>();
            _originalSkinWidth = _controller.skinWidth;
            ApplyControllerSettings();
        }

        void OnValidate()
        {
            if (_controller != null && syncControllerSettings) ApplyControllerSettings();
        }

        void ApplyControllerSettings()
        {
            if (_controller == null) return;
            CharacterProfile p = Profile;
            _controller.minMoveDistance = 0f;
            if (_controller.skinWidth <= 0f) _controller.skinWidth = 0.02f;
            if (_controller.stepOffset > Height * 0.5f) _controller.stepOffset = Height * 0.5f;
            if (_controller.slopeLimit <= 0f) _controller.slopeLimit = 45f;
            _controller.enableOverlapRecovery = true;
            if (p != null && syncControllerSettings)
            {
                // Keep the skin width tight, it is the main source of "sliding on slopes" artefacts.
                _controller.skinWidth = Mathf.Min(_originalSkinWidth, 0.03f);
            }
        }

        protected override bool ProbeGround(out GroundInfo info)
        {
            info = GroundInfo.None;
            CharacterController controller = Controller;
            if (!controller.enabled) return false;

            CharacterProfile p = Profile;
            float probeDistance = Mathf.Max(0.02f, p.groundProbeDistance);
            float radius = Mathf.Max(0.01f, controller.radius - 0.02f);

            // Start the probe above the bottom of the capsule so the sphere never begins inside the floor.
            Vector3 bottom = transform.TransformPoint(controller.center + Vector3.down * (controller.height * 0.5f - controller.radius));
            Vector3 origin = bottom + Vector3.up * probeDistance;
            float maxDistance = probeDistance + snapTolerance + controller.skinWidth;

            RaycastHit hit;
            if (Physics.SphereCast(origin, radius, Vector3.down, out hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                info = BuildGroundInfo(hit);
                info.Distance = Mathf.Max(0f, hit.distance - probeDistance);
                info.IsGrounded = controller.isGrounded || info.Distance <= snapTolerance + 0.01f;
                return info.IsGrounded;
            }

            if (controller.isGrounded)
            {
                // The controller reports contact but the probe missed it (edge case on seams and steps).
                info = GroundInfo.None;
                info.IsGrounded = true;
                info.Normal = Vector3.up;
                info.Point = bottom - Vector3.up * controller.skinWidth;
                info.Distance = 0f;
                info.SlopeAngle = 0f;
                return true;
            }

            return false;
        }

        protected override void CommitMotion(Vector3 motion, bool skipGroundSnap)
        {
            CharacterController controller = Controller;
            if (!controller.enabled)
            {
                transform.position += motion;
                return;
            }

            CharacterProfile p = Profile;
            bool wasGrounded = IsGrounded;

            CollisionFlags flags = controller.Move(motion);

            if ((flags & CollisionFlags.Above) != 0 && motion.y > 0f && velocity.y > 0f)
            {
                velocity.y = 0f;
                RaiseHitCeiling(null);
            }

            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Kill the velocity component that the wall absorbed so we do not keep pushing.
                Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
                if (planar.sqrMagnitude > 1e-5f)
                {
                    Vector3 actual = controller.velocity;
                    Vector3 actualPlanar = new Vector3(actual.x, 0f, actual.z);
                    if (actualPlanar.sqrMagnitude < planar.sqrMagnitude * 0.5f)
                    {
                        velocity.x = actualPlanar.x;
                        velocity.z = actualPlanar.z;
                    }
                }
            }

            // Ground snapping: glues the character to the floor when walking down slopes and small steps.
            if (!skipGroundSnap && wasGrounded && motion.y <= 0.001f && p.groundSnapDistance > 0f)
            {
                GroundInfo probe;
                if (!ProbeGround(out probe))
                {
                    float snap = p.groundSnapDistance + snapTolerance;
                    controller.Move(Vector3.down * snap);
                }
            }
        }

        public override void Teleport(Vector3 position, bool keepVelocity = false)
        {
            Teleport(position, transform.rotation, keepVelocity);
        }

        public override void Teleport(Vector3 position, Quaternion rotation, bool keepVelocity = false)
        {
            CharacterController controller = Controller;
            bool wasEnabled = controller.enabled;
            controller.enabled = false;

            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();

            controller.enabled = wasEnabled;
            if (wasEnabled) controller.Move(Vector3.zero);

            if (!keepVelocity)
            {
                ClearMotion();
                ClearRequests();
            }
        }

        public override void Resize(float height, float radius, Vector3 center)
        {
            CharacterController controller = Controller;
            controller.height = Mathf.Max(height, radius * 2f + 0.01f);
            controller.radius = Mathf.Max(0.05f, radius);
            controller.center = center;
        }

        public override bool CanFit(float height)
        {
            CharacterController controller = Controller;
            float target = Mathf.Max(height, controller.radius * 2f + 0.01f);
            float growth = target - controller.height;
            if (growth <= 0.001f) return true;

            Vector3 center = transform.TransformPoint(controller.center);
            float radius = Mathf.Max(0.02f, controller.radius - 0.02f);
            Vector3 top = center + Vector3.up * (controller.height * 0.5f - controller.radius);
            Vector3 destination = top + Vector3.up * growth;

            // The character's own collider is skipped, otherwise this would always fail.
            return CharacterCollision.CapsuleIsClear(top, destination, radius, obstructionMask, transform);
        }

        public override void SetCollisionEnabled(bool enabled)
        {
            Controller.enabled = enabled;
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            CharacterController controller = Controller;
            if (controller == null) return;

            CharacterProfile p = Profile;
            float probeDistance = p != null ? Mathf.Max(0.02f, p.groundProbeDistance) : 0.25f;
            float radius = Mathf.Max(0.01f, controller.radius - 0.02f);
            Vector3 bottom = transform.TransformPoint(controller.center + Vector3.down * (controller.height * 0.5f - controller.radius));
            Vector3 origin = bottom + Vector3.up * probeDistance;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(origin, radius);
        }
#endif
    }
}