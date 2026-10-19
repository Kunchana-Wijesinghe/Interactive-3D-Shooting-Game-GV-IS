using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using FacilityBreach;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated regression tests for the tactical-gloved first-person arms integration.
    /// Verifies asset integrity, PBR material properties, base arms prefab hierarchy,
    /// viewmodel prefab embedding across all three weapons, zero-collider enforcement,
    /// exact physical hand grip alignment to GripAttachment and SupportHand sockets,
    /// active finger curl postures, and compatibility with FpsViewModelObstruction.
    /// </summary>
    public static class FpsArmsTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/Run FPS Arms Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("<color=cyan>=== [FpsArmsTests] Starting Test Suite ===</color>");

            Test_AssetFilesExist();
            Test_MaterialConfiguration();
            Test_BaseArmsPrefab();
            Test_WeaponViewModels_ContainArms();
            Test_WeaponViewModels_ZeroColliders();
            Test_WeaponViewModels_KeepWeaponSockets();
            Test_ArmsGripAlignmentWithSockets();
            Test_FingerCurlPosture();
            Test_ObstructionCompatibilityWithArms();

            if (failedCount == 0)
            {
                Log($"<color=green>[FpsArmsTests] Completed {passedCount} checks: {passedCount} PASSED, 0 FAILED.</color>");
            }
            else
            {
                LogError($"<color=red>[FpsArmsTests] FAILED: {failedCount} failures out of {passedCount + failedCount} checks.</color>");
                foreach (var err in failureMessages)
                {
                    LogError("  - " + err);
                }
            }
        }

        public static void RunBatch()
        {
            RunAll();
            EditorApplication.Exit(failedCount == 0 ? 0 : 1);
        }

        private static void Assert(bool condition, string checkName, string details = "")
        {
            if (condition)
            {
                passedCount++;
                Log($"  [PASS] {checkName}");
            }
            else
            {
                failedCount++;
                string msg = $"FAIL: {checkName} - {details}";
                failureMessages.Add(msg);
                LogError($"  [FAIL] {checkName} ({details})");
            }
        }

        private static void Test_AssetFilesExist()
        {
            Assert(File.Exists(FpsArmsPrefabBuilder.FbxPath), "Arms FBX file exists on disk");
            Assert(File.Exists(FpsArmsPrefabBuilder.TacticalAlbedoPath), "Tactical glove albedo texture exists on disk");
            Assert(File.Exists(FpsArmsPrefabBuilder.NormalPath), "Arms normal map texture exists on disk");
            Assert(File.Exists(FpsArmsPrefabBuilder.AoPath), "Arms AO texture exists on disk");
            Assert(File.Exists(FpsArmsPrefabBuilder.RoughnessPath), "Arms roughness texture exists on disk");
            Assert(File.Exists(FpsArmsPrefabBuilder.MaterialPath), "Arms PBR material asset exists on disk");
            Assert(File.Exists(FpsArmsPrefabBuilder.ArmsPrefabPath), "Arms base prefab exists on disk");
            Assert(File.Exists("docs/individual/ATTRIBUTION_FPS_ARMS.md"), "CC-BY 4.0 arms attribution document exists");
        }

        private static void Test_MaterialConfiguration()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FpsArmsPrefabBuilder.MaterialPath);
            Assert(mat != null, "Arms material loads cleanly via AssetDatabase");
            if (mat != null)
            {
                Assert(mat.shader != null && mat.shader.name == "Standard", "Arms material utilizes Standard PBR shader");
                Assert(mat.mainTexture != null, "Arms material has main albedo texture assigned");
                Assert(mat.GetTexture("_BumpMap") != null, "Arms material has bump/normal map assigned");
                Assert(mat.IsKeywordEnabled("_NORMALMAP"), "Arms material has _NORMALMAP shader keyword enabled");
                Assert(mat.GetTexture("_OcclusionMap") != null, "Arms material has occlusion map assigned");
                Assert(mat.GetFloat("_Glossiness") > 0.1f && mat.GetFloat("_Glossiness") < 0.6f, "Arms material smoothness is within tactical matte range (0.1 - 0.6)");
            }
        }

        private static void Test_BaseArmsPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FpsArmsPrefabBuilder.ArmsPrefabPath);
            Assert(prefab != null, "Base arms prefab loads cleanly via AssetDatabase");
            if (prefab != null)
            {
                var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert(smr != null, "Base arms prefab contains a SkinnedMeshRenderer");
                if (smr != null)
                {
                    Assert(smr.sharedMesh != null && smr.sharedMesh.vertexCount > 1000, "Arms SkinnedMeshRenderer has valid high-detail mesh (>1000 verts)", $"verts: {smr.sharedMesh?.vertexCount}");
                    Assert(smr.sharedMaterial != null, "Arms SkinnedMeshRenderer has material assigned");
                    Assert(smr.rootBone != null, "Arms SkinnedMeshRenderer has rootBone assigned");
                }

                var cols = prefab.GetComponentsInChildren<Collider>(true);
                Assert(cols.Length == 0, "Base arms prefab has zero colliders");

                var cams = prefab.GetComponentsInChildren<Camera>(true);
                Assert(cams.Length == 0, "Base arms prefab has zero cameras");
            }
        }

        private static void Test_WeaponViewModels_ContainArms()
        {
            string[] weapons = { "Ammo1gun", "Ammo2gun", "Ammo3gun" };
            foreach (var weapon in weapons)
            {
                string path = $"Assets/Prefabs/FacilityBreach/Weapons/{weapon}_ViewModel.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert(prefab != null, $"Weapon viewmodel prefab exists for {weapon}");
                if (prefab != null)
                {
                    var armsChild = prefab.transform.Find("FPSArms");
                    Assert(armsChild != null, $"{weapon} contains 'FPSArms' child object");
                    if (armsChild != null)
                    {
                        var smr = armsChild.GetComponentInChildren<SkinnedMeshRenderer>(true);
                        Assert(smr != null, $"{weapon} FPSArms has a SkinnedMeshRenderer");
                        if (smr != null)
                        {
                            Assert(smr.sharedMaterial != null, $"{weapon} arms renderer has valid shared material");
                        }
                    }
                }
            }
        }

        private static void Test_WeaponViewModels_ZeroColliders()
        {
            string[] weapons = { "Ammo1gun", "Ammo2gun", "Ammo3gun" };
            foreach (var weapon in weapons)
            {
                string path = $"Assets/Prefabs/FacilityBreach/Weapons/{weapon}_ViewModel.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var cols = prefab.GetComponentsInChildren<Collider>(true);
                    Assert(cols.Length == 0, $"{weapon} viewmodel has strictly 0 colliders across all children (count={cols.Length})");
                }
            }
        }

        private static void Test_WeaponViewModels_KeepWeaponSockets()
        {
            string[] weapons = { "Ammo1gun", "Ammo2gun", "Ammo3gun" };
            foreach (var weapon in weapons)
            {
                string path = $"Assets/Prefabs/FacilityBreach/Weapons/{weapon}_ViewModel.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    Transform grip = null;
                    Transform support = null;
                    Transform muzzle = null;
                    foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "GripAttachment") grip = t;
                        else if (t.name == "SupportHand") support = t;
                        else if (t.name == "Muzzle") muzzle = t;
                    }

                    Assert(grip != null, $"{weapon} retains GripAttachment socket");
                    Assert(support != null, $"{weapon} retains SupportHand socket");
                    Assert(muzzle != null, $"{weapon} retains Muzzle socket");
                }
            }
        }

        private static void Test_ArmsGripAlignmentWithSockets()
        {
            string[] weapons = { "Ammo1gun", "Ammo2gun", "Ammo3gun" };
            foreach (var weapon in weapons)
            {
                string path = $"Assets/Prefabs/FacilityBreach/Weapons/{weapon}_ViewModel.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    try
                    {
                        Transform grip = null;
                        Transform support = null;
                        Transform rWrist = null;
                        Transform rMiddle = null;
                        Transform lWrist = null;
                        Transform lMiddle = null;

                        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == "GripAttachment") grip = t;
                            else if (t.name == "SupportHand") support = t;
                            else if (t.name == "R_wrist") rWrist = t;
                            else if (t.name == "R_middle1") rMiddle = t;
                            else if (t.name == "L_wrist") lWrist = t;
                            else if (t.name == "L_middle1") lMiddle = t;
                        }

                        Assert(grip != null && rWrist != null, $"{weapon} has both GripAttachment and R_wrist present");
                        Assert(support != null && lWrist != null, $"{weapon} has both SupportHand and L_wrist present");

                        if (grip != null && rWrist != null)
                        {
                            float rWristDist = Vector3.Distance(grip.position, rWrist.position);
                            Assert(rWristDist < 0.10f, $"{weapon} right wrist is aligned with GripAttachment (dist={rWristDist:F3}m < 0.10m)");
                        }

                        if (grip != null && rMiddle != null)
                        {
                            float rMiddleDist = Vector3.Distance(grip.position, rMiddle.position);
                            Assert(rMiddleDist < 0.08f, $"{weapon} right middle knuckle contacts pistol grip (dist={rMiddleDist:F3}m < 0.08m)");
                        }

                        if (support != null && lWrist != null)
                        {
                            float lWristDist = Vector3.Distance(support.position, lWrist.position);
                            Assert(lWristDist < 0.12f, $"{weapon} left wrist is aligned with SupportHand (dist={lWristDist:F3}m < 0.12m)");
                        }

                        if (support != null && lMiddle != null)
                        {
                            float lMiddleDist = Vector3.Distance(support.position, lMiddle.position);
                            Assert(lMiddleDist < 0.10f, $"{weapon} left middle knuckle cups handguard (dist={lMiddleDist:F3}m < 0.10m)");
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        private static void Test_FingerCurlPosture()
        {
            string[] weapons = { "Ammo1gun", "Ammo2gun", "Ammo3gun" };
            foreach (var weapon in weapons)
            {
                string path = $"Assets/Prefabs/FacilityBreach/Weapons/{weapon}_ViewModel.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    try
                    {
                        Transform rMiddle2 = null;
                        Transform lMiddle2 = null;

                        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name == "R_middle2") rMiddle2 = t;
                            else if (t.name == "L_middle2") lMiddle2 = t;
                        }

                        Assert(rMiddle2 != null, $"{weapon} contains R_middle2 finger joint");
                        Assert(lMiddle2 != null, $"{weapon} contains L_middle2 finger joint");

                        if (rMiddle2 != null)
                        {
                            // Finger should have an active curl rotation applied (> 30 degrees)
                            float angle = Quaternion.Angle(Quaternion.identity, rMiddle2.localRotation);
                            Assert(angle > 30f, $"{weapon} right middle finger is actively curled around grip (angle={angle:F1}° > 30°)");
                        }

                        if (lMiddle2 != null)
                        {
                            float angle = Quaternion.Angle(Quaternion.identity, lMiddle2.localRotation);
                            Assert(angle > 30f, $"{weapon} left middle finger is actively curled around handguard (angle={angle:F1}° > 30°)");
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        private static void Test_ObstructionCompatibilityWithArms()
        {
            var camObj = new GameObject("TestCamera");
            camObj.transform.position = Vector3.zero;
            camObj.transform.rotation = Quaternion.identity;
            var cam = camObj.AddComponent<Camera>();

            string path = "Assets/Prefabs/FacilityBreach/Weapons/Ammo1gun_ViewModel.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var weaponInstance = UnityEngine.Object.Instantiate(prefab, camObj.transform);
            weaponInstance.transform.localPosition = new Vector3(0.18f, -0.22f, 0.5f);
            weaponInstance.transform.localScale = Vector3.one * 0.7f;

            Transform muzzle = null;
            foreach (var t in weaponInstance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Muzzle") muzzle = t;
            }

            var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
            obstruction.Initialize(cam, null, weaponInstance.transform, muzzle);

            // Verify initial baseline pose includes arms
            Assert(Vector3.Distance(weaponInstance.transform.localPosition, new Vector3(0.18f, -0.22f, 0.5f)) < 0.001f, "Arms viewmodel initializes cleanly at standard FPS pose");

            // Verify obstruction retraction moves both gun and arms
            var arms = weaponInstance.transform.Find("FPSArms");
            Assert(arms != null, "FPSArms child is attached to weapon viewmodel during obstruction initialization");

            Vector3 initialArmsWorld = arms.position;

            // Spawn obstacle in front
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 0f, 0.4f);
            wall.transform.localScale = new Vector3(2f, 2f, 0.1f);
            Physics.SyncTransforms();

            bool hitFound = obstruction.CheckObstruction(out var hit, out float distance);
            Assert(hitFound, "Obstruction query detects obstacle with arms viewmodel equipped");

            // Evaluate procedural retraction
            obstruction.CalculateTargetPose(distance, out var targetPos, out var targetRot);
            Assert(targetPos.z < obstruction.DefaultLocalPosition.z, "Obstruction retracts viewmodel (and arms) backward", $"targetZ={targetPos.z}, defaultZ={obstruction.DefaultLocalPosition.z}");

            UnityEngine.Object.DestroyImmediate(camObj);
            UnityEngine.Object.DestroyImmediate(wall);
        }
    }
}
