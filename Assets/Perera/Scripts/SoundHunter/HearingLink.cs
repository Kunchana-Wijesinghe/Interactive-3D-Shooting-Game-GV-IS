using UnityEngine;

namespace Perera.SoundHunter
{
    /// <summary>
    /// Weighted, undirected acoustic connection between two HearingNodes.
    /// Cost = hearing budget lost crossing the link (open doorway small, muffling wall/door large).
    /// This is NOT a walkable route; movement uses its own navigation.
    /// Set Open=false to remove the link from the graph (e.g. a closed door) and call
    /// SceneHearingGraph.Rebuild().
    /// </summary>
    public class HearingLink : MonoBehaviour
    {
        [SerializeField] private HearingNode nodeA;
        [SerializeField] private HearingNode nodeB;
        [Tooltip("Finite and >= 0, in the same units as sound strength.")]
        [SerializeField] private float cost = 5f;
        [SerializeField] private bool open = true;

        public HearingNode NodeA { get => nodeA; set => nodeA = value; }
        public HearingNode NodeB { get => nodeB; set => nodeB = value; }
        public float Cost { get => cost; set => cost = value; }
        public bool Open { get => open; set => open = value; }

        public bool CostIsValid => !float.IsNaN(cost) && !float.IsInfinity(cost) && cost >= 0f;

        private void OnDrawGizmos()
        {
            if (!SceneHearingGraph.ShowGizmos || nodeA == null || nodeB == null) return;
            Gizmos.color = !open ? Color.gray : (CostIsValid ? Color.yellow : Color.red);
            Gizmos.DrawLine(nodeA.Position, nodeB.Position);
#if UNITY_EDITOR
            UnityEditor.Handles.Label((nodeA.Position + nodeB.Position) * 0.5f + Vector3.up * 0.4f,
                open ? cost.ToString("0.#") : cost.ToString("0.#") + " (closed)");
#endif
        }
    }
}
