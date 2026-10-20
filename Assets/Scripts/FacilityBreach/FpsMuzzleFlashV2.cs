using System;
using UnityEngine;

namespace FacilityBreach
{
    /// <summary>
    /// Visual-only first-person muzzle flash component (V2) for FacilityBreach.
    /// Uses a camera-facing billboard quad positioned at the active weapon muzzle
    /// with an explicitly configured transparent unlit material and procedural soft-alpha star texture.
    /// Strictly visual: contains zero lights, zero colliders, depth writing disabled, and no HDR emission.
    /// </summary>
    [DisallowMultipleComponent]
    public class FpsMuzzleFlashV2 : MonoBehaviour
    {
        [Header("Muzzle Flash Settings")]
        [Tooltip("Lifetime of the visual muzzle flash in seconds (0.055 - 0.075s).")]
        [SerializeField] private float flashDuration = 0.065f;

        [Tooltip("Size (diameter) of the camera-facing billboard quad in meters.")]
        [SerializeField] private float flashSize = 0.080f;

        [Tooltip("Color tint of the luminous muzzle flash.")]
        [SerializeField] private Color flashColor = new Color(1.0f, 0.85f, 0.45f, 0.95f);

        [Header("Camera Reference")]
        [Tooltip("Optional explicit reference to player camera. Falls back to Camera.main if unset.")]
        [SerializeField] private Camera playerCamera;

        // References & State
        private Transform currentMuzzle;
        private GameObject flashObject;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material sharedMaterial;
        private Mesh sharedMesh;
        private Texture2D sharedTexture;
        private float timer;
        private float currentRollAngle;

        // Telemetry & Diagnostic Properties for Unit Testing
        public float FlashDuration { get => flashDuration; set => flashDuration = value; }
        public float FlashSize => flashSize;
        public Color FlashColor => flashColor;
        public Transform CurrentMuzzle => currentMuzzle;
        public Camera PlayerCamera { get => playerCamera; set => playerCamera = value; }
        public GameObject FlashObject => flashObject;
        public Material SharedMaterial => sharedMaterial;
        public Mesh SharedMesh => sharedMesh;
        public Texture2D SharedTexture => sharedTexture;
        public bool IsFlashActive => flashObject != null && flashObject.activeSelf;
        public int TriggerCount { get; private set; }
        public Vector3 LastFlashPosition { get; private set; }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void Initialize(Transform initialMuzzle = null)
        {
            EnsureInitialized();
            if (initialMuzzle != null)
            {
                NotifyWeaponChanged(initialMuzzle);
            }
        }

        public void NotifyWeaponChanged(Transform newMuzzle)
        {
            currentMuzzle = newMuzzle;
        }

        /// <summary>
        /// Triggers the visual muzzle flash at the active weapon muzzle.
        /// Positions and aligns the camera-facing quad directly at the barrel tip.
        /// Does NOT modify raycasts, ammo, damage, or camera settings.
        /// </summary>
        public void TriggerFlash(Transform firingMuzzle = null)
        {
            EnsureInitialized();

            Transform activeMuzzle = firingMuzzle != null ? firingMuzzle : currentMuzzle;
            if (activeMuzzle == null && transform != null)
            {
                activeMuzzle = transform;
            }

            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;

            Vector3 muzzlePos = activeMuzzle != null ? activeMuzzle.position : transform.position;

            // Camera near-clip safety: ensure the flash quad never penetrates the camera's near plane (0.05m)
            if (cam != null)
            {
                float depth = Vector3.Dot(muzzlePos - camPos, camFwd);
                if (depth < 0.10f)
                {
                    // If retracted close to camera near-plane, safely clamp forward along camera view ray
                    muzzlePos = camPos + camFwd * 0.12f + Vector3.ProjectOnPlane(muzzlePos - camPos, camFwd);
                }
                else
                {
                    // Gentle 1.5cm forward nudge along sightline to guarantee the quad sits in front of barrel geometry
                    muzzlePos += camFwd * 0.015f;
                }
            }

            LastFlashPosition = muzzlePos;
            currentRollAngle = UnityEngine.Random.Range(0f, 360f);

            // Update flash transform: scale-independent (never parented under Muzzle to avoid 100x scale pollution)
            flashObject.transform.position = muzzlePos;
            flashObject.transform.rotation = Quaternion.LookRotation(camFwd, camUp) * Quaternion.Euler(0f, 0f, currentRollAngle);

            timer = flashDuration;
            flashObject.SetActive(true);
            TriggerCount++;
        }

        private void Update()
        {
            if (flashObject != null && flashObject.activeSelf)
            {
                // Keep the active flash tracking the muzzle if the player moves or weapon retracts during the 65ms
                Transform activeMuzzle = currentMuzzle != null ? currentMuzzle : transform;
                Camera cam = playerCamera != null ? playerCamera : Camera.main;
                if (activeMuzzle != null && cam != null)
                {
                    Vector3 camFwd = cam.transform.forward;
                    Vector3 camUp = cam.transform.up;
                    Vector3 muzzlePos = activeMuzzle.position + camFwd * 0.015f;
                    flashObject.transform.position = muzzlePos;
                    flashObject.transform.rotation = Quaternion.LookRotation(camFwd, camUp) * Quaternion.Euler(0f, 0f, currentRollAngle);
                }
            }

            AdvanceTimer(Time.deltaTime);
        }

        public void ManualUpdate(float deltaTime)
        {
            AdvanceTimer(deltaTime);
        }

        private void AdvanceTimer(float dt)
        {
            if (flashObject != null && flashObject.activeSelf)
            {
                timer -= dt;
                if (timer <= 0f)
                {
                    flashObject.SetActive(false);
                }
            }
        }

        private void EnsureInitialized()
        {
            if (flashObject != null && sharedMaterial != null && sharedMesh != null) return;

            CreateSharedAssets();
            BuildFlashObject();
            EnforceZeroColliders();
        }

        private void CreateSharedAssets()
        {
            if (sharedTexture == null)
            {
                sharedTexture = CreateProceduralFlashTexture();
            }

            if (sharedMaterial == null)
            {
                // Prefer classic rock-solid transparent unlit shaders
                Shader shader = Shader.Find("Unlit/Transparent")
                             ?? Shader.Find("Sprites/Default")
                             ?? Shader.Find("Mobile/Particles/Alpha Blended")
                             ?? Shader.Find("Particles/Standard Unlit")
                             ?? Shader.Find("Standard");

                sharedMaterial = new Material(shader)
                {
                    name = "FB_MuzzleFlashV2_SharedMat"
                };

                sharedMaterial.mainTexture = sharedTexture;
                sharedMaterial.color = flashColor;

                if (sharedMaterial.HasProperty("_TintColor")) sharedMaterial.SetColor("_TintColor", flashColor);

                // Safe transparent alpha blending (SrcAlpha / OneMinusSrcAlpha) - NO additive HDR blowout!
                if (sharedMaterial.HasProperty("_Mode")) sharedMaterial.SetFloat("_Mode", 2f); // Transparent
                if (sharedMaterial.HasProperty("_BlendOp")) sharedMaterial.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
                if (sharedMaterial.HasProperty("_SrcBlend")) sharedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (sharedMaterial.HasProperty("_DstBlend")) sharedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (sharedMaterial.HasProperty("_ZWrite")) sharedMaterial.SetInt("_ZWrite", 0); // DISABLED: prevents skybox occlusions
                if (sharedMaterial.HasProperty("_Cull")) sharedMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // Two-sided

                sharedMaterial.DisableKeyword("_EMISSION"); // Explicitly disable HDR emission
                sharedMaterial.DisableKeyword("_ALPHATEST_ON");
                sharedMaterial.EnableKeyword("_ALPHABLEND_ON");
                sharedMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100; // 3100: renders over weapon barrel
            }

            if (sharedMesh == null)
            {
                sharedMesh = CreateQuadMesh(flashSize);
            }
        }

        private static Texture2D CreateProceduralFlashTexture()
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "FB_MuzzleFlashV2_SoftStar_Tex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x - center) / center; // -1 to +1
                    float v = (y - center) / center; // -1 to +1
                    float r = Mathf.Sqrt(u * u + v * v);

                    // 4-point primary cross spikes
                    float hSpike = Mathf.Max(0f, 1f - Mathf.Abs(u)) * Mathf.Max(0f, 1f - Mathf.Abs(v) * 3.5f);
                    float vSpike = Mathf.Max(0f, 1f - Mathf.Abs(v)) * Mathf.Max(0f, 1f - Mathf.Abs(u) * 3.5f);

                    // 4-point diagonal secondary flares for organic star contour
                    float d1 = Mathf.Max(0f, 1f - Mathf.Abs(u + v) * 2.2f) * Mathf.Max(0f, 1f - Mathf.Abs(u - v) * 2.2f);

                    // Core luminous glow
                    float core = Mathf.Max(0f, 1f - r * 2.0f);

                    float alpha = Mathf.Clamp01(hSpike * 0.9f + vSpike * 0.9f + d1 * 0.4f + core * 1.2f);
                    alpha = Mathf.Pow(alpha, 1.4f); // feathered soft edges

                    // Smooth warm color ramp: hot white core to golden-orange fringe
                    Color col;
                    if (r < 0.25f)
                    {
                        col = Color.Lerp(new Color(1.0f, 0.98f, 0.85f, alpha), new Color(1.0f, 0.82f, 0.40f, alpha), r / 0.25f);
                    }
                    else
                    {
                        col = Color.Lerp(new Color(1.0f, 0.82f, 0.40f, alpha), new Color(1.0f, 0.55f, 0.15f, alpha), Mathf.Clamp01((r - 0.25f) / 0.75f));
                    }
                    col.a = alpha;

                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply(false, false);
            return tex;
        }

        private static Mesh CreateQuadMesh(float size)
        {
            float h = size * 0.5f;
            var mesh = new Mesh { name = "FB_MuzzleFlashV2_Quad" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-h, -h, 0f),
                new Vector3( h, -h, 0f),
                new Vector3( h,  h, 0f),
                new Vector3(-h,  h, 0f)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new int[]
            {
                0, 2, 1,
                0, 3, 2,
                // Double-sided
                1, 2, 0,
                2, 3, 0
            };
            mesh.colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildFlashObject()
        {
            if (flashObject == null)
            {
                // Parented to this controller (scale 1,1,1), NEVER to Muzzle (which has 100x scale and -90 deg rotation)
                flashObject = new GameObject("FB_MuzzleFlashV2_Visual");
                flashObject.transform.SetParent(transform, false);
                flashObject.transform.localScale = Vector3.one;

                meshFilter = flashObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = sharedMesh;

                meshRenderer = flashObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = sharedMaterial;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                flashObject.SetActive(false);
            }
        }

        private void EnforceZeroColliders()
        {
            if (flashObject != null)
            {
                var colliders = flashObject.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    DestroyImmediate(colliders[i]);
                }
            }
        }

        private void OnDestroy()
        {
            if (flashObject != null)
            {
                DestroyImmediate(flashObject);
                flashObject = null;
            }
            if (sharedMaterial != null)
            {
                DestroyImmediate(sharedMaterial);
                sharedMaterial = null;
            }
            if (sharedMesh != null)
            {
                DestroyImmediate(sharedMesh);
                sharedMesh = null;
            }
            if (sharedTexture != null)
            {
                DestroyImmediate(sharedTexture);
                sharedTexture = null;
            }
        }
    }
}
