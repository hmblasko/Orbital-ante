# Handmade feel, pass 1 (22 Nov 2026)

Matthew asked to keep making the game "not seem like it was created by AI". This pass looked through the playtest branch for the habits that give generated or procedural work away, and fixed the ones that can be fixed in code without new models (the Tripo thread is remaking the models).

## What changed

### 1. Rocks, trees and plants sit in the ground one by one
**Problem.** Every scattered thing stood dead upright along the planet's "up", sat on top of the ground with its base showing, kept the proportions it was modelled with and took the one rock colour of its world. Rows of identical, perfectly upright stones are the clearest sign of a machine placing things. The scatter is also placed on the ideal ground, which can be a little above or below the tile really drawn, so a rock could hover by its edge.

**Change.** `Planets/NaturalPose.cs` (plain C#) gives each placement, from its own seeded numbers:
- a tip away from upright (loose stones up to 14 degrees, most only a little; trees 4, palms 9, dead trees 11, small plants 7; hoodoos, spires, arches, basalt columns and balanced rocks 2.5 or less),
- a stretch along each axis (stones up to a quarter wider or narrower; trees change girth only, so the trunk capsule stays round and the height that decides what is solid is untouched),
- a sink into the ground as a share of its height (stones 10 to 24 per cent, slabs and logs a little more, trees 1.5 to 3 per cent, nothing that floats),
- one of three shades and three warmths of its colour (plus or minus about 9 per cent), rounded so the whole world shares nine materials per colour rather than one per rock.

`PlanetFeatures.Spawn` applies it. The same rock sits the same way every time for a seed.

**Serves** constitution 1 (fun core: worlds worth looking at) and 4 (discovery that matters: a hillside reads as a place, not a pattern).

### 2. The colour grade only seasons the picture
**Problem.** The picture finish pushed contrast and saturation up by 10 and split it teal in the shadows and amber in the lights (0.55) over a dark vignette. That teal and amber look is the house style of generated images and stock trailers. `docs/art_direction.md` already said the grade would be turned down once the realistic models were in, and they now are.

**Change.** `Look/GradeDefaults.cs`: contrast 5, saturation 0, split tone 0.18, vignette 0.1. The Look asset on Matthew's machine was made with the old numbers, so it takes the new grade by itself the first time it loads (`gradeVersion`, the same way the planets' defaults move on). Bloom, exposure, warmth, paint, shine and light band are untouched.

### 3. Sounds heard over and over never repeat exactly
**Problem.** Ten sounds the player hears hundreds of times a run played one clip at exactly the same pitch and level every time: menu select and back, the ship's confirm beep, melee pick-up and drop, and the melee hit sounds of the frying pan, mailbox, butterfly knife and laser sword. A sample repeated exactly is one of the quickest tells of something assembled rather than performed.

**Change.** A small pitch spread (0.3 to 1 semitone) and level spread (0.8 to 1.5 dB) on each, in both `tools/audio/sfx/*.py` and `audio_manifest.json`, so a rebuild keeps them. No sound file changed. The hit ding, kill chimes and reward sounds stay exact on purpose: their pitch is how the player learns what happened.

### Looked at and left alone
- **Wording.** The tips, title screen and break pages are already plain and specific (real numbers, real key names, no filler). Nothing generic enough to change.
- **Fonts.** Every label is B612 through `UiKit`; no default font is left.
- **Placeholder names.** Factions are A/B/C and the melee, perk and item names are already plain game names. Nothing to change without Matthew naming things.
- **Most other sounds** already have several takes and pitch spread.

## Tests
- New rule tests `HandmadeFeelTests` (9): same placement same pose; boulders differ and are bedded; nothing leans past 15 degrees or stretches past 30 per cent; landmarks stay upright; trees keep a round trunk and their height; floating things are never sunk; tint stays in range; the grade stays low; the ten sounds vary.
- `dotnet test tools/rules-tests`: 2,672 passed, 1 skipped (the baked-light test that needs Unity), 0 failed.
- The runtime code compiles against Unity reference assemblies (only the two known `excludeLayers` lines from the older reference set complain).

## Not verified (nothing here has run in Unity)
- How the tipped, bedded rocks look on the Deck, and whether any sink too far into a steep slope or tip into a neighbour.
- The new grade's look on each world and in the cabin. If it looks washed out, raise Split Tone or Saturation on the Look asset a little, then set Grade Version to 9999 so it stays.
- The few extra material switches (estimated under 0.1 ms on the Deck; see `docs/performance_budget.md`).
- How the sound spreads feel.
- The felling-trees work on the open bases branch has not been merged with this; whoever merges it should check a felled tree starts from the tipped pose.

## Matthew's check on the Deck (about 10 minutes)
1. In Desktop Mode open Konsole and run `cd ~/Orbital-ante`, then `git fetch`, `git checkout claude/playtest`, `git pull`.
2. Open the project in Unity Hub and wait for it to finish importing.
3. Press Play, start a New run and lift off.
4. Land on the Moon or Mars near rocks. Walk up to a few: they should look half buried and tipped different ways, not lined up upright.
5. Fly to Earth and land near trees: they should lean slightly and differ in thickness.
6. Look at the sky and shadows: the picture should look less orange and teal than before, closer to a photograph.
7. Open the pause menu and move up and down a few times: the clicks should differ very slightly.
8. Tell Claude anything that looks wrong, such as a rock floating, a tree tipped badly, or the picture looking grey.
