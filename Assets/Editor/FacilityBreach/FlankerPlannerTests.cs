using System;
using FacilityBreach.AI.Roles.Flanking;
using Perera.SoundHunter;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    public static class FlankerPlannerTests
    {
        private sealed class World : IFlankEnvironment
        {
            public int Queries;
            public Vector3 Self = Vector3.zero;
            public Func<Vector3, Vector3, FlankProbe> Override;
            public FlankProbe Probe(Vector3 position, Vector3 observed)
            {
                Queries++;
                return Override != null ? Override(position, observed) : new FlankProbe {
                    Position = position, Reachable = true, RouteLength = Vector3.Distance(Self, position), FiringUsefulness = 1
                };
            }
        }
        private static int checks;
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("IS Flanker FAIL: " + message); checks++; Debug.Log("IS Flanker PASS: " + message); }
        private static readonly Vector3 Target = new Vector3(0, 0, 12), Post = new Vector3(-10, 0, 0);
        private static FlankCommand Step(FlankDecisionPolicy p, World world, float now, MoveStatus move = MoveStatus.Moving,
            bool visible = true, Vector3? observation = null, Vector3? self = null) =>
            p.Step(now, self ?? Vector3.zero, visible, observation ?? Target, move, world, Post);

        [MenuItem("Facility Breach/IS Flanker/Run Deterministic Planner and Recovery Tests")]
        public static void RunAll()
        {
            checks = 0; var planner = new FlankTacticalPlanner(); var world = new World();
            foreach (float distance in new[] { 12f, 13f, 14f, 15f })
            {
                var plan = planner.Plan(Vector3.zero, Vector3.forward * distance, world);
                Check(plan.Selected != null && plan.Selected.AngleDegrees >= 45 && plan.Candidates.Count == 18,
                    $"stationary visible target at {distance} units has a target-centered flank");
            }
            var score = FlankTacticalPlanner.Score(120, 0, 6, 1);
            Check(Mathf.Abs(score.Total - 1) < .00001f, "normalized ideal utility is one");
            var cheap = FlankTacticalPlanner.Score(90, 3, 6, 1);
            var wide = FlankTacticalPlanner.Score(120, 30, 6, 1);
            Check(cheap.Total > wide.Total, "route savings can outweigh a wider angle");
            Check(FlankTacticalPlanner.Score(90, 10, 6, 1).Total > FlankTacticalPlanner.Score(90, 10, 9, 1).Total,
                "six-unit preferred attack distance wins otherwise equal scoring");
            Check(FlankTacticalPlanner.Score(90, 10, 6, 1).Total > FlankTacticalPlanner.Score(90, 10, 6, .5f).Total,
                "more useful firing position wins otherwise equal scoring");
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = true, RouteLength = 10, FiringUsefulness = 1 };
            var tied = planner.Plan(Vector3.zero, Target, world);
            Check(tied.Selected.Id == 10 && planner.Plan(Vector3.zero, Target, world).Selected.Id == 10,
                "symmetric utility ties choose stable radius-six negative-120 candidate");
            Vector3 blockedBest = tied.Selected.Position;
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = (v-blockedBest).sqrMagnitude > .01f, RouteLength = 10, FiringUsefulness = 1 };
            Check(planner.Plan(Vector3.zero, Target, world).Selected.Position != blockedBest, "unreachable best candidate yields a viable alternative");
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = true, Occupied = (v-blockedBest).sqrMagnitude < .01f, RouteLength = 10, FiringUsefulness = 1 };
            Check(planner.Plan(Vector3.zero, Target, world).Selected.Position != blockedBest, "occupied best candidate yields a viable alternative");
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = true, RouteLength = 10, FiringUsefulness = (v-blockedBest).sqrMagnitude < .01f ? 0 : 1 };
            Check(planner.Plan(Vector3.zero, Target, world).Selected.Position != blockedBest, "unusable best firing position yields a viable alternative");
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = true, RouteLength = 10, FiringUsefulness = 1 };
            var incumbent = planner.Evaluate(18, Target + Vector3.right * 6, Vector3.zero, Target, world, null);
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = true, RouteLength = (v-incumbent.Position).sqrMagnitude < .01f ? 0 : 10, FiringUsefulness = 1 };
            var retained = planner.Plan(Vector3.zero, Target, world, null, incumbent.Position);
            Check(retained.Selected.Id == 18 && retained.Reason.Contains("incumbent"), "switch margin retains a near-equal viable incumbent");
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = true, RouteLength = (v-incumbent.Position).sqrMagnitude < .01f ? 30 : 0, FiringUsefulness = 1 };
            Check(planner.Plan(Vector3.zero, Target, world, null, incumbent.Position).Selected.Id != 18, "large utility improvement switches incumbent");
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = false };
            var rejected = planner.Plan(Vector3.zero, Target, world);
            Check(rejected.Selected == null && rejected.Candidates.TrueForAll(c => c.Rejection.Contains("navigation")), "unreachable candidates explicitly rejected");
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = true, Occupied = true, FiringUsefulness = 1 };
            Check(planner.Plan(Vector3.zero, Target, world).Candidates.TrueForAll(c => c.Rejection.Contains("occupies")), "observed occupied positions rejected");
            world.Override = (v, _) => new FlankProbe { Position = v, Reachable = true, FiringUsefulness = 0 };
            Check(planner.Plan(Vector3.zero, Target, world).Selected == null, "all unusable firing positions rejected");
            var p = new FlankDecisionPolicy();
            Step(p, world, 0); Step(p, world, 1); Check(p.ReplanCount == 1, "initial rejection respects retry cooldown");
            Step(p, world, 2); Step(p, world, 4); int queries = world.Queries;
            for (int i = 5; i < 60; i++) Step(p, world, i);
            Check(p.ReplanCount == 3 && p.Phase == FlankPhase.Holding && world.Queries == queries, "all-rejected episode stops after three attempts, no endless queries");

            world = new World(); p = new FlankDecisionPolicy(); var first = Step(p, world, 0, MoveStatus.Idle);
            queries = world.Queries; Step(p, world, .1f, MoveStatus.Idle);
            Check(p.ReplanCount == 1 && world.Queries == queries, "deferred idle movement request does not become a tactical failure or new plan");
            Vector3 failedGoal = first.Goal;
            Step(p, world, .2f, MoveStatus.Failed);
            Check(!p.HasGoal && p.IsFailedGoal(failedGoal, .2f) && p.Phase == FlankPhase.Recovering, "movement failure blacklists goal without target motion");
            Step(p, world, 1); Check(p.ReplanCount == 1, "movement recovery cannot churn within cooldown");
            var alternative = Step(p, world, 2.3f, MoveStatus.Idle);
            Check(p.ReplanCount == 2 && alternative.Action == FlankAction.Move && Vector3.Distance(alternative.Goal, failedGoal) > 1,
                "unchanged target gets another candidate after failed movement");
            Check(p.IsFailedGoal(failedGoal, 6.19f) && !p.IsFailedGoal(failedGoal, 6.2f), "failed-goal exclusion expires at six-second boundary");
            Step(p, world, 2.4f, MoveStatus.Failed); Step(p, world, 4.5f, MoveStatus.Idle); Step(p, world, 4.6f, MoveStatus.Failed);
            Step(p, world, 8); queries = world.Queries; Step(p, world, 20);
            Check(p.ReplanCount == 3 && world.Queries == queries && p.Phase == FlankPhase.Holding, "repeated movement failures exhaust a finite tactical budget");
            var cooldownPolicy = new FlankDecisionPolicy();
            Step(cooldownPolicy, world, 0, MoveStatus.Idle); Step(cooldownPolicy, world, .2f, MoveStatus.Failed);
            Step(cooldownPolicy, world, .3f, MoveStatus.Idle, visible: false); Step(cooldownPolicy, world, .4f, MoveStatus.Idle);
            Step(cooldownPolicy, world, 2.1f, MoveStatus.Idle);
            Check(cooldownPolicy.ReplanCount == 1 && !cooldownPolicy.HasGoal, "reacquisition cannot bypass the failed-goal recovery wait");

            p = new FlankDecisionPolicy(); world = new World(); Step(p, world, 0, MoveStatus.Idle);
            Step(p, world, 1, observation: Target + Vector3.right * 4.1f); Check(p.ReplanCount == 1, "large observed movement cannot bypass two-second cooldown");
            Step(p, world, 2.1f, observation: Target + Vector3.right * 4.1f); Check(p.ReplanCount == 2, "observed displacement replans after cooldown");
            p = new FlankDecisionPolicy(); Step(p, world, 0, MoveStatus.Idle);
            Step(p, world, 3, observation: Target + Vector3.right * 4); Check(p.ReplanCount == 1, "exact four-unit observation change does not replan");
            p = new FlankDecisionPolicy(); Step(p, world, 0, MoveStatus.Idle);
            Vector3 stableGoal = p.Goal;
            Step(p, world, 5, MoveStatus.Arrived, self: stableGoal);
            Check(p.ReplanCount == 1 && p.Goal == stableGoal && p.Phase == FlankPhase.Combat, "arrival does not collapse angular score or rotate the approach reference");
            Step(p, world, 5.2f, MoveStatus.Arrived, visible: false, self: stableGoal);
            Step(p, world, 5.5f, MoveStatus.Arrived, self: stableGoal);
            Check(p.Goal == stableGoal && p.Phase == FlankPhase.Combat && p.ApproachOrigin == Vector3.zero,
                "brief loss/reacquisition at arrival preserves approach basis and does not orbit target");

            p = new FlankDecisionPolicy(); world = new World(); Step(p, world, 0, MoveStatus.Idle);
            queries = world.Queries; Step(p, world, 1, visible: false, observation: Vector3.one * 99);
            Check(p.ObservedTarget == Target && p.LastObservedAt == 0 && world.Queries == queries, "hidden live position is ignored and causes no candidate/firing probes");
            Step(p, world, 3, visible: false); Check(p.Phase == FlankPhase.Searching, "lost-sight pursuit ends at three seconds");
            var returning = Step(p, world, 7, MoveStatus.Idle, visible: false);
            Check(!p.HasObservation && returning.Action == FlankAction.Return && p.ReturnAttempts == 1 && world.Queries == queries,
                "memory expires at seven seconds and returns without hidden target queries");
            Step(p, world, 7.1f, MoveStatus.Failed, visible: false); Step(p, world, 8, MoveStatus.Idle, visible: false);
            Check(p.ReturnAttempts == 1, "blocked-return cooldown cannot create uncounted retries");
            Step(p, world, 9.2f, MoveStatus.Idle, visible: false); Step(p, world, 9.3f, MoveStatus.Failed, visible: false);
            var hold = Step(p, world, 30, MoveStatus.Idle, visible: false);
            Check(p.ReturnAttempts == 2 && p.Phase == FlankPhase.Holding && hold.Action == FlankAction.Hold, "blocked return stops after two attempts");
            Step(p, world, 31, MoveStatus.Idle, observation: Target + Vector3.left);
            Check(p.HasObservation && p.ObservedTarget == Target + Vector3.left && p.HasGoal, "fresh confirmed reacquisition resumes tactical planning");
            p = new FlankDecisionPolicy(); world = new World(); Step(p, world, 0, MoveStatus.Idle); Step(p, world, 7, MoveStatus.Idle, visible: false);
            Step(p, world, 19, MoveStatus.Moving, visible: false);
            Check(p.Phase == FlankPhase.Holding && p.Reason.Contains("timed out"), "return has a twelve-second deadline even without mover failure");
            p.Step(20, Vector3.zero, true, Target, MoveStatus.Idle, world, Post, false);
            Check(p.Phase == FlankPhase.Inactive && !p.HasObservation && !p.HasGoal && p.ReplanCount == 0 && p.LastPlan == null,
                "death/activity clears observation, goal, attempt counters and diagnostics");
            Step(p, world, 21, MoveStatus.Idle); Vector3 resetGoal = p.Goal;
            Step(p, world, 21.1f, MoveStatus.Failed); p.Reset();
            Check(!p.IsFailedGoal(resetGoal, 21.2f) && p.ReturnAttempts == 0 && p.Selected == null && p.Phase == FlankPhase.Idle,
                "reset/revival clears failed-goal cooldowns and recovery state");
            Step(p, world, 22, MoveStatus.Idle); Check(p.HasGoal && p.ReplanCount == 1, "reset agent accepts a fresh observation and goal");
            queries = world.Queries;
            var bad = planner.Plan(new Vector3(float.NaN, 0, 0), Target, world);
            Check(bad.Selected == null && world.Queries == queries, "invalid planner input does not reach navigation");
            Debug.Log($"[IS Flanker] PASS {checks} deterministic planner/recovery checks.");
        }
        public static void RunBatch()
        {
            try { RunAll(); AgentControllerTests.RunBatch(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
