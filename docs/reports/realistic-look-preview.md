# Realistic look: first before and after (the common enemy)

Status: **a drawing to judge, nothing in the game has changed.** The pictures were made in Blender from a new model script. They are not Unity screenshots, the game still draws the current enemy, and no frame time has been measured.

## What Matthew asked (14 Nov 2026)

"I don't want everything so cartoony goofy and blocky for art style. I want more realistic looking."

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

The fourteen breakable pieces are the same and are cut at the same joints, so the dismemberment system does not change. The picture in the game's "ready" pose shows the joints holding together when the knees and elbows bend.

## Why (constitution)

- **Principle 1, fun core gameplay first**, and the build prompt's "convincing, grounded space hardware": Matthew found the cartoon style was getting in the way of the game he wants. This is a look change only; no rule of play moves.
- **Principle 7, manage cognitive load**: the prompt asks that enemy kinds be "unmistakable at a glance". A realistic suit in off-white is harder to tell apart by colour than teal against orange. The plan to keep kinds readable is in `docs/art_direction.md`: build and height (already in the game), what each carries, and one band of colour. **This is not shown yet: only one kind has been drawn.**
- **Principle 10, evidence-based iteration**:
  - Problem: the art reads as cartoony, goofy and blocky.
  - Hypothesis: real proportions, real suit hardware, cloth that folds, natural colour and shine will read as realistic while staying inside the game's model pipeline and the Deck's budget.
  - Smallest test: this one enemy, drawn beside the current one.
  - Acceptance: Matthew says this is the level he wants (or says which way to move it).
  - Playtest protocol: none yet. Nothing is in the game.

## What was tested

- The model builds and draws in Blender (4.0.2) from `tools/blender/build_enemy_realistic.py`; the pictures above are its output.
- The same script stands both enemies in a pose dumped from the game's animator (`tools/blender/pose_dump`, mode `kinds`, pose `ready`).
- No inside-out faces showed in any of the four views (the preview marks them magenta).
- No game code changed, so the rule tests were not re-run for this.

## What is not verified, and what it would take to put in the game

- **Not in the game.** No model file was written. To ship it: write `Enemy.bytes` and its far copy from this script, check the hitbox fit (`BodyFit`; the helmet is a little smaller than the old one, and the head is the only kill, so that matters), and re-run the rule tests.
- **Shine is not in the game.** The pictures show smooth and metal surfaces because Blender's renderer does. The game's light code has no highlight or reflection yet. It needs one more value per corner in the model file and a small addition to `OA_Look.hlsl`. Without it the gold visor would be a flat ochre and the metal rings flat grey. This is the largest difference between the picture and what the game would draw today.
- **Cost on the Deck.** 21,188 triangles against 14,848 now, about 43% more for each enemy drawn in full. The far copies and the limit of 12 full bodies still apply, so the worst case rises from roughly 178,000 to 254,000 triangles for twelve full bodies. Not measured.
- **The other three kinds, the guns, the ship and the cabin** are still in the old style.
- **How far this route goes.** This is close to the ceiling for models built by script with colour per corner. It does not reach scanned or film quality: there is no cloth weave, no printed labels, no scratches. Going further needs textured models (the build prompt names Tripo AI) and a new import path. `docs/art_direction.md` sets this out.

## Files

- `tools/blender/oa_real.py` (new): shapes and paint for the realistic look.
- `tools/blender/build_enemy_realistic.py` (new): the enemy and the comparison pictures.
- `tools/blender/oa_blender.py`: the preview picture now shows what a surface is made of when a model says so. Models that do not say are drawn exactly as before.
- `docs/art_direction.md`: rewritten for the 14 Nov direction.
- `CLAUDE.md`: the Look note now carries the 14 Nov message.

## Agent review

Not run. No engine-facing code was written, so the three reviewers the repository asks for were not called. They will be when this model, the shine value and the shader change go into the game.
