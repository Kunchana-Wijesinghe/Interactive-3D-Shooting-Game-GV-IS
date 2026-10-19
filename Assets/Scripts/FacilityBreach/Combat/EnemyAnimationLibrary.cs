using System;
using UnityEngine;

namespace FacilityBreach.Combat
{
    /// <summary>References existing FBX subassets, so clips are included in player builds.</summary>
    public sealed class EnemyAnimationLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Set
        {
            public string Identity;
            public AnimationClip Idle, Move, Search, Attack, Defeat;
        }
        public Set[] Enemies;
        public Set Find(string identity) => Array.Find(Enemies ?? Array.Empty<Set>(), s => s.Identity == identity);
    }
}
