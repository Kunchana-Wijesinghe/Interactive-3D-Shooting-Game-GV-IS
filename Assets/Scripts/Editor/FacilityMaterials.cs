using UnityEngine;
using UnityEditor;

public class FacilityMaterials : EditorWindow
{
    private const string ADR_PATH = "Assets/ADR_Textures";
    private const string OUTPUT_PATH = "Assets/Materials";

    private static Material floorMat;
    private static Material wallMat;
    private static Material roomWallMat;

    [MenuItem("Facility Breach/Apply Textured Facility Materials")]
    public static void ApplyMaterials()
    {
        GameObject facility = GameObject.Find("Generated_Facility");

        if (facility == null)
        {
            Debug.LogError("Generated_Facility not found.");
            return;
        }

        // =====================================================
        // FIND ORIGINAL ADR MATERIALS
        // =====================================================

        Material roughConcrete = FindADRMaterial("rough");
        Material concreteAggregate = FindADRMaterial("concrete");
        Material agedPlaster = FindADRMaterial("aged");

        if (roughConcrete == null)
        {
            Debug.LogError(
                "Could not find Rough Concrete material inside ADR_Textures."
            );
            return;
        }

        if (concreteAggregate == null)
        {
            Debug.LogError(
                "Could not find Concrete Aggregate material inside ADR_Textures."
            );
            return;
        }

        if (agedPlaster == null)
        {
            Debug.LogError(
                "Could not find Aged Plaster material inside ADR_Textures."
            );
            return;
        }

        // =====================================================
        // CREATE FACILITY MATERIAL COPIES
        // =====================================================

        floorMat = CreateMaterialCopy(
            roughConcrete,
            "Facility_RoughConcrete_Floor"
        );

        wallMat = CreateMaterialCopy(
            concreteAggregate,
            "Facility_LightBlueGrey_MainWalls"
        );

        roomWallMat = CreateMaterialCopy(
            agedPlaster,
            "Facility_LightWarmPlaster_RoomWalls"
        );
        Material corridorRoofMaterial =
            AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/Facility_Roof_Dark.mat"
            );
        Material corridorBeamMaterial =
            AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/Facility_Roof_Beams.mat"
            );

        // =====================================================
        // COLORS
        // =====================================================

        // DARK GREY FLOOR
        // Keep this darker so walls are easy to distinguish.
        SetMaterialTint(
            floorMat,
            new Color(
                0.32f,
                0.34f,
                0.36f,
                1f
            )
        );

        // VERY LIGHT BLUE-GREY MAIN WALLS
        SetMaterialTint(
            wallMat,
            new Color(
                0.82f,
                0.90f,
                0.95f,
                1f
            )
        );

        // LIGHT WARM BEIGE ROOM WALLS
        SetMaterialTint(
            roomWallMat,
            new Color(
                0.95f,
                0.84f,
                0.68f,
                1f
            )
        );

        // =====================================================
        // FLOOR TEXTURE + DEPTH
        // =====================================================

        ConfigureMaterial(
            floorMat,
            new Vector2(10f, 14f),
            1.8f,
            0.035f
        );

        // =====================================================
        // MAIN WALL TEXTURE + DEPTH
        // =====================================================

        ConfigureMaterial(
            wallMat,
            new Vector2(4f, 2f),
            1.45f,
            0.025f
        );

        // =====================================================
        // ROOM WALL TEXTURE + DEPTH
        // =====================================================

        ConfigureMaterial(
            roomWallMat,
            new Vector2(3f, 2f),
            1.30f,
            0.02f
        );

        // =====================================================
        // APPLY MATERIALS
        // =====================================================

        Renderer[] renderers =
            facility.GetComponentsInChildren<Renderer>(true);

        int floorCount = 0;
        int wallCount = 0;
        int roomCount = 0;

        foreach (Renderer renderer in renderers)
        {
            string objectName =
                renderer.gameObject.name.ToLower();

            if (IsUnderCorridorLighting(renderer.transform))
                continue;

            if (objectName.Contains("corridor_roof_beam"))
            {
                if (corridorBeamMaterial == null)
                {
                    Debug.LogError(
                        "Could not find Facility_Roof_Beams for corridor ceiling supports."
                    );
                    continue;
                }

                renderer.sharedMaterial = corridorBeamMaterial;
                continue;
            }

            if (objectName.Contains("corridor_roof"))
            {
                if (corridorRoofMaterial == null)
                {
                    Debug.LogError(
                        "Could not find Facility_Roof_Dark for the corridor ceiling."
                    );
                    continue;
                }

                renderer.sharedMaterial = corridorRoofMaterial;
                continue;
            }

            // =================================================
            // FLOOR
            // =================================================

            if (objectName.Contains("facility_floor") ||
                objectName.Contains("corridor_floor"))
            {
                renderer.sharedMaterial = floorMat;
                floorCount++;
                continue;
            }

            // =================================================
            // ROOM WALLS
            // =================================================

            if (objectName.StartsWith("room1_") ||
                objectName.StartsWith("room2_") ||
                objectName.StartsWith("room3_"))
            {
                renderer.sharedMaterial = roomWallMat;
                roomCount++;
                continue;
            }

            // =================================================
            // KEEP COVER COLORS
            // =================================================

            if (objectName.StartsWith("cover"))
            {
                continue;
            }

            // =================================================
            // KEEP ENTRY / EXIT COLORS
            // =================================================

            if (objectName.Contains("player_entry") ||
                objectName.Contains("facility_exit"))
            {
                continue;
            }

            // =================================================
            // KEEP SCALE REFERENCES
            // =================================================

            if (objectName.Contains("reference"))
            {
                continue;
            }

            // =================================================
            // MAIN / OUTER / CORRIDOR WALLS
            // =================================================

            renderer.sharedMaterial = wallMat;
            wallCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "BRIGHT FACILITY MATERIALS APPLIED | " +
            "Floor: " + floorCount +
            " | Light Warm Room Walls: " + roomCount +
            " | Light Blue-Grey Main Walls: " + wallCount
        );
    }

    private static bool IsUnderCorridorLighting(Transform current)
    {
        while (current != null)
        {
            if (current.name == "Corridor_Lighting")
                return true;

            current = current.parent;
        }

        return false;
    }

    // =========================================================
    // MATERIAL COLOR
    // =========================================================

    private static void SetMaterialTint(
        Material material,
        Color color)
    {
        if (material == null)
            return;

        // Standard shader
        if (material.HasProperty("_Color"))
        {
            material.SetColor(
                "_Color",
                color
            );
        }

        // URP shader
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color
            );
        }

        EditorUtility.SetDirty(material);
    }

    // =========================================================
    // FIND ADR MATERIAL
    // =========================================================

    private static Material FindADRMaterial(
        string searchTerm)
    {
        string[] folders =
        {
            ADR_PATH
        };

        string[] guids =
            AssetDatabase.FindAssets(
                searchTerm + " t:Material",
                folders
            );

        if (guids.Length == 0)
        {
            Debug.LogWarning(
                "No ADR material found containing: " +
                searchTerm
            );

            return null;
        }

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    path
                );

            if (material == null)
                continue;

            if (material.name
                .ToLower()
                .Contains(searchTerm.ToLower()))
            {
                Debug.Log(
                    "Using ADR material: " +
                    material.name +
                    " | " +
                    path
                );

                return material;
            }
        }

        string firstPath =
            AssetDatabase.GUIDToAssetPath(
                guids[0]
            );

        return AssetDatabase
            .LoadAssetAtPath<Material>(
                firstPath
            );
    }

    // =========================================================
    // CREATE COPY OF IMPORTED MATERIAL
    // =========================================================

    private static Material CreateMaterialCopy(
        Material source,
        string newName)
    {
        EnsureOutputFolder();

        string path =
            OUTPUT_PATH +
            "/" +
            newName +
            ".mat";

        Material existing =
            AssetDatabase.LoadAssetAtPath<Material>(
                path
            );

        if (existing != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        Material copy =
            new Material(source);

        copy.name = newName;

        AssetDatabase.CreateAsset(
            copy,
            path
        );

        return copy;
    }

    // =========================================================
    // TEXTURE TILING + DEPTH
    // =========================================================

    private static void ConfigureMaterial(
        Material material,
        Vector2 tiling,
        float normalStrength,
        float parallaxStrength)
    {
        if (material == null)
            return;

        // =====================================================
        // STANDARD BASE TEXTURE
        // =====================================================

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale(
                "_MainTex",
                tiling
            );
        }

        // =====================================================
        // URP BASE TEXTURE
        // =====================================================

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale(
                "_BaseMap",
                tiling
            );
        }

        // =====================================================
        // NORMAL MAP
        // =====================================================

        if (material.HasProperty("_BumpMap"))
        {
            Texture normal =
                material.GetTexture(
                    "_BumpMap"
                );

            if (normal != null)
            {
                material.EnableKeyword(
                    "_NORMALMAP"
                );

                material.SetTextureScale(
                    "_BumpMap",
                    tiling
                );

                if (material.HasProperty(
                    "_BumpScale"))
                {
                    material.SetFloat(
                        "_BumpScale",
                        normalStrength
                    );
                }
            }
        }

        // =====================================================
        // URP NORMAL MAP
        // =====================================================

        if (material.HasProperty("_NormalMap"))
        {
            Texture normal =
                material.GetTexture(
                    "_NormalMap"
                );

            if (normal != null)
            {
                material.SetTextureScale(
                    "_NormalMap",
                    tiling
                );
            }
        }

        // =====================================================
        // HEIGHT / PARALLAX
        // =====================================================

        if (material.HasProperty("_ParallaxMap"))
        {
            Texture height =
                material.GetTexture(
                    "_ParallaxMap"
                );

            if (height != null)
            {
                material.EnableKeyword(
                    "_PARALLAXMAP"
                );

                material.SetTextureScale(
                    "_ParallaxMap",
                    tiling
                );

                if (material.HasProperty(
                    "_Parallax"))
                {
                    material.SetFloat(
                        "_Parallax",
                        parallaxStrength
                    );
                }
            }
        }

        // =====================================================
        // AMBIENT OCCLUSION
        // =====================================================

        if (material.HasProperty("_OcclusionMap"))
        {
            Texture ao =
                material.GetTexture(
                    "_OcclusionMap"
                );

            if (ao != null)
            {
                material.SetTextureScale(
                    "_OcclusionMap",
                    tiling
                );

                if (material.HasProperty(
                    "_OcclusionStrength"))
                {
                    material.SetFloat(
                        "_OcclusionStrength",
                        1f
                    );
                }
            }
        }

        // =====================================================
        // METALLIC / ROUGHNESS
        // =====================================================

        if (material.HasProperty(
            "_MetallicGlossMap"))
        {
            Texture metallic =
                material.GetTexture(
                    "_MetallicGlossMap"
                );

            if (metallic != null)
            {
                material.SetTextureScale(
                    "_MetallicGlossMap",
                    tiling
                );
            }
        }

        EditorUtility.SetDirty(material);
    }

    // =========================================================
    // MATERIAL OUTPUT FOLDER
    // =========================================================

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder(
            OUTPUT_PATH))
        {
            AssetDatabase.CreateFolder(
                "Assets",
                "Materials"
            );
        }
    }
}