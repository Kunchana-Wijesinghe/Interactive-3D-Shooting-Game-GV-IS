using Perera.SoundHunter;
using UnityEngine;
using UnityEngine.AI;
namespace FacilityBreach.AI.SoundHunter
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SoundHunterNavMeshMover : MonoBehaviour, IHunterMovement
    {
        private NavMeshAgent agent;
        private FacilityBreachRuntime runtime;
        private Vector3 goal, windowPosition;
        private float stoppingDistance, nextRequest, started, windowTime;
        private bool paused;
        public MoveStatus Status { get; private set; }
        public string FailureReason { get; private set; } = "";
        public int PathRequests { get; private set; }
        public Vector3 Goal => goal;
        public bool OnNavigation => agent && agent.isOnNavMesh;
        private void Awake()
        {
            runtime = FindAnyObjectByType<FacilityBreachRuntime>();
            agent = GetComponent<NavMeshAgent>(); agent.speed = 3; agent.acceleration = 12;
            agent.height = 1.8f; agent.radius = .35f; agent.baseOffset = 0;
            agent.autoRepath = true;
        }
        public bool RequestMove(Vector3 target, float stop)
        {
            if (!runtime || !runtime.AcceptsGameplay) return false;
            float now = runtime.GameplayTime;
            bool same = (goal - target).sqrMagnitude < .16f && Mathf.Abs(stoppingDistance - stop) < .05f;
            if (same && (Status == MoveStatus.Moving || Status == MoveStatus.Arrived)) return true;
            if (now < nextRequest) return false;
            nextRequest = now + .5f; goal = target; stoppingDistance = stop;
            if (!OnNavigation || !NavMesh.SamplePosition(target, out var hit, 1, NavMesh.AllAreas)) return Fail("Agent/goal outside navigation");
            var path = new NavMeshPath(); PathRequests++;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return Fail("No complete path");
            agent.stoppingDistance = Mathf.Max(.2f, stop); agent.isStopped = false;
            if (!agent.SetPath(path)) return Fail("SetPath rejected");
            Status = MoveStatus.Moving; FailureReason = ""; started = windowTime = now; windowPosition = transform.position;
            GameplayDiagnostics.Trace($"nav {name} goal={target} onMesh={OnNavigation} status={path.status} requests={PathRequests}", this);
            return true;
        }
        private bool Fail(string reason)
        {
            Status = MoveStatus.Failed; FailureReason = reason;
            if (OnNavigation) { agent.isStopped = true; agent.ResetPath(); }
            nextRequest = runtime ? runtime.GameplayTime + 2 : 2;
            GameplayDiagnostics.Trace($"nav {name} failure={reason}", this); return false;
        }
        public void Stop()
        { Status = MoveStatus.Idle; if (OnNavigation) { agent.isStopped = true; agent.ResetPath(); } }
        private void Update()
        {
            if (!runtime || !OnNavigation) return;
            if (!runtime.AcceptsGameplay)
            { if (!paused) { paused = true; agent.isStopped = true; } return; }
            if (paused) { paused = false; agent.isStopped = Status != MoveStatus.Moving; }
            if (Status != MoveStatus.Moving) return;
            if (!agent.pathPending && agent.remainingDistance <= stoppingDistance + .1f) { Stop(); Status = MoveStatus.Arrived; return; }
            if (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete) { Fail("Path invalidated by obstruction"); return; }
            float now = runtime.GameplayTime;
            if (now - started > 45) { Fail("Travel timeout"); return; }
            if (now - windowTime > 4)
            {
                if ((transform.position - windowPosition).sqrMagnitude < .01f) { Fail("Blocked/no movement progress"); return; }
                windowPosition = transform.position; windowTime = now;
            }
            Vector3 towardGoal = goal - transform.position;
            foreach (var hit in Physics.OverlapSphere(transform.position + Vector3.up, 1.8f))
            {
                var door = hit.GetComponentInParent<InteractiveDoor>();
                if (door && !door.IsOpen && Vector3.Dot(door.transform.position - transform.position, towardGoal) > 0) door.RequestOpen(true);
            }
        }
    }
}
