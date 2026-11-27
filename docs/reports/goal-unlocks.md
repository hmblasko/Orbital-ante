# Goal unlocks and starting kits (Ralph loop pass 1, 27 Nov 2026)

Matthew asked for "a fun unlockables system", and Part B section 5 asks for Balatro-style goal unlocks. The game had
no goal unlocks. Records were the only progress that lasted between runs. The prize envelopes open only after ante 9,
while a new player is expected to die around ante 3 or 4, and nothing inside them changes the game yet. **Nothing
here has been run in Unity.**

## What changed

| | |
|---|---|
| Problem | A run changed nothing about later runs. Nothing was in view to aim for beyond a record, and the one reward that lasted (the envelope) was out of a new player's reach. |
| Hypothesis | Clear one-run goals that each open a new way to play give every run a second thing to aim for, change what later runs can be, and come early enough that a player who dies at ante 3 still earns one. |
| Principle | Discovery that matters (4), meaningful replayability (6), legible risk/reward (5). Unlocks are sidegrades (section 4). Rule 9: no grind, no counting across runs, no timers outside a run. |
| Smallest test | Rule tests on the catalogue, the goal counting, the save and the drafts (`UnlockTests`). |
| Acceptance | Every goal is one feat inside one run. A locked perk is never offered. A kit run holds as many perks as a picking run. Every family can still complete its set with everything locked. An unlock is written to the save by the frame after it is earned. |
| Playtest protocol | On a fresh profile, play three runs on the Deck. Note which unlocks came and in which run. Note whether each unlock note was read in the moment, and whether the kit page made sense the first time. Then check on the Deck that the Unlocks page and a break with two unlock lines fit the screen. |

### The ten unlocks (`Ante/Unlocks.cs`, plain C#)

Listed easiest first. That is also the order the recap suggests the next one in.

| Unlock | What it is | Goal (one run) |
|---|---|---|
| Gunner kit | Start with Quick Study and Steady Hands | Clear ante 2 |
| Patient Hunter | Ghost perk | Kill an enemy while crouched or lying prone |
| Road Rage | Pilot perk | Splat an enemy with the rover |
| Ghost kit | Start with Eagle Eye and Cold Blood | Kill from the LONG SHOT distance or further |
| Pilot kit | Start with Frequent Flyer and Fleet Foot | Land on three different worlds in one ante |
| Change of Pace | Wild-card perk | Score four different ways in one ante |
| Daredevil kit | Start with Glass Cannon and No Brakes | Bank an ante with a minute or more on its clock |
| Trigger Discipline | Gunner perk | Climb the gun ladder to its last gun |
| In Your Face | Brawler perk | Kill an enemy with a melee weapon |
| All In | Daredevil perk | Raise the stakes, then clear that ante before its clock runs out |

- **Locked perks.** Six perks start locked out of the drafts. With all six locked, every family still has at least
  its set size of open perks (rule test). A perk unlocked mid-run joins the drafts from the next ante, so the cards on
  screen never change and an unlock is never a free reroll (`AnteRun.SetLocked`, called at each ante's start).
- **Starting kits.** A kit gives two perks of one family at the run's start, in place of the picks of antes 1 and 2
  (`AnteDirector.PicksGivenAhead`). A kit run holds as many perks as a run that picks, so a kit trades choice for an
  early start. The cost is written on the kit: "No perk pick in antes 1 and 2: these two are those picks." Standard
  (no kit) is always open and is the default. Skip cash in antes 1 and 2 goes too, as the picks are given.
- **Counting** (`UnlockTracker`). Each goal is counted inside one run, or one ante where it says so, and comes back
  once per run. Nothing counts across runs, so no goal is a grind. A low-stance kill does not count from the rover.
  Banking early and beating the clock with raised stakes do not count in overtime. Only solar-system runs count,
  because the practice range has no ante loop.
- **Saving** (`UnlockBook` on `SaveData`, save layout 5). The book is the save's `unlocks`, `unlocksSeen` and `kit`.
  Every way a run ends already saves. On top of that, an unlock is written the frame after it is earned, so a crash
  or a flat battery later in the run cannot lose it. Every goal met in one frame shares that one write, and it never
  lands on the kill's own frame (`AnteLoop.WriteOwedUnlockSave`). `SaveSystem.Save` now carries a profiler marker,
  `OA.SaveSystem.Save`, so the write can be timed on the Deck.

### The moment

- **In the field.** A note that nothing writes over for 3 s, for example "UNLOCKED: ROAD RAGE. IN YOUR PERK DRAFTS
  FROM THE NEXT ANTE". It waits its turn behind TARGET MET, so both get read. It plays its own sound, `reward.unlock`.
- **At a cleared ante.** The break page (and the victory page) lists each unlock in a line of its own. The sound
  waits until the ante's fanfare has finished.
- **The sound** (`tools/audio/sfx/rewards.py`). Three rising lock-pin clicks, a latch with a thunk, a harp and celesta
  run up D major, a bell, and a soft glass pad. It is in D like every reward and is used nowhere else. Measured, not
  listened to: 2.9 s, -11.7 LUFS, centroid 1,380 Hz, and nothing below 200 Hz, so the Deck's speakers lose none of it.
- **The recap.** It names this run's unlocks, then the next goal: "Next unlock, Patient Hunter: Kill an enemy while
  crouched or lying prone." That line goes before RunRecap's next mark.
- **Kit runs.** The first note of a kit run says what the kit gave. Where the field page and the break name the
  ante's perk, they say "from your Gunner kit".

### Title screen (`UI/MainMenu.cs`)

- **Unlocks.** Every goal is listed in order. Each shows UNLOCKED in green with what it gives, or its goal, and the
  ones not yet seen are marked NEW. The button says "Unlocks (2 new)" until the page is opened. This is a list, not a
  completion meter: there is no per cent and no bar.
- **Choose a kit.** Once a kit is earned, New run opens this page. It has one button per open kit, with the kit's
  perks and cost under the selected one, and the goals of the kits still to earn. With no kit earned, New run starts
  at once as before.
- **How to play** gains one line on unlocks.
- `SelectOnHover` (in `UiKit`): the mouse selects the button it is over, so the details follow the mouse as they
  follow the stick.

### Menus that fit (`UI/MenuFit`, plain C#)

The pause menu's choices page is a heading, a body and up to seven buttons. At 70-unit buttons, six rows came to 804
units, taller than the Deck's 800. Every break was taller than the 759 units a 16:9 monitor gives the same canvas.
Now the body grows with its words (unlock lines add to it). The buttons give up height to keep the card on the
screen, down to 44 units. The death page's recap grows with its lines too, up to 300.

## Tests

- `dotnet test tools/rules-tests`: 3,052 passed, 1 skipped. 34 are new, in `UnlockTests` and `MenuFitTests`.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- Specialist reviews: see below.

## Still unverified

- Nothing here has run in Unity. That includes the kit page and the Unlocks page, which are built in code and have
  never been seen, the note timing, and the sound in the mix.
- Whether the goals come at a good pace for a new player. The order is a guess from the rules: clearing ante 2, a
  crouched kill and a rover splat should be within a first player's reach. Only playtests can say.
- The six locked perks make early drafts a little narrower until they are earned.
