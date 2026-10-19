# IS Flanker validation — Ashen Bandara, IT24100315

Date: 19 October 2026. Unity **6000.6.2f1**. Branch **is/ashen-flanker**, created from GV commit **211c0bb24de1542fdfcf808c5741218a589568f1**. Automated results and user-reported manual results are recorded separately below. No push or merge was performed.

## Source and scope

Read the completed local IS audit and used Ashen's supplied SE3062 specification text. The GV baseline and teammate Flanker provenance are identified in the contribution document. Only FL orchestration and new Flanker-specific planner/policy/adapters/editor checks were implemented; shared GV code, other agents, combat, mission, scene/model assets and packages were not intentionally changed.

Tests ran in a complete isolated project copy at **Temp/GVAgentValidation**, with copied Assets, Packages and ProjectSettings. This reused the name of the earlier GV validation copy; it is not the live project or an additional source branch. The source comparison described below checks that the tested Flanker files match the final implementation.

The first isolated attempt initially contained only new Flanker sources because the old temporary copy's baseline dependencies were absent. That attempt failed compilation on missing baseline types. The copy was rebuilt from the complete current project before the successful results below. This was corrected test setup, not a claimed successful compile. No baseline types were patched to hide the issue.

## Executed: deterministic decisions plus unchanged GV regression checks

Unity entry point: `FacilityBreach.Editor.FlankerPlannerTests.RunBatch`.

Actual final output:

```text
[IS Flanker] PASS 42 deterministic planner/recovery checks.
[GV Agent Controller] PASS 15 route and turning checks (Edit Mode).
[GV Agent Controller] PASS 13 navigation lifecycle checks (Play Mode).
```

The batch method first runs the production Flanker planner/policy against injected facts, then calls the existing `AgentControllerTests.RunBatch`. That existing method also rebuilds/audits the clip library **inside the isolated copy**, runs the existing Edit Mode checks and enters its controlled Play Mode checks. Main-project clip assets were not rebuilt by this test run.

The final run also produced all 20 existing clip-audit entries. The deterministic/GV batch returned exit code 0.

The 42 checks exercise:

- Target-centered viable generation for stationary targets at 12, 13, 14 and 15 units.
- Normalized ideal utility; route-versus-angle tradeoff; preferred distance; firing usefulness; repeatable symmetric ties.
- Alternatives when the otherwise selected point is unreachable, occupied or useless for firing.
- Incumbent retention under the .08 improvement margin and switching when improvement exceeds it.
- Explicit rejection reasons, all-rejected cooldowns and stopping queries after three attempts.
- Deferred Idle movement not being mistaken for a tactical failure.
- Failed-goal exclusion, alternate selection without target movement, six-second expiry and bounded repeated movement failure.
- Target movement/cooldown boundaries, including exactly four units.
- Keeping the approach reference stable at arrival and across brief loss/reacquisition, rather than repeatedly orbiting the target.
- Hidden input position not changing remembered observations or causing tactical/visibility-provider calls.
- Three-second continuation boundary, seven-second expiry, controlled return, cooldown without uncounted retries, two failed return attempts and twelve-second deadline.
- Reacquisition, inactivity/death clearing state, reset clearing exclusions/counters, fresh planning after reset and invalid input not reaching navigation.
- Reacquisition preserving an outstanding failure-specific retry deadline rather than shortening it. This edge case was found in final review, corrected, and included in the final reruns.

These execute the actual production classes, not a separately reimplemented dummy decision algorithm. Fake probes deliberately isolate scoring/temporal behavior from scene-layout uncertainty. They do not establish real-world physics or visual quality.

## Executed: controlled Prototype integration

Unity entry point: `FacilityBreach.Editor.FlankerSceneValidation.RunBatch`.

Actual output:

```text
[IS Flanker] PASS 11 Prototype integration checks (controlled Play Mode fixture).
```

The final controlled integration batch returned exit code 0.

The fixture opens real Prototype and verifies all four runtime robots still have shared movement/presentation components. It disables other decision brains during the focused scenario, creates a temporary floor/NavMesh patch around (1000,0,1000), warps the actual FL-04 onto it and uses the actual player, shared sensor, NavMesh adapter, GV controller and combat activity sink. It never saves the scene.

The 11 checks establish:

1. Four runtime robots retain shared GV components.
2. The actual Flanker occupies legal test navigation.
3. A stationary target twelve units away produces a viable target-centered plan using real navigation/physics.
4. Automatic NavMesh rotation stays disabled and the existing animation graph remains valid.
5. Player movement out of sight/range causes no new tactical probes or remembered-position changes.
6. Actual shared perception can reacquire the player.
7. Pause freezes decision time and replanning.
8. New decisions produce actual body movement across frames through the unchanged GV controller.
9. Real damage/death dispatch immediately clears IS observations/counters and marks the policy Inactive.
10. Death retains shared movement stopping and attack cancellation.
11. No unexpected Error/Exception/Assert messages were captured during this controlled scenario.

This test verifies movement across frames but not a whole authored-corridor flank/attack/return encounter. Animation graph validity is not visual approval of every clip/blend. Physical safe revival is not exercised by this new fixture; reset behavior is covered deterministically and existing lifecycle checks are listed separately.

## Executed: unchanged legacy enemy-rule suite

Unity entry point: `FacilityBreach.Editor.EnemyRuleValidation.Run`.

Actual result: `enemy-rules-passed.txt` in the isolated copy begins **108 checks**, and the launched Unity process returned **exit code 0**. No failed-result file was produced. The suite's source/assertions were not edited to accommodate the new planner.

This existing suite covers shared enemy/mission contracts and five historical Flanker assertions (constraints, replan gates, hidden memory and entering Search), along with other agents and combat. It uses controlled isolation/frozen movement in several checks and temporarily re-enables automatic NavMesh rotation in one legacy Flanker section. Its pass does not independently establish single-writer visual quality throughout that test segment, optimal tactical scoring, all-four combat performance or exhaustive recovery. The new focused fixture separately checks that production FL leaves shared rotation ownership intact.

## Compilation, warnings and preservation

The successful complete-copy runs compiled the new runtime/editor scripts and executed their entry points. Final test logs contain no C# compilation errors or test exceptions. The controlled integration captured zero unexpected Console errors during its scenario. Existing project compiler warnings may remain; no project-wide warning-free claim is made.

The only modified tracked game source is `Assets/Scripts/FacilityBreach/AI/Roles/FlankerBrain.cs`. New production/editor files are Flanker-specific. Shared mover/presentation code, other brains, combat/mission scripts, scenes, models and packages remain baseline. New scripts have `.meta` files; the original FlankerBrain GUID was preserved.

The two pre-existing ProjectSettings changes remain local and outside this contribution. Their starting hashes were:

```text
ProjectAuditorSettings.asset  9e68444b9bda981e2927a98df9edf69ea88362f0c6b7f3c81b39b9d515d2a416
PackageManagerSettings.asset 85d73a27b51e7c052190e2b9498a591a1af692ca6f586f3f8af430028aabbd86
```

Final preservation checks confirmed these hashes still match, the tested Flanker source snapshot matches the implementation, and 211c0bb remains in branch ancestry. Comparison of every existing tracked Assets/Packages file except the authorized FlankerBrain change found no unexpected content changes. `git diff --check` passed; new script metadata and documentation whitespace/links were checked. The commit scope is restricted to the Flanker implementation, focused editor checks, required script metadata and these two IS documents; unrelated ProjectSettings and local ReferenceDocs are excluded. Generated logs/project copies remain under ignored Temp. Existing ReferenceDocs/audit/GV guide are preserved; only the requested local IS viva guide was added.

## Reproduce intentionally

Interactive menus, with Play Mode initially off:

- **Facility Breach → IS Flanker → Run Deterministic Planner and Recovery Tests**.
- **Facility Breach → IS Flanker → Run Prototype Integration Checks**. It offers to preserve unsaved scene work before opening Prototype, then enters/exits Play Mode. Use an isolated project copy for the closest reproduction of the recorded run.
- **Facility Breach → GV Agent Controller → Run Movement Checks** / **Run Play Mode Movement Checks**.

Batch example; substitute one of the three entry points above and use a separate isolated copy:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath '<isolated-project-copy>' `
  -executeMethod FacilityBreach.Editor.FlankerPlannerTests.RunBatch `
  -logFile '<writable-path>\is-flanker-tests.log'
```

Do not add `-quit`: these methods complete controlled Play Mode work and manage their own exit. Do not start a second editor on the project already open. The legacy entry can be run only in a disposable/isolated copy because it loads scenes and exits the editor. Logs from this implementation are `Temp/is-flanker-tests.log`, `Temp/is-flanker-scene.log` and `Temp/is-flanker-legacy-enemy.log` and may be deleted as generated material later.

## User-reported manual Prototype checks

Ashen Bandara reported the following five cases passed after testing this IS implementation in Prototype. These are user-performed observations, not additional automated assertions or gameplay independently witnessed by the coding assistant.

| Case | Result reported by Ashen |
| --- | --- |
| Stationary visible player | Flanker selects a useful side position. |
| Barricade obstruction | Alternative/recovery or bounded fallback, without endless jitter. |
| Lost sight and reacquisition | Remembered observation behaviour works; reacquisition permits a new plan. |
| Defeat and revival | Movement and decision logic resume correctly. |
| All four agents active | Gameplay felt responsive, without obvious stalls. This is qualitative observation, not measured performance evidence. |

**Manual gameplay Console status is unspecified.** Ashen has not separately confirmed it for these IS tests. The zero unexpected errors recorded by the controlled automated fixture above apply only to that fixture.

## Pending manual checks and limitations

The five reports above do not establish every detail of the following checks. Follow the numbered demonstration in `IT24100315_IS_Flanker.md` for additional verification:

- Measured 12-to-15-unit authored-room scenarios, completed arrival and legal attack without repeated orbiting.
- Door obstruction and firing-position invalidation while the player remains still.
- Visible teammate occupancy in the real layout; fixed samples may miss a useful alternative.
- Full observation-expiry timing, blocked return and restart in authored corridors.
- Inspector usability, legibility and diagnostic usefulness during a live demonstration remain pending.
- Measured performance (FPS/profiling) and standalone build verification remain pending. The all-four-agent report is qualitative only.
- Complete card/device/escape playthrough and second-machine verification remain pending.

Weights are initial tunings, not optimality claims. Unity supplies route search; no custom A* or teammate algorithm authorship is claimed. The bounded Holding state intentionally waits for target displacement/reacquisition/reset after its retry budget; reopening a route alone after exhaustion does not restart endless retries. No prior development effort or passing test results were fabricated.
