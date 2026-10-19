# IS Flanker — Ashen Bandara, IT24100315

Unity 6000.6.2f1. Branch: `is/ashen-flanker`, created from committed GV HEAD `211c0bb`. This is a new individual IS contribution built on the team's existing FL-04 integration. It is not a claim of five weeks of historical effort, original authorship of the baseline, or a predicted rubric grade. Codex assisted with implementation, tests and documentation; Ashen must review, understand and demonstrate it.

## Baseline and scope

Baseline FlankerBrain was introduced in teammate commit `1069fc4`. It sampled 18 positions, required at least 50 degrees of enemy-centered deviation, minimized a Unity path's length, and replanned only after target movement. That could reject every candidate for a stationary player 12 units away and never reconsider; movement failures/blocked return had no bounded tactical recovery.

Ashen's earlier `211c0bb` is the separate **GV** controller/animation contribution. Its Flanker change was a rotation handoff, not new IS decisions. This IS branch keeps that commit in its ancestry and preserves the shared GV controller, all other agents, combat rules, mission, models and scene assets.

New IS work consists of:

- `Assets/Scripts/FacilityBreach/AI/Roles/FlankTacticalPlanner.cs`: plain candidate/probe/score/result data and deterministic utility selection.
- `.../FlankDecisionPolicy.cs`: observation memory, bounded retry/failed-goal exclusion, switching control, scanning/return and inactivity/reset state.
- `.../FlankUnityEnvironment.cs`: adapter for existing Unity navigation and physics, with permission-gated observation probes.
- Modified `.../FlankerBrain.cs`: FL-only orchestration through the unchanged shared perception, mover and combat contracts.
- `Assets/Editor/FacilityBreach/FlankerBrainEditor.cs`: cached live Inspector diagnostics.
- `.../FlankerPlannerTests.cs` and `.../FlankerSceneValidation.cs`: deterministic policy/planner checks and controlled Prototype integration checks.

## Architecture and data flow

```text
unchanged shared perception → confirmed seen position
  → FlankDecisionPolicy (memory, timing, failure/recovery)
  → FlankTacticalPlanner (generate/evaluate/rank)
  ↔ IFlankEnvironment / FlankUnityEnvironment (legal paths, visible occupants, physical lines)
  → command (move / attack / scan / return / hold)
  → FlankerBrain → unchanged GV mover and shared combat
  → existing presentation reads movement/combat/role state
```

The plain C# planner/policy uses Vector3 and the shared MoveStatus enum but no GameObjects, transforms, Animator or MonoBehaviour. Deterministic tests inject `IFlankEnvironment` results. The Unity adapter computes **Unity NavMesh** paths; this is not custom A*, BFS, or a copy of the Patrol Guard algorithm. The distinct AI contribution is utility-driven tactical choice plus bounded stateful recovery.

## Candidate geometry and utility equation

Candidates surround the **last confirmed observed player position**. Radii are 4, 6 and 8 world units; rotations are ±60, ±90 and ±120 degrees, producing 18 raw candidates. The reference radial direction is target → agent's approach origin, captured at the start of an observation episode. Separation is measured **at the target**, between this approach radial and target → candidate.

The approach origin stays fixed while travelling. Otherwise arrival would redefine the current goal as zero angular separation and provoke another orbit around the target. Brief reacquisition with the same viable goal retains this reference. A new target-displacement episode or a fresh episode after expired memory establishes a new approach reference.

For a projected candidate:

```text
A = clamp01(target-centered separation degrees / 120)
R = 1 - clamp01(NavMesh route length / 30)
D = 1 - clamp01(abs(horizontal target distance - 6) / 3)
F = 1 for clear body + upper lines, 0.5 for clear body only
U = 0.35 A + 0.25 R + 0.20 D + 0.20 F
```

Each factor is in [0,1], weights sum to one, and utility is dimensionless. A body line blocked by a solid gives F=0 and is rejected rather than selected with compensating high factors. Body/upper line endpoints use the confirmed observation plus heights 1.0/1.3; their origin is candidate + height 1.4. These are tactical estimates; the unchanged combat adapter still validates actual attacks, range, LOS, cooldown and damage.

Example: angle 90°, route 6, target distance 6, clear lines gives A=.75, R=.8, D=1, F=1, hence **U=.8625**. Angle 120°, route 18, distance 8, body-only line gives U≈.6167. A wider but costly and less useful flank can lose to a balanced one.

Hard rejections: invalid data; failed-goal cooldown; no complete path; observed teammate occupies within one unit; route longer than 30; unusable firing line; sampled horizontal radius outside [3,9]; separation below 45°. Positions within .1 unit of an earlier viable projected candidate are duplicates. Navigation samples within one unit of the proposed point, using the current agent's area mask and path calculation.

Candidates use fixed radius/angle enumeration; a score difference not exceeding .00001 keeps the earlier candidate. A viable incumbent is re-evaluated with current observation/facts, and retained unless the best alternative improves utility by at least .08. Invalid incumbents are not protected by switching hysteresis. Candidate/rejection records and the selection reason are exposed.

### Rationale — initial design values, not proven optima

| Choice | Purpose / tradeoff |
| --- | --- |
| Angle weight .35 | Make genuine side/back displacement matter, rather than always minimizing chase distance. It is relative to approach, not player gaze. |
| Route weight .25 and cap 30 | Penalize expensive detours and bound useful route length in this facility. A short geometric route is not a custom search heuristic. |
| Distance weight .20, preferred 6, scale 3 | Favor a practical attack stand-off within the unchanged 12-unit FL combat range; 3/9 receive zero distance preference. |
| Firing weight .20 | Favor less obstructed useful positions; body visibility remains a hard prerequisite. |
| Raw radii 4/6/8 | Sample near/preferred/far positions without starting exactly on strict projected 3/9 boundaries. |
| 45° minimum, saturation at 120° | Exclude near-direct approaches while recognizing side/back flanks. Avoid the old enemy-bearing restriction that cannot work for distant targets. |
| .08 switching improvement | Resist marginal tactical switches; meaningful improvements and invalidation still allow switching. |
| Occupancy distance 1 | Conservative spacing for visible robot bodies, not cooperative goal reservation. |

These values need encounter tuning and visual/performance evaluation. No experiment proves them globally optimal. The old baseline, pure shortest-path ranking and direct pursuit are useful comparison policies; no measured advantage is claimed here without test evidence.

## Observation rules and memory

The unchanged RoleDecisionBrain sensor uses range/FOV/physical LOS. Flanker passes its resulting `SeesPlayer` and `LastKnownPosition` into the policy. The policy updates observation position/time only on confirmed sight. It stores no player Transform or rotation.

Candidate and firing/occupancy queries run **only while sight is confirmed**. Their player endpoints are copied observation values. The Unity adapter independently rejects all probes when not permitted. While hidden, the policy does not call that provider at all: no new firing rays, target tracking or candidate ranking. Seeing a teammate uses the shared sensor; only successfully observed teammate positions participate in occupancy checks. There is no assumed radio knowledge of hidden allies or their planned goals. Local avoidance/physical collision remains the navigation system's responsibility.

After losing sight, the policy may follow its existing goal for at most three gameplay seconds. It then scans in place through the shared mover. Arrival or lack of a viable goal can start scanning earlier. At seven seconds since last confirmed sight, the observation is expired and return begins. Thus the remaining scan interval is bounded by memory expiry; it is not a spatial search algorithm or guaranteed four-second scan in every case. Attacks require current confirmed sight and arrival.

## Recovery and state transitions

| Situation | Response |
| --- | --- |
| First confirmed observation | Start a tactical episode and evaluate candidates immediately. |
| Target moves more than four units from episode anchor | Refresh episode/budget; full planning still obeys two-second cooldown. Exactly four does not qualify. |
| Confirmed reacquisition | Refresh episode and budget; respect cooldown. Retain approach basis for a still-viable remembered goal to avoid orbiting after brief LOS loss. |
| Viable selected goal | Move through existing RequestMove; attack only when Arrived and visible. |
| While visible, every two seconds | Revalidate only the current goal; invalidate if unreachable, occupied or otherwise unusable. This is at most one probe between full planning calls. |
| Movement Failed | Exclude positions within one unit of the failed goal for six seconds; clear selected goal, wait two seconds, then select another candidate even with unchanged target observations. |
| All candidates rejected | Retry with two-second spacing; at most three full planning attempts in the stationary observation episode, including the initial attempt. |
| Retry budget exhausted | Safe Holding, no further candidate queries until displacement, reacquisition or reset. Holding does not fire or repeatedly issue movement. |
| Hidden / memory expires | Follow stored goal briefly, bounded scan, then return to original post. No hidden tactical probes. |
| Return failed | Wait two seconds before a second return attempt. After two failures, hold; no uncounted retry loop after clearing a mover failure. |
| Return still unresolved | Twelve-second return deadline ends travel with safe hold even if mover failure never arrives. |
| Reaches post | Idle/hold, no repeated return commands. |
| Pause | Existing gameplay gates freeze decisions and GameplayTime. Preserve policy/route state. |
| Death/inactive/disable | Explicit FL activity-sink handling clears observations, failures, counters and candidate state. Death/disable diagnostics become Inactive. |
| Revival/reset | Existing lifecycle calls ResetEnemyActions; reset policy and shared actions; await a fresh observation. |

Detailed phases are Idle, Flanking, Combat, Recovering, Searching, Returning, Holding and Inactive. To avoid changing shared enums/visual rules, FL maps them to existing RoleState values: Flank, Combat, Search, Return or Patrol for idle/recovery/hold. Thus the shared state light is not a unique indicator of recovery; use the IS debug Inspector.

RequestMove can return false because its existing cooldown defers a request. That is not treated as a tactical failure unless status is Failed. The policy issues a new-goal stop only when changing movement ownership/goal, then lets the existing mover accept/retry compatible requests. It never writes root position/rotation itself. A failed selected goal is distinguished from a deferred request.

## Diagnostics and query limits

During Play Mode select `FB_Agent_FL04` in Hierarchy. Its FlankerBrain Inspector shows cached phase/recovery reason, planning attempts, return attempts, navigation query count, observation/time, frozen approach origin, selected point/angle/radius, normalized score factors and last-plan selection reason. Expand the candidate foldout for every candidate and rejection reason. Repainting this view performs no path queries and creates no game HUD/debug meshes.

`Plans` remains an attempt counter for compatibility; it is not the number of successful plans. `FlankAngle` now means target-centered angular separation. PathQueries counts the adapter's lifetime path calls and is not a resettable tactical counter. Policy diagnostic data/counters reset on death/reset. GameplayDiagnostics emits one detailed line when the plan-attempt count changes, rather than every decision frame; shared state tracing only occurs on role transitions.

A full plan has at most 18 candidate probes plus one incumbent probe. Failed-goal prefiltering can reduce calls. No full planning more often than two gameplay seconds; stationary episodes stop after three attempts. Current-goal validation is every two seconds while visible. These are bounds on calls, not measured FPS guarantees.

## Exact manual Unity demonstration

1. Open this project in Unity 6000.6.2f1 on `is/ashen-flanker`. Wait for compilation. Open Window → General → Console. Open `Assets/Scenes/Prototype.unity`.
2. Press Play → **Start Mission**; click Game view. WASD/mouse moves/looks, Shift sprints, E interacts, left click shoots, R reloads, Q throws, Escape pauses.
3. In Hierarchy search `FB_Agent_FL04`; select its FlankerBrain. Inspect the cached diagnostics. Resume if changing focus auto-paused gameplay. Do not add scripts or change shared components.
4. Let FL see you around 12–15 units away with an open route. Remain still. Confirm it can select a target-centered flank rather than reject everything based solely on the old bearing. Compare angle, route, distance and firing factors in the Inspector; the exact chosen point depends on real navigation/obstructions.
5. Let it travel/arrive and observe shared smooth turning/animation and legal attacks. Confirm it does not continually orbit just because it reached its goal. A player move greater than four units can deliberately prompt reconsideration after cooldown.
6. Put the existing barricade in the selected route or obstruct the chosen firing position, without moving the visible player. Watch for movement failure or periodic goal invalidation, a recovery reason, six-second goal exclusion and an alternate candidate after cooldown. If no alternatives exist, observe finite retries followed by explicit Holding. Do not confuse NavMesh successfully rerouting the same path with a tactical failure.
7. Hide fully and move while hidden. Observe that remembered position/time, candidate plan and query counters do not track hidden motion. It may finish stored travel briefly, scan, then expire memory and return. Reappear to confirm fresh observation/reacquisition.
8. Block return if practical. It must stop safely after two failures or its twelve-second deadline, rather than looping forever. This is a deliberately limited fallback, not a guarantee it can always return.
9. Pause while it travels; resume. Defeat only FL while moving, check Inactive/cleared IS diagnostics and no attacks. Wait for the existing thirty-gameplay-second safe revival and verify fresh planning, or use Retry/Restart to reset the scene. Do not defeat all four together when demonstrating revival: existing team roster rules can make that defeat permanent.
10. Observe all four agents to check that teammates' distinct behaviors and completed GV visuals remain. Inspect Console for **new** errors/warnings and record exactly what you tested. Stop Play Mode; do not save temporary runtime objects or apply unwanted prefab changes.

Editor commands: **Facility Breach → IS Flanker → Run Deterministic Planner and Recovery Tests**; **Run Prototype Integration Checks**. The integration command opens Prototype and enters/exits Play Mode; preserve unsaved scene work first. See the validation document for actual executed results and scope.

## Limitations

- Utility and thresholds are initial design choices; no claimed optimality, grade, measured tactical advantage or frame-rate improvement.
- This is finite candidate optimization plus stateful recovery using Unity navigation, not custom informed route search. It does not reuse/claim the Patrol Guard A* implementation.
- Fixed samples can miss a useful continuous-space location; future firing estimates do not guarantee a later shot. No prediction of hidden target motion, gaze-based stealth score, spatial search or coordinated team-goal reservations.
- After a stationary episode exhausts its budget, Holding waits for meaningful new evidence/reset. Merely reopening a route after all retries does not autonomously restart planning. This is an intentional bounded fallback, not indefinite navigation churn.
- Occupancy knowledge is currently visible robot positions only; hidden/unobserved bodies are left to physical collision/avoidance. No player-orientation data is used for scoring.
- Shared movement timeouts can take longer to report a failure than a human expects; visible goal revalidation helps but does not replace all movement edge cases.
- This new work cannot retroactively satisfy five weeks of version-control history. No push or merge is performed as part of this contribution handover.
