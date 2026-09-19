using System;
using System.Collections.Generic;
using UnityEngine;

namespace Patterns.Character
{
    /// <summary>A single pool of a resource, with its own regeneration rules.</summary>
    [Serializable]
    public class CharacterResource
    {
        public ResourceType type = ResourceType.Stamina;
        public string displayName = "Stamina";
        [Min(0f)] public float max = 100f;
        [Min(0f)] public float current = 100f;
        [Min(0f)] public float regenPerSecond = 20f;
        [Min(0f)] public float regenDelay = 0.75f;

        [NonSerialized] public float lastSpendTime = -100f;
        [NonSerialized] public bool regenPaused;

        public float Normalized
        {
            get { return max > 0f ? Mathf.Clamp01(current / max) : 0f; }
        }

        public bool IsEmpty
        {
            get { return current <= 0f; }
        }

        public bool IsFull
        {
            get { return current >= max - 0.0001f; }
        }

        public void Reset()
        {
            current = max;
            lastSpendTime = -100f;
        }

        public CharacterResource Clone()
        {
            return new CharacterResource
            {
                type = type,
                displayName = displayName,
                max = max,
                current = current,
                regenPerSecond = regenPerSecond,
                regenDelay = regenDelay
            };
        }
    }

    /// <summary>
    /// Health / stamina / energy (and custom resources) with regen, spend gating and events.
    /// Every ability that costs something goes through this component.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterResources : MonoBehaviour
    {
        [SerializeField] List<CharacterResource> resources = new List<CharacterResource>();

        [Tooltip("Regeneration is skipped while true. Set by abilities that keep the character in a costly state.")]
        public bool regenPaused;

        [Tooltip("Seconds of invulnerability granted after taking damage.")]
        [Min(0f)] public float invulnerabilityTime = 0.35f;

        [Tooltip("Global multiplier applied to incoming damage.")]
        [Min(0f)] public float damageMultiplier = 1f;

        readonly Dictionary<ResourceType, CharacterResource> _lookup = new Dictionary<ResourceType, CharacterResource>();

        float _lastDamageTime = -100f;

        /// <summary>(resource, previousValue, newValue)</summary>
        public event Action<CharacterResource, float, float> Changed;
        public event Action<CharacterResource> Emptied;
        public event Action<CharacterResource> Filled;
        public event Action<float, GameObject> Damaged;
        public event Action<float> Healed;
        public event Action<CharacterResources> Died;

        public bool IsDead { get; private set; }
        public IReadOnlyList<CharacterResource> All { get { return resources; } }

        public CharacterContext Context { get; internal set; }

        void Awake()
        {
            EnsureDefaults();
        }

        void OnValidate()
        {
            EnsureDefaults();
        }

        void EnsureDefaults()
        {
            if (resources == null) resources = new List<CharacterResource>();

            bool hasHealth = false, hasStamina = false, hasEnergy = false;
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i] == null) continue;
                if (resources[i].type == ResourceType.Health) hasHealth = true;
                if (resources[i].type == ResourceType.Stamina) hasStamina = true;
                if (resources[i].type == ResourceType.Energy) hasEnergy = true;
                if (string.IsNullOrEmpty(resources[i].displayName)) resources[i].displayName = resources[i].type.ToString();
            }

            CharacterProfile p = Context != null ? Context.Profile : null;

            if (!hasHealth)
            {
                resources.Add(new CharacterResource
                {
                    type = ResourceType.Health,
                    displayName = "Health",
                    max = p != null ? p.maxHealth : 100f,
                    current = p != null ? p.maxHealth : 100f,
                    regenPerSecond = 0f,
                    regenDelay = 0f
                });
            }

            if (!hasStamina)
            {
                resources.Add(new CharacterResource
                {
                    type = ResourceType.Stamina,
                    displayName = "Stamina",
                    max = p != null ? p.maxStamina : 100f,
                    current = p != null ? p.maxStamina : 100f,
                    regenPerSecond = p != null ? p.staminaRegenPerSecond : 22f,
                    regenDelay = p != null ? p.staminaRegenDelay : 0.75f
                });
            }

            if (!hasEnergy)
            {
                resources.Add(new CharacterResource
                {
                    type = ResourceType.Energy,
                    displayName = "Energy",
                    max = 100f,
                    current = 100f,
                    regenPerSecond = 12f,
                    regenDelay = 1f
                });
            }

            RebuildLookup();
        }

        void RebuildLookup()
        {
            _lookup.Clear();
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i] == null) continue;
                _lookup[resources[i].type] = resources[i];
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < resources.Count; i++)
            {
                CharacterResource resource = resources[i];
                if (resource == null || resource.regenPerSecond <= 0f) continue;
                if (regenPaused || resource.regenPaused || IsDead) continue;
                if (Time.time - resource.lastSpendTime < resource.regenDelay) continue;
                if (resource.IsFull) continue;

                SetValue(resource, resource.current + resource.regenPerSecond * dt);
            }
        }

        // ---------------------------------------------------------------- queries

        public CharacterResource Get(ResourceType type)
        {
            CharacterResource resource;
            if (_lookup.TryGetValue(type, out resource)) return resource;
            return null;
        }

        public float GetNormalized(ResourceType type)
        {
            CharacterResource resource = Get(type);
            return resource != null ? resource.Normalized : 0f;
        }

        public bool Has(ResourceType type, float amount)
        {
            CharacterResource resource = Get(type);
            return resource != null && resource.current >= amount;
        }

        public bool IsFull(ResourceType type)
        {
            CharacterResource resource = Get(type);
            return resource == null || resource.IsFull;
        }

        // ---------------------------------------------------------------- mutation

        /// <summary>Spends a resource if there is enough of it. Returns false and changes nothing otherwise.</summary>
        public bool TrySpend(ResourceType type, float amount)
        {
            if (amount <= 0f) return true;

            CharacterResource resource = Get(type);
            if (resource == null) return true; // No pool of this type: treat the cost as free.
            if (resource.current < amount) return false;

            resource.lastSpendTime = Time.time;
            SetValue(resource, resource.current - amount);
            return true;
        }

        /// <summary>Spends without checking, used by continuous drains such as sprint and glide.</summary>
        public bool Drain(ResourceType type, float amount)
        {
            if (amount <= 0f) return true;

            CharacterResource resource = Get(type);
            if (resource == null) return true;
            if (resource.IsEmpty) return false;

            resource.lastSpendTime = Time.time;
            SetValue(resource, resource.current - amount);
            return true;
        }

        public void Refill(ResourceType type)
        {
            CharacterResource resource = Get(type);
            if (resource == null) return;
            SetValue(resource, resource.max);
        }

        public void RefillAll()
        {
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i] == null) continue;
                SetValue(resources[i], resources[i].max);
            }
            IsDead = false;
        }

        public void Set(ResourceType type, float value)
        {
            CharacterResource resource = Get(type);
            if (resource == null) return;
            SetValue(resource, value);
        }

        public void SetMax(ResourceType type, float max, bool keepRatio = true)
        {
            CharacterResource resource = Get(type);
            if (resource == null) return;

            float ratio = resource.Normalized;
            resource.max = Mathf.Max(1f, max);
            SetValue(resource, keepRatio ? resource.max * ratio : Mathf.Min(resource.current, resource.max));
        }

        /// <summary>Applies damage, respecting invulnerability windows and the global damage multiplier.</summary>
        public bool TakeDamage(float amount, GameObject source = null, bool ignoreInvulnerability = false)
        {
            if (IsDead || amount <= 0f) return false;
            if (!ignoreInvulnerability && Time.time - _lastDamageTime < invulnerabilityTime) return false;

            CharacterResource health = Get(ResourceType.Health);
            if (health == null) return false;

            float applied = amount * damageMultiplier;
            _lastDamageTime = Time.time;
            SetValue(health, health.current - applied);

            var handler = Damaged;
            if (handler != null) handler(applied, source);

            if (health.current <= 0f && !IsDead)
            {
                IsDead = true;
                var deathHandler = Died;
                if (deathHandler != null) deathHandler(this);
            }

            return true;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            CharacterResource health = Get(ResourceType.Health);
            if (health == null) return;

            SetValue(health, health.current + amount);
            var handler = Healed;
            if (handler != null) handler(amount);
        }

        /// <summary>Revives the character with the supplied health fraction.</summary>
        public void Revive(float healthFraction = 1f)
        {
            IsDead = false;
            CharacterResource health = Get(ResourceType.Health);
            if (health == null) return;
            SetValue(health, health.max * Mathf.Clamp01(healthFraction));
        }

        void SetValue(CharacterResource resource, float value)
        {
            float previous = resource.current;
            float clamped = Mathf.Clamp(value, 0f, resource.max);
            if (Mathf.Approximately(previous, clamped)) return;

            resource.current = clamped;

            var handler = Changed;
            if (handler != null) handler(resource, previous, clamped);

            if (clamped <= 0f)
            {
                var emptied = Emptied;
                if (emptied != null) emptied(resource);
            }
            else if (clamped >= resource.max && previous < resource.max)
            {
                var filled = Filled;
                if (filled != null) filled(resource);
            }
        }
    }
}