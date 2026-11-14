# Art direction

Status: **direction changed on 14 Nov 2026 to a more realistic look. One asset (the common enemy) has been drawn in the new look as a before and after. The level of realism is waiting for Matthew's yes before anything in the game is replaced.** Nothing here has been seen running in Unity.

## Where this came from

- 13 Nov 2026: Matthew ran the game for the first time on his Steam Deck and said it looked like a PS2 game. He asked for "an aesthetic just like Outer Wilds". That was read as a stylised, hand-crafted look with chunky shapes and painterly colour, and the sky, light, haze and finish built for it (look pass 1) are still the foundation.
- 14 Nov 2026: after seeing the models made in that style, Matthew wrote: **"I don't want everything so cartoony goofy and blocky for art style. I want more realistic looking. I also want landscape to look much more realistic and have more interesting geography and much more geography."**

The 14 Nov message wins. It replaces the "chunky, slightly exaggerated" shapes and "painted, not flat" materials of the 13 Nov direction, and the "like TF2 and/or Gang Beasts" line in section 3 of the build prompt, for how things **look**. It does not change how the game **plays or jokes**.

**A reading Matthew has not confirmed:** that the build prompt's humour now lives in the gibs, the sounds, the behaviour and the UI, on top of what the prompt itself calls "convincing, grounded space hardware". His message was about the art style. Whether a realistic soldier losing limbs is still funny rather than grim is a question for the first fight on the Deck, and for him.

Three lines of the build prompt that his message did not overturn, and that this direction has to keep:

- **"Not overly technical looking"** (section 1). Realistic detail must be few, large and legible parts with a job, not a crust of small fittings.
- **"Colours/lighting should keep the world interesting rather than bland"** (section 3). Natural colour on surfaces is only acceptable because the light, the sky and the air carry the colour. If a scene comes out grey, that is a failure of this direction, not its aim.
- **Spoof and silly guns** (section 9) and a cockpit dash that is "cartoony yet technical" (section 7). Absurd objects stay absurd. They are made as if somebody really built them.

## What "more realistic" means here

**Things look like what they are.** A space suit looks like a pressure suit somebody could wear. A rifle looks like a rifle that could fire. A lander looks like a machine that was built, flown and repaired. The test for any asset: put a photograph of the real thing beside it. It should look like a careful model of that thing, not a toy of it.

It does **not** mean photographs wrapped on models, and it does not mean a grim, grey game. It means:

1. **Real proportions.** People have the anatomy of people in suits. No big heads, mitts or boots. Hardware is the size it needs to be to work. Enemy kinds still differ clearly in size, as the build prompt asks: the difference is in the whole body's scale (already in the game) and in the bulk of suit, armour and kit, never in a cartoon anatomy.
2. **Real reference, named.** Every asset starts from the real thing it is based on, and its report names it and the pictures looked at (the build prompt, section 3: "Pull real reference images before designing any asset"). The first enemy was drawn from knowledge of the Apollo A7L suit and the present-day space-walk suit, and of infantry webbing. No reference pictures were collected for it, which is a gap to close before the next asset.
3. **Real materials.** Cloth hangs, gathers at joints and creases. Metal is machined, anodised or painted, and worn at its edges. Rubber and silicone are dull. Glass and gold visors reflect the sky.
4. **Real colour.** Colours are taken from what the thing is made of, as it looks after use: off-white suit cloth, coyote webbing, grey aluminium, dark polymer. Strong colour is rare and has a job (a red band to tell one suit from another, blue and red hose connectors, a warning label).
5. **Use shows.** Dust settles low and on top. Edges wear to bare metal. Shadow gathers where things crowd together. All three are worked out from the shape and kept in the paint.
6. **Detail in layers.** A big clear form first, then the working parts fixed to it, then small fittings. A realistic thing has more parts than a cartoon one, but each is there for a reason.

## What stays from before

- **Clear silhouettes.** The build prompt asks that enemy kinds be "unmistakable at a glance". Realism must not cost that. See "Reading a fight" below.
- **Never deliberately retro.** No low-resolution textures, wobble or filters.
- **Sky, air and finish (look pass 1).** Gradient skies, haze that fades into the sky behind it, warm sunlight with sky-coloured shadow, bloom on things that shine, tone mapping. These are how real air and cameras behave and they stay.
- **Destructible things split along their real parts.**
- **Original designs.** Real hardware is the reference, never copied badge for badge. No flags, names or markings.

## Reading a fight

A realistic suit in one pale colour is harder to read than teal against orange. The old paint happened to colour each breakable part differently, and the new one does not. These are requirements on every enemy model before it goes in the game. None of them has been shown yet.

**Telling kinds apart** (shape first, never hue alone, because warm haze and sunsets change hue):
- Whole-body size and bulk of suit and armour.
- **Helmet shape**, one per kind, because the head is what the player aims at.
- Stance and gait (`BodyStyle` already gives each kind its own).
- The outline of the gun and of what is carried on the back.
- A pattern of light and dark that survives any light (dark plates on a pale suit, or the other way).
- Band colour is kept in reserve for telling factions apart in Phase 2 (colour for the side, shape for the kind).

**Telling parts apart** (limb pops, disarms and leg shots must "read instantly"):
- Gloves and boots clearly darker than the suit.
- A dark ring at every place a body comes apart: wrist, elbow, shoulder, hip, knee, ankle, neck.
- A wound (`Stump`) that stands out against pale cloth.
- The gun arm must not vanish against the chest.

**The head** is the only kill, so it must be findable from every side: nothing the same colour standing behind it (the pack's top stays below the neck ring), and a mark on the back of the helmet.

**Nothing is shown that cannot be used.** A holstered pistol that cannot be shot off or picked up is a false promise; fittings like that are left off.

**Glints must not pass for tells.** A highlight on a visor or a metal ring must stay well below the brightness of a muzzle flash or the Marksman's lens.

**The proof** is a sheet drawn at the game's own size (1280 x 800, the game's width of view) from 5, 10, 24 and 40 m, front, side and back, on pale ground, in orange haze and at dusk: holding its gun, running, with an arm off, a leg off, head only, a loose limb on the ground, and the far copy; and all four kinds as black outlines at those sizes.

## The pillars, rewritten

### 1. Palette
- **Natural colours for every surface**, measured against photographs. Ground: real rock, soil, sand, ice. Hardware: real cloth, metal, paint, rubber. Nothing brighter than pale sand except snow, ice and white cloth in full sun.
- **Colour comes from light and air**, not from paint. Warm sunlight, sky-coloured shadow, sunsets, each world's own sky and haze: this is where the picture's colour and mood live. A grey rock at sunset is not grey.
- **Strong colour is saved for meaning**: warnings, things that shine, the bands that tell enemies apart, loot.
- The colour grade is turned down to match: less added saturation, no teal and amber push. (A dial on the Look asset; today's value stays until Matthew has seen the new models in the game.)

### 2. Lighting
- **Soft sunlight with a natural fall-off.** The old look firmed the edge between light and dark into a band, which is a cartoon habit. The band's firmness is now a dial on the Look asset (`Light Band`). It is left at the old value (0.6) and will be turned down while looking at the new models in the game, not before.
- **Sky light from above, bounce from below, warm light and sky-coloured shadow** stay.
- **Shine.** Until now nothing in the game was shiny: every surface was lit like chalk. A model can now say what each surface is made of (how smooth it is, and whether it is bare metal), and smooth surfaces show a highlight from the Sun and a reflection of the sky. This is what makes metal look like metal and a visor like a visor. It is two numbers per corner of a model, a function in the shared light code (`OA_Shine`) and a shader for models (`OA_Model.shader`). The code is written and has not been seen in Unity, and no model in the game says what it is made of yet, so nothing shines until one does. Inside the ship there is no shine yet either.
- **Shadow where things crowd together** is worked out when a model is built and kept in its paint, so it costs nothing in the game.
- The baked cabin light is untouched.

### 3. Sky and air
Unchanged from look pass 1.

### 4. Materials
- **What it is made of decides how it looks**: matte cloth, satin paint, bright bare metal, dull rubber, mirror visor.
- **Dust, wear and grime follow the shape**: dust low and on upward faces, wear on outward edges, grime in creases.
- The painted warm and cool dabs of the old look are turned well down on models and on the ground. They read as illustration.
- No texture pictures on the game's own models so far. That follows Matthew's 12 Nov instruction that assets are made in Blender, and it is the open question on the 14 Nov decision card. See "How far this can go" below.

### 5. Shapes
- Real proportions and real mechanisms. Reference first.
- **Soft things are modelled soft**: cloth is a skin drawn through cross-sections with folds pressed into it, not a straight tube.
- **Hard things are modelled as built**: panels, fasteners, hoses, hinges, connectors where they would really be.
- No shape is a plain box if the real thing is not. Boxes get the bevels, covers and fittings the real one has.

### 6. Finish
Bloom, tone mapping, vignette and edge smoothing stay. The grade is made more neutral (see Palette).

## How far this can go, said plainly

The game's own models are built by scripts in Blender and coloured per corner, with no texture pictures (Matthew asked on 12 Nov for assets to be made in Blender). That keeps the files small and lets every model be rebuilt and changed. What it reaches is the level in the first before and after, which is for Matthew to judge.

It does not reach film or scan quality. Fine surface detail (the weave of cloth, scratches, printed labels) needs texture pictures, which this pipeline does not have. The build prompt's own pipeline (section 3) is different: concept pictures, then models from Tripo AI, and NASA's free models for space hardware. If Matthew wants to go further than the before and after shows, the ways are:

- **Textured models everywhere.** Unity loads such models without new code. The real costs are elsewhere: a generated enemy does not arrive cut into fourteen pieces at the joints with wounds, it needs its far copy and hitboxes fitted, the shared light needs a textured version, style drifts from one generated asset to the next, and texture memory on the Deck has not been measured.
- **A mix.** NASA or generated models for hardware that does not come apart limb by limb (ships, satellites, rovers, the pad), and script-built bodies for enemies, where the dismemberment system needs exact cuts. Not on the 14 Nov card; to be offered when Matthew answers.
- **Higgsfield**, as far as is known here, makes pictures and video, not models the game can load. It could supply the concept pictures that the build prompt gives to ChatGPT.

## Order of work

1. **The common enemy, as the test of level** (drawn, not yet in the game). See `docs/reports/realistic-look-preview.md`.
2. On Matthew's yes: put that enemy in the game (model file, far copy, hitbox fit, the shine value in the model file and the light code), then the other three kinds on the same suit with their own build and kit.
3. The guns: real mechanisms, real finishes (blued steel, parkerised steel, polymer, worn wood). The spoof and silly guns of section 9 keep their jokes and are built as if real.
4. The lander's outside. Then the cabin's finish only: the things Matthew asked for on 13 and 14 Nov (warm lamps, things from home, the wooden ribs and brass crown, the orrery, the hardware) stay as they are, and nothing in the cabin is reshaped without asking him first.
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
