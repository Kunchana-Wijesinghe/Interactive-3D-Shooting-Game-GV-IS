using FacilityBreach.AI.Roles.Flanking;
using FacilityBreach.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.AI.Roles
{
    // Baseline FL-04 integration retained; Ashen's IS policy/scorer replace its tactical decisions.
    public sealed class FlankerBrain : RoleDecisionBrain, IEnemyActivitySink
    {
        public FlankDecisionPolicy Policy { get; } = new FlankDecisionPolicy();
        public Vector3 FlankGoal => Policy.HasGoal ? Policy.Goal : Vector3.zero;
        public float FlankAngle => Policy.Selected?.AngleDegrees ?? 0;
        public float FlankRadius => Policy.Selected?.TargetDistance ?? 0;
        public int Plans => Policy.ReplanCount;
        public int NavigationQueries => environment?.PathQueries ?? 0;
        private FlankUnityEnvironment environment;
        private int tracedPlan;

        protected override void Start()
        {
            base.Start(); Combat.Range = 12;
            environment = new FlankUnityEnvironment(transform, GetComponent<NavMeshAgent>());
        }
        protected override void BeforePerception()
        {
            if (Policy.HasObservation && Mover.Status == Perera.SoundHunter.MoveStatus.Arrived)
                Mover.FacePoint(Policy.ObservedTarget);
        }
        protected override void Decide(float now)
        {
            environment.BeginDecision(SeesPlayer, SightRange, FieldOfView);
            var command = Policy.Step(now, transform.position, SeesPlayer, LastKnownPosition,
                Mover.Status, environment, Post);
            if (!Policy.HasObservation && !SeesPlayer) HasLastKnown = false;
            var state = Policy.Phase == FlankPhase.Combat ? RoleState.Combat :
                Policy.Phase == FlankPhase.Flanking ? RoleState.Flank :
                Policy.Phase == FlankPhase.Searching ? RoleState.Search :
                Policy.Phase == FlankPhase.Returning ? RoleState.Return : RoleState.Patrol;
            SetState(state);
            if (command.Action != FlankAction.Attack) Combat.CancelPendingAttack();
            switch (command.Action)
            {
                case FlankAction.Move:
                case FlankAction.Return:
                    if (command.NewGoal) Mover.Stop();
                    Mover.RequestMove(command.Goal, .6f);
                    break;
                case FlankAction.Attack:
                    Mover.FacePoint(Policy.ObservedTarget);
                    if (SeesPlayer) Combat.TryAttack(Runtime.PlayerTransform);
                    break;
                case FlankAction.Scan:
                    Mover.Stop(); Mover.Scan(); break;
                default:
                    Mover.Stop();
                    if (SeesPlayer) Mover.FacePoint(Policy.ObservedTarget);
                    break;
            }
            if (tracedPlan != Plans)
            {
                tracedPlan = Plans;
                GameplayDiagnostics.Trace($"FL IS plan={Plans} phase={Policy.Phase} goal={FlankGoal} {Policy.Selected?.Score.ToString()} reason={Policy.Reason}", this);
            }
        }
        void IEnemyActivitySink.SetEnemyActive(bool value)
        { base.SetEnemyActive(value); if (!value) { Policy.Deactivate(); tracedPlan = 0; } }
        void IEnemyActivitySink.ResetEnemyActions() => ResetEnemyActions();
        public override void ResetEnemyActions()
        { base.ResetEnemyActions(); Policy.Reset(); tracedPlan = 0; }
        protected override void OnDisable()
        { base.OnDisable(); Policy.Deactivate(); HasLastKnown = SeesPlayer = false; tracedPlan = 0; }
    }
}
