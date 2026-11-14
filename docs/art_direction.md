# Art direction

Status: **direction changed on 14 Nov 2026 to a more realistic look. One asset (the common enemy) has been drawn in the new look as a before and after. The level of realism is waiting for Matthew's yes before anything in the game is replaced.** Nothing here has been seen running in Unity.

## Where this came from

- 13 Nov 2026: Matthew ran the game for the first time on his Steam Deck and said it looked like a PS2 game. He asked for "an aesthetic just like Outer Wilds". That was read as a stylised, hand-crafted look with chunky shapes and painterly colour, and the sky, light, haze and finish built for it (look pass 1) are still the foundation.
- 14 Nov 2026: after seeing the models made in that style, Matthew wrote: **"I don't want everything so cartoony goofy and blocky for art style. I want more realistic looking. I also want landscape to look much more realistic and have more interesting geography and much more geography."**

The 14 Nov message wins. It replaces the "chunky, slightly exaggerated" shapes and "painted, not flat" materials of the 13 Nov direction, and the "like TF2 and/or Gang Beasts" line in section 3 of the build prompt, for how things **look**. It does not change how the game **plays or jokes**: the build prompt's humour stays in the gibs, the sounds, the behaviour and the UI, on top of what the prompt itself calls "convincing, grounded space hardware".

## What "more realistic" means here

**Things look like what they are.** A space suit looks like a pressure suit somebody could wear. A rifle looks like a rifle that could fire. A lander looks like a machine that was built, flown and repaired. The test for any asset: put a photograph of the real thing beside it. It should look like a careful model of that thing, not a toy of it.

It does **not** mean photographs wrapped on models, and it does not mean a grim, grey game. It means:

1. **Real proportions.** People are the height and build of people in suits. No big heads, mitts or boots. Hardware is the size it needs to be to work.
2. **Real reference, named.** Every asset starts from the real thing it is based on, and its report says what that was (for the enemy: Apollo and present-day space suits, and ordinary soldiers' webbing).
3. **Real materials.** Cloth hangs, gathers at joints and creases. Metal is machined, anodised or painted, and worn at its edges. Rubber and silicone are dull. Glass and gold visors reflect the sky.
4. **Real colour.** Colours are taken from what the thing is made of, as it looks after use: off-white suit cloth, coyote webbing, grey aluminium, dark polymer. Strong colour is rare and has a job (a red band to tell one suit from another, blue and red hose connectors, a warning label).
5. **Use shows.** Dust settles low and on top. Edges wear to bare metal. Shadow gathers where things crowd together. All three are worked out from the shape and kept in the paint.
6. **Detail in layers.** A big clear form first, then the working parts fixed to it, then small fittings. A realistic thing has more parts than a cartoon one, but each is there for a reason.

## What stays from before

- **Clear silhouettes.** The build prompt asks that enemy kinds be "unmistakable at a glance". Realism must not cost that. Kinds differ by build and height (the game already scales them), by what they carry (pack, plates, long gun, shield) and by one band of colour, the way real crews are told apart.
- **Never deliberately retro.** No low-resolution textures, wobble or filters.
- **Sky, air and finish (look pass 1).** Gradient skies, haze that fades into the sky behind it, warm sunlight with sky-coloured shadow, bloom on things that shine, tone mapping. These are how real air and cameras behave and they stay.
- **Destructible things split along their real parts.**
- **Original designs.** Real hardware is the reference, never copied badge for badge. No flags, names or markings.

## The pillars, rewritten

### 1. Palette
- **Natural colours for every surface**, measured against photographs. Ground: real rock, soil, sand, ice. Hardware: real cloth, metal, paint, rubber. Nothing brighter than pale sand except snow, ice and white cloth in full sun.
- **Colour comes from light and air**, not from paint. Warm sunlight, sky-coloured shadow, sunsets, each world's own sky and haze: this is where the picture's colour and mood live. A grey rock at sunset is not grey.
- **Strong colour is saved for meaning**: warnings, things that shine, the bands that tell enemies apart, loot.
- The colour grade is turned down to match: less added saturation, no teal and amber push. (A dial on the Look asset; today's value stays until Matthew has seen the new models in the game.)

### 2. Lighting
- **Soft sunlight with a natural fall-off.** The old look firmed the edge between light and dark into a band, which is a cartoon habit. The band's firmness goes on a dial and is set soft.
- **Sky light from above, bounce from below, warm light and sky-coloured shadow** stay.
- **Shine.** Until now nothing in the game was shiny: every surface was lit like chalk. Models now say what each surface is made of (how smooth it is, and whether it is bare metal), and smooth surfaces show a highlight from the Sun and a reflection of the sky. This is what makes metal look like metal and a visor like a visor. It is one more value per corner of a model and a few lines in the shared light code, not a new lighting model.
- **Shadow where things crowd together** is worked out when a model is built and kept in its paint, so it costs nothing in the game.
- The baked cabin light is untouched.

### 3. Sky and air
Unchanged from look pass 1.

### 4. Materials
- **What it is made of decides how it looks**: matte cloth, satin paint, bright bare metal, dull rubber, mirror visor.
- **Dust, wear and grime follow the shape**: dust low and on upward faces, wear on outward edges, grime in creases.
- The painted warm and cool dabs of the old look are turned well down on models and on the ground. They read as illustration.
- Still no photographic texture files on the game's own models. See "How far this can go" below.

### 5. Shapes
- Real proportions and real mechanisms. Reference first.
- **Soft things are modelled soft**: cloth is a skin drawn through cross-sections with folds pressed into it, not a straight tube.
- **Hard things are modelled as built**: panels, fasteners, hoses, hinges, connectors where they would really be.
- No shape is a plain box if the real thing is not. Boxes get the bevels, covers and fittings the real one has.

### 6. Finish
Bloom, tone mapping, vignette and edge smoothing stay. The grade is made more neutral (see Palette).

## How far this can go, said plainly

The game's own models are built by scripts in Blender and coloured per corner, with no texture pictures. That keeps them small and fast on the Deck and lets every one be rebuilt and changed. It reaches the level in the first before and after: a convincing, well-made model of a real thing.

It does not reach film or scan quality. Fine surface detail (the weave of cloth, scratches, printed labels) needs texture pictures, which this pipeline does not have. If Matthew wants to go further than the before and after shows, the way is:

- **Textured models from a 3D generator** (the build prompt names Tripo AI; NASA's own free models are another source), brought in through a new import path with textures. This is a real piece of engine work and each model costs more on the Deck.
- **Higgsfield** makes pictures and video, not models the game can load. It can supply reference and concept pictures for either route.

## Order of work

1. **The common enemy, as the test of level** (drawn, not yet in the game). See `docs/reports/realistic-look-preview.md`.
2. On Matthew's yes: put that enemy in the game (model file, far copy, hitbox fit, the shine value in the model file and the light code), then the other three kinds on the same suit with their own build and kit.
3. The guns: real mechanisms, real finishes (blued steel, parkerised steel, polymer, worn wood).
4. The lander's outside, then the cabin, keeping the cabin's warmth (Matthew asked for a place of refuge, and a real cabin can be that).
5. The look dials (band firmness, grade) set while looking at the new models in the game.

The landscape (shape of the ground, more geography, natural ground colours, rocks, trees and bases) is being done in the bases and scenery thread on the same rules.

## Rules for anyone adding to the look
- Name the real thing the asset is based on before modelling it.
- New shaders include `OA_Look.hlsl` and take their sky, light and haze from it. Do not write a second lighting model.
- Never fade to a flat fog colour. Use `OA_ApplyHaze`.
- Colours are picked by eye (as seen on screen) and converted to linear once, where they are handed to the graphics card: shader settings in `SkyGlobals.Linear`; mesh colours when the mesh is made (`ShipMeshes.ToMesh`, `ModelLibrary.ToMesh`). Model files (`.bytes`) store colours as seen on screen. The baked cabin light is the exception: it is stored already linear. Never convert twice.
- Realistic shapes and paint for Blender scripts are in `tools/blender/oa_real.py` (skins through cross-sections, hoses and straps, cloth folds, crowd shadow, dust, what a surface is made of).
- Anything that shines by itself is drawn brighter than white so it blooms, rather than given a glow sprite.
- Every new cost goes in `docs/performance_budget.md`.
- Nothing is called finished on looks until it has passed the visual quality gate on real captures.
