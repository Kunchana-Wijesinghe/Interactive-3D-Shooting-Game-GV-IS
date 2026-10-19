using System;
using System.Collections.Generic;
using FacilityBreach;
using FacilityBreach.AI.Patrol;
using Perera.SoundHunter;
using UnityEngine;

namespace FacilityBreach.AI.Roles
{
    /// <summary>
    /// Autonomous Patrol Guard agent (PG-01) for the facility security zone.
    /// Uses authored A* graph navigation for corridor and doorway patrol routes,
    /// integrated with local NavMesh movement, visual perception, combat, and alert states.
    /// Milestone 2.2A: Enhanced lost-sight memory and deterministic multi-point search.
    /// </summary>
    public sealed class PatrolGuardBrain : RoleDecisionBrain
    {
        public Vector3[] Route;
        public float DetectionSeconds = .5f;
        public float SearchSeconds = 4f;
        public bool Detected { get; private set; }

        [Header("A* Navigation Configuration & Diagnostics")]
        [Tooltip("When enabled, emits verbose A* route planning and traversal diagnostic logs.")]
        public bool EnableAStarDiagnostics = false;

        [Header("Search & Memory Configuration")]
        [Tooltip("Radius around last-known position to inspect for candidate search nodes.")]
        public float SearchRadius = 6.0f;
        [Tooltip("Maximum candidate search points to inspect around last-known position during the 4-second search window.")]
        public int MaxSearchPoints = 3;
        [Tooltip("When enabled, emits verbose search pattern and lost-sight memory diagnostic logs.")]
        public bool EnableSearchDiagnostics = false;

        [Header("Return Configuration & Diagnostics")]
        [Tooltip("When enabled, emits verbose return candidate evaluation and traversal diagnostic logs.")]
        public bool EnableReturnDiagnostics = false;

        [Header("Dynamic Obstacle & Rerouting Configuration")]
        [Tooltip("When enabled, emits verbose dynamic obstacle detection, edge blocking, and replanning diagnostic logs.")]
        public bool EnableObstacleDiagnostics = false;
        [Tooltip("Maximum detour replan attempts allowed for a single target waypoint before advancing.")]
        public int MaxReplansPerTarget = 2;
        [Tooltip("Minimum duration in seconds between successive replanning attempts to avoid tight repath churn.")]
        public float ReplanCooldown = 1.0f;
        [Tooltip("Probe interval in seconds between corridor clearance verification checks.")]
        public float ObstacleProbeInterval = 4.0f;

        private PatrolAStarGraph graph;
        private List<Vector3> activeAStarPath;
        private List<int> activeAStarNodeIds;
        private int pathIndex;
        private int currentTargetWaypointIndex = -1;

        private float sightStarted = -1f;
        private float searchUntil;
        private int waypoint;

        // Milestone 2.2A: Search & Memory State
        private List<Vector3> activeSearchPoints;
        private int currentSearchPointIndex;
        private Vector3 lastInvestigatedPosition;
        private float lastInvestigatedTime;

        // Milestone 2.2B: Tactical Return State
        private List<PatrolReturnCandidate> rankedReturnCandidates;
        private int currentReturnCandidateIndex = -1;
        private Vector3 currentReturnTargetPos;

        // Milestone 2.3: Dynamic Obstacle & Rerouting State
        public sealed class BlockedEdgeRecord
        {
            public int FromNode { get; }
            public int ToNode { get; }
            public float BlockedTimestamp { get; }
            public float NextProbeTime { get; set; }

            public BlockedEdgeRecord(int from, int to, float now, float probeInterval = 4.0f)
            {
                FromNode = from;
                ToNode = to;
                BlockedTimestamp = now;
                NextProbeTime = now + probeInterval;
            }
        }

        private readonly List<BlockedEdgeRecord> activeBlockedEdges = new List<BlockedEdgeRecord>();
        private int replanAttemptsForCurrentTarget = 0;
        private float lastReplanTime = -10f;
        private float nextReplanAllowedTime = 0f;
        private bool isReplanPending = false;
        public Func<int, int, bool> EdgeClearanceProbeOverride;

        public PatrolAStarGraph Graph
        {
            get => graph;
            set => graph = value;
        }

        public IReadOnlyList<int> CurrentPathNodeIds => activeAStarNodeIds;
        public IReadOnlyList<Vector3> CurrentPathWaypoints => activeAStarPath;
        public int CurrentPathIndex => pathIndex;
        public int CurrentWaypointIndex => waypoint;
        public bool HasActiveAStarPath => activeAStarPath != null && pathIndex < activeAStarPath.Count;

        public Vector3 LastInvestigatedPosition => lastInvestigatedPosition;
        public float LastInvestigatedTime => lastInvestigatedTime;
        public IReadOnlyList<Vector3> ActiveSearchPoints => activeSearchPoints;
        public int CurrentSearchPointIndex => currentSearchPointIndex;
        public float SearchTimeRemaining => Mathf.Max(0f, searchUntil - (Runtime ? Runtime.GameplayTime : Time.time));

        // Milestone 2.2B: Return State Properties
        public IReadOnlyList<PatrolReturnCandidate> RankedReturnCandidates => rankedReturnCandidates;
        public int CurrentReturnCandidateIndex => currentReturnCandidateIndex;
        public Vector3 CurrentReturnTargetPos => currentReturnTargetPos;
        public PatrolReturnCandidate ActiveReturnCandidate =>
            (rankedReturnCandidates != null && currentReturnCandidateIndex >= 0 && currentReturnCandidateIndex < rankedReturnCandidates.Count)
                ? rankedReturnCandidates[currentReturnCandidateIndex]
                : null;

        // Milestone 2.3: Obstacle & Rerouting Properties
        public IReadOnlyList<BlockedEdgeRecord> ActiveBlockedEdges => activeBlockedEdges;
        public bool IsReplanPending => isReplanPending;
        public int CurrentReplanAttempts => replanAttemptsForCurrentTarget;
        public float NextReplanAllowedTime => nextReplanAllowedTime;

        protected override void Start()
        {
            base.Start();
            SightRange = 13;
            FieldOfView = 110;
            Combat.Range = 12;

            if (graph == null)
            {
                graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            }

            if (Route != null && Route.Length > 0)
            {
                ValidateRouteAgainstGraph(out var diagnostic);
                TraceAStar($"Initial route validation: {diagnostic}");
            }
        }

        protected override void Decide(float now)
        {
            // 1. Perception Reacquisition & Combat
            if (SeesPlayer)
            {
                if (State == RoleState.Investigate || State == RoleState.Search || State == RoleState.Return)
                {
                    TraceReturn($"Player reacquired at {LastKnownPosition} during {State}. Cancelling return/search/investigation.");
                }

                ClearActivePath();
                ClearSearchState();
                ClearReturnState();

                if (sightStarted < 0) sightStarted = now;

                if (!Detected && now - sightStarted < DetectionSeconds)
                {
                    SetState(RoleState.Detecting);
                    Mover?.Stop();
                    return;
                }

                Detected = true;
                SetState(RoleState.Combat);

                if (Vector3.Distance(transform.position, LastKnownPosition) > 10)
                {
                    Mover.RequestMove(LastKnownPosition, 10);
                }
                else
                {
                    Mover.Stop();
                }

                Combat.TryAttack(Runtime.PlayerTransform);
                return;
            }

            sightStarted = -1;
            Combat.CancelPendingAttack();

            // 2. Sight Lost Transition: from Detected/Combat to Investigate
            if (Detected)
            {
                Detected = false;
                Mover?.Stop();
                SetState(RoleState.Investigate);
                ClearActivePath();
                ClearSearchState();
                ClearReturnState();
                lastInvestigatedPosition = LastKnownPosition;
                lastInvestigatedTime = now;
                TraceSearch($"Lost sight of player. Entering Investigate state for position {LastKnownPosition} (recorded at t={now:F2}).");
            }

            // 3. Investigate State: navigate to LastKnownPosition
            if (State == RoleState.Investigate)
            {
                bool arrivedAtTarget = (Mover.Status == MoveStatus.Arrived && activeAStarPath != null && pathIndex >= activeAStarPath.Count) ||
                                       (Vector3.Distance(transform.position, LastKnownPosition) <= 0.6f && (activeAStarPath == null || pathIndex >= (activeAStarPath?.Count ?? 0)));

                if (arrivedAtTarget)
                {
                    TraceSearch($"Arrived at investigation target {LastKnownPosition}. Initiating {SearchSeconds:F1}s multi-point search.");
                    Mover.Stop();
                    ClearActivePath();
                    searchUntil = now + SearchSeconds;
                    InitSearchPoints(now);
                    SetState(RoleState.Search);
                    return;
                }

                if (Mover.Status == MoveStatus.Failed)
                {
                    TraceSearch($"Movement to investigation target {LastKnownPosition} failed ({Mover.FailureReason}). Falling back to search phase.");
                    Mover.Stop();
                    ClearActivePath();
                    searchUntil = now + SearchSeconds;
                    InitSearchPoints(now);
                    SetState(RoleState.Search);
                    return;
                }

                // Intermediate waypoint arrival
                if (Mover.Status == MoveStatus.Arrived && activeAStarPath != null && pathIndex < activeAStarPath.Count)
                {
                    TraceAStar($"Arrived at intermediate investigate node (Index {pathIndex}/{activeAStarPath.Count - 1}, Pos={activeAStarPath[pathIndex]}).");
                    pathIndex++;
                    Mover.Stop();
                    if (pathIndex >= activeAStarPath.Count)
                    {
                        TraceSearch($"Arrived at investigation target {LastKnownPosition}. Initiating {SearchSeconds:F1}s multi-point search.");
                        Mover.Stop();
                        ClearActivePath();
                        searchUntil = now + SearchSeconds;
                        InitSearchPoints(now);
                        SetState(RoleState.Search);
                        return;
                    }
                }

                // Plan path to LastKnownPosition if needed
                if (activeAStarPath == null || activeAStarPath.Count == 0)
                {
                    PlanPathToPosition(LastKnownPosition, "InvestigateTarget");
                }

                // Stream next waypoint if Mover is idle
                if (activeAStarPath != null && pathIndex < activeAStarPath.Count && Mover.Status != MoveStatus.Moving)
                {
                    Vector3 nextStep = activeAStarPath[pathIndex];
                    float stop = (pathIndex == activeAStarPath.Count - 1) ? 0.6f : 0.5f;
                    TraceAStar($"Requesting move to investigate waypoint {pathIndex}/{activeAStarPath.Count - 1} at {nextStep}");
                    Mover.RequestMove(nextStep, stop);
                }
                return;
            }

            // 4. Search State: multi-point search around LastKnownPosition
            if (State == RoleState.Search)
            {
                // Strict timeout: SearchSeconds = 4f
                if (now >= searchUntil)
                {
                    TraceSearch($"Search duration ({SearchSeconds:F1}s) completed. Initiating tactical return to patrol.");
                    ClearSearchState();
                    ClearActivePath();
                    HasLastKnown = false;
                    InitiateReturnToPatrol(now);
                    return;
                }

                // If no search points or all candidates have been visited: rotational sweep
                if (activeSearchPoints == null || activeSearchPoints.Count == 0 || currentSearchPointIndex >= activeSearchPoints.Count)
                {
                    ClearActivePath();
                    Mover.Scan();
                    return;
                }

                Vector3 currentSearchTarget = activeSearchPoints[currentSearchPointIndex];

                // Arrival at candidate search point
                bool arrivedAtPoint = (Mover.Status == MoveStatus.Arrived && activeAStarPath != null && pathIndex >= activeAStarPath.Count) ||
                                      (Vector3.Distance(transform.position, currentSearchTarget) <= 0.6f && (activeAStarPath == null || pathIndex >= (activeAStarPath?.Count ?? 0)));

                if (arrivedAtPoint)
                {
                    TraceSearch($"Arrived at search candidate {currentSearchPointIndex + 1}/{activeSearchPoints.Count} at {currentSearchTarget}.");
                    currentSearchPointIndex++;
                    Mover.Stop();
                    ClearActivePath();
                    Mover.Scan();
                    return;
                }

                if (Mover.Status == MoveStatus.Failed)
                {
                    TraceSearch($"Failed to reach search candidate {currentSearchPointIndex + 1}/{activeSearchPoints.Count} ({Mover.FailureReason}). Advancing to next point.");
                    currentSearchPointIndex++;
                    Mover.Stop();
                    ClearActivePath();
                    return;
                }

                // Intermediate waypoint arrival along path to search point
                if (Mover.Status == MoveStatus.Arrived && activeAStarPath != null && pathIndex < activeAStarPath.Count)
                {
                    TraceAStar($"Arrived at intermediate search node (Index {pathIndex}/{activeAStarPath.Count - 1}, Pos={activeAStarPath[pathIndex]}).");
                    pathIndex++;
                    Mover.Stop();
                    if (pathIndex >= activeAStarPath.Count)
                    {
                        TraceSearch($"Arrived at search candidate {currentSearchPointIndex + 1}/{activeSearchPoints.Count} at {currentSearchTarget}.");
                        currentSearchPointIndex++;
                        Mover.Stop();
                        ClearActivePath();
                        Mover.Scan();
                        return;
                    }
                }

                // Plan path to current search target if not planned
                if (activeAStarPath == null || activeAStarPath.Count == 0)
                {
                    PlanPathToPosition(currentSearchTarget, $"SearchPoint[{currentSearchPointIndex}]");
                }

                // Stream next waypoint if Mover is idle
                if (activeAStarPath != null && pathIndex < activeAStarPath.Count && Mover.Status != MoveStatus.Moving)
                {
                    Vector3 nextStep = activeAStarPath[pathIndex];
                    float stop = (pathIndex == activeAStarPath.Count - 1) ? 0.6f : 0.5f;
                    TraceAStar($"Requesting move to search waypoint {pathIndex}/{activeAStarPath.Count - 1} at {nextStep}");
                    Mover.RequestMove(nextStep, stop);
                }
                return;
            }

            // 5. Return State: tactical return to nearest reachable patrol route waypoint
            if (State == RoleState.Return)
            {
                ExecuteReturnNavigation();
                return;
            }

            // 6. Detecting State fallback
            if (State == RoleState.Detecting)
            {
                SetState(RoleState.Patrol);
            }

            // 7. Patrol State Navigation via Authored A* Graph
            ExecutePatrolNavigation(now);
        }

        private void ExecutePatrolNavigation(float now)
        {
            if (Route == null || Route.Length == 0)
            {
                Mover?.Stop();
                ClearActivePath();
                return;
            }

            if (waypoint < 0 || waypoint >= Route.Length)
            {
                waypoint = 0;
            }

            Vector3 target = Route[waypoint];

            // 1. Update dynamic blocked edge clearance probes
            UpdateBlockedEdgeClearance(now);

            // 2. Resume pending replan if cooldown has elapsed
            if (isReplanPending)
            {
                if (now < nextReplanAllowedTime)
                {
                    // Deferral phase: hold position, zero path requests, zero CPU churn
                    return;
                }

                isReplanPending = false;
                ExecuteDetourReplan(now, waypoint);
                return;
            }

            // 3. If already within arrival distance of the target waypoint and route has multiple points, advance to next
            if (Route.Length > 1 && Vector3.Distance(transform.position, target) <= 0.6f && (activeAStarPath == null || pathIndex >= activeAStarPath.Count))
            {
                TraceAStar($"Already within arrival tolerance of route waypoint {waypoint} ({target}). Advancing to next waypoint.");
                waypoint = (waypoint + 1) % Route.Length;
                target = Route[waypoint];
                replanAttemptsForCurrentTarget = 0;
                isReplanPending = false;
                ClearActivePath();
            }

            // 4. Handle movement failure
            if (Mover != null && Mover.Status == MoveStatus.Failed)
            {
                string reason = Mover.FailureReason;
                TraceAStar($"Mover failed along path (Reason: {reason}).");

                if (IsObstructionFailure(reason))
                {
                    HandleObstructionFailure(now, waypoint);
                }
                else
                {
                    // Transient non-obstruction failure (e.g. agent/goal outside navigation, setpath rejected)
                    // Do NOT block any graph edges. Advance to next waypoint cleanly.
                    Mover.Stop();
                    waypoint = (waypoint + 1) % Route.Length;
                    replanAttemptsForCurrentTarget = 0;
                    isReplanPending = false;
                    ClearActivePath();
                }
                return;
            }

            // 5. Handle intermediate or final waypoint arrival
            if (Mover != null && Mover.Status == MoveStatus.Arrived && activeAStarPath != null && activeAStarPath.Count > 0)
            {
                TraceAStar($"Arrived at intermediate path node (Index {pathIndex}/{activeAStarPath.Count - 1}, Pos={activeAStarPath[pathIndex]}).");
                pathIndex++;
                Mover.Stop();

                if (pathIndex >= activeAStarPath.Count)
                {
                    TraceAStar($"Completed full A* path to route waypoint {waypoint} ({target}). Advancing to next route waypoint.");
                    waypoint = (waypoint + 1) % Route.Length;
                    replanAttemptsForCurrentTarget = 0;
                    isReplanPending = false;
                    ClearActivePath();
                    return;
                }
            }

            // 6. Plan a new A* path if needed or if target changed
            if (activeAStarPath == null || activeAStarPath.Count == 0 || currentTargetWaypointIndex != waypoint)
            {
                replanAttemptsForCurrentTarget = 0;
                isReplanPending = false;
                PlanPathToWaypoint(waypoint);
            }

            // 7. Sequentially stream next waypoint to Mover if not currently moving
            if (activeAStarPath != null && pathIndex < activeAStarPath.Count)
            {
                if (Mover != null && Mover.Status != MoveStatus.Moving)
                {
                    Vector3 nextStep = activeAStarPath[pathIndex];
                    TraceAStar($"Requesting move to waypoint {pathIndex}/{activeAStarPath.Count - 1} at {nextStep}");
                    Mover.RequestMove(nextStep, .5f);
                }
            }
        }

        private void PlanPathToWaypoint(int targetWaypointIndex)
        {
            currentTargetWaypointIndex = targetWaypointIndex;
            Vector3 targetPos = Route[targetWaypointIndex];
            PlanPathToPosition(targetPos, $"Route[{targetWaypointIndex}]", targetWaypointIndex);
        }

        private void PlanPathToPosition(Vector3 targetPos, string targetLabel, int routeIndex = -1)
        {
            currentTargetWaypointIndex = routeIndex;

            if (graph == null)
            {
                graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            }

            int startNode = graph.GetNearestNode(transform.position, verifiedOnly: true);
            int goalNode = graph.GetNearestNode(targetPos, verifiedOnly: true);

            if (startNode < 0 || goalNode < 0)
            {
                TraceAStar($"Nearest nodes not found for {targetLabel} (start={startNode}, goal={goalNode}). Falling back to direct movement.");
                activeAStarNodeIds = new List<int>();
                activeAStarPath = new List<Vector3> { targetPos };
                pathIndex = 0;
                return;
            }

            // If goal node is too far (> 5m) from targetPos, use direct movement
            if (Vector3.Distance(targetPos, graph.GetNode(goalNode).Position) > 5f)
            {
                TraceAStar($"Goal node {goalNode} is >5m from target {targetPos}. Direct movement used for {targetLabel}.");
                activeAStarNodeIds = new List<int>();
                activeAStarPath = new List<Vector3> { targetPos };
                pathIndex = 0;
                return;
            }

            var result = PatrolAStarPathfinder.FindPath(graph, startNode, goalNode);
            if (!result.Success)
            {
                TraceAStar($"A* search failed for {targetLabel} from node {startNode} to {goalNode}: {result.FailureReason}. Fallback to direct navigation.");
                activeAStarNodeIds = new List<int>();
                activeAStarPath = new List<Vector3> { targetPos };
                pathIndex = 0;
                return;
            }

            activeAStarNodeIds = new List<int>(result.NodeIds);
            activeAStarPath = new List<Vector3>(result.Waypoints);

            if (Vector3.Distance(result.Waypoints[result.Waypoints.Count - 1], targetPos) > 0.2f)
            {
                activeAStarPath.Add(targetPos);
            }

            // Determine starting path index:
            // If already standing within arrival distance of the start node, advance to waypoint 1 directly.
            if (activeAStarPath.Count > 1 && Vector3.Distance(transform.position, activeAStarPath[0]) <= 0.6f)
            {
                pathIndex = 1;
            }
            else
            {
                pathIndex = 0;
            }

            string nodeSeq = string.Join(" -> ", result.NodeIds);
            TraceAStar($"Planned path for {targetLabel} ({targetPos}): nodes [{nodeSeq}], total cost={result.TotalCost:F2}, explored={result.NodesExplored}");
        }

        private void InitSearchPoints(float now)
        {
            if (graph == null) graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();

            activeSearchPoints = PatrolSearchPlanner.SelectSearchPoints(
                graph,
                LastKnownPosition,
                transform.position,
                searchRadius: SearchRadius,
                maxPoints: MaxSearchPoints,
                isPointReachable: IsSearchPointReachable
            );

            currentSearchPointIndex = 0;
            ClearActivePath();

            string pointsDesc = (activeSearchPoints != null && activeSearchPoints.Count > 0)
                ? string.Join(", ", activeSearchPoints)
                : "None (using rotational fallback)";
            TraceSearch($"Generated {activeSearchPoints?.Count ?? 0} candidate search points around {LastKnownPosition}: [{pointsDesc}]");
        }

        private bool IsSearchPointReachable(Vector3 point)
        {
            if (Mover == null || !Mover.OnNavigation)
            {
                // In environments without active NavMeshAgent, rely on verified graph coordinates
                return true;
            }
            return Reachable(point, out _, out _);
        }

        public bool ValidateRouteAgainstGraph(out string diagnostic)
        {
            if (graph == null) graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();
            if (Route == null || Route.Length == 0)
            {
                diagnostic = "Route is null or empty.";
                return false;
            }

            for (int i = 0; i < Route.Length; i++)
            {
                int nearest = graph.GetNearestNode(Route[i], verifiedOnly: true);
                if (nearest < 0)
                {
                    diagnostic = $"Route waypoint {i} ({Route[i]}) has no nearby verified graph node.";
                    return false;
                }

                float dist = Vector3.Distance(Route[i], graph.GetNode(nearest).Position);
                if (dist > 5f)
                {
                    diagnostic = $"Route waypoint {i} ({Route[i]}) is {dist:F1}m away from nearest graph node {nearest}.";
                    return false;
                }

                int nextIdx = (i + 1) % Route.Length;
                int nextNearest = graph.GetNearestNode(Route[nextIdx], verifiedOnly: true);
                var pathResult = PatrolAStarPathfinder.FindPath(graph, nearest, nextNearest);
                if (!pathResult.Success)
                {
                    diagnostic = $"Route segment {i}->{nextIdx} (node {nearest}->{nextNearest}) has no traversable path: {pathResult.FailureReason}";
                    return false;
                }
            }

            diagnostic = $"Route verified with {Route.Length} valid connected waypoints on security zone graph.";
            return true;
        }

        public void SetRoute(Vector3[] newRoute)
        {
            Route = newRoute;
            waypoint = 0;
            ClearActivePath();
            if (Route != null && Route.Length > 0)
            {
                ValidateRouteAgainstGraph(out var diagnostic);
                TraceAStar($"SetRoute validation: {diagnostic}");
            }
        }

        private void ClearActivePath()
        {
            activeAStarPath = null;
            activeAStarNodeIds = null;
            pathIndex = 0;
            currentTargetWaypointIndex = -1;
        }

        private void ClearSearchState()
        {
            activeSearchPoints = null;
            currentSearchPointIndex = 0;
            searchUntil = 0;
        }

        private void ClearReturnState()
        {
            rankedReturnCandidates = null;
            currentReturnCandidateIndex = -1;
            currentReturnTargetPos = Vector3.zero;
        }

        private void InitiateReturnToPatrol(float now)
        {
            if (graph == null) graph = FacilityPatrolGraphFactory.CreateSecurityZoneGraph();

            if (Route == null || Route.Length == 0)
            {
                TraceReturn("No authored patrol route found. Defaulting to Patrol state at current position.");
                Mover?.Stop();
                ClearReturnState();
                ClearActivePath();
                SetState(RoleState.Patrol);
                return;
            }

            rankedReturnCandidates = PatrolReturnPlanner.EvaluateReturnCandidates(
                graph,
                Route,
                transform.position,
                isPointReachable: IsReturnPointReachable
            );

            currentReturnCandidateIndex = 0;

            var bestCandidate = PatrolReturnPlanner.SelectBestCandidate(rankedReturnCandidates);
            if (bestCandidate == null)
            {
                TraceReturn("No reachable return candidates found. Gracefully resuming Patrol at waypoint 0.");
                Mover?.Stop();
                ClearReturnState();
                ClearActivePath();
                waypoint = 0;
                SetState(RoleState.Patrol);
                return;
            }

            currentReturnCandidateIndex = rankedReturnCandidates.IndexOf(bestCandidate);

            // Check if already standing within arrival tolerance of this candidate
            float directDist = Vector3.Distance(transform.position, bestCandidate.Position);
            if (directDist <= PatrolReturnPlanner.ArrivalTolerance)
            {
                TraceReturn($"Already within arrival tolerance ({directDist:F2}m <= {PatrolReturnPlanner.ArrivalTolerance:F2}m) of Route[{bestCandidate.RouteIndex}] ({bestCandidate.Position}). Resuming Patrol directly.");
                waypoint = bestCandidate.RouteIndex;
                Mover?.Stop();
                ClearReturnState();
                ClearActivePath();
                SetState(RoleState.Patrol);
                return;
            }

            SetState(RoleState.Return);
            EngageReturnCandidate(bestCandidate);
        }

        private void EngageReturnCandidate(PatrolReturnCandidate candidate)
        {
            if (candidate == null || !candidate.IsReachable)
            {
                TryNextReturnCandidate();
                return;
            }

            currentReturnTargetPos = candidate.Position;
            activeAStarNodeIds = new List<int>(candidate.NodeIds);
            activeAStarPath = new List<Vector3>(candidate.Waypoints);

            if (activeAStarPath.Count > 1 && Vector3.Distance(transform.position, activeAStarPath[0]) <= 0.6f)
            {
                pathIndex = 1;
            }
            else
            {
                pathIndex = 0;
            }

            TraceReturn($"Engaged return to Route[{candidate.RouteIndex}] ({candidate.Position}) with cost {candidate.TotalCost:F2}. Path steps: {activeAStarPath.Count}");
            Mover?.Stop();
        }

        private void TryNextReturnCandidate()
        {
            currentReturnCandidateIndex++;
            while (rankedReturnCandidates != null && currentReturnCandidateIndex < rankedReturnCandidates.Count)
            {
                var nextCandidate = rankedReturnCandidates[currentReturnCandidateIndex];
                if (nextCandidate.IsReachable)
                {
                    TraceReturn($"Retrying return navigation with candidate #{currentReturnCandidateIndex}: Route[{nextCandidate.RouteIndex}] ({nextCandidate.Position}).");
                    EngageReturnCandidate(nextCandidate);
                    return;
                }
                currentReturnCandidateIndex++;
            }

            TraceReturn("All return candidates exhausted or unreachable. Gracefully falling back to Patrol state at waypoint 0.");
            Mover?.Stop();
            ClearReturnState();
            ClearActivePath();
            waypoint = 0;
            SetState(RoleState.Patrol);
        }

        private void ExecuteReturnNavigation()
        {
            var currentCandidate = ActiveReturnCandidate;
            if (currentCandidate == null || !currentCandidate.IsReachable)
            {
                TryNextReturnCandidate();
                return;
            }

            // Check arrival at return destination
            bool arrivedAtDestination = (Mover != null && Mover.Status == MoveStatus.Arrived && activeAStarPath != null && pathIndex >= activeAStarPath.Count) ||
                                        (Vector3.Distance(transform.position, currentCandidate.Position) <= PatrolReturnPlanner.ArrivalTolerance && (activeAStarPath == null || pathIndex >= (activeAStarPath?.Count ?? 0)));

            if (arrivedAtDestination)
            {
                TraceReturn($"Arrived at return patrol waypoint Route[{currentCandidate.RouteIndex}] ({currentCandidate.Position}). Resuming normal patrol.");
                waypoint = currentCandidate.RouteIndex;
                Mover?.Stop();
                ClearReturnState();
                ClearActivePath();
                SetState(RoleState.Patrol);
                return;
            }

            // Handle movement failure: try next candidate
            if (Mover != null && Mover.Status == MoveStatus.Failed)
            {
                TraceReturn($"Movement to return target Route[{currentCandidate.RouteIndex}] failed ({Mover.FailureReason}). Attempting next candidate.");
                Mover?.Stop();
                ClearActivePath();
                TryNextReturnCandidate();
                return;
            }

            // Intermediate waypoint arrival along return path
            if (Mover != null && Mover.Status == MoveStatus.Arrived && activeAStarPath != null && pathIndex < activeAStarPath.Count)
            {
                TraceAStar($"Arrived at intermediate return node (Index {pathIndex}/{activeAStarPath.Count - 1}, Pos={activeAStarPath[pathIndex]}).");
                pathIndex++;
                Mover?.Stop();
                if (pathIndex >= activeAStarPath.Count)
                {
                    TraceReturn($"Arrived at return patrol waypoint Route[{currentCandidate.RouteIndex}] ({currentCandidate.Position}). Resuming normal patrol.");
                    waypoint = currentCandidate.RouteIndex;
                    Mover?.Stop();
                    ClearReturnState();
                    ClearActivePath();
                    SetState(RoleState.Patrol);
                    return;
                }
            }

            // If path is somehow empty, re-engage
            if (activeAStarPath == null || activeAStarPath.Count == 0)
            {
                EngageReturnCandidate(currentCandidate);
            }

            // Stream next waypoint if Mover is idle
            if (activeAStarPath != null && pathIndex < activeAStarPath.Count && (Mover == null || Mover.Status != MoveStatus.Moving))
            {
                Vector3 nextStep = activeAStarPath[pathIndex];
                float stop = (pathIndex == activeAStarPath.Count - 1) ? 0.6f : 0.5f;
                TraceAStar($"Requesting move to return waypoint {pathIndex}/{activeAStarPath.Count - 1} at {nextStep}");
                Mover?.RequestMove(nextStep, stop);
            }
        }

        private bool IsReturnPointReachable(Vector3 point)
        {
            if (Mover == null || !Mover.OnNavigation)
            {
                return true;
            }
            return Reachable(point, out _, out _);
        }

        private void TraceAStar(string message)
        {
            if (EnableAStarDiagnostics)
            {
                Debug.Log($"[PG-01 A*] {message}", this);
            }
            else
            {
                GameplayDiagnostics.Trace($"[PG-01 A*] {message}", this);
            }
        }

        private void TraceSearch(string message)
        {
            if (EnableAStarDiagnostics || EnableSearchDiagnostics)
            {
                Debug.Log($"[PG-01 Search] {message}", this);
            }
            else
            {
                GameplayDiagnostics.Trace($"[PG-01 Search] {message}", this);
            }
        }

        private void TraceReturn(string message)
        {
            if (EnableAStarDiagnostics || EnableReturnDiagnostics)
            {
                Debug.Log($"[PG-01 Return] {message}", this);
            }
            else
            {
                GameplayDiagnostics.Trace($"[PG-01 Return] {message}", this);
            }
        }

        private void TraceObstacle(string message)
        {
            if (EnableAStarDiagnostics || EnableObstacleDiagnostics)
            {
                Debug.Log($"[PG-01 Reroute] {message}", this);
            }
            else
            {
                GameplayDiagnostics.Trace($"[PG-01 Reroute] {message}", this);
            }
        }

        // ====================================================================
        // Milestone 2.3: Dynamic Obstacle, Barricade Detection & Rerouting
        // ====================================================================

        public static bool IsObstructionFailure(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return false;
            return reason == "Path invalidated by obstruction" ||
                   reason == "Blocked/no movement progress" ||
                   reason == "No complete path";
        }

        public static bool TryDeriveActiveGraphEdge(IReadOnlyList<int> nodeIds, int currentPathIndex, PatrolAStarGraph graphInstance, out int fromNode, out int toNode)
        {
            fromNode = -1;
            toNode = -1;

            if (nodeIds == null || nodeIds.Count < 2)
                return false;

            if (currentPathIndex < 1 || currentPathIndex >= nodeIds.Count)
                return false;

            fromNode = nodeIds[currentPathIndex - 1];
            toNode = nodeIds[currentPathIndex];

            return fromNode != toNode &&
                   graphInstance != null &&
                   graphInstance.ContainsNode(fromNode) &&
                   graphInstance.ContainsNode(toNode);
        }

        public bool TryGetActiveGraphEdge(out int fromNode, out int toNode)
        {
            return TryDeriveActiveGraphEdge(activeAStarNodeIds, pathIndex, graph, out fromNode, out toNode);
        }

        public static bool EvaluateReplanAction(float now, float nextAllowedTime, int currentAttempts, int maxAttempts, out bool isDeferred, out bool isExhausted)
        {
            if (currentAttempts >= maxAttempts)
            {
                isExhausted = true;
                isDeferred = false;
                return false;
            }

            if (now < nextAllowedTime)
            {
                isExhausted = false;
                isDeferred = true;
                return false;
            }

            isExhausted = false;
            isDeferred = false;
            return true;
        }

        public void BlockEdge(int fromNode, int toNode, float now)
        {
            if (graph == null) return;

            graph.SetEdgeBlocked(fromNode, toNode, true);

            bool alreadyTracked = false;
            for (int i = 0; i < activeBlockedEdges.Count; i++)
            {
                var r = activeBlockedEdges[i];
                if ((r.FromNode == fromNode && r.ToNode == toNode) || (r.FromNode == toNode && r.ToNode == fromNode))
                {
                    alreadyTracked = true;
                    r.NextProbeTime = now + ObstacleProbeInterval;
                    break;
                }
            }

            if (!alreadyTracked)
            {
                activeBlockedEdges.Add(new BlockedEdgeRecord(fromNode, toNode, now, ObstacleProbeInterval));
            }
        }

        public void UnblockEdge(int fromNode, int toNode)
        {
            if (graph == null) return;

            graph.SetEdgeBlocked(fromNode, toNode, false);

            for (int i = activeBlockedEdges.Count - 1; i >= 0; i--)
            {
                var r = activeBlockedEdges[i];
                if ((r.FromNode == fromNode && r.ToNode == toNode) || (r.FromNode == toNode && r.ToNode == fromNode))
                {
                    activeBlockedEdges.RemoveAt(i);
                }
            }
        }

        private void HandleObstructionFailure(float now, int currentTargetIndex)
        {
            if (TryGetActiveGraphEdge(out int fromNode, out int toNode))
            {
                BlockEdge(fromNode, toNode, now);
                TraceObstacle($"Physical obstruction identified on edge ({fromNode} <-> {toNode}). Graph edge marked blocked.");
            }
            else
            {
                TraceObstacle("Obstruction failure occurred on off-graph connector or direct path segment. No graph edge blocked.");
            }

            ClearActivePath();
            Mover?.Stop();

            bool canReplan = EvaluateReplanAction(now, nextReplanAllowedTime, replanAttemptsForCurrentTarget, MaxReplansPerTarget, out bool isDeferred, out bool isExhausted);

            if (isExhausted)
            {
                TraceObstacle($"Replan budget exhausted ({replanAttemptsForCurrentTarget}/{MaxReplansPerTarget}) for Route[{currentTargetIndex}]. Advancing to next patrol waypoint.");
                waypoint = (waypoint + 1) % Route.Length;
                replanAttemptsForCurrentTarget = 0;
                isReplanPending = false;
                return;
            }

            if (isDeferred)
            {
                TraceObstacle($"Replan cooldown active. Deferring detour replan for Route[{currentTargetIndex}] until t={nextReplanAllowedTime:F2}.");
                isReplanPending = true;
                return;
            }

            if (canReplan)
            {
                ExecuteDetourReplan(now, currentTargetIndex);
            }
        }

        private void ExecuteDetourReplan(float now, int currentTargetIndex)
        {
            replanAttemptsForCurrentTarget++;
            lastReplanTime = now;
            nextReplanAllowedTime = now + ReplanCooldown;

            if (Route == null || currentTargetIndex < 0 || currentTargetIndex >= Route.Length)
            {
                waypoint = (waypoint + 1) % (Route?.Length > 0 ? Route.Length : 1);
                replanAttemptsForCurrentTarget = 0;
                isReplanPending = false;
                return;
            }

            Vector3 targetPos = Route[currentTargetIndex];
            int startNode = graph.GetNearestNode(transform.position, verifiedOnly: true);
            int goalNode = graph.GetNearestNode(targetPos, verifiedOnly: true);

            if (startNode < 0 || goalNode < 0)
            {
                TraceObstacle($"Cannot replan detour: nearest nodes not found (start={startNode}, goal={goalNode}). Advancing waypoint.");
                waypoint = (waypoint + 1) % Route.Length;
                replanAttemptsForCurrentTarget = 0;
                isReplanPending = false;
                return;
            }

            var result = PatrolAStarPathfinder.FindPath(graph, startNode, goalNode);
            if (!result.Success)
            {
                TraceObstacle($"No alternative detour path exists to Route[{currentTargetIndex}] (Start={startNode}, Goal={goalNode}, Reason={result.FailureReason}). Bypassing unreachable target.");
                waypoint = (waypoint + 1) % Route.Length;
                replanAttemptsForCurrentTarget = 0;
                isReplanPending = false;
                return;
            }

            activeAStarNodeIds = new List<int>(result.NodeIds);
            activeAStarPath = new List<Vector3>(result.Waypoints);

            if (Vector3.Distance(result.Waypoints[result.Waypoints.Count - 1], targetPos) > 0.2f)
            {
                activeAStarPath.Add(targetPos);
            }

            if (activeAStarPath.Count > 1 && Vector3.Distance(transform.position, activeAStarPath[0]) <= 0.6f)
            {
                pathIndex = 1;
            }
            else
            {
                pathIndex = 0;
            }

            string nodeSeq = string.Join(" -> ", result.NodeIds);
            TraceObstacle($"Successfully planned detour for Route[{currentTargetIndex}] ({targetPos}): nodes [{nodeSeq}], total cost={result.TotalCost:F2}, explored={result.NodesExplored}");

            if (pathIndex < activeAStarPath.Count)
            {
                Vector3 nextStep = activeAStarPath[pathIndex];
                Mover?.RequestMove(nextStep, .5f);
            }
        }

        public bool IsEdgePhysicallyClear(int fromNodeId, int toNodeId)
        {
            if (EdgeClearanceProbeOverride != null)
            {
                return EdgeClearanceProbeOverride(fromNodeId, toNodeId);
            }

            if (graph == null || !graph.TryGetNode(fromNodeId, out var nodeA) || !graph.TryGetNode(toNodeId, out var nodeB))
                return false;

            Vector3 posA = nodeA.Position;
            Vector3 posB = nodeB.Position;
            Vector3 dir = posB - posA;
            float dist = dir.magnitude;

            if (dist < 0.05f)
                return false;

            Vector3 dirNorm = dir / dist;

            // 1. Physical Corridor Clearance Probe (Capsule Cast)
            Vector3 bottom = posA + dirNorm * 0.35f + Vector3.up * 0.4f;
            Vector3 top = posA + dirNorm * 0.35f + Vector3.up * 1.4f;
            float castDist = Mathf.Max(0f, dist - 0.7f);

            if (castDist > 0.05f)
            {
                var hits = Physics.CapsuleCastAll(bottom, top, 0.35f, dirNorm, castDist, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length; i++)
                {
                    var col = hits[i].collider;
                    if (col == null) continue;

                    if (col.name.StartsWith("Barricade") ||
                        (col.attachedRigidbody != null && col.attachedRigidbody.name.StartsWith("Barricade")) ||
                        (col.transform.root != null && col.transform.root.name.StartsWith("Barricade")))
                    {
                        return false;
                    }

                    var door = col.GetComponentInParent<InteractiveDoor>();
                    if (door != null && !door.IsOpen)
                    {
                        return false;
                    }
                }
            }

            // 2. NavMesh Centerline Clearance Probe
            if (UnityEngine.AI.NavMesh.SamplePosition(posA, out var hitA, 1.0f, UnityEngine.AI.NavMesh.AllAreas) &&
                UnityEngine.AI.NavMesh.SamplePosition(posB, out var hitB, 1.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (UnityEngine.AI.NavMesh.Raycast(hitA.position, hitB.position, out var navHit, UnityEngine.AI.NavMesh.AllAreas))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        private void UpdateBlockedEdgeClearance(float now)
        {
            if (activeBlockedEdges.Count == 0) return;

            for (int i = activeBlockedEdges.Count - 1; i >= 0; i--)
            {
                var record = activeBlockedEdges[i];
                if (now < record.NextProbeTime)
                    continue;

                record.NextProbeTime = now + ObstacleProbeInterval;

                if (IsEdgePhysicallyClear(record.FromNode, record.ToNode))
                {
                    graph.SetEdgeBlocked(record.FromNode, record.ToNode, false);
                    activeBlockedEdges.RemoveAt(i);
                    TraceObstacle($"Obstruction verified cleared on edge ({record.FromNode} <-> {record.ToNode}). Edge unblocked.");
                }
                else
                {
                    TraceObstacle($"Edge ({record.FromNode} <-> {record.ToNode}) remains obstructed upon probe verification. Block retained.");
                }
            }
        }

        // ====================================================================
        // Public Test Hooks for Milestone 2.3
        // ====================================================================

        public void TriggerObstructionFailure(float now)
        {
            HandleObstructionFailure(now, waypoint);
        }

        public void TriggerDetourReplan(float now)
        {
            ExecuteDetourReplan(now, waypoint);
        }

        public void SetTestPathState(List<int> nodeIds, List<Vector3> waypoints, int currentIndex)
        {
            activeAStarNodeIds = nodeIds != null ? new List<int>(nodeIds) : null;
            activeAStarPath = waypoints != null ? new List<Vector3>(waypoints) : null;
            pathIndex = currentIndex;
        }

        public void SetTestPendingReplan(bool pending, float nextAllowedTime, int attempts)
        {
            isReplanPending = pending;
            nextReplanAllowedTime = nextAllowedTime;
            replanAttemptsForCurrentTarget = attempts;
        }

        /// <summary>
        /// Public test hook to evaluate and trigger tactical return to patrol.
        /// </summary>
        public void TriggerReturnToPatrol(float now)
        {
            InitiateReturnToPatrol(now);
        }

        /// <summary>
        /// Public test hook to simulate candidate movement failure and retry with next reachable candidate.
        /// </summary>
        public void TriggerNextReturnCandidate()
        {
            TryNextReturnCandidate();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearActivePath();
            ClearSearchState();
            ClearReturnState();
            activeBlockedEdges.Clear();
            replanAttemptsForCurrentTarget = 0;
            isReplanPending = false;
        }

        public override void ResetEnemyActions()
        {
            base.ResetEnemyActions();
            sightStarted = -1;
            searchUntil = 0;
            Detected = false;
            waypoint = 0;
            ClearActivePath();
            ClearSearchState();
            ClearReturnState();
            lastInvestigatedPosition = Vector3.zero;
            lastInvestigatedTime = 0;
            activeBlockedEdges.Clear();
            replanAttemptsForCurrentTarget = 0;
            lastReplanTime = -10f;
            nextReplanAllowedTime = 0f;
            isReplanPending = false;
            if (graph != null)
            {
                graph.ResetAllEdgeBlocks();
            }
        }
    }
}
