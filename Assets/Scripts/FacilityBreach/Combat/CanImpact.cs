using Perera.Core;
using UnityEngine;

namespace FacilityBreach
{
    public sealed class CanImpact : MonoBehaviour
    {
        private bool emitted;
        private float lifetime;
        private void OnCollisionEnter(Collision collision)
        {
            if (emitted) return; emitted = true;
            SoundEventBus.Emit(transform.position, 22, SoundCategory.Thrown, GetEntityId().GetHashCode());
        }
        private void Update() { lifetime += Time.deltaTime; if (lifetime >= 15) Destroy(gameObject); }
    }
}
