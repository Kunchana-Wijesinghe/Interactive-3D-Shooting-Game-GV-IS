using UnityEngine;

namespace FacilityBreach.AI.SoundHunter
{
    public static class AgentPerception
    {
        public static bool Visible(Transform observer, Transform target, float range, out Vector3 seen)
        {
            seen = Vector3.zero;
            if (!target) return false;
            Vector3 origin = observer.position + Vector3.up * 1.4f;
            Vector3 delta = target.position + Vector3.up - origin;
            if (delta.magnitude > range) return false;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(observer) && !hit.transform.IsChildOf(target)) return false;
            seen = target.position; return true;
        }
    }
}
