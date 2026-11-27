# Audio QA report

Written by `tools/audio/qa.py`. Every number here was measured from the files on disk. **None of it is a listening result**: these checks can prove a file is clean, level, loops exactly and measures differently from its neighbours. They cannot say it sounds good.

**Result: PASS** (0 failed checks, 8 warnings).

## Warnings
- Audio/Sfx/combat/fx_slowmo_in: 86% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/melee/melee_butterfly_knife_takedown: 100% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/melee/melee_hit_wall_01: 100% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/melee/melee_laser_sword_ready: 86% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/player/player_land_01: 89% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/player/player_land_02: 87% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/ship/ship_engine_ignite_01: 90% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/ship/ship_engine_ignite_02: 91% of its energy is under 250 Hz, so little of it would reach Deck speakers

## Sound effects

141 events, 237 files, all 48 kHz.

- Highest true peak in any file: -1.0 dB.
- Clipped samples: 0 files affected.
- Loudness of one-shots as played (file loudness plus the event's volume): -41 to -14 LUFS.

## Weapon distinctiveness

Each shot is reduced to twelve numbers (length, tail decay, brightness, how pitched it is, energy in eight bands). The table is the distance between weapons; the last column is the largest distance between two recordings of the *same* weapon. A weapon is distinct when every other weapon is further away than its own recordings are from each other.

| | rifle | smg | handcannon | sniper | suppressed | laser_revolver | raygun | laser_shotgun | own spread |
|---|---|---|---|---|---|---|---|---|---|
| **rifle** | - | 4.9 | 3.6 | 2.1 | 7.5 | 3.4 | 2.4 | 2.5 | 1.2 |
| **smg** | 4.9 | - | 7.9 | 5.8 | 4.1 | 4.3 | 4.9 | 6.7 | 1.7 |
| **handcannon** | 3.6 | 7.9 | - | 4.1 | 10.1 | 6.1 | 5.2 | 3.2 | 1.6 |
| **sniper** | 2.1 | 5.8 | 4.1 | - | 8.0 | 4.0 | 3.0 | 2.4 | 1.2 |
| **suppressed** | 7.5 | 4.1 | 10.1 | 8.0 | - | 6.5 | 6.7 | 8.5 | 1.5 |
| **laser_revolver** | 3.4 | 4.3 | 6.1 | 4.0 | 6.5 | - | 3.7 | 4.2 | 0.7 |
| **raygun** | 2.4 | 4.9 | 5.2 | 3.0 | 6.7 | 3.7 | - | 3.5 | 1.1 |
| **laser_shotgun** | 2.5 | 6.7 | 3.2 | 2.4 | 8.5 | 4.2 | 3.5 | - | 0.7 |

## Creature warnings and the clock

Each creature warns with two sounds: its wind-up, then its aim (one hard mark, played when it takes aim). *Ring* is how far a sound stands, in dB, over its neighbouring pitches two semitones either side of the clock's tick (979 and 1629 Hz) or of its warning's knocks (640 Hz); the clock's own tick stands 37 to 55 dB over. Allowed: under 10. *Mark over wind-up* compares the loudest 5 ms of each above 200 Hz, as played (at least 5 dB). *Wind-up heard* is the wind-up's loudest 100 ms above 200 Hz, so a quiet one is not lost on the Deck's speakers (at least -35 dB).

| Creature | Aim's mark at | Mark rings at 979 / 1629 Hz | Wind-up rings at 979 / 1629 / 640 Hz | Mark over wind-up | Wind-up heard |
|---|---|---|---|---|---|
| strider | 6 ms | -32 / 1 | -3 / -5 / -2 | 10.1 dB | -26.8 dB |
| sprinter | 4 ms | -1 / -4 | -1 / -10 / -1 | 12.2 dB | -28.7 dB |
| roller | 8 ms | -8 / 5 | 6 / 2 / 2 | 12.4 dB | -31.0 dB |
| hunter | 4 ms | -3 / -3 | 3 / 3 / 0 | 11.2 dB | -26.3 dB |
| maw | 7 ms | 0 / -16 | 3 / -3 / 7 | 8.0 dB | -26.4 dB |
| biter | 4 ms | 2 / -3 | 3 / -6 / -1 | 8.9 dB | -29.3 dB |
| screamer | 4 ms | 4 / -16 | 3 / -2 / -1 | 10.0 dB | -31.7 dB |
| ambusher | 5 ms | -8 / -4 | 0 / 1 / 3 | 8.4 dB | -24.8 dB |
| stalker | 4 ms | -14 / -4 | 1 / 6 / 2 | 19.0 dB | -31.1 dB |

## Music

| Piece | Tempo | Bars | Length | Stems | Full mix | True peak | Under 250 Hz | Above 2 kHz | Loop exactness |
|---|---|---|---|---|---|---|---|---|---|
| All In | 126 | 64 | 121.9 s | 5 | -15.1 LUFS | -1.3 dB | 46% | 8% | -136.5 dB |
| Regolith Lullaby | 126 | 64 | 121.9 s | 6 | -17.5 LUFS | -1.5 dB | 55% | 5% | -138.7 dB |
| Burn Window | 126 | 64 | 121.9 s | 5 | -15.6 LUFS | -1.3 dB | 59% | 11% | -138.1 dB |
| Long Way From Anywhere | 126 | 64 | 121.9 s | 5 | -16.5 LUFS | -1.3 dB | 49% | 8% | -136.0 dB |
| What the Dish Heard | 126 | 48 | 91.4 s | 4 | -20.5 LUFS | -7.1 dB | 44% | 5% | -214.2 dB |

Loop exactness: each piece is rendered as three joined copies and the third is compared with the second. Below -100 dB means going round the loop is, sample for sample, the same as carrying on. Measured before Ogg encoding.

| Cue | Length | Lead-in | Loudness | True peak |
|---|---|---|---|---|
| hit_combat | 3.0 s | 0.00 s | -14.1 LUFS | -1.9 dB |
| resolve_combat | 7.5 s | 0.00 s | -14.2 LUFS | -3.0 dB |
| to_calm | 5.0 s | 0.00 s | -17.4 LUFS | -7.8 dB |
| to_combat | 5.1 s | 1.90 s | -13.2 LUFS | -1.2 dB |
| to_flight | 6.4 s | 3.81 s | -12.8 LUFS | -1.8 dB |

## The five-minute showcase (offline preview of the game's own decisions)

Whole run -23.51 LUFS. Highest peak of everything added together, with no limiter: **-4.53 dBFS**; samples over full scale: **0**.

| Section | Starts | Loudness | Peak | Music alone |
|---|---|---|---|---|
| Exploration | 0:00 | -25.7 LUFS | -7.4 dB | -26.4 LUFS |
| Discovery | 0:55 | -28.2 LUFS | -11.2 dB | -29.2 LUFS |
| Rising Tension | 1:38 | -24.5 LUFS | -8.3 dB | -25.0 LUFS |
| Combat | 2:13 | -20.4 LUFS | -4.7 dB | -23.0 LUFS |
| Chaos | 2:32 | -20.8 LUFS | -4.5 dB | -23.1 LUFS |
| Loot And Scoring | 3:10 | -24.2 LUFS | -10.5 dB | -27.5 LUFS |
| Take-Off | 3:36 | -22.6 LUFS | -7.0 dB | -24.7 LUFS |
| Return To Calm | 4:35 | -27.2 LUFS | -9.7 dB | -27.3 LUFS |

