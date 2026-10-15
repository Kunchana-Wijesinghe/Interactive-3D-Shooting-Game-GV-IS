# Phase 0 report — local foundation

Branch: `it24101848-gv-combat`

Base: fetched `origin/develop` at `add95ee`; world-builder `b7c1425` already merged.

Changes: mission state/timer simulation; card/device/escape; temporary labelled
player adapter; shared damage/sound contracts; finite weapon magazines and shared
reserve; healing and ammo crates; shared enemy combat/revival contracts;
HUD/minimap/menu foundation; safe door closing and pause gating; restart scene.

Prototype additions: one serialized FB_Gameplay root with runtime gameplay
children; all three Machinery openings gated; explicit toxic-waste placeholder
because the committed scene lacks the generator output. Existing camera/listener
reused. Build settings now include Prototype.

Protected-folder check: PASS, all 86 files match pre-edit hashes, including seven
pre-existing local material edits. No protected paths appear in feature commits.

Environment preservation: PASS, 1,354 authored transforms and all original scene
component records unchanged; original root list preserved with one appended root.
No generators run; no props/walls moved. Existing door component identities/GUIDs
preserved; code adds safety/API behavior documented before editing.

Compile: Unity 6000.6.2f1 isolated project, zero C# errors. Existing generator
obsolete-API warnings were left untouched. Windows64 development build succeeds
with zero build errors/warnings; executable not manually played.

Standalone C#: PASS, 50,102 assertions. Reproducible authored .NET harness included.

EditMode: PASS, 27 Unity Editor assertions; custom Editor harness, not NUnit.

PlayMode: PASS, 51 cumulative assertions in Prototype; programmatic interactions
and mission contracts, actual hitscan and wall/muzzle obstruction tests. The victory
test drives state/contracts; it is not a complete manually walked playthrough.

Manual checks: not performed. No keyboard/mouse, audio, animation or lighting pass
is claimed. Full physical-route, restart and standalone interactive checks remain.

Limitations: teammates' controller/three AIs absent; no full four-agent integration.
Sound Hunter adaptation, navigation/robot permissions, active enemy map dots,
final effects/audio and Unity model prefabs await subsequent phases. Temporary
movement and waste visuals are explicitly labelled.

Commit: `42f2cc7` core; `bc36209` Prototype integration; `86870c6` validation/audit;
`ffda6eb` authored standalone project. This report is a subsequent local commit.

Push: **not performed**. Automatic approval review rejected the push twice due
to private-code egress authorization; the user then explicitly selected
"Keep the completed commits local." No alternate push path was attempted.

PR: not created; prepared description in `foundation-pr.md`.

Actual merge status: **not merged**. Remote develop is unchanged by this work.

Next phase: focused `it24101848-is-sound-hunter`, only after actual foundation
merge and fetch of current develop. No later branches were created from stale
develop. Read-only IS reuse/adapter preparation is in `is-integration-audit.md`.

Scene approval: CLAUDE.md says "Pesanjith coordinates and approves all
modifications to the main level scene". That review remains outstanding, and
no unreviewed merge was performed.

Independent Blender candidates are prepared outside the repository in
`C:\Users\Praveen\AppData\Local\Temp\FB-Art-Preparation-20261010`.
These are not part of the foundation commits and are not completed model phases.
Their local README describes automated generation and remaining Unity integration.
