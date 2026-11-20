# Ante tasks, batch 1 (20 Nov 2026)

**Status: built and checked in the cloud. Nothing here has been run in Unity.** No playtest results exist yet; none are claimed.

Matthew asked for purpose: tasks the player chooses, done their own way, with no menus, always on the move, and every task earning points toward the ante. The research is in "Most fun video game tasks" (project files, `orbital-ante/ante-tasks/`). The plan is the "Ante tasks plan" doc. Matthew chose "Batch 1 first".

## What changed

**The ante loop is on** for solar-system runs (the arena has none).
- Antes are 300 s, with the Hybrid end Matthew chose on 14 Nov.
- The top right shows the ante, the clock, points banked against the target (counted up like a slot machine, with the coin tick), the streak, and "+points" lines naming what earned them.
- Once the target is met, the pause menu shows **Bank this ante now** under Resume.
- If the timer runs out short of the target, the run ends with "TIME'S UP" and what was banked.
- Between antes a simple card says what was banked and what the next ante needs.
- The perk draft and scratch-off are not built yet, so those picks are given up for now.
- The clock stops while paused and while the run-start envelope is on screen.

**Kills** score by kind: tier 1 (Runner, Trooper, Faction A Scout), tier 2 (Marksman, Faction A Line), tier 3 (Bruiser, Faction A Heavy).
- Style feats: ONE SHOT (the first hit landed killed it), LONG SHOT (60 m or more), AIRBORNE, MELEE, FRESH GUN (the first kill this ante with a tool, after a kill with another), TAKEN APART (all four limbs off).
- A rover SPLAT is one feat and nothing stacks on it.
- One feat is +10% and two or more is +20% (the existing skill multiplier).
- Five kills within 8 s of each other start a streak. The next task finished gets one more feat, but only if that raises its bonus.
- One enemy per world is a **marked target**, shown by a red light over its head and worth elite points.

**Four tasks in the world**, each something you see and go to:

| Task | What you do | Points (ante 1) | Feat |
|---|---|---|---|
| Clear a base | Bring down every guard | 250 / 400 / 550 by size | QUIET (no guard ever saw you), QUICK (within 60 s of the first shot) |
| Every base on a world | Clear them all | 400 | none |
| Signal mast (near half the bases) | Shoot out its 3 glowing cells, or ram it with the rover at 15 m/s or more. While it stands it calls every enemy in reach onto you every 45 s, and the screen says so when you come into reach | 180 | BEFORE IT CALLED (ramming is never a feat) |
| Salvage (2 crates a world) | Carry it back into the ship at 0.6 walking speed. Driving it home is allowed | 240 | CARRIED HOME UNSCRATCHED (on foot all the way and never hurt) |
| Supply drop (4 s into an ante, then every 150 s, one at a time) | Race the enemies to where it falls; its landing is loud. Press for half a Focus meter, or hold (once an ante) for three gun-ladder kills | 250 | CONTESTED (an enemy got to it first) |
| First landing on a world | Land | 150 planet, 110 moon | none |
| Hard landing | Land where the ground stands 12 m off its surroundings (not on the pad or a base floor). 3 an ante | 100 | FEATHER SOFT (under 0.7 m/s) |

A tall light stands over each of the three nearest unfinished tasks. The system map shows how many tasks are left on each world you have visited. Every reach is held to a share of the world's size, because worlds are only 40 to 500 m across.

Code:
- Rules: `Scripts/Runtime/Tasks/TaskRules.cs` (plain C#), dials on the `TaskConfig` asset.
- World side: `TaskDirector.cs`; loop and readout: `AnteLoop.cs`.
- Smaller hooks in the enemy spawner, astronaut, pause menu, gun ladder, Focus, rover and system map.
- Stand-in shapes are Unity primitives until models exist.

## Why, and which principles

| Principle | How this serves it |
|---|---|
| 1. Fun core first | Every task is played with the core verbs: shoot, fly, drive, jetpack, sneak, melee. |
| 2. Agency and trade-offs | The player chooses what to do and how. Masts make a fight harder until dealt with. Salvage slows you down. Drops pull you somewhere loud. |
| 3. Learnable challenge, no hidden rules | Each feat is named when it pays, the mast says when it will call, and the base feats are told on the first shot. |
| 5. Legible risk and reward | Every task's points and feats are in the table above. |
| 7. Cognitive load | No menus, three lights at most, one note line. |
| 8. Varied intensity | Quiet base clears, loud drops, masts that escalate. |
| 9. Ethical design | No real-world timers. The streak never takes points away. |

## What was tested, and how

- `dotnet test tools/rules-tests`: 2,580 passed, 0 failed, 1 skipped (a Unity-only file). These include 26 new tests in `Tests/EditMode/TaskRulesTests.cs`:
  - style feats, splats, fresh tools per ante, the streak;
  - base clears and feats, masts (cells, ram, calls, "has called"), salvage, drops and their clock;
  - enemy tiers, hard landings and their cells, claim ids, reaches on small worlds;
  - the skill ceiling for two feats, the hard-landing cap, and mast points.
- The runtime scripts compile against Unity 2021.3 reference libraries with the Unity 6 names mapped back. The only errors are the five known clashes with an unrelated `IHittable` in that library.

## Agent review

- **Unity reviewer:** found 13 problems; all are fixed except the beacons' missing haze. The main ones:
  - A rover ram could never register, because the mast's pole is part of the planet's compound collider. The rover now reports the knock.
  - The clock ran during the envelope.
  - Tasks still appeared with the ante loop turned off.
  - While carrying salvage you could not get into the rover, and Interact could also grab a gun.
  - A drop could open without a press.
  - The "+points" lines overlapped the ship's damage list.
  - The text on the card between antes spilled over its buttons.
  - The bank button showed state left over from an earlier visit.
  - Numbers were formatted for the machine's language.
  - The marked target could be missed, and its light stayed on the body after death.
  - The carried crate covered the view.
  - Landing on a base floor counted as a hard landing.
- **Game designer:** found that several feats were free, the rover dominated, and distances were sized for worlds ten times larger. Fixed:
  - HEADSHOT replaced by ONE SHOT and LONG SHOT (every death is a head, so nearly every kill was a headshot);
  - FRESH GUN once a tool per ante;
  - splats one feat only, rams no feat at 15 m/s;
  - mast feat BEFORE IT CALLED, cells at 70 health;
  - soft landing 0.7 m/s;
  - reaches scaled to each world;
  - the drop gives three ladder kills once an ante, not a whole gun, and half a Focus meter;
  - the streak is spent only when it helps, and its sound plays at five;
  - a world-clear bonus.
  - Left for Matthew: whether targets should rise faster, and whether Select should bank the ante (below).
  - Its rough sum: on foot, ante 1's 850 takes about 3.5 to 5 minutes from the pad, so it is tight but possible. That was worked out on paper, not played.
- **Steam Deck review:** found a likely hitch of about 10 to 40 ms on arriving at a world, from up to about 225 primitives built in one frame. Fixed:
  - views are built one a pass, never in the layout frame;
  - shared meshes, with colliders only where solid;
  - a pool of three lights;
  - views put away beyond 1.1 km;
  - the pod is not solid while falling;
  - no garbage from prompts;
  - distance tests no longer ask about the ground.
  - Its budget lines are in `docs/performance_budget.md`.

## Still unverified (needs Unity on Matthew's Deck)

- Everything on screen: the readout, pop-ups, card, lights, stand-in shapes, prompts.
- Whether the rover ram registers through `MoonRover.OnCollisionEnter`.
- Frame time on arriving at a world, and physics cost of the task colliders on the planet's body.
- The beacons use Unity's plain unlit shader, so they get none of the game's haze, and may shimmer when thin.
- Whether an ante's target can really be met in 300 s, and how it feels.
- Still to be built:
  - batch 2: cave raids, hot tasks, ante twists;
  - batch 3: tasks that depend on the open base, meteor, digging and alien PRs;
  - the upgrade screen, perk draft and scratch-off;
  - enemies getting tougher with the ante.

## Questions for Matthew

- **Should ante targets rise in real effort?** Today each target grows only as fast as the points' own ante multiplier, so ante 4 asks about the same effort as ante 1. Part B wants first-time players dying around ante 3 to 4. Recommended: about 1.6 times the ante 1 effort by ante 4, until enemies scale with the ante.
- **Should Select bank the ante** once the target is met (later opening the upgrade screen, as Part B section 2 says)? Floodlights would keep L only.

## Playtest steps (on the Deck)

1. In a terminal: `cd ~/Orbital-ante`, `git fetch`, `git checkout claude/playtest`, `git pull`.
2. Open the project in Unity Hub. Let it import, then press Play and start a solar-system run.
3. Look at the top right: you should see "ANTE 1 5:00" and "0 / 850".
4. Leave the ship and walk to the nearest tall light. Clear that base. Note which feats appeared.
5. If a mast is near, wait for the "in range" line, then shoot out its three cells.
6. When "SUPPLY DROP INCOMING" shows, go to it. Try both a press and a hold.
7. Pick up a salvage crate and carry it back up the lift.
8. Lower the rover and ram a mast at full speed.
9. Bank the ante from the pause menu once the target is met, or let the clock run out.
10. Note how long ante 1 took and which tasks you chose, then send that back.
