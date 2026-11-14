"""What the Dish Heard: discovery and cosmic mystery. 120 BPM felt at half speed, around D, 48 bars, 1 min 36 s.

Reworked on 14 Nov 2026 to sit in the same record as everything else (Matthew: existential, a good deep house
beat, fewer bells). It keeps its own strange harmony, but it now lives on the shared 120 grid, so the game can
mix into and out of it on a bar line, and it is fully synthesised.

Almost no rhythm: a drone, slow soft tones carrying the Cosmic Mystery theme, a choir of glass, and (only when
things turn tense) the kick of the other pieces heard through a wall, with something that might be a signal.
Its last bars quote the Exploration theme, so it leads back to the surface music.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, motifs, synth
from oa_audio.sampler import DR
from oa_audio.seq import Note, bars, progression, shift

from .common import balance, bars_of, calibrate, held, root_of, stem

ID = "mystery_dish"
BPM = 120.0
BARS = 48

META = dict(
    title="What the Dish Heard", context="mystery", bpm=BPM, beatsPerBar=4, bars=BARS, key="D Lydian",
    motifs=["cosmic_mystery", "exploration (last bars)"], entryBars=[1, 33],
    stems=[
        dict(id="drone", lo=0, hi=100, fadeInBars=4, fadeOutBars=4),
        dict(id="tones", lo=0, hi=100, fadeInBars=2, fadeOutBars=4),
        dict(id="choir", lo=25, hi=100, fadeInBars=4, fadeOutBars=4),
        dict(id="pulse", lo=45, hi=100, fadeInBars=2, fadeOutBars=2),
    ],
)

PROG = [("Dmaj7#11", 32), ("Bbmaj7#5/D", 32), ("F#madd9/C#", 32), ("Eadd9/D", 32), ("Dmaj7#11", 32), ("Cmaj7#11", 32)]

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
    slow = lambda notes: [Note(n.start * 2, n.dur * 2, n.pitch, n.vel) for n in notes]   # written at 60, played on the 120 grid
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
    sweep = 500 + 350 * L.lfo(1 / 48.0)
    air = dsp.svf(np.concatenate([air, air]), np.concatenate([sweep, sweep]), 3.0, "bp")[L.n:] * 0.5
    k = np.linspace(0, 1, dsp.secs(2.0))[:, None]
    air[: len(k)] *= k  # the air breathes out and back in across the loop point
    air[-len(k):] *= k[::-1]

    def drone_fx(lo, g, a):
        return lo * 0.3 + dsp.reverb(dsp.chorus(g, L.rate(0.12), 5.0, 18.0, 0.5) + a * 0.25, 7.0, 0.55, 0.5, 0.05, 2.0, seed=501)

    out["drone"] = stem(L, L.fx(drone_fx, low, glass, air), 0.0, hp=30)

    # ---------------------------------------------------------------- tones: the Cosmic Mystery theme, soft and far off
    tone_n = slow(bars(BELLS, 4, 0, vel=82))
    glassy = L.synth("tones_glass", tone_n, synth.v_glass, attack=0.25, release=3.5, shimmer=0.5)
    whistle = L.synth("tones_whistle", [n for n in tone_n if n.dur >= 4], synth.v_whistle, release=1.5)
    string_n = [n for n in tone_n if n.start >= 184] + shift([n for n in tone_n if n.start < 32], 0, -12, 0.7)
    string = L.synth("tones_string", string_n, synth.v_string_pluck, bright=0.5)

    def tones_fx(g, w, p):
        x = dsp.lowpass(g, 4200) * 0.9 + w * 0.35 + p * 0.9
        x = dsp.delay(x, L.beat * 1.5, L.beat * 2.5, 0.45, 0.35, 2800)
        return dsp.reverb(x, 8.0, 0.6, 0.45, 0.06, 2.2, seed=502)

    out["tones"] = stem(L, L.fx(tones_fx, glassy, whistle, string), 0.0, hp=180)

    # ---------------------------------------------------------------- choir of glass
    ch_n = held(prog, low=60, count=4, vel=58, rootless=True, overlap=2.0, top=79)
    choir = L.synth("choir_pad", ch_n, synth.v_pad, bright=0.8, attack=3.0, release=4.0)
    gl = L.synth("choir_glass", shift(ch_n, 0, 12, 0.8), synth.v_glass, attack=4.0, release=5.0)
    swell = (0.55 + 0.45 * L.lfo(1 / 32.0, -0.25))[:, None]  # one slow breath per chord

    def choir_fx(c, g):
        return dsp.reverb(dsp.chorus(dsp.lowpass(c, 3000), L.rate(0.17), 5.0, 16.0, 0.5) * 0.8 + g, 7.0, 0.6, 0.5, 0.05, 2.0, seed=503)

    out["choir"] = stem(L, L.fx(choir_fx, choir * swell, gl * swell), 0.0, hp=200)

    # ---------------------------------------------------------------- pulse (tension only): heartbeat, a signal, unease
    heart_n = bars_of(lambda b: "x...x...x...x...", BARS, DR["kick"], vel=92)      # the other pieces' kick, through a wall
    heart = L.hits("pulse_heart", heart_n, lambda n, r: synth.kick(n.vel / 127, 45, 150, 0.21, 0.1, 1.4, r))
    code = "x.xx...x ....x.x. ..xx.... x......."  # an irregular, almost-meaningful rhythm
    sig_n = bars_of(lambda b: code[((b // 2) % 4) * 9:((b // 2) % 4) * 9 + 8] if b % 4 == 0 else "", BARS, 93, step=0.5, vel=70)
    sig = L.synth("pulse_signal", sig_n, synth.v_tine, release=0.08)
    trem_n = []
    for s, d, c in prog:
        r = root_of(c, 72)
        trem_n += [Note(s + 16, 16.0, r + 6, 52), Note(s + 16, 16.0, r + 7, 48)]  # the sharpened fourth rubbing on the fifth
    trem = L.synth("pulse_tremolo", trem_n, synth.v_string_trem, rate=9.0, release=1.5)

    def pulse_fx(h, s, t):
        h = dsp.lowpass(h, 260) + dsp.bandpass(h, 500, 1100) * 0.5
        return h * 0.8 + dsp.reverb(dsp.delay(s * 0.5, L.beat * 0.75, L.beat * 1.5, 0.4, 0.4, 2500) + t * 0.8, 5.0, 0.5, 0.5, 0.04, 1.8, seed=504)

    out["pulse"] = stem(L, L.fx(pulse_fx, heart, sig, trem), 0.0, hp=35)

    out = balance(out, dict(drone=-25.0, tones=-24.0, choir=-26.0, pulse=-25.0))
    return calibrate(out, target_lufs=-20.5), L
