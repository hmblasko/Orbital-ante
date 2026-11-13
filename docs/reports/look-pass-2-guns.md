# Look pass 2 report, part 1 - the guns

Status: **built, not yet run in Unity.** The models are real (made in Blender here, pictures below). The code that loads them compiles against Unity reference assemblies and its rules are tested outside Unity (892 rule tests run, 892 pass; 6 are new). Nobody has seen these guns in the game yet.

The pictures in `docs/art/previews/` are **Blender's own renders of the models**, not screenshots of the game. They show the true shapes and colours. The game lights them with its own shader, so light and shadow will differ.

## Why

On 13 Nov 2026 Matthew said the game must not look PS2, "including the guns and ship". The guns were boxes and cylinders. This is the first Blender work of pass 2; the outside of the ship is next.

## What changed

**Two new gun models, original work.**
- **Rifle** (the AK-47 spoof from Milestone 3): gunmetal body with a ribbed lid, warm orange wood (handguards, grip, stock with a cheek rest), a fat curved magazine with a mustard band, a slanted muzzle brake, a fat orange front sight post, bare-metal rivets, a round charging knob, and mustard tape wrapped a little crookedly round the stock. About 7,800 triangles.
- **Pistol** (the test pistol): a big pale steel slide with grip grooves over a dark frame, wooden grip panels with a screw, an orange front sight, a mustard magazine floor. About 3,900 triangles.
- Every edge is rounded. Colour is stored at each corner of the model: lighter worn paint on outward edges, darker grime in creases, a slight top-to-bottom gradient. There are no texture files.
- The muzzle, the support hand hold and the magazine sit exactly where the old box guns had them, so tracers, hands and reloads line up as before. The magazine is its own piece and still vanishes for a moment during a reload.

**A way of making models that needs no clicking.** Each model is built by a script (`tools/blender/build_weapons.py`, with shared helpers in `tools/blender/oa_blender.py`). Running the script in Blender writes the model files into `unity/Assets/OrbitalAnte/Resources/Models/`. The game reads those files itself (`ModelFile`, `ModelLibrary`). Matthew does not need Blender installed: the finished files are in the repository.

**The guns use the game's shared look**: the same soft warm-and-cool light, sky fill and haze as the ground and the ship, plus the painted break-up of colour at a smaller size to suit something held in the hand. (The mannequins and the arena's floor and walls still use Unity's standard material: pass 3.)

**Fallbacks.** A model Matthew assigns by hand on a weapon (a CC0 pack) still wins. If a model file is missing or damaged, the old box gun is used and the Console says why.

## Which principles it serves
- **Fun core gameplay first (1):** the gun is on screen the whole time on foot; it is the single most-looked-at object in combat.
- **Learnable challenge (3):** nothing about how the guns work changed. Same muzzle point, same numbers.
- **Evidence-based iteration (10):** problem: the guns read as PS2 placeholder boxes. Hypothesis: chunky rounded shapes with hand-painted colour, under the new light, fix that without textures. Smallest test: the two Milestone 3 guns. Acceptance: Matthew no longer calls the guns PS2 and can still tell rifle from pistol at a glance at Deck size; the readout still says OK. Protocol: the checklist below.

## Choices you should know about
1. **The designs are mine and are a first version.** Tell me what to change: proportions, colours, how silly. Changes are quick because the models are scripts.
2. **The accent colours** (mustard tape and band, orange sight) are there for a hand-made feel. If they read as too toy-like, they are one line each.
3. **No textures.** Colour per corner keeps the guns consistent with the ship and the ground and costs nothing extra to draw.
4. **Final loadout rules are unchanged:** the pistol is still only a test weapon (Milestone 3's note).

## What was tested, and how
- **Rule tests: 892 run, 892 pass** (886 before, 6 new), with `dotnet test` outside Unity. New: a model survives being written and read back; damaged or cut-short files are refused with a reason and never crash; both real gun files have a body, a separate magazine and all three attachment points; muzzles are exactly where the old guns had them and the barrel really ends there; each gun is the expected length and the same on both sides; every triangle faces outward the way Unity expects; each gun is within its triangle cap.
- **The game's scripts compile** against Unity reference assemblies (an older Unity's, with Unity 6 names mapped back).
- **The changed shader's own code compiles** with a stand-in for Unity's shader library.
- **The models were built and rendered in Blender 4.0** from the scripts in this repository.

## What is unverified
- **Everything in the game.** The guns have not been seen in Unity: not in the hand, not dropped, not in an enemy's hands.
- **Whether the gun sits well in the first-person view** (size on screen, whether the stock or tape pokes into view).
- **How the game's light looks on them**, outdoors and with the even indoor light they take inside the ship.
- **Shadows:** the new guns do not cast or receive shadows in the arena (the old box guns did). See the agent review.
- **Frame time on the Deck.** Estimates only, in `docs/performance_budget.md`.
- **PlayMode tests** have not been run with this change.
- **The visual quality gate:** still BLOCKED until a capture runs in Unity.

## Agent review

Three reviews read the work before it was committed. None of them ran anything in Unity.

**Unity reviewer.** Checked the file loading, the shared meshes and material, the material cache, batching, static resets, the magazine's placement and the PlayMode tests (none read gun meshes or materials; no problem found in any of these). It also confirmed by its own parse that the triangles face the way Unity expects, bar ten tiny hidden slivers on the rifle. Fixed from its findings: inside the ship the pistol would have been lit by the Sun straight through the hull (dark on the night side, changing as the ship turns); held guns now switch to an even indoor light whenever you are inside. **Left open:** the guns neither cast nor receive shadows in the arena, because the game's own shader has no shadow pass yet (the old box guns did both). A shadow pass cannot be checked without Unity, and a mistake in it would switch the whole game to `SIMPLE SHADERS`, so it waits for a session that can run Unity or for pass 3. With "simple shaders" ticked the guns are flat and matte.

**Deck performance.** Counted 10 guns at most in the arena and 1 in the solar system. Its estimates and a cap are now in `docs/performance_budget.md` under "Guns": about 65,000 triangles for all guns, 20 draw calls where the box guns took about 100. Fixed from its findings: the rifle's magazine was trimmed. **Left as a follow-up:** dropped and enemy guns need a simpler copy (about 1,500 triangles) before Phase 2 enemy counts; the model reader keeps about 0.4 MB of file data it could drop.

**Game designer.** Judged the guns to fit the direction on paper (chunky, rounded, hand-made, recognisable from their parts) and confirmed the attachment points match the old guns, so shots come from the same place. Fixed from its findings: the rifle's orange sight post was hidden between its ears and is now taller and fatter; small fiddly detail was thinned (three fat lid ribs instead of five, bigger rivets, a thicker cleaning rod). **For your eyes:** the palette; whether the rifle is funny enough or just a faithful chunky AK; whether orange wood is easy to spot when dropped on Mars; the mustard and orange may have to give way when rarity colours arrive in Phase 2. It recommends the pistol stays test-only for now. **Not done yet:** a flat-black silhouette picture and a picture from the player's own eye at Deck size, which the art direction asks for.

## How to see them on the Steam Deck

1. Close Unity if it is open.
2. Open a terminal (Konsole) and type these lines, pressing Enter after each:
   1. `cd ~/Orbital-ante`
   2. `git fetch origin`
   3. `git checkout claude/look-pass-1`
   4. `git pull`
3. Open the project in Unity Hub as usual and wait for it to take in the new files.
4. Look at the Console tab. If there is any **red** line, stop and send me the first red line.
5. Press Play and start the combat arena from the menu.

### What to look at
1. Is the gun in your hands the new rifle (wood, tape, curved magazine) or still grey boxes? If boxes, send me any yellow Console line that mentions "model".
2. Does it sit well in view, or does it cover too much, or does part of it poke into the camera?
3. Fire: do the flash and tracer come from the end of the barrel?
4. Reload: does the magazine vanish and come back?
5. Switch to the pistol. Same questions.
6. Shoot a gun out of a mannequin's hands and look at it on the floor. Does it look right lying there?
7. Does the readout say `SIMPLE SHADERS`? What are the frames per second and the Budget line?
8. The big one: do the guns still feel PS2? What would you change first?

## To change a gun (for whoever does it next)
1. Edit `tools/blender/build_weapons.py`.
2. Run `blender -b -P tools/blender/build_weapons.py -- --preview <a folder for pictures>` (Blender 4.0; leave off everything from `--` on to skip the pictures).
3. Run the rule tests. Commit the script and the two `.bytes` files together.

## What comes next
1. The outside of the ship, made the same way, keeping all its breakable parts, belly entry and baked cabin.
2. Your eyes on the guns, and changes from what you say.
3. ChatGPT's confirmed notes on the guns and the ship brief, when they arrive.
