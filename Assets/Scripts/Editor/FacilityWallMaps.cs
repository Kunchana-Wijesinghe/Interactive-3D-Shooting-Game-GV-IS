using UnityEngine;
using UnityEditor;

public class FacilityWallMaps : EditorWindow
{
    private const string ROOT_NAME = "Facility_WallMaps";
    private const string MATERIAL_FOLDER = "Assets/Materials";

    private static Material frameMat;
    private static Material paperMat;
    private static Material paperDarkMat;
    private static Material mapLineMat;
    private static Material routeMat;
    private static Material markerMat;

    [MenuItem("Facility Breach/Generate Wall Maps")]
    public static void GenerateWallMaps()
    {
        // =====================================================
        // REMOVE OLD MAPS
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

        frameMat = CreateMaterial(
            "WallMap_Frame",
            new Color(0.035f, 0.04f, 0.045f)
        );

        // Old faded map paper
        paperMat = CreateMaterial(
            "WallMap_Paper",
            new Color(0.52f, 0.49f, 0.38f)
        );

        paperDarkMat = CreateMaterial(
            "WallMap_PaperDark",
            new Color(0.34f, 0.35f, 0.30f)
        );

        mapLineMat = CreateMaterial(
            "WallMap_Lines",
            new Color(0.08f, 0.10f, 0.09f)
        );

        routeMat = CreateMaterial(
            "WallMap_Route",
            new Color(0.60f, 0.42f, 0.10f)
        );

        markerMat = CreateMaterial(
            "WallMap_Marker",
            new Color(0.65f, 0.05f, 0.035f)
        );

        // =====================================================
        // MAP 1 - ENTRANCE AREA
        // =====================================================

        CreateMapOnZWall(
            root.transform,
            "Map_Entrance",
            new Vector3(-6f, 2.0f, -29.77f),
            true,
            0
        );

        // =====================================================
        // MAP 2 - STORAGE AREA
        // =====================================================

        CreateMapOnXWall(
            root.transform,
            "Map_Storage",
            new Vector3(-20.77f, 2.0f, -12f),
            true,
            1
        );

        // =====================================================
        // MAP 3 - CENTRAL FACILITY
        // =====================================================

        CreateMapOnZWall(
            root.transform,
            "Map_Central",
            new Vector3(5f, 2.0f, 10.18f),
            false,
            2
        );

        // =====================================================
        // MAP 4 - EAST CORRIDOR
        // =====================================================

        CreateMapOnXWall(
            root.transform,
            "Map_EastCorridor",
            new Vector3(20.77f, 2.0f, 5f),
            false,
            3
        );

        // =====================================================
        // MAP 5 - MACHINERY AREA
        // =====================================================

        CreateMapOnXWall(
            root.transform,
            "Map_Machinery",
            new Vector3(10.18f, 2.0f, 16f),
            false,
            4
        );

        // =====================================================
        // MAP 6 - EXIT AREA
        // =====================================================

        CreateMapOnZWall(
            root.transform,
            "Map_Exit",
            new Vector3(6f, 2.0f, 29.77f),
            false,
            5
        );

        // =====================================================
        // FINISH
        // =====================================================

        Undo.RegisterCreatedObjectUndo(
            root,
            "Generate Facility Wall Maps"
        );

        Selection.activeGameObject = root;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "6 decorative facility wall maps generated."
        );
    }

    // =========================================================
    // MAP ON Z WALL
    // =========================================================

    private static void CreateMapOnZWall(
        Transform parent,
        string mapName,
        Vector3 position,
        bool facePositiveZ,
        int layout)
    {
        GameObject holder = new GameObject(mapName);

        holder.transform.SetParent(parent);
        holder.transform.position = position;

        holder.transform.rotation =
            Quaternion.Euler(
                0f,
                facePositiveZ ? 180f : 0f,
                0f
            );

        CreateMap(holder.transform, layout);
    }

    // =========================================================
    // MAP ON X WALL
    // =========================================================

    private static void CreateMapOnXWall(
        Transform parent,
        string mapName,
        Vector3 position,
        bool facePositiveX,
        int layout)
    {
        GameObject holder = new GameObject(mapName);

        holder.transform.SetParent(parent);
        holder.transform.position = position;

        holder.transform.rotation =
            Quaternion.Euler(
                0f,
                facePositiveX ? -90f : 90f,
                0f
            );

        CreateMap(holder.transform, layout);
    }

    // =========================================================
    // CREATE COMPLETE MAP
    // =========================================================

    private static void CreateMap(
        Transform holder,
        int layout)
    {
        float width = 2.3f;
        float height = 1.65f;

        // =====================================================
        // BLACK FRAME
        // =====================================================

        CreateBox(
            holder,
            "Metal_Frame",
            Vector3.zero,
            new Vector3(
                width + 0.18f,
                height + 0.18f,
                0.10f
            ),
            frameMat
        );

        // =====================================================
        // OLD MAP PAPER
        // =====================================================

        CreateBox(
            holder,
            "Map_Surface",
            new Vector3(
                0f,
                0f,
                -0.065f
            ),
            new Vector3(
                width,
                height,
                0.045f
            ),
            layout % 2 == 0
                ? paperMat
                : paperDarkMat
        );

        // =====================================================
        // MAP BORDER
        // =====================================================

        float z = -0.095f;

        CreateBox(
            holder,
            "MapBorder_Top",
            new Vector3(0f, 0.68f, z),
            new Vector3(2.0f, 0.035f, 0.018f),
            mapLineMat
        );

        CreateBox(
            holder,
            "MapBorder_Bottom",
            new Vector3(0f, -0.68f, z),
            new Vector3(2.0f, 0.035f, 0.018f),
            mapLineMat
        );

        CreateBox(
            holder,
            "MapBorder_Left",
            new Vector3(-1.0f, 0f, z),
            new Vector3(0.035f, 1.38f, 0.018f),
            mapLineMat
        );

        CreateBox(
            holder,
            "MapBorder_Right",
            new Vector3(1.0f, 0f, z),
            new Vector3(0.035f, 1.38f, 0.018f),
            mapLineMat
        );

        // =====================================================
        // DIFFERENT MAP LAYOUTS
        // =====================================================

        switch (layout)
        {
            case 0:
                LayoutA(holder);
                break;

            case 1:
                LayoutB(holder);
                break;

            case 2:
                LayoutC(holder);
                break;

            case 3:
                LayoutD(holder);
                break;

            case 4:
                LayoutE(holder);
                break;

            default:
                LayoutF(holder);
                break;
        }

        // =====================================================
        // RED "YOU ARE HERE" MARKER
        // =====================================================

        Vector2 markerPosition =
            GetMarkerPosition(layout);

        GameObject marker =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        marker.name = "You_Are_Here";

        marker.transform.SetParent(holder);

        marker.transform.localPosition =
            new Vector3(
                markerPosition.x,
                markerPosition.y,
                -0.125f
            );

        marker.transform.localScale =
            new Vector3(
                0.13f,
                0.13f,
                0.035f
            );

        SetMaterial(
            marker,
            markerMat
        );

        RemoveCollider(marker);
    }

    // =========================================================
    // MAP LAYOUT A
    // =========================================================

    private static void LayoutA(Transform parent)
    {
        MapLine(parent, -0.55f, 0.30f, 0.7f, 0.07f);
        MapLine(parent, 0.10f, 0.30f, 0.07f, 0.75f);
        MapLine(parent, 0.45f, -0.05f, 0.75f, 0.07f);
        MapLine(parent, -0.45f, -0.40f, 0.07f, 0.55f);

        RouteLine(parent, -0.20f, 0.52f, 1.20f, 0.045f);
    }

    // =========================================================
    // MAP LAYOUT B
    // =========================================================

    private static void LayoutB(Transform parent)
    {
        MapLine(parent, -0.50f, 0.42f, 0.80f, 0.07f);
        MapLine(parent, -0.10f, 0.05f, 0.07f, 0.80f);
        MapLine(parent, 0.40f, 0.05f, 0.65f, 0.07f);
        MapLine(parent, 0.65f, -0.35f, 0.07f, 0.60f);

        RouteLine(parent, 0.20f, -0.52f, 1.30f, 0.045f);
    }

    // =========================================================
    // MAP LAYOUT C
    // =========================================================

    private static void LayoutC(Transform parent)
    {
        MapLine(parent, 0f, 0.42f, 1.45f, 0.07f);
        MapLine(parent, -0.55f, 0f, 0.07f, 0.80f);
        MapLine(parent, 0.10f, -0.10f, 0.07f, 0.70f);
        MapLine(parent, 0.55f, -0.40f, 0.65f, 0.07f);

        RouteLine(parent, 0.52f, 0.15f, 0.045f, 0.80f);
    }

    // =========================================================
    // MAP LAYOUT D
    // =========================================================

    private static void LayoutD(Transform parent)
    {
        MapLine(parent, -0.45f, 0.10f, 0.07f, 1.0f);
        MapLine(parent, 0.05f, 0.48f, 0.95f, 0.07f);
        MapLine(parent, 0.45f, 0.05f, 0.07f, 0.80f);
        MapLine(parent, 0.05f, -0.35f, 0.80f, 0.07f);

        RouteLine(parent, -0.10f, 0f, 0.045f, 0.90f);
    }

    // =========================================================
    // MAP LAYOUT E
    // =========================================================

    private static void LayoutE(Transform parent)
    {
        MapLine(parent, -0.55f, 0.45f, 0.65f, 0.07f);
        MapLine(parent, -0.25f, 0.05f, 0.07f, 0.80f);
        MapLine(parent, 0.35f, 0.05f, 0.90f, 0.07f);
        MapLine(parent, 0.65f, -0.35f, 0.07f, 0.55f);

        RouteLine(parent, 0.20f, -0.48f, 1.10f, 0.045f);
    }

    // =========================================================
    // MAP LAYOUT F
    // =========================================================

    private static void LayoutF(Transform parent)
    {
        MapLine(parent, 0f, 0.48f, 1.50f, 0.07f);
        MapLine(parent, -0.60f, 0f, 0.07f, 0.90f);
        MapLine(parent, 0f, -0.05f, 0.07f, 0.75f);
        MapLine(parent, 0.55f, -0.38f, 0.70f, 0.07f);

        RouteLine(parent, 0.55f, 0.12f, 0.045f, 0.75f);
    }

    // =========================================================
    // MAP LINE
    // =========================================================

    private static void MapLine(
        Transform parent,
        float x,
        float y,
        float width,
        float height)
    {
        CreateBox(
            parent,
            "Map_Line",
            new Vector3(
                x,
                y,
                -0.105f
            ),
            new Vector3(
                width,
                height,
                0.015f
            ),
            mapLineMat
        );
    }

    // =========================================================
    // ROUTE LINE
    // =========================================================

    private static void RouteLine(
        Transform parent,
        float x,
        float y,
        float width,
        float height)
    {
        CreateBox(
            parent,
            "Marked_Route",
            new Vector3(
                x,
                y,
                -0.112f
            ),
            new Vector3(
                width,
                height,
                0.018f
            ),
            routeMat
        );
    }

    // =========================================================
    // MARKER POSITIONS
    // =========================================================

    private static Vector2 GetMarkerPosition(int layout)
    {
        switch (layout)
        {
            case 0:
                return new Vector2(-0.65f, -0.48f);

            case 1:
                return new Vector2(0.55f, 0.45f);

            case 2:
                return new Vector2(0.15f, -0.40f);

            case 3:
                return new Vector2(-0.55f, 0.15f);

            case 4:
                return new Vector2(0.60f, -0.42f);

            default:
                return new Vector2(-0.50f, 0.45f);
        }
    }

    // =========================================================
    // GENERIC BOX
    // =========================================================

    private static GameObject CreateBox(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject obj =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        obj.name = objectName;

        obj.transform.SetParent(parent);

        obj.transform.localPosition =
            localPosition;

        obj.transform.localRotation =
            Quaternion.identity;

        obj.transform.localScale =
            localScale;

        SetMaterial(
            obj,
            material
        );

        RemoveCollider(obj);

        return obj;
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
    // CREATE MATERIAL
    // =========================================================

    private static Material CreateMaterial(
        string materialName,
        Color color)
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

            material.name =
                materialName;

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

        if (materialName.Contains("Frame"))
        {
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat(
                    "_Metallic",
                    0.45f
                );
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat(
                    "_Smoothness",
                    0.20f
                );
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat(
                    "_Glossiness",
                    0.20f
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