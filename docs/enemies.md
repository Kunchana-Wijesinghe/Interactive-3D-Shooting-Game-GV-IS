# Enemy Roles — Facility Breach

Status: facts below are code-checked (read from the game scripts). Behavior has **not** been confirmed
in a Play mode run unless stated otherwise. See "Unverified" at the end for details that may differ
once observed in-game.

## Map reference

The facility floor runs **x −21 to +21, z −30 to +30** (42 m wide × 60 m long, portrait layout).
Entrance is at the south end, exit at the north end.

## Shared combat rules (all four enemies)

- **Sight range:** ~16 m
- **Shooting range:** ~12–14 m
- **Warning wind-up:** each shot is telegraphed for about 0.4 s (the enemy's chest light turns white) before it resolves
- **Miss rate:** about 40% of shots miss
- **Line of sight:** enemies do not see through walls
- **Revival:** a killed enemy is out for 30 seconds, then returns to life where it fell
- **Permanent defeat:** only happens if all four enemies are killed within the same 30-second window

## Roles

| Enemy | Colour | HP | Start location | Role |
|---|---|---|---|---|
| **Patrol Guard (PG-01)** | Teal | 60 | Patrols a loop at x 5–7, z 18–22 (east of centre, north half) | **The sentry.** Walks a fixed route through corridors and doorways, shoots on sight. If it loses the player, it searches the last known area in a set pattern, then returns to its route. Also reacts to barricades the player moves. |
| **Sound Hunter (SH-01)** | Orange | 80 | Near the Exit | **The listener.** Does not see the player from afar. Hears player noises (shots, thrown cans, kicks, sprinting) from anywhere on the map and walks to where the noise occurred — not to the player's actual position. Only chases and shoots once it has actually seen the player. Has the longest-range pull on the map. |
| **Cover Defender (CD-03)** | Violet | 90 | Inside Machinery, facing the device | **The guard of the objective.** Active from the start of the game, same as the others — it is contained inside Machinery by the card-locked door, not dormant. Takes cover when it sees the player and pops out briefly to shoot. Highest HP of the four; its job is to protect the boom device. |
| **Flanker (FL-04)** | Lime | 70 | Inside Machinery | **The attacker.** Instead of charging straight in, it calculates a path around to the player's side and fires from there. After a fight, it searches briefly and returns to its spot. Also active from game start, contained by the same locked door as Cover Defender. |

## How they fit together

- **Patrol Guard** and **Sound Hunter** are the two enemies who can reach the player *before* Machinery is
  opened — PG on its fixed route, SH reacting to noise from anywhere on the map.
- **Cover Defender** and **Flanker** are active and running their AI from the start of the game as well,
  but they're physically contained inside the card-locked Machinery room. The locked door keeps them in
  and everyone else out until the player opens it — it is a containment barrier, not a spawn trigger.
- The player needs the access card to open that door and reach the Machinery pair.

## Unverified

The following have been read from team-written AI code but not confirmed in a Play mode run, and may
differ in practice:

- Cover Defender's exact peek timing/frequency when popping out of cover
- Flanker's exact angle-selection logic when choosing a flanking path
- Any other fine-grained behavior not directly observed in gameplay
