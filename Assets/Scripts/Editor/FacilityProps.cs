using UnityEngine;
using UnityEditor;

public class FacilityProps : EditorWindow
{
    private const string MaterialFolder = "Assets/Materials";

    private static Material woodMat;
    private static Material darkWoodMat;
    private static Material metalMat;
    private static Material darkMetalMat;
    private static Material pipeMat;
    private static Material trashMat;
    private static Material cabinetMat;
    private static Material warningMat;
    private static Material barrelMat;
    private static Material crateMat;
    private static Material boxMat;

    [MenuItem("Facility Breach/Generate Facility Props")]
    public static void GenerateProps()
    {
        GameObject old = GameObject.Find("Facility_ExtraProps");

        if (old != null)
            DestroyImmediate(old);

        GameObject root = new GameObject("Facility_ExtraProps");

        CreateMaterials();

        // =====================================================
        // 1. WOODEN PALLETS
        // =====================================================

        CreatePallet(
            "Pallet_Entrance",
            new Vector3(-10f, 0.12f, -15.5f),
            15f,
            false,
            root.transform
        );

        CreatePallet(
            "Pallet_LeftWall",
            new Vector3(-19.2f, 1.0f, -7f),
            0f,
            true,
            root.transform
        );

        CreatePallet(
            "Pallet_UpperLeft",
            new Vector3(-17f, 0.12f, 22f),
            -20f,
            false,
            root.transform
        );

        CreatePallet(
            "Pallet_RightWall",
            new Vector3(19.2f, 1.0f, -7f),
            0f,
            true,
            root.transform
        );

        // =====================================================
        // 2. METAL SHELVING / RACKS
        // =====================================================

        CreateShelf(
            "Shelf_Room1",
            new Vector3(-17.2f, 1.4f, -14.5f),
            90f,
            root.transform
        );

        CreateShelf(
            "Shelf_Room2",
            new Vector3(6.7f, 1.4f, 7.8f),
            0f,
            root.transform
        );

        CreateShelf(
            "Shelf_Room3",
            new Vector3(16.8f, 1.4f, 21.8f),
            90f,
            root.transform
        );

        CreateShelf(
            "Shelf_UpperLeft",
            new Vector3(-18.5f, 1.4f, 15.5f),
            90f,
            root.transform
        );

        // =====================================================
        // 3. INDUSTRIAL PIPE BUNDLES
        // =====================================================

        CreatePipeBundle(
            "Pipes_LeftLower",
            new Vector3(-18.8f, 0.55f, -20f),
            0f,
            root.transform
        );

        CreatePipeBundle(
            "Pipes_RightLower",
            new Vector3(18.8f, 0.55f, -11f),
            0f,
            root.transform
        );

        CreatePipeBundle(
            "Pipes_UpperLeft",
            new Vector3(-18f, 0.55f, 26f),
            90f,
            root.transform
        );

        CreatePipeBundle(
            "Pipes_UpperRight",
            new Vector3(19.5f, 0.55f, 26f),
            0f,
            root.transform
        );

        // =====================================================
        // 4. WORKBENCHES
        // =====================================================

        CreateWorkbench(
            "Workbench_Room1",
            new Vector3(-13.5f, 0f, -13.5f),
            0f,
            true,
            root.transform
        );

        CreateWorkbench(
            "Workbench_Room3",
            new Vector3(12.5f, 0f, 20f),
            90f,
            true,
            root.transform
        );

        CreateWorkbench(
            "Workbench_UpperLeft",
            new Vector3(-12f, 0f, 25f),
            15f,
            false,
            root.transform
        );

        // =====================================================
        // 5. TRASH / DEBRIS
        // =====================================================

        CreateDebrisPile(
            "Debris_EntranceCorner",
            new Vector3(-19f, 0.15f, -28f),
            root.transform
        );

        CreateDebrisPile(
            "Debris_LeftMiddle",
            new Vector3(-19f, 0.15f, 4f),
            root.transform
        );

        CreateDebrisPile(
            "Debris_RightLower",
            new Vector3(19f, 0.15f, -15f),
            root.transform
        );

        CreateDebrisPile(
            "Debris_UpperLeft",
            new Vector3(-18.5f, 0.15f, 27.5f),
            root.transform
        );

        CreateDebrisPile(
            "Debris_UpperRight",
            new Vector3(19f, 0.15f, 27f),
            root.transform
        );

        // =====================================================
        // 6. ELECTRICAL CABINETS
        // =====================================================

        CreateElectricalCabinet(
            "Electrical_LeftLower",
            new Vector3(-20f, 1.15f, -16f),
            90f,
            root.transform
        );

        CreateElectricalCabinet(
            "Electrical_RightLower",
            new Vector3(20f, 1.15f, -5f),
            -90f,
            root.transform
        );

        CreateElectricalCabinet(
            "Electrical_UpperLeft",
            new Vector3(-20f, 1.15f, 11f),
            90f,
            root.transform
        );

        CreateElectricalCabinet(
            "Electrical_UpperRight",
            new Vector3(20f, 1.15f, 18f),
            -90f,
            root.transform
        );

        // =====================================================
        // 7. OIL DRUM GROUPS
        // =====================================================

        CreateDrumGroup(
            "DrumGroup_LowerRight",
            new Vector3(18f, 0f, -27f),
            root.transform
        );

        CreateDrumGroup(
            "DrumGroup_UpperLeft",
            new Vector3(-18f, 0f, 10f),
            root.transform
        );

        CreateDrumGroup(
            "DrumGroup_UpperRight",
            new Vector3(18f, 0f, 16f),
            root.transform
        );

        // =====================================================
        // 8. STACKED CRATES
        // =====================================================

        CreateCrateStack(
            "CrateStack_LeftLower",
            new Vector3(-17.5f, 0f, -21f),
            20f,
            2,
            root.transform
        );

        CreateCrateStack(
            "CrateStack_RightMiddle",
            new Vector3(18f, 0f, 7f),
            -15f,
            3,
            root.transform
        );

        CreateCrateStack(
            "CrateStack_UpperLeft",
            new Vector3(-17f, 0f, 16f),
            10f,
            2,
            root.transform
        );

        CreateCrateStack(
            "CrateStack_UpperRight",
            new Vector3(18f, 0f, 25f),
            -20f,
            2,
            root.transform
        );

        Selection.activeGameObject = root;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Facility props generated with brighter visible industrial colors."
        );
    }

    // =====================================================
    // PALLET
    // =====================================================

    private static void CreatePallet(
        string name,
        Vector3 position,
        float rotationY,
        bool leaning,
        Transform parent)
    {
        GameObject pallet = new GameObject(name);

        pallet.transform.position = position;
        pallet.transform.SetParent(parent);

        if (leaning)
        {
            pallet.transform.rotation =
                Quaternion.Euler(0f, rotationY, 78f);
        }
        else
        {
            pallet.transform.rotation =
                Quaternion.Euler(0f, rotationY, 0f);
        }

        float[] xPositions =
        {
            -0.85f,
            -0.42f,
            0f,
            0.42f,
            0.85f
        };

        foreach (float x in xPositions)
        {
            CreateVisualCube(
                name + "_Top_" + x,
                pallet.transform,
                new Vector3(x, 0.10f, 0f),
                new Vector3(0.26f, 0.14f, 1.7f),
                woodMat
            );
        }

        CreateVisualCube(
            name + "_SupportLeft",
            pallet.transform,
            new Vector3(-0.75f, -0.03f, 0f),
            new Vector3(0.20f, 0.18f, 1.75f),
            darkWoodMat
        );

        CreateVisualCube(
            name + "_SupportCenter",
            pallet.transform,
            new Vector3(0f, -0.03f, 0f),
            new Vector3(0.20f, 0.18f, 1.75f),
            darkWoodMat
        );

        CreateVisualCube(
            name + "_SupportRight",
            pallet.transform,
            new Vector3(0.75f, -0.03f, 0f),
            new Vector3(0.20f, 0.18f, 1.75f),
            darkWoodMat
        );
    }

    // =====================================================
    // METAL SHELF
    // =====================================================

    private static void CreateShelf(
        string name,
        Vector3 position,
        float rotationY,
        Transform parent)
    {
        GameObject shelf = new GameObject(name);

        shelf.transform.position = position;
        shelf.transform.rotation =
            Quaternion.Euler(0f, rotationY, 0f);

        shelf.transform.SetParent(parent);

        float width = 2.8f;
        float height = 2.8f;
        float depth = 0.7f;

        CreateVisualCube(
            name + "_Leg1",
            shelf.transform,
            new Vector3(-1.3f, 0f, -0.3f),
            new Vector3(0.10f, height, 0.10f),
            metalMat
        );

        CreateVisualCube(
            name + "_Leg2",
            shelf.transform,
            new Vector3(1.3f, 0f, -0.3f),
            new Vector3(0.10f, height, 0.10f),
            metalMat
        );

        CreateVisualCube(
            name + "_Leg3",
            shelf.transform,
            new Vector3(-1.3f, 0f, 0.3f),
            new Vector3(0.10f, height, 0.10f),
            metalMat
        );

        CreateVisualCube(
            name + "_Leg4",
            shelf.transform,
            new Vector3(1.3f, 0f, 0.3f),
            new Vector3(0.10f, height, 0.10f),
            metalMat
        );

        float[] levels =
        {
            -1.1f,
            -0.25f,
            0.60f,
            1.30f
        };

        foreach (float y in levels)
        {
            CreateVisualCube(
                name + "_Shelf_" + y,
                shelf.transform,
                new Vector3(0f, y, 0f),
                new Vector3(width, 0.10f, depth),
                darkMetalMat
            );
        }

        CreateVisualCube(
            name + "_Box1",
            shelf.transform,
            new Vector3(-0.75f, -0.70f, 0f),
            new Vector3(0.65f, 0.65f, 0.55f),
            boxMat
        );

        CreateVisualCube(
            name + "_Box2",
            shelf.transform,
            new Vector3(0.65f, 0.10f, 0f),
            new Vector3(0.75f, 0.60f, 0.55f),
            boxMat
        );

        CreateVisualCube(
            name + "_Box3",
            shelf.transform,
            new Vector3(-0.55f, 0.95f, 0f),
            new Vector3(0.55f, 0.55f, 0.55f),
            boxMat
        );
    }

    // =====================================================
    // PIPE BUNDLE
    // =====================================================

    private static void CreatePipeBundle(
        string name,
        Vector3 position,
        float rotationY,
        Transform parent)
    {
        GameObject pipes = new GameObject(name);

        pipes.transform.position = position;
        pipes.transform.rotation =
            Quaternion.Euler(0f, rotationY, 90f);

        pipes.transform.SetParent(parent);

        Vector3[] offsets =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0.34f, 0f),
            new Vector3(0f, 0f, 0.34f),
            new Vector3(0f, 0.34f, 0.34f),
            new Vector3(0f, 0.68f, 0.17f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject pipe =
                GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            pipe.name = name + "_Pipe_" + i;

            pipe.transform.SetParent(pipes.transform);
            pipe.transform.localPosition = offsets[i];
            pipe.transform.localRotation = Quaternion.identity;

            pipe.transform.localScale =
                new Vector3(0.14f, 1.8f, 0.14f);

            Renderer renderer =
                pipe.GetComponent<Renderer>();

            if (renderer != null)
                renderer.sharedMaterial = pipeMat;

            RemoveCollider(pipe);
        }

        CreateVisualCube(
            name + "_Strap1",
            pipes.transform,
            new Vector3(0f, -1.1f, 0.17f),
            new Vector3(0.65f, 0.08f, 0.65f),
            darkMetalMat
        );

        CreateVisualCube(
            name + "_Strap2",
            pipes.transform,
            new Vector3(0f, 1.1f, 0.17f),
            new Vector3(0.65f, 0.08f, 0.65f),
            darkMetalMat
        );
    }

    // =====================================================
    // WORKBENCH
    // =====================================================

    private static void CreateWorkbench(
        string name,
        Vector3 position,
        float rotationY,
        bool broken,
        Transform parent)
    {
        GameObject bench = new GameObject(name);

        bench.transform.position = position;
        bench.transform.rotation =
            Quaternion.Euler(0f, rotationY, 0f);

        bench.transform.SetParent(parent);

        CreateVisualCube(
            name + "_Top",
            bench.transform,
            new Vector3(0f, 1.05f, 0f),
            new Vector3(2.7f, 0.18f, 1.0f),
            darkWoodMat
        );

        CreateVisualCube(
            name + "_BackRail",
            bench.transform,
            new Vector3(0f, 1.42f, 0.43f),
            new Vector3(2.6f, 0.55f, 0.10f),
            metalMat
        );

        CreateVisualCube(
            name + "_Leg1",
            bench.transform,
            new Vector3(-1.05f, 0.50f, -0.32f),
            new Vector3(0.16f, 1.0f, 0.16f),
            metalMat
        );

        CreateVisualCube(
            name + "_Leg2",
            bench.transform,
            new Vector3(1.05f, 0.50f, -0.32f),
            new Vector3(0.16f, 1.0f, 0.16f),
            metalMat
        );

        CreateVisualCube(
            name + "_Leg3",
            bench.transform,
            new Vector3(-1.05f, 0.50f, 0.32f),
            new Vector3(0.16f, 1.0f, 0.16f),
            metalMat
        );

        if (!broken)
        {
            CreateVisualCube(
                name + "_Leg4",
                bench.transform,
                new Vector3(1.05f, 0.50f, 0.32f),
                new Vector3(0.16f, 1.0f, 0.16f),
                metalMat
            );
        }
        else
        {
            GameObject brokenLeg = CreateVisualCube(
                name + "_BrokenLeg",
                bench.transform,
                new Vector3(1.2f, 0.12f, 0.55f),
                new Vector3(0.16f, 0.85f, 0.16f),
                metalMat
            );

            brokenLeg.transform.localRotation =
                Quaternion.Euler(0f, 0f, 72f);
        }

        CreateVisualCube(
            name + "_Tool",
            bench.transform,
            new Vector3(-0.55f, 1.20f, 0f),
            new Vector3(0.65f, 0.12f, 0.16f),
            warningMat
        );
    }

    // =====================================================
    // DEBRIS
    // =====================================================

    private static void CreateDebrisPile(
        string name,
        Vector3 position,
        Transform parent)
    {
        GameObject debris = new GameObject(name);

        debris.transform.position = position;
        debris.transform.SetParent(parent);

        CreateDebrisPiece(
            name + "_01",
            debris.transform,
            new Vector3(0f, 0f, 0f),
            new Vector3(0.7f, 0.18f, 0.45f),
            15f
        );

        CreateDebrisPiece(
            name + "_02",
            debris.transform,
            new Vector3(0.4f, 0.05f, 0.25f),
            new Vector3(0.5f, 0.25f, 0.5f),
            -25f
        );

        CreateDebrisPiece(
            name + "_03",
            debris.transform,
            new Vector3(-0.35f, 0.03f, 0.3f),
            new Vector3(0.4f, 0.15f, 0.65f),
            35f
        );

        CreateDebrisPiece(
            name + "_04",
            debris.transform,
            new Vector3(0.15f, 0.10f, -0.35f),
            new Vector3(0.35f, 0.30f, 0.35f),
            5f
        );

        GameObject bag =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        bag.name = name + "_TrashBag";
        bag.transform.SetParent(debris.transform);

        bag.transform.localPosition =
            new Vector3(-0.5f, 0.22f, -0.25f);

        bag.transform.localScale =
            new Vector3(0.55f, 0.45f, 0.55f);

        Renderer renderer =
            bag.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = trashMat;

        RemoveCollider(bag);
    }

    private static void CreateDebrisPiece(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        float rotationY)
    {
        GameObject piece = CreateVisualCube(
            name,
            parent,
            position,
            scale,
            trashMat
        );

        piece.transform.localRotation =
            Quaternion.Euler(
                Random.Range(-10f, 10f),
                rotationY,
                Random.Range(-10f, 10f)
            );
    }

    // =====================================================
    // ELECTRICAL CABINET
    // =====================================================

    private static void CreateElectricalCabinet(
        string name,
        Vector3 position,
        float rotationY,
        Transform parent)
    {
        GameObject cabinet = new GameObject(name);

        cabinet.transform.position = position;
        cabinet.transform.rotation =
            Quaternion.Euler(0f, rotationY, 0f);

        cabinet.transform.SetParent(parent);

        CreateVisualCube(
            name + "_Body",
            cabinet.transform,
            Vector3.zero,
            new Vector3(1.2f, 2.3f, 0.45f),
            cabinetMat
        );

        CreateVisualCube(
            name + "_Door",
            cabinet.transform,
            new Vector3(0f, 0f, -0.245f),
            new Vector3(1.02f, 2.05f, 0.05f),
            darkMetalMat
        );

        CreateVisualCube(
            name + "_Warning",
            cabinet.transform,
            new Vector3(0f, 0.35f, -0.285f),
            new Vector3(0.42f, 0.42f, 0.03f),
            warningMat
        );

        CreateVisualCube(
            name + "_Handle",
            cabinet.transform,
            new Vector3(0.38f, 0f, -0.31f),
            new Vector3(0.08f, 0.38f, 0.08f),
            metalMat
        );

        CreateIndicator(
            name + "_RedLight",
            cabinet.transform,
            new Vector3(-0.28f, 0.78f, -0.31f),
            new Color(1f, 0.08f, 0.03f)
        );

        CreateIndicator(
            name + "_GreenLight",
            cabinet.transform,
            new Vector3(0f, 0.78f, -0.31f),
            new Color(0.05f, 0.9f, 0.15f)
        );
    }

    private static void CreateIndicator(
        string name,
        Transform parent,
        Vector3 position,
        Color color)
    {
        GameObject light =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        light.name = name;
        light.transform.SetParent(parent);
        light.transform.localPosition = position;

        light.transform.localScale =
            new Vector3(0.11f, 0.11f, 0.06f);

        Renderer renderer =
            light.GetComponent<Renderer>();

        if (renderer != null)
        {
            Material mat =
                new Material(Shader.Find("Standard"));

            mat.color = color;

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            renderer.sharedMaterial = mat;
        }

        RemoveCollider(light);
    }

    // =====================================================
    // OIL DRUM GROUP
    // =====================================================

    private static void CreateDrumGroup(
        string name,
        Vector3 position,
        Transform parent)
    {
        GameObject group = new GameObject(name);

        group.transform.position = position;
        group.transform.SetParent(parent);

        CreateDrum(
            name + "_Drum1",
            group.transform,
            new Vector3(0f, 0.75f, 0f)
        );

        CreateDrum(
            name + "_Drum2",
            group.transform,
            new Vector3(1.05f, 0.75f, 0.20f)
        );

        CreateDrum(
            name + "_Drum3",
            group.transform,
            new Vector3(0.45f, 0.75f, 0.95f)
        );
    }

    private static void CreateDrum(
        string name,
        Transform parent,
        Vector3 position)
    {
        GameObject drum =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        drum.name = name;
        drum.transform.SetParent(parent);
        drum.transform.localPosition = position;

        drum.transform.localScale =
            new Vector3(0.62f, 0.75f, 0.62f);

        Renderer renderer =
            drum.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = barrelMat;

        RemoveCollider(drum);

        CreateCylinderDetail(
            name + "_TopBand",
            parent,
            position + new Vector3(0f, 0.62f, 0f),
            0.66f
        );

        CreateCylinderDetail(
            name + "_MiddleBand",
            parent,
            position,
            0.65f
        );

        CreateCylinderDetail(
            name + "_BottomBand",
            parent,
            position + new Vector3(0f, -0.62f, 0f),
            0.66f
        );
    }

    private static void CreateCylinderDetail(
        string name,
        Transform parent,
        Vector3 position,
        float radius)
    {
        GameObject ring =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        ring.name = name;
        ring.transform.SetParent(parent);
        ring.transform.localPosition = position;

        ring.transform.localScale =
            new Vector3(radius, 0.035f, radius);

        Renderer renderer =
            ring.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = metalMat;

        RemoveCollider(ring);
    }

    // =====================================================
    // STACKED CRATES
    // =====================================================

    private static void CreateCrateStack(
        string name,
        Vector3 position,
        float rotationY,
        int height,
        Transform parent)
    {
        GameObject stack = new GameObject(name);

        stack.transform.position = position;
        stack.transform.rotation =
            Quaternion.Euler(0f, rotationY, 0f);

        stack.transform.SetParent(parent);

        for (int i = 0; i < height; i++)
        {
            float y = 0.52f + (i * 1.02f);
            float xOffset =
                (i % 2 == 0) ? 0f : 0.08f;

            CreateDetailedCrate(
                name + "_Crate_" + i,
                stack.transform,
                new Vector3(xOffset, y, 0f),
                1f
            );
        }

        if (height >= 3)
        {
            CreateDetailedCrate(
                name + "_SideCrate",
                stack.transform,
                new Vector3(1.05f, 0.52f, 0.15f),
                1f
            );
        }
    }

    private static void CreateDetailedCrate(
        string name,
        Transform parent,
        Vector3 position,
        float size)
    {
        CreateVisualCube(
            name + "_Body",
            parent,
            position,
            new Vector3(size, size, size),
            crateMat
        );

        float half = size * 0.46f;

        CreateVisualCube(
            name + "_FrontTop",
            parent,
            position + new Vector3(0f, half, -0.51f),
            new Vector3(size, 0.10f, 0.08f),
            darkWoodMat
        );

        CreateVisualCube(
            name + "_FrontBottom",
            parent,
            position + new Vector3(0f, -half, -0.51f),
            new Vector3(size, 0.10f, 0.08f),
            darkWoodMat
        );

        CreateVisualCube(
            name + "_FrontLeft",
            parent,
            position + new Vector3(-half, 0f, -0.51f),
            new Vector3(0.10f, size, 0.08f),
            darkWoodMat
        );

        CreateVisualCube(
            name + "_FrontRight",
            parent,
            position + new Vector3(half, 0f, -0.51f),
            new Vector3(0.10f, size, 0.08f),
            darkWoodMat
        );

        GameObject diagonal1 = CreateVisualCube(
            name + "_Brace1",
            parent,
            position + new Vector3(0f, 0f, -0.53f),
            new Vector3(1.15f, 0.09f, 0.07f),
            darkWoodMat
        );

        diagonal1.transform.localRotation =
            Quaternion.Euler(0f, 0f, 45f);

        GameObject diagonal2 = CreateVisualCube(
            name + "_Brace2",
            parent,
            position + new Vector3(0f, 0f, -0.54f),
            new Vector3(1.15f, 0.09f, 0.07f),
            darkWoodMat
        );

        diagonal2.transform.localRotation =
            Quaternion.Euler(0f, 0f, -45f);
    }

    // =====================================================
    // VISUAL CUBE
    // =====================================================

    private static GameObject CreateVisualCube(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 scale,
        Material material)
    {
        GameObject obj =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        obj.name = name;
        obj.transform.SetParent(parent);

        obj.transform.localPosition =
            localPosition;

        obj.transform.localRotation =
            Quaternion.identity;

        obj.transform.localScale =
            scale;

        Renderer renderer =
            obj.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = material;

        RemoveCollider(obj);

        return obj;
    }

    private static void RemoveCollider(GameObject obj)
    {
        Collider collider =
            obj.GetComponent<Collider>();

        if (collider != null)
            DestroyImmediate(collider);
    }

    // =====================================================
    // BRIGHTER / MORE VISIBLE PROP MATERIALS
    // =====================================================

    private static void CreateMaterials()
    {
        EnsureMaterialFolder();

        // LIGHT WARM WOOD - PALLETS
        woodMat = CreateMaterial(
            "Props_PalletWood",
            new Color(0.72f, 0.48f, 0.22f),
            0.01f,
            0.12f
        );

        // DARKER ORANGE WOOD - WORKBENCH DETAILS
        darkWoodMat = CreateMaterial(
            "Props_DarkWood",
            new Color(0.48f, 0.25f, 0.09f),
            0.01f,
            0.10f
        );

        // LIGHT STEEL - SHELVES AND DETAILS
        metalMat = CreateMaterial(
            "Props_Metal",
            new Color(0.48f, 0.58f, 0.64f),
            0.55f,
            0.22f
        );

        // BLUE-GREY DARK METAL
        darkMetalMat = CreateMaterial(
            "Props_DarkMetal",
            new Color(0.28f, 0.35f, 0.40f),
            0.55f,
            0.18f
        );

        // GREEN INDUSTRIAL PIPES
        pipeMat = CreateMaterial(
            "Props_Pipe",
            new Color(0.24f, 0.58f, 0.38f),
            0.38f,
            0.16f
        );

        // VISIBLE GREY DEBRIS
        trashMat = CreateMaterial(
            "Props_Trash",
            new Color(0.32f, 0.32f, 0.30f),
            0.02f,
            0.08f
        );

        // OLIVE ELECTRICAL CABINETS
        cabinetMat = CreateMaterial(
            "Props_ElectricalCabinet",
            new Color(0.42f, 0.55f, 0.28f),
            0.35f,
            0.17f
        );

        // YELLOW WARNING DETAILS
        warningMat = CreateMaterial(
            "Props_Warning",
            new Color(0.92f, 0.65f, 0.08f),
            0.05f,
            0.15f
        );

        // BLUE OIL DRUMS
        barrelMat = CreateMaterial(
            "Props_OilDrum",
            new Color(0.12f, 0.42f, 0.68f),
            0.45f,
            0.20f
        );

        // ORANGE-BROWN STACKED CRATES
        crateMat = CreateMaterial(
            "Props_CrateWood",
            new Color(0.68f, 0.36f, 0.10f),
            0.01f,
            0.10f
        );

        // LIGHT TAN SHELF BOXES
        boxMat = CreateMaterial(
            "Props_ShelfBox",
            new Color(0.72f, 0.60f, 0.36f),
            0.01f,
            0.08f
        );
    }

    // =====================================================
    // MATERIAL CREATOR
    // =====================================================

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
    // MATERIAL FOLDER
    // =====================================================

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
}