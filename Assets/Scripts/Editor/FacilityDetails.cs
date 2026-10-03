using UnityEngine;
using UnityEditor;

public class FacilityDetails : EditorWindow
{
    private const string ROOT_NAME = "Facility_Details";
    private const string MATERIAL_FOLDER = "Assets/Materials";

    private static Material darkMetal;
    private static Material pipeMetal;
    private static Material hazardYellow;
    private static Material warningRed;
    private static Material industrialOrange;
    private static Material panelBlue;
    private static Material screenGlow;
    private static Material ventMaterial;
    private static Material tealTrim;

    [MenuItem("Facility Breach/Generate Facility Details")]
    public static void GenerateDetails()
    {
        // =====================================================
        // REMOVE OLD DETAILS
        // =====================================================

        GameObject oldRoot = GameObject.Find(ROOT_NAME);

        if (oldRoot != null)
        {
            DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject(ROOT_NAME);

        // =====================================================
        // MATERIALS
        // =====================================================

        darkMetal = CreateMaterial(
            "Detail_DarkMetal",
            new Color(0.10f, 0.11f, 0.12f),
            false
        );

        pipeMetal = CreateMaterial(
            "Detail_PipeMetal",
            new Color(0.45f, 0.48f, 0.50f),
            false
        );

        hazardYellow = CreateMaterial(
            "Detail_HazardYellow",
            new Color(0.95f, 0.65f, 0.08f),
            false
        );

        warningRed = CreateMaterial(
            "Detail_WarningRed",
            new Color(0.60f, 0.08f, 0.06f),
            false
        );

        industrialOrange = CreateMaterial(
            "Detail_IndustrialOrange",
            new Color(0.75f, 0.30f, 0.07f),
            false
        );

        panelBlue = CreateMaterial(
            "Detail_PanelBlue",
            new Color(0.08f, 0.25f, 0.38f),
            false
        );

        screenGlow = CreateMaterial(
            "Detail_ScreenGlow",
            new Color(0.05f, 0.65f, 0.85f),
            true
        );

        ventMaterial = CreateMaterial(
            "Detail_VentMetal",
            new Color(0.25f, 0.28f, 0.30f),
            false
        );

        tealTrim = CreateMaterial(
            "Detail_TealTrim",
            new Color(0.05f, 0.35f, 0.38f),
            false
        );

        // =====================================================
        // 1. SUPPORT COLUMNS
        // Positioned mainly near walls/corners.
        // =====================================================

        CreateBox(
            root.transform,
            "SupportColumn_01",
            new Vector3(-19.5f, 2f, -20f),
            new Vector3(0.7f, 4f, 0.7f),
            darkMetal
        );

        CreateBox(
            root.transform,
            "SupportColumn_02",
            new Vector3(19.5f, 2f, -20f),
            new Vector3(0.7f, 4f, 0.7f),
            darkMetal
        );

        CreateBox(
            root.transform,
            "SupportColumn_03",
            new Vector3(-19.5f, 2f, 0f),
            new Vector3(0.7f, 4f, 0.7f),
            darkMetal
        );

        CreateBox(
            root.transform,
            "SupportColumn_04",
            new Vector3(19.5f, 2f, 0f),
            new Vector3(0.7f, 4f, 0.7f),
            darkMetal
        );

        CreateBox(
            root.transform,
            "SupportColumn_05",
            new Vector3(-19.5f, 2f, 20f),
            new Vector3(0.7f, 4f, 0.7f),
            darkMetal
        );

        CreateBox(
            root.transform,
            "SupportColumn_06",
            new Vector3(19.5f, 2f, 20f),
            new Vector3(0.7f, 4f, 0.7f),
            darkMetal
        );

        // =====================================================
        // 2. WALL TRIMS
        // Helps navigation and separates floor/walls.
        // =====================================================

        CreateBox(
            root.transform,
            "LeftWallTrim",
            new Vector3(-20.65f, 0.45f, 0f),
            new Vector3(0.10f, 0.35f, 56f),
            tealTrim
        );

        CreateBox(
            root.transform,
            "RightWallTrim",
            new Vector3(20.65f, 0.45f, 0f),
            new Vector3(0.10f, 0.35f, 56f),
            tealTrim
        );

        // =====================================================
        // 3. OVERHEAD INDUSTRIAL PIPES
        // High enough that they don't block navigation.
        // =====================================================

        CreatePipeZ(
            root.transform,
            "Pipe_Left_Main",
            new Vector3(-19f, 3.35f, 0f),
            52f,
            0.16f,
            pipeMetal
        );

        CreatePipeZ(
            root.transform,
            "Pipe_Right_Main",
            new Vector3(19f, 3.35f, 0f),
            52f,
            0.16f,
            pipeMetal
        );

        CreatePipeZ(
            root.transform,
            "Pipe_Left_Secondary",
            new Vector3(-18.5f, 3.05f, 2f),
            48f,
            0.10f,
            industrialOrange
        );

        // =====================================================
        // 4. VENTS
        // =====================================================

        CreateVent(
            root.transform,
            "Vent_SouthWest",
            new Vector3(-20.35f, 2.7f, -20f),
            new Vector3(0.18f, 1.0f, 2.2f)
        );

        CreateVent(
            root.transform,
            "Vent_WestMiddle",
            new Vector3(-20.35f, 2.7f, 3f),
            new Vector3(0.18f, 1.0f, 2.2f)
        );

        CreateVent(
            root.transform,
            "Vent_EastMiddle",
            new Vector3(20.35f, 2.7f, 5f),
            new Vector3(0.18f, 1.0f, 2.2f)
        );

        CreateVent(
            root.transform,
            "Vent_NorthEast",
            new Vector3(20.35f, 2.7f, 20f),
            new Vector3(0.18f, 1.0f, 2.2f)
        );

        // =====================================================
        // 5. ELECTRICAL / CONTROL PANELS
        // =====================================================

        CreateControlPanel(
            root.transform,
            "EntranceControl",
            new Vector3(-16.5f, 1.3f, -29.35f),
            new Vector3(1.2f, 1.8f, 0.25f)
        );

        CreateControlPanel(
            root.transform,
            "Room1Control",
            new Vector3(-16f, 1.3f, -6.15f),
            new Vector3(1.2f, 1.8f, 0.25f)
        );

        CreateControlPanel(
            root.transform,
            "CentralControl",
            new Vector3(5f, 1.3f, 9.65f),
            new Vector3(1.2f, 1.8f, 0.25f)
        );

        CreateControlPanel(
            root.transform,
            "Room3Control",
            new Vector3(17f, 1.3f, 23.65f),
            new Vector3(1.2f, 1.8f, 0.25f)
        );

        CreateControlPanel(
            root.transform,
            "ExitControl",
            new Vector3(16f, 1.3f, 29.35f),
            new Vector3(1.2f, 1.8f, 0.25f)
        );

        // =====================================================
        // 6. HAZARD MARKERS AROUND IMPORTANT AREAS
        // Thin strips on floor, not navigation blockers.
        // =====================================================

        CreateHazardStrip(
            root.transform,
            "EntranceHazard",
            new Vector3(-13f, 0.03f, -27.5f),
            new Vector3(4.5f, 0.04f, 0.25f)
        );

        CreateHazardStrip(
            root.transform,
            "CentralHazard",
            new Vector3(1f, 0.03f, -1.5f),
            new Vector3(5f, 0.04f, 0.25f)
        );

        CreateHazardStrip(
            root.transform,
            "Room3Hazard",
            new Vector3(14f, 0.03f, 12.5f),
            new Vector3(3f, 0.04f, 0.25f)
        );

        CreateHazardStrip(
            root.transform,
            "ExitHazard",
            new Vector3(13f, 0.03f, 27.5f),
            new Vector3(4.5f, 0.04f, 0.25f)
        );

        // =====================================================
        // 7. ROOM 1 - STORAGE AREA
        // =====================================================

        CreateCrate(
            root.transform,
            "Room1_Crate_A",
            new Vector3(-17f, 0.65f, -10f),
            new Vector3(1.3f, 1.3f, 1.3f)
        );

        CreateCrate(
            root.transform,
            "Room1_Crate_B",
            new Vector3(-15.4f, 0.5f, -10f),
            new Vector3(1f, 1f, 1f)
        );

        CreateCrate(
            root.transform,
            "Room1_Crate_C",
            new Vector3(-17f, 1.75f, -10f),
            new Vector3(1.1f, 0.8f, 1.1f)
        );

        // =====================================================
        // 8. ROOM 2 - CONTROL / SERVER AREA
        // =====================================================

        CreateMachine(
            root.transform,
            "Room2_Server_A",
            new Vector3(-4.8f, 1.2f, 1f),
            new Vector3(1.1f, 2.4f, 1.0f)
        );

        CreateMachine(
            root.transform,
            "Room2_Server_B",
            new Vector3(-4.8f, 1.2f, 4f),
            new Vector3(1.1f, 2.4f, 1.0f)
        );

        CreateMachine(
            root.transform,
            "Room2_Server_C",
            new Vector3(-4.8f, 1.2f, 7f),
            new Vector3(1.1f, 2.4f, 1.0f)
        );

        // =====================================================
        // 9. ROOM 3 - MACHINERY AREA
        // =====================================================

        CreateMachine(
            root.transform,
            "Room3_Machine_A",
            new Vector3(17f, 0.9f, 16f),
            new Vector3(2f, 1.8f, 1.5f)
        );

        CreateMachine(
            root.transform,
            "Room3_Machine_B",
            new Vector3(17f, 0.9f, 20f),
            new Vector3(2f, 1.8f, 1.5f)
        );

        // =====================================================
        // 10. WALL WARNING PANELS
        // =====================================================

        CreateWarningPanel(
            root.transform,
            "WarningPanel_Entrance",
            new Vector3(-10f, 2.2f, -29.45f),
            new Vector3(2f, 0.8f, 0.12f)
        );

        CreateWarningPanel(
            root.transform,
            "WarningPanel_Room3",
            new Vector3(17f, 2.2f, 23.55f),
            new Vector3(2f, 0.8f, 0.12f)
        );

        CreateWarningPanel(
            root.transform,
            "WarningPanel_Exit",
            new Vector3(9f, 2.2f, 29.45f),
            new Vector3(2f, 0.8f, 0.12f)
        );

        // =====================================================
        // COMPLETE
        // =====================================================

        Undo.RegisterCreatedObjectUndo(
            root,
            "Generate Facility Details"
        );

        Selection.activeGameObject = root;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Facility environmental details generated successfully."
        );
    }

    // =========================================================
    // CONTROL PANEL
    // =========================================================

    private static void CreateControlPanel(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale)
    {
        GameObject panel = CreateBox(
            parent,
            name,
            position,
            scale,
            panelBlue
        );

        GameObject screen = CreateBox(
            panel.transform,
            "Screen",
            position + new Vector3(0f, 0.25f, -0.15f),
            new Vector3(
                scale.x * 0.65f,
                scale.y * 0.30f,
                0.08f
            ),
            screenGlow
        );

        screen.transform.SetParent(parent);

        GameObject warning = CreateBox(
            panel.transform,
            "WarningLight",
            position + new Vector3(
                0f,
                -0.35f,
                -0.16f
            ),
            new Vector3(
                0.25f,
                0.25f,
                0.08f
            ),
            warningRed
        );

        warning.transform.SetParent(parent);
    }

    // =========================================================
    // VENT
    // =========================================================

    private static void CreateVent(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale)
    {
        GameObject vent = CreateBox(
            parent,
            name,
            position,
            scale,
            ventMaterial
        );

        // Vent bars
        for (int i = -2; i <= 2; i++)
        {
            CreateBox(
                parent,
                name + "_Bar_" + i,
                position +
                new Vector3(
                    -0.11f,
                    i * 0.15f,
                    0f
                ),
                new Vector3(
                    0.08f,
                    0.05f,
                    scale.z * 0.8f
                ),
                darkMetal
            );
        }
    }

    // =========================================================
    // MACHINE
    // =========================================================

    private static void CreateMachine(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale)
    {
        CreateBox(
            parent,
            name,
            position,
            scale,
            darkMetal
        );

        CreateBox(
            parent,
            name + "_Panel",
            position +
            new Vector3(
                0f,
                0.25f,
                -(scale.z / 2f) - 0.03f
            ),
            new Vector3(
                scale.x * 0.65f,
                scale.y * 0.35f,
                0.08f
            ),
            screenGlow
        );

        CreateBox(
            parent,
            name + "_Warning",
            position +
            new Vector3(
                scale.x * 0.3f,
                scale.y * 0.35f,
                -(scale.z / 2f) - 0.04f
            ),
            new Vector3(
                0.18f,
                0.18f,
                0.08f
            ),
            warningRed
        );
    }

    // =========================================================
    // CRATE
    // =========================================================

    private static void CreateCrate(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale)
    {
        GameObject crate = CreateBox(
            parent,
            name,
            position,
            scale,
            industrialOrange
        );

        // Dark band around crate
        CreateBox(
            parent,
            name + "_Band",
            position,
            new Vector3(
                scale.x + 0.04f,
                scale.y * 0.15f,
                scale.z + 0.04f
            ),
            darkMetal
        );
    }

    // =========================================================
    // WARNING PANEL
    // =========================================================

    private static void CreateWarningPanel(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale)
    {
        CreateBox(
            parent,
            name,
            position,
            scale,
            warningRed
        );

        CreateBox(
            parent,
            name + "_Center",
            position +
            new Vector3(
                0f,
                0f,
                -0.07f
            ),
            new Vector3(
                scale.x * 0.65f,
                scale.y * 0.35f,
                0.05f
            ),
            hazardYellow
        );
    }

    // =========================================================
    // HAZARD FLOOR STRIP
    // =========================================================

    private static void CreateHazardStrip(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale)
    {
        CreateBox(
            parent,
            name,
            position,
            scale,
            hazardYellow
        );

        // Small dark sections create a simple hazard pattern.
        int count = 5;

        for (int i = 0; i < count; i++)
        {
            float x =
                position.x -
                (scale.x / 2f) +
                ((i + 0.5f) * scale.x / count);

            CreateBox(
                parent,
                name + "_Dark_" + i,
                new Vector3(
                    x,
                    position.y + 0.025f,
                    position.z
                ),
                new Vector3(
                    scale.x / (count * 2f),
                    0.02f,
                    scale.z + 0.02f
                ),
                darkMetal
            );
        }
    }

    // =========================================================
    // PIPE ALONG Z
    // =========================================================

    private static void CreatePipeZ(
        Transform parent,
        string name,
        Vector3 position,
        float length,
        float radius,
        Material material)
    {
        GameObject pipe =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        pipe.name = name;
        pipe.transform.SetParent(parent);

        pipe.transform.position = position;

        // Cylinder normally points along Y.
        // Rotate to run along Z.
        pipe.transform.rotation =
            Quaternion.Euler(
                90f,
                0f,
                0f
            );

        pipe.transform.localScale =
            new Vector3(
                radius,
                length / 2f,
                radius
            );

        Renderer renderer =
            pipe.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        RemoveCollider(pipe);
    }

    // =========================================================
    // GENERIC BOX
    // =========================================================

    private static GameObject CreateBox(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject obj =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        obj.name = name;

        obj.transform.SetParent(parent);

        obj.transform.position = position;

        obj.transform.localScale = scale;

        Renderer renderer =
            obj.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        return obj;
    }

    // =========================================================
    // CREATE MATERIAL
    // =========================================================

    private static Material CreateMaterial(
        string materialName,
        Color color,
        bool emission)
    {
        if (!AssetDatabase.IsValidFolder(
            MATERIAL_FOLDER))
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
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit"
                );

            if (shader == null)
            {
                shader =
                    Shader.Find("Standard");
            }

            material =
                new Material(shader);

            material.name = materialName;

            AssetDatabase.CreateAsset(
                material,
                path
            );
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color
            );
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor(
                "_Color",
                color
            );
        }

        if (emission)
        {
            material.EnableKeyword("_EMISSION");

            if (material.HasProperty(
                "_EmissionColor"))
            {
                material.SetColor(
                    "_EmissionColor",
                    color * 2.5f
                );
            }
        }

        EditorUtility.SetDirty(material);

        return material;
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
        {
            DestroyImmediate(collider);
        }
    }
}