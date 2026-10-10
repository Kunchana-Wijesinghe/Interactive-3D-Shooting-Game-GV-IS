using System.Collections.Generic;
using FacilityBreach.Combat;
using Perera.Core;
using Perera.SoundHunter;
using UnityEngine;
namespace FacilityBreach.AI.SoundHunter
{
    public enum SoundHunterState { Patrol, Investigate, Search, Combat, Inactive }
    [RequireComponent(typeof(EnemyCombatAdapter))]
    public sealed class FacilityBreachSoundHunterBrain : MonoBehaviour,IEnemyActivitySink
    {
        public SoundHunterState CurrentState {get;private set;}=SoundHunterState.Patrol;
        public float SightRange=16,MemoryExpirySeconds=10,SearchDuration=4;
        public Vector3 InvestigationTarget {get;private set;}
        public bool HasSoundTarget=>soundTarget;
        public int HeardCount {get;private set;}
        public int MemoryCount=>memory?.Count??0;
        private EnemyCombatAdapter combat;
        private IHunterMovement mover;
        private FacilityBreachHearingGraphSetup graph;
        private FacilityBreachRuntime runtime;
        private SoundMemory memory;
        private SoundRanker ranker;
        private bool active=true,soundTarget,lastSeenTarget;
        private SoundMemory.Entry entry;
        private float searchEnd,nextDecision;
        private readonly List<Vector3> patrol=new List<Vector3>();
        private int patrolIndex;
        private void Awake()
        {
            combat=GetComponent<EnemyCombatAdapter>();
            mover=GetComponent<IHunterMovement>();if(mover==null)mover=gameObject.AddComponent<SoundHunterNavMeshMover>();
            graph=GetComponent<FacilityBreachHearingGraphSetup>();if(!graph)graph=gameObject.AddComponent<FacilityBreachHearingGraphSetup>();
            ranker=new SoundRanker();memory=new SoundMemory(ranker,MemoryExpirySeconds,4);
        }
        private void Start()
        {
            runtime=FindAnyObjectByType<FacilityBreachRuntime>();graph.BuildGraph();
            var exit=GameObject.Find("Door_Main_Exit");
            if(exit){patrol.Add(exit.transform.position+new Vector3(-3,0,-2));patrol.Add(exit.transform.position+new Vector3(-6,0,-2));}
        }
        private void OnEnable(){SoundEventBus.Emitted-=OnSoundEmitted;SoundEventBus.Emitted+=OnSoundEmitted;}
        private void OnDisable(){SoundEventBus.Emitted-=OnSoundEmitted;}
        public void OnSoundEmitted(SoundStimulus stimulus)
        {
            if(!active||!runtime||!runtime.AcceptsGameplay||combat.IsDead||stimulus.SourceId==combat.GetEntityId().GetHashCode())return;
            if(stimulus.Category!=SoundCategory.Gunshot&&stimulus.Category!=SoundCategory.Thrown)return;
            if(!float.IsFinite(stimulus.Strength)||!float.IsFinite(stimulus.CreatedTime)||!float.IsFinite(stimulus.Position.x)||!float.IsFinite(stimulus.Position.y)||!float.IsFinite(stimulus.Position.z))return;
            if(!graph.TryMapPoint(stimulus.Position,out int source)||!graph.TryMapPoint(transform.position,out int listener))return;
            if(new HearingEvaluator(graph.Graph).TryHear(stimulus,source,listener,out var heard))
            {memory.Remember(heard,runtime.GameplayTime);HeardCount++;GameplayDiagnostics.Trace($"SH heard origin={stimulus.Position} cost={heard.Cost} remaining={heard.Remaining}",this);}
        }
        private void Update()
        {
            if(!runtime||!runtime.AcceptsGameplay||!active||combat.IsDead)return;
            float now=runtime.GameplayTime;memory.Prune(now);
            if(now<nextDecision)return;nextDecision=now+.1f;
            bool visible=AgentPerception.Visible(transform,runtime.PlayerTransform,SightRange,out var seen);
            if(visible)
            {
                InvestigationTarget=seen;lastSeenTarget=true;soundTarget=false;SetState(SoundHunterState.Combat);
                mover.RequestMove(seen,6);combat.TryAttack(runtime.PlayerTransform);return;
            }
            if(CurrentState==SoundHunterState.Combat){mover.Stop();SetState(SoundHunterState.Investigate);}
            if(!lastSeenTarget&&memory.TryGetBest(now,out var best)&&(!soundTarget||best.Revision!=entry.Revision))
            {entry=best;soundTarget=true;InvestigationTarget=best.Position;mover.Stop();SetState(SoundHunterState.Investigate);}
            if(CurrentState==SoundHunterState.Investigate)
            {
                if(soundTarget&&!memory.Contains(entry.Revision)){soundTarget=false;mover.Stop();SetState(SoundHunterState.Patrol);}
                else if(mover.Status==MoveStatus.Arrived||mover.Status==MoveStatus.Failed)
                {mover.Stop();lastSeenTarget=false;SetState(SoundHunterState.Search);searchEnd=now+SearchDuration;}
                else mover.RequestMove(InvestigationTarget,.6f);
            }
            if(CurrentState==SoundHunterState.Search)
            {
                transform.Rotate(0,9,0);
                if(now>=searchEnd){if(soundTarget)memory.Forget(entry);soundTarget=false;SetState(SoundHunterState.Patrol);}
            }
            if(CurrentState==SoundHunterState.Patrol&&patrol.Count>0)
            {
                if(mover.Status==MoveStatus.Arrived||mover.Status==MoveStatus.Failed){patrolIndex=(patrolIndex+1)%patrol.Count;mover.Stop();}
                mover.RequestMove(patrol[patrolIndex],.5f);
            }
        }
        private void SetState(SoundHunterState state)
        {if(CurrentState==state)return;CurrentState=state;GameplayDiagnostics.Trace($"SH state={state} target={InvestigationTarget}",this);}
        public void SetEnemyActive(bool value)
        {active=value;if(!value){mover.Stop();memory.Clear();soundTarget=lastSeenTarget=false;SetState(SoundHunterState.Inactive);}}
        public void ResetEnemyActions(){memory.Clear();mover.Stop();soundTarget=lastSeenTarget=false;nextDecision=0;patrolIndex=0;SetState(SoundHunterState.Patrol);}
    }
}
