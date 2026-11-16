# Gun ladder (16 Nov 2026)

Matthew asked for guns that upgrade as you kill a set number of enemies, getting better and better through the run. This is our own version of a kill-count weapon ladder: our guns, our numbers, our readout. Nothing here has been run in Unity.

## How it plays

1. A run starts with the test pistol.
2. Every enemy you kill counts one, whatever gun you killed it with.
3. When you have made a step's kills, the next gun lands in your hands at once, fully loaded, with a banner ("NEW GUN: Double Zapper") and a reward sound. The fanfare climbs with the ladder (reward tiers 1 to 4).
4. The ladder's old gun is gone. If you were holding a gun you picked up off an enemy, that one is dropped at your feet instead, so you can take it back if you like it more.
5. The last gun stays to the end of the run. You never go back down. Dying ends the run; a new run starts at the bottom.
6. A line above the Focus meter always says where you are: `GUN 2 OF 5   3 KILLS TO LONG SHOT`.

| Step | Gun | Kills to the next |
|---|---|---|
| 1 | Test Pistol | 4 |
| 2 | Double Zapper (enemy shotgun) | 5 |
| 3 | Long Shot (enemy sniper) | 6 |
| 4 | Buzz Gun (enemy SMG) | 7 |
| 5 | AK-ish rifle | stays to the end |

22 kills reach the rifle. All numbers are first guesses, not playtested.

**Why this order:** the five guns we have were not made as a power ladder. Measured by damage over a long fight (torso), it runs sniper about 54 a second, shotgun 88, pistol 120, SMG 240, rifle 340. The rifle is the strongest in the near-constant combat Matthew wants, so it is the top. The specialists come early so each step plays differently (close-range blasts, then long shots, then spray). The ladder gets much more interesting once Milestone 13 adds the planned roster (hand cannon, ray gun, laser revolver and so on): each is one more line on the asset.

## Changing it (no code)

Open `Assets/OrbitalAnte/Resources/Config/GunLadderConfig` in Unity (made by setup). `Rules > Rungs` lists the steps: Weapon Id is the gun's file name (Pistol, Smg, Rifle, Shotgun, Sniper), Kills To Next is the count. Untick `Enabled` to go back to starting with the rifle and keeping it.

## Fit with the ante and point system

The ante rules (`Runtime/Ante`) are not wired into play until Milestone 14, so the ladder gives no points yet. When they are, the natural hooks are: a score event for each step climbed, and the ladder carrying on across antes within a run (it does already). Nothing in `Runtime/Ante` was changed.

## Phase order

Phase 1 says "refine before expanding", and gun progression is closer to Phase 2 run content. It is built because Matthew asked for it, sits on its own branch, and can be switched off with one tick. It uses only Phase 1 guns.

## What changed

- New: `Combat/GunLadder.cs` (the rule, plain C#), `Combat/GunLadderConfig.cs` (dials), `Combat/GunLadderFx.cs` (counts kills, hands over guns, readout, banner), `Tests/EditMode/GunLadderRulesTests.cs` (11 tests).
- `PlayerWeapons.TakeNewGun` and an `Indoors` flag so a gun handed over in the cabin takes the cabin's light.
- `Astronaut` and `TestArena` start the player with `GunLadderFx.StartingGun()`.
- `GameBootstrap` adds the ladder to the game root and resets it with the run; `ProjectSetup` makes the asset.

Constitution: 1 (fun core: every kill now moves you somewhere), 5 (legible risk/reward: the line says exactly how far to the next gun), 3 and 9 (no hidden rules, nothing random, nothing lost by waiting), 2 (agency: a gun you picked up is dropped, not deleted).

## Tested

- 11 new rule tests pass under `dotnet test tools/rules-tests`; whole suite 2,221 passed, 1 skipped.
- All runtime scripts compile against UnityEngine.Modules 2021.3.33 and LethalCompany.GameLibs.Steam (only the known IHittable name clash).

## Not verified (needs Unity on the Deck)

- Nothing has been run in Unity. How the shotgun and sniper look held by the player is unchecked: they use the rifle's place in front of the camera, and the long sniper may fill the screen or clip.
- In the arena a spare rifle lies near the start, so you can skip the ladder there by picking it up (it will be dropped, not lost, when you next climb).
- Whether 22 kills is the right length. With near-constant combat it may be reached in a few minutes.

Playtest protocol (principle 10): problem, kills changed nothing about your kit; hypothesis, a visible ladder makes each kill feel like progress; smallest test, one run on Mars to the rifle; acceptance, every handover is noticed, the line is readable mid-fight on the Deck, no hitch on handover, and the rifle arrives neither in the first minute nor never.

## Agent review

One code read covering the Unity, design and Deck viewpoints (Fable agents are on a usage limit, so Opus). Fixed from it: the ladder deleted guns the player had picked up (now dropped); the readout built strings every frame (now only on a kill); the scene search ran every frame with no player (now twice a second, and a missed handover is retried); the order ended on the sniper, the worst gun to be stuck with in close fights (now ends on the rifle, counts raised from 18 to 22). Left open: the sniper and shotgun view-model place (needs the editor).

Codex review of PR 49 (one P2, checked and real): the handover compared gun kinds, so a same-kind pickup (an enemy's shotgun swapped for the ladder's shotgun) was deleted instead of dropped. Fixed in a follow-up: `PlayerWeapons.IssuedGun` remembers the very gun the run handed out (start guns go in through `EquipIssued`), and only that object is used up. PlayMode regression test `WeaponTests.GunLadder_UsesUpOnlyTheGunItIssued_EvenWhenAPickupIsTheSameKind` (written, not run: needs Unity).
