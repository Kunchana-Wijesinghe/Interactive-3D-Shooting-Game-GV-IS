using FacilityBreach.Audio;
using Perera.Core;
using UnityEngine;

namespace FacilityBreach
{
    public sealed class CanImpact : MonoBehaviour
    {
        private bool emitted;
        private float lifetime;
        private float lastImpactSoundTime = -10f;
        private const float MinImpactVelocity = 0.8f;
        private const float ImpactCooldown = 0.10f;

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed >= MinImpactVelocity && (Time.time - lastImpactSoundTime) >= ImpactCooldown)
            {
                lastImpactSoundTime = Time.time;
                Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                float volume = Mathf.Clamp01(speed / 6.0f + 0.35f);
                FacilityAudioPlayer.Instance.PlayProjectileImpact(contactPoint, volume);
            }

            if (emitted) return;
            emitted = true;
            SoundEventBus.Emit(transform.position, 22, SoundCategory.Thrown, GetEntityId().GetHashCode());
        }

        private void Update() { lifetime += Time.deltaTime; if (lifetime >= 15) Destroy(gameObject); }
    }
}
