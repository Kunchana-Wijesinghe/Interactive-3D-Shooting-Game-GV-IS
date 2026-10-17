using System.Collections;
using System.Collections.Generic;
using System.IO;
using FacilityBreach.AI.Roles;
using FacilityBreach.AI.SoundHunter;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FacilityBreach.AI.Patrol
{
    /// <summary>
    /// TEST-ONLY automated Play Mode verification runner for Milestone 2.3 Dynamic Obstacle Rerouting.
    /// Can be launched via top menu: "Facility Breach > AI Tests > Run Live Barricade Rerouting Scenario (Play Mode)"
    /// Uses bounded real-time polling and post-scene lifecycle initialization to guarantee execution.
    /// Restores all barricade transforms and test-modified edges in a guaranteed finally block.
    /// </summary>
    public sealed class PatrolGuardObstacleScenarioRunner : MonoBehaviour
    {
        private const string AutoRunPrefKey = "PG01_AutoRunObstacleScenario";
        private static int passedChecks = 0;
        private static int failedChecks = 0;
        private static readonly List<string> scenarioLogs = new List<string>();

#if UNITY_EDITOR
        [MenuItem("Facility Breach/AI Tests/Run Live Barricade Rerouting Scenario (Play Mode)")]
        public static void TriggerLiveScenario()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[PG-01 Scenario] Unity is not in Play Mode. Setting auto-run pref and entering Play Mode...");
                EditorPrefs.SetBool(AutoRunPrefKey, true);
                EditorApplication.isPlaying = true;
                return;
            }

            Debug.Log("[PG-01 Scenario] Unity is already in Play Mode. Initiating scenario execution...");
            ExecuteInActivePlayMode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedAfterPlay()
        {
            if (EditorPrefs.GetBool(AutoRunPrefKey, false))
            {
                EditorPrefs.DeleteKey(AutoRunPrefKey);
                Debug.Log("[PG-01 Scenario] Play Mode active scene loaded. Spawning scenario runner...");
                ExecuteInActivePlayMode();
            }
        }

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Clean up auto-run key if user manually exits Play Mode
                if (EditorPrefs.HasKey(AutoRunPrefKey))
                {
                    EditorPrefs.DeleteKey(AutoRunPrefKey);
                }
            }
        }
#endif

        public static void ExecuteInActivePlayMode()
        {
            var existing = FindAnyObjectByType<PatrolGuardObstacleScenarioRunner>();
            if (existing != null)
            {
                Debug.LogWarning("[PG-01 Scenario] Verification scenario is already in progress.");
                return;
            }

            var go = new GameObject("PG01_ObstacleScenarioRunner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<PatrolGuardObstacleScenarioRunner>();
            runner.StartCoroutine(runner.RunScenarioRoutine());
        }

        private void Check(bool condition, string checkName, string details = "")
        {
            if (condition)
            {
                passedChecks++;
                string msg = $"[Obstacle Scenario PASS] {checkName}";
                scenarioLogs.Add(msg);
                Debug.Log(msg);
            }
            else
            {
                failedChecks++;
                string msg = $"[Obstacle Scenario FAIL] {checkName}. {details}";
                scenarioLogs.Add(msg);
                Debug.LogError(msg);
            }
        }

        private static bool IsEdgeBlockedInGraph(PatrolAStarGraph graph, int fromNode, int toNode)
        {
            if (graph == null) return false;
            foreach (var edge in graph.GetEdges(fromNode))
            {
                if (edge.To == toNode) return edge.IsBlocked;
            }
            return false;
        }

        private IEnumerator RunScenarioRoutine()
        {
            passedChecks = 0;
            failedChecks = 0;
            scenarioLogs.Clear();

            Debug.Log("==== [PG-01 Obstacle Scenario] Starting Live Dynamic Obstacle Rerouting Verification ====");

            // State tracking for guaranteed cleanup
            GameObject barricade = null;
            Rigidbody barricadeRb = null;
            Vector3 originalBarricadePos = Vector3.zero;
            Quaternion originalBarricadeRot = Quaternion.identity;
            bool barricadeRepositioned = false;

            PatrolGuardBrain brain = null;
            FacilityBreachRuntime runtime = null;
            SoundHunterNavMeshMover mover = null;
            bool initialEdge13To14Blocked = false;
            bool initialEdge14To13Blocked = false;
            bool edgeModifiedByTest = false;

            try
            {
                // Stage 0: Bounded real-time wait for required scene entities (up to 5.0 seconds)
                Debug.Log("[PG-01 Scenario Stage 0/8] Awaiting scene and entity initialization (bounded 5.0s real-time timeout)...");
                float entityTimeout = Time.realtimeSinceStartup + 5.0f;
                while (Time.realtimeSinceStartup < entityTimeout)
                {
                    if (brain == null) brain = FindAnyObjectByType<PatrolGuardBrain>();
                    if (runtime == null) runtime = FindAnyObjectByType<FacilityBreachRuntime>();
                    if (mover == null && brain != null) mover = brain.GetComponent<SoundHunterNavMeshMover>();
                    if (barricade == null)
                    {
                        barricade = GameObject.Find("Barricade_ExitArea");
                        if (barricade == null)
                        {
                            foreach (var rb in FindObjectsByType<Rigidbody>())
                            {
                                if (rb.name == "Barricade_ExitArea" || rb.name.StartsWith("Barricade"))
                                {
                                    barricade = rb.gameObject;
                                    break;
                                }
                            }
                        }
                    }

                    if (brain != null && runtime != null && mover != null && barricade != null)
                        break;

                    yield return new WaitForSecondsRealtime(0.15f);
                }

                // Stage 1: Validate entity discovery
                Debug.Log($"[PG-01 Scenario Stage 1/8] Validating discovered entities (Brain={(brain != null)}, Mover={(mover != null)}, Barricade={(barricade != null)}, Runtime={(runtime != null)})...");
                Check(brain != null, "Step 1A: Located PatrolGuardBrain component in live scene",
                    brain == null ? "PatrolGuardBrain not found after 5.0s timeout." : $"Located at {brain.transform.position}");
                Check(mover != null, "Step 1B: Located SoundHunterNavMeshMover on PG-01",
                    mover == null ? "SoundHunterNavMeshMover not found on PG-01." : "Located on PG-01");
                Check(barricade != null, "Step 1C: Located Barricade_ExitArea GameObject",
                    barricade == null ? "Barricade_ExitArea not found in scene." : $"Located at {barricade.transform.position}");

                if (brain == null || mover == null || barricade == null)
                {
                    Debug.LogError("[PG-01 Scenario] Aborting: required components missing from active scene.");
                    yield break;
                }

                brain.EnableAStarDiagnostics = true;
                brain.EnableObstacleDiagnostics = true;

                // Cache original transform, physics, and edge states for cleanup
                originalBarricadePos = barricade.transform.position;
                originalBarricadeRot = barricade.transform.rotation;
                barricadeRb = barricade.GetComponent<Rigidbody>();

                if (brain.Graph != null)
                {
                    initialEdge13To14Blocked = IsEdgeBlockedInGraph(brain.Graph, 13, 14);
                    initialEdge14To13Blocked = IsEdgeBlockedInGraph(brain.Graph, 14, 13);
                }

                // Stage 2: Reposition Barricade across active corridor (13 <-> 14) at (6.00, 0.65, 22.00)
                Vector3 corridorPos = new Vector3(6.0f, 0.65f, 22.0f);
                Quaternion corridorRot = Quaternion.Euler(0f, 90f, 0f);

                Debug.Log($"[PG-01 Scenario Stage 2/8] Repositioning Barricade_ExitArea across corridor (13 <-> 14) at {corridorPos}...");
                barricade.transform.position = corridorPos;
                barricade.transform.rotation = corridorRot;
                if (barricadeRb != null)
                {
                    barricadeRb.position = corridorPos;
                    barricadeRb.rotation = corridorRot;
                    barricadeRb.linearVelocity = Vector3.zero;
                    barricadeRb.angularVelocity = Vector3.zero;
                }
                Physics.SyncTransforms();
                barricadeRepositioned = true;

                Check(Vector3.Distance(barricade.transform.position, corridorPos) < 0.1f,
                    "Step 2: Placed Barricade_ExitArea across corridor (13 <-> 14) at (6.00, 0.65, 22.00)");

                // Stage 3: Bounded real-time polling for DynamicNavigationObstacles proxy synchronization (up to 3.0s)
                Debug.Log("[PG-01 Scenario Stage 3/8] Awaiting DynamicNavigationObstacles proxy synchronization (bounded 3.0s real-time timeout)...");
                float proxyTimeout = Time.realtimeSinceStartup + 3.0f;
                GameObject proxyObj = null;
                NavMeshObstacle proxyObstacle = null;
                bool proxySynced = false;

                while (Time.realtimeSinceStartup < proxyTimeout)
                {
                    yield return new WaitForSecondsRealtime(0.1f);
                    proxyObj = GameObject.Find("FB_NavObstacle_Barricade_ExitArea");
                    if (proxyObj != null)
                    {
                        proxyObstacle = proxyObj.GetComponent<NavMeshObstacle>();
                        if (proxyObstacle != null && proxyObstacle.carving && Vector3.Distance(proxyObj.transform.position, corridorPos) < 1.0f)
                        {
                            proxySynced = true;
                            break;
                        }
                    }
                }

                Check(proxySynced, "Step 3: Verified DynamicNavigationObstacles proxy moved to corridor with carving=true",
                    proxyObj == null ? "Proxy GameObject FB_NavObstacle_Barricade_ExitArea not found within 3.0s." :
                    proxyObstacle == null ? "NavMeshObstacle component missing on proxy." :
                    !proxyObstacle.carving ? "Proxy obstacle carving is false." :
                    $"Proxy position ({proxyObj.transform.position}) distance from target exceeds 1.0m.");

                // Stage 4: Simulate PG-01 navigating toward Node 14 and trigger obstruction failure
                Debug.Log("[PG-01 Scenario Stage 4/8] Triggering PG-01 obstruction failure along edge (13 -> 14)...");
                var agent = brain.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.Warp(new Vector3(5.0f, 0f, 22.0f));
                }
                else
                {
                    brain.transform.position = new Vector3(5.0f, 0f, 22.0f);
                }

                brain.SetTestPathState(
                    new List<int> { 13, 14 },
                    new List<Vector3> { new Vector3(5f, 0f, 22f), new Vector3(7f, 0f, 22f) },
                    1
                );

                float now = runtime != null ? runtime.GameplayTime : Time.time;
                edgeModifiedByTest = true;
                brain.TriggerObstructionFailure(now);

                bool edge13To14Blocked = IsEdgeBlockedInGraph(brain.Graph, 13, 14);
                Check(edge13To14Blocked, "Step 4: PG-01 detected obstruction failure and dynamically blocked edge (13 <-> 14)",
                    $"Edge (13 -> 14) blocked state: {edge13To14Blocked}");

                // Stage 5: Verify detour planning around the facility loop
                Debug.Log("[PG-01 Scenario Stage 5/8] Computing and verifying optimal A* detour path...");
                var detourResult = PatrolAStarPathfinder.FindPath(brain.Graph, 13, 14);
                bool detourValid = detourResult.Success &&
                                   Mathf.Approximately(detourResult.TotalCost, 10.0f) &&
                                   detourResult.NodeIds.Count == 4 &&
                                   detourResult.NodeIds[0] == 13 &&
                                   detourResult.NodeIds[1] == 12 &&
                                   detourResult.NodeIds[2] == 15 &&
                                   detourResult.NodeIds[3] == 14;

                Check(detourValid, "Step 5: PG-01 planned optimal A* detour [13 -> 12 -> 15 -> 14] with cost 10.00m",
                    $"Success={detourResult.Success}, Cost={detourResult.TotalCost:F2}, Nodes={(detourResult.NodeIds != null ? string.Join("->", detourResult.NodeIds) : "null")}");

                // Stage 6: Restore Barricade_ExitArea to original position
                Debug.Log($"[PG-01 Scenario Stage 6/8] Restoring Barricade_ExitArea to original position ({originalBarricadePos.x:F2}, {originalBarricadePos.y:F2}, {originalBarricadePos.z:F2})...");
                barricade.transform.position = originalBarricadePos;
                barricade.transform.rotation = originalBarricadeRot;
                if (barricadeRb != null)
                {
                    barricadeRb.position = originalBarricadePos;
                    barricadeRb.rotation = originalBarricadeRot;
                    barricadeRb.linearVelocity = Vector3.zero;
                    barricadeRb.angularVelocity = Vector3.zero;
                }
                Physics.SyncTransforms();
                barricadeRepositioned = false; // Barricade is back at authored location

                Check(Vector3.Distance(barricade.transform.position, originalBarricadePos) < 0.1f,
                    $"Step 6: Restored Barricade_ExitArea to original position ({originalBarricadePos.x:F2}, {originalBarricadePos.y:F2}, {originalBarricadePos.z:F2})");

                // Stage 7: Bounded real-time polling for physical corridor clearance probe (up to 3.0s)
                Debug.Log("[PG-01 Scenario Stage 7/8] Awaiting physical corridor probe confirmation (bounded 3.0s real-time timeout)...");
                float probeTimeout = Time.realtimeSinceStartup + 3.0f;
                bool corridorClear = false;
                while (Time.realtimeSinceStartup < probeTimeout)
                {
                    yield return new WaitForSecondsRealtime(0.1f);
                    if (brain.IsEdgePhysicallyClear(13, 14))
                    {
                        corridorClear = true;
                        break;
                    }
                }

                Check(corridorClear, "Step 7: Physical corridor probe confirmed edge (13, 14) is clear after barricade removal",
                    "IsEdgePhysicallyClear(13, 14) returned false after 3.0s timeout.");

                // Stage 8: Unblock edge and verify restored direct route
                Debug.Log("[PG-01 Scenario Stage 8/8] Unblocking edge (13, 14) and verifying direct route restoration...");
                brain.UnblockEdge(13, 14);
                edgeModifiedByTest = false; // Test-owned edge change cleanly restored

                var restoredResult = PatrolAStarPathfinder.FindPath(brain.Graph, 13, 14);
                bool restoredDirect = restoredResult.Success &&
                                      Mathf.Approximately(restoredResult.TotalCost, 2.0f) &&
                                      restoredResult.NodeIds.Count == 2 &&
                                      restoredResult.NodeIds[0] == 13 &&
                                      restoredResult.NodeIds[1] == 14;

                Check(restoredDirect, "Step 8: Direct optimal corridor route [13 -> 14] restored with cost 2.00m",
                    $"Success={restoredResult.Success}, Cost={restoredResult.TotalCost:F2}, Nodes={(restoredResult.NodeIds != null ? string.Join("->", restoredResult.NodeIds) : "null")}");

                // Summary
                int total = passedChecks + failedChecks;
                string summary = $"[PG-01 Obstacle Scenario] Completed {total} checks: {passedChecks} PASSED, {failedChecks} FAILED.";

                if (failedChecks == 0)
                {
                    Debug.Log($"<color=green>{summary} Milestone 2.3 Live Rerouting Verified!</color>");
                    File.WriteAllText("patrol-live-reroute-passed.txt", summary + "\nAll Milestone 2.3 live Play Mode scenario checks passed successfully.\n" + string.Join("\n", scenarioLogs) + "\n");
                }
                else
                {
                    Debug.LogError($"<color=red>{summary}</color>\nFailures:\n" + string.Join("\n", scenarioLogs));
                    File.WriteAllText("patrol-live-reroute-failed.txt", summary + "\n" + string.Join("\n", scenarioLogs) + "\n");
                }
            }
            finally
            {
                // Guaranteed safety cleanup
                if (barricade != null && barricadeRepositioned)
                {
                    barricade.transform.position = originalBarricadePos;
                    barricade.transform.rotation = originalBarricadeRot;
                    if (barricadeRb != null)
                    {
                        barricadeRb.position = originalBarricadePos;
                        barricadeRb.rotation = originalBarricadeRot;
                        barricadeRb.linearVelocity = Vector3.zero;
                        barricadeRb.angularVelocity = Vector3.zero;
                    }
                    Physics.SyncTransforms();
                    Debug.Log($"[PG-01 Scenario Cleanup] Restored Barricade_ExitArea transform to original position.");
                }

                if (brain != null && brain.Graph != null && edgeModifiedByTest)
                {
                    // Restore ONLY the specific edges modified by this test; never reset unrelated edges
                    if (initialEdge13To14Blocked)
                    {
                        brain.Graph.SetEdgeBlocked(13, 14, true);
                    }
                    else
                    {
                        brain.UnblockEdge(13, 14);
                    }
                    brain.SetTestPathState(null, null, 0);
                    Debug.Log($"[PG-01 Scenario Cleanup] Restored edge (13 <-> 14) to initial blocked state (13->14: {initialEdge13To14Blocked}).");
                }

                Destroy(gameObject);
            }
        }
    }
}
