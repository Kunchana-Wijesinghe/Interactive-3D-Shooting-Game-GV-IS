using UnityEngine;
using UnityEditor;

public class FacilitySignage : EditorWindow
{
    private const string ROOT_NAME = "Facility_Signage";
    private const string MATERIAL_FOLDER = "Assets/Materials";

    private static Material blueSign;
    private static Material yellowSign;
    private static Material redSign;
    private static Material greenSign;

    private static Material frameMaterial;
    private static Material boltMaterial;

    // =========================================================
    // GENERATE ALL SIGNS
    // =========================================================

    [MenuItem("Facility Breach/Generate Facility Signage")]
    public static void GenerateSignage()
    {
        // Delete previous generated signage
        GameObject oldRoot = GameObject.Find(ROOT_NAME);

        if (oldRoot != null)
        {
            DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject(ROOT_NAME);

        // =====================================================
        // MATERIALS
        // =====================================================

        blueSign = CreateMaterial(
            "Sign_Blue",
            new Color(
                0.055f,
                0.16f,
                0.23f
            )
        );

        yellowSign = CreateMaterial(
            "Sign_Yellow",
            new Color(
                0.62f,
                0.43f,
                0.07f
            )
        );

        redSign = CreateMaterial(
            "Sign_Red",
            new Color(
                0.40f,
                0.055f,
                0.045f
            )
        );

        greenSign = CreateMaterial(
            "Sign_Green",
            new Color(
                0.035f,
                0.28f,
                0.12f
            )
        );

        frameMaterial = CreateMaterial(
            "Sign_Frame_Black",
            new Color(
                0.018f,
                0.022f,
                0.025f
            )
        );

        boltMaterial = CreateMaterial(
            "Sign_Bolts_Metal",
            new Color(
                0.32f,
                0.34f,
                0.35f
            )
        );

        // =====================================================
        // ENTRANCE
        // =====================================================

        CreateSignOnZWall(
            root.transform,
            "Entrance_Sign",
            "FACILITY ENTRANCE",
            new Vector3(
                -17f,
                2.8f,
                -29.78f
            ),
            true,
            blueSign,

            // Soft warm-grey text
            new Color(
                0.78f,
                0.82f,
                0.78f
            ),

            4.4f
        );

        // =====================================================
        // STORAGE
        // =====================================================

        CreateSignOnZWall(
            root.transform,
            "Storage_Sign",
            "STORAGE",
            new Vector3(
                -17f,
                2.75f,
                -6.32f
            ),
            false,
            yellowSign,

            // Dark brown/black text
            new Color(
                0.10f,
                0.085f,
                0.055f
            ),

            3.0f
        );

        CreateSignOnXWall(
            root.transform,
            "Storage_Side_Sign",
            "STORAGE",
            new Vector3(
                -7.82f,
                2.7f,
                -15f
            ),
            false,
            yellowSign,

            new Color(
                0.10f,
                0.085f,
                0.055f
            ),

            3.0f
        );

        // =====================================================
        // CONTROL
        // =====================================================

        CreateSignOnZWall(
            root.transform,
            "Control_Sign",
            "CONTROL",
            new Vector3(
                5.5f,
                2.75f,
                -1.82f
            ),
            true,
            blueSign,

            // Muted pale cyan-grey
            new Color(
                0.55f,
                0.78f,
                0.80f
            ),

            3.0f
        );

        CreateSignOnXWall(
            root.transform,
            "Control_Side_Sign",
            "CONTROL",
            new Vector3(
                -6.82f,
                2.7f,
                7.5f
            ),
            true,
            blueSign,

            new Color(
                0.55f,
                0.78f,
                0.80f
            ),

            3.0f
        );

        // =====================================================
        // MACHINERY
        // =====================================================

        CreateSignOnZWall(
            root.transform,
            "Machinery_Sign",
            "MACHINERY",
            new Vector3(
                17f,
                2.75f,
                12.18f
            ),
            true,
            redSign,

            // Muted warning cream
            new Color(
                0.88f,
                0.72f,
                0.42f
            ),

            3.6f
        );

        // =====================================================
        // RESTRICTED
        // =====================================================

        CreateSignOnXWall(
            root.transform,
            "Restricted_Sign",
            "RESTRICTED",
            new Vector3(
                10.18f,
                2.7f,
                21.5f
            ),
            true,
            redSign,

            new Color(
                0.88f,
                0.72f,
                0.42f
            ),

            3.6f
        );

        // =====================================================
        // EXIT
        // =====================================================

        CreateSignOnZWall(
            root.transform,
            "Exit_Sign",
            "EXIT",
            new Vector3(
                17f,
                2.8f,
                29.78f
            ),
            false,
            greenSign,

            // Soft green-grey lettering
            new Color(
                0.62f,
                0.84f,
                0.65f
            ),

            2.6f
        );

        // =====================================================
        // FINISH
        // =====================================================

        Undo.RegisterCreatedObjectUndo(
            root,
            "Generate Facility Signage"
        );

        Selection.activeGameObject = root;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Corrected industrial signage generated successfully."
        );
    }

    // =========================================================
    // Z WALL
    // =========================================================

    private static void CreateSignOnZWall(
        Transform parent,
        string objectName,
        string text,
        Vector3 position,
        bool facePositiveZ,
        Material boardMaterial,
        Color textColor,
        float width)
    {
        GameObject holder =
            new GameObject(objectName);

        holder.transform.SetParent(parent);

        holder.transform.position =
            position;

        holder.transform.rotation =
            Quaternion.Euler(
                0f,
                facePositiveZ ? 180f : 0f,
                0f
            );

        CreateSignParts(
            holder.transform,
            text,
            boardMaterial,
            textColor,
            width
        );
    }

    // =========================================================
    // X WALL
    // =========================================================

    private static void CreateSignOnXWall(
        Transform parent,
        string objectName,
        string text,
        Vector3 position,
        bool facePositiveX,
        Material boardMaterial,
        Color textColor,
        float width)
    {
        GameObject holder =
            new GameObject(objectName);

        holder.transform.SetParent(parent);

        holder.transform.position =
            position;

        holder.transform.rotation =
            Quaternion.Euler(
                0f,
                facePositiveX ? -90f : 90f,
                0f
            );

        CreateSignParts(
            holder.transform,
            text,
            boardMaterial,
            textColor,
            width
        );
    }

    // =========================================================
    // COMPLETE INDUSTRIAL SIGN
    // =========================================================

    private static void CreateSignParts(
        Transform holder,
        string text,
        Material boardMaterial,
        Color textColor,
        float width)
    {
        float height = 0.82f;

        // =====================================================
        // OUTER FRAME
        // =====================================================

        GameObject frame =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        frame.name =
            "Metal_Frame";

        frame.transform.SetParent(holder);

        frame.transform.localPosition =
            Vector3.zero;

        frame.transform.localRotation =
            Quaternion.identity;

        frame.transform.localScale =
            new Vector3(
                width + 0.20f,
                height + 0.20f,
                0.10f
            );

        SetMaterial(
            frame,
            frameMaterial
        );

        RemoveCollider(frame);

        // =====================================================
        // COLORED BOARD FACE
        // =====================================================

        GameObject board =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        board.name =
            "Sign_Board";

        board.transform.SetParent(holder);

        board.transform.localPosition =
            new Vector3(
                0f,
                0f,
                -0.065f
            );

        board.transform.localRotation =
            Quaternion.identity;

        board.transform.localScale =
            new Vector3(
                width,
                height,
                0.045f
            );

        SetMaterial(
            board,
            boardMaterial
        );

        RemoveCollider(board);

        // =====================================================
        // INDUSTRIAL INNER BORDER
        // =====================================================

        float borderZ = -0.094f;

        float borderThickness =
            0.028f;

        CreateSmallBox(
            holder,
            "Border_Top",
            new Vector3(
                0f,
                0.32f,
                borderZ
            ),
            new Vector3(
                width - 0.20f,
                borderThickness,
                0.015f
            ),
            frameMaterial
        );

        CreateSmallBox(
            holder,
            "Border_Bottom",
            new Vector3(
                0f,
                -0.32f,
                borderZ
            ),
            new Vector3(
                width - 0.20f,
                borderThickness,
                0.015f
            ),
            frameMaterial
        );

        CreateSmallBox(
            holder,
            "Border_Left",
            new Vector3(
                -(width / 2f) + 0.09f,
                0f,
                borderZ
            ),
            new Vector3(
                borderThickness,
                height - 0.20f,
                0.015f
            ),
            frameMaterial
        );

        CreateSmallBox(
            holder,
            "Border_Right",
            new Vector3(
                (width / 2f) - 0.09f,
                0f,
                borderZ
            ),
            new Vector3(
                borderThickness,
                height - 0.20f,
                0.015f
            ),
            frameMaterial
        );

        // =====================================================
        // TEXT
        //
        // IMPORTANT:
        // No 180-degree rotation here.
        // That was causing the inverted/mirrored lettering.
        // =====================================================

        GameObject textObject =
            new GameObject(
                "Printed_Text"
            );

        textObject.transform.SetParent(holder);

        // Almost flush with board face
        textObject.transform.localPosition =
            new Vector3(
                0f,
                -0.015f,
                -0.101f
            );

        // FIX:
        // Text faces the same direction as the sign.
        textObject.transform.localRotation =
            Quaternion.identity;

        TextMesh textMesh =
            textObject.AddComponent<TextMesh>();

        textMesh.text =
            text;

        textMesh.anchor =
            TextAnchor.MiddleCenter;

        textMesh.alignment =
            TextAlignment.Center;

        textMesh.fontStyle =
            FontStyle.Bold;

        textMesh.color =
            textColor;

        textMesh.richText =
            false;

        // =====================================================
        // LARGER LETTERING
        // =====================================================

        textMesh.fontSize = 64;

        textMesh.characterSize =
            0.055f;

        // =====================================================
        // SIZE BASED ON WORD LENGTH
        //
        // Larger than previous version but still contained.
        // =====================================================

        float scale;

        if (text.Length >= 15)
        {
            // FACILITY ENTRANCE
            scale = 0.50f;
        }
        else if (text.Length >= 10)
        {
            scale = 0.62f;
        }
        else if (text.Length >= 8)
        {
            // MACHINERY / RESTRICTED
            scale = 0.72f;
        }
        else if (text.Length >= 7)
        {
            // CONTROL / STORAGE
            scale = 0.82f;
        }
        else
        {
            // EXIT
            scale = 0.90f;
        }

        textObject.transform.localScale =
            new Vector3(
                scale,
                scale,
                scale
            );

        // =====================================================
        // MOUNTING BOLTS
        // =====================================================

        float boltX =
            (width / 2f) - 0.13f;

        float boltY =
            (height / 2f) - 0.13f;

        CreateBolt(
            holder,
            new Vector3(
                -boltX,
                boltY,
                -0.105f
            )
        );

        CreateBolt(
            holder,
            new Vector3(
                boltX,
                boltY,
                -0.105f
            )
        );

        CreateBolt(
            holder,
            new Vector3(
                -boltX,
                -boltY,
                -0.105f
            )
        );

        CreateBolt(
            holder,
            new Vector3(
                boltX,
                -boltY,
                -0.105f
            )
        );
    }

    // =========================================================
    // BOLT
    // =========================================================

    private static void CreateBolt(
        Transform parent,
        Vector3 localPosition)
    {
        GameObject bolt =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        bolt.name =
            "Mounting_Bolt";

        bolt.transform.SetParent(parent);

        bolt.transform.localPosition =
            localPosition;

        bolt.transform.localRotation =
            Quaternion.identity;

        bolt.transform.localScale =
            new Vector3(
                0.055f,
                0.055f,
                0.025f
            );

        SetMaterial(
            bolt,
            boltMaterial
        );

        RemoveCollider(bolt);
    }

    // =========================================================
    // SMALL BOX
    // =========================================================

    private static void CreateSmallBox(
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

        obj.name =
            objectName;

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

        // =====================================================
        // METAL SETTINGS
        // =====================================================

        if (
            materialName.Contains("Frame") ||
            materialName.Contains("Bolt")
        )
        {
            if (material.HasProperty(
                "_Metallic"))
            {
                material.SetFloat(
                    "_Metallic",
                    0.40f
                );
            }

            if (material.HasProperty(
                "_Smoothness"))
            {
                material.SetFloat(
                    "_Smoothness",
                    0.25f
                );
            }

            if (material.HasProperty(
                "_Glossiness"))
            {
                material.SetFloat(
                    "_Glossiness",
                    0.25f
                );
            }
        }

        EditorUtility.SetDirty(
            material
        );

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