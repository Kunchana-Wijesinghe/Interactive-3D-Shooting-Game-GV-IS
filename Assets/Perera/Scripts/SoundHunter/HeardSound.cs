using Perera.Core;

namespace Perera.SoundHunter
{
    /// <summary>A stimulus that reached the listener, with its propagation outcome.</summary>
    public readonly struct HeardSound
    {
        public readonly SoundStimulus Stimulus;
        /// <summary>Total acoustic cost from source node to listener node.</summary>
        public readonly float Cost;
        /// <summary>Effective budget (Strength * hearing multiplier) minus Cost. Always >= 0.</summary>
        public readonly float Remaining;

        public HeardSound(SoundStimulus stimulus, float cost, float budget)
        {
            Stimulus = stimulus; Cost = cost; Remaining = budget - cost;
        }
    }
}
