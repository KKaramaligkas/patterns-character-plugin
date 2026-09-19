using UnityEngine;
using UnityEngine.Events;

namespace Patterns.Character
{
    /// <summary>
    /// Anything the character can interact with. Implement this on doors, chests, NPCs, pickups,
    /// or reuse <see cref="InteractableBehaviour"/> for the common case.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Text shown in the prompt, for example "Open". Return null to hide the prompt.</summary>
        string GetPrompt(GameObject interactor);

        /// <summary>False when the interaction is currently unavailable (locked, no key, done already).</summary>
        bool CanInteract(GameObject interactor);

        /// <summary>Seconds the button must be held. 0 = instant.</summary>
        float HoldDuration { get; }

        /// <summary>Called when the interaction completes.</summary>
        void Interact(GameObject interactor);
    }

    /// <summary>Ready to use <see cref="IInteractable"/> with a UnityEvent hookup.</summary>
    [AddComponentMenu("Patterns/Character/Interaction/Interactable")]
    public class InteractableBehaviour : MonoBehaviour, IInteractable
    {
        [Header("Prompt")]
        [Tooltip("Text shown while the character looks at this object.")]
        public string prompt = "Interact";

        [Tooltip("When true the prompt and the interaction are disabled after the first use.")]
        public bool oneShot;

        [Tooltip("Seconds the player must hold the button. 0 = single press.")]
        [Min(0f)] public float holdDuration;

        [Tooltip("Cooldown before the object can be used again.")]
        [Min(0f)] public float cooldown;

        [Header("Availability")]
        [Tooltip("Extra condition evaluated from code, for example an inventory check.")]
        public UnityEvent canInteractCheck;

        [Header("Events")]
        public UnityEvent onInteract;
        public UnityEvent onFocus;
        public UnityEvent onBlur;

        bool _used;
        float _lastUseTime = -100f;

        /// <summary>True when the object has been used and is a one shot.</summary>
        public bool WasUsed { get { return _used; } }

        public virtual string GetPrompt(GameObject interactor)
        {
            if (!CanInteract(interactor)) return null;
            return prompt;
        }

        public virtual bool CanInteract(GameObject interactor)
        {
            if (oneShot && _used) return false;
            if (Time.time - _lastUseTime < cooldown) return false;
            return true;
        }

        public virtual float HoldDuration { get { return holdDuration; } }

        public virtual void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            _used = true;
            _lastUseTime = Time.time;

            var handler = onInteract;
            if (handler != null) handler.Invoke();
        }

        /// <summary>Called by the interaction ability when the object gains / loses focus.</summary>
        public virtual void SetFocused(bool focused)
        {
            if (focused)
            {
                var handler = onFocus;
                if (handler != null) handler.Invoke();
            }
            else
            {
                var handler = onBlur;
                if (handler != null) handler.Invoke();
            }
        }

        /// <summary>Resets a one shot object so it can be used again.</summary>
        public virtual void ResetInteractable()
        {
            _used = false;
            _lastUseTime = -100f;
        }
    }
}