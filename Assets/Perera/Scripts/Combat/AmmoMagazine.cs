using System;

namespace Perera.Combat
{
    /// <summary>
    /// Pure ammo logic (no Unity dependencies, unit-testable).
    /// Policy: finite reserve; reload tops the magazine up to capacity using
    /// reserve rounds; rounds left in the magazine are kept (no discard).
    /// </summary>
    public sealed class AmmoMagazine
    {
        public int Capacity { get; }
        public int InMagazine { get; private set; }
        public int Reserve { get; private set; }

        public AmmoMagazine(int capacity, int startingReserve)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            InMagazine = capacity;
            Reserve = Math.Max(0, startingReserve);
        }

        public bool CanFire => InMagazine > 0;
        public bool CanReload => InMagazine < Capacity && Reserve > 0;

        public bool TryConsume()
        {
            if (InMagazine <= 0) return false;
            InMagazine--;
            return true;
        }

        /// <summary>Moves rounds from reserve into the magazine. Returns rounds loaded.</summary>
        public int Reload()
        {
            int loaded = Math.Min(Capacity - InMagazine, Reserve);
            InMagazine += loaded;
            Reserve -= loaded;
            return loaded;
        }

        public void AddReserve(int rounds) => Reserve += Math.Max(0, rounds);
    }
}
