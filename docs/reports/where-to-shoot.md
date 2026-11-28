# Where to shoot a creature (Ralph loop pass 7, 28 Nov 2026)

Matthew asked for a game that is "easily understandable". Every one of the nine creatures has one weak place that kills
it quickest, armour that is the slow way, and pieces it fights with that can be shot off ("one weak place to find and
one strike to read", `Tasks.CreatureTiers`). Nothing said which was which except a dull knock and the blood, and the hit
bell rang the same on the Strider's lid as on its belly, so the game's reward went as much to the shots that did least.
The Pack biter's weak place, its head (65 hit points against its body's 80), even played the armour knock, which said
the opposite of the truth. **Nothing here has been run in Unity, and none of the sounds has been listened to: their
numbers are measurements.**

## What changed

| | |
|---|---|
| Problem | A player could not tell a creature's armour from its weak place except by a knock and blood, and the hit bell rewarded both the same. |
| Hypothesis | If every hit on a creature says where it landed in the marks players already learn on people (gold and the bell a fifth up for the place that kills; cyan and the bell a fourth up for a weapon gone), and armour that held gets a small grey flat mark and a soft dull knock in place of the bell, players find each creature's weak place by shooting at it; one short line, the first time a kind's armour turns their shots, names the piece that turned it and where to aim, for anyone who does not. |
| Principle | Learnable challenge, never hidden rules (3); legible risk and reward (5); managing cognitive load (7): the line comes only to someone doing the less effective thing, at most twice a kind, and never once they have found the weak place; ethical design (9): no bestiary, no completion meter. The critique loop's sound rules: a reward is a bell in D, anything else never is; a creature's one hard mark is the only thing in a fight that sounds like one. |
| Smallest test | Rule tests (`WhereToShootTests`, 13 tests): every kind has exactly one weak place and it is that kind's quickest kill (from the kinds' own hit points); armour that comes off knocks while it holds and is a piece off once broken; a jaw or a drum the player should shoot is never grey while it holds; the pieces a creature fights with are a weapon off when shot off, fins and wings never; the marker says each (a glance is smaller, thinner, grey, shorter, never pops and never rings the bell, and never cuts a real hit's marker short); a kill through the weak place is the best kill for the marker and the slow-mo alike; every kind with armour has a line of at most 44 letters that names one place, starts with the piece that turned the shot and never says ARMOUR, and the Pack biter has none; the lines say when a hidden weak place can be shot; the line's memory (only on a glance, until found, at most twice, 40 s apart, kept across sessions, junk read safely). `tools/audio/qa.py`: the glance and the shell knock against every creature's mark. The hit marker's own 7 tests still pass unchanged, so people are marked exactly as before. |
| Acceptance (offline, met) | All of the above; `dotnet test tools/rules-tests`: 3,202 run, 3,201 passed, 1 skipped (the cabin bake check, as before); the runtime compiles apart from the two known `excludeLayers` errors; `qa.py` passes (0 failed checks, the 8 warnings it had before). |
| Acceptance (Deck, not yet run) | See "Playtest protocol". |

## Each creature

| Creature | Armour: grey, a knock, no bell | Weak place: gold, the head's bell | Weapon off: cyan, the disarm's bell | The line, the first time its armour turns a shot |
|---|---|---|---|---|
| Strider | its lid (360) | the belly underneath (240) | each claw arm | LID · AIM UNDER IT, AT THE BELLY |
| Sprinter | its head in front (200) | the abdomen behind (150) | each jaw | HEAD · AIM FOR THE ABDOMEN BEHIND |
| Roller | its head (300); plates and tail fan until they break | the soft body under the plates (200) | the plate that leaves it too few to close into a ball | SHELL · SHOOT ITS BELLY WHILE ON ITS BACK |
| Cave hunter | its head (220) | the fore body, the three segments behind the head (150) | none: it strikes with its head | HEAD · AIM JUST BEHIND THE HEAD |
| Screamer | its fore body (300) | the abdomen (150) | each drum (its scream) | SHELL · AIM FOR ITS SOFT ABDOMEN |
| Ambusher | its head (200) | the body (90 a length) | each jaw (white while it holds: shooting it off breaks a hold) | HEAD · WHEN IT REARS, AIM AT ITS BODY |
| Blind stalker | its skull (280) | the trunk (200) | none: its jaw is not a piece of its own | SKULL · AIM FOR THE BODY, NOT THE HEAD |
| Drifting maw | its skull (420) | down its open throat (its body's 320 at 2.5 times: 128) | its jaw | SKULL · SHOOT DOWN ITS THROAT WHEN IT OPENS |
| Pack biter | none | its head (65) | its jaw | none: nothing turns a shot |

Grey only ever means "the slow way". A piece the player should shoot is never grey while it holds: every jaw is white
until it comes off (the Ambusher's was grey in the first build, though shooting it off frees a player it holds), and so
is a drum until its scream goes with it.

A creature killed through its weak place gets the headshot kill's marker (red with the gold ring outside), its
three-bell chime and, when it is switched on (pause, Feel), the headshot slow-mo (`HitZones.BestKill`, one rule for all
three); one killed through its armour, or by a one-hit-kill gun anywhere else, the plain kill's two bells. So the best
way to kill each one is also the one that sounds and feels best. A piece that comes off and does not take a weapon (a
leg, a fin, a wing, a tail, the Cave hunter's cut-off rear) is the "limb off" mark as before.

The weapon-off mark is the disarm's look and bell only. It never counts as the "disarm" unlock goal, which is still a
gun shot out of an enemy's hands (`HitOutcome.Disarmed` is untouched; the creature says `HitZone.WeaponOff`).

## The line over the crosshair

The piece that turned the shot (LID, HEAD, SHELL or SKULL) in the glance's grey, a dot, then where to aim in cream,
Overpass Mono 15 px with an outline and a shadow, 70 px over the middle of the screen: inside the damage ring, clear of
the biggest X. The first build put it under the crosshair, where it covered what it names (a belly under a lid, a
reared body under a head) and met the pick-up prompts (-92, -96) and the melee TAKEDOWN (-52). It never says ARMOUR:
on the suit plate that word is the player's own (ARMOUR FAILING, NO ARMOUR LEFT), and the line would have read as a
warning about them. It holds 2.7 s and fades in 0.5 s, and its clock stands still while the game is paused. When
(`Creatures/AimLines`):

- only after a shot glances off that kind's armour, so it answers what the player just did;
- never once the player has hit that kind's weak place, however they found it;
- at most twice a kind ever (a second chance for a line missed in the noise of a fight), and never within 40 s of the
  last line of any kind;
- never with the new-player tips switched off, and forgotten with them when the player asks for the tips again
  (title screen, Extras).

A line held still under the death page is put away at the next run's start. The memory is kept in PlayerPrefs
(`oa.aim.lines`, for example `Maw=2,Strider=found`), written in memory only, never flushed mid-fight; the end of a run
and quitting write it, and if the game crashes first the line just shows again. It works the same in the practice
range, where the land creatures live, and in a run.

## The sounds

- `hit.glance` (`tools/audio/sfx/combat.py`) plays in the player's head in place of the bell: a dull thud and the shot
  skidding off (a band of noise sliding from about 1.8 to 0.9 kHz), its front rising over about 5 ms, nothing kept
  above 2.6 kHz, 0.14 s, -17 dB, 2 voices, 50 ms cooldown. The first build had a crack in front that measured like the
  marks of the four creatures that face the player armour-first while they wind up (the Screamer, Cave hunter,
  Sprinter and Ambusher), so the player's own shots, one a shot, could have buried or stood in for the one hard mark
  that says "move".
- `impact.armor`, the creature's own knock where the shot landed, is remade too. It was struck metal, and at the
  pitches the creatures play it at its partials rang by the ante clock's tick and warning (979, 1629 and 640 Hz: up to
  57 dB over the neighbouring pitches at the Drifting maw's, 46 at the Screamer's), a family no creature may borrow. It
  is now a dry knock of noise through broad filters with a crack in its edge, nothing under 250 Hz (so the Deck's
  speakers carry all of it) and no ring at any pitch.
- `tools/audio/qa.py` holds both to every creature's mark ("The glance and the armour knock against the marks"; the
  knock at each creature's pitch and as far as its own pitch spread takes it, 0.42 to 1.06):

| Sound | Front (share of its loudest 1 ms in) | Brightness (first 45 ms) | Under the quietest mark | Nearest mark (measure of difference) | Rings at 979 / 1629 / 640 Hz |
|---|---|---|---|---|---|
| `hit.glance` | 0.10 (allowed 0.35; the bright marks 0.6 to 1) | 1317 Hz (under 2 kHz; the bright marks 2.4 to 4.8 kHz) | 6.4 dB (at least 6) | 7.0, the Strider's (at least 3) | 2 / 3 / 2 dB (under 10) |
| `impact.armor` 1, worst pitch | 1.00 (a knock's front: it plays where the shot landed, on the shot) | 1590 Hz | | 4.0, the Strider's | 6 / 4 / 9 dB |
| `impact.armor` 2, worst pitch | 0.90 | 1847 Hz | | 3.6, the Strider's | 4 / 6 / 7 dB |

## Fixed on the way

- **Every hit on a creature froze the game.** The creatures gave every hit some "juice" (0.04 for armour, 0.12 to 0.3
  otherwise), and the hitstop and camera shake fire for any juice above 0: 50 ms of near-freeze and a limb's shake on
  every shot, so firing full-auto at a creature stuttered the whole game, glances included, which also said the
  opposite of "that did little". A hit that takes nothing off is now 0, as it always was for people (Part B: "hitstop
  on the shooter when a limb pops"); a piece off, a choke and a kill keep theirs.
- **The shell knock rang like the ante clock** (above).

## How it is built

- `Core/HitZone.cs`: `Plain`, `Armour`, `Vital`, `WeaponOff`, carried by `HitOutcome.Zone` (people leave it `Plain`),
  and `HitZones.BestKill`, which the marker and the slow-mo share.
- `Creatures/WhereToShoot.cs` (plain C#): one function per kind from the piece hit and whether it broke, the shell
  words and the lines, and `AimLines`. The seven `XxxPiece` enums moved from the Unity classes into their plain
  `XxxLayout.cs` files so the rules can name them.
- Each creature's `TakeHit` sets the zone, before a one-hit-kill gun's follow-up starts the shared result afresh, and
  its armour knock plays exactly when the zone is `Armour`, so the knock and the grey mark can never disagree. A
  walker's claw or jaw counts only if it was still on (`Walker.TakesWeapon`); the Roller's charge counts as lost on the
  plate that takes it below `RollerBody.PlatesToRoll`.
- The Pack biter's head no longer plays the armour knock. It bleeds and rings gold. The Ambusher's jaws bleed like
  every other jaw.
- `UI/HitConfirm.cs`: `HitKind.Glance` (small, thin, grey, no pop, 0.16 s, below every real hit) and
  `Classify(..., zone)`. `UI/HitMarker.cs` draws it and the line; `Audio/GameAudioHooks.cs` plays the glance in place
  of the bell; `Core/TimeFx.cs` gives a weak-place kill the headshot slow-mo.
- `ICreature.Kind` names the kind (its model's name), in the practice range as well as in a run.

## Reviews

Each finding was checked against the code before it was applied.

- **unity-reviewer** (read the whole commit, every creature's `TakeHit` line by line against the build before):
  - a line held still under the death page (dying pauses) showed again on the launch pad after "Try again": now put
    away whenever the HUD is shown or hidden, which every run's start does;
  - the line's memory was not reset between Play sessions with domain reload off, so a line shown ten minutes into one
    session held back every line for the first ten of the next: `HitMarker.ResetStatics`;
  - the Ambusher and the Screamer read the zone after a one-hit-kill gun's follow-up had started their shared result
    afresh, so a jaw or drum shot off by that gun said armour (no visible effect today, since it always kills): moved
    above it;
  - the line crowded the arena's pick-up prompt: moved over the crosshair;
  - a comment said the line's clock stands still through a hitstop, which it does not (the time scale stays above 0):
    corrected.
  
  Its view of the rest: no marker kind is stored anywhere, so the shifted enum breaks nothing; with no zone, `Classify`
  gives people, guns and loose pieces exactly what it gave before; every creature's knock gives the old answer. Its
  design question, that the headshot slow-mo never fired for creatures although the marker now said "headshot kill",
  is answered: it fires for a kill through the weak place.
- **game-designer**:
  - the glance's crack measured like four creatures' marks: remade soft and dull, and `qa.py` now holds the glance
    and the knock to every mark (which found the knock's clock ring);
  - "ARMOUR" is the word for the player's own health: each kind names its piece instead;
  - three lines: the Ambusher's pointed at a body that is usually underground at the first glance ("WHEN IT REARS");
    the Roller's "flips" also fits its curl and "underneath" faces up on its back ("WHILE ON ITS BACK"); the
    Screamer's "OR" said the drums kill it (one place now, the drums left to their cyan mark);
  - the Ambusher's jaw was grey while it holds, though shooting it off is the counter: plain now, with a rule test;
  - the line covered what it names: moved over the crosshair. Not proven with offline frames: from standing height a
    body behind a head shows a little above it on the screen (about 30 px at 8 m for the Blind stalker's trunk, more
    up close), so at 4 m the line can still cross the far end of a trunk for its three seconds.
  
  Its answers: gold on the Blind stalker's and Ambusher's bodies, yes (a grey skull beside a gold body teaches the
  reverse of people in one burst); the maw's throat, yes (the quickest kill, the choke, and gold teaches the timing);
  no bell on armour, yes. Still not learnable, and accepted: the Ambusher's body lengths do not share damage, so a
  spread burst rings gold but may not kill. Rule 9: clean.
- **deck-performance**: nothing blocks; a normal hit allocates nothing new and nothing runs a frame unless a line is
  up; the line's alpha was set every frame of its 3.2 s, now only on the frames it changes (the fade). Budget rows
  written from it.
- After the fixes: `WhereToShootTests` 13, the full rule suite and the compile check as in the table above; `qa.py`
  PASS.

## Playtest protocol

From the design review. Not run: nothing here is a playtest result.

- **Testers:** five or more who have not met the creatures, with the line's memory cleared (title screen, Extras,
  "Show the new-player tips again").
- **Setup:** the practice range, the kinds in a random order, the rifle and then the SMG, on the Deck's speakers and
  on headphones. Half the testers play with the tips (and so the line) off, to measure what the line adds.
- **Record** every hit (kind, piece, zone, range) and every fight (time to kill, where the kill landed, whether the
  line showed, whether the creature's mark was dodged). The game has no hit log yet; until it does, record the screen
  and count from the video.
- **After each fight, ask:** where would you shoot it next time? What did the grey X mean, and what did the line
  mean? Did anything sound like you being hit, or like the creature's mark?
- **A day later,** the same session again, without the line.
- **Report counts,** not percentages.

Acceptance on the Deck (first guesses, to be revised by the first session):

- From the second fight with a kind, at least half the hits on pieces that kill land on its weak place, and most
  kills go through it.
- After the session each tester names the weak place of 7 of the 8 armoured kinds.
- Nobody reads the grey mark or the line as their own armour, or as being hit.
- Testers dodge on the mark as often while firing at armour through a wind-up as when they are not firing.
- Nobody finds the glance, the Blind stalker's gold or the line annoying.

## Still unverified

- Nothing here has been run in Unity: the grey mark, the line's place and fade, and the sounds mixed with the
  creature's own are read from the code.
- None of the sounds has been listened to.
- Whether players learn each creature's weak place from the marks alone, whether the line helps or distracts in a
  fight, and whether the line ever covers the place it names, are playtest questions.
