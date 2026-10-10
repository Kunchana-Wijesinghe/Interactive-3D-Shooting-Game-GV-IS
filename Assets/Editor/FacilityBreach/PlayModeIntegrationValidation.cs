using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FacilityBreach.AI.SoundHunter;
using FacilityBreach.Combat;
using FacilityBreach.Core;
using FacilityBreach.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FacilityBreach.Editor
{
    public static class PlayModeIntegrationValidation
    {
        private static readonly Stack<IEnumerator> pending = new Stack<IEnumerator>();
        private static readonly List<string> errors = new List<string>();
        private static readonly List<string> evidence = new List<string>();
        private static double deadline;
        private static int checks;
        public static void Run()
        {
            File.Delete("integration-playmode-passed.txt"); File.Delete("integration-playmode-failed.txt");
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
            SessionState.SetBool("FBIntegrationTests", true); Register(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool("FBIntegrationTests", false)) Register(); }
        private static void Register()
        {
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update -= Step; EditorApplication.update += Step;
            Application.logMessageReceived -= Console; Application.logMessageReceived += Console;
        }
        private static void Console(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) pending.Push(Checks());
            if (state == PlayModeStateChange.EnteredEditMode && File.Exists("integration-playmode-passed.txt"))
            { SessionState.SetBool("FBIntegrationTests", false); EditorApplication.Exit(0); }
        }
        private static void Step()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Fail(new Exception("Play Mode validation timeout")); return; }
            if (pending.Count == 0) return;
            try
            {
                var task = pending.Peek();
                if (!task.MoveNext()) { if (pending.Count > 0 && ReferenceEquals(pending.Peek(), task)) pending.Pop(); return; }
                if (task.Current is IEnumerator nested) pending.Push(nested);
            }
            catch (Exception exception) { Fail(exception); }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("FAILED: " + message); checks++; evidence.Add("PASS: " + message); Debug.Log("FB integration PASS: " + message); }
        private static IEnumerator Wait(float duration)
        { float end = Time.time + duration; while (Time.time < end) yield return null; }
        private static void Warp(FacilityBreachRuntime run, Vector3 point, Vector3 aim)
        {
            var body = run.PlayerTransform.GetComponent<CharacterController>(); body.enabled = false;
            run.PlayerTransform.position = point; run.PlayerTransform.rotation = Quaternion.identity; body.enabled = true;
            run.GameplayCamera.transform.rotation = Quaternion.LookRotation(aim - run.GameplayCamera.transform.position);
            Physics.SyncTransforms();
        }
        private static void Approach(FacilityBreachRuntime run, ResourceStation station)
        { Warp(run, new Vector3(station.transform.position.x, .1f, station.transform.position.z - 1.2f), station.InteractionPoint); }
        private static IEnumerator Checks()
        {
            for (int i = 0; i < 20; i++) yield return null;
            var run = UnityEngine.Object.FindAnyObjectByType<FacilityBreachRuntime>();
            run.DevelopmentDiagnostics = true; run.StartMission();
            Check(run.Navigation && run.AcceptsGameplay, "Prototype loaded and mission accepts gameplay nav=" + (bool)run.Navigation + " state=" + run.Run.State + " hp=" + run.Resources.Health + " models=" + run.EnemyVisuals.Count(x=>x));
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyCombatAdapter>();
            Check(enemies.Length == 4 && enemies.All(x => x.CurrentHealth == x.MaximumHealth), "four authoritative enemy records initialized at full health");
            Check(enemies.All(x => x.transform.Find("ModelVisual") && !x.GetComponent<Renderer>().enabled), "all four live roots use imported robot visuals; capsule renderers hidden");
            var weaponRenderers = run.GameplayCamera.GetComponentsInChildren<Renderer>();
            Check(weaponRenderers.Length > 0 && weaponRenderers.Any(r => r.enabled && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(run.GameplayCamera), r.bounds)), "equipped FBX renderer intersects camera frustum");
            var pg = enemies.Single(x => x.Identity == "PG-01"); var sh = enemies.Single(x => x.Identity == "SH-01");
            var pgMover = pg.GetComponent<SoundHunterNavMeshMover>(); var shMover = sh.GetComponent<SoundHunterNavMeshMover>();
            Check(pgMover.OnNavigation && shMover.OnNavigation, "PG and SH agents on scoped facility navigation");
            Vector3 pgStart = pg.transform.position; yield return Wait(2);
            Check(Vector3.Distance(pgStart, pg.transform.position) > .2f && pgMover.PathRequests <= 8, "temporary visual PG patrol moves with bounded requests");
            var hunter = sh.GetComponent<FacilityBreachSoundHunterBrain>();
            Warp(run, new Vector3(0, .1f, 24), new Vector3(0, 1, 20)); int heardBefore = hunter.HeardCount;
            Vector3 emitted = run.PlayerTransform.position;
            Check(run.TryFire(), "actual player shot emits gunshot"); yield return Wait(.3f);
            Check(hunter.HeardCount == heardBefore + 1, "SH listener receives exactly one audible gunshot");
            Check(hunter.CurrentState == SoundHunterState.Investigate && hunter.HasSoundTarget && Vector3.Distance(hunter.InvestigationTarget, emitted) < .01f, "occluded gunshot triggers emitted-origin investigation");
            Warp(run, new Vector3(-12.5f, .1f, -28), new Vector3(-12.5f, 1, -24));
            yield return Wait(.6f);
            Check(Vector3.Distance(shMover.Goal, emitted) < .1f, "SH navigation goal does not follow hidden player's new position");
            yield return Wait(10.2f);
            Check(hunter.MemoryCount == 0 && hunter.CurrentState != SoundHunterState.Combat, "sound memory expires without new events");
            var stations = UnityEngine.Object.FindObjectsByType<ResourceStation>();
            var ammo2 = stations.Single(x => x.Kind == StationKind.Weapon && x.Weapon == 1);
            var ammo3 = stations.Single(x => x.Kind == StationKind.Weapon && x.Weapon == 2);
            Approach(run, ammo2);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name = "Validation solid obstruction";
            blocker.transform.position = Vector3.Lerp(run.GameplayCamera.transform.position, ammo2.InteractionPoint, .5f);
            blocker.transform.localScale = Vector3.one * .5f; Physics.SyncTransforms();
            Check(run.FindInteractionTarget(out var blockedReason) != ammo2 && !run.TryInteract() && blockedReason.Contains("Blocked"), "solid cover blocks pickup interaction");
            Check(run.TryFire() && run.LastShotCollider == blocker.GetComponent<Collider>(), "solid cover is nearest hitscan receiver");
            UnityEngine.Object.Destroy(blocker); yield return Wait(.22f);
            int mag1 = run.Resources.Magazine; int reserve = run.Resources.Reserve;
            run.BeginReload(); Approach(run, ammo2);
            Check(run.FindInteractionTarget(out _) == ammo2 && run.TryInteract() && run.Resources.Equipped == 1 && run.Resources.Magazine == 75, "same E interaction path equips Ammo2 and cancels reload");
            Approach(run, ammo2); Check(run.TryInteract() && run.Resources.Equipped == 0 && run.Resources.Magazine == mag1 && run.Resources.Reserve == reserve, "swap back preserves Ammo1 magazine/reserve");
            Approach(run, ammo3); Check(run.TryInteract() && run.Resources.Equipped == 2 && run.Resources.Magazine == 100, "same E interaction path equips Ammo3");
            Approach(run, ammo3); Check(run.TryInteract() && run.Resources.Equipped == 0 && run.Resources.Magazine == mag1 && run.Resources.Reserve == reserve, "swap back from Ammo3 creates no ammo");
            yield return null;
            Check(ammo2.DisplayVisual && ammo3.DisplayVisual, "pickup visuals instantiated and survive swaps");
            foreach (var enemy in enemies)
            {
                enemy.enabled = false; // Isolated full-health hit count; prohibit attacks/revival during this check.
                foreach (var sink in enemy.GetComponents<MonoBehaviour>().OfType<IEnemyActivitySink>()) sink.SetEnemyActive(false);
                int requiredHits = enemy.MaximumHealth / 10;
                Vector3 pose = enemy.transform.position + Vector3.left * 2;
                for (int shot = 1; shot <= requiredHits; shot++)
                {
                    Warp(run, new Vector3(pose.x, .1f, pose.z), enemy.transform.position + Vector3.up);
                    Check(run.TryFire() && run.LastShotCollider && run.LastShotCollider.GetComponentInParent<EnemyCombatAdapter>() == enemy, enemy.Identity + " actual hitscan hit " + shot);
                    Check(enemy.CurrentHealth == Mathf.Max(0, enemy.MaximumHealth - shot * 10), enemy.Identity + " authoritative HP after hit " + shot);
                    yield return Wait(.22f);
                }
                Check(enemy.IsDead && !enemy.Warning && !enemy.GetComponent<Collider>().enabled, enemy.Identity + " defeated after " + requiredHits + " successful Ammo1 hits");
            }
            var crate25 = stations.Single(x => x.Kind == StationKind.AmmoCrate && x.Quantity == 25);
            var crate50 = stations.Single(x => x.Kind == StationKind.AmmoCrate && x.Quantity == 50);
            foreach (var crate in new[] { crate25, crate50 })
            {
                Approach(run, crate); Check(run.TryFire() && crate.Broken, "one real shot breaks crate " + crate.Quantity);
                Check(crate.transform.childCount > 0 && crate.GetComponentsInChildren<Collider>().Count(c => c.enabled) == 1, "one associated ammo pickup exists");
                int before = run.Resources.Reserve; Approach(run, crate);
                Check(run.TryInteract() && run.Resources.Reserve == before + crate.Quantity && crate.Rounds == 0, "E collects " + crate.Quantity + " into reserve"); yield return Wait(.22f);
            }
            int needed = 50 - run.Resources.Magazine, beforeReload = run.Resources.Reserve;
            run.BeginReload(); yield return Wait(1.6f);
            Check(run.Resources.Magazine == 50 && run.Resources.Reserve == beforeReload - needed, "reload uses collected reserve");
            run.SetPause(true); double timeBefore = run.Run.Remaining; float crateBefore = crate25.RestoreRemaining;
            double realEnd = EditorApplication.timeSinceStartup + .3;
            while (EditorApplication.timeSinceStartup < realEnd) yield return null;
            Check(run.Run.Remaining == timeBefore && crate25.RestoreRemaining == crateBefore, "pause freezes mission and crate timers");
            run.SetPause(false); Time.timeScale = 8; yield return Wait(15.1f); Time.timeScale = 1;
            Check(!crate25.Broken && !crate50.Broken && crate25.Rounds == 25 && crate50.Rounds == 50, "both crates restore/refill after gameplay timeout");
            run.Resources.CollectAmmo(200); Approach(run, crate25); Check(run.TryFire() && crate25.Broken, "restored crate breaks once");
            Approach(run, crate25); Check(!run.TryInteract() && crate25.Rounds == 25, "full reserve rejects without consuming pickup");
            run.BeginReload(); yield return Wait(1.6f); Approach(run, crate25);
            Check(run.TryInteract() && run.Resources.Reserve == 200 && crate25.Rounds == 24, "partial cap transfer leaves 24 rounds");
            run.Restart(); for (int i = 0; i < 25; i++) yield return null;
            run = UnityEngine.Object.FindAnyObjectByType<FacilityBreachRuntime>();
            Check(run.Run.State == MissionState.Menu && run.Resources.Reserve == 50 && run.Resources.Magazine == 50 && UnityEngine.Object.FindObjectsByType<EnemyCombatAdapter>().Length == 4, "restart restores mission/resources/four roots without duplicates");
            Check(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1 && UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "restart retains one camera/listener");
            run.StartMission();
            enemies = UnityEngine.Object.FindObjectsByType<EnemyCombatAdapter>();
            var revivedTarget = enemies.Single(x => x.Identity == "CD-03");
            foreach (var other in enemies.Where(x => x != revivedTarget))
            {
                other.enabled = false;
                foreach (var sink in other.GetComponents<MonoBehaviour>().OfType<IEnemyActivitySink>()) sink.SetEnemyActive(false);
            }
            stations = UnityEngine.Object.FindObjectsByType<ResourceStation>();
            ammo2 = stations.Single(x => x.Kind == StationKind.Weapon && x.Weapon == 1);
            ammo3 = stations.Single(x => x.Kind == StationKind.Weapon && x.Weapon == 2);
            Vector3 shotPose = revivedTarget.transform.position + Vector3.left * 2;
            foreach (var pickup in new[] { ammo2, ammo3 })
            {
                Approach(run, pickup); Check(run.TryInteract(), "post-restart weapon swap");
                int before = revivedTarget.CurrentHealth, damage = run.Resources.Equipped == 1 ? 15 : 20;
                Warp(run, new Vector3(shotPose.x, .1f, shotPose.z), revivedTarget.transform.position + Vector3.up);
                Check(run.TryFire() && revivedTarget.CurrentHealth == before - damage, "confirmed Ammo" + (run.Resources.Equipped + 1) + " damage " + damage);
                yield return Wait(.22f);
                Approach(run, pickup); Check(run.TryInteract() && run.Resources.Equipped == 0, "return to Ammo1 after confirmed alternate-weapon hit");
            }
            for (int i = 0; i < 6; i++)
            {
                Warp(run, new Vector3(shotPose.x, .1f, shotPose.z), revivedTarget.transform.position + Vector3.up);
                Check(run.TryFire(), "revival setup actual shot"); yield return Wait(.22f);
            }
            Check(revivedTarget.IsDead && revivedTarget.Life == EnemyLife.TemporarilyInactive && !run.Enemies.ChallengeComplete, "single enemy starts temporary lifecycle");
            Warp(run, new Vector3(-12.5f, .1f, -28), new Vector3(-12.5f, 1, -24));
            Time.timeScale = 8; yield return Wait(29);
            Check(revivedTarget.IsDead, "no premature revival before thirty gameplay seconds");
            yield return Wait(2); Time.timeScale = 1;
            Check(!revivedTarget.IsDead && revivedTarget.CurrentHealth == 90 && revivedTarget.GetComponent<Collider>().enabled, "safe CD post revives at full HP after thirty gameplay seconds");
            Check(errors.Count == 0, "no unexpected Console errors: " + string.Join("; ", errors));
            evidence.Add("Programmatic API/physics/nav Play Mode checks in Prototype; manual keyboard/mouse and rendered image review are not claimed.");
            File.WriteAllText("integration-playmode-passed.txt", checks + " checks\n" + string.Join("\n", evidence));
            pending.Clear(); EditorApplication.isPlaying = false;
        }
        private static void Fail(Exception exception)
        {
            SessionState.SetBool("FBIntegrationTests", false); EditorApplication.update -= Step;
            File.WriteAllText("integration-playmode-failed.txt", exception + "\n" + string.Join("\n", evidence) + "\nErrors: " + string.Join("; ", errors));
            EditorApplication.Exit(1);
        }
    }
}
