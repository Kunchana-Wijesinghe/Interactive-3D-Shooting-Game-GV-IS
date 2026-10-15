using System;
using System.Collections.Generic;
using UnityEngine;

namespace Perera.SoundHunter
{
    /// <summary>
    /// Remembered sound origins. Stores where sounds were heard, never where the player is now.
    /// Time is passed explicitly (seconds on ONE caller-owned clock) so expiry is testable.
    ///
    /// Identity: a source is (SourceId, Category). Hearing it again REFRESHES the existing entry
    /// (new position, new timer) and assigns a NEW Revision. Revisions are unique and increasing,
    /// so consuming an old investigation (Forget(entry)) can never delete a newer refresh of the
    /// same source: the stale revision no longer exists and Forget returns false.
    ///
    /// An entry expires when now - HeardAt >= ExpirySeconds (boundary expires).
    /// Over Capacity the lowest-ranked entry is dropped.
    /// </summary>
    public sealed class SoundMemory
    {
        public readonly struct Entry
        {
            public readonly HeardSound Sound;
            public readonly float HeardAt;
            public readonly int Revision;
            public Vector3 Position => Sound.Stimulus.Position;
            public Entry(HeardSound sound, float heardAt, int revision) { Sound = sound; HeardAt = heardAt; Revision = revision; }
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly SoundRanker ranker;
        private int nextRevision = 1;
        private float expirySeconds = 10f;

        /// <summary>Must be finite and &gt; 0; invalid values are ignored.</summary>
        public float ExpirySeconds
        {
            get => expirySeconds;
            set { if (IsFinite(value) && value > 0f) expirySeconds = value; }
        }
        public int Capacity { get; set; }
        public int Count => entries.Count;

        public SoundMemory(SoundRanker ranker, float expirySeconds = 10f, int capacity = 4)
        {
            this.ranker = ranker;
            ExpirySeconds = expirySeconds;
            Capacity = Math.Max(1, capacity);
        }

        /// <summary>False (and nothing stored) if the sound or time is non-finite.</summary>
        public bool Remember(in HeardSound sound, float now) => Remember(sound, now, out _);

        public bool Remember(in HeardSound sound, float now, out Entry stored)
        {
            stored = default;
            var p = sound.Stimulus.Position;
            if (!IsFinite(now) || !IsFinite(sound.Cost) || !IsFinite(sound.Remaining) || !IsFinite(sound.Stimulus.Strength)
                || !IsFinite(p.x) || !IsFinite(p.y) || !IsFinite(p.z))
                return false;

            Prune(now);
            var entry = new Entry(sound, now, nextRevision++);
            stored = entry;
            for (int i = 0; i < entries.Count; i++)
            {
                var old = entries[i].Sound.Stimulus;
                if (old.SourceId == sound.Stimulus.SourceId && old.Category == sound.Stimulus.Category)
                {
                    entries[i] = entry; // refresh: latest hearing replaces position, timer and revision
                    return true;
                }
            }
            entries.Add(entry);
            if (entries.Count > Capacity) entries.RemoveAt(WorstIndex());
            return true;
        }

        /// <summary>Highest-ranked unexpired memory. False when nothing is remembered.</summary>
        public bool TryGetBest(float now, out Entry best)
        {
            Prune(now);
            best = default;
            if (entries.Count == 0) return false;
            int bi = 0;
            for (int i = 1; i < entries.Count; i++)
                if (ranker.Compare(entries[i].Sound, entries[bi].Sound) < 0) bi = i;
            best = entries[bi];
            return true;
        }

        /// <summary>The live entry for the same source as <paramref name="entry"/> (may be a newer revision).</summary>
        public bool TryGetLatestFor(in Entry entry, out Entry latest)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var s = entries[i].Sound.Stimulus;
                if (s.SourceId == entry.Sound.Stimulus.SourceId && s.Category == entry.Sound.Stimulus.Category)
                { latest = entries[i]; return true; }
            }
            latest = default;
            return false;
        }

        public bool Contains(int revision)
        {
            for (int i = 0; i < entries.Count; i++) if (entries[i].Revision == revision) return true;
            return false;
        }

        /// <summary>Drop exactly this revision once investigated. A refreshed (newer) entry survives.</summary>
        public bool Forget(in Entry entry)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Revision == entry.Revision) { entries.RemoveAt(i); return true; }
            return false;
        }

        public float SecondsUntilExpiry(in Entry entry, float now) => entry.HeardAt + expirySeconds - now;

        /// <summary>Copy of live entries (for debug display). Does not prune.</summary>
        public void CopyTo(List<Entry> destination) { destination.Clear(); destination.AddRange(entries); }

        public void Clear() => entries.Clear();

        public void Prune(float now)
        {
            if (!IsFinite(now)) return;
            entries.RemoveAll(e => now - e.HeardAt >= expirySeconds);
        }

        private int WorstIndex()
        {
            int w = 0;
            for (int i = 1; i < entries.Count; i++)
                if (ranker.Compare(entries[i].Sound, entries[w].Sound) > 0) w = i;
            return w;
        }

        private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
