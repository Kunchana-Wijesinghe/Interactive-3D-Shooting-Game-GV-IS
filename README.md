# Interactive 3D Shooting Game

## Project Overview
- **Assignment Theme**: Interactive 3D Shooting Game
- **Project Status**: Working project: Interactive 3D Shooting Game. The final game name, story, and setting have not yet been decided by the team.
- **Joint Modules**:
  - **SE3032**: Graphics and Visualization (GV)
  - **SE3062**: Intelligent Systems (IS)

---

## Technical Specifications & Requirements
- **Unity Editor Version**: `6000.6.2f1` (Unity 6)
- **Target Platform**: PC Standalone (Windows 64-bit)
- **Render Pipeline**: Standard Core 3D

---

## Getting Started: Clone and Open Project

### Prerequisites
1. Install [Unity Hub](https://unity.com/download).
2. Install Unity Editor version **`6000.6.2f1`** via Unity Hub.
3. Install Git.

### Setup Instructions
1. Clone the repository:
   ```bash
   git clone https://github.com/Kunchana-Wijesinghe/Interactive-3D-Shooting-Game-GV-IS.git
   ```
2. Open **Unity Hub**.
3. Click **Add** -> **Add project from disk**.
4. Select the cloned repository root folder.
5. Ensure the Editor version is set to **`6000.6.2f1`**.
6. Open the project and allow Unity to import initial package dependencies.

---

## Team Roles & Responsibilities

### Graphics and Visualization (SE3032) Roles
| Member | Part | Role | Responsibilities |
|---|---|---|---|
| **Pesanjith** | Part 1 | World Builder | Level architecture, lighting, environmental meshes, scene coordination |
| **Wijesinghe K** | Part 2 | Systems Engineer | Rendering pipeline, camera systems, VFX, UI/HUD integration |
| **Perera** | Part 3 | Core Developer | Core gameplay loop, weapon mechanics, ballistics, damage system |
| **Bandara** | Part 4 | Agent Controller | Animation state machines, character physics, shared movement controller |

---

### Intelligent Systems (SE3062) Roles *(Proposed - Pending Final Team Confirmation)*
> [!NOTE]
> The following IS agent allocations are currently **PROPOSED** and subject to final confirmation by the team.

| Member | Proposed Agent Role | Description |
|---|---|---|
| **Pesanjith** | Cover Defender | Evaluates tactical cover positions, dynamic occlusion, and defensive positioning |
| **Wijesinghe** | Patrol Guard | Pathfinding along sensory waypoints, area sweeping, vigilance/alert state transitions |
| **Perera** | Sound Hunter | Auditory stimulus perception, audio cue triangulation, dynamic investigation |
| **Bandara** | Flanker | Flanking trajectory calculation, spatial pincer movement, cooperative interception |

---

## Repository Structure & Version Control Guidelines
- Tracked directories: `Assets/`, `Packages/`, `ProjectSettings/` and their respective `.meta` files.
- Ignored directories: `Library/`, `Temp/`, `Obj/`, `Logs/`, `Build/`, `Builds/`, `UserSettings/`, and IDE-specific caches.
- Please review [CLAUDE.md](CLAUDE.md) for scene coordination, agent architecture boundaries, and workflow guidelines before contributing.
