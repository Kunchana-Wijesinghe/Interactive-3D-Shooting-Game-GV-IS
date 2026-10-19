# Cover Defender validation evidence

This folder contains the results of a Unity Play Mode check against
`Assets/Scenes/Prototype.unity` using Unity `6000.6.2f1`.

## Results

- Unity runtime and editor assemblies compiled successfully. The compiler
  output, including pre-existing warnings in unrelated scripts, is captured in
  [compilation-results.txt](./compilation-results.txt).
- All 15 focused integration assertions passed, including Cover Defender
  behavior, a collider/raycast check for the Machinery room wall gap, and a
  check that all three access-card blockers remain active. The captured output is in
  [playmode-results.txt](./playmode-results.txt).
- The final clean-console gate did **not** pass. Unity logged an
  `ArgumentOutOfRangeException` from `UnityEditor.Search.SearchDatabase` during
  editor startup. The stack is in the results file; it points to Unity Editor
  Search code rather than a game script. Treat this as an outstanding editor
  error, not a clean-console result.

## Re-running the focused check

Open the project in Unity Editor and invoke the public editor method
`FacilityBreach.Editor.EnemyRuleValidation.RunCoverDefender` using the Unity
Editor `-executeMethod` option (without `-batchmode`, because the check runs
over Play Mode updates). The method loads `Prototype.unity`, enters Play Mode,
and writes a fresh `playmode-results.txt` in this folder before evaluating the
final console gate.

## Manual Play Mode check

1. Open `Assets/Scenes/Prototype.unity` and enter Play Mode.
2. Start the mission and move the player into CD-03's field of view.
3. Confirm CD remembers the visible player, moves toward a reachable position
   behind world cover, and attacks through the existing combat system.
4. Break line of sight. Confirm CD searches the last known position, peeks from
   cover when possible, then returns to its post after the hold period.
5. Damage CD and confirm the existing enemy health changes. Check that SH, PD,
   and FL remain present and functional.
6. At the Machinery room's left doorway, walk up to the wall segment directly
   beyond the doorway and confirm there is no side gap. Confirm the orange
   access-card blocker remains until the card is collected.
7. Inspect the Console. The editor startup Search exception described above is
   currently observed in automated validation; distinguish it from any new
   gameplay errors.
