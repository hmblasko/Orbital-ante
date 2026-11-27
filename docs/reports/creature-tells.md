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
| Hypothesis | If every creature's warning has its own sound with one hard mark where it takes aim, and the visor shows its wind-up in the same three stages as the sound, a player learns one rule for every creature ("move after the mark") and can find a warning they cannot see. |
| Principle | Learnable challenge, never hidden rules (3); legible risk and reward (5); managing cognitive load (7). The critique loop's sound rules: warnings are dry and never musical, and carry above 200 Hz for the Deck's speakers. |
| Smallest test | Rule tests on the sounds (each has its own file and its mark at the creature's default lock; the loudest moment is the mark), the stage of every creature read from its brain's orders, three creatures run through a real warning, the arc's look and the speed rule (`WindupTests`). |
| Acceptance | Each warning sound is used by one creature only. Its loudest 5 ms falls within 10 ms before to 45 ms after its lock at the default dials, and the wind-up before the mark is heard. A run of the real brain turns the arc from Winding to Aimed within a frame and a half of the lock, and never steps back. Every arc on the visor points at its danger after the player turns. |
| Playtest protocol | See the end of this report. |

### One rule for every creature

Every creature that strikes warns in three stages (`Creatures/Windups.cs`, plain C#), read from its own brain:

- **Winding.** It winds up and still turns to follow the player. Moving now does not help yet.
- **Aimed.** It has fixed where the strike will land. A step aside now is a miss.
- **Striking.** The roll, scream, draw-in, dart or lunge, until it is over.

The stage comes from what each brain already tests for aiming: a walker's claw at `WalkerBrain.LockShare` of its
strike, the Roller, Cave hunter, Screamer and Ambusher at their `LockAt` share of the warning, the Drifting maw and Pack
biter by the `Locked` they report, and the Blind stalker at `StalkerBrain.LockAt`. Nothing about any creature's fight
changed.

### Their own sounds (`tools/audio/sfx/creature.py`)

Each warning has one hard mark at the moment it takes aim. The mark's time is the event's `lockAt` in the manifest
(read into `AudioEventDef.LockAt`), and a rule test holds it to the creature's default lock time. If a dial changes how
long a warning lasts, `Windups.PitchFor` plays the sound faster or slower (0.8 to 1.25 times) so the mark still lands
on the lock.

| Sound | Length | The mark | What it is | Loudness | Centroid | Under 250 Hz |
|---|---|---|---|---|---|---|
| `creature.strider.raise` | 1.0 s | 0.40 s, double clack | Claw arm creaking up joint by joint, heavy shell on shell; the clack as the claw sets open; a heavy swing whoosh that ends where the claw lands (0.99 s) | -26.6 LUFS | 760 Hz | 2% |
| `creature.sprinter.raise` | 0.68 s | 0.26 s, snick | Dry fast chitter of jaws rubbed together (a camel spider's sound), rising; the jaws cocking; a hiss into the lunge | -24.3 | 3,530 | 0% |
| `creature.roller.curl` | 0.9 s | 0.54 s, clunk | Plates folding over one another, clacks coming quicker and quicker; the ball snapping shut; grit crunching as it sets off | -24.1 | 555 | 49% |
| `creature.hunter.rear` | 0.95 s | 0.49 s, fang click | Many legs skittering under a tightening hiss; the fangs flying wide; the whip of its head coming down (0.75 to 0.95 s) | -22.5 | 3,030 | 0% |
| `creature.maw.gape` | 1.4 s | 0.84 s, thoom | Heard through water: a jaw creaking wider, bubbles getting away; a deep hollow thoom as the jaw drops; the water starting to move | -22.9 | 315 | 59% |
| `creature.biter.mark` | 0.9 s | 0.54 s, tock | Teeth chattering as the jaws flare, a swish as it rolls its flank to the light; the jaws setting and the fins sweeping back; a thin held breath before the dart | -22.9 | 1,700 | 0% |
| `creature.maw.inhale` | 2.6 s | | The draw-in: a roaring rush of water that swells and climbs, so its rise tells how long is left; played to last as long as the draw-in does | -20.7 | 591 | 36% |
| `creature.maw.choke` | 0.75 s | | Two hard gags and a burst of bubbles (a shot down its open throat) | -18.6 | 974 | 13% |
| `creature.biter.dart` | 0.36 s | | A rip through the water and the jaws snapping shut as it passes | | 1,710 | 0% |

The Screamer, Ambusher and Blind stalker already had their warnings; they now carry their `lockAt` too (0.60, 0.495 and
0.45 s). Two older sounds were nearly all below what the Deck's speakers play: the Ambusher coming up (95% of its weight
under 250 Hz) and the Blind stalker's lunge (91%). The Ambusher's rise is its tell ("the heave comes first on purpose,
so there is time to look down"), so it now has the ground crackling as it strains (47% under 250 Hz), and the lunge's
thump was turned down under its clap (59%). Every warning was checked by measurement for where its loudest moment
falls: each is at its mark (the maw's thoom peaks 27 ms after it starts, the Screamer's double click on its second
click). `tools/audio/qa.py` passes with no failures.

The stand-ins are gone: no creature borrows the Scout's flare, the Heavy's spin-up or a splat for its warning any
more. A warning that fails to play (on cooldown, out of voices) is not replaced by another warning's sound.

### The visor (`UI/DamageFeedback.cs`, rules in `UI/WindupCues.cs`)

- **Every arc follows its danger.** A damage arc keeps the way the shot came from, and a "you have been seen" arc the
  way to the gunman. Both are turned every frame as the player turns (only past a quarter of a degree, so a still view
  does not touch the canvas). Turning toward an arc brings it round to the top of the ring, which is straight ahead.
- **A creature's wind-up on the visor.** As a creature starts a warning it says so (`CombatWorld.CreatureWindup`, once),
  and an amber arc on the same ring as the gunman's arc points at it until the warning is over, following it as it
  moves. The amber ring means the same thing for both: danger from there, coming.
  - **Winding:** 40 degrees wide, flickering six times a second.
  - **Aimed:** it snaps to 18 degrees in 0.08 s and holds steady at full brightness, at the same moment as the sound's
    mark. The change is one of shape, not only of brightness, so it reads without telling colours apart.
  - **Striking:** steady and narrow through the strike.
  - Then it fades over 0.35 s. A creature killed, shot out of its warning or put away mid-warning fades where it was.
  - At most three at once. A pack of biters takes turns, so three is more than a fight should need.
- Game time throughout, like the creature: Focus slows the arc with it.

## Tests

- `WindupTests` (new, 10 tests):
  - Every creature's warning has its own sound file with its mark at its default lock, and each sound's loudest moment
    is its mark with an audible wind-up before it (read from the WAV files).
  - The speed rule: 1 at the default dials, a Strider tuned to a 1.4 s strike takes aim at 0.35 s and its clack is
    played to land there, never past 0.8 or 1.25.
  - Every creature's stage from its brain's orders, including a Sprinter with no arms (aimed from the start), a biter
    shot out of its mark and an Ambusher calling its strike off.
  - Strider, Sprinter, Cave hunter and Roller run through a real warning: Winding then Aimed at the lock, within a
    frame and a half, then the strike, never a step back.
  - The arc's flicker, snap, fade and turn threshold, and an arc staying on its danger as the player turns.
- `dotnet test tools/rules-tests`: see the log for the run.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- `tools/audio/qa.py`: pass, no failed checks.

## Still unverified

- Nothing here has run in Unity. Whether the arcs read in a fight, whether six flickers a second is right, and the
  sounds in the mix all need the Deck.
- No sound here has been listened to. The designs are written to be what their docstrings say, and the measurements
  say where their weight and their marks are, not whether they sound like the animal.
- Slow motion (Focus) lowers world sounds by its own amount, which is not quite the game's slow-down, so in Focus a
  mark can land a little off the lock. The arc is on game time and is right.
- A Sprinter with both arms shot off bites where the player stood when it began (aimed from the start): its arc is
  narrow at once, but its sound still clacks at the usual time.

## Playtest protocol

1. In the practice range, stand still in front of each creature in turn and let it strike. Note whether the mark in its
   sound and the arc's snap arrive together, and whether stepping aside on the mark beats the strike.
2. Turn your back on a Strider and a Blind stalker. Note whether the sound alone says which animal it is, and whether
   turning toward the arc finds it.
3. In a fight with gunmen and creatures together, note whether the two kinds of amber arc are told apart (the
   creature's flickers and snaps narrow; the gunman's pulses and fades).
4. Note any discomfort from the flicker.
