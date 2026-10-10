using System;
using System.IO;
using System.Linq;
using FacilityBreach;
using FacilityBreach.Core;
using FacilityBreach.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FacilityBreach.Editor
{
    // Run only in an isolated project when the user's Editor is open.
    public static class FoundationValidation
    {
        private static int checks, frame;
        private static double deadline;
        private static bool playRequested;
        public static void BuildWindows()
        {
            var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Prototype.unity" },
                locationPathName = "Builds/FacilityBreach/FacilityBreach.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText("foundation-build.txt", report.summary.result + "; errors=" + report.summary.totalErrors + "; warnings=" + report.summary.totalWarnings);
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
        private static void Check(bool condition, string name)
        { if (!condition) throw new Exception("FAILED: " + name); checks++; Debug.Log("FB PASS: " + name); }
        public static void Run()
        {
            try
            {
                foreach (string stale in new[] { "foundation-failed.txt", "foundation-editmode.txt", "foundation-playmode.txt" })
                    if (File.Exists(stale)) File.Delete(stale);
                var run = new MissionRun(); Check(run.Remaining == 300 && run.State == MissionState.Menu, "menu timer 05:00");
                run.Start(); Check(!run.Connect(true), "card required"); Check(run.CollectCard() && !run.CollectCard(), "card once");
                Check(!run.Connect(false) && run.Connect(true) && !run.Connect(true), "range and duplicate connection");
                run.Paused = true; run.Tick(12); Check(run.Remaining == 300 && run.ShutdownRemaining == 5, "pause freezes both timers");
                run.Paused = false; run.Tick(2); Check(run.Remaining == 298 && run.State == MissionState.Deactivating, "connection clock continues");
                run.Tick(4); Check(run.Remaining == 295 && run.State == MissionState.Escape && run.Elapsed == 6, "substep completion preserves clock");
                run.Tick(1000); Check(run.Remaining == 295 && run.Exit(true), "escape unlimited and living victory");
                run.Lose("late damage"); Check(run.State == MissionState.Won, "victory terminal");
                run = new MissionRun(); run.Start(); run.Tick(295); run.CollectCard(); run.Connect(true); run.Tick(5);
                Check(run.State == MissionState.Lost, "exact timeout tie loses");
                run = new MissionRun(); run.Start(); run.Tick(297); run.CollectCard(); run.Connect(true); run.Tick(4);
                Check(run.State == MissionState.Lost && run.Elapsed == 300, "timeout inside shutdown step");
                run = new MissionRun(); run.Start(); Check(!run.Exit(true), "exit locked before shutdown");
                run.CollectCard(); run.Connect(true); run.Tick(5); run.Lose("Player eliminated"); Check(run.State == MissionState.Lost, "death after shutdown loses");
                var resources = new PlayerResources(); resources.Hurt(49); Check(!resources.Heal() && resources.Health == 51, "HP51 rejected");
                resources.Hurt(1); Check(resources.Heal() && resources.Health == 100, "HP50 heals to100");
                resources.Hurt(70); Check(resources.Heal() && resources.Health == 80, "HP30 heals to80");
                resources.Hurt(100); Check(!resources.Heal(), "no resurrection");
                resources = new PlayerResources(); resources.Shoot(); resources.Swap(1); resources.Shoot(); resources.Swap(0);
                Check(resources.Magazine == 49 && resources.Reserve == 50, "swaps conserve per weapon magazine");
                Check(resources.Reload() == 1 && resources.Magazine == 50 && resources.Reserve == 49, "reload tops up");
                Check(resources.CollectAmmo(200) == 151 && resources.CollectAmmo(25) == 0 && resources.Reserve == 200, "partial reserve transfer cap");
                resources.Swap(2); Check(resources.Magazine == 100 && resources.Damage[2] == 20, "third weapon identity");
                var roster = new EnemyRoster();
                foreach (string id in new[] { "PG-01", "SH-01", "CD-03", "FL-04" }) roster.Register(id, 80);
                roster.Damage("PG-01", 80, 0); roster.Damage("SH-01", 80, 1); roster.Damage("CD-03", 80, 2); roster.Damage("FL-04", 80, 30);
                Check(!roster.ChallengeComplete, "expired revival cannot count toward challenge");
                roster = new EnemyRoster();
                var pg = roster.Register("PG-01", 60); roster.Register("SH-01", 80); roster.Register("CD-03", 90); roster.Register("FL-04", 70);
                roster.Damage("PG-01", 60, 0); roster.Damage("SH-01", 80, 1); roster.Damage("CD-03", 90, 2); roster.Damage("FL-04", 70, 29.99);
                Check(roster.ChallengeComplete && pg.Life == EnemyLife.Permanent && !roster.Revive(pg, true), "all four temporary defeats become permanent");
                roster = new EnemyRoster(); pg = roster.Register("PG-01", 60); roster.Damage("PG-01", 60, 0); roster.Tick(30);
                Check(pg.Life == EnemyLife.AwaitingSafeSpawn && !roster.Revive(pg, false) && roster.Revive(pg, true) && pg.Health == 60, "safe spawn postponement and full revival");
                EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
                Check(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1, "one authored camera");
                Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "one authored listener");
                foreach (var obj in UnityEngine.Object.FindObjectsByType<GameObject>())
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj) > 0) throw new Exception("Missing script: " + obj.name);
                Check(UnityEngine.Object.FindObjectsByType<FacilityBreachRuntime>().Length == 1, "one scene mission root");
                File.WriteAllText("foundation-editmode.txt", "PASS " + checks + " assertions. Unity Editor execution; not NUnit.\n");
                SessionState.SetInt("FB_Checks", checks); SessionState.SetBool("FB_Validation", true);
                EditorApplication.playModeStateChanged += StateChanged;
                EditorApplication.update += Watch;
                deadline = EditorApplication.timeSinceStartup + 90;
                playRequested = true; EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Fail(error); }
        }
        [InitializeOnLoadMethod]
        private static void Restore()
        {
            if (!SessionState.GetBool("FB_Validation", false)) return;
            checks = SessionState.GetInt("FB_Checks", 0); deadline = EditorApplication.timeSinceStartup + 90; playRequested = true;
            EditorApplication.playModeStateChanged += StateChanged; EditorApplication.update += Watch;
        }
        private static void StateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) frame = 0;
            if (state == PlayModeStateChange.EnteredEditMode && File.Exists("foundation-playmode.txt"))
            { SessionState.SetBool("FB_Validation", false); EditorApplication.Exit(0); }
        }
        private static void Watch()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Fail(new Exception("Playmode validation timeout")); return; }
            if (!playRequested || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (++frame < 15) return;
            try
            {
                var runtime = UnityEngine.Object.FindAnyObjectByType<FacilityBreachRuntime>();
                Check(runtime && runtime.PlayerTransform, "Prototype runtime player created");
                Check(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1 && UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "no duplicate runtime camera or listener");
                var stations = UnityEngine.Object.FindObjectsByType<ResourceStation>();
                Check(stations.Length == 7, "seven foundation stations");
                Check(UnityEngine.Object.FindObjectsByType<GameObject>().Count(x => x.name.StartsWith("Card required")) == 3, "all three Machinery gates");
                Check(GameObject.Find("FB_ToxicWaste_IntegrationPlaceholder") != null, "missing authored waste represented explicitly");
                foreach (var s in stations)
                {
                    var overlaps = Physics.OverlapBox(s.transform.position, s.transform.localScale * .45f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    var blocking = overlaps.Where(c => c.gameObject != s.gameObject && !c.transform.IsChildOf(s.transform)).ToArray();
                    Check(blocking.Length == 0, "pickup clear geometry: " + s.name + "; blockers=" + string.Join(",", blocking.Select(c => c.name)));
                }
                runtime.Run.Start();
                CombatChecks(runtime);
                var card = stations.Single(x => x.Kind == StationKind.Card);
                Check(card.Use(runtime.Run, runtime.Resources) && runtime.Run.HasCard && !card.Available, "Prototype card interaction");
                var crate = stations.First(x => x.Kind == StationKind.AmmoCrate && x.Quantity == 50);
                Check(crate.TakeDamage(new Perera.Core.DamageInfo(10, crate.transform.position, Vector3.forward, runtime.gameObject)), "Prototype crate one shot");
                runtime.Resources.CollectAmmo(140); Check(crate.Use(runtime.Run, runtime.Resources) && crate.Rounds == 40, "crate partial pickup remains");
                crate.Tick(14.99f); Check(crate.Broken, "crate before15"); crate.Tick(.02f); Check(!crate.Broken && crate.Rounds == 50, "crate restores without stale ammo");
                var health = stations.Single(x => x.Kind == StationKind.Health);
                runtime.Resources.Hurt(50); Check(health.Use(runtime.Run, runtime.Resources) && !health.Available, "Prototype health interaction");
                health.Tick(5); Check(health.Available, "health respawn5");
                stations.Single(x => x.Kind == StationKind.Device).Use(runtime.Run, runtime.Resources);
                runtime.Run.Tick(5); Check(runtime.Run.ExitUnlocked && runtime.Run.Exit(true), "Prototype victory contract scenario");
                var defeat = new MissionRun(); defeat.Start(); defeat.Tick(300); Check(defeat.State == MissionState.Lost, "defeat scenario timer");
                File.WriteAllText("foundation-playmode.txt", "PASS " + checks + " cumulative assertions in Prototype Play mode. Programmatic interactions/contracts; no manual keyboard, mouse, audio or lighting verification.\n");
                EditorApplication.update -= Watch; EditorApplication.isPlaying = false;
            }
            catch (Exception error) { Fail(error); }
        }
        private static void CombatChecks(FacilityBreachRuntime runtime)
        {
            var view = Camera.main;
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Validation damage target";
            target.transform.position = view.transform.position + view.transform.forward * 7;
            var crate = target.AddComponent<ResourceStation>(); crate.Kind = StationKind.AmmoCrate;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var fire = typeof(FacilityBreachRuntime).GetMethod("TryFire", flags | System.Reflection.BindingFlags.Public);
            var cadence = typeof(FacilityBreachRuntime).GetField("cadence", flags);
            int heard = 0;
            Action<Perera.Core.SoundStimulus> listener = sound => { if (sound.Category == Perera.Core.SoundCategory.Gunshot) heard++; };
            Perera.Core.SoundEventBus.Emitted += listener;
            try
            {
                Physics.SyncTransforms(); fire.Invoke(runtime, null);
                Check(!crate.Broken && runtime.Resources.Magazine == 49 && heard == 1, "existing Entrance wall blocks shot; miss consumes and emits sound");
                target.transform.position = view.transform.position + view.transform.forward * 1.2f;
                cadence.SetValue(runtime, 0f); Physics.SyncTransforms(); fire.Invoke(runtime, null);
                Check(crate.Broken && runtime.Resources.Magazine == 48 && heard == 2, "nearest exposed crate hit once");
                var muzzle = (Transform)typeof(FacilityBreachRuntime).GetField("muzzle", flags).GetValue(runtime);
                var blocked = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocked.name = "Validation muzzle obstruction";
                blocked.transform.position = muzzle.position + Vector3.forward * .12f;
                blocked.transform.localScale = Vector3.one * .15f;
                var target2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target2.transform.position = view.transform.position + view.transform.forward * 1.8f;
                var crate2 = target2.AddComponent<ResourceStation>(); crate2.Kind = StationKind.AmmoCrate;
                target.SetActive(false); cadence.SetValue(runtime, 0f); Physics.SyncTransforms(); fire.Invoke(runtime, null);
                Check(!crate2.Broken && heard == 3, "muzzle obstruction blocks camera-visible target");
                UnityEngine.Object.Destroy(blocked); UnityEngine.Object.Destroy(target2);
            }
            finally { Perera.Core.SoundEventBus.Emitted -= listener; UnityEngine.Object.Destroy(target); }
        }
        private static void Fail(Exception error)
        { SessionState.SetBool("FB_Validation", false); Debug.LogException(error); File.WriteAllText("foundation-failed.txt", error.ToString()); EditorApplication.Exit(1); }
    }
}
