# Audio licence manifest

Every sound in `unity/Assets/OrbitalAnte/Resources/Audio/` is listed here by origin. The rule: nothing goes into the game unless it is on this page with a licence that allows commercial use.

## Summary

| Origin | What | Licence | Files |
|---|---|---|---|
| **Original synthesis** | Made from nothing by code in this repository (`tools/audio/oa_audio/`, `tools/audio/sfx/`, `tools/audio/music/`). No recordings, no samples. | Owned by the project. | Every sound effect; the synthesised layers of every piece of music. |
| **FluidR3 GM sound bank** | Sampled instruments (basses, drum kits, electric piano, brass, strings, choir, glockenspiel, celesta) played by this repository's scores. | MIT (below). Commercial use allowed. | Layers inside every music stem and transition cue, and inside seven reward sounds. |

No other source was used. In particular: **no recordings from any sound library, no commercial sample packs, no audio from any existing game, film or song, and no AI audio-generation service.** The compositions are original; the references named in the build prompt were used as a description of style only.

## FluidR3 GM

- Name: Fluid (R3) General MIDI SoundFont, `FluidR3_GM.sf2`
- Author: Frank Wen, with the contributors listed in its README
- Obtained from: the Debian/Ubuntu package `fluid-soundfont-gm` (version 3.1-5.3)
- Licence: MIT. Copyright (c) 2000-2002, 2008 Frank Wen. "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files ... to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies ..." The full text is in the package's `copyright` file.
- How it is used: the sound bank itself is **not** in the repository or the game. Only audio rendered from it is. The MIT licence asks that its notice travel with copies of the sound bank; it places no condition on music made with it. As a courtesy the game's credits should include: "Sampled instruments: FluidR3 GM by Frank Wen (MIT licence)."
- Sound effects that include it: `reward.tier2`, `reward.tier3`, `reward.tier4`, `reward.multiplier`, `reward.points.rack_end`, `reward.loot.rare`, `reward.discovery` (bell and celesta notes). All other sound effects are pure synthesis.

## By folder

| Folder | Origin |
|---|---|
| `Sfx/weapons`, `Sfx/combat`, `Sfx/ship`, `Sfx/player`, `Sfx/ambience`, `Sfx/ui` | Original synthesis. |
| `Sfx/rewards` | Original synthesis; seven files also contain FluidR3 GM notes (listed above). |
| `Music/*` | Original compositions. Performed by FluidR3 GM instruments and original synthesis. |

## Adding audio later

If a recording or sample pack is ever brought in, add a row here **before** the file is committed: its name, where it came from (URL), the licence, the date, and exactly which game files contain it. CC0 and MIT-style licences are fine. Anything "non-commercial", "editorial use only" or of unknown origin is not.
