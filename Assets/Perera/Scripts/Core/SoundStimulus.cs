using System;
using UnityEngine;

namespace Perera.Core
{
    public enum SoundCategory { Gunshot, Footstep, Thrown, Interaction, Other }

    /// <summary>
    /// Gameplay hearing event. Independent of AudioSource playback.
    /// Strength is the hearing budget the sound carries at its origin.
    /// </summary>
    public readonly struct SoundStimulus
    {
        public readonly Vector3 Position;
        public readonly float Strength;
        public readonly float CreatedTime;
        public readonly SoundCategory Category;
        public readonly int SourceId;

        public SoundStimulus(Vector3 position, float strength, SoundCategory category, int sourceId, float createdTime)
        {
            Position = position;
            Strength = strength;
            Category = category;
            SourceId = sourceId;
            CreatedTime = createdTime;
        }
    }

    /// <summary>Lightweight static event bus. Listeners must unsubscribe in OnDisable.</summary>
    public static class SoundEventBus
    {
        public static event Action<SoundStimulus> Emitted;

        public static void Emit(in SoundStimulus stimulus) => Emitted?.Invoke(stimulus);

        public static void Emit(Vector3 position, float strength, SoundCategory category, int sourceId)
            => Emitted?.Invoke(new SoundStimulus(position, strength, category, sourceId, Time.time));

        // Static state survives Play Mode entry when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Emitted = null;
    }
}
