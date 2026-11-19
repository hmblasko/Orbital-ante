# Orbital Ante audio vertical slice: independent critic report

**Verdict: 6.1 / 10. It does not pass (pass mark 8.5).** The files are technically clean and the adaptive system is mostly sound, but the music is rendered through a General MIDI soundfont with no velocity layers, and several important sounds are lost on small speakers or under the mix.

Nothing here was listened to. **M** = I measured it (audio files, or the game's own C# rules run in my own simulator). **I** = inferred from source.

**The target moved during the review.** Assets are as on disk at 14:40 (music re-render finished 14:31). The maker kept editing source until 14:36:
- `sfx/weapons.py` now redesigns the sniper, but the sniper wavs are still the 13:13 renders. Source and assets disagree; my sniper numbers describe the old files.
- Two problems I found early were fixed in source mid-review and are not listed as issues: UI one-shots sharing one source's pitch, and flight/surface context flapping at 400 m (the hook now uses 350/500 m plus a 3 s wait).
- `tools/audio/build/demo/schedule.json` predates the current `MusicBrain.cs`; I regenerated the schedule with my own build of the rules.

## Scorecard

| Area | Score | Basis | Why |
|---|---|---|---|
| Technical cleanliness | 7.5 | M | 130 SFX files all 48 kHz, true peak at or under -1.0 dB, no DC, no end clicks, loop seams clean, music lengths exact to under 1 sample. Summed stems exceed the -1 dB rule; no output limiter. |
| Loudness and mix balance | 6 | M | Music hits its LUFS targets. Hit marker, flesh impact and ambiences sit far under the mix; ray gun is louder than the hand cannon; flight mix is 56-68% sub-200 Hz. |
| Small-speaker translation | 5.5 | M | Leads, keys and most weapons survive. Bass stems lose 4-5 LU, three stems and two ambiences mostly vanish, enemy fire in vacuum drops about 30 dB. |
| Weapon distinctiveness | 6.5 | M | Five clear clusters. Rifle, hand cannon and (old) sniper are one family; SMG and suppressed pistol are close. |
| Sound-effect design depth | 6 | I | Real layering in the gunshot recipe, but everything is filtered noise plus sines; metal is 3-5 partials. |
| Composition | 6.5 | I | Themes are coherent and placed where claimed; harmony is competent. Bass is three templates; melody is absent at common intensities. Memorability cannot be judged. |
| Production realism | 4 | M+I | FluidR3 General MIDI for nearly every "real" instrument, with measured zero timbre change by velocity on bass, Rhodes, guitar and choir. This is the owner's "cheap" complaint. |
| Adaptive behaviour | 6.5 | M (sim) | On-grid, no flapping at thresholds, rests work. Ambush path unreachable, 12 s latency on re-engagement, combat music from a crash or target practice. |
| Variation and repetition | 6 | M+I | Most SFX variants differ properly. Ray gun and gravity-grab variants are near-identical; pieces always restart at bar 1. |
| Fit to style bible and brief | 6 | I | Structure matches. Several bible claims are not true in the build (bass slides, ambush hit, four layers on every weapon, -1 dB peak). |

## Ranked issues

### Blockers

**1. The band is a General MIDI soundfont with no dynamics. (M + I)**
- Source: `tools/audio/oa_audio/sampler.py` renders FluidR3_GM with reverb and chorus off, no controllers, no pitch bend.
- I rendered single notes at velocity 60 and 115. Slap bass, finger bass, Rhodes, clean guitar and choir change level by 11.3 dB and nothing else (spectral-shape difference 0.0 dB). The same note rendered twice is bit-identical.
- So every bass "ghost note" is the accent sample turned down, and combat's 16 bars of eighth-note slap bass (`RIFF_DRIVE`) retrigger one sample about 5.7 times a second.
- GM strings take 252 ms to reach 70% level, but the flight "top" stem and the `to_flight` cue play runs at 121 ms per note. GM brass takes 103 ms; combat stabs last 105 ms.
- Most exposed: `combat_all_in/bass`, `stabs`, `drums` (GM power kit); `flight_burn_window/bass`, `top`, lead vibes; `menu_house_orbits/rhythm`, `melody`; `mystery_dish/choir`.
- Fix: replace the bass first with a multi-velocity, round-robin sampled bass with slides and dead notes, then drums and brass. If it must stay synthetic, drop GM brass, strings and choir for the repo's own synth voices, which are at least not recognisably MIDI.

**2. Enemy gunfire nearly disappears in vacuum on Deck speakers. (M)**
- `AcousticsModel.cs` gives air-borne sounds a 320 Hz low-pass and -16 dB at zero air.
- Rifle shot, in-game level through a 200 Hz high-pass: -15.8 dB in air, -45.3 dB in vacuum, -34.1 dB at Mars air (0.01). Music sits around -19 to -24 LUFS.
- Most bodies in the game are airless, so "being shot at" would be inaudible on the handheld.
- Fix: for the `air` policy at zero air use about 900 Hz and -9 dB, and add a suit-borne mid layer (a 500-1500 Hz knock) for nearby shots and impacts.

### Major

**3. The hit marker is at risk of being masked. (M)**
- In-game level -34 dB, a 70 ms tone at 1734 Hz.
- In its own band it measures -34.8 dBFS. The player's rifle is still at -27.4 in that band 50-120 ms after the shot, the ray gun -20.0, the laser shotgun -18.7; combat music is at -35.4 median.
- It is 14 LU quieter than the kill confirm (-19.6).
- Fix: `hit.marker` volumeDb -13 to about -7, move it to 2.5-3.5 kHz, and add a 1-2 ms click.

**4. The "funky bass" mostly lives below 200 Hz. (M)**
- Energy under 200 Hz: combat bass 81%, flight bass 68%, menu rhythm 82%.
- Loudness lost through a 200 Hz high-pass: 5.4, 4.0 and 3.9 LU. The bass stem is also 6.3 LU under the full mix in both combat and flight.
- Flight drums are 85% sub-200 Hz and lose 4.3 LU; the claps and hats sit 14-16 dB under the kick.
- Fix: add a 700-1500 Hz growl or pick layer to the bass (not just the +3 dB at 900 Hz), raise bass 2 LU, raise drum tops 4-6 dB in `drum_fx`.

**5. Re-engagement after a fight gets no music for about 12 s. (M, sim)**
- Enemy fire resumes 1 s after the ending cue: the riser starts 9.6 s later and combat lands 12.4 s after the fire resumed. Meanwhile the calm chord rings and exploration fades in.
- Cause: `_busyUntil = enter + 4 s` after a 5.5 s hush (`MusicBrain.Change`).
- Fix: let a combat request break the busy window during a hush; cancel the pending start and go straight to the riser or hit.

**6. The ambush hit can never play. (M, sim)**
- Intensity rises at 45 per second and the brain acts the moment it crosses 60, so it is never at 85 when the decision is made.
- Threat 24 to 95: the riser plays and combat lands 4.35 s later. Style bible section 6 rule 2 is false in practice.
- Fix: decide from the target (`max(heat, threat)`) or wait about 0.3 s before choosing the cue.

**7. Combat music starts from things that are not fights. (M, sim)**
- Three seconds of rifle fire with 60% hits on a passive target: riser, 14 s of "All In", ending cue, then a 5.5 s hush.
- Two `Crash` events 0.5 s apart while flying: the same. Nothing reports `Crash` yet, so this is latent.
- `EnemyShotNearby` is any shot not from the player, at any distance (`WeaponAudio.cs` line 51).
- Fix: cap `HitDealt`, `PartSevered` and `Kill` at 55 unless a hostile is aware; cap `Crash` at 55; weight enemy shots by distance.

**8. No limiter, and the 3 dB headroom is thin. (M + I)**
- Summed combat stems reach -0.3 dB true peak at intensity 75 (full mix -0.6), over the bible's -1 dB rule.
- Hand cannon, sniper and laser shotgun are peak -1 dBFS at volumeDb 0. Two coinciding over music exceed full scale even after -3 dB, and Unity hard-clips.
- The maker's own showcase peaked at -0.42 dBFS before headroom.
- Fix: put a limiter on the master mixer group, or drop the big guns to -3 dB and the headroom to -5 dB.

**9. The melody is missing at the intensities players will mostly sit at. (I + M)**
- Flight lead needs intensity 36, which is engine above 0.44; coasting gives 20, so chords and bass only. That is the same 8-bar progression back to back (15.5 s), a 2-bar bass cell every 3.9 s, and a -19.2 LUFS mix with 1% of its energy above 2 kHz.
- The Conflict theme needs 68; fights at 60-67 never state it, and bars 1-8 have no lead at all.
- Fix: flight lead lo 36 to 22; combat lead lo 68 to 60; write a lead for combat bars 1-8.

**10. Every piece always restarts at bar 1. (I)**
- `entryBars` is in the manifest but no C# reads it. After every fight, rest or landing the same opening returns.
- Fix: start at a seeded choice of `entryBars`.

**11. Rifle, hand cannon and sniper are one recipe. (M)**
- Log-spectrum correlation 0.96-0.98 between the three.
- Band-envelope difference on a Deck-filtered signal: rifle to hand cannon 5.2 dB, hand cannon to sniper 5.3 dB, against 2.9-3.4 dB between variants of one gun. They differ mainly in tail length (0.8, 1.4, 2.2 s).
- SMG to suppressed pistol is 5.2 dB (4.0 dB in vacuum).
- The 14:36 sniper edit addresses the sniper but is not rendered.
- Fix: rebuild and re-measure; give the hand cannon a tonal signature (a 300-500 Hz barrel ring) rather than only "longer and lower".

**12. Weapon loudness order contradicts the design. (M)**
- In-game 100 ms loudness: gravity punt -7.2, laser shotgun -8.4, ray gun -9.4, hand cannon -11.5, sniper -11.9, laser revolver -12.9, rifle -13.4, SMG -18.4, suppressed -24.1.
- The "cartoon pyoo" is 2 LU louder than the "heaviest" gun and 4 LU louder than the rifle.
- Fix: ray gun volumeDb -3 to -6, punt -1 to -4.

**13. Exploration mix is dark by the pipeline's own rule. (M)**
- 4-5% of energy above 2 kHz with keys in; `tilt()` in `music/common.py` treats under 9% as "in the next room". The keys stem has 1% above 2 kHz.
- Fix: let the tilt go past +5 dB here, or brighten the vibes and Rhodes at source.

### Minor

**14. Stems and beds that vanish on small speakers. (M)**
- Loss through a 200 Hz high-pass: mystery `pulse` -5.9 LU (94% sub-200 Hz), explore `perc` -4.0 (86%), `amb.cave` -13.9 (99%), `amb.facility` -9.9, `weapon.gravity.grab` -9.4, `ship.cabin.loop` -5.6.
- Fix: add 300-800 Hz content to each (the heartbeat's "knock", the cave's air).

**15. Ambiences sit 20+ LU under the music. (M)**
- In-game about -42 to -51 dB; `amb.space.suit` is -50.7, so the "deliberate silence" bed is effectively digital silence.
- Fix: raise the beds 8-10 dB when no music is playing.

**16. Some variants do not vary. (M)**
- Waveform cross-correlation between variants: ray gun 0.98, gravity grab 1.00, laser revolver 0.86.
- Rifle variant 01 is the odd one out: 49% sub-200 Hz against 32-33%, and 2.7 LU quieter than variant 03.
- Fix: randomise sweep time and wobble rate per variant; re-seed rifle 01.

**17. First frame of a zero-fade start is silent. (I)**
- `AdaptiveMusicPlayer` sets volume to 0 at schedule time and raises it on the first frame after the start, 16-40 ms late. The combat downbeat's own kick is shaved; the cue's hit covers it.
- Fix: set the volumes of "on" stems at schedule time when `Seconds == 0`.

**18. Combat ends from any bar. (I)**
- A 0.12 s cut into a D minor hit, even mid-phrase or over Ebmaj7#11. Baked reverb tails are cut too.
- Fix: quantize to 2 bars, or supply a second ending.

**19. Riser tempo clash from flight. (I, sim timing)**
- The 172 BPM snare roll runs 2.8 s over the 124 BPM flight drums as they fade.
- Fix: a drumless riser variant, or cut the old drums stem in one beat.

**20. Big rewards overlap. (I)**
- Gaps are 0.9-1.2 s but tier 4, discovery and rare loot run 4.6, 4.2 and 3.2 s. Discovery's G# lands over tier 4's D major. The music duck lasts 0.9 s.
- Tier 2 peaks 0.8 LU under tier 1.
- Fix: gap of at least 60% of clip length for the big three; duck for the clip length.

**21. Energy weapons lack the mechanical layer. (I)**
- Ray gun and laser shotgun have none, against the bible's "every weapon has all four".
- All metal comes from four shared frequency lists of 4-5 partials.

**22. Style bible inaccuracies. (I)**
- Bass "slides and ghost notes": there is no pitch bend, and ghosts are quiet copies.
- The menu piece does not import `motifs.py`; its theme is retyped.

**23. Encoding choices. (I)**
- Music is Vorbis twice (Ogg q6, then Unity 0.7). Reward bells are stored as ADPCM.
- Fix: ship stems as WAV into Unity; use Vorbis or PCM for `Sfx/rewards`.

## What is good and should not change

- **File hygiene (M).** Every SFX file is 48 kHz, at or under -1.0 dB true peak, DC under 0.0002, and silent at the end. Loop seams show no step or kink above normal signal slope (worst ratio 1.04). All five pieces are within 0.5 sample of bars times bar length, and tempos measure exactly as stated.
- **Music loudness targets (M).** -14.8, -17.0, -15.0, -17.5 and -20.5 LUFS against -15, -17, -15, -17 and -20. Stems sum without clipping and mono-sum loss is 0.4 dB or less on full mixes.
- **Director fundamentals (M, sim).** No flapping with threat oscillating 37/43 or 57/63. Fades are honoured (6 s and 4 s). The riser lands on the combat downbeat. The rest happens at exactly two loops and returns after 50 s. Player shots alone cap at 34.
- **The 86/172 tempo relationship and the D-centred key plan.**
- **Variation done well (M).** Rifle, SMG, footsteps, ricochets and metal impacts all have variant cross-correlation under 0.35.
- **Clear outliers (M).** SMG, suppressed pistol, laser revolver and ray gun are each far from everything else.
- **Reward tier energy rises monotonically (M).** In-game exposure -35.2, -30.9, -20.6, -14.5 dB for tiers 1 to 4.
- **The harmony writing and the Orbital Ante theme's shape.** I found no quotation of a known tune.

## Listening questions for the owner, most important first

1. Solo `combat_all_in/bass` and `flight_burn_window/bass`. Does it sound like a bass player, or a keyboard "slap bass" preset?
2. Combat stabs and the flight string runs: real section, or MIDI file?
3. Fire the rifle, hand cannon and sniper. Are they real guns, or shaped noise bursts? Can you tell hand cannon from sniper blind?
4. On the Deck's own speakers, in combat: can you hear each hit marker while holding the trigger? Can you follow the bass line at all?
5. Does the combat ending feel like an ending when it lands mid-phrase?
6. After 4 minutes of coasting flight (chords and bass only), is it annoying?
7. Exploration theme (D up to A, down to G, slowly): does it remind you of Star Wars or Outer Wilds? The opening rising fifth and step down is a very common gesture; I cannot rule out a resemblance by ear.
8. Flight theme bars 2 and 6: C natural over an A13 chord. Bluesy wink, or wrong note?
9. Suit breath: a fixed 4.0 s noise loop. Calm astronaut, or Darth Vader?
10. Regolith footsteps are 80% above 2 kHz. Boot on dust, or "tss"?

## Note

This is the critic's first report, kept as written (13 Nov 2026). The critic's own measuring scripts were working files and are not in the repository. What was done about each issue afterwards is in `docs/reports/audio-slice.md`.
