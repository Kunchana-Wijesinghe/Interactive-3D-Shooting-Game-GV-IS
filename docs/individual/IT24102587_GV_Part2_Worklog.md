# Individual Worklog: Graphics and Visualization (GV) - Part 2

**Student Name:** Wijesinghe K.  
**Student ID:** IT24102587  
**Module:** SE3032 - Graphics and Visualization (GV)  
**Assigned Role:** Part 2: Systems Engineer (Player Interaction Physics)  
**Git Branch:** `gv/Kunchana-interactions`  
**Target Unity Version:** Unity 6000.6.2f1 (URP / 3D)  
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
- [01_Door_OutOfRange.png](evidence/01_Door_OutOfRange.png)
- [02_Door_Open.png](evidence/02_Door_Open.png)
- [03_Door_ClosedCollision.png](evidence/03_Door_ClosedCollision.png)

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
- [04_Barricade_SceneOverview.png](evidence/04_Barricade_SceneOverview.png)
- [05_Barricade_Grabbed.png](evidence/05_Barricade_Grabbed.png)
- [06_Barricade_WallCollision.png](evidence/06_Barricade_WallCollision.png)
- [07_Barricade_DoorwayBlocked.png](evidence/07_Barricade_DoorwayBlocked.png)

#### AI Assistance & Manual Review
- AI assisted in drafting `MovableBarricade.cs` physics logic, updating `SimplePlayerController.cs`, and constructing the scene builder.
- Manually reviewed component parameters, damping values, and constraint locks.
- Manually reviewed and tested in Unity Play mode on 2026-10-04, confirming full pass of all listed interactions.

---

## 3. Future Roadmap (GV Part 2)
1. **AI Navigation Carving**: Attach `NavMeshObstacle` components with runtime carving to doors and movable barricades to dynamically reshape NavMesh pathways when objects are opened, closed, or maneuvered to block pathways.
2. **Audio & Visual Polish**: Add particle dust effects and scraping audio clips when sliding heavy metal/wood barricades across concrete floors.
3. **Full Scene Integration**: Coordinate with Pesanjith to integrate tested interactive doors and movable barricades into `Prototype.unity` and main facility corridors.
