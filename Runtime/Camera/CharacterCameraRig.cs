using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Base class of every camera mode. A rig only has to compute where the camera should be, the base
    /// class handles smoothing, field of view, aim blending and activation.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public abstract class CharacterCameraRig : MonoBehaviour
    {
        [Header("Rig")]
        [Tooltip("Name used by code to switch to this rig, for example 'Third Person'.")]
        public string rigId = "Camera Rig";

        [Tooltip("Higher priority rigs are picked first when no rig is specified.")]
        public int priority;

        [Tooltip("When true the cursor is locked while this rig is active.")]
        public bool wantsCursorLocked = true;

        [Tooltip("Camera driven by this rig. Created as a child when left empty.")]
        public Camera cameraComponent;

        [Header("Smoothing")]
        [Tooltip("SmoothDamp time for the camera position. 0 snaps instantly.")]
        [Min(0f)] public float positionSmoothTime = 0.06f;

        [Tooltip("How quickly the camera rotation catches up. 0 snaps instantly.")]
        [Min(0f)] public float rotationSharpness = 22f;

        [Tooltip("Smoothing used for a short while after this rig becomes active, makes mode changes blend.")]
        [Min(0f)] public float transitionSharpness = 5f;

        [Tooltip("Offset applied to the computed position, in camera local space.")]
        public Vector3 positionOffset;

        [Tooltip("Added to the field of view while this rig is active.")]
        public float fovOffset;

        [Tooltip("Roll applied to the camera, in degrees.")]
        public float roll;

        [Header("Look Pivots (optional)")]
        [Tooltip("Optional transform rotated by the camera yaw, for user supplied head / model rigs.")]
        public Transform yawPivot;

        [Tooltip("Optional transform rotated by the camera pitch, for user supplied head / model rigs.")]
        public Transform pitchPivot;

        [Header("Pitch Limits")]
        [Tooltip("When true this rig overrides the profile pitch limits while it is active.")]
        public bool overridePitchLimits;

        [Tooltip("Minimum and maximum pitch used when the limits are overridden. Top-down rigs usually use 10 / 89.")]
        public Vector2 pitchLimits = new Vector2(-80f, 80f);

        Vector3 _smoothedPosition;
        Vector3 _positionVelocity;
        float _transitionTimer;
        float _baseFov = 60f;
        bool _snapped;
        bool _fovCaptured;

        /// <summary>Aim blend, 0 = hip fire, 1 = fully aimed.</summary>
        protected float aimRatio;
        protected float aimFovDelta;
        protected float aimDistanceScale = 1f;
        protected Vector3 aimShoulderOffset;

        public CharacterContext Context { get; internal set; }
        public bool IsActive { get; private set; }

        public Camera Camera
        {
            get
            {
                if (cameraComponent == null) cameraComponent = GetComponentInChildren<Camera>();
                return cameraComponent;
            }
        }

        public Transform CameraTransform
        {
            get
            {
                Camera cam = Camera;
                return cam != null ? cam.transform : transform;
            }
        }

        /// <summary>True while the rig is blending in after a mode change.</summary>
        public bool IsTransitioning { get { return _transitionTimer > 0f; } }

        protected virtual void Awake()
        {
            Camera cam = Camera;
            if (cam == null)
            {
                GameObject go = new GameObject("Camera");
                go.transform.SetParent(transform, false);
                cam = go.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cameraComponent = cam;
            }

            _baseFov = cam.fieldOfView;
            _fovCaptured = true;
            _smoothedPosition = cam.transform.position;
        }

        protected virtual void OnEnable()
        {
            if (IsActive)
            {
                _transitionTimer = 0.25f;
            }
        }

        internal void Activate(CharacterContext context, float blendTime)
        {
            Context = context;
            IsActive = true;
            _transitionTimer = blendTime > 0f ? blendTime : 0.2f;
            _snapped = false;
            OnActivate();
        }

        internal void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            OnDeactivate();
        }

        /// <summary>Called when the rig becomes the active camera.</summary>
        protected virtual void OnActivate() { }

        /// <summary>Called when another rig takes over.</summary>
        protected virtual void OnDeactivate() { }

        /// <summary>Where the camera should be and how it should be rotated, before smoothing.</summary>
        protected abstract void ComputeTarget(out Vector3 position, out Quaternion rotation);

        /// <summary>Field of view this rig wants, including aim and speed effects.</summary>
        protected virtual float ComputeFov(float baseFov)
        {
            return baseFov + fovOffset + aimFovDelta * aimRatio;
        }

        /// <summary>Called by the switcher when aiming, values are already blended by the ability.</summary>
        public virtual void ApplyAim(float ratio, float fovDelta, float distanceScale, Vector3 shoulderOffset)
        {
            aimRatio = Mathf.Clamp01(ratio);
            aimFovDelta = fovDelta;
            aimDistanceScale = Mathf.Clamp(distanceScale, 0.05f, 2f);
            aimShoulderOffset = shoulderOffset;
        }

        /// <summary>Extra rotation applied after the rig rotation, used by shake and lean effects.</summary>
        public virtual Quaternion AdditionalRotation { get { return Quaternion.identity; } }

        /// <summary>Extra world space position applied after smoothing, used by shake.</summary>
        public virtual Vector3 AdditionalPosition { get { return Vector3.zero; } }

        /// <summary>Places the camera exactly where the rig wants it, skipping all smoothing.</summary>
        public void Snap()
        {
            Vector3 position;
            Quaternion rotation;
            ComputeTarget(out position, out rotation);

            position += rotation * positionOffset + AdditionalPosition;
            rotation *= Quaternion.Euler(0f, 0f, roll) * AdditionalRotation;

            _smoothedPosition = position;
            _positionVelocity = Vector3.zero;
            _snapped = true;
            _transitionTimer = 0f;

            Camera cam = Camera;
            if (cam == null) return;

            cam.transform.SetPositionAndRotation(position, rotation);
            cam.fieldOfView = ComputeFov(_baseFov);
        }

        protected virtual void LateUpdate()
        {
            if (!IsActive || !isActiveAndEnabled) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_transitionTimer > 0f) _transitionTimer = Mathf.Max(0f, _transitionTimer - dt);

            Vector3 position;
            Quaternion rotation;
            ComputeTarget(out position, out rotation);

            position += rotation * positionOffset;
            rotation *= Quaternion.Euler(0f, 0f, roll);

            Transform cameraTransform = CameraTransform;
            if (cameraTransform == null) return;

            if (!_snapped)
            {
                _smoothedPosition = cameraTransform.position;
                _snapped = true;
            }

            float smoothTime = _transitionTimer > 0f ? Mathf.Max(positionSmoothTime, 0.18f) : positionSmoothTime;
            if (smoothTime <= 0f)
            {
                _smoothedPosition = position;
                _positionVelocity = Vector3.zero;
            }
            else
            {
                _smoothedPosition = Vector3.SmoothDamp(_smoothedPosition, position, ref _positionVelocity, smoothTime, Mathf.Infinity, dt);
            }

            float sharpness = _transitionTimer > 0f ? transitionSharpness : rotationSharpness;
            Quaternion smoothedRotation;
            if (sharpness <= 0f)
            {
                smoothedRotation = rotation;
            }
            else
            {
                smoothedRotation = Quaternion.Slerp(cameraTransform.rotation, rotation, CharacterMath.SmoothFactor(sharpness, dt));
            }

            cameraTransform.SetPositionAndRotation(_smoothedPosition + AdditionalPosition, smoothedRotation * AdditionalRotation);

            Camera cam = Camera;
            if (cam != null)
            {
                if (!_fovCaptured)
                {
                    _baseFov = cam.fieldOfView;
                    _fovCaptured = true;
                }

                float targetFov = ComputeFov(_baseFov);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, CharacterMath.SmoothFactor(12f, dt));
            }
        }

        /// <summary>Base field of view captured when the rig woke up.</summary>
        public float BaseFov
        {
            get { return _baseFov; }
        }
    }
}