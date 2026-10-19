# GV Agent Controller validation — 19 October 2026

Branch: `gv/ashen-agent-controller`. Unity: `6000.6.2f1`.

## Performed

- Read repository README/CLAUDE guidelines and relevant implementation/model documentation. No AGENTS.md was found in the workspace or checked parent locations.
- Read the lecturer's GV assignment PDF and both team-plan DOCX files in the parent workspace's ReferenceDocs. These local references were not added to the repository.
- Inspected the existing mover, four brains, shared interfaces, runtime factory, combat, presentation, validation scripts, FBX metadata, binary animation stack names and visual prefab rig hierarchy before integration.
- Ran Unity compilation, imported-clip audits, Edit Mode checks and controlled Play Mode lifecycle checks in an isolated copy at `Temp/GVAgentValidation`. The live editor/project session was not used for the test run. Assets, Packages and ProjectSettings were copied without changing package versions. After testing, compared script contents: **no source snapshot mismatches** between the final implementation and validation copy.

Batch invocation (from project root; substitute the full validation-copy path):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath '<project>\Temp\GVAgentValidation' `
  -executeMethod FacilityBreach.Editor.AgentControllerTests.RunBatch `
  -logFile '<project>\Temp\gv-agent-validation-final.log'
```

The sandbox's first launch stalled at licensing; the successful run used the normal Windows account. `RunBatch` exits after its Play Mode checks return to Edit Mode, so `-quit` is intentionally omitted.

## Actual final results

```text
[GV Agent Controller] PASS 15 route and turning checks (Edit Mode).
[GV Agent Controller] PASS 13 navigation lifecycle checks (Play Mode).
```

The final Unity log contains no C# compilation errors or test exceptions. It contains existing compiler warnings (20 CS warning output lines, including repeated compiler output, plus the existing GameplayDiagnostics UAC0009 warning); none point at the newly added scripts. This is not a claim of a warning-free project.

Twenty `[GV clip audit]` entries confirmed Idle, Move, Search, Attack and Defeat for PG-01, SH-01, CD-03 and FL-04. Every clip is 1.00 seconds long and resolves **80/80** transform bindings beneath its prefab's inner ModelVisual. The resulting Resources library was copied back with its Unity-generated metadata and original FBX references.

The 15 Edit Mode checks cover copied input ownership, point order, final arrival, single completion notification, replacement/revision interruption, retained failure reason, null/empty routes, NaN/infinity rejection, reset, turning at 30 versus 144 frames/second, vertical aim and zero elapsed time. Turning output is normalized to prevent accumulated quaternion drift.

The 13 Play Mode checks build real temporary NavMesh data and exercise the actual mover: legal navigation, exclusive rotation ownership, complete multi-point request/path acceptance, pause preservation and resume, stop interruption/path clearing, inactive/death-sink rejection, revival reset, empty-route stopping, invalid tolerance and off-mesh failure. Navigation stop ordering clears the path before setting `isStopped`.

`git diff --check` and the final pre-commit `git diff --cached --check` passed. Trailing whitespace in two newly generated Resources metadata files was removed before the staged check passed. The pre-existing ProjectAuditorSettings and PackageManagerSettings changes were left in place and excluded from staging. Exactly the 23 user-authorized contribution files were staged. No scenes, model/material assets, package versions or input configuration were changed by this contribution. No push or merge was performed.

## Existing check that failed independently

```text
dotnet run --project docs/facility-breach/standalone/Checks.csproj
```

This existing standalone project fails with CS0246 because its `Core/*.cs` wildcard includes `DynamicNavigationObstacles.cs` and `GameplayDiagnostics.cs`, which require UnityEngine assemblies that the standalone project does not reference. That runner was not modified as part of the GV contribution. The Unity checks above do not rely on it.

## User-performed Prototype checks — 19 October 2026

Tester: **Ashen Bandara (user-reported)**, in Prototype. These results were reported by the user after manual testing; they were not observed by Codex and are separate from the automated checks above. Duplicate descriptions in the report are combined below.

| Case actually tested | User-reported result |
| --- | --- |
| Observe all four enemies moving | PASS — all four move and animate correctly, without floating, sideways sliding or rapid spinning. |
| Move around a corner while being chased | PASS — enemies turn smoothly and follow valid routes without passing through walls. |
| Move a barricade into an enemy's route | PASS — the enemy finds another route or stops safely, without endless jittering. |
| Open/close a door near an enemy | PASS — no wall clipping or permanent sticking was observed. |
| Let an enemy spot the player | PASS — it faces and attacks the player without rapid spinning or sideways sliding. |
| Defeat an enemy while it is moving | PASS — movement and attacks stop, and the defeat animation plays correctly. |
| Wait for the existing respawn | PASS — the enemy returns upright and can move and animate again. |
| Pause and resume while enemies move | PASS — movement and animation freeze and resume together. |
| Restart after losing / restart the mission | PASS — enemies return alive, reset and behave normally. |

Console result (user-reported): **Ashen checked the Unity Console after gameplay testing and observed no new red errors.** Warning status was not reported; this does not establish that the project has no existing errors or warnings. No screenshots, video, frame-rate measurements or build results were supplied with this report.

## Remaining verification scope

The controlled automated Play Mode checks exercise a temporary agent and navigation patch, not a full Prototype encounter. The user-performed checks above provide the reported Prototype gameplay/visual results. A dedicated whole-array `RequestRoute` traversal across frames, numerical FPS/performance measurements, a player build, and a complete **card → device → escape** mission playthrough have not been reported as tested. The reported enemy-spotting test does not separately establish stationary aiming behavior. No additional cases are marked as passed by inference.

Detailed generated logs and the copied validation project remain under ignored `Temp/`; this record captures the reproducible command, bounded scope and actual results without committing generated caches.
