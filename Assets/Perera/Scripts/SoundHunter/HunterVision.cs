using Perera.Core;
using UnityEngine;

namespace Perera.SoundHunter
{
    public enum SightResult { Seen, NoTarget, TargetDead, OutOfRange, OutOfFov, Occluded }

    /// <summary>
    /// Self-contained sight sense (no visual detection system exists in the repo yet).
    /// Range + horizontal field of view + obstacle-aware ray casts to several body points.
    ///
    /// KNOWLEDGE RULE: the target reference is only the thing to LOOK FOR. Its position is exposed
    /// to the decision logic solely through TryPerceive, and only when range, FOV and line of
    /// sight all pass. Failing any check returns no position, so a hidden player's live position
    /// never becomes decision knowledge.
    /// </summary>
    public class HunterVision : MonoBehaviour
    {
        [SerializeField] private Health target;
        [SerializeField] private Transform eye;
        [SerializeField] private float range = 18f;
        [Range(10f, 360f)] [SerializeField] private float fieldOfView = 120f;
        [SerializeField] private LayerMask obstacleMask = ~(1 << 2); // everything except Ignore Raycast

        public Health Target { get => target; set => target = value; }
        public float Range => range;
        public float FieldOfView => fieldOfView;
        public SightResult LastResult { get; private set; } = SightResult.NoTarget;
        public Collider LastBlocker { get; private set; }

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Collider[] targetColliders;

        private Vector3 EyePos => eye != null ? eye.position : transform.position + Vector3.up * 1.6f;

        /// <summary>The ONLY way to learn the target's position. Returns false (position = zero) unless truly visible.</summary>
        public bool TryPerceive(out Vector3 seenPosition)
        {
            seenPosition = Vector3.zero;
            LastBlocker = null;
            if (target == null) { LastResult = SightResult.NoTarget; return false; }
            if (target.IsDead) { LastResult = SightResult.TargetDead; return false; }

            if (targetColliders == null || targetColliders.Length == 0) targetColliders = target.GetComponentsInChildren<Collider>();
            if (targetColliders.Length == 0) { LastResult = SightResult.NoTarget; return false; }

            Bounds b = targetColliders[0].bounds;
            for (int i = 1; i < targetColliders.Length; i++) b.Encapsulate(targetColliders[i].bounds);

            Vector3 eyePos = EyePos;
            // Cheap rejection with the bounds centre first (range, then FOV).
            Vector3 toCentre = b.center - eyePos;
            if (toCentre.magnitude > range) { LastResult = SightResult.OutOfRange; return false; }
            Vector3 flat = new Vector3(toCentre.x, 0f, toCentre.z);
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(fwd, flat) > fieldOfView * 0.5f)
            { LastResult = SightResult.OutOfFov; return false; }

            // Sample head / chest / legs so partial cover still allows detection.
            Vector3 e = b.extents;
            Vector3 c = b.center;
            Vector3 p0 = c, p1 = c + Vector3.up * e.y * 0.8f, p2 = c - Vector3.up * e.y * 0.8f;
            LastResult = SightResult.Occluded;
            if (Visible(eyePos, p0)) { seenPosition = p0; LastResult = SightResult.Seen; return true; }
            if (Visible(eyePos, p1)) { seenPosition = p1; LastResult = SightResult.Seen; return true; }
            if (Visible(eyePos, p2)) { seenPosition = p2; LastResult = SightResult.Seen; return true; }
            return false;
        }

        /// <summary>True if the first non-self, non-trigger collider along the ray belongs to the target.</summary>
        private bool Visible(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(from, d / dist, hits, dist + 0.05f, obstacleMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Collider first = null;
            for (int i = 0; i < n; i++)
            {
                var col = hits[i].collider;
                if (col.transform.IsChildOf(transform)) continue; // own body
                if (hits[i].distance < best) { best = hits[i].distance; first = col; }
            }
            if (first == null) return false;
            if (first.GetComponentInParent<Health>() == target) return true;
            LastBlocker = first;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Vector3 p = EyePos;
            float h = fieldOfView * 0.5f;
            Vector3 l = Quaternion.Euler(0, -h, 0) * transform.forward * range;
            Vector3 r = Quaternion.Euler(0, h, 0) * transform.forward * range;
            Gizmos.DrawLine(p, p + l); Gizmos.DrawLine(p, p + r);
        }
    }
}
