using FacilityBreach.Audio;
using FacilityBreach.Noise;
using UnityEngine;

namespace FacilityBreach
{
    public enum PropMaterialType
    {
        Wood,
        Metal
    }

    /// <summary>
    /// GV Part 2: Kickable Physics Prop Component.
    /// Manages physical properties, collision response, audible impact playback,
    /// and neutral noise event publication for interactive physics objects (boxes, crates, barrels).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KickablePhysicsProp : MonoBehaviour
    {
        [Header("Material & Audio Profile")]
        [SerializeField] private PropMaterialType materialType = PropMaterialType.Wood;
        [SerializeField] private float minImpactVelocity = 1.0f;
        [SerializeField] private float impactCooldown = 0.15f;
        [SerializeField] private float maxNoiseRadius = 15.0f;

        [Header("Physical Constraints")]
        [SerializeField] private float baseMass = 4.0f;
        [SerializeField] private float linearDamping = 1.0f;
        [SerializeField] private float angularDamping = 1.5f;

        private Rigidbody body;
        private float lastImpactSoundTime = -10f;
        private float lastKickTime = -10f;

        public Rigidbody Body => body != null ? body : (body = GetComponent<Rigidbody>());
        public PropMaterialType MaterialType
        {
            get => materialType;
            set => materialType = value;
        }

        public float MinImpactVelocity
        {
            get => minImpactVelocity;
            set => minImpactVelocity = Mathf.Max(0.1f, value);
        }

        public float ImpactCooldown
        {
            get => impactCooldown;
            set => impactCooldown = Mathf.Max(0.01f, value);
        }

        public float BaseMass
        {
            get => baseMass;
            set
            {
                baseMass = Mathf.Max(0.5f, value);
                if (Body != null) Body.mass = baseMass;
            }
        }

        public float LinearDamping
        {
            get => linearDamping;
            set
            {
                linearDamping = Mathf.Max(0.0f, value);
                if (Body != null) Body.linearDamping = linearDamping;
            }
        }

        public float AngularDamping
        {
            get => angularDamping;
            set
            {
                angularDamping = Mathf.Max(0.0f, value);
                if (Body != null) Body.angularDamping = angularDamping;
            }
        }

        public bool IsKickedRecently => (Time.time - lastKickTime) < 0.5f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ConfigurePhysics();
        }

        public void ConfigurePhysics()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (body == null) return;

            body.mass = baseMass;
            body.linearDamping = linearDamping;
            body.angularDamping = angularDamping;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 30.0f;
        }

        /// <summary>
        /// Applies a directional kick impulse at the specified impact point.
        /// Initiates natural forward rolling torque for cylindrical metal barrels.
        /// </summary>
        public bool ApplyKick(Vector3 impulse, Vector3 hitPoint)
        {
            if (Body == null || Body.isKinematic) return false;

            lastKickTime = Time.time;
            Body.AddForceAtPosition(impulse, hitPoint, ForceMode.Impulse);

            // For metal cylindrical props (barrels), add rolling torque around horizontal axis perpendicular to impulse
            if (materialType == PropMaterialType.Metal)
            {
                Vector3 horizontalFwd = Vector3.ProjectOnPlane(impulse, Vector3.up).normalized;
                if (horizontalFwd.sqrMagnitude > 0.01f)
                {
                    Vector3 rollAxis = Vector3.Cross(Vector3.up, horizontalFwd).normalized;
                    Body.AddTorque(rollAxis * (impulse.magnitude * 0.35f), ForceMode.Impulse);
                }
            }

            return true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Ignore sub-threshold cooldown to prevent audio spam on sliding / continuous contacts
            if (Time.time - lastImpactSoundTime < impactCooldown) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minImpactVelocity) return;

            lastImpactSoundTime = Time.time;

            Vector3 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

            // Compute scaled volume (0.25 to 1.0) based on collision velocity
            float volume = Mathf.Clamp01((speed - minImpactVelocity) / 6.0f + 0.30f);

            FacilityAudioClipType clipType = materialType switch
            {
                PropMaterialType.Wood  => FacilityAudioClipType.ImpactWood,
                PropMaterialType.Metal => FacilityAudioClipType.ImpactMetal,
                _                      => FacilityAudioClipType.ImpactWood
            };

            FacilityAudioPlayer.Instance.PlayImpact(clipType, contactPoint, volume);

            // Emit neutral noise event for physics impact
            float noiseRadius = Mathf.Clamp(speed * 1.6f, 3.0f, maxNoiseRadius);
            FacilityNoiseHub.Emit(contactPoint, noiseRadius, FacilityNoiseType.PhysicsImpact, gameObject);
        }
    }
}
