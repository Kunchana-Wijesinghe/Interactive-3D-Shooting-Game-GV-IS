using UnityEngine;
using UnityEditor;

public class FacilityBloodMarks : EditorWindow
{
    private const string ROOT_NAME = "Facility_BloodMarks";
    private const string MATERIAL_FOLDER = "Assets/Materials";

    private static Material bloodDark;
    private static Material bloodDried;
    private static Material bloodOld;
    private static Material bloodDeep;

    // =========================================================
    // GENERATE EVERYTHING
    // =========================================================

    [MenuItem("Facility Breach/Generate Blood Marks")]
    public static void GenerateBloodMarks()
    {
        GameObject oldRoot = GameObject.Find(ROOT_NAME);

        if (oldRoot != null)
            DestroyImmediate(oldRoot);

        GameObject root = new GameObject(ROOT_NAME);

        CreateMaterials();

        // =====================================================
        // OUTER WALL SPLATTERS
        // =====================================================

        WallSplatZ(root.transform, "Entrance_Splat_01",
            new Vector3(-18f, 1.35f, -29.72f),
            true, 1.35f, 0.95f, 12f);

        WallSplatZ(root.transform, "Entrance_Splat_02",
            new Vector3(-5f, 2.0f, -29.72f),
            true, 0.65f, 0.45f, -20f);

        WallSplatX(root.transform, "West_Splat_01",
            new Vector3(-20.72f, 1.45f, -20f),
            true, 1.05f, 0.75f, -10f);

        WallSplatX(root.transform, "West_Splat_02",
            new Vector3(-20.72f, 2.1f, -7f),
            true, 0.80f, 0.60f, 16f);

        WallSplatX(root.transform, "West_Splat_03",
            new Vector3(-20.72f, 1.15f, 14f),
            true, 1.40f, 0.85f, -18f);

        WallSplatX(root.transform, "West_Splat_04",
            new Vector3(-20.72f, 2.15f, 23f),
            true, 0.65f, 0.50f, 20f);

        WallSplatX(root.transform, "East_Splat_01",
            new Vector3(20.72f, 1.3f, -18f),
            false, 0.95f, 0.65f, 14f);

        WallSplatX(root.transform, "East_Splat_02",
            new Vector3(20.72f, 1.65f, 7f),
            false, 1.30f, 0.90f, -16f);

        WallSplatX(root.transform, "East_Splat_03",
            new Vector3(20.72f, 2.1f, 19f),
            false, 0.75f, 0.55f, 24f);

        WallSplatZ(root.transform, "Exit_Splat_01",
            new Vector3(17f, 1.2f, 29.72f),
            false, 1.20f, 0.80f, -12f);

        WallSplatZ(root.transform, "Exit_Splat_02",
            new Vector3(5f, 2.0f, 29.72f),
            false, 0.65f, 0.45f, 18f);

        // =====================================================
        // ROOM / INTERNAL WALL SPLATTERS
        // =====================================================

        WallSplatZ(root.transform, "Room1_Splat_01",
            new Vector3(-15.5f, 1.35f, -6.28f),
            false, 1.15f, 0.80f, -12f);

        WallSplatX(root.transform, "Room1_Splat_02",
            new Vector3(-7.82f, 1.8f, -16f),
            false, 0.75f, 0.55f, 17f);

        WallSplatZ(root.transform, "Room2_Splat_01",
            new Vector3(5.5f, 1.4f, -1.72f),
            true, 1.25f, 0.85f, 9f);

        WallSplatZ(root.transform, "Room2_Splat_02",
            new Vector3(-3f, 2.0f, 10.22f),
            false, 0.75f, 0.50f, -21f);

        WallSplatX(root.transform, "Room2_Splat_03",
            new Vector3(8.82f, 1.2f, 7f),
            false, 1.05f, 0.70f, 15f);

        WallSplatX(root.transform, "Room3_Splat_01",
            new Vector3(10.22f, 1.35f, 16f),
            true, 1.30f, 0.90f, -8f);

        WallSplatX(root.transform, "Room3_Splat_02",
            new Vector3(10.22f, 2.05f, 21f),
            true, 0.75f, 0.55f, 18f);

        WallSplatZ(root.transform, "Room3_Splat_03",
            new Vector3(16f, 1.25f, 12.22f),
            true, 1.20f, 0.85f, -15f);

        WallSplatZ(root.transform, "Room3_Splat_04",
            new Vector3(13f, 1.8f, 24.22f),
            false, 0.85f, 0.60f, 12f);

        // =====================================================
        // WALL SMEARS
        // =====================================================

        WallSmearX(root.transform, "Smear_West_01",
            new Vector3(-20.71f, 1.25f, 4f), true, 1.5f);

        WallSmearX(root.transform, "Smear_West_02",
            new Vector3(-20.71f, 1.15f, 18f), true, 1.25f);

        WallSmearX(root.transform, "Smear_East_01",
            new Vector3(20.71f, 1.3f, -5f), false, 1.4f);

        WallSmearZ(root.transform, "Smear_Room1",
            new Vector3(-11f, 1.25f, -6.29f), false, 1.35f);

        WallSmearZ(root.transform, "Smear_Room2",
            new Vector3(4f, 1.2f, 10.21f), false, 1.5f);

        WallSmearX(root.transform, "Smear_Room3",
            new Vector3(10.21f, 1.25f, 19f), true, 1.6f);

        // =====================================================
        // FLOOR POOLS
        // =====================================================

        FloorPool(root.transform, "Pool_01",
            new Vector3(-11f, 0.02f, -24f),
            1.7f, 1.15f, 20f);

        FloorPool(root.transform, "Pool_02",
            new Vector3(-15f, 0.02f, -13f),
            1.25f, 0.85f, -16f);

        FloorPool(root.transform, "Pool_03",
            new Vector3(-5f, 0.02f, -11f),
            0.9f, 0.65f, 30f);

        FloorPool(root.transform, "Pool_04",
            new Vector3(1f, 0.02f, -4f),
            1.35f, 0.85f, -28f);

        FloorPool(root.transform, "Pool_05",
            new Vector3(3f, 0.02f, 3f),
            1.8f, 1.05f, 17f);

        FloorPool(root.transform, "Pool_06",
            new Vector3(-10f, 0.02f, 5f),
            1.05f, 0.7f, -8f);

        FloorPool(root.transform, "Pool_07",
            new Vector3(14f, 0.02f, 6f),
            1.4f, 0.85f, 32f);

        FloorPool(root.transform, "Pool_08",
            new Vector3(-9f, 0.02f, 16f),
            1.15f, 0.75f, 12f);

        FloorPool(root.transform, "Pool_09",
            new Vector3(14f, 0.02f, 17f),
            1.9f, 1.1f, -20f);

        FloorPool(root.transform, "Pool_10",
            new Vector3(17f, 0.02f, 21f),
            1.0f, 0.65f, 14f);

        FloorPool(root.transform, "Pool_11",
            new Vector3(-3f, 0.02f, 25f),
            1.35f, 0.85f, -32f);

        FloorPool(root.transform, "Pool_12",
            new Vector3(11f, 0.02f, 26f),
            1.15f, 0.75f, 8f);

        // =====================================================
        // DRAG TRAILS
        // =====================================================

        DragTrail(root.transform, "DragTrail_01",
            new Vector3(-9f, 0.025f, -20f),
            new Vector3(0.7f, 0f, 1f), 7);

        DragTrail(root.transform, "DragTrail_02",
            new Vector3(-13f, 0.025f, -8f),
            new Vector3(1f, 0f, 0.25f), 6);

        DragTrail(root.transform, "DragTrail_03",
            new Vector3(4f, 0.025f, 6f),
            new Vector3(1f, 0f, 0.7f), 7);

        DragTrail(root.transform, "DragTrail_04",
            new Vector3(13f, 0.025f, 13f),
            new Vector3(0.2f, 0f, 1f), 8);

        DragTrail(root.transform, "DragTrail_05",
            new Vector3(-5f, 0.025f, 21f),
            new Vector3(1f, 0f, 0.3f), 6);

        // =====================================================
        // DROPLET TRAILS
        // =====================================================

        DropletTrail(root.transform, "Drops_01",
            new Vector3(-15f, 0.025f, -21f),
            new Vector3(0.7f, 0f, 1f), 9);

        DropletTrail(root.transform, "Drops_02",
            new Vector3(-6f, 0.025f, -5f),
            new Vector3(1f, 0f, 0.4f), 8);

        DropletTrail(root.transform, "Drops_03",
            new Vector3(9f, 0.025f, 3f),
            new Vector3(0.5f, 0f, 1f), 10);

        DropletTrail(root.transform, "Drops_04",
            new Vector3(-13f, 0.025f, 13f),
            new Vector3(0.8f, 0f, 0.7f), 8);

        DropletTrail(root.transform, "Drops_05",
            new Vector3(15f, 0.025f, 20f),
            new Vector3(-0.4f, 0f, 1f), 9);

        // =====================================================
        // SAVE
        // =====================================================

        Undo.RegisterCreatedObjectUndo(
            root,
            "Generate Facility Blood Marks"
        );

        Selection.activeGameObject = root;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Expanded facility blood decoration generated successfully."
        );
    }

    // =========================================================
    // MATERIALS
    // =========================================================

    private static void CreateMaterials()
    {
        bloodDark = CreateMaterial(
            "Blood_Dark",
            new Color(0.075f, 0.006f, 0.004f)
        );

        bloodDried = CreateMaterial(
            "Blood_Dried",
            new Color(0.20f, 0.015f, 0.009f)
        );

        bloodOld = CreateMaterial(
            "Blood_Old",
            new Color(0.12f, 0.025f, 0.015f)
        );

        bloodDeep = CreateMaterial(
            "Blood_Deep",
            new Color(0.28f, 0.012f, 0.008f)
        );
    }

    // =========================================================
    // WALL SPLATTER Z
    // =========================================================

    private static void WallSplatZ(
        Transform parent,
        string name,
        Vector3 position,
        bool positive,
        float width,
        float height,
        float angle)
    {
        GameObject holder = new GameObject(name);

        holder.transform.SetParent(parent);
        holder.transform.position = position;

        holder.transform.rotation = Quaternion.Euler(
            0f,
            positive ? 180f : 0f,
            0f
        );

        CreateSplat(holder.transform, width, height, angle);
    }

    // =========================================================
    // WALL SPLATTER X
    // =========================================================

    private static void WallSplatX(
        Transform parent,
        string name,
        Vector3 position,
        bool positive,
        float width,
        float height,
        float angle)
    {
        GameObject holder = new GameObject(name);

        holder.transform.SetParent(parent);
        holder.transform.position = position;

        holder.transform.rotation = Quaternion.Euler(
            0f,
            positive ? -90f : 90f,
            0f
        );

        CreateSplat(holder.transform, width, height, angle);
    }

    // =========================================================
    // SPLATTER
    // =========================================================

    private static void CreateSplat(
        Transform parent,
        float width,
        float height,
        float angle)
    {
        WallOval(
            parent,
            "Main_Splat",
            new Vector3(0f, 0f, -0.028f),
            width,
            height,
            angle,
            bloodDried
        );

        WallOval(
            parent,
            "Dark_Center",
            new Vector3(
                width * 0.12f,
                height * 0.05f,
                -0.036f
            ),
            width * 0.55f,
            height * 0.52f,
            angle + 15f,
            bloodDark
        );

        WallOval(
            parent,
            "Side_Splat",
            new Vector3(
                -width * 0.32f,
                height * 0.22f,
                -0.038f
            ),
            width * 0.35f,
            height * 0.25f,
            angle - 20f,
            bloodOld
        );

        WallDrop(parent,
            new Vector3(-width * 0.62f, height * 0.35f, -0.04f),
            0.11f);

        WallDrop(parent,
            new Vector3(width * 0.58f, height * 0.42f, -0.04f),
            0.08f);

        WallDrop(parent,
            new Vector3(width * 0.70f, -height * 0.18f, -0.04f),
            0.06f);

        WallDrop(parent,
            new Vector3(-width * 0.45f, -height * 0.40f, -0.04f),
            0.075f);

        WallDrop(parent,
            new Vector3(width * 0.30f, height * 0.60f, -0.04f),
            0.05f);

        WallDrop(parent,
            new Vector3(-width * 0.72f, 0f, -0.04f),
            0.045f);

        // Drips
        CreateBox(
            parent,
            "Drip_01",
            new Vector3(
                -width * 0.17f,
                -height * 0.65f,
                -0.038f
            ),
            new Vector3(
                0.045f,
                height * 0.70f,
                0.016f
            ),
            bloodDried
        );

        CreateBox(
            parent,
            "Drip_02",
            new Vector3(
                width * 0.15f,
                -height * 0.52f,
                -0.039f
            ),
            new Vector3(
                0.032f,
                height * 0.45f,
                0.016f
            ),
            bloodDark
        );

        CreateBox(
            parent,
            "Drip_03",
            new Vector3(
                width * 0.34f,
                -height * 0.45f,
                -0.039f
            ),
            new Vector3(
                0.025f,
                height * 0.32f,
                0.016f
            ),
            bloodOld
        );
    }

    // =========================================================
    // WALL OVAL
    // =========================================================

    private static void WallOval(
        Transform parent,
        string name,
        Vector3 position,
        float width,
        float height,
        float angle,
        Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Sphere
        );

        obj.name = name;

        obj.transform.SetParent(parent);
        obj.transform.localPosition = position;

        obj.transform.localRotation = Quaternion.Euler(
            0f,
            0f,
            angle
        );

        obj.transform.localScale = new Vector3(
            width,
            height,
            0.025f
        );

        SetMaterial(obj, material);
        RemoveCollider(obj);
    }

    // =========================================================
    // WALL DROP
    // =========================================================

    private static void WallDrop(
        Transform parent,
        Vector3 position,
        float size)
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Sphere
        );

        obj.name = "Wall_Drop";

        obj.transform.SetParent(parent);
        obj.transform.localPosition = position;

        obj.transform.localScale = new Vector3(
            size,
            size * 0.75f,
            0.018f
        );

        SetMaterial(obj, bloodDeep);
        RemoveCollider(obj);
    }

    // =========================================================
    // WALL SMEAR Z
    // =========================================================

    private static void WallSmearZ(
        Transform parent,
        string name,
        Vector3 position,
        bool positive,
        float length)
    {
        GameObject holder = new GameObject(name);

        holder.transform.SetParent(parent);
        holder.transform.position = position;

        holder.transform.rotation = Quaternion.Euler(
            0f,
            positive ? 180f : 0f,
            0f
        );

        CreateSmear(holder.transform, length);
    }

    // =========================================================
    // WALL SMEAR X
    // =========================================================

    private static void WallSmearX(
        Transform parent,
        string name,
        Vector3 position,
        bool positive,
        float length)
    {
        GameObject holder = new GameObject(name);

        holder.transform.SetParent(parent);
        holder.transform.position = position;

        holder.transform.rotation = Quaternion.Euler(
            0f,
            positive ? -90f : 90f,
            0f
        );

        CreateSmear(holder.transform, length);
    }

    // =========================================================
    // SMEAR
    // =========================================================

    private static void CreateSmear(
        Transform parent,
        float length)
    {
        CreateBox(
            parent,
            "Smear_Main",
            new Vector3(0f, 0f, -0.03f),
            new Vector3(
                0.18f,
                length,
                0.018f
            ),
            bloodOld
        );

        CreateBox(
            parent,
            "Smear_Left",
            new Vector3(
                -0.15f,
                -0.12f,
                -0.034f
            ),
            new Vector3(
                0.07f,
                length * 0.72f,
                0.016f
            ),
            bloodDried
        );

        CreateBox(
            parent,
            "Smear_Right",
            new Vector3(
                0.16f,
                -0.25f,
                -0.034f
            ),
            new Vector3(
                0.05f,
                length * 0.55f,
                0.016f
            ),
            bloodDark
        );

        WallDrop(
            parent,
            new Vector3(
                -0.18f,
                length * 0.40f,
                -0.04f
            ),
            0.14f
        );

        WallDrop(
            parent,
            new Vector3(
                0.19f,
                length * 0.30f,
                -0.04f
            ),
            0.09f
        );
    }

    // =========================================================
    // FLOOR POOL
    // =========================================================

    private static void FloorPool(
        Transform parent,
        string name,
        Vector3 position,
        float width,
        float length,
        float angle)
    {
        GameObject holder = new GameObject(name);

        holder.transform.SetParent(parent);
        holder.transform.position = position;

        holder.transform.rotation = Quaternion.Euler(
            0f,
            angle,
            0f
        );

        FloorOval(
            holder.transform,
            "Main_Pool",
            Vector3.zero,
            width,
            length,
            bloodDried
        );

        FloorOval(
            holder.transform,
            "Pool_Dark",
            new Vector3(
                width * 0.10f,
                0.004f,
                -length * 0.08f
            ),
            width * 0.62f,
            length * 0.58f,
            bloodDark
        );

        FloorOval(
            holder.transform,
            "Pool_Edge",
            new Vector3(
                -width * 0.32f,
                0.006f,
                length * 0.20f
            ),
            width * 0.42f,
            length * 0.32f,
            bloodOld
        );

        FloorDrop(
            holder.transform,
            new Vector3(width * 0.65f, 0.01f, length * 0.20f),
            0.12f
        );

        FloorDrop(
            holder.transform,
            new Vector3(-width * 0.58f, 0.01f, -length * 0.28f),
            0.09f
        );

        FloorDrop(
            holder.transform,
            new Vector3(width * 0.38f, 0.01f, -length * 0.58f),
            0.07f
        );

        FloorDrop(
            holder.transform,
            new Vector3(-width * 0.25f, 0.01f, length * 0.65f),
            0.055f
        );
    }

    // =========================================================
    // FLOOR OVAL
    // =========================================================

    private static void FloorOval(
        Transform parent,
        string name,
        Vector3 position,
        float width,
        float length,
        Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Sphere
        );

        obj.name = name;

        obj.transform.SetParent(parent);
        obj.transform.localPosition = position;

        obj.transform.localScale = new Vector3(
            width,
            0.018f,
            length
        );

        SetMaterial(obj, material);
        RemoveCollider(obj);
    }

    // =========================================================
    // FLOOR DROP
    // =========================================================

    private static void FloorDrop(
        Transform parent,
        Vector3 position,
        float size)
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Sphere
        );

        obj.name = "Floor_Drop";

        obj.transform.SetParent(parent);
        obj.transform.localPosition = position;

        obj.transform.localScale = new Vector3(
            size,
            0.015f,
            size * 0.85f
        );

        SetMaterial(obj, bloodDeep);
        RemoveCollider(obj);
    }

    // =========================================================
    // DRAG TRAIL
    // =========================================================

    private static void DragTrail(
        Transform parent,
        string name,
        Vector3 start,
        Vector3 direction,
        int count)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent);

        Vector3 dir = direction.normalized;

        float angle = Mathf.Atan2(
            dir.x,
            dir.z
        ) * Mathf.Rad2Deg;

        for (int i = 0; i < count; i++)
        {
            Vector3 position =
                start + dir * (i * 0.62f);

            position.y = 0.025f;

            GameObject obj = GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

            obj.name = "Drag_Smear_" + i;

            obj.transform.SetParent(holder.transform);
            obj.transform.position = position;

            float width =
                Mathf.Max(
                    0.42f - (i * 0.025f),
                    0.22f
                );

            obj.transform.localScale = new Vector3(
                width,
                0.018f,
                0.78f
            );

            obj.transform.rotation = Quaternion.Euler(
                0f,
                angle,
                0f
            );

            SetMaterial(
                obj,
                i % 3 == 0
                    ? bloodDark
                    : bloodDried
            );

            RemoveCollider(obj);
        }
    }

    // =========================================================
    // DROPLET TRAIL
    // =========================================================

    private static void DropletTrail(
        Transform parent,
        string name,
        Vector3 start,
        Vector3 direction,
        int count)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent);

        Vector3 dir = direction.normalized;

        for (int i = 0; i < count; i++)
        {
            Vector3 side =
                Vector3.Cross(
                    dir,
                    Vector3.up
                ) * ((i % 2 == 0) ? 0.12f : -0.10f);

            Vector3 position =
                start +
                dir * (i * 0.52f) +
                side;

            position.y = 0.025f;

            float size =
                0.055f +
                ((i % 4) * 0.018f);

            GameObject obj = GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

            obj.name = "Trail_Drop_" + i;

            obj.transform.SetParent(holder.transform);
            obj.transform.position = position;

            obj.transform.localScale = new Vector3(
                size,
                0.014f,
                size * 0.85f
            );

            SetMaterial(
                obj,
                i % 2 == 0
                    ? bloodDried
                    : bloodDark
            );

            RemoveCollider(obj);
        }
    }

    // =========================================================
    // BOX
    // =========================================================

    private static GameObject CreateBox(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Cube
        );

        obj.name = name;

        obj.transform.SetParent(parent);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = localScale;

        SetMaterial(obj, material);
        RemoveCollider(obj);

        return obj;
    }

    // =========================================================
    // MATERIAL CREATION
    // =========================================================

    private static Material CreateMaterial(
        string materialName,
        Color color)
    {
        if (!AssetDatabase.IsValidFolder(MATERIAL_FOLDER))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "Materials"
            );
        }

        string path =
            MATERIAL_FOLDER +
            "/" +
            materialName +
            ".mat";

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(
                path
            );

        if (material == null)
        {
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Lit"
            );

            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader);
            material.name = materialName;

            AssetDatabase.CreateAsset(
                material,
                path
            );
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.06f);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.06f);

        EditorUtility.SetDirty(material);

        return material;
    }

    // =========================================================
    // SET MATERIAL
    // =========================================================

    private static void SetMaterial(
        GameObject obj,
        Material material)
    {
        Renderer renderer =
            obj.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    // =========================================================
    // REMOVE COLLIDER
    // =========================================================

    private static void RemoveCollider(
        GameObject obj)
    {
        Collider collider =
            obj.GetComponent<Collider>();

        if (collider != null)
            DestroyImmediate(collider);
    }
}