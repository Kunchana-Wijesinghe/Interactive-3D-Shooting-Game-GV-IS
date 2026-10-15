using System;
using System.Collections.Generic;
using UnityEngine;

namespace Perera.SoundHunter
{
    /// <summary>
    /// Acoustic propagation graph. Edge cost = hearing budget a sound loses crossing that link
    /// (walls/doors cost more than open corridors). This is NOT a navmesh or movement path:
    /// it only answers "how much does a sound weaken between node A and node B".
    /// Undirected, non-negative costs. Pure logic, no scene access.
    /// </summary>
    public sealed class HearingGraph
    {
        public const float Unreachable = float.PositiveInfinity;

        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<List<(int to, float cost)>> adjacency = new List<List<(int, float)>>();

        public int NodeCount => positions.Count;

        public int AddNode(Vector3 position)
        {
            positions.Add(position);
            adjacency.Add(new List<(int, float)>());
            return positions.Count - 1;
        }

        public Vector3 GetPosition(int node) { CheckNode(node); return positions[node]; }

        public void AddEdge(int a, int b, float cost)
        {
            CheckNode(a); CheckNode(b);
            if (float.IsNaN(cost) || cost < 0f) throw new ArgumentOutOfRangeException(nameof(cost), "cost must be >= 0");
            adjacency[a].Add((b, cost));
            adjacency[b].Add((a, cost));
        }

        /// <summary>Nearest node by straight-line distance; lowest index wins ties. -1 if empty.</summary>
        public int NearestNode(Vector3 point)
        {
            int best = -1; float bestSq = float.MaxValue;
            for (int i = 0; i < positions.Count; i++)
            {
                float d = (positions[i] - point).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Dijkstra minimum propagation cost from source to every node (Unreachable if disconnected).
        /// Settles nodes in (cost, index) order so results are deterministic.
        /// </summary>
        public float[] MinCostsFrom(int source)
        {
            CheckNode(source);
            var cost = new float[positions.Count];
            for (int i = 0; i < cost.Length; i++) cost[i] = Unreachable;
            cost[source] = 0f;

            var open = new SortedSet<(float cost, int node)>(Comparer<(float cost, int node)>.Create(
                (x, y) => { int c = x.cost.CompareTo(y.cost); return c != 0 ? c : x.node.CompareTo(y.node); }));
            open.Add((0f, source));

            while (open.Count > 0)
            {
                var cur = open.Min; open.Remove(cur);
                if (cur.cost > cost[cur.node]) continue;
                foreach (var (to, w) in adjacency[cur.node])
                {
                    float next = cur.cost + w;
                    if (next < cost[to])
                    {
                        if (cost[to] != Unreachable) open.Remove((cost[to], to));
                        cost[to] = next;
                        open.Add((next, to));
                    }
                }
            }
            return cost;
        }

        public float MinCost(int from, int to) { CheckNode(to); return MinCostsFrom(from)[to]; }

        private void CheckNode(int n)
        {
            if (n < 0 || n >= positions.Count) throw new ArgumentOutOfRangeException(nameof(n), "unknown node " + n);
        }
    }
}
