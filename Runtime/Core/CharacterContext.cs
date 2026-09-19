using System;
using System.Collections.Generic;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// The hub of a character. It owns the input frame, ticks the abilities in a deterministic order,
    /// caches the shared components and exposes them to abilities and user code.
    /// Add one CharacterContext per character, anywhere on the character hierarchy.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public class CharacterContext : MonoBehaviour
    {
        public enum InputSourceMode
        {
            /// <summary>Pick the highest priority source that has a device producing input.</summary>
            Auto = 0,
            /// <summary>Always use the explicitly assigned source.</summary>
            Explicit = 1,
            /// <summary>Nothing is polled, another system writes into <see cref="Input"/>.</summary>
            Scripted = 2
        }

        [Header("Data")]
        [Tooltip("Optional. Left empty the built in runtime defaults are used, which is enough to get moving.")]
        [SerializeField] CharacterProfile profile;

        [Header("Components")]
        [SerializeField] MotorBase motor;
        [SerializeField] CharacterLook look;
        [SerializeField] CharacterResources resources;
        [SerializeField] CharacterCameraRigSwitcher cameraRig;

        [Header("Anchors")]
        [Tooltip("Where cameras orbit and animation aims. Created automatically when empty.")]
        [SerializeField] Transform cameraTarget;

        [Tooltip("Height of the auto created camera target, as a fraction of the character height.")]
        [Range(0f, 1.5f)] public float cameraTargetHeight = 0.85f;

        [Header("Input")]
        [SerializeField] InputSourceMode inputSourceMode = InputSourceMode.Auto;
        [SerializeField] CharacterInputSourceBehaviour explicitInputSource;

        [Header("Abilities")]
        [Tooltip("Collect every CharacterAbility found on this object and its children at Awake.")]
        [SerializeField] bool autoCollectAbilities = true;
        [SerializeField] List<CharacterAbility> abilities = new List<CharacterAbility>();

        readonly List<CharacterAbility> _sorted = new List<CharacterAbility>(8);
        readonly CharacterInputFrame _inputFrame = new CharacterInputFrame();
        ICharacterInputSource _activeSource;
        bool _bound;

        /// <summary>Every character currently enabled in the scene.</summary>
        public static readonly List<CharacterContext> All = new List<CharacterContext>();

        /// <summary>Convenience accessor for the first registered character, updated by the static list.</summary>
        public static CharacterContext Primary
        {
            get { return All.Count > 0 ? All[0] : null; }
        }

        public CharacterProfile Profile
        {
            get { return profile != null ? profile : CharacterProfile.Default; }
        }

        public CharacterInputFrame Input { get { return _inputFrame; } }
        public MotorBase Motor { get { return motor; } }
        public CharacterLook Look { get { return look; } }
        public CharacterResources Resources { get { return resources; } }
        public CharacterCameraRigSwitcher CameraRig { get { return cameraRig; } }

        public IReadOnlyList<CharacterAbility> Abilities { get { return _sorted; } }

        public bool IsDead
        {
            get { return resources != null && resources.IsDead; }
        }

        public float Height
        {
            get { return motor != null ? motor.Height : 2f; }
        }

        public Vector3 CameraTargetPosition
        {
            get { return cameraTarget != null ? cameraTarget.position : transform.position + Vector3.up * Height * cameraTargetHeight; }
        }

        public Transform CameraTarget { get { return cameraTarget; } }

        /// <summary>True when movement input has been produced this frame.</summary>
        public bool HasMovementInput
        {
            get { return _inputFrame.HasMovementInput; }
        }

        public event Action<CharacterContext> Bound;
        public event Action<CharacterContext> Died;
        public event Action<CharacterContext, CharacterAbility, AbilityState, AbilityState> AbilityStateChanged;
        public event Action<ICharacterInputSource, ICharacterInputSource> InputSourceChanged;

        void Awake()
        {
            ResolveReferences();
            ResolveAbilities();
            EnsureCameraTarget();
            SetProfileOnComponents();
            _bound = true;
            var handler = Bound;
            if (handler != null) handler(this);
        }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            if (resources != null) resources.Died += HandleDeath;
        }

        void OnDisable()
        {
            All.Remove(this);
            if (resources != null) resources.Died -= HandleDeath;
            if (_activeSource != null)
            {
                _activeSource.OnDeactivated();
                _activeSource = null;
            }
        }

        void HandleDeath(CharacterResources r)
        {
            var handler = Died;
            if (handler != null) handler(this);
        }

        // ---------------------------------------------------------------- setup

        void ResolveReferences()
        {
            if (motor == null) motor = GetComponentInChildren<MotorBase>(true);
            if (look == null) look = GetComponentInChildren<CharacterLook>(true);
            if (resources == null) resources = GetComponentInChildren<CharacterResources>(true);
            if (cameraRig == null) cameraRig = GetComponentInChildren<CharacterCameraRigSwitcher>(true);
            if (explicitInputSource == null) explicitInputSource = GetComponentInChildren<CharacterInputSourceBehaviour>(true);
        }

        void ResolveAbilities()
        {
            _sorted.Clear();

            if (autoCollectAbilities)
            {
                CharacterAbility[] found = GetComponentsInChildren<CharacterAbility>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] == null || _sorted.Contains(found[i])) continue;
                    _sorted.Add(found[i]);
                }
            }

            for (int i = 0; i < abilities.Count; i++)
            {
                CharacterAbility ability = abilities[i];
                if (ability == null || _sorted.Contains(ability)) continue;
                _sorted.Add(ability);
            }

            _sorted.Sort(CompareAbilities);
        }

        static int CompareAbilities(CharacterAbility a, CharacterAbility b)
        {
            int order = a.executionOrder.CompareTo(b.executionOrder);
            if (order != 0) return order;
            return string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
        }

        void EnsureCameraTarget()
        {
            if (cameraTarget == null)
            {
                Transform existing = transform.Find("CameraTarget");
                if (existing != null)
                {
                    cameraTarget = existing;
                }
                else
                {
                    GameObject target = new GameObject("CameraTarget");
                    target.transform.SetParent(transform, false);
                    target.transform.localPosition = Vector3.up * Mathf.Max(0.1f, Height * cameraTargetHeight);
                    cameraTarget = target.transform;
                }
            }
        }

        void SetProfileOnComponents()
        {
            if (motor != null) motor.Context = this;
            if (look != null) look.Context = this;
            if (resources != null) resources.Context = this;
            if (cameraRig != null) cameraRig.Context = this;

            for (int i = 0; i < _sorted.Count; i++)
            {
                if (_sorted[i] != null)
                {
                    _sorted[i].Bind(this);
                    _sorted[i].StateChanged -= HandleAbilityStateChanged;
                    _sorted[i].StateChanged += HandleAbilityStateChanged;
                }
            }
        }

        void HandleAbilityStateChanged(CharacterAbility ability, AbilityState previous, AbilityState current)
        {
            var handler = AbilityStateChanged;
            if (handler != null) handler(this, ability, previous, current);
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _inputFrame.Clear();
            PollInput(dt);

            for (int i = 0; i < _sorted.Count; i++)
            {
                CharacterAbility ability = _sorted[i];
                if (ability == null) continue;
                ability.TickInternal(dt);
            }
        }

        void PollInput(float dt)
        {
            if (inputSourceMode == InputSourceMode.Scripted)
            {
                OnScriptedInput(dt);
                return;
            }

            ICharacterInputSource source = SelectSource();
            if (source != _activeSource)
            {
                ICharacterInputSource previous = _activeSource;
                if (previous != null) previous.OnDeactivated();
                _activeSource = source;
                if (source != null) source.OnActivated();

                var handler = InputSourceChanged;
                if (handler != null) handler(previous, source);
            }

            if (_activeSource != null) _activeSource.Poll(_inputFrame, dt);
        }

        /// <summary>Override point for AI and network driven characters.</summary>
        protected virtual void OnScriptedInput(float deltaTime)
        {
        }

        ICharacterInputSource SelectSource()
        {
            if (inputSourceMode == InputSourceMode.Explicit)
            {
                return explicitInputSource != null && explicitInputSource.IsAvailable ? explicitInputSource : null;
            }

            ICharacterInputSource best = null;
            int bestScore = int.MinValue;

            CharacterInputSourceBehaviour[] sources = GetComponentsInChildren<CharacterInputSourceBehaviour>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                CharacterInputSourceBehaviour source = sources[i];
                if (source == null || !source.IsAvailable) continue;

                // Sources that produced input recently always win, priority breaks the tie.
                int score = source.Priority + (source.IsActive ? 10000 : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = source;
                }
            }

            return best;
        }

        public string ActiveInputDeviceName
        {
            get { return _activeSource != null ? _activeSource.DeviceName : "None"; }
        }

        // ---------------------------------------------------------------- public API

        /// <summary>Registers an ability created at runtime.</summary>
        public void RegisterAbility(CharacterAbility ability)
        {
            if (ability == null) return;

            if (!_sorted.Contains(ability))
            {
                _sorted.Add(ability);
                _sorted.Sort(CompareAbilities);
            }

            if (_bound)
            {
                ability.Bind(this);
                // Subscribe once per ability, even when it registers more than once.
                ability.StateChanged -= HandleAbilityStateChanged;
                ability.StateChanged += HandleAbilityStateChanged;
            }
        }

        public void UnregisterAbility(CharacterAbility ability)
        {
            if (ability == null) return;
            ability.StateChanged -= HandleAbilityStateChanged;
            _sorted.Remove(ability);
        }

        /// <summary>Finds the first ability of the requested type.</summary>
        public T GetAbility<T>() where T : CharacterAbility
        {
            for (int i = 0; i < _sorted.Count; i++)
            {
                T typed = _sorted[i] as T;
                if (typed != null) return typed;
            }
            return null;
        }

        public bool HasAbility<T>() where T : CharacterAbility
        {
            return GetAbility<T>() != null;
        }

        /// <summary>Finds every ability of the requested type, without allocating when the list is reused.</summary>
        public void GetAbilities<T>(List<T> results) where T : CharacterAbility
        {
            if (results == null) return;
            results.Clear();
            for (int i = 0; i < _sorted.Count; i++)
            {
                T typed = _sorted[i] as T;
                if (typed != null) results.Add(typed);
            }
        }

        /// <summary>Enables or disables every ability of a type at runtime (granting and revoking abilities).</summary>
        public void SetAbilityEnabled<T>(bool value) where T : CharacterAbility
        {
            for (int i = 0; i < _sorted.Count; i++)
            {
                T typed = _sorted[i] as T;
                if (typed != null) typed.SetAbilityEnabled(value);
            }
        }

        /// <summary>Switches every movement related ability on or off (cutscenes, dialogue, menues).</summary>
        public void SetMovementLocked(bool locked)
        {
            if (motor != null) motor.movementLocked = locked;
        }

        /// <summary>Teleports the character and optionally keeps the camera in sync.</summary>
        public void Teleport(Vector3 position, Quaternion rotation, bool keepVelocity = false, bool snapCamera = true)
        {
            if (motor != null) motor.Teleport(position, rotation, keepVelocity);
            if (snapCamera)
            {
                if (look != null)
                {
                    look.Snap();
                    look.Apply();
                }
                if (cameraRig != null) cameraRig.SnapAll();
            }
        }

        public void Teleport(Vector3 position, bool keepVelocity = false, bool snapCamera = true)
        {
            Teleport(position, transform.rotation, keepVelocity, snapCamera);
        }

        /// <summary>Applies a full heal and clears cooldowns, used on respawn.</summary>
        public void ResetCharacter()
        {
            if (resources != null) resources.RefillAll();
            for (int i = 0; i < _sorted.Count; i++)
            {
                if (_sorted[i] != null) _sorted[i].ClearCooldown();
            }
        }

        /// <summary>Requests a look reset through the active input source.</summary>
        public void ResetLookInput()
        {
            if (_activeSource != null) _activeSource.ResetLook();
        }

        // ---------------------------------------------------------------- editor helpers

        void OnValidate()
        {
            if (abilities == null) abilities = new List<CharacterAbility>();
        }
    }
}