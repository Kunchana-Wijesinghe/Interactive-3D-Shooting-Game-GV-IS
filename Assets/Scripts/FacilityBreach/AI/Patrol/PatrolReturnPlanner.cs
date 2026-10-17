using System;
using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.AI.Patrol
{
    /// <summary>
    /// Encapsulates an evaluated patrol route destination candidate for tactical return.
    /// Tracks route index, target position, A* path nodes, total travel cost, and reachability.
    /// </summary>
    public sealed class PatrolReturnCandidate : IComparable<PatrolReturnCandidate>
    {
        public int RouteIndex { get; }
        public Vector3 Position { get; }
        public bool IsReachable { get; }
        public float TotalCost { get; }
        public IReadOnlyList<int> NodeIds { get; }
        public IReadOnlyList<Vector3> Waypoints { get; }
        public string FailureReason { get; }

        public PatrolReturnCandidate(
            int routeIndex,
            Vector3 position,
            bool isReachable,
            float totalCost,
            IReadOnlyList<int> nodeIds,
            IReadOnlyList<Vector3> waypoints,
            string failureReason = null)
        {
            RouteIndex = routeIndex;
            Position = position;
            IsReachable = isReachable;
            TotalCost = totalCost;
            NodeIds = nodeIds ?? Array.Empty<int>();
            Waypoints = waypoints ?? Array.Empty<Vector3>();
            FailureReason = failureReason ?? string.Empty;
        }

        public int CompareTo(PatrolReturnCandidate other)
        {
            if (other == null) return -1;

            // Reachable candidates always prioritized before unreachable ones
            if (IsReachable != other.IsReachable)
                return IsReachable ? -1 : 1;

            // Lowest total travel cost first
            int costComp = TotalCost.CompareTo(other.TotalCost);
            if (Mathf.Abs(TotalCost - other.TotalCost) > 0.001f)
                return costComp;

            // Deterministic tie-breaker: lower route index
            return RouteIndex.CompareTo(other.RouteIndex);
        }

        public override string ToString() =>
            IsReachable ? $"Route[{RouteIndex}] ({Position}): Cost={TotalCost:F2}, Nodes={NodeIds.Count}" :
                          $"Route[{RouteIndex}] ({Position}): Unreachable ({FailureReason})";
    }

    /// <summary>
    /// Tactical return planner for Patrol Guard (PG-01).
    /// Evaluates authored patrol route destinations using actual A* graph costs
    /// to determine the optimal, lowest-cost reachable waypoint to resume patrol.
    /// Independent of animation and physics engine simulation; fully testable in edit mode.
    /// </summary>
    public static class PatrolReturnPlanner
    {
        public const float ArrivalTolerance = 0.6f;
        public const float MaxConnectorDistance = 6.0f;

        /// <summary>
        /// Evaluates and ranks all authored route destinations from the current agent position.
        /// Returns a sorted list of candidates (reachable lowest-cost first, tie-broken by route index).
        /// </summary>
        public static List<PatrolReturnCandidate> EvaluateReturnCandidates(
            PatrolAStarGraph graph,
            Vector3[] route,
            Vector3 agentPosition,
            Func<Vector3, bool> isPointReachable = null)
        {
            var candidates = new List<PatrolReturnCandidate>();
            if (route == null || route.Length == 0) return candidates;

            for (int i = 0; i < route.Length; i++)
            {
                Vector3 targetPos = route[i];
                float directDist = Vector3.Distance(agentPosition, targetPos);

                // Case 1: Agent is already at or within arrival distance of this waypoint
                if (directDist <= ArrivalTolerance)
                {
                    candidates.Add(new PatrolReturnCandidate(
                        i,
                        targetPos,
                        isReachable: true,
                        totalCost: directDist,
                        nodeIds: Array.Empty<int>(),
                        waypoints: new List<Vector3> { targetPos }
                    ));
                    continue;
                }

                // Reachability predicate filter (if provided)
                if (isPointReachable != null && !isPointReachable(targetPos))
                {
                    candidates.Add(new PatrolReturnCandidate(
                        i,
                        targetPos,
                        isReachable: false,
                        totalCost: float.PositiveInfinity,
                        nodeIds: null,
                        waypoints: null,
                        failureReason: "Failed reachability check"
                    ));
                    continue;
                }

                // Case 2: Graph-based topological A* evaluation
                if (graph == null)
                {
                    // Fallback without graph: direct Euclidean distance
                    candidates.Add(new PatrolReturnCandidate(
                        i,
                        targetPos,
                        isReachable: true,
                        totalCost: directDist,
                        nodeIds: Array.Empty<int>(),
                        waypoints: new List<Vector3> { targetPos }
                    ));
                    continue;
                }

                int startNodeId = graph.GetNearestNode(agentPosition, verifiedOnly: true);
                int goalNodeId = graph.GetNearestNode(targetPos, verifiedOnly: true);

                if (startNodeId < 0 || goalNodeId < 0)
                {
                    candidates.Add(new PatrolReturnCandidate(
                        i,
                        targetPos,
                        isReachable: false,
                        totalCost: float.PositiveInfinity,
                        nodeIds: null,
                        waypoints: null,
                        failureReason: $"Nearest graph node not found (start={startNodeId}, goal={goalNodeId})"
                    ));
                    continue;
                }

                var startNode = graph.GetNode(startNodeId);
                var goalNode = graph.GetNode(goalNodeId);

                float startConnectorDist = Vector3.Distance(agentPosition, startNode.Position);
                float goalConnectorDist = Vector3.Distance(goalNode.Position, targetPos);

                if (startConnectorDist > MaxConnectorDistance || goalConnectorDist > MaxConnectorDistance)
                {
                    candidates.Add(new PatrolReturnCandidate(
                        i,
                        targetPos,
                        isReachable: false,
                        totalCost: float.PositiveInfinity,
                        nodeIds: null,
                        waypoints: null,
                        failureReason: $"Connector distance exceeded (start={startConnectorDist:F1}m, goal={goalConnectorDist:F1}m)"
                    ));
                    continue;
                }

                var astarResult = PatrolAStarPathfinder.FindPath(graph, startNodeId, goalNodeId);
                if (!astarResult.Success)
                {
                    candidates.Add(new PatrolReturnCandidate(
                        i,
                        targetPos,
                        isReachable: false,
                        totalCost: float.PositiveInfinity,
                        nodeIds: null,
                        waypoints: null,
                        failureReason: $"A* pathfinding failed: {astarResult.FailureReason}"
                    ));
                    continue;
                }

                float totalCost = startConnectorDist + astarResult.TotalCost + goalConnectorDist;
                var waypoints = new List<Vector3>(astarResult.Waypoints);

                if (waypoints.Count == 0 || Vector3.Distance(waypoints[waypoints.Count - 1], targetPos) > 0.2f)
                {
                    waypoints.Add(targetPos);
                }

                candidates.Add(new PatrolReturnCandidate(
                    i,
                    targetPos,
                    isReachable: true,
                    totalCost: totalCost,
                    nodeIds: astarResult.NodeIds,
                    waypoints: waypoints
                ));
            }

            // Rank candidates deterministically
            candidates.Sort();
            return candidates;
        }

        /// <summary>
        /// Selects the single best (lowest-cost reachable) candidate from evaluated return candidates.
        /// Returns null if no candidates are reachable.
        /// </summary>
        public static PatrolReturnCandidate SelectBestCandidate(List<PatrolReturnCandidate> rankedCandidates)
        {
            if (rankedCandidates == null || rankedCandidates.Count == 0) return null;
            return rankedCandidates.Find(c => c.IsReachable);
        }
    }
}
