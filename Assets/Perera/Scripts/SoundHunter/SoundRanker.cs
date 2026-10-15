using System;
using System.Collections.Generic;
using Perera.Core;

namespace Perera.SoundHunter
{
    /// <summary>
    /// Deterministic ordering of heard sounds. Better sound sorts first. Tie-break chain:
    ///  1. higher category priority
    ///  2. higher Remaining budget (louder / closer at the listener)
    ///  3. newer CreatedTime
    ///  4. lower SourceId
    ///  5. lexicographic Position (x, then y, then z), lower first
    /// Fully identical sounds compare equal (0).
    /// </summary>
    public sealed class SoundRanker : IComparer<HeardSound>
    {
        private readonly Dictionary<SoundCategory, int> priority = new Dictionary<SoundCategory, int>
        {
            { SoundCategory.Gunshot, 4 },
            { SoundCategory.Thrown, 3 },
            { SoundCategory.Interaction, 2 },
            { SoundCategory.Footstep, 1 },
            { SoundCategory.Other, 0 },
        };

        public void SetPriority(SoundCategory category, int value) { priority[category] = value; }
        public int GetPriority(SoundCategory category) => priority.TryGetValue(category, out var p) ? p : 0;

        public int Compare(HeardSound a, HeardSound b)
        {
            int c = GetPriority(b.Stimulus.Category).CompareTo(GetPriority(a.Stimulus.Category));
            if (c != 0) return c;
            c = b.Remaining.CompareTo(a.Remaining);
            if (c != 0) return c;
            c = b.Stimulus.CreatedTime.CompareTo(a.Stimulus.CreatedTime);
            if (c != 0) return c;
            c = a.Stimulus.SourceId.CompareTo(b.Stimulus.SourceId);
            if (c != 0) return c;
            var pa = a.Stimulus.Position; var pb = b.Stimulus.Position;
            c = pa.x.CompareTo(pb.x); if (c != 0) return c;
            c = pa.y.CompareTo(pb.y); if (c != 0) return c;
            return pa.z.CompareTo(pb.z);
        }

        /// <summary>Index of the best sound, or -1 if empty. First occurrence wins exact ties.</summary>
        public int IndexOfBest(IReadOnlyList<HeardSound> sounds)
        {
            int best = -1;
            for (int i = 0; i < sounds.Count; i++)
                if (best < 0 || Compare(sounds[i], sounds[best]) < 0) best = i;
            return best;
        }
    }
}
