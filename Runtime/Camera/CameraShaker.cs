using UnityEngine;

namespace Patterns.Character
{
    /// <summary>
    /// Trauma based camera shake (the Squirrel Eiserloh approach: one value, noise driven, no state machine).
    /// It can subscribe to the character's own events, so landing and dashing shake the camera out of the box.
    /// </summary>
    [DefaultExecutionOrder(300)]
    [AddComponentMenu("Patterns/Character/Camera/Camera Shaker")]
    public class CameraShaker : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform that gets shaken. Leave empty to shake the active camera of the character.")]
        public Transform target;

        [Header("Shake")]
        [Tooltip("Current trauma, 0..1. Add to it with AddTrauma.")]
        [Range(0f, 1f)] public float trauma;

        [Tooltip("Trauma removed per second.")]
        [Min(0f)] public float traumaDecay = 1.5f;

        [Tooltip("Maximum positional offset in meters at full trauma.")]
        [Min(0f)] public float maxPositionOffset = 0.35f;

        [Tooltip("Maximum rotational offset in degrees at full trauma.")]
        [Min(0f)] public float maxRotationOffset = 4f;

        [Tooltip("Shake frequency. Lower is heavier, higher is snappier.")]
        [Min(0.1f)] public float frequency = 22f;

        [Tooltip("Trauma is raised to this power, which makes small shakes feel weak and big ones feel violent.")]
        [Range(1f, 4f)] public float traumaExponent = 2f;

        [Header("Field Of View")]
        [Tooltip("Field of view kick at full trauma.")]
        public float fovKick = 2.5f;

        [Tooltip("Field of view kick blend speed.")]
        [Min(0.1f)] public float fovKickSharpness = 10f;

        [Header("Character Events")]
        [Tooltip("Automatically shake when the character lands, dashes or takes damage.")]
        public bool subscribeToCharacterEvents = true;

        [Tooltip("Trauma added by a hard landing.")]
        [Range(0f, 1f)] public float landingTrauma = 0.45f;

        [Tooltip("Trauma added by a soft landing, scaled by impact speed.")]
        [Range(0f, 1f)] public float softLandingTrauma = 0.15f;

        [Tooltip("Trauma added when taking damage.")]
        [Range(0f, 1f)] public float damageTrauma = 0.35f;

        [Tooltip("Trauma added when dashing.")]
        [Range(0f, 1f)] public float dashTrauma = 0.12f;

        CharacterContext _context;
        CharacterCameraRigSwitcher _switcher;
        Camera _camera;
        float _fovKick;
        float _seed;

        void Awake()
        {
            _seed = Random.value * 100f;
        }

        void OnEnable()
        {
            _context = GetComponentInParent<CharacterContext>();
            _switcher = _context != null ? _context.CameraRig : null;

            if (target == null && _switcher != null) target = _switcher.ActiveCameraTransform;

            if (subscribeToCharacterEvents) HookCharacterEvents(true);
        }

        void OnDisable()
        {
            if (subscribeToCharacterEvents) HookCharacterEvents(false);
        }

        void HookCharacterEvents(bool hook)
        {
            if (_context == null) return;

            MotorBase motor = _context.Motor;
            if (motor != null)
            {
                if (hook) motor.Landed += HandleLanded;
                else motor.Landed -= HandleLanded;
            }

            if (hook) _context.Died += HandleDied;
            else _context.Died -= HandleDied;

            if (!hook) return;

            DashAbility dash = _context.GetAbility<DashAbility>();
            if (dash != null)
            {
                if (hook) dash.Dashed += HandleDashed;
                else dash.Dashed -= HandleDashed;
            }
        }

        void HandleLanded(LandingInfo info)
        {
            float amount = info.HardLanding
                ? landingTrauma
                : Mathf.Clamp01(info.ImpactSpeed * 0.04f) * softLandingTrauma;

            AddTrauma(amount);
        }

        void HandleDied(CharacterContext context)
        {
            AddTrauma(1f);
        }

        void HandleDashed(Vector3 direction)
        {
            AddTrauma(dashTrauma);
        }

        /// <summary>Adds shake. 0.2 is a tap, 1 is a screen filling explosion.</summary>
        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        /// <summary>One shot shake independent of the trauma value.</summary>
        public void Shake(float amount, float fovAmount = 0f)
        {
            AddTrauma(amount);
            if (fovAmount != 0f) _fovKick = fovAmount;
        }

        /// <summary>Shakes every character in the scene that has a shaker, scaled by distance.</summary>
        public static void ShakeAt(Vector3 position, float radius, float amount)
        {
#if UNITY_2023_1_OR_NEWER
            CameraShaker[] shakers = FindObjectsByType<CameraShaker>(FindObjectsSortMode.None);
#else
            CameraShaker[] shakers = FindObjectsOfType<CameraShaker>();
#endif
            for (int i = 0; i < shakers.Length; i++)
            {
                CameraShaker shaker = shakers[i];
                if (shaker == null) continue;

                float distance = Vector3.Distance(shaker.transform.position, position);
                if (distance > radius) continue;

                float falloff = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, radius));
                shaker.AddTrauma(amount * falloff * falloff);
            }
        }

        void LateUpdate()
        {
            if (target == null && _switcher != null) target = _switcher.ActiveCameraTransform;
            if (target == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (trauma > 0f)
            {
                trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
            }

            float shake = trauma <= 0f ? 0f : Mathf.Pow(trauma, traumaExponent);

            if (shake > 0f)
            {
                float time = Time.time * frequency;

                Vector3 offset = new Vector3(
                    Noise(time, 0f) + Noise(time, 5.13f),
                    Noise(time, 11.7f) + Noise(time, 17.3f),
                    Noise(time, 23.1f)) * maxPositionOffset * shake;

                Vector3 rotationOffset = new Vector3(
                    Noise(time, 31.7f),
                    Noise(time, 41.3f),
                    Noise(time, 53.9f)) * maxRotationOffset * shake;

                target.position += target.rotation * offset;
                target.rotation *= Quaternion.Euler(rotationOffset);
            }

            Camera cam = ResolveCamera();
            if (cam != null)
            {
                _fovKick = Mathf.Lerp(_fovKick, 0f, CharacterMath.SmoothFactor(Mathf.Max(0.1f, fovKickSharpness), dt));

                float targetFov = cam.fieldOfView + _fovKick * shake;
                if (Mathf.Abs(targetFov - cam.fieldOfView) > 0.0001f && _fovKick > 0f)
                {
                    cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, CharacterMath.SmoothFactor(20f, dt));
                }
            }
        }

        Camera ResolveCamera()
        {
            if (_camera != null) return _camera;
            if (_switcher != null)
            {
                _camera = _switcher.ActiveCamera;
                if (_camera != null) return _camera;
            }

            _camera = GetComponent<Camera>();
            if (_camera == null && target != null) _camera = target.GetComponent<Camera>();
            return _camera;
        }

        static float Noise(float time, float channel)
        {
            // Perlin noise gives smooth, non looping shake. The channel offsets decorrelate the axes.
            return Mathf.PerlinNoise(time + channel * 3.7f, channel) * 2f - 1f;
        }
    }
}