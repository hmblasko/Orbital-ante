# Visor gauges for air and health, and a new font (15 Nov 2026)

Matthew asked: "I really want more interesting display of health, oxygen. Also, the font of words should be more interesting and easy to read."

## What was shown first

Three offline mockups at the Deck's 1280 x 800, over an offline terrain picture (not game footage), plus a sheet of the fonts at their real sizes. They are in the project folder `orbital-ante/hud/`:

- **A, Visor arcs** (B612): air and suit as curved gauges in the lower corners, like markings on the helmet glass. Recommended and built.
- **B, Heartbeat and breath** (Chakra Petch + Barlow Semi Condensed): a medical monitor with a heart trace and a breathing wave.
- **C, Tank and plates** (Russo One + Atkinson Hyperlegible): an air tank with bubbles and hexagon armour plates at the bottom centre.

Matthew's choice was asked on a card; A was built while waiting. If he picks B or C, the rules in `VisorReadout` stay and only the drawing changes.

## What changed

- **Font.** Every label in the game is now B612 (Regular and Bold), a typeface drawn for aircraft cockpit screens to stay readable when small, at a glance and in poor light. It is under the SIL Open Font Licence 1.1 (logged in `docs/asset_licence_ledger.md`, licence text beside the files in `Resources/Fonts`). `UiKit.Font` loads it, with Unity's built-in font as the fallback. Bold labels use the real bold file (`UiKit.SetStyle`), not Unity's faked bold. The cockpit dash's words go through the same call.
- **Halo.** Labels over the world get `UiKit.Halo` (a thin dark outline plus a shadow) instead of a shadow alone, so they read over a bright sky or sand.
- **Air** (outside the ship, and aboard while the suit fills): an arc round the left of a large per cent in the lower left, filling from the bottom, with ticks every tenth and "4:12 left" under it (time left at a resting breath). While the jets are burning air the line says "jets burning air" instead, because a resting-breath time would then be many times too long, and the arc turns amber. It turns red and blinks when air is low; "NO AIR 9" counts the last breath.
- **Suit (health)**: an arc of ten plates round the right of a large per cent in the lower right. A plate goes dark only when it is used up, so a plate still showing always has something left; the last one still on turns amber once it is worn into. At 35% or less all plates turn red and the line under the number says "armour failing" ("no armour left" at zero); above that there is no line, because the plates say it. A fresh hit flashes the plates. Shown whenever you are outside (it used to appear only as "SUIT 87%" text once hurt). The combat arena's health bar is now the same plate gauge.
- **Visor shade**: outside the ship, the lower corners of the view are softly darkened like the edge of a helmet's glass, so both gauges sit on something darker than the sky. Dial `Visor Shade` on the Suit asset; 0 turns it off.
- Jet fuel stays an upright bar, now just outside the air arc; speed and tether moved to the right of the air gauge's words; the suit's notice moved up a little.
- The arena's reload word is now "RELOAD", so it stays clear of the plates at the same size.

## Files

- New: `Scripts/Runtime/UI/VisorReadout.cs` (plain C# rules), `Scripts/Runtime/UI/VisorGauge.cs` (the drawing), `Tests/EditMode/VisorReadoutTests.cs`, `Resources/Fonts/B612-Regular.ttf`, `B612-Bold.ttf`, `B612-OFL.txt`.
- Changed: `UiKit.cs` (font, bold, halo, ring sprite), `SuitHud.cs`, `SuitConfig.cs` (`visorShade`), `Hud.cs`, `CockpitDash.cs` (one line: bold through `UiKit.SetStyle`).

## Constitution

Principle 7 (manage cognitive load): two numbers you need most, each with one shape that is learnt once and read from the corner of the eye. Principle 3 (learnable challenge, no hidden rules): the plates show armour honestly (never a plate that is not there), the air clock says it is at a resting breath, and the notice still says when the jets are burning air. Principle 5 (legible risk): the colour steps (amber worn, red danger) match the rest of the HUD.

## Tested

- The new rules: `VisorReadoutTests`, 8 tests, pass with `dotnet test tools/rules-tests` (plates never show more than there is, never rise as the suit wears down, amber only once worn, per cent never reads 0 while anything is left, the air clock in minutes and seconds and the last-breath countdown). Whole suite: 2,178 passed, 1 skipped.
- All runtime scripts compile against UnityEngine.Modules 2021.3.33 and LethalCompany.GameLibs.Steam (only the known Unity 6 name and IHittable clash errors from those libraries).

## Not verified (needs Unity on the Deck)

- Nothing here has been run in Unity. How the arcs, plates, shade and font look in the game, and whether the arcs point the right way, is unchecked.
- B612 at the same size: bold text measures the same width as the old font (0.7% narrower over a test sentence), regular text about 5% wider, and lines are about 8% taller. Labels overflow rather than clip, but a few menu rows might look tight. The first thing to check is the PlayMode test `ControlsPageTests.BothMapsAndBothDeviceTabsFitTheDeckAndContainEveryBinding`.
- Whether the alarm colours and the halo read well on the Deck's screen, and the canvas cost with both gauges up (Profiler: GC Alloc, `Canvas.BuildBatch`, draw calls). Each label uses one of two font files, so mixing bold and regular can add a draw call or two.
- The arena's health and the suit's armour outside are two separate health pools that are both labelled SUIT. They follow the same rule (`PlayerHealth`), only in different places.
- The PlayMode tests in `SpacewalkTests` that read the suit readout should still pass (the notice and `Shown` are unchanged), but have not been run.

Playtest protocol (principle 10): problem, health and air were a small ring and a text line that were easy to miss; hypothesis, corner gauges that change shape (plates going dark, the arc emptying) are read without looking away from the fight; smallest test, step outside on Mars in daylight, get shot to below half and let the air run under a quarter; acceptance, both numbers and the time left can be read on the Deck in one glance, the plates going dark is noticed during a fight, and nothing overlaps the crosshair, the notice or the ammo count.

## Agent review

Two reviews, reading the code only (unity-reviewer; game-designer and deck-performance together).

- **Unity reviewer.** Confirmed the arc directions (left arc from about half past six, clockwise through nine; right from about half past five, anticlockwise through three, plates go dark from the top), the mirrored corner shade, the font import and bold handling. Fixed from its findings: the gauge numbers and words were tinted on top of a coloured base, so alarm red came out muddy (now white underneath, colour set once); colour writes are skipped when unchanged; the arc rounds up like the number, so a last scrap of air still shows; generated sprites use full-rect meshes. Left: commit the `.meta` files Unity makes for the fonts after first import; the width check above. The licence file stays in `Resources` on purpose: the OFL wants it to travel with the font.
- **Game designer.** The air clock misled while the jets burn air (it said 8:00 when a full tank lasts under a minute of full push): fixed as above. The suit was said three ways (plates, per cent, "7 of 10 plates", which also disagreed with the mockup's "6 of 10"): the plates line is gone. The ammo crowded the arena gauge: the gauge moved in (380 px from the edge). Raised for Matthew: whether the arena health should be called something other than SUIT.
- **Deck performance.** The plates line was rebuilt nearly every frame while health came back: it is now one of three fixed strings. The reload word changed font size, which can grow the font's glyph sheet mid-fight: it no longer does. Outline plus shadow drew each label six times: only the two big numbers keep the outline. The corner shades were shrunk (440 x 270 each, about 23% of the screen, mostly faint).
