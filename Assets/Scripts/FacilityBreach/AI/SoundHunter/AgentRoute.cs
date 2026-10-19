using System;
using System.Collections.Generic;
using UnityEngine;

namespace FacilityBreach.AI.SoundHunter
{
    public enum RouteState { Idle, Following, Arrived, Interrupted, Failed }

    /// <summary>Owned copy of AI world-space points. No perception, planning or transform writes.</summary>
    public sealed class AgentRoute
    {
        private Vector3[] points = Array.Empty<Vector3>();
        public RouteState State { get; private set; }
        public int Index { get; private set; }
        public int Count => points.Length;
        public int Revision { get; private set; }
        public string Reason { get; private set; } = "";
        public Vector3 Current => points[Index];
        public event Action<int, RouteState, string> Ended;

        public bool Replace(IReadOnlyList<Vector3> route)
        {
            Interrupt("Replaced by a new route");
            Revision++; Index = 0; Reason = "";
            points = route == null ? Array.Empty<Vector3>() : new Vector3[route.Count];
            for (int i = 0; i < points.Length; i++)
            {
                if (!Finite(route[i])) { Finish(RouteState.Failed, "Non-finite route point"); return false; }
                points[i] = route[i];
            }
            State = RouteState.Following;
            if (points.Length == 0) Finish(RouteState.Arrived, "Empty route; no travel required");
            return true;
        }

        public bool Advance()
        {
            if (State != RouteState.Following) return false;
            if (++Index >= Count) { Finish(RouteState.Arrived, ""); return false; }
            return true;
        }
        public void Fail(string reason) { if (State == RouteState.Following) Finish(RouteState.Failed, reason); }
        public void Reject(string reason)
        {
            Interrupt("Replaced by an invalid route"); Revision++; Index = 0; points = Array.Empty<Vector3>();
            Finish(RouteState.Failed, reason);
        }
        public void Interrupt(string reason) { if (State == RouteState.Following) Finish(RouteState.Interrupted, reason); }
        public void Reset() { Interrupt("Reset"); points = Array.Empty<Vector3>(); Index = 0; State = RouteState.Idle; Reason = ""; }
        private void Finish(RouteState state, string reason) { State = state; Reason = reason; Ended?.Invoke(Revision, state, reason); }
        public static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
