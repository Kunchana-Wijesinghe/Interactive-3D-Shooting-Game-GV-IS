using System.Collections.Generic;
using Perera.SoundHunter;
using UnityEngine;

namespace FacilityBreach.AI.Roles.Flanking
{
    public enum FlankPhase { Idle, Flanking, Combat, Recovering, Searching, Returning, Holding, Inactive }
    public enum FlankAction { Hold, Move, Attack, Scan, Return, Inactive }
    public struct FlankCommand
    {
        public FlankAction Action;
        public Vector3 Goal;
        public bool NewGoal;
    }

    /// <summary>Deterministic observation/recovery state; no GameObjects, transforms, combat or animation writes.</summary>
    public sealed class FlankDecisionPolicy
    {
        public const float PlanCooldown = 2, TargetMoveThreshold = 4, FailedGoalSeconds = 6;
        public const float FollowMemorySeconds = 3, MemorySeconds = 7, ReturnDeadlineSeconds = 12;
        public const int MaximumAttempts = 3, MaximumReturnAttempts = 2;
        private struct FailedGoal { public Vector3 Point; public float Until; }
        private readonly List<FailedGoal> failed = new List<FailedGoal>();
        private readonly FlankTacticalPlanner planner = new FlankTacticalPlanner();
        private Vector3 episodeAnchor, goal;
        private bool hadSight, hasGoal, needPlan, newGoal, returnStarted, returnFinished, returnInFlight;
        private float lastPlanAt = float.NegativeInfinity, nextPlanAt, nextValidation, nextReturnAt, returnStartedAt;
        private int attempts, returnAttempts;
        public FlankPhase Phase { get; private set; }
        public bool HasObservation { get; private set; }
        public Vector3 ObservedTarget { get; private set; }
        public Vector3 ApproachOrigin { get; private set; }
        public float LastObservedAt { get; private set; }
        public Vector3 Goal => goal;
        public bool HasGoal => hasGoal;
        public int ReplanCount { get; private set; }
        public int Attempts => attempts;
        public int ReturnAttempts => returnAttempts;
        public float NextPlanAt => nextPlanAt;
        public string Reason { get; private set; } = "No observation";
        public string LastRecoveryReason { get; private set; } = "None";
        public FlankPlan LastPlan { get; private set; }
        public FlankCandidate Selected { get; private set; }

        public bool IsFailedGoal(Vector3 point, float now) => failed.Exists(x => now < x.Until && (x.Point - point).sqrMagnitude <= 1);
        private FlankCommand Command(FlankAction action)
        {
            var command = new FlankCommand { Action = action, Goal = goal, NewGoal = newGoal };
            newGoal = false; return command;
        }
        private void RejectGoal(float now, string reason)
        {
            if (hasGoal) failed.Add(new FailedGoal { Point = goal, Until = now + FailedGoalSeconds });
            hasGoal = false; Selected = null; needPlan = true;
            nextPlanAt = Mathf.Max(now + PlanCooldown, lastPlanAt + PlanCooldown);
            Reason = LastRecoveryReason = reason; Phase = FlankPhase.Recovering;
        }
        private void BeginEpisode(float now, Vector3 self, bool preserveApproach)
        {
            if (!preserveApproach) ApproachOrigin = self;
            episodeAnchor = ObservedTarget; attempts = 0; needPlan = true;
            nextPlanAt = Mathf.Max(nextPlanAt, Mathf.Max(now, lastPlanAt + PlanCooldown));
            returnStarted = returnFinished = returnInFlight = false;
        }
        public FlankCommand Step(float now, Vector3 self, bool visible, Vector3 sensedPosition,
            MoveStatus movement, IFlankEnvironment environment, Vector3 post, bool active = true)
        {
            if (!active) { Deactivate(); return Command(FlankAction.Inactive); }
            if (Phase == FlankPhase.Inactive) Phase = FlankPhase.Idle;
            if (!float.IsFinite(now) || !FlankTacticalPlanner.Finite(self)) { Reason = "Invalid decision input"; return Command(FlankAction.Hold); }
            failed.RemoveAll(x => now >= x.Until);
            visible = visible && FlankTacticalPlanner.Finite(sensedPosition);
            bool reacquired = visible && !hadSight;
            if (visible)
            {
                bool first = !HasObservation;
                ObservedTarget = sensedPosition; LastObservedAt = now; HasObservation = true;
                bool displaced = Vector3.Distance(ObservedTarget, episodeAnchor) > TargetMoveThreshold;
                if (first || reacquired || displaced)
                {
                    bool preserveApproach = reacquired && !first && !displaced && hasGoal;
                    if (Phase == FlankPhase.Returning || Phase == FlankPhase.Holding)
                    { hasGoal = false; Selected = null; }
                    BeginEpisode(now, self, preserveApproach);
                }
            }
            hadSight = visible;
            if (!visible)
            {
                // Do not call the candidate/visibility provider while the target is hidden.
                if (HasObservation && now - LastObservedAt < MemorySeconds)
                {
                    if (hasGoal && movement == MoveStatus.Failed) RejectGoal(now, "Remembered route failed; search without new probes");
                    if (hasGoal && now - LastObservedAt < FollowMemorySeconds && movement != MoveStatus.Arrived)
                    { Phase = FlankPhase.Flanking; return Command(FlankAction.Move); }
                    Phase = FlankPhase.Searching; Reason = "Bounded stationary scan of remembered observation";
                    return Command(FlankAction.Scan);
                }
                if (HasObservation)
                { HasObservation = false; hasGoal = false; Selected = null; returnStarted = true; returnFinished = returnInFlight = false; returnAttempts = 0; nextReturnAt = now; returnStartedAt = now; }
                return returnStarted && !returnFinished ? Return(now, self, movement, post) : Command(FlankAction.Hold);
            }

            if (hasGoal && movement == MoveStatus.Failed) RejectGoal(now, "Movement failed: blacklist goal for six seconds");
            if (hasGoal && !needPlan && now >= nextValidation)
            {
                nextValidation = now + PlanCooldown;
                var check = planner.Evaluate(18, goal, self, ObservedTarget, environment, p => IsFailedGoal(p, now), ApproachOrigin);
                if (!check.Viable) RejectGoal(now, "Goal invalidated: " + check.Rejection);
                else Selected = check;
            }
            if (needPlan && now >= nextPlanAt && attempts < MaximumAttempts)
            {
                lastPlanAt = now; nextPlanAt = now + PlanCooldown; nextValidation = now + PlanCooldown;
                attempts++; ReplanCount++;
                LastPlan = planner.Plan(self, ObservedTarget, environment, p => IsFailedGoal(p, now), hasGoal ? goal : (Vector3?)null, ApproachOrigin);
                Reason = LastPlan.Reason;
                if (LastPlan.Selected == null) { hasGoal = false; Selected = null; LastRecoveryReason = LastPlan.Reason; }
                else
                {
                    newGoal = !hasGoal || (goal - LastPlan.Selected.Position).sqrMagnitude > .16f;
                    goal = LastPlan.Selected.Position; hasGoal = true; Selected = LastPlan.Selected;
                }
                needPlan = !hasGoal;
            }
            if (!hasGoal)
            {
                Phase = attempts >= MaximumAttempts ? FlankPhase.Holding : FlankPhase.Recovering;
                if (attempts >= MaximumAttempts) Reason = LastRecoveryReason = "Three tactical attempts exhausted; hold until displacement/reacquisition/reset";
                return Command(FlankAction.Hold);
            }
            if (!newGoal && movement == MoveStatus.Arrived) { Phase = FlankPhase.Combat; return Command(FlankAction.Attack); }
            Phase = FlankPhase.Flanking; return Command(FlankAction.Move);
        }

        private FlankCommand Return(float now, Vector3 self, MoveStatus movement, Vector3 post)
        {
            if (Vector3.Distance(self, post) <= .6f || (returnInFlight && movement == MoveStatus.Arrived))
            { returnFinished = true; Phase = FlankPhase.Idle; Reason = "Returned to post"; return Command(FlankAction.Hold); }
            if (movement == MoveStatus.Failed && returnInFlight)
            { returnInFlight = false; nextReturnAt = now + PlanCooldown; Reason = LastRecoveryReason = "Return movement failed; cooldown before bounded retry"; }
            if (now - returnStartedAt >= ReturnDeadlineSeconds || (returnAttempts >= MaximumReturnAttempts && !returnInFlight))
            { returnFinished = true; Phase = FlankPhase.Holding; Reason = LastRecoveryReason = "Return blocked/timed out; hold until fresh sight or reset"; return Command(FlankAction.Hold); }
            if (!returnInFlight)
            {
                if (now < nextReturnAt) { Phase = FlankPhase.Returning; return Command(FlankAction.Hold); }
                returnAttempts++; goal = post; newGoal = true; returnInFlight = true;
            }
            Phase = FlankPhase.Returning; Reason = $"Return attempt {returnAttempts}/{MaximumReturnAttempts}";
            return Command(FlankAction.Return);
        }

        public void Deactivate() { Reset(); Phase = FlankPhase.Inactive; Reason = "Inactive/dead"; }
        public void Reset()
        {
            failed.Clear(); hadSight = hasGoal = needPlan = newGoal = returnStarted = returnFinished = returnInFlight = false;
            HasObservation = false; ObservedTarget = ApproachOrigin = episodeAnchor = goal = default;
            LastObservedAt = nextPlanAt = nextValidation = nextReturnAt = returnStartedAt = 0;
            lastPlanAt = float.NegativeInfinity; attempts = returnAttempts = ReplanCount = 0;
            Selected = null; LastPlan = null; Phase = FlankPhase.Idle; Reason = "Reset/no observation"; LastRecoveryReason = "None";
        }
    }
}
