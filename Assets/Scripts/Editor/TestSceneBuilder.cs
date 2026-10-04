using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class TestSceneBuilder : EditorWindow
{
    private const string SCENE_DIR = "Assets/Scenes/Tests";
    private const string DOOR_SCENE_PATH = "Assets/Scenes/Tests/DoorInteractionTest.unity";
    private const string BARRICADE_SCENE_PATH = "Assets/Scenes/Tests/BarricadeInteractionTest.unity";
    private const string MAT_DIR = "Assets/Materials";

    [MenuItem("Facility Breach/Tests/Build Door Interaction Test Scene")]
    public static void BuildDoorScene()
    {
        EnsureFolders();
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting
        CreateSun();

        // Materials
        Material floorMat = LoadOrCreateMat("Facility_RoughConcrete_Floor", new Color(0.32f, 0.34f, 0.36f));
        Material wallMat = LoadOrCreateMat("Facility_LightBlueGrey_MainWalls", new Color(0.82f, 0.90f, 0.95f));
        Material frameMat = LoadOrCreateMat("Door_Frame", new Color(0.04f, 0.04f, 0.05f));
        Material doorMat = LoadOrCreateMat("Door_Red", new Color(0.42f, 0.025f, 0.025f));
        Material indicatorMat = LoadOrCreateMat("Door_Indicator", new Color(0.0f, 0.9f, 1.0f));

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Test_Floor";
        floor.transform.position = new Vector3(0, -0.25f, 0);
        floor.transform.localScale = new Vector3(24f, 0.5f, 24f);
        if (floorMat != null) floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        // Walls (Doorway gap in center, width ~ 3.8m)
        GameObject wallLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallLeft.name = "Wall_Left";
        wallLeft.transform.position = new Vector3(-6.0f, 2.0f, 0f);
        wallLeft.transform.localScale = new Vector3(8.0f, 4.0f, 0.35f);
        if (wallMat != null) wallLeft.GetComponent<Renderer>().sharedMaterial = wallMat;

        GameObject wallRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallRight.name = "Wall_Right";
        wallRight.transform.position = new Vector3(6.0f, 2.0f, 0f);
        wallRight.transform.localScale = new Vector3(8.0f, 4.0f, 0.35f);
        if (wallMat != null) wallRight.GetComponent<Renderer>().sharedMaterial = wallMat;

        GameObject wallLintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallLintel.name = "Wall_Lintel";
        wallLintel.transform.position = new Vector3(0f, 3.55f, 0f);
        wallLintel.transform.localScale = new Vector3(4.0f, 0.9f, 0.35f);
        if (wallMat != null) wallLintel.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Door
        float width = 3.8f;
        Vector3 doorPos = Vector3.zero;

        GameObject doorRoot = new GameObject("Door_Test");
        doorRoot.transform.position = doorPos;

        CreateCube("Frame_Left", doorPos + new Vector3(-width / 2f - 0.15f, 1.5f, 0), new Vector3(0.3f, 3f, 0.45f), frameMat, doorRoot.transform);
        CreateCube("Frame_Right", doorPos + new Vector3(width / 2f + 0.15f, 1.5f, 0), new Vector3(0.3f, 3f, 0.45f), frameMat, doorRoot.transform);
        CreateCube("Frame_Top", doorPos + new Vector3(0, 3.1f, 0), new Vector3(width + 0.6f, 0.25f, 0.45f), frameMat, doorRoot.transform);

        GameObject hinge = new GameObject("Door_Test_Hinge");
        hinge.transform.position = doorPos + new Vector3(-width / 2f, 0, 0);
        hinge.transform.SetParent(doorRoot.transform);

        InteractiveDoor interactiveDoor = hinge.AddComponent<InteractiveDoor>();
        interactiveDoor.openAngle = 90f;
        interactiveDoor.openSpeed = 4f;

        GameObject panel = CreateCube("Door_Test_Panel", doorPos, new Vector3(width, 3f, 0.25f), doorMat, hinge.transform);
        panel.transform.position = hinge.transform.position + new Vector3(width / 2f, 1.5f, 0);

        GameObject trigger = new GameObject("Door_Test_Trigger");
        trigger.transform.position = doorPos + new Vector3(0, 1.5f, 0);
        trigger.transform.SetParent(doorRoot.transform);

        BoxCollider triggerCol = trigger.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(width + 2f, 3f, 4f);

        DoorTrigger doorTrigger = trigger.AddComponent<DoorTrigger>();
        doorTrigger.door = interactiveDoor;

        GameObject indicator = CreateCube("Door_Test_Indicator", doorPos + new Vector3(width / 2f + 0.25f, 2.2f, -0.3f), new Vector3(0.18f, 0.35f, 0.18f), indicatorMat, doorRoot.transform);
        Collider indCol = indicator.GetComponent<Collider>();
        if (indCol != null) Object.DestroyImmediate(indCol);

        // Player
        CreateTestPlayer(new Vector3(0f, 1.0f, -5.0f));

        EditorSceneManager.SaveScene(scene, DOOR_SCENE_PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Door Interaction Test Scene successfully built at: " + DOOR_SCENE_PATH);
    }

    [MenuItem("Facility Breach/Tests/Build Barricade Interaction Test Scene")]
    public static void BuildBarricadeScene()
    {
        EnsureFolders();
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting
        CreateSun();

        // Materials
        Material floorMat = LoadOrCreateMat("Facility_RoughConcrete_Floor", new Color(0.32f, 0.34f, 0.36f));
        Material wallMat = LoadOrCreateMat("Facility_LightBlueGrey_MainWalls", new Color(0.82f, 0.90f, 0.95f));
        Material barrierMat = LoadOrCreateMat("Prop_Barrier_Yellow", new Color(0.9f, 0.75f, 0.1f));
        Material metalMat = LoadOrCreateMat("Detail_DarkMetal", new Color(0.15f, 0.15f, 0.16f));
        Material hazardMat = LoadOrCreateMat("Detail_HazardYellow", new Color(1.0f, 0.85f, 0.0f));

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Test_Floor";
        floor.transform.position = new Vector3(0, -0.25f, 0);
        floor.transform.localScale = new Vector3(28f, 0.5f, 28f);
        if (floorMat != null) floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        // Testing Arena Walls:
        // 1. Solid back wall at Z = 10m (to test pushing against solid walls)
        GameObject wallNorth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallNorth.name = "Wall_North_Solid";
        wallNorth.transform.position = new Vector3(0f, 2.0f, 10.0f);
        wallNorth.transform.localScale = new Vector3(24.0f, 4.0f, 0.5f);
        if (wallMat != null) wallNorth.GetComponent<Renderer>().sharedMaterial = wallMat;

        // 2. West and East boundary walls
        GameObject wallWest = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallWest.name = "Wall_West";
        wallWest.transform.position = new Vector3(-12.0f, 2.0f, 0f);
        wallWest.transform.localScale = new Vector3(0.5f, 4.0f, 20.0f);
        if (wallMat != null) wallWest.GetComponent<Renderer>().sharedMaterial = wallMat;

        GameObject wallEast = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallEast.name = "Wall_East";
        wallEast.transform.position = new Vector3(12.0f, 2.0f, 0f);
        wallEast.transform.localScale = new Vector3(0.5f, 4.0f, 20.0f);
        if (wallMat != null) wallEast.GetComponent<Renderer>().sharedMaterial = wallMat;

        // 3. Partition wall at Z = 4.5m with a 3.0m doorway gap
        // Gap from X = -1.5 to +1.5. Left partition: X from -1.5 to -12 (width 10.5m, center -6.75m)
        GameObject partLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        partLeft.name = "Partition_Left";
        partLeft.transform.position = new Vector3(-6.75f, 2.0f, 4.5f);
        partLeft.transform.localScale = new Vector3(10.5f, 4.0f, 0.5f);
        if (wallMat != null) partLeft.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Right partition: X from +1.5 to +12 (width 10.5m, center +6.75m)
        GameObject partRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        partRight.name = "Partition_Right";
        partRight.transform.position = new Vector3(6.75f, 2.0f, 4.5f);
        partRight.transform.localScale = new Vector3(10.5f, 4.0f, 0.5f);
        if (wallMat != null) partRight.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Doorway lintel
        GameObject partLintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        partLintel.name = "Partition_Lintel";
        partLintel.transform.position = new Vector3(0f, 3.6f, 4.5f);
        partLintel.transform.localScale = new Vector3(3.0f, 0.8f, 0.5f);
        if (wallMat != null) partLintel.GetComponent<Renderer>().sharedMaterial = wallMat;

        // =====================================================
        // MOVABLE BARRICADE
        // =====================================================
        Vector3 barricadePos = new Vector3(0f, 0f, 0.5f);
        GameObject barricadeRoot = new GameObject("Movable_Barricade");
        barricadeRoot.transform.position = barricadePos;

        // Main barricade visual body (without individual collider)
        GameObject barrierBody = CreateVisualCube("Barricade_Body", barricadePos + new Vector3(0f, 0.65f, 0f), new Vector3(2.4f, 1.0f, 0.35f), barrierMat, barricadeRoot.transform);

        // Support feet (metal skids)
        CreateVisualCube("Foot_Left", barricadePos + new Vector3(-0.95f, 0.1f, 0f), new Vector3(0.25f, 0.2f, 0.9f), metalMat, barricadeRoot.transform);
        CreateVisualCube("Foot_Right", barricadePos + new Vector3(0.95f, 0.1f, 0f), new Vector3(0.25f, 0.2f, 0.9f), metalMat, barricadeRoot.transform);

        // Top hazard warning rail
        CreateVisualCube("Hazard_Top", barricadePos + new Vector3(0f, 1.2f, 0f), new Vector3(2.45f, 0.12f, 0.38f), hazardMat, barricadeRoot.transform);

        // Diagonal bracing struts
        CreateVisualCube("Strut_Left", barricadePos + new Vector3(-0.95f, 0.5f, 0f), new Vector3(0.12f, 0.8f, 0.12f), metalMat, barricadeRoot.transform);
        CreateVisualCube("Strut_Right", barricadePos + new Vector3(0.95f, 0.5f, 0f), new Vector3(0.12f, 0.8f, 0.12f), metalMat, barricadeRoot.transform);

        // Single clean BoxCollider on root
        BoxCollider boxCol = barricadeRoot.AddComponent<BoxCollider>();
        boxCol.center = new Vector3(0f, 0.65f, 0f);
        boxCol.size = new Vector3(2.45f, 1.3f, 0.9f);

        // Rigidbody
        Rigidbody rb = barricadeRoot.AddComponent<Rigidbody>();
        rb.mass = 60f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // MovableBarricade logic
        MovableBarricade barricadeScript = barricadeRoot.AddComponent<MovableBarricade>();
        barricadeScript.interactionDistance = 3.0f;
        barricadeScript.holdDistance = 2.0f;
        barricadeScript.followSpeed = 8.0f;
        barricadeScript.maxHoldDistance = 3.8f;
        barricadeScript.mass = 60.0f;

        // Player at (0, 1.0, -4.0) facing +Z toward barricade
        CreateTestPlayer(new Vector3(0f, 1.0f, -4.0f));

        EditorSceneManager.SaveScene(scene, BARRICADE_SCENE_PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Barricade Interaction Test Scene successfully built at: " + BARRICADE_SCENE_PATH);
    }

    private static void CreateSun()
    {
        GameObject sunObj = new GameObject("Directional_Light");
        Light sun = sunObj.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.9f);
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        sunObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void CreateTestPlayer(Vector3 position)
    {
        GameObject player = new GameObject("TestPlayer");
        player.tag = "Player";
        player.transform.position = position;
        player.transform.rotation = Quaternion.identity;

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2.0f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;

        // Visual capsule body (no collider)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Player_Visual";
        body.transform.SetParent(player.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = Vector3.one;
        Collider bodyCol = body.GetComponent<Collider>();
        if (bodyCol != null) Object.DestroyImmediate(bodyCol);

        // Camera
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        camObj.transform.SetParent(player.transform);
        camObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        camObj.transform.localRotation = Quaternion.identity;

        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.1f;
        camObj.AddComponent<AudioListener>();

        // Controller script
        SimplePlayerController playerController = player.AddComponent<SimplePlayerController>();
        playerController.playerCamera = cam;
        playerController.moveSpeed = 5.0f;
        playerController.physicalPushPower = 40.0f;
        playerController.interactionRangeHint = 3.0f;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
        if (!AssetDatabase.IsValidFolder(SCENE_DIR))
        {
            AssetDatabase.CreateFolder("Assets/Scenes", "Tests");
        }
    }

    private static Material LoadOrCreateMat(string name, Color fallback)
    {
        string path = MAT_DIR + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fallback);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", fallback);
            }
        }
        return mat;
    }

    private static GameObject CreateCube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.position = pos;
        obj.transform.localScale = scale;
        obj.transform.SetParent(parent);
        if (mat != null)
        {
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }
        return obj;
    }

    private static GameObject CreateVisualCube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject obj = CreateCube(name, pos, scale, mat, parent);
        Collider col = obj.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        return obj;
    }
}
