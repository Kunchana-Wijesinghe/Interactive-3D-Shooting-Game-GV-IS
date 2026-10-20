using System;
using UnityEngine;

namespace FacilityBreach
{
    /// <summary>
    /// Visual-only first-person bullet tracer component for FacilityBreach.
    /// Manages a bounded pool of LineRenderer tracer streaks without modifying
    /// hitscan calculations, raycast queries, damage, ammo, or collision authority.
    /// Ensures depth writing is disabled, protects against camera near-plane division artifacts,
    /// and bounds miss travel distance to prevent skybox streaks.
    /// </summary>
    [DisallowMultipleComponent]
    public class FpsBulletTracer : MonoBehaviour
    {
        [Header("Tracer Visual Settings")]
        [Tooltip("Lifetime of each tracer streak in seconds.")]
        [SerializeField] private float tracerDuration = 0.040f;

        [Tooltip("Width of the tracer at the visual muzzle start.")]
        [SerializeField] private float startWidth = 0.015f;

        [Tooltip("Width of the tracer at the projectile endpoint.")]
        [SerializeField] private float endWidth = 0.008f;

        [Tooltip("Luminous warm color of the tracer beam.")]
        [SerializeField] private Color tracerColor = new Color(1.0f, 0.82f, 0.35f, 0.85f);

        [Tooltip("Maximum visual distance a tracer streak travels on a miss (prevents skybox streaks).")]
        [SerializeField] private float maxMissDistance = 35.0f;

        [Header("Pool Configuration")]
        [SerializeField] private int poolCapacity = 10;

        [Header("Camera Reference")]
        [Tooltip("Optional explicit reference to player camera. Falls back to Camera.main if unset.")]
        [SerializeField] private Camera playerCamera;

        // References & State
        private Transform currentMuzzle;
        private GameObject poolRoot;
        private Material sharedMaterial;

        private struct PooledTracer
        {
            public GameObject gameObject;
            public LineRenderer lineRenderer;
            public float timer;
        }
        private PooledTracer[] pool;
        private int nextIndex;

        // Telemetry & Diagnostic Properties for Unit Testing
        public int PoolSize => poolCapacity;
        public Transform CurrentMuzzle => currentMuzzle;
        public Camera PlayerCamera { get => playerCamera; set => playerCamera = value; }
        public float TracerDuration { get => tracerDuration; set => tracerDuration = value; }
        public float MaxMissDistance { get => maxMissDistance; set => maxMissDistance = value; }
        public Material SharedMaterial => sharedMaterial;
        public Vector3 LastTracerStart { get; private set; }
        public Vector3 LastTracerEnd { get; private set; }
        public bool LastShotWasHit { get; private set; }

        public int ActiveTracerCount
        {
            get
            {
                if (pool == null) return 0;
                int count = 0;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i].gameObject != null && pool[i].gameObject.activeSelf &&
                        pool[i].lineRenderer != null && pool[i].lineRenderer.enabled)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

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
        /// Spawns a visual tracer streak from firing muzzle to target point.
        /// Protects against camera near-plane intersection, reverse streaks, and unbounded skybox lines.
        /// </summary>
        public void SpawnTracer(Transform firingMuzzle, Vector3 targetEndPoint, bool isHit)
        {
            EnsureInitialized();

            Transform activeMuzzle = firingMuzzle != null ? firingMuzzle : currentMuzzle;
            Vector3 startPos = activeMuzzle != null ? activeMuzzle.position : transform.position;
            Vector3 endPos = targetEndPoint;

            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            bool suppressTracer = false;

            if (cam != null)
            {
                Vector3 camPos = cam.transform.position;
                Vector3 camFwd = cam.transform.forward;

                // 1. Prevent reverse-traveling tracers (e.g. wall obstruction where hit is behind muzzle)
                float forwardDisp = Vector3.Dot(endPos - startPos, camFwd);
                if (forwardDisp < 0.08f || Vector3.Distance(startPos, endPos) < 0.15f)
                {
                    // Point-blank obstruction contact: suppress the line streak to avoid camera near-clip division artifact
                    suppressTracer = true;
                }
                else
                {
                    // 2. Ensure startPos never penetrates the camera's near clip plane
                    Vector3 toStart = startPos - camPos;
                    float depth = Vector3.Dot(toStart, camFwd);
                    if (depth < 0.12f)
                    {
                        startPos = camPos + camFwd * 0.15f + Vector3.ProjectOnPlane(toStart, camFwd);
                    }

                    // 3. Limit visual tracer travel on misses to avoid infinite skybox streaks
                    if (!isHit && Vector3.Distance(startPos, endPos) > maxMissDistance)
                    {
                        endPos = startPos + (endPos - startPos).normalized * maxMissDistance;
                    }
                }
            }
            else
            {
                if (!isHit && Vector3.Distance(startPos, endPos) > maxMissDistance)
                {
                    endPos = startPos + (endPos - startPos).normalized * maxMissDistance;
                }
            }

            LastTracerStart = startPos;
            LastTracerEnd = endPos;
            LastShotWasHit = isHit;

            if (!suppressTracer)
            {
                ActivatePooledTracer(startPos, endPos);
            }
        }

        private void ActivatePooledTracer(Vector3 start, Vector3 end)
        {
            if (pool == null || pool.Length == 0) return;

            int index = nextIndex;
            nextIndex = (nextIndex + 1) % pool.Length;

            ref PooledTracer item = ref pool[index];
            if (item.gameObject == null || item.lineRenderer == null) return;

            Vector3[] pts = new Vector3[] { start, end };
            item.lineRenderer.SetPositions(pts);
            item.timer = tracerDuration;
            item.lineRenderer.enabled = true;
            item.gameObject.SetActive(true);
        }

        private void Update()
        {
            AdvanceTimers(Time.deltaTime);
        }

        public void ManualUpdate(float deltaTime)
        {
            AdvanceTimers(deltaTime);
        }

        private void AdvanceTimers(float dt)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].gameObject != null && pool[i].gameObject.activeSelf)
                {
                    pool[i].timer -= dt;
                    if (pool[i].timer <= 0f)
                    {
                        if (pool[i].lineRenderer != null) pool[i].lineRenderer.enabled = false;
                        pool[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void EnsureInitialized()
        {
            if (pool != null && pool.Length > 0 && pool[0].gameObject != null) return;

            CreateSharedMaterial();
            BuildTracerPool();
            EnforceZeroColliders();
        }

        private void CreateSharedMaterial()
        {
            if (sharedMaterial != null) return;

            Shader shader = Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Mobile/Particles/Additive")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard");

            sharedMaterial = new Material(shader)
            {
                name = "FB_BulletTracer_SharedMat"
            };

            sharedMaterial.mainTexture = Texture2D.whiteTexture;
            sharedMaterial.color = tracerColor;

            if (sharedMaterial.HasProperty("_TintColor")) sharedMaterial.SetColor("_TintColor", tracerColor);
            if (sharedMaterial.HasProperty("_EmissionColor"))
            {
                sharedMaterial.EnableKeyword("_EMISSION");
                sharedMaterial.SetColor("_EmissionColor", tracerColor);
            }

            // Universal transparent/additive blending with depth writing strictly disabled
            if (sharedMaterial.HasProperty("_Mode")) sharedMaterial.SetFloat("_Mode", 2f); // Fade/Transparent
            if (sharedMaterial.HasProperty("_BlendOp")) sharedMaterial.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
            if (sharedMaterial.HasProperty("_SrcBlend")) sharedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (sharedMaterial.HasProperty("_DstBlend")) sharedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (sharedMaterial.HasProperty("_ZWrite")) sharedMaterial.SetInt("_ZWrite", 0); // DISABLED: prevents skybox occlusions
            if (sharedMaterial.HasProperty("_Cull")) sharedMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            sharedMaterial.DisableKeyword("_ALPHATEST_ON");
            sharedMaterial.EnableKeyword("_ALPHABLEND_ON");
            sharedMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void BuildTracerPool()
        {
            if (poolRoot == null)
            {
                poolRoot = new GameObject("[VFX_BulletTracerPool]");
                poolRoot.transform.SetParent(transform, false);
            }

            pool = new PooledTracer[poolCapacity];
            for (int i = 0; i < poolCapacity; i++)
            {
                var tracerObj = new GameObject($"TracerBeam_{i:D2}");
                tracerObj.transform.SetParent(poolRoot.transform, false);

                var lr = tracerObj.AddComponent<LineRenderer>();
                lr.material = sharedMaterial;
                lr.startWidth = startWidth;
                lr.endWidth = endWidth;
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                lr.alignment = LineAlignment.View;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 2;
                lr.enabled = false;

                // Color gradient from bright start to tapered alpha end
                var grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(tracerColor, 0f), new GradientColorKey(tracerColor, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(tracerColor.a, 0f), new GradientAlphaKey(tracerColor.a * 0.4f, 1f) }
                );
                lr.colorGradient = grad;

                tracerObj.SetActive(false);

                pool[i] = new PooledTracer
                {
                    gameObject = tracerObj,
                    lineRenderer = lr,
                    timer = 0f
                };
            }
        }

        private void EnforceZeroColliders()
        {
            if (poolRoot != null)
            {
                var colliders = poolRoot.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    DestroyImmediate(colliders[i]);
                }
            }
        }

        private void OnDestroy()
        {
            if (poolRoot != null)
            {
                DestroyImmediate(poolRoot);
                poolRoot = null;
            }
            if (sharedMaterial != null)
            {
                DestroyImmediate(sharedMaterial);
                sharedMaterial = null;
            }
        }
    }
}
