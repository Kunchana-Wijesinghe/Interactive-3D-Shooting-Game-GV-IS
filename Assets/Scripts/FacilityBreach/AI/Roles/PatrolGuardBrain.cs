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
                if (State == RoleState.Investigate || State == RoleState.Search)
                {
                    TraceSearch($"Player reacquired at {LastKnownPosition} during {State}. Cancelling search/investigation.");
                }

                ClearActivePath();
                ClearSearchState();

                if (sightStarted < 0) sightStarted = now;

                if (!Detected && now - sightStarted < DetectionSeconds)
                {
                    SetState(RoleState.Detecting);
                    Mover.Stop();
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
                Mover.Stop();
                SetState(RoleState.Investigate);
                ClearActivePath();
                ClearSearchState();
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
                    TraceSearch($"Search duration ({SearchSeconds:F1}s) completed. Returning to patrol.");
                    ClearSearchState();
                    ClearActivePath();
                    HasLastKnown = false;
                    Mover.Stop();
                    SetState(RoleState.Patrol);
                    return;
                }

                // If no search points or all candidates have been visited: rotational sweep
                if (activeSearchPoints == null || activeSearchPoints.Count == 0 || currentSearchPointIndex >= activeSearchPoints.Count)
                {
                    ClearActivePath();
                    transform.Rotate(0, 9, 0);
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
                    transform.Rotate(0, 9, 0);
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
                        transform.Rotate(0, 9, 0);
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

            // 5. Detecting State fallback
            if (State == RoleState.Detecting)
            {
                SetState(RoleState.Patrol);
            }

            // 6. Patrol State Navigation via Authored A* Graph
            ExecutePatrolNavigation();
        }

        private void ExecutePatrolNavigation()
        {
            if (Route == null || Route.Length == 0)
            {
                Mover.Stop();
                ClearActivePath();
                return;
            }

            if (waypoint < 0 || waypoint >= Route.Length)
            {
                waypoint = 0;
            }

            Vector3 target = Route[waypoint];

            // If already within arrival distance of the target waypoint and route has multiple points, advance to next
            if (Route.Length > 1 && Vector3.Distance(transform.position, target) <= 0.6f && (activeAStarPath == null || pathIndex >= activeAStarPath.Count))
            {
                TraceAStar($"Already within arrival tolerance of route waypoint {waypoint} ({target}). Advancing to next waypoint.");
                waypoint = (waypoint + 1) % Route.Length;
                target = Route[waypoint];
                ClearActivePath();
            }

            // Handle movement failure: advance to next waypoint without oscillation
            if (Mover.Status == MoveStatus.Failed)
            {
                TraceAStar($"Mover failed along path (Reason: {Mover.FailureReason}). Advancing to next route waypoint.");
                Mover.Stop();
                waypoint = (waypoint + 1) % Route.Length;
                ClearActivePath();
                return;
            }

            // Handle intermediate or final waypoint arrival
            if (Mover.Status == MoveStatus.Arrived && activeAStarPath != null && activeAStarPath.Count > 0)
            {
                TraceAStar($"Arrived at intermediate path node (Index {pathIndex}/{activeAStarPath.Count - 1}, Pos={activeAStarPath[pathIndex]}).");
                pathIndex++;
                Mover.Stop();

                if (pathIndex >= activeAStarPath.Count)
                {
                    TraceAStar($"Completed full A* path to route waypoint {waypoint} ({target}). Advancing to next route waypoint.");
                    waypoint = (waypoint + 1) % Route.Length;
                    ClearActivePath();
                    return;
                }
            }

            // Plan a new A* path if needed or if target changed
            if (activeAStarPath == null || activeAStarPath.Count == 0 || currentTargetWaypointIndex != waypoint)
            {
                PlanPathToWaypoint(waypoint);
            }

            // Sequentially stream next waypoint to Mover if not currently moving
            if (activeAStarPath != null && pathIndex < activeAStarPath.Count)
            {
                if (Mover.Status != MoveStatus.Moving)
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

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearActivePath();
            ClearSearchState();
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
            lastInvestigatedPosition = Vector3.zero;
            lastInvestigatedTime = 0;
        }
    }
}
