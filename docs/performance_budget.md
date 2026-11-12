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

## Lighting (asked for on 11 Nov 2026)

Matthew asked for lighting to be baked in. How that is read, until he says otherwise:

- **No real-time ray tracing.** The Steam Deck cannot afford it at 60 frames a second.
- **Fixed places get baked light**: the ship's interior (Milestone 7) and the launch base (Milestone 8). Unity's lightmapper traces light paths once, in the editor, and stores the result in textures, so soft bounced light costs almost nothing while playing. Budget: at most 2 lightmap textures of 1024 x 1024 per place (about 5 MB each), baked by one menu click.
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
