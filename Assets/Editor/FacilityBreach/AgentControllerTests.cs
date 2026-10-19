using System;
using System.Collections.Generic;
using System.Reflection;
using FacilityBreach.AI.SoundHunter;
using Perera.SoundHunter;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.Editor
{
    public static class AgentControllerTests
    {
        private static int checks;
        private static void Check(bool ok, string message) { if (!ok) throw new Exception("GV agent check failed: " + message); checks++; }
        private static void Call(object instance, string method) => instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);

        [MenuItem("Facility Breach/GV Agent Controller/Run Movement Checks")]
        public static void RunAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run movement checks outside Play Mode.");
            checks = 0;
            var route = new AgentRoute(); var results = new List<RouteState>();
            route.Ended += (_, state, __) => results.Add(state);
            var source = new[] { Vector3.right, Vector3.forward };
            Check(route.Replace(source), "ordered route accepted"); source[0] = Vector3.one * 99;
            Check(route.Current == Vector3.right, "caller cannot mutate owned route");
            Check(route.Advance() && route.Current == Vector3.forward, "ordered intermediate arrival");
            Check(!route.Advance() && route.State == RouteState.Arrived, "final arrival");
            Check(!route.Advance() && results.Count == 1, "completion reported once");
            route.Replace(new[] { Vector3.zero }); int revision = route.Revision;
            route.Replace(new[] { Vector3.one });
            Check(results[1] == RouteState.Interrupted && route.Revision == revision + 1, "replacement interrupts previous revision");
            route.Fail("Blocked/no movement progress"); Check(route.State == RouteState.Failed && route.Reason.Contains("Blocked"), "failure retained");
            route.Replace(null); Check(route.State == RouteState.Arrived, "null route completes without travel");
            route.Replace(Array.Empty<Vector3>()); Check(route.State == RouteState.Arrived, "empty route completes");
            Check(!route.Replace(new[] { new Vector3(float.NaN, 0, 0) }) && route.State == RouteState.Failed, "NaN rejected");
            Check(!route.Replace(new[] { new Vector3(0, float.PositiveInfinity, 0) }), "infinity rejected");
            route.Reset(); Check(route.State == RouteState.Idle && route.Count == 0 && route.Reason == "", "reset clears route");
            Quaternion a = Quaternion.identity, b = Quaternion.identity;
            for (int i = 0; i < 30; i++) a = SoundHunterNavMeshMover.TurnTowards(a, Vector3.right, 45, 1f / 30);
            for (int i = 0; i < 144; i++) b = SoundHunterNavMeshMover.TurnTowards(b, Vector3.right, 45, 1f / 144);
            Check(Quaternion.Angle(a, b) < .15f && Mathf.Abs(Quaternion.Angle(Quaternion.identity, a) - 45) < .15f,
                $"turning agrees at 30 and 144 frames per second (difference={Quaternion.Angle(a, b):F4}, angle={Quaternion.Angle(Quaternion.identity, a):F4})");
            Check(SoundHunterNavMeshMover.TurnTowards(a, Vector3.up, 45, 1) == a, "vertical/zero planar aim preserves rotation");
            Check(SoundHunterNavMeshMover.TurnTowards(a, Vector3.right, 45, 0) == a, "zero elapsed time preserves rotation");
            Debug.Log($"[GV Agent Controller] PASS {checks} route and turning checks (Edit Mode).");
        }

        private static void RunNavigationChecks()
        {
            checks = 0;
            // Exercise the actual adapter on an isolated, synchronous NavMesh; no mission Start or scene edits.
            var runtimeObject = new GameObject("GV test runtime"); var actor = new GameObject("GV test agent");
            NavMeshDataInstance mesh = default;
            NavMeshData data = null;
            try
            {
                var runtime = runtimeObject.AddComponent<FacilityBreachRuntime>(); runtime.enabled = false; runtime.Run.Start();
                var settings = NavMesh.GetSettingsByID(0);
                data = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource> {
                    new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(16, .2f, 16), transform = Matrix4x4.TRS(new Vector3(1000, -.1f, 1000), Quaternion.identity, Vector3.one), area = 0 }
                }, new Bounds(new Vector3(1000, 0, 1000), new Vector3(20, 6, 20)), Vector3.zero, Quaternion.identity);
                Check(data, "test navigation built"); mesh = NavMesh.AddNavMeshData(data);
                actor.transform.position = new Vector3(1000, 0, 1000);
                var mover = actor.AddComponent<SoundHunterNavMeshMover>(); Call(mover, "Awake");
                typeof(SoundHunterNavMeshMover).GetField("runtime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mover, runtime);
                var nav = actor.GetComponent<NavMeshAgent>(); Check(nav.Warp(actor.transform.position) && mover.OnNavigation, "agent on legal navigation");
                Check(!nav.updateRotation, "NavMesh root rotation disabled");
                Check(mover.RequestRoute(new[] { actor.transform.position + Vector3.right * 3, actor.transform.position + Vector3.forward * 3 }), "multi-point NavMesh route accepted");
                Check(mover.Status == MoveStatus.Moving && nav.hasPath, "route uses NavMesh path");
                runtime.Run.Paused = true; Call(mover, "Update");
                Check(nav.isStopped && mover.Route.State == RouteState.Following, "pause retains route and stops navigation");
                runtime.Run.Paused = false; Call(mover, "Update"); Check(!nav.isStopped, "resume restores travel");
                mover.Stop(); Check(!nav.hasPath && mover.Route.State == RouteState.Interrupted && mover.Status == MoveStatus.Idle, "stop clears path and reports interruption");
                mover.RequestRoute(new[] { actor.transform.position + Vector3.right * 2 }); mover.SetEnemyActive(false);
                bool inactiveAccepted = mover.RequestRoute(new[] { actor.transform.position });
                Check(nav.isStopped && !nav.hasPath && !inactiveAccepted,
                    $"death/activity suspension blocks travel (stopped={nav.isStopped}, path={nav.hasPath}, accepted={inactiveAccepted}, onMesh={mover.OnNavigation})");
                mover.SetEnemyActive(true); mover.ResetEnemyActions();
                Check(mover.Route.State == RouteState.Idle && mover.FailureReason == "", "revival reset clears stale route");
                Check(mover.RequestRoute(null) && mover.Status == MoveStatus.Arrived && !nav.hasPath, "empty adapter route stops old travel");
                Check(!mover.RequestRoute(new[] { actor.transform.position }, float.NaN) && mover.Status == MoveStatus.Failed, "invalid tolerance fails safely");
                Check(!mover.RequestRoute(new[] { new Vector3(2000, 0, 2000) }) && mover.Status == MoveStatus.Failed && !nav.hasPath, "off-mesh goal fails without direct movement");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(runtimeObject); if (mesh.valid) mesh.Remove(); if (data) UnityEngine.Object.DestroyImmediate(data); }
            Debug.Log($"[GV Agent Controller] PASS {checks} navigation lifecycle checks (Play Mode).");
        }
        private const string Pending = "GVAgentChecks.Pending", Batch = "GVAgentChecks.Batch", Result = "GVAgentChecks.Result";
        [InitializeOnLoadMethod]
        private static void Subscribe() { EditorApplication.playModeStateChanged -= PlayState; EditorApplication.playModeStateChanged += PlayState; }
        [MenuItem("Facility Breach/GV Agent Controller/Run Play Mode Movement Checks")]
        public static void BeginPlayChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start checks outside Play Mode.");
            Subscribe(); SessionState.SetBool(Pending, true); SessionState.SetInt(Result, 1); EditorApplication.EnterPlaymode();
        }
        private static void PlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                try { RunNavigationChecks(); SessionState.SetInt(Result, 0); }
                catch (Exception error) { Debug.LogException(error); }
                finally { EditorApplication.ExitPlaymode(); }
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Pending, false);
                if (SessionState.GetBool(Batch, false)) { SessionState.SetBool(Batch, false); EditorApplication.Exit(SessionState.GetInt(Result, 1)); }
            }
        }
        public static void RunBatch()
        {
            try { AgentAnimationLibraryBuilder.Rebuild(); RunAll(); SessionState.SetBool(Batch, true); BeginPlayChecks(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
