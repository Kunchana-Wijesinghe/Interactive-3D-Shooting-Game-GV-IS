using System.Reflection;
using UnityEditor;
using UnityEditor.AI;
using UnityEngine;

namespace FacilityBreach.Editor
{
    // Editor drawing only. Never changes NavMesh data, components, scene geometry or materials.
    [InitializeOnLoad]
    public static class EnemyNavigationDebug
    {
        private const string DebugEnabledKey = "FacilityBreach.NavigationOverlayEnabled";
        // In Unity 6000.6 these are internal native-backed properties. The obsolete public
        // showNavigation property is only a managed counter and does not control drawing.
        private static readonly PropertyInfo ShowNavMesh = typeof(NavMeshVisualizationSettings)
            .GetProperty("showNavMesh", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        private static double nextCheck;

        static EnemyNavigationDebug()
        {
            EditorApplication.delayCall += ApplyPreference;
            EditorApplication.update += KeepNormalView;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (ShowNavMesh == null)
                Debug.LogWarning("Facility Breach: navigation display API unavailable. Disable Show NavMesh in the Scene view AI Navigation overlay.");
        }

        private static void OnPlayModeChanged(PlayModeStateChange state) => ApplyPreference();

        private static void KeepNormalView()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 0.25;
            if (!SessionState.GetBool(DebugEnabledKey, false)) HideNavigation();
        }

        private static void ApplyPreference()
        {
            if (!SessionState.GetBool(DebugEnabledKey, false)) HideNavigation();
        }

        private static void HideNavigation()
        {
            if (ShowNavMesh == null || !(bool)ShowNavMesh.GetValue(null)) return;
            ShowNavMesh.SetValue(null, false);
            RepaintViews();
        }

        private static void RepaintViews()
        {
            SceneView.RepaintAll();
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetType().FullName == "UnityEditor.GameView") window.Repaint();
        }

        [MenuItem("Facility Breach/Restore normal view")]
        private static void RestoreNormalView()
        {
            SessionState.SetBool(DebugEnabledKey, false);
            HideNavigation();
        }

        [MenuItem("Facility Breach/Debug/Toggle navigation overlay")]
        private static void Toggle()
        {
            if (ShowNavMesh == null) return;
            bool enabled = !SessionState.GetBool(DebugEnabledKey, false);
            SessionState.SetBool(DebugEnabledKey, enabled);
            ShowNavMesh.SetValue(null, enabled);
            RepaintViews();
        }
    }
}
