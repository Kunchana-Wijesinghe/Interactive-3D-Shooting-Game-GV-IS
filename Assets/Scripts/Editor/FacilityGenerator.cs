using UnityEngine;
using UnityEditor;

public class FacilityGenerator : EditorWindow
{
    private const float H = 4f;
    private const float T = 0.35f;

    [MenuItem("Facility Breach/Generate Final Facility V4")]
    public static void Generate()
    {
        GameObject old = GameObject.Find("Generated_Facility");

        if (old != null)
            DestroyImmediate(old);

        GameObject root = new GameObject("Generated_Facility");

        // FLOOR
        Block(
            "Facility_Floor",
            new Vector3(0, -0.25f, 0),
            new Vector3(42, 0.5f, 60),
            root.transform
        );

        // =====================================================
        // OUTER WALLS
        // =====================================================

        Wall(
            "Outer_Left",
            new Vector3(-21, 2, 0),
            new Vector3(T, H, 60),
            root.transform
        );

        Wall(
            "Outer_Right",
            new Vector3(21, 2, 0),
            new Vector3(T, H, 60),
            root.transform
        );

        // MAIN ENTRANCE — 2 UNIT OPENING
        // Center = -12.5
        // Opening = -13.5 to -11.5

        Wall(
            "South_Left",
            new Vector3(-17.25f, 2, -30),
            new Vector3(7.5f, H, T),
            root.transform
        );

        Wall(
            "South_Right",
            new Vector3(4.75f, 2, -30),
            new Vector3(32.5f, H, T),
            root.transform
        );

        // MAIN EXIT — 2 UNIT OPENING
        // Center = 12.5
        // Opening = 11.5 to 13.5

        Wall(
            "North_Left",
            new Vector3(-4.75f, 2, 30),
            new Vector3(32.5f, H, T),
            root.transform
        );

        Wall(
            "North_Right",
            new Vector3(17.25f, 2, 30),
            new Vector3(7.5f, H, T),
            root.transform
        );

        // =====================================================
        // ENTRANCE AREA
        // =====================================================

        Wall(
            "Entry_Blocker",
            new Vector3(-10, 2, -24),
            new Vector3(13, H, T),
            root.transform
        );

        Wall(
            "Entry_Side",
            new Vector3(-3, 2, -20),
            new Vector3(T, H, 5),
            root.transform
        );

        // =====================================================
        // ROOM 1
        // =====================================================

        Wall(
            "Room1_Left",
            new Vector3(-18, 2, -12),
            new Vector3(T, H, 11),
            root.transform
        );

        Wall(
            "Room1_Bottom",
            new Vector3(-13, 2, -17.5f),
            new Vector3(10, H, T),
            root.transform
        );

        // RIGHT DOOR — 2 UNITS
        // Center Z = -11.75

        Wall(
            "Room1_Right_Bottom",
            new Vector3(-8, 2, -15.625f),
            new Vector3(T, H, 3.75f),
            root.transform
        );

        Wall(
            "Room1_Right_Top",
            new Vector3(-8, 2, -8.625f),
            new Vector3(T, H, 4.25f),
            root.transform
        );

        // TOP DOOR — 2 UNITS
        // Center X = -13

        Wall(
            "Room1_Top_Left",
            new Vector3(-16, 2, -6.5f),
            new Vector3(4, H, T),
            root.transform
        );

        Wall(
            "Room1_Top_Right",
            new Vector3(-10, 2, -6.5f),
            new Vector3(4, H, T),
            root.transform
        );

        // =====================================================
        // ROOM 2
        // =====================================================

        // BOTTOM DOOR — 2 UNITS
        // Center X = 1

        Wall(
            "Room2_Bottom_Left",
            new Vector3(-3.5f, 2, -2),
            new Vector3(7, H, T),
            root.transform
        );

        Wall(
            "Room2_Bottom_Right",
            new Vector3(5.5f, 2, -2),
            new Vector3(7, H, T),
            root.transform
        );

        // TOP DOOR — 2 UNITS

        Wall(
            "Room2_Top_Left",
            new Vector3(-3.5f, 2, 10),
            new Vector3(7, H, T),
            root.transform
        );

        Wall(
            "Room2_Top_Right",
            new Vector3(5.5f, 2, 10),
            new Vector3(7, H, T),
            root.transform
        );

        // LEFT DOOR — 2 UNITS
        // Center Z = 4

        Wall(
            "Room2_Left_Bottom",
            new Vector3(-7, 2, 0.5f),
            new Vector3(T, H, 5),
            root.transform
        );

        Wall(
            "Room2_Left_Top",
            new Vector3(-7, 2, 7.5f),
            new Vector3(T, H, 5),
            root.transform
        );

        // RIGHT DOOR — 2 UNITS

        Wall(
            "Room2_Right_Bottom",
            new Vector3(9, 2, 0.5f),
            new Vector3(T, H, 5),
            root.transform
        );

        Wall(
            "Room2_Right_Top",
            new Vector3(9, 2, 7.5f),
            new Vector3(T, H, 5),
            root.transform
        );

        // =====================================================
        // ROOM 3
        // =====================================================

        Wall(
            "Room3_Right",
            new Vector3(18, 2, 18),
            new Vector3(T, H, 12),
            root.transform
        );

        // LEFT DOOR — 2 UNITS
        // Center Z = 18.75

        Wall(
            "Room3_Left_Bottom",
            new Vector3(10, 2, 15),
            new Vector3(T, H, 6),
            root.transform
        );

        Wall(
            "Room3_Left_Top",
            new Vector3(10, 2, 22.5f),
            new Vector3(T, H, 3),
            root.transform
        );

        // BOTTOM DOOR — 2 UNITS
        // Center X = 14

        Wall(
            "Room3_Bottom_Left",
            new Vector3(11.5f, 2, 12),
            new Vector3(3, H, T),
            root.transform
        );

        Wall(
            "Room3_Bottom_Right",
            new Vector3(16.5f, 2, 12),
            new Vector3(3, H, T),
            root.transform
        );

        // TOP DOOR — 2 UNITS

        Wall(
            "Room3_Top_Left",
            new Vector3(11.5f, 2, 24),
            new Vector3(3, H, T),
            root.transform
        );

        Wall(
            "Room3_Top_Right",
            new Vector3(16.5f, 2, 24),
            new Vector3(3, H, T),
            root.transform
        );

        // =====================================================
        // LOWER ROUTES
        // =====================================================

        Wall(
            "Lower_Center_Block",
            new Vector3(6, 2, -22),
            new Vector3(16, H, T),
            root.transform
        );

        Wall(
            "Lower_Right_Block",
            new Vector3(16, 2, -17),
            new Vector3(T, H, 7),
            root.transform
        );

        Wall(
            "Lower_Middle_Block",
            new Vector3(3, 2, -11),
            new Vector3(9, H, T),
            root.transform
        );

        // =====================================================
        // MIDDLE ROUTES
        // =====================================================

        Wall(
            "Middle_Left_Block",
            new Vector3(-15, 2, 1),
            new Vector3(8, H, T),
            root.transform
        );

        Wall(
            "Middle_Right_Block",
            new Vector3(16, 2, 3),
            new Vector3(5, H, T),
            root.transform
        );

        Wall(
            "Middle_Offset_Block",
            new Vector3(3, 2, 16),
            new Vector3(T, H, 4),
            root.transform
        );

        // =====================================================
        // UPPER ROUTES
        // =====================================================

        Wall(
            "Upper_Left_Block",
            new Vector3(-14, 2, 18),
            new Vector3(10, H, T),
            root.transform
        );

        Wall(
            "Upper_Left_Vertical",
            new Vector3(-7, 2, 23),
            new Vector3(T, H, 5),
            root.transform
        );

        Wall(
            "Upper_Center_Block",
            new Vector3(1, 2, 26),
            new Vector3(8, H, T),
            root.transform
        );

        // =====================================================
        // COVER OBJECTS
        // =====================================================

        Cover(
            "Cover_Entry",
            new Vector3(-4, 0.75f, -26),
            new Vector3(2.5f, 1.5f, 1),
            root.transform
        );

        Cover(
            "Cover_Room1",
            new Vector3(-13, 0.75f, -11),
            new Vector3(2.5f, 1.5f, 1),
            root.transform
        );

        Cover(
            "Cover_LowerRoute",
            new Vector3(11, 0.75f, -14),
            new Vector3(1, 1.5f, 2),
            root.transform
        );

        Cover(
            "Cover_Room2_A",
            new Vector3(-2, 0.75f, 4),
            new Vector3(2.5f, 1.5f, 1),
            root.transform
        );

        Cover(
            "Cover_Room2_B",
            new Vector3(5, 0.75f, 6),
            new Vector3(1, 1.5f, 2),
            root.transform
        );

        Cover(
            "Cover_LeftRoute",
            new Vector3(-14, 0.75f, 11),
            new Vector3(2.5f, 1.5f, 1),
            root.transform
        );

        Cover(
            "Cover_UpperRoute",
            new Vector3(-1, 0.75f, 21),
            new Vector3(2.5f, 1.5f, 1),
            root.transform
        );

        Cover(
            "Cover_Room3",
            new Vector3(14, 0.75f, 18),
            new Vector3(2.5f, 1.5f, 1),
            root.transform
        );

        // =====================================================
        // ENTRY / EXIT MARKERS
        // =====================================================

        Marker(
            "PLAYER_ENTRY",
            new Vector3(-12.5f, 0.06f, -28),
            new Vector3(2, 0.12f, 2),
            root.transform
        );

        Marker(
            "FACILITY_EXIT",
            new Vector3(12.5f, 0.06f, 28),
            new Vector3(2, 0.12f, 2),
            root.transform
        );

        // =====================================================
        // PLAYER / NPC REFERENCES
        // =====================================================

        Capsule(
            "Player_Scale_Reference",
            new Vector3(-13, 1, -26),
            root.transform
        );

        Capsule(
            "NPC_Scale_Reference",
            new Vector3(-9, 1, -26),
            root.transform
        );

        Selection.activeGameObject = root;

        Debug.Log(
            "Facility generated with all door openings set to 2 units."
        );
    }

    private static void Wall(
        string name,
        Vector3 position,
        Vector3 scale,
        Transform parent)
    {
        Block(name, position, scale, parent);
    }

    private static void Cover(
        string name,
        Vector3 position,
        Vector3 scale,
        Transform parent)
    {
        Block(name, position, scale, parent);
    }

    private static GameObject Block(
        string name,
        Vector3 position,
        Vector3 scale,
        Transform parent)
    {
        GameObject obj =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.transform.SetParent(parent);

        return obj;
    }

    private static void Marker(
        string name,
        Vector3 position,
        Vector3 scale,
        Transform parent)
    {
        GameObject marker =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        marker.name = name;
        marker.transform.position = position;
        marker.transform.localScale = scale;
        marker.transform.SetParent(parent);

        Collider col = marker.GetComponent<Collider>();

        if (col != null)
            DestroyImmediate(col);
    }

    private static void Capsule(
        string name,
        Vector3 position,
        Transform parent)
    {
        GameObject capsule =
            GameObject.CreatePrimitive(PrimitiveType.Capsule);

        capsule.name = name;
        capsule.transform.position = position;
        capsule.transform.localScale =
            new Vector3(0.65f, 0.9f, 0.65f);

        capsule.transform.SetParent(parent);

        Collider col = capsule.GetComponent<Collider>();

        if (col != null)
            DestroyImmediate(col);
    }
}