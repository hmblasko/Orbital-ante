# Orbital Ante audio pipeline

Everything the game plays is made here, from source, by one command. There are no hand-edited audio files in the repository: to change a sound, change its source and rebuild.

## Why it is built this way

The cloud workspace this was made in has no audio workstation and cannot reach any sound library (the download hosts are blocked), so nothing could be recorded or sourced. Instead:

- **Music** is written as scores in Python (`music/*.py`: notes, chords, drum patterns, arrangement), played by sampled instruments (the FluidR3 General MIDI sound bank, through FluidSynth) and by synthesisers written in this repository (`oa_audio/synth.py`), then mixed with the repository's own effects (EQ, compression, convolution reverb, limiting).
- **Sound effects** are designed in Python (`sfx/*.py`), each built from layers.
- The same seed always gives the same audio.

Each piece of music also writes its parts as standard MIDI files to `sessions/<piece>/`. Those are the editable source for anyone who wants to open a piece in a real audio workstation later.

## Set-up (Linux)

```
sudo apt-get install fluidsynth fluid-soundfont-gm ffmpeg dotnet-sdk-8.0
pip install -r tools/audio/requirements.txt
```

## Commands

Run all of these from the repository root.

| Command | What it does |
|---|---|
| `python3 tools/audio/build.py all` | Renders every sound effect and every piece of music into `unity/Assets/OrbitalAnte/Resources/Audio/` and rewrites the game's audio manifest to match. About 15 minutes. |
| `python3 tools/audio/build.py sfx --only weapon.rifle` | Only the events whose id starts with that text. |
| `python3 tools/audio/build.py music --only combat_all_in` | Only that piece. `--only cue` renders the transition cues and refreshes the music rules in the manifest. |
| `OA_SEAM_CHECK=1 python3 tools/audio/build.py music` | Also proves each loop is exact (renders three copies and compares the third with the second). |
| `python3 tools/audio/qa.py` | Measures every file and writes `docs/audio/qa_report.md`. Fails if anything clips, is missing, loops badly, or if two weapons measure alike. |
| `python3 tools/audio/table.py weapon` | Prints the stored measurements for files whose path contains that text. |
| `python3 tools/audio/spectro.py <files>` | Draws spectrograms. |
| `dotnet run --project tools/audio/sim -- unity/Assets/OrbitalAnte/Resources/Audio/audio_manifest.json tools/audio/build/demo/schedule.json` | Runs the game's real music director and audio rules over the five-minute showcase script (no Unity needed) and records every decision. |
| `python3 tools/audio/render_demo.py` | Turns that record into an audio file you can listen to: `tools/audio/build/demo/orbital_ante_audio_showcase.ogg`. |

## Layout

| Path | What |
|---|---|
| `oa_audio/dsp.py` | Oscillators, filters, dynamics, reverb, limiter. |
| `oa_audio/seq.py` | The score notation (`"D4:2 A4:2"` is two half notes), chords, swing, humanising. |
| `oa_audio/sampler.py` | Plays notes through FluidSynth. |
| `oa_audio/synth.py` | Synth voices and synthesised drums. |
| `oa_audio/mixing.py` | Seamless-loop rendering (`Loop`), file writing, loudness and peak meters. |
| `oa_audio/motifs.py` | The four themes. Every piece takes its notes from here. |
| `music/` | One file per composition, `cues.py` for transition cues, `__init__.py` for the rules that tell the game when to play what. |
| `sfx/` | One file per family of sound effects. |
| `sim/` | A small C# program that links the game's own audio rule classes. |

## How loops are made seamless

Each part is rendered once with its tail (reverb, ringing notes) left hanging past the end, and the tail is folded back onto the start. Effects are then run across joined copies and only the last copy is kept, so every effect has already "heard" the end of the loop when the loop begins. The proof is the loop exactness figure in the QA report.

## Adding a sound effect

1. Add a function to the right file in `sfx/` with the `@sfx("my.event.id", "category", variants=3, ...)` decorator. It returns audio as a numpy array.
2. `python3 tools/audio/build.py sfx --only my.event.id`
3. In the game: `AudioDirector.Play("my.event.id", position)`. Nothing else to wire.

## Limits to be honest about

Nothing here has been listened to by the system that made it: it cannot hear. Balance was set by meter (`music/common.py`, `balance` and `calibrate`), and those numbers are the first things to revise after a human listens.
