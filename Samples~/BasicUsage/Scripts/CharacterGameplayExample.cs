using Patterns.Character;
using UnityEngine;

namespace Patterns.Character.Samples
{
    /// <summary>
    /// Small helpers that show the API from gameplay code: camera mode hotkeys, teleporting, locking
    /// movement during dialogue, granting and revoking abilities, and reacting to character events.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Samples/Character Gameplay Example")]
    public class CharacterGameplayExample : MonoBehaviour
    {
        public CharacterContext context;

        void Start()
        {
            if (context == null) context = GetComponentInParent<CharacterContext>();
            if (context == null) return;

            // ---- events
            if (context.Motor != null) context.Motor.Landed += info => Debug.LogFormat("Landed at {0:0.00} m/s", info.ImpactSpeed);
            if (context.Resources != null) context.Resources.Died += r => Debug.Log("Character died");
            context.Died += c => Debug.Log("The context says the character is dead");

            // ---- camera modes are available at runtime
            if (context.CameraRig != null) context.CameraRig.RigChanged += (previous, current) => Debug.Log("Camera mode: " + current.rigId);
        }

        void Update()
        {
            if (context == null) return;

            // 1 / 2 / 3 switch camera modes.
            if (Input.GetKeyDown(KeyCode.Alpha1)) context.CameraRig.SetRigById("First Person");
            if (Input.GetKeyDown(KeyCode.Alpha2)) context.CameraRig.SetRigById("Third Person");
            if (Input.GetKeyDown(KeyCode.Alpha3)) context.CameraRig.SetRigById("Top Down");

            // Press R to teleport and snap the camera along.
            if (Input.GetKeyDown(KeyCode.R)) context.Teleport(new Vector3(0f, 0.5f, 10f));

            // Press L to freeze the character (dialogue, cutscene, menu).
            if (Input.GetKeyDown(KeyCode.L)) context.SetMovementLocked(!context.Motor.movementLocked);

            // Press G to grant or revoke flight.
            if (Input.GetKeyDown(KeyCode.G)) context.SetAbilityEnabled<FlyAbility>(!context.HasAbility<FlyAbility>());
        }

        /// <summary>Sample of a damage flow that goes through the resource system.</summary>
        public void ApplyDamage(float amount)
        {
            if (context == null || context.Resources == null) return;

            context.Resources.TakeDamage(amount, gameObject);

            // Knock the character back using the impulse channel.
            if (context.Motor != null)
            {
                Vector3 direction = (context.transform.position - transform.position).normalized;
                context.Motor.AddImpulse(direction * 6f + Vector3.up * 2f);
            }
        }
    }
}