using UnityEngine;

namespace Perera.SoundHunter
{
    /// <summary>
    /// A point in the acoustic graph plus the ZONE (axis-aligned box) it is responsible for.
    /// The zone is what prevents wrong mappings: a sound or listener may only map to a node whose
    /// zone contains it, so a source on the far side of a wall maps to the neighbouring room's node
    /// (or to nothing) instead of to the "nearest" node through the wall.
    /// Author zones to match rooms: one box per room, not crossing walls. Zones are axis-aligned
    /// (rotation is ignored). Overlaps are resolved deterministically: smallest zone volume wins,
    /// then nearest to the node, then lowest graph index - so a small zone (e.g. a raised platform)
    /// can sit inside a big room zone and take precedence for points inside it.
    /// </summary>
    public class HearingNode : MonoBehaviour
    {
        [SerializeField] private string zoneName = "";
        [Tooltip("World-space size of this node's zone box, centred on the node plus Zone Offset.")]
        [SerializeField] private Vector3 zoneSize = new Vector3(12f, 4f, 12f);
        [SerializeField] private Vector3 zoneOffset = Vector3.zero;
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 1f);

        public string ZoneName => string.IsNullOrEmpty(zoneName) ? name : zoneName;
        public Vector3 Position => transform.position;
        public Vector3 ZoneSize => zoneSize;
        public Vector3 ZoneCenter => transform.position + zoneOffset;
        public float ZoneVolume => Mathf.Abs(zoneSize.x * zoneSize.y * zoneSize.z);

        public bool ZoneIsValid =>
            IsFinite(zoneSize) && IsFinite(zoneOffset) && zoneSize.x > 0f && zoneSize.y > 0f && zoneSize.z > 0f;

        public bool ZoneContains(Vector3 point)
        {
            if (!ZoneIsValid) return false;
            Vector3 d = point - ZoneCenter;
            Vector3 h = zoneSize * 0.5f;
            return Mathf.Abs(d.x) <= h.x && Mathf.Abs(d.y) <= h.y && Mathf.Abs(d.z) <= h.z;
        }

        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        private void Reset() { zoneName = name; }

        private void OnDrawGizmos()
        {
            if (!SceneHearingGraph.ShowGizmos) return;
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, 0.35f);
            Color zone = gizmoColor; zone.a = 0.25f;
            Gizmos.color = zone;
            if (ZoneIsValid) Gizmos.DrawWireCube(ZoneCenter, zoneSize);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f, ZoneName);
#endif
        }
    }
}
