using System.Collections.Generic;
using FacilityBreach.Combat;
using Perera.SoundHunter;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach.AI.Roles
{
    public sealed class CoverDefenderBrain : RoleDecisionBrain
    {
        public float ReassessSeconds = 1.5f;
        public float HoldSeconds = 3;
        public float CoverSearchRadius = 18;
        public float MaximumCoverPath = 20;
        public float PreferredCoverDistance = 7;
        public float MinimumCoverDistance = 3.5f;
        public float PeekDistance = 1.25f;
        public float PeekHoldSeconds = .8f;
        public float PeekInterval = 2;
        public Vector3 CoverGoal { get; private set; }
        public Vector3 PeekGoal { get; private set; }
        public int CoverEvaluations { get; private set; }
        public bool HasCoverPosition { get; private set; }
        public bool IsPeeking { get; private set; }

        private float nextAssessment;
        private float nextPeek;
        private float peekHoldUntil;

        protected override void Start()
        {
            base.Start();
            Combat.Range = 12;
            CoverGoal = Post;
            var agent = GetComponent<NavMeshAgent>();
            if (agent) agent.updateRotation = false;
        }

        protected override void Decide(float now)
        {
            if (SeesPlayer)
            {
                IsPeeking = false;
                peekHoldUntil = 0;
                SetState(RoleState.Cover);

                bool coverUnsafe = HasCoverPosition && !IsSheltered(CoverGoal, LastKnownPosition);
                if (now >= nextAssessment || coverUnsafe) EvaluateCover(now);

                if (HasCoverPosition && Vector3.Distance(transform.position, CoverGoal) > .65f)
                    Mover.RequestMove(CoverGoal, .5f);
                else
                    Mover.Stop();

                Combat.TryAttack(Runtime.PlayerTransform);
                return;
            }

            Combat.CancelPendingAttack();
            if (!HasLastKnown)
            {
                IsPeeking = false;
                Mover.Stop();
                SetState(RoleState.Patrol);
                return;
            }

            if (now - LastSeenTime < HoldSeconds)
            {
                if (HasCoverPosition && !IsSheltered(CoverGoal, LastKnownPosition))
                    EvaluateCover(now);

                SetState(RoleState.Search);
                SearchFromCover(now);
                return;
            }

            IsPeeking = false;
            peekHoldUntil = 0;
            ReturnToPost();
        }

        private void EvaluateCover(float now)
        {
            CoverEvaluations++;
            nextAssessment = now + Mathf.Max(.5f, ReassessSeconds);
            HasCoverPosition = false;

            if (!Runtime.PlayerTransform || !Mover.OnNavigation)
            {
                CoverGoal = transform.position;
                return;
            }

            float bestScore = float.NegativeInfinity;
            Vector3 bestPosition = transform.position;
            var tested = new HashSet<Vector3Int>();
            ConsiderCoverPosition(transform.position, ref bestScore, ref bestPosition, tested);
            ConsiderCoverPosition(Post, ref bestScore, ref bestPosition, tested);

            Vector3 scanCenter = transform.position + Vector3.up;
            Collider[] nearby = Physics.OverlapSphere(
                scanCenter, CoverSearchRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider obstacle in nearby)
            {
                if (!IsPotentialCover(obstacle)) continue;

                Bounds bounds = obstacle.bounds;
                float y = transform.position.y;
                ConsiderCoverPosition(new Vector3(bounds.min.x - .75f, y, bounds.center.z),
                    ref bestScore, ref bestPosition, tested);
                ConsiderCoverPosition(new Vector3(bounds.max.x + .75f, y, bounds.center.z),
                    ref bestScore, ref bestPosition, tested);
                ConsiderCoverPosition(new Vector3(bounds.center.x, y, bounds.min.z - .75f),
                    ref bestScore, ref bestPosition, tested);
                ConsiderCoverPosition(new Vector3(bounds.center.x, y, bounds.max.z + .75f),
                    ref bestScore, ref bestPosition, tested);
            }

            if (HasCoverPosition) CoverGoal = bestPosition;
            else CoverGoal = transform.position;

            GameplayDiagnostics.Trace(
                $"CD cover={CoverGoal} safe={HasCoverPosition} evaluations={CoverEvaluations} lastKnown={LastKnownPosition}",
                this);
        }

        private bool IsPotentialCover(Collider obstacle)
        {
            if (!obstacle || !obstacle.enabled || obstacle.isTrigger) return false;
            if (obstacle.transform.IsChildOf(transform)) return false;
            if (Runtime.PlayerTransform && obstacle.transform.IsChildOf(Runtime.PlayerTransform)) return false;
            if (obstacle.GetComponentInParent<EnemyCombatAdapter>()) return false;
            return obstacle.bounds.size.y >= .75f;
        }

        private void ConsiderCoverPosition(
            Vector3 point, ref float bestScore, ref Vector3 bestPosition, HashSet<Vector3Int> tested)
        {
            var key = new Vector3Int(
                Mathf.RoundToInt(point.x * 2),
                Mathf.RoundToInt(point.y * 2),
                Mathf.RoundToInt(point.z * 2));
            if (!tested.Add(key)) return;

            if (!Reachable(point, out Vector3 candidate, out float pathLength) ||
                pathLength > MaximumCoverPath ||
                Vector3.Distance(candidate, LastKnownPosition) < MinimumCoverDistance ||
                Vector3.Distance(candidate, LastKnownPosition) > SightRange ||
                !IsSheltered(candidate, LastKnownPosition))
                return;

            float playerDistance = Vector3.Distance(candidate, LastKnownPosition);
            float travelDistance = Vector3.Distance(transform.position, candidate);
            float score = 100
                - pathLength * 1.5f
                - Mathf.Abs(playerDistance - PreferredCoverDistance) * 1.5f
                - travelDistance * .25f;
            if (score <= bestScore) return;

            bestScore = score;
            bestPosition = candidate;
            HasCoverPosition = true;
        }

        private bool IsSheltered(Vector3 position, Vector3 threatPosition)
        {
            Vector3 origin = position + Vector3.up * 1.4f;
            Vector3 target = threatPosition + Vector3.up;
            Vector3 delta = target - origin;
            if (delta.sqrMagnitude < .01f) return false;

            foreach (RaycastHit hit in Physics.RaycastAll(
                origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsAgentOrPlayer(hit.collider)) continue;
                return true;
            }

            return false;
        }

        private bool HasClearView(Vector3 position, Vector3 targetPosition)
        {
            Vector3 origin = position + Vector3.up * 1.4f;
            Vector3 target = targetPosition + Vector3.up;
            Vector3 delta = target - origin;
            if (delta.sqrMagnitude < .01f) return true;

            foreach (RaycastHit hit in Physics.RaycastAll(
                origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsAgentOrPlayer(hit.collider)) continue;
                return false;
            }

            return true;
        }

        private bool IsAgentOrPlayer(Collider collider)
        {
            if (collider.transform.IsChildOf(transform)) return true;
            if (Runtime.PlayerTransform && collider.transform.IsChildOf(Runtime.PlayerTransform)) return true;
            return collider.GetComponentInParent<EnemyCombatAdapter>() != null;
        }

        private void SearchFromCover(float now)
        {
            if (!HasCoverPosition)
            {
                Mover.Stop();
                transform.Rotate(0, 18 * Time.deltaTime, 0);
                return;
            }

            if (IsPeeking)
            {
                if (Mover.Status != MoveStatus.Arrived)
                {
                    Mover.RequestMove(PeekGoal, .2f);
                    return;
                }

                if (peekHoldUntil <= 0) peekHoldUntil = now + PeekHoldSeconds;
                FaceRememberedPosition();
                if (now < peekHoldUntil) return;

                IsPeeking = false;
                peekHoldUntil = 0;
                nextPeek = now + PeekInterval;
                Mover.RequestMove(CoverGoal, .5f);
                return;
            }

            if (Vector3.Distance(transform.position, CoverGoal) > .65f)
            {
                Mover.RequestMove(CoverGoal, .5f);
                return;
            }

            Mover.Stop();
            if (now >= nextPeek)
            {
                if (TryFindPeekPosition(out Vector3 peekPosition))
                {
                    PeekGoal = peekPosition;
                    IsPeeking = true;
                    peekHoldUntil = 0;
                    Mover.RequestMove(PeekGoal, .2f);
                    return;
                }

                nextPeek = now + PeekInterval;
            }

            transform.Rotate(0, 18 * Time.deltaTime, 0);
        }

        private bool TryFindPeekPosition(out Vector3 peekPosition)
        {
            peekPosition = default;
            Vector3 towardThreat = LastKnownPosition - CoverGoal;
            towardThreat.y = 0;
            if (towardThreat.sqrMagnitude < .01f) return false;
            towardThreat.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, towardThreat);
            Vector3[] offsets =
            {
                side * PeekDistance,
                -side * PeekDistance,
                towardThreat * PeekDistance,
                (towardThreat + side).normalized * PeekDistance
            };

            float shortestPath = float.PositiveInfinity;
            foreach (Vector3 offset in offsets)
            {
                Vector3 point = CoverGoal + offset;
                if (!HasClearView(point, LastKnownPosition) ||
                    !Reachable(point, out Vector3 candidate, out float pathLength) ||
                    pathLength > PeekDistance * 3 ||
                    pathLength >= shortestPath)
                    continue;

                shortestPath = pathLength;
                peekPosition = candidate;
            }

            return !float.IsPositiveInfinity(shortestPath);
        }

        public override void ResetEnemyActions()
        {
            base.ResetEnemyActions();
            nextAssessment = 0;
            nextPeek = 0;
            peekHoldUntil = 0;
            CoverGoal = Post;
            PeekGoal = default;
            CoverEvaluations = 0;
            HasCoverPosition = false;
            IsPeeking = false;
        }
    }
}
