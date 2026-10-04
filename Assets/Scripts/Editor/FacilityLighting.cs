using UnityEngine;
using UnityEditor;

public class FacilityLighting : EditorWindow
{
    private const string ROOT_NAME = "Facility_Lighting";

    [MenuItem("Facility Breach/Generate Yellow Hanging Lights")]
    public static void GenerateLighting()
    {
        // =====================================================
        // DELETE OLD GENERATED LIGHTING
        // =====================================================

        GameObject oldRoot = GameObject.Find(ROOT_NAME);

        if (oldRoot != null)
        {
            DestroyImmediate(oldRoot);
        }

        // Disable Unity's original Directional Light
        Light[] existingLights =
            Object.FindObjectsByType<Light>(
                FindObjectsSortMode.None
            );

        foreach (Light light in existingLights)
        {
            if (light.type == LightType.Directional)
            {
                light.enabled = false;
            }
        }

        // =====================================================
        // CREATE LIGHTING ROOT
        // =====================================================

        GameObject root =
            new GameObject(ROOT_NAME);

        // =====================================================
        // MATERIALS
        // =====================================================

        Material metalMaterial =
            CreateMaterial(
                "Facility_Lamp_Metal",
                new Color(
                    0.08f,
                    0.08f,
                    0.07f,
                    1f
                ),
                false
            );

        Material cableMaterial =
            CreateMaterial(
                "Facility_Lamp_Cable",
                new Color(
                    0.03f,
                    0.03f,
                    0.03f,
                    1f
                ),
                false
            );

        Material yellowGlow =
            CreateMaterial(
                "Facility_Lamp_YellowGlow",
                new Color(
                    1.0f,
                    0.65f,
                    0.15f,
                    1f
                ),
                true
            );

        // =====================================================
        // LIGHT POSITIONS
        // =====================================================
        //
        // Roof underside is around Y = 4.1
        // Fixtures hang down to around Y = 3.3
        //
        // Positions distributed throughout the facility.
        // =====================================================

        Vector3[] lightPositions =
        {
            // Entrance
            new Vector3(-13f, 3.25f, -25f),

            // Southern corridor
            new Vector3(-4f, 3.25f, -20f),
            new Vector3(7f, 3.25f, -20f),

            // Room 1
            new Vector3(-13f, 3.25f, -11f),

            // Central area
            new Vector3(1f, 3.25f, -5f),
            new Vector3(1f, 3.25f, 4f),

            // Left middle corridor
            new Vector3(-13f, 3.25f, 5f),

            // Right middle corridor
            new Vector3(14f, 3.25f, 5f),

            // Upper-left
            new Vector3(-11f, 3.25f, 16f),

            // Room 3 / upper-right
            new Vector3(14f, 3.25f, 17f),

            // Northern corridor
            new Vector3(-4f, 3.25f, 25f),

            // Exit
            new Vector3(13f, 3.25f, 26f)
        };

        // =====================================================
        // GENERATE LIGHTS
        // =====================================================

        for (int i = 0; i < lightPositions.Length; i++)
        {
            CreateHangingLight(
                root.transform,
                "HangingLight_" + (i + 1),
                lightPositions[i],
                metalMaterial,
                cableMaterial,
                yellowGlow
            );
        }

        // =====================================================
        // FINISH
        // =====================================================

        Undo.RegisterCreatedObjectUndo(
            root,
            "Generate Yellow Hanging Lights"
        );

        Selection.activeGameObject = root;

        AssetDatabase.SaveAssets();

        Debug.Log(
            lightPositions.Length +
            " warm yellow hanging lights generated successfully."
        );
    }

    // =========================================================
    // CREATE ONE HANGING LIGHT
    // =========================================================

    private static void CreateHangingLight(
        Transform parent,
        string lightName,
        Vector3 position,
        Material metalMaterial,
        Material cableMaterial,
        Material glowMaterial)
    {
        GameObject holder =
            new GameObject(lightName);

        holder.transform.SetParent(parent);
        holder.transform.position = position;

        // =====================================================
        // HANGING CABLE
        // =====================================================

        GameObject cable =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        cable.name = "Cable";

        cable.transform.SetParent(
            holder.transform
        );

        cable.transform.localPosition =
            new Vector3(
                0f,
                0.48f,
                0f
            );

        cable.transform.localScale =
            new Vector3(
                0.025f,
                0.38f,
                0.025f
            );

        SetMaterial(
            cable,
            cableMaterial
        );

        // Remove unnecessary collider
        Collider cableCollider =
            cable.GetComponent<Collider>();

        if (cableCollider != null)
        {
            DestroyImmediate(cableCollider);
        }

        // =====================================================
        // TOP CONNECTOR
        // =====================================================

        GameObject connector =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        connector.name = "RoofConnector";

        connector.transform.SetParent(
            holder.transform
        );

        connector.transform.localPosition =
            new Vector3(
                0f,
                0.88f,
                0f
            );

        connector.transform.localScale =
            new Vector3(
                0.12f,
                0.05f,
                0.12f
            );

        SetMaterial(
            connector,
            metalMaterial
        );

        RemoveCollider(connector);

        // =====================================================
        // INDUSTRIAL LAMP SHADE
        // =====================================================

        GameObject shade =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

        shade.name = "Industrial_Shade";

        shade.transform.SetParent(
            holder.transform
        );

        shade.transform.localPosition =
            new Vector3(
                0f,
                0.02f,
                0f
            );

        shade.transform.localScale =
            new Vector3(
                0.55f,
                0.12f,
                0.55f
            );

        SetMaterial(
            shade,
            metalMaterial
        );

        RemoveCollider(shade);

        // =====================================================
        // GLOWING YELLOW BULB
        // =====================================================

        GameObject bulb =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        bulb.name = "Yellow_Bulb";

        bulb.transform.SetParent(
            holder.transform
        );

        bulb.transform.localPosition =
            new Vector3(
                0f,
                -0.18f,
                0f
            );

        bulb.transform.localScale =
            new Vector3(
                0.32f,
                0.22f,
                0.32f
            );

        SetMaterial(
            bulb,
            glowMaterial
        );

        RemoveCollider(bulb);

        // =====================================================
        // ACTUAL YELLOW LIGHT
        // =====================================================

        GameObject lightObject =
            new GameObject(
                "WarmYellowLight"
            );

        lightObject.transform.SetParent(
            holder.transform
        );

        lightObject.transform.localPosition =
            new Vector3(
                0f,
                -0.28f,
                0f
            );

        Light pointLight =
            lightObject.AddComponent<Light>();

        pointLight.type =
            LightType.Point;

        // Warm industrial yellow
        pointLight.color =
            new Color(
                1.0f,
                0.68f,
                0.28f
            );

        pointLight.intensity = 3.2f;

        pointLight.range = 10f;

        pointLight.shadows =
            LightShadows.Soft;

        pointLight.shadowStrength =
            0.65f;

        pointLight.renderMode =
            LightRenderMode.Auto;
    }

    // =========================================================
    // CREATE MATERIAL
    // =========================================================

    private static Material CreateMaterial(
        string materialName,
        Color color,
        bool emission)
    {
        const string folder =
            "Assets/Materials";

        if (!AssetDatabase.IsValidFolder(
            folder))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "Materials"
            );
        }

        string path =
            folder +
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

            material.name =
                materialName;

            AssetDatabase.CreateAsset(
                material,
                path
            );
        }

        // Base color
        if (material.HasProperty(
            "_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color
            );
        }

        if (material.HasProperty(
            "_Color"))
        {
            material.SetColor(
                "_Color",
                color
            );
        }

        // Emission for bulb
        if (emission)
        {
            Color emissionColor =
                new Color(
                    1.0f,
                    0.48f,
                    0.08f,
                    1f
                ) * 3f;

            material.EnableKeyword(
                "_EMISSION"
            );

            if (material.HasProperty(
                "_EmissionColor"))
            {
                material.SetColor(
                    "_EmissionColor",
                    emissionColor
                );
            }
        }

        EditorUtility.SetDirty(
            material
        );

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
        {
            renderer.sharedMaterial =
                material;
        }
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