using UnityEngine;
using UnityEditor;

public class FacilityBarricades : EditorWindow
{
    private const string MaterialFolder = "Assets/Materials";

    private static Material barricadeMat;
    private static Material hazardMat;
    private static Material outlineMat;
    private static Material crateMat;
    private static Material barrelMat;
    private static Material boxMat;
    private static Material metalDetailMat;

    // NEW CLUTTER DETAIL MATERIALS
    private static Material woodSlatMat;
    private static Material tapeMat;
    private static Material barrelBandMat;
    private static Material caseDetailMat;

    [MenuItem("Facility Breach/Generate Pushable Barricades")]
    public static void GenerateBarricades()
    {
        GameObject old = GameObject.Find("Facility_Barricades");

        if (old != null)
            DestroyImmediate(old);

        GameObject root = new GameObject("Facility_Barricades");

        CreateMaterials();

        // =====================================================
        // MAIN PUSHABLE BARRICADES - UNCHANGED
        // =====================================================

        CreateBarricade(
            "Barricade_Entrance",
            new Vector3(-7f, 0.65f, -25f),
            15f,
            root.transform
        );

        CreateBarricade(
            "Barricade_LowerRoute",
            new Vector3(11f, 0.65f, -18f),
            -20f,
            root.transform
        );

        CreateBarricade(
            "Barricade_Room1Area",
            new Vector3(-12f, 0.65f, -3.5f),
            25f,
            root.transform
        );

        CreateBarricade(
            "Barricade_Middle",
            new Vector3(13f, 0.65f, 7f),
            -15f,
            root.transform
        );

        CreateBarricade(
            "Barricade_UpperLeft",
            new Vector3(-11f, 0.65f, 14f),
            20f,
            root.transform
        );

        CreateBarricade(
            "Barricade_ExitArea",
            new Vector3(7f, 0.65f, 25f),
            -25f,
            root.transform
        );

        // =====================================================
        // ENTRANCE CLUTTER
        // =====================================================

        CreateCrate(
            "Crate_Entrance_01",
            new Vector3(-9.5f, 0.6f, -27f),
            15f,
            1.2f,
            root.transform
        );

        CreateSmallBox(
            "Box_Entrance_01",
            new Vector3(-15f, 0.4f, -26f),
            -12f,
            root.transform
        );

        CreateBarrel(
            "Barrel_Entrance_01",
            new Vector3(-16.5f, 0.75f, -24f),
            root.transform
        );

        // =====================================================
        // ROOM 1 CLUTTER
        // =====================================================

        CreateCrate(
            "Crate_Room1_Right",
            new Vector3(-6.3f, 0.55f, -10f),
            25f,
            1.1f,
            root.transform
        );

        CreateSmallBox(
            "Box_Room1_Top",
            new Vector3(-15f, 0.4f, -4.7f),
            18f,
            root.transform
        );

        CreateBarrel(
            "Barrel_Room1_01",
            new Vector3(-10.5f, 0.75f, -9f),
            root.transform
        );

        // =====================================================
        // ROOM 2 CLUTTER
        // =====================================================

        CreateCrate(
            "Crate_Room2_Bottom",
            new Vector3(3.2f, 0.55f, -4f),
            -20f,
            1.1f,
            root.transform
        );

        CreateSmallBox(
            "Box_Room2_Bottom",
            new Vector3(-1.5f, 0.4f, -4f),
            12f,
            root.transform
        );

        CreateBarrel(
            "Barrel_Room2_Left",
            new Vector3(-9f, 0.75f, 5.5f),
            root.transform
        );

        CreateCrate(
            "Crate_Room2_Right",
            new Vector3(11f, 0.55f, 2.5f),
            15f,
            1.1f,
            root.transform
        );

        CreateSmallBox(
            "Box_Room2_Top",
            new Vector3(3.5f, 0.4f, 12f),
            -20f,
            root.transform
        );

        CreateBarrel(
            "Barrel_Room2_Top",
            new Vector3(-1f, 0.75f, 12.5f),
            root.transform
        );

        // =====================================================
        // ROOM 3 CLUTTER
        // =====================================================

        CreateCrate(
            "Crate_Room3_Left",
            new Vector3(8f, 0.55f, 20.5f),
            20f,
            1.1f,
            root.transform
        );

        CreateBarrel(
            "Barrel_Room3_Bottom",
            new Vector3(15.8f, 0.75f, 10f),
            root.transform
        );

        CreateSmallBox(
            "Box_Room3_Bottom",
            new Vector3(12f, 0.4f, 10f),
            -15f,
            root.transform
        );

        CreateCrate(
            "Crate_Room3_Top",
            new Vector3(16f, 0.55f, 26f),
            -18f,
            1.1f,
            root.transform
        );

        // =====================================================
        // ROUTE CLUTTER
        // =====================================================

        CreateBarrel(
            "Barrel_LowerRoute_01",
            new Vector3(18f, 0.75f, -23f),
            root.transform
        );

        CreateSmallBox(
            "Box_LowerRoute_01",
            new Vector3(13f, 0.4f, -25f),
            35f,
            root.transform
        );

        CreateCrate(
            "Crate_LeftRoute_01",
            new Vector3(-17f, 0.55f, 7f),
            -25f,
            1.1f,
            root.transform
        );

        CreateBarrel(
            "Barrel_LeftRoute_01",
            new Vector3(-18f, 0.75f, 15f),
            root.transform
        );

        CreateSmallBox(
            "Box_CenterRoute_01",
            new Vector3(5f, 0.4f, 13f),
            20f,
            root.transform
        );

        CreateCrate(
            "Crate_UpperRoute_01",
            new Vector3(-3f, 0.55f, 24f),
            30f,
            1.1f,
            root.transform
        );

        // =====================================================
        // EXIT CLUTTER
        // =====================================================

        CreateBarrel(
            "Barrel_Exit_01",
            new Vector3(9.5f, 0.75f, 27f),
            root.transform
        );

        CreateSmallBox(
            "Box_Exit_01",
            new Vector3(16f, 0.4f, 26f),
            -20f,
            root.transform
        );

        Selection.activeGameObject = root;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Facility props generated. Barricades unchanged. Clutter upgraded.");
    }

    // =====================================================
    // MAIN BARRICADE - UNCHANGED
    // =====================================================

    private static void CreateBarricade(
        string name,
        Vector3 position,
        float rotationY,
        Transform parent)
    {
        GameObject barricade = new GameObject(name);

        barricade.transform.position = position;
        barricade.transform.rotation =
            Quaternion.Euler(0f, rotationY, 0f);

        barricade.transform.SetParent(parent);

        GameObject body = CreateCube(
            name + "_Body",
            barricade.transform,
            Vector3.zero,
            new Vector3(3f, 0.75f, 0.55f),
            barricadeMat
        );

        GameObject topBar = CreateCube(
            name + "_HazardBar",
            barricade.transform,
            new Vector3(0f, 0.52f, 0f),
            new Vector3(3.25f, 0.28f, 0.65f),
            hazardMat
        );

        GameObject frontStrip = CreateCube(
            name + "_FrontMetalStrip",
            barricade.transform,
            new Vector3(0f, -0.28f, -0.31f),
            new Vector3(2.65f, 0.13f, 0.08f),
            metalDetailMat
        );

        GameObject backStrip = CreateCube(
            name + "_BackMetalStrip",
            barricade.transform,
            new Vector3(0f, -0.28f, 0.31f),
            new Vector3(2.65f, 0.13f, 0.08f),
            metalDetailMat
        );

        GameObject leftFoot = CreateCube(
            name + "_LeftFoot",
            barricade.transform,
            new Vector3(-1f, -0.5f, 0f),
            new Vector3(0.38f, 0.5f, 1.15f),
            barricadeMat
        );

        GameObject rightFoot = CreateCube(
            name + "_RightFoot",
            barricade.transform,
            new Vector3(1f, -0.5f, 0f),
            new Vector3(0.38f, 0.5f, 1.15f),
            barricadeMat
        );

        CreateOutline(name, barricade.transform);

        RemoveCollider(body);
        RemoveCollider(topBar);
        RemoveCollider(frontStrip);
        RemoveCollider(backStrip);
        RemoveCollider(leftFoot);
        RemoveCollider(rightFoot);

        BoxCollider collider =
            barricade.AddComponent<BoxCollider>();

        collider.center = Vector3.zero;
        collider.size = new Vector3(3.4f, 1.85f, 1.15f);

        Rigidbody rb =
            barricade.AddComponent<Rigidbody>();

        SetupRigidbody(rb, 12f, true);
    }

    // =====================================================
    // GLOWING ORANGE BARRICADE OUTLINE - UNCHANGED
    // =====================================================

    private static void CreateOutline(
        string name,
        Transform parent)
    {
        float edge = 0.11f;
        float halfWidth = 1.72f;

        float topY = 0.72f;
        float bottomY = -0.55f;

        float frontZ = -0.38f;
        float backZ = 0.38f;

        float middleY = (topY + bottomY) / 2f;
        float verticalHeight = topY - bottomY;

        GameObject frontTop = CreateCube(
            name + "_Glow_FrontTop",
            parent,
            new Vector3(0f, topY, frontZ),
            new Vector3(3.55f, edge, edge),
            outlineMat
        );

        GameObject frontBottom = CreateCube(
            name + "_Glow_FrontBottom",
            parent,
            new Vector3(0f, bottomY, frontZ),
            new Vector3(3.55f, edge, edge),
            outlineMat
        );

        GameObject frontLeft = CreateCube(
            name + "_Glow_FrontLeft",
            parent,
            new Vector3(-halfWidth, middleY, frontZ),
            new Vector3(edge, verticalHeight, edge),
            outlineMat
        );

        GameObject frontRight = CreateCube(
            name + "_Glow_FrontRight",
            parent,
            new Vector3(halfWidth, middleY, frontZ),
            new Vector3(edge, verticalHeight, edge),
            outlineMat
        );

        GameObject backTop = CreateCube(
            name + "_Glow_BackTop",
            parent,
            new Vector3(0f, topY, backZ),
            new Vector3(3.55f, edge, edge),
            outlineMat
        );

        GameObject backBottom = CreateCube(
            name + "_Glow_BackBottom",
            parent,
            new Vector3(0f, bottomY, backZ),
            new Vector3(3.55f, edge, edge),
            outlineMat
        );

        GameObject backLeft = CreateCube(
            name + "_Glow_BackLeft",
            parent,
            new Vector3(-halfWidth, middleY, backZ),
            new Vector3(edge, verticalHeight, edge),
            outlineMat
        );

        GameObject backRight = CreateCube(
            name + "_Glow_BackRight",
            parent,
            new Vector3(halfWidth, middleY, backZ),
            new Vector3(edge, verticalHeight, edge),
            outlineMat
        );

        GameObject topLeft = CreateCube(
            name + "_Glow_TopLeft",
            parent,
            new Vector3(-halfWidth, topY, 0f),
            new Vector3(edge, edge, 0.86f),
            outlineMat
        );

        GameObject topRight = CreateCube(
            name + "_Glow_TopRight",
            parent,
            new Vector3(halfWidth, topY, 0f),
            new Vector3(edge, edge, 0.86f),
            outlineMat
        );

        GameObject bottomLeft = CreateCube(
            name + "_Glow_BottomLeft",
            parent,
            new Vector3(-halfWidth, bottomY, 0f),
            new Vector3(edge, edge, 0.86f),
            outlineMat
        );

        GameObject bottomRight = CreateCube(
            name + "_Glow_BottomRight",
            parent,
            new Vector3(halfWidth, bottomY, 0f),
            new Vector3(edge, edge, 0.86f),
            outlineMat
        );

        RemoveCollider(frontTop);
        RemoveCollider(frontBottom);
        RemoveCollider(frontLeft);
        RemoveCollider(frontRight);

        RemoveCollider(backTop);
        RemoveCollider(backBottom);
        RemoveCollider(backLeft);
        RemoveCollider(backRight);

        RemoveCollider(topLeft);
        RemoveCollider(topRight);
        RemoveCollider(bottomLeft);
        RemoveCollider(bottomRight);
    }

    // =====================================================
    // IMPROVED WOODEN CRATE
    // =====================================================

    private static void CreateCrate(
        string name,
        Vector3 position,
        float rotationY,
        float size,
        Transform parent)
    {
        GameObject crate = new GameObject(name);

        crate.transform.position = position;
        crate.transform.rotation =
            Quaternion.Euler(0f, rotationY, 0f);

        crate.transform.SetParent(parent);

        // Main wooden body
        GameObject body = CreateCube(
            name + "_WoodBody",
            crate.transform,
            Vector3.zero,
            new Vector3(size, size, size),
            crateMat
        );

        RemoveCollider(body);

        float half = size * 0.46f;
        float beam = size * 0.10f;
        float front = -(size * 0.51f);
        float back = size * 0.51f;

        // FRONT FRAME
        CreateVisualCube(
            name + "_FrontTop",
            crate.transform,
            new Vector3(0f, half, front),
            new Vector3(size, beam, beam),
            woodSlatMat
        );

        CreateVisualCube(
            name + "_FrontBottom",
            crate.transform,
            new Vector3(0f, -half, front),
            new Vector3(size, beam, beam),
            woodSlatMat
        );

        CreateVisualCube(
            name + "_FrontLeft",
            crate.transform,
            new Vector3(-half, 0f, front),
            new Vector3(beam, size, beam),
            woodSlatMat
        );

        CreateVisualCube(
            name + "_FrontRight",
            crate.transform,
            new Vector3(half, 0f, front),
            new Vector3(beam, size, beam),
            woodSlatMat
        );

        // FRONT CROSS BRACING
        GameObject brace1 = CreateVisualCube(
            name + "_FrontBrace1",
            crate.transform,
            new Vector3(0f, 0f, front - 0.01f),
            new Vector3(size * 1.15f, beam * 0.65f, beam * 0.55f),
            woodSlatMat
        );

        brace1.transform.localRotation =
            Quaternion.Euler(0f, 0f, 45f);

        GameObject brace2 = CreateVisualCube(
            name + "_FrontBrace2",
            crate.transform,
            new Vector3(0f, 0f, front - 0.015f),
            new Vector3(size * 1.15f, beam * 0.65f, beam * 0.55f),
            woodSlatMat
        );

        brace2.transform.localRotation =
            Quaternion.Euler(0f, 0f, -45f);

        // BACK FRAME
        CreateVisualCube(
            name + "_BackTop",
            crate.transform,
            new Vector3(0f, half, back),
            new Vector3(size, beam, beam),
            woodSlatMat
        );

        CreateVisualCube(
            name + "_BackBottom",
            crate.transform,
            new Vector3(0f, -half, back),
            new Vector3(size, beam, beam),
            woodSlatMat
        );

        CreateVisualCube(
            name + "_BackLeft",
            crate.transform,
            new Vector3(-half, 0f, back),
            new Vector3(beam, size, beam),
            woodSlatMat
        );

        CreateVisualCube(
            name + "_BackRight",
            crate.transform,
            new Vector3(half, 0f, back),
            new Vector3(beam, size, beam),
            woodSlatMat
        );

        // SIDE/TOP WOODEN SLATS
        CreateVisualCube(
            name + "_TopSlat1",
            crate.transform,
            new Vector3(-size * 0.28f, size * 0.51f, 0f),
            new Vector3(beam, beam * 0.6f, size),
            woodSlatMat
        );

        CreateVisualCube(
            name + "_TopSlat2",
            crate.transform,
            new Vector3(size * 0.28f, size * 0.51f, 0f),
            new Vector3(beam, beam * 0.6f, size),
            woodSlatMat
        );

        BoxCollider collider =
            crate.AddComponent<BoxCollider>();

        collider.size =
            new Vector3(size, size, size);

        Rigidbody rb =
            crate.AddComponent<Rigidbody>();

        SetupRigidbody(rb, 5f, false);
    }

    // =====================================================
    // IMPROVED STORAGE / CARDBOARD BOX
    // =====================================================

    private static void CreateSmallBox(
        string name,
        Vector3 position,
        float rotationY,
        Transform parent)
    {
        GameObject box = new GameObject(name);

        box.transform.position = position;
        box.transform.rotation =
            Quaternion.Euler(0f, rotationY, 0f);

        box.transform.SetParent(parent);

        // Main cardboard body
        GameObject body = CreateCube(
            name + "_CardboardBody",
            box.transform,
            Vector3.zero,
            new Vector3(0.9f, 0.75f, 0.75f),
            boxMat
        );

        RemoveCollider(body);

        // Top lid seam
        CreateVisualCube(
            name + "_TopTape",
            box.transform,
            new Vector3(0f, 0.39f, 0f),
            new Vector3(0.13f, 0.025f, 0.77f),
            tapeMat
        );

        // Front vertical tape
        CreateVisualCube(
            name + "_FrontTape",
            box.transform,
            new Vector3(0f, 0f, -0.385f),
            new Vector3(0.13f, 0.76f, 0.025f),
            tapeMat
        );

        // Back vertical tape
        CreateVisualCube(
            name + "_BackTape",
            box.transform,
            new Vector3(0f, 0f, 0.385f),
            new Vector3(0.13f, 0.76f, 0.025f),
            tapeMat
        );

        // Lid edges
        CreateVisualCube(
            name + "_TopFrontEdge",
            box.transform,
            new Vector3(0f, 0.385f, -0.35f),
            new Vector3(0.88f, 0.035f, 0.035f),
            caseDetailMat
        );

        CreateVisualCube(
            name + "_TopBackEdge",
            box.transform,
            new Vector3(0f, 0.385f, 0.35f),
            new Vector3(0.88f, 0.035f, 0.035f),
            caseDetailMat
        );

        // Small label panel
        CreateVisualCube(
            name + "_Label",
            box.transform,
            new Vector3(0.20f, 0.05f, -0.39f),
            new Vector3(0.30f, 0.20f, 0.025f),
            tapeMat
        );

        BoxCollider collider =
            box.AddComponent<BoxCollider>();

        collider.size =
            new Vector3(0.9f, 0.75f, 0.75f);

        Rigidbody rb =
            box.AddComponent<Rigidbody>();

        SetupRigidbody(rb, 2.5f, false);
    }

    // =====================================================
    // IMPROVED METAL BARREL
    // =====================================================

    private static void CreateBarrel(
        string name,
        Vector3 position,
        Transform parent)
    {
        GameObject barrel = new GameObject(name);

        barrel.transform.position = position;
        barrel.transform.SetParent(parent);

        // Main barrel
        GameObject body =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        body.name = name + "_Body";
        body.transform.SetParent(barrel.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale =
            new Vector3(0.65f, 0.75f, 0.65f);

        Renderer bodyRenderer =
            body.GetComponent<Renderer>();

        if (bodyRenderer != null)
            bodyRenderer.sharedMaterial = barrelMat;

        RemoveCollider(body);

        // Top rim
        CreateBarrelRing(
            name + "_TopRim",
            barrel.transform,
            0.73f,
            0.70f
        );

        // Upper band
        CreateBarrelRing(
            name + "_UpperBand",
            barrel.transform,
            0.35f,
            0.68f
        );

        // Middle band
        CreateBarrelRing(
            name + "_MiddleBand",
            barrel.transform,
            0f,
            0.68f
        );

        // Lower band
        CreateBarrelRing(
            name + "_LowerBand",
            barrel.transform,
            -0.35f,
            0.68f
        );

        // Bottom rim
        CreateBarrelRing(
            name + "_BottomRim",
            barrel.transform,
            -0.73f,
            0.70f
        );

        // Top cap
        GameObject cap =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        cap.name = name + "_TopCap";
        cap.transform.SetParent(barrel.transform);
        cap.transform.localPosition =
            new Vector3(0f, 0.755f, 0f);

        cap.transform.localScale =
            new Vector3(0.59f, 0.025f, 0.59f);

        Renderer capRenderer =
            cap.GetComponent<Renderer>();

        if (capRenderer != null)
            capRenderer.sharedMaterial = barrelBandMat;

        RemoveCollider(cap);

        // Small barrel plug
        GameObject plug =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        plug.name = name + "_Plug";
        plug.transform.SetParent(barrel.transform);

        plug.transform.localPosition =
            new Vector3(0.22f, 0.79f, 0f);

        plug.transform.localScale =
            new Vector3(0.07f, 0.025f, 0.07f);

        Renderer plugRenderer =
            plug.GetComponent<Renderer>();

        if (plugRenderer != null)
            plugRenderer.sharedMaterial = metalDetailMat;

        RemoveCollider(plug);

        CapsuleCollider collider =
            barrel.AddComponent<CapsuleCollider>();

        collider.direction = 1;
        collider.radius = 0.65f;
        collider.height = 1.5f;

        Rigidbody rb =
            barrel.AddComponent<Rigidbody>();

        SetupRigidbody(rb, 6f, false);
    }

    private static void CreateBarrelRing(
        string name,
        Transform parent,
        float y,
        float radius)
    {
        GameObject ring =
            GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        ring.name = name;
        ring.transform.SetParent(parent);

        ring.transform.localPosition =
            new Vector3(0f, y, 0f);

        ring.transform.localRotation =
            Quaternion.identity;

        ring.transform.localScale =
            new Vector3(radius, 0.035f, radius);

        Renderer renderer =
            ring.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = barrelBandMat;

        RemoveCollider(ring);
    }

    // =====================================================
    // VISUAL DETAIL CUBE - NO COLLIDER
    // =====================================================

    private static GameObject CreateVisualCube(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 scale,
        Material material)
    {
        GameObject obj = CreateCube(
            name,
            parent,
            localPosition,
            scale,
            material
        );

        RemoveCollider(obj);

        return obj;
    }

    // =====================================================
    // PHYSICS
    // =====================================================

    private static void SetupRigidbody(
        Rigidbody rb,
        float mass,
        bool keepUpright)
    {
        rb.mass = mass;

        rb.linearDamping = 2f;
        rb.angularDamping = 2.5f;

        rb.useGravity = true;

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        rb.collisionDetectionMode =
            CollisionDetectionMode.Continuous;

        if (keepUpright)
        {
            rb.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }
    }

    // =====================================================
    // CREATE CUBE
    // =====================================================

    private static GameObject CreateCube(
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

        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return obj;
    }

    // =====================================================
    // REMOVE COLLIDER
    // =====================================================

    private static void RemoveCollider(
        GameObject obj)
    {
        Collider collider =
            obj.GetComponent<Collider>();

        if (collider != null)
            DestroyImmediate(collider);
    }

    // =====================================================
    // MATERIALS
    // =====================================================

    private static void CreateMaterials()
    {
        EnsureMaterialFolder();

        // =====================================================
        // BARRICADE MATERIALS - UNCHANGED
        // =====================================================

        barricadeMat = CreateMaterial(
            "Barricade_IndustrialMetal",
            new Color(0.08f, 0.11f, 0.13f),
            0.72f,
            0.23f
        );

        hazardMat = CreateMaterial(
            "Barricade_HazardYellow",
            new Color(0.72f, 0.45f, 0.045f),
            0.32f,
            0.18f
        );

        outlineMat = CreateGlowMaterial(
            "Barricade_Outline_Glow",
            new Color(1.00f, 0.22f, 0.01f),
            4.5f
        );

        metalDetailMat = CreateMaterial(
            "Barricade_MetalDetail",
            new Color(0.28f, 0.30f, 0.31f),
            0.78f,
            0.32f
        );

        // =====================================================
        // IMPROVED CLUTTER MATERIALS
        // =====================================================

        // Main wood
        crateMat = CreateMaterial(
            "Clutter_WoodCrate",
            new Color(0.32f, 0.17f, 0.055f),
            0.01f,
            0.10f
        );

        // Dark wooden slats
        woodSlatMat = CreateMaterial(
            "Clutter_WoodSlats",
            new Color(0.16f, 0.075f, 0.025f),
            0.01f,
            0.08f
        );

        // Dark industrial barrel
        barrelMat = CreateMaterial(
            "Clutter_OldSteel",
            new Color(0.10f, 0.18f, 0.21f),
            0.60f,
            0.20f
        );

        // Barrel rims
        barrelBandMat = CreateMaterial(
            "Clutter_BarrelBands",
            new Color(0.27f, 0.29f, 0.30f),
            0.85f,
            0.30f
        );

        // Cardboard
        boxMat = CreateMaterial(
            "Clutter_StorageBox",
            new Color(0.43f, 0.29f, 0.13f),
            0.01f,
            0.06f
        );

        // Packing tape / labels
        tapeMat = CreateMaterial(
            "Clutter_PackingTape",
            new Color(0.68f, 0.58f, 0.35f),
            0.01f,
            0.16f
        );

        // Dark seams/details
        caseDetailMat = CreateMaterial(
            "Clutter_BoxDetail",
            new Color(0.16f, 0.11f, 0.055f),
            0.02f,
            0.08f
        );
    }

    // =====================================================
    // NORMAL MATERIAL
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
    // GLOW MATERIAL - BARRICADE ONLY
    // =====================================================

    private static Material CreateGlowMaterial(
        string name,
        Color color,
        float glowStrength)
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

        material.EnableKeyword("_EMISSION");

        Color emissionColor =
            color * glowStrength;

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor(
                "_EmissionColor",
                emissionColor
            );
        }

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.05f);

        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.40f);

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.40f);

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
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "Materials"
            );
        }
    }
}