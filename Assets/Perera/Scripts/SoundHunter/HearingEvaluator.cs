using Perera.Core;

namespace Perera.SoundHunter
{
    /// <summary>
    /// Decides whether a stimulus is audible to a listener standing at a graph node.
    /// Rule: audible iff minCost(sourceNode, listenerNode) &lt;= Strength * hearingMultiplier.
    /// The boundary is INCLUSIVE (cost == budget is heard). Disconnected nodes are never heard.
    /// Non-finite or negative strength/multiplier is rejected.
    /// The listener position is the hunter's own node, never the player's.
    ///
    /// In the game use the explicit-node overload: the caller maps positions to nodes with room
    /// membership rules (SceneHearingGraph). The position-based overload uses raw nearest-node and
    /// is only appropriate where no walls separate unrelated nodes (tests).
    /// </summary>
    public sealed class HearingEvaluator
    {
        private readonly HearingGraph graph;
        /// <summary>Scales every stimulus budget (e.g. a deaf/alert hunter). Must be finite and >= 0.</summary>
        public float HearingMultiplier { get; set; } = 1f;

        public HearingEvaluator(HearingGraph graph) { this.graph = graph; }

        public bool TryHear(in SoundStimulus stimulus, int listenerNode, out HeardSound heard)
        {
            heard = default;
            int source = graph.NearestNode(stimulus.Position);
            return source >= 0 && TryHear(stimulus, source, listenerNode, out heard);
        }

        public bool TryHear(in SoundStimulus stimulus, int sourceNode, int listenerNode, out HeardSound heard)
        {
            heard = default;
            float budget = stimulus.Strength * HearingMultiplier;
            if (float.IsNaN(budget) || float.IsInfinity(budget) || budget < 0f) return false;
            if (sourceNode < 0 || sourceNode >= graph.NodeCount || listenerNode < 0 || listenerNode >= graph.NodeCount) return false;
            float cost = graph.MinCost(sourceNode, listenerNode);
            if (float.IsInfinity(cost) || cost > budget) return false;
            heard = new HeardSound(stimulus, cost, budget);
            return true;
        }
    }
}
