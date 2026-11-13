# Audio rebuild report 1 - the vertical slice

Date: 13 Nov 2026. Branch `claude/audio-slice`.

Status: **built and measured, never heard, not yet run in Unity.** Nothing in this report is a listening result. The system that wrote and mixed this audio cannot hear, and neither can the critics it used. Every quality statement below is a measurement or a reading of the source, and says which. Matthew's first listen decides what happens next.

The honest headline: an independent critic scored the first version **6.1 out of 10 against a pass mark of 8.5**. Most of what it found is fixed. Its biggest finding is not: the "real" instruments (bass, drums, brass, electric piano) come from a free General MIDI sound bank, and that is the most likely place for the music to still sound cheap. See "The main open problem".

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
| 30 rule tests (`Tests/EditMode/AudioRulesTests.cs`), run with `dotnet test` outside Unity | All pass | The music director, intensity, rewards, vacuum, voice pool and the five-minute script behave as written. Not that Unity plays them correctly. |
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

**Second critic pass:** see the end of this report.

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
| Critic pass and fixes | Done twice over; score below the 8.5 target |
| AudioMixer groups and snapshots | **Partial.** The eight buses exist in code with levels and sliders. A real AudioMixer asset (needed for a limiter and for snapshots) is not made: it cannot be created or checked without the editor. There is an experimental menu item, Orbital Ante > Audio, for it. |
| Sourced recordings (real guns, real engines, real instruments) | **Blocked** from the cloud: sound-library sites are not reachable. |
| Steam Deck findings | **Blocked** until it runs on the Deck. Estimates only, in `docs/performance_budget.md`. |
| Listening | **Blocked.** Matthew. |
| The rest of the brief (24+ compositions, 200+ effects) | Not started, on purpose: quality first. |

## How to hear it (Steam Deck, Desktop Mode)

The quick way, no Unity: play the attached `orbital_ante_audio_showcase.ogg` (five minutes). Use headphones once and the Deck's own speakers once.

In the game:

1. Open Konsole and run `cd ~/Orbital-ante && git fetch && git checkout claude/audio-slice && git pull`.
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
