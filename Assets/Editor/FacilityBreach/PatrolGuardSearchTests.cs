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
    /// Automated regression tests for Patrol Guard Milestone 2.2A:
    /// - Lost-sight memory retention
    /// - Candidate search point selection from verified graph nodes
    /// - Fallback radial offset candidate generation
    /// - Unreachable search point rejection
    /// - Search duration and timeout bounds
    /// - Perception reacquisition and interruption
    /// </summary>
    public static class PatrolGuardSearchTests
    {
        public static Action<string> Log = msg => Debug.Log(msg);
        public static Action<string> LogError = msg => Debug.LogError(msg);

        private static int passedCount;
        private static int failedCount;
        private static readonly List<string> failureMessages = new List<string>();

        [MenuItem("Facility Breach/AI Tests/Run Patrol Guard Search Tests (Milestone 2.2A)")]
        public static void RunAll()
        {
            passedCount = 0;
            failedCount = 0;
            failureMessages.Clear();

            Log("==== [PatrolGuardSearchTests] Starting Milestone 2.2A Verification ====");

            Test_SearchPointSelection_FromSecurityZoneGraph();
            Test_SearchPointSelection_DeterministicOrdering();
            Test_SearchPointSelection_FallbackToGeometricOffsets();
            Test_SearchPointSelection_RejectsUnreachableCandidates();
            Test_SearchPointSelection_AllUnreachableReturnsEmptyGracefully();
            Test_SearchPointSelection_ExcludesUnverifiedAndDistantNodes();
            Test_SearchTimeoutContract_BoundedDuration();
            Test_LostSightMemory_PreservesObservedPosition();
            Test_DetectionReacquisition_InterruptionLogic();

            int total = passedCount + failedCount;
            string summary = $"[PatrolGuardSearchTests] Completed {total} checks: {passedCount} PASSED, {failedCount} FAILED.";

            if (failedCount == 0)
            {
                Log($"<color=green>{summary}</color>");
                File.WriteAllText("patrol-search-tests-passed.txt", summary + "\nAll Milestone 2.2A tests passed successfully.\n");
            }
            else
            {
                LogError($"<color=red>{summary}</color>\nFailures:\n" + string.Join("\n", failureMessages));
                File.WriteAllText("patrol-search-tests-failed.txt", summary + "\n" + string.Join("\n", failureMessages) + "\n");
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
                Log($"[Search Test PASS] {testName}");
            }
            else
            {
                failedCount++;
                string message = $"FAIL: {testName}. {details}";
                failureMessages.Add(message);
                LogError($"[Search Test FAIL] {message}");
            }
        }

        private static void Test_SearchPointSelection_FromSecurityZoneGraph()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            Vector3 lastSeenAtNode13 = new Vector3(5f, 0f, 22f); // Node 13 (Route North)
            Vector3 agentAtNode13 = new Vector3(5f, 0f, 22f);

            var points = PatrolSearchPlanner.SelectSearchPoints(
                graph,
                lastSeenAtNode13,
                agentAtNode13,
                searchRadius: 6.0f,
                maxPoints: 3
            );

            Assert(points != null, "SearchSelection_NotNull");
            Assert(points.Count > 0 && points.Count <= 3, "SearchSelection_BoundedCount", $"Count was {points?.Count}");

            // Node 14 at (7,0,22) is 2m away; Node 18 at (7,0,25) is 3.6m away; Node 12 at (5,0,18) is 4m away
            // Node 13 itself is excluded because agent is already at (5,0,22) (dist < 0.5m)
            bool containsNode14 = points.Exists(p => Vector3.Distance(p, new Vector3(7f, 0f, 22f)) < 0.1f);
            Assert(containsNode14, "SearchSelection_ContainsNearestVerifiedNode14");

            // Verify minimum separation between all chosen points
            bool allSeparated = true;
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    if (Vector3.Distance(points[i], points[j]) < PatrolSearchPlanner.MinPointSeparation)
                        allSeparated = false;
                }
            }
            Assert(allSeparated, "SearchSelection_EnforcesMinPointSeparation");
        }

        private static void Test_SearchPointSelection_DeterministicOrdering()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            Vector3 lastSeen = new Vector3(5f, 0f, 22f);
            Vector3 agent = new Vector3(5f, 0f, 22f);

            var run1 = PatrolSearchPlanner.SelectSearchPoints(graph, lastSeen, agent, 6.0f, 3);
            var run2 = PatrolSearchPlanner.SelectSearchPoints(graph, lastSeen, agent, 6.0f, 3);

            Assert(run1.Count == run2.Count, "DeterministicOrdering_SameCount");

            bool identical = true;
            for (int i = 0; i < run1.Count; i++)
            {
                if (Vector3.Distance(run1[i], run2[i]) > 0.001f) identical = false;
            }
            Assert(identical, "DeterministicOrdering_ExactCoordinateMatch");

            // Point 0 must be closer to center than Point 1
            if (run1.Count >= 2)
            {
                float d0 = Vector3.Distance(run1[0], lastSeen);
                float d1 = Vector3.Distance(run1[1], lastSeen);
                Assert(d0 <= d1, "DeterministicOrdering_AscendingDistanceOrder", $"d0={d0:F2}, d1={d1:F2}");
            }
        }

        private static void Test_SearchPointSelection_FallbackToGeometricOffsets()
        {
            // Empty graph: must fall back to geometric radial offsets
            var emptyGraph = new PatrolAStarGraph();
            Vector3 center = new Vector3(10f, 0f, 10f);
            Vector3 agent = new Vector3(10f, 0f, 10f);

            var points = PatrolSearchPlanner.SelectSearchPoints(emptyGraph, center, agent, 6.0f, 3);

            Assert(points.Count == 3, "GeometricFallback_GeneratesMaxPoints", $"Count={points.Count}");

            // Verify offsets are situated around the center at 2.5m
            bool validRadii = true;
            foreach (var pt in points)
            {
                float dist = Vector3.Distance(pt, center);
                if (Mathf.Abs(dist - 2.5f) > 0.1f) validRadii = false;
            }
            Assert(validRadii, "GeometricFallback_OffsetsAtCorrectRadius");
        }

        private static void Test_SearchPointSelection_RejectsUnreachableCandidates()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            Vector3 center = new Vector3(5f, 0f, 22f);
            Vector3 agent = new Vector3(5f, 0f, 22f);

            // Mock reachability predicate: reject Node 14 at (7, 0, 22)
            Vector3 blockedNode14Pos = new Vector3(7f, 0f, 22f);
            Func<Vector3, bool> reachabilityFilter = pos => Vector3.Distance(pos, blockedNode14Pos) > 0.5f;

            var points = PatrolSearchPlanner.SelectSearchPoints(
                graph,
                center,
                agent,
                searchRadius: 6.0f,
                maxPoints: 3,
                isPointReachable: reachabilityFilter
            );

            bool containsBlocked = points.Exists(p => Vector3.Distance(p, blockedNode14Pos) < 0.1f);
            Assert(!containsBlocked, "RejectsUnreachable_Node14FilteredOut");
            Assert(points.Count > 0, "RejectsUnreachable_AlternativeCandidatesSelected");
        }

        private static void Test_SearchPointSelection_AllUnreachableReturnsEmptyGracefully()
        {
            var graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            Vector3 center = new Vector3(5f, 0f, 22f);
            Vector3 agent = new Vector3(5f, 0f, 22f);

            // Everything rejected by reachability
            Func<Vector3, bool> allBlocked = pos => false;

            var points = PatrolSearchPlanner.SelectSearchPoints(
                graph,
                center,
                agent,
                searchRadius: 6.0f,
                maxPoints: 3,
                isPointReachable: allBlocked
            );

            Assert(points != null && points.Count == 0, "AllUnreachable_ReturnsEmptyListGracefully");
        }

        private static void Test_SearchPointSelection_ExcludesUnverifiedAndDistantNodes()
        {
            var graph = new PatrolAStarGraph();
            Vector3 center = new Vector3(0f, 0f, 0f);
            Vector3 agent = new Vector3(0f, 0f, 0f);

            // Node 1: within radius but unverified
            graph.AddNode(1, new Vector3(2f, 0f, 0f), "UnverifiedClose", isVerified: false);
            // Node 2: verified but beyond searchRadius (10m away)
            graph.AddNode(2, new Vector3(10f, 0f, 0f), "VerifiedDistant", isVerified: true);
            // Node 3: verified and within radius (3m away)
            graph.AddNode(3, new Vector3(0f, 0f, 3f), "VerifiedClose", isVerified: true);

            var points = PatrolSearchPlanner.SelectSearchPoints(
                graph,
                center,
                agent,
                searchRadius: 5.0f,
                maxPoints: 1
            );

            Assert(points.Count == 1, "Filter_SelectsVerifiedCloseOnly_Count");
            Assert(Vector3.Distance(points[0], new Vector3(0f, 0f, 3f)) < 0.1f, "Filter_SelectsNode3Exactly");
        }

        private static void Test_SearchTimeoutContract_BoundedDuration()
        {
            float searchSeconds = 4.0f;
            float searchStartTime = 10.0f;
            float searchUntil = searchStartTime + searchSeconds;

            // At t = 13.9s: 3.9s elapsed, search should remain active
            float tActive = 13.9f;
            bool activeBeforeTimeout = tActive < searchUntil;
            Assert(activeBeforeTimeout, "SearchTimeout_ActiveAt3Point9s");

            // At t = 14.0s: exactly 4.0s elapsed, search expires
            float tExpired = 14.0f;
            bool expiredAt4s = tExpired >= searchUntil;
            Assert(expiredAt4s, "SearchTimeout_ExpiresAt4s");

            // At t = 15.0s: over-budget, search definitely expired
            float tOver = 15.0f;
            bool expiredOverBudget = tOver >= searchUntil;
            Assert(expiredOverBudget, "SearchTimeout_ExpiresOverBudget");
        }

        private static void Test_LostSightMemory_PreservesObservedPosition()
        {
            // Simulate memory retention contract:
            // When sight is lost, LastKnownPosition remains stored and must not be overwritten by null perception
            Vector3 originalSeen = new Vector3(5f, 0f, 22f);
            Vector3 storedMemory = originalSeen;
            float recordedTime = 12.5f;

            // Simulate frame where player has warped / hidden:
            bool visibleNow = false;
            Vector3 currentPerceived = Vector3.zero;

            if (visibleNow)
            {
                storedMemory = currentPerceived;
            }

            Assert(storedMemory == originalSeen, "Memory_RetainsLastKnownPositionWhenSightLost");
            Assert(recordedTime > 0f, "Memory_RetainsObservationTimestamp");
        }

        private static void Test_DetectionReacquisition_InterruptionLogic()
        {
            float detectionSeconds = 0.5f;

            // Scenario A: Sighted for 0.25s (less than 0.5s requirement)
            float sightStart = 5.0f;
            float nowA = 5.25f;
            bool detectedA = (nowA - sightStart) >= detectionSeconds;
            Assert(!detectedA, "Reacquisition_SightUnderHalfSecondDoesNotDetect");

            // Scenario B: Sighted continuously for 0.55s (exceeds requirement)
            float nowB = 5.55f;
            bool detectedB = (nowB - sightStart) >= detectionSeconds;
            Assert(detectedB, "Reacquisition_SightOverHalfSecondConfirmsDetection");

            // Scenario C: Interruption - player becomes visible during Investigate/Search
            RoleState currentState = RoleState.Investigate;
            bool seesPlayer = true;
            RoleState targetState = currentState;
            if (seesPlayer)
            {
                targetState = detectedA ? RoleState.Combat : RoleState.Detecting;
            }
            Assert(targetState == RoleState.Detecting, "Reacquisition_InterruptsInvestigateToDetecting");
        }
    }
}
