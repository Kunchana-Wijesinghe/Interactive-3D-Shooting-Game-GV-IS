using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FacilityBreach;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated regression tests for first-person bullet tracers (FpsBulletTracer).
    /// Verifies tracer start/end vectors, bounded miss endpoints to prevent skybox streaks,
    /// camera near-plane protection against reverse/point-blank visual glitches,
    /// transparent depth-writing disabled material invariant, zero-collider enforcement,
    /// clean pooled lifetime deactivation, and muzzle tracking across weapon swaps.
    /// </summary>
    public static class FpsBulletTracerTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/Run FPS Bullet Tracer Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("<color=cyan>=== [FpsBulletTracerTests] Starting Test Suite ===</color>");

            Test_TracerInitialization_CreatesPoolWithZeroColliders();
            Test_SpawnTracer_HitScanEndpoint();
            Test_SpawnTracer_WithinRangeMiss_ReachesAimPoint();
            Test_SpawnTracer_BeyondRangeMiss_ClampedToMaxDistance();
            Test_SpawnTracer_PointBlankOrReverse_SuppressesTracerNearCamera();
            Test_LifetimeExpiry_DeactivatesTracerCleanly();
            Test_MultiWeapon_MuzzleSwitching();
            Test_ObstructionRetraction_TracerTracksRetractedMuzzle();
            Test_Material_TransparentAndDepthWriteDisabled();
            Test_ZeroColliderInvariant_AcrossAllPooledTracers();

            if (failedCount == 0)
            {
                Log($"<color=green>[FpsBulletTracerTests] Completed {passedCount} checks: {passedCount} PASSED, 0 FAILED.</color>");
            }
            else
            {
                LogError($"<color=red>[FpsBulletTracerTests] FAILED: {failedCount} failures out of {passedCount + failedCount} checks.</color>");
                foreach (var err in failureMessages)
                {
                    LogError("  - " + err);
                }
            }
        }

        public static void RunBatch()
        {
            RunAll();
            if (failedCount > 0)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                EditorApplication.Exit(0);
            }
        }

        [MenuItem("Facility Breach/Run All 8 Suites (Regression + Bullet Tracer)")]
        public static void RunAllSuitesBatch()
        {
            Log("<color=yellow>=== RUNNING ALL 8 TEST SUITES (288 Baseline + Bullet Tracer) ===</color>");
            FpsArmsTests.RunAll();
            FpsViewModelObstructionTests.RunAll();
            FacilityInteractionDispatcherTests.RunAll();
            PatrolGuardAStarTests.RunAll();
            PatrolGuardSearchTests.RunAll();
            PatrolGuardReturnTests.RunAll();
            PatrolGuardObstacleTests.RunAll();
            FpsBulletTracerTests.RunAll();

            if (failedCount > 0)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                EditorApplication.Exit(0);
            }
        }

        private static void Assert(bool condition, string testName, string detail = null)
        {
            if (condition)
            {
                passedCount++;
                Log($"[Tracer Test PASS] {testName}");
            }
            else
            {
                failedCount++;
                string msg = $"{testName} FAILED" + (string.IsNullOrEmpty(detail) ? "" : $" ({detail})");
                failureMessages.Add(msg);
                LogError($"[Tracer Test FAIL] {msg}");
            }
        }

        private static void Test_TracerInitialization_CreatesPoolWithZeroColliders()
        {
            var root = new GameObject("TestRoot_TracerInit");
            try
            {
                var tracer = root.AddComponent<FpsBulletTracer>();
                tracer.Initialize();

                Assert(tracer.PoolSize >= 8, "Tracer pool capacity is at least 8", $"Got {tracer.PoolSize}");
                Assert(tracer.ActiveTracerCount == 0, "Tracer pool begins with 0 active tracers");

                var colliders = root.GetComponentsInChildren<Collider>(true);
                Assert(colliders.Length == 0, "Tracer hierarchy contains strictly zero colliders");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_SpawnTracer_HitScanEndpoint()
        {
            var root = new GameObject("TestRoot_HitTracer");
            try
            {
                var tracer = root.AddComponent<FpsBulletTracer>();
                var muzzleObj = new GameObject("TestMuzzle");
                muzzleObj.transform.SetParent(root.transform);
                muzzleObj.transform.position = new Vector3(0.2f, 1.4f, 0.5f);

                tracer.Initialize(muzzleObj.transform);

                Vector3 hitPoint = new Vector3(3.5f, 1.4f, 12.0f);
                tracer.SpawnTracer(muzzleObj.transform, hitPoint, isHit: true);

                Assert(tracer.ActiveTracerCount == 1, "Hit shot activates exactly one tracer streak");
                Assert(Vector3.Distance(tracer.LastTracerStart, muzzleObj.transform.position) < 0.001f, "Tracer start matches active muzzle position");
                Assert(Vector3.Distance(tracer.LastTracerEnd, hitPoint) < 0.001f, "Tracer end matches hitscan hit point");
                Assert(tracer.LastShotWasHit, "Tracer records shot as hit");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_SpawnTracer_WithinRangeMiss_ReachesAimPoint()
        {
            var root = new GameObject("TestRoot_WithinRangeMiss");
            try
            {
                var tracer = root.AddComponent<FpsBulletTracer>();
                var muzzleObj = new GameObject("TestMuzzle");
                muzzleObj.transform.SetParent(root.transform);
                muzzleObj.transform.position = new Vector3(0.2f, 1.4f, 0.5f);

                tracer.Initialize(muzzleObj.transform);

                // Aim point at 25m (within 35m max miss distance)
                Vector3 missAimPoint = new Vector3(0.2f, 1.4f, 25.0f);
                tracer.SpawnTracer(muzzleObj.transform, missAimPoint, isHit: false);

                Assert(tracer.ActiveTracerCount == 1, "Miss shot within range activates exactly one tracer streak");
                Assert(Vector3.Distance(tracer.LastTracerStart, muzzleObj.transform.position) < 0.001f, "Miss tracer start matches muzzle position");
                Assert(Vector3.Distance(tracer.LastTracerEnd, missAimPoint) < 0.001f, "Miss tracer within range reaches aim point");
                Assert(!tracer.LastShotWasHit, "Tracer records shot as miss");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_SpawnTracer_BeyondRangeMiss_ClampedToMaxDistance()
        {
            var root = new GameObject("TestRoot_BeyondRangeMiss");
            try
            {
                var camObj = new GameObject("PlayerCam");
                camObj.transform.SetParent(root.transform);
                camObj.transform.position = new Vector3(0f, 1.4f, 0f);
                camObj.transform.forward = Vector3.forward;
                var cam = camObj.AddComponent<Camera>();

                var muzzleObj = new GameObject("TestMuzzle");
                muzzleObj.transform.SetParent(root.transform);
                muzzleObj.transform.position = new Vector3(0.2f, 1.4f, 0.5f);

                var tracer = root.AddComponent<FpsBulletTracer>();
                tracer.PlayerCamera = cam;
                tracer.Initialize(muzzleObj.transform);

                // Target aim point is 60m away (e.g. miss into skybox)
                Vector3 distantMissAim = new Vector3(0.2f, 1.4f, 60.5f);
                tracer.SpawnTracer(muzzleObj.transform, distantMissAim, isHit: false);

                Assert(tracer.ActiveTracerCount == 1, "Distant miss shot spawns a tracer streak");
                float visualTravelDist = Vector3.Distance(tracer.LastTracerStart, tracer.LastTracerEnd);
                Assert(Mathf.Abs(visualTravelDist - tracer.MaxMissDistance) < 0.05f,
                    $"Distant miss endpoint is clamped to MaxMissDistance ({tracer.MaxMissDistance}m)",
                    $"Actual travel distance: {visualTravelDist:F2}m");
                Assert(visualTravelDist <= tracer.MaxMissDistance + 0.01f, "Tracer travel strictly does not exceed max miss distance");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_SpawnTracer_PointBlankOrReverse_SuppressesTracerNearCamera()
        {
            var root = new GameObject("TestRoot_PointBlankReverse");
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

                var tracer = root.AddComponent<FpsBulletTracer>();
                tracer.PlayerCamera = cam;
                tracer.Initialize(muzzleObj.transform);

                // Case 1: Reverse hit (target point is behind muzzle, e.g. obstacle retraction)
                Vector3 behindMuzzlePoint = new Vector3(0.2f, 1.4f, 0.40f);
                tracer.SpawnTracer(muzzleObj.transform, behindMuzzlePoint, isHit: true);

                Assert(tracer.ActiveTracerCount == 0, "Tracer streak is suppressed when target is behind muzzle to prevent near-clip camera division artifacts");

                // Reset
                tracer.ManualUpdate(1.0f);

                // Case 2: Extreme point-blank hit (< 0.15m from muzzle)
                Vector3 pointBlankPoint = new Vector3(0.2f, 1.4f, 0.58f); // 0.08m forward
                tracer.SpawnTracer(muzzleObj.transform, pointBlankPoint, isHit: true);

                Assert(tracer.ActiveTracerCount == 0, "Tracer streak is suppressed on extreme point-blank collision (< 0.15m)");

                // Reset
                tracer.ManualUpdate(1.0f);

                // Case 3: Clear unobstructed distance (> 0.15m and forward)
                Vector3 clearPoint = new Vector3(0.2f, 1.4f, 5.0f);
                tracer.SpawnTracer(muzzleObj.transform, clearPoint, isHit: true);

                Assert(tracer.ActiveTracerCount == 1, "Tracer streak renders normally when distance and camera clearance are safe");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_LifetimeExpiry_DeactivatesTracerCleanly()
        {
            var root = new GameObject("TestRoot_LifetimeCleanup");
            try
            {
                var tracer = root.AddComponent<FpsBulletTracer>();
                var muzzleObj = new GameObject("TestMuzzle");
                muzzleObj.transform.SetParent(root.transform);

                tracer.Initialize(muzzleObj.transform);

                Vector3 hitPoint = new Vector3(1f, 1f, 5f);
                tracer.SpawnTracer(muzzleObj.transform, hitPoint, isHit: true);

                Assert(tracer.ActiveTracerCount == 1, "Tracer active immediately after shot");

                // Step time forward past tracer duration (40ms)
                tracer.ManualUpdate(0.10f);

                Assert(tracer.ActiveTracerCount == 0, "Tracer cleanly deactivated after duration expires");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_MultiWeapon_MuzzleSwitching()
        {
            var root = new GameObject("TestRoot_WeaponSwitching");
            try
            {
                var tracer = root.AddComponent<FpsBulletTracer>();

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
                tracer.Initialize(m1.transform);
                Assert(tracer.CurrentMuzzle == m1.transform, "Tracer tracks Weapon 1 muzzle");
                tracer.SpawnTracer(m1.transform, new Vector3(0f, 1.4f, 10f), true);
                Assert(Vector3.Distance(tracer.LastTracerStart, m1.transform.position) < 0.001f, "Shot 1 originates at Weapon 1 muzzle");

                // 2. Switch to Weapon 2
                tracer.NotifyWeaponChanged(m2.transform);
                Assert(tracer.CurrentMuzzle == m2.transform, "Tracer updates to Weapon 2 muzzle after swap");
                tracer.SpawnTracer(m2.transform, new Vector3(0f, 1.4f, 10f), true);
                Assert(Vector3.Distance(tracer.LastTracerStart, m2.transform.position) < 0.001f, "Shot 2 originates at Weapon 2 muzzle");

                // 3. Switch to Weapon 3
                tracer.NotifyWeaponChanged(m3.transform);
                Assert(tracer.CurrentMuzzle == m3.transform, "Tracer updates to Weapon 3 muzzle after swap");
                tracer.SpawnTracer(m3.transform, new Vector3(0f, 1.4f, 10f), true);
                Assert(Vector3.Distance(tracer.LastTracerStart, m3.transform.position) < 0.001f, "Shot 3 originates at Weapon 3 muzzle");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_ObstructionRetraction_TracerTracksRetractedMuzzle()
        {
            var root = new GameObject("TestRoot_ObstructionRetraction");
            try
            {
                var camObj = new GameObject("PlayerCam");
                camObj.transform.SetParent(root.transform);
                var cam = camObj.AddComponent<Camera>();

                var weaponRoot = new GameObject("EquippedWeapon");
                weaponRoot.transform.SetParent(camObj.transform);
                weaponRoot.transform.localPosition = new Vector3(0.18f, -0.22f, 0.50f);

                var muzzleObj = new GameObject("Muzzle");
                muzzleObj.transform.SetParent(weaponRoot.transform);
                muzzleObj.transform.localPosition = new Vector3(0f, 0f, 0.40f);

                var obstruction = camObj.AddComponent<FpsViewModelObstruction>();
                obstruction.Initialize(cam, root.transform, weaponRoot.transform, muzzleObj.transform);

                var tracer = root.AddComponent<FpsBulletTracer>();
                tracer.Initialize(muzzleObj.transform);

                Vector3 initialMuzzlePos = muzzleObj.transform.position;

                // Simulate physical wall obstruction: weapon slides backward by 0.20m
                weaponRoot.transform.localPosition = new Vector3(0.18f, -0.32f, 0.30f);
                Vector3 retractedMuzzlePos = muzzleObj.transform.position;

                Assert(retractedMuzzlePos.z < initialMuzzlePos.z, "Muzzle world position moves backward under wall retraction");

                // Fire shot from retracted weapon
                tracer.SpawnTracer(muzzleObj.transform, new Vector3(0f, 0f, 10f), true);
                Assert(Vector3.Distance(tracer.LastTracerStart, retractedMuzzlePos) < 0.001f, "Tracer starts from retracted muzzle location near obstacle");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_Material_TransparentAndDepthWriteDisabled()
        {
            var root = new GameObject("TestRoot_MaterialProperties");
            try
            {
                var tracer = root.AddComponent<FpsBulletTracer>();
                tracer.Initialize();

                var mat = tracer.SharedMaterial;
                Assert(mat != null, "Shared tracer material is instantiated");
                Assert(mat.renderQueue == (int)UnityEngine.Rendering.RenderQueue.Transparent,
                    "Tracer material render queue is Transparent (3000)", $"Got {mat?.renderQueue}");

                if (mat.HasProperty("_ZWrite"))
                {
                    Assert(mat.GetInt("_ZWrite") == 0, "Tracer material _ZWrite is 0 (depth writing disabled to prevent skybox occlusions)");
                }
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_ZeroColliderInvariant_AcrossAllPooledTracers()
        {
            var root = new GameObject("TestRoot_ZeroColliders");
            try
            {
                var tracer = root.AddComponent<FpsBulletTracer>();
                tracer.Initialize();

                // Fire multiple shots to cycle through the pool
                for (int i = 0; i < 15; i++)
                {
                    tracer.SpawnTracer(null, new Vector3(i, 0f, 10f), isHit: true);
                }

                var colliders = root.GetComponentsInChildren<Collider>(true);
                Assert(colliders.Length == 0, "No Collider components exist on any active or pooled tracer object", $"Found {colliders.Length} colliders");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }
    }
}
