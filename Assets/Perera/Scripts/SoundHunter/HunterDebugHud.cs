using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Perera.SoundHunter
{
    /// <summary>Test-scene IMGUI readout: state, selected sound, hearing cost, memory expiry, senses.</summary>
    public class HunterDebugHud : MonoBehaviour
    {
        [SerializeField] private SoundHunterBrain brain;
        [SerializeField] private bool visible = true;

        private readonly List<SoundMemory.Entry> scratch = new List<SoundMemory.Entry>();
        private readonly StringBuilder sb = new StringBuilder();
        private GUIStyle style;

        public void Bind(SoundHunterBrain b) => brain = b;

        private void OnGUI()
        {
            if (!visible || brain == null) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = false };

            var l = brain.Listener;
            float now = l.Now;
            sb.Clear();
            sb.AppendLine("SOUND HUNTER (test scene)");
            sb.AppendLine($"State: {brain.State}   Move: {brain.Movement?.Status} {brain.Movement?.FailureReason}");
            if (brain.HasGoalEntry)
            {
                var e = brain.GoalEntry;
                sb.AppendLine($"Goal sound: {e.Sound.Stimulus.Category} src {e.Sound.Stimulus.SourceId} rev {e.Revision}");
                sb.AppendLine($"  cost {e.Sound.Cost:0.#}  remaining {e.Sound.Remaining:0.#}  at {e.Position:0.0}");
            }
            else if (brain.GoalIsLastSeen) sb.AppendLine($"Goal: last seen position {brain.GoalPosition:0.0}");
            else sb.AppendLine("Goal: none");
            sb.AppendLine($"Hunter zone: {(l.Graph != null ? l.Graph.NodeName(l.ListenerNode) : "?")}   vision: {brain.Vision.LastResult}");
            sb.AppendLine($"Heard {l.HeardCount}  inaudible {l.InaudibleCount}  rejected {l.RejectedCount}   path requests {brain.PathRequests}  failed moves {brain.FailedMoves}");
            l.Memory.CopyTo(scratch);
            sb.AppendLine($"Memory ({scratch.Count}/{l.Memory.Capacity}):");
            foreach (var e in scratch)
                sb.AppendLine($"  {e.Sound.Stimulus.Category} src {e.Sound.Stimulus.SourceId} rev {e.Revision}  cost {e.Sound.Cost:0.#}  expires in {l.Memory.SecondsUntilExpiry(e, now):0.0}s");
            sb.AppendLine("Log:");
            for (int i = Mathf.Max(0, brain.History.Count - 4); i < brain.History.Count; i++) sb.AppendLine("  " + brain.History[i]);

            GUI.Box(new Rect(Screen.width - 520, 10, 510, 270), sb.ToString(), style);
        }
    }
}
