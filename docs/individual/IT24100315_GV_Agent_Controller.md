# Ashen Bandara — IT24100315 — GV Part 4

The lecturer's `Assignmnet_GV.pdf` defines this role as translating calculated path arrays into smooth movement, rotations and animations. The two local team plans describe proposals, not implementation evidence. ReferenceDocs remains local reference material. The implemented mission remains **access card → deactivate device → escape**.

## Before this contribution

All four enemies already navigated, perceived, chose targets and attacked. `SoundHunterNavMeshMover` supplied single-goal NavMesh paths, door opening, timeouts and stuck detection. Patrol Guard already planned and streamed A* waypoints, including search, return and obstacle logic. Sound Hunter already had hearing and memory; Cover Defender and Flanker already selected tactical goals. **FlankerBrain is baseline team code, not a new Ashen IS contribution.** Combat, revival, models, materials and state lights were existing team work.

## Changed files and ownership

- `AI/SoundHunter/AgentRoute.cs`: new copied ordered route data, point index, revision, completion/interruption/failure reporting.
- `AI/SoundHunter/SoundHunterNavMeshMover.cs`: extends the existing shared controller with `RequestRoute`, segment execution, single-owner smooth turning, aiming and scanning. Retains NavMesh navigation, doors, obstruction handling and the existing `IHunterMovement` API.
- `Combat/EnemyAnimationLibrary.cs`: references the team's existing FBX clips for player builds.
- `Combat/EnemyModelAnimation.cs`: new visual animation state selection and blending using Unity Playables.
- `Combat/EnemyPresentation.cs`: keeps state-light feedback and existing missing-animation pose fallback; avoids competing defeat transforms when clips play.
- `Combat/EnemyCombatAdapter.cs`: forwards the AI-supplied attack target for smooth aiming. Damage, timing and hit rules remain team code.
- `FacilityBreachRuntime.cs`: attaches the shared mover and animation player when creating every enemy.
- `RoleDecisionBrain`, `PatrolGuardBrain`, `FlankerBrain`, `FacilityBreachSoundHunterBrain`: narrow rotation handoffs, replacing direct body rotations with shared facing/scan requests. Their planning and state policies remain baseline.
- `Editor/FacilityBreach/AgentAnimationLibraryBuilder.cs`: audits imported clips and their prefab transform bindings; rebuilds the resource library.
- `Editor/FacilityBreach/AgentControllerTests.cs`: focused editor-menu and batch checks using the existing project's validation style.
- `Resources/EnemyAnimationLibrary.asset`: references imported FBX subassets; no copied animation curves or new Animator Controller assets.

## AI → route execution → presentation

AI chooses **where and why**. Patrol Guard continues streaming its existing authored A* points through `RequestMove`; the other brains continue supplying their chosen world goals. A `RequestMove` is treated as a one-point route. A future AI planner can submit a whole ordered `IReadOnlyList<Vector3>` with `RequestRoute(points, stoppingDistance)` without changing the execution layer. This contribution does not claim to replace the team's search algorithms.

The mover copies points, obtains a complete NavMesh path for each segment and uses the NavMeshAgent for translation, acceleration and avoidance. It does not interpolate directly through walls or teleport to route points. Intermediate points use a small arrival tolerance and disable braking; the final point brakes using the requested tolerance. Only the current segment is validated in advance: a later unreachable point fails when reached in the sequence.

`agent.updateRotation = false` gives the mover sole ownership of body turning. Facing requests take priority, then travel direction, then stationary search scans. `Quaternion.RotateTowards` uses degrees/second multiplied by `Time.deltaTime`; a scan similarly uses degrees/second. Brief facing-request lifetimes prevent an old attack or search instruction from sticking forever. The combat adapter forwards perceived targets; the controller does not discover or track hidden players.

Presentation reads actual agent velocity, brain search state, attack warning/resolved shots and death. Its priority is Defeat → Attack → Move → Search → Idle. Unity Playables blends the existing clips; movement clips loop locally, defeat clamps at the last frame. Root motion is disabled, so animation cannot translate the authoritative combat/NavMesh body. Animation time freezes when gameplay is paused.

### Route contract

```csharp
var mover = enemy.GetComponent<SoundHunterNavMeshMover>();
mover.Route.Ended += (revision, state, reason) => Debug.Log($"{revision}: {state} {reason}");
mover.RequestRoute(new[] { firstWorldPoint, secondWorldPoint }, .5f);
mover.FacePoint(perceivedAimPosition); // renew while aiming
mover.Stop();
```

Use `RequestRoute`/`Stop` to mutate execution; treat `Route` as diagnostic state. Notifications are synchronous; observe them rather than issuing another route from inside a notification. `Status` retains the existing Idle/Moving/Arrived/Failed compatibility values. `Route.State` additionally distinguishes interruption. Every route has a revision so listeners can distinguish a replaced request from its replacement.

Null/empty routes clear previous travel and arrive immediately. Invalid coordinates/tolerances fail safely. Stop reports interruption and resets the NavMesh path. Replacing a route interrupts the old revision and starts from the current position. Pause retains the route and stops the agent; resume continues it. Death/activity suspension interrupts movement; revival resets route, request cooldown and facing memory. The existing combat lifecycle remains responsible for safe respawn. Existing single-goal requests retain the team's half-second request throttle and two-second failure cooldown; `false` during that throttle means deferred, and callers continue polling as before. Explicit `RequestRoute` replaces immediately.

## Animation asset findings

All four FBX importers use Generic animation (`animationType: 2`), enable import, and have empty explicit `clipAnimations`/`referencedClips` lists. Those empty lists **do not prove there are no embedded clips**. Binary inspection found named Idle, Move, Search, Attack and Defeat animation stacks for each robot. Existing visual prefabs contain skinned meshes and rigs with `root`, `torso`, `head`, `Lleg`, `Rleg`, `Larm` and `Rarm` transforms; mesh names such as `LLeg` are separate from bone names. The inner `ModelVisual` is the clip binding root, inside the runtime-created `ModelVisual` wrapper.

The Unity audit confirmed all 20 clips: each is one second long and all 80 transform bindings per clip resolve against that inner hierarchy. Existing model transforms, rigs and materials are preserved. The clips and original models are teammates' assets; the new contribution is their runtime integration. There is no authored hit-reaction clip wired here. These are full-body clips, without a separate upper-body firing layer or foot IK; visual blending and foot sliding require manual review. If the library or model hierarchy is missing, the existing static/death-pose presentation remains and a warning identifies the missing integration; this is a fallback, not full procedural locomotion.

## Exact Unity testing instructions

1. Open this project in **Unity 6000.6.2f1**. Leave Play Mode off and wait for the import/compile spinner to finish. Open **Window → General → Console**. Resolve any red compilation errors before continuing.
2. Choose **Facility Breach → GV Agent Controller → Rebuild FBX Animation Library**. Expect 20 `[GV clip audit]` entries, covering five clips for each enemy, with matching resolved/total binding counts. The library is supplied with this change; rebuilding is an audit/repair action, not a required attachment step.
3. Choose **Facility Breach → GV Agent Controller → Run Movement Checks** outside Play Mode. Expect `PASS 15 route and turning checks (Edit Mode)`. Then choose **Facility Breach → GV Agent Controller → Run Play Mode Movement Checks**. This automatically enters Play Mode, creates a temporary agent and navigation patch, runs the checks, cleans them up and returns to Edit Mode. Expect `PASS 13 navigation lifecycle checks (Play Mode)`. Neither command saves a scene. These checks exercise the controller, not a full enemy encounter.
4. In the Project panel open **Assets/Scenes/Prototype.unity**. Press the triangle **Play** button at the top, then click the game's Start button. Click inside Game view to capture the mouse. Use WASD/mouse, E to interact, left mouse to shoot, R to reload, Q to throw, Escape to pause.
5. In Hierarchy expand **FacilityBreachRuntime's object → FB_Agents** (the runtime object name may differ). Select `FB_Agent_PG01`, `FB_Agent_SH01`, `FB_Agent_CD03` and `FB_Agent_FL04`. Each must have Sound Hunter Nav Mesh Mover, Enemy Model Animation, Enemy Presentation and its existing brain. You should not add components manually. Their runtime child `ModelVisual/ModelVisual` has the animation Animator with root motion disabled.
6. Watch PG patrol between its corridor waypoints. It should turn gradually, animate while moving and stop at its destination. Make noise out of view to trigger SH investigation, approach CD to see cover movement, and let FL choose a flank. All four must retain distinct AI behavior and existing colors/state lights.
7. Let an enemy see you, then stand within its attack distance. It should aim gradually while stationary and show attack feedback. Break line of sight: it should follow its existing remembered-position/search policy, including a smooth stationary scan where that policy calls for one.
8. Press Escape during travel or attack. Position, turning and animation should freeze. Resume and confirm travel continues without a teleport or accumulated timeout.
9. Interact with a door in an enemy's route and push the existing barricade into a corridor. Expect navigation around valid obstacles or a controlled failure/replan according to the brain; never movement straight through the obstruction. Restore the passage and verify recovery. Watch the Console for errors rather than assuming every temporary stop is a controller failure.
10. Shoot an enemy during movement. It must stop moving/firing, play defeat and remain inactive until the existing safe revival system allows it back. On revival it should return to live animation and accept fresh AI movement without resuming an old route. Also test Retry/Replay after a loss/win.
11. Finish **card → device → exit** to verify mission flow. Stop Play Mode with the top Play button. Temporary enemies disappear; do not save Play Mode objects as scene changes. Record visual findings and unexpected Console messages separately from automated check results.

## Viva concepts to explain

- **Decision versus execution:** an AI route says where to go; the GV controller makes the body follow it visibly. The pre-existing Flanker algorithm is not this GV contribution.
- **World and local space:** route points are world positions; animation bones operate inside the model's original hierarchy.
- **Navigation safety:** complete NavMesh paths, obstacle avoidance and door interaction preserve legal movement; a direct position Lerp would ignore walls.
- **Frame independence:** speed/turn rate multiplied by delta time; exponential blend `1 - exp(-rate * dt)` reduces dependence on frame rate.
- **One writer per responsibility:** NavMesh owns position, the mover owns root rotation, Playables owns the visual pose, combat owns life/damage.
- **State and lifecycle:** arrival differs from interruption/failure; revisions identify requests; pause preserves a route whereas death cancels it.
- **Animation integration:** imported clips can exist without standalone `.anim` files; verify bindings instead of guessing from metadata. Disable root motion to avoid competing movement.
- **Optimization and limits:** reuse existing rigs/clips, blend five cached playables per enemy, avoid per-frame pathfinding, and retain bounded request retries. Actual smoothness, avoidance, foot sliding and simultaneous-agent performance still need visual Play Mode checks.

## Verification record

See the accompanying validation record for commands, actual results, user-reported Prototype checks and remaining manual checks. Automated editor checks are not proof of a visually verified Play Mode session or a successful player build. Committing requires explicit user authorization; no push, merge or change to ReferenceDocs is part of this implementation.
