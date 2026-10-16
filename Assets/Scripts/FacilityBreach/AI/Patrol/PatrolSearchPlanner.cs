using System;
using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.AI.Patrol
{
    /// <summary>
    /// Deterministic candidate generator and planner for Patrol Guard multi-point search behavior.
    /// Selects bounded, verified search points around a last-known position.
    /// Independent of physics and animation engines; fully testable in edit-mode test suites.
    /// </summary>
    public static class PatrolSearchPlanner
    {
        public const float DefaultSearchRadius = 6.0f;
        public const int DefaultMaxSearchPoints = 3;
        public const float MinPointSeparation = 1.5f;

        /// <summary>
        /// Selects deterministic search points around the last-known position.
        /// Prioritizes verified graph nodes within searchRadius, supplemented by
        /// deterministic radial offsets if graph nodes are sparse.
        /// </summary>
        public static List<Vector3> SelectSearchPoints(
            PatrolAStarGraph graph,
            Vector3 searchCenter,
            Vector3 agentPosition,
            float searchRadius = DefaultSearchRadius,
            int maxPoints = DefaultMaxSearchPoints,
            Func<Vector3, bool> isPointReachable = null)
        {
            var selectedPoints = new List<Vector3>();
            if (maxPoints <= 0) return selectedPoints;

            // 1. Gather verified graph nodes within searchRadius of searchCenter
            if (graph != null)
            {
                var candidateNodes = new List<PatrolNode>();
                foreach (var node in graph.Nodes)
                {
                    if (!node.IsVerified) continue;
                    float distFromCenter = Vector3.Distance(node.Position, searchCenter);
                    if (distFromCenter <= searchRadius)
                    {
                        // Avoid picking a node that the agent is already standing on (< 0.5m)
                        float distFromAgent = Vector3.Distance(node.Position, agentPosition);
                        if (distFromAgent >= 0.5f)
                        {
                            candidateNodes.Add(node);
                        }
                    }
                }

                // Deterministic sort: ascending distance to searchCenter, tie-break by node ID
                candidateNodes.Sort((a, b) =>
                {
                    float distA = Vector3.Distance(a.Position, searchCenter);
                    float distB = Vector3.Distance(b.Position, searchCenter);
                    int comp = distA.CompareTo(distB);
                    if (comp != 0) return comp;
                    return a.Id.CompareTo(b.Id);
                });

                foreach (var candidate in candidateNodes)
                {
                    if (selectedPoints.Count >= maxPoints) break;

                    if (isPointReachable != null && !isPointReachable(candidate.Position))
                    {
                        continue;
                    }

                    if (IsDistinctPoint(selectedPoints, candidate.Position, MinPointSeparation))
                    {
                        selectedPoints.Add(candidate.Position);
                    }
                }
            }

            // 2. If fewer than maxPoints found from graph, supplement with deterministic geometric offsets
            if (selectedPoints.Count < maxPoints)
            {
                // Cardinal search offsets at 2.5m radius from searchCenter
                Vector3[] offsets = new Vector3[]
                {
                    searchCenter + new Vector3(0f, 0f, 2.5f),   // North
                    searchCenter + new Vector3(2.5f, 0f, 0f),   // East
                    searchCenter + new Vector3(0f, 0f, -2.5f),  // South
                    searchCenter + new Vector3(-2.5f, 0f, 0f)   // West
                };

                foreach (var offset in offsets)
                {
                    if (selectedPoints.Count >= maxPoints) break;

                    if (Vector3.Distance(offset, agentPosition) < 0.5f) continue;

                    if (isPointReachable != null && !isPointReachable(offset))
                    {
                        continue;
                    }

                    if (IsDistinctPoint(selectedPoints, offset, MinPointSeparation))
                    {
                        selectedPoints.Add(offset);
                    }
                }
            }

            return selectedPoints;
        }

        private static bool IsDistinctPoint(List<Vector3> existingPoints, Vector3 candidate, float minSeparation)
        {
            for (int i = 0; i < existingPoints.Count; i++)
            {
                if (Vector3.Distance(existingPoints[i], candidate) < minSeparation)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
