using System;
using System.Collections.Generic;
using System.IO;
using FacilityBreach.AI.Patrol;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Pure logic and algorithmic tests for the Patrol Guard A* pathfinding system.
    /// Runs in Unity Editor without requiring Play Mode.
    /// Compatible with menu execution and automated CLI batch execution.
    /// </summary>
    public static class PatrolGuardAStarTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/Run Patrol Guard A* Tests")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("==== [PatrolGuardAStarTests] Starting Phase 1 A* Verification ====");

            Test_ShortestPath_OnSmallGraph();
            Test_AStar_ComparedWithDijkstraReference();
            Test_BlockedEdge_ChoosesAlternativePath();
            Test_DynamicEdgeReEnablement_RestoresRoute();
            Test_NoAvailableRoute_ReturnsControlledFailure();
            Test_StartEqualsGoal_ReturnsZeroCost();
            Test_InvalidNodeAndCostHandling();
            Test_DeterministicTieBreaking();
            Test_HeuristicAdmissibility();
            Test_SecurityZoneGraph_TopologyAndRouting();

            int total = passedCount + failedCount;
            string summary = $"[PatrolGuardAStarTests] Completed {total} checks: {passedCount} PASSED, {failedCount} FAILED.";

            if (failedCount == 0)
            {
                Log($"<color=green>{summary}</color>");
                File.WriteAllText("patrol-astar-tests-passed.txt", summary + "\nAll tests passed successfully.\n");
            }
            else
            {
                LogError($"<color=red>{summary}</color>\nFailures:\n" + string.Join("\n", failureMessages));
                File.WriteAllText("patrol-astar-tests-failed.txt", summary + "\n" + string.Join("\n", failureMessages) + "\n");
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
                Log($"[A* Test PASS] {testName}");
            }
            else
            {
                failedCount++;
                string message = $"FAIL: {testName}. {details}";
                failureMessages.Add(message);
                LogError($"[A* Test FAIL] {message}");
            }
        }

        private static void Test_ShortestPath_OnSmallGraph()
        {
            // Diamond graph with asymmetric costs:
            // Node 0: (0,0,0)
            // Node 1: (0,0,4)  Cost(0->1) = 4, Cost(1->3) = 4 => Path 0->1->3 cost = 8.0
            // Node 2: (4,0,0)  Cost(0->2) = 1, Cost(2->3) = 2 => Path 0->2->3 cost = 3.0
            // Node 3: (4,0,4)  Goal
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0, 0, 0), "Start");
            graph.AddNode(1, new Vector3(0, 0, 4), "North");
            graph.AddNode(2, new Vector3(4, 0, 0), "East");
            graph.AddNode(3, new Vector3(4, 0, 4), "Goal");

            graph.AddBidirectionalEdge(0, 1, 4.0f);
            graph.AddBidirectionalEdge(1, 3, 4.0f);
            graph.AddBidirectionalEdge(0, 2, 1.0f);
            graph.AddBidirectionalEdge(2, 3, 2.0f);

            var result = PatrolAStarPathfinder.FindPath(graph, 0, 3);

            Assert(result.Success, "ShortestPath_Success", "Path search should succeed.");
            Assert(Mathf.Approximately(result.TotalCost, 3.0f), "ShortestPath_Cost", $"Expected cost 3.0, got {result.TotalCost}");
            Assert(result.NodeIds.Count == 3, "ShortestPath_NodeCount", $"Expected 3 nodes, got {result.NodeIds.Count}");
            Assert(result.NodeIds[0] == 0 && result.NodeIds[1] == 2 && result.NodeIds[2] == 3,
                "ShortestPath_Sequence", "Path should traverse [0, 2, 3].");
        }

        private static void Test_AStar_ComparedWithDijkstraReference()
        {
            // Build a 5-node graph with multiple branching paths
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0, 0, 0));
            graph.AddNode(1, new Vector3(2, 0, 1));
            graph.AddNode(2, new Vector3(1, 0, 4));
            graph.AddNode(3, new Vector3(5, 0, 2));
            graph.AddNode(4, new Vector3(6, 0, 5));

            graph.AddBidirectionalEdge(0, 1, 2.5f);
            graph.AddBidirectionalEdge(1, 2, 3.2f);
            graph.AddBidirectionalEdge(0, 2, 6.0f);
            graph.AddBidirectionalEdge(1, 3, 3.1f);
            graph.AddBidirectionalEdge(2, 4, 3.5f);
            graph.AddBidirectionalEdge(3, 4, 2.8f);

            var aStarResult = PatrolAStarPathfinder.FindPath(graph, 0, 4);
            float dijkstraCost = ComputeDijkstraCost(graph, 0, 4);

            Assert(aStarResult.Success, "DijkstraComparison_Success");
            Assert(Mathf.Approximately(aStarResult.TotalCost, dijkstraCost),
                "DijkstraComparison_EqualCost",
                $"A* cost ({aStarResult.TotalCost}) must exactly match Dijkstra reference cost ({dijkstraCost}).");
        }

        private static void Test_BlockedEdge_ChoosesAlternativePath()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0, 0, 0));
            graph.AddNode(1, new Vector3(0, 0, 5));
            graph.AddNode(2, new Vector3(5, 0, 0));
            graph.AddNode(3, new Vector3(5, 0, 5));

            graph.AddBidirectionalEdge(0, 1, 5.0f);
            graph.AddBidirectionalEdge(1, 3, 5.0f);
            graph.AddBidirectionalEdge(0, 2, 2.0f);
            graph.AddBidirectionalEdge(2, 3, 2.0f);

            // Initially [0, 2, 3] with cost 4.0 is optimal
            var initialResult = PatrolAStarPathfinder.FindPath(graph, 0, 3);
            Assert(initialResult.Success && Mathf.Approximately(initialResult.TotalCost, 4.0f), "BlockedEdge_InitialOptimal");

            // Block edge (0, 2) simulating a moved barricade obstructing the corridor
            bool blocked = graph.SetEdgeBlocked(0, 2, true);
            Assert(blocked, "BlockedEdge_FlagSet", "Edge block should be applied.");

            var detourResult = PatrolAStarPathfinder.FindPath(graph, 0, 3);
            Assert(detourResult.Success, "BlockedEdge_DetourSuccess", "Detour path should succeed.");
            Assert(Mathf.Approximately(detourResult.TotalCost, 10.0f), "BlockedEdge_DetourCost", $"Expected detour cost 10.0, got {detourResult.TotalCost}");
            Assert(detourResult.NodeIds.Count == 3 && detourResult.NodeIds[1] == 1,
                "BlockedEdge_DetourSequence", "Path should detour through node 1: [0, 1, 3].");
        }

        private static void Test_DynamicEdgeReEnablement_RestoresRoute()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0, 0, 0));
            graph.AddNode(1, new Vector3(0, 0, 5));
            graph.AddNode(2, new Vector3(5, 0, 0));
            graph.AddNode(3, new Vector3(5, 0, 5));

            graph.AddBidirectionalEdge(0, 1, 5.0f);
            graph.AddBidirectionalEdge(1, 3, 5.0f);
            graph.AddBidirectionalEdge(0, 2, 2.0f);
            graph.AddBidirectionalEdge(2, 3, 2.0f);

            graph.SetEdgeBlocked(0, 2, true);
            var blockedResult = PatrolAStarPathfinder.FindPath(graph, 0, 3);
            Assert(blockedResult.NodeIds[1] == 1, "ReEnable_WasDetoured");

            // Re-enable edge (barricade pushed away or cleared)
            graph.SetEdgeBlocked(0, 2, false);
            var restoredResult = PatrolAStarPathfinder.FindPath(graph, 0, 3);
            Assert(restoredResult.Success && Mathf.Approximately(restoredResult.TotalCost, 4.0f),
                "ReEnable_RestoredOptimal", "Optimal route [0, 2, 3] should be restored after unblocking edge.");
            Assert(restoredResult.NodeIds[1] == 2, "ReEnable_CorrectSequence");
        }

        private static void Test_NoAvailableRoute_ReturnsControlledFailure()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0, 0, 0));
            graph.AddNode(1, new Vector3(2, 0, 0));
            graph.AddNode(2, new Vector3(10, 0, 10)); // Completely disconnected island

            graph.AddBidirectionalEdge(0, 1, 2.0f);

            var result = PatrolAStarPathfinder.FindPath(graph, 0, 2);
            Assert(!result.Success, "Unreachable_SuccessIsFalse", "Pathfinder must return false when goal is unreachable.");
            Assert(float.IsPositiveInfinity(result.TotalCost), "Unreachable_InfiniteCost", "Cost must be infinity on failure.");
            Assert(result.NodeIds.Count == 0, "Unreachable_EmptyNodes", "Node list must be empty on failure.");
            Assert(!string.IsNullOrEmpty(result.FailureReason), "Unreachable_HasFailureReason", "Failure reason must be documented.");
        }

        private static void Test_StartEqualsGoal_ReturnsZeroCost()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(42, new Vector3(15.5f, 1.2f, -8.3f), "Station");

            var result = PatrolAStarPathfinder.FindPath(graph, 42, 42);
            Assert(result.Success, "StartEqualsGoal_Success");
            Assert(Mathf.Approximately(result.TotalCost, 0f), "StartEqualsGoal_ZeroCost", $"Expected cost 0, got {result.TotalCost}");
            Assert(result.NodeIds.Count == 1 && result.NodeIds[0] == 42, "StartEqualsGoal_SingleNode");
            Assert(result.Waypoints.Count == 1 && result.Waypoints[0] == new Vector3(15.5f, 1.2f, -8.3f), "StartEqualsGoal_MatchingWaypoint");
        }

        private static void Test_InvalidNodeAndCostHandling()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(1, Vector3.zero);

            // 1. Missing start or goal node
            var missingResult = PatrolAStarPathfinder.FindPath(graph, 1, 999);
            Assert(!missingResult.Success && missingResult.FailureReason.Contains("999"), "InvalidNode_MissingGoal");

            var nullGraphResult = PatrolAStarPathfinder.FindPath(null, 1, 1);
            Assert(!nullGraphResult.Success && nullGraphResult.FailureReason.Contains("null"), "InvalidNode_NullGraph");

            // 2. Reject negative edge cost
            bool threwNegativeCost = false;
            try { graph.AddEdge(1, 1, -5.0f); }
            catch (ArgumentException) { threwNegativeCost = true; }
            Assert(threwNegativeCost, "InvalidCost_RejectNegativeEdge", "Negative edge cost must throw ArgumentException.");

            // 3. Reject non-finite node coordinates
            bool threwNonFinite = false;
            try { graph.AddNode(2, new Vector3(float.NaN, 0, 0)); }
            catch (ArgumentException) { threwNonFinite = true; }
            Assert(threwNonFinite, "InvalidPos_RejectNaN", "NaN node coordinates must throw ArgumentException.");
        }

        private static void Test_DeterministicTieBreaking()
        {
            // Symmetric graph with two identical-cost paths from 0 to 3:
            // Path A: 0 -> 1 -> 3 (cost = 5 + 5 = 10, pos 1 is (0, 0, 5))
            // Path B: 0 -> 2 -> 3 (cost = 5 + 5 = 10, pos 2 is (5, 0, 0))
            // Tie-breaking by lower node ID must deterministically choose Path A (node 1 < node 2).
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0, 0, 0));
            graph.AddNode(1, new Vector3(0, 0, 5));
            graph.AddNode(2, new Vector3(5, 0, 0));
            graph.AddNode(3, new Vector3(5, 0, 5));

            graph.AddBidirectionalEdge(0, 1, 5.0f);
            graph.AddBidirectionalEdge(1, 3, 5.0f);
            graph.AddBidirectionalEdge(0, 2, 5.0f);
            graph.AddBidirectionalEdge(2, 3, 5.0f);

            var firstRun = PatrolAStarPathfinder.FindPath(graph, 0, 3);
            Assert(firstRun.Success, "TieBreaking_FirstRunSuccess");

            bool allIdentical = true;
            for (int i = 0; i < 20; i++)
            {
                var run = PatrolAStarPathfinder.FindPath(graph, 0, 3);
                if (run.NodeIds.Count != firstRun.NodeIds.Count ||
                    run.NodeIds[1] != firstRun.NodeIds[1] ||
                    !Mathf.Approximately(run.TotalCost, firstRun.TotalCost))
                {
                    allIdentical = false;
                    break;
                }
            }

            Assert(allIdentical, "TieBreaking_DeterministicAcross20Runs", "Tie-breaking must produce identical results on repeated runs.");
            Assert(firstRun.NodeIds[1] == 1, "TieBreaking_PrefersLowerNodeId", "Deterministic tie-breaking should select lower node ID 1 over 2.");
        }

        private static void Test_HeuristicAdmissibility()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            bool strictlyAdmissible = true;
            string violationDetail = "";

            var nodeList = new List<PatrolNode>(graph.Nodes);
            for (int i = 0; i < nodeList.Count; i++)
            {
                for (int j = 0; j < nodeList.Count; j++)
                {
                    int u = nodeList[i].Id;
                    int v = nodeList[j].Id;

                    float h = PatrolAStarPathfinder.Heuristic(nodeList[i].Position, nodeList[j].Position);
                    var result = PatrolAStarPathfinder.FindPath(graph, u, v);

                    if (result.Success)
                    {
                        // Admissibility condition: h(u, v) <= true shortest distance h*(u, v)
                        if (h > result.TotalCost + 0.001f)
                        {
                            strictlyAdmissible = false;
                            violationDetail = $"Heuristic violation: h({u}, {v}) = {h:F3} > trueCost = {result.TotalCost:F3}";
                            break;
                        }
                    }
                }
                if (!strictlyAdmissible) break;
            }

            Assert(strictlyAdmissible, "Heuristic_StrictlyAdmissible", violationDetail);
        }

        private static void Test_SecurityZoneGraph_TopologyAndRouting()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();

            // Verify node count and core post presence
            Assert(graph.NodeCount == 9, "SecurityZone_NodeCount", $"Expected 9 security nodes, got {graph.NodeCount}");
            Assert(graph.ContainsNode(FacilityPatrolGraphFactory.Node_PG_Spawn), "SecurityZone_ContainsSpawnPost");

            // Verify primary patrol loop connectivity: 12 -> 13 -> 14 -> 15 -> 12
            var pathLoop = PatrolAStarPathfinder.FindPath(graph,
                FacilityPatrolGraphFactory.Node_PG_Spawn,
                FacilityPatrolGraphFactory.Node_PG_Route_2);

            Assert(pathLoop.Success, "SecurityZone_PatrolRouteSuccess");
            Assert(pathLoop.NodeIds.Count == 3, "SecurityZone_PatrolRouteNodeCount");
            Assert(pathLoop.NodeIds[0] == 12 && pathLoop.NodeIds[1] == 13 && pathLoop.NodeIds[2] == 14,
                "SecurityZone_PatrolRouteSequence", "Path should traverse [12 -> 13 -> 14].");
            Assert(Mathf.Approximately(pathLoop.TotalCost, 6.0f), "SecurityZone_PatrolRouteCost",
                $"Expected cost 6.0m (4m + 2m), got {pathLoop.TotalCost:F2}");

            // Verify Room 3 alternate exit bypass
            var exitPath = PatrolAStarPathfinder.FindPath(graph,
                FacilityPatrolGraphFactory.Node_PG_Spawn,
                FacilityPatrolGraphFactory.Node_Exit_Approach);
            Assert(exitPath.Success, "SecurityZone_ExitApproachReachable");
        }

        /// <summary>
        /// Reference Dijkstra algorithm (zero heuristic) for mathematical ground-truth cost validation.
        /// </summary>
        private static float ComputeDijkstraCost(PatrolAStarGraph graph, int start, int goal)
        {
            var dist = new Dictionary<int, float>();
            var visited = new HashSet<int>();
            var pq = new SortedSet<(float cost, int id)>();

            foreach (var n in graph.Nodes) dist[n.Id] = float.PositiveInfinity;
            dist[start] = 0f;
            pq.Add((0f, start));

            while (pq.Count > 0)
            {
                var current = pq.Min;
                pq.Remove(current);

                if (current.id == goal) return current.cost;
                if (!visited.Add(current.id)) continue;

                foreach (var edge in graph.GetEdges(current.id))
                {
                    if (edge.IsBlocked) continue;
                    float newDist = current.cost + edge.EffectiveCost;
                    if (newDist < dist[edge.To])
                    {
                        pq.Remove((dist[edge.To], edge.To));
                        dist[edge.To] = newDist;
                        pq.Add((newDist, edge.To));
                    }
                }
            }

            return dist[goal];
        }
    }
}

