# Milestone 4 report - Foundation

Status: **built, not yet run in Unity.** Same position as Milestones 1 to 3. This milestone is mostly plain logic, so more of it than usual is properly tested here: 61 rule tests run and pass outside Unity. The parts that touch the engine (the save file through Unity's JSON, the content database, the on-screen readout) compile but have not been run.

The brief said to keep this lean, and it is: four small pieces, each only as big as the rest of Phase 1 needs. Same branch as before (`claude/unity-milestone-1`).

## What changed

**1. Seeded random numbers**
- New generator, `SeededRandom`. It uses only whole-number maths, so the same seed gives the same numbers on a Steam Deck, a desktop and any future Unity version. (.NET's built-in generator does not promise that.)
- **Named streams.** Each system asks for its own stream by name, for example `GameSession.Stream("arena.mannequins")`. A stream depends only on the run seed and its name. Adding a new system later, or changing how many numbers one system draws, cannot change what any other system gets. This is what will let "seed 1234" keep meaning the same solar system as the game grows.
- Everything that decides gameplay now draws from it: where mannequins spawn and which gun each gets, enemy aim and burst timing, wandering, and bullet spread.
- Still on Unity's ordinary random: purely visual scatter (blood particles, gib spin, splat rotation, sound pitch). See "Unverified" for what that means.

**2. Save system**
- One readable JSON file, `orbital_ante_save.json`, in Unity's per-user folder.
- **Safe writes.** A save is written to a temporary file first, then swapped in, and the previous save is kept as a backup. If the game or the Deck dies mid-save, one intact save always remains.
- **Recovery.** If the save is damaged, the backup is loaded and a warning is logged. If both are unusable the game starts fresh rather than refusing to start.
- **Versioned.** The file carries a version number and there is one place (`SaveMigrations`) where old saves get upgraded as fields are added. A save from a newer build is left alone, not misread.
- What is saved today is deliberately tiny, because Phase 1 has nothing to unlock yet: the last seed played, runs started, and time played.
- Visible result: the main menu gets a **"Play seed N again"** button once you have played a run, and it survives closing the game.
- Key bindings and the Feel toggles stay where they were (Unity's per-machine settings).

**3. Content pipeline**
- `ContentDefinition` is now the base for anything defined as data. `WeaponData` is the first. An asset's file name is its id.
- `ContentDatabase` finds every content asset in its folder, checks it, and hands it out by id. **Adding a gun is now: duplicate an asset, rename it, change numbers.** No code changes.
- **Validation in plain language.** Each asset is checked on load (for a gun: fire rate above zero, magazine at least 1, every body part has damage, one-handed multipliers not below 1, and so on). Duplicate ids are caught.
- New menu item **Orbital Ante > Check Content** shows the result in a dialog.
- Later content (enemies, items, factions) plugs into the same three steps: a data class, a folder, one line in the database.

**4. Steam Deck performance budget**
- `docs/performance_budget.md` sets the numbers: 1280x800, 60 fps (16.7 ms a frame), a 25 ms floor, how the 16.7 ms is shared between scripts, physics and drawing, and scene budgets for draw calls, triangles and lights.
- The same numbers live in a `PerformanceBudget` asset, and the game **checks itself against them while you play**. The top-left readout gains a line: `Budget 16.7 ms: OK / TIGHT / OVER`, the 1% worst frame time, and the share of frames over budget, over the last ten seconds.
- When a run ends, one summary line goes to the Console and log, so a playtest produces a number you can paste to me.
- VSync is now switched on at start-up (it was uncapped).

**Also**
- New menu item **Orbital Ante > Show Save Folder**.
- Build label is now "Milestone 4 - foundation".

## Why

Milestones 5 and 6 generate a solar system and planets from a seed. Without stable, independent random streams, every later addition would silently reshuffle existing worlds. The save system, content database and budget are the other three things every later milestone leans on, and each is far cheaper to put in now than to retrofit.

## Constitution principles served

- **3. Learnable challenge, no hidden rules**: a seed is fully repeatable, so a run can be replayed and learned. Validation stops a broken data value (a limb with zero damage) from quietly becoming an unfair rule.
- **6. Meaningful replayability**: replaying a seed is one button.
- **9. Ethical design**: the save holds no streaks, timers or login counts. Time played is recorded for our own balancing ("rewards fairly match the time spent"), and is not shown as a meter.
- **10. Evidence-based iteration**: the performance readout and log line turn "it feels slow" into numbers.

## What was tested, and how

Run for real here (`dotnet test`, no Unity needed), all passing:
- **61 rule tests**: the 33 from before, plus 28 new.
  - Random (11): same seed gives same numbers; different seeds differ; the exact sequence is pinned so it cannot change by accident; streams are independent; ranges stay in bounds and reach both ends; even spread; shuffle and pick.
  - Save file (10): round trip; previous save becomes the backup; damaged, empty or missing save falls back to the backup; both damaged gives a fresh start; a half-written temporary file from a crash does no harm; newer-version saves are refused.
  - Frame stats (7): average, worst, the 1% figure ignores one freak frame but shows regular stutter, share over budget.

Compiled only (against Unity reference assemblies and stand-ins for the editor), not run:
- All game code, the setup tool, and **42 PlayMode tests** (33 from before, 9 new: save round trip through Unity's JSON, backup recovery, newer-version save, content database finds both guns with no problems and lists in a stable order, validation catches a broken gun, the OK / TIGHT / OVER verdicts, session streams).

## Unverified

- **Nothing has been run in Unity**, now across four milestones.
- **No frame time has been measured anywhere.** Every budget number is a target chosen from the Deck's specifications. I do not know whether the arena is at 5 ms or 50.
- **"Same seed, same run" is not the same as "same seed, same fight".** The seed fixes what is generated and what the AI decides. It does not make physics replay identically: Unity's physics depends on frame timing, and where you stand changes everything after. That is the right promise for this game (same worlds, same placements), but it is not frame-for-frame determinism.
- Visual scatter is not seeded. If you ever want two runs of a seed to look identical down to the blood, that is a further step.
- Unity's JSON reader is stricter in some ways and looser in others than I can check here. The damaged-save test will show how it treats a half-written file.
- Whether VSync behaves well on your monitor in the editor. If the Game view feels capped oddly, untick **V Sync** on the PerformanceBudget asset.
- The readout box top-left is now three or four lines. It may crowd the screen at 1280x800.

## Playtest checklist (short, this milestone is mostly invisible)

- [ ] **Orbital Ante > Set Up Project** runs without new red errors (it now also creates the PerformanceBudget asset).
- [ ] **Orbital Ante > Check Content** says all content is fine, 2 weapons checked.
- [ ] Play a run, quit to the main menu: a **"Play seed N again"** button is there. Close Unity's Play mode, press Play again: still there, same number.
- [ ] Press it: the mannequins start in the same places with the same guns as last time.
- [ ] Top-left readout shows a Budget line. What does it say during a fight: OK, TIGHT or OVER, and the two numbers?
- [ ] After a run ends, the Console has a line starting `Orbital Ante performance`. Paste it to me.
- [ ] **Test Runner**: EditMode 61 green, PlayMode 42 green.
- [ ] Try it: in `Assets/OrbitalAnte/Resources/Weapons`, select **Pistol**, press Ctrl+D to duplicate, rename the copy `Pistol2`, set its Magazine Size to 0, then run **Check Content**. It should name the problem. Delete `Pistol2` after.

## Assets needed from you

None for this milestone.

## Next

Milestone 5, the solar system. This is the first milestone where I cannot check the important part without the engine: whether flying between moving bodies feels good is something only a person at the controls can judge. I will build it the same way (rules as testable plain code: orbits, gravity, seeded positions, the moving-frame maths), but four milestones are now waiting on a first press of Play, and each one stacked on top makes a first-run problem harder to find. Half an hour with `docs/SETUP.md` would be worth more than anything else right now.
