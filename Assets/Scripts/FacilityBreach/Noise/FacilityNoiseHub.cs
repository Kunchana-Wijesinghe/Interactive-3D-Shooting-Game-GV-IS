using System;
using UnityEngine;

namespace FacilityBreach.Noise
{
    /// <summary>
    /// GV Part 2: Neutral Facility Noise Event Hub.
    /// Acts as a central, lightweight publication bus for in-game acoustic occurrences.
    /// Provides safe static reset on domain reload to prevent subscriber memory leaks.
    /// </summary>
    public static class FacilityNoiseHub
    {
        public static event Action<FacilityNoiseEvent> OnNoiseEmitted;

        public static void Emit(FacilityNoiseEvent noiseEvent)
        {
            OnNoiseEmitted?.Invoke(noiseEvent);
        }

        public static void Emit(Vector3 position, float radius, FacilityNoiseType type, GameObject source = null)
        {
            Emit(new FacilityNoiseEvent(position, radius, type, source));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnNoiseEmitted = null;
        }
    }
}
