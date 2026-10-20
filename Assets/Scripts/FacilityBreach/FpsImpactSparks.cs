using System;
using UnityEngine;

namespace FacilityBreach
{
    /// <summary>
    /// Visual-only first-person environmental impact spark component for FacilityBreach.
    /// Manages a bounded pool of subtle, short-lived micro-spark streaks at hitscan environment impact points.
    /// Uses transparent unlit rendering with depth writing disabled.
    /// Strictly visual: contains zero lights, zero colliders, zero raycasts, and no HDR emission.
    /// </summary>
    [DisallowMultipleComponent]
    public class FpsImpactSparks : MonoBehaviour
    {
        [Header("Impact Spark Settings")]
        [Tooltip("Lifetime of the impact spark visual in seconds.")]
        [SerializeField] private float sparkDuration = 0.11f;

        [Tooltip("Base luminous color of the impact sparks.")]
        [SerializeField] private Color sparkColor = new Color(1.0f, 0.85f, 0.45f, 0.90f);

        [Tooltip("Number of micro-spark streaks per impact point.")]
        [SerializeField] private int sparkCount = 5;

        [Header("Pool Configuration")]
        [SerializeField] private int poolCapacity = 8;

        // References & State
        private GameObject poolRoot;
        private Material sharedMaterial;

        private struct PooledImpact
        {
            public GameObject gameObject;
            public LineRenderer[] sparkLines;
            public float timer;
        }
        private PooledImpact[] pool;
        private int nextIndex;

        // Telemetry & Diagnostic Properties for Unit Testing
        public int PoolSize => poolCapacity;
        public float SparkDuration { get => sparkDuration; set => sparkDuration = value; }
        public Color SparkColor => sparkColor;
        public Material SharedMaterial => sharedMaterial;
        public Vector3 LastImpactPoint { get; private set; }
        public Vector3 LastImpactNormal { get; private set; }
        public bool LastShotWasEnvironment { get; private set; }
        public int TriggerCount { get; private set; }

        public int ActiveImpactCount
        {
            get
            {
                if (pool == null) return 0;
                int count = 0;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i].gameObject != null && pool[i].gameObject.activeSelf)
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

        public void Initialize()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Spawns subtle environmental impact sparks at the hitscan impact point.
        /// Only called for valid static environment hits (walls, floors, doors, barricades).
        /// </summary>
        public void SpawnSparks(Vector3 hitPoint, Vector3 hitNormal)
        {
            EnsureInitialized();
            if (pool == null || pool.Length == 0) return;

            int index = nextIndex;
            nextIndex = (nextIndex + 1) % pool.Length;

            ref PooledImpact item = ref pool[index];
            if (item.gameObject == null) return;

            Vector3 surfaceOffset = hitNormal.sqrMagnitude > 0.01f ? hitNormal.normalized * 0.01f : Vector3.up * 0.01f;
            item.gameObject.transform.position = hitPoint + surfaceOffset;

            // Robust orientation: avoids collinear forward/up errors on floor (up) and ceiling (down) hits
            Vector3 fwd = hitNormal.sqrMagnitude > 0.01f ? hitNormal.normalized : Vector3.forward;
            Vector3 upRef = Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.85f ? Vector3.forward : Vector3.up;
            item.gameObject.transform.rotation = Quaternion.LookRotation(fwd, upRef);

            // Re-orient subtle micro spark streaks
            if (item.sparkLines != null)
            {
                for (int s = 0; s < item.sparkLines.Length; s++)
                {
                    var lr = item.sparkLines[s];
                    if (lr == null) continue;
                    lr.SetPosition(0, Vector3.zero);
                    float angle = (s * (360f / item.sparkLines.Length)) + UnityEngine.Random.Range(-15f, 15f);
                    float rad = angle * Mathf.Deg2Rad;
                    // Spread outward along hemisphere (modest visibility: ~0.08-0.12m length)
                    float length = UnityEngine.Random.Range(0.08f, 0.11f);
                    Vector3 spreadDir = new Vector3(Mathf.Cos(rad) * length, Mathf.Sin(rad) * length, UnityEngine.Random.Range(0.03f, 0.06f));
                    lr.SetPosition(1, spreadDir);
                    lr.enabled = true;
                }
            }

            item.timer = sparkDuration;
            item.gameObject.SetActive(true);

            LastImpactPoint = hitPoint;
            LastImpactNormal = hitNormal;
            LastShotWasEnvironment = true;
            TriggerCount++;
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
                        if (pool[i].sparkLines != null)
                        {
                            for (int s = 0; s < pool[i].sparkLines.Length; s++)
                            {
                                if (pool[i].sparkLines[s] != null) pool[i].sparkLines[s].enabled = false;
                            }
                        }
                        pool[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void EnsureInitialized()
        {
            if (pool != null && pool.Length > 0 && pool[0].gameObject != null) return;

            CreateSharedMaterial();
            BuildImpactPool();
            EnforceZeroColliders();
        }

        private void CreateSharedMaterial()
        {
            if (sharedMaterial != null) return;

            Shader shader = Shader.Find("Unlit/Transparent")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Mobile/Particles/Alpha Blended")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Standard");

            sharedMaterial = new Material(shader)
            {
                name = "FB_ImpactSparks_SharedMat"
            };

            sharedMaterial.mainTexture = Texture2D.whiteTexture;
            sharedMaterial.color = sparkColor;

            if (sharedMaterial.HasProperty("_TintColor")) sharedMaterial.SetColor("_TintColor", sparkColor);

            // Safe transparent alpha blending (SrcAlpha / OneMinusSrcAlpha)
            if (sharedMaterial.HasProperty("_Mode")) sharedMaterial.SetFloat("_Mode", 2f); // Transparent
            if (sharedMaterial.HasProperty("_BlendOp")) sharedMaterial.SetInt("_BlendOp", (int)UnityEngine.Rendering.BlendOp.Add);
            if (sharedMaterial.HasProperty("_SrcBlend")) sharedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (sharedMaterial.HasProperty("_DstBlend")) sharedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (sharedMaterial.HasProperty("_ZWrite")) sharedMaterial.SetInt("_ZWrite", 0); // DISABLED: prevents skybox occlusions
            if (sharedMaterial.HasProperty("_Cull")) sharedMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            sharedMaterial.DisableKeyword("_EMISSION");
            sharedMaterial.DisableKeyword("_ALPHATEST_ON");
            sharedMaterial.EnableKeyword("_ALPHABLEND_ON");
            sharedMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void BuildImpactPool()
        {
            if (poolRoot == null)
            {
                poolRoot = new GameObject("[VFX_ImpactSparksPool]");
                poolRoot.transform.SetParent(transform, false);
            }

            pool = new PooledImpact[poolCapacity];
            for (int i = 0; i < poolCapacity; i++)
            {
                var impactObj = new GameObject($"ImpactSpark_{i:D2}");
                impactObj.transform.SetParent(poolRoot.transform, false);

                var lines = new LineRenderer[sparkCount];
                for (int s = 0; s < sparkCount; s++)
                {
                    var lineObj = new GameObject($"SparkLine_{s}");
                    lineObj.transform.SetParent(impactObj.transform, false);

                    var lr = lineObj.AddComponent<LineRenderer>();
                    lr.material = sharedMaterial;
                    lr.startWidth = 0.012f;
                    lr.endWidth = 0.005f;
                    lr.useWorldSpace = false;
                    lr.positionCount = 2;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    lr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    lr.alignment = LineAlignment.View;
                    lr.numCapVertices = 2;
                    lr.numCornerVertices = 2;
                    lr.enabled = false;

                    var grad = new Gradient();
                    grad.SetKeys(
                        new GradientColorKey[] { new GradientColorKey(sparkColor, 0f), new GradientColorKey(sparkColor, 1f) },
                        new GradientAlphaKey[] { new GradientAlphaKey(sparkColor.a, 0f), new GradientAlphaKey(0f, 1f) }
                    );
                    lr.colorGradient = grad;

                    lines[s] = lr;
                }

                impactObj.SetActive(false);

                pool[i] = new PooledImpact
                {
                    gameObject = impactObj,
                    sparkLines = lines,
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
