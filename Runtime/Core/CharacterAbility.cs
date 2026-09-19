using System;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>Lifecycle of an ability.</summary>
    public enum AbilityState
    {
        /// <summary>Not ticking. Either switched off in the inspector or blocked by a rule.</summary>
        Disabled = 0,
        /// <summary>Ticking, waiting for its trigger condition.</summary>
        Ready = 1,
        /// <summary>Currently running (dash in progress, crouch held, flight on).</summary>
        Active = 2,
        /// <summary>Finished, waiting for the cooldown to expire.</summary>
        Cooldown = 3
    }

    /// <summary>
    /// Base class of every character ability. Abilities never run their own Update, the
    /// <see cref="CharacterContext"/> ticks them in a deterministic order, which is what makes
    /// movement layering predictable.
    /// </summary>
    public abstract class CharacterAbility : MonoBehaviour
    {
        [Header("Ability")]
        [Tooltip("Abilities tick from the lowest number to the highest. Locomotion 0, modifiers 100, jump 200, dash 300, modes 400.")]
        public int executionOrder;

        [Tooltip("Priority used when this ability submits a movement request to the motor. Higher wins.")]
        public int motorPriority;

        [Tooltip("When true the ability stops running while the character is dead.")]
        public bool blockedByDeath = true;

        [Tooltip("When true the ability keeps running while the motor is locked (cutscene, dialogue).")]
        public bool runWhileMovementLocked;

        [Header("Cost")]
        public ResourceType costResource = ResourceType.Stamina;

        [Tooltip("Resource spent when the ability triggers.")]
        [Min(0f)] public float activationCost;

        [Tooltip("Resource drained per second while the ability is active.")]
        [Min(0f)] public float drainPerSecond;

        float _cooldownRemaining;
        AbilityState _state = AbilityState.Ready;

        public CharacterContext Context { get; internal set; }
        public AbilityState State { get { return _state; } }
        public float CooldownRemaining { get { return _cooldownRemaining; } }
        public float CooldownNormalized { get { return _cooldownDuration > 0f ? Mathf.Clamp01(_cooldownRemaining / _cooldownDuration) : 0f; } }

        float _cooldownDuration;

        protected CharacterInputFrame Input { get { return Context != null ? Context.Input : null; } }
        protected CharacterProfile Profile { get { return Context != null ? Context.Profile : CharacterProfile.Default; } }
        protected MotorBase Motor { get { return Context != null ? Context.Motor : null; } }
        protected CharacterLook Look { get { return Context != null ? Context.Look : null; } }
        protected CharacterResources Resources { get { return Context != null ? Context.Resources : null; } }

        /// <summary>True when the ability is switched on and its blocking rules allow it to run.</summary>
        public virtual bool IsRunning
        {
            get
            {
                if (!enabled || !isActiveAndEnabled) return false;
                if (Context == null) return false;
                if (blockedByDeath && Resources != null && Resources.IsDead) return false;
                if (!runWhileMovementLocked && Motor != null && Motor.movementLocked) return false;
                return true;
            }
        }

        public event Action<CharacterAbility, AbilityState, AbilityState> StateChanged;

        /// <summary>
        /// Registers with the context. Abilities added at runtime are picked up automatically, so a
        /// component can be attached mid-game and start working the same frame.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (Context == null) Context = GetComponentInParent<CharacterContext>(true);
            if (Context != null) Context.RegisterAbility(this);
        }

        /// <summary>Called once when the context binds this ability.</summary>
        protected virtual void OnBind() { }

        /// <summary>Called every frame by the context while <see cref="IsRunning"/> is true.</summary>
        protected abstract void OnTick(float deltaTime);

        /// <summary>Called when the ability is switched off at runtime.</summary>
        protected virtual void OnDisabled() { }

        /// <summary>Called when the character dies, or when <see cref="blockedByDeath"/> starts applying.</summary>
        protected virtual void OnBlocked() { }

        internal void Bind(CharacterContext context)
        {
            Context = context;
            OnBind();
            SetState(AbilityState.Ready);
        }

        internal void TickInternal(float deltaTime)
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);
                if (_cooldownRemaining <= 0f && _state == AbilityState.Cooldown) SetState(AbilityState.Ready);
            }

            if (!IsRunning)
            {
                if (_state != AbilityState.Disabled)
                {
                    OnBlocked();
                    SetState(AbilityState.Disabled);
                }
                return;
            }

            if (_state == AbilityState.Disabled) SetState(AbilityState.Ready);

            OnTick(deltaTime);
        }

        /// <summary>Switches the ability on or off at runtime.</summary>
        public void SetAbilityEnabled(bool value)
        {
            if (enabled == value) return;
            enabled = value;
            if (!value)
            {
                OnDisabled();
                SetState(AbilityState.Disabled);
            }
            else
            {
                SetState(AbilityState.Ready);
            }
        }

        protected void SetState(AbilityState newState)
        {
            if (_state == newState) return;
            AbilityState previous = _state;
            _state = newState;

            var handler = StateChanged;
            if (handler != null) handler(this, previous, newState);
        }

        /// <summary>Starts (or restarts) the ability cooldown.</summary>
        public void StartCooldown(float duration)
        {
            _cooldownDuration = Mathf.Max(0f, duration);
            _cooldownRemaining = _cooldownDuration;
            if (_cooldownRemaining > 0f) SetState(AbilityState.Cooldown);
        }

        public void ClearCooldown()
        {
            _cooldownRemaining = 0f;
            if (_state == AbilityState.Cooldown) SetState(AbilityState.Ready);
        }

        /// <summary>Checks the cost and spends it. Returns false when the character cannot pay.</summary>
        protected bool TryPayCost(float multiplier = 1f)
        {
            if (activationCost <= 0f || Resources == null) return true;
            return Resources.TrySpend(costResource, activationCost * multiplier);
        }

        /// <summary>Continuous drain used by sprint, glide and flight.</summary>
        protected bool TryDrain(float deltaTime, float multiplier = 1f)
        {
            if (drainPerSecond <= 0f || Resources == null) return true;
            return Resources.Drain(costResource, drainPerSecond * multiplier * deltaTime);
        }

        /// <summary>Convenience: submits a planar movement request to the motor with this ability's priority.</summary>
        protected void SubmitMotion(Vector3 worldVelocity, bool useGravity = true, bool drivesVertical = false, bool skipGroundSnap = false)
        {
            if (Motor == null) return;
            Motor.SubmitRequest(this, motorPriority, worldVelocity, useGravity, drivesVertical, skipGroundSnap);
        }

        protected virtual void Reset()
        {
            executionOrder = DefaultExecutionOrderFor(GetType());
            motorPriority = DefaultMotorPriorityFor(GetType());
        }

        /// <summary>Sensible defaults so a freshly added ability behaves well without tuning.</summary>
        public static int DefaultExecutionOrderFor(Type type)
        {
            if (type == null) return 0;
            string name = type.Name;
            if (name.Contains("Orientation")) return 50;
            if (name.Contains("Sprint")) return 100;
            if (name.Contains("Crouch")) return 110;
            if (name.Contains("Glide")) return 120;
            if (name.Contains("Jump")) return 200;
            if (name.Contains("Dash")) return 300;
            if (name.Contains("Interaction")) return 350;
            if (name.Contains("Fly")) return 400;
            if (name.Contains("Swim")) return 410;
            if (name.Contains("Ladder")) return 420;
            if (name.Contains("Locomotion")) return 0;
            return 0;
        }

        /// <summary>Motor priorities are ordered so that modes beat locomotion and dash beats everything.</summary>
        public static int DefaultMotorPriorityFor(Type type)
        {
            if (type == null) return 0;
            string name = type.Name;
            if (name.Contains("Locomotion")) return 0;
            if (name.Contains("Fly")) return 500;
            if (name.Contains("Swim")) return 500;
            if (name.Contains("Ladder")) return 500;
            if (name.Contains("Dash")) return 900;
            if (name.Contains("Jump")) return 100;
            return 0;
        }
    }

    /// <summary>An ability that runs continuously: locomotion, sprint modifier, crouch, glide, aim.</summary>
    public abstract class ContinuousAbility : CharacterAbility
    {
    }

    /// <summary>An ability that fires once per input press, with optional cost, duration and cooldown.</summary>
    public abstract class InputTriggeredAbility : CharacterAbility
    {
        [Header("Trigger")]
        [Tooltip("Action that fires this ability.")]
        public CharacterAction action = CharacterAction.Jump;

        [Tooltip("When true the action is only available while standing on the ground.")]
        public bool requiresGrounded;

        [Tooltip("When true the action is only available while airborne.")]
        public bool requiresAirborne;

        [Tooltip("Seconds the ability stays active after triggering. 0 = fires and finishes immediately.")]
        [Min(0f)] public float activeDuration;

        [Tooltip("Cooldown applied after the ability finishes.")]
        [Min(0f)] public float cooldown;

        [Tooltip("The ability may retrigger while it is already active (used by multi jump).")]
        public bool allowRetriggerWhileActive;

        float _activeTimer;
        float _lastTriggerTime = -100f;

        /// <summary>Time since the ability last fired, useful for coyote time and buffers.</summary>
        public float TimeSinceTrigger { get { return Time.time - _lastTriggerTime; } }

        public bool IsActive { get { return _activeTimer > 0f; } }

        /// <summary>True when grounded / airborne rules are satisfied.</summary>
        protected virtual bool MeetsActivationConditions
        {
            get
            {
                if (Motor == null) return true;
                if (requiresGrounded && !Motor.IsGrounded) return false;
                if (requiresAirborne && Motor.IsGrounded) return false;
                return true;
            }
        }

        protected override void OnTick(float deltaTime)
        {
            if (_activeTimer > 0f)
            {
                _activeTimer = Mathf.Max(0f, _activeTimer - deltaTime);
                if (_activeTimer <= 0f)
                {
                    OnActiveEnd();
                    SetState(AbilityState.Ready);
                }
            }

            bool wantsToTrigger = WantsToTrigger();

            if (wantsToTrigger && CanTrigger())
            {
                _lastTriggerTime = Time.time;
                OnTrigger();

                if (activeDuration > 0f)
                {
                    _activeTimer = activeDuration;
                    SetState(AbilityState.Active);
                }
                else
                {
                    OnActiveEnd();
                }

                if (cooldown > 0f) StartCooldown(cooldown);
                return;
            }

            if (_activeTimer > 0f) OnActiveTick(deltaTime);
        }

        /// <summary>Default trigger condition: the action was pressed and has not been claimed by another ability.</summary>
        protected virtual bool WantsToTrigger()
        {
            if (Input == null) return false;
            if (IsActive && !allowRetriggerWhileActive) return false;
            return Input.ConsumePressed(action);
        }

        /// <summary>Extra rules on top of the grounded / airborne checks.</summary>
        protected virtual bool CanTrigger()
        {
            if (State == AbilityState.Cooldown) return false;
            if (!MeetsActivationConditions) return false;
            return true;
        }

        /// <summary>Fires the ability. Spend the cost and start the cooldown here if the timing matters.</summary>
        protected abstract void OnTrigger();

        /// <summary>Called every frame while the ability is active, for abilities with a duration.</summary>
        protected virtual void OnActiveTick(float deltaTime) { }

        /// <summary>Called when the active window ends, and immediately for instant abilities.</summary>
        protected virtual void OnActiveEnd() { }

        /// <summary>Ends the active window early (dash hitting a wall, ladder reaching the top).</summary>
        protected void FinishActive()
        {
            _activeTimer = 0f;
            OnActiveEnd();
            SetState(AbilityState.Ready);
        }
    }

    /// <summary>An ability that switches between off and on, such as flight or noclip.</summary>
    public abstract class ToggleAbility : CharacterAbility
    {
        [Header("Toggle")]
        public CharacterAction action = CharacterAction.Fly;

        [Tooltip("Cooldown applied after switching the ability off.")]
        [Min(0f)] public float cooldown;

        public bool IsOn { get; private set; }

        protected override void OnTick(float deltaTime)
        {
            if (Input != null && Input.ConsumePressed(action))
            {
                if (IsOn) TurnOff(); else TurnOn();
            }

            if (IsOn) OnToggleTick(deltaTime);
        }

        void TurnOn()
        {
            if (State == AbilityState.Cooldown) return;
            if (activationCost > 0f && !TryPayCost()) return;

            IsOn = true;
            SetState(AbilityState.Active);
            OnTurnedOn();
        }

        void TurnOff()
        {
            IsOn = false;
            SetState(AbilityState.Ready);
            OnTurnedOff();
            if (cooldown > 0f) StartCooldown(cooldown);
        }

        /// <summary>Forces the ability off without firing the toggle input.</summary>
        public void ForceOff()
        {
            if (!IsOn) return;
            TurnOff();
        }

        /// <summary>Forces the ability on.</summary>
        public void ForceOn()
        {
            if (IsOn) return;
            IsOn = true;
            SetState(AbilityState.Active);
            OnTurnedOn();
        }

        protected abstract void OnTurnedOn();
        protected abstract void OnTurnedOff();
        protected virtual void OnToggleTick(float deltaTime) { }
    }
}