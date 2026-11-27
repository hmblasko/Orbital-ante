# Creature tells (Ralph loop pass 2, 27 Nov 2026)

Matthew asked for a game that is "easily understandable". The creatures were the hardest part of the game to read.
Six of the nine (Strider, Sprinter, Roller, Cave hunter, Drifting maw, Pack biter) borrowed the Faction A Scout's flare
sound as their warning, and the maw's draw-in borrowed the Heavy's spin-up, so one sound meant six different dangers.
The visor's arcs stayed at the screen angle they fired at, so turning toward a shot left its arc pointing the old way.
A creature winding up out of view showed nothing at all. **Nothing here has been run in Unity, and no sound here has
been listened to: every sound number below is a measurement.**

## What changed

| | |
|---|---|
| Problem | Creature warnings could not be told apart by ear, a warning from behind could not be found, and the arcs on the visor pointed the wrong way once the player turned. |
| Hypothesis | If every creature warns in its own sound with one hard mark the moment it takes aim, and the visor counts its wind-up down and snaps at that same moment, a player learns one rule for every creature ("move on the mark") and can find a warning they cannot see. |
| Principle | Learnable challenge, never hidden rules (3); legible risk and reward (5); managing cognitive load (7). The critique loop's sound rules: a warning is dry and never musical, keeps one timbre per family (never the clock's wood), and carries above 200 Hz for the Deck's speakers. |
| Smallest test | Rule tests (`WindupTests`, 18 tests) on the sounds as files, on what each change of stage plays, on every creature's stage and countdown read from its brain, on four real brains run through a warning, and on the arc's countdown, snap, dimming and turning. `tools/audio/qa.py` measures the same nine pairs against the clock. |
| Acceptance (offline, met) | Each sound is one creature's. Every aim's loudest 5 ms above 200 Hz is in its first 45 ms and stands at least 5 dB over its wind-up's; every wind-up's loudest 100 ms is at least -35 dB. No mark or wind-up rings within 2 semitones of the clock's tick (979 and 1629 Hz) or of its warning's knocks (640 Hz) by 10 dB or more. A real brain run gives one wind-up and one mark per warning, the mark within a frame and a half of the brain's lock, and the brain stops following on that frame. |
| Acceptance (Deck, not yet run) | From the design review, below. |
| Playtest protocol | At the end of this report. |

### One rule for every creature

Every creature that strikes warns in three stages (`Creatures/Windups.cs`, plain C#), read from its own brain:

- **Winding.** It winds up and still turns to follow the player. Moving now does not help yet.
- **Aimed.** It has fixed where the strike will land. A step aside now is a miss.
- **Striking.** The roll, scream, draw-in, dart or lunge, until it is over.

The stage comes from what each brain already tests for aiming: a walker's claw at `WalkerBrain.LockShare` of its
strike, the Roller, Cave hunter, Screamer and Ambusher at their `LockAt` share of the warning, the Drifting maw and Pack
biter by the `Locked` they report, and the Blind stalker at `StalkerBrain.LockAt`. While it winds up the reading also
carries how near the lock it is (`WindupRead.Progress`, 0 to 1), which the visor shows as a countdown. Nothing about
any creature's fight changed.

### Their own sounds (`tools/audio/sfx/creature.py`)

Each warning is two sounds. The **wind-up** plays the frame the stage turns to Winding. The **aim**, one hard mark at
the very front of its file followed by what comes next (a whoosh, a hiss, the water starting to move), plays the frame
it turns to Aimed (`Windups.Change`, `WindupTrack`, `WindupTell` in `Creatures/IWindup.cs`). Both halves are designed
as one sound and cut apart at the lock, so they play at one level and fit together at the default dials.

Because the mark is played by the game at the moment of the lock, it is on the lock whatever happens: a dial that makes
a warning longer or shorter, Focus (which slows sounds less than the game), a hitstop, a frame hitch. A warning called
off before it takes aim (an Ambusher backing off, a biter shot out of its mark, a Screamer choked) never gives its
mark, so the mark always means "move now". A Sprinter with both arms shot off is aimed from the start: it gives its
mark at once with no wind-up. A frame long enough to skip the whole aimed stretch still gives the mark, once.

| Creature | Wind-up (as it starts) | Aim (the frame it takes aim) |
|---|---|---|
| Strider | `creature.strider.raise`, 0.62 s: its claw arm creaking up joint by joint, heavy shell grinding on shell | `creature.strider.aim`, 0.60 s: a hard double clack as the claw sets open, then the heavy whoosh of the swing, gone where the claw comes down |
| Sprinter | `creature.sprinter.raise`, 0.38 s: a dry, fast chitter of its jaws rubbed together, rising | `creature.sprinter.aim`, 0.41 s: the sharp snick of its jaws cocking, then a hiss that ends as it lunges |
| Roller | `creature.roller.curl`, 0.62 s: its plates sliding over one another, rough scrapes coming quicker and quicker | `creature.roller.aim`, 0.36 s: the heavy clunk of the ball snapping shut, then a low grinding roll as it sets off |
| Cave hunter | `creature.hunter.rear`, 0.69 s: the skitter of its many legs under a dry hiss that tightens | `creature.hunter.aim`, 0.47 s: the double click of its fangs flying wide, then the whip of its head coming down |
| Drifting maw | `creature.maw.gape`, 1.04 s, heard through water: the slow creak of its jaw, a few bubbles getting away | `creature.maw.aim`, 0.56 s: a deep hollow thoom as its jaw drops fully open, and the water starting to move |
| Pack biter | `creature.biter.mark`, 0.64 s, heard through water: teeth chattering as its jaws flare, a swish as it rolls its flank | `creature.biter.aim`, 0.36 s: the hard crack of its jaws setting, then a held breath before the dart |
| Screamer | `creature.screamer.windup`, 1.0 s: dry clicks of its drums quickening into a rattle | `creature.screamer.aim`, 0.07 s: a hard double click of both drums as it jerks to full height |
| Ambusher | `creature.ambusher.coil`, 0.9 s: a wet hiss that tightens as it rears, ticks of its jaws half closing | `creature.ambusher.aim`, 0.23 s: the ringing scrape of both jaws flying wide |
| Blind stalker | `creature.stalker.gape`, 0.9 s: a long wet breath drawn in through its opening mouth | `creature.stalker.aim`, 0.18 s: the pop of its jaw dropping wide and its gills snapping out |

Also made: `creature.maw.inhale` (2.6 s, the draw-in, a roaring rush of water that swells and climbs, played faster or
slower to last as long as the draw-in does, `Windups.PitchFor`), `creature.maw.choke` (two gags and a burst of bubbles)
and `creature.biter.dart` (a rip through the water and the jaws snapping shut, two variants). Two older sounds that sat
almost entirely below what the Deck's speakers play were fixed: the Ambusher coming up and the Blind stalker's lunge.
The stand-ins are gone: no creature borrows the Scout's flare, the Heavy's spin-up or a splat for its warning, and a
sound that fails to play is never replaced by another's.

Measured by `tools/audio/qa.py` (above 200 Hz, as played; 979 and 1629 Hz are the clock's tick, 640 Hz its warning's
knocks; the clock's own tick stands 37 to 55 dB over its neighbours, and under 10 is allowed):

| Creature | Mark at | Mark rings at 979 / 1629 Hz | Wind-up rings at 979 / 1629 / 640 Hz | Mark over wind-up | Wind-up's loudest 100 ms |
|---|---|---|---|---|---|
| Strider | 6 ms | -32 / 1 dB | -3 / -5 / -2 dB | 10.1 dB | -26.8 dB |
| Sprinter | 4 ms | -1 / -4 | -1 / -10 / -1 | 12.2 | -28.7 |
| Roller | 8 ms | -8 / 5 | 6 / 2 / 2 | 12.4 | -31.0 |
| Cave hunter | 4 ms | -3 / -3 | 3 / 3 / 0 | 11.2 | -26.3 |
| Drifting maw | 7 ms | 0 / -16 | 3 / -3 / 7 | 8.0 | -26.4 |
| Pack biter | 4 ms | 2 / -3 | 3 / -6 / -1 | 8.9 | -29.3 |
| Screamer | 4 ms | 4 / -16 | 3 / -2 / -1 | 10.0 | -31.7 |
| Ambusher | 5 ms | -8 / -4 | 0 / 1 / 3 | 8.4 | -24.8 |
| Blind stalker | 4 ms | -14 / -4 | 1 / 6 / 2 | 19.0 | -31.1 |

Before the reviews, the biter's tock rang 28 dB over its neighbours at the clock's tick and the Roller opened on even
knocks at the clock's warning pitch; both are now noise (the Roller's plates a rough scrape-rattle from 1 to 3 kHz, the
biter's mark a broadband crack under 5 ms).

### The visor (`UI/DamageFeedback.cs`, rules in `UI/WindupCues.cs`)

- **Every arc follows its danger.** A damage arc keeps the way the shot came from, and a "you have been seen" arc the
  way to the gunman. Both are turned every frame as the player turns (only past a quarter of a degree, so a still view
  does not touch the canvas). Turning toward an arc brings it round to the top of the ring, which is straight ahead.
  The angle now turns smoothly between "round the compass" and "up or down the screen" as a danger passes overhead or
  underneath (`DamageCues.MarkerAngle`, between 15% and 35% of the direction along the ground), where it used to flip
  from top to bottom.
- **A creature's warning on the visor.** As a creature starts a warning it says so (`CombatWorld.CreatureWindup`, once),
  and an amber arc on the same ring as the gunman's arc points at it until its strike is over, following it as it moves.
  The amber ring means the same for both: danger from there, coming.
  - **Winding:** steady at 0.6 brightness, closing from 44 to 30 degrees as the lock comes nearer. A countdown that can
    be read with the sound off; nothing about it looks more urgent while moving is still useless.
  - **Aimed:** it snaps to 15 degrees in 0.08 s at full brightness, the same frame as the sound's mark. Half the width it
    had a moment before, so it reads without telling colours apart.
  - **Striking:** narrow and bright while the strike can still reach the player, and dimmed to 0.35 (over 0.15 s) once
    it cannot: the Screamer's last beat missed them (out of the cone or behind cover), the maw's pull no longer has them,
    the Roller's ball is no longer coming their way or has already hit (`RollerBrain.RollReaches`). It brightens again at
    once if the danger comes back. The quick strikes (claw, whip, dart, lunges) always count as reaching.
  - Then it fades over 0.35 s. A creature killed, shot out of its warning or put away mid-warning fades where it was.
  - At most three at once, the nearest: a new warning takes a free arc, then one fading out, then the arc of the
    furthest creature if it is nearer. Each warning is still heard.
- **A gunman's "seen" arc beside a creature that has taken aim is dimmed to a third**, so two amber arcs on one ring
  never run together and hide the snap.
- **Amber arcs are hidden while the game is paused and once the player is dead.** Red damage arcs fade as before.
- The visor runs late in the frame (`DefaultExecutionOrder(100)`), after the arena's controller turns the eye, so an
  arc is never a frame behind the view. Game time throughout, like the creature: Focus slows the arc with it.

## The specialist reviews and what was done

| Review | Finding | Done |
|---|---|---|
| Unity | A refused warning was silent: cooldowns of 250 to 400 ms per event meant a second Sprinter or Strider within 0.3 s made no sound | Both sounds of every warning now have a 60 ms cooldown, with 2 voices each (`WARN_GAP_MS`) |
| Unity | The mark landed on the lock only when nothing went wrong: Focus slows sounds to 0.7 against the game's 0.35, a hitstop stops the game but not a sound, and a warning called off still played its mark | The mark is its own sound, played the frame the stage turns to Aimed. `AudioEventDef.LockAt`, `AudioDirector.LockMark` and the warnings' use of `Windups.PitchFor` are gone |
| Unity | A new warning could take the arc of a creature still mid-strike, because a fading arc never counted as free | Arcs are taken free first, then fading, then the furthest creature's |
| Unity | In the arena the arcs could use last frame's view | `DefaultExecutionOrder(100)` on `DamageFeedback` |
| Unity | Arcs flipped from top to bottom while looking up steeply, now that they follow every frame | `MarkerAngle` blends between its two ways of measuring, so it never jumps there (rule test sweeps a danger over and under the player) |
| Unity | Creature arcs froze at full brightness under the pause and death pages | Amber arcs hidden while paused or dead |
| Unity | An armless Sprinter's sound still clacked at 0.26 s, a false mark | With the split it gives its mark at once, with no wind-up |
| Design | The Roller's curl and the biter's tock matched the clock's wood in pitch and rhythm | Re-voiced as noise; `qa.py` now fails any creature sound that rings by the clock |
| Design | A gunman's sighting arc swallowed the creature arc's snap | Sightings within 25 degrees of an aimed creature dimmed to 0.35 |
| Design | The flicker looked most urgent exactly when moving is useless, and five of nine creatures lock within 0.5 s (1.6 to 3 flickers) | Replaced by the steady countdown and the snap to half the width |
| Design | A full arc held 2.4 to 2.6 s after a good dodge read as "still coming" | Dims once the strike can no longer reach them |
| Design | Every other mode of every brain should read None, so ambushes keep their surprise | Rule test over every mode of every creature |
| Design | Mark at least 5 dB over the build-up, measured above 200 Hz | Rule test and `qa.py`; the lowest is now 8.0 dB (the maw) |
| Design | `creature.py` claimed every warning was mostly above 250 Hz, untrue for the maw | Claim removed; the Deck rule is now the measured margins and loudness above 200 Hz |
| Design | A warning past `PitchFor`'s clamp would lose its mark: add a check in `CreatureConfig.OnValidate` | No longer needed: no warning is sped up or slowed down |
| Design | CLAUDE.md out of date (the alien creatures bullet; a creature line in the sound families) | Both written |
| Deck | Each showing arc re-read the eye's axes | `DamageFeedback.Eye` reads them once a frame |
| Deck | Fits the budget | Rows updated in `docs/performance_budget.md` for the split (20 clips made this pass, 1.1 MiB unpacked) |

## Tests

- `WindupTests` (18 tests): every creature has a wind-up and an aim of its own, and no aim is made and left unplayed;
  every aim marks in its first 45 ms and stands at least 5 dB over its wind-up above 200 Hz, and every wind-up is heard
  (read from the WAV files); what each change of stage plays (the mark on the lock, never when called off, at once for
  a creature aimed from the start, once if a frame skips the aimed stretch); one wind-up and one mark per warning
  however many frames it lasts; the countdown from every creature's orders, and None in every other mode of every
  brain; a Strider and a Sprinter, a Sprinter with no arms, a Cave hunter and a Roller run through real warnings (the
  mark within a frame and a half of the lock, the brain following before it and not after, the roll no longer
  threatening once dodged); where a roll counts as reaching; the arc's countdown, snap, brightness, dimming, crowding,
  fade and turning; an arc following a danger over the player without jumping.
- `DamageCuesTests`: unchanged and passing with the new `MarkerAngle`.
- `dotnet test tools/rules-tests`: 3,080 passed, 1 skipped (the suite as a whole, after the reviews were applied).
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- `tools/audio/qa.py`: pass, no failed checks (8 warnings, none of them creature sounds).

## Still unverified

- Nothing here has run in Unity, and no sound here has been listened to. The designs are written to be what their
  descriptions say, and the measurements say where their weight and their marks are, not whether they sound like the
  animal or carry in a fight.
- In Focus a wind-up ends early (sounds slow to 0.7, the game to 0.35), so there is a quiet gap before the mark. The
  mark itself is on the lock.
- An aim's tail (the swing's whoosh, the Sprinter's hiss) is cut to the default dials, so a dial that changes how long
  the strike takes can leave the tail ending a little before or after the strike lands.
- Where a danger passes exactly through the line ahead and below (or behind and above) the player while 70 to 80
  degrees off level, its arc can swap from one side of the ring to the other. Some such place cannot be avoided when a
  sphere of directions is drawn on a ring; the blend makes it a single line instead of a whole band.
- The Roller's "still in its way" is a measure along its line (`RollReaches`): someone on a slope or ledge beside it
  may be judged a little wrong.

## Acceptance on the Deck (from the design review, handheld)

- After one session, testers name at least 5 of the 6 animals that used to share one sound, by sound alone.
- By a tester's third meeting with a kind, more of their sideways moves start after the mark than before it, and
  fewer strikes hit. (Needs a per-strike log: kind, on screen or not, first sideways move against the lock, hit or
  miss. Not built yet.)
- For a wind-up from behind, the first turn goes toward the arc.
- Nobody calls the visor flashy or busy, or takes an animal's arc for "I've been seen".
- The Ambusher and the Blind stalker still surprise. Ask: "Did the visor give it away before it moved?"

## Playtest protocol

1. In the practice range, on the Deck's own speakers first, then on headphones.
2. For each kind, take 5 strikes facing it, then 5 with your back to it. Note whether the mark and the arc's snap
   arrive together, and whether stepping aside on the mark beats the strike.
3. Face a gunman squad and a creature in the same direction. Note whether the two amber arcs are told apart.
4. With the sound muted, face the Sprinter (0.39 s from its mark to its bite), the Drifting maw and the Blind stalker:
   is the countdown enough on its own?
5. Ask the questions after each block, never during it.
