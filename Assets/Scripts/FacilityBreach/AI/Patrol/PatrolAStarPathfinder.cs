using System;
using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.AI.Patrol
{
    /// <summary>
    /// Encapsulates the complete result and diagnostics of an A* path search.
    /// </summary>
    public sealed class AStarResult
    {
        public bool Success { get; }
        public IReadOnlyList<int> NodeIds { get; }
        public IReadOnlyList<Vector3> Waypoints { get; }
        public float TotalCost { get; }
        public int NodesExplored { get; }
        public string FailureReason { get; }

        private AStarResult(bool success, IReadOnlyList<int> nodeIds, IReadOnlyList<Vector3> waypoints, float cost, int explored, string reason)
        {
            Success = success;
            NodeIds = nodeIds ?? Array.Empty<int>();
            Waypoints = waypoints ?? Array.Empty<Vector3>();
            TotalCost = cost;
            NodesExplored = explored;
            FailureReason = reason ?? string.Empty;
        }

        public static AStarResult Succeeded(List<int> nodeIds, List<Vector3> waypoints, float totalCost, int nodesExplored) =>
            new AStarResult(true, nodeIds, waypoints, totalCost, nodesExplored, null);

        public static AStarResult Failed(string reason, int nodesExplored = 0) =>
            new AStarResult(false, null, null, float.PositiveInfinity, nodesExplored, reason);

        public override string ToString() =>
            Success ? $"A* Success [Nodes={NodeIds.Count}, Cost={TotalCost:F2}, Explored={NodesExplored}]" :
                      $"A* Failure [{FailureReason}, Explored={NodesExplored}]";
    }

    /// <summary>
    /// Pure algorithmic implementation of the A* informed search algorithm on a PatrolAStarGraph.
    /// Uses an admissible Euclidean heuristic with deterministic tie-breaking.
    /// Independent of animation, combat, and physics engine simulation.
    /// </summary>
    public static class PatrolAStarPathfinder
    {
        private readonly struct SearchNode : IComparable<SearchNode>
        {
            public readonly int NodeId;
            public readonly float FScore;
            public readonly float HScore;

            public SearchNode(int nodeId, float fScore, float hScore)
            {
                NodeId = nodeId;
                FScore = fScore;
                HScore = hScore;
            }

            public int CompareTo(SearchNode other)
            {
                int fComp = FScore.CompareTo(other.FScore);
                if (fComp != 0) return fComp;

                int hComp = HScore.CompareTo(other.HScore);
                if (hComp != 0) return hComp;

                return NodeId.CompareTo(other.NodeId);
            }
        }

        /// <summary>
        /// Computes the optimal shortest path between startNodeId and goalNodeId on graph.
        /// </summary>
        public static AStarResult FindPath(PatrolAStarGraph graph, int startNodeId, int goalNodeId)
        {
            if (graph == null)
                return AStarResult.Failed("Graph instance is null.");

            if (!graph.TryGetNode(startNodeId, out var startNode))
                return AStarResult.Failed($"Start node ID {startNodeId} does not exist in graph.");

            if (!graph.TryGetNode(goalNodeId, out var goalNode))
                return AStarResult.Failed($"Goal node ID {goalNodeId} does not exist in graph.");

            // Trivial case: Start is already Goal
            if (startNodeId == goalNodeId)
            {
                return AStarResult.Succeeded(
                    new List<int> { startNodeId },
                    new List<Vector3> { startNode.Position },
                    0f,
                    1
                );
            }

            var gScore = new Dictionary<int, float>();
            var cameFrom = new Dictionary<int, int>();
            var closedSet = new HashSet<int>();
            var openSet = new SortedSet<SearchNode>();
            var openTracker = new Dictionary<int, SearchNode>();

            float startH = Heuristic(startNode.Position, goalNode.Position);
            gScore[startNodeId] = 0f;
            var initialNode = new SearchNode(startNodeId, startH, startH);
            openSet.Add(initialNode);
            openTracker[startNodeId] = initialNode;

            int nodesExplored = 0;
            int maxIterations = Math.Max(100, graph.NodeCount * 10);

            while (openSet.Count > 0)
            {
                nodesExplored++;
                if (nodesExplored > maxIterations)
                    return AStarResult.Failed("Path search iteration limit exceeded (prevented potential cyclic loop).", nodesExplored);

                var current = openSet.Min;
                openSet.Remove(current);
                openTracker.Remove(current.NodeId);

                int currentId = current.NodeId;

                // Reached goal: reconstruct path
                if (currentId == goalNodeId)
                {
                    return ReconstructPath(graph, cameFrom, currentId, gScore[currentId], nodesExplored);
                }

                closedSet.Add(currentId);
                float currentG = gScore[currentId];

                foreach (var edge in graph.GetEdges(currentId))
                {
                    if (edge.IsBlocked || !float.IsFinite(edge.EffectiveCost))
                        continue;

                    int neighborId = edge.To;
                    if (closedSet.Contains(neighborId))
                        continue;

                    float tentativeG = currentG + edge.EffectiveCost;
                    if (!gScore.TryGetValue(neighborId, out float existingG) || tentativeG < existingG)
                    {
                        cameFrom[neighborId] = currentId;
                        gScore[neighborId] = tentativeG;

                        var neighborNode = graph.GetNode(neighborId);
                        float h = Heuristic(neighborNode.Position, goalNode.Position);
                        float f = tentativeG + h;

                        if (openTracker.TryGetValue(neighborId, out var existingSearchNode))
                        {
                            openSet.Remove(existingSearchNode);
                        }

                        var newSearchNode = new SearchNode(neighborId, f, h);
                        openSet.Add(newSearchNode);
                        openTracker[neighborId] = newSearchNode;
                    }
                }
            }

            return AStarResult.Failed($"No traversable path from node {startNodeId} to node {goalNodeId}.", nodesExplored);
        }

        /// <summary>
        /// Admissible and consistent Euclidean distance heuristic.
        /// Guaranteed: h(n) <= true distance for all nodes.
        /// </summary>
        public static float Heuristic(Vector3 current, Vector3 goal) =>
            Vector3.Distance(current, goal);

        private static AStarResult ReconstructPath(
            PatrolAStarGraph graph,
            Dictionary<int, int> cameFrom,
            int currentId,
            float totalCost,
            int nodesExplored)
        {
            var nodeIds = new List<int> { currentId };
            while (cameFrom.TryGetValue(currentId, out int parentId))
            {
                currentId = parentId;
                nodeIds.Add(currentId);
            }

            nodeIds.Reverse();

            var waypoints = new List<Vector3>(nodeIds.Count);
            for (int i = 0; i < nodeIds.Count; i++)
            {
                waypoints.Add(graph.GetNode(nodeIds[i]).Position);
            }

            return AStarResult.Succeeded(nodeIds, waypoints, totalCost, nodesExplored);
        }
    }
}

