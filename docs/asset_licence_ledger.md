# Asset licence ledger

Log every asset, texture, sound and track with its source and licence before it goes into `unity/Assets`.

| Asset | Path | Source / URL | Licence | AI-generated? | Added | Notes |
|---|---|---|---|---|---|---|
| (none yet) | | | | | | Milestone 1 uses only Unity built-in primitives, Unity's built-in font and textures generated in code. |
| Game audio: 123 sound-effect files, 24 music stems, 5 transition cues | `unity/Assets/OrbitalAnte/Resources/Audio/` | Made in this repository by `tools/audio/` (original synthesis and original compositions) | Project-owned | No AI audio service. Written and rendered by code. | 13 Nov 2026 | Full detail in `docs/audio/licence_manifest.md`. |
| FluidR3 GM sound bank (sampled instruments heard inside the music and seven reward sounds) | Not in the repository; only audio rendered from it | Debian package `fluid-soundfont-gm` 3.1-5.3, by Frank Wen | MIT | No | 13 Nov 2026 | Credit line suggested in `docs/audio/licence_manifest.md`. |
