using System.Collections.Generic;
using FacilityBreach.AI.SoundHunter;
using FacilityBreach.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.AI.Roles.Flanking
{
    /// <summary>Queried only on confirmed sight. Probe endpoints are observation values, never a live player Transform.</summary>
    public sealed class FlankUnityEnvironment : IFlankEnvironment
    {
        private readonly Transform self;
        private readonly NavMeshAgent nav;
        private readonly List<Vector3> visibleOccupants = new List<Vector3>();
        private readonly NavMeshPath path = new NavMeshPath();
        private bool permitted, captured;
        private float sightRange, fieldOfView;
        public int PathQueries { get; private set; }
        public FlankUnityEnvironment(Transform self, NavMeshAgent nav) { this.self = self; this.nav = nav; }
        public void BeginDecision(bool targetVisible, float range, float fov)
        { permitted = targetVisible; captured = false; sightRange = range; fieldOfView = fov; }
        private void CaptureOccupants()
        {
            if (captured) return; captured = true; visibleOccupants.Clear();
            foreach (var other in Object.FindObjectsByType<EnemyCombatAdapter>())
                if (other.transform != self && !other.IsDead &&
                    AgentPerception.Visible(self, other.transform, sightRange, out var position, fieldOfView))
                    visibleOccupants.Add(position);
        }
        public FlankProbe Probe(Vector3 desiredPosition, Vector3 observedTarget)
        {
            var result = new FlankProbe { Position = desiredPosition };
            if (!permitted || !nav || !nav.isOnNavMesh || !FlankTacticalPlanner.Finite(desiredPosition) || !FlankTacticalPlanner.Finite(observedTarget)) return result;
            if (!NavMesh.SamplePosition(desiredPosition, out var sample, 1, nav.areaMask)) return result;
            result.Position = sample.position; PathQueries++;
            if (!nav.CalculatePath(sample.position, path) || path.status != NavMeshPathStatus.PathComplete) return result;
            result.Reachable = true;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++) result.RouteLength += Vector3.Distance(corners[i - 1], corners[i]);
            CaptureOccupants();
            foreach (var other in visibleOccupants)
                if (Vector3.Distance(other, sample.position) < 1) { result.Occupied = true; break; }
            bool body = ClearLine(sample.position + Vector3.up * 1.4f, observedTarget + Vector3.up);
            bool upper = body && ClearLine(sample.position + Vector3.up * 1.4f, observedTarget + Vector3.up * 1.3f);
            result.FiringUsefulness = body ? (upper ? 1 : .5f) : 0;
            return result;
        }
        private bool ClearLine(Vector3 origin, Vector3 end)
        {
            Vector3 delta = end - origin;
            if (delta.sqrMagnitude < .001f) return true;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(self) && !hit.collider.GetComponentInParent<PlayerDamageAdapter>()) return false;
            return true;
        }
    }
}
