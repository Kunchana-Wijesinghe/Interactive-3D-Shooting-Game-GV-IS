using FacilityBreach.AI.SoundHunter;
using FacilityBreach.Combat;
using Perera.SoundHunter;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.AI.Roles
{
    public enum RoleState { Patrol, Detecting, Combat, Investigate, Search, Return, Cover, Flank, Inactive }
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyCombatAdapter), typeof(SoundHunterNavMeshMover))]
    public abstract class RoleDecisionBrain : MonoBehaviour, IEnemyActivitySink
    {
        public RoleState State { get; protected set; } = RoleState.Patrol;
        public Vector3 LastKnownPosition { get; protected set; }
        public bool SeesPlayer { get; protected set; }
        public float SightRange = 16, FieldOfView = 110;
        protected FacilityBreachRuntime Runtime;
        protected EnemyCombatAdapter Combat;
        protected SoundHunterNavMeshMover Mover;
        protected Vector3 Post;
        protected bool Active = true, HasLastKnown;
        protected float LastSeenTime, NextDecision;
        protected virtual void Start()
        {
            Runtime = FindAnyObjectByType<FacilityBreachRuntime>(); Combat = GetComponent<EnemyCombatAdapter>();
            Mover = GetComponent<SoundHunterNavMeshMover>(); Post = transform.position;
        }
        protected virtual void Update()
        {
            if (!Runtime || !Active || Combat.IsDead || !Runtime.AcceptsGameplay) return;
            float now = Runtime.GameplayTime; if (now < NextDecision) return; NextDecision = now + .1f;
            BeforePerception();
            SeesPlayer = AgentPerception.Visible(transform, Runtime.PlayerTransform, SightRange, out var seen, FieldOfView);
            if (SeesPlayer) { LastKnownPosition = seen; LastSeenTime = now; HasLastKnown = true; }
            Decide(now);
        }
        protected virtual void BeforePerception() { }
        protected void FaceRememberedPosition()
        {
            Vector3 direction = LastKnownPosition - transform.position; direction.y = 0;
            if (direction.sqrMagnitude > .01f) Mover.FacePoint(LastKnownPosition);
        }
        protected abstract void Decide(float now);
        protected void SetState(RoleState state)
        {
            if (State == state) return; State = state;
            GameplayDiagnostics.Trace($"role {Combat.Identity} state={state} visible={SeesPlayer} lastKnown={LastKnownPosition}", this);
        }
        protected bool Reachable(Vector3 point, out Vector3 reachable, out float length)
        {
            reachable = default; length = 0;
            if (!Mover.OnNavigation || !NavMesh.SamplePosition(point, out var hit, 1, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            reachable = hit.position; for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return true;
        }
        protected void ReturnToPost()
        {
            SetState(RoleState.Return); Mover.RequestMove(Post, .5f);
            if (Mover.Status == MoveStatus.Arrived) { HasLastKnown = false; Mover.Stop(); SetState(RoleState.Patrol); }
        }
        protected virtual void OnDisable() { if (Mover) Mover.Stop(); if (Combat) Combat.CancelPendingAttack(); }
        public void SetEnemyActive(bool value)
        {
            Active = value;
            if (!value) { SeesPlayer = false; HasLastKnown = false; if (Mover) Mover.Stop(); if (Combat) Combat.CancelPendingAttack(); SetState(RoleState.Inactive); }
        }
        public virtual void ResetEnemyActions()
        {
            HasLastKnown = SeesPlayer = false; LastKnownPosition = default; LastSeenTime = NextDecision = 0;
            if (Mover) Mover.ResetMovement(); SetState(RoleState.Patrol);
        }
    }
}
