# Orbital Ante audio slice: second critic report

**Verdict: 6.7 / 10 (was 6.1). It does not pass (pass mark 8.5).**

Nothing was listened to. **M** = measured by me (files, or the game's C# rules in my own simulator). **I** = inferred from source. Assets and source were stable this time, and the shipped `schedule.json` matches the current rules byte for byte.

## Scorecard

| Area | Old | New | Basis | Why |
|---|---|---|---|---|
| Technical cleanliness | 7.5 | 7.5 | M | 130 SFX files still clean (max true peak -1.00 dB). Summed combat stems now -0.01 dB; still no limiter. |
| Loudness and mix balance | 6 | 7 | M | Hit marker +6 dB, beds +5 to +9 dB. Sniper now quieter than the rifle; whole game is quiet. |
| Small-speaker translation | 5.5 | 6.5 | M | Vacuum gunfire +14 dB; flight bass better. Combat bass and the beds barely moved. |
| Weapon distinctiveness | 6.5 | 7.5 | M | Sniper is now its own sound. Rifle/hand cannon and SMG/suppressed still close. |
| Sound-effect design depth | 6 | 6 | I | Same recipe. |
| Composition | 6.5 | 6.5 | M+I | Thresholds moved, but the melody is still absent where it was missed. |
| Production realism | 4 | 4.5 | M | Same sound bank, one recording per note, now filtered three ways. Drums untouched. |
| Adaptive behaviour | 6.5 | 7.5 | M (sim) | Ambush, re-engagement, false fights and 2-bar endings work. New collisions and rapid cycling. |
| Variation and repetition | 6 | 7 | M | Ray gun fixed, entries rotate. |
| Fit to bible and brief | 6 | 7 | I | Bible now tells the truth, except the -1 dB peak rule. |

## The 23 issues

| # | Issue | Status | Report 1 | Now |
|---|---|---|---|---|
| 1 | Sound bank, no dynamics | Partly | 0.0 dB timbre change by velocity; repeats bit-identical | Slap bass soft vs hard: 3.5 dB less energy above 2 kHz; alternate takes 9 cents and 5% apart |
| 2 | Enemy fire in vacuum | Partly | Rifle on Deck -45.3 dB; Mars -34.1 | -31.4; -27.1. No suit layer; still 12 LU under combat music |
| 3 | Hit marker | Partly | -34 dB, 1734 Hz | -27.9 dB, 2906 Hz, click at 0.3 ms. Still 8 dB under ray gun and laser shotgun in its band |
| 4 | Bass under 200 Hz | Partly | Loss on Deck: combat 5.4, flight 4.0, menu 3.9 LU; flight drums 4.3 | 4.5, 2.4, 3.3; drums 2.0. Combat bass still 75% sub-200 Hz |
| 5 | Re-engagement delay | Fixed | 12.4 s | 3.8 s |
| 6 | Ambush hit | Fixed | Never; riser, 4.35 s | Hit lands 1.56 s after the threat jump |
| 7 | Combat from non-fights | Partly | Target practice and crashes start combat | Peak 55 and 58, no combat. Steady fire 100 m away still does (78) |
| 8 | No limiter | Partly | -0.3 dB summed; -3 dB headroom | See new problems 5 and 6 |
| 9 | Melody missing | Partly | Flight lead from 36, combat 68 | 22 and 60, but coasting reports 20, and combat lead stem is silent in bars 1-8 (-51 dB) |
| 10 | Always bar 1 | Fixed | Unused | Rotates 1, 17, 33; offsets on bar lines within one sample |
| 11 | Rifle family | Partly | Hand cannon-sniper 5.3 dB; rifle-hand cannon 5.2 | 10.8; 6.0. SMG-suppressed 5.2 unchanged |
| 12 | Loudness order | Partly | Ray gun 2.1 LU over hand cannon | 0.7 LU over; sniper now 2.5 LU under the rifle |
| 13 | Exploration dark | Not fixed | 4-5% above 2 kHz | 5-7% (own rule: 9%) |
| 14 | Vanishing stems | Not fixed | Cave -13.9, pulse -5.9 LU | -13.9, -5.1 |
| 15 | Ambience level | Partly | -42 to -51 dB | -35 to -42 dB, raised always, not only without music |
| 16 | Variants | Partly | Ray gun 0.98, grab 1.00, revolver 0.86 | 0.27, 0.69, 0.86. Rifle 01 still 49% sub-200 Hz against 33% |
| 17 | Silent first frame | Fixed (I) | 16-40 ms late | Covered at 34 fps and above |
| 18 | Ends on any bar | Fixed | Any bar | Exactly 12 and 10 bars in my runs |
| 19 | Riser tempo clash | Not fixed | | |
| 20 | Reward overlap | Partly | Gap 0.9-1.2 s | Gap 61% of clip; tier 4 only 3.4 dB down when the next starts; duck 2.4 s for a 4.6 s clip |
| 21 | Energy mechanics | Not fixed | | |
| 22 | Bible accuracy | Fixed (I) | | |
| 23 | Encoding | Not fixed | | |

## New problems introduced, ranked

**1. Major: a fight that flares up again collides with the ending. (M sim + I)**
- Combat told to end at 40.92 s, to stop at 43.84. Re-requested at 42.90: riser at 43.20, the ending cue still fires at 43.96 inside it, combat restarts at 45.99.
- `AdaptiveMusicPlayer` calls `Stop()` on the combat stems the moment it receives the new start, so the music still playing is cut dead with no fade.
- The ending cue is never cancelled. It sits at -14 dB RMS for five seconds, 6 dB louder than combat music (-19.9).
- Cause: `urgent` plus the 2-bar wait; cues cannot be stopped.
- Fix: if combat has not yet stopped, cancel the stop and carry on; add a cue fade-out command.

**2. Major: combat music can now cycle every 16 s. (M sim)**
- Threat alternating 75/15 every 8 s: 15 combat starts and 29 cues in 240 s, each from bar 1.
- Fix: resume a recent fight at bar 41 without riser; require intensity under 38 for several seconds.

**3. Medium: level jump between velocity buckets. (M)**
- Velocity 99 to 100: +2.1 to +2.8 dB (plain sound bank +0.1). Guitar also +2.2 dB at 71 to 72.
- 38-44% of combat and flight bass notes lie within 5 steps of 100.
- Cause: `saturate(x, 1.35, 0.5)` boosts quiet signals by 1.27.
- Fix: gain-match the takes; crossfade neighbouring buckets.

**4. Medium: sniper quieter than the rifle. (M)** -15.9 against -13.4 dB (was -11.9), while still peaking at -2.9 dBFS. Fix: restore 300 Hz-3 kHz body, volumeDb 0.

**5. Medium: the whole game is quiet. (M)** Showcase -23.9 LUFS, exploration -27 to -29, peak -4.2 dBFS. A point-blank kill (big gun plus torso pop) still peaks at -2.0 dBFS median, +0.5 worst; three coinciding sounds clip in 51% of trials. Fix: master limiter, then give 3 dB back.

**6. Medium: summed combat stems at intensity 60-79 reach -0.01 dB true peak (M)** (was -0.3). Riser tail plus downbeat: +1.7 dB raw. Cause: the limiter curve is worked out with all five stems. Fix: calibrate on the worst reachable set.

**7. Minor: lift-off cue lands on bar 17. (I)** Its held Gmaj9 chord meets Bm9 every third take-off. Fix: cued transitions enter at bar 1.

**8. Minor: one distant shot (132 m) unlocks the full ceiling for 10 s. (M sim)** Fix: scale by weight.

**Checked and clean:** the master tilt made nothing harsh (energy above 6 kHz: 5.4-5.6% combat, flight, menu; 2.1% exploration) and loudness targets hold (-14.6, -17.0, -15.0, -17.5, -20.5 LUFS). Stems stay aligned at entry offsets. Two takes at one velocity differ by 0.03-0.66 dB.

## The three changes that would raise the score most

1. **A real multi-sampled bass and drum kit.** Production realism is still the blocker, and filtering one recording cannot fix it. If downloads are blocked, the owner supplies a CC0 library.
2. **A master limiter, then raise the game about 5 dB;** fix new problems 1, 2 and 6 in the same pass.
3. **Finish the half-fixes:** suit-borne knock for vacuum fire, mid-range in the combat bass, a lead in combat bars 1-8, flight lead from 18, brighter exploration.

## Note

This is the critic's second report, kept as written (13 Nov 2026). It reviewed the version merged in pull request 24. What was done about its new problems afterwards is in `docs/reports/audio-slice.md`.
