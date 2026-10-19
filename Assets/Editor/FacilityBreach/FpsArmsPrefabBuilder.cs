using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Builder utility for configuring the tactical-gloved first person arms asset,
    /// generating the arms material and prefab, and embedding the arms visual into the
    /// 3 weapon viewmodels with natural two-handed grip pose aligned to GripAttachment
    /// and SupportHand sockets.
    /// </summary>
    public static class FpsArmsPrefabBuilder
    {
        public const string FbxPath = "Assets/Models/FacilityBreach/Arms/fpsarms.fbx";
        public const string TacticalAlbedoPath = "Assets/Textures/FacilityBreach/Arms/armColor_TacticalGloves.png";
        public const string NormalPath = "Assets/Textures/FacilityBreach/Arms/armnormal.png";
        public const string AoPath = "Assets/Textures/FacilityBreach/Arms/armAO.png";
        public const string RoughnessPath = "Assets/Textures/FacilityBreach/Arms/armRoughness.png";
        public const string MaterialPath = "Assets/Materials/FacilityBreach/Arms/FPSArms_Mat.mat";
        public const string PrefabDir = "Assets/Prefabs/FacilityBreach/Arms";
        public const string ArmsPrefabPath = "Assets/Prefabs/FacilityBreach/Arms/FPSArms_ViewModel.prefab";
        public const string PreviewImagePath = "Assets/_Recovery/fps_arms_game_preview.png";

        [MenuItem("Facility Breach/Build Tactical Arms and ViewModels")]
        public static void BuildAll()
        {
            try
            {
                Debug.Log("=== [FpsArmsPrefabBuilder] Starting BuildAll ===");
                EnsureDirectories();
                ConfigureImporters();
                var mat = CreateOrUpdateMaterial();
                var armsPrefab = CreateBaseArmsPrefab(mat);
                IntegrateArmsIntoWeaponViewModels(armsPrefab);
                CapturePreview(PreviewImagePath);
                Debug.Log("<color=green>=== [FpsArmsPrefabBuilder] BuildAll Completed Successfully! ===</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FpsArmsPrefabBuilder] BuildAll failed: {ex}");
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        [MenuItem("Facility Breach/Log Rest Pose")]
        public static void LogRestPose()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var inst = UnityEngine.Object.Instantiate(fbx);
            try
            {
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "chest" || t.name.StartsWith("R_") || t.name.StartsWith("L_"))
                    {
                        Debug.Log($"[RestPose] {t.name}: parent={t.parent?.name}, localPos={t.localPosition}, localEuler={t.localEulerAngles}, worldPos={t.position}");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
            EditorApplication.Exit(0);
        }

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/Models/FacilityBreach/Arms");
            Directory.CreateDirectory("Assets/Textures/FacilityBreach/Arms");
            Directory.CreateDirectory("Assets/Materials/FacilityBreach/Arms");
            Directory.CreateDirectory("Assets/Prefabs/FacilityBreach/Arms");
            Directory.CreateDirectory("Assets/_Recovery");
            AssetDatabase.Refresh();
        }

        public static void ConfigureImporters()
        {
            // Configure Normal Map
            var normalImporter = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (normalImporter != null)
            {
                bool dirty = false;
                if (normalImporter.textureType != TextureImporterType.NormalMap)
                {
                    normalImporter.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
                if (normalImporter.sRGBTexture)
                {
                    normalImporter.sRGBTexture = false;
                    dirty = true;
                }
                if (dirty)
                {
                    normalImporter.SaveAndReimport();
                    Debug.Log("[FpsArmsPrefabBuilder] Reimported armnormal.png as NormalMap");
                }
            }

            // Configure FBX Importer
            var fbxImporter = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (fbxImporter != null)
            {
                bool dirty = false;
                if (fbxImporter.materialImportMode != ModelImporterMaterialImportMode.None)
                {
                    fbxImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                    dirty = true;
                }
                if (fbxImporter.animationType != ModelImporterAnimationType.Generic)
                {
                    fbxImporter.animationType = ModelImporterAnimationType.Generic;
                    dirty = true;
                }
                if (fbxImporter.importAnimation)
                {
                    fbxImporter.importAnimation = false;
                    dirty = true;
                }
                if (dirty)
                {
                    fbxImporter.SaveAndReimport();
                    Debug.Log("[FpsArmsPrefabBuilder] Reimported fpsarms.fbx with generic rig, no animation");
                }
            }
        }

        public static Material CreateOrUpdateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!mat)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TacticalAlbedoPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(AoPath);

            mat.shader = Shader.Find("Standard");
            if (albedo) mat.SetTexture("_MainTex", albedo);
            if (normal)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (ao) mat.SetTexture("_OcclusionMap", ao);

            mat.color = Color.white;
            mat.SetFloat("_Glossiness", 0.30f);
            mat.SetFloat("_Metallic", 0.05f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FpsArmsPrefabBuilder] Configured tactical arms material at {MaterialPath}");
            return mat;
        }

        public static GameObject CreateBaseArmsPrefab(Material mat)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (!fbx) throw new FileNotFoundException("Cannot find FBX at " + FbxPath);

            var root = new GameObject("FPSArms_ViewModel");
            try
            {
                var armsInstance = UnityEngine.Object.Instantiate(fbx, root.transform);
                armsInstance.name = "ArmsRig";

                // Strip any colliders or cameras that might exist
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(c);
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    UnityEngine.Object.DestroyImmediate(cam);

                // Assign material to all renderers
                foreach (var r in armsInstance.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = mat;
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, ArmsPrefabPath);
                Debug.Log($"[FpsArmsPrefabBuilder] Created base arms prefab at {ArmsPrefabPath}");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static readonly Dictionary<string, Vector3> BaseVisualPositions = new Dictionary<string, Vector3>()
        {
            { "Ammo1gun", new Vector3(0f, 0.13100775f, 0.12093023f) },
            { "Ammo2gun", new Vector3(0f, 0.09898477f, 0.09137056f) },
            { "Ammo3gun", new Vector3(0f, 0.09822221f, 0.09066666f) }
        };

        /// <summary>
        /// Dedicated configurable visual offset that moves the viewmodel slightly lower and slightly more centered
        /// toward lower-center-right to provide a standard FPS weapon pose while leaving the center crosshair clear.
        /// Applied at the prefab level to ModelVisual so the gun and hands remain perfectly aligned.
        /// </summary>
        public static readonly Vector3 ViewModelFramingOffset = new Vector3(0.010f, -0.050f, 0.000f);

        public static void IntegrateArmsIntoWeaponViewModels(GameObject baseArmsPrefab)
        {
            string[] weapons = { "Ammo1gun", "Ammo2gun", "Ammo3gun" };

            for (int i = 0; i < weapons.Length; i++)
            {
                string weaponName = weapons[i];
                string prefabPath = $"Assets/Prefabs/FacilityBreach/Weapons/{weaponName}_ViewModel.prefab";
                var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!weaponPrefab)
                {
                    Debug.LogWarning($"[FpsArmsPrefabBuilder] Cannot find weapon prefab: {prefabPath}");
                    continue;
                }

                var weaponInstance = UnityEngine.Object.Instantiate(weaponPrefab);
                try
                {
                    // Clean any existing FPSArms child first
                    var existingArms = weaponInstance.transform.Find("FPSArms");
                    if (existingArms) UnityEngine.Object.DestroyImmediate(existingArms.gameObject);

                    // Reset and apply dedicated framing offset to ModelVisual
                    var modelVisual = weaponInstance.transform.Find("ModelVisual");
                    if (modelVisual != null && BaseVisualPositions.TryGetValue(weaponName, out var basePos))
                    {
                        modelVisual.localPosition = basePos + ViewModelFramingOffset;
                    }

                    // Find GripAttachment and SupportHand sockets
                    Transform gripSocket = null;
                    Transform supportSocket = null;
                    foreach (var t in weaponInstance.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "GripAttachment") gripSocket = t;
                        else if (t.name == "SupportHand") supportSocket = t;
                    }

                    Vector3 gripPos = gripSocket ? weaponInstance.transform.InverseTransformPoint(gripSocket.position) : Vector3.zero;
                    Vector3 supportPos = supportSocket ? weaponInstance.transform.InverseTransformPoint(supportSocket.position) : new Vector3(0f, 0.08f, 0.26f);

                    Debug.Log($"[FpsArmsPrefabBuilder] {weaponName} gripLocal={gripPos}, supportLocal={supportPos}");

                    // Instantiate arms prefab under weapon root
                    var armsObj = UnityEngine.Object.Instantiate(baseArmsPrefab, weaponInstance.transform);
                    armsObj.name = "FPSArms";

                    // Pose the arms and fingers naturally for the two-handed weapon grip
                    PoseArmsForWeapon(armsObj.transform, gripPos, supportPos);

                    // Ensure strictly zero colliders exist across the entire viewmodel hierarchy
                    foreach (var col in weaponInstance.GetComponentsInChildren<Collider>(true))
                    {
                        UnityEngine.Object.DestroyImmediate(col);
                    }

                    // Save updated prefab
                    PrefabUtility.SaveAsPrefabAsset(weaponInstance, prefabPath);
                    Debug.Log($"[FpsArmsPrefabBuilder] Successfully integrated posed FPSArms into {prefabPath}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(weaponInstance);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Applies realistic tactical two-handed rifle grip pose:
        /// Upper arms / shoulders positioned cleanly off-screen below camera view.
        /// Right hand wraps securely around the pistol grip with forearm entering from lower-right screen edge
        /// and index finger in a neutral trigger-ready pose.
        /// Left hand cups and supports the forward handguard under SupportHand with forearm entering from lower-left.
        /// </summary>
        public static void PoseArmsForWeapon(Transform armsRoot, Vector3 gripLocalPos, Vector3 supportLocalPos)
        {
            // Position arms root so shoulders are cleanly off-screen below camera view
            // and arms have natural reach to both grip and forward support handguard
            armsRoot.localPosition = new Vector3(gripLocalPos.x + 0.020f, gripLocalPos.y - 0.135f, gripLocalPos.z + 0.060f);
            armsRoot.localRotation = Quaternion.identity;
            armsRoot.localScale = Vector3.one;

            var bones = new Dictionary<string, Transform>();
            foreach (var t in armsRoot.GetComponentsInChildren<Transform>(true))
            {
                bones[t.name] = t;
            }

            // 1. Torso bladed rifle stance (22 deg yaw, slight forward pitch)
            if (bones.TryGetValue("chest", out var chest))
            {
                chest.localRotation = chest.localRotation * Quaternion.Euler(4.0f, 22.0f, -2.0f);
            }

            // 2. Wrists and fingers setup
            // Right wrist: angled so palm contacts pistol grip, back of hand visible to camera
            if (bones.TryGetValue("R_wrist", out var rWrist))
            {
                rWrist.localRotation = rWrist.localRotation * Quaternion.Euler(15f, -32f, -28f);
            }
            // Left wrist: angled so palm cups underside of forward handguard
            if (bones.TryGetValue("L_wrist", out var lWrist))
            {
                lWrist.localRotation = lWrist.localRotation * Quaternion.Euler(35f, 25f, 40f);
            }

            // Fingers: curl for natural grip
            CurlFinger(bones, "R", "middle", 45f, 70f, 55f, 30f);
            CurlFinger(bones, "R", "ring",   45f, 70f, 55f, 30f);
            CurlFinger(bones, "R", "pink",   45f, 70f, 55f, 30f);
            CurlFinger(bones, "R", "point",  8f, 12f, 10f, 5f); // index finger in neutral trigger-ready pose along frame
            CurlFinger(bones, "R", "thumb",  25f, 40f, 35f, 20f); // thumb wraps opposite side

            // Left hand fingers cupping handguard
            CurlFinger(bones, "L", "point",  40f, 65f, 50f, 25f);
            CurlFinger(bones, "L", "middle", 40f, 65f, 50f, 25f);
            CurlFinger(bones, "L", "ring",   40f, 65f, 50f, 25f);
            CurlFinger(bones, "L", "pink",   40f, 65f, 50f, 25f);
            CurlFinger(bones, "L", "thumb",  15f, 25f, 25f, 15f);

            // 3. Solve Arm IK for exact hand placement and entry angles
            if (bones.TryGetValue("R_arm", out var rArm) &&
                bones.TryGetValue("R_elbow", out var rElbow) &&
                bones.TryGetValue("R_wrist", out rWrist) &&
                bones.TryGetValue("R_middle1", out var rMiddle))
            {
                Vector3 targetRWrist = gripLocalPos + new Vector3(0.040f, -0.015f, -0.020f);
                Vector3 targetRMiddle = gripLocalPos + new Vector3(0.015f, -0.010f, 0.015f);
                SolveArmIK(rArm, rElbow, rWrist, rMiddle, targetRWrist, targetRMiddle, true);
            }

            if (bones.TryGetValue("L_arm", out var lArm) &&
                bones.TryGetValue("L_elbow", out var lElbow) &&
                bones.TryGetValue("L_wrist", out lWrist) &&
                bones.TryGetValue("L_middle1", out var lMiddle))
            {
                Vector3 targetLWrist = supportLocalPos + new Vector3(-0.015f, -0.040f, -0.010f);
                Vector3 targetLMiddle = supportLocalPos + new Vector3(0.015f, -0.015f, 0.015f);
                SolveArmIK(lArm, lElbow, lWrist, lMiddle, targetLWrist, targetLMiddle, false);
            }
        }

        private static void SolveArmIK(
            Transform arm, Transform elbow,
            Transform wrist, Transform middle,
            Vector3 targetWrist, Vector3 targetMiddle,
            bool isRightArm)
        {
            Quaternion restArm = arm.localRotation;
            Quaternion restElbow = elbow.localRotation;

            Vector3 bestArmEuler = Vector3.zero;
            Vector3 bestElbowEuler = Vector3.zero;

            float EvaluateScore(Vector3 aEuler, Vector3 eEuler)
            {
                arm.localRotation = restArm * Quaternion.Euler(aEuler);
                elbow.localRotation = restElbow * Quaternion.Euler(eEuler);

                float dW = Vector3.Distance(wrist.position, targetWrist);
                float dM = Vector3.Distance(middle.position, targetMiddle);
                float score = dW * 2.5f + dM;

                // Enforce proper forearm entry angle:
                // Right elbow must enter from lower-right (+X, -Y relative to wrist)
                // Left elbow must enter from lower-left (-X, -Y relative to wrist)
                if (isRightArm)
                {
                    if (elbow.position.x < wrist.position.x + 0.06f)
                        score += 15f * (wrist.position.x + 0.06f - elbow.position.x);
                    if (elbow.position.y > wrist.position.y)
                        score += 8f * (elbow.position.y - wrist.position.y);
                }
                else
                {
                    if (elbow.position.x > wrist.position.x - 0.05f)
                        score += 15f * (elbow.position.x - (wrist.position.x - 0.05f));
                    if (elbow.position.y > wrist.position.y)
                        score += 8f * (elbow.position.y - wrist.position.y);
                }

                return score;
            }

            float currentScore = EvaluateScore(bestArmEuler, bestElbowEuler);
            float[] stepSizes = { 15f, 8f, 4f, 2f, 1f, 0.5f };

            foreach (var step in stepSizes)
            {
                for (int iter = 0; iter < 35; iter++)
                {
                    bool improved = false;
                    // Test arm angles
                    for (int axis = 0; axis < 3; axis++)
                    {
                        foreach (float dir in new float[] { 1f, -1f })
                        {
                            Vector3 cand = bestArmEuler;
                            if (axis == 0) cand.x += dir * step;
                            else if (axis == 1) cand.y += dir * step;
                            else cand.z += dir * step;

                            float score = EvaluateScore(cand, bestElbowEuler);
                            if (score < currentScore)
                            {
                                currentScore = score;
                                bestArmEuler = cand;
                                improved = true;
                            }
                        }
                    }

                    // Test elbow angles
                    for (int axis = 0; axis < 3; axis++)
                    {
                        foreach (float dir in new float[] { 1f, -1f })
                        {
                            Vector3 cand = bestElbowEuler;
                            if (axis == 0) cand.x += dir * step;
                            else if (axis == 1) cand.y += dir * step;
                            else cand.z += dir * step;

                            float score = EvaluateScore(bestArmEuler, cand);
                            if (score < currentScore)
                            {
                                currentScore = score;
                                bestElbowEuler = cand;
                                improved = true;
                            }
                        }
                    }

                    if (!improved) break;
                }
            }

            arm.localRotation = restArm * Quaternion.Euler(bestArmEuler);
            elbow.localRotation = restElbow * Quaternion.Euler(bestElbowEuler);
            Debug.Log($"[FpsArmsPrefabBuilder] Solved {(isRightArm ? "Right" : "Left")} Arm: armEuler={bestArmEuler}, elbowEuler={bestElbowEuler}, score={currentScore:F4}, wristDist={Vector3.Distance(wrist.position, targetWrist):F3}m, middleDist={Vector3.Distance(middle.position, targetMiddle):F3}m");
        }

        private static void CurlFinger(Dictionary<string, Transform> bones, string side, string finger, float j1, float j2, float j3, float j4)
        {
            float[] angles = { j1, j2, j3, j4 };
            for (int i = 1; i <= 4; i++)
            {
                string name = $"{side}_{finger}{i}";
                if (bones.TryGetValue(name, out var t))
                {
                    t.localRotation = t.localRotation * Quaternion.Euler(angles[i - 1], 0f, 0f);
                }
            }
        }

        /// <summary>
        /// Renders an in-engine snapshot matching the gameplay camera and viewmodel settings,
        /// writing a verification image to disk for automated and visual validation.
        /// </summary>
        public static void CapturePreview(string outputPath)
        {
            var camObj = new GameObject("PreviewCam");
            try
            {
                var cam = camObj.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.aspect = 16f / 9f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 100f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

                string prefabPath = "Assets/Prefabs/FacilityBreach/Weapons/Ammo1gun_ViewModel.prefab";
                var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!weaponPrefab) return;

                var weaponInstance = UnityEngine.Object.Instantiate(weaponPrefab, camObj.transform);
                weaponInstance.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);
                weaponInstance.transform.localScale = Vector3.one * 0.7f;
                weaponInstance.transform.localRotation = Quaternion.identity;

                var lightObj = new GameObject("KeyLight");
                try
                {
                    var light = lightObj.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.2f;
                    light.color = Color.white;
                    lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

                    var rt = new RenderTexture(960, 540, 24);
                    cam.targetTexture = rt;

                    foreach (var t in weaponInstance.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "chest" || t.name == "R_arm" || t.name == "R_elbow" || t.name == "R_wrist" ||
                            t.name == "L_arm" || t.name == "L_elbow" || t.name == "L_wrist" ||
                            t.name == "GripAttachment" || t.name == "SupportHand" || t.name == "Muzzle")
                        {
                            Vector3 vp = cam.WorldToViewportPoint(t.position);
                            Vector3 camLocal = cam.transform.InverseTransformPoint(t.position);
                            Debug.Log($"[BoneDebug] {t.name}: viewport=({vp.x:F3}, {vp.y:F3}, z={vp.z:F3}), camLocal=({camLocal.x:F3}, {camLocal.y:F3}, {camLocal.z:F3})");
                        }
                    }

                    cam.Render();

                    RenderTexture.active = rt;
                    var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);

                    // Overlay HUD center crosshair at screen center (480, 270)
                    int cx = 480;
                    int cy = 270;
                    Color ch = new Color(0.2f, 1.0f, 0.4f, 1.0f); // crisp tactical green
                    for (int dx = -14; dx <= 14; dx++)
                    {
                        if (Mathf.Abs(dx) >= 3)
                        {
                            tex.SetPixel(cx + dx, cy, ch);
                            tex.SetPixel(cx + dx, cy + 1, ch);
                        }
                    }
                    for (int dy = -14; dy <= 14; dy++)
                    {
                        if (Mathf.Abs(dy) >= 3)
                        {
                            tex.SetPixel(cx, cy + dy, ch);
                            tex.SetPixel(cx + 1, cy + dy, ch);
                        }
                    }
                    tex.SetPixel(cx, cy, ch);
                    tex.Apply();

                    string dir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllBytes(outputPath, tex.EncodeToPNG());

                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(rt);
                    UnityEngine.Object.DestroyImmediate(tex);

                    Debug.Log($"[FpsArmsPrefabBuilder] In-engine camera preview saved to {outputPath}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(lightObj);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camObj);
            }
        }
    }
}
