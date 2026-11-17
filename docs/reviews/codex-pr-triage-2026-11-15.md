# Codex pull requests: what was taken, 15 Nov 2026

Every open Codex pull request was read against `claude/playtest`. What was worth keeping is now in `claude/playtest`; the rest is either already done there, out of date, or waiting for a decision. Nothing here has been run in Unity. Rule tests (`dotnet test tools/rules-tests`): 2,157 pass, 0 fail.

## Taken into claude/playtest

| PR | What was taken | Why |
|---|---|---|
| 11, 12, 13, 14 | The four boundary-test drafts (firearm, planets, space, gibs) | All pass against today's rules; free coverage. |
| 39, 40 | Whole (saved controls come back after a restart; saves never overwrite a good backup or a newer save) | Taken on 14 Nov. |
| 41, part | `TerrainCollision` and its changes to `PlanetSurface` and `PlanetWorld`: the ground under the player and a parked ship is found by which tile it lies in, not by distance to a tile's middle (which tall mountains and deep valleys throw off); finer ground is made solid before the coarser piece under you is put away; a cave mouth tile waits for its cave walls. | Falling through the ground is the worst bug a landing game can have, and the realistic ground made the old test less reliable. |
| 41, part | `FirearmState` fire-rate fix | At the Deck's 30 to 40 frames a second an automatic gun fired slower than its rate, because the time left over each frame was thrown away. |
| 41, part | `EnemySpawner`: a gun counts as loose only when it is really lying on the ground, and a camp member cannot be woken twice | A gun in the player's hands has no holder, so the spawner could delete the player's own captured gun when its old owner's camp went to sleep or the loose-gun cap was reached. |
| 41, part | Tests for the above (`TerrainCollisionTests`, `FirearmCadenceTests`, `FirearmCadenceBoundaryTests`, PlayMode `TerrainCollisionPlayModeTests`, `CapturedWeaponCleanupTests`) | |

## Waiting on Matthew

Matthew chose (15 Nov) to hold these until after the Phase 1 sign-off. PR 41's new mechanics are not taken yet: crouch, lean, wall runs and ledge climbs (`ParkourMotor`, `ParkourRules`, `StanceRules`, head and camera clearance), up to two enemy ships whose crews fly, land and board (`EnemyFleet`, `EnemyShipCrew`, `EnemyShipRules`), enemies shooting into an occupied ship, and the on-screen marker pointing to your ship (`ShipLocator`). They are new features rather than fixes, enemy ships belong with the factions in Phase 2, and none was in Matthew's requests, so they wait for his call. PR 41 stays open until then.

## Already done on claude/playtest, or out of date (proposed to close)

| PR | What it was | Why close |
|---|---|---|
| 9 | Art briefs for the look pass | The look thread built the guns, ship and enemies since; the briefs describe the old plan. |
| 10 | Seed audit | Its gameplay findings are physics spin on dropped guns and gibs, which the seed rule does not cover (seeds decide the world, not every bounce). The one real defect it named, `GetInstanceID`, is already gone. |
| 15 | Phase 1 playtest checklist draft | Replaced by `docs/phase-1-signoff-checklist.md`. |
| 17 | Milestone 7 design questions | Milestone 7 is done and merged. |
| 18 | Deck cost audit | Finding 1 (gib cap) is fixed (PR 37, now in playtest). Finding 2 is Codex job 2. Finding 3 (terrain collider safety path) is partly addressed by the PR 41 contact fix. Findings 4 to 6 (native resources on restart, HUD strings, job reset) are logged below for a Deck measurement. |
| 19 | Unity 6 audit | Finding 2 is the gib cap (fixed). Finding 3 (stale thrust across pause) was fixed in Milestone 10. Findings 1 and 4 are logged below. |
| 20 | Config audit | An inventory of numbers in code; useful reading, nothing to merge. |
| 21 | SETUP.md rewrite | `docs/SETUP.md` has been rewritten many times since; merging would undo that. |
| 25 | Launch and audio integration checks | Belongs to the audio thread's PR 28, which is still separate. |
| 26 | Control hints that follow rebinding | The flight, map and on-foot hints already read the live bindings (`GameInput.Describe`). |
| 32, 33 | Squad tactics, navigation fixes | Matthew chose "take the good parts, close both" on 14 Nov; the good parts are in. |
| 39, 40 | Saved controls, safer saves | Taken whole. |

## Logged for the first Deck measurement (not fixed)

- Hitboxes and shot rays may sample different instants of a moving enemy's pose (Unity 6 audit 1). Check by shooting the hand of a strafing enemy.
- Per-run materials and meshes are not destroyed on restart (Deck audit 4). Check memory after ten restarts.
- The combat HUD builds its ammo and pickup strings every frame (Deck audit 5).
- Resetting the planet job lists forgets tasks still running (Deck audit 6).
- A `GravityBody` enabled before the solar system exists is never registered (Unity 6 audit 4); the game always builds them in the safe order today.
