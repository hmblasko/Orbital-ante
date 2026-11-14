# Orbital Ante audio style bible

This is the rulebook for how the game sounds. It is short on purpose: every rule here is one that the music, the sound effects and the code in this repository already follow, and that new work must follow too.

Nothing in this document is a claim about how the audio sounds to a person. Nobody has listened to it yet (see "What has and has not been checked" at the end).

## 1. The sound in one paragraph

Convincing space hardware with a wink. Machines sound heavy, mechanical and real: relays clack, pumps whine, metal rings. On top of that sits one deep house record that changes with what you are doing: a soft, steady kick, an off-beat synth bass, minor-ninth chords through a dub echo, pads, and a sparse lead with long gaps (as of 14 Nov 2026; the first version was a funk band with brass, and was the wrong mood). The mood can go lonely and strange, and when a fight starts it goes fast and loud, but it never goes grim. If a sound could be on a film trailer about the end of the world, it is wrong for this game. If it could be in a toy advert, it is also wrong.

Three words to test any new sound against: **grounded, groovy, generous** (it rewards the player for doing things).

## 2. What it is not

- Not PS2 or retro: no bit-crushing, no chip tunes, no deliberately cheap samples.
- Not a generic orchestral score: no string ostinatos with big drums, no choir of doom.
- Not a recreation of anything: the brief's references (Outer Wilds, TF2, Balatro, the two bass tracks) are a direction of travel. No melody, riff or sound in this repository is copied from them, and none should be.
- Not noisy: silence and near-silence are part of the score (section 6).

## 3. The four themes

All four are written around the note D so any of them can follow any other. They live in one file, `tools/audio/oa_audio/motifs.py`, and the exploration, combat, flight and mystery pieces and every reward sound take their notes from there. (The main theme uses the ante cell in the minor, A C D, and the Cosmic Mystery theme's wide slow intervals, written out in its own file.)

| Theme | The idea | The notes (bar by bar) | Where it is now |
|---|---|---|---|
| **Orbital Ante** | Whimsical, adventurous. A rising three-note call (A, B, D), an answer that leans on a flattened seventh (the "wink"), the call again one step higher, then home. | `A B D` / `F# E C A` / `B D E` / `F# E D B D` | Stated in full in the flight piece. Appears in the minor in the third section of the combat piece. Its first three notes are the "ante cell". |
| **Ante cell** | The first three notes of the above: A, B, D. The game's signature. | `A B D` | The reward ladder (tier 1 is A and D together, tier 2 and the end of the score count-up are the whole cell, the bigger tiers grow out of it). |
| **Exploration** | Wonder and curiosity. An open rising fifth that settles, then a reach up that never quite resolves, like a question. | `D A` / `G` / `C B G` / `A` | The exploration piece (a far-off plucked string, at half speed). Quoted by a plucked string in the combat ending, to say "back to exploring". |
| **Conflict** | Urgency. A tight figure worrying at the home note and the note a semitone above it, a drop, and a scramble back up. | `D D Eb D C A` / `Bb F E D` / `D D Eb D C F` / `G F E D` | The lead of the combat piece. |
| **Cosmic Mystery** | Loneliness and awe. Wide, slow intervals; a sharpened fourth keeps it weightless. | `D` / `A G#` / `E` / `C# F#` | The mystery piece (soft glass tones and a pad choir). |

Rules for the themes:

1. A theme means something. Exploration plays when exploring, Conflict when fighting. Do not use one as filler.
2. The ante cell is for rewards and for the game speaking to the player. It should never be attached to bad news.
3. Themes may be changed (minor, slower, different instrument) but must stay recognisable: keep the rhythm or keep the shape.

## 4. The band

| Role | Instruments | Notes |
|---|---|---|
| Bass | A synth sub with a filtered pulse on top; a rolling growl in combat | Plays on the off-beat, around the kick, with octave jumps. The filtered layer is what a handheld's speakers will play. |
| Drums | One synthesised drum machine shared by every piece (`tools/audio/music/deep.py`) | Soft four-on-the-floor. Barely there when exploring, firmer in flight, hardest in combat. |
| Chords | Minor-ninth stabs through a slow filter sweep and a dotted echo, over pads | The warm centre, and the "existential" part: they never quite land at home. |
| Lead | A soft synth in flight, a plucked string when exploring, a hard lead in combat | Four bars of tune, then four of nothing. Bells are used sparingly (Matthew, 14 Nov 2026: "less bells"). |
| Colour | Noise swells, a ride and a squelching sixteenth-note line for the worst moments | |

Main theme, exploration, flight, combat and the transition cues are synthesised entirely in this repository's own code. All of the music is now synthesised; the sampled sound bank remains only in seven reward sounds. The sampled instruments come from one MIT-licensed sound bank (see `docs/audio/licence_manifest.md`).

## 5. Tempo and key plan

| Piece | Situation | Tempo | Key | Why |
|---|---|---|---|---|
| Regolith Lullaby | On foot, exploring | 120 | D minor | The record heard from far away: pad, a plucked theme with long gaps, then dub chords, bass and a soft kick as interest rises. |
| All In | Combat | 120 | D minor | The same record getting harder, not a different one: heavier kick, rolling bass, the Conflict theme on a hard lead. |
| Burn Window | Flying | 120 | D minor | The record with the engines lit: a turning arpeggio, the Orbital Ante theme in the minor. |
| Long Way From Anywhere | Main theme (menus) | 120 | D minor | Deep house: a steady, soft four-on-the-floor under slow minor ninths and one distant bell. Existential, not upbeat, as Matthew asked on 14 Nov 2026. Entirely synthesised. Replaced the first menu piece, a swung jazz number that was the wrong mood. |
| What the Dish Heard | Caves, seas, the unknown | 120, felt at 60 | D Lydian | Slow enough to feel like no tempo at all, but on the same grid as the rest, so it mixes in and out on a bar line. Under tension, the other pieces' kick is heard through a wall. |

One tempo for everything (and one key for everything but the mystery piece, which stays on D with its own strange harmony) is what lets the game mix from any piece into any other on a bar line, like a DJ, which is what the build prompt asks for ("blends continuously like a club DJ at a steady tempo").

**Direction from Matthew (14 Nov 2026):** the main music should be "more existential and not upbeat, but still have a good deep house beat". He heard the rewritten main theme and said "Less bells but otherwise like it". Exploration, flight and combat were then rewritten in the same mood (they had been 86 BPM vibraphone, 124 BPM D major disco and 172 BPM breakbeat with brass). His verdict on those three: "Much better". The mystery piece was then moved onto the same tempo and made fully synthesised, with its bells replaced by soft tones; he has not heard that one yet.

## 6. How the music behaves

The music follows one number, **intensity**, from 0 to 100, in five bands:

| Band | Range | What you hear |
|---|---|---|
| Solitude | 0 to 19 | The barest layers: a pad and a few plucked notes. Often nothing at all. |
| Exploration | 20 to 39 | Keys and bass join, and the tune with them. |
| Tension | 40 to 59 | Percussion, then a held, uneasy layer. |
| Combat | 60 to 79 | The combat piece takes over, with its Conflict tune from the first bar. |
| Chaos | 80 to 100 | Its extra, wilder layer comes in. |

Rules the music director keeps (the code is `MusicBrain`, and each of these has a test):

1. **Nothing changes off the grid.** Layers enter and leave on bar lines. Pieces hand over on a beat or a bar.
2. **Combat arrives with a riser** that starts on the next beat and lands on the combat piece's first downbeat, about three seconds later. An ambush (the intensity is heading straight above 85, not climbing there) skips the riser and hits on the next beat. If a fight flares up again during the quiet after the last one, combat music does not wait for that quiet to finish.
3. **Combat ends with an ending**, not a fade: a final hit at the end of a two-bar phrase, a chord that resolves, about five seconds of ring, and then the exploration piece returns from its barest layers.
4. **No flapping.** Combat music starts at 60 and will not stop until intensity has stayed under 38 for four seconds and it has played for at least 14 seconds.
5. **Firing at nothing is not a battle.** The player's own shots can raise intensity only into the Exploration band, and hits, severed parts and kills on something that is not fighting back stop in the Tension band. Being shot at or taking damage is what starts combat music. A crash is a fright, not a fight, and also stops short of it.
6. **A piece that comes back does not start over.** Each piece has two or three entry points, and takes the next one each time it returns.
7. **Music rests.** After two times round, the exploration and flight pieces fade out at the end of their loop and stay away for most of a minute. Deep space has no music at all. The silence makes the next entry matter.
8. **Rewards are heard.** The music dips under the biggest reward sounds for as long as their melody lasts.

## 7. Sound effects

**Layering.** A gunshot is not one sound. Each bullet gun is built from a transient (the crack), a body (the thump you feel), a mechanical layer (the action cycling) and a tail (the space answering), with its own recipe for all four, so they differ in more than pitch. The energy weapons are built differently (a pitched discharge, a body and a tail) and do not yet have a mechanical layer of their own; that is an open item.

**The weapons, and how to tell them apart with your eyes shut:**

| Weapon | Character |
|---|---|
| Rifle | Hard mid-range crack, short tail, loose rattle of the action. The reference "normal gun". |
| SMG | Higher, thinner, drier and much shorter: made to be heard twelve times a second. |
| Hand cannon | One huge low boom with a hollow bark in the low mids. The heaviest conventional sound in the game. |
| Sniper | A thin, very sharp supersonic crack with a long, darker tail, then a separate bolt sound. |
| Suppressed pistol | Mostly mechanism: a soft cough and the slide. |
| Laser revolver | A pitched zap with a spaghetti-western twang. The joke gun, and it sounds like one. |
| Ray gun | Wobbling, vowel-like pulp science fiction. |
| Laser shotgun | Two charged blasts with a tearing edge. |
| Gravity gun | No shot at all: a rising hum when it grabs, a held throb, a deep punt. |

**Variation.** Any sound that can repeat within a second has at least two recordings, and the same one never plays twice running. Level and pitch also move slightly each time.

**Every important sound has mid-range content.** A Steam Deck's speakers produce almost nothing below about 200 Hz. A sound that is only sub-bass does not exist for a handheld player.

**Combat feedback** is a ladder: a hit is a tick, a headshot is a bright ping, a kill is a lower, rounder confirm. Only the biggest that applies plays.

**Gore is funny.** Pops are wet, pitched and a little cartoonish, bigger part meaning lower and longer.

## 8. Rewards

Small to large: points tick, tier 1 to tier 4, multiplier, common loot, rare loot, discovery. The tiers grow out of the ante cell (A, B, D) and the home chord of D, and each step up adds notes, layers and length, so a bigger reward always sounds bigger. Discovery borrows the Cosmic Mystery theme's sharpened fourth instead: finding something strange should not sound like winning points. Score ticks climb a scale as they repeat.

Bursts are limited by rule (`RewardRules`): one reward at a time, the biggest waiting reward first, stale small ones dropped, ticks capped to about 18 a second.

Per the design constitution (principle 9, ethical design), reward sounds confirm something the player did. They are never used to nag, to count down, or to make a loss feel like a near win.

## 9. Space and acoustics

The rule for vacuum is **grounded, not silent**:

- A sound made by something you are holding or standing on reaches you through your suit: a little quieter and duller.
- A sound from somewhere else in a vacuum arrives through the ground: clearly duller and quieter, but deliberately still audible on a handheld's small speakers, because "someone is shooting at me" has to survive on an airless moon.
- Sounds inside the suit (breath, alarms, radio) and all interface sounds never change.
- Thin air (Mars) sits in between. Real Mars would be nearly silent; this is tuned for play.
- Echo needs air. Caves and interiors have reverb; open vacuum has none.

This makes a firefight on the Moon sound unmistakably different from one on Mars, using the same sound files.

## 10. Levels

| | Target |
|---|---|
| Exploration and menu music | about -17 LUFS |
| Mystery music | about -20 LUFS |
| Flight and combat music | about -15 LUFS |
| True peak of any one file | no higher than -1 dB. Music layers playing together can add up to just under full scale; the headroom below is what covers that until there is a limiter. |
| Whole-game headroom | 4 dB kept free at the output, because Unity clips instead of limiting |
| Sample rate | 48 kHz everywhere |

Mix buses: Music, Weapons, CombatImpacts, Engines, Environment, PlayerEquipment, UI, Dialogue. The player has four volume settings: master, music, effects, and radio and voices.

## 11. What has and has not been checked

- **Checked by measurement:** loudness, peaks, clipping, loop points, frequency balance, the timing of every music change, and that every weapon measures differently from every other. See `docs/audio/qa_report.md`.
- **Not checked at all:** whether any of it sounds good. The audio was written and mixed by meter, by a system that cannot hear. The first human listen is Matthew's, and section 7's "how to tell them apart" table is a design intention until he confirms it.
