#!/usr/bin/env python3
"""Draws waveform + spectrogram sheets so sounds can be inspected by eye.

    python3 tools/audio/spectro.py out.png file1.wav file2.ogg ...
"""
import sys, os
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import soundfile as sf
from scipy import signal

def main():
    out, files = sys.argv[1], sys.argv[2:]
    cols = min(4, len(files)); rows = (len(files) + cols - 1) // cols
    fig, axes = plt.subplots(rows * 2, cols, figsize=(5 * cols, 3.6 * rows), squeeze=False,
                             gridspec_kw={"height_ratios": [1, 2.4] * rows})
    for i, path in enumerate(files):
        x, sr = sf.read(path, always_2d=True); m = x.mean(axis=1)
        r, c = divmod(i, cols)
        t = np.arange(len(m)) / sr
        a = axes[r * 2][c]; a.plot(t, m, lw=0.4, color="#234"); a.set_ylim(-1, 1); a.set_xlim(0, t[-1])
        a.set_title(os.path.basename(path), fontsize=9); a.set_xticks([])
        nper = 1024 if len(m) > 8192 else 256
        f, tt, S = signal.spectrogram(m, sr, nperseg=nper, noverlap=nper * 3 // 4)
        b = axes[r * 2 + 1][c]
        b.pcolormesh(tt, f, 10 * np.log10(S + 1e-12), vmin=-110, vmax=-30, shading="auto", cmap="magma")
        b.set_yscale("symlog", linthresh=200); b.set_ylim(30, 20000); b.set_yticks([50, 200, 1000, 5000, 15000]); b.set_yticklabels(["50", "200", "1k", "5k", "15k"], fontsize=7)
    for j in range(len(files), rows * cols):
        r, c = divmod(j, cols); axes[r * 2][c].axis("off"); axes[r * 2 + 1][c].axis("off")
    plt.tight_layout(); plt.savefig(out, dpi=70)

if __name__ == "__main__":
    main()
