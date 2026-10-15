using UnityEngine;
using UnityEditor;

public class ToxicWasteObstacle : EditorWindow
{
    private const string ROOT_NAME = "Toxic_Waste_Obstacle";
    private const string MATERIAL_FOLDER = "Assets/Materials";

    private static Material barrelMat;
    private static Material barrelDarkMat;
    private static Material hazardMat;
    private static Material toxicMat;
    private static Material baseMat;
    private static Material metalMat;

    // Position of the large empty area circled in the screenshot.
    // Easy to change later if we need to move it slightly.
    private static readonly Vector3 OBSTACLE_POSITION =
        new Vector3(-2f, 0f, 20f);

    [MenuItem("Facility Breach/Generate Toxic Waste Obstacle")]
    public static void Generate()
    {
        GameObject old = GameObject.Find(ROOT_NAME);

        if (old != null)
            DestroyImmediate(old);

        CreateMaterials();

        GameObject root = new GameObject(ROOT_NAME);
        root.transform.position = OBSTACLE_POSITION;

        // =====================================================
        // LARGE CONTAINMENT PLATFORM
        // =====================================================

        GameObject platform = CreateCube(
            "Containment_Platform",
            root.transform,
            new Vector3(0f, 0.12f, 0f),
            new Vector3(5.2f, 0.24f, 4.8f),
            baseMat
        );

        // Keep collider on platform.
        BoxCollider platformCollider =
            platform.GetComponent<BoxCollider>();

        if (platformCollider != null)
            platformCollider.enabled = true;

        // Raised containment edges
        CreateCube(
            "Containment_Edge_Front",
            root.transform,
            new Vector3(0f, 0.30f, -2.25f),
            new Vector3(5.2f, 0.35f, 0.22f),
            hazardMat
        );

        CreateCube(
            "Containment_Edge_Back",
            root.transform,
            new Vector3(0f, 0.30f, 2.25f),
            new Vector3(5.2f, 0.35f, 0.22f),
            hazardMat
        );

        CreateCube(
            "Containment_Edge_Left",
            root.transform,
            new Vector3(-2.5f, 0.30f, 0f),
            new Vector3(0.22f, 0.35f, 4.3f),
            hazardMat
        );

        CreateCube(
            "Containment_Edge_Right",
            root.transform,
            new Vector3(2.5f, 0.30f, 0f),
            new Vector3(0.22f, 0.35f, 4.3f),
            hazardMat
        );

        // =====================================================
        // TOXIC WASTE BARRELS
        // =====================================================

        CreateToxicBarrel(
            "ToxicBarrel_01",
            root.transform,
            new Vector3(-1.25f, 1.05f, -0.85f),
            8f
        );

        CreateToxicBarrel(
            "ToxicBarrel_02",
            root.transform,
            new Vector3(0f, 1.05f, -0.95f),
            -12f
        );

        CreateToxicBarrel(
            "ToxicBarrel_03",
            root.transform,
            new Vector3(1.25f, 1.05f, -0.75f),
            14f
        );

        CreateToxicBarrel(
            "ToxicBarrel_04",
            root.transform,
            new Vector3(-0.70f, 1.05f, 0.65f),
            -7f
        );

        CreateToxicBarrel(
            "ToxicBarrel_05",
            root.transform,
            new Vector3(0.70f, 1.05f, 0.70f),
            10f
        );

        // =====================================================
        // EXTRA BARRELS STACKED ABOVE
        // =====================================================

        CreateToxicBarrel(
            "ToxicBarrel_06_Stacked",
            root.transform,
            new Vector3(-0.65f, 2.70f, -0.15f),
            90f
        );

        CreateToxicBarrel(
            "ToxicBarrel_07_Stacked",
            root.transform,
            new Vector3(0.75f, 2.70f, -0.10f),
            90f
        );

        // =====================================================
        // TOXIC LIQUID POOLS
        // =====================================================

        CreateToxicPool(
            "ToxicLeak_Main",
            root.transform,
            new Vector3(0.3f, 0.29f, 1.15f),
            new Vector3(2.4f, 0.035f, 1.35f)
        );

        CreateToxicPool(
            "ToxicLeak_Left",
            root.transform,
            new Vector3(-1.55f, 0.29f, 0.35f),
            new Vector3(1.35f, 0.03f, 0.85f)
        );

        CreateToxicPool(
            "ToxicLeak_Front",
            root.transform,
            new Vector3(1.35f, 0.29f, -1.45f),
            new Vector3(1.15f, 0.03f, 0.65f)
        );

        // =====================================================
        // TOXIC GLOW MARKERS
        // =====================================================

        CreateGlowSphere(
            "ToxicGlow_01",
            root.transform,
            new Vector3(-0.7f, 0.42f, 1.2f),
            0.20f
        );

        CreateGlowSphere(
            "ToxicGlow_02",
            root.transform,
            new Vector3(0.4f, 0.42f, 1.45f),
            0.16f
        );

        CreateGlowSphere(
            "ToxicGlow_03",
            root.transform,
            new Vector3(1.45f, 0.42f, -1.35f),
            0.13f
        );

        // =====================================================
        // HAZARD POSTS
        // =====================================================

        CreateHazardPost(
            "HazardPost_FL",
            root.transform,
            new Vector3(-2.25f, 0.8f, -2f)
        );

        CreateHazardPost(
            "HazardPost_FR",
            root.transform,
            new Vector3(2.25f, 0.8f, -2f)
        );

        CreateHazardPost(
            "HazardPost_BL",
            root.transform,
            new Vector3(-2.25f, 0.8f, 2f)
        );

        CreateHazardPost(
            "HazardPost_BR",
            root.transform,
            new Vector3(2.25f, 0.8f, 2f)
        );

        // =====================================================
        // MAIN BLOCKING COLLIDER
        //
        // This makes the whole waste area a real obstacle.
        // Player / NPC cannot simply walk through the barrels.
        // =====================================================

        GameObject blocker = new GameObject("ToxicWaste_MainCollider");
        blocker.transform.SetParent(root.transform);

        blocker.transform.localPosition =
            new Vector3(0f, 1.15f, 0f);

        BoxCollider mainCollider =
            blocker.AddComponent<BoxCollider>();

        mainCollider.size =
            new Vector3(4.7f, 2.3f, 4.2f);

        // =====================================================
        // SELECTION
        // =====================================================

        Selection.activeGameObject = root;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Large toxic waste obstacle generated at " +
            OBSTACLE_POSITION +
            ". Player must route around it."
        );
    }

    // =====================================================
    // TOXIC BARREL
    // =====================================================

    private static void CreateToxicBarrel(
        string name,
        Transform parent,
        Vector3 position,
        float rotationY)
    {
        GameObject barrelRoot = new GameObject(name);

        barrelRoot.transform.SetParent(parent);
        barrelRoot.transform.localPosition = position;
        barrelRoot.transform.localRotation =
            Quaternion.Euler(0f, rotationY, 0f);

        // Main barrel body
        GameObject body =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        body.name = name + "_Body";
        body.transform.SetParent(barrelRoot.transform);

        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;

        body.transform.localScale =
            new Vector3(0.72f, 0.90f, 0.72f);

        ApplyMaterial(body, barrelMat);
        RemoveCollider(body);

        // Top ring
        CreateBarrelRing(
            name + "_TopRing",
            barrelRoot.transform,
            new Vector3(0f, 0.78f, 0f)
        );

        // Upper ring
        CreateBarrelRing(
            name + "_UpperRing",
            barrelRoot.transform,
            new Vector3(0f, 0.43f, 0f)
        );

        // Middle ring
        CreateBarrelRing(
            name + "_MiddleRing",
            barrelRoot.transform,
            new Vector3(0f, 0f, 0f)
        );

        // Lower ring
        CreateBarrelRing(
            name + "_LowerRing",
            barrelRoot.transform,
            new Vector3(0f, -0.43f, 0f)
        );

        // Bottom ring
        CreateBarrelRing(
            name + "_BottomRing",
            barrelRoot.transform,
            new Vector3(0f, -0.78f, 0f)
        );

        // Hazard stripe
        GameObject stripe =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        stripe.name = name + "_HazardStripe";
        stripe.transform.SetParent(barrelRoot.transform);

        stripe.transform.localPosition =
            new Vector3(0f, 0.18f, 0f);

        stripe.transform.localScale =
            new Vector3(0.735f, 0.13f, 0.735f);

        ApplyMaterial(stripe, hazardMat);
        RemoveCollider(stripe);

        // Front toxic label
        GameObject label = CreateCube(
            name + "_ToxicLabel",
            barrelRoot.transform,
            new Vector3(0f, 0.15f, -0.725f),
            new Vector3(0.72f, 0.55f, 0.045f),
            toxicMat
        );

        RemoveCollider(label);

        // Smaller black warning block on label
        GameObject warningBlock = CreateCube(
            name + "_WarningMark",
            barrelRoot.transform,
            new Vector3(0f, 0.15f, -0.755f),
            new Vector3(0.26f, 0.26f, 0.025f),
            barrelDarkMat
        );

        warningBlock.transform.localRotation =
            Quaternion.Euler(0f, 0f, 45f);

        RemoveCollider(warningBlock);

        // Top cap
        GameObject cap =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        cap.name = name + "_TopCap";
        cap.transform.SetParent(barrelRoot.transform);

        cap.transform.localPosition =
            new Vector3(0f, 0.91f, 0f);

        cap.transform.localScale =
            new Vector3(0.64f, 0.035f, 0.64f);

        ApplyMaterial(cap, barrelDarkMat);
        RemoveCollider(cap);

        // Barrel collider
        CapsuleCollider collider =
            barrelRoot.AddComponent<CapsuleCollider>();

        collider.radius = 0.72f;
        collider.height = 1.85f;
        collider.direction = 1;
        collider.center = Vector3.zero;
    }

    // =====================================================
    // BARREL RING
    // =====================================================

    private static void CreateBarrelRing(
        string name,
        Transform parent,
        Vector3 position)
    {
        GameObject ring =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        ring.name = name;
        ring.transform.SetParent(parent);

        ring.transform.localPosition = position;
        ring.transform.localRotation = Quaternion.identity;

        ring.transform.localScale =
            new Vector3(0.76f, 0.045f, 0.76f);

        ApplyMaterial(ring, metalMat);
        RemoveCollider(ring);
    }

    // =====================================================
    // TOXIC POOL
    // =====================================================

    private static void CreateToxicPool(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale)
    {
        GameObject pool =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        pool.name = name;
        pool.transform.SetParent(parent);

        pool.transform.localPosition = position;

        pool.transform.localScale =
            new Vector3(
                scale.x,
                scale.y,
                scale.z
            );

        ApplyMaterial(pool, toxicMat);
        RemoveCollider(pool);
    }

    // =====================================================
    // GLOW SPHERE
    // =====================================================

    private static void CreateGlowSphere(
        string name,
        Transform parent,
        Vector3 position,
        float size)
    {
        GameObject glow =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        glow.name = name;
        glow.transform.SetParent(parent);

        glow.transform.localPosition = position;

        glow.transform.localScale =
            new Vector3(size, size, size);

        ApplyMaterial(glow, toxicMat);
        RemoveCollider(glow);
    }

    // =====================================================
    // HAZARD POST
    // =====================================================

    private static void CreateHazardPost(
        string name,
        Transform parent,
        Vector3 position)
    {
        GameObject post = new GameObject(name);

        post.transform.SetParent(parent);
        post.transform.localPosition = position;

        CreateCube(
            name + "_Body",
            post.transform,
            Vector3.zero,
            new Vector3(0.18f, 1.5f, 0.18f),
            hazardMat
        );

        CreateCube(
            name + "_DarkStripe1",
            post.transform,
            new Vector3(0f, 0.30f, -0.10f),
            new Vector3(0.20f, 0.20f, 0.04f),
            barrelDarkMat
        );

        CreateCube(
            name + "_DarkStripe2",
            post.transform,
            new Vector3(0f, -0.30f, -0.10f),
            new Vector3(0.20f, 0.20f, 0.04f),
            barrelDarkMat
        );
    }

    // =====================================================
    // CUBE HELPER
    // =====================================================

    private static GameObject CreateCube(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject obj =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        obj.name = name;
        obj.transform.SetParent(parent);

        obj.transform.localPosition = position;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = scale;

        ApplyMaterial(obj, material);

        return obj;
    }

    // =====================================================
    // MATERIAL HELPER
    // =====================================================

    private static void ApplyMaterial(
        GameObject obj,
        Material material)
    {
        Renderer renderer = obj.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    private static void RemoveCollider(GameObject obj)
    {
        Collider collider = obj.GetComponent<Collider>();

        if (collider != null)
            DestroyImmediate(collider);
    }

    // =====================================================
    // MATERIALS
    // =====================================================

    private static void CreateMaterials()
    {
        EnsureMaterialFolder();

        // Bright industrial yellow-green barrels
        barrelMat = CreateMaterial(
            "ToxicWaste_Barrel",
            new Color(0.65f, 0.72f, 0.08f),
            0.40f,
            0.18f
        );

        // Dark details
        barrelDarkMat = CreateMaterial(
            "ToxicWaste_Dark",
            new Color(0.07f, 0.09f, 0.07f),
            0.55f,
            0.15f
        );

        // Hazard yellow
        hazardMat = CreateMaterial(
            "ToxicWaste_HazardYellow",
            new Color(1.00f, 0.70f, 0.03f),
            0.12f,
            0.16f
        );

        // Dark containment platform
        baseMat = CreateMaterial(
            "ToxicWaste_Base",
            new Color(0.10f, 0.13f, 0.12f),
            0.55f,
            0.14f
        );

        // Metal barrel bands
        metalMat = CreateMaterial(
            "ToxicWaste_Metal",
            new Color(0.36f, 0.42f, 0.40f),
            0.75f,
            0.25f
        );

        // Bright glowing toxic green
        toxicMat = CreateGlowMaterial(
            "ToxicWaste_Glow",
            new Color(0.18f, 1.00f, 0.04f),
            4.5f
        );
    }

    private static Material CreateMaterial(
        string name,
        Color color,
        float metallic,
        float smoothness)
    {
        string path =
            MATERIAL_FOLDER + "/" + name + ".mat";

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Standard");

            material = new Material(shader);

            AssetDatabase.CreateAsset(
                material,
                path
            );
        }

        material.color = color;

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);

        EditorUtility.SetDirty(material);

        return material;
    }

    // =====================================================
    // GLOW MATERIAL
    // =====================================================

    private static Material CreateGlowMaterial(
        string name,
        Color color,
        float glowStrength)
    {
        string path =
            MATERIAL_FOLDER + "/" + name + ".mat";

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Standard");

            material = new Material(shader);

            AssetDatabase.CreateAsset(
                material,
                path
            );
        }

        material.color = color;

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        material.EnableKeyword("_EMISSION");

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor(
                "_EmissionColor",
                color * glowStrength
            );
        }

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.05f);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.35f);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.35f);

        material.globalIlluminationFlags =
            MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EditorUtility.SetDirty(material);

        return material;
    }

    // =====================================================
    // MATERIAL FOLDER
    // =====================================================

    private static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder(MATERIAL_FOLDER))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "Materials"
            );
        }
    }
}