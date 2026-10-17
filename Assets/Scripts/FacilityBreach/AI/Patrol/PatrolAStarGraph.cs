using System;
using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.AI.Patrol
{
    /// <summary>
    /// Discrete topological graph representing facility navigation routes, corridors, and doorways.
    /// Manages node definitions, adjacency lists, and runtime edge blockage states.
    /// </summary>
    public sealed class PatrolAStarGraph
    {
        private readonly Dictionary<int, PatrolNode> nodes = new Dictionary<int, PatrolNode>();
        private readonly Dictionary<int, List<PatrolEdge>> adjacency = new Dictionary<int, List<PatrolEdge>>();
        private static readonly IReadOnlyList<PatrolEdge> EmptyEdges = Array.Empty<PatrolEdge>();

        public int NodeCount => nodes.Count;
        public IEnumerable<PatrolNode> Nodes => nodes.Values;

        public bool ContainsNode(int id) => nodes.ContainsKey(id);

        public bool TryGetNode(int id, out PatrolNode node) => nodes.TryGetValue(id, out node);

        public PatrolNode GetNode(int id)
        {
            if (!nodes.TryGetValue(id, out var node))
                throw new KeyNotFoundException($"Node with ID {id} does not exist in graph.");
            return node;
        }

        public void AddNode(int id, Vector3 position, string name = null, bool isVerified = true)
        {
            if (nodes.ContainsKey(id))
                throw new ArgumentException($"Node with ID {id} already exists in graph.", nameof(id));

            var node = new PatrolNode(id, position, name, isVerified);
            nodes.Add(id, node);
            adjacency.Add(id, new List<PatrolEdge>());
        }

        public void AddEdge(int from, int to, float cost = -1f, bool isVerified = true)
        {
            if (!nodes.TryGetValue(from, out var fromNode))
                throw new KeyNotFoundException($"Source node {from} does not exist in graph.");
            if (!nodes.TryGetValue(to, out var toNode))
                throw new KeyNotFoundException($"Target node {to} does not exist in graph.");

            float edgeCost = cost >= 0f ? cost : Vector3.Distance(fromNode.Position, toNode.Position);
            if (!float.IsFinite(edgeCost) || edgeCost < 0f)
                throw new ArgumentException($"Calculated edge cost must be non-negative finite: {edgeCost}", nameof(cost));

            var edgeList = adjacency[from];
            edgeList.RemoveAll(e => e.To == to);
            edgeList.Add(new PatrolEdge(from, to, edgeCost, isVerified));
        }

        public void AddBidirectionalEdge(int a, int b, float cost = -1f, bool isVerified = true)
        {
            if (!nodes.TryGetValue(a, out var nodeA) || !nodes.TryGetValue(b, out var nodeB))
                throw new KeyNotFoundException($"Nodes {a} and {b} must both exist to create a bidirectional edge.");

            float edgeCost = cost >= 0f ? cost : Vector3.Distance(nodeA.Position, nodeB.Position);
            AddEdge(a, b, edgeCost, isVerified);
            AddEdge(b, a, edgeCost, isVerified);
        }

        public IReadOnlyList<PatrolEdge> GetEdges(int nodeId)
        {
            return adjacency.TryGetValue(nodeId, out var list) ? list : EmptyEdges;
        }

        public bool SetEdgeBlocked(int a, int b, bool blocked)
        {
            bool modified = false;
            if (adjacency.TryGetValue(a, out var listA))
            {
                foreach (var edge in listA)
                {
                    if (edge.To == b) { edge.SetBlocked(blocked); modified = true; }
                }
            }
            if (adjacency.TryGetValue(b, out var listB))
            {
                foreach (var edge in listB)
                {
                    if (edge.To == a) { edge.SetBlocked(blocked); modified = true; }
                }
            }
            return modified;
        }

        public void ResetAllEdgeBlocks()
        {
            foreach (var edgeList in adjacency.Values)
            {
                foreach (var edge in edgeList)
                    edge.SetBlocked(false);
            }
        }

        public int GetNearestNode(Vector3 position, bool verifiedOnly = false)
        {
            if (nodes.Count == 0) return -1;

            int nearestId = -1;
            float shortestSqDist = float.PositiveInfinity;

            foreach (var node in nodes.Values)
            {
                if (verifiedOnly && !node.IsVerified) continue;

                float sqDist = (node.Position - position).sqrMagnitude;
                if (sqDist < shortestSqDist)
                {
                    shortestSqDist = sqDist;
                    nearestId = node.Id;
                }
            }

            return nearestId;
        }

        public void Clear()
        {
            nodes.Clear();
            adjacency.Clear();
        }
    }
}

