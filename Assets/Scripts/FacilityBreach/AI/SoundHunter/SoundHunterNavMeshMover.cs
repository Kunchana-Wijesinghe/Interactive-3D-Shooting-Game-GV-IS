using Perera.SoundHunter;
using FacilityBreach.Combat;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
namespace FacilityBreach.AI.SoundHunter
{
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class SoundHunterNavMeshMover : MonoBehaviour, IHunterMovement, IEnemyActivitySink
    {
        private NavMeshAgent agent;
        private FacilityBreachRuntime runtime;
        private Vector3 goal, windowPosition;
        private float stoppingDistance, nextRequest, started, windowTime;
        private bool paused, active = true;
        private EnemyCombatAdapter combat;
        private Vector3 facingPoint;
        private float facingUntil, scanUntil, scanSpeed;
        private bool segmentStarted;
        public readonly AgentRoute Route = new AgentRoute();
        public float TurnDegreesPerSecond = 240;
        public Vector3 Velocity => OnNavigation && !paused && active && Status == MoveStatus.Moving ? agent.velocity : Vector3.zero;
        public MoveStatus Status { get; private set; }
        public string FailureReason { get; private set; } = "";
        public int PathRequests { get; private set; }
        public Vector3 Goal => goal;
        public bool OnNavigation => agent && agent.isOnNavMesh;
        private void Awake()
        {
            runtime = FindAnyObjectByType<FacilityBreachRuntime>();
            combat = GetComponent<EnemyCombatAdapter>();
            agent = GetComponent<NavMeshAgent>(); agent.speed = 3; agent.acceleration = 12;
            agent.height = 1.8f; agent.radius = .35f; agent.baseOffset = 0;
            agent.autoRepath = true;
            agent.updateRotation = false; // This controller is the sole runtime root rotation writer.
        }
        public bool RequestMove(Vector3 target, float stop)
        {
            if (!active || !runtime || !runtime.AcceptsGameplay || (combat && combat.IsDead)) return false;
            if (!AgentRoute.Finite(target) || !float.IsFinite(stop) || stop < 0) return RequestRoute(new[] { target }, stop);
            float now = runtime.GameplayTime;
            bool same = Route.Count == 1 && (goal - target).sqrMagnitude < .16f && Mathf.Abs(stoppingDistance - Mathf.Max(.2f, stop)) < .05f;
            if (same && (Status == MoveStatus.Moving || Status == MoveStatus.Arrived)) return true;
            if (now < nextRequest) return false;
            return RequestRoute(new[] { target }, stop);
        }
        /// <summary>Replaces the previous route. Points are copied; empty/null routes stop and arrive.
        /// Route.Ended reports revision, arrival, interruption or failure. AI may also poll Status.</summary>
        public bool RequestRoute(IReadOnlyList<Vector3> points, float stop = .5f)
        {
            if (!active || !runtime || !runtime.AcceptsGameplay || (combat && combat.IsDead)) return false;
            StopNavigation(); segmentStarted = false; ClearFacing();
            if (!float.IsFinite(stop) || stop < 0) { Route.Reject("Invalid stopping distance"); return Fail(Route.Reason); }
            if (!Route.Replace(points)) return Fail(Route.Reason);
            stoppingDistance = Mathf.Max(.2f, stop);
            FailureReason = "";
            if (Route.State == RouteState.Arrived) { Status = MoveStatus.Arrived; return true; }
            return StartSegment();
        }
        private bool StartSegment()
        {
            goal = Route.Current;
            float now = runtime.GameplayTime;
            nextRequest = now + .5f;
            if (!OnNavigation || !NavMesh.SamplePosition(goal, out var hit, 1, NavMesh.AllAreas)) return Fail("Agent/goal outside navigation");
            var path = new NavMeshPath(); PathRequests++;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return Fail("No complete path");
            agent.stoppingDistance = SegmentStop; agent.autoBraking = Route.Index == Route.Count - 1; agent.isStopped = false;
            if (!agent.SetPath(path)) return Fail("SetPath rejected");
            segmentStarted = true; Status = MoveStatus.Moving; FailureReason = ""; started = windowTime = now; windowPosition = transform.position;
            GameplayDiagnostics.Trace($"nav {name} goal={goal} onMesh={OnNavigation} status={path.status} requests={PathRequests}", this);
            return true;
        }
        private float SegmentStop => Route.Index == Route.Count - 1 ? stoppingDistance : Mathf.Min(.3f, stoppingDistance);
        private bool Fail(string reason)
        {
            Status = MoveStatus.Failed; FailureReason = reason;
            Route.Fail(reason); segmentStarted = false;
            StopNavigation();
            nextRequest = runtime ? runtime.GameplayTime + 2 : 2;
            GameplayDiagnostics.Trace($"nav {name} failure={reason}", this); return false;
        }
        public void Stop()
        { Route.Interrupt("Stopped"); Status = MoveStatus.Idle; segmentStarted = false; FailureReason = ""; ClearFacing(); StopNavigation(); }
        private void StopNavigation() { if (OnNavigation) { agent.ResetPath(); agent.isStopped = true; } }
        public void ResetMovement() { Stop(); Route.Reset(); goal = transform.position; nextRequest = started = windowTime = 0; paused = false; }
        public void FacePoint(Vector3 point) { if (!runtime || !AgentRoute.Finite(point)) return; facingPoint = point; facingUntil = runtime.GameplayTime + .25f; scanUntil = 0; }
        public void Scan(float degreesPerSecond = 90) { if (!runtime) return; scanSpeed = degreesPerSecond; scanUntil = runtime.GameplayTime + .25f; facingUntil = 0; }
        private void ClearFacing() { facingUntil = scanUntil = 0; }
        private void OnDisable() { Stop(); }
        public void SetEnemyActive(bool value) { active = value; if (!value) Stop(); }
        public void ResetEnemyActions() { ResetMovement(); }
        private void Update()
        {
            if (!runtime) return;
            if (!active || (combat && combat.IsDead) || !runtime.AcceptsGameplay)
            { if (!paused) { paused = true; if (OnNavigation) agent.isStopped = true; } return; }
            if (!OnNavigation) { if (Status == MoveStatus.Moving) Fail("Agent left navigation"); return; }
            if (paused) { paused = false; agent.isStopped = Status != MoveStatus.Moving; }
            if (Status != MoveStatus.Moving) return;
            if (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete) { Fail("Path invalidated by obstruction"); return; }
            if (segmentStarted && !agent.pathPending && agent.remainingDistance <= SegmentStop + .1f &&
                Vector3.Distance(transform.position, agent.destination) <= SegmentStop + .2f)
            {
                segmentStarted = false;
                if (Route.Advance()) StartSegment(); else { StopNavigation(); Status = MoveStatus.Arrived; }
                return;
            }
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
                if (door && !door.PlayerOnly && !door.IsOpen && Vector3.Dot(door.transform.position - transform.position, towardGoal) > 0) door.RequestOpen(true);
            }
        }
        private void LateUpdate()
        {
            if (!runtime || !active || paused || !runtime.AcceptsGameplay || (combat && combat.IsDead)) return;
            float now = runtime.GameplayTime;
            Vector3 direction = Vector3.zero;
            if (now < facingUntil) direction = facingPoint - transform.position;
            else if (Status == MoveStatus.Moving && OnNavigation) direction = agent.desiredVelocity;
            else if (now < scanUntil) { transform.Rotate(0, scanSpeed * Time.deltaTime, 0, Space.World); return; }
            direction.y = 0;
            transform.rotation = TurnTowards(transform.rotation, direction, TurnDegreesPerSecond, Time.deltaTime);
        }
        public static Quaternion TurnTowards(Quaternion current, Vector3 direction, float degreesPerSecond, float dt)
        {
            direction.y = 0;
            if (!AgentRoute.Finite(direction) || direction.sqrMagnitude < .001f || !float.IsFinite(dt) || !float.IsFinite(degreesPerSecond)) return current;
            return Quaternion.Normalize(Quaternion.RotateTowards(current, Quaternion.LookRotation(direction), Mathf.Max(0, degreesPerSecond) * Mathf.Max(0, dt)));
        }
    }
}
