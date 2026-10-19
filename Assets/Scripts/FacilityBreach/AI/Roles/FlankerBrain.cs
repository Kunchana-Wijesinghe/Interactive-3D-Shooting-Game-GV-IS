using Perera.SoundHunter;
using UnityEngine;
namespace FacilityBreach.AI.Roles
{
    public sealed class FlankerBrain : RoleDecisionBrain
    {
        public Vector3 FlankGoal { get; private set; }
        public float FlankAngle { get; private set; }
        public float FlankRadius { get; private set; }
        public int Plans { get; private set; }
        private Vector3 plannedAgainst;
        private bool planned, attempted;
        private float plannedAt, searchUntil;
        protected override void Start() { base.Start(); Combat.Range = 12; }
        protected override void BeforePerception()
        { if (HasLastKnown && Mover.Status == MoveStatus.Arrived) FaceRememberedPosition(); }
        protected override void Decide(float now)
        {
            if (SeesPlayer)
            {
                if (!attempted || (now - plannedAt >= 2 && Vector3.Distance(LastKnownPosition, plannedAgainst) > 4)) Plan(now);
                if (planned)
                {
                    SetState(RoleState.Flank); Mover.RequestMove(FlankGoal, .6f);
                    if (Mover.Status == MoveStatus.Arrived)
                    { SetState(RoleState.Combat); Combat.TryAttack(Runtime.PlayerTransform); }
                    else Combat.CancelPendingAttack();
                }
                else { Mover.Stop(); Combat.CancelPendingAttack(); }
                return;
            }
            Combat.CancelPendingAttack();
            if (!HasLastKnown) { Mover.Stop(); SetState(RoleState.Patrol); return; }
            if (now - LastSeenTime < 3) { if (planned) Mover.RequestMove(FlankGoal, .6f); return; }
            if (State != RoleState.Search && State != RoleState.Return) { Mover.Stop(); searchUntil = now + 4; SetState(RoleState.Search); }
            if (State == RoleState.Search) { Mover.Scan(); if (now < searchUntil) return; }
            ReturnToPost(); if (State == RoleState.Patrol) { planned = attempted = false; }
        }
        private void Plan(float now)
        {
            attempted = true; plannedAt = now; plannedAgainst = LastKnownPosition; Plans++; planned = false;
            Vector3 direct = LastKnownPosition - transform.position; direct.y = 0;
            if (direct.sqrMagnitude < .01f) direct = transform.forward;
            float best = float.PositiveInfinity;
            foreach (float radius in new[] { 3f, 6f, 9f }) foreach (float angle in new[] { -60f, 60f, -90f, 90f, -120f, 120f })
            {
                Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * direct.normalized * radius;
                if (!Reachable(LastKnownPosition + offset, out var candidate, out float length) || length > 30) continue;
                Vector3 toGoal = candidate - transform.position; toGoal.y = 0;
                float deviation = Vector3.Angle(direct, toGoal);
                float candidateRadius = Vector3.Distance(new Vector3(candidate.x, 0, candidate.z), new Vector3(LastKnownPosition.x, 0, LastKnownPosition.z));
                if (deviation < 50 || candidateRadius < 3 || candidateRadius > 9) continue;
                if (length < best) { best = length; FlankGoal = candidate; FlankAngle = deviation; FlankRadius = candidateRadius; planned = true; }
            }
            if (planned) Mover.Stop();
            GameplayDiagnostics.Trace($"FL plan={Plans} reachable={planned} goal={FlankGoal} angle={FlankAngle} radius={FlankRadius}", this);
        }
        public override void ResetEnemyActions() { base.ResetEnemyActions(); planned = attempted = false; plannedAt = searchUntil = 0; Plans = 0; plannedAgainst = FlankGoal = default; }
    }
}
