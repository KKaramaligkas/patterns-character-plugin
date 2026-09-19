using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Flight and noclip / spectator mode. Flight keeps collisions and adds full 3D control,
    /// noclip disables the collider entirely for debugging, cinematics or spectator cameras.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Fly")]
    public class FlyAbility : ToggleAbility
    {
        public enum FlightMode
        {
            /// <summary>Collides with the world, full 3D movement.</summary>
            Fly = 0,
            /// <summary>Passes through everything. Toggle back before landing.</summary>
            NoClip = 1
        }

        [Header("Mode")]
        public FlightMode flightMode = FlightMode.Fly;

        [Header("Movement")]
        [Tooltip("Flight speed in m/s. Negative uses the profile value.")]
        public float flySpeed = -1f;

        [Tooltip("Vertical speed as a fraction of the flight speed.")]
        [Range(0.1f, 1f)] public float verticalSpeedRatio = 0.8f;

        [Tooltip("When true the character moves along the full camera basis so looking up and pressing forward climbs.")]
        public bool moveAlongCameraBasis = true;

        [Tooltip("When true the character has no inertia at all while flying.")]
        public bool instantResponse;

        [Tooltip("Gravity is disabled while flying.")]
        public bool disableGravity = true;

        [Tooltip("When true flight is only available while airborne (jetpack style).")]
        public bool onlyWhileAirborne;

        [Tooltip("Applies an upward launch as flight starts, used for jetpack jumps.")]
        [Min(0f)] public float takeOffImpulse;

        [Tooltip("Applies a downward impulse as flight ends so the character falls with weight.")]
        [Min(0f)] public float landingImpulse;

        float _previousGravityScale = 1f;

        /// <summary>True while the character is flying.</summary>
        public bool IsFlying { get { return IsOn; } }

        /// <summary>True when flying with collisions off.</summary>
        public bool IsNoClip { get { return IsOn && flightMode == FlightMode.NoClip; } }

        protected override void OnTurnedOn()
        {
            if (Motor == null) return;

            _previousGravityScale = Motor.persistentGravityScale;
            if (disableGravity) Motor.persistentGravityScale = 0f;

            if (onlyWhileAirborne && Motor.IsGrounded)
            {
                ForceOff();
                return;
            }

            if (flightMode == FlightMode.NoClip)
            {
                Motor.SetCollisionEnabled(false);
            }

            if (takeOffImpulse > 0f) Motor.SetVerticalVelocity(takeOffImpulse);
        }

        protected override void OnTurnedOff()
        {
            if (Motor == null) return;

            Motor.persistentGravityScale = _previousGravityScale;

            if (flightMode == FlightMode.NoClip)
            {
                Motor.SetCollisionEnabled(true);
            }

            if (landingImpulse > 0f) Motor.SetVerticalVelocity(-landingImpulse);
        }

        protected override void OnToggleTick(float deltaTime)
        {
            if (Motor == null) return;

            CharacterProfile p = Profile;
            float speed = flySpeed >= 0f ? flySpeed : p.flySpeed;

            float horizontal = Input != null ? Input.move.x : 0f;
            float forward = Input != null ? Input.move.y : 0f;
            float vertical = 0f;

            if (Input != null)
            {
                if (Input.IsHeld(CharacterAction.Jump)) vertical += 1f;
                if (Input.IsHeld(CharacterAction.Crouch)) vertical -= 1f;
            }

            Transform cameraTransform = moveAlongCameraBasis && Context != null && Context.CameraRig != null
                ? Context.CameraRig.ActiveCameraTransform
                : null;

            Vector3 direction;

            if (cameraTransform != null)
            {
                Vector3 forwardBasis = cameraTransform.forward;
                Vector3 rightBasis = cameraTransform.right;
                direction = rightBasis * horizontal + forwardBasis * forward;

                if (!moveAlongCameraBasis)
                {
                    direction = Vector3.ProjectOnPlane(direction, Vector3.up);
                }
            }
            else
            {
                direction = transform.right * horizontal + transform.forward * forward;
            }

            direction += Vector3.up * vertical * verticalSpeedRatio;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            Vector3 target = direction * speed;

            if (direction.sqrMagnitude < 1e-4f)
            {
                // Hovering: kill the velocity so the character stays exactly where it is.
                target = Vector3.zero;
            }

            SubmitMotion(target, false, true);

            if (instantResponse)
            {
                Motor.ClearExternalVelocity();
            }
        }

        /// <summary>Switches between flight and noclip without leaving fly mode.</summary>
        public void SetFlightMode(FlightMode newMode)
        {
            if (flightMode == newMode) return;
            bool wasOn = IsOn;
            ForceOff();
            flightMode = newMode;
            if (wasOn) ForceOn();
        }
    }
}