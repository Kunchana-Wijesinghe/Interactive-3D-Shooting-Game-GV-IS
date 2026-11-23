using System;
using System.Collections.Generic;
using UnityEngine;
using FacilityBreach.Audio;

namespace FacilityBreach.Environment
{
    /// <summary>
    /// GV Systems Engineer: Toxic Barrel Meltdown VFX & Pre-Detonation Warning.
    /// Manages the toxic barrels near the access card:
    /// 1. Pre-Detonation Smoke (Last 5 seconds of mission countdown):
    ///    Dense rising toxic smoke plumes and rapid overpressure lid pulsing.
    /// 2. Meltdown Detonation (Timeout / Critical Failure at t=0):
    ///    Violent physical explosion blast launching barrels with AddExplosionForce,
    ///    detonation fireball burst, and catastrophic explosion acoustics.
    /// </summary>
    public class FacilityToxicBarrelMeltdownVfx : MonoBehaviour
    {
        public static FacilityToxicBarrelMeltdownVfx Instance { get; private set; }

        [Header("Timing & Thresholds")]
        [Tooltip("Remaining seconds threshold at which smoke venting begins.")]
        [SerializeField] private float smokeWarningThreshold = 15.0f;

        [Header("Physical Explosion Parameters")]
        [SerializeField] private float blastForce = 1200f;
        [SerializeField] private float blastRadius = 16f;
        [SerializeField] private float blastUpwardModifier = 2.4f;

        // References
        private Vector3 clusterCenter;
        private readonly List<Transform> barrelTransforms = new List<Transform>();
        private readonly List<Renderer> glowingLidRenderers = new List<Renderer>();
        private readonly List<Material> lidMaterials = new List<Material>();

        private ParticleSystem smokeParticleSystem;
        private ParticleSystem explosionParticleSystem;
        private AudioSource ventingHissSource;

        private static Texture2D cachedSmokePuffTexture;

        /// <summary>
        /// Procedurally synthesizes a soft, organic Gaussian-feathered smoke puff texture with
        /// multi-frequency Perlin noise billowing contours. Eliminates untextured square quad artifacts.
        /// </summary>
        public static Texture2D GetOrCreateSmokePuffTexture()
        {
            if (cachedSmokePuffTexture != null) return cachedSmokePuffTexture;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Procedural_Realistic_SmokePuff_Alpha";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float invSize = 1.0f / (size - 1);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float ny = y * invSize;
                float dy = (ny - 0.5f) * 2.0f; // [-1, 1]

                for (int x = 0; x < size; x++)
                {
                    float nx = x * invSize;
                    float dx = (nx - 0.5f) * 2.0f; // [-1, 1]

                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 0.0f;

                    if (dist < 1.0f)
                    {
                        // Gaussian core falloff
                        float gaussian = Mathf.Exp(-3.2f * dist * dist);

                        // Organic noise octaves to produce billowing smoke puff lobes
                        float noise1 = Mathf.PerlinNoise(nx * 3.8f + 1.25f, ny * 3.8f + 2.73f);
                        float noise2 = Mathf.PerlinNoise(nx * 7.6f + 4.19f, ny * 7.6f + 6.31f) * 0.35f;
                        float organicPuff = gaussian * (0.65f + 0.35f * noise1 + 0.15f * noise2);

                        // Feathered outer boundary fade ensuring 0 alpha at perimeter
                        float edgeFade = Mathf.SmoothStep(1.0f, 0.0f, dist);
                        alpha = Mathf.Clamp01(organicPuff * edgeFade);
                    }

                    // Pure white with modulated alpha for particle shader tinting
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            cachedSmokePuffTexture = tex;
            return cachedSmokePuffTexture;
        }

        private static Material CreateParticleMaterial(string name, Color baseColor)
        {
            Shader shader = Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                         ?? Shader.Find("Mobile/Particles/Alpha Blended")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard");

            var mat = new Material(shader)
            {
                name = name,
                mainTexture = GetOrCreateSmokePuffTexture(),
                color = baseColor,
                renderQueue = 3000
            };

            // Configure Standard / Unlit particle material keywords for alpha blending
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 2); // 2 = Fade (Alpha Blended)
            }
            if (mat.HasProperty("_BlendMode"))
            {
                mat.SetFloat("_BlendMode", 0); // 0 = Alpha
            }

            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");

            return mat;
        }

        private bool isVentingSmoke = false;
        private bool hasDetonated = false;
        private float ventingTimer = 0f;

        public bool IsVentingSmoke => isVentingSmoke;
        public bool HasDetonated => hasDetonated;
        public Vector3 ClusterCenter => clusterCenter;
        public int TrackedBarrelCount => barrelTransforms.Count;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public void Initialize(Transform rootTransform, Vector3 center)
        {
            clusterCenter = center;
            barrelTransforms.Clear();
            glowingLidRenderers.Clear();
            lidMaterials.Clear();

            // Collect all barrel geometry under the toxic waste container
            foreach (Transform child in rootTransform)
            {
                barrelTransforms.Add(child);
                var rend = child.GetComponent<Renderer>();
                if (rend != null && child.name.Contains("lid"))
                {
                    glowingLidRenderers.Add(rend);
                    lidMaterials.Add(rend.material);
                }
            }

            BuildSmokeParticleSystem();
            BuildExplosionParticleSystem();
            SetupVentingAudio();
        }

        private void BuildSmokeParticleSystem()
        {
            if (smokeParticleSystem != null) return;

            var smokeGo = new GameObject("VFX_ToxicBarrel_SmokeVents");
            smokeGo.transform.SetParent(transform);
            smokeGo.transform.position = clusterCenter + Vector3.up * 0.9f;

            smokeParticleSystem = smokeGo.AddComponent<ParticleSystem>();
            smokeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smokeParticleSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 4.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startColor = new Color(0.20f, 0.26f, 0.18f, 0.70f); // Soft toxic soot/olive-gray
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.16f; // Naturally buoyant, rises smoothly

            var emission = smokeParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 32f;

            // Wide Cone shape to billow outward in 360-degree radial dispersion
            var shape = smokeParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 2.0f;
            shape.radiusThickness = 1.0f;

            // Multidirectional dispersion velocity: spreading out across X and Z while rising in Y
            var velocityOverLifetime = smokeParticleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.9f, 2.5f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);

            // Realistic Perlin noise turbulence for swirling eddies and organic smoke curls
            var noise = smokeParticleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.90f;
            noise.frequency = 0.40f;
            noise.scrollSpeed = 0.50f;
            noise.damping = true;
            noise.octaveCount = 2;

            // Angular rotation over lifetime to swirl while expanding
            var rotationOverLifetime = smokeParticleSystem.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-35f * Mathf.Deg2Rad, 35f * Mathf.Deg2Rad);

            // Size expansion curve: starts compact at barrel vent (0.7x), expands to 4.8x as it dissipates
            var sizeOverLifetime = smokeParticleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.7f);
            curve.AddKey(0.35f, 2.4f);
            curve.AddKey(1.0f, 4.8f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

            var colorOverLifetime = smokeParticleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.24f, 0.32f, 0.20f), 0.0f),
                    new GradientColorKey(new Color(0.15f, 0.18f, 0.14f), 0.5f),
                    new GradientColorKey(new Color(0.08f, 0.09f, 0.08f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.70f, 0.15f),
                    new GradientAlphaKey(0.45f, 0.65f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            // Renderer setup with soft organic puff material (eliminates square quads)
            var rend = smokeGo.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.alignment = ParticleSystemRenderSpace.View;
            rend.sortMode = ParticleSystemSortMode.Distance;
            rend.material = CreateParticleMaterial("Mat_ToxicSmoke_SoftPuff", new Color(0.22f, 0.30f, 0.19f, 0.70f));

            smokeParticleSystem.Stop();
        }

        private void BuildExplosionParticleSystem()
        {
            if (explosionParticleSystem != null) return;

            var expGo = new GameObject("VFX_ToxicBarrel_ExplosionBurst");
            expGo.transform.SetParent(transform);
            expGo.transform.position = clusterCenter + Vector3.up * 0.5f;

            explosionParticleSystem = expGo.AddComponent<ParticleSystem>();
            explosionParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = explosionParticleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.startLifetime = 1.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.0f, 14.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
            main.startColor = new Color(1.0f, 0.55f, 0.15f, 0.95f); // Blinding fire-orange
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.35f;

            var emission = explosionParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 60)
            });

            var shape = explosionParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.5f;

            var rend = expGo.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.alignment = ParticleSystemRenderSpace.View;
            rend.sortMode = ParticleSystemSortMode.Distance;
            rend.material = CreateParticleMaterial("Mat_MeltdownExplosion_SoftFire", new Color(1.0f, 0.65f, 0.20f, 0.95f));

            explosionParticleSystem.Stop();
        }

        private void SetupVentingAudio()
        {
            if (ventingHissSource != null) return;
            ventingHissSource = gameObject.AddComponent<AudioSource>();
            ventingHissSource.playOnAwake = false;
            ventingHissSource.loop = true;
            ventingHissSource.spatialBlend = 1.0f; // 3D spatialized at barrels
            ventingHissSource.minDistance = 2.0f;
            ventingHissSource.maxDistance = 22.0f;
            ventingHissSource.volume = 0.55f;
            ventingHissSource.clip = FacilityAudioSynthesizer.GetOrCreateClip(FacilityAudioClipType.EmergencySirenLoop);
            ventingHissSource.pitch = 1.85f; // Higher pitch whistle/vent
        }

        public void UpdateCountdown(float remainingSeconds)
        {
            if (hasDetonated) return;

            // Trigger smoke venting in last 15 seconds of countdown with realistic dynamic escalation
            if (remainingSeconds <= smokeWarningThreshold && remainingSeconds > 0f)
            {
                if (!isVentingSmoke)
                {
                    StartSmokeVenting();
                }

                ventingTimer += Time.deltaTime;

                // Urgency progression (0 at 15s remaining -> 1.0 at 0s remaining)
                float urgency = Mathf.Clamp01((smokeWarningThreshold - remainingSeconds) / smokeWarningThreshold);

                // Dynamically ramp up particle emission rate as meltdown draws nearer (25 -> 65 particles/s)
                if (smokeParticleSystem != null)
                {
                    var em = smokeParticleSystem.emission;
                    em.rateOverTime = Mathf.Lerp(25f, 65f, urgency);
                }

                // Dynamically escalate audio venting volume and pitch
                if (ventingHissSource != null)
                {
                    ventingHissSource.volume = Mathf.Lerp(0.35f, 0.75f, urgency);
                    ventingHissSource.pitch = Mathf.Lerp(1.4f, 2.1f, urgency);
                }

                // Escalating warning pulse on glowing barrel lids (flashing speed ramps from 4Hz to 18Hz)
                float pulseFreq = Mathf.Lerp(4f, 18f, urgency);
                float pulse = (Mathf.Sin(ventingTimer * pulseFreq) * 0.5f + 0.5f);
                Color warningEmission = Color.Lerp(
                    new Color(0.2f, 1.0f, 0.25f) * 1.5f,
                    new Color(1.0f, 0.7f, 0.1f) * 3.5f,
                    pulse
                );

                foreach (var mat in lidMaterials)
                {
                    if (mat != null)
                    {
                        mat.SetColor("_EmissionColor", warningEmission);
                    }
                }
            }
            else if (remainingSeconds <= 0f && isVentingSmoke && !hasDetonated)
            {
                Detonate();
            }
        }

        public void StartSmokeVenting()
        {
            if (isVentingSmoke || hasDetonated) return;
            isVentingSmoke = true;

            if (smokeParticleSystem != null)
            {
                smokeParticleSystem.Play();
            }

            if (ventingHissSource != null && !ventingHissSource.isPlaying)
            {
                ventingHissSource.Play();
            }
        }

        public void Detonate()
        {
            if (hasDetonated) return;
            hasDetonated = true;
            isVentingSmoke = false;

            // 1. Stop venting and audio
            if (smokeParticleSystem != null)
            {
                smokeParticleSystem.Stop();
            }
            if (ventingHissSource != null && ventingHissSource.isPlaying)
            {
                ventingHissSource.Stop();
            }

            // 2. Fire explosion burst VFX
            if (explosionParticleSystem != null)
            {
                explosionParticleSystem.Play();
            }

            // 3. Physical Blast: Convert visual drum parts to dynamic rigidbodies and blast outward
            foreach (var barrel in barrelTransforms)
            {
                if (barrel == null) continue;

                var rb = barrel.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = barrel.gameObject.AddComponent<Rigidbody>();
                    rb.mass = 28f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                    var col = barrel.GetComponent<Collider>();
                    if (col == null)
                    {
                        var cap = barrel.gameObject.AddComponent<CapsuleCollider>();
                        cap.height = 1.0f;
                        cap.radius = 0.45f;
                    }
                }

                if (rb != null)
                {
                    rb.isKinematic = false;
                    // Violent blast outward from cluster center
                    rb.AddExplosionForce(blastForce, clusterCenter, blastRadius, blastUpwardModifier, ForceMode.Impulse);
                    rb.AddTorque(UnityEngine.Random.insideUnitSphere * 450f, ForceMode.Impulse);
                }
            }

            // 4. Play audio at toxic barrel cluster
            if (FacilityAudioPlayer.Instance != null)
            {
                FacilityAudioPlayer.Instance.PlayCatastrophicExplosion(clusterCenter, 1.0f);
            }
        }

        public void ResetVfx()
        {
            isVentingSmoke = false;
            hasDetonated = false;
            ventingTimer = 0f;

            if (smokeParticleSystem != null) smokeParticleSystem.Stop();
            if (explosionParticleSystem != null) explosionParticleSystem.Stop();
            if (ventingHissSource != null) ventingHissSource.Stop();
        }
    }
}
