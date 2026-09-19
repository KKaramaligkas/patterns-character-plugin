using System;
using System.Collections.Generic;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// A single frame movement intent. Abilities push these into the motor every frame they want to move
    /// and the motor resolves the winner by priority, which is what keeps dash, fly and locomotion from
    /// fighting each other.
    /// </summary>
    public struct MotorRequest
    {
        public object Source;
        public int Priority;
        public Vector3 Velocity;
        /// <summary>When false the motor integrates gravity on the vertical axis (jump / fall).</summary>
        public bool UseGravity;
        /// <summary>When true the request's Y value is used verbatim (fly, swim, ladders).</summary>
        public bool DrivesVertical;
        /// <summary>Ground snapping is skipped (used right after a jump so the character does not stick).</summary>
        public bool SkipGroundSnap;
    }

    /// <summary>
    /// Base class for anything that moves a character. The plugin ships a <see cref="CharacterMotor"/>
    /// (CharacterController based) and a <see cref="RigidbodyCharacterMotor"/>; write your own for
    /// custom physics, root motion or network prediction.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public abstract class MotorBase : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Leave empty to use the CharacterContext profile or the built in runtime defaults.")]
        [SerializeField] protected CharacterProfile profile;

        [Header("Collision")]
        [SerializeField] protected LayerMask groundMask = ~0;

        [Header("Movement")]
        [Tooltip("Master switch, exposed so cutscenes, death and dialogue can freeze the character.")]
        public bool movementLocked;

        [Tooltip("Multiplies the gravity of the profile. Changed at runtime by swim / fly abilities.")]
        public float persistentGravityScale = 1f;

        [Tooltip("How fast external velocities such as knockback and dash decay (units per second squared).")]
        [Min(0f)] public float externalDrag = 18f;

        [Tooltip("Extra planar smoothing on top of the profile acceleration. 0 = purely profile driven.")]
        [Min(0f)] public float additionalSmoothing;

        protected readonly List<MotorRequest> requests = new List<MotorRequest>(8);

        protected Vector3 velocity;
        protected Vector3 externalVelocity;
        protected Vector3 platformMotion;
        protected bool externalPreserved;

        bool _wasGrounded;
        float _airTime;
        float _frameGravityScale = 1f;
        float _frameFallLimit = -1f;
        bool _grounded;
        GroundInfo _ground = GroundInfo.None;
        MovementBlockReason _blockReason = MovementBlockReason.NoRequest;

        public CharacterContext Context { get; internal set; }

        /// <summary>Final velocity including gravity, requests, impulses and platform motion.</summary>
        public Vector3 Velocity { get { return velocity + externalVelocity; } }

        /// <summary>Velocity owned by the motor (gravity + movement). Excludes impulses.</summary>
        public Vector3 MotorVelocity { get { return velocity; } }

        public Vector3 PlanarVelocity
        {
            get { return Vector3.ProjectOnPlane(Velocity, Vector3.up); }
        }

        public float Speed { get { return Velocity.magnitude; } }
        public float PlanarSpeed { get { return PlanarVelocity.magnitude; } }
        public bool IsGrounded { get { return _grounded; } }
        public GroundInfo Ground { get { return _ground; } }
        public float AirTime { get { return _airTime; } }
        public MovementBlockReason BlockReason { get { return _blockReason; } }
        public bool IsAirborne { get { return !_grounded; } }

        /// <summary>True while at least one ability is asking the motor to move this frame.</summary>
        public bool HasRequest { get; private set; }

        /// <summary>Requested planar direction, normalised. Zero when no request is active.</summary>
        public Vector3 RequestedDirection { get; private set; }

        /// <summary>Requested planar speed in units per second.</summary>
        public float RequestedSpeed { get; private set; }

        /// <summary>How long the character has been standing on ground steeper than the slope limit.</summary>
        public bool IsOnSteepSlope
        {
            get { return _grounded && _ground.IsGrounded && _ground.SlopeAngle > SlopeLimit + 0.5f; }
        }

        public virtual CharacterProfile Profile
        {
            get
            {
                if (profile != null) return profile;
                if (Context != null && Context.Profile != null) return Context.Profile;
                return CharacterProfile.Default;
            }
        }

        public event Action<LandingInfo> Landed;
        public event Action<GroundInfo> BecameGrounded;
        public event Action LeftGround;
        public event Action<Collider> HitCeiling;

        /// <summary>Raised by concrete motors when the character bumps into a ceiling.</summary>
        protected void RaiseHitCeiling(Collider collider)
        {
            var handler = HitCeiling;
            if (handler != null) handler(collider);
        }

        /// <summary>Slope limit in degrees, above which the character slides down.</summary>
        public abstract float SlopeLimit { get; }

        /// <summary>Current collider height, used by crouch to grow / shrink the character.</summary>
        public abstract float Height { get; set; }

        /// <summary>Current collider radius.</summary>
        public abstract float Radius { get; }

        /// <summary>Centre of the capsule in world space, used by camera and animation anchors.</summary>
        public abstract Vector3 ColliderCenter { get; }

        /// <summary>Bottom point of the character, in world space.</summary>
        public abstract Vector3 FootPosition { get; }

        void Awake()
        {
            _frameGravityScale = persistentGravityScale;
            OnMotorAwake();
        }

        protected virtual void OnMotorAwake() { }

        protected virtual void OnEnable()
        {
            _wasGrounded = false;
            _airTime = 0f;
        }

        protected virtual void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateGrounding(dt);
            ApplyMotionInternal(dt);

            // Frame scoped modifiers live exactly one frame, abilities re-arm them every tick.
            _frameGravityScale = persistentGravityScale;
            _frameFallLimit = -1f;
        }

        // ---------------------------------------------------------------- public API

        /// <summary>Submits a movement intent for this frame. Highest priority request wins.</summary>
        public void SubmitRequest(object source, int priority, Vector3 worldVelocity,
            bool useGravity = true, bool drivesVertical = false, bool skipGroundSnap = false)
        {
            MotorRequest request;
            request.Source = source;
            request.Priority = priority;
            request.Velocity = worldVelocity;
            request.UseGravity = useGravity;
            request.DrivesVertical = drivesVertical;
            request.SkipGroundSnap = skipGroundSnap;
            requests.Add(request);
        }

        /// <summary>Convenience overload for abilities that only care about planar movement.</summary>
        public void SubmitPlanarRequest(object source, int priority, Vector3 planarVelocity)
        {
            SubmitRequest(source, priority, Vector3.ProjectOnPlane(planarVelocity, Vector3.up), true, false, false);
        }

        /// <summary>Adds an instant velocity change (jump, knockback, launch).</summary>
        public void AddImpulse(Vector3 impulse, bool cancelVertical = false)
        {
            if (cancelVertical)
            {
                externalVelocity.y = 0f;
                velocity.y = 0f;
            }
            externalVelocity += impulse;
        }

        /// <summary>Sets the external velocity directly, replacing whatever impulses were pending.</summary>
        public void SetExternalVelocity(Vector3 worldVelocity)
        {
            externalVelocity = worldVelocity;
        }

        /// <summary>
        /// Launches the character vertically (jump, bounce pad, ladder dismount). Pending vertical
        /// impulses are cancelled first so the launch velocity is exactly what was asked for.
        /// </summary>
        public void Launch(float verticalVelocity)
        {
            externalVelocity.y = 0f;
            velocity.y = verticalVelocity;
        }

        /// <summary>Moves remaining impulse velocity into the motor velocity so the motion trails off naturally.</summary>
        public void TransferExternalVelocityToMotor(float retention = 1f)
        {
            velocity += externalVelocity * Mathf.Clamp01(retention);
            externalVelocity = Vector3.zero;
        }

        public Vector3 ExternalVelocity { get { return externalVelocity; } }

        public void ClearExternalVelocity()
        {
            externalVelocity = Vector3.zero;
        }

        public void SetVerticalVelocity(float verticalVelocity)
        {
            velocity.y = verticalVelocity;
        }

        /// <summary>Multiplies gravity for this frame only. Abilities call it every tick while active.</summary>
        public void SetGravityScale(float scale)
        {
            _frameGravityScale = scale;
        }

        /// <summary>Overrides the falling speed clamp for this frame only. Negative = use the profile value.</summary>
        public void SetFallSpeedLimit(float limit)
        {
            _frameFallLimit = limit;
        }

        /// <summary>Freezes the character in place (dash freeze frame, hit stop, cutscene).</summary>
        public void ClearMotion()
        {
            velocity = Vector3.zero;
            externalVelocity = Vector3.zero;
        }

        public void ClearRequests()
        {
            requests.Clear();
            HasRequest = false;
            RequestedDirection = Vector3.zero;
            RequestedSpeed = 0f;
        }

        /// <summary>Teleports the character, clearing velocity by default.</summary>
        public abstract void Teleport(Vector3 position, bool keepVelocity = false);
        public abstract void Teleport(Vector3 position, Quaternion rotation, bool keepVelocity = false);

        /// <summary>Runtime adjustment of the collision capsule, used by crouch.</summary>
        public abstract void Resize(float height, float radius, Vector3 center);

        /// <summary>Checks whether there is room to grow to the supplied height. Used when standing up.</summary>
        public virtual bool CanFit(float height)
        {
            return true;
        }

        public abstract void SetCollisionEnabled(bool enabled);

        /// <summary>Instant planar push used by wind zones, treadmills and conveyor belts.</summary>
        public void AddEnvironmentalVelocity(Vector3 planarVelocity)
        {
            externalVelocity += Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        }

        // ---------------------------------------------------------------- internals

        void UpdateGrounding(float dt)
        {
            GroundInfo info;
            bool grounded = ProbeGround(out info);

            _grounded = grounded;
            _ground = grounded ? info : GroundInfo.None;

            // Moving platform inheritance.
            platformMotion = Vector3.zero;
            if (grounded && _ground.Platform != null && _ground.Platform.inheritVelocity)
            {
                platformMotion = _ground.Platform.DeltaPosition;
            }

            if (grounded && !_wasGrounded)
            {
                LandingInfo landing;
                landing.Position = transform.position;
                landing.ImpactSpeed = Mathf.Abs(Mathf.Min(0f, velocity.y));
                landing.AirTime = _airTime;
                landing.Ground = _ground;
                landing.HardLanding = landing.ImpactSpeed > Mathf.Abs(Profile.gravity) * 0.35f;

                _airTime = 0f;
                var handler = Landed;
                if (handler != null) handler(landing);

                var groundedHandler = BecameGrounded;
                if (groundedHandler != null) groundedHandler(_ground);
            }
            else if (!grounded && _wasGrounded)
            {
                var handler = LeftGround;
                if (handler != null) handler();
            }

            if (!grounded) _airTime += dt;
            _wasGrounded = grounded;
        }

        void ApplyMotionInternal(float dt)
        {
            MotorRequest request;
            bool hasRequest = TryResolveRequest(out request);

            HasRequest = hasRequest;
            if (hasRequest)
            {
                Vector3 planar = Vector3.ProjectOnPlane(request.Velocity, Vector3.up);
                RequestedSpeed = planar.magnitude;
                RequestedDirection = RequestedSpeed > 1e-4f ? planar / RequestedSpeed : Vector3.zero;
            }
            else
            {
                RequestedSpeed = 0f;
                RequestedDirection = Vector3.zero;
            }

            if (movementLocked)
            {
                _blockReason = MovementBlockReason.Locked;
                velocity = Vector3.Lerp(velocity, Vector3.zero, CharacterMath.SmoothFactor(20f, dt));
                velocity.y = ApplyVertical(velocity.y, 0f, false, true, dt);
                externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, externalDrag * dt);
                CommitMotion(velocity * dt + externalVelocity * dt + platformMotion, true);
                requests.Clear();
                return;
            }

            _blockReason = hasRequest ? MovementBlockReason.None : MovementBlockReason.NoRequest;

            CharacterProfile p = Profile;
            bool airborne = !_grounded;

            // ---- planar
            Vector3 targetPlanar = hasRequest ? Vector3.ProjectOnPlane(request.Velocity, Vector3.up) : Vector3.zero;
            Vector3 currentPlanar = Vector3.ProjectOnPlane(velocity, Vector3.up);

            float accel = p.acceleration;
            float decel = p.deceleration;
            if (additionalSmoothing > 0f)
            {
                accel = Mathf.Min(accel, additionalSmoothing);
                decel = Mathf.Min(decel, additionalSmoothing);
            }

            if (airborne)
            {
                // In the air both ramping up and slowing down get muted, scaled by airControl.
                float scale = Mathf.Clamp01(p.airControl);
                accel = Mathf.Max(accel * scale, 1f);
                decel = Mathf.Max(decel * scale, 1f);
            }

            Vector3 planarVelocity = CharacterMath.ApproachVelocity(currentPlanar, targetPlanar, accel, decel, dt);

            // Slope slide: on a surface too steep to stand on, slide downhill.
            if (p.slideOnSteepSlopes && _grounded && _ground.IsGrounded && _ground.SlopeAngle > SlopeLimit)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _ground.Normal).normalized;
                planarVelocity += downhill * p.slopeSlideSpeed;
            }

            velocity.x = planarVelocity.x;
            velocity.z = planarVelocity.z;

            float requestedVertical = request.Velocity.y;
            bool drivesVertical = hasRequest && request.DrivesVertical;
            bool useGravity = !hasRequest || request.UseGravity;

            velocity.y = ApplyVertical(velocity.y, requestedVertical, drivesVertical, useGravity, dt);

            // ---- impulses
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, externalDrag * dt);

            bool skipSnap = hasRequest && request.SkipGroundSnap;
            CommitMotion(velocity * dt + externalVelocity * dt + platformMotion, skipSnap);

            requests.Clear();
        }

        /// <summary>Resolves the vertical axis for this frame.</summary>
        float ApplyVertical(float current, float target, bool drivesVertical, bool useGravity, float dt)
        {
            CharacterProfile p = Profile;
            float verticalAccel = Mathf.Max(p.acceleration, 10f);

            if (drivesVertical || !useGravity)
            {
                // Fly, swim, ladders and hover: the ability owns the vertical axis.
                return Mathf.MoveTowards(current, target, verticalAccel * dt);
            }

            float limit = _frameFallLimit > 0f ? _frameFallLimit : p.maxFallSpeed;
            float vertical = current + p.gravity * _frameGravityScale * dt;
            if (vertical < -limit) vertical = -limit;
            return vertical;
        }

        /// <summary>Applies the final motion to the underlying collider. Implemented by concrete motors.</summary>
        protected abstract void CommitMotion(Vector3 motion, bool skipGroundSnap);

        /// <summary>Fills <paramref name="info"/> with the surface below the character.</summary>
        protected abstract bool ProbeGround(out GroundInfo info);

        bool TryResolveRequest(out MotorRequest winner)
        {
            winner = default(MotorRequest);
            if (requests.Count == 0) return false;

            int bestIndex = 0;
            for (int i = 1; i < requests.Count; i++)
            {
                if (requests[i].Priority >= requests[bestIndex].Priority) bestIndex = i;
            }
            winner = requests[bestIndex];
            return true;
        }

        /// <summary>Builds a GroundInfo from a raycast hit against the ground mask.</summary>
        protected GroundInfo BuildGroundInfo(RaycastHit hit)
        {
            GroundInfo info;
            info.IsGrounded = true;
            info.Normal = hit.normal;
            info.Point = hit.point;
            info.Distance = hit.distance;
            info.SlopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            info.Collider = hit.collider;
            info.Platform = hit.collider != null ? hit.collider.GetComponentInParent<MovingPlatform>() : null;
            return info;
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = _grounded ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(FootPosition, 0.06f);
            if (_ground.IsGrounded)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(FootPosition, _ground.Point);
            }
        }
#endif
    }
}