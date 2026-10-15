using Perera.SoundHunter;
using UnityEngine;
namespace FacilityBreach.AI.Roles
{
    public sealed class PatrolGuardBrain : RoleDecisionBrain
    {
        public Vector3[] Route;
        public float DetectionSeconds = .5f, SearchSeconds = 4;
        public bool Detected { get; private set; }
        private float sightStarted = -1, searchUntil;
        private int waypoint;
        protected override void Start() { base.Start(); SightRange = 13; FieldOfView = 110; Combat.Range = 12; }
        protected override void Decide(float now)
        {
            if (SeesPlayer)
            {
                if (sightStarted < 0) sightStarted = now;
                if (!Detected && now - sightStarted < DetectionSeconds) { SetState(RoleState.Detecting); Mover.Stop(); return; }
                Detected = true; SetState(RoleState.Combat);
                if (Vector3.Distance(transform.position, LastKnownPosition) > 10) Mover.RequestMove(LastKnownPosition, 10);
                else Mover.Stop(); Combat.TryAttack(Runtime.PlayerTransform); return;
            }
            sightStarted = -1; Combat.CancelPendingAttack();
            if (Detected)
            {
                Detected = false; Mover.Stop(); SetState(RoleState.Investigate);
            }
            if (State == RoleState.Investigate)
            {
                Mover.RequestMove(LastKnownPosition, .6f);
                if (Mover.Status == MoveStatus.Arrived || Mover.Status == MoveStatus.Failed) { Mover.Stop(); searchUntil = now + SearchSeconds; SetState(RoleState.Search); }
                return;
            }
            if (State == RoleState.Search)
            {
                transform.Rotate(0, 9, 0); if (now < searchUntil) return;
                HasLastKnown = false; Mover.Stop(); SetState(RoleState.Patrol);
            }
            if (State == RoleState.Detecting) SetState(RoleState.Patrol);
            if (Route == null || Route.Length == 0) { Mover.Stop(); return; }
            if (Mover.Status == MoveStatus.Arrived || Mover.Status == MoveStatus.Failed) { waypoint = (waypoint + 1) % Route.Length; Mover.Stop(); }
            Mover.RequestMove(Route[waypoint], .5f);
        }
        public override void ResetEnemyActions() { base.ResetEnemyActions(); sightStarted = -1; searchUntil = 0; Detected = false; waypoint = 0; }
    }
}
