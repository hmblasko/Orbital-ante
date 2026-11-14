# Realistic look: first before and after (the common enemy)

Status: **a drawing to judge. The game still draws the current enemy.** The pictures were made in Blender (its Cycles renderer) from a new model script. They are not Unity screenshots and no frame time has been measured. A second commit on this branch adds the code that lets a model shine (see "Shine" at the end); it changes which shader the game's models are drawn with, and it has not been run in Unity.

## What Matthew asked (14 Nov 2026)

"I don't want everything so cartoony goofy and blocky for art style. I want more realistic looking. I also want landscape to look much more realistic and have more interesting geography and much more geography."

The landscape half of that (two of his three sentences) is not covered here. It is being worked on in the bases and scenery thread, on branch `claude/realistic-landscapes`, and needs its own before and after from there.

Before the ship, cabin, guns and enemies are all redone, one asset has been redone so the level of realism can be agreed first. The common enemy was chosen because it was the most cartoon-like thing in the game: a big round helmet with a snout, mitts, big boots and teal, orange and mustard paint.

## The pictures

`docs/art/previews/realistic_enemy_before_after.jpg` (all four views), and one file per view: `realistic_enemy_front.jpg`, `realistic_enemy_close.jpg`, `realistic_enemy_back.jpg`.

Each picture shows the current enemy ("Now") and the new one ("Proposed") in the same light, from the same camera, both standing in the "ready" pose that the game's own animator gives the common enemy.

## What changed, and what it is based on

The new enemy is a soldier in a real pressure suit. The reference is the Apollo lunar suits and present-day space-walk suits, with ordinary infantry webbing worn over the top. Nothing is copied badge for badge and there are no flags, names or markings.

| | Now | Proposed |
|---|---|---|
| Proportions | Head, hands and feet enlarged | A person in a suit, on the same skeleton |
| Helmet | Round ball, amber slot, orange snout and crest | White shell, gold sun visor, shade, lamps, a camera, a metal neck ring |
| Cloth | Smooth tubes with rubber bellows | Cloth that creases, and gathers in rings at elbows, knees and hips |
| Chest | A box with three coloured buttons | A control box with a display, switches, a gauge and two valve knobs; blue and red hose connectors with hoses running round to the pack |
| Pack | A box with two orange tanks and a bedroll | A cloth-covered life-support pack with a top unit, straps, an access plate, a canister and an aerial |
| Kit | Two belt pouches | A webbing belt with magazine pouches, a pistol in a thigh holster, a cargo pocket, knee pads, a checklist on the left forearm |
| Hands and feet | Mitts, boots with orange toe caps | Gloves with fingers and blue silicone tips; moulded boots on ribbed soles |
| Colour | Teal, cream, orange, mustard | Off-white cloth, coyote webbing, grey metal, dark polymer; one red band on arms, legs and helmet |
| Paint | Soft blotches of lighter and darker paint | Dust that settles low and on top, shadow where things crowd together, wear on edges |
| Shine | None: everything lit like chalk | Each surface says what it is made of: matte cloth, satin shell, bright metal, a mirror-gold visor |
| Triangles | 14,848 | 21,188 |

The fourteen breakable pieces are the same and are meant to be cut at the same joints, so the dismemberment system is designed not to change. That is not proved: the pictures show one pose (the game's "ready" pose, in which the limbs are nearly straight), with no gun, no limb off and no wound. A run, a crouch and a crawl have not been drawn.

Four things in the table are less than they sound. Dust and edge wear are in the paint but faint, and hard to find in the pictures. The pack is still close to a plain box with a plate on it. The helmet is not really smaller than the old one (a ball of radius 0.163 m against 0.160 m stretched to 0.166 across): it looks smaller because the crest, snout and aerial are gone and the shoulders are wider. And how close the result is to a photograph is for Matthew to say; no photograph was put beside it.

## Why (constitution)

- This follows the director's instruction of 14 Nov and the build prompt's "convincing, grounded space hardware". It is a look change only; no rule of play moves.
- **It puts principles 3, 5 and 7 at risk** (learnable challenge, legible risk, cognitive load), and the build prompt's "unmistakable at a glance" and "limb pops, disarms and leg shots all read instantly". A pale suit on pale ground, in one colour across all fourteen pieces, is harder to read than teal against orange. What every enemy model must show before it goes in the game is now written in `docs/art_direction.md` under "Reading a fight". **None of it is shown yet.** The model as drawn fails several of those points today: gloves, boots and the cut lines are not dark enough to mark the parts, the top of the pack stands behind the lower quarter of the helmet in the same colour, the bands are too thin to see beyond about 10 m, and it wears a holstered pistol that could not be shot off or picked up.
- **Principle 10, evidence-based iteration**:
  - Problem: the art reads as cartoony, goofy and blocky.
  - Hypothesis: real proportions, real suit hardware, cloth that folds, natural colour and shine will read as realistic while staying inside the game's model pipeline and the Deck's budget.
  - Smallest test: this one enemy, drawn beside the current one.
  - Acceptance for the level of realism: Matthew says this is the level he wants (or says which way to move it).
  - Acceptance before the model replaces the current one: the "Reading a fight" sheet in `docs/art_direction.md` has been drawn and looked at, the rule tests pass with the new model files (six fail today, see below), and the worst case fits `docs/performance_budget.md`. It fails if the kind cannot be named, or a lost limb cannot be seen, at 24 m on the sheet.
  - Playtest protocol for the Deck (to run, never to predict; the pass marks are first guesses for Matthew to change):
    1. Handheld, 1280 x 800, same seed, the arena, then the Moon, then Mars, with the old and the new model.
    2. Name the kind from a one-second look at 10, 24 and 40 m, from front, side and back, by day, at dusk and by lamp. Pass: 9 of 10 right at 24 m.
    3. Headshots: shots to the first headshot at 10 and 24 m, from front and behind, old against new. Pass: the new one is no more than 15% worse.
    4. Called shots with the sound off ("the gun arm", "a leg"). Pass: the tester says what came off without hearing the pop.
    5. Disarm: does the tester see the gun fall, and find it and the limb on pale ground?
    6. Tells: the Marksman's aim tell with the Sun behind the player. Pass: no glint is mistaken for it.
    7. A crowd at the cap with far copies on: the Budget line and frame time on F3, with `Shine` at 1 and at 0.
    8. After each fight: "Did anything make you laugh?" (the Milestone 12 bar).
    9. Captures go to the visual quality gate for a score from somebody other than the builder.

## What was tested

- The model builds and draws in Blender (4.0.2) from `tools/blender/build_enemy_realistic.py`; the pictures above are its output.
- The same script stands both enemies in a pose dumped from the game's animator (`tools/blender/pose_dump`, mode `kinds`, pose `ready`).
- No inside-out faces showed in any of the four views (the preview marks them magenta). That is one pose from four angles, not the far copy's measured check.
- For the drawing itself no game code changed. The shine commit did change game code; what was run for it is under "Shine" below.

## What is not verified, and what it would take to put in the game

- **Not in the game.** No model file was written. To ship it: write `Enemy.bytes` and its far copy from this script, check the hitbox fit (`BodyFit`; the helmet is a little smaller than the old one, and the head is the only kill, so that matters), and re-run the rule tests.
- **Shine has not been seen in the game.** The pictures show smooth and metal surfaces because Blender's renderer does. The code for it is now written (see "Shine" below) but has not run in Unity, and it will not look the same as Blender's: the game's version is one Sun highlight and the painted sky, with no reflections of nearby things.
- **Shadows.** Blender's pictures have cast shadows and soft shading where parts touch. The game has no cast shadows on models. The shadow where things crowd together is in the model's paint and will carry over; the shadow of an arm across the chest will not.
- **Cost on the Deck.** 21,188 triangles against 14,848 now, about 43% more for each enemy drawn in full. The far copies and the limit of 12 full bodies still apply, so the worst case rises from roughly 178,000 to 254,000 triangles for twelve full bodies. Not measured. Not yet counted: the new far copy (a first trial came out too heavy and needs tuning), the number of corners, loose limbs, and the cost per pixel of shine.
- **The other three kinds, the guns, the ship and the cabin** are still in the old style.
- **How far this route goes.** This is close to the ceiling for models built by script with colour per corner. It does not reach scanned or film quality: there is no cloth weave, no printed labels, no scratches. Going further needs textured models (the build prompt names Tripo AI) and a new import path. `docs/art_direction.md` sets this out.

## Files

- `tools/blender/oa_real.py` (new): shapes and paint for the realistic look.
- `tools/blender/build_enemy_realistic.py` (new): the enemy and the comparison pictures.
- `tools/blender/oa_blender.py`: the preview picture now shows what a surface is made of when a model says so. Models that do not say are drawn exactly as before.
- `docs/art_direction.md`: rewritten for the 14 Nov direction.
- `CLAUDE.md`: the Look note now carries the 14 Nov message.

## Shine (second commit on this branch)

**What changed.** A model can say, corner by corner, how smooth a surface is and whether it is bare metal, and the game's light gives smooth surfaces a highlight from the Sun and the lamp and a reflection of the sky.

- Model files: a new layout, "OAM2", with two more bytes a corner (`ModelFile`, `ModelPart.Finish`; written by `oa.export` when a piece has a "Mat" layer; carried into far copies). "OAM1" files read as before.
- `OA_Look.hlsl`: `OA_Shine`, `OA_Highlight`, `OA_ShineAmount`, `OA_DaylitSky`, and the global `_OA_Finish`. `OA_Light` takes the firmness of the shadow's edge from a dial; the dial's default (0.6) is the old fixed value, so the ground is lit as before.
- `OA_Model.shader` (new): what the ground's shader does for a coloured mesh, plus shine. `ModelLibrary` draws enemies, guns, base buildings and the cockpit's camera fittings with it through `PlanetMaterials.Model`, which falls back to the ground's shader (and says so in the log) if it cannot be used.
- Two dials on the Look asset under "Realism": `Shine` (1) and `Light Band` (0.6).

**Why.** Without it the proposed enemy's gold visor would be flat ochre and its metal flat grey. It serves the 14 Nov direction; it changes no rule of play.

**What it changes today.** Nothing that should be visible: no model in the game says what it is made of, so every pixel takes the path with no shine. But every library model is now drawn by a shader Unity has never compiled. **That is the risk in this commit.**

**Tested (outside Unity).**
- The C# compiles against reference assemblies and the rule tests pass: 1,962 passed, 5 skipped, 0 failed. Four are new (`ModelFileTests`: a file with no finish reads back with none, a finish survives writing and reading, pieces without one are chalk, a file from a newer game is refused with a reason).
- The three pixel programs of `OA_Model.shader` compile with glslang against a stand-in for Unity's library. The corner program stops glslang on one construct the ground's shader already uses on the Deck. This is not Unity's compiler.
- A trial build of the proposed enemy as OAM2 files in a scratch folder (not in the game) read back and ran through the rule tests: five fail (the triangle cap of 16,000, hitbox fit by 3 mm, one piece reaching 1.05 from its joint against 0.75, and the far copy's share), which is the list of work before that model can go in. A sixth failure was the scratch folder lacking the cabin's light file.

**Not verified. To check first in Unity:**
1. Start a run and look at the Console, an enemy and a gun. Any magenta, or any error naming "OrbitalAnte/Model", means the shader did not compile; a compile error is not caught by the fallback. In the Project window select `OA_Model` and read the "SRP Batcher" line (expected: compatible).
2. With `Shine` at 1, in the arena and on the pad, today's models must look exactly as before, with no bright patches. (The shader relies on a mesh with no second UV pair reading as zero. That is how Unity is believed to behave and how the ground's shader has always been used, but it is not a documented promise.)
3. The Stats window's SetPass calls before and after this commit (models and ground no longer share a shader).
4. Everything about how shine looks: nobody has seen it. In particular bare metal by day, at night under the lamp, in open space and aboard the ship, and whether highlights sparkle on thin parts at 5 to 20 m.

**Known gaps.** The lander's outside, the launch pad and Earth scenery are drawn with the ground's shader and will not shine even if rebuilt with a finish. Inside the ship only the lamp's highlight shows. Models cast no shadows on planets, as before.

## Agent review

All three reviewers the repository asks for read the shine change and the direction on 14 Nov 2026. None of them ran Unity.

**unity-reviewer**
- *The finish was stored in the first UV pair, which the ground's shader reads as "up".* A model with a finish drawn by the ground's shader (the fallback, the lander, glow pieces) would have had its sunlight switched off as it turned. **Fixed:** the finish is in the second pair.
- *Bare metal lost 85% of its scattered light everywhere but only got the Sun and sky back.* Dark metal aboard the ship, under the lamp at night and in open space. **Fixed:** metal gives up scattered light only under a daylit sky, and the lamp now makes a highlight.
- *Highlights on smooth metal would sparkle* (smaller than a pixel, then bloomed). **Fixed, unseen:** a highlight is spread by how fast the surface turns across a pixel, and capped at 2 instead of 6.
- *A missing model shader fell back silently.* **Fixed:** it is logged. *A shader that exists but fails to compile is not caught by any fallback.* **Open:** needs Unity; first check above.
- No findings on: `_OA_Finish` before the first sky is set (every scene sets it), the SRP Batcher layout, material lifetime and cache keys, shaders without `.meta` files.

**deck-performance**
- *The shader change costs nothing today and a chalk pixel costs what it did.* Counted with a different compiler, not measured.
- *On the proposed enemy no pixel skipped the shine,* because even cloth had a little smoothness. **Fixed:** anything at 0.1 or below (about 70% of that model) is chalk and skips it, and the skip is marked so the compiler keeps it.
- *The real cost is the proposed enemy's triangles,* not shine: 21,188 against a cap of 16,000, most of it finer than a pixel beyond 9 m. **Open, for the model:** slim it before it goes in; if not, `Full Bodies` 8. Written into `docs/performance_budget.md`.
- *Moving shine to the corners would cost more, not less,* and break the highlight. Not done.
- *SetPass calls may rise; one new graphics program is built the first time a model with a finish is drawn.* **Open:** to be read off in Unity.

**game-designer**
- *Readability in a fight is not shown, and the model as drawn has specific problems* (pale on pale, no dark marks at the cut lines, pack behind the helmet, thin bands, a pistol that cannot be used, glints that could pass for tells). **Taken:** "Reading a fight" in `docs/art_direction.md` now sets what a model must show; the cap on highlights was lowered for the last point. **Open:** the model itself is not yet changed and the sheet is not yet drawn.
- *The report claimed more than the pictures show* (joints bending, wear, helmet size, "convincing"). **Fixed** above.
- *The art direction dropped lines of the build prompt Matthew did not overturn* ("not overly technical looking", "interesting rather than bland", spoof guns) *and promised to redo the cabin unasked.* **Fixed** in the art direction.
- *The decision card called textured models "going further", though the build prompt's own pipeline is textured models, and left out a mix* (NASA or generated models for hardware, script-built bodies for enemies). **Partly answered:** script-built models follow Matthew's 12 Nov instruction that assets are made in Blender, which the reviewer did not have. The mix is a fair option and is now in the art direction, to be offered when Matthew answers.
- *The landscape, two thirds of his message, has no before and after.* It belongs to the bases and scenery thread; said at the top of this report.
- *The reviewer thought the `Light Band` default changed the ground's light.* It does not (0.6 was the old fixed value); the dial's tooltip now says so.
- *Open questions it raised for Matthew:* whether gibs stay comic on a realistic soldier, and whether the common enemy should look NASA-like when the build prompt gives the NASA suit to the player and other references to the factions.
