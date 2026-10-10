using UnityEditor;
using UnityEditor.AI;
using UnityEngine;
namespace FacilityBreach.Editor
{
    // Editor overlay only: never creates render meshes, colliders, or changes authored geometry.
    [InitializeOnLoad]
    public static class EnemyNavigationDebug
    {
        static EnemyNavigationDebug() { EditorApplication.playModeStateChanged += Changed; }
        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("FBHiddenNavigationOverlay", false))
            {
                NavMeshVisualizationSettings.showNavigation = SessionState.GetInt("FBPreviousNavigationRequests", 0);
                SessionState.SetBool("FBHiddenNavigationOverlay", false); return;
            }
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            var runtime = Object.FindAnyObjectByType<FacilityBreachRuntime>();
            if (runtime && !runtime.DevelopmentDiagnostics)
            {
                SessionState.SetInt("FBPreviousNavigationRequests", NavMeshVisualizationSettings.showNavigation);
                SessionState.SetBool("FBHiddenNavigationOverlay", true);
                NavMeshVisualizationSettings.showNavigation = 0;
            }
        }
        [MenuItem("Facility Breach/Debug/Toggle navigation overlay")]
        private static void Toggle()
        { NavMeshVisualizationSettings.showNavigation = NavMeshVisualizationSettings.showNavigation > 0 ? 0 : 1; SceneView.RepaintAll(); }
    }
}
