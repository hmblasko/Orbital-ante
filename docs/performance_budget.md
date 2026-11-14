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
| Ship outside | about 37,000 triangles (fittings about 34,000 from `Models/Lander`, of which the two camera booms and the landing camera are about 4,200, counted from the file; hull and details about 3,000), about 15 draw calls, one shader. Estimate, not measured in Unity. | fittings 36,000 triangles (a rule test fails above it); one ship in Phase 1. First trim if needed: one bevel step fewer on the base's foil panels. |
| Reading the lander model | 914 KB, once per launch on the main thread; 4 ms warm, 19 ms cold measured on a desktop outside Unity, guessed 20 to 80 ms on a Deck | once per launch |
| Lander model memory | about 1.1 MB kept, about 1.0 MB on the graphics card (estimate) | - |
| Round shading of the cabin dome | worked out once when the ship is built: under 5 ms on a desktop outside Unity | - |
| Cabin | 11,758 triangles in 11 pieces, vertex colours only, no textures, no lights | fixed |
| Glass | 5 panes, one see-through layer each | 5 |
| Crack lines | up to about 4,850 triangles a pane (about 24,000 if all 5 are badly cracked); rebuilt only when a new hit is recorded or the crack stage changes, guessed 3 to 8 ms each time on Deck | 6 recorded hits a pane |
| Lift beam | one see-through layer, full screen while riding | 1 |
| Physics | 1 rigidbody, 23 colliders (2 convex, 8 capsules, 8 spheres, 5 boxes), speculative fast-collision checking | fixed; cost in a crash unknown until measured |
| The player aboard | no physics at all | - |
| The player outside | 1 rigidbody, 1 capsule, 1 sphere cast per physics step | - |
| Wreckage (torn-off parts) | 1 rigidbody each, pulled by gravity every step | 9 pieces (4 legs, engines, 4 thruster clusters); removed once 400 m from both you and the ship |
| Building the ship at run start | guessed 100 to 250 ms on Deck, on the main thread (20 ms for meshes and 9 ms for the bake file on a desktop; sounds not timed) | once per run |
| Sounds | 15 short clips, about 0.64 MB, 2 audio sources; freed on restart | fixed |
| Baked light | 31 KB file, read once at start | - |
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
| Guns at once | 10 in the arena (8 mannequins, yours, 1 spare), 1 in the solar system | 12, until dropped and enemy guns get a simpler copy of about 1,500 triangles (needed before Phase 2 enemy counts) |

### Launch base model (look pass 2)

| | Cost | Cap |
|---|---|---|
| `Resources/Models/LaunchPad.bytes` | About 31,600 triangles in 17 pieces (10 solid, 7 that shine), one base in the world | 36,000 triangles |

### Enemies (look pass 2)

| | Cost | Cap |
|---|---|---|
| Enemy model (`Resources/Models/Enemy.bytes`) | About 14,850 triangles in 14 pieces (one per breakable part), one shared material, 14 draw calls. In the arena, where the sun casts real shadows, an unseen copy of each piece casts the shadow, so the triangles are drawn again per shadow cascade. No shadow copies on planets or in space. | 16,000 triangles (the rule test `EnemyModelFile_MatchesTheSkeleton` enforces it) |
| Enemies at full detail | About 119,000 triangles per pass for the arena's 8 | 12, until a far copy of about 3,000 triangles exists (needed before Phase 2 enemy counts); then 30 with at most 8 at full detail |
| Enemy animation (`BodyAnimator`) | Worked out per enemy per frame, no stored clips, no garbage. 45 microseconds for 30 enemies on a desktop outside Unity; not measured on the Deck | 30 |
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

## Lighting (asked for on 11 Nov 2026)

Matthew asked for lighting to be baked in. How that is read, until he says otherwise:

- **No real-time ray tracing.** The Steam Deck cannot afford it at 60 frames a second.
- **Fixed places get baked light.** The ship's interior (Milestone 7) is done: the game's own path tracer follows light from the cabin lamps round the room once and stores the result as a colour at every corner of the cabin's meshes. It is shipped as one small file (`Resources/Ship/LanderBake`, about 31 KB) and costs nothing while playing: no lights, no shadows, no lightmap textures. There is nothing for Matthew to click. If the cabin's shape changes and the file no longer fits, the game notices (fingerprint), lights the cabin flatly, and re-bakes on background threads in a few seconds. The launch base (Milestone 8) will be done the same way, or with Unity's lightmapper if it needs finer shadows than corners can hold (budget then: at most 2 lightmap textures of 1024 x 1024).
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
