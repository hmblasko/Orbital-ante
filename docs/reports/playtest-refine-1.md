# Playtest and refine, pass 1 (24 Nov 2026)

Matthew, 24 Nov 2026: "Play test the game and make refinements".

There is no Unity in the cloud workspace, so this pass is a code-level playthrough of the full loop on `claude/playtest`, a compile check and the rule tests. **Nothing here was run in Unity.** No playtest results are claimed; the checklist at the end is how to get real ones on the Deck.

## What is in this branch

`claude/playtest-refine` = `claude/playtest` + the five PRs that were waiting to merge into it + this pass's fixes:

| PR | What it brings |
| --- | --- |
| 78 (includes 79) | Controller aim assist, ante loop fixes (dead player's clock, star chart only on foot), shop on Select out on a world |
| 80 | Debug statistics hidden during play |
| 81 | Base-clearing objective removed (Matthew, 21 Nov) |
| 82 | Handmade feel pass 1 (bedded rocks, quieter grade, varied repeat sounds) |

They merged cleanly except PR 81 against 78/79 in `TaskDirector.cs` (the base-clear alarm hook that 79 had touched is gone with the base task) and `CLAUDE.md` (both sides' notes kept).

## What was walked through in code

Run start (title, envelope, goal card, clock waiting for liftoff), seat and cabin, lift, outside, combat and kills scoring, the ante readout, target met, field upgrades and star chart, the clock running out (overtime), the ante break (perk, shop, ticket, stakes, start), victory and Keep going, death (burst, hold, death page), restart with the same seed and a new one, quit to menu. Also: static state reset between runs, event subscriptions paired with unsubscriptions, UI placement on the 1280 x 800 canvas, input bindings per map.

The loop holds together: every run-scoped system is reset in `GameBootstrap.DestroyWorld` / `AnteLoop.BeginRun`, every static event has a matching unsubscribe, the death page and the break cannot be backed out of into a dead or paused world, and nothing can be spent while dying.

## Fixed

1. **The ship's damage list sat on top of the ante readout outside.** Out on foot the top right corner holds the ante line, score, bar, streak, world line, PERK READY and up to four "+points" lines (down to about -226), and the ship's list of hurt parts started at -110 in the same corner, so after any hard landing the two overlapped for the rest of the fight. Now (`ShipHud.PlaceList`): aboard it stays at -110 (the "+points" lines and world line already step aside there); outside or driving it starts at -250, under the readout, and it is hidden more than 45 m from the ship, where it is only clutter. Constitution 7 (cognitive load).
2. **Dead code left by removing base clears.** `TaskDirector.SystemAlarm` and `_raising` only protected the base's QUIET clear, which PR 81 removed; overtime's hunt no longer sets it. Two stale tooltips on `TaskConfig` still named base clears.

## Found, not changed (other threads or needs Matthew)

- **PR 72 (seat display redesign)** moves the damage list onto a plate pinned at -18 in the top right, which is where the ante readout's first line is (-16). When PR 72 is merged it needs to start under the readout, or the readout needs to move. This branch's change to the same lines will conflict with PR 72 and is the one to keep for outside.
- **First Deck build (not the editor):** several materials are made with `Shader.Find` on built-in shaders (`Universal Render Pipeline/Lit`, `Unlit`, the particle shaders, `Shader Graphs/Decal`). A player build strips shaders nothing references, and `Shader.Find` then returns null (pink or invisible). Playing in the editor is unaffected. Before the first standalone build, `ProjectSetup` should add them to Always Included Shaders.

## Tested

- Runtime compile check against Unity reference assemblies: clean (only the two known `excludeLayers` lines, which exist in Unity 6 but not in the 2021 reference).
- Rule tests: 2,675 passed, 1 skipped (the cabin bake check, skipped as before).
- Not run in Unity: everything about how it looks and feels, including the two fixes.

## Playtest checklist for the Deck (about 20 minutes)

Get the build first:

1. Open Konsole in Desktop Mode.
2. `cd ~/Orbital-ante`
3. `git fetch`
4. `git checkout claude/playtest`
5. `git pull`
6. Open the project in Unity Hub and press Play.

Then, noting anything odd (a screenshot or the first red line in the Console is ideal):

1. Title screen: New run. Does the envelope show, then the goal card (first run only)?
2. On the pad the readout says STARTS AT LIFTOFF. Lift off: does the clock start and say CLOCK STARTED?
3. Fly to a world with the map and autopilot. Land hard once on purpose so a part is damaged.
4. Walk outside. The list of hurt parts should now sit under the ante readout, not on top of it, and vanish when you walk well away from the ship.
5. Fight. Do kills show "+points" lines top right and count up? Does the hit ding play?
6. Meet the target. Does PERK READY show? Press Select (Tab): does the upgrades page open, and the star chart from it?
7. Let the clock run out on a later ante without meeting the target: does OVERTIME show and do enemies come looking for you?
8. At the break, try each row: pick a perk, shop, scratch the ticket, raise the stakes, start the next ante.
9. Die. Does the body burst, then the death page with the recap? Try again with the same seed: is everything back to the start (pistol, full Focus, ante 1, no perks)?
10. Quit to the main menu, then Practice range, then quit and start a New run: do enemies shoot in the new run?
