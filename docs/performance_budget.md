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
