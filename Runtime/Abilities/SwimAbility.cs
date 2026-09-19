using System.Collections.Generic;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Swimming and buoyancy. Enter any <see cref="CharacterVolume"/> of type Water and the character
    /// switches to 3D movement with buoyancy, drag, optional stamina drain and flow support.
    /// Volumes are detected with triggers and with a slow overlap sweep so spawning inside water works.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Abilities/Swim")]
    public class SwimAbility : ContinuousAbility
    {
        [Header("Water")]
        [Tooltip("Layers searched for CharacterVolume components. Narrow it down for performance.")]
        public LayerMask volumeMask = ~0;

        [Tooltip("Seconds between overlap sweeps that catch volumes missed by trigger events.")]
        [Min(0.05f)] public float volumeSweepInterval = 0.3f;

        [Header("Movement")]
        public int tierPriority = 300;
        public LocomotionTier tier = LocomotionTier.Swim;

        [Tooltip("Multiplies the swim speed from the profile.")]
        [Min(0f)] public float speedMultiplier = 1f;

        [Tooltip("Swim in the full camera basis, so looking down and pressing forward dives.")]
        public bool swimAlongCameraBasis = true;

        [Tooltip("Extra speed while diving or surfacing with jump / crouch.")]
        [Min(0.1f)] public float verticalSpeedRatio = 0.8f;

        [Header("Buoyancy")]
        [Tooltip("How far below the surface the character floats when idle.")]
        [Min(0f)] public float floatDepth = 0.6f;

        [Tooltip("Buoyancy strength. Higher numbers pop the character up faster.")]
        [Min(0f)] public float buoyancy = -1f;

        [Tooltip("Water drag. 0 keeps momentum, 1 stops the character immediately.")]
        [Range(0f, 1f)] public float drag = -1f;

        [Tooltip("Upward impulse applied when leaving the water while rising.")]
        public float exitImpulse = -1f;

        [Tooltip("Gravity multiplier while in the water. 0 means the character only sinks with input.")]
        [Range(0f, 1f)] public float gravityScale = 0f;

        [Header("Damage")]
        [Tooltip("Take damage per second from volumes that define it (lava, acid).")]
        public bool applyVolumeDamage = true;

        readonly List<CharacterVolume> _volumes = new List<CharacterVolume>(4);
        float _nextSweep;
        bool _wasInWater;

        /// <summary>True while the character overlaps at least one water volume.</summary>
        public bool IsInWater { get { return _volumes.Count > 0; } }

        /// <summary>True while diving with the crouch action held.</summary>
        public bool IsDiving { get; private set; }

        /// <summary>World height of the water surface the character is in.</summary>
        public float SurfaceHeight { get; private set; }

        /// <summary>How deep the character's feet are below the surface.</summary>
        public float SubmersionDepth
        {
            get
            {
                if (Motor == null || !IsInWater) return 0f;
                return Mathf.Max(0f, SurfaceHeight - Motor.FootPosition.y);
            }
        }

        public event System.Action EnteredWater;
        public event System.Action ExitedWater;

        protected override void OnTick(float deltaTime)
        {
            if (Motor == null) return;

            if (Time.time >= _nextSweep)
            {
                _nextSweep = Time.time + volumeSweepInterval;
                SweepVolumes();
            }

            if (!IsInWater)
            {
                HandleExit();
                return;
            }

            if (!_wasInWater)
            {
                _wasInWater = true;
                var handler = EnteredWater;
                if (handler != null) handler();
            }

            CharacterProfile p = Profile;
            CharacterVolume primary = PrimaryVolume();
            SurfaceHeight = primary != null ? primary.SurfaceHeight : transform.position.y;

            float speed = p.swimSpeed * speedMultiplier * Mathf.Max(0.05f, _volumeSpeedFactor);

            float horizontal = Input != null ? Input.move.x : 0f;
            float forward = Input != null ? Input.move.y : 0f;
            float verticalInput = 0f;

            if (Input != null)
            {
                if (Input.IsHeld(CharacterAction.Jump)) verticalInput += 1f;
                if (Input.IsHeld(CharacterAction.Crouch)) verticalInput -= 1f;
            }

            IsDiving = verticalInput < 0f;

            Transform cameraTransform = swimAlongCameraBasis && Context != null && Context.CameraRig != null
                ? Context.CameraRig.ActiveCameraTransform
                : null;

            Vector3 direction;
            if (cameraTransform != null)
            {
                direction = cameraTransform.right * horizontal + cameraTransform.forward * forward;
            }
            else
            {
                direction = transform.right * horizontal + transform.forward * forward;
            }

            // Buoyancy: float towards the surface when the player is not steering vertically.
            float floatTarget = SurfaceHeight - floatDepth;
            if (verticalInput == 0f && Motor.FootPosition.y < floatTarget)
            {
                float strength = buoyancy >= 0f ? buoyancy : p.buoyancy;
                verticalInput = Mathf.Clamp01((floatTarget - Motor.FootPosition.y) * 0.5f) * Mathf.Max(0.2f, strength * 0.25f);
            }
            else if (verticalInput == 0f && Motor.FootPosition.y > SurfaceHeight)
            {
                // Above the surface: fall back in.
                verticalInput = -0.4f;
            }

            direction += Vector3.up * verticalInput * verticalSpeedFactor;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            Motor.SetGravityScale(gravityScale);

            Vector3 targetVelocity = direction * speed;

            if (direction.sqrMagnitude < 1e-4f)
            {
                // No input at all: drift to a stop, keep buoyancy working.
                targetVelocity = Vector3.up * (verticalInput * speed * verticalSpeedFactor);
            }

            SubmitMotion(targetVelocity, false, true);

            if (primary != null)
            {
                Vector3 flow = primary.Flow;
                if (flow.sqrMagnitude > 0f) Motor.AddEnvironmentalVelocity(flow);

                if (applyVolumeDamage && primary.damagePerSecond > 0f && Resources != null)
                {
                    Resources.TakeDamage(primary.damagePerSecond * deltaTime, gameObject, true);
                }
            }

            LocomotionAbility locomotion = Context != null ? Context.GetAbility<LocomotionAbility>() : null;
            if (locomotion != null) locomotion.RequestTier(this, tierPriority, tier, speedMultiplier);

            if (drainPerSecond > 0f) TryDrain(deltaTime);
        }

        float verticalSpeedFactor
        {
            get { return Mathf.Max(0.1f, verticalSpeedRatio); }
        }

        float _volumeSpeedFactor = 1f;

        void HandleExit()
        {
            if (!_wasInWater) return;
            _wasInWater = false;

            CharacterProfile p = Profile;
            float impulse = exitImpulse >= 0f ? exitImpulse : p.waterExitImpulse;

            if (impulse > 0f && Motor.Velocity.y > 0.1f)
            {
                Motor.Launch(Mathf.Max(Motor.Velocity.y, impulse));
            }

            LocomotionAbility locomotion = Context != null ? Context.GetAbility<LocomotionAbility>() : null;
            if (locomotion != null) locomotion.ClearTierRequest(this);

            var handler = ExitedWater;
            if (handler != null) handler();
        }

        CharacterVolume PrimaryVolume()
        {
            CharacterVolume best = null;
            float bestDepth = float.MinValue;

            for (int i = 0; i < _volumes.Count; i++)
            {
                CharacterVolume volume = _volumes[i];
                if (volume == null) continue;

                float depth = volume.SurfaceHeight - Motor.FootPosition.y;
                if (depth > bestDepth)
                {
                    bestDepth = depth;
                    best = volume;
                }
            }

            return best;
        }

        void SweepVolumes()
        {
            if (Motor == null) return;

            float radius = Mathf.Max(0.2f, Motor.Radius * 1.2f);
            Collider[] hits = Physics.OverlapSphere(Motor.ColliderCenter, radius + Motor.Height * 0.5f, volumeMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits.Length; i++)
            {
                CharacterVolume volume = hits[i].GetComponentInParent<CharacterVolume>();
                if (volume == null || volume.type != VolumeType.Water) continue;

                // Only count volumes the character is actually inside of.
                if (!volume.Collider.bounds.Contains(new Vector3(transform.position.x, volume.SurfaceHeight - 0.01f, transform.position.z))) continue;

                if (_volumes.Contains(volume)) continue;
                AddVolume(volume);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            CharacterVolume volume = other.GetComponentInParent<CharacterVolume>();
            if (volume == null) return;

            if (volume.type == VolumeType.Water)
            {
                AddVolume(volume);
            }
            else if (volume.type == VolumeType.Lava)
            {
                AddVolume(volume);
            }
        }

        void OnTriggerExit(Collider other)
        {
            CharacterVolume volume = other.GetComponentInParent<CharacterVolume>();
            if (volume == null) return;
            _volumes.Remove(volume);

            if (volume.drag > 0f) _volumeSpeedFactor = 1f - volume.drag;
            else _volumeSpeedFactor = 1f;
        }

        void AddVolume(CharacterVolume volume)
        {
            if (_volumes.Contains(volume)) return;
            _volumes.Add(volume);
            _volumeSpeedFactor = volume.drag > 0f ? Mathf.Clamp(1f - volume.drag, 0.05f, 1f) : 1f;
        }

        protected override void OnDisabled()
        {
            _volumes.Clear();
            _wasInWater = false;
            if (Motor != null) Motor.SetGravityScale(1f);
        }
    }
}