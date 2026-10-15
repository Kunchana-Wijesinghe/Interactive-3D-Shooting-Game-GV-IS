# Facility Breach — Phase 0 foundation

Owner: Perera H.L.P.S. / IT24101848. Unity 6000.6.2f1, built-in pipeline,
legacy Input Manager retained. Main scene: `Assets/Scenes/Prototype.unity`.

## Audit

Fetched origin on 2026-10-10. Base `origin/develop`: `add95ee`.
World-builder branch still points to `b7c1425`, which is already an ancestor of
develop. No world-builder commits need merging into this feature.
README and CLAUDE were read. No applicable AGENTS.md was found in the project.

Existing scene supplies walls, props, signage, lights, maps, doors and six
barricades. The scene does **not** contain `Toxic_Waste_Obstacle`, `PLAYER_ENTRY`,
`FACILITY_EXIT`, a playable controller, team AI or a baked facility NavMesh.
Navigation package and world-builder setup scripts exist; they were not run.
The toxic-waste generator exists but its output is absent from the committed scene.
Scene geometry confirms Storage = Room1, Control = Room2, Machinery = Room3.
Machinery openings are left `(10,0,18.75)`, bottom `(14,0,12)`, top `(14,0,24)`.
Ordinary routes remain intact on either side of Control and Machinery.
Entrance and exit are anchored to existing main door roots, offset 1.75 units
inward. Pickup placements query actual colliders within a bounded 2-unit radius.

The only previous combat and hearing implementation lives in protected Perera
test scenes. Reuse: `Perera.Core.IDamageable`, `DamageInfo`, `SoundEventBus`.
Its Health has no healing API; AmmoMagazine has independent unlimited reserve.
New mission resources therefore implement required healing and capped shared
reserve, without activating a duplicate protected test-scene system.
The existing Sound Hunter decision implementation is inspected/read-only and
remains a candidate for adaptation during the focused IS phase.

Initial local changes: seven protected material files and untracked
`ProjectSettings/PackageManagerSettings.asset`. Neither is staged or changed by
this work. Baseline hashes include the user's local material edits. Existing
ScriptAssemblies were present; current baseline source compilation was not
independently established before editing. Final isolated compilation passes.

## Additions and boundaries

Only serialized scene addition: `FB_Gameplay` and its runtime component.
All original scene records remain byte-equivalent after newline normalization;
SceneRoots gains one appended root. The runtime constructs gameplay children
under that root. No destructive generator is invoked.

The sole camera/AudioListener is reused and parented to the temporary player
at runtime. Its authored scene transform is unchanged. There was no Bandara
controller to reuse: `TemporaryPlayerAdapter` is explicitly temporary and does
not represent completion of Bandara's movement contribution.

Runtime children include card, two weapon swap stations, health station, two
marked ammo crates, fictional shutdown device and three visible Machinery
card barriers. The missing toxic-waste output is represented by an explicitly
named blocking placeholder at the generator's authored location. It deals no
damage. This requires world-builder visual review/replacement later.

`InteractiveDoor` retains its serialized settings and existing E listener.
Its new RequestOpen API exposes state for later adapters; closing rejects
nearby character/dynamic-body occupancy. Input toggles are ignored while
paused. Existing door objects, transforms and GUIDs are unchanged.
The Machinery barriers enforce player card access even if a door opens.
Robot traversal permissions/acoustic costs are pending the IS integration.

`EditorBuildSettings` now lists Prototype so scene reload works in a standalone
build. No pipeline, Unity version, input or unrelated settings were changed.

## Architecture and tuning

- MissionRun: pure state simulation. Timeout wins ties. Shutdown consumes five
  unpaused seconds; completing inside a step freezes remaining countdown at
  the event time. Escape has no additional limit. Death remains fatal.
- PlayerResources: HP100; <=50 heals by50; shared reserve50/max200; capacities
  50/75/100 and damage10/15/20. Each weapon retains its magazine across swaps.
  Shared reserve compatibility is a prototype simplification.
- ResourceStation: one-use card; five-second health restoration; persistent
  weapon swap stations; one associated ammo pickup per broken crate, partial
  transfers, 15-second restoration clears its prior pickup.
- Runtime: interaction ray2.5; hitscan range50; cadence0.2; reload1.5; self
  geometry ignored; camera aim then muzzle obstruction ray; every actual shot
  consumes one round and emits a shared gunshot event. Can impact emits once.
- EnemyRoster: logical revival expiry before lethal damage; safe physical
  revival can be postponed; only four actual registered identities can finish
  the permanent-defeat challenge. Missing team agents are not substituted.
- EnemyCombatAdapter: shared damage10, magazine50, reserve1000, miss40%, warning
  0.4 seconds, reload1.5, LOS/range recheck. Decision components supply perceived
  targets. IEnemyActivitySink clears/stops decisions on lifecycle transitions.
  No enemy adapters are instantiated yet; teammates' missing agents are reported.
- GUI: mission/menu/pause/results, health/ammo/card/objective/reload/prompts,
  shutdown progress and north-up map from scene collider geometry. Team-marker
  integration remains pending actual agents. Map data is never an AI input.
- Restart reloads Prototype, rebuilding mission/resources/objects and timers.
  Gameplay timers use scaled time. Focus loss pauses; resume requires releasing
  held action/movement inputs. Start/Resume clicks cannot become shots.

Inspector setup: FB_Gameplay references its runtime script; anchors resolve
existing named scene roots with fail-fast errors. Shot, reload and interaction
parameters are serialized on this component. No protected references are edited.

## Validation actually performed

Testing project: `C:\Users\Praveen\AppData\Local\Temp\FB-Isolated-Foundation-20261010`.
The user's open project was never opened by another Unity test process.

- Standalone C#: 50,102 assertions, boundary sweep and seeded ammo conservation.
- Unity Editor checks: 27 assertions, executed by FoundationValidation, not NUnit.
- Unity Play Mode: 51 cumulative assertions in Prototype. Programmatic card,
  device, health and crate interactions; victory/timeout contracts; all seven
  pickups free of scene overlaps; all three Machinery gates; actual shots
  blocked by existing wall and muzzle obstruction; misses emit sound; exposed
  crate receives one valid hit; no duplicate camera/listener.
- Preservation: 86 protected files and 1,354 original transforms unchanged;
  all original non-root scene component records unchanged.
- Manual interactive verification: **not performed**. Keyboard/mouse feel,
  audio, animation, lighting, complete walked victory/defeat routes, physical
  door sweep safety and restart still require manual review.
- Windows64 development build: succeeded in the isolated copy, zero build
  errors/warnings. Executable was not manually played. Build artifacts stay
  outside the repository.

Reproduce pure checks: `dotnet run --project docs/facility-breach/standalone/Checks.csproj`.
Preservation: `python docs/facility-breach/verify-preservation.py`.
Unity isolated copy: launch with `-batchmode -nographics -executeMethod
FacilityBreach.Editor.FoundationValidation.Run`; method manages Play Mode and exit.
Evidence extracts are in `evidence/`.

## Remaining phases and team dependencies

This is foundation preparation, not all four completed contributions. Sound
Hunter focused decision integration, Blender robot models, Blender guns, final
combat presentation, full four-agent integration and manual validation remain.
The protected test-scene Hunter is not silently claimed as integrated.
No teammate Patrol Guard/Cover Defender/Flanker behaviors exist in Prototype.
No models, chest lights, effects or shot/reload audio are claimed here.
The map has a legend but active enemy dots await actual agent integration.
Robot-compatible card barriers and navigation need the subsequent adapter phase.

CLAUDE.md assigns main-scene coordination/approval to Pesanjith. The foundation
PR requests `it24101505` review. Do not bypass that approval to start the IS
branch. Once actually merged, fetch origin and create the exact IS branch from
current develop, then follow the requested model/weapon/final-combat sequence.
When reusing this combat branch, inspect merge strategy and merge current develop
without rewriting history or reintroducing foundation commits.

Viva: explain event-time resolution rather than frame-end timer comparisons;
per-weapon magazine ownership rather than creating ammo during swaps; logical
revival eligibility distinct from safe physical spawn; and authored scene
preservation via recorded hashes and component identities. Temporary movement
and automated scaffolding are labelled truthfully as AI-assisted work.
