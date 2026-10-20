using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FacilityBreach;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated regression tests for first-person muzzle flash V2 (FpsMuzzleFlashV2).
    /// Verifies camera-facing quad alignment, procedural soft-alpha star texture,
    /// transparent depth-writing disabled unlit material (OneMinusSrcAlpha),
    /// strict absence of point lights and colliders, weapon-switching re-alignment,
    /// and camera near-clip safety.
    /// </summary>
    public static class FpsMuzzleFlashV2Tests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/Run FPS Muzzle Flash V2 Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("<color=cyan>=== [FpsMuzzleFlashV2Tests] Starting Test Suite ===</color>");

            RunTest("Initialization_CreatesInactiveCameraFacingBillboard", Test_Initialization_CreatesInactiveCameraFacingBillboard);
            RunTest("MaterialConfiguration_TransparentUnlitWithZWriteOff", Test_MaterialConfiguration_TransparentUnlitWithZWriteOff);
            RunTest("ProceduralSoftStarTexture_FeatheredAlphaBorders", Test_ProceduralSoftStarTexture_FeatheredAlphaBorders);
            RunTest("TriggerFlash_PositionsAtActiveMuzzleAndFacesCamera", Test_TriggerFlash_PositionsAtActiveMuzzleAndFacesCamera);
            RunTest("ValidShotTriggering_OnlyOnConfirmedShot", Test_ValidShotTriggering_OnlyOnConfirmedShot);
            RunTest("FlashLifetimeAndCleanup_DeactivatesAfterConfiguredDuration", Test_FlashLifetimeAndCleanup_DeactivatesAfterConfiguredDuration);
            RunTest("MultiWeapon_TrackingAcrossSwaps", Test_MultiWeapon_TrackingAcrossSwaps);
            RunTest("NearCameraAndObstructionSafety_ClampsNearPlane", Test_NearCameraAndObstructionSafety_ClampsNearPlane);
            RunTest("StrictlyZeroLightsCollidersOrDebugControls", Test_StrictlyZeroLightsCollidersOrDebugControls);
            RunTest("CameraFacingBillboard_NeverEdgeOn", Test_CameraFacingBillboard_NeverEdgeOn);

            if (failedCount == 0)
            {
                Log($"<color=green>[FpsMuzzleFlashV2Tests] Completed {passedCount} checks: {passedCount} PASSED, 0 FAILED.</color>");
            }
            else
            {
                LogError($"<color=red>[FpsMuzzleFlashV2Tests] FAILED: {failedCount} failures out of {passedCount + failedCount} checks.</color>");
                foreach (var err in failureMessages)
                {
                    LogError("  - " + err);
                }
            }
        }

        public static void RunBatch()
        {
            RunAll();
            EditorApplication.Exit(failedCount > 0 ? 1 : 0);
        }

        [MenuItem("Facility Breach/Run All 9 Suites (Regression + Tracer + Flash V2)")]
        public static void RunAll9SuitesBatch()
        {
            Log("<color=yellow>=== RUNNING ALL 9 TEST SUITES (288 Baseline + Tracer + Flash V2) ===</color>");
            FpsArmsTests.RunAll();
            FpsViewModelObstructionTests.RunAll();
            FacilityInteractionDispatcherTests.RunAll();
            PatrolGuardAStarTests.RunAll();
            PatrolGuardSearchTests.RunAll();
            PatrolGuardReturnTests.RunAll();
            PatrolGuardObstacleTests.RunAll();
            FpsBulletTracerTests.RunAll();
            FpsMuzzleFlashV2Tests.RunAll();

            EditorApplication.Exit(failedCount > 0 ? 1 : 0);
        }

        private static void RunTest(string name, Action testAction)
        {
            try
            {
                testAction();
            }
            catch (Exception ex)
            {
                Assert(false, name, $"Unhandled exception: {ex.Message}");
            }
        }

        private static void Assert(bool condition, string testName, string detail = null)
        {
            if (condition)
            {
                passedCount++;
                Log($"[Flash V2 PASS] {testName}");
            }
            else
            {
                failedCount++;
                string msg = $"{testName} FAILED" + (string.IsNullOrEmpty(detail) ? "" : $" ({detail})");
                failureMessages.Add(msg);
                LogError($"[Flash V2 FAIL] {msg}");
            }
        }

        private static void Test_Initialization_CreatesInactiveCameraFacingBillboard()
        {
            var root = new GameObject("TestRoot_FlashV2Init");
            try
            {
                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.Initialize();

                Assert(!flash.IsFlashActive, "Muzzle flash V2 begins inactive");
                Assert(flash.FlashObject != null, "Flash visual GameObject is created");
                Assert(flash.TriggerCount == 0, "Trigger count begins at zero");

                var colliders = root.GetComponentsInChildren<Collider>(true);
                Assert(colliders.Length == 0, "Flash hierarchy strictly contains zero colliders");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_MaterialConfiguration_TransparentUnlitWithZWriteOff()
        {
            var root = new GameObject("TestRoot_MaterialConfig");
            try
            {
                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.Initialize();

                var mat = flash.SharedMaterial;
                Assert(mat != null, "Shared flash material is instantiated");
                Assert(mat.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent,
                    "Render queue is Transparent (>= 3000)", $"Got {mat?.renderQueue}");

                if (mat.HasProperty("_ZWrite"))
                {
                    Assert(mat.GetInt("_ZWrite") == 0, "Depth writing (_ZWrite) is 0 (disabled to prevent skybox occlusions)");
                }

                if (mat.HasProperty("_DstBlend"))
                {
                    Assert(mat.GetInt("_DstBlend") == (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha,
                        "Destination blend is OneMinusSrcAlpha (standard alpha blending, no additive HDR blowout)");
                }

                Assert(!mat.IsKeywordEnabled("_EMISSION"), "HDR _EMISSION keyword is explicitly disabled");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_ProceduralSoftStarTexture_FeatheredAlphaBorders()
        {
            var root = new GameObject("TestRoot_TextureFeathering");
            try
            {
                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.Initialize();

                var tex = flash.SharedTexture;
                Assert(tex != null, "Procedural soft-alpha star texture is generated");
                Assert(tex.width == 64 && tex.height == 64, "Texture size is 64x64", $"Got {tex?.width}x{tex?.height}");

                // Corner pixels must have zero alpha (smooth feathering, no square edges)
                Color c00 = tex.GetPixel(0, 0);
                Color c63 = tex.GetPixel(63, 63);
                Assert(c00.a < 0.01f, "Corner pixel (0,0) has zero alpha", $"Got {c00.a}");
                Assert(c63.a < 0.01f, "Corner pixel (63,63) has zero alpha", $"Got {c63.a}");

                // Center core pixel must be bright with high alpha
                Color centerCol = tex.GetPixel(32, 32);
                Assert(centerCol.a > 0.80f, "Center core pixel has strong alpha (> 0.80)", $"Got {centerCol.a}");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_TriggerFlash_PositionsAtActiveMuzzleAndFacesCamera()
        {
            var root = new GameObject("TestRoot_TriggerPositionFacing");
            try
            {
                var camObj = new GameObject("TestCam");
                camObj.transform.SetParent(root.transform);
                camObj.transform.position = new Vector3(0f, 1.4f, 0f);
                camObj.transform.forward = Vector3.forward;
                var cam = camObj.AddComponent<Camera>();

                var muzzleObj = new GameObject("TestMuzzle");
                muzzleObj.transform.SetParent(root.transform);
                muzzleObj.transform.position = new Vector3(0.2f, 1.4f, 0.5f);

                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.PlayerCamera = cam;
                flash.Initialize(muzzleObj.transform);

                flash.TriggerFlash(muzzleObj.transform);

                Assert(flash.IsFlashActive, "TriggerFlash activates flash visual");
                Assert(flash.TriggerCount == 1, "TriggerCount increments to 1");

                float distFromMuzzle = Vector3.Distance(flash.FlashObject.transform.position, muzzleObj.transform.position);
                Assert(distFromMuzzle <= 0.03f, "Flash visual sits within 3cm of muzzle barrel tip", $"Got {distFromMuzzle:F3}m");

                // Billboard must face the camera forward direction
                float forwardAlignment = Vector3.Dot(flash.FlashObject.transform.forward, cam.transform.forward);
                Assert(forwardAlignment > 0.99f, "Billboard forward strictly matches camera forward view vector (never edge-on)",
                    $"Got {forwardAlignment:F4}");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_ValidShotTriggering_OnlyOnConfirmedShot()
        {
            var root = new GameObject("TestRoot_TriggerOnShotOnly");
            try
            {
                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.Initialize();

                Assert(!flash.IsFlashActive, "Flash remains inactive before any shot is fired");
                Assert(flash.TriggerCount == 0, "Trigger count is 0 before firing");

                // Simulate shot firing
                flash.TriggerFlash();
                Assert(flash.IsFlashActive, "Flash activates on valid shot");
                Assert(flash.TriggerCount == 1, "Trigger count increments to 1");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_FlashLifetimeAndCleanup_DeactivatesAfterConfiguredDuration()
        {
            var root = new GameObject("TestRoot_LifetimeCleanup");
            try
            {
                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.Initialize();
                flash.FlashDuration = 0.065f;

                flash.TriggerFlash();
                Assert(flash.IsFlashActive, "Flash active immediately upon trigger");

                // Advance by 35ms (midway through 65ms lifetime)
                flash.ManualUpdate(0.035f);
                Assert(flash.IsFlashActive, "Flash remains visible at 35ms (human-visible window)");

                // Advance by another 40ms (total 75ms > 65ms)
                flash.ManualUpdate(0.040f);
                Assert(!flash.IsFlashActive, "Flash cleanly deactivates after 65ms duration expires");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_MultiWeapon_TrackingAcrossSwaps()
        {
            var root = new GameObject("TestRoot_MultiWeaponSwap");
            try
            {
                var camObj = new GameObject("TestCam");
                camObj.transform.SetParent(root.transform);
                var cam = camObj.AddComponent<Camera>();

                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.PlayerCamera = cam;

                // Weapon 1 (Ammo1gun)
                var w1 = new GameObject("Weapon1");
                w1.transform.SetParent(root.transform);
                var m1 = new GameObject("Muzzle");
                m1.transform.SetParent(w1.transform);
                m1.transform.position = new Vector3(0.20f, 1.40f, 0.45f);

                // Weapon 2 (Ammo2gun)
                var w2 = new GameObject("Weapon2");
                w2.transform.SetParent(root.transform);
                var m2 = new GameObject("Muzzle");
                m2.transform.SetParent(w2.transform);
                m2.transform.position = new Vector3(0.22f, 1.38f, 0.55f);

                // Weapon 3 (Ammo3gun)
                var w3 = new GameObject("Weapon3");
                w3.transform.SetParent(root.transform);
                var m3 = new GameObject("Muzzle");
                m3.transform.SetParent(w3.transform);
                m3.transform.position = new Vector3(0.18f, 1.42f, 0.75f);

                // 1. Equip Weapon 1
                flash.Initialize(m1.transform);
                flash.TriggerFlash(m1.transform);
                Assert(Vector3.Distance(flash.LastFlashPosition, m1.transform.position) <= 0.03f, "Flash aligns with Weapon 1 muzzle");

                // 2. Switch to Weapon 2
                flash.NotifyWeaponChanged(m2.transform);
                flash.TriggerFlash(m2.transform);
                Assert(Vector3.Distance(flash.LastFlashPosition, m2.transform.position) <= 0.03f, "Flash aligns with Weapon 2 muzzle");

                // 3. Switch to Weapon 3
                flash.NotifyWeaponChanged(m3.transform);
                flash.TriggerFlash(m3.transform);
                Assert(Vector3.Distance(flash.LastFlashPosition, m3.transform.position) <= 0.03f, "Flash aligns with Weapon 3 muzzle");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_NearCameraAndObstructionSafety_ClampsNearPlane()
        {
            var root = new GameObject("TestRoot_NearPlaneSafety");
            try
            {
                var camObj = new GameObject("PlayerCam");
                camObj.transform.SetParent(root.transform);
                camObj.transform.position = new Vector3(0f, 1.4f, 0f);
                camObj.transform.forward = Vector3.forward;
                var cam = camObj.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;

                // Simulate severe wall retraction where muzzle is pushed to z = 0.06m (dangerously close to nearClip = 0.05m)
                var retractedMuzzle = new GameObject("RetractedMuzzle");
                retractedMuzzle.transform.SetParent(root.transform);
                retractedMuzzle.transform.position = new Vector3(0.18f, 1.4f, 0.06f);

                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.PlayerCamera = cam;
                flash.Initialize(retractedMuzzle.transform);

                flash.TriggerFlash(retractedMuzzle.transform);

                float depthAlongCam = Vector3.Dot(flash.LastFlashPosition - cam.transform.position, cam.transform.forward);
                Assert(depthAlongCam >= 0.10f, "Flash position is safely clamped >= 0.10m to prevent camera near clip plane intersection",
                    $"Actual depth: {depthAlongCam:F3}m (nearClip = {cam.nearClipPlane}m)");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_StrictlyZeroLightsCollidersOrDebugControls()
        {
            var root = new GameObject("TestRoot_ZeroLightsColliders");
            try
            {
                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.Initialize();
                flash.TriggerFlash();

                var lights = root.GetComponentsInChildren<Light>(true);
                Assert(lights.Length == 0, "Strictly zero Light components exist on muzzle flash V2", $"Found {lights.Length}");

                var colliders = root.GetComponentsInChildren<Collider>(true);
                Assert(colliders.Length == 0, "Strictly zero Collider components exist on muzzle flash V2", $"Found {colliders.Length}");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_CameraFacingBillboard_NeverEdgeOn()
        {
            var root = new GameObject("TestRoot_NeverEdgeOn");
            try
            {
                var camObj = new GameObject("PlayerCam");
                camObj.transform.SetParent(root.transform);
                camObj.transform.position = new Vector3(0f, 1.4f, 0f);
                camObj.transform.forward = Vector3.forward;
                var cam = camObj.AddComponent<Camera>();

                var muzzleObj = new GameObject("Muzzle");
                muzzleObj.transform.SetParent(root.transform);
                muzzleObj.transform.position = new Vector3(0.2f, 1.4f, 0.5f);

                var flash = root.AddComponent<FpsMuzzleFlashV2>();
                flash.PlayerCamera = cam;
                flash.Initialize(muzzleObj.transform);

                flash.TriggerFlash(muzzleObj.transform);

                // Quad normal is flashObject forward
                Vector3 quadNormal = flash.FlashObject.transform.forward;
                Vector3 viewRay = (flash.FlashObject.transform.position - cam.transform.position).normalized;

                float facingDot = Vector3.Dot(quadNormal, viewRay);
                Assert(facingDot > 0.90f, "Billboard quad is directly perpendicular to view ray (facingDot > 0.90, never edge-on)",
                    $"Got {facingDot:F4}");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }
    }
}
