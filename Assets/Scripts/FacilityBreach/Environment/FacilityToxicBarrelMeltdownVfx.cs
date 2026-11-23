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
        /// Procedurally synthesizes an ultra-soft, organic multi-lobed Gaussian smoke puff texture
        /// with multi-octave Perlin noise billowing contours. Eliminates untextured square quad artifacts
        /// and guarantees zero-alpha feathered borders.
        /// </summary>
        public static Texture2D GetOrCreateSmokePuffTexture()
        {
            if (cachedSmokePuffTexture != null) return cachedSmokePuffTexture;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Procedural_Realistic_SmokePuff_Alpha";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float invSize = 1.0f / (size - 1);
            Color[] pixels = new Color[size * size];

            // 5 organic sub-puff centers that give smoke cloud natural billowing lobes
            Vector2[] lobes = new Vector2[]
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(-0.25f, 0.18f),
                new Vector2(0.26f, 0.15f),
                new Vector2(-0.18f, -0.22f),
                new Vector2(0.20f, -0.20f)
            };
            float[] lobeWeights = new float[] { 1.0f, 0.82f, 0.78f, 0.72f, 0.72f };
            float[] lobeRadii = new float[] { 0.58f, 0.44f, 0.42f, 0.40f, 0.40f };

            for (int y = 0; y < size; y++)
            {
                float ny = y * invSize;
                float dy = (ny - 0.5f) * 2.0f; // [-1, 1]

                for (int x = 0; x < size; x++)
                {
                    float nx = x * invSize;
                    float dx = (nx - 0.5f) * 2.0f; // [-1, 1]

                    float maxLobeAlpha = 0f;
                    for (int i = 0; i < lobes.Length; i++)
                    {
                        float ldx = dx - lobes[i].x;
                        float ldy = dy - lobes[i].y;
                        float ldist = Mathf.Sqrt(ldx * ldx + ldy * ldy) / lobeRadii[i];
                        if (ldist < 1.0f)
                        {
                            // Soft Gaussian density within lobe
                            float g = Mathf.Exp(-2.8f * ldist * ldist) * lobeWeights[i];
                            if (g > maxLobeAlpha) maxLobeAlpha = g;
                        }
                    }

                    float distFromCenter = Mathf.Sqrt(dx * dx + dy * dy);
                    // Feather to complete 0.0 before reaching border (guarantees no box edges)
                    float edgeFade = Mathf.SmoothStep(0.95f, 0.25f, distFromCenter);

                    float noise1 = Mathf.PerlinNoise(nx * 4.2f + 1.25f, ny * 4.2f + 2.73f);
                    float noise2 = Mathf.PerlinNoise(nx * 8.4f + 5.11f, ny * 8.4f + 7.43f) * 0.35f;
                    float organicMod = 0.65f + 0.35f * noise1 + 0.15f * noise2;

                    float finalAlpha = Mathf.Clamp01(maxLobeAlpha * organicMod * edgeFade);

                    // Soft smoky off-white so material tinting looks natural
                    pixels[y * size + x] = new Color(0.92f, 0.94f, 0.90f, finalAlpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            cachedSmokePuffTexture = tex;
            return cachedSmokePuffTexture;
        }

        private static Material CreateParticleMaterial(string name, Color baseColor)
        {
            // Prefer dedicated particle alpha-blended shaders first
            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                         ?? Shader.Find("Mobile/Particles/Alpha Blended")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Transparent")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Standard");

            var mat = new Material(shader)
            {
                name = name,
                mainTexture = GetOrCreateSmokePuffTexture(),
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
            };

            // Set main texture across all shader property naming standards
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mat.mainTexture);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mat.mainTexture);

            // Set tint colors across both legacy particles (_TintColor) and standard (_Color)
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

            // Explicitly force alpha blending render states if Standard / Particles Standard shader is picked
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 2f); // 2 = Fade (Alpha)
            if (mat.HasProperty("_BlendMode")) mat.SetFloat("_BlendMode", 0f); // 0 = Alpha blend
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);

            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.DisableKeyword("_ALPHATEST_ON");

            return mat;
        }

        [ContextMenu("Toggle Smoke Venting (Debug)")]
        public void DebugToggleSmoke()
        {
            if (isVentingSmoke) ResetVfx();
            else StartSmokeVenting();
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.2f, 6.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.3f, 2.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startColor = new Color(0.24f, 0.28f, 0.20f, 0.60f); // Translucent toxic soot/olive-gray
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.14f; // Naturally buoyant, rises smoothly

            var emission = smokeParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 35f;

            // Wide Cone shape to billow outward in 360-degree radial dispersion
            var shape = smokeParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 2.4f;
            shape.radiusThickness = 1.0f;

            // Multidirectional dispersion velocity: spreading out across X and Z while rising in Y
            var velocityOverLifetime = smokeParticleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(1.1f, 2.8f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);

            // Realistic Perlin noise turbulence for swirling eddies and organic smoke curls
            var noise = smokeParticleSystem.noise;
            noise.enabled = true;
            noise.strength = 1.15f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.60f;
            noise.damping = true;
            noise.octaveCount = 2;

            // Angular rotation over lifetime to swirl while expanding
            var rotationOverLifetime = smokeParticleSystem.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-35f * Mathf.Deg2Rad, 35f * Mathf.Deg2Rad);

            // Size expansion curve: starts compact at barrel vent (0.6x), expands to 5.5x as a huge billowing cloud
            var sizeOverLifetime = smokeParticleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.6f);
            curve.AddKey(0.35f, 2.6f);
            curve.AddKey(1.0f, 5.5f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

            var colorOverLifetime = smokeParticleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.26f, 0.35f, 0.22f), 0.0f),
                    new GradientColorKey(new Color(0.18f, 0.20f, 0.16f), 0.5f),
                    new GradientColorKey(new Color(0.10f, 0.11f, 0.10f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.60f, 0.18f),
                    new GradientAlphaKey(0.35f, 0.65f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            // Renderer setup with soft organic puff material (eliminates square quads)
            var rend = smokeGo.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.alignment = ParticleSystemRenderSpace.View;
            rend.sortMode = ParticleSystemSortMode.Distance;
            rend.material = CreateParticleMaterial("Mat_ToxicSmoke_SoftPuff", new Color(0.24f, 0.32f, 0.20f, 0.60f));

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
