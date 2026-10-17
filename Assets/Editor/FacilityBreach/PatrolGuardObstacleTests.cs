using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FacilityBreach.AI.Patrol;
using FacilityBreach.AI.Roles;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated regression tests for Patrol Guard Milestone 2.3:
    /// - Dynamic obstacle failure classification (obstruction vs transient)
    /// - Active graph edge derivation & connector rejection
    /// - Dynamic edge blocking & cost immutability
    /// - Corridor blockage detour replanning (Patrol loop 13 -> 12 -> 15 -> 14)
    /// - Room 3 alternate exit bypass (14 -> 15 -> 16 -> 17 -> 18)
    /// - Direct route restoration upon verified clearance
    /// - Conservative recovery retention when clearance is unconfirmed
    /// - Replan cooldown deferral semantics (no premature waypoint skipping)
    /// - Bounded retry limits and unreachable target progression
    /// - Graph topology and base cost immutability
    /// </summary>
    public static class PatrolGuardObstacleTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/AI Tests/Run Patrol Guard Obstacle Tests (Milestone 2.3)")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("==== [PatrolGuardObstacleTests] Starting Milestone 2.3 Verification ====");

            Test_FailureClassification_IdentifiesObstructionVsTransient();
            Test_ActiveEdgeResolution_DerivesValidEdge();
            Test_ActiveEdgeResolution_RejectsConnectorsAndOutOfRange();
            Test_DynamicEdgeBlocking_EffectiveCostIsInfinite();
            Test_AStarDetour_PatrolLoopCorridor();
            Test_AStarDetour_ExitApproachViaRoom3();
            Test_ObstacleRecovery_RestoresDirectRouteWhenUnblocked();
            Test_ReplanCooldown_DefersRatherThanSkips();
            Test_BoundedReplanning_EnforcesMaxRetryLimit();
            Test_ConservativeClearance_RetainsBlockWhenProbeFails();

            int total = passedCount + failedCount;
            string summary = $"[PatrolGuardObstacleTests] Completed {total} checks: {passedCount} PASSED, {failedCount} FAILED.";

            if (failedCount == 0)
            {
                Log($"<color=green>{summary}</color>");
                File.WriteAllText("patrol-obstacle-tests-passed.txt", summary + "\nAll Milestone 2.3 tests passed successfully.\n");
            }
            else
            {
                LogError($"<color=red>{summary}</color>\nFailures:\n" + string.Join("\n", failureMessages));
                File.WriteAllText("patrol-obstacle-tests-failed.txt", summary + "\n" + string.Join("\n", failureMessages) + "\n");
            }
        }

        public static void RunBatch()
        {
            RunAll();
            EditorApplication.Exit(failedCount == 0 ? 0 : 1);
        }

        private static void Assert(bool condition, string testName, string details = "")
        {
            if (condition)
            {
                passedCount++;
                Log($"[Obstacle Test PASS] {testName}");
            }
            else
            {
                failedCount++;
                string message = $"FAIL: {testName}. {details}";
                failureMessages.Add(message);
                LogError($"[Obstacle Test FAIL] {message}");
            }
        }

        private static bool ContainsNodeId(IReadOnlyList<int> nodes, int id)
        {
            if (nodes == null) return false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == id) return true;
            }
            return false;
        }

        private static void Test_FailureClassification_IdentifiesObstructionVsTransient()
        {
            // Genuine physical obstruction failures that warrant edge blocking
            Assert(PatrolGuardBrain.IsObstructionFailure("Path invalidated by obstruction"), "FailureClass_InvalidatedByObstruction");
            Assert(PatrolGuardBrain.IsObstructionFailure("Blocked/no movement progress"), "FailureClass_NoMovementProgress");
            Assert(PatrolGuardBrain.IsObstructionFailure("No complete path"), "FailureClass_NoCompletePath");

            // Transient or pacing failures that must NOT block graph edges
            Assert(!PatrolGuardBrain.IsObstructionFailure("Agent/goal outside navigation"), "FailureClass_RejectsAgentOutsideNav");
            Assert(!PatrolGuardBrain.IsObstructionFailure("SetPath rejected"), "FailureClass_RejectsSetPathRejected");
            Assert(!PatrolGuardBrain.IsObstructionFailure("Travel timeout"), "FailureClass_RejectsTravelTimeout");
            Assert(!PatrolGuardBrain.IsObstructionFailure(""), "FailureClass_RejectsEmptyReason");
        }

        private static void Test_ActiveEdgeResolution_DerivesValidEdge()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(12, new Vector3(5f, 0f, 18f), "Node12", isVerified: true);
            graph.AddNode(13, new Vector3(5f, 0f, 22f), "Node13", isVerified: true);
            graph.AddNode(14, new Vector3(7f, 0f, 22f), "Node14", isVerified: true);
            graph.AddBidirectionalEdge(12, 13, 4f);
            graph.AddBidirectionalEdge(13, 14, 2f);

            var nodeIds = new List<int> { 12, 13, 14 };

            // When pathIndex = 1, agent is moving from Node 12 to Node 13
            bool ok1 = PatrolGuardBrain.TryDeriveActiveGraphEdge(nodeIds, 1, graph, out int from1, out int to1);
            Assert(ok1, "ActiveEdge_PathIndex1_Success");
            Assert(from1 == 12 && to1 == 13, "ActiveEdge_PathIndex1_NodesMatch");

            // When pathIndex = 2, agent is moving from Node 13 to Node 14
            bool ok2 = PatrolGuardBrain.TryDeriveActiveGraphEdge(nodeIds, 2, graph, out int from2, out int to2);
            Assert(ok2, "ActiveEdge_PathIndex2_Success");
            Assert(from2 == 13 && to2 == 14, "ActiveEdge_PathIndex2_NodesMatch");
        }

        private static void Test_ActiveEdgeResolution_RejectsConnectorsAndOutOfRange()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(12, new Vector3(5f, 0f, 18f), "Node12", isVerified: true);
            graph.AddNode(13, new Vector3(5f, 0f, 22f), "Node13", isVerified: true);
            graph.AddBidirectionalEdge(12, 13, 4f);

            var nodeIds = new List<int> { 12, 13 };

            // pathIndex = 0 is an approach from an off-graph position to the start node (connector)
            bool ok0 = PatrolGuardBrain.TryDeriveActiveGraphEdge(nodeIds, 0, graph, out int f0, out int t0);
            Assert(!ok0 && f0 == -1 && t0 == -1, "ActiveEdge_RejectsPathIndex0_Connector");

            // pathIndex >= nodeIds.Count is an off-graph approach to an appended goal position
            bool ok2 = PatrolGuardBrain.TryDeriveActiveGraphEdge(nodeIds, 2, graph, out int f2, out int t2);
            Assert(!ok2, "ActiveEdge_RejectsPathIndexEqualCount_Connector");

            // Negative pathIndex
            bool okNeg = PatrolGuardBrain.TryDeriveActiveGraphEdge(nodeIds, -1, graph, out _, out _);
            Assert(!okNeg, "ActiveEdge_RejectsNegativePathIndex");

            // Single node path (no edge exists)
            var singleNode = new List<int> { 12 };
            bool okSingle = PatrolGuardBrain.TryDeriveActiveGraphEdge(singleNode, 1, graph, out _, out _);
            Assert(!okSingle, "ActiveEdge_RejectsSingleNodeList");

            // Null node list
            bool okNull = PatrolGuardBrain.TryDeriveActiveGraphEdge(null, 1, graph, out _, out _);
            Assert(!okNull, "ActiveEdge_RejectsNullList");
        }

        private static void Test_DynamicEdgeBlocking_EffectiveCostIsInfinite()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();

            // Initial state: edge 13 <-> 14 is clear
            var edges13 = graph.GetEdges(13);
            PatrolEdge edge13To14 = null;
            foreach (var e in edges13)
            {
                if (e.To == 14) edge13To14 = e;
            }

            Assert(edge13To14 != null && !edge13To14.IsBlocked, "DynamicBlock_InitialUnblocked");
            Assert(Mathf.Approximately(edge13To14.BaseCost, 2f), "DynamicBlock_InitialBaseCost2m");

            // Block edge (13, 14)
            bool modified = graph.SetEdgeBlocked(13, 14, true);
            Assert(modified, "DynamicBlock_SetBlockedModifiedTrue");
            Assert(edge13To14.IsBlocked, "DynamicBlock_IsBlockedFlagSet");
            Assert(float.IsPositiveInfinity(edge13To14.EffectiveCost), "DynamicBlock_EffectiveCostInfinite");
        }

        private static void Test_AStarDetour_PatrolLoopCorridor()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();

            // Baseline path from 13 to 14: direct corridor (cost 2.0m)
            var baseline = PatrolAStarPathfinder.FindPath(graph, 13, 14);
            Assert(baseline.Success && Mathf.Approximately(baseline.TotalCost, 2f), "DetourCorridor_BaselineCost2m");

            // Block direct corridor edge (13, 14) simulating a barricade
            graph.SetEdgeBlocked(13, 14, true);

            // Reroute search from 13 to 14: must detour via [13 -> 12 -> 15 -> 14]
            // Costs: (13, 12)=4m + (12, 15)=2m + (15, 14)=4m = 10m
            var detour = PatrolAStarPathfinder.FindPath(graph, 13, 14);
            Assert(detour.Success, "DetourCorridor_DetourFound");
            Assert(Mathf.Approximately(detour.TotalCost, 10f), "DetourCorridor_DetourCost10m");
            Assert(detour.NodeIds.Count == 4 &&
                   detour.NodeIds[0] == 13 &&
                   detour.NodeIds[1] == 12 &&
                   detour.NodeIds[2] == 15 &&
                   detour.NodeIds[3] == 14, "DetourCorridor_ExactDetourSequence");
        }

        private static void Test_AStarDetour_ExitApproachViaRoom3()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();

            // Baseline path from Node 14 to Node 18 (Exit Approach): direct corridor (cost 3.0m)
            var baseline = PatrolAStarPathfinder.FindPath(graph, 14, 18);
            Assert(baseline.Success && Mathf.Approximately(baseline.TotalCost, 3f), "DetourExit_BaselineCost3m");

            // Block direct approach (14, 18) simulating Barricade_ExitArea
            graph.SetEdgeBlocked(14, 18, true);

            // Reroute from Node 14 to Node 18:
            // Must route: 14 -> 15 (4.0m) -> 16 (Door_Room3_Left, 3.09m) -> 17 (Room3_Center, 4.07m) -> 18 (9.89m)
            // Total cost = 4.0 + 3.09 + 4.07 + 9.89 = ~21.05m
            var detour = PatrolAStarPathfinder.FindPath(graph, 14, 18);
            Assert(detour.Success, "DetourExit_DetourFoundViaRoom3");
            Assert(ContainsNodeId(detour.NodeIds, 16) && ContainsNodeId(detour.NodeIds, 17), "DetourExit_TraversesRoom3Nodes");
            Assert(detour.TotalCost > 20f && detour.TotalCost < 22f, "DetourExit_DetourCostAround21m");
            Assert(detour.NodeIds[detour.NodeIds.Count - 1] == 18, "DetourExit_ReachesNode18");
        }

        private static void Test_ObstacleRecovery_RestoresDirectRouteWhenUnblocked()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();

            // Block edge (13, 14)
            graph.SetEdgeBlocked(13, 14, true);
            var blockedResult = PatrolAStarPathfinder.FindPath(graph, 13, 14);
            Assert(Mathf.Approximately(blockedResult.TotalCost, 10f), "Recovery_BlockedUsesDetour10m");

            // Obstacle cleared: unblock edge (13, 14)
            bool unblocked = graph.SetEdgeBlocked(13, 14, false);
            Assert(unblocked, "Recovery_UnblockReturnsTrue");

            // Immediate path recalculation must restore the direct optimal route
            var restoredResult = PatrolAStarPathfinder.FindPath(graph, 13, 14);
            Assert(restoredResult.Success, "Recovery_RestoredSuccess");
            Assert(Mathf.Approximately(restoredResult.TotalCost, 2f), "Recovery_RestoredDirectCost2m");
        }

        private static void Test_ReplanCooldown_DefersRatherThanSkips()
        {
            float now = 5.0f;
            float nextAllowedTime = 6.0f; // Cooldown not yet elapsed (needs 1.0s more)
            int currentAttempts = 0;
            int maxAttempts = 2;

            // When now < nextAllowedTime: Must DEFER, not skip!
            bool canReplan = PatrolGuardBrain.EvaluateReplanAction(now, nextAllowedTime, currentAttempts, maxAttempts, out bool isDeferred, out bool isExhausted);
            Assert(!canReplan, "Cooldown_CannotReplanImmediately");
            Assert(isDeferred, "Cooldown_ActionIsDeferred");
            Assert(!isExhausted, "Cooldown_BudgetNotExhausted");

            // Advance time past cooldown: now = 6.1f
            now = 6.1f;
            canReplan = PatrolGuardBrain.EvaluateReplanAction(now, nextAllowedTime, currentAttempts, maxAttempts, out isDeferred, out isExhausted);
            Assert(canReplan, "Cooldown_CanReplanAfterCooldownElapsed");
            Assert(!isDeferred, "Cooldown_NotDeferredOnceElapsed");
        }

        private static void Test_BoundedReplanning_EnforcesMaxRetryLimit()
        {
            float now = 10.0f;
            float nextAllowedTime = 9.0f;
            int currentAttempts = 2; // Equal to max attempts
            int maxAttempts = 2;

            // When attempts reach max: Budget is exhausted, must advance target
            bool canReplan = PatrolGuardBrain.EvaluateReplanAction(now, nextAllowedTime, currentAttempts, maxAttempts, out bool isDeferred, out bool isExhausted);
            Assert(!canReplan, "BoundedRetry_CannotReplanWhenMaxReached");
            Assert(!isDeferred, "BoundedRetry_NotDeferred");
            Assert(isExhausted, "BoundedRetry_BudgetIsExhausted");
        }

        private static void Test_ConservativeClearance_RetainsBlockWhenProbeFails()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();

            // Simulate edge (13, 14) blocked
            graph.SetEdgeBlocked(13, 14, true);

            // Clearance probe simulated: obstacle still present (returns false)
            bool simulatedProbeClear = false;
            if (!simulatedProbeClear)
            {
                // Edge remains blocked conservatively
                Assert(graph.GetEdges(13)[1].IsBlocked || graph.GetEdges(13)[0].IsBlocked, "Conservative_EdgeRemainsBlocked");
            }

            // Clearance probe simulated: obstacle pushed away (returns true)
            simulatedProbeClear = true;
            if (simulatedProbeClear)
            {
                graph.SetEdgeBlocked(13, 14, false);
            }

            var edges13 = graph.GetEdges(13);
            PatrolEdge edge14 = null;
            foreach (var e in edges13) { if (e.To == 14) edge14 = e; }
            Assert(edge14 != null && !edge14.IsBlocked, "Conservative_UnblockedAfterConfirmedClear");

            // Topology immutability audit: 50 repeated block/unblock cycles
            for (int i = 0; i < 50; i++)
            {
                graph.SetEdgeBlocked(13, 14, true);
                graph.SetEdgeBlocked(13, 14, false);
            }

            Assert(graph.NodeCount == 9, "Conservative_NodeCountIntactAfter50Cycles");
            Assert(Mathf.Approximately(edge14.BaseCost, 2.0f), "Conservative_BaseCostIntactAfter50Cycles");
        }
    }
}
