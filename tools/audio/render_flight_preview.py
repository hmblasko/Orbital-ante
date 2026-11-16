"""A listening file for the solar-system (flight) music: the piece once through, the way it builds in the game
as the ship goes from drifting to a hard burn. Chords from the start, bass from bar 5, drums and lead from
bar 9, the hard-burn layer from bar 41, then the opening again to hear the loop point, and a fade.

    python3 tools/audio/render_flight_preview.py
"""
from __future__ import annotations

import os

import numpy as np
import soundfile as sf

from oa_audio import dsp, mixing

HERE = os.path.dirname(os.path.abspath(__file__))
DIR = os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Audio", "Music", "flight_burn_window")
BAR = 4 * 60.0 / 126.0
ENTER = dict(chords=0, bass=4, drums=8, lead=8, top=40)


def main() -> None:
    stems = {f[:-4]: sf.read(os.path.join(DIR, f), always_2d=True)[0] for f in os.listdir(DIR) if f.endswith(".ogg")}
    n_loop = len(next(iter(stems.values())))
    n = n_loop + dsp.secs(16 * BAR)
    t = np.arange(n) / dsp.SR
    out = np.zeros((n, 2))
    for name, x in stems.items():
        x = np.take(x, np.arange(n), axis=0, mode="wrap")
        gate = np.clip((t - ENTER[name] * BAR) / (2 * BAR), 0, 1)
        out += x * gate[:, None]
    fade = np.clip((n / dsp.SR - t) / (8 * BAR), 0, 1)
    out *= fade[:, None] * 10 ** (-4 / 20)
    path = os.path.join(HERE, "build", "demo", "solar_system_music_preview.ogg")
    mixing.write_ogg(path, out)
    print(f"{path}: {n / dsp.SR:.0f} s, peak {20 * np.log10(np.max(np.abs(out)) + 1e-9):.1f} dB")


if __name__ == "__main__":
    main()
