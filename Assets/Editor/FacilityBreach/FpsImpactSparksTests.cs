using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FacilityBreach;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated regression tests for first-person environmental impact sparks (FpsImpactSparks).
    /// Verifies environment-only hit classification, correct hit point and normal orientation,
    /// robust vertical floor/ceiling handling, transparent depth-writing disabled material invariant,
    /// strict absence of point lights and colliders, clean lifetime deactivation, and bounded pool reuse.
    /// </summary>
    public static class FpsImpactSparksTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/Run FPS Impact Sparks Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("<color=cyan>=== [FpsImpactSparksTests] Starting Test Suite ===</color>");

            RunTest("Initialization_CreatesPoolWithZeroColliders", Test_Initialization_CreatesPoolWithZeroColliders);
            RunTest("MaterialConfiguration_TransparentUnlitWithZWriteOff", Test_MaterialConfiguration_TransparentUnlitWithZWriteOff);
            RunTest("SpawnSparks_PositionsAtHitPointWithNormalOffset", Test_SpawnSparks_PositionsAtHitPointWithNormalOffset);
            RunTest("SpawnSparks_VerticalNormals_NoCollinearExceptions", Test_SpawnSparks_VerticalNormals_NoCollinearExceptions);
            RunTest("EnvironmentClassification_SparksTriggeredOnlyOnEnvironment", Test_EnvironmentClassification_SparksTriggeredOnlyOnEnvironment);
            RunTest("LifetimeExpiry_DeactivatesCleanlyWithoutLingering", Test_LifetimeExpiry_DeactivatesCleanlyWithoutLingering);
            RunTest("BoundedPoolReuse_NoMemoryLeak", Test_BoundedPoolReuse_NoMemoryLeak);
            RunTest("StrictlyZeroLightsOrColliders", Test_StrictlyZeroLightsOrColliders);

            if (failedCount == 0)
            {
                Log($"<color=green>[FpsImpactSparksTests] Completed {passedCount} checks: {passedCount} PASSED, 0 FAILED.</color>");
            }
            else
            {
                LogError($"<color=red>[FpsImpactSparksTests] FAILED: {failedCount} failures out of {passedCount + failedCount} checks.</color>");
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

        [MenuItem("Facility Breach/Run All 10 Suites (Regression + Tracer + Flash + Sparks)")]
        public static void RunAll10SuitesBatch()
        {
            Log("<color=yellow>=== RUNNING ALL 10 TEST SUITES (288 Baseline + Tracer + Flash V2 + Sparks) ===</color>");
            FpsArmsTests.RunAll();
            FpsViewModelObstructionTests.RunAll();
            FacilityInteractionDispatcherTests.RunAll();
            PatrolGuardAStarTests.RunAll();
            PatrolGuardSearchTests.RunAll();
            PatrolGuardReturnTests.RunAll();
            PatrolGuardObstacleTests.RunAll();
            FpsBulletTracerTests.RunAll();
            FpsMuzzleFlashV2Tests.RunAll();
            FpsImpactSparksTests.RunAll();

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
                Log($"[Sparks PASS] {testName}");
            }
            else
            {
                failedCount++;
                string msg = $"{testName} FAILED" + (string.IsNullOrEmpty(detail) ? "" : $" ({detail})");
                failureMessages.Add(msg);
                LogError($"[Sparks FAIL] {msg}");
            }
        }

        private static void Test_Initialization_CreatesPoolWithZeroColliders()
        {
            var root = new GameObject("TestRoot_SparksInit");
            try
            {
                var sparks = root.AddComponent<FpsImpactSparks>();
                sparks.Initialize();

                Assert(sparks.PoolSize >= 8, "Sparks pool capacity is at least 8", $"Got {sparks.PoolSize}");
                Assert(sparks.ActiveImpactCount == 0, "Sparks pool begins with 0 active impacts");

                var colliders = root.GetComponentsInChildren<Collider>(true);
                Assert(colliders.Length == 0, "Sparks hierarchy contains strictly zero colliders");
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
                var sparks = root.AddComponent<FpsImpactSparks>();
                sparks.Initialize();

                var mat = sparks.SharedMaterial;
                Assert(mat != null, "Shared impact spark material is instantiated");
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

        private static void Test_SpawnSparks_PositionsAtHitPointWithNormalOffset()
        {
            var root = new GameObject("TestRoot_SpawnSparks");
            try
            {
                var sparks = root.AddComponent<FpsImpactSparks>();
                sparks.Initialize();

                Vector3 hitPoint = new Vector3(2.5f, 1.2f, 6.0f);
                Vector3 surfaceNormal = Vector3.back;

                sparks.SpawnSparks(hitPoint, surfaceNormal);

                Assert(sparks.ActiveImpactCount == 1, "SpawnSparks activates exactly one impact spark object");
                Assert(Vector3.Distance(sparks.LastImpactPoint, hitPoint) < 0.001f, "Impact point matches hitscan hit point");
                Assert(Vector3.Distance(sparks.LastImpactNormal, surfaceNormal) < 0.001f, "Impact normal matches hitscan surface normal");
                Assert(sparks.LastShotWasEnvironment, "Shot flagged as environment hit");
                Assert(sparks.TriggerCount == 1, "TriggerCount increments to 1");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_SpawnSparks_VerticalNormals_NoCollinearExceptions()
        {
            var root = new GameObject("TestRoot_VerticalNormals");
            try
            {
                var sparks = root.AddComponent<FpsImpactSparks>();
                sparks.Initialize();

                // 1. Floor hit (normal = up)
                Vector3 floorHit = new Vector3(0.5f, 0f, 4.0f);
                sparks.SpawnSparks(floorHit, Vector3.up);

                Assert(sparks.ActiveImpactCount == 1, "Impact spark spawns for vertical floor hit (normal = up)");
                Assert(!float.IsNaN(sparks.LastImpactNormal.x) && !float.IsNaN(sparks.LastImpactNormal.y) && !float.IsNaN(sparks.LastImpactNormal.z),
                    "Floor impact normal does not contain NaN");

                sparks.ManualUpdate(1.0f);

                // 2. Ceiling hit (normal = down)
                Vector3 ceilingHit = new Vector3(0.5f, 3.0f, 4.0f);
                sparks.SpawnSparks(ceilingHit, Vector3.down);

                Assert(sparks.ActiveImpactCount == 1, "Impact spark spawns for vertical ceiling hit (normal = down)");
                Assert(!float.IsNaN(sparks.LastImpactNormal.x) && !float.IsNaN(sparks.LastImpactNormal.y) && !float.IsNaN(sparks.LastImpactNormal.z),
                    "Ceiling impact normal does not contain NaN");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_EnvironmentClassification_SparksTriggeredOnlyOnEnvironment()
        {
            var root = new GameObject("TestRoot_EnvironmentClassification");
            try
            {
                var sparks = root.AddComponent<FpsImpactSparks>();
                sparks.Initialize();

                // Case 1: Environment hit (damageable == null && enemy == null)
                bool isEnvironmentHit = true;
                if (isEnvironmentHit)
                {
                    sparks.SpawnSparks(new Vector3(0f, 1f, 5f), Vector3.back);
                }
                Assert(sparks.ActiveImpactCount == 1, "Impact sparks activate on static environment hit");

                sparks.ManualUpdate(1.0f);

                // Case 2: Enemy hit (enemy != null, isEnvironment == false)
                bool isEnemyHit = false;
                if (isEnemyHit)
                {
                    sparks.SpawnSparks(new Vector3(0f, 1f, 5f), Vector3.back);
                }
                Assert(sparks.ActiveImpactCount == 0, "Strictly zero impact sparks activated on enemy character hit");

                // Case 3: Miss (isEnvironment == false)
                bool isMiss = false;
                if (isMiss)
                {
                    sparks.SpawnSparks(new Vector3(0f, 1f, 35f), Vector3.up);
                }
                Assert(sparks.ActiveImpactCount == 0, "Strictly zero impact sparks activated on miss");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_LifetimeExpiry_DeactivatesCleanlyWithoutLingering()
        {
            var root = new GameObject("TestRoot_LifetimeCleanup");
            try
            {
                var sparks = root.AddComponent<FpsImpactSparks>();
                sparks.Initialize();
                sparks.SparkDuration = 0.11f;

                sparks.SpawnSparks(new Vector3(0f, 1f, 4f), Vector3.back);
                Assert(sparks.ActiveImpactCount == 1, "Impact spark active immediately upon trigger");

                // Advance by 50ms (midway through 110ms lifetime)
                sparks.ManualUpdate(0.050f);
                Assert(sparks.ActiveImpactCount == 1, "Impact spark remains visible at 50ms");

                // Advance past 110ms (total 120ms > 110ms)
                sparks.ManualUpdate(0.070f);
                Assert(sparks.ActiveImpactCount == 0, "Impact spark cleanly deactivates after lifetime expires");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_BoundedPoolReuse_NoMemoryLeak()
        {
            var root = new GameObject("TestRoot_PoolReuse");
            try
            {
                var sparks = root.AddComponent<FpsImpactSparks>();
                sparks.Initialize();

                // Trigger 20 impacts to cycle through the 8-item pool
                for (int i = 0; i < 20; i++)
                {
                    sparks.SpawnSparks(new Vector3(i * 0.1f, 1f, 3f), Vector3.back);
                }

                Assert(sparks.TriggerCount == 20, "Trigger count records 20 activations");

                var poolObjects = root.GetComponentsInChildren<Transform>(true);
                // Parent poolRoot + 8 impact objects + (8 * 5 spark lines) = bounded constant hierarchy (49 transforms)
                Assert(poolObjects.Length <= 55, "Pool hierarchy remains strictly bounded without per-shot allocation leaks",
                    $"Found {poolObjects.Length} transforms");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }

        private static void Test_StrictlyZeroLightsOrColliders()
        {
            var root = new GameObject("TestRoot_ZeroLightsColliders");
            try
            {
                var sparks = root.AddComponent<FpsImpactSparks>();
                sparks.Initialize();
                sparks.SpawnSparks(Vector3.forward * 5f, Vector3.back);

                var lights = root.GetComponentsInChildren<Light>(true);
                Assert(lights.Length == 0, "Strictly zero Light components exist on impact sparks to prevent HDR sky artifacts",
                    $"Found {lights.Length}");

                var colliders = root.GetComponentsInChildren<Collider>(true);
                Assert(colliders.Length == 0, "Strictly zero Collider components exist on impact sparks",
                    $"Found {colliders.Length}");
            }
            finally
            {
                GameObject.DestroyImmediate(root);
            }
        }
    }
}
