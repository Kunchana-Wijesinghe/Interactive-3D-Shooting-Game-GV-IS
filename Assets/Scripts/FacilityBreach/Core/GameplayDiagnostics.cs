using System.Diagnostics;
using UnityEngine;

namespace FacilityBreach
{
    public static class GameplayDiagnostics
    {
        public static bool Enabled;
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Trace(string message, Object context = null)
        { if (Enabled) UnityEngine.Debug.Log("[FB gameplay] " + message, context); }
    }
}
