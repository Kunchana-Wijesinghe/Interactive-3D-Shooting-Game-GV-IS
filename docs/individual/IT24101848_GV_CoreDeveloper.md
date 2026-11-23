# GV Report — Core Developer

**Student:** Perera H.L.P.S. — IT24101848
**Component:** Game Visuals/Gameplay (GV) — Core Developer: core gameplay loop, weapon mechanics, ballistics, damage system
**Project:** Facility Breach (Unity 6000.6.2f1)
**Status note:** Everything below is read from the committed source. It has not been re-run in a Unity Play mode session while writing this report; see "Verification".

---

## 1. Purpose and scope

This part of the project is the player-facing core: the mission loop (what the player must do and how they win or lose), the weapons the player uses, how a shot travels, and how damage is applied to the player, enemies and props.

## 2. Core gameplay loop

**Objective:** retrieve the access card near the toxic waste, connect it to the device in Machinery within **five minutes**, then escape alive through the exit.

The loop is a pure, testable state machine (`Assets/Scripts/FacilityBreach/Core/MissionRun.cs`):

```text
Menu --Start--> Active --card collected + Connect--> Deactivating (5 s) --> Escape --Exit alive--> Won
                  |                                   |                        |
                  +----------- timer reaches 0 -------+----------------------- (Lost: "Time expired")
                  +----------- player eliminated ----------------------------- (Lost: "Player eliminated")
```

- Timer starts at 300 s; during Deactivating the shorter of the main timer and the 5 s shutdown resolves first, and **timeout wins ties**.
- The card is required to Connect; Connect is refused unless the device is in range, the mission is Active and not paused.
- Exit works only in the Escape state, within 1.2 m of the exit, with the player alive.
- Pause, restart and main menu are supported (restart reloads the scene and resets the run).

**World interactions** (`FacilityBreachRuntime`, `ResourceStation`, `FacilityInteractionDispatcher`):
- First-aid kit (white box with a red cross): +50 HP only when HP is 50 or lower.
- Ammo crates (olive green): shoot one open and it drops a pack of bullets (25 or 50 rounds); it respawns after 15 s.
- Weapon swap stations, the gold access card, the boom device (status lamp green while active, red once deactivated).
- Machinery doors need the card, and only the player can open them; barricades can be moved.
- **Controls:** WASD, Shift sprint, Space jump, E interact, F kick boxes and barrels, left click shoot, R reload, Q throw a can, Escape pause.

## 3. Weapon mechanics

`PlayerResources` (`Assets/Scripts/FacilityBreach/Core/PlayerResources.cs`) models three prototype weapons with a **shared reserve**:

| Weapon | Magazine | Damage per shot |
|---|---|---|
| Ammo1gun | 50 | 10 |
| Ammo2gun | 75 | 15 |
| Ammo3gun | 100 | 20 |

- Each weapon keeps its own magazine; the reserve starts at 50 and caps at 200 (`CollectAmmo`).
- Reload takes 1.5 s and tops the magazine up from the reserve (`min(reserve, capacity - magazine)`); an empty magazine triggers reload automatically; "NO RESERVE AMMO" is shown when none is left.
- Fire cadence 0.2 s between shots (`ShotInterval`); fire is blocked while reloading or paused.
- The three first-person gun models were modelled in Blender (`Blender_files/Guns/`) and exported to FBX with `Muzzle` attachment points; the equipped model is swapped in `UpdateWeaponVisual`.
- Feedback per shot: muzzle flash, bullet tracer, impact sparks on environment hits, a 3D gunshot sound, and a gameplay noise event that the Sound Hunter can hear.

## 4. Ballistics

The game uses **hitscan** (an instant ray), not simulated projectiles: there is no bullet travel time, drop or spread. Implementation (`FacilityBreachRuntime.TryFire`):

1. Cast a ray from the camera forward up to **50 m** (`ShotRange`) to find the *intended point* (the aim point).
2. Check the camera-to-muzzle segment first: a long view model can put the muzzle inside or beyond nearby cover, so cover in front of the barrel absorbs the shot.
3. Then cast from the muzzle toward the aim point; the first valid hit receives the damage.
4. The hit collider's parent is searched for an `IDamageable`; a miss draws a tracer to the aim point.

A standalone weapon for the combat test scene (`Assets/Perera/Scripts/Combat/HitscanWeapon.cs`) follows the same two-step aim and obstruction idea with its own parameters: damage 25, range 60, 450 rounds/min, magazine 12, reserve 48, reload 1.6 s, `AmmoMagazine` pure logic (finite reserve, reload tops up, rounds left in the magazine are kept).

Thrown cans (Q) are physical rigidbodies launched forward and up; they make noise where they land.

## 5. Damage system

**Contract:** `IDamageable.TakeDamage(DamageInfo)` where `DamageInfo` carries amount, hit point, direction and source (`Assets/Perera/Scripts/Core/IDamageable.cs`). Any object with an `IDamageable` in its parent chain can be shot.

| Target | Implementation | Rules |
|---|---|---|
| Player | `PlayerDamageAdapter` -> `PlayerResources.Hurt` | Ignored when dead, not running, paused, or the amount is non-finite or not positive. Reaching 0 HP loses the run ("Player eliminated"). |
| Enemies | `EnemyCombatAdapter` -> `EnemyRoster` | HP: PG-01 60, SH-01 80, CD-03 90, FL-04 70. At 0 HP an enemy is **temporarily inactive for 30 s**, then revives where it fell (safe-spawn check); killing all four within the same window makes the defeat permanent. |
| Generic actor | `Health` component (`Perera.Core`) | Events `Damaged` and `Died`; death fires exactly once until `ResetHealth`. |
| Ammo crates | `ResourceStation` | Shot once -> breaks, drops pickup, plays a destruction sound. |

**Enemy offence (so damage is fair):** each enemy telegraphs a shot for about 0.4 s (the chest light turns white), then 40% of shots miss, and a hit deals 10 damage. Line of sight is checked at the moment the shot resolves.

## 6. Files

- Core: `Assets/Scripts/FacilityBreach/FacilityBreachRuntime.cs`, `Core/MissionRun.cs`, `Core/PlayerResources.cs`, `Core/EnemyRoster.cs`
- Weapons/ballistics: `FacilityBreachRuntime.TryFire`, `FpsBulletTracer.cs`, `FpsMuzzleFlashV2.cs`, `FpsImpactSparks.cs`, `Assets/Perera/Scripts/Combat/` (`HitscanWeapon`, `AmmoMagazine`)
- Damage: `Combat/PlayerDamageAdapter.cs`, `Combat/EnemyCombatAdapter.cs`, `Assets/Perera/Scripts/Core/Health.cs`, `IDamageable.cs`
- Interactions: `Interactions/ResourceStation.cs`, `FacilityInteractionDispatcher.cs`

## 7. Verification

| Evidence | Location | Status |
|---|---|---|
| Play mode integration and foundation validation | `Assets/Editor/FacilityBreach/PlayModeIntegrationValidation.cs`, `FoundationValidation.cs`, `FinalCombatValidation.cs`, `EnemyRuleValidation.cs` | exist; **not re-run while writing this report** |
| Pure ammo/health logic | `Assets/Perera/Scripts/Editor/PereraLogicSelfTests.cs` | exists; not re-run |
| Earlier reports | `docs/facility-breach/phase-4-report.md`, `phase-0-report.md`, `weapon-models-report.md`, `playmode-integration-fixes.md` | committed documents |
| Manual check of the latest changes (jump, card-locked Machinery doors, enemy revive in place) | — | **pending** |

Play mode check to record: complete a full mission (card -> device -> 5 s -> exit), verify the HUD ammo counts through a reload and a weapon swap, verify a shot is blocked by cover in front of the muzzle, die once to confirm the loss screen, and confirm the Console shows no errors.

## 8. Limitations and future work

- Hitscan only; no bullet drop, spread or recoil patterns. A projectile mode could be added per weapon.
- Damage values are constants in `PlayerResources`; a data asset per weapon would allow tuning without code changes.
- Player and enemy damage rules (10 per enemy hit, 40% miss) are not yet scaled by distance or difficulty.

## 9. AI assistance disclosure

Several items in this report (card-locked Machinery doors, revive-in-place, jump, first-aid kit and ammo crate visuals and sounds, boom device, enemy weapon effects) were developed with the help of an AI coding assistant (Claude Code) in the author's working session. Adjust this statement to your course's disclosure policy before submitting.
