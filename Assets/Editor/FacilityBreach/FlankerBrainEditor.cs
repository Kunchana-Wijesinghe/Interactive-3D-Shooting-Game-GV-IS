using FacilityBreach.AI.Roles;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    [CustomEditor(typeof(FlankerBrain))]
    public sealed class FlankerBrainEditor : UnityEditor.Editor
    {
        private bool showCandidates;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var brain = (FlankerBrain)target; var p = brain.Policy;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Ashen IS Flanker — cached diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State", p.Phase.ToString());
            EditorGUILayout.HelpBox(p.Reason, MessageType.Info);
            EditorGUILayout.LabelField("Last recovery cause", p.LastRecoveryReason);
            EditorGUILayout.LabelField("Replans / episode attempts", $"{p.ReplanCount} / {p.Attempts}");
            EditorGUILayout.LabelField("Return attempts / NavMesh queries", $"{p.ReturnAttempts} / {brain.NavigationQueries}");
            EditorGUILayout.LabelField("Observation", p.HasObservation ? $"{p.ObservedTarget:F2} at {p.LastObservedAt:F1}s" : "None / expired");
            EditorGUILayout.LabelField("Frozen approach origin", p.ApproachOrigin.ToString("F2"));
            if (p.Selected != null)
            {
                EditorGUILayout.LabelField("Selected goal", p.Selected.Position.ToString("F2"));
                EditorGUILayout.LabelField("Candidate / angle / radius", $"{p.Selected.Id} / {p.Selected.AngleDegrees:F1}° / {p.Selected.TargetDistance:F1}");
                EditorGUILayout.HelpBox(p.Selected.Score.ToString(), MessageType.None);
            }
            EditorGUILayout.LabelField("Last selection", p.LastPlan?.Reason ?? "No plan");
            showCandidates = EditorGUILayout.Foldout(showCandidates, "Last plan: all candidates and rejection reasons");
            if (showCandidates && p.LastPlan != null)
                foreach (var c in p.LastPlan.Candidates)
                    EditorGUILayout.LabelField($"#{c.Id} {c.Position:F1}", c.Viable ? c.Score.ToString() : c.Rejection);
            if (Application.isPlaying) Repaint(); // Reads cached data; never asks for a new plan/path.
        }
    }
}
