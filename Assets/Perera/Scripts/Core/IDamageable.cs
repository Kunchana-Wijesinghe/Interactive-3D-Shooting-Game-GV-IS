using UnityEngine;

namespace Perera.Core
{
    /// <summary>Data describing one instance of damage.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly GameObject Source;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
        }
    }

    /// <summary>
    /// Shared damage contract. Resolve from a hit collider with
    /// collider.GetComponentInParent&lt;IDamageable&gt;() so child colliders work.
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }

        /// <summary>Returns true if the damage was applied (false when dead or invalid).</summary>
        bool TakeDamage(in DamageInfo info);
    }
}
