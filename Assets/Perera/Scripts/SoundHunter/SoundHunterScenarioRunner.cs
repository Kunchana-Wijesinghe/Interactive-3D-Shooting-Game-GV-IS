using System.Collections;
using System.Collections.Generic;
using Perera.Combat;
using Perera.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Perera.SoundHunter
{
    /// <summary>
    /// TEST-ONLY automated Play Mode verification for Perera_SoundHunterTest.
    /// Leave Auto Run OFF for normal play. Enable it (or tick "Run Now" via the context menu) to
    /// execute every scenario with accelerated time and print PASS/FAIL lines plus a summary.
    /// In batch mode the editor exits with code 0 only when every check passed.
    /// </summary>
    public class SoundHunterScenarioRunner : MonoBehaviour
    {
        [SerializeField] private bool autoRun;
        [SerializeField] private float timeScale = 4f;
        [SerializeField] private SoundHunterBrain hunter;
        [SerializeField] private Health player;
        [SerializeField] private HitscanWeapon weapon;
        [SerializeField] private HunterTestControls controls;
        [SerializeField] private Transform patrolStart;

        public void Bind(SoundHunterBrain h, Health p, HitscanWeapon w, HunterTestControls c, Transform start)
        { hunter = h; player = p; weapon = w; controls = c; patrolStart = start; }

        // survives the scene reload used by the restart check
        private static int phase;
        private static int passed, failed;

        private void Start()
        {
            bool batch = Application.isBatchMode;
            if (autoRun || (batch && System.Environment.GetEnvironmentVariable("PERERA_AUTORUN") == "1"))
                StartCoroutine(Run());
        }

        [ContextMenu("Run Now")]
        private void RunNow() => StartCoroutine(Run());

        private void Check(bool ok, string name)
        {
            if (ok) passed++; else failed++;
            Debug.Log($"[Perera.Scenario] {(ok ? "PASS" : "FAIL")}: {name}");
        }

        private IEnumerator Until(System.Func<bool> cond, float timeout)
        {
            float end = Time.time + timeout;
            while (!cond() && Time.time < end) yield return null;
        }

        private void ResetWorld(Vector3? hunterPos = null, float yaw = 90f)
        {
            player.ResetHealth();
            hunter.GetComponent<Health>().ResetHealth();
            var agent = hunter.GetComponent<NavMeshAgent>();
            Vector3 p = hunterPos ?? patrolStart.position;
            agent.Warp(p);
            hunter.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            hunter.gameObject.SetActive(false); // full disable/enable = restart path
            hunter.gameObject.SetActive(true);
            hunter.Vision.enabled = true;
            agent.Warp(p);
            hunter.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            hunter.ResetBehaviour();
        }

        private IEnumerator Run()
        {
            Time.timeScale = timeScale;
            if (phase == 0) { passed = failed = 0; }
            yield return null; yield return null;

            if (phase == 1) { yield return Phase2(); yield break; }

            var L = hunter.Listener;
            var mem = L.Memory;

            // ---- Scenario 1: audible shot triggers investigation (real weapon, real bus) ----
            ResetWorld();
            controls.Teleport(0);                          // player in room A
            yield return new WaitForSeconds(0.3f);
            bool fired = weapon.TryFire();
            yield return Until(() => hunter.State == HunterState.Investigate, 3f);
            Check(fired, "S1 player weapon fired");
            Check(hunter.State == HunterState.Investigate, "S1 audible gunshot (cost 24 <= 45 via weighted route) triggers Investigate");
            Check(L.HasLastHeard && Mathf.Abs(L.LastHeard.Cost - 24f) < 0.01f, $"S1 Dijkstra picks 8+8+8=24 over direct 40 (cost {L.LastHeard.Cost})");
            Vector3 shotPos = hunter.GoalPosition;
            Check(L.HasLastHeard && (L.LastHeard.Stimulus.Position - shotPos).sqrMagnitude < 1e-6f, "S1 goal is the exact remembered sound position");

            // ---- Scenario 2: moving away silently keeps investigating the ORIGINAL shot ----
            controls.Teleport(5);                          // player to far room F, no sound
            yield return new WaitForSeconds(2f);
            Check(hunter.State == HunterState.Investigate && (hunter.GoalPosition - shotPos).sqrMagnitude < 1e-6f,
                "S2 silent relocation does not change the goal (no live player tracking)");

            // ---- Scenario 3: arrival -> search -> consume -> patrol ----
            yield return Until(() => hunter.State == HunterState.Search, 40f);
            Check(hunter.State == HunterState.Search, "S3 arrival leads to Search");
            Check(Vector3.Distance(new Vector3(hunter.transform.position.x, 0, hunter.transform.position.z), new Vector3(shotPos.x, 0, shotPos.z)) < 2.5f, "S3 search happens at the sound location");
            yield return Until(() => hunter.State == HunterState.Patrol, 15f);
            Check(hunter.State == HunterState.Patrol && mem.Count == 0, "S3 search ends, sound consumed, back to Patrol");

            // ---- Scenario 4: inaudible / disconnected / unmapped sounds ignored ----
            ResetWorld();
            int heard0 = L.HeardCount, inaud0 = L.InaudibleCount, rej0 = L.RejectedCount;
            controls.EmitAt(GetPoint("vault"), 200f, SoundCategory.Gunshot, HunterTestControls.SourceVault);
            controls.EmitAt(GetPoint("a"), 10f, SoundCategory.Footstep, HunterTestControls.SourceQuiet);
            SoundEventBus.Emit(new Vector3(100f, 1f, 100f), 500f, SoundCategory.Gunshot, 777);              // outside every zone
            SoundEventBus.Emit(new Vector3(float.NaN, 1f, 0f), 45f, SoundCategory.Gunshot, 778);             // non-finite position
            SoundEventBus.Emit(GetPoint("a").position, float.PositiveInfinity, SoundCategory.Gunshot, 779);  // non-finite strength
            yield return new WaitForSeconds(1.5f);
            Check(L.HeardCount == heard0 && mem.Count == 0 && hunter.State == HunterState.Patrol, "S4 nothing stored, hunter keeps patrolling");
            Check(L.InaudibleCount - inaud0 == 2, $"S4 vault(disconnected) and weak footstep counted inaudible ({L.InaudibleCount - inaud0})");
            Check(L.RejectedCount - rej0 == 3, $"S4 unmapped/NaN/Infinity rejected ({L.RejectedCount - rej0})");

            // ---- Scenario 5: replacement policy ----
            ResetWorld();
            controls.EmitAt(GetPoint("d"), 45f, SoundCategory.Thrown, HunterTestControls.SourceRefresh);
            yield return Until(() => hunter.State == HunterState.Investigate, 2f);
            Vector3 dPos = GetPoint("d").position;
            Check(hunter.State == HunterState.Investigate && (hunter.GoalPosition - dPos).sqrMagnitude < 1e-6f, "S5 first sound becomes goal");
            controls.EmitAt(GetPoint("a"), 45f, SoundCategory.Footstep, 5001);           // lower priority than Thrown
            yield return new WaitForSeconds(1.2f);
            Check((hunter.GoalPosition - dPos).sqrMagnitude < 1e-6f, "S5 lower-priority sound does NOT replace the goal");
            controls.EmitAt(GetPoint("a"), 45f, SoundCategory.Gunshot, 5002);            // higher priority
            yield return Until(() => (hunter.GoalPosition - GetPoint("a").position).sqrMagnitude < 1e-6f, 2f);
            Check((hunter.GoalPosition - GetPoint("a").position).sqrMagnitude < 1e-6f, "S5 higher-priority Gunshot replaces the goal");
            ResetWorld();
            controls.EmitAt(GetPoint("d"), 45f, SoundCategory.Thrown, HunterTestControls.SourceRefresh);
            yield return Until(() => hunter.State == HunterState.Investigate, 2f);
            controls.EmitAt(GetPoint("c"), 45f, SoundCategory.Thrown, HunterTestControls.SourceRefresh);   // same source, newer position
            yield return Until(() => (hunter.GoalPosition - GetPoint("c").position).sqrMagnitude < 1e-6f, 2f);
            Check((hunter.GoalPosition - GetPoint("c").position).sqrMagnitude < 1e-6f && mem.Count == 1, "S5 same-source refresh updates goal position, no duplicate memory");

            // ---- Scenario 6: memory expires with no new events (brain disabled so it cannot consume) ----
            ResetWorld();
            hunter.enabled = false;
            controls.EmitAt(GetPoint("a"), 45f, SoundCategory.Gunshot, 6001);
            yield return null;
            Check(mem.Count == 1, "S6 sound remembered");
            yield return new WaitForSeconds(L.MemorySeconds + 0.5f);
            Check(mem.Count == 0, "S6 memory expired by listener update with no new sounds");
            hunter.enabled = true;

            // ---- Scenario 7: visible player triggers combat; cover prevents detection ----
            ResetWorld(new Vector3(10f, 0f, 0f), 90f);
            hunter.enabled = false;                                // freeze brain to probe vision alone
            controls.Teleport(4);                                  // behind cover wall at x=16
            yield return new WaitForSeconds(0.3f);
            bool seen = hunter.Vision.TryPerceive(out _);
            Check(!seen && hunter.Vision.LastResult == SightResult.Occluded, $"S7 cover blocks sight ({hunter.Vision.LastResult})");
            player.transform.GetComponent<CharacterController>().enabled = false;
            player.transform.position = new Vector3(14f, 1.1f, 0f);  // open floor, in front, before the cover
            player.transform.GetComponent<CharacterController>().enabled = true;
            yield return new WaitForSeconds(0.2f);
            seen = hunter.Vision.TryPerceive(out _);
            Check(seen && hunter.Vision.LastResult == SightResult.Seen, "S7 same range, no cover: visible");
            hunter.enabled = true;
            yield return Until(() => hunter.State == HunterState.Combat, 2f);
            Check(hunter.State == HunterState.Combat, "S7 visible player triggers Combat");
            float hp0 = player.Current;
            yield return Until(() => hunter.ShotsFired > 0, 4f);
            Check(hunter.ShotsFired > 0, "S7 hunter attacks only while perceiving (shots fired)");

            // ---- Scenario 8: loss of sight -> last seen position -> search -> patrol ----
            controls.Teleport(4);                                  // duck behind cover
            yield return Until(() => hunter.State == HunterState.Investigate && hunter.GoalIsLastSeen, 6f);
            Check(hunter.State == HunterState.Investigate && hunter.GoalIsLastSeen, "S8 lost sight -> investigates last SEEN position (not live player)");
            yield return Until(() => hunter.State == HunterState.Patrol, 40f);
            Check(hunter.State == HunterState.Patrol || hunter.State == HunterState.Combat, "S8 resolves to Patrol (or re-engages if seen again)");

            // ---- Scenario 9: unreachable goal -> bounded fallback ----
            ResetWorld();
            controls.Teleport(5);                                  // away from the platform room sight lines
            controls.EmitAt(GetPoint("platform"), 45f, SoundCategory.Thrown, HunterTestControls.SourcePlatform);
            yield return Until(() => hunter.FailedMoves > 0, 15f);
            Check(hunter.FailedMoves == 1, $"S9 platform sound reachable acoustically but not on foot: movement failed ({hunter.Movement.FailureReason})");
            yield return new WaitForSeconds(6f);
            Check(hunter.FailedMoves == 1 && mem.Count == 0, "S9 unreachable memory dropped, no retry loop");
            Check(hunter.InvestigateRequests <= 2, $"S9 bounded path requests for the unreachable goal ({hunter.InvestigateRequests})");

            // ---- Scenario 10: death stops everything ----
            ResetWorld();
            int active0 = SoundHunterListener.ActiveInstances;
            controls.EmitAt(GetPoint("a"), 45f, SoundCategory.Gunshot, 10001);
            yield return new WaitForSeconds(1f);
            hunter.GetComponent<Health>().TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null));
            yield return null;
            Vector3 deadPos = hunter.transform.position;
            int shots = hunter.ShotsFired;
            controls.EmitAt(GetPoint("a"), 45f, SoundCategory.Gunshot, 10002);
            controls.Teleport(3);
            yield return new WaitForSeconds(2.5f);
            Check(hunter.State == HunterState.Dead, "S10 state Dead");
            Check(!L.Listening && mem.Count == 0 && SoundHunterListener.ActiveInstances == active0 - 1, "S10 listener unsubscribed and memory cleared");
            Check((hunter.transform.position - deadPos).sqrMagnitude < 0.01f, "S10 no movement after death");
            Check(hunter.ShotsFired == shots && player.Current >= 100f - 0.01f, "S10 no attacks after death");

            // ---- Scenario 11: restart (re-enable + real scene reload) ----
            ResetWorld();
            Check(SoundHunterListener.ActiveInstances == 1 && mem.Count == 0 && !hunter.GetComponent<Health>().IsDead && hunter.State == HunterState.Patrol,
                "S11a re-enable: one listener, empty memory, alive, Patrol");
            controls.EmitAt(GetPoint("a"), 45f, SoundCategory.Gunshot, 11001);
            yield return new WaitForSeconds(0.5f);
            phase = 1;
#if UNITY_EDITOR
            // Editor play mode: reload without needing the scene in Build Settings.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
        }

        private IEnumerator Phase2()
        {
            // After a real scene reload.
            yield return new WaitForSeconds(0.5f);
            Check(SoundHunterListener.ActiveInstances == 1, $"S11b after scene reload exactly one active listener ({SoundHunterListener.ActiveInstances})");
            Check(hunter.Listener.Memory.Count == 0 && hunter.State == HunterState.Patrol, "S11b no stale memory or state after reload");
            Summary();
        }

        private void Summary()
        {
            Debug.Log($"[Perera.Scenario] SUMMARY {passed} passed, {failed} failed");
            phase = 0;
            Time.timeScale = 1f;
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(failed == 0 ? 0 : 1);
#endif
        }

        private Transform GetPoint(string key) => controls.Anchor(key);
    }
}
