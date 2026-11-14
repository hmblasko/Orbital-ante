# Phase 1 sign-off checklist

For Matthew, on the Steam Deck. This is the gate in the build prompt (section 15): Phase 2 does not start until you say Phase 1 is good. **Nothing below has been ticked by anyone. Claude cannot run Unity; every line is waiting on you.**

How to use it: play, and for each line write **yes**, **no** or **not sure**, with a few words when it is not a yes. "It works but it is not fun" is a **no**. Send the file back, a photo of it, or just the numbers of the lines that failed.

## Before you start

1. In Desktop Mode open a terminal in `~/Orbital-ante`.
2. Type `git fetch`, then `git checkout claude/playtest`, then `git pull`.
3. Open the project in Unity Hub and wait for it to finish importing.
4. In the top menu choose **Orbital Ante > Set Up Project** (it is safe to run every time).
5. Press **Play**. Press **F3** for the readout with frame rate and the "rescues" number.
6. Controls are in `docs/SETUP.md` and under **Pause > Controls**.

If Unity shows red errors in the Console before you can press Play, stop and send the first red line. Nothing else on this list matters until that is fixed.

## A. It runs

| # | Check | Yes / no / not sure |
|---|---|---|
| A1 | The project opens and plays with no red errors in the Console | |
| A2 | The frame rate on F3 stays near 60 on the pad, in flight and on foot | |
| A3 | The frame rate holds in a fight with a full camp | |
| A4 | The pause menu opens, rebinding a key works, and the new key is still there after quitting and playing again | |
| A5 | Restart (same seed) puts the same worlds, camps and bases in the same places | |

## B. Solar system (Milestone 5)

| # | Check | |
|---|---|---|
| B1 | Planets and moons visibly move; the map shows it live | |
| B2 | Flying between worlds is a pleasure and never drags | |
| B3 | Each world's gravity feels different and playable | |

## C. Planets (Milestone 6)

| # | Check | |
|---|---|---|
| C1 | Every world is recognisable at a glance from space | |
| C2 | Landing anywhere gives you something you want to walk to | |
| C3 | A gas giant can be flown into and has ground inside; its sky changes | |
| C4 | Neptune reads as an ocean world | |
| C5 | Weather shows on the worlds that have it | |
| C6 | Nothing looks cheap or like a PS2 game | |

## D. Ship (Milestone 7)

| # | Check | |
|---|---|---|
| D1 | The cabin feels like a cosy little spacecraft you want to protect | |
| D2 | The legs are long enough and the ship stands properly on them | |
| D3 | The hatch opens by looking at it and pressing E, every time, and cannot be broken | |
| D4 | The lift takes you in and out without glitches | |
| D5 | The seat belt goes on and off cleanly | |
| D6 | Damage shows, parts can be mended, glass breaks and can be mended | |
| D7 | A broken ship does not end the game; only your own death does | |

## E. Launch (Milestone 8)

| # | Check | |
|---|---|---|
| E1 | The ship sits still on the pad until you fly it | |
| E2 | Takeoff gives a little rush: smoke, grit, light, shake, sound | |
| E3 | It still does on the fifth launch | |

## F. Cockpit (Milestone 9)

| # | Check | |
|---|---|---|
| F1 | Speed, fuel, air and direction can be read at a glance in flight | |
| F2 | The console and dash look great and are never in the way | |
| F3 | The mouse looks around the cabin; holding the right mouse button turns the ship | |
| F4 | The three ship cameras work and are worth using | |

## G. Ship navigation (Milestone 10)

| # | Check | |
|---|---|---|
| G1 | Flying is learnable and satisfying | |
| G2 | Lock-on works, and works while the autopilot is flying | |
| G3 | The autopilot gets you to a world and lets go sensibly | |
| G4 | Match velocity makes landing on a spinning moon an achievement, not a fight | |
| G5 | The ship never falls through the ground when landing. Write down the "rescues" number on F3 after ten landings | |
| G6 | Crashes bounce and break parts; unbuckled, a bad one kills you; buckled, it does not | |
| G7 | The thruster sound changes with the direction you push | |

## H. Jetpack (Milestone 11)

| # | Check | |
|---|---|---|
| H1 | You can leave the hatch in space and float | |
| H2 | Floating round the ship feels free, precise and a little scary | |
| H3 | Match velocity brings you to rest against the ship or the ground | |
| H4 | The tether stops you drifting off without pulling you in | |
| H5 | You can see and hear which jets are firing | |
| H6 | You can leave a moving ship and mend it | |

## I. Combat and gibbing (Milestones 1 to 3 and 12)

| # | Check | |
|---|---|---|
| I1 | Enemies stand, walk and aim properly on slopes and anywhere on a world | |
| I2 | You can tell the four types apart at a glance | |
| I3 | Each type fights differently: the Runner rushes, the Bruiser walks you down, the Marksman stands off | |
| I4 | A red line shows for a second before every Marksman shot, and that feels fair | |
| I5 | Limbs come off in one to three hits, with blood and a pop, on every type | |
| I6 | Shooting a gun or an arm disarms; shooting a leg drops them | |
| I7 | Gibs fly convincingly (and funnily) on a heavy world, a light moon and in space | |
| I8 | There is nearly always a fight close by, and it never feels unfair or swarming | |
| I9 | A cleared camp stays cleared; a half-cleared one has only the survivors when you come back | |
| I10 | Every fight produces a moment worth laughing about | |
| I11 | Being shot shows clearly, and dying outside tells you why | |

## J. Added on 14 Nov 2026 at your request

| # | Check | |
|---|---|---|
| J1 | Deep Breath (hold Q, or click the right stick) slows the world and your aim stays quick | |
| J2 | It feels limited: the bar runs out, and a kill buys a little back | |
| J3 | Holding the right stick click while aiming is comfortable on the Deck (if not, say what button you would rather) | |
| J4 | Bases can be found on every world; guards stand on them | |
| J5 | A bigger base feels like a different, harder fight | |
| J6 | The base buildings look good, and guards move sensibly round them; a lookout stays up on its tower | |
| J7 | Earth has more landscapes, trees and mountains, and they look right | |

## Known gaps, so they do not surprise you

- Base buildings have no insides and no doors that open yet.
- Heat shimmer at launch is not built.
- Saved key bindings not loading back after a restart was reported earlier and is not yet fixed (line A4 checks it).
- The ante timer, points, upgrades, loot, factions and the rest of Phase 2 are not in the game, on purpose.

## The decision

When you have been through it: **Phase 1 approved**, or the list of lines to fix first. Phase 2 (first faction, then the ante and point system, which you asked to have next) starts only on your word.
