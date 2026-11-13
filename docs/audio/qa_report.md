# Audio QA report

Written by `tools/audio/qa.py`. Every number here was measured from the files on disk. **None of it is a listening result**: these checks can prove a file is clean, level, loops exactly and measures differently from its neighbours. They cannot say it sounds good.

**Result: PASS** (0 failed checks, 4 warnings).

## Warnings
- Audio/Sfx/player/player_land_01: 89% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/player/player_land_02: 87% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/ship/ship_engine_ignite_01: 90% of its energy is under 250 Hz, so little of it would reach Deck speakers
- Audio/Sfx/ship/ship_engine_ignite_02: 91% of its energy is under 250 Hz, so little of it would reach Deck speakers

## Sound effects

68 events, 130 files, all 48 kHz.

- Highest true peak in any file: -1.0 dB.
- Clipped samples: 0 files affected.
- Loudness of one-shots as played (file loudness plus the event's volume): -37 to -15 LUFS.

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

## Music

| Piece | Tempo | Bars | Length | Stems | Full mix | True peak | Under 250 Hz | Above 2 kHz | Loop exactness |
|---|---|---|---|---|---|---|---|---|---|
| All In | 172 | 64 | 89.3 s | 5 | -14.6 LUFS | -0.7 dB | 47% | 17% | -240.0 dB |
| Regolith Lullaby | 86 | 48 | 134.0 s | 6 | -17.0 LUFS | -1.4 dB | 46% | 6% | -212.2 dB |
| Burn Window | 124 | 64 | 123.9 s | 5 | -15.0 LUFS | -1.5 dB | 49% | 16% | -212.1 dB |
| The House Always Orbits | 104 | 40 | 92.3 s | 4 | -17.5 LUFS | -1.9 dB | 35% | 11% | -218.8 dB |
| What the Dish Heard | 60 | 24 | 96.0 s | 4 | -20.5 LUFS | -6.8 dB | 44% | 8% | -215.7 dB |

Loop exactness: each piece is rendered as three joined copies and the third is compared with the second. Below -100 dB means going round the loop is, sample for sample, the same as carrying on. Measured before Ogg encoding.

| Cue | Length | Lead-in | Loudness | True peak |
|---|---|---|---|---|
| hit_combat | 3.0 s | 0.00 s | -12.9 LUFS | -1.8 dB |
| resolve_combat | 7.5 s | 0.00 s | -12.0 LUFS | -2.8 dB |
| to_calm | 5.0 s | 0.00 s | -16.3 LUFS | -7.6 dB |
| to_combat | 6.0 s | 2.79 s | -12.2 LUFS | -0.8 dB |
| to_flight | 6.5 s | 3.87 s | -12.7 LUFS | -1.9 dB |

## The five-minute showcase (offline preview of the game's own decisions)

Whole run -23.73 LUFS. Highest peak of everything added together, with no limiter: **-4.08 dBFS**; samples over full scale: **0**.

| Section | Starts | Loudness | Peak | Music alone |
|---|---|---|---|---|
| Exploration | 0:00 | -27.1 LUFS | -10.7 dB | -28.2 LUFS |
| Discovery | 0:55 | -28.2 LUFS | -11.8 dB | -29.2 LUFS |
| Rising Tension | 1:38 | -25.7 LUFS | -10.7 dB | -26.2 LUFS |
| Combat | 2:13 | -20.7 LUFS | -4.1 dB | -23.4 LUFS |
| Chaos | 2:32 | -21.1 LUFS | -4.3 dB | -23.7 LUFS |
| Loot And Scoring | 3:10 | -22.9 LUFS | -9.9 dB | -24.5 LUFS |
| Take-Off | 3:36 | -22.2 LUFS | -6.7 dB | -23.3 LUFS |
| Return To Calm | 4:35 | -27.7 LUFS | -11.5 dB | -27.8 LUFS |

