# Steam Deck performance budget

Status: **targets set, nothing measured yet.** Every number below is a starting budget chosen from the Deck's hardware and screen, not a measurement. The first run in Unity, and then the first run on a Deck, will tell us which ones are wrong.

## The target

| | Budget | Why |
|---|---|---|
| Resolution | 1280 x 800 | The Deck's own screen. |
| Frame rate | 60 frames per second | The Deck screen refreshes 60 times a second. |
| Time per frame | 16.7 ms | 1000 ms / 60. |
| Floor | 25 ms (40 fps) | The Deck's other smooth mode. Anything slower is a visible hitch. |
| Frames over 16.7 ms | no more than 5% | A few slow frames are invisible; a steady stream is not. |
| Frames over 25 ms | no more than 1% | Hitches are what "runs badly" feels like. |

These live in the asset `Assets/OrbitalAnte/Resources/Config/PerformanceBudget` and can be changed there.

On the plain pause pages (pause menu, controls, feel, audio, restart, quit, confirm) the frozen world is drawn every second frame (`PerformanceBudget.halfRateWhenPaused`, `OnDemandRendering.renderFrameInterval` 2; 24 Nov 2026): 30 pictures a second at 60 Hz while input is still read every frame. The ante break, its choices, the star chart and the death page keep full rate. `Apply()` puts it back to 1 on every un-pause. Not measured on the Deck.

## How one frame's 16.7 ms is shared out

| Part | Budget | Notes |
|---|---|---|
| Game scripts | 4 ms | AI, weapons, gore bookkeeping, UI. |
| Physics | 3 ms | Mannequins, gibs, dropped guns. Later: the ship and its parts. |
| Drawing (CPU side) | 4 ms | Preparing what to draw. Kept down by the draw call budget. |
| Drawing (GPU) | fits in 16.7 ms | Runs alongside the CPU; kept down by triangles, lights and effects. |
| Spare | about 5 ms | Headroom for the busiest moments. A budget with no spare is already over. |

## Scene budgets

| | Budget |
|---|---|
| Draw calls (Batches in Unity's Stats panel) | 1,500 |
| Triangles on screen | 600,000 |
| Real-time lights affecting one object | 8 |

Caps already enforced by code (set in `GoreConfig`):

| | Cap |
|---|---|
| Loose body chunks at once | 40 (oldest is recycled) |
| Blood particles | 500 |
| Blood splats | 48 (oldest is recycled) |

Every new system from here on gets a line in this file: what it may cost and what caps it.

## Controls reliability refinement: source review only

| Work | Scope and bound |
|---|---|
| Persistent binding identities | One SHA-256 digest per registered Gameplay/Flight binding during input initialization only; scratch allocations scale with the finite action definitions. No hashing or identity work during play, physics, or rebinding. |
| Controls list | One vertical ScrollRect and RectMask2D while the controls page is open; a 480-unit viewport keeps existing rows inside the 1280 x 800 reference layout. All registered rows still exist; this is scrolling, not virtualization. |
| Controller selection follow | Check the selected object while the controls page is active; reuse four corners. Force layout and adjust scroll only when a different binding row is selected. No new polling outside the controls page. |

No timings, allocation measurements, Unity layout results or Deck frame rates have been collected for this refinement. Check `ControlsPage.Tick`, `Canvas.SendWillRenderCanvases` and `Canvas.BuildBatch` in the Profiler while holding controller navigation and while idle in Controls. See `docs/reviews/controls-persistence-and-access.md` for the acceptance protocol.

## Solar system (Milestone 5): estimates from reading the code, not measured

| | Cap or expected cost |
|---|---|
| Bodies moved each physics step | 150 at most (today 130: Sun, 8 planets, 11 moons, 110 asteroids) |
| Asteroids | 110 (`Asteroid Belt > Count` on the SolarSystem asset) |
| Surface textures | 9 at 384 x 192, 11 moons at 192 x 96, about 5 MB in all |
| Building those textures when a run starts | a pause of under half a second on Deck is the target. Estimated 0.15 to 0.5 s in a build, longer in the editor. Not measured. No loading screen yet. |
| Draw calls with everything in view | about 140 (estimate) against the 1,500 budget |
| Triangles with everything in view | under 150,000 (estimate). Each ball drops to a simpler mesh when it is small on screen. |
| Stars | 1,680 squares in 3 meshes (3 draw calls) |
| Solar system script time per physics step | 0.2 ms (estimate) |
| Flight readouts | words refreshed 8 times a second, not every frame |
| Small thruster flames (`ShipJetFx`, 15 Nov 2026) | 16 cones of 8 triangles, one shared mesh and material; each switched off while its nozzle is idle, so at most 16 extra draw calls while turning hard and none at rest. Script time about 0.01 ms a frame (estimate, not measured). |
| Gravity near worlds (`PullStrength`, 15 Nov 2026) | one `Math.Pow` per body pulling on each mover per physics step (two or three bodies in a neighbourhood); no cost to speak of. |
| Map, while open | 1 ms per frame (estimate), redrawn every frame |
| Shadows | off in the solar system for now: nothing casts one yet |
| Camera far limit | 300 km (the system is about 240 km across) |

Numbers to read off in Unity for this section are listed in `docs/reports/milestone-5.md`.

## Planets (Milestone 6): estimates from reading the code, not measured

| | Cap or expected cost |
|---|---|
| Ground pieces ("tiles") drawn at once | about 150 standing on Earth, the biggest body (counted by a test program, with `Split Distance` 1.2). Each is 2,560 triangles, so about 390,000 triangles exist and roughly half are in view. Hard to exceed: the finest tile size is fixed per body. |
| `Split Distance` on the Planets asset | 1.2. Raising it to 1.7 gives sharper far ground but 237 tiles (about 600,000 triangles). This is the first number to lower if planets are slow. |
| New ground handed to the graphics card per frame | 2 pieces (`Uploads Per Frame`) |
| Ground made solid per frame | 1 piece, plus any within 12 m of the ship or the suit (those never wait: a late one is a hole to fall through; up to 6 in one frame on first arrival, guessed 1 to 3 ms each on Deck). Only ground within 90 m is solid, more when moving fast, never beyond 260 m. This is the likeliest cause of a stutter while landing: it is still done on the main thread. |
| Ground kept under the parked ship | The ship and the suit each keep the ground under them in its finest shape and solid (30 m round whichever one the player is not in): up to 15 more ground pieces drawn and 9 to 24 more solid, counted by a test program on a smooth ball, not measured. | 4 places (`GroundPointLimit`) |
| Working out one piece of ground | One height question costs 3.6 millionths of a second on Earth (was 2.9), 4.2 on Mars (was 3.0), 3.0 on the Moon (was 2.8); one colour 0.8 to 1.3 (was 0.7 to 1.1). Measured outside Unity on a cloud machine with `tools/terrain_view cost`, the quickest of five goes. A piece is 1,225 heights and 1,089 colours, and timed whole (`bench`) the finest pieces took 2 to 7 ms there (were 1.6 to 4), so a guessed 8 to 30 ms on a Deck, on background workers, never on the frame. 73 KB of garbage a piece, unchanged. The rate ground appears is still set by `Uploads Per Frame`. | Per world: `erosion`, `valleyDepth`, `terraceStep`, `cragHeight` at 0 on its profile give back the old cost. |
| Seeded landforms | Half as many again on every world (Earth 152 to 193 stamps, Mars 111 to 153, the Moon 224 to 317). Each height question only looks at the stamps near it, so the cost above already includes them. | `PlanetProfiles.MoreLandforms` |
| Background workers | 3 (`Background Workers`). Painting far-away pictures never takes the last free one, so ground under you is never kept waiting. |
| Rocks, trees and other placed shapes | 500 at most (`Feature Cap`), only within 170 m for loose rocks, and at most 24 put out per frame |
| Solid placed shapes | only those taller than 1.2 m get a collider |
| Far pictures | 4 planets at 1024 x 512 and 11 moons at 512 x 256 (the gas giants have none), with smaller copies for distance. Painted in the background over the first part of a run. The realistic ground (14 Nov 2026) makes each dot dearer: a review agent timed 13.2 s of one core before and 16.7 s after on a desktop outside Unity, before slopes in the picture were made to leave out bumps too small to show, which should win most of that back (not timed again). Guessed 25 to 50 s on a Deck on two workers. |
| Caves | each cave is one mesh and one collider, built in the background when you come near its body |
| Cave networks (16 Nov 2026) | Earth (karst, up to 2), the Moon (lava tubes, 1), Mars (lava tubes, 2) and Ganymede (ice, 1). Each is 22,000 to 38,000 triangles in 40 to 70 blocks of 16 m, each block its own mesh, so blocks out of view are not drawn (cap `CaveBudget.MostTriangles` 140,000, held by a rule test). Built as patient background work when you come near the body (never waited for when a run starts, never takes the last free core), with the colours made linear there too: 2.0 to 3.7 s of one core for the walls and 0.1 to 0.35 s for the light, measured outside Unity on a cloud machine (a guessed 5 to 10 s on a Deck, never on the frame). Drawn 1 block a frame. A block gets its collider (cooked without welding or cleaning) only once the suit or ship is within 90 m of it (`CaveNetworkView.SolidReach`), at most 1 a frame, so flying past a cave never cooks one. The light is one 3D texture of 2 m cells, at most 1.25 MB (`CaveBudget.MostLightBytes`, raised from 1 MB on 19 Nov 2026 because a ridge or rock tower over a way in makes the box taller; the largest seen is 1.05 MB), read by the shaders only while the eye is within 30 m of its box (one branch in the lighting, skipped everywhere else; the model shader reads it once a pixel). No real-time lights: the camps' work lamps are baked into that texture. Enemies in caves wake only within 70 m; each plans its path at most once a second (or on reaching its step), and at most 2 plans a frame are made among all of them (`CaveFooting.PlansPerFrame`); finding the nearest waypoint tests sight lines nearest first and stops at the first clear one, within 15 m. The second sight line (from the player back to the enemy) is cast only while a network is built. Not measured in Unity. |
| Weather | one particle system (up to 1,200 particles at `Weather Particles` 1), one lightning line, no extra lights |
| Plumes (geysers, smoke) | 5 emitters shared by the whole planet, moved to the nearest vents |
| Sea | one ball of 192 segments per body, drawn only when its ground is shown |
| Lights | none added. Ground is lit by the Sun's true position in the shader; the lamp is one point in the shader, not a Unity light. |
| Shadows | still off |
| Planet script time per frame | under 1 ms flying, under 4 ms while new ground is arriving (estimate) |

**What to read off first in Unity** (press **F3** for the debug readout):
1. The `tiles` and `things` numbers standing on Earth, the Moon and inside Jupiter.
2. In the Game view **Stats**: tell me if **Tris** goes over 600k, **Batches** over 1,500 or **SetPass calls** over 100.
3. Whether "Ground ready" reaches 19/19 within about 30 seconds of starting a run.
4. Whether the Budget line says OK, TIGHT or OVER while flying low and fast over Mars.
5. Whether the shoreline flickers when you look at Earth's coast from about 1 km up.

For the realistic ground (14 Nov 2026), also: the seconds from pressing Play until the F3 line `Ground ready` is full; the Budget line's ms standing on Mars at noon looking at the horizon with `Ground Relief` at 1 and then at 0 (stop Play between); and whether ground keeps up when flying low and fast over Earth's mountains.

Known follow-up: making ground solid (`Physics.BakeMesh`) could move to a background thread. I left it on the main thread, one piece a frame, until we can see in the Profiler whether it actually hitches.

## Ship (Milestone 7): estimates from reading the code, not measured

Counted and timed by the deck-performance agent on a desktop processor outside Unity. Its guess is that the Deck under Unity is 3 to 6 times slower. Nothing here looked like a threat to the 16.7 ms frame in steady play; the costs are spikes.

| System | Cost | Cap |
|---|---|---|
| Ship outside | about 39,500 triangles (fittings 34,124 from `Models/Lander`, 17 pieces, of which the two camera booms and the landing camera are about 4,200; hull and details 5,372), about 15 draw calls, one shader. Counted from the files, not measured in Unity. | fittings 36,000 triangles (a rule test fails above it); one ship in Phase 1. First trim if needed: one bevel step fewer on the base's foil panels. |
| Reading the lander model | 1,098 KB, once per launch on the main thread; 4 ms warm, 19 ms cold measured on a desktop outside Unity, guessed 20 to 80 ms on a Deck | once per launch |
| Lander model memory | about 1.1 MB kept, about 1.0 MB on the graphics card (estimate) | - |
| Round shading of the cabin dome | worked out once when the ship is built: under 5 ms on a desktop outside Unity | - |
| Cabin | 27,888 triangles, 21,377 corners, 12 pieces, one baked material, 12 draw calls, about 1.0 MB on the graphics card (the working hardware is 8,830 triangles, the lockers with the ship's computer 2,760). Vertex colours only: no textures, no lights, no shadows. Drawn whenever the ship is in view, from outside too (to do: hide it when you are outside and more than 30 m away). Baked light file 64 KB, read once at the start of a run. Counted from the files, not measured in Unity. | 26,000 triangles of fittings (rule test; today 24,096); nothing thinner than 12 mm (`ShipProps.Thinnest`) |
| Cockpit console | 8,204 triangles, 9 pieces (body 7,752, throttle lever 216), 4 to 9 draw calls (lamps that are off are not drawn); `Models/Console` 326 KB plus the lit copy `Models/LanderCabin` 317 KB; two small materials of its own; one scale, one rotation, one slide (the throttle lever, which has no collider and must never get one) and five on/off checks a frame, 0 bytes a frame. The plain body mesh in `Models/Console` is built but not drawn when the lit copy is used (about 0.36 MB; to do: build meshes only when asked for). Not measured in Unity. | 9,000 triangles (rule test) |
| Cabin: fallback bake | Only if the shipped file does not fit the cabin: one background thread, about 4.6 s on a desktop at 48 paths, guessed 15 to 30 s on a Deck, with the cabin flatly lit meanwhile | should never run in a shipped build (a rule test and a PlayMode test fail when the file is stale) |
| Glass | 5 panes, one see-through layer each | 5 |
| Crack lines | up to about 4,850 triangles a pane (about 24,000 if all 5 are badly cracked); rebuilt only when a new hit is recorded or the crack stage changes, guessed 3 to 8 ms each time on Deck | 6 recorded hits a pane |
| Lift beam | one see-through layer, full screen while riding | 1 |
| Physics | 1 rigidbody, 23 colliders (2 convex, 8 capsules, 8 spheres, 5 boxes), speculative fast-collision checking | fixed; cost in a crash unknown until measured |
| The player aboard | no physics at all | - |
| The player outside | 1 rigidbody, 1 capsule, 1 sphere cast per physics step | - |
| Wreckage (torn-off parts) | 1 rigidbody each, pulled by gravity every step | 9 pieces (4 legs, engines, 4 thruster clusters); removed once 400 m from both you and the ship |
| Building the ship at run start | guessed 100 to 250 ms on Deck, on the main thread (20 ms for meshes and 9 ms for the bake file on a desktop; sounds not timed) | once per run |
| Sounds | 15 short clips, about 0.64 MB, 2 audio sources; freed on restart | fixed |
| Fallback bake | 1 background thread; 3.9 s on a desktop at 48 paths, guessed 10 to 20 s on Deck | only if the shipped file is stale |
| Ship HUD | list refreshed 4 times a second; prompt words rebuilt at most once a second | target: 0 bytes of garbage per frame when idle |

Known follow-ups, to do only if the Profiler says so: halve the crack triangles by drawing them double-sided in the shader; build the sounds and mesh data on background threads at run start; fold the four engine colliders into the base.

What to read off in Unity (see "How to measure"): Batches and Tris seated in orbit and again standing outside the landed ship; GC Alloc per frame seated and idle; the worst frame while shooting one window until it breaks; the Physics row during a hard landing.

## Launch (Milestone 8): estimates from reading the code, not measured

Counted and timed by the deck-performance agent on a desktop processor outside Unity. Deck figures are that time multiplied by 3 to 6, so they are guesses. Nothing here has been run in Unity.

| System | Cost | Cap |
|---|---|---|
| Launch base | 6,072 triangles in 10 pieces sharing one material (counted from the stand-in shapes), about 10 draw calls. No textures, no lights, no shadows. | fixed; one base, on Earth |
| Base physics | 8 solid shapes that never move by themselves (1 mesh of 144 triangles, 2 boxes, 5 capsules) | fixed |
| Choosing the pad's spot | once per run on the main thread. 27 to 36 ms measured on a desktop for today's Earth (469 spots tried) before spots were dropped at the first bit of sea, so less now (not re-timed); guessed under 220 ms on Deck. | 41 rings, 4,921 spots |
| Building the base | shapes 2.5 to 16 ms on a desktop, guessed 10 to 100 ms on Deck, once per run on the main thread | once per run |
| Soft shade on the base | 12 rays a corner on a background thread, started on the first frame: 96 to 139 ms on a desktop, guessed 0.3 to 0.8 s on Deck. The base shows unshaded until it arrives. | once per run; never takes the last free worker |
| Engine sounds | 4 clips made at run start (7.2 s of sound at 22,050 Hz): 11 to 15 ms on a desktop, guessed 35 to 90 ms on Deck. 3 audio sources. | fixed; freed on restart |
| Flames | 4 cones of 10 triangles, one material, solid (not see-through) | 4 |
| Smoke | solid lumps, 132 triangles each, plain colour with no painted detail: 14,520 triangles at the cap, 1 draw call. Guessed up to a few ms on the graphics card when the plume fills the view, because lumps overlap. | 110 (`Max Smoke` on the Launch asset; code limit 400). **First thing to lower.** |
| Grit | 8 triangles each: 480 triangles at the cap, 1 draw call | 60 (`Max Debris`; code limit 200) |
| Light on the ground | none added: the engines borrow the shader's one lamp while they are the brighter | - |
| Shake | view only, 3 noise lookups a frame | off with the camera shake setting |
| Looking for the ground | 1 ray of 50 m per frame; ground colour looked up every 2 s | - |
| Garbage | target: 0 bytes per frame while burning. One new material at most every 2 s when the ground colour changes. | 729 dust colours |
| Heat shimmer | not built yet. If added as a screen effect, budget 0.5 to 1 ms and one copy of the screen. | - |

Follow-ups, only if the Profiler says so: choose the pad's spot on a background thread when a run starts in orbit; make the sounds on a background thread; a lighter smoke lump.

What to read off in Unity, seated on the pad on Earth:
1. Press **F3**. Before touching anything, note the Budget line, then **Batches**, **Tris** and **SetPass calls** in the Game view's **Stats** panel.
2. Hold "up" with boost through lift-off until you are about 50 m up. Note the same four things at the smokiest moment.
3. Stop Play, set `Max Smoke` to 8 on the Launch asset, press Play and repeat step 2. The difference in the Budget line is the cost of the smoke.
4. **Window > Analysis > Profiler**, CPU Usage, during lift-off: the **GC Alloc** column for `LaunchFx.Update` (expect 0 B) and the time on the `ParticleSystem` rows.
5. **Edit > Project Settings > Physics**: tell me whether **Reuse Collision Callbacks** is ticked.

## The look (pass 1, 13 Nov 2026): estimates from reading the code, not measured

Nothing here has been timed. The figures are estimates for a Steam Deck at 1280 x 800 from the Deck performance review of the code. The first run on a Deck replaces them.

| | Estimated cost | Cap, and what to turn down first |
|---|---|---|
| Smoothed edges | 0.5 to 1 ms at the default (2 samples). 4 samples: 1 to 2.5 ms. | `Edge Smoothing` on the Look asset. **First thing to lower**: `Fast` or `Off`. |
| Bloom | 0.5 to 1 ms. Starts from a half-size picture, 4 halvings, cheap filtering. | `Bloom` on the Look asset; 0 switches it off. **Second thing to lower.** |
| Tone mapping, colour grade, warmth, split tone | about 0.3 to 0.5 ms with the vignette. They are folded into one colour table, so switching one of them off saves nothing by itself. | `Finish` on the Look asset switches the whole finish off. |
| Ground shader (ground, rocks, outside of the ship) | 1.5 to 3.5 ms with ground filling the screen, as before, on rocks, trees, buildings and the ship (soft light, sky fill, bounce, rim, haze, up to three lookups of painted noise). Ground itself now has grit, stones and hummocks in place of the paint: the same count of lookups at the same sort of distances (one always, a second within 150 m, a third within 42 m, none where the far-away picture has taken over) plus a tilt of the surface worked out from neighbouring dots. Guessed 0 to 0.5 ms more than the paint; not measured. | `Ground Relief` 0 on the Planets asset switches the relief off (ground is then plain, with no paint either). `Paint` 0 on the Look asset still removes the paint from everything else. |
| Model shader (`OA_Model`: enemy bodies, guns, base buildings, the cockpit's camera fittings) | The ground shader's light and paint without the far-away picture, plus shine (a highlight from the Sun and from the lamp, and one more reading of the sky colour) on every corner a model marks as smoother than cloth (above 0.1; full from 0.3). Counted outside Unity with a different compiler on 14 Nov 2026, not measured: a model pixel is about 530 steps beyond 23 m, 745 nearer and 960 within 3 m, the same as under the ground shader to within 2%; shine added about 190, a fifth to a third more, before the lamp's highlight and the spreading of small highlights were added (a guessed 50 more, not counted). Guessed on a Deck: under 0.1 ms in an ordinary fight, 0.2 to 0.5 ms with one enemy in your face, 0.45 to 0.9 ms if shiny surfaces fill the screen (a base wall, once bases say what they are made of). Paid in every fight since the realistic trooper went in (14 Nov 2026: `Enemy` and `EnemyFar` carry a finish). About 70% of the trooper's surface is cloth and webbing under the 0.1 floor, but the work is skipped only where every pixel of a group the graphics card shades together is under it, and a small trooper mixes visor, shell and cloth in the same groups, so expect it to pay for shine on much more than 30% of what it covers. Draw calls do not change, but models and ground no longer share a shader, so SetPass calls may rise (guessed 10 to 30 in a base fight; to be read off the Stats window). A finish is 2 bytes a corner in the file, 8 on the graphics card and 8 kept in ordinary memory: 143 KB of each for the trooper with its far copy, 2.3 MB of each if every model carried one. The first model with a finish to be drawn makes the graphics card build one new program: a one-off hitch, guessed 5 to 30 ms, which now falls when the first trooper comes into view, mid-game; nothing warms shaders today (known follow-up: warm `OA_Model` while loading). The finest paint dab within 3 m (about 215 steps) costs more than the shine and could be dropped on models once the held gun has been looked at without it. | `Shine` 0 on the Look asset puts models back on the ground's shader at the next Play (`PlanetMaterials`), which skips the work and the hitch. Lower `Full Bodies` before touching this. |
| Sky ball | 0.2 to 0.5 ms. One draw call, about 1,000 triangles, one texture lookup. | Known follow-up: draw it after the ground instead of before, so covered sky is not paid for. |
| Real sky (stars and the picture of deep space) | Guessed 0.1 to 0.4 ms more than the earlier sky, not measured. 27,096 stars in one draw call (108,384 corners, about 4 MB on the graphics card), each a round dot about 3 screen dots across laid over the sky (the earlier 1,680 square stars took 3 draw calls); the picture is 4096 x 2048 with no smaller copies (8.4 MB on the graphics card, was 0.5 MB). The first run of a session builds the star mesh on the main thread: a guessed 15 to 60 ms with the picture's load and about 5 MB of short-lived memory; later runs reuse it. The cockpit's camera feeds stop at 30 km and so show no stars, as before. | At most 40,000 stars (`StarCatalogue.MostStars`). `Real Sky` off on the Look asset brings back the earlier sky. If memory is tight or the picture shimmers toward its poles, bake it at 2048 x 1024 (2.1 MB). |
| Planets from space (far ball's real look, air shell, clouds, fine rings) | Guessed, not measured: under 0.1 ms on a far rocky world (under 5% of the screen); 0.5 to 1.1 ms more than the earlier rim descending on Earth (cloud and air shells over the ground); 0.5 to 1.2 ms more close to Saturn. About 28 MB on the graphics card (pictures 11 MB, relief for 14 bodies 16 MB, night lights up to 2 MB). About 26 MB held while pictures paint. A picture's relief is uploaded the frame after the picture: guessed 2 to 5 ms for a planet each. | Two see-through shells a world, clouds on three worlds, one ring band. No relief for gas giants or a world under an unbroken deck (Venus). `Real Planets` off on the Look asset brings back the earlier look. First to lower: relief at half width (saves about 12 MB). |
| Sun shadows (26 Nov 2026, `SunShadows`, `SunShadowRules`, `OA_SunShade`) | The Sun's light casts URP main-light shadows in the solar system: one 2048 x 1024 depth picture (two 1024 cascades, a guessed 4 to 8 MB), drawn every frame except deep in the night, read by the ground and model shaders with URP's cheapest soft filter (4 reads a dot). Casting: ground tiles, scenery big enough to be solid, the lander's hull and fittings, the launch base, base buildings, enemy bodies (their own meshes; no hidden shadow copies any more, so `Full Triangles` counts each body once again, in the arena too), guns, the rover, melee items, gibs. Never: small plants, glow pieces, cave walls and lamps, the cabin. Guessed, not measured, at 70 m: drawing the picture 0.3 to 0.8 ms on the Deck's graphics chip, the filter 0.15 to 0.3 ms at 1280 x 800, the longer light sum 0.05 to 0.2 ms, cloud shadows on the ground 0.1 to 0.3 ms only from above; 170 to 260 more draw calls (SRP-batched; they count against the 1,500) and 110,000 to 170,000 shadow triangles in a wood with a base and a squad, or a 12-enemy fight; 0.3 to 0.8 ms of processor time. A first-use shader hitch is possible the first time a shadow is drawn (guessed 20 to 150 ms, once; the lander casts from the first frame on the pad, which should put it at the start of a run). The arena's shadows now use these settings too (it had 50 m, one cascade, hard). Coarse ground tiles crossing the shadow volume at a low Sun are unchecked. Not seen in Unity. | `shadowDistance` 70 m (30 to 250), 2 cascades, 2048, soft at Low; at most 250 shadow draw calls and 200,000 shadow triangles; 1.5 ms of graphics time. **First thing to lower: `shadowDistance` to 45 m, then Sun Shadows off (soft to hard needs a code change), then scenery casting off.** |
| Water | 0.3 to 0.8 ms when it fills the view. | - |
| Picture of deep space | No cost per frame. 512 x 256 (0.5 MB), painted once on a background thread when a run starts: 0.2 s measured on a desktop, guessed about 1 s on a Deck. Space is plain black until it arrives. | `Space Backdrop` 0 on the Look asset skips it. |
| Lights and shadows | None added. | - |

What to read off in Unity, standing on Earth at noon, then at a coast, then seated in orbit:
1. The Budget line on the readout, with `Finish` ticked and then unticked on the Look asset (the difference is the cost of the finish). Stop Play, change the dial, and press Play again each time: the finish is set up once when Play starts, so a change made during Play does nothing.
2. The same with `Edge Smoothing` at Multisample4, Multisample2 and Off.
3. The same with `Paint` at 1 and at 0.
4. Batches and Tris in the Stats panel (they should be within a handful of what they were before this pass).

## Guns (Blender models, 13 Nov 2026): estimates from reading the code, not measured

| | Cost | Cap |
|---|---|---|
| Rifle | 3,970 triangles, 2 pieces, since the realistic rifle of 19 Nov 2026 (about 7,800 before); its far copy 1,584. It is the first textured model: one set at 2048 (colour and bumps 5.3 MB each, mask at 1024 1.3 MB, about 12 MB with mip maps; worked out, not measured) | 9,000 triangles a gun (a rule test fails above it) |
| Pistol | 1,200 triangles, 2 pieces, since the realistic pistol of 19 Nov 2026 (about 3,900 before); far copy 479. One texture set at 1024 (about 3 MB with mip maps; worked out, not measured) | 9,000 triangles a gun |
| Smg, Shotgun, Sniper (the enemy kinds' guns) | 1,132, 4,772 and 3,158 triangles since the realistic guns of 19 Nov 2026 (4,124, 5,668 and 6,140 before); far copies 479, 1,832 and 1,231. Smg has 2 pieces; Shotgun and Sniper have 3: the third (`CellGlow`, `LensGlow`) is the part that shines and is drawn with one shared glowing material, so each of those guns is one draw call more than the rifle. Each has one texture set: Smg at 1024 (about 3 MB), Shotgun and Sniper at 2048 (about 12 MB each with mip maps; worked out, not measured). All five guns' pictures together are about 42 MB of texture memory if every gun is in view at once | 9,000 triangles a gun; 140 MB of model pictures in all (a rule test) |
| Guns at once | 10 in the arena (8 mannequins, yours, 1 spare); 13 in the solar system (12 enemies' and yours), 31 if 30 enemies are ever present, plus up to 12 lying loose. Beyond 9 m (stretched by its holder's size) a gun is drawn as its far copy (`RifleFar` and so on): about 1,500 to 2,000 triangles against 3,868 to 7,696, in the same 2 or 3 pieces, so its draw calls do not fall. A gun in an enemy's hands is never drawn finer than the body holding it, so full guns are held to the count of full bodies. Guns lying loose are not counted: 12 of them within 9 m is up to 92,000 triangles. Counted from the files, not measured in Unity | far copy 2,200 triangles a gun, under 0.45 of the full gun's triangles and 0.62 of its corners (rule test) |

### Launch base model (look pass 2)

| | Cost | Cap |
|---|---|---|
| `Resources/Models/LaunchPad.bytes` | About 31,600 triangles in 17 pieces (10 solid, 7 that shine), one base in the world | 36,000 triangles |

### Enemies (look pass 2)

| | Cost | Cap |
|---|---|---|
| Enemy model (`Resources/Models/Enemy.bytes`; since 20 Nov 2026 Matthew's Tripo soldier, fitted by `tools/blender/tripo/`: 21,274 triangles drawn, far copy 4,474, one 2048 texture set; counted from the files, not measured. The figures that follow are the script-built trooper's of 14 Nov, within 1% in triangles) | 21,176 triangles drawn (21,460 in the file with the wound), 13,488 corners, in 14 pieces (one per breakable part), one shared material, 14 draw calls. It replaced the first model of 14,848: 43% more for each one drawn in full. Its average triangle is smaller than a pixel beyond 9 m (worked out from the file), so much of the detail is only seen close; it has not been slimmed. Graphics cards pay for triangles smaller than a pixel several times over, so a finer body goes over to its far copy sooner (`DetailRule.RangeFor`): the trooper at 20.9 m instead of 24. In the arena, where the sun casts real shadows, an unseen copy of each piece casts the shadow, so the triangles are drawn again per shadow cascade. No shadow copies on planets or in space. Carries a finish (what each surface is made of): 8 bytes a corner on the graphics card and 8 in ordinary memory, about 143 KB of each with its far copy (the second figure if Unity keeps a readable copy of the mesh, not checked). | 22,000 triangles for this model, 16,000 for the other kinds (the rule test `EnemyModelFile_MatchesTheSkeleton` enforces both, and that eight of any body fit inside `DetailRule.FullTriangles`) |
| Three more enemy bodies (`Models/EnemyRunner`, `EnemyBruiser`, `EnemyMarksman`) | 12,520, 13,510 and 14,020 triangles drawn (the files hold 284 more each for the wound stump), each in the same 14 pieces on the same skeleton, drawn with the same shared material: one more kind costs no more per enemy than the trooper. Files 385, 416 and 440 KB, read the first time that kind is made. Counted from the files, not measured in Unity. Each body, the trooper included, has a far copy (`EnemyFar`, `EnemyRunnerFar`, `EnemyBruiserFar`, `EnemyMarksmanFar`): 4,473, 4,469, 4,476 and 4,462 triangles and 4,100 to 4,700 corners (the full bodies have 8,400 to 9,900) in the same 14 pieces, files 162 to 175 KB | 16,000 triangles each; 4,800 a far copy, under 0.4 of the full body's triangles and 0.62 of its corners (rule tests fail above these) |
| Strider (`Creatures/Strider`, 19 Nov 2026; the first alien creature) | 34 draw calls (2 body halves, 30 limb pieces, 2 claw fingers; 2 more for each joint shot through), one shared material. 16,212 triangles drawn in full (7,998 in the file: a leg is one mesh drawn eight times), 4,285 as its far copy, which it wears beyond 38 m; a counted body, so one in full takes about a tenth of the full-body allowance. No real-time shadow. 1 kinematic body with 32 hitboxes moved every frame; the hitbox layer touches nothing, so no contacts. Scripts guessed 0.05 to 0.15 ms each on a Deck (ten limb reaches and 33 transforms a frame, even when out of view), no garbage a frame; one sphere check and one ray a moving frame, one sight line four times a second. Death: up to 14 loose pieces made in one frame (not pooled), guessed 1 to 3 ms. Loose pieces are separate from the gib pool's cap. Pictures 12 MB, read on the main thread the first time one is made (in the arena, at the start). Worked out from the code and the files, not measured in Unity. | 3 alive at once (102 draw calls, 49,000 triangles all in full); 20 loose pieces, 30 s each. If it proves dear: stop posing it while out of view, then halve the loose pieces. |
| Sprinter (`Creatures/Walker`, 19 Nov 2026; the camel spider, on the Strider's shared walker code) | 36 draw calls (head, abdomen, 24 leg pieces, 6 feeler pieces, 2 jaws and their 2 fingers; 2 more for each joint shot through), one shared material. 10,516 triangles drawn in full (4,722 in the file: a leg is one mesh drawn eight times, its bristles are four-triangle slivers), 2,934 as its far copy (no bristles), which it wears a tenth further out than a trooper its size would. A counted body. No real-time shadow, on the body or the meat. 1 kinematic body with 36 hitboxes moved every frame; because it lunges, the hitboxes are synced once more after the pose (`Physics.SyncTransforms`) on the frames it is posed. One line of sight before a bite counts. Death: up to 16 loose pieces made in one frame (not pooled). Pictures 12 MB, read the first time one is made. Worked out from the code and the files, not measured in Unity. | 2 in the arena beside 1 Strider (174 draw calls, 37,000 triangles all in full); 34 loose walker pieces between all kinds, 30 s each. If it proves dear: leave the bristles out of the full model beyond 10 m, then stop posing it out of view. |
| Roller (`Creatures/Roller`, 19 Nov 2026; the giant isopod) | 30 draw calls (head, 7 segments, tail, 7 plates, 14 legs; 1 more for each leg shot off and for the tail), one shared material; nothing is mirrored (a leg is the same either side). 15,428 triangles drawn in full since the 26 Nov 2026 quality pass (4,938 in the file: a segment, a plate and a leg are each one mesh drawn many times; it was 6,228), 3,821 as its far copy (the plates lose every other row and column there, `FAR_HALVE`: a thinned arch opens), which it wears a tenth further out than a trooper. A counted body. No real-time shadow, on the body or the meat. 1 kinematic body with 44 hitboxes (9 boxes, 21 plate boxes, 14 leg capsules) moved every frame, in view or not; the hitbox layer touches nothing, so no contacts. While it is a ball (1 to 2 s at a time) the hitboxes are synced once more after the pose (`Physics.SyncTransforms`). Scripts guessed 0.05 to 0.15 ms on a Deck, no garbage a frame; one sphere check and one ray a moving frame, one sight line four times a second, one more before a hit counts. Death: up to 15 loose pieces made in one frame (not pooled), one box each for the segments (what rides on a loose segment is only drawn), guessed 1.5 to 3 ms. A loose piece that starts to fade is made kinematic and its colliders switched off (all walkers' pieces too). Pictures 10 MB, read on the main thread the first time one is made. Worked out from the code and the files, not measured in Unity. | 1 in the arena beside 1 Strider and 2 Sprinters (about 204 draw calls, 43,500 triangles all in full, 148 hitboxes moved a frame, 34 MB of creature pictures); at most 2 Rollers by the config; 34 loose pieces between all creatures, 30 s each. If it proves dear: stop posing it out of view, then one sync for all creatures before the player's shot instead of one each. |
| Cave hunter (`Creatures/Hunter`, 20 Nov 2026; the giant centipede) | 55 draw calls (head, 16 segments, 32 legs, 2 fangs, 2 feelers, 2 tail legs; 2 more for each leg shot off and for each cut), one shared material; nothing is mirrored. 14,962 triangles drawn in full since the 27 Nov 2026 quality pass (it was 13,370; a segment is one mesh drawn sixteen times, a leg thirty-two), 5,121 as its far copy (it was 7,737; the back plates lose every other row and column there, `FAR_HALVE`), which it wears a tenth further out than a trooper. A counted body. No real-time shadow. 1 kinematic body with 49 hitboxes (17 boxes, 32 leg capsules) moved every frame, in view or not; the hitbox layer touches nothing, so no contacts. Hitboxes are synced once more after the pose (`Physics.SyncTransforms`) on every frame the player can see it and while it rears and strikes, so thin legs and a fast head are where they are drawn. Scripts guessed 0.06 to 0.2 ms on a Deck, no garbage a frame; one sphere check, one sphere sweep and one ray a moving frame, one sight line four times a second, one more before a bite counts. A cut: up to 13 loose pieces made in one frame (not pooled), one box each (the legs riding on a loose segment are only drawn, their colliders switched off before they are moved), guessed 1.5 to 3 ms; death up to 19. Pictures 9 MB on disk, read on the main thread the first time one is made. Worked out from the code and the files, not measured in Unity. | 1 in the arena beside 1 Strider, 2 Sprinters and 1 Roller (about 259 draw calls, 57,000 triangles all in full, 197 hitboxes moved a frame); at most 2 Cave hunters by the config; 34 loose pieces between all creatures, 30 s each. If it proves dear: stop posing it out of view, plain (Discrete) collision for loose segments, then one sync for all creatures before the player's shot instead of one each. |
| Screamer (`Creatures/Screamer`, 20 Nov 2026; the cicada) | 20 draw calls (fore body, abdomen, 2 drums, 4 wings, 12 leg pieces; 2 more for each piece shot off), one shared material; nothing is mirrored. 11,192 triangles drawn in full since the 26 Nov 2026 quality pass (7,020 in the file: a wing is one mesh drawn twice, a leg six times; it was 4,378), 3,596 as its far copy. A counted body. No real-time shadow. 1 kinematic body with 20 hitboxes moved every frame, in view or not; the hitbox layer touches nothing, so no contacts. No garbage a frame. One sight line four times a second; while it screams one more line from its mouth to the player five times a second (once a beat). The rings (`ScreamRings`): 5 ring objects made once and reused (one shared mesh), opaque, one sent out a beat, no overdraw to speak of. One looping sound for all Screamers. Death: up to 18 loose pieces made in one frame (not pooled), guessed 1.5 to 3 ms. Pictures 11 MB on disk, read on the main thread the first time one is made. Worked out from the code and the files, not measured in Unity. | 1 in the arena beside 1 Strider, 2 Sprinters, 1 Roller and 1 Cave hunter (about 279 draw calls, 61,000 triangles all in full, 217 hitboxes moved a frame); at most 2 Screamers by the config; 34 loose pieces between all creatures, 30 s each. If it proves dear: stop posing it out of view. |
| Ambusher (`Creatures/Ambusher`, 26 Nov 2026; the bobbit worm) | At most 24 draw calls (head, 15 body lengths, 2 jaws, 5 feelers, the heap), one shared material; lengths down the burrow are not drawn (about 8 to 12 calls waiting, none but the heap while it is under ground). 15,742 triangles with every length out since the 26 Nov 2026 quality pass (6,640 in the file: the three neck lengths have their own pieces and the other twelve share one; it was 8,008), 6,395 as its far copy; about half its lengths are down the hole and not drawn while it fights. A counted body. No real-time shadow. 1 kinematic body with 23 hitboxes (16 node boxes, 2 jaw boxes, 5 capsules) moved every frame it is out of the ground; under ground they are switched off and it is not posed. The hitbox layer touches nothing, so no contacts. No garbage a frame. One sight line four times a second (every frame of a strike, which lasts 0.18 s); while it tunnels one sphere check and one short ray a frame. `Physics.SyncTransforms` once a frame only while it warns, strikes or is out with the player in line. Holding: one `CharacterController.Move` the player was making anyway. Death: up to 26 loose pieces made in one frame (not pooled), guessed 2 to 4 ms. Pictures 9 MB on disk, read on the main thread the first time one is made. Worked out from the code and the files, not measured in Unity. | 1 in the arena beside 1 Strider, 2 Sprinters, 1 Roller, 1 Cave hunter and 1 Screamer (about 303 draw calls, 240 hitboxes moved a frame at the very most); at most 2 Ambushers by the config; 34 loose pieces between all creatures, 30 s each. If it proves dear: draw the lengths as one skinned mesh. |
| Blind stalker (`Creatures/Stalker`, 26 Nov 2026; the olm) | 22 draw calls (head, jaw, 11 body lengths, 4 legs of 2 pieces, 2 gills), one shared material. 15,868 triangles since the 26 Nov 2026 quality pass (12,170 in the file: every length of the body is its own piece now, legs and gills are drawn several times; 4,056 of them are the two bushy gills; it was 7,208), 4,631 as its far copy. This is at the 16,000 allowance: anything added must come out of the gills. A counted body. No real-time shadow. 1 kinematic body with 22 hitboxes (12 node boxes, 4 legs with their shins, 2 gills). Lying still with nothing to hear, which is most of its life, it is posed five times a second and its hitboxes are not synced; otherwise posed every frame, with `Physics.SyncTransforms` only on frames it changed with the player within 30 m. One ray and one sphere check a frame while it moves (is the way clear, is there ground); one line cast for a bite. No garbage a frame (hits, senses and orders are structs). Death: up to 20 loose pieces made in one frame (not pooled), guessed 2 to 4 ms. Pictures about 11 MB on disk, read on the main thread the first time one is made. Worked out from the code and the files, not measured in Unity. | 1 in the arena beside 1 Strider, 2 Sprinters, 1 Roller, 1 Cave hunter, 1 Screamer and 1 Ambusher (about 326 draw calls, 262 hitboxes moved a frame at the very most); at most 2 Blind stalkers by the config; 34 loose pieces between all creatures, 30 s each. If it proves dear: draw the body as one skinned mesh. |
| Pack biter (`Creatures/Biter`, 27 Nov 2026; the Goliath tigerfish, a shoal of 3 to 5) | 10 draw calls a fish (head, 4 lengths, jaw, 2 fins, back fin, tail fin), one shared material. 5,844 triangles drawn in full (6,210 in the file), 1,612 as its far copy, which it wears beyond half a body's detail range (about 12 m). A counted body each. No real-time shadow. Not a physics object while alive: 10 hitboxes moved a frame, posed every frame within 70 m (5 a second beyond while shoaling); one `Physics.SyncTransforms` for the whole shoal on frames a fish within 30 m was posed. One sphere cast and two water readings a fish a frame. No garbage a frame. A pack is built one fish a frame. Death: 11 loose pieces in one frame (not pooled). Pictures 1024 (about 3 MB once compressed), read when the run is built. Worked out from the code and the files, not measured in Unity. | Only one pack awake at a time (`SeaCreatures`), with at most one Drifting maw beside it (`SeaFieldRules.liveCap` 2): at most 5 fish (50 draw calls, 29,200 triangles in full, 50 hitboxes, 5 of the 12 full places). 34 loose pieces between all creatures, 25 s each. If it proves dear: one water reading a shoal, the whole think at 5 a second far off. |
| Drifting maw (`Creatures/Maw`, 27 Nov 2026; the megamouth shark) | 14 draw calls (head, 8 lengths, jaw, 2 fins, back fin, tail fin) plus 5 ring objects while it draws in, one shared material. 9,232 triangles drawn in full (9,518 in the file), 2,840 as its far copy. A counted body. No real-time shadow. 15 hitboxes moved a frame (the throat sphere only while its mouth is open), posed every frame within 90 m (5 a second beyond while drifting), `Physics.SyncTransforms` on posed frames within 40 m. One sphere cast and up to two water readings a frame. No garbage a frame. Death: 18 loose pieces in one frame (not pooled), guessed 2 to 4 ms. Pictures 9.5 MB on disk, read when the run is built. Worked out from the code and the files, not measured in Unity. | Counted in the 2 sea haunts awake at once with the Biter packs (two maws: 28 draw calls, 18,500 triangles; a maw and a pack: about 64 draw calls, 38,500 triangles). 1 in the arena by default (`arenaMaws`). If it proves dear: stop posing it out of view. |
| Arena creatures together (26 Nov 2026 quality pass; arithmetic, not measured) | At the default arena counts: about 108,900 triangles with every creature in full (about 101,000 really drawn, half the Ambusher being under ground), about 36,300 with every one as its far copy. `DetailRule` still holds full bodies to 12 and 180,000 triangles. `ModelDetail` counts the pieces a body holds, not the ones drawn, so the Ambusher takes 15,742 of that while drawing about 8,000. Before creatures are spawned in crowds outside the arena, the Ambusher's far copy (6,395) should come down to about 4,800 (the Cave hunter's came down to 5,121 on 27 Nov). First cut if a measurement asks for one: the Stalker's gills (4,056). |
| Texture pictures on models (19 Nov 2026, `ModelLibrary.MaterialFor`, keyword `_OA_TEXTURED`; none in the game yet) | Per textured model, one set of three pictures shared by every copy and its far copy. BC7 (colour, mask) and BC5 (bumps, set by `ModelTextureImportRules`) are one byte a pixel, and mip maps add a third: a 2048 picture is 5.3 MB, a 1024 picture 1.3 MB. The mask is saved at half size (`oa.save_texture_set`), so a body's set at 2048 is 12 MB and a gun's at 1024 is 3 MB. Five bodies and ten guns: about 99 MB with one gun at 2048, 135 MB if all five ladder guns are 2048; about the same again on disk. Worked out, not measured. One material per set, kept on the model so a new body asks once; draw calls unchanged; one more shader variant, so a guessed 5 to 30 more SetPass calls where textured and untextured pieces are mixed. In the shader: three picture reads a pixel (colour filtered at anisotropic 4, the others at 1) and about 40 more steps for the bumps; the painted break-up is skipped; untextured models carry none of it (the extra data is only in the textured variant). Shine is paid wherever the mask's green is over 0.1, so cloth must be baked at 0.06 or under. Corners carry 24 more bytes on the graphics card and 24 in ordinary memory, and there are more corners (they split at every seam of the pictures; guessed 15 to 50% more). Pictures are read on the main thread, a guessed 30 to 200 ms a set: the enemy spawner reads every roster body's and gun's set while the world loads (`MaterialFor`), but a ladder gun not carried by any enemy is read when first handed over. Not measured in Unity. | 2048 a picture (`ModelTextureImportRules.MaxSize`, rule test on the PNG sizes); about 140 MB of pictures in all; no picture read mid-fight |
| Enemies drawn, full and far copies | A body is drawn in full only within 24 m (20.9 m for the realistic trooper, see the row above; 20 m Runner, 31 m Bruiser, 29 m Marksman: about 36 pixels tall on the Deck's screen there), and only the nearest 12 in view; the rest wear the far copy, and so does a gun in a far body's hands. Per pass, for the roster's mix with guns, counted before the realistic trooper: **12 enemies** 76,000 triangles with nobody near, 238,000 with all 12 close (the same as before far copies, because 12 are allowed in full). **30 enemies** 191,000 with nobody near, 353,000 at the very worst (12 full, 18 far); drawn all in full they would be 596,000. With the Look asset's `Full Bodies` at 8 the worst is 299,000. **Draw calls do not fall:** 14 a body and 2 or 3 a gun whichever copy shows, so 196 for 12 enemies and about 490 for 30, a third of the whole budget; twice that, and twice the triangles, if a depth pass runs (blood decals may switch one on; not yet looked at in Unity). In the arena add the shadow copies, which are swapped too. Counted from the files, not measured. **Since the realistic trooper (14 Nov 2026):** full bodies are also held to 180,000 triangles between them (`DetailRule.FullTriangles`: twelve of the first model came to 178,000), counting only the pieces a body still holds (not the wound; a body that has lost limbs counts less). So a finer body takes a full place and a half: 8 troopers in full at once, or 11 or 12 of an even mix of the four kinds, where it was 12. Twelve troopers all close are 8 full and 4 far: 187,000 body triangles drawn against 178,000 before, 5% more; thirty all close about 268,000 against 258,000, 4% more. This limit holds the triangles of full bodies, not what they cost in pixels (see the row above for that). Not counted again for every mix, and not measured | 12 full bodies and 180,000 triangles of them, whichever is reached first (`DetailRule.FullBodies`, `FullTriangles`; dials `Full Bodies` and `Full Triangles` on the Look asset; F3 shows both). `liveCap` stays 12 until it has been tried at 20 and then 30 on a Deck, mid-fight in an Earth wood beside a base, with the Budget line reading OK, Batches under 1,500 and Tris under 600k |
| Enemy animation (`BodyAnimator`) | Worked out per enemy per frame, no stored clips, no garbage. Each kind carries itself its own way through one shared `BodyStyle` (eleven numbers, made once; nothing per frame). 45 microseconds for 30 enemies on a desktop outside Unity; not measured on the Deck. Reaction layers (startle, arm-loss reel, stumble, teeter, look-at; 15 Nov 2026; the wound clutch and its arm IK were taken out the same day) add an estimated 10 to 30 microseconds for 30 enemies (counted, not timed), no garbage, and one `InverseTransformPoint` per enemy per frame while it looks at something. The gait rewrite (15 Nov 2026: stride from leg length, heel-to-toe roll, knee give on landing) adds an estimated 5 to 15 microseconds for 30 enemies (counted, not timed), no garbage | 30 |
| Reading enemy and gun models | Every roster body and gun is read, with its far copy, when the spawner is built (the rifle when the player's own is made), on the main thread during loading; nothing is read mid-fight. Outside Unity on a desktop: 3.3 ms for the nine full models and about 1 ms more for the nine far copies; guessed 35 to 100 ms in all on the Deck. The far copies add 1.0 MB of files, about 1.3 MB of kept data and about 1 to 1.2 MB on the graphics card | once per run |
| Choosing full or far (`ModelDetail`) | Every watched body and gun is looked at together 5 times a second, not every frame, and once more on the frame something new is made. The rule alone took under 1 microsecond typically and 6 at worst for 61 things on a desktop outside Unity; the whole look, with the check for limbs that have come off, is guessed at 30 to 120 microseconds on the Deck for 30 enemies and 31 guns. A body changing copy re-points 14 meshes (28 in the arena): guessed 30 to 100 microseconds. Limbs and bodies lying loose are watched one piece at a time by distance alone. About 3 KB of garbage when an enemy is made, none per look | 0 bytes a look; working lists hold 128 things before they are re-made |
| Stances and stealth (`StanceState`, `Astronaut.UpdateStance`, `MannequinCombat` hearing) | A few comparisons per frame, no garbage. One capsule check (`Physics.CheckCapsule`) only when the player tries to rise, and each frame while airborne and still low; enemies add no physics queries (hearing is a distance test, the heard place a stored point). The suit display rewrites its stance words only when they change. Guessed under 10 microseconds a frame; not measured. | - |
| Objects per enemy | About 87 (17 bones, 14 parts, 14 meshes, 14 shadow copies in the arena, 26 hidden wound stumps) | - |
| Triangles, all guns | about 65,000; twice that if a depth pass runs | 100,000 |
| Draw calls | 20 (2 a gun, one shared material), down from about 100 with the box guns | - |
| Paint on the held gun | guessed 0.1 to 0.4 ms in the arena, about nil on planets | `Paint` 0 on the Look asset |
| Reading the models | 0.6 ms measured on a desktop outside Unity; guessed under 15 ms on a Deck, once | - |
| Memory | about 0.4 MB on the graphics card, 0.4 MB of kept file data | - |
| Shadows | guns cast none: their shader has no shadow pass | - |

What to read off in Unity, in the arena: Tris, Batches and SetPass calls in the Stats panel looking at the guns and then away from them (expect a difference of about 65,000 or 130,000 triangles); the Budget line with `Paint` at 1 and at 0.

## Cockpit (Milestone 9): estimates from reading the code, not measured

Counted and timed by the deck-performance agent on a desktop processor outside Unity. Deck figures are guesses. Nothing here has been run in Unity.

| System | Cost | Cap |
|---|---|---|
| Dash picture | 1,224 to 2,841 corners and 10 to 19 words, one canvas, 3 draw calls | 30 paints a second (`Redraws Per Second`); 1,200 triangles and 24 words (rule test) |
| Painting the dash | 12 to 41 microseconds and 0 bytes on a desktop. The mesh is only handed to Unity when the picture differs from the last one (when coasting, 299 of 300 were the same); guessed 0.3 to 1 ms on Deck each time it does | not painted while a camera fills the view |
| Words | legacy Text, touched only when changed. Words keep their place, so a warning blink changes 1 to 3 words (rule test) | 24 words |
| Dash overdraw | about a fifth of the screen, 2.1 to 2.7 layers of plain blending (Glass 0.27) | fixed |
| Little camera screen | one extra camera into 240 x 150, a cull and draw of what is within 30 km, no bloom, grade or shadows, once every 6 frames, only while seated. Guessed up to 1 to 2 ms on that frame over a planet. **First thing to lower if the pad is over budget.** | `Feed Every Frames` 6 (1 to 12) |
| Camera filling the view | replaces the Eye, with the full finish, so no added cost | one camera at a time |
| Cameras | 3, at most 1 drawing; they leave out the dash's own layer | 3 |
| Camera hardware on the lander | about 4,200 triangles (two booms, three cameras), no shadows | part of the lander's 36,000 |
| Garbage | target: 0 bytes per frame holding steady; about 0.3 KB a second while numbers change | - |
| Memory | feed picture about 0.2 MB | - |

What to read off in Unity, seated on the pad on Earth:
1. Press **F3**. Note the Budget line, then **Batches**, **Tris** and **SetPass calls** in the Game view's **Stats** panel.
2. In the **Project** window open **Assets > OrbitalAnte > Resources > Config** and click **CockpitConfig**. Set `Feed Every Frames` to 1 and note the same four; then 12. The rise at 1 is what the little screen costs.
3. **Window > Analysis > Profiler**, CPU Usage, Hierarchy: type `ShipCamera` in the search box. Note **Time ms** and **GC Alloc** on a frame where it appears.
4. In the same list: `CockpitDash.Update` (Time ms, GC Alloc; expect 0 B) and `Canvas.SendWillRenderCanvases`.
5. **Window > Analysis > Frame Debugger > Enable**. Under the ShipCamera group, note how many events there are and whether Bloom, UberPost or a depth pre-pass appear (none should).
6. Burn fuel below 20% and note whether the Profiler graph spikes twice a second (it should not).

## Navigation (Milestone 10): estimates from reading the code, not measured

Read by the deck-performance agent. Nothing here has been run in Unity; Deck figures are guesses. The one timing is from a desktop processor outside Unity.

| System | Cost | Cap |
|---|---|---|
| Autopilot steering | a few square roots per physics step, no garbage. Guessed under 5 microseconds on Deck. | one ship |
| Autopilot path check | all 130 bodies, twice a second, only while the autopilot is on. Two small lists (about 4 KB) made once. | 2 checks a second |
| Finding what the nose points at | all 130 bodies every frame (older code, now also feeds the lock). Guessed 15 to 40 microseconds on Deck. | 150 bodies |
| Autopilot words (dash and HUD) | remade when the shown word or whole second changes: about 0.6 KB of garbage a second while it flies, 0 when off. The shown time only counts down and the word holds for 0.3 s, so it cannot flicker. | 1 remake a second each |
| Words for why it let go | fixed strings; one small string when a body is in the way | once per stop |
| Knocks on the pilot | a few sums per touch, no garbage. One string when a knock kills. | - |
| View kick | 4 small spring steps a frame, always running | - |
| Crash sounds | a thump per rising touch, a 1.6 s groan per damaging blow, on the ship's one-shot source | one groan a second |
| Skid scrape | one looping source on the ship, silent unless sliding; one small sum per touch per physics step while anything touches the hull | 1 source |
| Groan and scrape clips | made at run start: the groan took 4 to 6 ms on a desktop, guessed 12 to 36 ms on Deck; the scrape is a little shorter. About 0.13 MB kept. | once per run |

What to read off in Unity:
1. Open **Window > Analysis > Profiler**. Click the **CPU Usage** row. In the lower panel's drop-down choose **Hierarchy**.
2. Press Play, take off, lock a far planet (**T**) and start the autopilot (**R**). Type `TestCraft` in the search box. Note **Time ms** and **GC Alloc** for `TestCraft.FixedUpdate` and `TestCraft.Update` (expect 0 B).
3. Search `CockpitDash.Update`, then `FlightHud.LateUpdate`. Note **GC Alloc** on ten frames in a row: it should be above 0 B on about one frame a second, not most frames.
4. Fly into the ground at about 20 m/s. Click the tallest spike. Note the total ms, then search `Ship.OnCollisionEnter` and note **Calls**, **Time ms** and **GC Alloc**.
5. Click the **Audio** row in the Profiler and repeat the crash. Note the highest **Playing Audio Sources** and **Audio Voices**.
6. **Edit > Project Settings > Physics**: note whether **Reuse Collision Callbacks** is ticked. Then **Edit > Project Settings > Time**: note the **Fixed Timestep** number.

## Spacewalk (Milestone 11): estimates from reading the code, not measured

| System | Cost | Cap |
|---|---|---|
| Suit body | one capsule, one sphere cast per physics step, no garbage. The real ground height is asked of the planets every sixth step | one suit; switched off aboard |
| Tether line | 60 corners rewritten per frame while clipped on, 1 draw call | 14 segments |
| Jet flames | 96 triangles, up to 12 draw calls, one material | 12 |
| Suit readout | one canvas, guessed 5 to 8 draw calls. Gauges are only rewritten when they move by half a percent, and colours are set on the renderer, so the canvas is not rebuilt every frame | text 8 times a second |
| Readout words | only built again when what they depend on changes | none while nothing changes |
| Visor rim | four soft bands 90 px deep, about a third of the screen blended at most (all four only on a forward push). Guessed 0.1 to 0.3 ms on the graphics card. First thing to lower: band depth | 4 bands |
| Suit sounds | 7 clips made at run start, guessed 27 to 80 ms once on a Deck; about 0.16 MB kept; 2 sources | once per run |
| Shot push in zero-G (`ZeroGKick`; 24 Nov 2026) | Per shot while floating free: 3 delegate calls made once at setup, a dot product and one `linearVelocity` write; no allocation | 1 push a shot; drift from shooting capped at `mostDrift` 3 m/s |

What to read off in Unity (floating outside, tethered): in **Window > Analysis > Profiler**, CPU Usage, Hierarchy, search `SuitHud.LateUpdate`, `SuitFx.LateUpdate` and `Astronaut.FixedUpdate` for Time ms and GC Alloc (expect 0 B on most frames), and `Canvas.BuildBatch` while drifting and then while thrusting.

## Enemies on the worlds (14 Nov 2026): estimates from reading the code, not measured in Unity

| | Cost | Cap |
|---|---|---|
| Enemies present at once | See "Enemies drawn, full and far copies" above: 76,000 to 238,000 triangles and 196 draw calls for 12 with guns; scripts guessed 0.5 to 1.5 ms | 12 (`liveCap`), the fallen included until cleared (25 s) |
| Waking a camp | about 80 objects and 16 colliders per enemy; one enemy a frame | 1 camp at a time, 1 enemy a frame |
| Ground and wall checks | about 1,500 raycasts a second for 12; guessed 0.1 to 0.4 ms a frame | 2 a frame per enemy |
| Placing camps | 1 to 3.5 ms on Earth at 70 camps, 7 ms on Neptune, measured on a desktop outside Unity; guessed under 40 ms on Deck | once per body; 120 camps |
| Dropped enemy guns | about 1,500 to 2,000 triangles each beyond 9 m, up to 7,696 within, and a moving body each | 12 loose; cleared with their owner |
| Aim line (Marksman) | one line renderer while aiming | 1 per Marksman, 1 Marksman per camp |
| Player death burst (`PlayerBurstFx`, `DeathBurst`; 15 Nov 2026) | Once a run, then the death page pauses the game. Frame 0: one breakable body (about 87 objects, 16 colliders), about 6 detachments, and aboard a kinematic cage of 98 boxes; guessed 2 to 6 ms on the Deck, plus 5 to 30 ms the first time `OA_Model`, blood or decals are drawn in a run (nothing warms them yet). Then 8 single splits over 0.31 s; 14 gibs, about 300 droplets, 14 pops over 8 voices; the cage moved each physics step for 2.6 s; 10 to 30 KB of garbage on frame 0 only. Estimated from the code | 1 body a run; 98 cage boxes; gibs within `maxActiveGibs` 40; frame 0 under 25 ms (if not: warm the shaders, then build the cage ahead, switched off) |
| Ship floodlights (`ShipLightFx`, `OA_Beam`; 16 Nov 2026) | One cone worked out in the shaders, not a Unity light: about 30 more operations per ground, model or water pixel while lit (3-6% of the ground shader), guessed 0.05-0.2 ms with ground filling the screen. Off, or with the eye further than its reach plus 30 m: skipped by a branch on a uniform, guessed 0 ms if the Deck's compiler keeps the branch, the lit cost if it flattens it (not checked). 2 lamp discs (28 triangles, 2 draw calls), 3 global vectors a frame, 0 bytes a frame. A real spot light (no shadow) only under the simple-shader fallback. Estimated from the code, not measured | One beam (one ship); under 0.3 ms on the Deck, or drop it from water first, then from models |
| Moon rover (`Rover/MoonRover`; 16 Nov 2026) | 51,000 triangles drawn as built (body 20,214, four wheels of 6,844, four arms of 916), about 12 draw calls, one shader; no far copy yet. Each physics step while it rolls: 4 raycasts, one box overlap against hitboxes and loose pieces (only above 3 m/s), about 20 forces, 0 bytes after the first hits. Parked with nobody in it, it is fixed to the ground and costs nothing but its draw calls. Model file 0.94 MB. Estimated from the code and files, not measured in Unity | One rover. If the Deck needs it: a far copy of the wheel (most of the triangles are its chevrons) and the body, as the enemies have |
| Restless camps (`LullRules`, `EnemySpawner.Restless`; 24 Nov 2026) | Once a think (0.4 s): two loops over at most 12 present enemies (distance, `AwareOfPlayer`), no allocation; a call sets a place on one squad (3 to 5 `SendToLook`). After a quiet of 35 s (20 s at the floor) with nobody within `callReach` (240 m), the nearest ordinary camp may wake from up to `wakeReach` (200 m, under 0.95 of `sleepRange`) instead of 150 m: the same wake cost as any wake (one enemy a frame); that camp is remembered and called after a 12 s grace, and nothing else wakes for the lull meanwhile. Never while driving. Guards and caves keep their own ranges. Estimated from the code, not measured | 1 call or 1 further wake per quiet; still `liveCap` 12, the fallen included |

## Earth scenery (14 Nov 2026): estimates from reading the code, not measured in Unity

Counted and timed by the deck-performance agent on a desktop processor outside Unity. Deck figures are guesses.

| System | Cost | Cap |
|---|---|---|
| Earth scenery models | 80 shared meshes in 3 files (2.4 MB). Trees average 1,225 triangles (largest 1,608), plants 411, rocks 1,166. One draw call each, no shadows. Guessed 350,000 to 400,000 triangles existing in a dense wood, about a third in view, on top of the ground. | 500 things (`Feature Cap`), within 170 m; tree 1,700, plant 620, rock 1,400 triangles. At the cap with only trees: 804,000, over budget. **First things to lower if a wood is slow: `Feature Cap`, then the trees' `perHectare` in `PlanetProfiles.Earth()`.** |
| Scenery physics | trees 1 capsule; rocks 1 mesh of 96 to 192 triangles; small plants none. Cheaper than the old drawn-mesh colliders. | solid shape 220 triangles |
| Reading the scenery files | when Earth's ground is got ready (`SceneryModels.Warm`), main thread: 6.0 / 1.3 / 1.3 ms on a desktop; guessed 20 to 60 ms on Deck for the trees | once per run |
| Putting a tree out | mesh, trunk and solid shape are looked up once per kind and kept (`SceneryModels`): no searching and no garbage per tree after the first | 24 a frame (`Features Per Frame`), a whole tile at once |
| Choosing where scenery goes | 18 rules on Earth, on background workers. The latitude and patch tests run before the ground is sampled, so they save time. About 15 KB of garbage a tile (a name and a colour list per rule): not yet removed. | |

Not done, and worth doing if a wood reads slow on the Deck: draw plants under 2 m only within about 60 m (they cover a few pixels beyond that and spend the cap), a low-detail piece per tree, and dropping the model files' corner lists after the meshes are made (about 6.5 MB held).

What to read off in Unity: F3 `things` in an Earth wood; Stats Tris and Batches there; in the Profiler, the worst frame and GC Alloc flying low over forest and on first approach to a stronghold.

## Bases (14 Nov 2026): estimates from reading the code, not measured in Unity

Timed by the deck-performance agent on a desktop processor outside Unity, before the early give-up and the finer sampling went in. Deck figures are that time multiplied by 3 to 6, so they are guesses.

| System | Cost | Cap |
|---|---|---|
| Choosing where a world's bases go | once per world per run on the main thread, in the same frame as placing its camps. Measured on a desktop before the changes: Jupiter 14 ms, Venus 10 ms, Mars 7 ms, Earth 5 to 9 ms, Moon 0.5 ms. A place is now refused as soon as it is too rough (about half the time in a trial), but each place is looked at in 61 spots, not 25. **The likeliest hitch on approach: read it off in the Profiler.** | 24 bases a world (`mostBases`), 24 places looked at per base |
| Bases standing at once | those within 350 m, kept until 450 m (or while something is parked on them) | 6 (`PlanetBases.ShownCap`) |
| One foundation | 168 triangles, 1 draw call, 1 solid mesh shape; made once per base and kept | 6 at once |
| Base models | 5 draw calls and 2 shared materials a base, no shadows. Drawn triangles: relay outpost 6,498, fuel depot 8,050, dome colony 12,926, mining works 15,098, stronghold 32,990. Above the 6,000 / 12,000 / 20,000 first proposed: agreed as the price of bases worth looking at, with the caps tightened to what was built. A world has few large bases. | small 9,000, medium 16,000, large 34,000 (`BaseKinds.TriangleCap`, rule test `BaseModelTests`) |
| Base physics | 1 solid mesh of closed boxes and drums (216 to 1,052 triangles) plus the foundation, riding the planet's body | 700 triangles; large 1,400 |
| Reading the base models | all five at the start of a run (`BaseView.Warm`), main thread: 1.0 to 4.4 ms each on a desktop outside Unity, guessed 40 to 100 ms in all on Deck, during loading. About 3 MB of garbage each, once. | once per run |
| Keeping rocks off base floors | 0.4 microseconds a check on a desktop, a handful of checks a frame | 50 patches a world |
| Standout landforms: canyons, ridges, rock towers, dunes (19 Nov 2026) | Part of every height sample, on background threads. Height with them and twice the seeded landforms: Earth 3.6 millionths of a second (was 3.7), Mars 4.9 (was 3.1), Titan 3.8 (was 3.5); cloud machine, not a Deck. Guessed 20 to 30 ms a Mars tile on Deck | zero `canyonDepth`, `ridgeHeight`, `spireHeight`, `duneHeight` on a world's profile; `MoreLandforms` back to 1.5 |
| Choosing bases (19 Nov 2026) | One background job a world, 40 to 80 ms on a cloud machine, guessed 200 to 400 ms on Deck; ranks as a ground tile 300 m off and never takes the last core; enemy camps wait for it. One sweep of standing scenery per base when it lands, guessed 2 to 6 ms once a world, not yet batched | 50 bases a world, 40 tries each, 6 shown |

## Deep Breath (14 Nov 2026): estimates from reading the code, not measured in Unity

| System | Cost | Cap |
|---|---|---|
| Physics while slowed | the physics step shrinks with the slowing, so physics runs as often per real second as at normal speed: no extra cost, and none of the saving slow motion would otherwise give | step never below a tenth of normal (`LeastStepShare`); slowest world speed 0.35 (`slowTo`) |
| Catch-up after a hitch while slowed | unknown: may run more steps in one frame than at normal speed. To be read off in the Profiler. | - |
| Rim round the view | one full-screen see-through layer while slowed; guessed 0.1 to 0.3 ms on the Deck's graphics card | 6 s a full meter |
| Meter | its own canvas, 5 elements; rewritten at most 33 times a second while draining | 200 steps |

## Gun ladder (16 Nov 2026): estimates from reading the code, not measured in Unity

| System | Cost | Cap |
|---|---|---|
| Counting kills | one check per player hit; nothing per frame | - |
| Handing over a gun | one gun destroyed and one built (the same work as picking a gun up), at most once per kill | 4 times a run with the default ladder |
| Readout and banner | their own canvas, 2 labels; the line's text is rebuilt only when it changes (a kill) | - |
| Finding the player's guns | a scene search at most twice a second, and only while none is known | - |
| Carried guns (16 Nov 2026) | hidden objects under the player's hand, nothing per frame; a switch is one object shown and one hidden | one per ladder step climbed (4 with the default ladder) |
| Faction A abilities (16 Nov 2026) | No garbage per frame; a few comparisons per enemy (those without an ability return at once); no physics queries. One unlit sphere (about 760 triangles, 1 draw call, no shadow, one shared material) per raised hand and per flare in the air; a property block on each Faction A body's torso and head (2 renderers each leave the SRP batcher). A flare reaches every awake enemy once through one event. Not measured in Unity | One flare per Scout per life, 9.2 s each: at most 12 at the live cap |
| Rare gun at bases | one gun object per standing base that holds one; one extra seeded roll per shot of a gun that can blow up | at most one per world |

## Melee (16 Nov 2026): estimates from reading the code, not measured in Unity

| System | Cost | Cap |
|---|---|---|
| The melee slot (`PlayerMelee`) while an item is held | one overlap sphere (reach + 0.6 m, hitbox, gib and item layers) a frame, every frame an item is held (the in-range cue uses it): a closest-point per collider it finds (what each collider belongs to is looked up once and kept), the targeting sort with no garbage, then a line-of-sight ray only for the 1 to 3 things chosen (a blocked one is dropped and the choice made again, at most 4 times); guessed under 0.1 ms with three enemies in reach (about 40 hitboxes), not measured | 96 colliders a frame (`_overlaps`); nothing at all with no item in hand but the pick-up search over the few melee items |
| A swing's contact | the same hits as bullets (`Mannequin.ApplyHit`), once per swing | 3 things a swing (the mailbox) |
| Knocked-down enemies | one ray a frame per sliding body | lasts about 1.2 s |
| Melee HUD | its own canvas, 6 elements; text rebuilt only when the item or the lock changes; the heat bar is resized only when it moves a whole pixel | - |
| Player screen plates (ante, task, suit, gun, warning strip) | four plates and a strip, about 120 images and 25 labels over four canvases; words and sizes written only when they change; colours set on the renderers (one colour compare per graphic per frame, no canvas rebuild); magazine blocks and kill pips are remade only when the gun or the step changes; plates are two flat quads each, about a fifth of the screen in all | 0 bytes of garbage per frame when nothing changes; not measured on the Deck yet |
| Melee items in the world | one stand-in object (a few primitives) per item lying about; at most one per world at a base, five in the arena | 1 per world |

## Combat in the solar system (Milestone 12, 16 Nov 2026): estimates from reading the code, not measured in Unity

| System | Cost | Cap |
|---|---|---|
| Loose limbs and guns pulled by the solar system (`GravityBody` with `Sank`) | per physics step each awake one gets a neighbourhood search and a pull over the body list; on a world, the air, sea and wind sample, and one full ground height question one step in four. Guessed 0.2 to 0.4 ms per step at the caps on Earth with everything awake. Resting ones are put to sleep after 0.5 s still and then cost nothing | 40 gibs + 12 loose guns |
| Sleeping | target: Profiler Physics **Active Dynamic Bodies** back under 15 within 60 s of a fight ending (if not: the ground's own `MovePosition` may be keeping them awake; look again) | - |
| Marks that follow what they are on (`StuckMarks`) | 48 bullet marks + 48 splats, one transform write each per frame while live, guessed under 0.1 ms; no garbage | 96 |
| Re-centre with blood in the air | up to 500 blood + 160 spark particles copied out and back once per re-centre, guessed under 0.2 ms; the buffers are made when the systems are built | once per 2 km |
| Blood decals (URP Decal feature, Screen Space) | 48 at most, one instanced material, drawn within 60 m. Needs the camera depth texture: a depth copy after opaques is expected (guessed 0.1 to 0.3 ms GPU at 1280 x 800). A depth prepass is not allowed: if the Frame Debugger shows one, set Depth Texture Mode to After Opaques with Depth Priming off, or use edge smoothing Fast on the Deck. Never switch on decal rendering layers | 48 |
| `Physics.gravity` (blood and sparks only) | written when the felt pull has changed by more than 0.5 m/s squared | - |

## Handmade feel (22 Nov 2026): estimates from reading the code, not measured in Unity

| System | Cost | Cap |
|---|---|---|
| Rocks, trees and plants set into the ground one by one (`NaturalPose`) | a few multiplies and one hash per thing when it is shown, nothing per frame. Each rock colour now has up to 9 shared materials (3 shades x 3 warmths) instead of 1, so the SRP batcher sees a few more material switches on scattered ground (guessed under 0.1 ms CPU on the Deck) | 9 materials per scatter colour |

## Lighting (asked for on 11 Nov 2026)

Matthew asked for lighting to be baked in. How that is read, until he says otherwise:

- **No real-time ray tracing.** The Steam Deck cannot afford it at 60 frames a second.
- **Fixed places get baked light.** The ship's interior (Milestone 7) is done: the game's own path tracer follows light from the cabin lamps round the room once and stores the result as a colour at every corner of the cabin's meshes. It is shipped as one small file (`Resources/Ship/LanderBake`, about 47 KB) and costs nothing while playing: no lights, no shadows, no lightmap textures. There is nothing for Matthew to click. If the cabin's shape changes and the file no longer fits, the game notices (fingerprint), lights the cabin flatly, and re-bakes on a background thread, in up to half a minute on a Deck (a guess). The launch base (Milestone 8) will be done the same way, or with Unity's lightmapper if it needs finer shadows than corners can hold (budget then: at most 2 lightmap textures of 1024 x 1024).
- **Not baked:** sunlight coming in through the ship's windows, because the Sun moves. The cabin is lit by its own lamps only.
- **Planets cannot be baked in the editor**, because their ground is made from the run's seed when the run starts and the Sun moves all the time. They are lit directly from the Sun's true position, which is cheap, and anything that can be worked out once when the ground is made (the shading of slopes in the far pictures, the darkness inside caves) is worked out then, not every frame.

## Audio (13 Nov 2026): estimates from reading the code, not measured

| | Cap or expected cost |
|---|---|
| One-shot voices | 24 at most (`VoiceAllocator.DefaultVoices`), plus 6 for interface and reward sounds and 3 for music cues. Unity's own limit of real voices is raised to 48 at start-up so music stems, loops and one-shots all fit. When all are busy the least important, oldest sound gives its voice up; less important sounds are not played. The worst legal case (24 + 6 + 3 + 11 music stems + 4 beds) is exactly 48, so there is no slack: if the Profiler shows sounds being dropped by Unity, the one-shot pool is the thing to shrink. The older code-made weapon and gore sources (18, idle while the produced sounds exist) are still created. |
| Per-event limits | Each event has its own voice limit and cooldown in the audio manifest (a rifle may hold 4 voices, footsteps 2), so one sound cannot fill the pool. |
| Music | One piece plays at a time (4 to 6 stems, each one compressed stream being decoded); two overlap only during a hand-over. Silent layers still decode, and the cues and the two long beds (cabin, engine) are compressed streams too, so the worst moment (a flight hand-over) is 13 to 15 Vorbis decoders for a few seconds, and 5 to 8 the rest of the time. If Audio CPU is high, the fix is to merge stems that always move together. |
| Looping beds | One source each, created when first asked for, stopped (not destroyed) when faded out. In flight: cabin, main engine and up to three manoeuvring thrusters at once (two directions plus turning), so about 5; opposite thrusters never sound together except for a fifth of a second while one fades. |
| Low-pass filters | One per one-shot voice, switched on only where there is thin air or vacuum. In normal air none are running. |
| Reverb | One reverb zone for the whole game, following the listener. |
| Memory | Music stays compressed in memory: about 44 MB for the five pieces going by the source files (Unity re-encodes them, so read the real figure from "Imported Size" in the Inspector). Sound effects are unpacked on load to plain 16-bit sound: about 22.8 MiB for the 218 effect clips the manifest loads, going by the WAV files (27 Nov 2026; `reward.unlock` is 0.53 MiB of it, the creature warnings and strikes 0.98 MiB). Read the real figure as Total Audio Memory in the Profiler's Audio module. If memory gets tight, music can move to streaming from disk (one setting in `AudioImportRules`), at the cost of less exact start times. |
| Per-frame work | A fixed loop over the playing stems and beds to set volumes, and the music director's update (a few comparisons). No allocations in play; the debug panel builds a string ten times a second and lays its text out on every screen event, only while it is open (F9). |
| Loading | Music (24 stems and 5 cues) is read in one go as the game starts, before the first frame: a guess of 0.1 s from internal storage and up to 0.5 s from a microSD card. Sound effects are loaded a few per frame during the first seconds on the main menu, so the first gunshot does not hitch. The audio device is also restarted once at start-up to raise the voice limit to 48; moving that into project settings (`ProjectSetup.cs`) would remove the restart. |

Not measured: none of this has run in Unity or on a Deck. The first thing to look at there is Unity's Profiler, Audio section: "Playing Sources" should stay at or under 48 and "Audio CPU" under about 10%.

## What the game checks by itself

While you play, the top-left readout has a line like:

`Budget 16.7 ms: OK   1% worst 15.9 ms   2% of frames over`

- **OK**: within budget.
- **TIGHT**: more than 5% of the last ten seconds of frames missed 16.7 ms, but no real hitches.
- **OVER**: more than 1% of frames were slower than 25 ms.
- **1% worst**: only one frame in a hundred was slower than this. It is what stutter feels like, which an average hides.

It only counts frames while a run is live (not in menus or while paused, or in the 2 frames after a pause ends). When a run ends or you quit, one summary line is written to the Console (and to `Player.log` in a built game), starting with `Orbital Ante performance`.

Note: in the Unity editor the numbers are worse than a real build, because the editor itself takes time each frame. Editor numbers are only good for comparing one change with another. The real test is a build on the Deck.

## How to measure (when you are ready, not needed for every playtest)

**In the editor, a rough look**
1. Press Play, start a run, press **H** to start the fight, and shoot limbs off for a minute.
2. Read the Budget line top-left. Tell me what it says (OK, TIGHT or OVER) and the two numbers.
3. In the Game view's top-right corner click **Stats**. Tell me the **Batches** and **Tris** numbers during the busiest moment.

**On the Steam Deck, the real test** (I will write the exact build steps when we get to the first Deck build; there is no Deck build yet)
1. Make a Linux build and copy it to the Deck.
2. Play the same minute of fighting.
3. Read the Budget line, and send me the `Orbital Ante performance` line from `Player.log`.

## Not decided yet

- Whether busy scenes later (planet surfaces, launch) hold 60, or the game should offer a locked 40 fps mode on Deck. The budget asset already has the 25 ms floor so that choice stays open.
- Shadow distance, anti-aliasing and render scale. These wait for the first measurements; guessing them now would be tuning blind.

## Save reliability refinement: source review only

Save checkpoints add up to two full-file UTF-8 reads with version-header-only deserialization, plus one read/deserialization of the old main before deciding whether to rotate the backup. The existing JSON serialization, temporary-file allocation/write and synchronous `Flush(true)` remain; promotion now uses `File.Replace` for an existing main. Writes happen at run start, every run end (records), menu return, quit, an envelope won or opened, the title screen's Unlocks page when something is new, and mid-run when a goal unlocks something new (at most 10 per profile, one write the frame after the goal). None per frame. `SaveSystem.Save` carries the profiler marker `OA.SaveSystem.Save`. A latched newer-save or failed-load protection skips subsequent writes and I/O until reload/reset.

Current save data is small. File size, allocations, storage latency and checkpoint-frame time have not been measured in Unity or on Deck; there is no promised maximum file-size cost. Profile `SaveSystem.Save`, `SaveFileStore.Write` and `FileStream.Flush` on internal storage and microSD. See `docs/reviews/save-recovery-and-version-protection.md` for the regression evidence and target-runtime checks.

## Enemy cover and flanking (16 Nov 2026): estimates from reading the code, not measured

| | Cap or expected cost |
|---|---|
| Cover searches | 1 a frame for every enemy in every place (`CoverFinder.SearchesPerFrame`); an enemy searches again only when the player has moved 6 m, after 9 s, when its cover is exposed or after 2 s with nothing found |
| One search | one overlap sphere (none in caves), at most 14 ground rays, at most 10 places line-checked at up to about 14 checks each: typically 40 to 80 physics queries, at worst about 160 (estimate 0.1 to 0.3 ms on the Deck, not measured) |
| Hiding enemies | two short line checks each, twice a second (is the cover still hiding it) |
| Squad roles | plain C#, a loop over the squad's members per think; no allocations after the squad is built |
| Cave routes | 8 remembered routes per camp, searches still capped at `CaveFooting.PlansPerFrame` (2) |


## Prize envelopes (16 Nov 2026): estimates from the offline tool, not measured on the Deck

| Work | Cost and cap |
|---|---|
| Painting the envelope pieces | 13 pictures, about 0.55 s in all under .NET 8 on the cloud machine (`tools/envelope_view pieces` prints each). Mono in Unity is likely 2 to 3 times slower. Painted one piece a frame while the title screen shows (`EnvelopeDirector.PaintAhead`), so the worst single frame is the seal, about 120 ms (.NET): one hitch on the title screen, never in a run. |
| Texture memory | About 5 MB, uncompressed RGBA, kept for the session (`EnvelopeTextures`). One card (280 x 392) is painted per reveal and replaces the last. |
| The screen while showing | One canvas, about 30 to 60 images and labels, laid out from `EnvelopeScene.Frame` each frame (one list, reused; a few small allocations a frame). Only while the envelope shows (run start and reveals), never during play. Off: nothing. |

## Ante loop and tasks (20 Nov 2026): estimates from reading the code, not measured in Unity

The Steam Deck review's worst finding (up to about 225 primitives built in one frame on arriving at a world) was fixed before merge: views are built one per slow pass and never in the frame a world's tasks are laid out, pieces use shared meshes with colliders only where solid, and the lights are a small shared pool.

| | Cost | Cap |
|---|---|---|
| `AnteLoop` readout | One canvas, 9 labels with halo; text rewritten only when it changes (clock once a second, score each frame only during a count-up of at most 1.2 s). About 100 B of garbage a second at rest. Guessed under 0.05 ms a frame | 4 "+points" lines |
| `TaskDirector` every frame | Carrying, landing, supply drops; one `LivingWithin` (up to the live enemy count) while a drop is on the ground. Interact prompts are cached and the key name is looked up once a second. Guessed under 0.05 ms, 0 B a frame | 1 drop at a time |
| `TaskDirector` slow pass (4 a second) | Distance tests in the world's own space against positions stored at layout (no ground questions), mast calls, building at most one view, snapping views within 250 m once a second, the spotted check over woken enemies, choosing lights over at most about 80 tasks. Guessed 0.05 to 0.15 ms on a Deck | 50 bases, about 25 masts, 2 crates a world |
| Laying out a world's tasks | Once per world per run: about 27 height questions plus a loop over bases and camps. Guessed under 1 ms on a Deck, in a frame of its own | once per world |
| Task views (Props model, 27 Nov 2026) | Mast: within 150 m the model (footing 1,396 triangles, mast 2,393, three cells of 698 with a 248 lit piece each: about 6,600 triangles, 8 draw calls, 5 shadow casters); beyond 175 m the old plain shapes (about 210 triangles, 6 draw calls, no shadow). Crate 1,958 + 352 lit, 2 draw calls, 1 caster (none while carried); pod 2,195 + 428 lit, 2 draw calls, 1 caster; cave lamp 2,200 + 44 lit, 2 draw calls, no shadow, 6 to 14 a cave network. Built within 900 m, put away beyond 1.1 km (colliders too). Both model files are read when the solar world starts, not mid-play. Counted from code, not measured | at most 2 or 3 masts wear the model at once (about 20,000 triangles); about 150 draw calls if every mast on a world were in view as plain shapes |
| Melee items in the world (Melee model) | 708 to 2,590 triangles, 1 or 2 draw calls, casts a shadow while loose (the sword's blade never) | 1 a world; 5 in the arena |
| HUD plates and menus (Hull stencil, 27 Nov 2026) | About 120 graphics on 4 existing canvases (no new canvas), 5 small code-made pictures, 2 more dynamic fonts; guessed 25 to 40 UI batches and 0.37 of a screen of blended fill; words written only on change, colours and fades on the renderers; the clock re-batches its plate once a second. Menus: one 24 px plate picture, 4 screws a card, one `ChosenInk` a button (no Update). Not measured. | 5 plates, at most 60 graphics a plate, 0 bytes a frame when idle, no Outline on a line that changes more than once a second |
| Task lights | Unlit opaque cylinders, 1 draw call each, no shadow or transparency; about 2 px wide; no taller than the world is wide; hidden within 25 m | `liveNearby` (3) shared, plus one per falling or landed drop |
| Colliders on the planet body | Every solid task piece is a shape of the world's kinematic rigidbody. Cost per shape not measured | about 150 a world at most |

## Ante break and run build (20 Nov 2026): estimates from reading the code, not measured in Unity

| | Cost | Cap |
|---|---|---|
| `RunEffects` questions | A few multiplies per shot, blow or enemy hit; the build is rebuilt only when it changes (between antes). 0 B a frame | none needed |
| `RunPerksFx` | Runs only on an enemy kill. 0 B at rest | one kill at a time |
| `EnemyPressure` | One hash per enemy woken (int overload, no strings); sharpness is a number on each enemy, rewritten over the live enemies only when an ante changes it | once per ante |
| Break pages | UI built once (7 buttons) and relabelled; only while the game is paused between antes | 7 buttons |

## Run guide (21 Nov 2026): estimates from reading the code, not measured in Unity

| | Cost | Cap |
|---|---|---|
| `RunGuideFx` | One canvas, 2 labels with halo. The "left to score here" line counts the current world's bases, masts and crates twice a second (short lists, no allocation) and rewrites its text only when it changes; the tip check runs only between tips. Guessed under 0.02 ms a frame | 1 tip on screen |

## Fun pass: star chart, sized pops, pace tick, recap (21 Nov 2026): estimates from reading the code, not measured in Unity

| | Cost | Cap |
|---|---|---|
| `StarChartView` | Its own canvas (order 110), built at boot with 3 cards and 3 buttons, inactive until opened: no work a frame while closed. The 640 x 400 sky texture (1 MB, kept for the session) is made on the first open, while paused (guessed a few ms on the Deck). Open (game paused): the pulse fades 1 to 2 images with `canvasRenderer.SetAlpha` (a re-batch of this canvas, about 100 graphics, guessed 0.1 to 0.3 ms); a card change rebuilds about 60 chart objects (guessed 1 to 3 ms and some garbage); 20 to 40 UI draw calls. The 3D world keeps rendering behind the cover, as under the pause menu (a follow-up for both: stop the world cameras while a full-screen page is up) | open only while paused; 5 families x 3 stars |
| Sized pops | Size, colour and text set once per award; the fade rebuilds each label only in its last 0.5 s; one reward sound per feat or big award (the sounds' own cooldowns cap them) | 4 lines |
| Pace tick | `TargetPace.Read` every frame: a few divides, no allocation; its words are built only when the status, points a minute or the remainder change; the tick moves only on a whole-pixel change | 1 tick, 1 label |
| `RunRecap` | Integer adds per award, no allocation; text built once at the end of a run | one per run |
| Aim assist | Stick only, gun in hand: per present enemy two angle checks (head, chest; about 40 for 20 enemies), then one raycast to the best; no allocation (`Mannequin.Present` is a kept list). Nothing with a mouse or no gun | 1 ray a frame |

## Critique loop round 1 (26 Nov 2026): estimates from reading the code, not measured in Unity

| | Cost | Cap |
|---|---|---|
| Seen warning (`ThreatCues`, `DamageFeedback`) | One static event per fresh sighting (an enemy that had not seen the player for its chase memory); the listener does a dot product per arc slot and sets one rotation. Each frame 3 more `SetAlpha` calls on the damage canvas while an arc shows; nothing while none does | 3 arcs, 1 sting per 4 s |
| Radio call | One event, one sound and one note when an armed squad not already coming is sent; one sound per overtime hunt alarm | the lull's own pace (20 s or more); every 15 s in overtime |
| Clock cues (`ClockCues`) | Two ceilings and compares a frame, no allocation | 1 warn + 5 ticks an ante |
| Pulse (`PulseClock`) | A few float compares a frame; one short sound a beat while on the last plates | 80 beats a minute |
| Notes (`NoteBoard`) | Two float subtractions a frame; the label is touched only when the text changes | 1 showing, 1 waiting |
| Jump grace (`JumpGrace`) | Three float compares a physics step | none |

## Critique loop round 2 (26 Nov 2026): estimates from reading the code, not measured in Unity

| System | Cost | Cap |
|---|---|---|
| Readout focus (`ReadoutFocus`, `AnteLoop.InFight`) | A subtraction and compare per frame, no allocations | At most 3 `SetActive` calls (two "+points" rows, the task line) when a fight starts or ends, at most one flip per 6 s |
| Selection hold (`SelectionHold` in `PauseMenu`) | Float arithmetic and one `IsPressed()` per frame, only while a menu page is open | One selection per page opening |

## Goal unlocks and starting kits (Ralph loop pass 1, 27 Nov 2026): estimates from reading the code, not measured in Unity

| | Cost | Cap |
|---|---|---|
| `AnteLoop.UpdateUnlocks` (every frame) | A few compares, no allocation. A note's and the banner's text are set once per unlock, the kit note once a run; while the banner shows (3.5 s) one `canvasRenderer.SetAlpha` a frame, no canvas rebuild | 1 banner and 1 note at a time |
| Counting goals (per award, kill and touchdown; the ladder's top once a run) | One set add, one bit or a few bool tests; no loop over enemies | none needed |
| Drafts | Locked perks are taken out once per ante (one small set and list); a newly unlocked perk is put in the first draft that can use it (one list look-up and replace per draft rolled); its star chart card gets one more coloured word | once per ante |
| Saving an unlock mid-run | `SaveSystem.Save` on the main thread: three reads, three parses, one pretty write, a temporary file flushed to disk (`Flush(true)`) and renamed over the save. A few KB of file, about 25 to 60 KB of garbage. The flush is the cost: not measured (guessed 1 to 20 ms on the Deck's own storage, longer while Steam writes). All goals met in one frame share one write, made the frame after (`AnteLoop.WriteOwedUnlockSave`), never on the kill's own frame. If the Deck shows it long, write it at the next pause instead | only a goal not earned before: at most 10 writes per profile |
| Break, victory and death pages | One or two more lines in a body set once per page. Each page shown measures its body once with the label's own text generator (`PauseMenu.WordsHeight`: one layout pass of a few hundred characters) and runs `MenuFit` arithmetic; bodies and buttons use best fit, which Unity works out only when the text or size changes | paused only |
| Title screen Kit and Unlocks pages | Two cards built at boot, inactive until shown. Kit details written when the cursor moves to another kit; one rich-text label per visit; a save if anything was new | title screen only |
| `reward.unlock` | 2.9 s stereo, unpacked on load in the title screen's warm-up: 0.53 MiB. One voice, 1.5 s cooldown | one per unlock |

## Creature warnings on the visor (Ralph loop pass 2, 27 Nov 2026): estimates from reading the code, not measured in Unity

| | Cost | Cap |
|---|---|---|
| Warning stage (`Windups`) | One switch on a few fields a creature a frame, no garbage; `CombatWorld.CreatureWindup`, `AudioDirector.LockMark` and `Windups.PitchFor` once per warning | one event per warning |
| Arcs that follow (`DamageFeedback`, `WindupCues`) | The eye's place and axes are read once a frame (`DamageFeedback.Eye`); every arc showing is re-angled with three dot products and an angle, and turned only on a move of 0.25 degrees or more. While turning, one re-batch a frame of the 20-image damage canvas (which a fading arc's `SetAlpha` causes anyway); a mesh rebuild of one arc only during its 0.08 s snap; no new draw call (same ring sprite) and no garbage. Guessed under 0.1 ms on the Deck | 4 red, 3 seen and 3 creature arcs |
| Creature warning and strike sounds | 11 clips, 10.6 s, 0.98 MiB unpacked on load; 2 or 3 more frames of the title screen's warm-up, at most 0.68 MiB in one of them; nothing loads mid-fight. One voice a sound (0.36 to 2.6 s), priority 55 (above impacts and gore, below guns, melee and the player's gear) | 2 voices and a 150 to 400 ms cooldown per event |

To read off in Unity: in the arena, turn hard while two creatures warn and read `DamageFeedback.LateUpdate` in the Profiler's CPU Usage (expect 0 B of GC Alloc) with `Canvas.BuildBatch`; the Audio module's Total Audio Memory should be about 1 MB more than before and Playing Sources under 48.
