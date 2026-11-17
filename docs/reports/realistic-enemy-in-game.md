# The realistic trooper goes into the game

Status: **in the game's files, not run in Unity.** Everything below about how it looks in the game, and what it costs on the Deck, is unverified until Matthew has run it.

## What Matthew decided (14 Nov 2026)

Shown the before and after of the common enemy (`docs/reports/realistic-look-preview.md`), he wrote "Looks better" and chose **"Yes, this level"** on the decision card. The other two choices were textured models from a 3D generator, and "not this far".

## What changed

1. **The common enemy is the realistic trooper.** `Resources/Models/Enemy.bytes` and its far copy `EnemyFar.bytes` are now written by `tools/blender/build_enemy_realistic.py`. The first enemy model is no longer in the game; `tools/blender/build_enemy.py -- --classic <folder>` can still write it somewhere else for comparison.
2. **Three small changes to the model so it fits the game's rules.** The aerial is gone (the torso piece may not be more than 0.75 m top to bottom). The gloves are 7% bulkier, so they fill the hand's hitbox. There is no holstered pistol. **No hitbox changed.**
3. **It is the first model that shines.** Its file says what each surface is made of, so its visor, metal rings and painted shell take the new highlight and sky reflection. Cloth, webbing and rubber do not.
4. **Fewer are drawn in full at once, and not as far out.** The trooper has 21,176 triangles where the first model had 14,848. So the game now limits the full bodies by triangles (180,000 between them) as well as by number (12), counting only the pieces a body still holds. Eight troopers are drawn in full at once where it was twelve; any more wear the far copy (4,495 triangles). And because its triangles are smaller, the trooper goes over to its far copy at 20.9 m instead of 24 (the other kinds keep their ranges). The triangles of a big fight stay within about 5% of what they were. New dial: `Full Triangles` on the Look asset (20,000 to 600,000). F3 shows it.
5. **A way out.** If the new model shader fails on a machine, setting `Shine` to 0 on the Look asset puts models back on the ground's shader at the next Play. In the editor, a model shader with compile errors is now also noticed by itself and replaced, with a warning.
6. **A fix to the model writer.** A far copy written straight after its full model had its sockets named "Head.001" and so on. The names are now clean. The rule test for far copies would have caught it; it was found by that test on a trial file.

## What is still in the earlier style

The Runner, the Bruiser and the Marksman, all five guns, the lander and the cabin. **The game will show both styles side by side until they are redone.** The trooper is the enemy met most, so it comes first; the other three kinds are next.

## Why (constitution)

- It follows the director's instruction of 14 Nov and his choice on the card. No rule of play changes.
- **Principle 3 (learnable challenge) and 5 (legible risk):** what looks hit must be hit. The hitboxes are the game's and did not move; the model was made to fill them (rule test `EnemyModelFile_ItsHitboxesFitIt`). The dark hands, feet and joint bands, the wider red bands and the dark back of the helmet are meant to make limbs and the head findable (`docs/art_direction.md`, "Reading a fight"). The only evidence is the builder's own sheet. It showed the head findable to 24 m, a lost lower leg readable to about 10 m, a lost forearm not readable even at 5 m without the wound, and the red bands nearly gone at dusk. Nobody has seen it in the game with the wound, the gun or the haze.
- **Risk to the tone:** the build prompt wants TF2-style humour in the gibs. A realistic soldier losing limbs may read as grim rather than comic. Nothing about the gibs, sounds or impulses changed here, so if it is not funny, those are where to tune it. If that is not enough, it is a call for Matthew.
- **Principle 10 (evidence):**
  - Problem: the enemy read as cartoony, goofy and blocky.
  - Hypothesis: this model reads as realistic and still reads in a fight, at no more cost than the worst frame had before.
  - Smallest test: this one kind in the game beside the three old ones.
  - Acceptance: Matthew's checks below. It is taken out again (restore `Enemy.bytes` and `EnemyFar.bytes` together; they must never be split) if, in steps 6 and 7, Matthew cannot say without a second look which part came off (forearm, whole arm, leg, head) at 5 to 10 m, or cannot find the head from behind at 24 m, or sees sparkle or flicker in step 5; or if a Deck build (not the Editor) shows **OVER** on the F3 Budget line in a fight with more than eight troopers near, with `Full Triangles` at 180,000, and still shows OVER at 120,000. All of these ranges and numbers are first guesses.
  - Playtest protocol: the nine steps in `docs/reports/realistic-look-preview.md` when there is time for them; the ten-minute version is below.

## What was tested (outside Unity)

- The C# compiles against reference assemblies. The rule tests pass on the real files: **1,965 passed, 5 skipped, 0 failed.** New: three for the triangle limit and the shorter range of a finer body (`DetailRuleTests`), and the model tests now hold the trooper to 22,000 triangles, the other kinds to 16,000, and every body to "eight fit in full at once".
- The trooper passes the same model tests as the other kinds: fourteen pieces and the wound, sockets on the skeleton's joints, every piece on its own joint and within its size, boots on the ground, hitboxes neither fatter nor much thinner than the limbs, helmet and head hitbox agree.
- The far copy: same pieces and sockets, same outline within the test's limits, still painted, 4,495 triangles, and **0.00% seen from inside** (4,000 rays; a second count of 20,000 found none). It was drawn beside the full model at the game's size from 3, 5, 9 and 24 m: at 3 m the edges of the bands on the limbs are slightly ragged, at 5 m the builder could see no difference (not a playtest).
- The model shader's pixel programs compile with glslang against a stand-in for Unity's library (not Unity's compiler).

## What is not verified

- **Whether Unity compiles the model shader.** If it does not, enemies, guns and base buildings turn bright pink. Step 4 below is the way out.
- **How shine looks.** Nobody has seen it. It will be simpler than Blender's pictures: one highlight from the Sun, one from the lamp, and the painted sky. No reflections of nearby things, and no cast shadows on planets.
- **Whether a mesh with a second UV pair and no first is drawn correctly on the Deck.** Believed fine.
- **Frame rate.** Nothing is measured. See `docs/performance_budget.md`.
- **Whether the trooper reads in a real fight**, with a gun in its hands, a wound where a limb was, and the game's haze. The sheet in the earlier report showed that a missing forearm does not read without the wound.
- **Whether it is still funny** when a realistic soldier loses limbs.
- **How the two styles look side by side.**
- The arena spawns only troopers, so the steps below say nothing about the two styles side by side.
- PlayMode tests were not run.

## For Matthew: ten minutes on the Deck

These steps are for after this work has been joined into the `claude/playtest` branch (the build thread does that and will say when).

1. In Konsole: `cd ~/Orbital-ante`, then `git fetch`, then `git checkout claude/playtest`, then `git pull`.
2. Open the project in Unity Hub and wait for it to import.
3. Menu **Orbital Ante > Open Test Arena**, press **Play**, click **Combat test arena**.
4. **Look at an enemy and at your rifle. If either is bright pink:** press **Play** to stop. In the **Project** window open **Assets > OrbitalAnte > Resources > Config** and click **LookConfig**. In the **Inspector**, under **Realism**, drag **Shine** to **0**. Press **Play** again; things should look as they used to. Then open **Window > General > Console**, copy the red lines that mention `OrbitalAnte/Model`, and send them to me.
5. If nothing is pink: walk up to a trooper (the white suit with the gold visor). Is it the level of realism you agreed to in the pictures? Does the visor shine, and does anything sparkle or flicker as it moves?
6. Press **H** to start the fight. Shoot off a forearm (below the elbow), then a whole arm, then a leg, then the head. Could you tell what came off? Could you find the head from behind?
7. Back off to the far wall, about 40 m from the troopers on the other side. Beyond 24 m you are seeing the simpler far copy. Shoot at one. Can you still pick out its head, front and back?
8. Get all eight troopers in view within about 20 m (press **H** again to make them hold fire if that helps). Press **F3** and read me the line that starts **Far copies** and the line that starts **Budget**. It should say full 8/8. Play mode in the Editor runs slower than a real build, so this is a first look, not the Deck measurement.
9. Tell me whether losing limbs is still funny on this one. (The arena has only troopers. Seeing them next to the other three kinds needs a run on a planet and is not part of these ten minutes.)

## A question for Matthew, no hurry

Should the common enemy look NASA-like (white suit, gold visor), when the build prompt gives the NASA suit to the player? Recommendation: keep it for now and settle it with Faction A in Phase 2.

## Agent review

Three reviewers read this change on 15 Nov 2026 (the first attempt on 14 Nov stopped on a usage limit). None of them ran Unity.

**Unity reviewer.** No bug that breaks the first Play. Old Look assets get 180,000, not 0; statics are reset; the new working list is sized with the others; 16-bit indices are fine. Found and fixed:
- A body that had lost limbs still counted as a whole body against the limit, so a few maimed troopers close by could push an unhurt one into its far copy. It now counts only the pieces it still holds.
- The limit counted the wound stump, which an unhurt body never shows (21,460 instead of 21,176). It now counts only pieces that can be swapped, so "twelve of the first model came to 178,000" is true by the game's own count.
- `Full Triangles` at 0 meant no limit, the most expensive setting, at the left end of a slider where `Full Bodies` 0 means none. The slider now runs from 20,000.
- `isSupported` may say yes for a shader that failed to compile. In the editor the game now also asks whether the shader has errors.

**Deck performance.** No allocation per look; the extra work in choosing is microseconds. Found and acted on:
- The big risk is pixels, not triangles: the trooper was drawn in full out to 24 m although its triangles are smaller than a pixel beyond about 9 m, and graphics cards pay for those several times over. The triangle limit does nothing about that. A finer body now changes to its far copy sooner, by a rule (`DetailRule.RangeFor`): its triangles are never smaller on screen where it changes than a 16,000-triangle body's. That is 20.9 m for the trooper. The reviewer suggested 12 m; the rule is the smaller step, and `Far Copy Distance` on the Look asset can bring it in further if the Deck says so.
- Shine is chosen pixel by pixel, but the graphics card shades pixels in groups, so a small trooper will pay for shine on more than its 30% of shiny surface. Written into the budget.
- The first trooper in view now triggers the one-off build of the model shader (guessed 5 to 30 ms) mid-game. Written into the budget as a known follow-up (warm it while loading); not fixed here.
- Budget text that claimed more than it knew was corrected (row for the model shader, the enemy model row, the "worst frame stays the same" line).

**Game designer.** Checked the steps against the arena code. Found and fixed: the arena spawns only troopers, so the old step 9 could not be done; the arena has exactly eight troopers, so step 8 now asks for all eight close and warns that Editor play is slower than a build; acceptance now names ranges and the forearm; the legibility claim now says the only evidence is the builder's own sheet; the risk to the tone of the gibs is named; restoring means both files; the far copy judgement is marked as the builder's. Also carried over a question for Matthew (below).

**For a longer session on the Deck** (not the ten minutes): the Deck reviewer's five readings are the way to measure this properly. With twelve or more troopers within 10 m, read F3's Far copies and Budget lines and the Stats panel's Tris, Batches and SetPass calls; then again with `Full Triangles` 600,000; then with it back at 180,000 and `Far Copy Distance` 0.5 with the troopers at 12 to 20 m; then with `Shine` 0; and watch "ms worst" on F3 the moment the first trooper comes into view after pressing Play.
