using System;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Looks for an <see cref="IInteractable"/> in front of the character (or the camera) and raises the
    /// interaction. Exposes everything a prompt UI needs: the current target, the prompt text and the hold
    /// progress.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Interaction")]
    public class InteractionAbility : ContinuousAbility
    {
        [Header("Detection")]
        [Tooltip("Layers that can contain interactables.")]
        public LayerMask interactionMask = ~0;

        [Tooltip("How far the character can reach.")]
        [Min(0.1f)] public float maxDistance = 3f;

        [Tooltip("Radius of the detection sphere, makes small objects easier to target.")]
        [Min(0.01f)] public float detectionRadius = 0.18f;

        [Tooltip("Interactables are usually trigger colliders, so triggers are included by default.")]
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Origin")]
        [Tooltip("Cast from the active camera (crosshair style) instead of the character.")]
        public bool useCameraOrigin = true;

        [Tooltip("Cast from the character's eyes instead of its centre.")]
        public bool useEyeHeight = true;

        [Tooltip("Offset applied to the cast origin in local space.")]
        public Vector3 originOffset;

        [Header("Input")]
        public CharacterAction action = CharacterAction.Interact;

        [Tooltip("When true the interact action is claimed exclusively, so other abilities cannot use it.")]
        public bool consumeInput = true;

        [Header("Hold")]
        [Tooltip("When true the ability itself handles hold to interact. Off lets user code do it with HoldProgress.")]
        public bool handleHold = true;

        readonly RaycastHit[] _hits = new RaycastHit[8];

        float _holdTimer;
        IInteractable _focused;

        /// <summary>Interactable currently in range and valid, null when there is none.</summary>
        public IInteractable Current { get; private set; }

        /// <summary>Prompt text of the current target, or null.</summary>
        public string CurrentPrompt { get; private set; }

        /// <summary>True while the character is looking at something it can use.</summary>
        public bool HasTarget { get { return Current != null; } }

        /// <summary>0..1 progress of a hold to interact.</summary>
        public float HoldProgress
        {
            get
            {
                if (Current == null || Current.HoldDuration <= 0f) return 0f;
                return Mathf.Clamp01(_holdTimer / Current.HoldDuration);
            }
        }

        /// <summary>True while the interact button is held on a valid target.</summary>
        public bool IsHolding { get; private set; }

        /// <summary>World position of the current target, for markers and VFX.</summary>
        public Vector3 TargetPosition { get; private set; }

        public event Action<IInteractable> TargetChanged;
        public event Action<IInteractable> Interacted;

        protected override void OnTick(float deltaTime)
        {
            Detect();

            HandleInput(deltaTime);
        }

        void Detect()
        {
            IInteractable previous = Current;
            Current = null;
            CurrentPrompt = null;

            Vector3 origin = GetOrigin();
            Vector3 direction = GetDirection();

            if (direction.sqrMagnitude < 1e-4f) direction = transform.forward;

            int count = Physics.SphereCastNonAlloc(origin, detectionRadius, direction, _hits, maxDistance, interactionMask, triggerInteraction);

            float bestDistance = float.MaxValue;
            IInteractable best = null;
            Vector3 bestPoint = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null) continue;

                // Ignore the character's own colliders.
                if (hit.collider.transform.IsChildOf(transform)) continue;

                IInteractable candidate = hit.collider.GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                if (!candidate.CanInteract(gameObject)) continue;
                if (candidate.GetPrompt(gameObject) == null) continue;

                float distance = hit.distance;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                    bestPoint = hit.point;
                }
            }

            Current = best;
            TargetPosition = best != null ? bestPoint : Vector3.zero;

            if (best != null)
            {
                CurrentPrompt = best.GetPrompt(gameObject);
            }

            if (!ReferenceEquals(previous, Current))
            {
                if (previous is InteractableBehaviour previousBehaviour) previousBehaviour.SetFocused(false);
                if (Current is InteractableBehaviour currentBehaviour) currentBehaviour.SetFocused(true);

                _holdTimer = 0f;
                IsHolding = false;

                var handler = TargetChanged;
                if (handler != null) handler(Current);
            }
        }

        void HandleInput(float deltaTime)
        {
            if (Current == null)
            {
                _holdTimer = 0f;
                IsHolding = false;
                return;
            }

            if (Input == null) return;

            bool pressed = consumeInput ? Input.ConsumePressed(action) : Input.IsPressed(action);
            bool held = Input.IsHeld(action);

            if (!handleHold)
            {
                IsHolding = held;
                return;
            }

            float required = Current.HoldDuration;

            if (required <= 0f)
            {
                IsHolding = false;
                if (pressed) Complete();
                return;
            }

            if (pressed) _holdTimer = 0f;

            if (held)
            {
                IsHolding = true;
                _holdTimer += deltaTime;

                if (_holdTimer >= required) Complete();
            }
            else
            {
                IsHolding = false;
                _holdTimer = Mathf.Max(0f, _holdTimer - deltaTime * 2f);
            }
        }

        /// <summary>Completes the interaction with the current target from code.</summary>
        public void Complete()
        {
            IInteractable target = Current;
            if (target == null) return;

            _holdTimer = 0f;
            target.Interact(gameObject);
            Detect();

            var handler = Interacted;
            if (handler != null) handler(target);
        }

        Vector3 GetOrigin()
        {
            Vector3 origin;

            if (useCameraOrigin && Context != null && Context.CameraRig != null && Context.CameraRig.ActiveCameraTransform != null)
            {
                origin = Context.CameraRig.ActiveCameraTransform.position;
            }
            else if (useEyeHeight && Motor != null)
            {
                origin = Motor.ColliderCenter;
            }
            else
            {
                origin = Motor != null ? Motor.ColliderCenter : transform.position;
            }

            return origin + transform.TransformDirection(originOffset);
        }

        Vector3 GetDirection()
        {
            if (useCameraOrigin && Context != null && Context.CameraRig != null && Context.CameraRig.ActiveCameraTransform != null)
            {
                return Context.CameraRig.ActiveCameraTransform.forward;
            }

            if (Look != null)
            {
                return Look.CameraRotation * Vector3.forward;
            }

            return transform.forward;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.7f);
            Vector3 origin = Application.isPlaying ? GetOrigin() : transform.position + transform.up;
            Vector3 direction = Application.isPlaying ? GetDirection() : transform.forward;
            Gizmos.DrawWireSphere(origin + direction * maxDistance, detectionRadius);
            Gizmos.DrawLine(origin, origin + direction * maxDistance);
        }
#endif
    }
}