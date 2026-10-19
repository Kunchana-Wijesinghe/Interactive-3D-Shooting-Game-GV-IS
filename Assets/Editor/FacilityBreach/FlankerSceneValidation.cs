using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FacilityBreach.AI.Roles;
using FacilityBreach.AI.Roles.Flanking;
using FacilityBreach.AI.SoundHunter;
using FacilityBreach.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.Editor
{
    // Opens Prototype and creates a temporary remote navigation patch in Play Mode; never saves scenes.
    public static class FlankerSceneValidation
    {
        private const string Pending = "FlankerScene.Pending", Batch = "FlankerScene.Batch";
        private static IEnumerator routine;
        private static readonly List<string> errors = new List<string>();
        private static int count;
        private static bool passed;
        private static double deadline;
        [MenuItem("Facility Breach/IS Flanker/Run Prototype Integration Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
            SessionState.SetBool(Pending, true); Subscribe(); EditorApplication.EnterPlaymode();
        }
        public static void RunBatch() { SessionState.SetBool(Batch, true); Run(); }
        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool(Pending, false)) Subscribe(); }
        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                count = 0; passed = false; errors.Clear(); deadline = EditorApplication.timeSinceStartup + 100;
                Application.logMessageReceived += Console; routine = Checks();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Pending, false); EditorApplication.update -= Tick;
                if (SessionState.GetBool(Batch, false)) { SessionState.SetBool(Batch, false); EditorApplication.Exit(passed ? 0 : 1); }
            }
        }
        private static void Console(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        private static void Tick()
        {
            if (routine == null) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Flanker integration timeout");
                if (routine.MoveNext()) return;
                passed = true; Finish();
            }
            catch (Exception error) { Debug.LogException(error); passed = false; Finish(); }
        }
        private static void Finish()
        {
            (routine as IDisposable)?.Dispose(); routine = null;
            Application.logMessageReceived -= Console; EditorApplication.ExitPlaymode();
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("IS Flanker integration FAIL: " + message); count++; Debug.Log("IS Flanker integration PASS: " + message); }
        private static void WarpPlayer(FacilityBreachRuntime runtime, Vector3 point)
        {
            var body = runtime.PlayerTransform.GetComponent<CharacterController>(); body.enabled = false;
            runtime.PlayerTransform.position = point; body.enabled = true; Physics.SyncTransforms();
        }
        private static IEnumerator Checks()
        {
            for (int i = 0; i < 20; i++) yield return null;
            var runtime = UnityEngine.Object.FindAnyObjectByType<FacilityBreachRuntime>(); runtime.StartMission();
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyCombatAdapter>();
            Check(enemies.Length == 4 && enemies.All(e => e.GetComponent<SoundHunterNavMeshMover>() && e.GetComponent<EnemyModelAnimation>()), "four runtime robots retain shared GV components");
            foreach (var e in enemies)
            {
                foreach (var b in e.GetComponents<MonoBehaviour>()) if (b is RoleDecisionBrain || b is FacilityBreachSoundHunterBrain) b.enabled = false;
                e.CancelPendingAttack(); e.MissChance = 1;
            }
            var fl = enemies.Single(e => e.Identity == "FL-04"); var brain = fl.GetComponent<FlankerBrain>(); var nav = fl.GetComponent<NavMeshAgent>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "IS test floor (temporary)";
            floor.transform.position = new Vector3(1000, -.1f, 1000); floor.transform.localScale = new Vector3(80, .2f, 80);
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(nav.agentTypeID), new List<NavMeshBuildSource> {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(80, .2f, 80), transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one) }
            }, new Bounds(new Vector3(1000, 0, 1000), new Vector3(84, 6, 84)), Vector3.zero, Quaternion.identity);
            var mesh = NavMesh.AddNavMeshData(data);
            try
            {
                Check(nav.Warp(new Vector3(1000, 0, 1000)), "real Flanker warped onto isolated legal navigation fixture");
                fl.transform.rotation = Quaternion.identity; nav.speed = 0;
                WarpPlayer(runtime, new Vector3(1000, .1f, 1012)); brain.enabled = true;
                float until = runtime.GameplayTime + .5f; while (runtime.GameplayTime < until) yield return null;
                Check(brain.Plans == 1 && brain.Policy.HasGoal && brain.FlankAngle >= 45, "Unity NavMesh/physics accept a flank for stationary target twelve units away");
                Check(!nav.updateRotation && brain.GetComponent<EnemyModelAnimation>().HasClips, "Flanker leaves shared rotation ownership and animation graph intact");
                var observation = brain.Policy.ObservedTarget; int queries = brain.NavigationQueries;
                WarpPlayer(runtime, new Vector3(1000, .1f, 1030));
                until = runtime.GameplayTime + .5f; while (runtime.GameplayTime < until) yield return null;
                Check(!brain.SeesPlayer && brain.Policy.ObservedTarget == observation && brain.NavigationQueries == queries, "hidden out-of-sight player movement causes no new tactical probes or memory changes");
                WarpPlayer(runtime, new Vector3(1000, .1f, 1012));
                until = runtime.GameplayTime + .4f; while (runtime.GameplayTime < until) yield return null;
                Check(brain.SeesPlayer, "actual shared perception reacquires player");
                runtime.SetPause(true); float clock = runtime.GameplayTime; int plans = brain.Plans;
                double realUntil = EditorApplication.timeSinceStartup + .2; while (EditorApplication.timeSinceStartup < realUntil) yield return null;
                Check(runtime.GameplayTime == clock && brain.Plans == plans, "pause freezes decision clock and replanning"); runtime.SetPause(false);
                nav.speed = 3; Vector3 start = fl.transform.position;
                until = runtime.GameplayTime + 1; while (runtime.GameplayTime < until) yield return null;
                Check(Vector3.Distance(start, fl.transform.position) > .3f, "new tactical decision produces actual shared-controller movement across frames");
                fl.TakeDamage(new Perera.Core.DamageInfo(70, fl.transform.position, Vector3.forward, runtime.PlayerTransform.gameObject));
                Check(fl.IsDead && !brain.Policy.HasObservation && brain.Plans == 0 && brain.Policy.Phase == FlankPhase.Inactive, "actual combat activity sink clears IS state immediately on death");
                Check(!fl.Warning && fl.GetComponent<SoundHunterNavMeshMover>().Status == Perera.SoundHunter.MoveStatus.Idle, "death preserves existing attack cancellation and shared movement stop");
                Check(errors.Count == 0, "no unexpected integration Console errors");
                Debug.Log($"[IS Flanker] PASS {count} Prototype integration checks (controlled Play Mode fixture).");
            }
            finally { UnityEngine.Object.Destroy(floor); if (mesh.valid) mesh.Remove(); UnityEngine.Object.Destroy(data); }
        }
    }
}
