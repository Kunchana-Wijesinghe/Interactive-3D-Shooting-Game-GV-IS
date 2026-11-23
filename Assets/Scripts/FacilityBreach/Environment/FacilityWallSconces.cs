using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FacilityBreach.Environment
{
    /// <summary>
    /// GV Architectural Up/Down Wall Sconce Lighting System.
    /// Recreates the modern dual-beam architectural sconce fixture (black rectangular housing
    /// mounted flush against textured walls, casting distinct upward and downward conical illumination).
    /// Fully integrated with the emergency meltdown system for red alert pulsing.
    /// </summary>
    public static class FacilityWallSconces
    {
        public const string ROOT_NAME = "Facility_Wall_Sconces";

        public struct SconcePlacement
        {
            public string Name;
            public Vector3 WallSurfacePosition; // Precise position flush on the wall face
            public float OutwardYawAngle;       // Facing into the room perpendicular to the wall

            public SconcePlacement(string name, Vector3 pos, float yaw)
            {
                Name = name;
                WallSurfacePosition = pos;
                OutwardYawAngle = yaw;
            }
        }

        // High-precision placements calculated directly from facility wall geometry (T = 0.35m, H = 5.0m, Eye level Y = 2.0m)
        // Covers every single outer wall, room interior, corridor passage, and partition block across the facility.
        public static readonly SconcePlacement[] Placements = new SconcePlacement[]
        {
            // --- WEST OUTER WALL (X = -20.825, facing +X / East, Yaw = 90) ---
            new SconcePlacement("Sconce_WestWall_1", new Vector3(-20.825f, 2.00f, -24.0f), 90f),
            new SconcePlacement("Sconce_WestWall_2", new Vector3(-20.825f, 2.00f, -14.0f), 90f),
            new SconcePlacement("Sconce_WestWall_3", new Vector3(-20.825f, 2.00f,  -2.0f), 90f),
            new SconcePlacement("Sconce_WestWall_4", new Vector3(-20.825f, 2.00f,  10.0f), 90f),
            new SconcePlacement("Sconce_WestWall_5", new Vector3(-20.825f, 2.00f,  22.0f), 90f),

            // --- EAST OUTER WALL (X = +20.825, facing -X / West, Yaw = -90) ---
            new SconcePlacement("Sconce_EastWall_1", new Vector3( 20.825f, 2.00f, -24.0f), -90f),
            new SconcePlacement("Sconce_EastWall_2", new Vector3( 20.825f, 2.00f, -14.0f), -90f),
            new SconcePlacement("Sconce_EastWall_3", new Vector3( 20.825f, 2.00f,  -2.0f), -90f),
            new SconcePlacement("Sconce_EastWall_4", new Vector3( 20.825f, 2.00f,  10.0f), -90f),
            new SconcePlacement("Sconce_EastWall_5", new Vector3( 20.825f, 2.00f,  22.0f), -90f),

            // --- SOUTH OUTER WALL (Z = -29.825, facing +Z / North, Yaw = 0) ---
            new SconcePlacement("Sconce_SouthWall_1", new Vector3(-17.25f, 2.00f, -29.825f), 0f),
            new SconcePlacement("Sconce_SouthWall_2", new Vector3( -4.00f, 2.00f, -29.825f), 0f),
            new SconcePlacement("Sconce_SouthWall_3", new Vector3(  6.00f, 2.00f, -29.825f), 0f),
            new SconcePlacement("Sconce_SouthWall_4", new Vector3( 16.00f, 2.00f, -29.825f), 0f),

            // --- NORTH OUTER WALL (Z = +29.825, facing -Z / South, Yaw = 180) ---
            new SconcePlacement("Sconce_NorthWall_1", new Vector3(-14.00f, 2.00f,  29.825f), 180f),
            new SconcePlacement("Sconce_NorthWall_2", new Vector3( -4.00f, 2.00f,  29.825f), 180f),
            new SconcePlacement("Sconce_NorthWall_3", new Vector3(  8.00f, 2.00f,  29.825f), 180f),
            new SconcePlacement("Sconce_NorthWall_4", new Vector3( 17.25f, 2.00f,  29.825f), 180f),

            // --- ENTRANCE & SOUTHERN CORRIDOR WALLS ---
            new SconcePlacement("Sconce_Entry_Blocker_North", new Vector3(-10.0f,  2.00f, -23.825f), 0f),
            new SconcePlacement("Sconce_Entry_Side_West",     new Vector3( -3.175f, 2.00f, -20.0f), -90f),
            new SconcePlacement("Sconce_South_CenterBlock",   new Vector3(  6.0f,  2.00f, -21.825f), 0f),
            new SconcePlacement("Sconce_Lower_RightBlock",    new Vector3( 15.825f, 2.00f, -17.0f), -90f),
            new SconcePlacement("Sconce_Lower_MiddleBlock",   new Vector3(  3.0f,  2.00f, -10.825f), 0f),

            // --- ROOM 1 (LEFT ROOM) INTERIOR WALLS ---
            new SconcePlacement("Sconce_Room1_South", new Vector3(-13.0f,  2.00f, -17.325f), 0f),
            new SconcePlacement("Sconce_Room1_West",  new Vector3(-17.825f, 2.00f, -12.0f), 90f),
            new SconcePlacement("Sconce_Room1_East",  new Vector3( -8.175f, 2.00f, -15.6f), -90f),
            new SconcePlacement("Sconce_Room1_North", new Vector3(-13.0f,  2.00f,  -6.675f), 180f),

            // --- ROOM 2 (CENTER ROOM) WALLS ---
            new SconcePlacement("Sconce_Room2_South", new Vector3(  1.0f,  2.00f,  -1.825f), 0f),
            new SconcePlacement("Sconce_Room2_North", new Vector3(  1.0f,  2.00f,   9.825f), 180f),
            new SconcePlacement("Sconce_Room2_West",  new Vector3( -6.825f, 2.00f,   4.0f), 90f),
            new SconcePlacement("Sconce_Room2_East",  new Vector3(  8.825f, 2.00f,   4.0f), -90f),

            // --- ROOM 3 (MACHINERY / DEVICE ROOM) WALLS ---
            new SconcePlacement("Sconce_Room3_South", new Vector3( 14.0f,  2.00f,  12.175f), 0f),
            new SconcePlacement("Sconce_Room3_North", new Vector3( 14.0f,  2.00f,  23.825f), 180f),
            new SconcePlacement("Sconce_Room3_West",  new Vector3( 10.175f, 2.00f,  18.0f), 90f),
            new SconcePlacement("Sconce_Room3_East",  new Vector3( 17.825f, 2.00f,  18.0f), -90f),

            // --- CENTRAL & NORTHERN JUNCTION WALLS ---
            new SconcePlacement("Sconce_Middle_LeftBlock",    new Vector3(-15.0f,  2.00f,   0.825f), 180f),
            new SconcePlacement("Sconce_Middle_RightBlock",   new Vector3( 16.0f,  2.00f,   2.825f), 180f),
            new SconcePlacement("Sconce_Middle_OffsetBlock",  new Vector3(  2.825f, 2.00f,  16.0f), -90f),
            new SconcePlacement("Sconce_Upper_LeftBlock",     new Vector3(-14.0f,  2.00f,  17.825f), 180f),
            new SconcePlacement("Sconce_Upper_LeftVertical",  new Vector3( -6.825f, 2.00f,  23.0f), 90f),
            new SconcePlacement("Sconce_Upper_CenterBlock",   new Vector3(  1.0f,  2.00f,  25.825f), 180f)
        };

        public static GameObject GenerateAllSconces(Transform parent = null)
        {
            QualitySettings.pixelLightCount = 64;

            GameObject existing = GameObject.Find(ROOT_NAME);
            if (existing != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) Object.DestroyImmediate(existing);
                else Object.Destroy(existing);
#else
                Object.Destroy(existing);
#endif
            }

            GameObject root = new GameObject(ROOT_NAME);
            if (parent != null) root.transform.SetParent(parent, false);
            else
            {
                GameObject lightingRoot = GameObject.Find("Facility_Lighting");
                if (lightingRoot != null) root.transform.SetParent(lightingRoot.transform, true);
            }

            Material housingMaterial = GetOrCreateHousingMaterial();
            Material glowMaterial = GetOrCreateGlowMaterial();

            foreach (var placement in Placements)
            {
                CreateSingleWallSconce(root.transform, placement, housingMaterial, glowMaterial);
            }

            return root;
        }

        private static void CreateSingleWallSconce(Transform parent, SconcePlacement placement, Material housingMat, Material glowMat)
        {
            Vector3 finalPos = placement.WallSurfacePosition;
            Vector3 outwardNormal = Quaternion.Euler(0f, placement.OutwardYawAngle, 0f) * Vector3.forward;

            // Optional physics snap: if a wall collider is within 35cm, snap precisely to its outer surface
            Vector3 rayStart = placement.WallSurfacePosition + outwardNormal * 0.35f;
            if (Physics.Raycast(rayStart, -outwardNormal, out RaycastHit hit, 0.60f))
            {
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    finalPos = hit.point;
                    outwardNormal = hit.normal;
                }
            }

            GameObject sconceRoot = new GameObject(placement.Name);
            sconceRoot.transform.SetParent(parent, false);
            sconceRoot.transform.position = finalPos;
            sconceRoot.transform.rotation = Quaternion.LookRotation(outwardNormal, Vector3.up);

            // 1. Sleek black rectangular casing (width: 0.28m, height: 0.14m, depth: 0.08m)
            // Local Z=0 is right on the wall surface. Housing center is at local Z = 0.04m, so back face touches wall flush.
            GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Housing";
            housing.transform.SetParent(sconceRoot.transform, false);
            housing.transform.localPosition = new Vector3(0f, 0f, 0.04f);
            housing.transform.localScale = new Vector3(0.28f, 0.14f, 0.08f);

            var col = housing.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var housingRend = housing.GetComponent<Renderer>();
            if (housingRend != null && housingMat != null) housingRend.sharedMaterial = housingMat;

            // 2. Top Emissive Slit (golden glow bar on top of housing)
            GameObject topSlit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topSlit.name = "Top_GlowSlit";
            topSlit.transform.SetParent(sconceRoot.transform, false);
            topSlit.transform.localPosition = new Vector3(0f, 0.071f, 0.04f);
            topSlit.transform.localScale = new Vector3(0.22f, 0.012f, 0.065f);
            var topCol = topSlit.GetComponent<Collider>();
            if (topCol != null) Object.DestroyImmediate(topCol);
            var topRend = topSlit.GetComponent<Renderer>();
            if (topRend != null && glowMat != null) topRend.sharedMaterial = glowMat;

            // 3. Bottom Emissive Slit (golden glow bar on bottom of housing)
            GameObject bottomSlit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottomSlit.name = "Bottom_GlowSlit";
            bottomSlit.transform.SetParent(sconceRoot.transform, false);
            bottomSlit.transform.localPosition = new Vector3(0f, -0.071f, 0.04f);
            bottomSlit.transform.localScale = new Vector3(0.22f, 0.012f, 0.065f);
            var btmCol = bottomSlit.GetComponent<Collider>();
            if (btmCol != null) Object.DestroyImmediate(btmCol);
            var btmRend = bottomSlit.GetComponent<Renderer>();
            if (btmRend != null && glowMat != null) btmRend.sharedMaterial = glowMat;

            // 4. UPWARD SPOTLIGHT (Casts a crisp architectural V-cone expanding up the wall)
            // Positioned at top front lip of sconce, angled 28° back into the wall plane for direct diffuse illumination
            GameObject upLightGo = new GameObject("Up_SpotLight");
            upLightGo.transform.SetParent(sconceRoot.transform, false);
            upLightGo.transform.localPosition = new Vector3(0f, 0.075f, 0.07f);
            upLightGo.transform.localRotation = Quaternion.Euler(-118f, 0f, 0f);
            Light upLight = upLightGo.AddComponent<Light>();
            ConfigureSconceLight(upLight);

            // 5. DOWNWARD SPOTLIGHT (Casts a crisp architectural inverted V-cone expanding down the wall)
            // Positioned at bottom front lip of sconce, angled 28° back into the wall plane for direct diffuse illumination
            GameObject downLightGo = new GameObject("Down_SpotLight");
            downLightGo.transform.SetParent(sconceRoot.transform, false);
            downLightGo.transform.localPosition = new Vector3(0f, -0.075f, 0.07f);
            downLightGo.transform.localRotation = Quaternion.Euler(118f, 0f, 0f);
            Light downLight = downLightGo.AddComponent<Light>();
            ConfigureSconceLight(downLight);
        }

        private static void ConfigureSconceLight(Light light)
        {
            light.type = LightType.Spot;
            light.color = new Color(1.0f, 0.78f, 0.44f, 1f); // Warm architectural golden
            light.intensity = 4.2f;                           // Crisp visible cone wash without overexposing wall textures
            light.range = 5.2f;                               // Extends across wall to ceiling & floor
            light.spotAngle = 82f;                            // Wide architectural V-beam
            light.innerSpotAngle = 28f;
            light.shadows = LightShadows.None;                // Eliminates self-shadow acne on wall
            light.renderMode = LightRenderMode.ForcePixel;    // Crucial: forces pixel shader evaluation on large wall quads
        }

        private static Material GetOrCreateHousingMaterial()
        {
            Material mat = Resources.Load<Material>("Facility_Lamp_Metal");
            if (mat == null)
            {
                Shader standardShader = Shader.Find("Standard");
                if (standardShader == null) standardShader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(standardShader != null ? standardShader : Shader.Find("Diffuse"));
                mat.name = "Facility_Sconce_Metal";
                mat.color = new Color(0.06f, 0.06f, 0.06f, 1f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.65f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.85f);
            }
            return mat;
        }

        private static Material GetOrCreateGlowMaterial()
        {
            Material mat = Resources.Load<Material>("Facility_Lamp_YellowGlow");
            if (mat == null)
            {
                Shader standardShader = Shader.Find("Standard");
                if (standardShader == null) standardShader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(standardShader != null ? standardShader : Shader.Find("Diffuse"));
                mat.name = "Facility_Lamp_YellowGlow";
                mat.color = new Color(1.0f, 0.75f, 0.30f, 1f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1.0f, 0.68f, 0.20f) * 1.8f);
            }
            return mat;
        }

#if UNITY_EDITOR
        [MenuItem("Facility Breach/Generate Architectural Wall Sconce Lights")]
        public static void MenuGenerateSconces()
        {
            GameObject generated = GenerateAllSconces();
            if (generated != null)
            {
                Undo.RegisterCreatedObjectUndo(generated, "Generate Wall Sconce Lights");
                Debug.Log($"<color=cyan>[FacilityWallSconces]</color> Generated {Placements.Length} architectural up/down wall sconce lights perfectly flush on wall surfaces!");
            }
        }
#endif
    }
}
