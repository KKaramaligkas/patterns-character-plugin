using System;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Owns the camera look state (yaw / pitch) and pushes it onto the pivots supplied by whichever
    /// camera rig is active. Body rotation is deliberately not handled here, that is the job of
    /// <see cref="OrientationAbility"/> so first person, strafing and top-down can share one source of truth.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterLook : MonoBehaviour
    {
        [Header("State")]
        [Tooltip("Current yaw in degrees, world space. Wrapped to -180 / 180.")]
        public float yaw;

        [Tooltip("Current pitch in degrees, negative looks down.")]
        public float pitch = 10f;

        [Header("Limits")]
        [Tooltip("When true the pitch limits come from the CharacterProfile instead of the values below.")]
        public bool useProfileLimits = true;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        [Header("Smoothing")]
        [Tooltip("0 = instant (first person). Higher values smooth the camera out for third person and top-down.")]
        [Min(0f)] public float yawSharpness;
        [Min(0f)] public float pitchSharpness;

        [Header("Pivots")]
        [Tooltip("Transform rotated by the yaw only. Set by the active camera rig.")]
        public Transform yawPivot;

        [Tooltip("Transform rotated by the pitch only. Set by the active camera rig.")]
        public Transform pitchPivot;

        [Tooltip("Optional transform used as the orbit centre (head / chest).")]
        public Transform lookTarget;

        float _displayYaw;
        float _displayPitch;
        bool _hasDisplay;

        public CharacterContext Context { get; internal set; }
        public CharacterProfile Profile { get { return Context != null ? Context.Profile : CharacterProfile.Default; } }

        public float MinPitch { get { return useProfileLimits ? Profile.minPitch : minPitch; } }
        public float MaxPitch { get { return useProfileLimits ? Profile.maxPitch : maxPitch; } }

        /// <summary>Yaw actually applied to the pivot this frame (smoothed).</summary>
        public float DisplayYaw { get { return _displayYaw; } }

        /// <summary>Pitch actually applied to the pivot this frame (smoothed).</summary>
        public float DisplayPitch { get { return _displayPitch; } }

        /// <summary>Forward direction of the camera yaw, flattened.</summary>
        public Vector3 YawForward
        {
            get { return Quaternion.Euler(0f, _displayYaw, 0f) * Vector3.forward; }
        }

        /// <summary>Right direction of the camera yaw, flattened.</summary>
        public Vector3 YawRight
        {
            get { return Quaternion.Euler(0f, _displayYaw, 0f) * Vector3.right; }
        }

        public event Action<float, float> LookApplied;

        void Awake()
        {
            _displayYaw = yaw;
            _displayPitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            _hasDisplay = true;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!_hasDisplay)
            {
                _displayYaw = yaw;
                _displayPitch = pitch;
                _hasDisplay = true;
            }

            _displayYaw = yawSharpness <= 0f
                ? yaw
                : Mathf.LerpAngle(_displayYaw, yaw, CharacterMath.SmoothFactor(yawSharpness, dt));

            _displayPitch = pitchSharpness <= 0f
                ? pitch
                : Mathf.Lerp(_displayPitch, pitch, CharacterMath.SmoothFactor(pitchSharpness, dt));

            Apply();

            var handler = LookApplied;
            if (handler != null) handler(_displayYaw, _displayPitch);
        }

        /// <summary>Rotates the yaw, wrapping the value.</summary>
        public void AddYaw(float degrees)
        {
            yaw = Mathf.Repeat(yaw + degrees + 180f, 360f) - 180f;
        }

        /// <summary>Rotates the pitch and clamps it to the configured limits.</summary>
        public void AddPitch(float degrees)
        {
            pitch = Mathf.Clamp(pitch + degrees, MinPitch, MaxPitch);
        }

        /// <summary>Sets both axes at once, used when a camera mode activates.</summary>
        public void SetLook(float newYaw, float newPitch, bool immediate = true)
        {
            yaw = Mathf.Repeat(newYaw + 180f, 360f) - 180f;
            pitch = Mathf.Clamp(newPitch, MinPitch, MaxPitch);
            if (immediate)
            {
                _displayYaw = yaw;
                _displayPitch = pitch;
            }
        }

        /// <summary>Aligns the yaw with a world direction.</summary>
        public void SetYawFromDirection(Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flat.sqrMagnitude < 1e-5f) return;
            SetLook(Quaternion.LookRotation(flat).eulerAngles.y, pitch);
        }

        /// <summary>Snaps the smoothed values to the current target, used after teleports and mode changes.</summary>
        public void Snap()
        {
            _displayYaw = yaw;
            _displayPitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        }

        /// <summary>Points the yaw / pitch pivots at the current values.</summary>
        public void Apply()
        {
            if (yawPivot != null)
            {
                yawPivot.rotation = Quaternion.Euler(0f, _displayYaw, 0f);
            }

            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.Euler(_displayPitch, 0f, 0f);
            }
        }

        /// <summary>Called by a camera rig when it becomes active.</summary>
        public void SetPivots(Transform newYawPivot, Transform newPitchPivot, bool snap = true)
        {
            yawPivot = newYawPivot;
            pitchPivot = newPitchPivot;
            if (snap) Snap();
            Apply();
        }

        /// <summary>Rotation the camera should use, combining yaw and pitch.</summary>
        public Quaternion CameraRotation
        {
            get { return Quaternion.Euler(_displayPitch, _displayYaw, 0f); }
        }
    }
}