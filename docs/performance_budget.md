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
| Background workers | 3 (`Background Workers`). Painting far-away pictures never takes the last free one, so ground under you is never kept waiting. |
| Rocks, trees and other placed shapes | 500 at most (`Feature Cap`), only within 170 m for loose rocks, and at most 24 put out per frame |
| Solid placed shapes | only those taller than 1.2 m get a collider |
| Far pictures | 9 planets at 1024 x 512 and 10 moons at 512 x 256, with smaller copies for distance: about 19 MB. Painted in the background over the first half minute of a run. |
| Caves | each cave is one mesh and one collider, built in the background when you come near its body |
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
| Ground shader (ground, rocks, outside of the ship) | 1.5 to 3.5 ms with ground filling the screen. Soft light, sky fill, bounce, rim, haze into the sky colour, and up to three lookups of painted noise. | Small dabs of paint are only worked out within 320 m and the finest within 45 m; none where the far-away picture has taken over; none for the baked cabin. `Paint` 0 on the Look asset removes the rest. |
| Model shader (`OA_Model`: enemy bodies, guns, base buildings, the cockpit's camera fittings) | The ground shader's light and paint without the far-away picture, plus shine (a highlight from the Sun and from the lamp, and one more reading of the sky colour) on every corner a model marks as smoother than cloth (above 0.1; full from 0.3). Counted outside Unity with a different compiler on 14 Nov 2026, not measured: a model pixel is about 530 steps beyond 23 m, 745 nearer and 960 within 3 m, the same as under the ground shader to within 2%; shine added about 190, a fifth to a third more, before the lamp's highlight and the spreading of small highlights were added (a guessed 50 more, not counted). Guessed on a Deck: under 0.1 ms in an ordinary fight, 0.2 to 0.5 ms with one enemy in your face, 0.45 to 0.9 ms if shiny surfaces fill the screen (a base wall, once bases say what they are made of). Today it costs nothing: no shipped model has a finish. On the proposed realistic enemy about 70% of the surface is cloth and webbing under the 0.1 floor and skips the work. Draw calls do not change, but models and ground no longer share a shader, so SetPass calls may rise (guessed 10 to 30 in a base fight; to be read off the Stats window). A finish is 2 bytes a corner in the file, 8 on the graphics card and 8 kept in ordinary memory: 143 KB of each for the proposed enemy with its far copy, 2.3 MB of each if every model carried one. The first model with a finish to be drawn makes the graphics card build one new program: a one-off hitch, guessed 5 to 30 ms; nothing warms shaders today. The finest paint dab within 3 m (about 215 steps) costs more than the shine and could be dropped on models once the held gun has been looked at without it. | `Shine` 0 on the Look asset (to be confirmed in Unity that it skips the work). Lower `Full Bodies` before touching this. |
| Sky ball | 0.2 to 0.5 ms. One draw call, about 1,000 triangles, one texture lookup. | Known follow-up: draw it after the ground instead of before, so covered sky is not paid for. |
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
| Rifle | about 7,800 triangles, 2 pieces | 9,000 triangles a gun (a rule test fails above it) |
| Pistol | about 3,900 triangles, 2 pieces | 9,000 triangles a gun |
| Smg, Shotgun, Sniper (the enemy kinds' guns) | 4,124, 5,668 and 6,140 triangles. Smg has 2 pieces; Shotgun and Sniper have 3: the third (`CellGlow`, `LensGlow`) is the part that shines and is drawn with one shared glowing material, so each of those guns is one draw call more than the rifle. Files 120, 181 and 189 KB, read the first time that gun is made. Counted from the files, not measured in Unity | 9,000 triangles a gun (a rule test fails above it); a shining piece stays under 400 triangles |
| Guns at once | 10 in the arena (8 mannequins, yours, 1 spare); 13 in the solar system (12 enemies' and yours), 31 if 30 enemies are ever present, plus up to 12 lying loose. Beyond 9 m (stretched by its holder's size) a gun is drawn as its far copy (`RifleFar` and so on): about 1,500 to 2,000 triangles against 3,868 to 7,696, in the same 2 or 3 pieces, so its draw calls do not fall. A gun in an enemy's hands is never drawn finer than the body holding it, so full guns are held to the count of full bodies. Guns lying loose are not counted: 12 of them within 9 m is up to 92,000 triangles. Counted from the files, not measured in Unity | far copy 2,200 triangles a gun, under 0.45 of the full gun's triangles and 0.62 of its corners (rule test) |

### Launch base model (look pass 2)

| | Cost | Cap |
|---|---|---|
| `Resources/Models/LaunchPad.bytes` | About 31,600 triangles in 17 pieces (10 solid, 7 that shine), one base in the world | 36,000 triangles |

### Enemies (look pass 2)

| | Cost | Cap |
|---|---|---|
| Enemy model (`Resources/Models/Enemy.bytes`) | About 14,850 triangles in 14 pieces (one per breakable part), one shared material, 14 draw calls. In the arena, where the sun casts real shadows, an unseen copy of each piece casts the shadow, so the triangles are drawn again per shadow cascade. No shadow copies on planets or in space. | 16,000 triangles (the rule test `EnemyModelFile_MatchesTheSkeleton` enforces it) |
| Three more enemy bodies (`Models/EnemyRunner`, `EnemyBruiser`, `EnemyMarksman`) | 12,520, 13,510 and 14,020 triangles drawn (the files hold 284 more each for the wound stump), each in the same 14 pieces on the same skeleton, drawn with the same shared material: one more kind costs no more per enemy than the trooper. Files 385, 416 and 440 KB, read the first time that kind is made. Counted from the files, not measured in Unity. Each body, the trooper included, has a far copy (`EnemyFar`, `EnemyRunnerFar`, `EnemyBruiserFar`, `EnemyMarksmanFar`): 4,473, 4,469, 4,476 and 4,462 triangles and 4,100 to 4,700 corners (the full bodies have 8,400 to 9,900) in the same 14 pieces, files 162 to 175 KB | 16,000 triangles each; 4,800 a far copy, under 0.4 of the full body's triangles and 0.62 of its corners (rule tests fail above these) |
| Enemies drawn, full and far copies | A body is drawn in full only within 24 m (20 m Runner, 31 m Bruiser, 29 m Marksman: about 36 pixels tall on the Deck's screen there), and only the nearest 12 in view; the rest wear the far copy, and so does a gun in a far body's hands. Per pass, for the roster's mix with guns: **12 enemies** 76,000 triangles with nobody near, 238,000 with all 12 close (the same as before far copies, because 12 are allowed in full). **30 enemies** 191,000 with nobody near, 353,000 at the very worst (12 full, 18 far); drawn all in full they would be 596,000. With the Look asset's `Full Bodies` at 8 the worst is 299,000. **Draw calls do not fall:** 14 a body and 2 or 3 a gun whichever copy shows, so 196 for 12 enemies and about 490 for 30, a third of the whole budget; twice that, and twice the triangles, if a depth pass runs (blood decals may switch one on; not yet looked at in Unity). In the arena add the shadow copies, which are swapped too. Counted from the files, not measured | 12 full bodies (`DetailRule.FullBodies`; dial `Full Bodies` on the Look asset). `liveCap` stays 12 until it has been tried at 20 and then 30 on a Deck, mid-fight in an Earth wood beside a base, with the Budget line reading OK, Batches under 1,500 and Tris under 600k |
| Proposed realistic common enemy (**not in the game**; `tools/blender/build_enemy_realistic.py`) | Built outside the game on 14 Nov 2026 and counted: 21,188 triangles drawn (21,472 in the file), 13,659 corners; first far copy 4,486 triangles, 4,256 corners; files 654 and 178 KB. Over the 16,000 cap, so the rule test fails until the cap is changed. 12 full and 18 far: 335,000 body triangles against 259,000 today; with guns about 429,000 against 353,000. By this document's own figures a fight in an Earth wood with 12 present goes from about 598,000 triangles to about 674,000, over the 600,000 line. Its average triangle is smaller than a pixel beyond 9 m and a quarter of a pixel at 18 m, so much of the detail is wasted at fighting range. Guessed 0.2 to 0.8 ms more per pass on a Deck. | If adopted: slim the model first (the detail nobody can see at 9 m); cap 22,000; and `Full Bodies` 8 (268,000, about today's figure) or `Far Copy Distance` 0.5 until measured on a Deck |
| Enemy animation (`BodyAnimator`) | Worked out per enemy per frame, no stored clips, no garbage. Each kind carries itself its own way through one shared `BodyStyle` (eleven numbers, made once; nothing per frame). 45 microseconds for 30 enemies on a desktop outside Unity; not measured on the Deck | 30 |
| Reading enemy and gun models | Every roster body and gun is read, with its far copy, when the spawner is built (the rifle when the player's own is made), on the main thread during loading; nothing is read mid-fight. Outside Unity on a desktop: 3.3 ms for the nine full models and about 1 ms more for the nine far copies; guessed 35 to 100 ms in all on the Deck. The far copies add 1.0 MB of files, about 1.3 MB of kept data and about 1 to 1.2 MB on the graphics card | once per run |
| Choosing full or far (`ModelDetail`) | Every watched body and gun is looked at together 5 times a second, not every frame, and once more on the frame something new is made. The rule alone took under 1 microsecond typically and 6 at worst for 61 things on a desktop outside Unity; the whole look, with the check for limbs that have come off, is guessed at 30 to 120 microseconds on the Deck for 30 enemies and 31 guns. A body changing copy re-points 14 meshes (28 in the arena): guessed 30 to 100 microseconds. Limbs and bodies lying loose are watched one piece at a time by distance alone. About 3 KB of garbage when an enemy is made, none per look | 0 bytes a look; working lists hold 128 things before they are re-made |
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
| Keeping rocks off base floors | 0.4 microseconds a check on a desktop, a handful of checks a frame | 24 patches a world |

## Deep Breath (14 Nov 2026): estimates from reading the code, not measured in Unity

| System | Cost | Cap |
|---|---|---|
| Physics while slowed | the physics step shrinks with the slowing, so physics runs as often per real second as at normal speed: no extra cost, and none of the saving slow motion would otherwise give | step never below a tenth of normal (`LeastStepShare`); slowest world speed 0.35 (`slowTo`) |
| Catch-up after a hitch while slowed | unknown: may run more steps in one frame than at normal speed. To be read off in the Profiler. | - |
| Rim round the view | one full-screen see-through layer while slowed; guessed 0.1 to 0.3 ms on the Deck's graphics card | 6 s a full meter |
| Meter | its own canvas, 5 elements; rewritten at most 33 times a second while draining | 200 steps |

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
| Looping beds | One source each, created when first asked for and destroyed when faded out. Today at most 4 at once (engine, cabin, wind, breath). |
| Low-pass filters | One per one-shot voice, switched on only where there is thin air or vacuum. In normal air none are running. |
| Reverb | One reverb zone for the whole game, following the listener. |
| Memory | Music stays compressed in memory: about 44 MB for the five pieces going by the source files (Unity re-encodes them, so read the real figure from "Imported Size" in the Inspector). Sound effects are unpacked on load to plain 16-bit sound: about 12.5 MB. If memory gets tight, music can move to streaming from disk (one setting in `AudioImportRules`), at the cost of less exact start times. |
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

It only counts frames while a run is live (not in menus or while paused). When a run ends or you quit, one summary line is written to the Console (and to `Player.log` in a built game), starting with `Orbital Ante performance`.

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
