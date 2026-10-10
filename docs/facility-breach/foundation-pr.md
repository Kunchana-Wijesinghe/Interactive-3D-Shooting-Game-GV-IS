Prototype previously contained the environment but no playable mission foundation.
This preparation PR adds Facility Breach's card/shutdown/escape loop, temporary
player adapter, shared combat/lifecycle contracts and finite resources on
`it24101848-gv-combat`. It is Phase 0, before the focused Sound Hunter contribution.

Prototype receives one additive FB_Gameplay root. Runtime children add pickups,
three Machinery card barriers, a fictional shutdown device, HUD/map/menus and an
explicit waste placeholder because the committed scene lacks the waste generator's
output. Existing camera and listener are reused. Door code gains safe closing,
pause gating and an adapter API; authored objects and serialized settings remain.
Prototype is added to build settings to support standalone restart.

Validation: 50,102 standalone C# assertions; 27 Unity Editor assertions; 51
cumulative assertions in actual Prototype Play Mode, including wall/muzzle shot
blocking and resource interactions; Windows64 development build succeeded with
zero build errors/warnings. Unity ran in a temporary isolated copy because the
user's Editor is open. Checks are an Editor harness, not NUnit. No manual
keyboard/mouse/audio/animation/lighting or walked end-to-end mission is claimed.

Preservation: all 86 protected Perera files retain their workspace baseline
hashes, including the user's seven pre-existing material edits. All 1,354 authored
transforms and original scene component records remain unchanged. No generators
were run; unrelated local settings are excluded.

Limitations: Bandara's controller and the three teammate AIs are missing. No
agents are substituted. Robot permissions/navigation, active enemy map dots,
focused Sound Hunter integration, Blender models, final effects/audio and full
four-agent playthrough remain for subsequent phases. The waste placeholder needs
world-builder visual review. Shared enemy adapters exist but are not instantiated.

Scene coordination review is requested from it24101505 as required by CLAUDE.md.
Do not merge or start the IS branch until that approval and actual merge occur.
Architecture, tuning, reuse audit and real evidence are under docs/facility-breach.
