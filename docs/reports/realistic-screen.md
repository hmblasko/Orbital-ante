# Realistic look, stage 5: the screen

27 Nov 2026. Matthew (26 Nov): refine all parts of the game so it does not look AI-made, "This includes what the player sees on their screen". On 27 Nov: "You make the executive decisions". This stage was built on that footing, with no card asked.

**Nothing here has been run in Unity.** The picture `docs/art/previews/menu_before_after.png` is an offline mock-up made from the menu code's numbers and the game's fonts, not game footage.

## What changed

1. **The Hull stencil plates are brought up to date.** Matthew chose them for the on-foot screen on 26 Nov (PR 95, report `docs/reports/player-hud.md`). That branch no longer merged into `claude/playtest`, so this branch carries it whole, joined with the bonus missions and critique loop work:
   - The roll count shows as ON A ROLL above the number on the ante plate (never the word "streak"), in the score's gold.
   - The bonus mission line is top right under the task plate and stays in a fight when the task plate steps aside.
   - While ante 1's clock waits on the pad the line under the ante plate says CLOCK STARTS AT LIFTOFF.
   - The ante's note line moved below the ante plate, and the NEW GUN banner below that, so neither lies over a plate.
   - The readout is hidden while the map is open (the map's title uses the same corner).
   - Outside the ship, the ship's damage list moved up to sit under the bonus line.
   - Orange on the readout now means "act on this" only: the clock's colon follows its digits.
2. **The menus match the plates.** Pause, title, death, ante break, controls, collection and the audio showcase are built from `UiKit.Card`, `Button` and the new `Heading`, so one change restyled all of them: dark square-cornered plates with a thin edge and screw heads in place of the rounded brown card with a thick orange border; headings stencilled and straight; a chosen button still fills orange, and its words now go dark so they can be read.
3. **Cheaper to draw.** "+points" lines and tips fade on the renderer and no longer rebuild their words each frame; the speed line lost its outline (it is rewritten several times a second).

## Why

The screen mixed three looks: bolted plates in play, rounded warm cards in menus, and tilted headings. One family reads as designed by one hand. Constitution 7 (cognitive load: one meaning for orange, nothing drawn over anything else) and 3 (no hidden rule: what starts the clock is said).

A judgement call to check: Part B section 13 asks for whimsical, somewhat goofy menus, "never dull". The stencil face, screws and flip tiles carry character, but the tilt and the warm brown are gone. The humour is left to the words. Easy to put a touch back if the menus feel too dry on the Deck.

## Tested

- Rule tests: 2,791 pass, 1 skipped (`dotnet test tools/rules-tests`). `HullReadoutTests` changed for the notice strip's new cap.
- The runtime code compiles offline against Unity reference assemblies.

## Agent review

- **game-designer:** found the lost liftoff words, orange used for both reward and warning, a possible overlap of the note with the bonus line, the damage list left at its old height, stale position notes and a leftover brown tab. All fixed. It also raised the "never dull" question above.
- **deck-performance:** nothing large. Fixed: per-frame colour writes on fading lines, the outline on the speed line. Left: glyph hitches the first time a size is used, small garbage on the clock and ammo words, round blocks rebuilt on a gun switch, plates drawn as edge under face (about 0.37 of a screen of blending, a guess). Budget row added.
- **unity-reviewer:** found the note line lying over the ante plate, the map title sharing the plate's corner, the task line unable to shrink, and the notice strip wider than the gap between the lower plates. All fixed. Left: with no gun plate, the fallback Focus bar sits under the notice strip. It confirmed the fonts have the signs used, and that the menu changes hold up on reading.
- Codex was not asked: its review allowance ran out on PR 99.

## Not verified (needs the Deck)

1. Open the pause menu, the title screen, the death page and an ante break: plates, screws, stencil headings, the chosen button's dark words. Move the selection with the stick and the mouse.
2. On foot: the four plates, the bonus line under the task plate, a long note (it should sit under the ante plate), NEW GUN banner, the map opening and closing.
3. Whether the two new fonts imported (if plate text is in the old lettering, they did not).
4. Frame time with the plates on.

## Not done

- The seat view (its redesign is PR 72, which has its own open card).
- The practice range still shows health on the old arc.
- The star chart and the envelope keep their own looks.
