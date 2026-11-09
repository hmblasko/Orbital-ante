# Milestone 4: foundation (kept lean)

Just enough plumbing for the rest of Phase 1: seeds, saving, data-driven content, and a
performance budget for the Steam Deck. No new gameplay.

## What's in it
**Seeded randomness** (already there since M0, now used everywhere it matters)
- One run seed. Every system asks for its own named stream (`RunSeed.CreateStream("name")`), so adding a
  new random system never changes the results of the old ones.
- Shot spread and recoil jitter now come from the seed too (they used Unity's global random in M3).

**Save system** (`Scripts/Runtime/Save`)
- One file, `save.json`, in Unity's per-user data folder. Plain text so you can open and read it.
- Holds the profile (your headshot slow-mo choice, runs started) and the run in progress (its seed).
- Safe writes: new data goes to a temporary file first and is then swapped in. The previous good save is kept
  as `save.json.bak`. A damaged save loads the backup and keeps the damaged file as `save.json.corrupt`.
- Versioned. A save from a newer build is never overwritten (the game plays without saving and says so in the log).
- Main menu shows **Continue (seed #...)** when a run is saved.
- Key bindings still save where they did before (Unity PlayerPrefs).

**Data-driven content** (`Scripts/Runtime/Content`)
- Every content asset has a stable **id** such as `weapon.rifle_ak`. Code and saves refer to content by id,
  never by file name, so files can be renamed and moved.
- Weapons are discovered automatically: any `WeaponDefinition` asset in `Resources/Weapons` with an id exists in
  the game. To add a gun: right-click that folder, **Create > Orbital Ante > Weapon Definition**, fill in the id.
- **Orbital Ante > Validate Content** checks every content asset: missing or repeated ids, and values that would
  break things (0-round magazines, aim spread wider than hip spread, and so on).
- Enemies, items and ship parts will plug into the same base class when their milestones arrive.

**Steam Deck performance budget** (`Scripts/Runtime/Perf`)
- `Resources/Config/PerformanceBudgetConfig`: target 16.7 ms (60 fps), floor 25 ms (40 fps), draw-call budget
  1500, vsync on. These are targets to measure against, not measurements.
- The F3 overlay now reads from one monitor and colours its numbers against that asset.
- If the average frame stays over the floor for 3 seconds, a warning with the scene counts goes to the log
  (at most every 30 s), so a slow spot can be found from a log file after a Deck session.
- **Orbital Ante > Build > Steam Deck (playtest, with profiler)** and **(release)** make a Linux build in
  `Builds/SteamDeck`. Steps to get it onto the Deck are in `docs/DECK_BUILD.md`.

## Constitution principles served
- **6 Meaningful replayability / 9 no opaque randomness:** same seed, same run, including spread.
- **7 Manage cognitive load:** one asset per gun, one budget asset, one content check.
- **10 Evidence-based iteration:** the budget and overlay make every later milestone measurable on the Deck.

## Problem, hypothesis, test (principle 10)
- **Problem:** later milestones (solar system, planets) need saved state, content looked up by id and a frame
  budget to be held to. None existed.
- **Hypothesis:** a small versioned save, an id-based content database and one budget asset are enough for
  the whole of Phase 1.
- **Smallest test:** the checklist below plus the new EditMode tests.
- **Accept if:** every row passes and the Deck build runs the arena.

## Playtest checklist (please fill in; I won't guess these)
| # | Do this | Pass if | Result |
|---|---|---|---|
| 1 | Window > General > Test Runner > EditMode > Run All | All green (send me any red ones) | |
| 2 | Orbital Ante > Run Project Setup, then Orbital Ante > Validate Content | "All good. Checked 2 content asset(s)." | |
| 3 | Play the arena, press M (slow-mo on), stop, play again | Slow-mo is still on | |
| 4 | Play, note the seed, Esc > Quit > main menu | Menu shows "Continue (seed #same number)" | |
| 5 | Click Continue | Same dummies in the same places | |
| 6 | Same seed twice: stand still and empty one magazine at a wall each time | Bullet pattern matches | |
| 7 | F3 overlay | Shows budget 16.7 / floor 25.0 and "draw calls N / 1500" | |
| 8 | In `Resources/Weapons/RifleAK`, set Magazine Size to 0, run Validate Content | It reports the problem. Set it back to 30. | |
| 9 | Make a Deck build (see `docs/DECK_BUILD.md`) and run the arena on the Deck | Note avg / worst 1 % ms here: | |

## What I could NOT verify
- **Nothing has been compiled or run** (no Unity editor on the cloud machine), including the new tests.
- The Deck build menu has never been run. Frame times on a real Deck are unknown.
- The draw-call budget (1500) is a starting target, not a measured limit.
- "Draw Calls Count" and "Main Thread" counters may read n/a in some builds; the overlay handles that.

## Assets needed
None for this milestone.
