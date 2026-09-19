using Patterns.Character;
using UnityEngine;

namespace Patterns.Character.Samples
{
    /// <summary>
    /// Shows how to write a new ability. This one is a super jump that costs stamina, shares the jump
    /// action with the normal jump ability and only fires while sprinting.
    /// Put it on the character next to the other abilities.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Samples/Super Jump Ability")]
    public class SuperJumpAbility : InputTriggeredAbility
    {
        [Header("Super Jump")]
        public float heightMultiplier = 2.2f;

        [Tooltip("Only allow the super jump while the sprint ability is active.")]
        public bool requiresSprinting = true;

        [Tooltip("Seconds the super jump is unavailable after use.")]
        public float superJumpCooldown = 4f;

        SprintAbility _sprint;

        protected override void OnBind()
        {
            // The context lets abilities find each other without hard references.
            _sprint = Context.GetAbility<SprintAbility>();

            action = CharacterAction.Jump;
            executionOrder = 210; // After the normal jump so it wins the buffered press.
            motorPriority = 200;
            cooldown = superJumpCooldown;
        }

        protected override bool CanTrigger()
        {
            if (!base.CanTrigger()) return false;
            if (Motor == null || !Motor.IsGrounded) return false;
            if (requiresSprinting && _sprint != null && !_sprint.IsSprinting) return false;
            return true;
        }

        protected override void OnTrigger()
        {
            // Spend stamina through the resource system so it integrates with regen and UI.
            if (!TryPayCost()) return;

            CharacterProfile profile = Profile;
            float height = profile.jumpHeight * heightMultiplier;
            Motor.Launch(profile.JumpVelocityFor(height, Mathf.Max(0.05f, Motor.persistentGravityScale)));

            // Extra horizontal push so the super jump also clears gaps.
            Motor.SubmitPlanarRequest(this, motorPriority, transform.forward * profile.sprintSpeed);
        }
    }

    /// <summary>
    /// Shows how to write an input source. This one drives the character itself, which is how you make
    /// AI, replays or network ghosts use the exact same abilities as the player.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Samples/Wander AI Input Source")]
    public class WanderAIInputSource : CharacterInputSourceBehaviour
    {
        public float changeDirectionEvery = 2.5f;
        public float jumpEvery = 6f;

        float _directionTimer;
        float _jumpTimer;
        Vector2 _move;

        public override string DeviceName
        {
            get { return "Wander AI"; }
        }

        void Update()
        {
            _directionTimer -= Time.deltaTime;
            if (_directionTimer <= 0f)
            {
                _directionTimer = changeDirectionEvery;
                _move = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
            }

            _jumpTimer -= Time.deltaTime;
            if (_jumpTimer <= 0f) _jumpTimer = jumpEvery;
        }

        public override void Poll(CharacterInputFrame frame, float deltaTime)
        {
            frame.move += _move;
            frame.moveMagnitude = Mathf.Min(1f, _move.magnitude);
            frame.sprintHeld = true;

            // A jump is a one frame pulse, exactly like a player press.
            if (_jumpTimer > jumpEvery - 0.05f)
            {
                frame.jumpPressed = true;
                frame.jumpHeld = true;
            }

            frame.deviceName = DeviceName;
            MarkActive();
        }
    }
}