using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FacilityBreach.AI.SoundHunter;
using FacilityBreach.AI.Roles;
using FacilityBreach.Combat;
using FacilityBreach.Core;
using FacilityBreach.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FacilityBreach.Editor
{
    public static class EnemyRuleValidation
    {
        private static readonly Stack<IEnumerator> pending = new Stack<IEnumerator>();
        private static readonly List<string> errors = new List<string>();
        private static readonly List<string> evidence = new List<string>();
        private static double deadline;
        private static int checks;
        public static void Run()
        {
            File.Delete("enemy-rules-passed.txt"); File.Delete("enemy-rules-failed.txt");
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
            SessionState.SetBool("FBEnemyRules", true); Register(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool("FBEnemyRules", false)) Register(); }
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
            if (state == PlayModeStateChange.EnteredEditMode && File.Exists("enemy-rules-passed.txt"))
            { SessionState.SetBool("FBEnemyRules", false); EditorApplication.Exit(0); }
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
        private static IEnumerator WaitReal(float duration)
        { double end=EditorApplication.timeSinceStartup+duration;while(EditorApplication.timeSinceStartup<end)yield return null; }
        private static void Isolate(EnemyCombatAdapter[] enemies)
        {
            foreach(var enemy in enemies)
            {
                foreach(var brain in enemy.GetComponents<MonoBehaviour>())
                    if(brain is RoleDecisionBrain || brain is FacilityBreachSoundHunterBrain) brain.enabled=false;
                enemy.CancelPendingAttack(); enemy.GetComponent<SoundHunterNavMeshMover>()?.Stop();
            }
        }
        private static void PositionVisiblePlayer(FacilityBreachRuntime run, EnemyCombatAdapter enemy, float radius)
        {
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8; Vector3 desired=enemy.transform.position+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius;
                if(!UnityEngine.AI.NavMesh.SamplePosition(desired,out var hit,.6f,UnityEngine.AI.NavMesh.AllAreas))continue;
                Warp(run,new Vector3(hit.position.x,.1f,hit.position.z),enemy.transform.position+Vector3.up);
                Vector3 facing=run.PlayerTransform.position-enemy.transform.position;facing.y=0;enemy.transform.rotation=Quaternion.LookRotation(facing);Physics.SyncTransforms();
                if(AgentPerception.Visible(enemy.transform,run.PlayerTransform,13,out _,110))return;
            }
            throw new Exception("No clear test pose near "+enemy.Identity);
        }
        private static IEnumerator Checks()
        {
            for(int i=0;i<20;i++) yield return null;
            var run=UnityEngine.Object.FindAnyObjectByType<FacilityBreachRuntime>();run.DevelopmentDiagnostics=true;run.StartMission();
            Check(run.Resources.Health==100&&run.Run.Remaining==300,"mission starts at 100 HP and five-minute countdown");
            var enemies=UnityEngine.Object.FindObjectsByType<EnemyCombatAdapter>();
            Check(enemies.Length==4&&enemies.All(x=>x.GetComponents<EnemyCombatAdapter>().Length==1),"one shared attack component per robot");
            Check(enemies.All(x=>x.GetComponents<RoleDecisionBrain>().Length+x.GetComponents<FacilityBreachSoundHunterBrain>().Length==1),"exactly one distinct decision brain per robot");
            Check(enemies.Single(x=>x.Identity=="PG-01").Range==12&&enemies.Single(x=>x.Identity=="SH-01").Range==14,"PG and SH fire ranges are 12 and 14");
            Check(enemies.All(x=>Mathf.Approximately(x.MissChance,.4f)&&x.Magazine==50),"initial miss probability forty percent and magazine fifty");
            Check(UnityEngine.Object.FindObjectsByType<LineRenderer>().Length==0,"no runtime debug line renderers");
            foreach(var enemy in enemies)
            {
                var light=enemy.GetComponentsInChildren<Renderer>().Single(x=>x.name=="StateLight");
                Check(light.bounds.size.magnitude<.3f&&light.GetComponents<Collider>().Length==0,enemy.Identity+" chest light is small and non-collidable");
            }
            Isolate(enemies);var pg=enemies.Single(x=>x.Identity=="PG-01");var guard=pg.GetComponent<PatrolGuardBrain>();
            pg.transform.rotation=Quaternion.identity;
            Vector3 point=pg.transform.position+Vector3.back*4;
            Warp(run,new Vector3(point.x,.1f,point.z),pg.transform.position+Vector3.up);
            Check(!AgentPerception.Visible(pg.transform,run.PlayerTransform,13,out _,110),"PG rejects player behind its 110-degree FOV");
            point=pg.transform.position+Vector3.forward*4;
            Warp(run,new Vector3(point.x,.1f,point.z),pg.transform.position+Vector3.up);pg.MissChance=0;guard.enabled=true;
            yield return Wait(.25f);Check(!guard.Detected&&run.Resources.Health==100,"continuous sight below half second cannot detect/fire");
            yield return Wait(.4f);Check(guard.Detected&&pg.Warning&&run.Resources.Health==100,"PG detects only after half second and starts warning");
            Check(pg.GetComponentsInChildren<Renderer>().Single(x=>x.name=="StateLight").sharedMaterial.color==Color.white,"chest warning turns white before shot resolves");
            yield return Wait(.45f);
            Check(run.Resources.Health==90&&pg.SuccessfulHits==1&&pg.Magazine==49,"one successful resolved Ammo1 hit changes 100 to 90 once");
            Vector3 guardMemory=guard.LastKnownPosition;
            Warp(run,new Vector3(-12.5f,.1f,-28),Vector3.zero);yield return Wait(.2f);
            Check(guard.State==RoleState.Investigate&&guard.LastKnownPosition==guardMemory,"PG investigates last seen position when sight is lost");
            bool searched=false;float returnDeadline=Time.time+7;
            while(Time.time<returnDeadline&&guard.State!=RoleState.Patrol) { searched|=guard.State==RoleState.Search;yield return null; }
            Check(searched&&guard.State==RoleState.Patrol&&guard.LastKnownPosition==guardMemory,"PG searches four seconds and returns to corridor patrol without hidden tracking");
            guard.enabled=false;PositionVisiblePlayer(run,pg,4);
            yield return Wait(.3f);pg.MissChance=1;int shots=pg.ResolvedShots;
            Check(pg.TryAttack(run.PlayerTransform)&&!pg.TryAttack(run.PlayerTransform),"only one pending shot can be queued");yield return Wait(.55f);
            Check(run.Resources.Health==90&&pg.Magazine==48&&pg.ResolvedShots==shots+1,"miss consumes one round and applies no damage");yield return Wait(.3f);
            pg.MissChance=0;Check(pg.TryAttack(run.PlayerTransform),"shot schedules before new solid obstruction");
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Enemy validation cover";
            blocker.transform.position=Vector3.Lerp(pg.transform.position+Vector3.up*1.4f,run.PlayerTransform.position+Vector3.up,.5f);blocker.transform.localScale=Vector3.one;
            Physics.SyncTransforms();yield return Wait(.5f);
            Check(run.Resources.Health==90&&pg.Magazine==48,"LOS recheck blocks shot behind newly inserted solid cover");UnityEngine.Object.Destroy(blocker);yield return Wait(.1f);
            Check(pg.TryAttack(run.PlayerTransform),"pre-pause warning starts");run.SetPause(true);
            Check(!pg.Warning,"pause immediately cancels pending attack");float clock=run.GameplayTime;
            double until=EditorApplication.timeSinceStartup+.2;while(EditorApplication.timeSinceStartup<until)yield return null;
            Check(run.GameplayTime==clock,"pause freezes enemy and mission gameplay clock");run.SetPause(false);yield return Wait(.7f);
            Check(run.Resources.Health==90&&pg.Magazine==48,"resume never resolves stale pre-pause shot");
            Vector3 originalPost=pg.transform.position;
            var door=GameObject.Find("Door_Room3_Left").GetComponentInChildren<InteractiveDoor>();
            var panel=door.GetComponentsInChildren<Collider>().Where(x=>!x.isTrigger).OrderByDescending(x=>x.bounds.size.sqrMagnitude).First();
            Vector3 normal=panel.bounds.size.x<panel.bounds.size.z?Vector3.right:Vector3.forward;
            var nav=pg.GetComponent<UnityEngine.AI.NavMeshAgent>();nav.enabled=false;
            pg.transform.position=new Vector3(panel.bounds.center.x,.1f,panel.bounds.center.z)-normal*2;
            point=new Vector3(panel.bounds.center.x,.1f,panel.bounds.center.z)+normal*2;
            Warp(run,point,pg.transform.position+Vector3.up);Physics.SyncTransforms();
            Check(!door.IsOpen&&Physics.RaycastAll(pg.transform.position+Vector3.up*1.4f,normal,4,~0,QueryTriggerInteraction.Ignore).Any(x=>x.transform.IsChildOf(door.transform)),"actual closed Machinery door intersects attack ray");
            Check(!pg.TryAttack(run.PlayerTransform)&&run.Resources.Health==90,"closed authored door blocks enemy attack");
            pg.transform.position=originalPost;nav.enabled=true;nav.Warp(originalPost);
            PositionVisiblePlayer(run,pg,4);
            Check(pg.TryAttack(run.PlayerTransform),"attack starts before enemy defeat");
            pg.TakeDamage(new Perera.Core.DamageInfo(60,pg.transform.position,Vector3.forward,run.PlayerTransform.gameObject));yield return null;
            Check(pg.IsDead&&!pg.Warning&&!pg.GetComponent<Collider>().enabled&&guard.State==RoleState.Inactive,"defeat cancels attack, collider and sensing immediately");
            Check(!pg.TryAttack(run.PlayerTransform)&&!pg.GetComponent<SoundHunterNavMeshMover>().RequestMove(point,.5f),"inactive enemy cannot attack or request movement");
            var lightOff=pg.GetComponentsInChildren<Renderer>().Single(x=>x.name=="StateLight");Check(!lightOff.enabled,"defeat switches off chest light");
            run.Restart();for(int i=0;i<25;i++)yield return null;run=UnityEngine.Object.FindAnyObjectByType<FacilityBreachRuntime>();run.StartMission();
            enemies=UnityEngine.Object.FindObjectsByType<EnemyCombatAdapter>();Isolate(enemies);
            var cd=enemies.Single(x=>x.Identity=="CD-03");var defender=cd.GetComponent<CoverDefenderBrain>();
            cd.transform.rotation=Quaternion.identity;point=cd.transform.position+Vector3.forward*3;
            PositionVisiblePlayer(run,cd,3);run.DevelopmentDiagnostics=true;cd.GetComponent<UnityEngine.AI.NavMeshAgent>().speed=0;cd.GetComponent<UnityEngine.AI.NavMeshAgent>().updateRotation=false;defender.enabled=true;cd.MissChance=1;
            yield return Wait(.3f);Check(defender.CoverEvaluations==1&&Vector3.Distance(defender.LastKnownPosition,run.PlayerTransform.position)<.15f,"CD activates visually and evaluates reachable cover from perceived position");
            yield return Wait(1);Check(defender.CoverEvaluations==1,"CD does not reassess before 1.5-second cadence");
            yield return Wait(.5f);Check(defender.CoverEvaluations==2,"CD reassesses reachable cover after 1.5 seconds of valid sight");
            cd.GetComponent<UnityEngine.AI.NavMeshAgent>().speed=3;cd.GetComponent<UnityEngine.AI.NavMeshAgent>().updateRotation=true;
            Vector3 known=defender.LastKnownPosition;int evaluations=defender.CoverEvaluations;
            Warp(run,new Vector3(-12.5f,.1f,-28),Vector3.zero);yield return Wait(.7f);
            Check(defender.LastKnownPosition==known&&defender.CoverEvaluations==evaluations&&defender.State==RoleState.Search,"CD holds remembered position without hidden-player tracking or reassessment");
            yield return Wait(3.1f);Check(defender.State==RoleState.Return||defender.State==RoleState.Patrol,"CD returns after three-second lost-sight hold");defender.enabled=false;
            var fl=enemies.Single(x=>x.Identity=="FL-04");var flanker=fl.GetComponent<FlankerBrain>();
            fl.transform.rotation=Quaternion.identity;point=fl.transform.position+Vector3.forward*2.5f;
            PositionVisiblePlayer(run,fl,2.5f);fl.GetComponent<UnityEngine.AI.NavMeshAgent>().speed=0;fl.GetComponent<UnityEngine.AI.NavMeshAgent>().updateRotation=false;flanker.enabled=true;fl.MissChance=1;
            yield return Wait(.3f);Check(flanker.Plans==1&&flanker.FlankAngle>=50&&flanker.FlankRadius>=3&&flanker.FlankRadius<=9,"FL chooses reachable three-to-nine-unit flank at least fifty degrees off chase");
            PositionVisiblePlayer(run,fl,7);yield return Wait(.6f);
            Check(flanker.Plans==1,"FL cannot replan before two seconds despite newly visible movement");
            yield return Wait(1.5f);Check(flanker.Plans==2,"FL replans after two seconds and over-four-unit newly perceived movement");
            fl.GetComponent<UnityEngine.AI.NavMeshAgent>().speed=3;fl.GetComponent<UnityEngine.AI.NavMeshAgent>().updateRotation=true;
            int plans=flanker.Plans;known=flanker.LastKnownPosition;
            Warp(run,new Vector3(-12.5f,.1f,-28),Vector3.zero);yield return Wait(.7f);
            Check(flanker.Plans==plans&&flanker.LastKnownPosition==known&&!fl.Warning,"FL does not replan or shoot toward hidden live player");
            yield return Wait(3.1f);Check(flanker.State==RoleState.Search,"FL searches after three seconds without sight");flanker.enabled=false;
            run.Restart();for(int i=0;i<25;i++)yield return null;run=UnityEngine.Object.FindAnyObjectByType<FacilityBreachRuntime>();run.StartMission();
            enemies=UnityEngine.Object.FindObjectsByType<EnemyCombatAdapter>();Isolate(enemies);pg=enemies.Single(x=>x.Identity=="PG-01");pg.MissChance=1;
            pg.transform.rotation=Quaternion.identity;point=pg.transform.position+Vector3.forward*4;Warp(run,new Vector3(point.x,.1f,point.z),pg.transform.position+Vector3.up);
            Time.timeScale=8;
            for(int shot=0;shot<50;shot++) { Check(pg.TryAttack(run.PlayerTransform),"bounded enemy magazine shot "+(shot+1));yield return Wait(.5f);yield return Wait(.25f); }
            Check(pg.Magazine==0&&pg.ReloadRemaining>0&&run.Resources.Health==100,"fifty missed shots consume magazine and enter reload without damage");
            Check(!pg.TryAttack(run.PlayerTransform),"cannot fire during enemy reload");yield return Wait(1.6f);Time.timeScale=1;
            Check(pg.Magazine==50,"enemy reload restores fifty from reserve");
            yield return Wait(.3f);pg.MissChance=0;Check(pg.TryAttack(run.PlayerTransform),"attack queued before terminal loss");
            run.PlayerTransform.GetComponent<PlayerDamageAdapter>().TakeDamage(new Perera.Core.DamageInfo(100,point,Vector3.forward,pg.gameObject));
            Check(run.Run.State==MissionState.Lost&&!pg.Warning,"player death immediately loses and cancels every pending attack");yield return WaitReal(.3f);
            Check(run.Resources.Health==0&&!pg.TryAttack(run.PlayerTransform),"terminal result rejects further enemy damage/attacks");
            run.Restart();for(int i=0;i<25;i++)yield return null;run=UnityEngine.Object.FindAnyObjectByType<FacilityBreachRuntime>();run.StartMission();
            enemies=UnityEngine.Object.FindObjectsByType<EnemyCombatAdapter>();Isolate(enemies);
            var stations=UnityEngine.Object.FindObjectsByType<ResourceStation>();
            var card=stations.Single(x=>x.Kind==StationKind.Card);Approach(run,card);Check(run.TryInteract()&&run.Run.HasCard,"card collected without any enemy kills");yield return null;
            Check(run.GetComponentsInChildren<Collider>().Where(x=>x.name.StartsWith("Card required")).All(x=>!x.gameObject.activeSelf),"card unlocks all three Machinery entrances");
            var device=stations.Single(x=>x.Kind==StationKind.Device);Approach(run,device);Check(run.TryInteract()&&run.Run.State==MissionState.Deactivating,"device connects with every enemy alive");
            double remaining=run.Run.Remaining;Time.timeScale=8;yield return Wait(5.2f);Time.timeScale=1;
            Check(run.Run.State==MissionState.Escape&&run.Run.Remaining<remaining&&run.Resources.Health==100,"shutdown counts down and enters alive escape with no kills");remaining=run.Run.Remaining;yield return Wait(.4f);
            Check(run.AcceptsGameplay&&run.Run.Remaining==remaining&&enemies.All(x=>!x.IsDead),"escape freezes countdown and keeps living enemy lifecycle active");
            pg=enemies.Single(x=>x.Identity=="PG-01");pg.MissChance=1;PositionVisiblePlayer(run,pg,4);
            Check(pg.TryAttack(run.PlayerTransform),"living enemy can queue an attack during unlimited escape");
            var exit=GameObject.Find("Door_Main_Exit").transform.position-Vector3.forward*1.75f;
            Warp(run,new Vector3(exit.x,.1f,exit.z),exit+Vector3.forward);yield return WaitReal(.2f);
            Check(run.Run.State==MissionState.Won&&enemies.All(x=>!x.IsDead)&&!pg.Warning,"unlocked exit wins with every robot alive");
            Check(!run.PlayerTransform.GetComponent<PlayerDamageAdapter>().TakeDamage(new Perera.Core.DamageInfo(10,exit,Vector3.forward,enemies[0].gameObject)),"Won rejects player damage");
            var tie=new MissionRun();tie.Start();tie.CollectCard();tie.Tick(295);tie.Connect(true);tie.Tick(5);Check(tie.State==MissionState.Lost,"timeout takes precedence over simultaneous shutdown completion");
            var roster=new EnemyRoster();var a=roster.Register("PG-01",60);roster.Register("SH-01",80);roster.Register("CD-03",90);roster.Register("FL-04",70);
            roster.Damage("PG-01",60,0);roster.Damage("SH-01",80,29);roster.Damage("CD-03",90,29);roster.Damage("FL-04",70,30);
            Check(!roster.ChallengeComplete&&a.Life==EnemyLife.AwaitingSafeSpawn,"expired revival timer processed before permanent-defeat test");
            Check(errors.Count==0,"no unexpected Console errors: "+string.Join("; ",errors));
            File.WriteAllText("enemy-rules-passed.txt",checks+" checks\n"+string.Join("\n",evidence));pending.Clear();EditorApplication.isPlaying=false;
        }
        private static void Fail(Exception exception)
        {
            SessionState.SetBool("FBEnemyRules", false); EditorApplication.update -= Step;
            File.WriteAllText("enemy-rules-failed.txt", exception + "\n" + string.Join("\n", evidence) + "\nErrors: " + string.Join("; ", errors));
            EditorApplication.Exit(1);
        }
    }
}
