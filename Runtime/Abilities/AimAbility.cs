using System;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Aim / ADS. While aiming the body faces the camera, the locomotion tier drops so the character moves
    /// slowly and the active camera rig pulls in and narrows the field of view. Everything is requested
    /// through the same priority system the motor uses, so releasing the button restores the previous state.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Aim")]
    public class AimAbility : ContinuousAbility
    {
        [Header("Input")]
        public CharacterAction action = CharacterAction.SecondaryAction;

        [Tooltip("When true aiming is toggled instead of held.")]
        public bool toggle;

        [Tooltip("When true the character can only aim while standing still on the ground.")]
        public bool requiresGrounded;

        [Header("Locomotion")]
        [Tooltip("Tier used while aiming.")]
        public LocomotionTier tier = LocomotionTier.Walk;

        [Tooltip("Priority of the tier request. Crouch uses 200, so aiming at 150 lets crouch win.")]
        public int tierPriority = 150;

        [Tooltip("Speed multiplier while aiming.")]
        [Range(0.05f, 1f)] public float speedMultiplier = 0.5f;

        [Header("Orientation")]
        [Tooltip("Force the body to face the camera while aiming.")]
        public bool faceCamera = true;

        [Tooltip("Priority of the orientation override.")]
        public int orientationPriority = 150;

        [Header("Camera")]
        [Tooltip("Field of view change while aiming, in degrees.")]
        public float fovDelta = -14f;

        [Tooltip("Camera distance multiplier while aiming, for over the shoulder framing.")]
        [Range(0.1f, 1f)] public float distanceMultiplier = 0.55f;

        [Tooltip("Camera shoulder offset while aiming, in camera local space.")]
        public Vector3 shoulderOffset = new Vector3(0.45f, 0f, 0f);

        [Tooltip("Look sensitivity multiplier while aiming.")]
        [Range(0.05f, 1f)] public float sensitivityMultiplier = 0.55f;

        [Tooltip("Seconds for the aim blend to reach full strength.")]
        [Min(0.01f)] public float blendSpeed = 12f;

        OrientationAbility _orientation;
        LocomotionAbility _locomotion;
        bool _toggled;

        /// <summary>True while the aim input is being held / toggled on.</summary>
        public bool IsAiming { get; private set; }

        /// <summary>0..1 blend used by cameras, animation and FOV.</summary>
        public float AimRatio { get; private set; }

        public event Action AimStarted;
        public event Action AimStopped;

        protected override void OnBind()
        {
            _orientation = Context != null ? Context.GetAbility<OrientationAbility>() : null;
            _locomotion = Context != null ? Context.GetAbility<LocomotionAbility>() : null;
        }

        protected override void OnTick(float deltaTime)
        {
            bool wants;

            if (toggle)
            {
                if (Input != null && Input.ConsumePressed(action)) _toggled = !_toggled;
                wants = _toggled;
            }
            else
            {
                wants = Input != null && Input.IsHeld(action);
            }

            if (requiresGrounded && Motor != null && !Motor.IsGrounded) wants = false;

            bool wasAiming = IsAiming;
            IsAiming = wants;

            AimRatio = Mathf.Lerp(AimRatio, IsAiming ? 1f : 0f, CharacterMath.SmoothFactor(blendSpeed, deltaTime));
            if (AimRatio < 0.001f && !IsAiming) AimRatio = 0f;

            if (IsAiming)
            {
                if (_locomotion != null) _locomotion.RequestTier(this, tierPriority, tier, speedMultiplier);

                if (faceCamera && _orientation != null)
                {
                    _orientation.SetModeOverride(this, orientationPriority, OrientationMode.FaceCamera);
                }

                if (Context != null && Context.CameraRig != null)
                {
                    Context.CameraRig.SetAimState(this, AimRatio, fovDelta, distanceMultiplier, shoulderOffset);
                    Context.CameraRig.SetSensitivityScale(this, Mathf.Lerp(1f, sensitivityMultiplier, AimRatio));
                }
            }
            else
            {
                if (_locomotion != null) _locomotion.ClearTierRequest(this);
                if (faceCamera && _orientation != null) _orientation.ClearModeOverride(this);

                if (Context != null && Context.CameraRig != null)
                {
                    Context.CameraRig.SetAimState(this, 0f, 0f, 1f, Vector3.zero);
                    Context.CameraRig.SetSensitivityScale(this, 1f);
                }
            }

            if (wasAiming != IsAiming)
            {
                if (IsAiming)
                {
                    var handler = AimStarted;
                    if (handler != null) handler();
                }
                else
                {
                    var handler = AimStopped;
                    if (handler != null) handler();
                }
            }
        }

        protected override void OnDisabled()
        {
            _toggled = false;
            IsAiming = false;
            AimRatio = 0f;

            if (_locomotion != null) _locomotion.ClearTierRequest(this);
            if (_orientation != null) _orientation.ClearModeOverride(this);

            if (Context != null && Context.CameraRig != null)
            {
                Context.CameraRig.SetAimState(this, 0f, 0f, 1f, Vector3.zero);
                Context.CameraRig.SetSensitivityScale(this, 1f);
            }
        }
    }
}