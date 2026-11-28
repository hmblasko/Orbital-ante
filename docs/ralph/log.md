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

- Ten unlocks, each earned by one feat inside a single run, listed easiest first (`Ante/Unlocks.cs`, plain C#). Every
  goal says how to meet it, with the player's own keys where a key is needed.
  - Six perks start locked out of the perk drafts. The first draft after one is unlocked shows it once, in its last
    place, marked NEW · UNLOCKED on the star chart.
  - Four starting kits (Gunner, Ghost, Pilot, Daredevil) are earned. A kit is a sidegrade: two perks of one family at
    the start, chosen for you, for one fewer over the run (the first pick is ante 4's). The price is written wherever
    it applies.
- Unlocks are written to the save the frame after the goal is met (save layout 5).
- A perk unlocked mid-run joins the drafts from the next ante, so the offers already on screen never change.
- The moment: a green stencil banner, UNLOCKED: and the name, over a note that nothing can write over, saying what
  earned it and where it turns up, and a new sound, `reward.unlock` (lock pins, a latch, a rising harp run, in D like
  every reward). It waits out a fight (20 s at most), an important note and the gun ladder's banner.
  - At a cleared ante, the break page and the victory page say what was unlocked instead.
  - The recap lists the run's unlocks and names the next two to earn.
- The five ways to score are named by what really pays them (`Ante/ScoreWays.cs`), for Change of Pace's goal, the
  chain perk's card and the recap. A rule test reads the game's code to keep the words true.
- Title screen:
  - The next unlock is named under the records.
  - The Unlocks page lists every goal and marks new ones.
  - New run asks for a kit once one is earned.
  - How to play says how unlocks work.
- The pause menu's pages now fit the screen (`UI/MenuFit`). Before, six rows at full height were taller than the
  Deck's 800 units, and every break was taller than a 16:9 monitor's 759. Bodies are measured with Unity's own text
  generator, and the canvas height is worked out from the screen.

**Checked:**

- `dotnet test tools/rules-tests`: 3,063 run, 1 skipped. One new test failed on the first run (a wrong expected
  number) and passed when fixed; its two classes were run again, 44 of 44. About 45 of the tests are new
  (`UnlockTests`, `MenuFitTests`).
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- `reward.unlock` was measured, not listened to: 2.9 s, -11.7 LUFS, centroid 1,380 Hz, nothing below 200 Hz.
- The run simulator now plays each kit against Standard (`1000 kits`; a made-up player). Average and Skilled: every
  kit within 0.21 antes and 6% of Standard's score. First-time: Gunner, Pilot and Daredevil reach ante 4 more often
  (56%, 60%, 57% against 50%), so for that player the kits fail the review's acceptance line. Part of it is the model
  (its Standard player picks its first perk at random and values seven perks at nothing). Perks at ante 1's target
  instead of the start took only 2 or 3 points off, so the kits stay as built and the playtest checks it.
- Every new on-screen string was dumped from the rules and measured offline with the game's fonts (B612, Saira
  Stencil One) against the box it lands in. All fit. The longest unlock notes come out at 15 px in the 640-wide note
  line, the smallest of its 13 to 20.

**Specialist reviews** (each run on this pass's code, each finding checked before it was applied):

- deck-performance: a kill that met three goals wrote the save three times on the kill's own frame. Now one write, the
  frame after. `SaveSystem.Save` has a profiler marker.
- game-designer: kits held as many perks as a picking run, so they were straight upgrades. Now they cost one perk.
  Goals did not say how (no keys, no "where is the rover"). The four-ways goal named kinds of points the game never
  pays. A rare unlock could go unseen for many runs. The moment was a note that a fight would bury. The recap named one
  goal. The Daredevil kit's goal (bank with a minute left) and In Your Face's (any melee kill) did not fit them: the
  Daredevil kit now comes from a kill under 30% armour, and a melee kill unlocks Heavy Swing (In Your Face is open from
  the start). All applied.
- unity-reviewer: a mouse resting over a button took the selection back from the stick every frame; menu bodies were
  measured by a character count, not the real text; the canvas height assumed the Deck's screen; the unlock sound
  could play over the envelope's reveal; `ReachedTop` was not reset between play sessions; the field note could be
  lost when an ante was banked during the wait. All applied. Not changed: save layout 5 stops older branches saving
  over it until this is merged (written into the report), and unlocking changes a seed's later drafts (by design).

**Scores after:** see `scorecard.md`, pass 1. Unlockables 2 to 6, all-round 5 to 6, the rest unchanged.

**Left open:** nothing here has run in Unity. The kit page, the Unlocks page, the banner and the break with unlock lines
need looking at on the Deck's screen. Next pass: creature tells (gap 2).

## Pass 2 (27 Nov 2026): creature tells

**Gap closed:** six of the nine creatures borrowed the Faction A Scout's flare as their warning (and the maw's draw-in
the Heavy's spin-up), so one sound meant six dangers; the visor's arcs stayed at the screen angle they fired at, so
turning toward a danger left its arc pointing the old way; and a creature winding up out of view showed nothing.

**Built** (report `docs/reports/creature-tells.md`):

- One rule for every creature that strikes (`Creatures/Windups.cs`, plain C#): it winds up while it still follows the
  player (Winding), takes aim at its brain's own lock (Aimed: a step aside now is a miss), then strikes (Striking). The
  stage, and while it winds up how near the lock it is, are read from each brain's orders; nothing about any fight
  changed.
- Each warning is two sounds of the creature's own, made from code in `tools/audio/sfx/creature.py`: a wind-up, played
  as the warning starts, and an aim, one hard mark at the very front of its file (a clack, a snick, a clunk, a fang
  click, a thoom, a crack, a drum click, a jaw scrape, a pop), played by the game the frame the creature takes aim
  (`WindupTrack`, `WindupTell`). So the mark is on the lock whatever a dial, Focus, a hitstop or a long frame does, and
  a warning called off before it takes aim never gives its mark. 20 new clips (12.1 s, 1.1 MiB): six new wind-ups,
  nine aims, the maw's draw-in and choke, the biter's dart. The Screamer's, Ambusher's and Blind stalker's older
  warnings were cut in two the same way. Every stand-in is gone: creatures play only their own sounds.
- Two older sounds were nearly all below what the Deck's speakers play (the Ambusher coming up, 95% of its weight under
  250 Hz, and the Blind stalker's lunge, 91%): the rise now crackles as the ground strains (47%), and the lunge's thump
  sits under its clap (59%).
- The visor (`UI/DamageFeedback.cs`, rules `UI/WindupCues.cs`): every arc keeps its danger's direction in the world and
  turns as the player turns, so turning toward an arc brings it to the top of the ring, and it turns smoothly as a
  danger passes overhead (`DamageCues.MarkerAngle`). A creature's warning is an amber arc on the gunman's ring that
  follows the creature: steady and closing from 44 to 30 degrees as the lock comes nearer, snapping to 15 degrees at
  full brightness the same frame as the sound's mark, dimmed once a strike can no longer reach the player (a scream
  that missed, a pull that let go, a ball that rolled past: `IWindup.Threatens`), then fading. The nearest three show;
  a gunman's arc beside a creature that has taken aim is dimmed so the snap is not lost; amber arcs hide while paused
  or dead.

**Checked:**

- `dotnet test tools/rules-tests`: 3,081 run, 3,080 passed, 1 skipped (the cabin bake check, as before). `WindupTests`
  is 18 tests, among them four real brains (a Strider and a Sprinter, a Sprinter with no arms, a Cave hunter, a Roller)
  run through warnings frame by frame: one wind-up and one mark each, the mark within a frame and a half of the lock,
  the brain following before it and not after.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- `tools/audio/qa.py`: pass, 0 failed checks; 8 warnings, none of them a creature sound (there were 12, and the 4
  removed were the Ambusher's rise and the Blind stalker's lunge). New creature table: every mark in its first 8 ms and
  8.0 to 19.0 dB over its wind-up above 200 Hz; every wind-up's loudest 100 ms -24.8 to -31.7 dB; no creature sound
  stands 10 dB over its neighbours at the clock's tick or warning (the clock's own tick stands 37 to 55 dB over).
- The sounds were measured, not listened to.

**Specialist reviews** (each run on this pass's first build, each finding checked before it was applied):

- unity-reviewer: the mark landed on the lock only when nothing went wrong (Focus slows sounds to 0.7 against the
  game's 0.35, a hitstop stops the game but not a sound, a call-off still played the mark), and a second Strider or
  Sprinter within the 250 to 400 ms cooldown warned in silence. This changed the design: the mark is now its own sound,
  played on the stage change, with a 60 ms cooldown; `AudioEventDef.LockAt`, `AudioDirector.LockMark` and the
  warnings' pitch stretch are gone. Also applied: a new warning could steal the arc of a creature still mid-strike; the
  arena's arcs could use last frame's view (`DefaultExecutionOrder(100)`); arcs flipped top to bottom looking up
  steeply; creature arcs froze under the pause and death pages; an armless Sprinter gave a false mark.
- game-designer: the Roller's curl (even knocks at 615 to 668 Hz) and the biter's tock (879 and 1617 Hz, 20 ms) sounded
  like the ante clock's wood: re-voiced as noise, and `qa.py` now fails any creature sound that rings by the clock. The
  flicker looked most urgent exactly when moving is useless: replaced by the countdown and the snap. A full arc held
  through a dodged strike read as "still coming": now it dims. A sighting arc could swallow the snap: now dimmed beside
  it. Asked for, and added: every other mode of every brain reads None (so ambushes keep their surprise), the mark at
  least 5 dB over its wind-up measured above 200 Hz, a false claim about 250 Hz taken out of `creature.py`, and the repo
  notes brought up to date. Its Deck acceptance criteria and playtest protocol are in the report.
- deck-performance: fits the budget (budget rows written for the split and the countdown). Each showing arc re-read
  the eye's axes: now read once a frame.

**Scores after:** see `scorecard.md`, pass 2. Understandable 6 to 7, the rest unchanged.

**Left open:** nothing here has run in Unity, and no sound has been listened to. Known limits: in Focus a wind-up ends
early, leaving a quiet gap before the mark; an aim's tail is cut to the default dials; on two half-lines overhead and
underfoot an arc can swap sides of the ring; the Roller's "still in its way" is measured along its line. The Deck
acceptance needs a per-strike log (kind, on screen, first sideways move against the lock, hit or miss), not built.
Next pass: gap 3, creature kills pay toward the ante and the music hears the player being hurt and hunted.

## Pass 3 (28 Nov 2026): creature kills pay, and the music hears danger

**Gap closed:** a creature fight paid nothing toward the ante (the task director scored only the camps' soldiers), so
the seas were a detour from the goal; and in the solar system the music heard only gunfire. Being seen, hunted, hurt
(the suit's armour was never watched there) or set on by a creature (which never shoots) left it where the place put it.

**Built** (report `docs/reports/creature-kills-and-danger-music.md`):

- Creature kills pay like a camp's (`TaskDirector.OnCreatureHit`, through the same `PayKill`): the tier
  (`CreatureTiers`: the Pack biter 1, the Drifting maw and the Strider 3, the rest 2), the style feats, ON A ROLL, the
  Ghost set's time back, a bonus mission's bounty or style, and the kill unlocks. Every creature names itself as the
  target of its hits and carries a `CreatureCredit` given by whatever woke it; only the seas do so today, so the maw and
  the biters pay and the practice range never does.
- Claims are one per world, haunt and place. The seas remember a pack's dead fish one by one: a pack woken again comes
  back without them, and still counts them toward its size, so its last fish makes off as it would have.
- A kill made while swimming is no longer AIRBORNE (it was, for camp kills too: a floating suit is "not grounded").
- The music's steady level follows how things stand (`Audio/MusicDanger.cs`): calm (a little fuller each ante, as Part
  B section 14 asks), tension while an armed enemy knows where you are or one is on its way to look, the fight music for
  the readout's six seconds after a sighting or a creature's warning, its top layer on the last plates (with the suit's
  pulse). The suit's armour is watched in the solar system, so being hurt is heard, except a crash's own hurt (Part B
  wants no crash music). Out on foot, which piece plays is read where the player is, not where the ship is.

**Checked:**

- `dotnet test tools/rules-tests`: 3,095 run, 3,094 passed, 1 skipped (the cabin bake check, as before), on the first
  build; 14 tests are new (`CreatureKillTests`, `MusicDangerTests`).
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
