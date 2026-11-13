"""What the Dish Heard: discovery and cosmic mystery. 60 BPM, D Lydian, 24 bars, 1 min 36 s.

Almost no rhythm: a drone, slow bells carrying the Cosmic Mystery theme, a choir of glass, and (only when
things turn tense) a heartbeat and something that might be a signal. Its last bar quotes the Exploration
theme, so it leads back to the surface music.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, motifs, synth
from oa_audio.sampler import DR, GM
from oa_audio.seq import Note, bars, progression, shift

from .common import balance, bars_of, calibrate, held, root_of, stem

ID = "mystery_dish"
BPM = 60.0
BARS = 24

META = dict(
    title="What the Dish Heard", context="mystery", bpm=BPM, beatsPerBar=4, bars=BARS, key="D Lydian",
    motifs=["cosmic_mystery", "exploration (last bar)"], entryBars=[1, 17],
    stems=[
        dict(id="drone", lo=0, hi=100, fadeInBars=2, fadeOutBars=2),
        dict(id="bells", lo=0, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="choir", lo=25, hi=100, fadeInBars=2, fadeOutBars=2),
        dict(id="pulse", lo=45, hi=100, fadeInBars=1, fadeOutBars=1),
    ],
)

PROG = [("Dmaj7#11", 16), ("Bbmaj7#5/D", 16), ("F#madd9/C#", 16), ("Eadd9/D", 16), ("Dmaj7#11", 16), ("Cmaj7#11", 16)]

BELLS = motifs.COSMIC_MYSTERY + [
    "r:2 F#4:2", "Bb4:3 A4:1", "D5:4", "r:4",
    "C#4:4", "G#4:3 F#4:1", "C#5:2 A4:2", "G#4:4",
    "r:1 B4:3", "E5:3 D5:1", "G#5:4", "r:2 E5:2",
    "D5:4", "A5:3 G#5:1", "E6:4", "C#6:3 F#5:1",
    "r:2 E5:2", "F#5:3 G5:1", "B5:4", "D4:1.5 A4:1.5 G4:1",
]


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=6001, tail=12.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(PROG)
    out = {}

    # ---------------------------------------------------------------- drone
    low = L.synth("drone_low", [Note(s, d + 1.0, root_of(c, 36) if root_of(c, 36) < 43 else root_of(c, 24), 70) for s, d, c in prog],
                  synth.v_sub, release=2.0, drive=1.0)
    fifths = []
    for s, d, c in prog:
        r = root_of(c, 48)
        fifths += [Note(s, d + 0.5, r, 56), Note(s, d + 0.5, r + 7, 52), Note(s, d + 0.5, r + 12, 46), Note(s, d + 0.5, r + 19, 40)]
    glass = L.synth("drone_glass", fifths, synth.v_glass, attack=3.0, release=4.0, shimmer=0.6)
    air = dsp.stereo(dsp.pink(L.n, np.random.default_rng(61)))
    air[:, 1] = dsp.pink(L.n, np.random.default_rng(62))
    sweep = 500 + 350 * L.lfo(1 / 24.0)
    air = dsp.svf(np.concatenate([air, air]), np.concatenate([sweep, sweep]), 3.0, "bp")[L.n:] * 0.5
    k = np.linspace(0, 1, dsp.secs(2.0))[:, None]
    air[: len(k)] *= k  # the air breathes out and back in across the loop point
    air[-len(k):] *= k[::-1]

    def drone_fx(lo, g, a):
        return lo * 0.3 + dsp.reverb(dsp.chorus(g, L.rate(0.12), 5.0, 18.0, 0.5) + a * 0.25, 7.0, 0.55, 0.5, 0.05, 2.0, seed=501)

    out["drone"] = stem(L, L.fx(drone_fx, low, glass, air), 0.0, hp=30)

    # ---------------------------------------------------------------- bells: the Cosmic Mystery theme
    bell_n = bars(BELLS, 4, 0, vel=82)
    bells = L.synth("bells", bell_n, synth.v_bell, ratio=3.5, decay=3.6, bright=0.75)
    low_bells = L.synth("bells_low", shift([n for n in bell_n if n.dur >= 3], 0, -12, 0.7), synth.v_bell, ratio=1.41, decay=4.5, bright=0.5)
    cel = L.sf("bells_celesta", shift([n for n in bell_n if 64 <= n.start < 80], 0, 12, 0.8), GM["celesta"], gain=1.0)
    harp_n = [n for n in bell_n if n.start >= 92] + shift([n for n in bell_n if n.start < 16], 0, -12, 0.7)
    harp = L.sf("bells_harp", harp_n, GM["harp"], gain=1.0)

    def bells_fx(b, lb, c, h):
        x = b + lb * 0.6 + c * 0.6 + h * 0.8
        x = dsp.delay(x, L.beat * 0.75, L.beat * 1.25, 0.45, 0.35, 2800)
        return dsp.reverb(x, 8.0, 0.6, 0.45, 0.06, 2.2, seed=502)

    out["bells"] = stem(L, L.fx(bells_fx, bells, low_bells, cel, harp), 0.0, hp=180)

    # ---------------------------------------------------------------- choir of glass
    ch_n = held(prog, low=60, count=4, vel=58, rootless=True, overlap=1.0, top=79)
    choir = L.sf("choir_voices", ch_n, GM["choir"], gain=1.0)
    gl = L.synth("choir_glass", shift(ch_n, 0, 12, 0.8), synth.v_glass, attack=4.0, release=5.0)
    swell = (0.55 + 0.45 * L.lfo(1 / 16.0, -0.25))[:, None]  # one slow breath per chord

    def choir_fx(c, g):
        return dsp.reverb(dsp.lowpass(c, 3500) * 0.8 + g, 7.0, 0.6, 0.5, 0.05, 2.0, seed=503)

    out["choir"] = stem(L, L.fx(choir_fx, choir * swell, gl * swell), 0.0, hp=200)

    # ---------------------------------------------------------------- pulse (tension only): heartbeat, a signal, unease
    heart_n = bars_of(lambda b: "x.o.....x.o.....", BARS, DR["kick"], vel=96, ghost=64)
    heart = L.hits("pulse_heart", heart_n, lambda n, r: synth.kick(n.vel / 127, 40, 45, 0.32, 0.0, 1.0, r))
    code = "x.xx...x ....x.x. ..xx.... x......."  # an irregular, almost-meaningful rhythm
    sig_n = bars_of(lambda b: code[(b % 4) * 9:(b % 4) * 9 + 8] if b % 2 == 0 else "", BARS, 93, step=0.5, vel=70)
    sig = L.synth("pulse_signal", sig_n, synth.v_bell, ratio=1.0, decay=0.12, bright=0.2)
    trem_n = []
    for s, d, c in prog:
        r = root_of(c, 72)
        trem_n += [Note(s + 8, 8.0, r + 6, 52), Note(s + 8, 8.0, r + 7, 48)]  # the sharpened fourth rubbing on the fifth
    trem = L.synth("pulse_tremolo", trem_n, synth.v_string_trem, rate=9.0, release=1.5)

    def pulse_fx(h, s, t):
        return h * 0.8 + dsp.reverb(dsp.delay(s * 0.5, L.beat * 0.5, L.beat * 0.75, 0.4, 0.4, 2500) + t * 0.8, 5.0, 0.5, 0.5, 0.04, 1.8, seed=504)

    out["pulse"] = stem(L, L.fx(pulse_fx, heart, sig, trem), 0.0, hp=35)

    out = balance(out, dict(drone=-25.0, bells=-23.0, choir=-26.0, pulse=-25.0))
    return calibrate(out, target_lufs=-20.5), L
