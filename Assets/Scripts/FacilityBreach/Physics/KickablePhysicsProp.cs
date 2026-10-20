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
        [SerializeField] private float linearDamping = 0.50f;
        [SerializeField] private float angularDamping = 1.0f;

        [Header("Rolling Audio (Metal Barrels)")]
        [SerializeField] private float minRollSpeed = 0.35f;
        [SerializeField] private float stopRollSpeed = 0.18f;
        [SerializeField] private float maxRollSpeed = 5.0f;
        [SerializeField] private float maxRollVolume = 0.85f;
        [SerializeField] private float rollFadeSpeed = 4.0f;

        private Rigidbody body;
        private float lastImpactSoundTime = -10f;
        private float lastKickTime = -10f;
        private AudioSource rollingAudioSource;
        private float currentRollVolume = 0f;
        private float lastContactTime = -10f;
        private bool isContactActive;

        public Rigidbody Body => body != null ? body : (body = GetComponent<Rigidbody>());
        public PropMaterialType MaterialType
        {
            get => materialType;
            set
            {
                materialType = value;
                if (materialType == PropMaterialType.Metal)
                {
                    EnsureRollingAudioSource();
                }
                else if (rollingAudioSource != null && rollingAudioSource.isPlaying)
                {
                    rollingAudioSource.Stop();
                    currentRollVolume = 0f;
                }
            }
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

        public float MinRollSpeed
        {
            get => minRollSpeed;
            set => minRollSpeed = Mathf.Max(0.05f, value);
        }

        public float StopRollSpeed
        {
            get => stopRollSpeed;
            set => stopRollSpeed = Mathf.Max(0.02f, value);
        }

        public float MaxRollVolume
        {
            get => maxRollVolume;
            set => maxRollVolume = Mathf.Clamp01(value);
        }

        public bool IsRollingAudioActive => currentRollVolume > 0.01f;
        public float CurrentRollVolume => currentRollVolume;
        public AudioSource RollingAudioSource => rollingAudioSource;
        public bool IsContactActive
        {
            get => isContactActive;
            set => isContactActive = value;
        }

        public bool IsKickedRecently => (Time.time - lastKickTime) < 0.5f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ConfigurePhysics();
            if (materialType == PropMaterialType.Metal) EnsureRollingAudioSource();
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

        public void EnsureRollingAudioSource()
        {
            if (materialType != PropMaterialType.Metal) return;
            if (rollingAudioSource != null) return;

            rollingAudioSource = GetComponent<AudioSource>();
            if (rollingAudioSource == null) rollingAudioSource = gameObject.AddComponent<AudioSource>();

            rollingAudioSource.clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.RollingMetal);
            rollingAudioSource.loop = true;
            rollingAudioSource.playOnAwake = false;
            rollingAudioSource.spatialBlend = 1.0f; // Full 3D spatialization
            rollingAudioSource.rolloffMode = AudioRolloffMode.Linear;
            rollingAudioSource.minDistance = 1.5f;
            rollingAudioSource.maxDistance = 28.0f;
            rollingAudioSource.dopplerLevel = 0f;
            rollingAudioSource.spread = 45f;
            rollingAudioSource.volume = 0f;
        }

        private void Update()
        {
            UpdateRollingAudio();
        }

        public void UpdateRollingAudio(float deltaTime = -1f)
        {
            if (materialType != PropMaterialType.Metal)
            {
                if (rollingAudioSource != null && rollingAudioSource.isPlaying)
                {
                    rollingAudioSource.Stop();
                }
                currentRollVolume = 0f;
                return;
            }

            if (rollingAudioSource == null)
            {
                EnsureRollingAudioSource();
                if (rollingAudioSource == null) return;
            }

            float dt = deltaTime >= 0f ? deltaTime : (Time.deltaTime > 0f ? Time.deltaTime : 0.02f);

            // Surface contact check: active collision stay, recent collision within 0.15s, or ground raycast
            bool grounded = isContactActive || (Time.time - lastContactTime < 0.15f);
            if (!grounded && Body != null)
            {
                grounded = Physics.Raycast(transform.position, Vector3.down, 0.85f, ~0, QueryTriggerInteraction.Ignore);
            }

            // Extract horizontal speed and rotational angular speed (ignoring purely vertical falling/jitter)
            Vector3 vel = Body != null ? Body.linearVelocity : Vector3.zero;
            float horizontalSpeed = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
            Vector3 angVel = Body != null ? Body.angularVelocity : Vector3.zero;
            float angularSpeed = angVel.magnitude;

            // Effective roll metric: combination of horizontal speed and cylinder roll rotation
            float rollMetric = Mathf.Max(horizontalSpeed, angularSpeed * 0.35f);

            // Start/stop hysteresis: needs minRollSpeed to activate, maintains down to stopRollSpeed
            float activeThreshold = currentRollVolume > 0.01f ? stopRollSpeed : minRollSpeed;
            bool shouldRoll = grounded && (rollMetric > activeThreshold);

            float targetVolume = 0f;
            float targetPitch = 1.0f;

            if (shouldRoll)
            {
                // Speed normalization across active range (0.20 to maxRollSpeed 5.0m/s)
                float speedNorm = Mathf.Clamp01((rollMetric - 0.20f) / Mathf.Max(0.1f, maxRollSpeed - 0.20f));

                // Natural perceptual volume curve: quiet at slow speeds, rich at moderate speeds, punchy when kicked hard
                targetVolume = Mathf.Lerp(0.08f, maxRollVolume, Mathf.Sqrt(speedNorm));

                // Dynamic speed-to-pitch response: deep low rumble (0.70x) up to fast rattling churn (1.35x)
                targetPitch = Mathf.Lerp(0.70f, 1.35f, speedNorm);
            }

            // Asymmetric smoothing: responsive attack on kick acceleration, smooth organic decay on stop
            float fadeRate = targetVolume > currentRollVolume ? 6.0f : rollFadeSpeed;
            float fadeDelta = fadeRate * dt;
            currentRollVolume = Mathf.MoveTowards(currentRollVolume, targetVolume, fadeDelta);

            if (currentRollVolume > 0.005f)
            {
                if (!rollingAudioSource.isPlaying)
                {
                    rollingAudioSource.Play();
                }
                rollingAudioSource.volume = currentRollVolume;
                rollingAudioSource.pitch = targetPitch;
            }
            else
            {
                if (rollingAudioSource.isPlaying)
                {
                    rollingAudioSource.Stop();
                }
                currentRollVolume = 0f;
                rollingAudioSource.volume = 0f;
            }
        }

        private void OnDisable()
        {
            if (rollingAudioSource != null && rollingAudioSource.isPlaying)
            {
                rollingAudioSource.Stop();
            }
            currentRollVolume = 0f;
            isContactActive = false;
        }

        private void OnDestroy()
        {
            if (rollingAudioSource != null)
            {
                rollingAudioSource.Stop();
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            lastContactTime = Time.time;
            isContactActive = true;
        }

        private void OnCollisionExit(Collision collision)
        {
            isContactActive = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            lastContactTime = Time.time;
            isContactActive = true;

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
