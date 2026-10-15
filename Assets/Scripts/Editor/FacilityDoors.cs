using UnityEngine;
using UnityEditor;

public class FacilityDoors : EditorWindow
{
    private const string MaterialFolder = "Assets/Materials";

    private static Material doorMat;
    private static Material frameMat;
    private static Material indicatorMat;

    private const float DOOR_WIDTH = 2f;

    [MenuItem("Facility Breach/Generate Interactive Doors")]
    public static void GenerateDoors()
    {
        GameObject facility = GameObject.Find("Generated_Facility");

        if (facility == null)
        {
            Debug.LogError("Generated_Facility not found.");
            return;
        }

        GameObject oldDoors = GameObject.Find("Facility_Doors");

        if (oldDoors != null)
            DestroyImmediate(oldDoors);

        GameObject root = new GameObject("Facility_Doors");

        CreateMaterials();

        // MAIN ENTRANCE
        CreateDoorZ(
            "Door_Main_Entrance",
            new Vector3(-12.5f, 0, -29.75f),
            DOOR_WIDTH,
            root.transform
        );

        // ROOM 1
        CreateDoorX(
            "Door_Room1_Right",
            new Vector3(-8, 0, -11.75f),
            DOOR_WIDTH,
            root.transform
        );

        CreateDoorZ(
            "Door_Room1_Top",
            new Vector3(-13, 0, -6.5f),
            DOOR_WIDTH,
            root.transform
        );

        // ROOM 2
        CreateDoorZ(
            "Door_Room2_Bottom",
            new Vector3(1, 0, -2),
            DOOR_WIDTH,
            root.transform
        );

        CreateDoorZ(
            "Door_Room2_Top",
            new Vector3(1, 0, 10),
            DOOR_WIDTH,
            root.transform
        );

        CreateDoorX(
            "Door_Room2_Left",
            new Vector3(-7, 0, 4),
            DOOR_WIDTH,
            root.transform
        );

        CreateDoorX(
            "Door_Room2_Right",
            new Vector3(9, 0, 4),
            DOOR_WIDTH,
            root.transform
        );

        // ROOM 3
        CreateDoorX(
            "Door_Room3_Left",
            new Vector3(10, 0, 18.75f),
            DOOR_WIDTH,
            root.transform
        );

        CreateDoorZ(
            "Door_Room3_Bottom",
            new Vector3(14, 0, 12),
            DOOR_WIDTH,
            root.transform
        );

        CreateDoorZ(
            "Door_Room3_Top",
            new Vector3(14, 0, 24),
            DOOR_WIDTH,
            root.transform
        );

        // MAIN EXIT
        CreateDoorZ(
            "Door_Main_Exit",
            new Vector3(12.5f, 0, 29.75f),
            DOOR_WIDTH,
            root.transform
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = root;

        Debug.Log(
            "Interactive doors generated. All doors are 2 units wide."
        );
    }

    // =====================================================
    // Z WALL DOOR
    // =====================================================

    private static void CreateDoorZ(
        string name,
        Vector3 position,
        float width,
        Transform parent)
    {
        GameObject holder = new GameObject(name);

        holder.transform.position = position;
        holder.transform.SetParent(parent);

        CreateCube(
            name + "_Frame_Left",
            position + new Vector3(
                -width / 2f - 0.15f,
                1.5f,
                0
            ),
            new Vector3(0.3f, 3f, 0.45f),
            frameMat,
            holder.transform
        );

        CreateCube(
            name + "_Frame_Right",
            position + new Vector3(
                width / 2f + 0.15f,
                1.5f,
                0
            ),
            new Vector3(0.3f, 3f, 0.45f),
            frameMat,
            holder.transform
        );

        CreateCube(
            name + "_Frame_Top",
            position + new Vector3(0, 3.1f, 0),
            new Vector3(width + 0.6f, 0.25f, 0.45f),
            frameMat,
            holder.transform
        );

        GameObject hinge =
            new GameObject(name + "_Hinge");

        hinge.transform.position =
            position +
            new Vector3(-width / 2f, 0, 0);

        hinge.transform.SetParent(holder.transform);

        InteractiveDoor door =
            hinge.AddComponent<InteractiveDoor>();

        door.openAngle = 90f;
        door.openSpeed = 4f;

        GameObject panel = CreateCube(
            name + "_Panel",
            position,
            new Vector3(width, 3f, 0.25f),
            doorMat,
            hinge.transform
        );

        panel.transform.position =
            hinge.transform.position +
            new Vector3(width / 2f, 1.5f, 0);

        CreateTrigger(
            name + "_Trigger",
            position + new Vector3(0, 1.5f, 0),
            new Vector3(width + 2f, 3f, 4f),
            holder.transform,
            door
        );

        CreateIndicator(
            name + "_Indicator",
            position +
            new Vector3(
                width / 2f + 0.25f,
                2.2f,
                -0.3f
            ),
            holder.transform
        );
    }

    // =====================================================
    // X WALL DOOR
    // =====================================================

    private static void CreateDoorX(
        string name,
        Vector3 position,
        float width,
        Transform parent)
    {
        GameObject holder = new GameObject(name);

        holder.transform.position = position;
        holder.transform.SetParent(parent);

        CreateCube(
            name + "_Frame_A",
            position +
            new Vector3(
                0,
                1.5f,
                -width / 2f - 0.15f
            ),
            new Vector3(0.45f, 3f, 0.3f),
            frameMat,
            holder.transform
        );

        CreateCube(
            name + "_Frame_B",
            position +
            new Vector3(
                0,
                1.5f,
                width / 2f + 0.15f
            ),
            new Vector3(0.45f, 3f, 0.3f),
            frameMat,
            holder.transform
        );

        CreateCube(
            name + "_Frame_Top",
            position + new Vector3(0, 3.1f, 0),
            new Vector3(0.45f, 0.25f, width + 0.6f),
            frameMat,
            holder.transform
        );

        GameObject hinge =
            new GameObject(name + "_Hinge");

        hinge.transform.position =
            position +
            new Vector3(0, 0, -width / 2f);

        hinge.transform.SetParent(holder.transform);

        InteractiveDoor door =
            hinge.AddComponent<InteractiveDoor>();

        door.openAngle = 90f;
        door.openSpeed = 4f;

        GameObject panel = CreateCube(
            name + "_Panel",
            position,
            new Vector3(0.25f, 3f, width),
            doorMat,
            hinge.transform
        );

        panel.transform.position =
            hinge.transform.position +
            new Vector3(0, 1.5f, width / 2f);

        CreateTrigger(
            name + "_Trigger",
            position + new Vector3(0, 1.5f, 0),
            new Vector3(4f, 3f, width + 2f),
            holder.transform,
            door
        );

        CreateIndicator(
            name + "_Indicator",
            position +
            new Vector3(
                -0.3f,
                2.2f,
                width / 2f + 0.25f
            ),
            holder.transform
        );
    }

    // =====================================================
    // TRIGGER
    // =====================================================

    private static void CreateTrigger(
        string name,
        Vector3 position,
        Vector3 size,
        Transform parent,
        InteractiveDoor door)
    {
        GameObject trigger = new GameObject(name);

        trigger.transform.position = position;
        trigger.transform.SetParent(parent);

        BoxCollider collider =
            trigger.AddComponent<BoxCollider>();

        collider.isTrigger = true;
        collider.size = size;

        DoorTrigger triggerScript =
            trigger.AddComponent<DoorTrigger>();

        triggerScript.door = door;
    }

    // =====================================================
    // INDICATOR
    // =====================================================

    private static void CreateIndicator(
        string name,
        Vector3 position,
        Transform parent)
    {
        GameObject indicator = CreateCube(
            name,
            position,
            new Vector3(0.18f, 0.35f, 0.18f),
            indicatorMat,
            parent
        );

        Collider collider =
            indicator.GetComponent<Collider>();

        if (collider != null)
            DestroyImmediate(collider);
    }

    // =====================================================
    // MATERIALS
    // =====================================================

    private static void CreateMaterials()
    {
        EnsureMaterialFolder();

        doorMat = CreateMaterial(
            "Door_Red",
            new Color(0.42f, 0.025f, 0.025f),
            0.75f,
            0.55f
        );

        frameMat = CreateMaterial(
            "Door_Frame",
            new Color(0.035f, 0.04f, 0.05f),
            0.90f,
            0.65f
        );

        indicatorMat = CreateEmissiveMaterial(
            "Door_Indicator",
            new Color(0.0f, 0.9f, 1.0f)
        );
    }

    private static Material CreateMaterial(
        string name,
        Color color,
        float metallic,
        float smoothness)
    {
        string path =
            MaterialFolder + "/" + name + ".mat";

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

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);

        EditorUtility.SetDirty(material);

        return material;
    }

    private static Material CreateEmissiveMaterial(
        string name,
        Color color)
    {
        Material material =
            CreateMaterial(
                name,
                color,
                0.2f,
                0.8f
            );

        material.EnableKeyword("_EMISSION");

        material.SetColor(
            "_EmissionColor",
            color * 3f
        );

        EditorUtility.SetDirty(material);

        return material;
    }

    private static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "Materials"
            );
        }
    }

    // =====================================================
    // CUBE
    // =====================================================

    private static GameObject CreateCube(
        string name,
        Vector3 position,
        Vector3 scale,
        Material material,
        Transform parent)
    {
        GameObject obj =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.transform.SetParent(parent);

        Renderer renderer =
            obj.GetComponent<Renderer>();

        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return obj;
    }
}