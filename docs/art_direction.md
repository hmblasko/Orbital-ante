# Art direction

Status: **direction set on 13 Nov 2026. Look pass 1 is built in code and has not been seen running in Unity yet.**

## Where this came from

On 13 Nov 2026 Matthew ran the game for the first time on his Steam Deck and said it looked like a PS2 game. He asked for "an aesthetic just like Outer Wilds".

That is read as: **a stylised, hand-crafted look with original art, in the spirit of that game.** Warm painterly colour, soft lighting, rich skies and atmospheres, chunky readable shapes, materials that look made by hand, and a good finish over the whole picture. Nothing is copied from Outer Wilds: no assets, characters, planets, props or designs.

This replaces the "like TF2 and/or Gang Beasts" line in section 3 of the build prompt as the look to aim for. The rest of section 3 still holds: clear silhouettes, never boxy, never deliberately retro, real reference images before designing an asset, destructible things split along natural parts. The TF2 tone stays where the prompt asks for it: humour, gibs, UI.

## Why it looked PS2

Found by reading the rendering code as it stood at Milestone 7:

| What was there | Why it reads as old |
|---|---|
| The sky was one flat colour | Real and painted skies are gradients. A flat sky is the single strongest "early 3D" signal. |
| Plain lighting: brightness straight from the angle to the Sun, plus one grey fill colour | Shadows went grey and dead. No warmth, no colour in the dark. |
| No finish over the picture: no bloom, no tone mapping, no colour grade | The Sun and glowing things were just white shapes. Bright areas clipped. |
| No edge smoothing | Every edge was a staircase. |
| Far-off ground faded into one flat haze colour | Distance had no depth or air. |
| Flat colour on every surface | Large areas read as untextured plastic. |
| No shadows cast by anything | Things float. (Not fixed in pass 1.) |
| Placeholder shapes for characters and weapons | Capsules and boxes. (Not fixed in pass 1: these need real models.) |

## The pillars

### 1. Palette
- **Warm light, cool shadow.** Sunlight is a warm cream. Shadows are filled by the sky, so they lean blue-violet in space and take the colour of the air on a planet. Shadows are never grey.
- **Each world keeps its own two colours** (sky and haze, already set per planet). The look deepens the sky colour overhead and pales it at the horizon.
- **Sunrise and sunset matter.** With the Sun near the horizon the horizon glows peach and coral, the top of the sky leans violet, and sunlight itself turns amber.
- **Deep space is not plain black.** A dim band of galaxy light and a few faint clouds of teal, violet and rose sit behind the stars. Dim enough that stars and planets stay the brightest things in the sky.
- **Saturated, not neon.** Colours are pushed a little past natural and then held there.

### 2. Lighting
- **Soft.** Light wraps a little way round into the shadow, then is firmed into a soft-edged band, so shapes read as rounded and solid.
- **A warm edge to every shadow.** The line between light and dark glows warm, as light does through air or skimming rough ground. Whole planets seen from space get the same warm line between day and night.
- **Sky light from above, bounce light from below.** Upward faces catch more sky. Sunlit ground throws warm light back onto overhangs and shadowed walls.
- **A rim of sky colour** where a surface turns away from the eye.
- Baked interior light (the ship's cabin) is untouched by all of this. It keeps its own path-traced light.

### 3. Sky and air
- A **gradient sky** drawn on a ball round the viewer, with a **glow round the Sun**.
- **Far-off things fade into the sky that is behind them**, not into one flat colour, so hills melt into the horizon and the horizon toward the Sun glows.
- The **rim of air** round a planet seen from space turns sunset colours along the day/night line and lights up when you look through it toward the Sun.
- **Water mirrors the sky**, more strongly the flatter you look along it, with a soft sheen and a bright core of glitter where the Sun catches it.

### 4. Materials
- **Painted, not flat.** Every outdoor surface has its colour broken up the way a painter would: big patches that drift warm and cool, and smaller dabs of lighter and darker paint. Small dabs fade with distance so far ground does not shimmer.
- No photographic textures. Detail comes from shape, colour and light.
- Later passes: hand-painted detail on the ship's hull panels, worn edges, simple fabric and metal responses for the suit and tools.

### 5. Shapes (needs Blender, not in pass 1)
- Chunky, slightly exaggerated proportions. Thick edges, rounded corners, big readable forms, few small fiddly parts.
- Built as if by hand: visible panels, straps, rivets, patches, slightly uneven lines.
- Every asset gets a silhouette check at Steam Deck size before detail is added.

### 6. Finish (over the whole picture)
- **Bloom** on things brighter than white paper in sunlight: the Sun, stars a little, the entry ring, cabin lamps, lava, glitter on water.
- **Tone mapping** so bright light rolls off smoothly instead of clipping.
- **Colour grade:** a little more contrast and saturation, slightly warm overall, shadows nudged toward teal and highlights toward amber.
- **Light vignette.**
- **Smoothed edges** (multisampling: 2 samples by default on the Deck, 4 available).

Every dial is on one asset, `Assets/OrbitalAnte/Resources/Config/LookConfig`, so the look can be tuned in the Inspector without touching code.

## Order of work

**Pass 1: the foundation, all in code (this pull request).** Sky, light, haze, paint, water, deep space, the finish. Changes every planet and the outside of the ship at once.

**Pass 2: assets made in Blender.** On 13 Nov 2026 Matthew asked that the guns and the ship in particular stop looking PS2, so they come first:
1. Weapons: the rifle and the test pistol (first versions built, see `docs/reports/look-pass-2-guns.md`).
2. The ship's outside: panel lines, painted wear, chunkier legs, engines and dish.
3. Rocks, trees and landmarks: a small hand-shaped set per planet family to replace the generated lumps nearest the player.
4. The suit's gloves and tools (what is on screen all the time on foot).
5. The mannequin's replacement (Milestone 12 asks for one proper enemy model).

How Blender models are made: each is built by a script in `tools/blender/` (so it can be rebuilt and changed without clicking), coloured per corner with painted wear and grime (no texture files), and written to a small file in `Resources/Models` that the game reads itself. They are drawn with the same shared look as the ground and the ship.

**Pass 3: depth.**
1. Soft shadows from the Sun for things near the player (ship, rocks, characters), within the Deck budget.
2. Clouds: a painted cloud layer on worlds with weather, and proper cloud tops on the gas giants.
3. The same lighting on characters and weapons, which still use Unity's standard material.
4. Ground detail close to the feet: pebbles, tufts, cracks.

## Rules for anyone adding to the look
- New shaders include `OA_Look.hlsl` and take their sky, light and haze from it. Do not write a second lighting model.
- Never fade to a flat fog colour. Use `OA_ApplyHaze`.
- Colours are picked by eye and converted to linear in one place (`SkyGlobals.Linear`).
- Anything that shines by itself is drawn brighter than white so it blooms, rather than given a glow sprite.
- Every new cost goes in `docs/performance_budget.md`.
- Nothing is called finished on looks until it has passed the visual quality gate on real captures.
