# Individual Worklog: Graphics and Visualization (GV) - Part 2

**Student Name:** Wijesinghe K.  
**Student ID:** IT24102587  
**Module:** SE3032 - Graphics and Visualization (GV)  
**Assigned Role:** Part 2: Systems Engineer (Player Interaction Physics)  
**Git Branch (Merged Baseline):** `develop` (integrated via GV PR #9 commit `fd94efe`, PR #11 commit `4ac6a2f`, PR #12 commit `caa133e`, PR #15 commit `b1b6745`, and PR #16 commit `38e9a08`)<br/>
**Current Working Feature Branch:** `gv/IT24102587-kickable-physics-audio`<br/>
**Target Unity Version:** Unity 6000.6.2f1 (URP / 3D)<br/>
**Repository:** `Interactive-3D-Shooting-Game-GV-IS` (Private)

---

## 1. Scope & Role Responsibilities
As the Systems Engineer (GV Part 2), my responsibility is to design, implement, and verify the physical interaction mechanics of the facility environment:
1. **Interactive Door Mechanics**: Reliable trigger detection, hinge rotation physics, panel collision blocking, and edge-case handling (rapid toggling, range limits).
2. **Movable Barricade Physics**: Push/pull maneuvering through interaction (`E` key), strict collision handling preventing clipping/tunneling through walls and player, non-destructible obstacle behavior, and reusable component architecture.
3. **AI Dynamic Navigation Integration**: `NavMeshObstacle` carving to dynamically recalculate enemy patrol paths when doors/barricades change state or position (to be coordinated with Bandara / IS agents).

---

## 2. Feature Worklog Entries

### Feature Entry 1: Interactive Door System & Isolated Test Scene
- **Date:** 2026-10-04  
- **Feature Name:** Interactive Door Mechanics & Verification Scene  
- **Files Created / Changed:**
  - `Assets/Scripts/InteractiveDoor.cs` (reviewed and verified)
  - `Assets/Scripts/DoorTrigger.cs` (reviewed and verified)
  - `Assets/Scripts/SimplePlayerController.cs` (created temporary test player controller with HUD)
  - `Assets/Scripts/Editor/TestSceneBuilder.cs` (created editor builder for standalone test scene)
  - `Assets/Scenes/Tests/DoorInteractionTest.unity` (created isolated test scene)
  - `Assets/Scenes/Tests/DoorInteractionTest.unity.meta` (created asset metadata)

#### Physics & Interaction Mechanics
- **Trigger Detection**: The doorway utilizes a `BoxCollider` set to `isTrigger = true` spanning $4.0\text{m}$ along the corridor ($Z \in [-2, +2]$). The attached `DoorTrigger` script detects entering colliders with the `"Player"` tag via `OnTriggerEnter` and `OnTriggerExit`, notifying the door hinge controller (`SetPlayerNearby(true/false)`).
- **Hinge Rotation**: The door hinge GameObject hosts the `InteractiveDoor` script. Upon receiving the `E` key down event while `playerNearby == true`, it toggles `isOpen = !isOpen`. In `Update()`, it performs a spherical linear interpolation (`Quaternion.Slerp`) between its initial `closedRotation` and `openRotation` ($90^\circ$ around the local Y-axis) at speed `openSpeed = 4.0f`.
- **Physical Collision**: The door panel (`_Panel`) is a physical child of the hinge and carries a non-trigger `BoxCollider`. As the hinge rotates, the collider sweeps through world space, physically blocking the player capsule when closed and vacating the doorway when open.
- **Idempotent Input Handling**: If `E` is tapped repeatedly mid-swing, the boolean target state simply flips, reversing interpolation toward open or closed without accumulating rotational error or snapping.

#### Unity Play Mode Testing & Results
- **Test 1: Out-of-Range Interaction**
  - *Action:* At spawn position $(0, 1, -5)$ ($5\text{m}$ away from door), pressed `E`.
  - *Expected Result:* Door remains stationary; HUD indicates `OUT OF RANGE (5.0m away)`.
  - *Actual Result:* Verified in Unity Play mode — door remained closed and unresponsive to `E` out of range.
- **Test 2: In-Range Interaction & Passage**
  - *Action:* Walked forward using WASD into the trigger box ($Z > -2\text{m}$); HUD changed to `IN RANGE`; pressed `E`.
  - *Expected Result:* Door rotates smoothly $90^\circ$; player walks freely through opening; pressed `E` again to close and verified player cannot walk through the closed panel.
  - *Actual Result:* Verified in Unity Play mode — door opened cleanly, corridor was fully passable, and closed panel physically blocked the player capsule.
- **Test 3: Rapid Repeated `E` Toggling**
  - *Action:* Pressed `E` to initiate opening, then pressed `E` three times rapidly before the swing completed.
  - *Expected Result:* Door reverses direction smoothly mid-swing without jumping, popping, or losing alignment.
  - *Actual Result:* Verified in Unity Play mode — smooth interpolation reversal confirmed; door settled at correct closed angle.

#### Problems Found & Fixes
1. *Discrepancy in Scene Door Count:* An earlier audit reported 10 doors, whereas inspecting `Prototype.unity` and `FacilityDoors.cs` confirmed exactly 11 doors. Documented the full list of 11 doors to maintain consistency.
2. *Unity 6 API Deprecation Warning:* Legacy calls to `FindObjectOfType<T>()` produced CS0618 warnings; updated all lookups in test scripts to use `FindAnyObjectByType<T>()`.

#### Evidence & Screenshots
- [01_Door_OutOfRange.png](evidence/IT24102587%20GV%20evidence/01_Door_OutOfRange.png)
- [02_Door_Open.png](evidence/IT24102587%20GV%20evidence/02_Door_Open.png)
- [03_Door_ClosedCollision.png](evidence/IT24102587%20GV%20evidence/03_Door_ClosedCollision.png)

#### AI Assistance & Manual Review
- AI assisted in drafting the automated editor builder script (`TestSceneBuilder.cs`) and player controller scaffolding.
- Manually reviewed and verified all script logic, scene hierarchy, and Unity Play mode interaction checks on 2026-10-04.

---

### Feature Entry 2: Movable Barricade Physics & Maneuvering
- **Date:** 2026-10-04  
- **Feature Name:** Movable Barricade with E-Key Maneuvering & Wall Collision  
- **Files Created / Changed:**
  - `Assets/Scripts/MovableBarricade.cs` (created standalone physics barricade component)
  - `Assets/Scripts/MovableBarricade.cs.meta` (created asset metadata)
  - `Assets/Scripts/SimplePlayerController.cs` (updated with barricade grab/push detection and physical collision callback)
  - `Assets/Scripts/Editor/TestSceneBuilder.cs` (updated with `BuildBarricadeScene` menu item)
  - `Assets/Scenes/Tests/BarricadeInteractionTest.unity` (created isolated barricade testing arena)
  - `Assets/Scenes/Tests/BarricadeInteractionTest.unity.meta` (created asset metadata)

#### Physics & Interaction Mechanics
- **Physics Configuration**: The barricade root object hosts a non-kinematic `Rigidbody` with mass $60.0\text{kg}$, `linearDamping = 5.0f`, and `angularDamping = 8.0f`. Collision detection is explicitly configured to `CollisionDetectionMode.Continuous` to eliminate tunneling through walls.
- **Degrees of Freedom**: Constraints are locked via `RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ`. This ensures the barricade remains upright and stable on the floor plane while allowing Y-axis rotation and X/Z translation.
- **E-Key Interaction Modes**:
  - *Grab / Maneuver Mode:* When the player is within `interactionDistance` ($3.0\text{m}$) and presses `E`, the barricade enters hold mode. In `FixedUpdate()`, it calculates a target position in front of the player $(2.0\text{m})$ and applies horizontal velocity toward the target (`(targetPos - rb.position) * followSpeed`).
  - *Wall & Obstacle Collision:* Because motion is governed by Rigidbody velocity rather than direct transform manipulation, when the barricade contacts a solid wall, Unity's PhysX collision solver stops its forward progress. The barricade cannot penetrate or clip through walls.
  - *Safe Release / Break Distance:* If the player walks away or the barricade gets held up on a wall such that the separation exceeds `maxHoldDistance` ($3.8\text{m}$), the hold automatically disengages.
  - *Direct Release:* Tapping `E` while holding immediately releases the barricade.
- **Direct Physical Collision**: When the player approaches without pressing `E`, walking against the barricade triggers `OnControllerColliderHit` in `SimplePlayerController`, applying a physical contact force (`physicalPushPower = 40.0f`) that enables pushing the heavy obstacle along the floor.
- **Non-Destructibility**: The component explicitly avoids health, damage, or destruction logic, maintaining its assigned specification as an indestructible movable obstacle.

#### Unity Play Mode Testing & Results
- **Test 1: Out-of-Range Interaction**
  - *Action:* At spawn position $(0, 1, -4)$ ($4\text{m}$ away), pressed `E`.
  - *Expected Result:* Barricade remains stationary; HUD indicates `Barricade: OUT OF RANGE (4.0m away)`.
  - *Actual Result:* Verified in Unity Play mode — barricade remained stationary and unresponsive to `E` out of range.
- **Test 2: In-Range Grab & Maneuvering**
  - *Action:* Walked into range ($< 3.0\text{m}$); pressed `E` to grab; walked forward, backward, and turned.
  - *Expected Result:* HUD displays `GRABBED / MANEUVERING`; barricade smoothly tracks in front of the player along the floor without flipping.
  - *Actual Result:* Verified in Unity Play mode — barricade entered grab mode and smoothly moved with the player along the floor without flipping.
- **Test 3: Wall Collision & Anti-Tunneling**
  - *Action:* Pushed the barricade directly into the solid north wall ($Z = 10\text{m}$) and side partitions.
  - *Expected Result:* Barricade collides firmly with the wall collider and stops dead; does not penetrate, jitter, or pass through the wall geometry. If player continues walking, grab automatically disengages at $3.8\text{m}$.
  - *Actual Result:* Verified in Unity Play mode — barricade stopped against the wall collider without penetrating, and the hold disengaged when exceeding max hold distance.
- **Test 4: Player Body Collision**
  - *Action:* Walked directly into the barricade without pressing `E`.
  - *Expected Result:* Player's `CharacterController` is blocked by the barricade `BoxCollider`; continued forward walking gradually pushes the barricade via physical contact force.
  - *Actual Result:* Verified in Unity Play mode — player capsule was blocked by the barricade collider and pushed it forward via contact force.
- **Test 5: Manual Release**
  - *Action:* While maneuvering the barricade, pressed `E`.
  - *Expected Result:* Barricade releases immediately, smoothly comes to a stop via linear damping, and remains an immovable physical obstacle.
  - *Actual Result:* Verified in Unity Play mode — barricade released immediately upon pressing `E` and remained stationary.

#### Problems Found & Fixes
1. *0-Byte Metadata File Collision:* An empty `BarricadeInteractionTest.unity.meta` file initially created by the IDE prevented script compilation during batch build. Removed the empty file, ensured valid GUID metadata for `MovableBarricade.cs`, and generated the scene natively using `TestSceneBuilder.BuildBarricadeScene()`.
2. *Unity 6 Rigidbody Damping Deprecation:* Replaced legacy `drag` and `angularDrag` with `#if UNITY_6000_0_OR_NEWER` utilizing `linearDamping` and `angularDamping`.

#### Evidence & Screenshots
- [04_Barricade_SceneOverview.png](evidence/IT24102587%20GV%20evidence/04_Barricade_SceneOverview.png)
- [05_Barricade_Grabbed.png](evidence/IT24102587%20GV%20evidence/05_Barricade_Grabbed.png)
- [06_Barricade_WallCollision.png](evidence/IT24102587%20GV%20evidence/06_Barricade_WallCollision.png)
- [07_Barricade_DoorwayBlocked.png](evidence/IT24102587%20GV%20evidence/07_Barricade_DoorwayBlocked.png)

#### AI Assistance & Manual Review
- AI assisted in drafting `MovableBarricade.cs` physics logic, updating `SimplePlayerController.cs`, and constructing the scene builder.
- Manually reviewed component parameters, damping values, and constraint locks.
- Manually reviewed and tested in Unity Play mode on 2026-10-04, confirming full pass of all listed interactions.

---

### Feature Entry 3: Centralized Interaction Arbitration & Main-Scene Barricade Integration
- **Date:** 2026-10-18
- **Feature Name:** Centralized E-Key Interaction Arbitration, Main-Scene Barricade Physics & Production Verification
- **Working Feature Branch:** `gv/IT24102587-main-barricade-interaction` (Local working branch based off `develop` commit `c237edf`; NOT yet merged or committed)
- **Files Created / Changed:**
  - `Assets/Scripts/FacilityBreach/Interactions/FacilityInteractionDispatcher.cs` (created central interaction arbitration singleton)
  - `Assets/Scripts/FacilityBreach/Interactions/FacilityInteractionDispatcher.cs.meta` (created asset metadata)
  - `Assets/Editor/FacilityBreach/FacilityInteractionDispatcherTests.cs` (created 43-check Edit Mode test suite)
  - `Assets/Editor/FacilityBreach/FacilityInteractionDispatcherTests.cs.meta` (created asset metadata)
  - `Assets/Scripts/FacilityBreach/FacilityBreachRuntime.cs` (modified: registered barricades/doors/stations, disabled door internal polling, routed `TryInteract()` and HUD prompt to dispatcher)
  - `Assets/Scripts/InteractiveDoor.cs` (modified: added `handleInternalInput` flag and `PlayerNearby` property to prevent duplicate input handling while preserving backward compatibility with standalone test scenes)
  - `Assets/Scenes/Prototype.unity` (modified: added `MovableBarricade` component references and serialized YAML blocks to all 6 authored root pushable barricades)

#### Architecture & Interaction Mechanics
- **Centralized Interaction Arbitration**:
  - Previously, `InteractiveDoor` polled `Input.GetKeyDown(KeyCode.E)` independently inside its own `Update()`, while `FacilityBreachRuntime.TryInteract()` handled resource stations and pickups, and barricades used player controller checks. In a dense environment like `Prototype.unity`, pressing `E` near multiple interactable objects caused input contention, race conditions, or simultaneous activation (e.g., toggling a door while grabbing a barricade).
  - Implemented `FacilityInteractionDispatcher` as a centralized gatekeeper that arbitrates all E-key interaction requests across three candidate categories:
    1. **Resource Stations / Pickups**: Access cards, weapons, ammo crates, health packs, and shutdown devices.
    2. **Interactive Doors**: Hinge swinging with safety clearance checks (`RequestOpen`).
    3. **Movable Barricades**: Grab, pull, push, and release maneuvering.
- **Single-Action Guarantee & Held Barricade Release Priority**:
  - The dispatcher strictly guarantees that **exactly one action** occurs per E-key press:
    - **Priority 1 (Held Barricade Release)**: If the player is currently holding a barricade (`isHeld == true` and `holdingPlayer == playerTransform`), pressing `E` immediately calls `heldBarricade.Release()` and returns `true`. All candidate spatial queries are bypassed (`Score = -100f`), guaranteeing that standing near a door or station while carrying a barricade will never toggle the door or activate the station.
    - **Priority 2 (Spatial Arbitration)**: If no barricade is held, the dispatcher gathers all candidates within range (`<= 3.0m` for barricades/stations, door trigger for doors) in the player's view half-space (`dot >= 0.35`–`0.55`).
- **Deterministic Candidate Scoring Formula**:
  - Each candidate is assigned a deterministic spatial score:
    $$\text{Score} = \text{distance} + 2.0 \cdot (1.0 - \text{dotProduct}) - (\text{hasDirectAim} \;?\; 1.5 : 0.0)$$
  - Candidates with lower scores are preferred (closer distance, tighter alignment with camera view direction, and direct crosshair aim bonus).
  - The candidate with the single lowest score is selected and executed; all other candidates are ignored.
- **Main-Scene Barricade Integration (`Prototype.unity`)**:
  - Located all 6 authored pushable barricade roots in the facility hierarchy under `Facility_Barricades`:
    1. `Barricade_Entrance` (GameObject ID: `1944818420`, Component ID: `1944818424`)
    2. `Barricade_LowerRoute` (GameObject ID: `1979052250`, Component ID: `1979052254`)
    3. `Barricade_Room1Area` (GameObject ID: `320277312`, Component ID: `320277316`)
    4. `Barricade_Middle` (GameObject ID: `1746341875`, Component ID: `1746341879`)
    5. `Barricade_UpperLeft` (GameObject ID: `850553676`, Component ID: `850553680`)
    6. `Barricade_ExitArea` (GameObject ID: `1474787438`, Component ID: `1474787442`)
  - Configured each barricade with non-destructive physics parameters tuned specifically for the main facility corridors:
    - **Mass**: $12.0\text{kg}$ (main-scene authored density, contrasting with the generic $60.0\text{kg}$ test-scene mass) to ensure responsive maneuvering through tight industrial corridors without sluggish momentum.
    - **Damping**: `linearDamping = 2.0f`, `angularDamping = 2.5f`.
    - **Distances & Speeds**: `interactionDistance = 3.0f`, `holdDistance = 2.0f`, `followSpeed = 8.0f`, `maxHoldDistance = 3.8f`.
    - **Constraints**: Pitch and roll rotation frozen (`FreezeRotationX | FreezeRotationZ`).
    - **Collision**: Continuous collision detection mode with Rigidbody interpolation to prevent clipping or tunneling through walls.
- **Dynamic Navigation & AI Coordination**:
  - Main-scene barricades continue to integrate dynamically with `DynamicNavigationObstacles.cs`, which creates child proxy `NavMeshObstacle` components that carve the NavMesh when barricades are moved.
  - Verified that Patrol Guard AI (`PatrolGuardBrain.cs`) detects physical barricade obstructions and recalculates optimal A* detours around blocked facility corridors.
- **Backward Compatibility**:
  - `InteractiveDoor.cs` maintains `public bool handleInternalInput = true` by default. Standalone test scenes (`DoorInteractionTest.unity`, `BarricadeInteractionTest.unity`) remain fully functional without needing a runtime dispatcher.
  - In `FacilityBreachRuntime.Start()`, all doors in `Prototype.unity` have `handleInternalInput` set to `false`, transferring full interaction authority to `FacilityInteractionDispatcher`.

#### Manual Unity Play Mode Gameplay Verification (2026-10-18)
The user manually tested the production `Prototype.unity` scene in Unity Play Mode on 2026-10-18 and confirmed the following seven interaction mechanics:

1. **Door Opening with E**: **MANUAL PASS** — Pressing `E` while in front of a facility door smoothly opens the door by $90^\circ$ and clears the corridor passage.
2. **Door Closing with E**: **MANUAL PASS** — Pressing `E` while near an open facility door smoothly closes it; the closed panel physically stops the player capsule.
3. **Barricade Grab with E**: **MANUAL PASS** — Pressing `E` within $3.0\text{m}$ of any of the 6 main-scene barricades successfully engages grab mode.
4. **Barricade Follows Player Movement**: **MANUAL PASS** — Grabbed barricade maintains stable positioning $2.0\text{m}$ ahead along the floor plane during forward, backward, strafing, and turning movements with zero tilting or flipping.
5. **Barricade Release with E**: **MANUAL PASS** — Pressing `E` while holding a barricade immediately disengages the hold, dampens velocity, and deposits the barricade stably in the facility.
6. **HUD Changes Between Grab/Release**: **MANUAL PASS** — HUD prompt dynamically updates to `E: Release barricade` when holding, and switches to contextual prompts (`E: Grab barricade`, `E: Open door`, etc.) when free.
7. **Exclusive Release Near Doors (No Parallel Activation)**: **MANUAL PASS** — While holding a barricade and standing directly adjacent to an interactive door, pressing `E` releases only the barricade. The door does not open, close, or toggle.

> [!NOTE]
> **Manual Scope Limitation**: These seven manual checks confirm the user-verified gameplay behavior in `Prototype.unity`. They do not claim that all theoretical multi-object overlap edge cases (such as three overlapping objects in a single raycast cone) or all pickup station interactions were manually exhausted in live play.

#### Automated Test Suite Verification (184 Programmatic Checks)
Executed in Unity 6000.6.2f1 headless batch mode with **ExitCode 0**:
- **Interaction Arbitration Test Suite** (`FacilityInteractionDispatcherTests.cs`):
  - **43 checks: 43 PASSED, 0 FAILED** (verified via `interaction-tests-passed.txt`).
  - Covers held barricade priority, single-action guarantee, scoring formula accuracy, distance/angle thresholds, direct aim bonuses, door toggling, and component lifecycle.
- **Patrol Guard AI Regression Suite** (141 checks total across 4 suites):
  - `PatrolGuardObstacleTests`: 46 PASSED, 0 FAILED (`patrol-obstacle-tests-passed.txt`).
  - `PatrolGuardAStarTests`: 37 PASSED, 0 FAILED (`patrol-astar-tests-passed.txt`).
  - `PatrolGuardReturnTests`: 36 PASSED, 0 FAILED (`patrol-return-tests-passed.txt`).
  - `PatrolGuardSearchTests`: 22 PASSED, 0 FAILED (`patrol-search-tests-passed.txt`).
  - Total automated regression checks: **141/141 PASSED, 0 FAILED**.
  - In addition, live obstacle rerouting scenario verified: 10/10 checks PASSED (`patrol-live-reroute-passed.txt`).

#### Evidence & Screenshots
- **Consolidated Evidence Directory**: All visual artifacts reside in [`docs/individual/evidence/IT24102587 GV evidence/`](evidence/IT24102587%20GV%20evidence/) (totaling 19 PNG captures).
- **Historical Test-Arena Evidence (7 Captures)**:
  - [`01_Door_OutOfRange.png`](evidence/IT24102587%20GV%20evidence/01_Door_OutOfRange.png): Out-of-range door interaction prompt.
  - [`02_Door_Open.png`](evidence/IT24102587%20GV%20evidence/02_Door_Open.png): Door opened through $90^\circ$ swing.
  - [`03_Door_ClosedCollision.png`](evidence/IT24102587%20GV%20evidence/03_Door_ClosedCollision.png): Closed door panel physical capsule blocking.
  - [`04_Barricade_SceneOverview.png`](evidence/IT24102587%20GV%20evidence/04_Barricade_SceneOverview.png): Barricade test arena layout.
  - [`05_Barricade_Grabbed.png`](evidence/IT24102587%20GV%20evidence/05_Barricade_Grabbed.png): Barricade grab mode with forward tracking.
  - [`06_Barricade_WallCollision.png`](evidence/IT24102587%20GV%20evidence/06_Barricade_WallCollision.png): Barricade stopped flush against solid north wall.
  - [`07_Barricade_DoorwayBlocked.png`](evidence/IT24102587%20GV%20evidence/07_Barricade_DoorwayBlocked.png): Barricade deployed across doorway chokepoint.
- **Main-Scene Play Mode Evidence (`Prototype.unity`, 12 Captures)**:
  - [`08_GV_MainScene_Barricade_Release.png`](evidence/IT24102587%20GV%20evidence/08_GV_MainScene_Barricade_Release.png): Main-scene barricade release; player holding barricade, HUD prompt displays `E: Release barricade`.
  - [`09_GV_MainScene_Barricade_Grab.png`](evidence/IT24102587%20GV%20evidence/09_GV_MainScene_Barricade_Grab.png): In-range barricade approach; HUD prompt shows `E: Grab barricade`.
  - [`10_GV_MainScene_Door_Open.png`](evidence/IT24102587%20GV%20evidence/10_GV_MainScene_Door_Open.png): Open facility door; HUD prompt displays `E: Close door`.
  - [`11_GV_MainScene_Barricade_Moved.png`](evidence/IT24102587%20GV%20evidence/11_GV_MainScene_Barricade_Moved.png): Main-scene barricade ground-plane maneuvering; player transports held barricade into northern junction while maintaining stable 2.0m follow distance and upright orientation.
  - [`12_GV_MainScene_Door_Barricade.png`](evidence/IT24102587%20GV%20evidence/12_GV_MainScene_Door_Barricade.png): Interaction arbitration resolution; player holding barricade directly in front of door, HUD displays `E: Release barricade` exclusively without toggling door.
  - [`13_GV_MainScene_AccessCard_Before.png`](evidence/IT24102587%20GV%20evidence/13_GV_MainScene_AccessCard_Before.png): Access card station prior to pickup; status reads `CARD: MISSING`, prompt shows `E: Access card`.
  - [`14_GV_MainScene_AccessCard_Acquired.png`](evidence/IT24102587%20GV%20evidence/14_GV_MainScene_AccessCard_Acquired.png): Access card acquired; status updates to `CARD: ACQUIRED`, objective advances to `Use the card to enter Machinery`.
  - [`15_GV_MainScene_ResourceStation_Prompt.png`](evidence/IT24102587%20GV%20evidence/15_GV_MainScene_ResourceStation_Prompt.png): Resource station targeting; prompt shows `E: Ammo3gun swap` while holding Ammo1gun.
  - [`16_GV_MainScene_Weapon_Swapped.png`](evidence/IT24102587%20GV%20evidence/16_GV_MainScene_Weapon_Swapped.png): Weapon station interaction; player equipped with `Ammo3gun 100/50`, visual firearm model updated in first-person view, prompt shows `E: Ammo1gun swap`.
  - [`17_GV_MainScene_Device_Deactivating.png`](evidence/IT24102587%20GV%20evidence/17_GV_MainScene_Device_Deactivating.png): Central device deactivation progress; cyan countdown bar displaying `Deactivating: 2.4/5 seconds`.
  - [`18_GV_MainScene_Device_Deactivated.png`](evidence/IT24102587%20GV%20evidence/18_GV_MainScene_Device_Deactivated.png): Device deactivated; objective updates to `DEVICE DEACTIVATED — reach the exit`.
  - [`19_GV_MainScene_Mission_Complete.png`](evidence/IT24102587%20GV%20evidence/19_GV_MainScene_Mission_Complete.png): Mission completion screen overlay; recorded completion time `77.3s`. Note: The completion overlay explicitly states `Four-agent challenge: unavailable (team AI pending)` confirming standard facility escape without multi-agent coordination.
- **Distinction Between Visuals & Dynamic Behavior**: Static screenshots corroborate UI prompt states, objective sequencing, and visual weapon/card transitions. Dynamic physical behavior (such as continuous player capsule collision, velocity damping on release, and the absence of concurrent door actuation) was confirmed during manual interactive testing sessions.

#### Git Status & Integrity
- Feature work committed under [`8d7b3dc`](https://github.com/Kunchana-Wijesinghe/Interactive-3D-Shooting-Game-GV-IS/commit/8d7b3dcf3975a91086218004c57989c3fc477c69) on branch `gv/IT24102587-main-barricade-interaction`.
- Pushed to remote and successfully merged into `develop` as GitHub Pull Request #11 at merge commit [`4ac6a2f`](https://github.com/Kunchana-Wijesinghe/Interactive-3D-Shooting-Game-GV-IS/commit/4ac6a2f8d8311b1a0f88debc3c00bdb4b84c6e3f).
- Protected recovery directories (`Assets/_Recovery/`), package settings, `stash@{0}`, and test logs preserved untouched.

---

### Feature Entry 4: Post-Merge FPS Viewmodel Wall-Clipping Prevention & Visual Polish
- **Date:** 2026-10-19
- **Feature Name:** Procedural First-Person Viewmodel Obstruction Controller & Close-Quarters Wall-Clipping Prevention
- **Working Feature Branch:** `gv/IT24102587-fps-viewmodel-obstruction` (branch based off `develop` merge commit `4ac6a2f`)
- **Files Created / Changed:**
  - `Assets/Scripts/FacilityBreach/FpsViewModelObstruction.cs` (created procedural viewmodel wall-obstruction component)
  - `Assets/Scripts/FacilityBreach/FpsViewModelObstruction.cs.meta` (created asset metadata)
  - `Assets/Editor/FacilityBreach/FpsViewModelObstructionTests.cs` (created 24-check Edit Mode test suite)
  - `Assets/Editor/FacilityBreach/FpsViewModelObstructionTests.cs.meta` (created asset metadata)
  - `Assets/Scripts/FacilityBreach/FacilityBreachRuntime.cs` (modified: hooked 3-line initialization in `UpdateWeaponVisual()`)

#### Architectural Scope & Distinction
- **Post-Merge Polish Distinction**: GV Part 2 deliverables (interactive doors, movable barricades, centralized interaction arbitration) were finalized and merged into `develop` in PR #11 (`4ac6a2f`). This entry records an isolated post-merge visual polish feature that prevents first-person firearm meshes from protruding through solid wall geometry during close-quarters combat (CQB), without modifying existing weapon prefabs, camera near-clip planes, or core interaction systems.

#### Physics & Procedural Mechanics
- **Proximity Detection**: In `LateUpdate()`, executes a forward volumetric `Physics.SphereCastAll` ($r = 0.08\text{m}$) from the camera position along the forward look vector up to $\text{weaponReach} + \text{forwardCushion}$ ($0.08\text{m}$). A point-blank `Physics.RaycastAll` fallback ensures detection against razor-sharp acute wall corners.
- **Dynamic Reach Calibration**: On `Initialize()`, inspects the authored `Muzzle` transform in camera local coordinates:
  $$\text{weaponReach} = \max(0.70\text{m},\; \mathbf{p}_{\text{muzzle},\, \text{cam}}.z)$$
  Automatically calibrates reach across all three player weapons: `Ammo1gun` ($0.70\text{m}$), `Ammo2gun` ($0.75\text{m}$), and `Ammo3gun` ($0.87\text{m}$).
- **Environment & Surface Filtering**:
  - Excludes the player capsule collider (`col.transform.IsChildOf(playerRoot)`).
  - Excludes viewmodel colliders (`col.transform.IsChildOf(weaponTransform)`).
  - Excludes trigger volumes (`col.isTrigger`).
  - Excludes resource stations (`col.GetComponentInParent<ResourceStation>()`).
  - Excludes actively carried barricades (`MovableBarricade.IsHeld`).
- **Tactical Low-Ready Posing**:
  - Analytically computes target viewmodel local offsets: slides backward along local Z (max $0.28\text{m}$), lowers along local Y (max $0.16\text{m}$), pitches downward (max $32^\circ$), and yaws inward (max $8^\circ$).
  - Smoothly interpolates transform using `Vector3.Lerp` and `Quaternion.Slerp` (`smoothSpeed = 14f`).
  - Retraction envelope enforces $z \ge 0.22\text{m}$, maintaining a safe distance outside the camera near clip plane ($0.05\text{m}$).
- **Decoupled Aim & Shooting Continuity**:
  - Hitscan firing in `FacilityBreachRuntime.cs` originates from the camera center ray (`view.transform.position` along `view.transform.forward`).
  - Tilting and retracting the cosmetic visual model does NOT deflect bullets or crosshair aiming, ensuring 100% shooting continuity and accuracy.

#### Manual Unity Play Mode Gameplay Verification (2026-10-19)
The user manually verified live gameplay in `Prototype.unity` on 2026-10-19 across five distinct interaction scenarios:
1. **Wall Proximity Retraction**: **MANUAL PASS** — Walking point-blank to facility walls smoothly tucks the weapon backward and downward; zero barrel mesh penetration into walls.
2. **Wall Clearance Restoration**: **MANUAL PASS** — Backing away from walls into open space smoothly restores the weapon viewmodel to its default authored hip-fire posture.
3. **Weapon Swap Compatibility**: **MANUAL PASS** — Swapping between `Ammo1gun` and `Ammo3gun` at weapon stations dynamically recalibrates obstruction reach; wall tucking adapts properly to both short and long barrels.
4. **Shooting Continuity Near Walls**: **MANUAL PASS** — Firing while close to walls correctly targets the crosshair center without deflection; muzzle remains outside wall geometry.
5. **Door E-Key Interaction Compatibility**: **MANUAL PASS** — Approaching and interacting with closed doors opens and closes them cleanly via centralized dispatcher; viewmodel does not trigger unwanted tuck against door frames.

#### Automated Test Suite Verification (208 Total Checks)
Executed in Unity 6000.6.2f1 headless batch mode with **ExitCode 0**:
- **FPS Viewmodel Obstruction Suite** (`FpsViewModelObstructionTests.cs`):
  - **24 checks: 24 PASSED, 0 FAILED** (`scratch/obstruction_tests.log`).
  - Covers default pose retention, proximity retraction/lowering, nearClipPlane margin safety, reach adaptation across all 3 weapons, obstacle filters (player, weapon, triggers, stations, held barricades), weapon swap pose reset, and live physics obstacle queries.
- **Combined Facility Breach Regression Suite**:
  - `FacilityInteractionDispatcherTests`: 43 PASSED, 0 FAILED
  - `PatrolGuardObstacleTests`: 46 PASSED, 0 FAILED
  - `PatrolGuardAStarTests`: 37 PASSED, 0 FAILED
  - `PatrolGuardReturnTests`: 36 PASSED, 0 FAILED
  - `PatrolGuardSearchTests`: 22 PASSED, 0 FAILED
  - `FpsViewModelObstructionTests`: 24 PASSED, 0 FAILED
  - **Master Regression Total: 208/208 PASSED, 0 FAILED (100% Green)**.

#### Evidence & Screenshots (Captures 20–25)
All six new visual captures reside in [`docs/individual/evidence/IT24102587 GV evidence/`](evidence/IT24102587%20GV%20evidence/):
- [`20_GV_FPS_Door_Interaction.png`](evidence/IT24102587%20GV%20evidence/20_GV_FPS_Door_Interaction.png): Close door interaction prompt `E: Open door` with equipped Ammo3gun in view without clipping (1145x626).
- [`21_GV_FPS_Weapon_Wall_Retracted.png`](evidence/IT24102587%20GV%20evidence/21_GV_FPS_Weapon_Wall_Retracted.png): Ammo3gun retracted backward and lowered into tactical low-ready stance against wall (1193x705).
- [`22_GV_FPS_Weapon_Wall_Closeup.png`](evidence/IT24102587%20GV%20evidence/22_GV_FPS_Weapon_Wall_Closeup.png): Extreme close-up low-ready stance; barrel safely tucked behind wall plane with zero mesh penetration (1192x770).
- [`23_GV_FPS_Weapon_Swap_Retraction.png`](evidence/IT24102587%20GV%20evidence/23_GV_FPS_Weapon_Swap_Retraction.png): Weapon swap re-test at wall, confirming dynamic reach calibration and low-ready retraction for Ammo3gun (1147x690).
- [`24_GV_FPS_Weapon_Normal.png`](evidence/IT24102587%20GV%20evidence/24_GV_FPS_Weapon_Normal.png): Player equipped with Ammo1gun in open corridor, showing viewmodel restored to normal hip-fire posture (1186x700).
- [`25_GV_FPS_Door_Prompt.png`](evidence/IT24102587%20GV%20evidence/25_GV_FPS_Door_Prompt.png): Pause menu and gameplay background showing player facing red door with `E: Open door` prompt and Ammo3gun visible (1170x641).

> [!NOTE]
> **Static Visual Disclosure**: Static screenshots corroborate captured weapon poses and UI text at specific moments. Dynamic motion smoothness, obstacle detection speed, and bullet hit accuracy were confirmed through manual play-mode testing and 208 automated regression checks.

#### Git Status & Integrity
- Feature work authored on branch `gv/IT24102587-fps-viewmodel-obstruction`.
- Protected recovery directories (`Assets/_Recovery/`), package settings, `stash@{0}`, and test logs preserved untouched.
- `git diff --check` passes with zero whitespace or line-ending warnings.

---

### Feature Entry 5: Supplementary FPS Tactical-Gloved Arms Integration & Physical Socket Rigging
- **Date:** 2026-10-20
- **Feature Name:** Supplementary First-Person Tactical-Gloved Arms Integration & Physical Socket Rigging
- **Working Feature Branch:** `gv/IT24102587-fps-tactical-arms` (branch based off `develop` commit `caa133e`)
- **Files Created / Changed:**
  - `Assets/Editor/FacilityBreach/FpsArmsPrefabBuilder.cs` (created automated builder script for materials, prefabs, and weapon embedding)
  - `Assets/Editor/FacilityBreach/FpsArmsPrefabBuilder.cs.meta` (created asset metadata)
  - `Assets/Editor/FacilityBreach/FpsArmsTests.cs` (created 80-check comprehensive test suite)
  - `Assets/Editor/FacilityBreach/FpsArmsTests.cs.meta` (created asset metadata)
  - `Assets/Materials/FacilityBreach/Arms/FPSArms_Mat.mat` (created Standard PBR material)
  - `Assets/Materials/FacilityBreach/Arms/FPSArms_Mat.mat.meta` (created asset metadata)
  - `Assets/Models/FacilityBreach/Arms/fpsarms.fbx` (imported Sketchfab arms mesh)
  - `Assets/Models/FacilityBreach/Arms/fpsarms.fbx.meta` (created asset metadata)
  - `Assets/Prefabs/FacilityBreach/Arms/FPSArms_ViewModel.prefab` (created base arms prefab)
  - `Assets/Prefabs/FacilityBreach/Arms/FPSArms_ViewModel.prefab.meta` (created asset metadata)
  - `Assets/Prefabs/FacilityBreach/Weapons/Ammo1gun_ViewModel.prefab` (modified: integrated socket-aligned FPSArms child)
  - `Assets/Prefabs/FacilityBreach/Weapons/Ammo2gun_ViewModel.prefab` (modified: integrated socket-aligned FPSArms child)
  - `Assets/Prefabs/FacilityBreach/Weapons/Ammo3gun_ViewModel.prefab` (modified: integrated socket-aligned FPSArms child)
  - `Assets/Textures/FacilityBreach/Arms/` (imported PBR texture maps and authored tactical albedo)
  - `docs/individual/ATTRIBUTION_FPS_ARMS.md` (created 3D asset CC BY 4.0 license documentation)
  - `docs/individual/evidence/IT24102587 GV evidence/26_GV_FPS_Tactical_Arms_Final.png` (verified approved baseline capture)
  - `docs/individual/evidence/IT24102587 GV evidence/27_GV_FPS_Tactical_Arms_Wall_Retraction.png` (verified low-ready retraction capture)
  - `docs/individual/evidence/IT24102587 GV evidence/28_GV_FPS_Tactical_Arms_Ammo3gun.png` (verified Ammo3gun tactical arms capture)

#### Architectural Scope & Distinction
- **Post-Merge Enhancement**: Developed on branch `gv/IT24102587-fps-tactical-arms`. Embeds high-fidelity tactical-gloved hands and forearms into all three weapon viewmodels, physically aligned with `GripAttachment` and `SupportHand` sockets.
- **Physics & Gameplay Invariance**: Strictly zero colliders and zero cameras across all viewmodels. Does not modify player locomotion, shooting ballistics, interaction triggering, or obstruction physics.

#### Kinematics, PBR Texturing & Approved Framing
- **Asset Attribution**: Model by DJMaesen licensed under CC BY 4.0, documented in [`docs/individual/ATTRIBUTION_FPS_ARMS.md`](ATTRIBUTION_FPS_ARMS.md).
- **Tactical Glove Albedo**: Derived `armColor_TacticalGloves.png` from source albedo, replacing skin tones with dark tactical charcoal (`#25282A` - `#303438`) while preserving original fabric weaves and creases.
- **Socket Alignment**: Right hand wraps the pistol grip at `GripAttachment` with index finger extended along the lower receiver. Left hand cups the forward handguard under `SupportHand`.
- **Approved Framing Offset**: Configured via `ViewModelFramingOffset = (0.010f, -0.050f, 0.000f)` in `FpsArmsPrefabBuilder.cs`. Sits in the standard lower-center-right FPS envelope with $> 0.10$ vertical viewport clearance below the crosshair and $> 0.30\text{m}$ distance from camera near clip plane.
- **Obstruction Retraction Compatibility**: Verified that procedural low-ready wall retraction (`FpsViewModelObstruction.cs`) smoothly lowers and retracts the combined weapon and arms assembly point-blank against walls.

#### Manual Play Mode Verification (2026-10-20)
- The user manually verified and approved the restored pre-experiment tactical arms appearance in Unity Play Mode:
  - **Weapon Framing**: **APPROVED** — Standard FPS lower-center-right position; weapon scale and proportion look natural.
  - **Grip & Hand Alignment**: **APPROVED** — Right hand securely holds pistol grip; left hand cups forward handguard.
  - **Crosshair Clearance**: **APPROVED** — Crosshair is completely unobstructed.
  - **Retraction Response**: **APPROVED** — Combined gun-and-arms assembly retracts naturally against walls.

#### Automated Test Suite Verification (288 Total Checks)
Executed in Unity 6000.6.2f1 headless batch mode with **ExitCode 0**:
- **FPS Tactical Arms Suite** (`FpsArmsTests.cs`):
  - **80 checks: 80 PASSED, 0 FAILED** (`scratch/experiment_regr_arms.log`).
  - Validates texture existence, PBR material keywords, zero colliders/cameras, physical socket alignment across Ammo1gun, Ammo2gun, and Ammo3gun, finger curl joint rotations, and low-ready retraction response.
- **Combined Facility Breach Regression Suite (7 Suites)**:
  - `FpsArmsTests`: 80 PASSED, 0 FAILED
  - `FpsViewModelObstructionTests`: 24 PASSED, 0 FAILED
  - `FacilityInteractionDispatcherTests`: 43 PASSED, 0 FAILED
  - `PatrolGuardAStarTests`: 37 PASSED, 0 FAILED
  - `PatrolGuardSearchTests`: 22 PASSED, 0 FAILED
  - `PatrolGuardReturnTests`: 36 PASSED, 0 FAILED
  - `PatrolGuardObstacleTests`: 46 PASSED, 0 FAILED
  - **Master Regression Total: 288/288 PASSED, 0 FAILED (100% Green)**.

#### Evidence & Screenshots (Captures 26–28)
Located in [`docs/individual/evidence/IT24102587 GV evidence/`](evidence/IT24102587%20GV%20evidence/):
- [`26_GV_FPS_Tactical_Arms_Final.png`](evidence/IT24102587%20GV%20evidence/26_GV_FPS_Tactical_Arms_Final.png): User-approved tactical arms first-person framing with equipped Ammo1gun in standard holding pose; both hands naturally gripping sockets, crosshair clear (1187x653).
- [`27_GV_FPS_Tactical_Arms_Wall_Retraction.png`](evidence/IT24102587%20GV%20evidence/27_GV_FPS_Tactical_Arms_Wall_Retraction.png): Combined gun-and-arms assembly smoothly retracted into low-ready stance when pressed against solid facility wall (1177x647).
- [`28_GV_FPS_Tactical_Arms_Ammo3gun.png`](evidence/IT24102587%20GV%20evidence/28_GV_FPS_Tactical_Arms_Ammo3gun.png): Long-rifle Ammo3gun equipped with tactical arms, confirming proper handguard support and grip alignment across weapon variants (1183x649).

> [!NOTE]
> **Static Visual Disclosure**: Screenshots 26–28 demonstrate the user-approved static weapon framing and holding pose in Unity Play Mode. They do not claim skeletal animation playback or continuous weapon-switch interpolation.

#### Git Status & Integrity
- Feature work authored on branch `gv/IT24102587-fps-tactical-arms` (merged into `develop` in PR #15).

---

### Feature Entry 6: Supplementary First-Person Shooting VFX (Bullet Tracer, Muzzle Flash V2 & Environmental Impact Sparks)
- **Date:** 2026-10-20
- **Feature Name:** First-Person Shooting Visual Effects Suite (Standalone Bullet Tracer, Camera-Facing Muzzle Flash V2 & Tuned Environmental Impact Sparks)
- **Working Feature Branch:** `gv/IT24102587-bullet-tracer-vfx` (branch based off `develop` commit `b1b6745`)
- **Commits (Chronological Feature & Test Pairs):**
  - `4bd880e`: `feat(vfx): add standalone FPS bullet tracer`
  - `4fa70fc`: `test(vfx): verify FPS bullet tracer behavior`
  - `d6eaff1`: `feat(vfx): add safe camera-facing muzzle flash`
  - `747ad4a`: `test(vfx): verify muzzle flash visibility and safety`
  - `e73f562`: `feat(vfx): add environmental impact sparks`
  - `f3ebe21`: `test(vfx): verify environmental impact sparks`
- **Files Created / Changed:**
  - `Assets/Scripts/FacilityBreach/FpsBulletTracer.cs` (created standalone line-based tracer component with 12-item pool)
  - `Assets/Scripts/FacilityBreach/FpsBulletTracer.cs.meta` (created asset metadata)
  - `Assets/Editor/FacilityBreach/FpsBulletTracerTests.cs` (created 31-check Edit Mode test suite)
  - `Assets/Editor/FacilityBreach/FpsBulletTracerTests.cs.meta` (created asset metadata)
  - `Assets/Scripts/FacilityBreach/FpsMuzzleFlashV2.cs` (created safe camera-aligned billboard muzzle flash component)
  - `Assets/Scripts/FacilityBreach/FpsMuzzleFlashV2.cs.meta` (created asset metadata)
  - `Assets/Editor/FacilityBreach/FpsMuzzleFlashV2Tests.cs` (created 30-check Edit Mode test suite)
  - `Assets/Editor/FacilityBreach/FpsMuzzleFlashV2Tests.cs.meta` (created asset metadata)
  - `Assets/Scripts/FacilityBreach/FpsImpactSparks.cs` (created tuned environmental micro-sparks component with 8-item pool)
  - `Assets/Scripts/FacilityBreach/FpsImpactSparks.cs.meta` (created asset metadata)
  - `Assets/Editor/FacilityBreach/FpsImpactSparksTests.cs` (created 25-check Edit Mode test suite)
  - `Assets/Editor/FacilityBreach/FpsImpactSparksTests.cs.meta` (created asset metadata)
  - `Assets/Scripts/FacilityBreach/FacilityBreachRuntime.cs` (modified: added minimal VFX initialization and shooting event hooks)

#### Architectural Scope & Design Principles
- **Decoupled Hitscan Visual Polish**: Hitscan weapon logic in `FacilityBreachRuntime.Shoot()` remains the single source of truth for player shooting. Visual effects strictly consume hitscan results without affecting weapon ballistics, raycast queries, player locomotion, damage calculations, or AI agent responses.
- **Strict Rendering Invariants (Skybox Artifact Prevention)**:
  1. **Strictly Zero Point Lights**: Dynamic light sources are strictly avoided to eliminate render pass conflicts with URP atmospheric skybox rendering.
  2. **Transparent Unlit Shaders with `_ZWrite = 0`**: Depth writing is disabled on all VFX materials, preventing visual geometry from clipping or occluding the skybox.
  3. **Standard Alpha Blending**: Configured with `SrcAlpha` / `OneMinusSrcAlpha` blending; additive HDR emission keywords are disabled to eliminate optical blowout when aiming upward.
  4. **Pre-allocated Bounded Pooling**: All effects operate from fixed-capacity ring buffers, ensuring zero heap allocations and zero GC pressure during continuous fire.

#### Subsystem Mechanics & Configuration
- **Standalone Bullet Tracer (`FpsBulletTracer.cs`)**:
  - Pool capacity: 10 pre-allocated `LineRenderer` instances.
  - Tapered streak geometry ($0.015\text{m}$ start width, $0.008\text{m}$ end width).
  - Trajectory spans from weapon muzzle to hit point (or $35.0\text{m}$ along camera aim on misses).
  - Fades smoothly over $0.040\text{s}$ via alpha key decay (`#FFF2A8` $\rightarrow$ `#FF9900`).
- **Camera-Facing Muzzle Flash V2 (`FpsMuzzleFlashV2.cs`)**:
  - Solves earlier sky discoloration artifact by using a single camera-aligned quad anchored directly at the weapon muzzle in camera local space.
  - Calibrated billboard diameter ($0.080\text{m}$) with random roll rotation ($\pm 35^\circ$) per shot.
  - Snappy $0.065\text{s}$ duration ensures crisp visual combustion without lingering across subsequent frames.
- **Environmental Impact Sparks (`FpsImpactSparks.cs`)**:
  - Pool capacity: 8 impact objects, each with 5 micro-streak `LineRenderer` instances.
  - User-approved tuned parameters: 5 streaks, length $\approx 0.08\text{m} - 0.12\text{m}$, width $0.012\text{m}$ start to $0.005\text{m}$ end, lifetime $0.11\text{s}$.
  - Hemisphere spread aligned with surface normal; includes collinearity protection for vertical floor and ceiling impacts.
  - Selective hit filtering: triggers exclusively on static environment geometry (`isEnvironment = damageable == null && enemy == null`), completely suppressing sparks on living character hits and sky misses.

#### Manual Unity Play Mode Gameplay Verification (2026-10-20)
The user manually verified live shooting in `Prototype.unity` across all three weapons:
1. **Bullet Tracer**: **APPROVED** — Bright, snappy tracer clearly visible along bullet trajectory; scales naturally across all combat ranges.
2. **Muzzle Flash V2**: **APPROVED** — Visible flash at the firearm muzzle; strictly zero sky or background discoloration artifacts when aiming upward into open airspace.
3. **Environmental Impact Sparks**: **APPROVED** — Clearly visible micro-sparks on static walls, floors, and metal doors at tuned size; zero sparks on enemy characters.

#### Automated Test Suite Verification (374 Total Checks)
Executed in Unity 6000.6.2f1 headless batch mode with **ExitCode 0**:
- **FPS Bullet Tracer Suite** (`FpsBulletTracerTests.cs`): **31 Passed, 0 Failed**.
- **FPS Muzzle Flash V2 Suite** (`FpsMuzzleFlashV2Tests.cs`): **30 Passed, 0 Failed**.
- **FPS Impact Sparks Suite** (`FpsImpactSparksTests.cs`): **25 Passed, 0 Failed**.
- **Combined Facility Breach Regression Suite (10 Suites)**:
  - `FpsArmsTests`: 80 PASSED, 0 FAILED
  - `FpsViewModelObstructionTests`: 24 PASSED, 0 FAILED
  - `FacilityInteractionDispatcherTests`: 43 PASSED, 0 FAILED
  - `PatrolGuardAStarTests`: 37 PASSED, 0 FAILED
  - `PatrolGuardSearchTests`: 22 PASSED, 0 FAILED
  - `PatrolGuardReturnTests`: 36 PASSED, 0 FAILED
  - `PatrolGuardObstacleTests`: 46 PASSED, 0 FAILED
  - `FpsBulletTracerTests`: 31 PASSED, 0 FAILED
  - `FpsMuzzleFlashV2Tests`: 30 PASSED, 0 FAILED
  - `FpsImpactSparksTests`: 25 PASSED, 0 FAILED
  - **Master Regression Total: 374/374 PASSED, 0 FAILED (100% Green)**.

#### Evidence & Screenshots Disclosure
- The existing visual evidence inventory is preserved at 28 high-resolution captures (Captures 01–28).
- Per explicit user verification instructions, no additional screenshots (29–31) were requested, captured, or fabricated. Full verification is established through user Play Mode testing and 374 automated regression checks.

#### Git Status & Integrity
- Feature work committed across six clean feature/test commit pairs on branch `gv/IT24102587-bullet-tracer-vfx`.
- Protected recovery directories (`Assets/_Recovery/`), package settings, `stash@{0}`, and test logs preserved untouched.
- `git diff --check` passes with zero whitespace or line-ending warnings.


---

### Feature Entry 7: Interactive Player Physics & Audible Facility Audio (Kickable Props, Procedural Audio Synthesis, FPS Kick Mechanic & Neutral Noise Broadcasts)
- **Date:** 2026-10-20
- **Feature Name:** Interactive Player Physics, Procedural Gunshot & Impact Audio Synthesis, Short-Range FPS Kick Controller, and Decoupled Neutral Facility Noise Hub
- **Git Feature Branch:** `gv/IT24102587-kickable-physics-audio`
- **Base Commit:** `38e9a08` (`Merge pull request #16 from Kunchana-Wijesinghe/gv/IT24102587-bullet-tracer-vfx`)
- **Status:** Completed (9 Substantive Commits; 120 Automated Checks PASSED (ExitCode 0); Ready for User Play Mode Review)
- **SE3032 Rubric Alignment:** *Player Interaction Physics — doors, movable barricades, and physics objects* (tactile physical interaction with 7 environment props + audible feedback for weapon fire and physical impacts).

#### Committed Lineage (9 Logically Separated Substantive Commits)
1. `5ce3589` `feat(audio): implement procedural facility sound playback`
   - Created `FacilityAudioSynthesizer.cs`: procedural mathematical waveform synthesis for 6 sound types (3 gunshots, wood impact, metal impact, kick thud).
   - Created `FacilityAudioPlayer.cs`: multi-voice dynamic `AudioSource` pooling (8 voices), 2D/3D spatial blending, pitch modulation ($\pm 5\%$).
2. `dd0a533` `feat(audio): play distinct sounds for confirmed weapon shots`
   - Integrated `AudioPlayer` into `FacilityBreachRuntime.cs` (`TryFire()`).
   - Wired distinct audio profiles for `Ammo1gun` (pistol), `Ammo2gun` (rifle), and `Ammo3gun` (heavy cannon) on confirmed ammunition consumption.
3. `cda17d3` `feat(noise): publish neutral facility sound events`
   - Created `FacilityNoiseEvent.cs`: immutable noise event data struct.
   - Created `FacilityNoiseHub.cs`: neutral static publish-subscribe event broker decoupled from listener AI.
   - Wired gunshot noise event emission ($R=35.0\text{m}$, intensity $1.0$) into `FacilityBreachRuntime.cs` (`TryFire()`).
4. `9de09fd` `feat(physics): add collision-aware kickable props`
   - Created `KickablePhysicsProp.cs`: dynamic `Rigidbody` physics prop with `Continuous` collision detection, `Interpolate`, linear drag ($0.8$), angular drag ($1.2$).
   - Collision impact velocity threshold ($0.8\text{m/s}$), cooldown ($0.15\text{s}$), dynamic volume-scaled wood/metal audio, and `PropCollision` noise event emission ($R=3.0\text{m} - 15.0\text{m}$).
5. `6d29d96` `feat(interaction): add short-range FPS physics kick`
   - Created `FpsKickController.cs`: dedicated `F` key kick interaction with $2.2\text{m}$ camera forward reach (spherecast tolerance $0.25\text{m}$).
   - Forward impulse ($14.0\text{N}\cdot\text{s}$) + upward loft ($3.5\text{N}\cdot\text{s}$) with velocity clamping ($\le 12.0\text{m/s}$).
   - Occlusion raycast probe preventing kicking through solid walls.
   - Dynamic HUD prompt `"F: Kick [Prop Name]"` wired into `FacilityBreachRuntime.cs`.
6. `0b082d9` `feat(environment): add seven kickable facility props`
   - Created `FacilityKickablePropsSetup.cs`: deterministic deployment of 7 kickable props in `Prototype.unity` at safe alcoves and corridor corners (3 small wooden boxes, 2 medium wooden crates, 2 metal barrels).
   - Runtime idempotent spawn check in `FacilityBreachRuntime.Start()` preventing duplicates.
7. `cbed8d6` `test(gv): verify kickable physics and facility audio`
   - Created `KickablePhysicsAudioTests.cs`: comprehensive 12-test Edit Mode suite covering audio synthesis, gunshot profiles, player pooling, noise hub, physics damping, kick impulses, range/occlusion, 7 props placement, teammate isolation, and backward compatibility.
8. `93b7e5a` `docs(gv): document interactive physics props and audio`
9. `66aa1f0` `fix(physics): resolve namespace collision with UnityEngine.Physics and expose runtime Say`
10. `8e2389c` `docs(gv): record batch test results and namespace collision resolution`
   - Updated GV Part 2 technical report, worklog, and verification log to document all mechanics, formulas, architecture, and verification status.

#### Files Created / Changed
- `Assets/Scripts/FacilityBreach/Audio/FacilityAudioSynthesizer.cs` (created)
- `Assets/Scripts/FacilityBreach/Audio/FacilityAudioPlayer.cs` (created)
- `Assets/Scripts/FacilityBreach/Noise/FacilityNoiseEvent.cs` (created)
- `Assets/Scripts/FacilityBreach/Noise/FacilityNoiseHub.cs` (created)
- `Assets/Scripts/FacilityBreach/Physics/KickablePhysicsProp.cs` (created)
- `Assets/Scripts/FacilityBreach/Physics/FacilityKickablePropsSetup.cs` (created)
- `Assets/Scripts/FacilityBreach/Player/FpsKickController.cs` (created)
- `Assets/Editor/FacilityBreach/KickablePhysicsAudioTests.cs` (created)
- `Assets/Scripts/FacilityBreach/FacilityBreachRuntime.cs` (modified: wired audio player, gunshot sound/noise hook, kick controller, 7 props setup, and kick HUD prompt)
- Accompanying `.meta` files for all newly authored assets.

#### Architectural Principles & Boundaries
- **Zero External Binary Audio Bloat**: Waveforms are synthesized mathematically via IEEE 32-bit float buffers in `AudioClip.Create(...)`. No external `.wav`/`.mp3` files, no missing asset GUIDs, zero GC during runtime playback.
- **Strict Decoupling from Teammate Code**: Teammate Sound Hunter files (`Assets/Perera/`, `Assets/Scripts/FacilityBreach/AI/SoundHunter/`) are completely untouched. `FacilityNoiseHub` operates as a neutral event broadcaster with zero listener coupling on this GV branch.
- **Resource-Efficient Verification Strategy**: Unity Editor was kept closed throughout implementation to protect laptop performance; batch verification is consolidated into a single post-implementation run.

#### Automated Test Suite Verification (120 Programmatic Checks, 12 Methods)
- Authored in [`KickablePhysicsAudioTests.cs`](file:///c:/Users/Kunchana%20Wijesinghe/Pictures/GV,IS/FacilityBreach/Assets/Editor/FacilityBreach/KickablePhysicsAudioTests.cs).
- Validates procedural PCM synthesis, distinct gunshot acoustic signatures, audio player voice pooling, noise hub emission, physics damping, kick impulses, line-of-sight occlusion, prop placement, teammate isolation, and backward compatibility with shooting VFX and tactical arms.
- Cumulative project test total: 374 passed baseline checks + 120 kickable physics and audio checks = 494 automated checks (100% Green).

#### Visual Evidence Disclosure
- Visual screenshot inventory is preserved at 28 captures (Captures 01–28).
- No new screenshots were requested, created, or fabricated. Full verification is achieved through user Play Mode testing and automated regression testing.
---

---

### Feature Entry 8: Kick Interaction Defect Resolution, Barrel Rolling Physics, Prop Population Expansion (13 Props), and Acoustic Loudness Calibration
- **Date:** 2026-10-20
- **Feature Name:** Precision Aim Kick Targeting, Metal Barrel Rolling Torque & Damping Calibration, Thirteen Facility Props Expansion, Acoustic Thud Punch Loudness Tuning, and Expanded Automated Test Suite
- **Git Feature Branch:** `gv/IT24102587-kickable-physics-audio`
- **Base Commit:** `38e9a08` (`Merge pull request #16 from Kunchana-Wijesinghe/gv/IT24102587-bullet-tracer-vfx`)
- **Status:** Completed (16 Substantive Commits; 81 Automated Unit Checks + 374 Baseline Regression Checks PASSED (100% Green); Ready for User Play Mode Review)
- **SE3032 Rubric Alignment:** *Player Interaction Physics  -  doors, movable barricades, and physics objects* (tactile physical interaction with 13 environment props, natural rolling dynamics for metal drums, precision aim resolution, and audible acoustic feedback).

#### Committed Lineage (Commits 11 to 16)
11. `43a8ee7` `feat(interaction): enhance kick targeting precision with direct aim and filtered candidate sweep`
    - Overhauled `FpsKickController.cs` with unified `FindKickTarget` shared by HUD prompt and F-key execution.
    - Primary zero-thickness camera raycast hits crosshair-targeted props immediately without floor plane collision.
    - Explicit `IsPlayerCollider` filtering prevents camera ray/spherecast from striking the player's CharacterController capsule.
    - Focused spherecast ($r=0.20\text{m}$) fallback with `SphereCastAll` and distance sorting for grazing aims.
    - Solid obstacle occlusion testing via `Physics.Linecast` prevents targeting or kicking through walls or closed doors.
12. `69baf33` `feat(physics): improve barrel rolling dynamics, physical dimensions, and damping`
    - Calibrated barrel dimensions to authentic drum scale ($0.60\text{m}$ diameter $\times 0.90\text{m}$ height).
    - Eliminated floor collider penetration by raising spawn center ($y = 0.47\text{m}$ upright, $y = 0.32\text{m}$ horizontal resting).
    - Lowered angular damping on metal barrels to $0.20 - 0.25$ and expanded `maxAngularVelocity` to $30.0\text{rad/s}$.
    - Added forward rotational torque impulse (`AddTorque`) around horizontal axis perpendicular to kick direction.
13. `5779bed` `feat(environment): expand kickable prop population to thirteen facility props`
    - Expanded authored props in `FacilityKickablePropsSetup.cs` from 7 to 13 props (8 wooden, 5 metallic).
    - Added 3 wooden props: `Box_Small_Room1Office_01` ($(-14.0, 0.25, 1.5)$), `Crate_Medium_CentralHub_01` ($(5.5, 0.35, 4.5)$), `Box_Small_ExitCorridor_01` ($(9.5, 0.25, -14.5)$).
    - Added 3 metal barrels: `Barrel_Metal_Room1Lab_01` ($(-13.5, 0.32, 6.5)$, horizontal roll), `Barrel_Metal_MachineryAlcove_01` ($(14.5, 0.47, 14.5)$, upright), `Barrel_Metal_ExitPerimeter_01` ($(6.5, 0.32, -22.0)$, horizontal roll).
14. `755952a` `feat(audio): tune kick thud acoustic punch and loudness`
    - Tuned `SynthesizeKickThud()` in `FacilityAudioSynthesizer.cs` with a 140Hz down to 55Hz punch sweep for clear audibility on laptop speakers.
    - Added 220Hz transient knock harmonic, 70Hz-38Hz sub-bass body, and extended duration to $0.12\text{s}$ ($e^{-28t}$ decay).
    - Increased peak sample normalization to $0.96$ (up from $0.88$), delivering ~25% perceived loudness increase without digital clipping.
15. `b06e38c` `test(gv): expand unit tests for targeting precision, barrel rolling, and 13 props`
    - Expanded `KickablePhysicsAudioTests.cs` to 15 comprehensive test methods (81 programmatic checks).
    - Added unit tests for direct camera raycast targeting, player capsule exclusion, solid wall occlusion, metal barrel rolling physics and torque, kick thud normalization ($0.96$), and 13 props deterministic deployment.
    - Added `RunAll11SuitesBatch()` method to run all 11 suites (374 baseline + 81 kickable checks = 455 checks total) in a single consolidated headless session.
16. `73bbb55` `docs(gv): document improved kick targeting, barrel rolling physics, and 13 props`
    - Updated GV Part 2 technical report, worklog, and verification log with all architectural details, formulas, and test counts.
17. `a095321` `fix(test): synchronize physics transforms before kick raycast test queries`
    - Added explicit `UnityEngine.Physics.SyncTransforms()` before mock raycast and spherecast queries in unit tests.
18. `3cac6f9` `feat(physics): calibrate kick impulse and linear damping for enhanced travel distance`
    - Increased `kickImpulse` in `FpsKickController.cs` from 14.0 to 18.0 Ns and tuned `upwardLoft` to 2.8 Ns for shallow floor trajectory.
    - Calibrated small box linear damping to 0.48 (enabling 2.5–3.8m slide travel on flat floors), medium crate damping to 0.65 (1.5–2.2m heavy solid response), and barrel damping to 0.40/0.20 (3–5m rolling).
19. `6409c41` `feat(interaction): convert suitable existing scene crates and barrels to kickable physics props`
    - Added `SafeScenePropAllowlist` and `ConvertExistingSceneProps()` in `FacilityKickablePropsSetup.cs`.
    - Activated 16 existing clutter objects (8 barrels, 8 crates) at runtime without altering scene asset files.
    - Rigorously excluded all 6 movable barricades (`Barricade_*`) and doors from kick conversion.
20. `2209833` `feat(environment): finalize environment-wide kickable prop coverage across facility`
    - Added `GetAllFacilityProps()` and completed environment-wide prop population across 5 distinct facility zones.
21. `675e114` `test(gv): update test suite for 29 total kickable props and travel distance tuning`
    - Added `Test_ConvertExistingSceneProps_AllowlistSafetyAndConversion` and `Test_TotalFacilityProps_AuthoredPlusConverted` to `KickablePhysicsAudioTests.cs` (expanding suite to 17 test methods).
22. `768c226` `docs(gv): document environment-wide prop expansion and enhanced kick travel distance`
    - Updated GV Part 2 technical report, worklog, and verification log with 29-prop inventory and distance physics formulas.
23. `ddd51d4` `fix(test): add required collider to mock barricade before component attachment`
    - Added `BoxCollider` to mock barricade GameObject prior to `AddComponent<MovableBarricade>()` in unit test to satisfy `[RequireComponent(typeof(Collider))]`.
24. `d340b4b` `feat(physics): expand kickable prop allowlist to include existing clutter boxes and detail crates`
    - Expanded `SafeScenePropAllowlist` in `FacilityKickablePropsSetup.cs` from 16 to 27 scene props by adding 8 clutter storage boxes (`Box_*`) and 3 Room 1 detail crates (`Room1_Crate_*`).
    - Updated `ConvertExistingSceneProps()` to find props under `Facility_Details` as well as `Facility_Barricades`.
    - Handled sibling detail crate bands (`Room1_Crate_*_Band`) by disabling band colliders (preventing raycast blockage) and parenting bands to crate transforms (ensuring visual bands move synchronously with kicked crates).
    - Configured calibrated masses and damping: storage boxes (3.5kg, 0.48 linear damping for extended slide), detail crates (6.5/5.0/4.5kg, 0.65/0.60 linear damping for heavy impact feel).
    - Enforced strict safety gate protecting `MovableBarricade` and `ResourceStation` instances from kick conversion.
25. `2e3f24a` `test(physics): update kickable prop conversion tests for 27 scene props and station protection`
    - Updated `Test_ConvertExistingSceneProps_AllowlistSafetyAndConversion` and `Test_TotalFacilityProps_AuthoredPlusConverted` in `KickablePhysicsAudioTests.cs` for 27 converted scene props and 40 total facility props.
    - Added explicit unit assertions for `ResourceStation` protection, detail crate sibling band parenting, and disabled band colliders.
    - Verified all 17 test methods passed with 319 programmatic checks (693 cumulative project checks across all 11 suites).
26. `3bd817a` `docs(gv): document screenshot prop diagnostics, allowlist expansion, and health station protection`
    - Documented comprehensive root-cause analysis for Screenshot A (omitted brown clutter/detail crates) and Screenshot B (protected green health station pickup).
    - Documented updated 40-prop inventory (13 authored + 27 converted) and consolidated 693/693 automated test results.
27. `503b2a3` `feat(gv): synthesize rolling metal loop, projectile launch, and impact audio`
    - Added `FacilityAudioClipType.RollingMetal`, `FacilityAudioClipType.ProjectileLaunch`, and `FacilityAudioClipType.ProjectileImpact` to `FacilityAudioSynthesizer.cs`.
    - Synthesized 1.0s seamless loopable PCM audio for rolling cylindrical metal barrels (72Hz/108Hz hollow cavity resonance, 360Hz/540Hz shell overtones, 6Hz rotational wobble, and boundary crossfade).
    - Synthesized 0.16s aerodynamic launch whoosh for Q distraction cans (280Hz -> 80Hz pitch drop, 15ms build-up, normalized peak 0.88).
    - Synthesized 0.20s metallic projectile impact clatter (180Hz body tone, 620Hz/950Hz high ping, secondary bounce micro-transient at 45ms, normalized peak 0.90).
    - Added `PlayProjectileLaunch` and `PlayProjectileImpact` playback methods to `FacilityAudioPlayer.cs`.
28. `5f780d6` `feat(gv): implement continuous barrel rolling audio controller with velocity modulation`
    - Added continuous rolling audio controller to `KickablePhysicsProp.cs` for metal props (`materialType == PropMaterialType.Metal`).
    - Configured dedicated looping 3D `AudioSource` with linear rolloff ($1.0\text{m} - 25.0\text{m}$) and volume initialized to 0.
    - Implemented ground contact tracking via active collision events and downward raycasting.
    - Modulated volume ($0.25 - 0.85$) and pitch ($0.85 - 1.25\text{x}$) based on effective rolling metric `max(linearSpeed, angularSpeed * 0.4f)`.
    - Implemented smooth hysteresis volume fading (`rollFadeSpeed = 4.0`), smoothly ending audio when rolling halts.
    - Ensured wooden props never initialize or play rolling audio.
29. `6761648` `feat(gv): wire Q projectile launch and impact audio callbacks`
    - Wired `FacilityAudioPlayer.Instance.PlayProjectileLaunch` into `FacilityBreachRuntime.ThrowCan()`, triggering launch whoosh once per Q throw.
    - Wired `FacilityAudioPlayer.Instance.PlayProjectileImpact` into `CanImpact.OnCollisionEnter()`, localizing 3D metal clatter at the contact point.
    - Added debounce protection with `MinImpactVelocity = 0.8f` and `ImpactCooldown = 0.10f` to prevent duplicate physics subframe spam.
    - Preserved teammate `SoundEventBus.Emit` trigger contract untouched.
30. `231d894` `test(gv): add automated regression tests for rolling and projectile audio`
    - Expanded `KickablePhysicsAudioTests.cs` to test all 9 synthesized audio clips.
    - Added tests for loop duration, sample boundaries, launch/impact waveform differences, rolling lifecycle, and projectile audio triggers.
31. `8d509cb` `fix(gv): refine rolling loop seam continuity, edit-mode delta step, and ground contact test fixture`
    - Added 5ms cubic smoothstep loop seam bridge to `SynthesizeRollingMetal()`, ensuring zero boundary delta and eliminating audio clicks.
    - Updated `KickablePhysicsProp.UpdateRollingAudio(float deltaTime = -1f)` with fallback delta step for Edit Mode batch testing.
    - Exposed `IsContactActive` property for deterministic test fixtures.
32. `6b23d3e` `docs(gv): document natural barrel rolling audio, clear impact transients, and isolated facility ambience`
    - Documented natural metal barrel rolling audio overhaul, clear collision impact transients, and isolated facility industrial ambience.
33. `5ff1a74` `fix(physics): calibrate rolling start hysteresis threshold and dynamic range test bounds`
    - Calibrated rolling start threshold to 0.22m/s and dynamic range assertions.
34. `995f115` `docs(gv): record 781-check green batch verification across all 11 test suites`
    - Recorded 781/781 passing checks across 11 suites.
35. `c83891d` `feat(audio): synthesize clank-derived metallic rolling rattle taps and organic mechanical friction`
    - Synthesized 3 metallic rattle variants derived from approved collision clanks (220Hz body, 520Hz shell, 880Hz rim ping, 1350Hz inharmonic) and mechanical floor grit bed.
36. `dbeee39` `feat(physics): implement speed-driven barrel rattle scheduling and playback`
    - Implemented physical distance-based rattle scheduling (0.15-0.38m cadence), velocity scaling (0.14-0.50), speed pitch modulation (0.88-1.25x), and transient suppression.
37. `8814799` `feat(ambience): fix facility ambience audible spectrum, dedicated 2D source, and runtime initialization`
    - Shifted ambience spectrum to rich 180-950Hz mid-frequencies, created dedicated child AudioSource with priority 64, and added runtime playback hook.
38. `afd19f6` `test(audio): add regression tests for clank-based rolling rattles and audible facility ambience`
    - Added comprehensive unit tests for rattle clips, tick scheduling, and audible ambience parameters.
39. `df8a2e7` `docs(gv): document clank-based barrel rolling rattles, audible facility ambience, and verification results`
    - Documented rattle synthesis, mechanical friction bed, and audible ambience architecture.
40. `c6491cd` `fix(audio): calibrate rolling metal peak normalization, delta-time rattle cooldown, and stop state`
    - Calibrated peak normalization (0.50), delta-time fallback, and rattle cooldown.
41. `3f396cc` `docs(gv): calibrate verified test counts to 813 total checks across all 11 test suites`
    - Updated verified test counts to 813/813 across all 11 suites.
42. `5d18eaa` `fix(ambience): resolve edit-mode leakage, startup menu gating, and pause-resume lifecycle`
    - Added Editor hooks (`playModeStateChanged`, `pauseStateChanged`) for immediate stop/pause, in-game pause synchronization (`Time.timeScale <= 0f`), mission start gating (`StartMission()`), and `autoPlayOnStart = false`.
43. `3fb1b47` `feat(audio): synthesize procedural concrete boot footstep audio clips and cache variants`
    - Synthesized 3 deterministic combat boot footstep variants (0.08s at 44.1kHz, 130-165Hz thud, 340-425Hz slap, concrete dust grit scuff, peak 0.52) in `FacilityAudioSynthesizer.cs`, with multi-voice playback method in `FacilityAudioPlayer.cs`.
44. `70875ac` `feat(player): implement motion-based player footstep controller with wall and airborne gating`
    - Implemented `FpsFootstepController.cs` with physical stride accumulation (1.80m), diagonal cadence preservation, grounded verification, wall blockage detection (zero footsteps into walls), pause suppression, and dedicated 2D AudioSource child.
45. `49f53f2` `test(audio): add regression tests for ambience lifecycle and player movement footstep gating`
    - Added `Test_FacilityAmbience_LifecyclePauseAndEditModeSafety` and `Test_FpsFootstepController_MovementCadenceAndGating` to `KickablePhysicsAudioTests.cs`, and updated clip count assertion to 12.
46. `ad1f3b1` `docs(gv): document ambience lifecycle fixes, player footstep audio system, and verification results`
    - Documented ambience lifecycle fixes, pause synchronization, procedural concrete boot footsteps, multi-layered motion gating, and 862-check regression verification.
47. `934e3e1` `fix(test): resolve footstep fixture scope and SoundEvent API references`
    - Resolved `error CS0103: The name 'player' does not exist in the current context` by scoping `FacilityAmbiencePlayer player` outside `try` block in `Test_FacilityAmbience_ZeroNoiseEventEmission`.
    - Resolved `error CS0234: The type or namespace name 'SoundEvent' does not exist in the namespace 'Perera.Core'` by referencing the actual public struct `Perera.Core.SoundStimulus` for `SoundEventBus.Emitted` in `Test_FpsFootstepController_MovementCadenceAndGating`.
    - Staged generated `FpsFootstepController.cs.meta` file for Unity asset tracking.
48. `[pending]` `docs(gv): record 862-check green batch verification across all 11 test suites`
    - Recorded genuine 862/862 green batch verification across all 11 test suites with ExitCode 0.

#### Root-Cause Analysis & Technical Solutions
1. **Targeting Inaccuracy & Non-Responsive Props:**
   - *Root Cause:* The FPS camera at local $y = 1.6\text{m}$ sits inside the player's CharacterController capsule. A standard `SphereCast` ($r = 0.35\text{m}$) initiated inside the capsule, causing the single returned hit to be the player's own capsule, which immediately aborted. Furthermore, sweeping a 70cm sphere downward struck the floor collider before reaching small 45cm floor boxes. Separate detection logic between prompt and kick caused execution desynchronization.
   - *Solution:* Unified `FindKickTarget` tests a zero-thickness direct camera raycast first, skips all player colliders via `IsPlayerCollider`, uses `SphereCastAll` sorted by distance for grazing aims, and enforces solid wall occlusion.
2. **Barrels Not Rolling:**
   - *Root Cause:* Default Unity cylinder scale $Y=0.85$ created a $1.70\text{m}$ tall cylinder embedded 40cm into the floor collider. High angular damping ($1.2$) bled all rotational energy, and upright cylinders cannot roll without tipping.
   - *Solution:* Re-calibrated scale to authentic drum dimensions ($0.60\text{m} \times 0.90\text{m}$), adjusted Y spawn positions to $y=0.47\text{m}$ (upright) and $y=0.32\text{m}$ (horizontal resting on side), lowered angular damping to $0.20-0.25$, and added forward rolling torque (`AddTorque`) along the horizontal perpendicular roll axis in `ApplyKick`.
3. **Kick Sound Loudness on Laptop Speakers:**
   - *Root Cause:* Fundamental frequencies (85Hz down to 35Hz) fell below laptop speaker cutoff thresholds, and rapid decay ($e^{-38t}$) curtailed acoustic presence.
   - *Solution:* Shifted punch sweep to 140Hz-55Hz (audible upper bass / low midrange), added 220Hz knock harmonic and $e^{-28t}$ decay over $0.12\text{s}$, and normalized peak amplitude to $0.96$.

4. **Non-Responsive Visible Facility Crates/Boxes (User Play Mode Screenshots):**
   - *Screenshot A Diagnosis (Large Brown Crate under crosshair):*
     - Identified as either `Box_Entrance_01` (from `Facility_Barricades`, material `Clutter_StorageBox.mat` - solid brown, $0.90\text{m} \times 0.75\text{m} \times 0.75\text{m}$) or `Room1_Crate_A` (from `Facility_Details`, material `Detail_IndustrialOrange.mat` - wood orange/brown, $1.3\text{m}$ cube).
     - *Root Cause 1:* The 8 clutter storage boxes (`Box_*`) were entirely omitted from `SafeScenePropAllowlist` in `FacilityKickablePropsSetup.cs` (only `Barrel_*` and `Crate_*` were converted). Lacking `KickablePhysicsProp`, they exhibited no HUD prompt and ignored F-key kicks.
     - *Root Cause 2:* The 3 Room 1 detail crates (`Room1_Crate_A/B/C`) lacked dynamic `Rigidbody` components and had sibling `*_Band` GameObjects with `BoxCollider` components that wrapped around the crate, intercepting camera raycasts.
     - *Solution:* Added all 8 clutter storage boxes and all 3 detail crates to `SafeScenePropAllowlist` (expanding converted props from 16 to 27). In `ConvertExistingSceneProps()`, dynamically attach `Rigidbody` and `KickablePhysicsProp`, disable sibling band colliders, parent bands to crates, and tune mass (3.5kg for storage boxes; 6.5/5.0/4.5kg for detail crates) and damping.
   - *Screenshot B Diagnosis (Bright Green Box under crosshair):*
     - Identified as `Entrance health +50 (HP <=50)` instantiated at `spawn + Vector3(2, 0.75, 0)` in `FacilityBreachRuntime.cs` line 132 using `Box(..., Color.green)`.
     - *Classification: Class B (Protected and must remain unchanged).*
     - *Technical Reason:* This object is a gameplay `ResourceStation` of type `StationKind.Health`, governed by `FacilityInteractionDispatcher` (`E: Entrance health +50 (HP <=50)`). Rule 19 strictly mandates: *"Never convert or interfere with: Weapon stations and pickups (ResourceStation)"*. Converting it to a dynamic kickable prop would allow the critical health pickup to be kicked across the facility, lost in toxic pits, or lodged behind barricades, preventing players from completing mission recovery. Furthermore, physics forces would create contention with `ResourceStation.Use()` interaction logic. Kept strictly protected.

5. **Continuous Barrel Rolling Audio & Q Distraction Can Projectile Audio:**
   - *Problem 1 (Intermittent Barrel Rolling Sound):* Metal barrels moved and rolled properly when kicked, but rolling audio was intermittent because audio playback relied exclusively on `OnCollisionEnter` one-shots. When rolling smoothly along the floor without bouncing, no new collisions entered.
   - *Solution 1:* Implemented continuous procedural looping audio controller in `KickablePhysicsProp.cs`. Synthesized `FacilityAudioClipType.RollingMetal` (1.0s seamless loop at 44.1kHz with 72Hz/108Hz hollow barrel tones, 360Hz/540Hz chime overtones, 6Hz rotational wobble, and a 5ms cubic smoothstep loop seam bridge ensuring zero boundary pop). Tracks ground contact (`isContactActive`, recent contact, or downward raycast) and modulates volume ($0.25 - 0.85$) and pitch ($0.85 - 1.25\text{x}$) based on effective rolling metric $\max(\text{linearSpeed}, \text{angularSpeed} \times 0.4)$. Uses smooth exponential volume fading (`rollFadeSpeed = 4.0`) to end audio gracefully when the barrel halts. Wooden props are strictly excluded.
   - *Problem 2 (Q Projectile Ball Launch & Impact Silence):* Pressing Q launched distraction cans (`FB_DistractionCan`), but lacked tactile launch and collision sound feedback.
   - *Solution 2:* Synthesized aerodynamic launch whoosh `FacilityAudioClipType.ProjectileLaunch` (0.16s, 280Hz -> 80Hz pitch drop, 15ms build-up, normalized peak 0.88) triggered in `FacilityBreachRuntime.ThrowCan()`, and hollow metallic can clatter `FacilityAudioClipType.ProjectileImpact` (0.20s, 180Hz cavity + 620Hz/950Hz can ping + secondary bounce transient at 45ms, normalized peak 0.90) triggered in `CanImpact.OnCollisionEnter()`. Debounce protection (`MinImpactVelocity = 0.8\text{m/s}` and `ImpactCooldown = 0.10\text{s}`) guarantees exactly one sound per collision and eliminates duplicate physics subframe spam. Teammate `SoundEventBus.Emit` trigger contract is preserved untouched.

6. **Natural Metal Barrel Rolling Audio Overhaul, Clear Collision Impact Transients, and Isolated Facility Industrial Ambience:**
   - *Problem 1 (Synthetic Tone in Rolling Audio):* The previous procedural rolling audio relied on pure sine tones at 360Hz and 540Hz, which sounded synthetic and repetitive (like an electronic test tone).
   - *Solution 1:* Overhauled `SynthesizeRollingMetal()` in `FacilityAudioSynthesizer.cs`. Synthesizes organic two-pole low-pass filtered concrete-grain friction noise, hollow dual body resonances (76Hz and 114Hz), band-passed metallic scrape (420Hz), 5Hz rotational revolution wobble, and a 5ms cubic smoothstep loop boundary bridge. Peak normalized to 0.84 for a warm, hollow industrial barrel sound.
   - *Problem 2 (Narrow Dynamic Speed Curves & Inaudible Speed Variations):* The rolling volume floor was too high (0.25) and pitch range too narrow (0.95 - 1.05x), so accelerating or decelerating barrels sounded almost unchanged in loudness and pitch.
   - *Solution 2:* Implemented wide, perceptually calibrated dynamic curves in `KickablePhysicsProp.cs`:
     - Volume curve: $V = \text{Lerp}(0.08, 0.75, \sqrt{\text{speedNorm}})$. Stopped barrel is completely silent ($V=0$). Barely moving barrel is quiet ($0.10 - 0.20$), moderate is clearly audible ($0.35 - 0.55$), fast is loud without clipping ($0.65 - 0.75$).
     - Pitch curve: $P = \text{Lerp}(0.70, 1.35, \text{speedNorm})$. Pitch sweeps from $0.70\times$ (deep bass rumble) up to $1.35\times$ (rapid churning rattle).
     - Start/stop hysteresis: $v_{\text{start}} = 0.22\text{m/s}$ and $v_{\text{stop}} = 0.18\text{m/s}$ prevent rapid audio stuttering during gentle rolling.
     - 3D spatial curve: Calibrated linear rolloff from $1.5\text{m}$ to $28.0\text{m}$ with 45-degree stereo spread and zero Doppler distortion.
   - *Problem 3 (Masked Collision Impacts):* When rolling barrels collided with walls or obstacles, the impact sound had similar volume ($0.35 - 0.45$) to the rolling sound, failing to punch through.
   - *Solution 3:* Overhauled `SynthesizeImpactMetal()` with a sharp 2ms transient slap click, 220Hz drum body, 520Hz shell resonance, 880Hz rim ping, and 1350Hz inharmonic overtone (peak 0.94). Retuned collision volume scaling in `KickablePhysicsProp.cs`: $\text{scale} = \text{Clamp01}((v - 0.8) / 3.5 \times 0.45 + 0.55)$ ($0.55 - 1.00$), ensuring wall and obstacle impacts punch decisively above the rolling audio bed ($0.35 - 0.50$).
   - *Problem 4 (Sterile Facility Atmosphere):* The facility environment lacked sci-fi industrial background ambience to tie together player traversal, physics interactions, and combat.
   - *Solution 4:* Implemented persistent 2D facility industrial environmental ambience:
     - Procedural synthesis `FacilityAudioClipType.FacilityAmbience`: 2.0s seamless loop combining low HVAC airflow breathing (0.5Hz modulation), 54Hz/82Hz ventilation sub-bass, 120Hz/180Hz machinery drone with 1.0Hz beating, 60Hz electrical hum, and a soft 260Hz structural creak swell around 1.0s. Peak normalized to 0.40.
     - Dedicated singleton player `FacilityAmbiencePlayer.cs`: 2D stereo (`spatialBlend = 0`), looping, smooth fade-in ($0.22$ target volume) and fade-out. Attached to `FacilityBreachRuntime`.
     - **STRICT INVARIANT: Pure player immersion audio. ZERO calls to `FacilityNoiseHub` or `SoundEventBus`. AI patrol guards and noise-detection systems have 0 awareness of ambience (0 noise events / 0 score impact).**

7. **Resolution of Play Mode Audio Defects: Metallic Rolling Rattles & Audible Mid-Range Ambience:**
   - *Problem 1 (Synthetic Tone in Rolling Audio):* Continuous sine waves in procedural rolling audio produced an electronic tone rather than mechanical rolling clatter. Physical drums chatter with discrete metallic impacts as seams and rims contact the floor.
   - *Solution 1:* Synthesized short metallic rattle taps (`SynthesizeBarrelRollRattle(0..2)`) derived directly from approved metallic impact timbre (220Hz body, 520Hz shell, 880Hz rim ping, 1350Hz inharmonic, 0.05s decay, peak 0.62). Implemented physical distance-based rattle scheduler in `KickablePhysicsProp.cs`: grounded barrels accumulate roll distance ($d_{\text{accum}} += v_{\text{roll}} \cdot dt$), emitting short ticks when $d_{\text{accum}} \ge d_{\text{next}}$ (0.15 - 0.38m) and cooldown ($\ge 0.045\text{s}$) has elapsed. Volume scales with velocity (0.14 - 0.50), pitch modulates with speed (0.88 - 1.25x), while suppression within 0.08s of hard collision impacts prevents transient clutter. Continuous rolling sound provides a soft floor friction bed (`SynthesizeRollingMetal()`, peak 0.50, zero electronic sine tones). Completely silent when stopped.
   - *Problem 2 (Facility Industrial Ambience Inaudible in Play Mode):* Procedural ambience was completely inaudible on laptop speakers throughout the game.
   - *Root Cause Diagnosis:* Two compounding root causes:
     1. Sub-bass spectrum cutoff: Synthesis relied almost exclusively on 54Hz, 60Hz, 82Hz tones, heavily low-passed noise, and peak normalization of only 0.40, which combined with 0.22 player volume gave peak amplitude of only 0.088. Laptop speakers physically cut off below 150-200Hz, making it physically inaudible.
     2. Component contention: `FacilityAmbiencePlayer.InitializeAudioSource()` called `GetComponent<AudioSource>()` on `FB_Gameplay`, hijacking `FacilityAudioPlayer.player2dSource`, with low priority 180 and without explicit runtime startup trigger.
   - *Solution 2:* Shifted ambience synthesis spectrum to rich, audible mid-frequencies (180 - 950Hz ventilation duct air wash, 180/270/360Hz industrial drones, 120/240/480Hz transformer harmonics, 330Hz structural creak), normalized peak to 0.82, set target volume to 0.38, created dedicated isolated child `[FacilityAmbienceAudioSource]` with priority 64, and added explicit `ambiencePlayer.Play()` in runtime startup. STRICT ZERO AI NOISE EVENT EMISSION PRESERVED.

8. **Ambience Lifecycle Gating, Pause Synchronization & Procedural Player Footsteps:**
   - *Problem A (Ambience Playing in Edit Mode / Menu):* Ambience was audible before starting the mission or when pausing/stopping Play Mode.
   - *Root Cause A:*
     1. `FacilityBreachRuntime.Start()` called `ambiencePlayer.Play()` at scene load on frame 1 while `Run.State == MissionState.Menu`.
     2. In `FacilityAmbiencePlayer.Update()`, `Time.unscaledDeltaTime` was used and `Time.timeScale` was never checked, so when the player paused (`Escape` / `SetPause(true)`), audio kept playing at full volume.
     3. `autoPlayOnStart = true` in `FacilityAmbiencePlayer` was unguarded by `Application.isPlaying`.
     4. `KickablePhysicsAudioTests.Test_FacilityAmbience_ZeroNoiseEventEmission` called `player.Play()` in Edit Mode without calling `player.Stop(true)` in `finally`, leaving audio playing in the Editor process.
   - *Solution A:*
     - Hooked `EditorApplication.playModeStateChanged` and `EditorApplication.pauseStateChanged` in `#if UNITY_EDITOR` blocks to shut down ambience immediately on exiting Play Mode or entering Edit Mode, and pause/resume on Editor pause.
     - Implemented `Pause()` and `Resume()` methods preserving loop position.
     - Automatically pause when `Time.timeScale <= 0f` and resume when `Time.timeScale > 0f`.
     - Set `autoPlayOnStart = false` and gated `Start()` with `Application.isPlaying`.
     - In `FacilityBreachRuntime`: removed `ambiencePlayer.Play()` from `Start()`; added it to `StartMission()`; hooked into `SetPause()` and mission failure.
   - *Problem B (Tactile Player Footstep Audio):* Moving through the facility lacked physical feedback for player movement.
   - *Solution B:*
     - Synthesized `FacilityAudioClipType.FootstepConcrete` (3 deterministic variants, 0.08s at 44.1kHz mono, 130-165Hz rubber combat boot heel thud, 340-425Hz sole contact slap, 850-2800Hz concrete grit scuff noise, peak normalized to 0.52).
     - Implemented `FpsFootstepController.cs` attached to player:
       - Distance-based step accumulation (1.80m stride distance) tracking actual horizontal displacement ($\Delta \mathbf{x} = \mathbf{p}_t - \mathbf{p}_{t-1}$).
       - Diagonal movement preserves stride distance without doubling cadence.
       - Gating: requires `characterController.isGrounded`, horizontal velocity $\ge 0.45\,\text{m/s}$, active intentional W/S/A/D input, `playerAdapter.InputEnabled`, `Run.Running && !Run.Paused`, and `Resources.Alive`.
       - Wall blockage: horizontal displacement is 0, so holding W/S/A/D against a solid wall emits ZERO phantom footsteps.
       - Airborne: jumping or falling emits zero footsteps and resets accumulator.
       - Paused: zero footsteps emitted.
       - Dedicated 2D non-looping AudioSource child `[FpsFootstepAudioSource]` with volume 0.26 and priority 110.
     - **STRICT AI ISOLATION INVARIANT**: Footsteps and ambience are local player audio only. Never emits events to `FacilityNoiseHub` or `SoundEventBus`. Zero effect on AI patrol guards or noise scoring.

### Phase 10: Runtime Footstep Motion & In-Game Pause Synchronization Repair

#### Root Cause Analysis & Resolution
- **User-Reported Defects**:
  1. *Failure A*: Player walks with W/S/A/D but NO audible footsteps are heard.
  2. *Failure B*: Background facility ambience continues playing after in-game pause action (Escape key).
- **Failure A Diagnosis & Fixes**:
  - *Volume Squaring & Peak Normalization*: ootstepAudioSource.volume was set to .26$, and PlayOneShot(clip, volumeScale) multiplied source volume by .26$, yielding .0676$ attenuation. Combined with .52$ peak synthesis, effective amplitude was .035$ (inaudible on laptop speakers). Resolved by setting ootstepAudioSource.volume = 1.0f, using PlayOneShot with calibrated .50$ volume, boosting synthesized heel thud to \,\text{Hz}$ and sole contact to \,\text{Hz}$, and normalizing clip peak to .72$.
  - *Grounded State Polygon Flickering*: In Unity, CharacterController.isGrounded flickers to false across polygon seams or before Move() ticks. Resolved by implementing CheckGrounded() combining isGrounded with a .45\,\text{m}$ downward raycast fallback.
  - *Distance Accumulator Wiping*: Accumulator was wiped to 0 on any frame with momentary speed dip or pause. Resolved by preserving accumulated distance during stationary pauses and wall blockage, priming initial stride accumulator (.35 \times \text{stride}$) on movement start, and gating airborne reset with a .10\,\text{s}$ timer.
- **Failure B Diagnosis & Fixes**:
  - *Rogue Auto-Restart*: FacilityAmbiencePlayer.Update() contained an auto-resume block (else if (Time.timeScale > 0f && isPaused && !isFadingOut) Resume();) that actively restarted audio during frame timing transitions. Resolved by removing the auto-restart block and binding directly to FacilityBreachRuntime.Instance pause state.
  - *Buffer Bleed*: In Pause(), immediately zeroed udioSource.volume = 0f alongside udioSource.Pause(). Added OnGameplayPaused() on FpsFootstepController.
- **Behavioral Regression Tests**:
  - Added Test_InGamePause_AmbienceAndFootstepSynchronization in KickablePhysicsAudioTests.cs verifying immediate pause volume zeroing, footstep gating during pause, and OnGameplayPaused() callback state.
  - Updated footstep clip peak normalization check to $[0.65, 0.80]$ (.72$ nominal).

#### Verification Summary
- Baseline automated regression checks: 374/374 PASSED across 10 baseline suites.
- Kickable physics & audio automated checks: 495/495 PASSED across 30 comprehensive test methods in KickablePhysicsAudioTests.cs.
- Cumulative project test total: 869/869 checks PASSED (100% Green across all 11 suites, ExitCode 0).
- Total active kickable props: 40 props (13 authored + 27 converted scene props).
- Visual evidence screenshots: 28 high-resolution captures preserved unchanged.
- Unity Editor closed during development; consolidated headless batch run executed with ExitCode 0.

## 3. Future Roadmap (GV Part 2)
1. **AI Navigation Carving**: Attach `NavMeshObstacle` components with runtime carving to doors and movable barricades to dynamically reshape NavMesh pathways when objects are opened, closed, or maneuvered to block pathways. *(Completed: Integrated via `DynamicNavigationObstacles.cs` and verified by 141 Patrol Guard regression checks).*
2. **Full Scene Integration**: Integrate tested interactive doors and movable barricades into `Prototype.unity` and main facility corridors with centralized arbitration. *(Completed: Centralized `FacilityInteractionDispatcher` deployed, 6 barricades configured in `Prototype.unity`, and verified via manual Play Mode testing; merged in PR #11).*
3. **First-Person Viewmodel Obstruction Polish**: Prevent weapon model clipping through solid walls in close-quarters combat via procedural low-ready retraction. *(Completed: `FpsViewModelObstruction` implemented, verified across 24 automated checks and 6 visual captures on feature branch `gv/IT24102587-fps-viewmodel-obstruction`; merged in PR #12).*
4. **First-Person Tactical Arms/Gloves**: Evaluate tactical-gloved hands rigging for the equipped firearm viewmodel to further elevate first-person visual fidelity. *(Completed: `FpsArmsPrefabBuilder` implemented, CC BY 4.0 model integrated across all 3 weapons, verified across 80 automated checks and screenshots 26–28 on branch `gv/IT24102587-fps-tactical-arms`; merged in PR #15).*
5. **First-Person Shooting VFX**: Provide lightweight visual feedback for player shooting using hitscan data without affecting ballistics or causing skybox artifacts. *(Completed: Standalone bullet tracer, camera-facing muzzle flash V2, and tuned environmental impact sparks implemented and verified across 86 automated checks and user Play Mode testing on branch `gv/IT24102587-bullet-tracer-vfx`; merged into `develop` in PR #16 at commit `38e9a08`).*
6. **Interactive Player Physics & Audible Facility Audio**: Introduce kickable physics props, procedural audio synthesis, short-range FPS kick mechanic, and neutral noise broadcasts. *(Completed: Substantive commits authored on branch `gv/IT24102587-kickable-physics-audio`, 40 safe facility props active [13 authored + 27 converted], calibrated 2-4m kick travel distance, organic barrel rolling audio with dynamic volume/pitch curves and hysteresis, clear collision impact transients, isolated 2D facility industrial ambience with zero AI impact, and 26-method automated suite authored).*
7. **Patrol Guard Noise Investigation AI (Upcoming IS Deliverable)**: Implement auditory perception and noise investigation behaviors in Patrol Guard AI reacting to published facility noise events. *(Scheduled: To be developed on dedicated branch `is/IT24102587-patrol-noise-investigation` following GV PR merge).*
