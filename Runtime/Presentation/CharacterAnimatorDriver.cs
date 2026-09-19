using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Drives an Animator from character state. Parameter names are configurable so this drops onto any
    /// rig, Humanoid or generic, without touching the Animator Controller.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Presentation/Animator Driver")]
    public class CharacterAnimatorDriver : MonoBehaviour
    {
        [Header("Animator")]
        public Animator animator;

        [Header("Parameters")]
        public string speedParameter = "Speed";
        public string forwardParameter = "Forward";
        public string rightParameter = "Right";
        public string verticalSpeedParameter = "VerticalSpeed";
        public string groundedParameter = "Grounded";
        public string jumpTrigger = "Jump";
        public string airJumpTrigger = "AirJump";
        public string landTrigger = "Land";
        public string crouchParameter = "Crouch";
        public string aimParameter = "Aim";
        public string climbingParameter = "Climbing";
        public string swimmingParameter = "Swimming";
        public string flyingParameter = "Flying";
        public string deadParameter = "Dead";
        public string dashTrigger = "Dash";

        [Header("Blend")]
        [Tooltip("How quickly the speed parameter follows the real speed.")]
        [Min(0f)] public float speedDamping = 0.12f;

        [Tooltip("Speed that maps to 1.0 in the animator. Negative uses the profile run speed.")]
        public float referenceSpeed = -1f;

        [Tooltip("Keep the animator in sync even when no CharacterContext is present.")]
        public bool standaloneFallback = true;

        CharacterContext _context;
        LocomotionAbility _locomotion;
        CrouchAbility _crouch;
        AimAbility _aim;
        SwimAbility _swim;
        LadderAbility _ladder;
        FlyAbility _fly;
        JumpAbility _jump;
        DashAbility _dash;
        CharacterMotor _characterMotor;

        float _speed;
        float _verticalSpeed;

        int _speedHash, _forwardHash, _rightHash, _verticalSpeedHash, _groundedHash;
        int _jumpHash, _airJumpHash, _landHash, _crouchHash, _aimHash;
        int _climbingHash, _swimmingHash, _flyingHash, _deadHash, _dashHash;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            _speedHash = Hash(speedParameter);
            _forwardHash = Hash(forwardParameter);
            _rightHash = Hash(rightParameter);
            _verticalSpeedHash = Hash(verticalSpeedParameter);
            _groundedHash = Hash(groundedParameter);
            _jumpHash = Hash(jumpTrigger);
            _airJumpHash = Hash(airJumpTrigger);
            _landHash = Hash(landTrigger);
            _crouchHash = Hash(crouchParameter);
            _aimHash = Hash(aimParameter);
            _climbingHash = Hash(climbingParameter);
            _swimmingHash = Hash(swimmingParameter);
            _flyingHash = Hash(flyingParameter);
            _deadHash = Hash(deadParameter);
            _dashHash = Hash(dashTrigger);
        }

        static int Hash(string parameter)
        {
            return string.IsNullOrEmpty(parameter) ? 0 : Animator.StringToHash(parameter);
        }

        void Start()
        {
            _context = GetComponentInParent<CharacterContext>();
            _characterMotor = GetComponentInParent<CharacterMotor>();

            if (_context != null)
            {
                _locomotion = _context.GetAbility<LocomotionAbility>();
                _crouch = _context.GetAbility<CrouchAbility>();
                _aim = _context.GetAbility<AimAbility>();
                _swim = _context.GetAbility<SwimAbility>();
                _ladder = _context.GetAbility<LadderAbility>();
                _fly = _context.GetAbility<FlyAbility>();
                _jump = _context.GetAbility<JumpAbility>();
                _dash = _context.GetAbility<DashAbility>();

                if (_jump != null) _jump.Jumped += HandleJumped;
                if (_dash != null) _dash.Dashed += HandleDashed;

                MotorBase motor = _context.Motor;
                if (motor != null) motor.Landed += HandleLanded;
            }
        }

        void OnDestroy()
        {
            if (_jump != null) _jump.Jumped -= HandleJumped;
            if (_dash != null) _dash.Dashed -= HandleDashed;

            if (_context != null && _context.Motor != null) _context.Motor.Landed -= HandleLanded;
        }

        void HandleJumped(bool airJump)
        {
            if (animator == null) return;
            if (airJump && _airJumpHash != 0) animator.SetTrigger(_airJumpHash);
            else if (_jumpHash != 0) animator.SetTrigger(_jumpHash);
        }

        void HandleLanded(LandingInfo info)
        {
            if (animator == null || _landHash == 0) return;
            animator.SetTrigger(_landHash);
            if (info.HardLanding) animator.SetFloat(_verticalSpeedHash, -Mathf.Abs(info.ImpactSpeed));
        }

        void HandleDashed(Vector3 direction)
        {
            if (animator == null || _dashHash == 0) return;
            animator.SetTrigger(_dashHash);
        }

        void Update()
        {
            if (animator == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            MotorBase motor = _context != null ? _context.Motor : _characterMotor;
            if (motor == null && !standaloneFallback) return;

            CharacterProfile profile = _context != null ? _context.Profile : CharacterProfile.Default;
            float reference = referenceSpeed > 0f ? referenceSpeed : Mathf.Max(0.1f, profile.runSpeed);

            float planarSpeed = motor != null ? motor.PlanarSpeed : 0f;
            float verticalSpeed = motor != null ? motor.Velocity.y : 0f;

            float speedTarget = Mathf.Clamp01(planarSpeed / reference);
            _speed = Mathf.Lerp(_speed, speedTarget, CharacterMath.SmoothFactor(1f / Mathf.Max(0.0001f, speedDamping), dt));
            _verticalSpeed = Mathf.Lerp(_verticalSpeed, verticalSpeed, CharacterMath.SmoothFactor(1f / Mathf.Max(0.0001f, speedDamping), dt));

            SetFloat(_speedHash, _speed);
            SetFloat(_verticalSpeedHash, _verticalSpeed);

            if (_locomotion != null)
            {
                SetFloat(_forwardHash, _locomotion.LocalForwardSpeed / reference);
                SetFloat(_rightHash, _locomotion.LocalRightSpeed / reference);
            }

            if (motor != null) SetBool(_groundedHash, motor.IsGrounded);

            if (_crouch != null) SetFloat(_crouchHash, _crouch.CrouchRatio);
            if (_aim != null) SetFloat(_aimHash, _aim.AimRatio);

            if (_ladder != null) SetBool(_climbingHash, _ladder.IsClimbing);
            if (_swim != null) SetBool(_swimmingHash, _swim.IsInWater && !_swim.IsDiving);
            if (_fly != null) SetBool(_flyingHash, _fly.IsFlying);

            if (_context != null && _context.Resources != null) SetBool(_deadHash, _context.Resources.IsDead);
        }

        void SetFloat(int hash, float value)
        {
            if (hash == 0) return;
            animator.SetFloat(hash, value);
        }

        void SetBool(int hash, bool value)
        {
            if (hash == 0) return;
            animator.SetBool(hash, value);
        }

        void SetTrigger(int hash)
        {
            if (hash == 0) return;
            animator.SetTrigger(hash);
        }

        /// <summary>Checks the Animator for the configured parameters and logs anything that is missing.</summary>
        [ContextMenu("Validate Parameters")]
        public void ValidateParameters()
        {
            if (animator == null)
            {
                Debug.LogWarning("[Patterns.Character] No Animator assigned to the animator driver.", this);
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("[Patterns.Character] The Animator has no controller assigned.", animator);
                return;
            }

            string[] expected =
            {
                speedParameter, forwardParameter, rightParameter, verticalSpeedParameter, groundedParameter,
                jumpTrigger, airJumpTrigger, landTrigger, crouchParameter, aimParameter,
                climbingParameter, swimmingParameter, flyingParameter, deadParameter, dashTrigger
            };

            AnimatorControllerParameter[] parameters = animator.parameters;
            int missing = 0;

            for (int i = 0; i < expected.Length; i++)
            {
                if (string.IsNullOrEmpty(expected[i])) continue;

                bool found = false;
                for (int j = 0; j < parameters.Length; j++)
                {
                    if (parameters[j].name == expected[i])
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    missing++;
                    Debug.LogWarningFormat("[Patterns.Character] Animator parameter '{0}' is missing.", expected[i]);
                }
            }

            if (missing == 0) Debug.Log("[Patterns.Character] All animator parameters are present.", this);
        }
    }
}