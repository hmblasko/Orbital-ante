# Brief for Fable: rebuild the enemies and guns realistically

Matthew, 18 Nov 2026: "what I really want Fable 5.2 to do is recreate the enemies, same gibbing and body parts to be shot off, but make them substantially more realistic and not obviously made in blender like the cheap assets they currently look like. No ps2 aesthetic on them, I want more realistic. The same is true for the weapons."

This brief was written on Opus on 18 Nov as preparation. The models themselves are Fable 5.2's job (Matthew's standing rule), starting Thursday 19 Nov. Nothing here has been run in Unity.

---

## 1. What is there now (audit, 18 Nov, branch `claude/playtest` at e64052f)

**Bodies.** Four enemy models, each in fourteen rigid pieces plus a wound, all on one skeleton:

| Model | Script | Full / far triangles | Look |
|---|---|---|---|
| `Enemy` (trooper; Faction A's Line wears it) | `build_enemy_realistic.py` | ~21,200 / ~4,500 | The "realistic" suit Matthew agreed on 14 Nov |
| `EnemyRunner` (Scout A wears it) | `build_enemy_types.py` | ~12,500-14,850 / ~4,470 | Earlier, toy-like style |
| `EnemyBruiser` (Heavy A wears it) | `build_enemy_types.py` | same band | Earlier style |
| `EnemyMarksman` | `build_enemy_types.py` | same band | Earlier style |

**Guns.** `Rifle`, `Pistol`, `Smg`, `Shotgun`, `Sniper` from `build_weapons.py` (3,900 to 7,700 triangles, far copies 1,500 to 2,000). `SmgA`, `RifleA`, `HeavyGunA` and the Wildcard borrow those models. Picture: `docs/art/previews/guns_enemy_kinds.jpg`.

**Why they look "made in Blender".** Looking at `realistic_enemy_close.jpg` and `guns_enemy_kinds.jpg`:
1. **No textures at all.** Every model is coloured per corner (`ModelFile`: "there are no texture files"). Colour can only change where there is a corner, so there is no fabric weave, stitching, scuffs, worn edges, machining marks, grip stippling or rust. This is the single biggest limit, and no amount of modelling gets past it.
2. **Same soft bevel on everything.** Boxes and cylinders from `oa.box`/`oa.cylinder` all have the same rounded edge, so steel, cloth and rubber read as one moulded plastic.
3. **Primitive construction shows.** Guns are boxes, tubes and slabs; you can name the primitive behind each part. Real guns have draft angles, chamfers, lightening cuts, stamped sheet steel, pins, screws, serrations.
4. **Toy proportions on the guns.** Barrels and grips are too thick, magazines too chunky, colours saturated (orange stock, yellow band, green body).
5. **Uniform surface response.** Smoothness is one number per corner (`Finish`), so there is no rough-to-glossy variation within a surface.
6. **Rigid pieces meet at a ball.** Bodies are rigid parts on bones, so joints look like an action figure's when they bend. On a pressure suit this can be made honest (real suits have rigid bearing rings and bellows at those joints), but today it is not hidden.

**What must be kept (the contract every new model obeys).** These are checked by rule tests; break one and the game falls back to primitives or a test fails.

Bodies:
- One piece per `BodyPartId`, named exactly: `Head`, `Torso`, `LeftUpperArm`, `LeftForearm`, `LeftHand`, `RightUpperArm`, `RightForearm`, `RightHand`, `LeftThigh`, `LeftShin`, `LeftFoot`, `RightThigh`, `RightShin`, `RightFoot`, plus `Stump` (the wound cap). (`MannequinFactory.HasModel`)
- Each piece built in its own joint's space from `BodySkeleton` (copied in `build_enemy.py`; `EnemyModelFile_MatchesTheSkeleton`), starting with a ball or ring over its joint so a cut looks closed.
- No piece over 0.75 m in any direction (0.9 across for the torso); a hand fills its hitbox.
- Built at the standard size. The game scales the root (Runner 0.85, Bruiser 1.3, Marksman 1.2). Never model a kind bigger or smaller.
- Hitboxes are fitted from `BodyFit` rows; `EnemyModelFile_ItsHitboxesFitIt` fails if a body outgrows them. Change the row if a kind's bulk changes on purpose.
- Triangle caps: 22,000 for the trooper, 16,000 for other bodies, and eight full bodies must fit in `DetailRule.FullTriangles` (180,000).
- A far copy (`XFar.bytes`) with the same pieces, names and sockets, about 4,500 triangles, written by the same script, 0.00% seen from inside.
- "Reading a fight" in `docs/art_direction.md`: dark gloves and boots, a dark ring at every cut line, a stump that stands out, the head findable from every side with a mark on the back of the helmet, nothing shown that cannot be used, glints never as bright as a tell.
- Animation is code (`BodyAnimator`), not clips, so there is nothing to re-rig. The disarm, hobble, crawl, stump look and the death burst all work on the pieces as they are.

Guns:
- Sockets `Muzzle`, `SupportHand`, `Magazine`, and a separate `Magazine` piece (reload takes it away). (`WeaponModelFactory.BuildFromModelFile`)
- Anything that shines in its own piece ending `Glow` (`CellGlow` on the Shotgun and HeavyGunA, `LensGlow` on the Sniper: the Marksman's aim tell).
- Far copy at 2,200 triangles or fewer.
- Same grip point and length as today, or the held pose and the first-person view shift. Measure before and after.

---

## 2. What "substantially more realistic" means here

The bar: put a frame of the model at game size next to a photo of the real thing (or a current AAA shooter) and nobody should be able to say "that's a free Blender asset". Concretely:

1. **Textured, physically based surfaces** (new: see section 3). Every model gets a texture set baked in Blender: colour, normal, and a mask (ambient occlusion, smoothness, metal). Colour comes from what the thing is made of, never from paint for its own sake.
2. **Detail where the eye lands, in the normal map, not the triangles.** Fabric weave, seams and stitching, creases and pressure folds at elbows and knees, webbing edges, Velcro, rivets, screw heads, knurling, grip texture, machining, stamped markings with no readable text. Sculpted or scripted on a high-detail copy and baked down.
3. **Wear that tells a story.** Edge wear to bare metal on corners, dust settling on top surfaces, grime in creases and around fasteners, scuffed boot toes, oil around actions, faded cloth on shoulders. Driven by curvature and ambient occlusion in the bake, then hand-varied so it is never uniform.
4. **Shapes from real construction.** Guns: stamped receiver, milled slide, polymer frame with draft angles, real screw and pin layout, believable barrel and bore sizes. Suits: layered cloth over a pressure bladder, bearing rings, a hard chest unit, hoses with real bend radius and fittings, pouches that sag under weight.
5. **Varied edges.** Sharp machined edges on steel, rolled edges on sheet metal, soft gathered edges on cloth, chunky moulded edges on polymer. No single bevel width.
6. **Real colours.** Parkerised or Cerakote greys and blacks, worn bluing, FDE and OD polymers, oiled wood that is dark and grained, suit whites that are warm and soiled. Strong colour only where it has a job (band colour, a tell).
7. **Joints made honest.** Every cut line sits under something a real suit has there: a bearing ring at the shoulder and wrist, a bellows at elbow and knee, a hip ring, a neck ring. That hides the rigid-piece joint and is also the dark ring "Reading a fight" asks for.
8. **Gore that matches.** The `Stump` cap and the inside of each cut get their own texture: torn suit layers, padding, a wet dark wound. Graphic, not cartoon red. The humour stays in how gibs fly, not in how they look.

Not in this pass: skinned (bent) meshes. Rigid pieces stay because the whole dismemberment system is built on them; the joints are hidden instead. If joints still look wrong in Unity after this pass, skinning each piece to two bones is the next step and is its own job.

---

## 3. Engine change needed first: textures on library models

Today the game reads only corner colours. Realism at the level Matthew asked for needs textures, and Matthew already chose "textured guns" on 16 Nov. The plan, sized so Opus can build and test it before Fable starts, leaving Fable to make art only:

1. **File layout "OAM3"** (`oa.export` and `ModelFile.Parse`): adds per piece one texture coordinate pair, and per file the name of its texture set. OAM1 and OAM2 files keep working as before.
2. **Texture coordinates go in mesh UV channel 2.** Channel 0 is read by the ground shader as "up" and channel 1 holds `Finish`; neither may move.
3. **Texture set per model**, one set shared by all its pieces (and its far copy): `Resources/Models/Textures/<Model>_Color.png`, `<Model>_Normal.png`, `<Model>_Mask.png` (R ambient occlusion, G smoothness, B metal). One set per model keeps one material, so draw calls do not rise.
4. **Shader**: `OA_Model.shader` gets a textured path (a keyword or a second pass) that samples the three maps and still takes all sky, light, haze, cave light and shine from `OA_Look.hlsl`. No second lighting model. Never name a variable `smooth`.
5. **Import settings from code**: an `AssetPostprocessor` marks `_Normal` as a normal map, `_Mask` as linear, sets compression and max size, so Matthew clicks nothing.
6. **Fallbacks**: no texture set, or `Shine` 0, draws as today from corner colours. Bake the texture's average colour into the corners too, so the fallback and the far copy still look right.
7. **Rule tests**: an OAM3 file's coordinates are inside 0..1, every piece has them, its far copy uses the same set, the set exists.

**Steam Deck budget.** Bodies 2048 x 2048, guns 1024 x 1024 (the player's held gun 2048, since it fills the screen). Compressed with mip maps that is about 11 MB per body set and about 3 MB per gun set, so four bodies plus ten guns stay well under 100 MB of the Deck's shared memory. Triangle caps do not change. Draw calls stay 14 a body (texturing adds none). First-person gun texture fetch is the only real new cost; it is one model on screen.

---

## 4. The Blender pipeline for Fable (all headless, in the cloud)

Same tools as now: `blender -b -P tools/blender/<script>.py`, Blender 4.0 from apt, Cycles on CPU. Steps per model:

1. **Reference first.** Name the real thing each model is built from (Matthew's build prompt: "Pull real reference images before designing any asset"). Use Astra's reference boards if Matthew has brought them back (section 6).
2. **Low model (the game mesh).** Built by script as now, on the skeleton, inside the triangle caps, with clean quad-ish topology where it bends the outline. Spend triangles on the outline and the head; flat surfaces get their detail from the normal map.
3. **High model (never shipped).** A copy with the detail: multiresolution or subdivision plus scripted displacement (cloth folds from curvature and noise, seams as boolean-free inset lines, stitching as displaced strips), real bevels, screws and rivets as separate small meshes. Lives only in the scratch folder.
4. **UVs.** Smart UV project or seams along real seams, packed into one 0..1 atlas per model with 8 px margin at 2048. Texel density the same across a model; the head and hands can get a little more.
5. **Bake** (Cycles, high to low, with a cage): normal (tangent space, OpenGL Y+, which Unity expects), ambient occlusion, curvature (for wear).
6. **Materials, then bake to the texture set.** Procedural Blender materials per surface type, kept in one shared library script (`oa_materials.py`, new): suit cloth, webbing, hard shell, rubber, blued steel, parkerised steel, anodised aluminium, polymer, oiled wood, gold visor. Wear masks from curvature and AO, dust from facing up, grime from AO. Bake colour, smoothness and metal into the three maps. If CC0 texture sources (ambientCG, Poly Haven) are reachable from the cloud they may be used as inputs and logged in `docs/asset_licence_ledger.md`; if not, procedural only.
7. **Far copy** from the low model with `oa.thin`, keeping UVs, sharing the same texture set.
8. **Preview, honestly.** Render the model in the game's light from the game's distances (`--sheet`: 5, 10, 24, 40 m) beside the current model, plus a close-up, plus a pose with an arm and a leg off and the stump showing. Describe from the pictures, never from memory.
9. **Run the rule tests** (`dotnet test tools/rules-tests`) with the new files, and the far-copy inside check.

**Scripts.** Keep one script per body family as now (`build_enemy_realistic.py` for the trooper, a new realistic builder for the three kinds) and rewrite `build_weapons.py` in the new style. Shared helpers move to `oa_real.py` / the new materials library. Never commit the high models or bake scratch files: only the scripts, `.bytes` and the PNG texture sets.

---

## 5. What Fable makes, in order

Order chosen so the most visible things land first if the Fable allowance runs out.

1. **Rifle** (the starting and most-held gun) as the pilot: proves the texture pipeline end to end, first person and on an enemy.
2. **Trooper** (`Enemy`): rebuild with textures and honest joints; the most common enemy and Faction A's Line.
3. **Pistol, Smg, Shotgun, Sniper** (the gun ladder's order of appearance: Pistol, Shotgun, Sniper, Smg).
4. **Runner, Bruiser, Marksman**: realistic rebuilds; each keeps its helmet shape, bulk, back outline and stance so kinds still read by shape.
5. Faction A's own suits and guns (`EnemyScoutA`, `EnemyLineA`, `EnemyHeavyA`, `SmgA`, `RifleA`, `HeavyGunA`) are in `faction-a-and-melee-models.md`. Do them in the same new style after the above, or in their own thread.

Each step ends with a before-and-after picture for Matthew. Nothing replaces the game's files until he has seen the pilot (the rifle and its pictures) and said the level is right.

**Thursday, side by side with the alien creatures job.** The 9 AM ET Thursday Fable job makes the alien creatures, in its own thread, and asks Matthew before creating. Both need the same textured pipeline (section 3) and the same materials library, so: section 3 lands before Thursday; the creature thread and this one share `oa_materials.py` and do not each write their own; they edit different build scripts, so they do not collide. Fable's allowance is shared, so if it runs short, Matthew chooses which goes first.

---

## 6. ChatGPT 6.2 Astra's attempt

Matthew wants Astra to try too. Astra gives text and code (pictures through its images tool); it does not make game-ready models by itself. Its prompts are in `/mnt/project-files/orbital-ante/chatgpt/astra/astra-realistic-enemies-guns-prompts.md`: a critique of the current models, reference boards, concept turnarounds, and a Blender Python attempt at one gun and one body that writes to a separate folder, never over the game's files. Whatever it brings back is compared side by side with Fable's at the same distances and in the same light, and Matthew picks.

---

## 7. Unverified, and how it will be checked

- Nothing here has run in Unity. The textured shader path, import settings, memory and frame cost all need Matthew's Deck (`tools/check-in-unity.sh`, then a look at a trooper at 5 m and the held rifle).
- Texture memory figures are estimates from the compressed sizes, not measured.
- Whether CC0 texture sites are reachable from the cloud is not checked yet.
