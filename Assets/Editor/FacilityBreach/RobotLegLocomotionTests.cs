using System;
using System.IO;
using FacilityBreach.Combat;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated test runner and verification suite for RobotLegLocomotion across all 4 enemy types
    /// (Flanker, Sound Hunter, Patrol Guard, Cover Defender).
    /// </summary>
    public static class RobotLegLocomotionTests
    {
        private static readonly string[] RobotPrefabs = {
            "Assets/Prefabs/FacilityBreach/Enemies/PatrolGuard_Visual.prefab",
            "Assets/Prefabs/FacilityBreach/Enemies/SoundHunter_Visual.prefab",
            "Assets/Prefabs/FacilityBreach/Enemies/CoverDefender_Visual.prefab",
            "Assets/Prefabs/FacilityBreach/Enemies/Flanker_Visual.prefab"
        };

        private static readonly string[] RobotIdentities = {
            "PG-01",
            "SH-01",
            "CD-03",
            "FL-04"
        };

        [MenuItem("Facility Breach/Run Robot Leg Locomotion Tests")]
        public static void RunAll()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("=================================================");
            log.AppendLine("   ROBOT LEG LOCOMOTION AUTOMATED TEST SUITE     ");
            log.AppendLine("=================================================");
            log.AppendLine($"Timestamp: {DateTime.UtcNow:u}\n");

            int passed = 0;
            int total = 0;

            for (int i = 0; i < RobotPrefabs.Length; i++)
            {
                string prefabPath = RobotPrefabs[i];
                string identity = RobotIdentities[i];

                log.AppendLine($"-------------------------------------------------");
                log.AppendLine($"TESTING ROBOT: {identity} ({Path.GetFileNameWithoutExtension(prefabPath)})");
                log.AppendLine($"-------------------------------------------------");

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!prefab)
                {
                    log.AppendLine($"[FAIL] Missing visual prefab: {prefabPath}");
                    total += 6;
                    continue;
                }

                // Create agent root container simulating runtime CreateAgent
                var agentRoot = new GameObject($"Test_Agent_{identity}");
                var combat = agentRoot.AddComponent<EnemyCombatAdapter>();
                combat.Identity = identity;
                combat.MaximumHealth = 100;

                var visual = UnityEngine.Object.Instantiate(prefab, agentRoot.transform);
                visual.name = "ModelVisual";

                var locomotion = agentRoot.AddComponent<RobotLegLocomotion>();
                locomotion.ApplyRobotSpecificProfile();

                // TEST 1: Bone Discovery & Binding
                total++;
                bool bound = locomotion.InitializeBones();
                if (bound && locomotion.IsBound)
                {
                    log.AppendLine($"  [PASS] Bone Binding: successfully bound Lleg and Rleg bones.");
                    passed++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Bone Binding: failed to bind Lleg and Rleg bones.");
                }

                // TEST 2: Standing Idle Pose (Pitches must be 0)
                total++;
                locomotion.SimulateStep(0.02f, Vector3.zero, 0f, 0f);
                if (Mathf.Abs(locomotion.LeftLegPitch) < 0.01f && Mathf.Abs(locomotion.RightLegPitch) < 0.01f && !locomotion.IsMoving)
                {
                    log.AppendLine($"  [PASS] Idle Pose: Left={locomotion.LeftLegPitch:F2} deg, Right={locomotion.RightLegPitch:F2} deg, IsMoving={locomotion.IsMoving}");
                    passed++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Idle Pose: Expected 0 deg when stationary, got Left={locomotion.LeftLegPitch}, Right={locomotion.RightLegPitch}");
                }

                // TEST 3: Forward Locomotion & Alternating Swing
                total++;
                float simDt = 0.02f;
                bool alternatingObserved = false;
                float maxObservedSwing = 0f;
                Vector3 simPos = Vector3.zero;

                for (int frame = 1; frame <= 40; frame++)
                {
                    simPos += Vector3.forward * (2.2f * simDt);
                    locomotion.SimulateStep(simDt, simPos, 0f, 2.2f);

                    float left = locomotion.LeftLegPitch;
                    float right = locomotion.RightLegPitch;
                    maxObservedSwing = Mathf.Max(maxObservedSwing, Mathf.Abs(left), Mathf.Abs(right));

                    // Check that left and right leg have opposite signs when swinging
                    if (Mathf.Abs(left) > 3f && Mathf.Abs(right) > 3f && Mathf.Sign(left) != Mathf.Sign(right))
                    {
                        alternatingObserved = true;
                    }
                }

                if (alternatingObserved && maxObservedSwing > 15f && locomotion.IsMoving)
                {
                    log.AppendLine($"  [PASS] Locomotion & Alternating Swing: alternating=true, maxSwing={maxObservedSwing:F1} deg, speed={locomotion.CurrentSpeed:F2} m/s");
                    passed++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Locomotion: alternating={alternatingObserved}, maxSwing={maxObservedSwing}, isMoving={locomotion.IsMoving}");
                }

                // TEST 4: Run Speed Increases Amplitude
                total++;
                float runMaxSwing = 0f;
                for (int frame = 1; frame <= 40; frame++)
                {
                    simPos += Vector3.forward * (3.5f * simDt);
                    locomotion.SimulateStep(simDt, simPos, 0f, 3.5f);
                    runMaxSwing = Mathf.Max(runMaxSwing, Mathf.Abs(locomotion.LeftLegPitch));
                }

                if (runMaxSwing >= maxObservedSwing)
                {
                    log.AppendLine($"  [PASS] Speed Scaling: runMaxSwing={runMaxSwing:F1} deg >= walkMaxSwing={maxObservedSwing:F1} deg");
                    passed++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Speed Scaling: runMaxSwing={runMaxSwing} < walkMaxSwing={maxObservedSwing}");
                }

                // TEST 5: Idle Recovery on Stop
                total++;
                // Stop moving: keep position constant and step 50 frames
                for (int stopFrame = 1; stopFrame <= 60; stopFrame++)
                {
                    locomotion.SimulateStep(simDt, simPos, 0f, 0f);
                }

                if (Mathf.Abs(locomotion.LeftLegPitch) < 0.2f && Mathf.Abs(locomotion.RightLegPitch) < 0.2f && !locomotion.IsMoving)
                {
                    log.AppendLine($"  [PASS] Idle Recovery: Left={locomotion.LeftLegPitch:F2} deg, Right={locomotion.RightLegPitch:F2} deg, weight={locomotion.MovementWeight:F3}");
                    passed++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Idle Recovery: Failed to return to rest, Left={locomotion.LeftLegPitch}, Right={locomotion.RightLegPitch}");
                }

                // TEST 6: Defeat / Death Pose Reset
                total++;
                // Set moving again
                simPos += Vector3.forward * 0.1f;
                locomotion.SimulateStep(simDt, simPos, 0f, 2f);

                // Force defeat
                locomotion.ForceDefeated = true;
                locomotion.SimulateStep(simDt, simPos, 0f, 0f);

                if (locomotion.IsDead && Mathf.Abs(locomotion.LeftLegPitch) < 0.001f && Mathf.Abs(locomotion.RightLegPitch) < 0.001f)
                {
                    log.AppendLine($"  [PASS] Defeat Reset: IsDead={locomotion.IsDead}, Left={locomotion.LeftLegPitch:F2} deg, Right={locomotion.RightLegPitch:F2} deg");
                    passed++;
                }
                else
                {
                    log.AppendLine($"  [FAIL] Defeat Reset: IsDead={locomotion.IsDead}, Left={locomotion.LeftLegPitch}, Right={locomotion.RightLegPitch}");
                }

                UnityEngine.Object.DestroyImmediate(agentRoot);
            }

            log.AppendLine("=================================================");
            log.AppendLine($"TOTAL TESTS: {total} | PASSED: {passed} | FAILED: {total - passed}");
            log.AppendLine($"SUCCESS RATE: {(float)passed / total * 100f:F1}%");
            log.AppendLine("=================================================");

            string logText = log.ToString();
            File.WriteAllText("robot_leg_tests.log", logText);
            Debug.Log(logText);

            if (passed != total)
            {
                Debug.LogError($"RobotLegLocomotionTests FAILED: {total - passed} failures!");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("RobotLegLocomotionTests PASSED: 100% of tests passed!");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }
    }
}
