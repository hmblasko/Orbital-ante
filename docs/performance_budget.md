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
| Ground made solid per frame | 1 piece (2 when the ship is within 12 m of it). Only ground within 90 m is solid, more when moving fast, never beyond 260 m. This is the likeliest cause of a stutter while landing: it is still done on the main thread. |
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
| Ship outside | about 32,000 triangles (fittings about 30,000 from `Models/Lander`, counted from the file; hull and details about 3,000), about 15 draw calls, one shader. Estimate, not measured in Unity. | fittings 34,000 triangles (a rule test fails above it); one ship in Phase 1. First trim if needed: one bevel step fewer on the base's foil panels. |
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

## Lighting (asked for on 11 Nov 2026)

Matthew asked for lighting to be baked in. How that is read, until he says otherwise:

- **No real-time ray tracing.** The Steam Deck cannot afford it at 60 frames a second.
- **Fixed places get baked light.** The ship's interior (Milestone 7) is done: the game's own path tracer follows light from the cabin lamps round the room once and stores the result as a colour at every corner of the cabin's meshes. It is shipped as one small file (`Resources/Ship/LanderBake`, about 31 KB) and costs nothing while playing: no lights, no shadows, no lightmap textures. There is nothing for Matthew to click. If the cabin's shape changes and the file no longer fits, the game notices (fingerprint), lights the cabin flatly, and re-bakes on background threads in a few seconds. The launch base (Milestone 8) will be done the same way, or with Unity's lightmapper if it needs finer shadows than corners can hold (budget then: at most 2 lightmap textures of 1024 x 1024).
- **Not baked:** sunlight coming in through the ship's windows, because the Sun moves. The cabin is lit by its own lamps only.
- **Planets cannot be baked in the editor**, because their ground is made from the run's seed when the run starts and the Sun moves all the time. They are lit directly from the Sun's true position, which is cheap, and anything that can be worked out once when the ground is made (the shading of slopes in the far pictures, the darkness inside caves) is worked out then, not every frame.

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
