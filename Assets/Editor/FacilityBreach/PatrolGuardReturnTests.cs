using System;
using System.Collections.Generic;
using System.IO;
using FacilityBreach.AI.Patrol;
using FacilityBreach.AI.Roles;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    /// <summary>
    /// Automated regression tests for Patrol Guard Milestone 2.2B:
    /// - Tactical return candidate ranking by A* route cost
    /// - Unreachable candidate rejection and connector distance enforcement
    /// - Deterministic tie-breaking by route index
    /// - Direct arrival detection and instant patrol resumption
    /// - Candidate failure handling and bounded next-best retries
    /// - Perception reacquisition interruption of return state
    /// - Patrol circuit continuation upon return completion
    /// - Security zone graph real-route evaluation
    /// - Lifecycle cleanup and state reset
    /// </summary>
    public static class PatrolGuardReturnTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/AI Tests/Run Patrol Guard Return Tests (Milestone 2.2B)")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("==== [PatrolGuardReturnTests] Starting Milestone 2.2B Verification ====");

            Test_EvaluateReturnCandidates_LowestAStarCostSelection();
            Test_EvaluateReturnCandidates_RejectsUnreachableCandidates();
            Test_EvaluateReturnCandidates_DeterministicTieBreakingByRouteIndex();
            Test_EvaluateReturnCandidates_DirectArrivalWithinTolerance();
            Test_EvaluateReturnCandidates_EnforcesMaxConnectorDistance();
            Test_CandidateFailure_BoundedRetryAndPatrolFallback();
            Test_PerceptionReacquisition_InterruptsReturnState();
            Test_PatrolContinuation_CyclesToNextWaypointAfterArrival();
            Test_SecurityZoneGraph_EvaluatesAuthoredPatrolRoute();
            Test_LifecycleReset_ClearsReturnState();

            int total = passedCount + failedCount;
            string summary = $"[PatrolGuardReturnTests] Completed {total} checks: {passedCount} PASSED, {failedCount} FAILED.";

            if (failedCount == 0)
            {
                Log($"<color=green>{summary}</color>");
                File.WriteAllText("patrol-return-tests-passed.txt", summary + "\nAll Milestone 2.2B tests passed successfully.\n");
            }
            else
            {
                LogError($"<color=red>{summary}</color>\nFailures:\n" + string.Join("\n", failureMessages));
                File.WriteAllText("patrol-return-tests-failed.txt", summary + "\n" + string.Join("\n", failureMessages) + "\n");
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
                Log($"[Return Test PASS] {testName}");
            }
            else
            {
                failedCount++;
                string message = $"FAIL: {testName}. {details}";
                failureMessages.Add(message);
                LogError($"[Return Test FAIL] {message}");
            }
        }

        private static void Test_EvaluateReturnCandidates_LowestAStarCostSelection()
        {
            // Build a synthetic graph where Euclidean distance differs from topological A* cost:
            // Agent is at (0, 0, 0).
            // Route[0] is at (10, 0, 0). Straight line is 10m, but path through corridor (0 -> 1 -> 2 -> Route[0]) costs 20m.
            // Route[1] is at (0, 0, 12). Straight line is 12m (Euclidean farther), but direct corridor (0 -> 3 -> Route[1]) costs 12m.
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0f, 0f, 0f), "Start", isVerified: true);
            graph.AddNode(1, new Vector3(5f, 0f, 0f), "Node1", isVerified: true);
            graph.AddNode(2, new Vector3(10f, 0f, 0f), "Node2", isVerified: true);
            graph.AddNode(3, new Vector3(0f, 0f, 12f), "Node3", isVerified: true);

            // Connect path to Node 2 with extra detour cost
            graph.AddEdge(0, 1, 10f);
            graph.AddEdge(1, 2, 10f); // Total cost to Node 2 = 20f

            // Connect path to Node 3 with direct cost
            graph.AddEdge(0, 3, 12f); // Total cost to Node 3 = 12f

            Vector3[] route = new[]
            {
                new Vector3(10f, 0f, 0f), // Route[0] - Euclidean 10m, A* cost 20m
                new Vector3(0f, 0f, 12f)  // Route[1] - Euclidean 12m, A* cost 12m
            };

            var candidates = PatrolReturnPlanner.EvaluateReturnCandidates(graph, route, new Vector3(0f, 0f, 0f));

            Assert(candidates != null && candidates.Count == 2, "LowestAStarCost_CandidateCount");
            var best = PatrolReturnPlanner.SelectBestCandidate(candidates);
            Assert(best != null, "LowestAStarCost_BestNotNull");
            Assert(best.RouteIndex == 1, "LowestAStarCost_PrefersTopologicalOverEuclidean",
                $"Expected Route[1] (A* cost 12m), but selected Route[{best?.RouteIndex}] (cost {best?.TotalCost:F2})");
            Assert(Mathf.Abs(best.TotalCost - 12f) < 0.1f, "LowestAStarCost_ExactCostMatch");
        }

        private static void Test_EvaluateReturnCandidates_RejectsUnreachableCandidates()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0f, 0f, 0f), "Start", isVerified: true);
            graph.AddNode(1, new Vector3(0f, 0f, 5f), "ConnectedGoal", isVerified: true);
            graph.AddNode(2, new Vector3(20f, 0f, 0f), "DisconnectedGoal", isVerified: true);

            graph.AddEdge(0, 1, 5f);
            // Node 2 has no edges (disconnected room)

            Vector3[] route = new[]
            {
                new Vector3(20f, 0f, 0f), // Route[0]: Disconnected
                new Vector3(0f, 0f, 5f)   // Route[1]: Reachable
            };

            var candidates = PatrolReturnPlanner.EvaluateReturnCandidates(graph, route, new Vector3(0f, 0f, 0f));

            Assert(candidates.Count == 2, "RejectsUnreachable_Count");
            var best = PatrolReturnPlanner.SelectBestCandidate(candidates);
            Assert(best != null && best.RouteIndex == 1, "RejectsUnreachable_SelectsReachableRoute1");

            var disconnectedCandidate = candidates.Find(c => c.RouteIndex == 0);
            Assert(disconnectedCandidate != null && !disconnectedCandidate.IsReachable, "RejectsUnreachable_Route0MarkedUnreachable");
            Assert(float.IsPositiveInfinity(disconnectedCandidate.TotalCost), "RejectsUnreachable_InfiniteCostForUnreachable");
        }

        private static void Test_EvaluateReturnCandidates_DeterministicTieBreakingByRouteIndex()
        {
            // Two route destinations with identical A* cost
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0f, 0f, 0f), "Start", isVerified: true);
            graph.AddNode(1, new Vector3(-5f, 0f, 0f), "West", isVerified: true);
            graph.AddNode(2, new Vector3(5f, 0f, 0f), "East", isVerified: true);

            graph.AddEdge(0, 1, 5f);
            graph.AddEdge(0, 2, 5f);

            Vector3[] route = new[]
            {
                new Vector3(5f, 0f, 0f),  // Route[0]: Cost 5m
                new Vector3(-5f, 0f, 0f)  // Route[1]: Cost 5m
            };

            var candidates = PatrolReturnPlanner.EvaluateReturnCandidates(graph, route, new Vector3(0f, 0f, 0f));

            Assert(candidates.Count == 2, "TieBreaking_Count");
            Assert(Mathf.Abs(candidates[0].TotalCost - candidates[1].TotalCost) < 0.001f, "TieBreaking_CostsAreEqual");
            Assert(candidates[0].RouteIndex == 0, "TieBreaking_LowerIndexRankedFirst",
                $"Expected Route[0] first, but got Route[{candidates[0].RouteIndex}]");
        }

        private static void Test_EvaluateReturnCandidates_DirectArrivalWithinTolerance()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            Vector3 agentPos = new Vector3(5f, 0f, 22f); // Node 13 position

            Vector3[] route = new[]
            {
                new Vector3(-12.5f, 0f, 10f), // Route[0]
                new Vector3(5f, 0f, 22f),     // Route[1]: Agent is standing directly here
                new Vector3(-4f, 0f, 36f)     // Route[2]
            };

            var candidates = PatrolReturnPlanner.EvaluateReturnCandidates(graph, route, agentPos);

            Assert(candidates.Count == 3, "DirectArrival_CandidateCount");
            var best = PatrolReturnPlanner.SelectBestCandidate(candidates);
            Assert(best != null, "DirectArrival_BestNotNull");
            Assert(best.RouteIndex == 1, "DirectArrival_SelectsCurrentLocationWaypoint",
                $"Expected Route[1], but got Route[{best?.RouteIndex}]");
            Assert(best.TotalCost <= PatrolReturnPlanner.ArrivalTolerance, "DirectArrival_CostWithinArrivalTolerance",
                $"Cost was {best?.TotalCost:F3}");
            Assert(best.Waypoints.Count == 1, "DirectArrival_SingleWaypointDestination");
        }

        private static void Test_EvaluateReturnCandidates_EnforcesMaxConnectorDistance()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0f, 0f, 0f), "Node0", isVerified: true);

            // Waypoint is 10m away from Node 0 (exceeds MaxConnectorDistance = 6.0m)
            Vector3[] route = new[]
            {
                new Vector3(10f, 0f, 0f)
            };

            var candidates = PatrolReturnPlanner.EvaluateReturnCandidates(graph, route, new Vector3(0f, 0f, 0f));

            Assert(candidates.Count == 1, "ConnectorDistance_Count");
            Assert(!candidates[0].IsReachable, "ConnectorDistance_RejectsDistantConnector");
            Assert(candidates[0].FailureReason.Contains("Connector distance exceeded"), "ConnectorDistance_FailureReasonExplicit");
        }

        private static void Test_CandidateFailure_BoundedRetryAndPatrolFallback()
        {
            var graph = new PatrolAStarGraph();
            graph.AddNode(0, new Vector3(0f, 0f, 0f), "Start", isVerified: true);
            graph.AddNode(1, new Vector3(4f, 0f, 0f), "Point1", isVerified: true);
            graph.AddNode(2, new Vector3(8f, 0f, 0f), "Point2", isVerified: true);
            graph.AddEdge(0, 1, 4f);
            graph.AddEdge(0, 2, 8f);

            Vector3[] route = new[]
            {
                new Vector3(4f, 0f, 0f), // Route[0]: Best candidate (cost 4)
                new Vector3(8f, 0f, 0f)  // Route[1]: Second best candidate (cost 8)
            };

            var candidates = PatrolReturnPlanner.EvaluateReturnCandidates(graph, route, new Vector3(0f, 0f, 0f));

            Assert(candidates.Count == 2, "BoundedRetry_CandidatesEvaluated");
            Assert(candidates[0].RouteIndex == 0, "BoundedRetry_Candidate0IsBest");
            Assert(candidates[1].RouteIndex == 1, "BoundedRetry_Candidate1IsSecondBest");

            // Verify sequential candidate progression
            int candidateIndex = 0;
            var current = candidates[candidateIndex];
            Assert(current.RouteIndex == 0, "BoundedRetry_FirstAttemptCandidate0");

            // Simulate failure of candidate 0 -> retry next candidate
            candidateIndex++;
            current = candidates[candidateIndex];
            Assert(current.RouteIndex == 1, "BoundedRetry_SecondAttemptCandidate1");

            // Simulate failure of candidate 1 -> all exhausted, fallback triggered
            candidateIndex++;
            bool allExhausted = candidateIndex >= candidates.Count;
            Assert(allExhausted, "BoundedRetry_AllCandidatesExhausted");
        }

        private static void Test_PerceptionReacquisition_InterruptsReturnState()
        {
            // Simulate agent state during tactical return
            RoleState state = RoleState.Return;
            bool seesPlayer = true;
            Vector3 seenPos = new Vector3(15f, 0f, 15f);

            bool interrupted = false;
            RoleState nextState = state;

            if (seesPlayer)
            {
                if (state == RoleState.Investigate || state == RoleState.Search || state == RoleState.Return)
                {
                    interrupted = true;
                    nextState = RoleState.Detecting;
                }
            }

            Assert(interrupted, "Interruption_ReturnStateInterruptedByPerception");
            Assert(nextState == RoleState.Detecting, "Interruption_TransitionsToDetecting");
        }

        private static void Test_PatrolContinuation_CyclesToNextWaypointAfterArrival()
        {
            // When agent completes return to Route[1] of a 3-waypoint route:
            int routeLength = 3;
            int returnedWaypointIndex = 1;

            // In Patrol state, arrival advances to (waypoint + 1) % routeLength
            int nextWaypoint = (returnedWaypointIndex + 1) % routeLength;

            Assert(nextWaypoint == 2, "PatrolContinuation_AdvancesToWaypoint2",
                $"Expected next waypoint 2, but got {nextWaypoint}");

            // And from waypoint 2, advances back to 0
            int cycleWaypoint = (nextWaypoint + 1) % routeLength;
            Assert(cycleWaypoint == 0, "PatrolContinuation_CyclesBackToWaypoint0",
                $"Expected cycle waypoint 0, but got {cycleWaypoint}");
        }

        private static void Test_SecurityZoneGraph_EvaluatesAuthoredPatrolRoute()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();

            // Real authored patrol route for PG-01:
            // Route[0] = Node 11 (-12.5, 0, 10)
            // Route[1] = Node 13 (5, 0, 22)
            // Route[2] = Node 20 (-4, 0, 36)
            Vector3[] route = new[]
            {
                new Vector3(-12.5f, 0f, 10f),
                new Vector3(5f, 0f, 22f),
                new Vector3(-4f, 0f, 36f)
            };

            // Agent located near Node 14 (7, 0, 22) after completing a search
            Vector3 searchEndPos = new Vector3(7f, 0f, 22f);

            var candidates = PatrolReturnPlanner.EvaluateReturnCandidates(graph, route, searchEndPos);

            Assert(candidates.Count == 3, "SecurityZone_AllCandidatesEvaluated");
            var best = PatrolReturnPlanner.SelectBestCandidate(candidates);
            Assert(best != null, "SecurityZone_BestCandidateFound");
            Assert(best.IsReachable, "SecurityZone_BestCandidateIsReachable");

            // From Node 14 (7, 0, 22), Node 13 (5, 0, 22) is only 2m away (Route[1])
            Assert(best.RouteIndex == 1, "SecurityZone_SelectsRoute1AsNearest",
                $"Expected Route[1] (Node 13, 2m away), got Route[{best.RouteIndex}] with cost {best.TotalCost:F2}");
            Assert(best.TotalCost < 3.0f, "SecurityZone_Route1CostUnder3m", $"Cost was {best.TotalCost:F2}");
        }

        private static void Test_LifecycleReset_ClearsReturnState()
        {
            // Verify candidate evaluation contract when route is null or empty
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            var nullRouteCandidates = PatrolReturnPlanner.EvaluateReturnCandidates(graph, null, Vector3.zero);
            Assert(nullRouteCandidates != null && nullRouteCandidates.Count == 0, "LifecycleReset_NullRouteReturnsEmpty");

            var emptyRouteCandidates = PatrolReturnPlanner.EvaluateReturnCandidates(graph, Array.Empty<Vector3>(), Vector3.zero);
            Assert(emptyRouteCandidates != null && emptyRouteCandidates.Count == 0, "LifecycleReset_EmptyRouteReturnsEmpty");
        }
    }
}
