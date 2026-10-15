using System;

namespace FacilityBreach.AI.Patrol
{
    /// <summary>
    /// Represents a directed connection between two patrol nodes with non-negative traversal cost.
    /// Supports dynamic edge blocking for runtime obstacle/barricade responsiveness.
    /// </summary>
    public sealed class PatrolEdge
    {
        public int From { get; }
        public int To { get; }
        public float BaseCost { get; }
        public bool IsBlocked { get; set; }
        public bool IsVerified { get; }

        public float EffectiveCost => IsBlocked ? float.PositiveInfinity : BaseCost;

        public PatrolEdge(int from, int to, float baseCost, bool isVerified = true)
        {
            if (from == to)
                throw new ArgumentException($"Edge cannot connect a node to itself: {from} -> {to}", nameof(to));

            if (!float.IsFinite(baseCost) || baseCost < 0f)
                throw new ArgumentException($"Edge cost must be a non-negative finite number. Received: {baseCost}", nameof(baseCost));

            From = from;
            To = to;
            BaseCost = baseCost;
            IsBlocked = false;
            IsVerified = isVerified;
        }

        public void SetBlocked(bool blocked) => IsBlocked = blocked;

        public override string ToString() =>
            $"{From} -> {To} [Cost={BaseCost:F2}, Blocked={IsBlocked}, Verified={IsVerified}]";
    }
}

