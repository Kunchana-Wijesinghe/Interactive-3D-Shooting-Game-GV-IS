using UnityEngine;
using FacilityBreach.Combat;
namespace FacilityBreach.AI.Roles
{
    public sealed class CoverDefenderBrain : RoleDecisionBrain
    {
        public float ReassessSeconds = 1.5f, HoldSeconds = 3;
        public Vector3 CoverGoal { get; private set; }
        public int CoverEvaluations { get; private set; }
        private float nextAssessment;
        protected override void Start() { base.Start(); Combat.Range = 12; CoverGoal = Post; GetComponent<UnityEngine.AI.NavMeshAgent>().updateRotation = false; }
        protected override void BeforePerception() { if (HasLastKnown) FaceRememberedPosition(); }
        protected override void Decide(float now)
        {
            if (SeesPlayer)
            {
                SetState(RoleState.Cover);
                if (now >= nextAssessment) { nextAssessment = now + Mathf.Max(1.5f, ReassessSeconds); EvaluateCover(); }
                Mover.RequestMove(CoverGoal, .5f); Combat.TryAttack(Runtime.PlayerTransform); return;
            }
            Combat.CancelPendingAttack();
            if (HasLastKnown && now - LastSeenTime < HoldSeconds) { Mover.Stop(); SetState(RoleState.Search); return; }
            if (HasLastKnown || State == RoleState.Return) ReturnToPost(); else { Mover.Stop(); SetState(RoleState.Patrol); }
        }
        private void EvaluateCover()
        {
            CoverEvaluations++; float best = float.NegativeInfinity; Vector3 selected = Post;
            // Sample guard-area positions and test actual intervening solids from remembered perception.
            for (int ring = 1; ring <= 3; ring++) for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                Vector3 point = Post + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring * 2;
                if (!Reachable(point, out var candidate, out float pathLength) || pathLength > 25) continue;
                Vector3 origin = candidate + Vector3.up * 1.4f, delta = LastKnownPosition + Vector3.up - origin;
                bool sheltered = false;
                foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.collider.GetComponentInParent<EnemyCombatAdapter>() && !hit.transform.IsChildOf(Runtime.PlayerTransform)) { sheltered = true; break; }
                float score = (sheltered ? 12 : 0) - pathLength * .4f - Mathf.Abs(delta.magnitude - 10) * .3f;
                if (score > best) { best = score; selected = candidate; }
            }
            CoverGoal = selected; GameplayDiagnostics.Trace($"CD cover={CoverGoal} evaluations={CoverEvaluations} lastKnown={LastKnownPosition}", this);
        }
        public override void ResetEnemyActions() { base.ResetEnemyActions(); nextAssessment = 0; CoverGoal = Post; }
    }
}
