# Ralph loop log

One entry per pass, newest last. What each pass scored, built, checked and left open. Nothing here is a playtest
result.

## Pass 0 (27 Nov 2026): setting up

- Wrote the checklist and the scale (`README.md`), and how the runtime is compiled in the cloud (`compile-check.md`).
- Scored the game from the code: clear goal 7, understandable 6, aesthetics 6, gameplay 6, unlockables 2, all-round 5
  (`scorecard.md`, pass 0).
- The biggest gap was the unlockables line. Part B section 5 asks for Balatro-style goal unlocks and the game had
  none. The prize envelopes open only after ante 9, while a new player is expected to die around ante 3 or 4, and
  nothing inside them changes the game yet.

## Pass 1 (27 Nov 2026): goal unlocks

**Gap closed:** there was nothing to unlock, so no run changed what later runs could be.

**Built** (report `docs/reports/goal-unlocks.md`):

- Ten unlocks, each earned by one feat inside a single run, listed easiest first (`Ante/Unlocks.cs`, plain C#).
  - Six perks start locked out of the perk drafts.
  - Four starting kits (Gunner, Ghost, Pilot, Daredevil) are earned. A kit's two perks stand in for the picks of
    antes 1 and 2 (`AnteDirector.PicksGivenAhead`), so a kit run holds as many perks as any other run (a sidegrade:
    an early start for less choice). The cost is written on the kit.
- Unlocks are saved the moment the goal is met (save layout 5).
- A perk unlocked mid-run joins the drafts from the next ante, so the offers already on screen never change.
- The moment: a note nothing can write over, and a new sound, `reward.unlock` (lock pins, a latch, a rising harp
  run, in D like every reward).
  - At a cleared ante, the break page and the victory page say what was unlocked instead.
  - The recap lists the run's unlocks and names the next goal.
- Title screen:
  - The Unlocks page lists every goal and marks new ones.
  - New run asks for a kit once one is earned.
  - How to play says how unlocks work.
- The pause menu's choices page now fits the screen (`UI/MenuFit`). Before, six rows at full height were taller than
  the Deck's 800 units, and every break was taller than a 16:9 monitor's 759.

**Checked:**

- `dotnet test tools/rules-tests`: 3,052 passed, 1 skipped. 34 of the tests are new (`UnlockTests`, `MenuFitTests`).
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- `reward.unlock` was measured, not listened to: 2.9 s, -11.7 LUFS, centroid 1,380 Hz, nothing below 200 Hz.
- Specialist reviews: see below.

**Scores after:** see `scorecard.md`, pass 1.

**Left open:** nothing here has run in Unity. The kit page, the Unlocks page and the break with unlock lines need
looking at on the Deck's screen.
