# Individual Worklog: Graphics and Visualization (GV) - Part 2

**Student Name:** Wijesinghe K.  
**Student ID:** IT24102587  
**Module:** SE3032 - Graphics and Visualization (GV)  
**Assigned Role:** Part 2: Systems Engineer (Player Interaction Physics)  
**Git Branch (Merged Baseline):** `develop` (integrated via GV PR #9 commit `fd94efe` and PR #11 commit `4ac6a2f`)<br/>
**Current Working Feature Branch:** `gv/IT24102587-fps-viewmodel-obstruction`<br/>
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
  - Pool capacity: 12 pre-allocated `LineRenderer` instances.
  - Tapered streak geometry ($0.025\text{m}$ start width, $0.008\text{m}$ end width).
  - Trajectory spans from weapon muzzle to hit point (or $40.0\text{m}$ along camera aim on misses).
  - Fades smoothly over $0.08\text{s}$ via alpha key decay (`#FFF2A8` $\rightarrow$ `#FF9900`).
- **Camera-Facing Muzzle Flash V2 (`FpsMuzzleFlashV2.cs`)**:
  - Solves earlier sky discoloration artifact by using a single camera-aligned quad anchored directly at the weapon muzzle in camera local space.
  - Scale variation ($0.12\text{m} - 0.16\text{m}$) and random roll rotation ($\pm 35^\circ$) per shot.
  - Snappy $0.030\text{s}$ duration ensures crisp visual combustion without lingering across subsequent frames.
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

## 3. Future Roadmap (GV Part 2)
1. **AI Navigation Carving**: Attach `NavMeshObstacle` components with runtime carving to doors and movable barricades to dynamically reshape NavMesh pathways when objects are opened, closed, or maneuvered to block pathways. *(Completed: Integrated via `DynamicNavigationObstacles.cs` and verified by 141 Patrol Guard regression checks).*
2. **Full Scene Integration**: Integrate tested interactive doors and movable barricades into `Prototype.unity` and main facility corridors with centralized arbitration. *(Completed: Centralized `FacilityInteractionDispatcher` deployed, 6 barricades configured in `Prototype.unity`, and verified via manual Play Mode testing; merged in PR #11).*
3. **First-Person Viewmodel Obstruction Polish**: Prevent weapon model clipping through solid walls in close-quarters combat via procedural low-ready retraction. *(Completed: `FpsViewModelObstruction` implemented, verified across 24 automated checks and 6 visual captures on feature branch `gv/IT24102587-fps-viewmodel-obstruction`; merged in PR #12).*
4. **First-Person Tactical Arms/Gloves**: Evaluate tactical-gloved hands rigging for the equipped firearm viewmodel to further elevate first-person visual fidelity. *(Completed: `FpsArmsPrefabBuilder` implemented, CC BY 4.0 model integrated across all 3 weapons, verified across 80 automated checks and screenshots 26–28 on branch `gv/IT24102587-fps-tactical-arms`; merged in PR #15).*
5. **First-Person Shooting VFX**: Provide lightweight visual feedback for player shooting using hitscan data without affecting ballistics or causing skybox artifacts. *(Completed: Standalone bullet tracer, camera-facing muzzle flash V2, and tuned environmental impact sparks implemented and verified across 86 automated checks and user Play Mode testing on branch `gv/IT24102587-bullet-tracer-vfx`).*
