using UnityEngine;
using UnityEditor;

public class FacilityRoof : EditorWindow
{
    private const string ROOT_NAME = "Facility_Roof";

    [MenuItem("Facility Breach/Generate Roof")]
    public static void GenerateRoof()
    {
        // Remove previous generated roof
        GameObject oldRoot = GameObject.Find(ROOT_NAME);

        if (oldRoot != null)
        {
            DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject(ROOT_NAME);

        // =====================================================
        // MATERIAL
        // =====================================================

        Material roofMaterial = CreateMaterial(
            "Facility_Roof_Dark",
            new Color(0.16f, 0.18f, 0.20f)
        );

        // =====================================================
        // ROOF
        // Facility is approximately 42 x 60
        //
        // Instead of one giant piece, use 6 roof sections.
        // This gives the ceiling more visible structure.
        // =====================================================

        float roofY = 4.25f;
        float thickness = 0.30f;

        CreateRoofSection(
            root.transform,
            "Roof_Section_01",
            new Vector3(-10.5f, roofY, -20f),
            new Vector3(21f, thickness, 20f),
            roofMaterial
        );

        CreateRoofSection(
            root.transform,
            "Roof_Section_02",
            new Vector3(10.5f, roofY, -20f),
            new Vector3(21f, thickness, 20f),
            roofMaterial
        );

        CreateRoofSection(
            root.transform,
            "Roof_Section_03",
            new Vector3(-10.5f, roofY, 0f),
            new Vector3(21f, thickness, 20f),
            roofMaterial
        );

        CreateRoofSection(
            root.transform,
            "Roof_Section_04",
            new Vector3(10.5f, roofY, 0f),
            new Vector3(21f, thickness, 20f),
            roofMaterial
        );

        CreateRoofSection(
            root.transform,
            "Roof_Section_05",
            new Vector3(-10.5f, roofY, 20f),
            new Vector3(21f, thickness, 20f),
            roofMaterial
        );

        CreateRoofSection(
            root.transform,
            "Roof_Section_06",
            new Vector3(10.5f, roofY, 20f),
            new Vector3(21f, thickness, 20f),
            roofMaterial
        );

        // =====================================================
        // SUPPORT BEAMS
        // Adds visible industrial structure under the roof.
        // =====================================================

        Material beamMaterial = CreateMaterial(
            "Facility_Roof_Beams",
            new Color(0.08f, 0.09f, 0.10f)
        );

        float beamY = 4.02f;

        CreateBeam(
            root.transform,
            "Beam_01",
            new Vector3(0f, beamY, -20f),
            new Vector3(42f, 0.22f, 0.35f),
            beamMaterial
        );

        CreateBeam(
            root.transform,
            "Beam_02",
            new Vector3(0f, beamY, -10f),
            new Vector3(42f, 0.22f, 0.35f),
            beamMaterial
        );

        CreateBeam(
            root.transform,
            "Beam_03",
            new Vector3(0f, beamY, 0f),
            new Vector3(42f, 0.22f, 0.35f),
            beamMaterial
        );

        CreateBeam(
            root.transform,
            "Beam_04",
            new Vector3(0f, beamY, 10f),
            new Vector3(42f, 0.22f, 0.35f),
            beamMaterial
        );

        CreateBeam(
            root.transform,
            "Beam_05",
            new Vector3(0f, beamY, 20f),
            new Vector3(42f, 0.22f, 0.35f),
            beamMaterial
        );

        // Long center support
        CreateBeam(
            root.transform,
            "Beam_Center",
            new Vector3(0f, beamY - 0.03f, 0f),
            new Vector3(0.35f, 0.25f, 60f),
            beamMaterial
        );

        Undo.RegisterCreatedObjectUndo(
            root,
            "Generate Facility Roof"
        );

        Selection.activeGameObject = root;

        Debug.Log(
            "Facility roof generated successfully. " +
            "Next step: generate hanging yellow lights."
        );
    }

    // =========================================================
    // CREATE ROOF SECTION
    // =========================================================

    private static void CreateRoofSection(
        Transform parent,
        string objectName,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject roof =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        roof.name = objectName;
        roof.transform.SetParent(parent);

        roof.transform.position = position;
        roof.transform.localScale = scale;

        Renderer renderer =
            roof.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    // =========================================================
    // CREATE SUPPORT BEAM
    // =========================================================

    private static void CreateBeam(
        Transform parent,
        string objectName,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject beam =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        beam.name = objectName;
        beam.transform.SetParent(parent);

        beam.transform.position = position;
        beam.transform.localScale = scale;

        Renderer renderer =
            beam.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    // =========================================================
    // CREATE / GET MATERIAL
    // =========================================================

    private static Material CreateMaterial(
        string materialName,
        Color color)
    {
        const string folder = "Assets/Materials";

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "Materials"
            );
        }

        string path =
            folder + "/" + materialName + ".mat";

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
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
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

        EditorUtility.SetDirty(material);

        AssetDatabase.SaveAssets();

        return material;
    }
}