using System;
using UnityEngine;

namespace GridSquad
{
    [DisallowMultipleComponent]
    public sealed class EntityHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumHealth = 100;

        private int currentHealth;
        private bool initialized;

        public event Action<EntityHealth, int> DamageApplied;
        public event Action<EntityHealth, int> HealthRestored;
        public event Action<EntityHealth> HealthChanged;
        public event Action<EntityHealth> HealthDepleted;

        public int CurrentHealth => currentHealth;
        public int MaximumHealth => maximumHealth;
        public bool IsAlive => initialized && currentHealth > 0;

        private void Awake()
        {
            Initialize(maximumHealth, true);
        }

        public void Initialize(int newMaximumHealth, bool refillHealth)
        {
            maximumHealth = Mathf.Max(1, newMaximumHealth);
            if (!initialized || refillHealth)
                currentHealth = maximumHealth;
            else
                currentHealth = Mathf.Min(currentHealth, maximumHealth);
            initialized = true;
        }

        public void InitializeWithCurrentHealth(int newMaximumHealth, int newCurrentHealth)
        {
            maximumHealth = Mathf.Max(1, newMaximumHealth);
            currentHealth = Mathf.Clamp(newCurrentHealth, 0, maximumHealth);
            initialized = true;
            HealthChanged?.Invoke(this);
            if (currentHealth == 0)
                HealthDepleted?.Invoke(this);
        }

        public void UpdateMaximumHealthWithoutHealing(int newMaximumHealth)
        {
            int previousMaximumHealth = maximumHealth;
            int previousHealth = currentHealth;
            maximumHealth = Mathf.Max(1, newMaximumHealth);
            currentHealth = Mathf.Min(currentHealth, maximumHealth);
            if (!initialized)
                return;
            if (previousMaximumHealth != maximumHealth || previousHealth != currentHealth)
                HealthChanged?.Invoke(this);
            if (previousHealth > 0 && currentHealth == 0)
                HealthDepleted?.Invoke(this);
        }

        public int ApplyDamage(int damage)
        {
            if (!IsAlive)
                return 0;

            int previousHealth = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));
            int appliedDamage = previousHealth - currentHealth;
            if (appliedDamage <= 0)
                return 0;

            DamageApplied?.Invoke(this, appliedDamage);
            HealthChanged?.Invoke(this);
            if (currentHealth == 0)
                HealthDepleted?.Invoke(this);
            return appliedDamage;
        }

        public int RestoreHealth(int amount)
        {
            if (!IsAlive || amount <= 0)
                return 0;
            int previousHealth = currentHealth;
            currentHealth = Mathf.Min(maximumHealth, currentHealth + amount);
            int restored = currentHealth - previousHealth;
            if (restored <= 0)
                return 0;
            HealthRestored?.Invoke(this, restored);
            HealthChanged?.Invoke(this);
            return restored;
        }

#if UNITY_EDITOR
        public void SetEditorMaximumHealth(int newMaximumHealth)
        {
            maximumHealth = Mathf.Max(1, newMaximumHealth);
        }
#endif
    }
}
