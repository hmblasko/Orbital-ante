# Look pass 1 report - the new visual style, foundation

Status: **built, not yet run in Unity.** The colour rules are plain maths and are tested here (13 new rule tests; 886 rule tests in all run and pass outside Unity). The shaders have been compiled for syntax with a stand-in for Unity's shader library, which is not the same as Unity accepting them. Nothing in this pass has been seen on a screen. The visual quality gate cannot score it until real captures exist.

This is a new pull request on a new branch, `claude/look-pass-1`, stacked on `claude/milestone-7-ship` (pull request #6), so it contains the ship.

The picture `look-pass-1-mockup.png` is **not a screenshot of the game**. It is a made-up test scene drawn here by a small program using the same sums as the new shaders, with today's lighting on the left and the new look on the right. It was used to tune the numbers and shows the direction of the colour and light only. The game's own ground, rocks and ship will look different.

## Why

On 13 Nov 2026 Matthew ran the game for the first time on the Steam Deck, said it looked like a PS2 game, and asked for a look like Outer Wilds. That is read as original art in that spirit: warm, painterly, soft, atmospheric, hand-made. Nothing is copied. The full direction, and why the game looked old, is in `docs/art_direction.md`.

## What changed

**A real sky.** The sky was one flat colour. It is now a ball drawn round you: the planet's sky colour deep overhead, paling to its haze colour at the horizon, with a glow round the Sun. With the Sun near the horizon the horizon turns peach and coral, the top of the sky leans violet, and the sunlight on the ground turns amber. Each planet keeps the two colours it already had.

**Deep space is no longer plain black.** A dim band of galaxy light crossed by dark dust, and a few faint clouds of teal, violet and rose, sit behind the stars. It is the same every run, like the stars, and turns with them. It is kept dim so stars and planets stay the brightest things.

**Softer, warmer light on everything outdoors** (ground, rocks, trees, the outside of the ship). Light wraps a little way into the shadow. The edge of every shadow glows warm. Shadows are filled with the colour of the sky instead of grey. Sunlit ground bounces warm light up under overhangs. Edges that turn away from you pick up a rim of sky colour.

**Painted surfaces.** Flat ground colour is broken into big patches that drift warm and cool and smaller dabs of lighter and darker paint. The small dabs fade with distance so far ground does not shimmer.

**Air with depth.** Far-off ground used to fade into one flat haze colour. It now fades into the sky that is actually behind it, so hills melt into the horizon and glow toward the Sun.

**Planets from space.** The line between day and night is softer and glows warm. The rim of air turns sunset colours along that line and lights up when you look through it toward the Sun.

**Water mirrors the sky**, more strongly the flatter you look along it, with a soft sheen and a bright core of glitter.

**A finish over the whole picture.** Bloom (the Sun, stars a little, the entry ring, cabin lamps, lava, lightning and glitter now glow), tone mapping (bright light rolls off instead of clipping), a colour grade (a little more contrast and colour, slightly warm, teal shadows, amber highlights), a light vignette and smoothed edges.

**One place to tune it.** A new asset, `Assets/OrbitalAnte/Resources/Config/LookConfig`, has a dial for each of these. The readout (top left) has a new line, `Picture finish: on`, or the reason it is off.

**The combat arena** gets the same sky and the same finish. Its ground, walls, mannequins and guns still use Unity's standard material.

## What did not change
- The ship's cabin. Its baked light is left exactly as it was (it only gains the finish over the picture).
- Any shape. Characters and weapons are still placeholder capsules and boxes. Rocks are still the generated lumps. Those need models made in Blender: pass 2.
- Nothing casts a shadow yet: pass 3.
- No gameplay, physics or controls.

## Which principles it serves
- **Discovery that matters (4)** and Milestone 6's bar "each world is recognisable at a glance": a planet's own colours now fill its whole sky and its shadows.
- **Varied intensity (8):** noon, sunset and night now feel different from one another.
- **Learnable challenge (3) and legible risk (5):** nothing here may make things harder to see. The design review flagged night as the main risk; fill light at night was raised in answer. This needs your eyes (see the checklist).
- **Evidence-based iteration (10):** problem: the game reads as PS2. Hypothesis: the biggest causes are the flat sky, grey shadows, no finish and jagged edges, all fixable in code before any new model is made. Smallest test: this pass. Acceptance: you no longer say PS2 when standing on a planet at sunset and when looking at a planet from orbit; the readout still says OK on the Deck. Protocol: the checklist below.

## Choices you should know about
1. **"Like Outer Wilds" is read as "in that spirit, with original art".** If you meant something closer, say so and say which parts.
2. **This replaces the "TF2 / Gang Beasts" line** in the build prompt's art section as the look to aim for. TF2 stays for humour, gibs and the UI.
3. **Code first, models second.** The sky, light and finish change every planet at once, so they came before any Blender work.
4. **Edge smoothing defaults to 2 samples**, not 4, on the Deck performance review's advice. 4 looks better; try it.
5. **Sunsets are warm on every planet.** Real sunsets on Mars are blue. Easy to give each planet its own sunset colour if you want that.
6. **The ship's hull is painted too** (big soft patches of slightly warmer and cooler white). If it looks dirty rather than hand-made, it is one number to turn off for the ship.

## What was tested, and how
- **Rule tests: 886 run, 886 pass** (873 before, 13 new), with `dotnet test` outside Unity. The new ones cover: sunset strongest with the Sun on the horizon and absent at noon and at night; the sky deeper overhead than at the horizon; warm horizon and warm sunlight at sunset; no sunset or Sun glow in a storm; one colour all round inside cloud or water; the sky fading to black at the top of the air with no jump in the Sun's glow; no colour ever negative; deep space the same picture every time, dim enough for stars, not plain black, and with no seam where the picture wraps.
- **The game's scripts compile** against Unity reference assemblies (an older Unity's, with Unity 6 names mapped back).
- **Each shader's own code compiles** (20 shader stages) using a small stand-in for Unity's shader library.
- **The names used to reach bloom, tone mapping and the rest** were checked against the source of the render pipeline version the project uses.
- **The deep-space painter was run for real**: 0.2 s on a desktop.

## What is unverified
- **Everything you would see.** No shader has been compiled by Unity; no frame has been drawn.
- **Whether Unity accepts the shaders.** If one is refused, the game falls back to Unity's standard shaders and the words `SIMPLE SHADERS` appear in the readout. That would look worse than before this pass, so please tell me if you see it.
- **Whether the finish switches on.** It is set up by name at run time. If a name is wrong the game still runs and the readout says what was missing.
- **Frame time on the Deck.** Estimates are in `docs/performance_budget.md` under "The look". None is a measurement.
- **PlayMode tests** have not been run with these changes.
- **The visual quality gate:** every target is still BLOCKED, because no capture has run in Unity.

## Agent review

Three reviews read the code before it was committed. None of them ran anything in Unity.

**Unity reviewer.** Checked every name used to reach the render pipeline against its source (all correct), the sky ball's draw order and culling, the meshes that lack the "which way is up" data, static resets and the PlayMode tests (none should be affected). Fixed from its findings: veiled bodies and stars now fade into exactly the sky the ball draws, so they do not show as dark specks near the Sun by day; a new camera is told to draw the finish before its first frame, not up to half a second later; the bloom threshold was raised from 0.95 to 1.25 so pale sunlit ground does not glow all over; geyser and smoke lumps are left unpainted (paint would have swum across them as they moved); cameras now discard bad pixels before bloom and dither dark gradients; the arena's sky ball was made smaller so it cannot be clipped; tidier clean-up. **Left open:** the game switches "bright picture" (HDR) and edge smoothing on in the render pipeline asset while it runs, and in the editor that setting stays changed after you stop Play. Harmless, but Unity may show the asset as modified. It could not check whether Unity resolves the shared shader file's path, nor whether a built game strips the settings reached by name.

**Deck performance.** Its estimates are in `docs/performance_budget.md`. Fixed from its findings: edge smoothing defaults to 2 samples rather than 4; the small dabs of paint are skipped where they are too far to show, paint is skipped where the far-away picture has taken over, and the baked cabin skips paint, light and haze altogether; bloom stops one step sooner. **Left as follow-ups:** drawing the sky ball after the ground (saves an estimated 0.2 to 0.5 ms), working haze out per corner instead of per dot, skipping water ripples far away.

**Game designer.** Judged the direction coherent with the game's tone. Fixed from its findings: night fill light on walls and overhangs was raised, since night readability was the main risk; characters and weapons (Unity's standard material) now get the amber sunset sunlight and fade toward the horizon colour, so they sit closer to the ground behind them; the deep-space dial is capped so it cannot drown the stars. **Left for later passes:** airless worlds and horror spaces having their own harder or colder light; a list of reserved "meaning" colours (entry ring blue, rarity colours) that the grade must not bend. Its first-run questions are folded into the checklist below.

## How to see it on the Steam Deck

1. Close Unity if it is open.
2. Open a terminal (Konsole) and type these four lines, pressing Enter after each:
   1. `cd ~/Orbital-ante`
   2. `git fetch origin`
   3. `git checkout claude/look-pass-1`
   4. `git pull`
3. Open Unity Hub and open the project (the `unity` folder) as usual. Unity will take a minute or two to take in the new files.
4. Look at the Console tab at the bottom. If there is any **red** line, stop and send me the first red line.
5. Press the Play button at the top.
6. Start a run in the solar system from the menu as you did before.
7. Look at the top-left readout. Find the line that starts `Picture finish:`. It should say `on`. Note the `Budget` line too (OK, TIGHT or OVER) and the frames per second.
8. Go through the checklist below.

To go back to how it was: close Unity, then in the terminal type `cd ~/Orbital-ante` and then `git checkout claude/milestone-7-ship`.

### What to look at (please tell me what you see; a photo of the screen is fine)
1. Does the readout say `Picture finish: on`? Does it say `SIMPLE SHADERS` anywhere?
2. What are the frames per second and the Budget line, seated in orbit and standing on a planet?
3. In space: is the sky behind the stars softly coloured rather than plain black? Does the Sun glow? Can you still judge your drift from the stars, or is the colour distracting?
4. Looking at a planet from space: is the line between day and night soft and warm?
5. On a planet by day: is the sky a gradient? Do far hills fade into the horizon?
6. Wait for or fly to sunset: does the horizon turn warm?
7. At night, with the lamp off and then on: can you still read the slopes of the ground?
8. Landing in haze or a storm: do you see the ground in time?
9. Is the blue entry ring still clearly blue? Does anything look smeared by glow, or are the corners too dark?
10. The outside of the ship: does the hull look hand-painted or dirty?
11. The big one: does it still feel PS2? If so, what is the first thing your eye lands on?

### To try the dials
1. In Unity's Project window (bottom), open `Assets` > `OrbitalAnte` > `Resources` > `Config`.
2. Click `LookConfig`. If it is not there, use the top menu: `Orbital Ante` > `Set Up Project`, then look again.
3. The dials appear in the Inspector on the right. Hover over a name to read what it does.
4. Change a dial, then press Play again (changes take effect when a run starts).

## What comes next
1. Your eyes on this pass, and tuning from what you report.
2. Pass 2, models made in Blender: the outside of the ship first, then rocks and landmarks, gloves and tools, weapons.
3. Pass 3: soft shadows near you, clouds, and the same light on characters and weapons.
