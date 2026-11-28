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
  the biters pay and the practice range never does. Only from within its reach (`ICreature.InReach`): a swimmer killed
  from the shore or from over the sea pays nothing, and the note says what does.
- Claims are one per world, haunt and place. The seas remember a pack's dead fish one by one: a pack woken again comes
  back without them, and still counts them toward its size, so its last fish makes off as it would have.
- A kill made while swimming is no longer AIRBORNE (it was, for camp kills too: a floating suit is "not grounded").
- The music's steady level follows how things stand (`Audio/MusicDanger.cs`): calm (fuller each ante, 4 an ante up to
  8, so it is heard by ante 3; flying, the lift goes with the engines and coasting stays quiet), hunted (the tension
  layer while an armed enemy knows where you are or holds a place to look for you, from a sound, an alarm or a restless
  squad's call, or a swimmer has noticed you), a fight (the readout's six seconds after a sighting by an armed enemy,
  or a creature's warning, held up to 24 s between a creature's strikes). On the last plates in a fight the music
  thins (its top layers held off, 4 dB down) so the suit's pulse is heard. No rests while someone is after you; a fight
  back within a minute comes in on the hit at a later entry point; the fight music's exit is now 48, so a search after
  a fight is heard as tension. The suit's armour is watched in the solar system, except a crash's own hurt (Part B
  wants no crash music). Out on foot, which piece plays is read where the player is, not where the ship is. A new world
  (a restart) starts the music calm.

**Checked:**

- `dotnet test tools/rules-tests`: 3,104 run, 3,103 passed, 1 skipped (the cabin bake check, as before), after the
  reviews' fixes; 23 tests are new (`CreatureKillTests` 8, `MusicDangerTests` 12, three in `AudioRulesTests`) and one
  was changed (a fight flaring up as the ending is counted in now comes back on the hit). The first build had 3,094 of
  3,095 passing.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- The music's rules in the manifest were refreshed from `tools/audio/music/__init__.py` with `build.py music --only
  none` (no sound was rendered again); only `combatExit` changed.
- Nothing was listened to.

**Specialist reviews** (each run on this pass's first build, each finding checked before it was applied):

- unity-reviewer: a rover crash would have brought fight music (the knock is now kept out); a creature's warning just
  before dying carried into the next run's pad, because "Try again" goes straight from one world to the next with the
  clock stopped (a new world now starts calm, and the old fight's heat goes too); the creature-kill path read a
  destroyed creature's position if a hit were ever heard late (now it still pays, never as a long shot); an all-dead
  pack could be made empty and block the world's other packs (refused now). The destroy order, one payment per animal,
  the dead fish's memory, the sea test's frame and the subscriptions were confirmed safe. Not changed: the creature
  code asks `TaskIds` for its claims (every claim's form in one place).
- game-designer: sea creatures paid full price to a player they could not hurt (shots pass the sea's surface; the
  swimmers ignore anyone out of the water), so a maw shot from a hover paid Tier 3 at no risk: kills from out of reach
  now pay nothing, with a note (its first choice, shots that stop at the surface, would change every fight near water
  and is left as a design question). Most ways enemies come looking were silent in the music (only a restless squad's
  call counted, and it stopped counting as the squad closed in): every way is counted now, held by a test that reads
  the code. Rests muted being hunted; creature fights would have flapped between strikes; a fight back soon opened with
  the same riser; a disarmed enemy's sighting held the fight music; the last plates' chaos layer would have buried the
  pulse; the ante's lift came after most first runs end and filled the coast. All applied as in the report. It found
  the tiers fair. Its Deck acceptance criteria and playtest protocol are in the report.
- deck-performance: fits the budget. The sea test asked for a full ground reading on every airborne hit, which high
  above a world could cost a full-detail height: it now asks only the sea's radius. Budget rows written.

**Scores after:** see `scorecard.md`, pass 3. Clear goal 7 to 8, enjoyable gameplay 6 to 7, the rest unchanged.

**Left open:** nothing here has run in Unity, and the music has not been listened to. Known limits: an animal put away
and woken again comes back healed; sea creatures killed from out of reach still count toward the gun ladder and Focus,
as before; the land creatures pay nothing until a spawner brings them onto a world; whether shots should stop at the
sea's surface is a design question for later. The Deck acceptance needs a per-event log of the music's band, piece
and layers beside shots and points (not built). Next pass: gap 4, envelope prizes that do something in the game, and
more unlocks.

## Pass 4 (28 Nov 2026): the Locker, envelope prizes that do something

**Gap closed:** the prize envelope (Matthew, 16 Nov 2026: "an item system like tf2") gave upgrades, ship paints, gun
skins and suit pieces that were only listed in the collection. Nothing in the game used them, so the envelope promised
nothing.

**Built** (report `docs/reports/locker.md`):

- The Locker (`Items/Locker.cs`, plain C#): one prize in use per place (upgrade, ship paint, a skin for every gun and
  one of a gun's own per gun, helmet, visor, suit, jetpack, badge), saved in layout 6. A won cosmetic, skin or paint
  goes to work by itself only where its place is empty; an upgrade never does, because each has a cost.
- Upgrades are sidegrades (`UpgradeEffects`): a gain and a cost on two different numbers, multiplied into the run's
  build on top of the perks, clamped to 0.5 to 2. New numbers the run can change: jet fuel, ship fuel, reload time.
  The cost is written wherever the upgrade is named: the reveal, the Collection page, a note as the run starts, the
  break and the UPGRADES page.
- Looks (`Items/PrizeLooks.cs`): six hull paints that keep the lander's dark panels and window bands; seven gun skin
  patterns in the model shader that keep the pictures' wear; helmet edge shade, a gentle visor tint, suit plate
  colours held to 7:1 contrast for the plate's words, jet colours that are never a damage red, and a badge.
- The reveal says where the prize went, what is in its place, or how to put an upgrade to use. The Collection page
  (six rows a page, grouped by place) puts a prize to use or takes it off in one press and says what each does.
- Fixed on the way: the suit plate's air time ignored a bigger tank bought in the shop.

**Checked:**

- `dotnet test tools/rules-tests`: 3,142 run, 3,141 passed, 1 skipped (the cabin bake check, as before), after the
  reviews' fixes. 38 tests are new (`LockerTests` 17, `PrizeLookTests` 21); `UnlockTests` no longer pins the save
  layout, and two older tests take the perks' word "max armour". The first full run after the fixes had one failure (a
  test still holding Heavy Hands' first numbers); it was fixed and re-run with every test that reads the changed files.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- Offline shader check: all 30 model shader programs pass glslang (the same count as before the skins).
- `tools/envelope_view` draws the reveal as the game does; the upgrade, helmet and Celestial reveals were looked at.

**Specialist reviews** (each run on this pass's first build, each finding checked before it was applied):

- game-designer: three upgrades were weak or cost in the wrong place (retuned; no upgrade costs gun or melee damage,
  held by a test; older item assets follow the catalogue through `ItemCatalog.Version`); the stat words differed from
  the perks'; "Locker" on screen promised Part B's base locker (kept off screen); the cost was first seen at the
  break (now a note at the run's start); "Choose" became "Press"; the reveal names what is already in a place;
  paintings say KEPT; a Celestial effect says it is not shown yet. Its next gap: reach.
- deck-performance: the skin keyword would have doubled the model shader's forward programs and stalled on first
  use (now a branch on a material value); skins were looked up in the save every time a gun came into the hand (now
  once a world); the visor tint used reflection every frame (setter bound once). Not changed: a save on every
  Collection press (title screen only). Budget rows written.
- unity-reviewer: B did nothing on the Collection page and a stray click left the D-pad with nothing to move (both
  fixed); the flake and star patterns would likely crawl on the held gun (now faded by the pixel's footprint, the fine
  wood grain with them); the save layout change means older builds will not write the save (said in the report).

**Scores after:** see `scorecard.md`, pass 4. Fun unlockables 6 to 7, the rest unchanged.

**Left open:** nothing here has run in Unity. Prizes open only after ante 9, so a new player meets the Collection
late; Celestial effects are not drawn; paintings wait for frames on the cabin wall and the five public-domain
pictures; the ticket's cosmetic token is still used by nothing. Next pass: gap 5, reach (goal unlocks that give a
cosmetic, and more goals).

## Pass 5 (28 Nov 2026): earned looks, within a new player's reach

**Gap closed:** reach. Envelopes open only after ante 9 (Matthew's rule, kept), and a first-time player is expected
to die around ante 3 or 4, so the looks pass 4 made to work stayed out of a new player's hands for many runs.

**Built** (report `docs/reports/earned-looks.md`):

- Eight goal unlocks give a look instead of a perk or a kit (`UnlockKind.Look`; eighteen unlocks in all), each in a
  place no other look takes: a ship paint for the first landing away from Earth, a gun skin for a disarm, a badge for
  a leg shot off, then a visor tint (a kill in Focus), a helmet shade (a salvage crate brought home), a jet flame
  colour (an AIRBORNE kill), a readout plate colour (a bonus mission) and a pistol skin (a kill in the sea, from in
  the water). The first three lead the catalogue, within a first run's reach.
- Each goal is one feat in one run (`UnlockTracker`; feats from `TaskDirector`: `HitOutcome.Disarmed`,
  `MannequinCombat.DisarmedFrame` for an arm shot off with its gun, `HitRecord.Legs` for a leg off, `KillFacts.inFocus`
  and `seaCreature`). Nothing counts across runs, and there are no meters.
- A look goes on as its moment shows: `LockerLook.Refresh` raises `LockerLook.Changed`, and the hull, the suit plate
  and visor, the jet flames and the gun in hand take it there and then.
- Kept out of the envelopes: `PrizeRoll.InEnvelopes` leaves earned looks out of every pool while the roll draws the
  same numbers, so every envelope gives what it gave before. An envelope's prize takes an earned look's place
  (`Locker.GivesWay`), and the reveal names it.
- On screen: the unlock moment says what the look is and where ("A SHIP PAINT, NOW ON YOUR LANDER"); the next unlocks
  carry a look's kind beside its name; an empty Collection names the next look before the envelope; earned looks are
  "Earned", with a cream stripe and the word on the row; the title screen's Unlocks page has two lists; the practice
  range says once that goals count only in a run (`PracticeNote`).
- The save for a goal met in a fight waits for the fight to end, a pause, or 20 s at most (`UnlockMoment.SaveNow`).

**Checked:**

- `dotnet test tools/rules-tests`, after the first reviews' fixes: 3,164 run, 3,163 passed, 1 skipped (the cabin
  bake check, as before). New: `EarnedLookTests` (7, then 8 after the second look below), fourteen in `UnlockTests`,
  one in `LockerTests`; older tests follow the new order and words.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.

**Specialist reviews** (on this pass's first build; each finding checked before it was applied):

- game-designer: the looks showed only from the next run (now at once); the landing should lead, as the first tips
  point at the Moon and the practice range cannot meet it; "a jetpack", "a helmet" and "a suit" read as gear (now
  what they change: "a jet flame colour" and so on); a look's name says nothing of what it is (its kind beside it);
  the earned stripe was the IN USE green (now cream, and the word "earned"); stripes on every gun looked like a toy
  (muted enamel); the visor's blue was the Focus rim's (smoke grey); the sea goal did not say where sea creatures
  live; the practice range would leave a new player wondering where the unlocks went (the note). Its view was 8 for
  unlockables once the first three were done; scored 7 here because the Celestial effects are still undrawn and can
  be done in the cloud.
- unity-reviewer: an envelope prize won after the earned looks would never go to work (now an earned look gives way);
  a torso blown off counted as a leg shot off (now the torso must still be on); checked that the arm-sever disarm
  reaches the director in the same frame; a dead field on the Unlocks page.
- deck-performance: a goal's save mid-fight could be felt (now it waits). Budget rows written.
- Second look (the code that puts a look on at its moment and the practice note were built after the reviews, so
  unity-reviewer and deck-performance read them again): every refresh built the hull again, a few ms on the Deck,
  whatever look changed (now only a new paint does); a refresh could share a frame with the save, or run twice in
  one at a break (now one, on the next frame that writes no save); the suit plate, badge and jet material were
  remade unchanged; the practice note read the save on every arena hit (now only for a disarm, a limb or a kill); a
  note cut short by a restart came back on the next world; and the retuned colours of the two earliest looks would
  never reach item assets made before them (now earned looks follow the catalogue, `ItemCatalog.Version` 3).
  Checked sound: every listener leaves on every way its object is destroyed, the old hull mesh is freed once, the
  visor tint never outlives the suit.
- After the fixes: `dotnet test tools/rules-tests` 3,165 run, 3,164 passed, 1 skipped; compile check clean apart
  from the two known lines.

**Scores after:** see `scorecard.md`, pass 5. All-round fun 6 to 7; Fun unlockables stays at 7 until the Celestial
effects are drawn (the design review would have given 8).

**Left open:** nothing here has run in Unity. Celestial effects are not drawn (the words say "not shown yet"), the
one part of the unlockables line the cloud can still close; paintings wait for frames and the five public-domain
pictures; the ticket's cosmetic token is still used by nothing. Next pass: gap 6, the Celestial effects.

## Pass 6 (28 Nov 2026): Celestial effects drawn

**Gap closed:** the top tier's effects. Part B section 12 asks for "an Unusual-like top tier featuring special effects
(e.g. fire)"; since pass 4 a Celestial prize went to work as an ordinary skin, paint or suit piece and its effect said
"not shown yet".

**Built** (report `docs/reports/celestial-effects.md`):

- Six effects drawn where the item shows: moving light on the gun in the player's hands and on the lander's painted
  hull (`OA_Fx` in `OA_Model.shader`, a branch on a material value, no keyword), pale beads of light just outside the
  suit's readout plate for a helmet, visor, suit, jetpack or badge (`SuitHud.BuildBeads`, `LightBeads`), and the same
  beads round the prize card on the envelope's reveal (`EnvelopeScene.EffectBeads`). Solar Flare (rising tongues,
  white-hot cores, on the gun; slow clouds in fire colours on the hull, which never rise), Aurora (swaying curtains),
  Comet Tail (heads running from the muzzle back to the hand), Nebula Drift (slow clouds), Starfall (stars lit one at
  a time, sliding down), Event Horizon (turning dark bands with bright rims).
- The sums are plain C# (`Items/CelestialLooks.cs`: `CelestialLooks.Surface`, `Lift`, `TurnedNoise`, `Face`,
  `RunScale`, `HullDamageShare`, `BloomShare`, the shader's hash and noise; `CelestialPlate` for the beads), so the
  tests and the offline previews judge what the GPU draws; a rule test compares the two sets of numbers.
- Never mistaken for damage, a warning or an aim tell: light on the item's surface only, never past white (under 1%
  of it reaches the bloom at the default threshold), steady from one second to the next (flames first swelled
  together about once a second; the noise is now read on a grid turned against the item), no reaction to anything in
  a fight, no star lit in under 0.48 s, comets never run toward the muzzle, the hull's dark trim left plain
  (`ShipShell.PaintedCorners` in the mesh's colour alpha), the hull's effect faded by its damage and gone from a
  broken panel, Event Horizon only 20% dark on the hull, no seam, cut or sparkle round the cabin, never on an enemy's
  gun (`MannequinCombat.TryPickUp` takes the skin off) or on a gun that can blow up (the Wildcard wears the skin, not
  its effect), and the beads outside the plate, at most 0.7 alpha, never lit from dark in under a quarter second,
  paler than the readout's orange, green and blue.
- The plate is quiet when it matters: the beads go dark beside a warning on the plate or the notice strip and in a
  fight (the readout's own fight, or a creature's warning), show off for 3 s as the plate comes up and then idle at
  0.45, and with several suit effects the next one shows each time the plate comes up. The speed lines moved up 8 px
  and the notice strip 12 px right to stay clear of them.
- Words: "Effect: Aurora (on every gun)", "(on that gun)", "(on your lander)", "(round your suit's readout)" on the
  reveal and in the Collection (`Locker.EffectWhere`, `EffectWords`); "not shown yet" is gone.
- A test button for Unity: the Collection's test row, "Try the next effect" (`Locker.TryNextEffect`, editor and
  development builds), puts the next effect on a test gun skin, ship paint and badge, in use; Celestial is one
  envelope in two hundred.
- Fixed on the way: `tools/envelope_view` had not compiled since pass 5 (`Unlocks.cs` reads `LaunchSite`); it now
  picks its files the way the rule tests do, and draws what a group puts outside its bounds unless the group clips.

**Checked:**

- `dotnet test tools/rules-tests`, on the first build: 3,182 run, 3,181 passed, 1 skipped (the cabin bake check, as
  before). New: `CelestialLooksTests` (17); `LockerTests` and `PrizeLookTests` follow the new words.
- Runtime compile check: clean apart from the two known `excludeLayers` lines (one slip fixed first: the cabin's
  `ShipMeshes.SetBaked` had been given the hull's new alpha, which it does not take).
- Offline shader check of `OA_Model.shader` (glslang, every pass and keyword set): no failures; no reserved names.
- Offline pictures of every effect on a gun, round the hull and round the plate, from the C# sums, and of the reveal
  with each effect's beads (`tools/envelope_view`).

**Specialist reviews** (on this pass's first build; each finding checked before it was applied):

- game-designer: the beads moved through the plate's own warnings and touched the speed line (hushed, and the lines
  moved); Solar Flare's rising flames on the hull read as thrust or fire (clouds there); Event Horizon darkened the
  hull like damage (20% there, and every hull effect fades with the damage); the reveal's sparks were the same for
  every effect and the plate's idle was a faint dotted frame (the effect's own beads on the reveal, a 3 s flourish,
  then 0.45); two fast comet heads read as "claim me" (one slow head); turns every 10 s changed the plate in a fight
  (a turn on each step outside); the words should name the gun ("on every gun", "on that gun"); the Wildcard wore an
  effect (never now); Starfall lit too many stars a second on a rifle (half speed); "never blooms" rested on the
  default threshold (held by a test). Its view: this meets section 12 on the gun and the hull.
- unity-reviewer: only the first motion had `[branch]` (each has its own now); no budget line (written); Starfall's
  cells cut stars along four upright lines on the hull (two faces there, cells round the turn); a sparkle at the dome's
  top (the pattern fades toward the axis); the words said the light does not slow with Focus, which it does
  (corrected); the longest reveal line nearly wrapped (a size smaller past 44 letters, and tested).
- deck-performance (an offline compile for the Deck's GPU, not Unity's compiler): the effect's share came after the
  whole sum (now first, with an early return, and the hull's `atan2` on the hull alone); the effect adds about 200 to
  390 steps a pixel where it is drawn; every model pixel now needs 64 registers instead of 56, worn or not (measured
  again after the change: still 64); a model pipeline builds in about 40 to 52 ms on a cold cache instead of 22 to 33.
  Kept a material value, not a keyword, so a gun ladder step never builds a program mid-fight; the budget says to make
  it a keyword and warm it if the register cost measures over 0.1 ms. Budget rows written from it.
- After the fixes: `dotnet test tools/rules-tests` 3,189 run, 3,188 passed, 1 skipped (`CelestialLooksTests` now 24);
  compile check clean apart from the two known lines; offline shader check of `OA_Model.shader`: no failures.

**Scores after:** see `scorecard.md`, pass 6. Fun unlockables 7 to 8: with pass 5's reach, the top tier now does what
section 12 asks.

**Left open:** nothing here has run in Unity or on a Deck. Warming the model shader at a run's start (owed since the
shine; now 40 to 52 ms a pipeline on a cold cache); making the effect a keyword only if its register cost measures
over 0.1 ms; the Collection's rows name an effect but do not show its beads; paintings wait for frames and the five
public-domain pictures. Next pass: gap 7, where to shoot a creature.

## Pass 7 (28 Nov 2026): where to shoot a creature

**Gap closed:** where to shoot a creature. Every creature has one weak place that kills it quickest, armour that is
the slow way and pieces it fights with that can be shot off, but only a knock and blood told them apart, and the hit
bell rang the same on the Strider's lid as on its belly. The Pack biter's weak place, its head, even played the armour
knock.

**Built** (report `docs/reports/where-to-shoot.md`):

- Every hit on a creature says where it landed (`Core/HitZone`, carried by `HitOutcome.Zone`): armour that held,
  the weak place, or a piece it strikes with shot off, from one function per kind in `Creatures/WhereToShoot.cs`
  (plain C#), set in each creature's `TakeHit`; its armour knock now plays exactly when the zone says armour. The
  seven creatures' piece enums moved into their plain layout files so the rules can name them.
- The marker says it in the language players learn on people (`UI/HitConfirm.cs`): a glance is a small, thin, grey X
  that does not pop, with the new `hit.glance` in place of the bell; the weak place is the head's gold and bell a fifth
  up, and a kill through it the headshot kill's marker, chime and slow-mo (`HitZones.BestKill`); a claw, jaw or drum
  shot off is the disarm's cyan and bell a fourth up (never the disarm unlock goal). People are marked exactly as
  before.
- The first time a kind's armour turns a shot, until the player has hit its weak place, one line over the crosshair
  names the piece and where to aim ("LID · AIM UNDER IT, AT THE BELLY"; `Creatures/AimLines`): at most twice a kind
  ever, 40 s apart, only with the new-player tips on, remembered per profile, forgotten with the tips.
- Fixed on the way: every hit on a creature asked for a 50 ms hitstop and a limb's shake, armour included, because the
  creatures gave every hit some juice; now only a piece off, a choke or a kill does, as for people. The creatures'
  shell knock (`impact.armor`) was struck metal that rang at the ante clock's pitches at most of the pitches the
  creatures play it at; it is now noise with no ring.

**Checked:**

- `dotnet test tools/rules-tests`, on the first build: 3,199 run, 3,198 passed, 1 skipped (the cabin bake check), after
  one line was shortened to fit its 44 letters. New: `WhereToShootTests` (10).
- Runtime compile check: clean apart from the two known `excludeLayers` lines (one slip fixed first: `Walker` already
  had a `Kind`, so its `ICreature.Kind` is written out for the interface).
- `tools/audio/qa.py`: PASS, the 8 warnings it had before.

**Specialist reviews** (on this pass's first build; each finding checked before it was applied):

- game-designer: the glance's crack measured like the marks of the four creatures that face the player armour-first
  while they wind up (remade soft-fronted and dull, and `qa.py` now holds the glance and the shell knock to every
  creature's mark, which is how the knock's clock ring was found); "ARMOUR" is the player's own suit on the suit plate
  (each kind names its piece: LID, HEAD, SHELL, SKULL); three lines reworded (the Ambusher's pointed at a body usually
  underground, the Roller's "flips" fitted its curl, the Screamer's named two places); the Ambusher's jaw was grey while
  it holds, though shooting it off frees the player (plain now, with a rule test); the line under the crosshair covered
  what it names (moved over it; not proven with offline frames). Its answers: gold on the Blind stalker's and Ambusher's
  bodies, the maw's throat, and no bell on armour, all yes. Its view: line 2 reaches 8 with these and a
  development-only playtest log.
- unity-reviewer: a line held still under the death page showed again on the launch pad (put away at every run's
  start); the line's memory was not reset between Play sessions with domain reload off (`HitMarker.ResetStatics`); the
  Ambusher and the Screamer read the zone after a one-hit-kill gun's follow-up had reset their shared result (moved
  above it); the line met the arena's pick-up prompt (moved); a wrong comment about hitstop. Its design question, that
  the headshot slow-mo never fired for creatures, is answered: it fires for a kill through the weak place.
- deck-performance: nothing blocks, no garbage a hit; the line's alpha is now set only on the frames it changes. Budget
  rows written from it.
- After the fixes: `dotnet test tools/rules-tests` 3,202 run, 3,201 passed, 1 skipped (`WhereToShootTests` now 13);
  compile check clean apart from the two known lines; `qa.py` PASS with the new section (the glance 0.10 of its loudest
  1 ms in where the bright marks are 0.6 to 1, centred at 1.3 kHz, 6.4 dB under the quietest mark; the knock under
  2 kHz and ringing under 10 dB at every pitch from 0.42 to 1.06).

**Scores after:** see `scorecard.md`, pass 7. No line moves: line 2's listed gap is closed in design, and the design
review's last cloud item for 8 is a playtest log (gap 9).

**Left open:** nothing here has run in Unity or on a Deck, and no sound has been listened to. Whether the line ever
covers the place it names (from standing height a body behind a head shows a little above it, about 30 px at 8 m);
the Ambusher's body lengths do not share damage, so a burst can ring gold without killing (accepted); a
development-only playtest log (gap 9). Next pass: gap 8, land creatures on the real worlds.


## Pass 8 (28 Nov 2026): land creatures on the worlds

**Gap closed:** gap 8, land creatures on the real worlds. The four creatures that walk and fly (the Strider, the
Sprinter, the Roller and the Screamer) lived only in the practice range, so what a player learned of their warnings
(pass 2) and weak places (pass 7) never paid in a run (pass 3), and every fight on a world's ground was a camp's.

**Built** (report `docs/reports/land-creatures.md`):

- Where they live (`Creatures/LandRules.cs`, plain C#): each world's own animals from the brief (`LandHabitats`), each
  kind of world led by its own animal (the hot, dry worlds by the Sprinter, the airless moons by the Strider, the
  worlds under air by the Screamer, the Roller everywhere at most a third, the Screamer only under air); haunts seeded
  per world on dry, gentle ground with room to fight round it, well apart and clear of the pad, the camps, the bases,
  ponds and caves (`LandField`); half of them lived in at ante 1, three quarters at ante 2, all from ante 3, and a
  haunt lived in once is lived in for the rest of the run.
- On the solar world (`Creatures/LandCreatures.cs`): the haunts worked out on a background job after the world's camps
  are known; the nearest lived-in haunt woken at 110 m, never within 70 m, two at once at most, put away beyond 180 m;
  the dead remembered for the run; every animal given its credit and a claim per haunt.
- Keeping to a round, hilly world (`Creatures/CreatureGround.cs`): the built ground found without colliders, "up" away
  from the world's middle, places kept in the world's space; a step refused by ground steeper than the kind takes
  (measured over the half metre ahead), the water's edge (the sea and ponds) and cave mouths, and stopped by anything
  standing higher than it steps over (30 cm, the Strider 1.2 m); the Roller and the Screamer lie with the ground, the
  Screamer's flight eased onto what is below it and never drawn inside a cliff; the practice range kept line for line.
- The walkers' legs on slopes (`WalkerAnimator`, only when given the ground): feet follow the ground, a foot steps once
  its leg is stretched past 90% of its reach, is put down within 86%, the body comes down so every foot that is down is
  within 88%, and a foot still out of reach slips along the ground toward its leg. Measured offline with a probe: walking
  straight, no foot is drawn more than 3 cm off on either walker (it was 7,831 frames of about 55,000 for the Sprinter).
- A kill pays only when the animal could answer the player (`LandReach`): kept from them for 4 s with its steps refused
  and no ground gained, it cannot, until it gains ground or they come within reach; its own opening never counts against
  it; the note says KILLS PAY ONLY WHEN IT CAN GET AT YOU.
- From the design review, for every creature: a creature's fight is a fight for the whole game (`ReadoutFocus` with
  the creature stamps: no tips, no unlock banner or save, no restless squad's radio call in the middle of one); a
  creature's kill names its strike on the death page in plain words (`StrikeWords`: "Caught in a giant claw.", and nine
  more); an animal with nothing left to hurt the player with is not after them, so the music lets go; ground the Roller
  cannot roll up ends its roll like any other opening, and only something solid is a crash.

**Checked:**

- `dotnet test tools/rules-tests`, on the first build: 3,214 run, 3,213 passed, 1 skipped (the cabin bake check). New:
  `LandRulesTests` (12).
- Runtime compile check: clean apart from the two known `excludeLayers` lines.

**Specialist reviews** (on this pass's first build; each finding checked before it was applied; the report's "Reviews"
section has all of them by number):

- game-designer (measured with a Python copy of the placement rules over 20 seeds a world, geometry only): the four
  findings that broke rules 3, 5 or 7 (a creature's fight not a fight, the death words, kills it could never answer,
  the Roller's unseen crash threshold), all applied; the world mixes could not be told apart (each kind of world now led
  by its own animal, tested on proportions); the music tense over a harmless animal; never wake within 70 m, room to
  fight round a haunt, the Strider's 1.2 m step-over and the slope test at each kind's steepest, all applied. Its
  finding that camps will join most creature fights (83 to 100% of haunts have a camp within a shot's hearing) is kept
  as it is and said in the report; the two sides noticing each other is the new gap 12.
- unity-reviewer: slopes judged over one frame's run (tile seams read as cliffs), a flying Screamer sinking into steep
  ground, walking on air over cave mouths, feet sliding on refused steps, take-off snapping upright, ponds ignored, a
  ball crashing at a downhill lip and no budget row: all applied. It checked by reading that the practice range is the
  same line for line.
- deck-performance: the plane cache was the largest cost (now 1,024 places, the slot from the hash's top 10 bits); the
  Sprinter's hitbox sync now only near or seen; the wake frame (guessed 2 to 4 ms), loose pieces and reading planted
  feet once are left to be measured. Budget section written from it.
- After the fixes: `dotnet test tools/rules-tests` 3,222 run, 3,221 passed, 1 skipped (`LandRulesTests` now 16,
  `CreatureFightTests` 3 new, one more in `RollerRulesTests`); compile check clean apart from the two known lines.

**Scores after:** see `scorecard.md`, pass 8. No line moves: gameplay stays 7 because creatures and the camps ignore
each other in the fights the placement makes likely (gap 12), and line 2 still waits on a development-only playtest
log (gap 9).

**Left open:** nothing here has run in Unity or on a Deck. Whether a foot slipping on the steepest ground at full pace,
or a Strider walking crouched on a steep slope, looks right; whether an animal can be trapped among rocks; whether the
worlds feel alive without crowding the camps. Not built: the Ambusher, the Cave hunter and the Blind stalker on the
worlds (they need soft ground and caves); an animal held below a ledge backing off home; creatures and the camps
noticing each other (gap 12). Next pass: gap 9 or gap 12, by re-scoring.
