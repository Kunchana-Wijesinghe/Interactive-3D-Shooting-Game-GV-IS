using System;
using UnityEngine;

namespace Perera.Core
{
    /// <summary>
    /// Current/max health with damage and death notifications.
    /// Death fires exactly once; later damage is ignored until ResetHealth().
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool debugLog;

        public event Action<Health, DamageInfo> Damaged;
        public event Action<Health> Died;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        private void Awake() => ResetHealth();

        public void ResetHealth()
        {
            Current = maxHealth;
            IsDead = false;
        }

        public bool TakeDamage(in DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f) return false;

            Current = Mathf.Max(0f, Current - info.Amount);
            if (debugLog) Debug.Log($"[Health] {name} took {info.Amount} -> {Current}/{maxHealth}", this);
            Damaged?.Invoke(this, info);

            if (Current <= 0f)
            {
                IsDead = true;
                if (debugLog) Debug.Log($"[Health] {name} died", this);
                Died?.Invoke(this);
            }
            return true;
        }
    }
}
