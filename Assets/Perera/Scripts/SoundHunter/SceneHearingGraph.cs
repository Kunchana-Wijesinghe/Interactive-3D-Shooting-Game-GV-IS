using System.Collections.Generic;
using UnityEngine;

namespace Perera.SoundHunter
{
    /// <summary>
    /// Builds a HearingGraph (reusing its Dijkstra) from the HearingNode / HearingLink components
    /// under this object and maps world positions to nodes.
    ///
    /// MAPPING RULE (TryMapPoint) - prevents mapping through walls into unrelated rooms:
    ///  1. Only nodes whose authored zone box CONTAINS the point are candidates (room membership).
    ///  2. The node must also be within MaxMappingDistance of the point.
    ///  3. Optionally the straight line point->node must not hit OcclusionMask geometry.
    ///  4. Among candidates: smallest zone volume, then nearest, then lowest index.
    ///  If no candidate remains the mapping is REJECTED (sound ignored / listener "outside").
    /// Invalid (non-finite) points are rejected.
    /// </summary>
    public class SceneHearingGraph : MonoBehaviour
    {
        /// <summary>Editor/play gizmo toggle shared by nodes and links.</summary>
        public static bool ShowGizmos = true;

        [SerializeField] private float maxMappingDistance = 10f;
        [Tooltip("Optional. If non-zero, a point is rejected when a ray to the node hits these layers. Leave empty when props/cover sit inside rooms.")]
        [SerializeField] private LayerMask occlusionMask = 0;
        [SerializeField] private bool logProblems = true;

        private readonly List<HearingNode> nodes = new List<HearingNode>();
        private readonly Dictionary<HearingNode, int> indexOf = new Dictionary<HearingNode, int>();
        private readonly List<string> problems = new List<string>();
        private readonly List<(float vol, float dist, int idx)> scratch = new List<(float, float, int)>();

        public HearingGraph Graph { get; private set; }
        public IReadOnlyList<string> Problems => problems;
        public IReadOnlyList<HearingNode> Nodes => nodes;
        public float MaxMappingDistance { get => maxMappingDistance; set => maxMappingDistance = value; }

        private void Awake() => Rebuild();

        /// <summary>Re-collect nodes/links and rebuild the graph. Invalid links are skipped and listed in Problems.</summary>
        public void Rebuild()
        {
            problems.Clear();
            nodes.Clear();
            indexOf.Clear();
            Graph = new HearingGraph();

            GetComponentsInChildren(false, nodes);
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                indexOf[n] = Graph.AddNode(n.Position);
                if (!n.ZoneIsValid) problems.Add($"Node '{n.name}' has an invalid zone size/offset");
            }

            var links = new List<HearingLink>();
            GetComponentsInChildren(false, links);
            var seen = new HashSet<(int, int)>();
            foreach (var l in links)
            {
                if (l.NodeA == null || l.NodeB == null) { problems.Add($"Link '{l.name}' has a missing node reference"); continue; }
                if (l.NodeA == l.NodeB) { problems.Add($"Link '{l.name}' connects a node to itself"); continue; }
                if (!l.CostIsValid) { problems.Add($"Link '{l.name}' has invalid cost {l.Cost} (must be finite and >= 0)"); continue; }
                if (!indexOf.TryGetValue(l.NodeA, out int a) || !indexOf.TryGetValue(l.NodeB, out int b))
                { problems.Add($"Link '{l.name}' references a node that is not under this graph"); continue; }
                var key = a < b ? (a, b) : (b, a);
                if (!seen.Add(key)) { problems.Add($"Link '{l.name}' duplicates an existing link {l.NodeA.name}<->{l.NodeB.name}"); continue; }
                if (!l.Open) continue; // valid but closed: intentionally absent from the graph
                Graph.AddEdge(a, b, l.Cost);
            }

            if (!float.IsFinite(maxMappingDistance) || maxMappingDistance <= 0f)
                problems.Add("MaxMappingDistance must be finite and > 0");

            if (logProblems)
                foreach (var p in problems) Debug.LogError("[SceneHearingGraph] " + p, this);
        }

        /// <summary>Map a world point to a node index under the rule documented on the class. False = rejected.</summary>
        public bool TryMapPoint(Vector3 point, out int node)
        {
            node = -1;
            if (Graph == null || !IsFinite(point) || !float.IsFinite(maxMappingDistance) || maxMappingDistance <= 0f) return false;

            scratch.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (!n.ZoneContains(point)) continue;
                float d = Vector3.Distance(point, n.Position);
                if (d > maxMappingDistance) continue;
                if (occlusionMask.value != 0 && Physics.Linecast(point, n.Position, occlusionMask, QueryTriggerInteraction.Ignore)) continue;
                scratch.Add((n.ZoneVolume, d, i));
            }
            if (scratch.Count == 0) return false;

            var best = scratch[0];
            for (int i = 1; i < scratch.Count; i++)
            {
                var c = scratch[i];
                if (c.vol < best.vol || (c.vol == best.vol && (c.dist < best.dist || (c.dist == best.dist && c.idx < best.idx)))) best = c;
            }
            node = best.idx;
            return true;
        }

        public string NodeName(int index) => index >= 0 && index < nodes.Count ? nodes[index].ZoneName : "(none)";

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
