# Clear goals: start, goal card, clock at liftoff, tips, title screen (21 Nov 2026)

Matthew asked (21 Nov 2026): "Prepare the game into a fully functioning game with clear goals". Plan: Claude Doc "Complete playable loop: audit and plan". This report covers batch B (start and goals). Batch A (merging PRs 73, 75, 74 into playtest) waits on Matthew's yes; batch C's first two pieces (what a missed target does, after the last ante) are below; the fuller recap is with the roguelite thread.

**Nothing here has been run in Unity.** It compiles against reference assemblies and the rule tests pass in the cloud.

## What changed, and why

| Built | Problem it answers | Constitution |
|---|---|---|
| Title screen: New run first, then the last seed, How to play, Records, Collection, Practice range (the old combat arena), Extras (audio showcase, show tips again, build label), Quit. Your best ante and score under the title. | The title was a developer menu: "working title", the build label, test arena and audio showcase next to Play | 1, 7 |
| How to play page: the goal in plain words, the three flying keys, how limbs and deaths work | A new player had nowhere to find out what the game is | 5, 7 |
| Records page: best ante, best score, antes cleared, runs finished, time played | Records were only seen on the death page | 6 |
| Goal card on a profile's first run: "Each ante, bank 850 points before its 5 minute clock runs out..." with Let's go, or Let's go and no more tips | Nothing told a new player what to do | 5, 7 |
| Ante 1's clock waits on the pad until liftoff (25 m up), stepping outside, the first point, or 1 minute. A run that doesn't start on the pad starts its clock at once. The readout says STARTS AT LIFTOFF, then CLOCK STARTED. | Buckling in, the build-up and the climb were eating the first ante | 3, 5 |
| One-time tips, low in the middle, one at a time: open the map and lock a world for the autopilot (only shown strapped into the seat), what scores here, first points, target met, one minute left. Each shows once per profile. Off switch: pause > Feel > Tips for new players. | The map, lock and autopilot are how you find points, and nothing pointed to them | 7 |
| A line under the ante readout: what is left on the world you are at ("MARS: 2 BASES · 1 MAST"), or "ALL DONE. FLY TO ANOTHER WORLD". Hidden aboard the ship, where the damage list uses that corner (the map shows tasks per world). | The goal was only in the map | 5, 7 |
| Fix: the run-start envelope now holds the world and the controls when a run is started from the title screen (before, the ante clock and enemies ran on behind it, and the controls were live). | Found in review | 3 |

## Choices I made (say if you want any changed)

- The clock starts at the first point even on the pad, so fighting round the pad can't be done on a frozen clock.
- The goal card shows only on a profile's first run. How to play on the title screen has the same words.
- Tips are stored in the game's settings (like the Feel toggles), not the save file, so the save version is unchanged.

## Tests

- `dotnet test tools/rules-tests`: 2,626 passed, 1 skipped (the cabin bake check that needs Unity's files).
- New `Tests/EditMode/RunGuideTests.cs` (14 tests): the hold's four ways to end and its reset; tips in order, once each, nothing on the pad, most urgent first, none between antes; never opening the map doesn't block other tips; map tips only at the seat; a run off the pad starts at once; every tip has words and its keys; the goal card names target, clock, antes and death; seen tips survive storage; the world line.
- Compile check of the whole Runtime folder against reference assemblies: only the two known `excludeLayers` errors. (Checked that this check does catch errors inside method bodies.)

## Agent review

One code read covering the Unity, design and Deck viewpoints (Opus). All taken:
- **Unity:** the hold let go on the first frame (the launch effects report "very high" before they first look down, and ran after the guide): the guide now runs after them and only trusts a real reading. Orbit starts held for the whole cap: now start at once. The goal card could open over the run-start envelope: it waits for it. "Show the tips again" only worked after a restart: fixed. The title card was taller than 800 px: buttons 48 px, footer dropped. How to play could spill onto Back: smaller text, taller box. The world line overlapped the ship's damage list: hidden aboard. Back on a title page now returns to the button that opened it.
- **Design:** "bank" meant both earning points and ending the ante early: the goal card and tips now say "score", and the target-met tip says the ante clears by itself when the clock runs out. Map tips showed on foot: now only strapped in. The wait's cap cut from 2 minutes to 1. "Nine antes" now comes from the balance.
- **Steam Deck:** the world line built strings twice a second: now only when a count changes. The tip check walked task lists every frame: it reads the line's counts and stops once every tip is seen. Seen tips were written to disk mid-flight: now at the end of the run.
- Found on the way (older): from the title screen the run-start envelope did not hold the world or the controls. Fixed.

## Unverified (needs Unity)

Everything seen or felt: the title pages and goal card on the Deck's screen and with a controller, whether the readout line and tips overlap anything, the moment the clock starts after liftoff, key names in the tips.

## Playtest on the Steam Deck (once this is on the testing branch)

1. In Desktop Mode open Konsole and type `cd ~/Orbital-ante`, then Enter.
2. Type `git fetch`, Enter; `git checkout claude/playtest`, Enter; `git pull`, Enter.
3. Open the project in Unity Hub and wait for it to finish compiling.
4. Press Play. On the title screen check New run is selected, then open How to play and Records and come back with B.
5. To see the first-run card even if you've played before: Extras > Show the new-player tips again, then Back.
6. Choose New run. Read the goal card and press Let's go.
7. Check the top right says ANTE 1 5:00 STARTS AT LIFTOFF and the clock does not move.
8. Hold the lift-off key. Once you are clear of the pad the note should say CLOCK STARTED: LIFTOFF and the clock should start counting down.
9. Watch for the tip about the map, open the map, then watch for the lock and autopilot tip.
10. Fly to any world. Check the line under the score says what is left there.
11. Tell me what felt clear, what didn't, and anything that overlapped or didn't show up.

## Batch C: overtime and Keep going (21 Nov 2026)

Matthew chose "Overtime until you hit it" on the decision card, and said perks continue even in overtime.

| Built | Problem it answers | Constitution |
|---|---|---|
| When the clock runs out short of the target the run goes on in overtime: the readout says ANTE n OVERTIME +m:ss in red, an alarm sounds once with OVERTIME: REACH N. THEY'RE HUNTING YOU, and every 15 s the enemies near you are told where you are. The ante clears the moment you reach the target, and the break says "Made it in overtime. Close one!" The perk pick is still offered. | A missed clock ended the run, which went against "the game ends only on player death" (13 Nov) | 3, 5, 8 |
| In overtime enemies react and aim as sharply as under Hunted, but no extra heavies (an enemy keeps the kind it woke as for the whole run, so heavies would have stayed for every later ante). | Found in review | 3 |
| After the last ante: a page with Keep going (endless antes, each target 1.25 times the one before, rounded to 10) or End the run here. Records are written only when the run really ends. | Ante 9 just stopped | 6 |
| Saved runs keep Keep going and overtime. A long-shot kill's "+time" is not shown in overtime (there is no clock to add to). | Found in review | 3, 5 |
| The old rule (time's up ends the run) is kept as a dial: `AnteBalance.missedTarget = EndRun`. | So it can be compared | 10 |

Tests: 2,634 passed, 1 skipped. New tests: into overtime in every end mode, clears at the target with the perk still offered, nothing carried over, pause and death in overtime, the last ante cleared in overtime is the win, Keep going's targets, endless targets keep the listed ones, and Keep going plus overtime survive a save round trip. Compile check clean.

Agent review (one Opus read, Unity, design and Deck): heavies woken in overtime would last all run (fixed: sharpness only); the long-shot "+time" popped in overtime but added nothing (fixed); saved runs dropped Keep going and overtime (fixed, with a test); the alarm's reach comment was wrong (fixed); the overtime readout and alarms could run after death (fixed); Keep going played the break's fanfare on top of its own sound (fixed); one weak test assertion (fixed).

Unverified (needs Unity): how overtime feels, whether enemies really come to the alarm in time, the red readout on the Deck's screen.

Extra Deck steps: 12. In a run, let ante 1's clock run out without reaching the target. Check the readout turns red and counts up, and enemies start coming. 13. Reach the target and check the break opens with the perk pick. 14. (Long) clear the last ante and press Keep going: the next ante's target should be bigger.
