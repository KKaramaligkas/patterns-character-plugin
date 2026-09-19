using UnityEngine;

namespace Patterns.Character
{
    /// <summary>Audio set for one kind of surface: footsteps, landings and jumps.</summary>
    [AddComponentMenu("Patterns/Character/Presentation/Surface Audio Set")]
    public class SurfaceAudioSet : MonoBehaviour
    {
        [Header("Footsteps")]
        public AudioClip[] footstepClips;
        public AudioClip[] jumpClips;
        public AudioClip[] landClips;

        [Range(0f, 2f)] public float volume = 1f;

        [Range(0.5f, 2f)] public float pitchMin = 0.92f;
        [Range(0.5f, 2f)] public float pitchMax = 1.08f;

        public AudioClip RandomFootstep()
        {
            return RandomClip(footstepClips);
        }

        public AudioClip RandomJump()
        {
            return RandomClip(jumpClips);
        }

        public AudioClip RandomLand()
        {
            return RandomClip(landClips);
        }

        AudioClip RandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            return clips[Random.Range(0, clips.Length)];
        }
    }

    /// <summary>
    /// Footsteps, jumps and landings driven by real character state, with surface detection through
    /// <see cref="SurfaceAudioSet"/>. Steps are spaced by distance travelled, not by a timer, so they
    /// stay in sync at every speed.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Presentation/Audio Driver")]
    public class CharacterAudioDriver : MonoBehaviour
    {
        [Header("Sources")]
        [Tooltip("Audio source used for one shots. Created automatically when empty.")]
        public AudioSource source;

        public AudioSource loopSource;

        [Header("Defaults")]
        [Tooltip("Used when the surface under the character has no SurfaceAudioSet.")]
        public SurfaceAudioSet defaultSet;

        [Tooltip("Distance between footsteps at walking speed, in meters.")]
        [Min(0.2f)] public float strideLength = 2f;

        [Tooltip("Play footsteps while airborne. Normally off.")]
        public bool footstepsInAir;

        [Tooltip("Minimum planar speed before footsteps play.")]
        [Min(0f)] public float minimumSpeed = 0.4f;

        [Header("Events")]
        public AudioClip dashClip;
        public AudioClip swimEnterClip;
        public AudioClip swimExitClip;

        [Header("Volumes")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float landingVolumeScale = 1f;

        CharacterContext _context;
        CharacterMotor _characterMotor;
        SurfaceAudioSet _currentSurface;
        float _distanceAccumulator;
        float _surfaceCheckTimer;

        void Awake()
        {
            if (source == null)
            {
                source = GetComponent<AudioSource>();
                if (source == null)
                {
                    source = gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 1f;
                }
            }

            if (loopSource == null)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                loopSource.loop = true;
                loopSource.spatialBlend = 1f;
            }
        }

        void Start()
        {
            _context = GetComponentInParent<CharacterContext>();
            _characterMotor = GetComponentInParent<CharacterMotor>();

            if (_context != null)
            {
                MotorBase motor = _context.Motor;
                if (motor != null) motor.Landed += HandleLanded;

                JumpAbility jump = _context.GetAbility<JumpAbility>();
                if (jump != null) jump.Jumped += HandleJumped;

                DashAbility dash = _context.GetAbility<DashAbility>();
                if (dash != null) dash.Dashed += HandleDashed;

                SwimAbility swim = _context.GetAbility<SwimAbility>();
                if (swim != null)
                {
                    swim.EnteredWater += HandleEnteredWater;
                    swim.ExitedWater += HandleExitedWater;
                }
            }
        }

        void OnDestroy()
        {
            if (_context == null) return;

            if (_context.Motor != null) _context.Motor.Landed -= HandleLanded;

            JumpAbility jump = _context.GetAbility<JumpAbility>();
            if (jump != null) jump.Jumped -= HandleJumped;

            DashAbility dash = _context.GetAbility<DashAbility>();
            if (dash != null) dash.Dashed -= HandleDashed;

            SwimAbility swim = _context.GetAbility<SwimAbility>();
            if (swim != null)
            {
                swim.EnteredWater -= HandleEnteredWater;
                swim.ExitedWater -= HandleExitedWater;
            }
        }

        void HandleJumped(bool airJump)
        {
            SurfaceAudioSet set = _currentSurface != null ? _currentSurface : defaultSet;
            if (set == null) return;

            AudioClip clip = set.RandomJump();
            if (clip == null) return;

            Play(clip, set);
        }

        void HandleLanded(LandingInfo info)
        {
            SurfaceAudioSet set = _currentSurface != null ? _currentSurface : defaultSet;
            if (set == null) return;

            AudioClip clip = set.RandomLand();
            if (clip == null) return;

            float impact = Mathf.Clamp01(info.ImpactSpeed / 20f);
            Play(clip, set, Mathf.Lerp(0.4f, 1.2f, impact) * landingVolumeScale);
        }

        void HandleDashed(Vector3 direction)
        {
            if (dashClip == null) return;
            source.PlayOneShot(dashClip, masterVolume);
        }

        void HandleEnteredWater()
        {
            if (swimEnterClip == null) return;
            source.PlayOneShot(swimEnterClip, masterVolume);
        }

        void HandleExitedWater()
        {
            if (swimExitClip == null) return;
            source.PlayOneShot(swimExitClip, masterVolume);
        }

        void Update()
        {
            if (source == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            MotorBase motor = _context != null ? _context.Motor : _characterMotor;
            if (motor == null) return;

            _surfaceCheckTimer -= dt;
            if (_surfaceCheckTimer <= 0f)
            {
                _surfaceCheckTimer = 0.25f;
                UpdateSurface(motor);
            }

            bool canStep = motor.IsGrounded || footstepsInAir;
            float speed = motor.PlanarSpeed;

            if (!canStep || speed < minimumSpeed)
            {
                _distanceAccumulator = Mathf.Max(0f, _distanceAccumulator - dt * 2f);
                return;
            }

            _distanceAccumulator += speed * dt;

            // Faster movement means longer strides, so the cadence stays believable.
            float stride = Mathf.Max(0.4f, strideLength);

            if (_distanceAccumulator >= stride)
            {
                _distanceAccumulator -= stride;
                PlayFootstep();
            }
        }

        void PlayFootstep()
        {
            SurfaceAudioSet set = _currentSurface != null ? _currentSurface : defaultSet;
            if (set == null) return;

            AudioClip clip = set.RandomFootstep();
            if (clip == null) return;

            Play(clip, set, 0.7f);
        }

        void Play(AudioClip clip, SurfaceAudioSet set, float volumeScale = 1f)
        {
            if (clip == null || source == null) return;

            float volume = masterVolume * volumeScale * (set != null ? set.volume : 1f);
            float pitch = set != null ? Random.Range(set.pitchMin, set.pitchMax) : 1f;

            source.pitch = pitch;
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        void UpdateSurface(MotorBase motor)
        {
            _currentSurface = null;

            GroundInfo ground = motor.Ground;
            if (!ground.IsGrounded || ground.Collider == null) return;

            _currentSurface = ground.Collider.GetComponentInParent<SurfaceAudioSet>();
        }
    }
}