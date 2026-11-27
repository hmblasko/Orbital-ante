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
| Smallest test | Rule tests on the catalogue, the words, the goal counting, the save, the drafts and the menu sizes (`UnlockTests`, `MenuFitTests`). |
| Acceptance | Every goal is one feat inside one run, and its words say how to do it with the player's own keys. A locked perk is never offered. A kit run is one perk behind a picking run from ante 3. Every family can still complete its set with everything locked. An unlock is written to the save by the frame after it is earned. Every menu page fits a 16:9 screen with its longest words. |
| Playtest protocol | On a fresh profile, play three runs on the Deck. Note which unlocks came and in which run. Note whether each unlock's banner was read in the moment, whether the NEW card was noticed in the draft, and whether the kit page made sense the first time. Then check on the Deck that the Unlocks page and a break with two unlock lines fit the screen. |

### The ten unlocks (`Ante/Unlocks.cs`, plain C#)

Listed easiest first. That is also the order the recap names the next ones in.

| Unlock | What it is | Goal (one run) |
|---|---|---|
| Gunner kit | Start with Quick Study and Steady Hands | Clear ante 2 |
| Patient Hunter | Ghost perk | Kill an enemy while crouched or lying prone (the words name the crouch key) |
| Road Rage | Pilot perk | Splat an enemy with the rover (the words say where it hangs and which key lowers it) |
| Ghost kit | Start with Eagle Eye and Cold Blood | A LONG SHOT kill (60 m or further) |
| Pilot kit | Start with Frequent Flyer and Fleet Foot | Land on three different worlds in one ante (moons count) |
| Change of Pace | Wild-card perk | Score four of the five ways in one ante (each way named by what pays it) |
| Daredevil kit | Start with Glass Cannon and No Brakes | A kill with your armour under 30% (3 chevrons or fewer) |
| Trigger Discipline | Gunner perk | Climb the gun ladder to its last gun |
| Heavy Swing | Brawler perk | A melee kill (some bases have a melee weapon) |
| All In | Daredevil perk | Raise the stakes at a break, then meet that ante's target before its clock runs out |

- **Locked perks.** Six perks start locked out of the drafts. With all six locked, every family still has at least
  its set size of open perks (rule test). A perk unlocked mid-run joins the drafts from the next ante, so the cards on
  screen never change and an unlock is never a free reroll (`AnteRun.SetLocked`, called at each ante's start).
- **The new perk is shown once.** Drafts roll a rarity first, so a rare unlock could go many antes unseen. The first
  draft after an unlock that can use it takes the new perk in its last place (`AnteRun.Feature`; never on a reroll,
  never a perk already held or of no use to this run). Its star chart card says NEW · UNLOCKED. Once it has been on
  screen the save forgets it (`SaveData.perksToShow`), so it is featured once, ever.
- **Starting kits are sidegrades.** A kit gives two perks of one family at the run's start, chosen for you. The price
  is one perk: the kit stands in for the picks of antes 1 to 3 (`StartingKits.PicksTaken`,
  `AnteDirector.PicksGivenAhead`), so the first pick is ante 4's. A kit run is ahead in antes 1 and 2 and one perk
  behind from ante 3 to the end. The price is written on the kit page, on the kit's first note and on the perk row of
  each ante it covers ("none this ante, the price of your Gunner kit"). Standard (no kit) is always open and is the
  default. Skip cash in the antes a kit covers goes too, since those picks were given.
- **Counting** (`UnlockTracker`). Each goal is counted inside one run, or one ante where it says so, and comes back
  once per run. Nothing counts across runs, so no goal is a grind. A low-stance kill does not count from the rover.
  Beating the clock with raised stakes does not count in overtime. Only solar-system runs count, because the practice
  range has no ante loop.
- **The ways to score** (`Ante/ScoreWays.cs`). Change of Pace's goal and the chain perk's card name the five kinds of
  points by what really pays them today: a kill or a mast, a first landing on a world, a HARD LANDING, a supply drop
  or a bonus mission, a delivered crate. A rule test reads the game's code and fails if a kind is paid by something
  the words do not name. The run recap uses the same names ("drops and missions", "hard landings").
- **Saving** (`UnlockBook` on `SaveData`, save layout 5). The book is the save's `unlocks`, `unlocksSeen`, `kit` and
  `perksToShow`. Every way a run ends already saves. On top of that, an unlock is written the frame after it is
  earned, so a crash or a flat battery later in the run cannot lose it. Every goal met in one frame shares that one
  write, and it never lands on the kill's own frame (`AnteLoop.WriteOwedUnlockSave`). `SaveSystem.Save` carries a
  profiler marker, `OA.SaveSystem.Save`, so the write can be timed on the Deck.

### The moment

- **In the field.** A green stencil banner, UNLOCKED: and the name, under the note line (where the gun ladder's NEW GUN
  shows), and a note that nothing writes over for 3 s, saying what earned it and where it turns up: "CROUCHED OR PRONE
  KILL · A GHOST PERK, IN YOUR DRAFTS FROM ANTE 3". It plays its own sound, `reward.unlock`.
- **It waits its turn** (`UnlockMoment.MayShow`): behind an important note such as TARGET MET, behind the gun ladder's
  banner, and while the readout is hidden. In a fight it waits until the fight is over, or 20 s at most, so it is read
  rather than lost in gunfire. If a new gun comes while the banner shows, the banner gives way and the note stays.
- **At a cleared ante.** The break page (and the victory page) lists each unlock in a line of its own: "UNLOCKED:
  Patient Hunter, for a crouched or prone kill. A Ghost perk, in your drafts from ante 3." Unlocks still waiting for
  their moment when the ante is banked go there too. The sound waits until the ante's fanfare has finished, and at
  victory until the envelope's reveal is over.
- **The sound** (`tools/audio/sfx/rewards.py`). Three rising lock-pin clicks, a latch with a thunk, a harp and celesta
  run up D major, a bell, and a soft glass pad. It is in D like every reward and is used nowhere else. Measured, not
  listened to: 2.9 s, -11.7 LUFS, centroid 1,380 Hz, and nothing below 200 Hz, so the Deck's speakers lose none of it.
- **The recap.** It names this run's unlocks ("See Unlocks on the title screen"), then the next two to earn in a few
  words each: "Next unlocks: Patient Hunter, for a crouched or prone kill · Road Rage, for a rover splat". When the
  next mark is clearing ante 2, it says so once: "Next mark: clear ante 2, which unlocks the Gunner kit."
- **Kit runs.** The first note of a kit run says what the kit gave and when the first pick comes. The pause menu's
  restart page, the title screen's replay button and the recap's build line name the kit.

### Title screen (`UI/MainMenu.cs`)

- **Next unlock.** Under the records: "Next unlock: Patient Hunter, for a crouched or prone kill".
- **Unlocks.** Every goal is listed in order. Each shows UNLOCKED in green with what it gives, or its goal and how to
  meet it, and the ones not yet seen are marked NEW. The button says "Unlocks (2 new)" until the page is opened. This
  is a list, not a completion meter: there is no per cent and no bar.
- **Choose a kit.** Once a kit is earned, New run opens this page. It has one button per open kit, with the kit's
  perks, what they do for the family's set and the price under the selected one, and the kits still to earn. With no
  kit earned, New run starts at once as before.
- **How to play** gains one line on unlocks.
- `SelectOnHover` (in `UiKit`): the mouse selects the button it moves over, so the details follow the mouse as they
  follow the stick. A cursor resting over a button does not take the selection back from the stick.

### Menus that fit (`UI/MenuFit`, plain C#)

The pause menu's choices page is a heading, a body and up to seven buttons. At 70-unit buttons, six rows came to 804
units, taller than the Deck's 800. Every break was taller than the 759 units a 16:9 monitor gives the same canvas.
Now the body grows with its words, measured by Unity's own text generator at the label's real width (unlock lines
add to it). The buttons give up height to keep the card on the screen, down to 44 units, and the words shrink a
little (best fit) only if they still do not fit. The canvas height is worked out from the screen (`MenuFit.CanvasHeight`),
so a 16:9 monitor is held to its 759. The death page's recap grows with its lines too, up to 300 or what the screen
leaves. The new words were measured offline with the game's own fonts (B612, Saira Stencil One) against every box
they land in: all fit.

## Notes for Matthew

- **Ship parts.** Part B section 5 names "base ship models and swappable ship components" as goal unlocks. The game
  has one ship and no swappable components yet, so the first unlocks are perks and kits. Ship parts can join the same
  catalogue once they exist.
- **Your save.** An existing profile starts with all ten locked, so its drafts lose six perks until they are earned.
- **Rare draws.** With three of the four rare perks locked, every rare offer on a fresh profile is On the Clock until
  one is earned.
- **Older branches.** Once this branch has written a save (layout 5), a branch without this work (the current
  playtest build, critique loop round 6) refuses to save over it until this is merged in.
- **Seeds.** Unlocking changes what a seed's later drafts offer, because locked perks are left out of the roll.
- **LONG SHOT with Eagle Eye** counts from 45 m, because the goal is "a LONG SHOT kill" and Eagle Eye moves the LONG
  SHOT line closer. Left as it is.

## Tests

- `dotnet test tools/rules-tests`: 3,063 run, 1 skipped. One failed on the first run (a wrong expected number in a new
  test) and passed when fixed; its two test classes were run again, 44 of 44.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- **Run simulator** (`tools/run-sim`, `1000 kits`: a made-up player model, never a playtest result). For the Average
  and Skilled models every kit is within 0.21 antes and 6% of Standard's run score: Ghost a little behind, Pilot and
  Daredevil a little ahead. The First-time model reaches ante 4 more often with three of the kits (Gunner 56%, Pilot
  60%, Daredevil 57%, Ghost 49%, against Standard's 50%). That fails the review's acceptance line ("no kit beats
  Standard on reaching ante 4 by more than a few points") for that player. Two reasons: a kit's price comes from ante 3
  on, where that player tends to die, and the model's Standard player takes its first perk at random and values seven
  perks at nothing, while a kit's two perks are chosen ones. Giving the perks at ante 1's target instead of the start
  was tried and took only 2 or 3 points off, at the cost of the "start the run as a Ghost" feel, so the kits stay as
  they are and this is the first thing a kit playtest should check (alternate kit and Standard runs on two seeds).
- Specialist reviews: deck-performance (one save write per frame, never the kill's own; a profiler marker), game-designer
  (kits as sidegrades, goals that say how, the ways to score named by what pays them, the new perk shown once, the
  banner, the recap naming the next two, Daredevil and Heavy Swing given goals that fit them) and unity-reviewer (the
  resting cursor, menus measured with the real text generator, the unlock sound waiting for the envelope). Each was
  applied.

## Still unverified

- Nothing here has run in Unity. That includes the kit page and the Unlocks page, which are built in code and have
  never been seen, the banner and note timing, and the sound in the mix.
- Whether the goals come at a good pace for a new player. The order is a guess from the rules: clearing ante 2, a
  crouched kill and a rover splat should be within a first player's reach. Only playtests can say.
- Whether a kit's price (one fewer perk all run) feels fair against two perks at the start. The model says a
  first-time player is helped by three of the kits (see Tests).
