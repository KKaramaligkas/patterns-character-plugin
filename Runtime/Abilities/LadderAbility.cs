using System.Collections.Generic;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Vertical ladder climbing. Put a <see cref="CharacterVolume"/> of type Ladder on the ladder collider
    /// (make sure the volume's blue arrow / +Z points away from the ladder face) and the character will snap
    /// on when pressing into it, climb with jump / crouch (or forward / back), and hop off at the top.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Ladder")]
    public class LadderAbility : ContinuousAbility
    {
        [Header("Attachment")]
        [Tooltip("Layers searched for ladder volumes.")]
        public LayerMask volumeMask = ~0;

        [Tooltip("Input direction must be within this angle of the ladder face to attach.")]
        [Range(-1f, 1f)] public float attachDot = 0.35f;

        [Tooltip("Distance to the ladder face where the character hangs.")]
        [Min(0f)] public float hangingOffset = 0.12f;

        [Tooltip("Vertical speed while climbing.")]
        [Min(0.1f)] public float climbSpeed = 2.6f;

        [Tooltip("Player must hold forward / backward to climb. When false the character also climbs with jump / crouch.")]
        public bool useForwardInputToClimb = true;

        [Header("Exit")]
        [Tooltip("Upward impulse applied when hopping off the top of the ladder.")]
        [Min(0f)] public float topHopImpulse = 5f;

        [Tooltip("Forward push applied when hopping off the top.")]
        [Min(0f)] public float topHopForward = 2f;

        [Tooltip("Automatically detach when the character climbs past the bottom of the ladder.")]
        public bool detachAtBottom = true;

        readonly List<CharacterVolume> _ladders = new List<CharacterVolume>(2);

        CharacterVolume _current;
        float _heightAlongLadder;
        float _rightOffset;
        bool _climbing;

        /// <summary>True while attached to a ladder.</summary>
        public bool IsClimbing { get { return _climbing; } }

        /// <summary>Ladder volume currently used, null when not climbing.</summary>
        public CharacterVolume CurrentLadder { get { return _current; } }

        /// <summary>Normalised position along the ladder, 0 at the bottom and 1 at the top.</summary>
        public float ClimbProgress { get; private set; }

        public event System.Action<CharacterVolume> ClimbStarted;
        public event System.Action<CharacterVolume> ClimbStopped;

        protected override void OnTick(float deltaTime)
        {
            if (Motor == null) return;

            if (_current == null || !_current.isActiveAndEnabled)
            {
                if (_climbing) Detach(false);
                TryAttach();
                return;
            }

            Climb(deltaTime);
        }

        void TryAttach()
        {
            if (_ladders.Count == 0) return;
            if (Input == null) return;

            // Prefer the ladder the character is looking at and pressing into.
            Vector3 inputDirection = Vector3.zero;
            bool hasInput = false;

            LocomotionAbility locomotion = Context != null ? Context.GetAbility<LocomotionAbility>() : null;
            if (locomotion != null && locomotion.HasInput)
            {
                inputDirection = locomotion.InputDirection.normalized;
                hasInput = true;
            }
            else if (Input.move.sqrMagnitude > 0.01f)
            {
                Transform cameraTransform = Context != null && Context.CameraRig != null ? Context.CameraRig.ActiveCameraTransform : null;
                inputDirection = CharacterMath.InputToWorld(Input.move, MovementSpace.CameraYaw, transform, cameraTransform, out hasInput);
            }

            CharacterVolume best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < _ladders.Count; i++)
            {
                CharacterVolume ladder = _ladders[i];
                if (ladder == null) continue;

                Vector3 faceNormal = -FlatForward(ladder);
                float score = hasInput ? Vector3.Dot(inputDirection, faceNormal) : 0.5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = ladder;
                }
            }

            if (best == null) return;
            if (hasInput && bestScore < attachDot) return;

            Attach(best);
        }

        static Vector3 FlatForward(CharacterVolume volume)
        {
            Vector3 forward = volume.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            return forward.normalized;
        }

        void Attach(CharacterVolume ladder)
        {
            _current = ladder;
            _climbing = true;

            Vector3 axisOrigin = ladder.transform.position;
            Vector3 right = ladder.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 1e-4f) right = Vector3.Cross(Vector3.up, FlatForward(ladder)).normalized;
            else right.Normalize();

            Vector3 flat = transform.position - axisOrigin;
            _rightOffset = Vector3.Dot(flat, right);
            _heightAlongLadder = Mathf.Max(0f, flat.y);

            Motor.SetGravityScale(0f);
            Motor.ClearExternalVelocity();

            OrientationAbility orientation = Context != null ? Context.GetAbility<OrientationAbility>() : null;
            if (orientation != null) orientation.SetModeOverride(this, 400, OrientationMode.Manual);

            var handler = ClimbStarted;
            if (handler != null) handler(ladder);
        }

        void Detach(bool hopOff)
        {
            if (_current != null)
            {
                OrientationAbility orientation = Context != null ? Context.GetAbility<OrientationAbility>() : null;
                if (orientation != null) orientation.ClearModeOverride(this);
            }

            CharacterVolume ladder = _current;
            _current = null;
            _climbing = false;

            if (Motor != null) Motor.SetGravityScale(1f);

            if (hopOff && ladder != null && Motor != null)
            {
                Vector3 forward = FlatForward(ladder);
                Vector3 direction = (Vector3.up * topHopImpulse + forward * topHopForward);
                Motor.AddImpulse(direction, true);
            }

            if (ladder != null)
            {
                var handler = ClimbStopped;
                if (handler != null) handler(ladder);
            }
        }

        void Climb(float deltaTime)
        {
            CharacterVolume ladder = _current;

            Vector3 axisOrigin = ladder.transform.position;
            Vector3 forward = FlatForward(ladder);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Bounds bounds = ladder.Collider != null ? ladder.Collider.bounds : new Bounds(axisOrigin, Vector3.one);
            float bottom = bounds.min.y - axisOrigin.y;
            float top = bounds.max.y - axisOrigin.y;
            float range = Mathf.Max(0.1f, top - bottom);

            float vertical = 0f;
            if (Input != null)
            {
                if (Input.IsHeld(CharacterAction.Jump)) vertical += 1f;
                if (Input.IsHeld(CharacterAction.Crouch)) vertical -= 1f;

                if (useForwardInputToClimb && Mathf.Abs(vertical) < 0.01f)
                {
                    vertical = Input.move.y;
                    if (vertical < -0.2f)
                    {
                        // Pressing back detaches instead of climbing down.
                        Detach(false);
                        return;
                    }
                }

                // Jump while holding only the climb keys is the hop-off.
                if (Input.IsHeld(CharacterAction.Jump) && Input.IsHeld(CharacterAction.Crouch) == false && vertical > 0.5f && _heightAlongLadder >= top - 0.15f)
                {
                    Detach(true);
                    return;
                }
            }

            _heightAlongLadder = Mathf.Clamp(_heightAlongLadder + vertical * climbSpeed * deltaTime, bottom, top);
            ClimbProgress = Mathf.Clamp01((_heightAlongLadder - bottom) / range);

            // Target position on the ladder face.
            float radius = Motor.Radius + hangingOffset;
            Vector3 targetPosition = axisOrigin
                                     + Vector3.up * _heightAlongLadder
                                     + right * _rightOffset
                                     + forward * radius;

            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;

            Vector3 velocity = toTarget / Mathf.Max(deltaTime, 1e-4f);
            velocity.y = vertical * climbSpeed;

            if (velocity.magnitude > climbSpeed * 3f) velocity = velocity.normalized * climbSpeed * 3f;

            SubmitMotion(velocity, false, true);

            // Face away from the ladder.
            Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, CharacterMath.SmoothFactor(12f, deltaTime));

            // Exit conditions.
            if (detachAtBottom && _heightAlongLadder <= bottom + 0.02f && Motor.IsGrounded && vertical < 0f)
            {
                Detach(false);
                return;
            }

            if (_heightAlongLadder >= top - 0.02f && vertical > 0f && !useForwardInputToClimb)
            {
                Detach(true);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            CharacterVolume volume = other.GetComponentInParent<CharacterVolume>();
            if (volume == null || volume.type != VolumeType.Ladder) return;
            if (!_ladders.Contains(volume)) _ladders.Add(volume);
        }

        void OnTriggerExit(Collider other)
        {
            CharacterVolume volume = other.GetComponentInParent<CharacterVolume>();
            if (volume == null) return;

            _ladders.Remove(volume);
            if (_current == volume) Detach(false);
        }

        protected override void OnDisabled()
        {
            if (_climbing) Detach(false);
            _ladders.Clear();
        }
    }
}