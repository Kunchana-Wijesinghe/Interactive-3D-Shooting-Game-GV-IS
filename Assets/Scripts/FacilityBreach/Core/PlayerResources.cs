using System;

namespace FacilityBreach.Core
{
    // One reserve shared by the three prototype weapons. Each magazine belongs to its weapon.
    public sealed class PlayerResources
    {
        public int Health { get; private set; } = 100;
        public int Reserve { get; private set; } = 50;
        public int Equipped { get; private set; }
        public readonly int[] Capacities = { 50, 75, 100 };
        public readonly int[] Damage = { 10, 15, 20 };
        private readonly int[] magazines = { 50, 75, 100 };
        public int Magazine => magazines[Equipped];
        public bool Alive => Health > 0;
        public void Hurt(int amount) { if (amount > 0) Health = Math.Max(0, Health - amount); }
        public bool Heal() { if (!Alive || Health > 50) return false; Health = Math.Min(100, Health + 50); return true; }
        public int CollectAmmo(int available)
        { int transfer = Math.Min(Math.Max(0, available), 200 - Reserve); Reserve += transfer; return transfer; }
        public bool Shoot() { if (!Alive || Magazine <= 0) return false; magazines[Equipped]--; return true; }
        public bool CanReload => Alive && Reserve > 0 && Magazine < Capacities[Equipped];
        public int Reload()
        { if (!CanReload) return 0; int n = Math.Min(Reserve, Capacities[Equipped] - Magazine); Reserve -= n; magazines[Equipped] += n; return n; }
        public int Swap(int weapon)
        { if (weapon < 0 || weapon > 2) throw new ArgumentOutOfRangeException(nameof(weapon)); int old = Equipped; Equipped = weapon; return old; }
    }
}
