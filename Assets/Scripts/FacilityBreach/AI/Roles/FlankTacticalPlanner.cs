using System;
using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.AI.Roles.Flanking
{
    // Plain observations/results. Implementations must not look up a hidden live player.
    public interface IFlankEnvironment
    {
        FlankProbe Probe(Vector3 desiredPosition, Vector3 observedTarget);
    }

    public struct FlankProbe
    {
        public Vector3 Position;
        public bool Reachable, Occupied;
        public float RouteLength, FiringUsefulness;
    }

    public struct FlankScore
    {
        public float Angle, Route, Distance, Firing;
        public float Total => .35f * Angle + .25f * Route + .20f * Distance + .20f * Firing;
        public override string ToString() => $"U={Total:F3} angle={Angle:F2} route={Route:F2} distance={Distance:F2} firing={Firing:F2}";
    }

    public sealed class FlankCandidate
    {
        public int Id;
        public Vector3 Position;
        public float AngleDegrees, TargetDistance, RouteLength;
        public FlankScore Score;
        public string Rejection = "";
        public bool Viable => Rejection.Length == 0;
    }

    public sealed class FlankPlan
    {
        public readonly List<FlankCandidate> Candidates = new List<FlankCandidate>();
        public FlankCandidate Selected;
        public string Reason;
    }

    /// <summary>Ashen's IS utility policy, not a graph-search/pathfinding implementation.
    /// NavMesh costs and physical firing/occupancy facts are injected by the Unity adapter.</summary>
    public sealed class FlankTacticalPlanner
    {
        public const float MaximumRoute = 30, PreferredDistance = 6, SwitchMargin = .08f;
        private static readonly float[] Radii = { 4, 6, 8 };
        private static readonly float[] Angles = { -60, 60, -90, 90, -120, 120 };
        public static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        private static Vector3 Flat(Vector3 p) { p.y = 0; return p; }

        public static FlankScore Score(float angle, float length, float distance, float firing) => new FlankScore
        {
            Angle = Mathf.Clamp01(angle / 120),
            Route = 1 - Mathf.Clamp01(length / MaximumRoute),
            Distance = 1 - Mathf.Clamp01(Mathf.Abs(distance - PreferredDistance) / 3),
            Firing = Mathf.Clamp01(firing)
        };

        public FlankCandidate Evaluate(int id, Vector3 desired, Vector3 self, Vector3 observed,
            IFlankEnvironment environment, Func<Vector3, bool> excluded, Vector3? approachOrigin = null)
        {
            var c = new FlankCandidate { Id = id, Position = desired };
            if (!Finite(desired) || !Finite(self) || !Finite(observed)) { c.Rejection = "Invalid input"; return c; }
            if (excluded != null && excluded(desired)) { c.Rejection = "Failed-goal cooldown"; return c; }
            var p = environment.Probe(desired, observed); c.Position = p.Position; c.RouteLength = p.RouteLength;
            if (!p.Reachable || !Finite(p.Position) || !float.IsFinite(p.RouteLength) || p.RouteLength < 0)
                c.Rejection = "No complete navigation path";
            else if (excluded != null && excluded(p.Position)) c.Rejection = "Failed-goal cooldown";
            else if (p.Occupied) c.Rejection = "Observed teammate occupies position";
            else if (p.RouteLength > MaximumRoute) c.Rejection = "Route exceeds 30 units";
            else if (!float.IsFinite(p.FiringUsefulness) || p.FiringUsefulness <= 0) c.Rejection = "No useful firing line to observation";
            if (!c.Viable) return c;
            Vector3 approach = Flat((approachOrigin ?? self) - observed);
            if (approach.sqrMagnitude < .01f) approach = Vector3.forward;
            Vector3 radial = Flat(p.Position - observed);
            c.TargetDistance = radial.magnitude;
            c.AngleDegrees = Vector3.Angle(approach, radial);
            if (c.TargetDistance < 3 || c.TargetDistance > 9) c.Rejection = "Projected radius outside 3..9";
            else if (c.AngleDegrees < 45) c.Rejection = "Target-centered separation below 45 degrees";
            c.Score = Score(c.AngleDegrees, p.RouteLength, c.TargetDistance, p.FiringUsefulness);
            return c;
        }

        public FlankPlan Plan(Vector3 self, Vector3 observed, IFlankEnvironment environment,
            Func<Vector3, bool> excluded = null, Vector3? incumbent = null, Vector3? approachOrigin = null)
        {
            var result = new FlankPlan();
            if (!Finite(self) || !Finite(observed)) { result.Reason = "Invalid observation or agent position"; return result; }
            Vector3 approach = Flat((approachOrigin ?? self) - observed);
            if (approach.sqrMagnitude < .01f) approach = Vector3.forward;
            approach.Normalize(); int id = 0;
            foreach (float radius in Radii) foreach (float angle in Angles)
            {
                Vector3 desired = observed + Quaternion.AngleAxis(angle, Vector3.up) * approach * radius;
                var c = Evaluate(id++, desired, self, observed, environment, excluded, approachOrigin);
                // Sampling can collapse different raw candidates onto the same point.
                if (c.Viable && result.Candidates.Exists(x => x.Viable && (x.Position - c.Position).sqrMagnitude < .01f))
                    c.Rejection = "Duplicate projected position";
                result.Candidates.Add(c);
                if (c.Viable && (result.Selected == null || c.Score.Total > result.Selected.Score.Total + .00001f)) result.Selected = c;
            }
            result.Reason = result.Selected == null ? "All 18 candidates rejected" : "Highest utility; stable enumeration breaks ties";
            if (incumbent.HasValue)
            {
                var current = Evaluate(18, incumbent.Value, self, observed, environment, excluded, approachOrigin);
                result.Candidates.Add(current);
                if (current.Viable && (result.Selected == null || result.Selected.Score.Total < current.Score.Total + SwitchMargin))
                { result.Selected = current; result.Reason = "Kept viable incumbent: improvement below 0.08"; }
            }
            return result;
        }
    }
}
