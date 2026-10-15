using System;
using UnityEngine;

namespace FacilityBreach.AI.Patrol
{
    /// <summary>
    /// Represents a discrete topological navigation node in the facility patrol network.
    /// Independent of visual animation, enemy combat, and character controllers.
    /// </summary>
    public sealed class PatrolNode : IEquatable<PatrolNode>
    {
        public int Id { get; }
        public Vector3 Position { get; }
        public string Name { get; }
        public bool IsVerified { get; }

        public PatrolNode(int id, Vector3 position, string name = null, bool isVerified = true)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
                throw new ArgumentException($"Node position must have finite coordinates. Received: {position}", nameof(position));

            Id = id;
            Position = position;
            Name = string.IsNullOrWhiteSpace(name) ? $"Node_{id}" : name;
            IsVerified = isVerified;
        }

        public bool Equals(PatrolNode other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override bool Equals(object obj) => Equals(obj as PatrolNode);

        public override int GetHashCode() => Id;

        public override string ToString() => $"{Name} (ID={Id}, Pos={Position}, Verified={IsVerified})";
    }
}

