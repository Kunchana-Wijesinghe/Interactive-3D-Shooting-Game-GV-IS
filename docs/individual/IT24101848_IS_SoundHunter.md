# IS Report — Sound Hunter (SH-01)

**Student:** Perera H.L.P.S. — IT24101848
**Component:** Intelligent Systems (IS) — Sound Hunter: perception of auditory stimuli, localisation of audio cues, dynamic investigation
**Project:** Facility Breach (Unity 6000.6.2f1)
**Status note:** Everything below is read from the committed source. It has not been re-run in a Unity Play mode session while writing this report; see "Verification" for what exists and what still needs a run.

---

## 1. Purpose and scope

The Sound Hunter does not see the player from far away. It reacts to **noise events** anywhere on the map, works out where they came from, walks there to investigate, and only switches to active pursuit once it actually sees the player. This report covers three parts:

1. **Perception of auditory stimuli** — how a sound becomes something the hunter can hear.
2. **Localisation of audio cues** (the "triangulation" part) — how heard sounds are turned into a remembered, ranked set of source positions.
3. **Dynamic investigation** — how the hunter decides where to go, replaces goals, searches and gives up.

## 2. Architecture

```text
gameplay emitters (gun, thrown can, kick, footsteps)
   -> SoundEventBus.Emit(SoundStimulus)                        [Perera/Core/SoundStimulus.cs]
   -> SoundHunterListener / FacilityBreachSoundHunterBrain      [hearing sense]
        -> HearingGraph + HearingEvaluator (is it audible?)      [acoustic propagation]
        -> SoundMemory + SoundRanker (what do I remember, which is best?)
   -> SoundHunterBrain decision states                          [Patrol / Investigate / Search / Combat / Dead]
        <- HunterVision (sight sense, the only source of player knowledge)
   -> IHunterMovement (movement interface, owned by the movement controller)
```

Design rules that follow the project guidelines (CLAUDE.md):
- Decision logic talks to movement only through the `IHunterMovement` interface, never by embedding movement in the state machine.
- Perception stays inside the Sound Hunter module. The shared movement controller is not modified.
- **Knowledge rule:** the target reference in `HunterVision` is only what to look *for*. The player's position becomes knowledge solely through `TryPerceive`, and only when range, field of view and line of sight all pass. Memory stores **where a sound was heard, never where the player is now.**

## 3. Perception of auditory stimuli

| Piece | File | What it does |
|---|---|---|
| `SoundStimulus` | `Assets/Perera/Scripts/Core/SoundStimulus.cs` | Position, hearing budget (`Strength`), creation time, category, source id. Categories: Gunshot, Footstep, Thrown, Interaction, Other. |
| `SoundEventBus` | same file | Lightweight static event bus; listeners must unsubscribe in `OnDisable` and statics reset on play start. |
| `HearingGraph` | `Perera/Scripts/SoundHunter/HearingGraph.cs` | Acoustic propagation graph: an edge cost is the budget a sound loses crossing that link. It is **not** a navmesh. Deterministic Dijkstra (`MinCostsFrom`, settles in (cost, index) order). |
| `HearingEvaluator` | `.../HearingEvaluator.cs` | A sound is audible iff `minCost(source, listener) <= Strength x multiplier`. The boundary is inclusive; disconnected nodes are never heard; non-finite or negative values are rejected. |
| `SceneHearingGraph` | `.../SceneHearingGraph.cs` | Authored room zones and links; maps a world position to a node by room membership, never by nearest distance across a wall (`MaxMappingDistance` 10). |
| `FacilityBreachHearingGraphSetup` | `Assets/Scripts/FacilityBreach/AI/SoundHunter/` | In the game level: a **1 m acoustic grid** over the floor. Each step costs 1; walls block; a **closed door adds 15** and an **open door adds 1**. The graph rebuilds when any door opens or closes (checked every 0.5 s). |

Every input is validated for non-finite values before it can reach the graph.

### Facility integration (what the player can do to be heard)

| Action | Source of the sound |
|---|---|
| Firing a weapon | Gunshot stimulus (strength 40, from the player's position) |
| Thrown can landing | Thrown stimulus (strength 22) |
| Kicking a box or barrel; a kicked prop hitting something | Interaction stimulus, forwarded by `FacilityNoiseBridge` from the facility noise hub (kick radius 12 m; impact radius grows with speed) |
| Sprinting | Footstep stimulus, radius 6 m. Walking is silent to enemies. |

**Player-made noise is heard map-wide.** The facility is 42 m x 60 m, and the walking distance between the entrance and the Sound Hunter's post is larger than the hearing budgets, so noises made at the entrance were never audible. `FacilityBreachSoundHunterBrain` therefore stores player-caused sounds directly, with no distance limit; the hunter still only *walks to the source* and still needs line of sight before it attacks. Sounds made by other enemies keep the graph-distance rule.

## 4. Localisation of audio cues

> **Honest scope note.** The system does not do geometric triangulation (intersecting bearings from several listeners). Each stimulus already carries its origin, so localisation here means *deciding which remembered origin to trust and walk to*, using propagation cost and ranking. True bearing triangulation would need several listeners or directional hearing and is listed under future work.

**`SoundRanker`** — deterministic ordering of heard sounds (best first). Tie-break chain:
1. higher category priority (Gunshot 4, Thrown 3, Interaction 2, Footstep 1, Other 0)
2. higher *remaining* budget (louder or closer at the listener)
3. newer creation time
4. lower source id
5. lexicographic position

**`SoundMemory`** — remembered sound origins:
- Identity of a source is `(SourceId, Category)`. Hearing it again **refreshes** the entry (new position, new timer) and assigns a new, strictly increasing `Revision`, so consuming an old investigation can never delete a newer refresh.
- Entries expire when `now - HeardAt >= ExpirySeconds` (the boundary expires). Over `Capacity` (default 4) the lowest-ranked entry is dropped.
- Time is passed explicitly on one caller-owned clock, which keeps expiry testable.
- Memory lifetime: 12 s in the standalone listener; **40 s** in the facility brain so the hunter can finish the long walk across the map.

## 5. Dynamic investigation

State machine in `SoundHunterBrain` (standalone prototype) and the facility version `FacilityBreachSoundHunterBrain` (Patrol, Investigate, Search, Combat, Inactive):

| Transition | Trigger |
|---|---|
| Patrol -> Investigate | Memory holds a sound |
| Patrol / Investigate / Search -> Combat | `HunterVision` sees the target (range, field of view and line of sight) |
| Investigate -> Search | Arrived at the remembered position, or movement failed (short bounded in-place search) |
| Search -> Investigate | Search finished and another valid memory exists, or a better sound arrived |
| Search -> Patrol | Search finished and memory is empty |
| Combat -> Investigate | Sight lost for `LoseSightDelay`: go to the **last seen position**, then Search |
| any -> Dead | Health died; sensing, movement and attacks stop permanently |

**Goal replacement (checked every replan interval):** a memory entry that ranks *strictly* better replaces the goal; a newer revision of the *same* source updates the goal position; equal or worse sounds never replace the goal and stay in memory; if the goal expired and nothing else exists, the hunter returns to Patrol. The investigated entry is forgotten *by revision*, so a refresh that arrived meanwhile survives.

**Bounded work:** vision runs every `SensorInterval` (0.1 s); path requests only every `ReplanInterval` (0.5 s) and at most one per `FailedRetryCooldown` (4 s) after a failure, so an unreachable goal cannot spam path requests.

**Vision (`HunterVision`):** range 18 and field of view 120 degrees (16 m in the facility version), with ray casts to three body points (centre, upper, lower) so partial cover still allows detection.

| Parameter | Value |
|---|---|
| Patrol pause | 1.5 s |
| Arrival distance | 1.2 |
| Search duration / sweep | 3.5 s, +-70 degrees at 120 degrees/s (facility version: 4 s) |
| Attack range / stop distance | 14 / 7 |
| Attack cooldown, damage, hit chance | 0.8 s, 6, 60% |
| Lose-sight delay | 2.5 s |
| Facility HP | 80 |

## 6. Files

- Standalone prototype: `Assets/Perera/Scripts/SoundHunter/` (Brain, Listener, Vision, HearingGraph, HearingEvaluator, SceneHearingGraph, SoundMemory, SoundRanker, HeardSound, movement interface, debug HUD, scenario runner)
- Facility integration: `Assets/Scripts/FacilityBreach/AI/SoundHunter/` (`FacilityBreachSoundHunterBrain`, `FacilityBreachHearingGraphSetup`, `SoundHunterNavMeshMover`) and `Assets/Scripts/FacilityBreach/Noise/` (`FacilityNoiseHub`, `FacilityNoiseBridge`)
- Presentation: `SoundHunterTacticalGear.cs` (orange-accented tactical outfit, rifle, muzzle flash) and the enemy gunshot audio

## 7. Verification

| Evidence | Location | Status |
|---|---|---|
| Pure-logic self tests (ranker, memory, graph, evaluator) | `Assets/Perera/Scripts/Editor/PereraLogicSelfTests.cs` | exist; **not re-run while writing this report** |
| Scenario runner (hearing, investigation, combat, death) | `SoundHunterScenarioRunner.cs`, `PereraSoundHunterScenarioLauncher.cs` | exist; not re-run |
| Facility integration validation | `Assets/Editor/FacilityBreach/SoundHunterValidation.cs`, `PlayModeIntegrationValidation.cs` | exist; not re-run |
| Manual Play mode check of the latest map-wide hearing change | — | **pending** |

Play mode check to record: make noise at the entrance (shoot, kick a barrel, throw a can, sprint) and watch the orange SH dot on the minimap move toward it; step into its view and confirm it chases and shoots; confirm the Console shows no errors.

## 8. Limitations and future work

- No true bearing triangulation; add directional hearing or multiple listeners to estimate a source from several cues.
- Map-wide hearing is a gameplay choice for the player's noise; a tunable "global loudness" option would help balance.
- The radii and strengths are constants in code; a ScriptableObject config would let designers tune them.

## 9. AI assistance disclosure

Parts of the facility integration (kick, prop-impact and sprint noise bridge, map-wide player-noise hearing, Sound Hunter tactical gear and gunshot sound) were developed with the help of an AI coding assistant (Claude Code) in the author's working session. Adjust this statement to your course's disclosure policy before submitting.
