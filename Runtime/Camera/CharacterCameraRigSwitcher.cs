using System;
using System.Collections.Generic;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Owns the camera modes of a character. It feeds look input into <see cref="CharacterLook"/>,
    /// switches between rigs with a smooth blend, manages the cursor and routes aim requests to whichever
    /// rig is active. First person, third person, top-down, 2.5D follow and fixed cameras are all rigs.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class CharacterCameraRigSwitcher : MonoBehaviour
    {
        [Header("Rigs")]
        [Tooltip("Rigs available to this character. Collected from the children when left empty.")]
        [SerializeField] List<CharacterCameraRig> rigs = new List<CharacterCameraRig>();

        [Tooltip("Rig selected on start. When empty the highest priority rig is used.")]
        [SerializeField] string startRigId = "";

        [Tooltip("Seconds used to blend when switching modes.")]
        [Min(0f)] public float blendTime = 0.35f;

        [Header("Input")]
        [Tooltip("Read look and zoom from the character input frame.")]
        public bool handleLookInput = true;

        [Tooltip("Cycle to the next rig when the toggle camera action is pressed.")]
        public bool allowToggleInput = true;

        public CharacterAction toggleAction = CharacterAction.ToggleCamera;

        [Tooltip("Mouse wheel zoom step in units, applied by rigs that support zooming.")]
        [Min(0f)] public float zoomStep = 2f;

        [Header("Cursor")]
        public bool manageCursor = true;

        [Tooltip("Releases the cursor when the cancel / escape action is pressed.")]
        public bool releaseCursorOnCancel = true;

        [Header("Mode Presets")]
        [Tooltip("Rotate the character body with the camera yaw. Turned on by first person and shooter modes.")]
        public bool rotateBodyWithCamera;

        readonly List<AimRequest> _aimRequests = new List<AimRequest>(2);
        readonly List<ScaleRequest> _scaleRequests = new List<ScaleRequest>(2);

        struct AimRequest
        {
            public object Source;
            public float Ratio;
            public float FovDelta;
            public float DistanceScale;
            public Vector3 ShoulderOffset;
            public float Timestamp;
        }

        struct ScaleRequest
        {
            public object Source;
            public float Scale;
            public float Timestamp;
        }

        CharacterCameraRig _activeRig;
        bool _cursorLocked;

        public CharacterContext Context { get; internal set; }

        /// <summary>Currently active rig, null before the switcher wakes up.</summary>
        public CharacterCameraRig ActiveRig { get { return _activeRig; } }

        public Camera ActiveCamera
        {
            get { return _activeRig != null ? _activeRig.Camera : null; }
        }

        public Transform ActiveCameraTransform
        {
            get { return _activeRig != null ? _activeRig.CameraTransform : null; }
        }

        public IReadOnlyList<CharacterCameraRig> Rigs { get { return rigs; } }

        /// <summary>True while the active rig wants the cursor locked.</summary>
        public bool CursorLocked { get { return _cursorLocked; } }

        public event Action<CharacterCameraRig, CharacterCameraRig> RigChanged;

        void Awake()
        {
            if (rigs == null) rigs = new List<CharacterCameraRig>();
            if (rigs.Count == 0)
            {
                CharacterCameraRig[] found = GetComponentsInChildren<CharacterCameraRig>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null && !rigs.Contains(found[i])) rigs.Add(found[i]);
                }
            }
        }

        void Start()
        {
            if (rigs.Count == 0) return;

            CharacterCameraRig start = null;

            if (!string.IsNullOrEmpty(startRigId))
            {
                for (int i = 0; i < rigs.Count; i++)
                {
                    if (rigs[i] != null && rigs[i].rigId == startRigId)
                    {
                        start = rigs[i];
                        break;
                    }
                }
            }

            if (start == null)
            {
                int bestPriority = int.MinValue;
                for (int i = 0; i < rigs.Count; i++)
                {
                    if (rigs[i] == null) continue;
                    if (rigs[i].priority >= bestPriority)
                    {
                        bestPriority = rigs[i].priority;
                        start = rigs[i];
                    }
                }
            }

            SetRig(start, 0f);
        }

        void LateUpdate()
        {
            if (Context == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            CharacterInputFrame frame = Context.Input;

            if (allowToggleInput && frame.ConsumePressed(toggleAction))
            {
                NextRig();
            }

            if (manageCursor && releaseCursorOnCancel && frame.cancelPressed)
            {
                SetCursorLocked(false);
            }

            if (handleLookInput) ApplyLookInput(frame, dt);

            ApplyRequests();

            // Keep the look pivots in sync with the active rig (used by user supplied head transforms).
            CharacterLook look = Context.Look;
            if (look != null && _activeRig != null)
            {
                if (look.yawPivot != _activeRig.yawPivot || look.pitchPivot != _activeRig.pitchPivot)
                {
                    look.SetPivots(_activeRig.yawPivot, _activeRig.pitchPivot, false);
                }
            }
        }

        // ---------------------------------------------------------------- rig control

        /// <summary>Switches to a rig with an optional blend time.</summary>
        public void SetRig(CharacterCameraRig rig, float blend = -1f)
        {
            if (rig == null || rig == _activeRig) return;

            CharacterCameraRig previous = _activeRig;
            if (previous != null) previous.Deactivate();

            _activeRig = rig;
            rig.Activate(Context, blend >= 0f ? blend : blendTime);

            if (manageCursor) SetCursorLocked(rig.wantsCursorLocked);

            var handler = RigChanged;
            if (handler != null) handler(previous, rig);
        }

        public void SetRig(int index, float blend = -1f)
        {
            if (index < 0 || index >= rigs.Count) return;
            SetRig(rigs[index], blend);
        }

        /// <summary>Switches by rig id, returns false when no rig matches.</summary>
        public bool SetRigById(string id, float blend = -1f)
        {
            for (int i = 0; i < rigs.Count; i++)
            {
                if (rigs[i] != null && rigs[i].rigId == id)
                {
                    SetRig(rigs[i], blend);
                    return true;
                }
            }
            return false;
        }

        public void NextRig(float blend = -1f)
        {
            if (rigs.Count < 2) return;
            int index = rigs.IndexOf(_activeRig);
            SetRig(rigs[(index + 1) % rigs.Count], blend);
        }

        public void PreviousRig(float blend = -1f)
        {
            if (rigs.Count < 2) return;
            int index = rigs.IndexOf(_activeRig);
            if (index < 0) index = 0;
            SetRig(rigs[(index - 1 + rigs.Count) % rigs.Count], blend);
        }

        /// <summary>Snaps every rig to its target position. Call after teleporting the character.</summary>
        public void SnapAll()
        {
            for (int i = 0; i < rigs.Count; i++)
            {
                if (rigs[i] != null) rigs[i].Snap();
            }

            if (Context != null && Context.Look != null) Context.Look.Snap();
        }

        /// <summary>Registers a rig created at runtime.</summary>
        public void AddRig(CharacterCameraRig rig)
        {
            if (rig == null || rigs.Contains(rig)) return;
            rigs.Add(rig);
        }

        public void RemoveRig(CharacterCameraRig rig)
        {
            if (rig == null) return;
            if (_activeRig == rig) _activeRig = null;
            rigs.Remove(rig);
        }

        // ---------------------------------------------------------------- input

        void ApplyLookInput(CharacterInputFrame frame, float dt)
        {
            CharacterLook look = Context.Look;
            if (look == null) return;

            Vector2 lookDelta = frame.look;
            if (lookDelta.sqrMagnitude > 0f)
            {
                CharacterProfile profile = Context.Profile;
                float sensitivity = frame.lookFromStick ? profile.gamepadSensitivity : profile.mouseSensitivity;
                sensitivity *= CurrentSensitivityScale();

                float yawDelta = lookDelta.x * sensitivity * (profile.invertX ? -1f : 1f);
                float pitchDelta = -lookDelta.y * sensitivity * (profile.invertY ? -1f : 1f);

                look.AddYaw(yawDelta);
                look.AddPitch(pitchDelta);
            }

            if (_activeRig != null && _activeRig.overridePitchLimits)
            {
                look.pitch = Mathf.Clamp(look.pitch, _activeRig.pitchLimits.x, _activeRig.pitchLimits.y);
            }
        }

        /// <summary>Zoom value for the current frame, already scaled by the switcher.</summary>
        public float ZoomInput
        {
            get
            {
                if (Context == null) return 0f;
                return Context.Input.zoom * zoomStep;
            }
        }

        // ---------------------------------------------------------------- requests

        /// <summary>Aiming abilities push their blend here, the strongest request wins.</summary>
        public void SetAimState(object source, float ratio, float fovDelta, float distanceScale, Vector3 shoulderOffset)
        {
            float now = Time.time;
            for (int i = 0; i < _aimRequests.Count; i++)
            {
                if (_aimRequests[i].Source == source)
                {
                    AimRequest existing = _aimRequests[i];
                    existing.Ratio = ratio;
                    existing.FovDelta = fovDelta;
                    existing.DistanceScale = distanceScale;
                    existing.ShoulderOffset = shoulderOffset;
                    existing.Timestamp = now;
                    _aimRequests[i] = existing;
                    return;
                }
            }

            AimRequest request;
            request.Source = source;
            request.Ratio = ratio;
            request.FovDelta = fovDelta;
            request.DistanceScale = distanceScale;
            request.ShoulderOffset = shoulderOffset;
            request.Timestamp = now;
            _aimRequests.Add(request);
        }

        /// <summary>Scales look sensitivity, used by aiming, scopes and cutscenes.</summary>
        public void SetSensitivityScale(object source, float scale)
        {
            float now = Time.time;
            for (int i = 0; i < _scaleRequests.Count; i++)
            {
                if (_scaleRequests[i].Source == source)
                {
                    ScaleRequest existing = _scaleRequests[i];
                    existing.Scale = scale;
                    existing.Timestamp = now;
                    _scaleRequests[i] = existing;
                    return;
                }
            }

            ScaleRequest request;
            request.Source = source;
            request.Scale = scale;
            request.Timestamp = now;
            _scaleRequests.Add(request);
        }

        float CurrentSensitivityScale()
        {
            float scale = 1f;
            float now = Time.time;

            for (int i = _scaleRequests.Count - 1; i >= 0; i--)
            {
                if (now - _scaleRequests[i].Timestamp > 0.5f)
                {
                    _scaleRequests.RemoveAt(i);
                    continue;
                }
                scale *= _scaleRequests[i].Scale;
            }

            return Mathf.Clamp(scale, 0.01f, 4f);
        }

        void ApplyRequests()
        {
            if (_activeRig == null) return;

            float now = Time.time;
            float bestRatio = 0f;
            float fovDelta = 0f;
            float distanceScale = 1f;
            Vector3 shoulderOffset = Vector3.zero;

            for (int i = _aimRequests.Count - 1; i >= 0; i--)
            {
                AimRequest request = _aimRequests[i];
                if (now - request.Timestamp > 0.5f)
                {
                    _aimRequests.RemoveAt(i);
                    continue;
                }

                if (request.Ratio >= bestRatio)
                {
                    bestRatio = request.Ratio;
                    fovDelta = request.FovDelta;
                    distanceScale = request.DistanceScale;
                    shoulderOffset = request.ShoulderOffset;
                }
            }

            _activeRig.ApplyAim(bestRatio, fovDelta, distanceScale, shoulderOffset);
        }

        // ---------------------------------------------------------------- cursor

        /// <summary>Locks or releases the cursor.</summary>
        public void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>Re-applies the cursor state the active rig wants.</summary>
        public void RefreshCursor()
        {
            if (_activeRig == null) return;
            SetCursorLocked(_activeRig.wantsCursorLocked);
        }
    }
}