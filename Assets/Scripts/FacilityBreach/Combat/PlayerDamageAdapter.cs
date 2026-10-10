using Perera.Core;
using UnityEngine;

namespace FacilityBreach.Combat
{
    public sealed class PlayerDamageAdapter : MonoBehaviour, IDamageable
    {
        public FacilityBreachRuntime Runtime;
        public bool IsDead => Runtime == null || !Runtime.Resources.Alive;
        public bool TakeDamage(in DamageInfo hit)
        {
            if (IsDead || !Runtime.Run.Running || Runtime.Run.Paused || hit.Amount <= 0 || !float.IsFinite(hit.Amount)) return false;
            Runtime.Resources.Hurt(Mathf.CeilToInt(hit.Amount));
            if (IsDead) { Runtime.Run.Lose("Player eliminated"); Runtime.CancelEnemyAttacks(); }
            return true;
        }
    }
}
