"""A listening file for the three game pieces: 44 s each of exploration, flight and combat, mixed from one into
the next on a bar line the way the game does (they share one tempo and key).

    python3 tools/audio/render_music_preview.py

Writes tools/audio/build/demo/music_preview_deep_house.ogg. Exploration is heard without its tension layer for
the first half and with it for the second; combat without its top layer for the first half.
"""
from __future__ import annotations

import os

import numpy as np
import soundfile as sf

from oa_audio import dsp, mixing

HERE = os.path.dirname(os.path.abspath(__file__))
MUSIC = os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Audio", "Music")
BAR = 2.0          # seconds, at 120
PART = 22          # bars of each piece
CROSS = 2          # bars of overlap
HALF = (PART // 2) * BAR


def piece(name: str, start_bar: int, late: tuple[str, ...]) -> np.ndarray:
    n = dsp.secs((PART + CROSS) * BAR)
    out = np.zeros((n, 2))
    for f in sorted(os.listdir(os.path.join(MUSIC, name))):
        if not f.endswith(".ogg"):
            continue
        x, sr = sf.read(os.path.join(MUSIC, name, f), always_2d=True)
        assert sr == dsp.SR
        a = dsp.secs(start_bar * BAR)
        x = np.take(x, np.arange(a, a + n), axis=0, mode="wrap")
        if f[:-4] in late:
            t = np.arange(n) / dsp.SR
            x = x * np.clip((t - HALF) / (2 * BAR), 0, 1)[:, None]
        out += x
    return out


def main() -> None:
    parts = [piece("explore_regolith", 0, ("tension",)), piece("flight_burn_window", 8, ()), piece("combat_all_in", 16, ("chaos",))]
    step = dsp.secs(PART * BAR)
    cross = dsp.secs(CROSS * BAR)
    total = np.zeros((step * len(parts) + cross, 2))
    ramp = np.linspace(0, 1, cross)[:, None]
    for i, p in enumerate(parts):
        p = p.copy()
        if i > 0:
            p[:cross] *= np.sin(ramp * np.pi / 2)
        p[-cross:] *= np.cos(ramp * np.pi / 2)
        # each part is PART + CROSS bars long and starts CROSS bars before the previous one ends
        at = i * step
        total[at:at + len(p)] += p[:len(total) - at]
    total *= 10 ** (-4 / 20)        # the game's headroom
    path = os.path.join(HERE, "build", "demo", "music_preview_deep_house.ogg")
    mixing.write_ogg(path, total)
    print(f"{path}: {len(total) / dsp.SR:.0f} s, peak {20 * np.log10(np.max(np.abs(total)) + 1e-9):.1f} dB")


if __name__ == "__main__":
    main()
