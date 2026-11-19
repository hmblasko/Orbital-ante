# Alien creatures: the Strider pilot (19 Nov 2026)

Matthew asked on 17 Nov 2026 for alien assets "that look realistic (not ps2 style)" drawn from "the most unusual bugs insects and animal creatures on the planet", with "interesting animations", that "gib and lose limbs", and named nine animals. On 19 Nov he chose to have those nine built first, starting with one pilot. This is the pilot: the **Strider** (placeholder name), from the Japanese spider crab. The list and build order are in `docs/briefs/alien-creatures.md`.

**Nothing here has been run in Unity.** The pictures are Blender's, drawn from the game's model and the game's own movement rules; they are not Unity screenshots.

## What it is

A crab about 7 m across its legs, carrying a spiny body the size of a table 2 m off the ground on eight stilt legs, with two long claws in front. Pictures: `docs/art/previews/creatures/` (whole, body, underside, legs lost, far copy, pieces).

- **It comes apart.** Ten limbs of three pieces each. Any piece that breaks comes off with everything beyond it, as one loose chain, and leaves a wound on both sides of the break. Leg pieces take 70, 55 and 35 damage, claw pieces 70 each (the rifle does 24: one to three hits).
- **It dies one way.** Only the body kills it: the soft underside (240) or the armoured lid on top (360). Then it bursts: every limb left is thrown off, the lid and the belly fly apart and two lumps of meat come out.
- **Losing legs changes how it moves.** It stands while five legs carry it with at least two a side, slower and lower with each one lost, leaning over the legs it has left. Below that it drops onto its belly and drags itself. A leg that has only lost its tip still carries weight on the stump.
- **One attack, with a warning.** It walks up, stops at arm's length and throws the claw on your side. The raise is the warning; the place the claw will land is fixed a quarter of the way through the 1.6 s swing, and it lands 0.6 s later, so stepping aside after it commits is a miss. 34 damage. Shoot a claw off in mid swing and the swing ends. It backs away if you get underneath it.
- **With both claws gone it cannot hurt you**, and it backs off to 32 m. (See "For Matthew to decide".)

## How it moves (no animation clips)

The walk is worked out every frame from where it is going: each foot stays planted on the spot it stepped on until it falls too far behind, then swings to a new one, and neighbours never lift together. Legs are bent to reach their feet; the body rides on springs, sways, sinks when legs are in the air and flinches when hit. Because nothing is a recording, it copes with any set of legs missing. Clips of the walk, a strike and losing six limbs are in the project's files (`alien-creatures/`).

## What changed

- `Scripts/Runtime/Creatures/`: `StriderLayout`, `StriderBody`, `StriderAnimator`, `StriderBrain` (plain C# rules) and `Strider`, `CreatureConfig` (Unity side).
- `tools/blender/build_creatures.py` (the model, textured at 2048, and its far copy), living materials in `oa_materials.py`, `tools/blender/creature_dump` (draws the game's animator outside Unity).
- `Resources/Models/Strider.bytes`, `StriderFar.bytes`, `Textures/Strider_*.png`.
- The combat arena starts with one Strider at the far end (`CreatureConfig.arenaStriders`).
- `ModelDetail.LetGo` (so a burst creature's pieces go on being watched for distance).

## Constitution

- **1, fun core:** a big target whose legs and claws each come off with their own result.
- **2, agency:** disarm it first, cripple it first, or go straight for the belly; each has a cost.
- **3, learnable challenge:** limbs are low; the armoured lid knocks and never bleeds, so the slow way says it is slow; the claw's landing place is committed in view.
- **5, legible risk:** the raise can be seen and heard (it borrows a sound until it has its own).

## Tested

- 15 rule tests in `StriderRulesTests` (body rules, stance, walking with feet planted in the world, losing legs, the strike's warning and lock, wandering by seed, the model against the layout, the far copy). All pass; the whole suite is 2,565 passed, 1 skipped.
- The Unity-side code compiles against Unity reference assemblies (older ones, with Unity 6 names mapped back).
- The model: 7,998 triangles in the file, 16,212 as drawn (a leg is drawn eight times), 4,285 as the far copy; seen from inside 0.00%.

## Agent review

- **unity-reviewer** found: a Strider down on its belly could never move (its own blocking check hit the floor); a piece shot off could still count as the animal for the rest of that frame (shotgun pellets); pieces thrown at death were not handed on for distance swapping; pieces of one burst would shove each other apart; steps over a shin high would snap the body up. All fixed. It also noted melee and the rover cannot hurt it and that it has no solid body: both true, listed below.
- **game-designer** found: four rifle hits disarmed it for good (claw pieces raised from 35 to 70); a safe spot under the body (it now backs off); the lid was inflated health with the same feedback as the belly (600 lowered to 360, and it now knocks instead of bleeding); the claw could hurt while visibly short (range 4.6 to 4.0, radius 1.0 to 0.8); and that fleeing when clawless is a poor outcome (left for Matthew).
- **deck-performance** found no garbage per frame; asked for no real-time shadows (done), a cap of 3 alive at once and fewer loose pieces (20). Its budget line is in `docs/performance_budget.md`.

## Not verified, and not built

- Not run in Unity: whether it appears, whether its legs bend the right way there, whether hitboxes fit, how the burst scatters, frame cost.
- It walks on level ground only. Its feet are not yet placed on slopes, so it is in the arena only, not on the planets.
- Melee weapons and the rover cannot hurt it yet. You can walk through its legs.
- A loose chain of limb pieces cannot be shot into smaller pieces (the mannequins' can).
- It has no sounds of its own; it borrows the gore pops, the armour knock and one raise sound.
- With the Down stance the belly is against the ground, so the lid is the only way in; whether that feels fair needs play.

## For Matthew to decide

1. Is this the level of realism you want for the other eight?
2. A Strider with both claws shot off cannot hurt you and backs away. Should it get a second, weaker attack (a stab with a leg) instead?

## Seeing it on the Deck

1. In Desktop Mode open a terminal in `~/Orbital-ante` and run `git fetch`, `git checkout claude/playtest`, `git pull` (after this work is merged there).
2. Open the project in Unity and wait for it to import (the three new pictures take a minute the first time).
3. Press Play, choose **Combat test arena**.
4. Walk forward: the Strider is at the far end. It only wanders until you press **H** (D-pad down), which starts the fight.
5. Try: a leg, a claw in mid swing, the underside, and the top. Press F3 for the counts.
6. If nothing appears, look in the Console for a line starting "Orbital Ante: the Strider's model" and send it to me.
