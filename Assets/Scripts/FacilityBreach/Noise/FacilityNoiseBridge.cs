using Perera.Core;
using UnityEngine;

namespace FacilityBreach.Noise
{
    /// <summary>
    /// Forwards the facility's gameplay noises (kicks, prop impacts, sprint footsteps) onto the shared SoundEventBus
    /// so listeners such as the Sound Hunter can hear them. Gunshots are skipped: the runtime already emits those on the bus.
    /// Emitters stay decoupled from listeners; the radius is used as the hearing budget.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacilityNoiseBridge : MonoBehaviour
    {
        private void OnEnable() { FacilityNoiseHub.OnNoiseEmitted -= Forward; FacilityNoiseHub.OnNoiseEmitted += Forward; }
        private void OnDisable() { FacilityNoiseHub.OnNoiseEmitted -= Forward; }

        private static void Forward(FacilityNoiseEvent noise)
        {
            SoundCategory category;
            switch (noise.Type)
            {
                case FacilityNoiseType.Kick:
                case FacilityNoiseType.PhysicsImpact:
                case FacilityNoiseType.Interaction: category = SoundCategory.Interaction; break;
                case FacilityNoiseType.Footstep: category = SoundCategory.Footstep; break;
                default: return;
            }
            SoundEventBus.Emit(noise.Position, noise.Radius, category, noise.Source ? noise.Source.GetEntityId().GetHashCode() : 0);
        }
    }
}
