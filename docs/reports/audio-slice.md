# Audio rebuild report 1 - the vertical slice

Date: 13 Nov 2026. Branch `claude/audio-slice`.

Status: **built and measured, never heard, not yet run in Unity.** Merged to main in pull request 24; this version of the report includes the follow-up round. Nothing in this report is a listening result. The system that wrote and mixed this audio cannot hear, and neither can the critics it used. Every quality statement below is a measurement or a reading of the source, and says which. Matthew's first listen decides what happens next.

The honest headline: an independent critic scored the first version **6.1 out of 10 against a pass mark of 8.5**, and a second pass after fixes scored it **6.7**. It does not pass. Its biggest finding is not: the "real" instruments (bass, drums, brass, electric piano) come from a free General MIDI sound bank, and that is the most likely place for the music to still sound cheap. See "The main open problem".

## What changed

**A production pipeline in the repository** (`tools/audio/`). Every sound and every piece of music is rendered from source by `python3 tools/audio/build.py all`, at 48 kHz. Nothing was downloaded from a sound library: those sites are blocked from the cloud workspace. Instruments are either synthesised by this repository's own code or played from one MIT-licensed sound bank (`docs/audio/licence_manifest.md`).

**A style bible** (`docs/audio/style_bible.md`) with the four themes (Orbital Ante, Exploration, Conflict, Cosmic Mystery), the band, the tempo and key plan, how the music behaves, and the rules for weapons, rewards and sound in a vacuum.

**Five pieces of music, in stems (24 stems), and five transition cues:**

| Piece | For | Tempo | Length | Layers |
|---|---|---|---|---|
| Regolith Lullaby | Exploring on foot | 86 | 2:14 | 6 |
| All In | Combat | 172 | 1:29 | 5 |
| Burn Window | Flying | 124 | 2:04 | 5 |
| The House Always Orbits | Main menu | 104 | 1:32 | 4 |
| What the Dish Heard | Caves and the unknown | 60 | 1:36 | 4 |

Cues: a riser into combat, an ambush hit, a combat ending, a lift into flight, a settle back to calm.

**68 sound events in 130 files:** nine distinct weapons (rifle, SMG, hand cannon, sniper and its bolt, suppressed pistol, laser revolver, ray gun, laser shotgun, gravity gun) plus reload, dry fire and drop sounds; impacts, limb pops, hit marker, headshot and kill; ship engine, cabin, switches, seatbelt, touchdown; footsteps, jetpack, breath, suit alarm; six ambience beds; a twelve-step reward ladder; menu sounds. The full list is in `docs/audio/implementation.md`.

**An adaptive music system in the game** (`Scripts/Runtime/Audio/`). One number, intensity (0 to 100, the five bands from the brief), decides which layers play and when combat music takes over. Changes happen on beats and bars using the audio clock, combat arrives with a riser and leaves with an ending, music rests after two times round, and deep space is silent.

**A director for sound effects:** a fixed pool of voices with priorities, per-sound limits and cooldowns, no-repeat variations, sound that changes in thin air and vacuum, a reward queue that plays one reward at a time, and four volume sliders on a new Audio page in the pause menu.

**A five-minute showcase** on the main menu ("Audio showcase (5 min)"): exploration, discovery, rising tension, combat, chaos, loot and scoring, take-off, return to calm. The same script was also rendered outside Unity by running the game's real music rules, and that recording is attached to the pull request message as `orbital_ante_audio_showcase.ogg`. **That recording is the quickest way to hear this work**, but it is a preview: Unity's own mixing is not in it.

**A debug panel** (F9): intensity, band, piece, bar and beat, which layers are up, voices in use, rewards waiting.

**What was left alone.** The old code-made sounds are still there and still play whenever a produced sound is missing, so nothing goes silent if the audio folder is absent. No scene, prefab or mixer asset was hand-written.

## Why, and which principles it serves

- **Fun core gameplay first (1).** Guns, hits and pops are the centre of Phase 1; each weapon now has its own recipe instead of sharing one noise burst.
- **Legible risk and reward (5).** Combat music means "something is shooting at you". Shooting at nothing, target practice on something that does not fight back, and crashes are all capped below it, and each of those rules has a test.
- **Manage cognitive load (7).** One reward at a time, biggest first, small ones dropped rather than queued up behind big ones; one hit sound per hit (marker, or headshot, or kill).
- **Varied intensity (8).** Music rests, silence in deep space, combat that ends properly and returns to bare layers.
- **Ethical design (9).** Reward sounds only confirm something the player did. No countdowns, no near-miss sounds.
- **Evidence-based iteration (10).** Every claim in this report is tagged as measured, read from source, or unverified.

## What was tested, and how

| Check | Result | What it proves, and what it does not |
|---|---|---|
| 33 rule tests (`Tests/EditMode/AudioRulesTests.cs`), run with `dotnet test` outside Unity | All pass | The music director, intensity, rewards, vacuum, voice pool and the five-minute script behave as written. Not that Unity plays them correctly. |
| The whole game's C# compiled against Unity reference assemblies | Compiles | No typos or missing references in runtime code. The two editor scripts could not be compiled here. |
| `tools/audio/qa.py` over every file (`docs/audio/qa_report.md`) | Pass, 0 failures, 4 warnings | Every file is 48 kHz, no clipping, true peak at or under -1 dB, starts and ends on silence, loops wrap cleanly; music hits its loudness targets; every weapon measures further from every other weapon than from its own variations. The warnings: landing thud and engine ignition are mostly below 250 Hz and will be weak on Deck speakers. |
| Loop exactness: each piece rendered three times round, third pass compared with the second | -212 dB or better on all five | The loops are sample-exact, so there is no click or gap at the wrap. |
| The five-minute showcase, rendered from the real rules | 67 music changes, 438 sounds, peak -4.2 dBFS, no samples over full scale | The script runs end to end and never clips in this render. One sound was refused by the voice rules, as designed. |

**Not tested at all:** how any of it sounds; anything in Unity (import settings, timing on the real audio clock, the pause menu page, the debug panel, the showcase button); anything on a Steam Deck (speakers, CPU, memory).

## The critic's verdict and what was done

The full first report is kept as written in `docs/audio/critic_report_1.md`. Scores: technical cleanliness 7.5, loudness and balance 6, small speakers 5.5, weapon distinctiveness 6.5, effect design 6, composition 6.5, production realism 4, adaptive behaviour 6.5, variation 6, fit to the brief 6.

| # | Issue | What was done |
|---|---|---|
| 1 | Instruments are a General MIDI sound bank with no change of tone between soft and loud notes | **Partly.** Notes are now rendered soft, medium and hard with different tone, and alternate between two slightly different takes. It is still the same sound bank. See below. |
| 2 | Enemy gunfire vanishes in vacuum on Deck speakers | **Changed.** Duller and quieter in vacuum, but no longer below what small speakers can play. The critic's second suggestion (a knock through the suit) is not done. |
| 3 | Hit marker masked by the player's own gun | **Changed.** 6 dB louder, moved higher, with a click. |
| 4 | Bass mostly below what a Deck can play | **Changed.** Added harmonics to the bass, a mastering step that corrects dull or boomy mixes, louder drum tops. |
| 5 | A fight that restarts gets no music for 12 seconds | **Fixed**, with a test: combat music may interrupt the quiet after the last fight. |
| 6 | The ambush hit could never play | **Fixed**, with a test. |
| 7 | Combat music from target practice or a crash | **Fixed**, with a test. Far-off enemy shots also count for less. |
| 8 | No limiter and thin headroom | **Partly.** The three loudest guns are 2 dB quieter and headroom is 4 dB. There is still no limiter: it needs an AudioMixer asset, which should be made in the editor. |
| 9 | Melody missing at common intensities | **Changed.** The tune comes in earlier in flight and from the first bar of combat. |
| 10 | Every piece always restarts from its first bar | **Fixed**, with a test: pieces rotate through two or three entry points. |
| 11 | Rifle, hand cannon and sniper too alike | **Changed.** Sniper and hand cannon redesigned. Measured distances are in the QA report; rifle and sniper are still the closest pair. |
| 12 | Ray gun louder than the hand cannon | **Fixed.** |
| 13 | Exploration mix dark | **Changed** by the mastering step. |
| 15 | Ambience far under the music | **Changed.** Beds raised. |
| 16 | Some variations identical | **Fixed** for ray gun and gravity gun. |
| 17 | First instant of a hard start silent | **Fixed** in code. |
| 18 | Combat could end mid-phrase | **Fixed.** It ends on two-bar boundaries. |
| 20 | Big rewards overlapping | **Fixed**, in the rules. |
| 22 | Style bible said things that were not true | **Fixed.** |
| 14, 19, 21, 23 | Cave, facility, cabin and two music layers lack mid-range; the combat riser's drum roll clashes with flight's tempo; energy weapons have no mechanical layer; music is compressed twice | **Not done.** |

### Second critic pass: 6.7 out of 10, still not a pass

A second independent pass re-measured the merged version (`docs/audio/critic_report_2.md`). **6.7, up from 6.1, against 8.5.** It confirmed as fixed: the 12-second wait for music when a fight restarts (now 3.8 s), the ambush hit, combat music from target practice and crashes, pieces always restarting from bar 1, endings mid-phrase, the silent first instant, and the style bible's accuracy. It rated most of the sound changes "partly fixed" (vacuum gunfire is 14 dB louder on small speakers but still well under the music; the hit marker is 6 dB up but still under the two loudest energy weapons; the combat bass is still mostly below what a Deck can play). Production realism moved only from 4 to 4.5: same sound bank.

It also found eight new problems caused by the fixes. What was done in this follow-up:

| New problem | What was done |
|---|---|
| A fight that flares up again as the ending is being counted in cut the music dead and let the ending ring over the new riser | **Fixed**, with a test: the ending always lands first, then fades out under the riser. |
| Danger that comes and goes could restart combat music every 16 seconds | **Fixed**, with a test: the danger must stay low for four seconds before a fight counts as over. |
| A 2 to 3 dB jump in level between a medium and a hard note | **Fixed** for the hard notes (level-matched). The smaller step between soft and medium on the guitar is not. |
| Sniper quieter than the rifle | **Changed.** Its body is back and it is 2 dB louder. |
| The whole game is quiet, and three big sounds at once can still clip | **Not done.** Needs a limiter on the output, which needs an AudioMixer asset made in the editor. |
| Combat layers without the top one reach -0.01 dB | **Not done.** Covered in play by the 4 dB of headroom; to be fixed with the limiter. |
| The lift-off cue could land on bar 17 of the flight piece | **Fixed**: a piece that arrives on a riser starts from its top. |
| One distant shot unlocked full combat intensity for ten seconds | **Fixed**, with a test. |

The fixed version has **not** been re-scored a third time.

**Codex review** (asked for on pull request 24): one finding, that storm wind and thunder ignored the new effects volume setting. Checked against the code, it was right, and it is fixed in this follow-up. Nothing from Codex was rejected.

### Matthew's first reaction (13 Nov): "no footstep sounds"

Footsteps were there (in the recording and in the arena) but far too quiet: measured, each step played about 14 dB under the music and was almost all hiss above 2 kHz, with nothing in the range a Deck's speakers play well. Changed: steps are about 14 dB louder, the dusty-ground step has a mid-range knock (40% of its energy is now between 250 Hz and 2 kHz, from 13%), and the arena's step trigger no longer loses steps when the ground check flickers for a frame. Not heard, so whether they are now right, too loud or still wrong is for Matthew's ear. Note: on main today the only place you walk is the combat arena; there is no walking in the solar system until the ship interior and suit are in.

### Matthew's request (14 Nov): hear the thrusters by direction

"You should hear thrusting in different direction based on the direction the thrusters are going."

**What changed.** Six new looping sounds and two valve sounds, one rule class and one call:

| The pilot pushes | Thruster that fires | Where and how it is heard |
|---|---|---|
| Forward | Main engine | The existing engine roar, low, all around |
| Backward | Nose thrusters | Close, bright bark dead ahead |
| Right | Left-side thrusters | Left ear (10.6 dB louder there), late and dull in the right |
| Left | Right-side thrusters | Right ear, the same the other way round |
| Up | Belly thrusters | Low drumming under the floor |
| Down | Roof thrusters | Thin, airy hiss overhead |
| Turning | Attitude jets | Quick cold-gas puffs alternating sides |

A thruster is heard from the side it sits on, which is the side opposite to the push, as on a real spacecraft. If that feels backwards in play (push right, hear it on the left), it is one line to flip. Each valve clacks once when it opens and once when it closes, panned to its side. Diagonals mix two thrusters. A resting stick is silent.

**For the flight code:** one call per frame, `AudioDirector.Instance.SetShipThrust(localPush, turn, boost)`, with the push the pilot is asking for in the ship's own axes (x right, y up, z forward, each -1 to 1), and `StopShipThrust()` when the player leaves the seat. The rules are in `ThrusterMix.cs` (plain C#). The craft on main already reports a three-axis push, so it is wired there now; turning is not wired, because that craft turns with the mouse and reports no turn command.

**Why (principles 1 and 5).** With three-axis thruster flight the ears can confirm what the hands just did, without looking at a gauge.

**Tested.** Four new rule tests (each direction fires only its own thruster, on the opposite side; diagonals; dead zone; one clack per open and close, not one per waggle; every thruster sound exists in the manifest). Measured from the files: left and right differ by 21 dB between the ears; belly sits around 360 Hz, nose around 2.4 kHz, roof around 5.1 kHz. `qa.py` passes. Thrusters and the main engine were also raised 3 to 7 dB after the footstep lesson, so they are not lost under the flight music.

**Unverified.** Whether the directions can really be told apart by ear, and whether up and down read as "below" and "above" (stereo cannot place a sound above you; only the tone suggests it). Not run in Unity. A 39-second preview, `thruster_preview.ogg`, plays each direction in the order of the table, then two diagonals; it is turned up for listening on its own.

### Matthew's direction (14 Nov): "The main music needs to be redone. More existential and not upbeat, but still a good deep house beat."

**What changed.** The menu piece, a swung jazz number in D major, is gone. In its place is a new main theme, "Long Way From Anywhere": deep house at 120, D minor, 64 bars (2 min 8 s), five layers. A soft, round kick on every beat with off-beat hats and a distant clap; an off-beat sub bass; slow minor ninth chords (two bars each) that never settle, played as dub-echoed stabs whose filter opens and closes over sixteen bars; and one bell with long gaps, using the game's own notes (the ante cell in the minor, and the Cosmic Mystery theme's wide intervals). Eight bars in the middle drop the kick altogether. It is entirely synthesised, so it does not lean on the General MIDI sound bank the critic objected to.

Also fixed while there: in the menus the old piece's melody never played at all, because it waited for an intensity the menus never reach. All five layers of the new theme play in the menus.

**Why (principles 8 and 1; build prompt section 1, "a thread of horror and existential unknown").** The old theme told the player this was a jaunty game.

**Tested.** Loop is sample-exact (-215 dB). -16.5 LUFS, true peak -1.5 dB. Through a 200 Hz high-pass (a stand-in for Deck speakers) the mix loses 1.5 LU; the bass layer alone loses 4.3 LU and the beat 2.7, so on a handheld the beat is carried by the kick's knock, the hats and the clap. `qa.py` passes.

**Unverified.** Whether it sounds existential, or good, or like deep house to someone who loves deep house. Nobody has heard it. Listening file: `main_theme_long_way.ogg` (the piece once through, then the first quarter again so the loop point can be heard, then a fade).

**Matthew's verdict (14 Nov): "Less bells but otherwise like it."** Done: the bell now stays out of more than half the piece (the first sixteen bars and the sixteen after the breakdown are groove and chords alone), its echo an octave down is kept only in the breakdown, what remains is softer (more glass tone than struck bell) and about 3 dB quieter, and the high shimmer layer is thinner. Not heard.

**Not changed yet.** Exploration, flight, combat and mystery. They wait for Matthew's verdict on this one, since his direction probably applies to them too.

### Slow motion ("Deep Breath", asked for by Matthew on 14 Nov; built in the core thread)

When the game slows time, the world's sounds now go with it: gunshots, impacts, pops, engines and beds drop in pitch (to 0.7 at the deepest slow, less than time itself, so a gunshot is still a gunshot) and lose their top (down to 3.2 kHz). Sounds inside the suit and the interface stay exactly as they are, so the player's own head is the one thing still at full speed. Music keeps its tempo, because it runs on the audio clock, and steps back by about a third instead. Going in there is a sinking breath with one heavy heartbeat; coming out, the same breath let go. The audio reads the game's time scale and never sets it, so it also covers the headshot slow-motion with no extra wiring. One new rule test. Not heard, not run in Unity.

### The main open problem

The bass, drums, brass, strings, choir and electric piano are played from FluidR3 GM, a free General MIDI sound bank. The critic measured that a soft note and a loud note from it differ only in volume, which is what makes MIDI files sound like MIDI files. The workaround here (three tone settings and two alternating takes per note) hides the worst of it by measurement, but a workaround is what it is. If the music still sounds cheap, this is the first suspect, and the fix is better instrument recordings, which cannot be downloaded from the cloud workspace. Two ways forward, to choose after listening:

1. Matthew downloads one or two free, properly licensed sampled instruments (a multi-velocity bass first, then a drum kit) into the repository, and the pipeline switches to them.
2. Drop the imitation of real instruments for the weakest ones and use the repository's own synthesisers, which do not pretend to be anything.

## Agent review

- **Unity reviewer:** 16 findings. 14 fixed (music waiting for its files before starting, the showcase leaving a blank screen if audio was missing, old bleeps playing over new sounds, interface sounds sharing one voice, Unity's voice limit, context flapping between flight and surface, import settings, and others). 2 left: one-shot sounds do not follow the floating origin when it shifts, and volume settings are stored in PlayerPrefs rather than in `SaveData`.
- **Audio critic:** above.
- **Deck performance (read from code, nothing measured):** no memory churn per frame in play. Costs to look at on the Deck: all music is read in one go as the game starts (a guess of 0.1 to 0.5 s); the audio device restarts once at start-up to raise the voice limit; up to 13 to 15 compressed streams decode at once during a flight hand-over; sound effects take about 12.5 MB, not the 5 first written. Two small fixes made (the old engine rumble is now stopped, not just muted; the search for the listener is limited to twice a second). `docs/performance_budget.md` is corrected. Not done: setting the voice limit in project settings, building the old placeholder sounds only when needed.
- **Game designer:** the intensity caps hold up against the constitution. Points raised, none changed without Matthew:
  1. The build prompt lists the "music system" under Phase 2. It is here because the audio brief of 13 Nov asked for it now.
  2. **The build prompt asks for music that "blends continuously like a club DJ at a steady tempo". This slice uses a different tempo per situation (86, 172, 124, 104, 60), with risers, endings and rests, as the audio brief describes.** These pull in different directions. Question for Matthew below.
  3. The reward ladder (four tiers, multiplier, common and rare loot, discovery) is ahead of the game: points and loot are Phase 2. It is audible only in the showcase. The prompt's "distinct sounds for art, cosmetics, ammo and new weapons" will need their own sounds then.
  4. Small rewards are dropped when a big one is playing. That is only acceptable if the on-screen notification always shows them. A full queue now drops its smallest entry instead of the newest (changed).
  5. With no enemy awareness in the game yet, the combat ending can play about nine seconds after the last incoming shot, while enemies may still be alive. Needs a "hostiles nearby" signal from the enemy AI.
  6. The prompt says "keep the ship-flying sound feel of earlier builds". The new engine loop replaces the old rumble in the solar system. Worth comparing by ear.
  7. The showcase script and the choice of variations use `System.Random`, not `SeededRandom`. Left as is: they are not part of a run's seed, but say so if you want them moved.

## Not done, partial and blocked

| | State |
|---|---|
| Style bible, pipeline, licence manifest, QA report, implementation guide | Done |
| 4 to 6 compositions | Done (5), never heard |
| 30 to 40 sound effects | Done (68 events), never heard |
| Adaptive music demo and five-minute sequence | Done in code and as an offline render; not run in Unity |
| Critic passes and fixes | Two passes (6.1, then 6.7; target 8.5) and two rounds of fixes. Below target. |
| AudioMixer groups and snapshots | **Partial.** The eight buses exist in code with levels and sliders. A real AudioMixer asset (needed for a limiter and for snapshots) is not made: it cannot be created or checked without the editor. There is an experimental menu item, Orbital Ante > Audio, for it. |
| Sourced recordings (real guns, real engines, real instruments) | **Blocked** from the cloud: sound-library sites are not reachable. |
| Steam Deck findings | **Blocked** until it runs on the Deck. Estimates only, in `docs/performance_budget.md`. |
| Listening | **Blocked.** Matthew. |
| The rest of the brief (24+ compositions, 200+ effects) | Not started, on purpose: quality first. |

## How to hear it (Steam Deck, Desktop Mode)

The quick way, no Unity: play the attached `orbital_ante_audio_showcase.ogg` (five minutes). Use headphones once and the Deck's own speakers once.

In the game:

1. Open Konsole and run `cd ~/Orbital-ante && git checkout main && git pull`.
2. Open the project in Unity Hub as usual. The first open takes a few extra minutes while Unity imports about 160 audio files.
3. In the top menu choose **Orbital Ante > Audio > Check Audio Files**. The Console should say how many events and pieces it found and list no errors. If it lists errors, copy them to me.
4. Press Play. Menu music should start within a second or two.
5. Click **Audio showcase (5 min)** on the main menu. Press **F9** to see the debug panel while it runs. Press Esc (or B) to leave it.
6. Start a run in the combat arena. Fire the rifle and the pistol at a mannequin. Open the pause menu and try the **Audio** page.
7. If you can, open **Window > Analysis > Profiler**, click the **Audio** row during the showcase's combat part, and note "Playing Sources", "Audio CPU" and "Total Audio Memory".

If Unity shows red errors on opening, send me the first one. Two editor scripts could not be compiled in the cloud.

## What I need from you, most important first

1. **Does the music sound cheap?** Listen to the bass in the combat and flight parts of the showcase. A bass player, or a keyboard preset? This decides between the two ways forward above.
2. **One club tempo, or a tempo per situation?** Your build prompt asks for DJ-style blending at a steady tempo; your audio brief asks for adaptive pieces with different moods. The slice follows the audio brief. If you want the steady club feel, the pieces need to be rewritten around one tempo.
3. Fire the rifle, hand cannon and sniper (showcase, 2:13 to 3:10). Real guns, or shaped noise? Can you tell them apart?
4. On the Deck's own speakers in combat: can you hear each hit marker while holding the trigger? Can you follow the bass at all?
5. Does the combat ending feel like an ending?
6. Does the Exploration theme (a slow rise from D to A, then down a step) remind you of any existing tune? It is a common shape and nobody here could check by ear.
7. The suit breathing loop: calm astronaut, or Darth Vader?
8. Is the new ship engine better or worse than the old rumble?

## Still unverified

Everything about how it sounds. Everything in Unity. Everything on the Deck. The two editor scripts. Whether Unity's re-compression of the music is audible. Whether 48 voices is the right limit.
