# Alien creatures: the Roller (19 Nov 2026)

The third of Matthew's nine (his go-ahead of 19 Nov 2026: "Continue to build these, spider crab looks good"): the **Roller** (placeholder name), from the giant isopod. It is the first creature that is not a walker, so it has its own rules.

**Nothing here has been run in Unity.** The pictures and clips are Blender's, drawn from the game's model and the game's own movement rules; they are not Unity screenshots.

## What it is

A deep-sea woodlouse 2.4 m long and a metre wide, waist high, in seven overlapping lilac-grey armour plates, with two large faceted eyes, long feelers, a flat tail fan and fourteen legs. Pictures: `docs/art/previews/creatures/roller_*.jpg`.

- **One loop, all of it in view.** On foot it is slow (2.6 m/s; you walk at 5). With 5 to 22 m of room it stops and curls up over 0.9 s. While its head and tail are only dipping it is still taking aim; the moment it snaps shut it has stopped following you. Then it bowls in a straight line at 11 m/s (faster than a sprint) to 5 m past where you stood. 30 damage. A walk to the side clears it from any distance (rule tests at 6, 10 and 15 m).
- **It pays for a miss.** At the end of the roll it falls open on its back, soft side up, legs in the air, for about 2.5 s in all before it has rocked back onto its feet. That is the time to shoot it. Rolled into a wall it lies there about 4.6 s, so luring it into something solid is the clever play.
- **Armour that says it is armour.** A plate takes three rifle hits, knocks instead of bleeding, then flies off and leaves that segment bare for good. With three plates gone it can no longer close into a ball: it turns its bare back and makes off, and butts (12 damage) only someone who crowds it.
- **What comes off:** seven plates (72 each), fourteen legs (30), the tail fan (70). Each leg lost slows it on foot and keeps it a little longer on its back; without the tail it lies on its back over half as long again.
- **It dies two ways:** the soft body under the plates (200), reached on its back or where a plate is gone, or the armoured head (300), the slow way. Then it comes apart at every joint: each segment goes its own way with its legs still on.
- Nearer than 5 m it backs off to make room; within 1.9 m it butts.

## How it moves (no animation clips)

Its body is a chain of nine links bent at eight joints: flat when it walks, a closed ring when it is a ball. The ball turns by exactly the distance it rolls, so it never slides. It gets up by half curling and rocking on round. Legs step in a wave that runs up the body, fold flat inside the ball, and wave in the air on its back. Clips: walk, the whole roll, and coming apart, in the project files.

## What changed

- `Scripts/Runtime/Creatures/`: `RollerLayout`, `RollerBody`, `RollerAnimator`, `RollerBrain` (plain C#), `Roller` (Unity side); `CreatureConfig.roller`, `arenaRollers`.
- `tools/blender/build_creatures.py`: the Roller, textured at 2048, and its far copy. `tools/blender/creature_dump`: the Roller's scenes.
- `Resources/Models/Roller.bytes`, `RollerFar.bytes`, `Textures/Roller_*.png`.
- The combat arena starts with one Roller part way down, to the right.
- Loose creature pieces (all kinds) stop being physics objects when they start to fade.

## Constitution

- **1, fun core:** dodge, then punish. A different verb from the Strider (aim) and the Sprinter (move).
- **2, agency:** wait for the roll and shoot the belly, strip plates to stop it rolling at all, or lure it into a wall.
- **3, learnable challenge:** the warning, the moment it takes aim, the straight line and the price of a miss are all visible; nothing is random.
- **5, legible risk:** armour knocks, flesh bleeds; the head is plainly the slow way.
- **8, varied intensity:** a rush, then a lull that belongs to the player.

## Tested

- 19 rule tests in `RollerRulesTests`: what comes off and what kills; the chain level on its feet, a closed ball when curled, belly up on its back; the ball turning by what it rolls; the warning, the lock and the straight line; a walk aside missing from three distances; time on its back, longer after a wall, falling open away from the wall; backing off, butting, making off with too few plates; the model against the layout. Whole suite: 2,593 passed, 1 skipped.
- The Unity-side code compiles against Unity reference assemblies.
- The model: 2,152 triangles in the file, 6,228 as drawn, 3,746 as the far copy; seen from inside 0.00%.

## Agent review

- **unity-reviewer** found: a Roller stopped by a wall opened out into the wall (it now falls open back the way it came); a long frame could carry the ball past thin cover or past the player (frames are capped at a twentieth of a second for it); an attack under way still hurt while enemies were told to hold fire (fixed). It also noted the model was missing at the time (now in), and that walking against an obstacle may stutter, as the walkers' does (left, noted below).
- **game-designer** found: the head (170) was a quicker kill than the soft body, so the roll would rarely be seen (now 300); plates took four hits where the brief says limbs take one to three (now 72, three hits); too long on its back made one dodge a certain kill (2.2 s down to 1.5 s, a wall still 3.6 s); nothing showed the moment it takes aim (it now only snaps shut at that moment); a Roller without plates was harmless and dull (it now makes off); the leg rule had a hidden cliff (now a little for each leg); it should start its roll from further off so the roll is seen (16 m to 22 m).
- **deck-performance** found no garbage per frame; asked for no mirrored legs (done: a leg is the same either side), one collider per loose segment (done), fading pieces to stop being physics objects (done, for every creature), and at most two Rollers until measured (done). Its budget line is in `docs/performance_budget.md`.

## Not verified, and not built

- Not run in Unity: whether it appears, whether the hitboxes sit on the plates while curled, how the roll feels at speed, whether 1.5 s on its back is enough with ordinary aim, frame cost.
- The legs are simple: one curved piece each. They read as legs under the body; close up they are plainer than the Sprinter's.
- Nothing yet shows that the tail is what it levers itself over with; the longer time on its back is real but unexplained.
- Level ground only, arena only. Melee and the rover cannot hurt it. No sounds of its own (it borrows the armour knock and a warning sound).
- The ball does not knock the player back; it only hurts.
- Walking into an obstacle it may stutter (the walkers do the same).

## Seeing it on the Deck

1. In Desktop Mode open a terminal in `~/Orbital-ante` and run `git fetch`, `git checkout claude/playtest`, `git pull` (after PR 70 is merged there).
2. Open the project in Unity and wait for it to import.
3. Press Play, choose **Combat test arena**.
4. The Roller is part way down the arena on the right. Press **H** (D-pad down) to start the fight.
5. Try: stand still and take the roll once; then step aside as it snaps shut and shoot the belly while it is on its back; get it to roll into a wall; shoot three plates off and watch it make off.
6. If nothing appears, look in the Console for a line starting "Orbital Ante: the Roller's model" and send it to me.
