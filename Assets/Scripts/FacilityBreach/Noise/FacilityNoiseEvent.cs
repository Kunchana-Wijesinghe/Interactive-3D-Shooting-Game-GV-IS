using System;
using UnityEngine;

namespace FacilityBreach.Noise
{
    public enum FacilityNoiseType
    {
        Gunshot,
        Kick,
        PhysicsImpact,
        Interaction,
        Footstep
    }

    /// <summary>
    /// GV Part 2: Neutral Facility Noise Event.
    /// Represents a broadcast acoustic event in world space, completely decoupled
    /// from actual AudioSource playback and listener AI logic.
    /// Suitable for consumption by Patrol Guard hearing systems without coupling to audio hardware.
    /// </summary>
    public readonly struct FacilityNoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly FacilityNoiseType Type;
        public readonly GameObject Source;
        public readonly float Timestamp;

        public FacilityNoiseEvent(Vector3 position, float radius, FacilityNoiseType type, GameObject source = null, float timestamp = -1f)
        {
            Position = position;
            Radius = Mathf.Max(0.1f, radius);
            Type = type;
            Source = source;
            Timestamp = timestamp >= 0f ? timestamp : (Application.isPlaying ? Time.time : 0f);
        }

        public override string ToString()
        {
            return $"[FacilityNoiseEvent: {Type} at {Position:F1}, Radius={Radius:F1}m, Source={(Source != null ? Source.name : "null")}, Time={Timestamp:F2}s]";
        }
    }
}
