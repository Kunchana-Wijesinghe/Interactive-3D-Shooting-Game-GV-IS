# Team Development Guidelines & Ownership Rules

> **Working Project**: Interactive 3D Shooting Game  
> **Status**: The final game name, story, and setting have not yet been decided by the team.

---

## 1. Team Ownership & Scene Management
- **Main Scene Coordination**: **Pesanjith** coordinates and approves all modifications to the main level scene (`.unity` files) to prevent merge conflicts in scene YAML files.
- **Prefabs & Sub-scenes**: Team members should construct their components as standalone Prefabs or dedicated sub-scenes in their respective feature folders instead of making direct uncoordinated edits to the main scene.
- **Merge Conflict Prevention**: Never resolve binary or complex scene merge conflicts blindly. Coordinate with Pesanjith before merging any branch touching the primary scene hierarchy.

---

## 2. Agent Architecture Boundaries
- **Shared Movement Controller**: **Bandara** owns and maintains the core shared movement controller, locomotion physics, and navigation interfaces.
- **Decoupled Decision Logic**:
  - Keep each IS agent's decision-making and search logic (Cover Defender, Patrol Guard, Sound Hunter, Flanker) completely decoupled from Bandara's shared movement controller.
  - Agents must communicate with the movement controller via clean interfaces or abstract steering commands rather than embedding movement execution inside decision trees/state machines.
  - Each agent's custom perception and sensory processing must remain self-contained within its assigned module.

---

## 3. Workflow & Verification Rules
- **Incremental Commits**: Make small, modular changes with clear, descriptive commit messages. Do not bundle multiple unrelated features into a single commit.
- **Play Mode Verification**:
  - After implementing any feature, execute a verification run in Unity Editor Play mode.
  - Document the exact Play mode check steps, expected behavior, and console status (zero unexpected errors/warnings) in the PR or task summary.
- **Meta Files Integrity**: Always commit `.meta` files alongside newly added assets, scripts, or folders. Never rename or move files outside of the Unity Editor to prevent GUID breakage.
