"""Regolith Lullaby: planetary exploration. 120 BPM deep house at half strength, D minor, 64 bars, 2 min 8 s.

Rewritten on 14 Nov 2026 to Matthew's direction (existential, not upbeat, a good deep house beat). The first
version was a vibraphone tune at 86; this one is the main theme's record heard from far away. It now shares
one tempo and key with every other piece, so the game can mix between them on any bar line.

At rest it is a pad and a few plucked notes with long gaps: the Exploration theme at half speed, a small figure
in a large empty place. Interest brings in dub chords and the bass, then a soft four-on-the-floor. Unease adds a
low pulse and a held minor second, without changing the beat.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, synth
from oa_audio.seq import Note, bars, humanize, progression

from . import deep
from .common import balance, calibrate, comp, held, stem

ID = "explore_regolith"
BPM = deep.BPM
BARS = 64

META = dict(
    title="Regolith Lullaby", context="surface", bpm=BPM, beatsPerBar=4, bars=BARS, key="D minor",
    motifs=["exploration (half speed)", "orbital_ante (cell, bars 45-46)"], entryBars=[1, 17, 33, 49],
    stems=[
        dict(id="pad", lo=0, hi=59, fadeInBars=2, fadeOutBars=2),
        dict(id="pluck", lo=0, hi=59, fadeInBars=1, fadeOutBars=2),
        dict(id="keys", lo=20, hi=59, fadeInBars=2, fadeOutBars=2),
        dict(id="bass", lo=20, hi=59, fadeInBars=1, fadeOutBars=2),
        dict(id="perc", lo=30, hi=59, fadeInBars=2, fadeOutBars=1),
        dict(id="tension", lo=40, hi=59, fadeInBars=2, fadeOutBars=2),
    ],
)

SEC_A = [("Dm9", 16), ("Bbmaj9", 16)]                               # two chords, a long time on each
SEC_B = [("Gm9", 16), ("Dm9", 8), ("Am7", 8)]
SEC_C = [("Bbmaj9", 16), ("Gm9", 8), ("Am7", 8)]

# The Exploration theme (D A, G, C B G, A) with every note twice as long and B flattened, then a rest as long again.
THEME = ["D4:4", "A4:4", "G4:4", "r:4", "C5:3 Bb4:1", "G4:4", "A4:4", "r:4"]
ANSWER = ["r:4", "r:4", "r:2 F4:2", "A4:4", "r:4", "r:4", "r:2 E4:2", "D4:4"]
HIGH = ["r:4", "A4:2 D5:2", "C5:4", "r:4", "r:4", "G4:2 C5:2", "A4:4", "r:4"]
CELL = ["r:4", "r:4", "r:4", "r:4", "r:.5 A4:.5 C5:.5 D5:2.5", "r:4", "r:4", "r:4"]   # the ante cell, once

BASS_1 = [(0.0, 1.5, 0, 96), (2.5, .4, 0, 84)]
BASS_2 = [(0.0, 1.5, 0, 96), (2.5, .4, 0, 84), (3.5, .4, 7, 78)]


def drum_bar(b: int) -> dict:
    last = b % 16 == 15
    return dict(kick="x...x...x...x..." if not last else "x...x...x.......",
                hat="" if b % 16 < 8 else "..x...x...x...x.",
                shut="..o...o...o..xo." if b % 2 == 0 else "..o..xo...o...o.",
                clap="", rim="....x.......x..." if b % 4 != 3 else "....x.....x..x..")


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=8602, tail=9.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(SEC_A + SEC_B + SEC_A + SEC_C)
    out = {}
    kick_n, k = deep.kit(L, rng, drum_bar, "perc", hard=0.0)
    duck = L.duck(kick_n, -5.0, 0.004, 0.22)

    # ---------------------------------------------------------------- pad (always there)
    low = L.synth("pad_low", held(prog, low=45, count=3, vel=56, top=62), synth.v_pad, bright=0.6, attack=2.0, release=3.0)
    high = L.synth("pad_high", held(prog, low=57, count=4, vel=50, rootless=True, top=76), synth.v_pad, bright=0.9, attack=2.5, release=3.0)
    sweep = deep.breathing(L, 2.0, 700, 3400)

    def pad_fx(a, b):
        x = dsp.svf(a + b * 0.8, sweep(len(a)), 0.8, "lp", 2)
        x = dsp.chorus(x, L.rate(0.13), 5.0, 16.0, 0.5)
        return dsp.reverb(x, 6.0, 0.5, 0.6, 0.04, 1.8, seed=861)

    out["pad"] = stem(L, L.fx(pad_fx, low, high), 0.0, hp=90)

    # ---------------------------------------------------------------- pluck: the theme, far away, with long gaps
    pl_n = (bars(THEME, 4, 0, vel=84) + bars(ANSWER, 4, 8, vel=74) + bars(HIGH, 4, 16, vel=80) + bars(ANSWER, 4, 24, vel=72)
            + bars(THEME, 4, 32, vel=86) + bars(CELL, 4, 40, vel=78) + bars(HIGH, 4, 48, vel=82) + bars(ANSWER, 4, 56, vel=74))
    pl_n = humanize(pl_n, rng, 0.012, 6, BPM)
    pluck = L.synth("pluck_string", pl_n, synth.v_string_pluck, bright=0.5)
    soft = L.synth("pluck_tine", pl_n, synth.v_tine, release=0.9)

    def pluck_fx(p, t):
        x = dsp.lowpass(p * 0.9 + t * 0.6, 5200)
        x = dsp.delay(x, L.beat * 1.5, L.beat * 3.0, 0.55, 0.45, 2400)
        return dsp.reverb(x, 6.5, 0.55, 0.55, 0.05, 1.8, seed=862)

    out["pluck"] = stem(L, L.fx(pluck_fx, pluck, soft), 0.0, hp=180)

    # ---------------------------------------------------------------- keys: dub chords on the off-beat
    def stab_rhythm(b):
        return [(1.5, .3, 0), (3.75, .25, -10)] if b % 2 == 0 else [(2.5, .3, -4)]

    keys = L.synth("keys_stab", humanize(comp(prog, stab_rhythm, low=57, count=4, vel=82, top=74), rng, 0.005, 5, BPM),
                   synth.v_stab, bright=0.5, release=0.25)
    k_sweep = deep.breathing(L, 4.0, 600, 2800)

    def keys_fx(s):
        s = dsp.svf(s, k_sweep(len(s)), 1.3, "lp", 2)
        s = dsp.delay(s, L.beat * 0.75, L.beat * 1.5, 0.6, 0.55, 2200)
        return dsp.reverb(s, 4.0, 0.4, 0.55, 0.02, 1.6, seed=863)

    out["keys"] = stem(L, L.fx(keys_fx, keys) * duck, 0.0, hp=160)

    # ---------------------------------------------------------------- bass: long, low, unhurried
    b_notes = humanize(deep.bass_notes(L, prog, lambda b: BASS_2 if b % 4 == 3 else BASS_1), rng, 0.003, 4, BPM)
    sub = L.synth("bass_sub", b_notes, synth.v_sub, release=0.2, drive=1.3)
    body = L.synth("bass_body", b_notes, synth.v_pulse, release=0.15)
    out["bass"] = stem(L, L.fx(deep.bass_fx(420, 0.5, 1.3), sub, body) * duck, 0.0, hp=34, lp=4000)

    # ---------------------------------------------------------------- perc: a soft four-on-the-floor heard through a wall
    def perc_fx(*parts):
        return deep.kit_fx(L, 864, tops_gain=2.1)(*parts)

    out["perc"] = stem(L, L.fx(perc_fx, k["kick"], k["ohat"], k["chat"], k["clap"], k["rim"], k["ride"]), 0.0, hp=30)

    # ---------------------------------------------------------------- tension: a pulse, and a note that should not be there
    pulse_n = [Note(b * 4.0 + s * 0.5, 0.3, 38, 70 + 14 * (s % 4 == 0)) for b in range(BARS) for s in range(8)]
    pulse = L.synth("tension_pulse", pulse_n, synth.v_pulse, release=0.05)
    rub_n = [Note(b * 4.0, 15.0, 63 if (b // 4) % 2 == 0 else 68, 46) for b in range(0, BARS, 4)]     # Eb, then Ab
    rub = L.synth("tension_rub", rub_n, synth.v_glass, attack=3.0, release=4.0, shimmer=0.4)

    def tension_fx(p, r):
        p = dsp.svf(p, 320, 1.4, "lp", 2)
        t = np.arange(len(r)) / dsp.SR
        r = r * (0.7 + 0.3 * np.sin(dsp.TAU * L.rate(5.0) * t))[:, None]
        return dsp.saturate(p, 1.6, 0.7, 0.03) * 0.9 + dsp.reverb(dsp.lowpass(r, 2400), 5.0, 0.5, 0.5, 0.04, 1.6, seed=865) * 0.8

    out["tension"] = stem(L, L.fx(tension_fx, pulse, rub), 0.0, hp=60)

    out = balance(out, dict(pad=-21.5, pluck=-24.0, keys=-23.5, bass=-21.0, perc=-20.5, tension=-25.0))
    return calibrate(out, target_lufs=-17.5), L
