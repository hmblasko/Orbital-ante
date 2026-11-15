"""Long Way From Anywhere: the main theme (menus, upgrades). 126 BPM deep house, D minor, 64 bars, about 2 min 2 s.

Moved to 126 on the shared kit with a deeper Faust sub bass and bongos on 15 Nov 2026 (Matthew: "more deep
house, deep bass, high bpm, kick step, rally house"). The melody, chords and mood are the ones he approved.

Asked for by Matthew on 14 Nov 2026: "more existential and not upbeat, but still have a good deep house beat".

So: a soft four-on-the-floor kick that never lets up, an off-beat sub bass, and over it almost nothing. Minor
ninth chords that move slowly and never quite come home, dub-delayed chord stabs that open and close like
breathing, and a lonely bell line that is silent for more than half the piece. The bell's notes are the game's own: the
ante cell in the minor (A, C, D) and the wide, slow intervals of the Cosmic Mystery theme.

Everything here is synthesised by this repository's own code. No sampled instruments: deep house is machine
music, so nothing in it is pretending to be a real player.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, studio, synth
from oa_audio.sampler import DR
from oa_audio.seq import Note, bars, chord_at, humanize, progression, shift, swing, voicing

from . import deep
from .common import balance, bars_of, calibrate, comp, glue, held, presence, root_of, stem

ID = "main_long_way"
BPM = deep.BPM
BARS = 64

META = dict(
    title="Long Way From Anywhere", context="menu", bpm=BPM, beatsPerBar=4, bars=BARS, key="D minor",
    motifs=["orbital_ante (cell, in the minor)", "cosmic_mystery (bars 17-32)"], entryBars=[1, 17, 41],
    stems=[
        dict(id="beat", lo=0, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="bass", lo=0, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="chords", lo=0, hi=100, fadeInBars=2, fadeOutBars=2),
        dict(id="lead", lo=0, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="air", lo=0, hi=100, fadeInBars=2, fadeOutBars=2),
    ],
)
# All five layers play from intensity 0: nothing in the menus raises intensity, and a theme with its melody
# switched off is not a theme. The piece does its own rising and falling (the breakdown at bar 33).

# Two bars a chord. Neither section ends on a chord that wants to resolve: it just turns over again.
SEC_A = [("Dm9", 8), ("Bbmaj9", 8), ("Gm9", 8), ("Am7", 8)]
SEC_B = [("Bbmaj9", 8), ("Am7", 8), ("Gm9", 8), ("Dm9", 8)]
SEC_BREAK = [("Gm9", 16), ("Bbmaj9", 8), ("Am7", 8)]

# The bell. Long notes, longer silences.
LEAD_A = ["r:4", "r:2 A4:1 C5:1", "D5:4", "r:4", "r:2 D5:1 F5:1", "E5:3 C5:1", "A4:4", "r:4"]
LEAD_A2 = ["r:2 A4:1 C5:1", "D5:3 F5:1", "E5:4", "r:2 C5:1 D5:1", "A5:3 G5:1", "E5:2 F5:2", "D5:4", "r:4"]
LEAD_B = ["D5:4", "A5:3 G5:1", "E5:4", "r:2 C5:2", "F5:4", "E5:2 D5:2", "A4:4", "r:4"]
LEAD_B2 = ["D5:4", "A5:3 G5:1", "C6:4", "r:2 A5:2", "F5:3 G5:1", "E5:2 C5:2", "D5:4", "r:4"]
LEAD_BREAK = ["A4:2 D5:2", "r:4", "C5:2 E5:2", "r:4", "A4:1 C5:1 D5:2", "r:4", "E5:4", "r:4"]

BASS_1 = [(0.5, .4, 0, 104), (1.5, .4, 0, 96), (2.75, .45, 0, 100), (3.5, .4, 12, 88)]
BASS_2 = [(0.5, .4, 0, 104), (1.5, .4, 0, 96), (2.5, .4, 7, 90), (3.25, .3, 10, 84), (3.75, .2, 12, 80)]


def in_break(b: int) -> bool:
    return 32 <= b < 40


def drum_bar(b: int) -> dict:
    if in_break(b):
        # the floor drops away: no kick, just the hats ticking and a clap far off
        return dict(kick="x..............." if b == 32 else "", hat="..x...x...x...x." if b >= 36 else "",
                    shut="" if b < 34 else "....o.......o...", clap="....x.......x..." if b >= 38 else "", rim="..........x.....",
                perc="..x..o.x..x..o.x" if b >= 38 else "")
    fill = b % 8 == 7
    return dict(kick="x...x...x...x..." if not fill else "x...x...x...x.x.", hat="..x...x...x...x.",
                perc="" if b < 8 else "..x..o.x..x..o.x" if b % 2 else "..x....x.ox..o..",
                shut="o.oxo.oxo.oxo.ox" if not fill else "o.oxo.oxo.oxoxox",
                clap="....x.......x...", rim=".......x..x....." if b % 2 else "..........x..x..")


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=12001, tail=8.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(SEC_A + SEC_A + SEC_B + SEC_B + SEC_BREAK + SEC_A + SEC_A + SEC_B)
    out = {}

    def lane(key, pitch, vel, **kw):
        return bars_of(lambda b: drum_bar(b)[key], BARS, pitch, vel=vel, **kw)

    kick_n, k = deep.kit(L, rng, drum_bar, "beat", hard=0.25)
    duck = L.duck(kick_n, -8.0, 0.004, 0.24)       # the pump that makes it house
    duck_soft = L.duck(kick_n, -4.0, 0.004, 0.18)

    # ---------------------------------------------------------------- beat: the shared club kit
    out["beat"] = stem(L, L.fx(deep.kit_fx(L, 1201), *deep.kit_parts(k)), 0.0, hp=30)

    # ---------------------------------------------------------------- bass: deep, off the beat, under everything
    b_notes = humanize(deep.bass_notes(L, prog, lambda b: BASS_2 if b % 4 == 3 else BASS_1, in_break), rng, 0.003, 4, BPM)
    low = deep.bass(L, "bass_sub", b_notes, weight=1.25, cutoff=220, envamt=1200, drive=2.4, decay=0.16)
    out["bass"] = stem(L, L.fx(deep.bass_fx(-9.5), low) * duck_soft, 0.0, hp=30, lp=5000)

    # ---------------------------------------------------------------- chords: breathing stabs over a held pad
    def stab_rhythm(b):
        if in_break(b):
            return [(0.0, 3.0, 0.7)] if b % 2 == 0 else []
        return [(0.5, .3, 1.0), (2.0, .25, .8), (3.25, .3, .9)] if b % 2 == 0 else [(0.5, .3, 1.0), (1.75, .25, .8), (3.0, .45, .95)]

    stab_n = humanize(comp(prog, stab_rhythm, low=57, count=4, vel=88, top=77), rng, 0.004, 5, BPM)
    stabs = L.synth("chords_stabs", stab_n, synth.v_stab, bright=0.55, release=0.2)
    pad = L.synth("chords_pad", held(prog, low=50, count=4, vel=58, top=69), synth.v_pad, bright=0.7, attack=1.6, release=2.4)

    def chords_fx(s, p):
        # the filter opens over sixteen bars and closes again: the only thing in the piece that is in a hurry
        # to get nowhere
        sweep = 700 + 2600 * (0.5 - 0.5 * np.cos(dsp.TAU * np.arange(len(s)) / (L.n / 4.0))) ** 1.5
        s = dsp.svf(s, sweep, 1.6, "lp", 2)
        s = dsp.delay(s, L.beat * 0.75, L.beat * 1.5, 0.52, 0.42, 2600)   # dub echo, dotted
        s = dsp.reverb(s, 3.2, 0.32, 0.55, 0.02, 1.5, seed=1202)
        p = dsp.chorus(dsp.lowpass(p, 2400), L.rate(0.21), 4.0, 14.0, 0.5)
        return s + dsp.reverb(p, 4.5, 0.4, 0.6, 0.03, 1.6, seed=1203) * 0.8

    out["chords"] = stem(L, L.fx(chords_fx, stabs, pad * duck), 0.0, hp=140)

    # ---------------------------------------------------------------- lead: one bell, a long way off
    # Matthew, 14 Nov: "less bells". The bell now stays out of half the piece: the opening sixteen bars and the
    # sixteen after the breakdown are groove and chords alone, so when it does speak it is an event.
    lead_n = (bars(LEAD_B, 4, 16, vel=90) + bars(LEAD_A, 4, 24, vel=88)
              + bars(LEAD_BREAK, 4, 32, vel=80) + bars(LEAD_B, 4, 56, vel=90))
    lead_n = humanize(lead_n, rng, 0.01, 6, BPM)
    bell = L.synth("lead_bell", lead_n, synth.v_bell, ratio=3.5, decay=2.0, bright=0.55)
    glass = L.synth("lead_glass", shift(lead_n, 0, 0, 0.8), synth.v_glass, attack=0.25, release=3.0, shimmer=0.7)
    # an answer an octave down, a bar late, the second time each section comes round: someone else, or an echo
    answer_n = shift([n for n in lead_n if 128 <= n.start < 156], 4.0, -12, 0.6)   # only in the breakdown now
    answer = L.synth("lead_answer", answer_n, synth.v_glass, attack=0.5, release=3.5, shimmer=0.4)

    def lead_fx(b, g, a):
        # more of the soft glass tone, less of the struck bell
        x = b * 0.5 + g * 0.9 + dsp.pan(dsp.mono(a), -0.4) * 0.6
        x = dsp.delay(x, L.beat * 1.5, L.beat * 2.0, 0.5, 0.35, 3200)
        return dsp.reverb(x, 5.0, 0.42, 0.5, 0.04, 1.8, seed=1204)

    out["lead"] = stem(L, L.fx(lead_fx, bell, glass, answer) * duck_soft, 0.0, hp=260)

    # ---------------------------------------------------------------- air (intensity 60+): shaker, shimmer and swells
    shaker_n = swing(bars_of(lambda b: "" if in_break(b) else "o.xoo.xoo.xoo.xo", BARS, DR["shaker"], vel=72, ghost=44), 0.55, 0.25)
    shaker = L.hits("air_shaker", humanize(shaker_n, rng, 0.004, 8, BPM), lambda n, r: synth.shaker(n.vel / 127, 0.06, r), pan=0.4)
    top_n = []
    for start, dur, ch in prog:
        v = voicing(ch, 74, 4, True, 93)
        top_n += [Note(start, dur, v[-1], 54)]
    shimmer = L.synth("air_shimmer", top_n, synth.v_glass, attack=2.0, release=4.0, shimmer=1.2)
    swells = np.zeros((L.n_render, 2))
    for at_bar, length_bars in [(16, 2), (32, 2), (40, 4), (56, 2), (64, 2)]:
        dsp.place(swells, synth.riser(length_bars * 4 * L.beat, np.random.default_rng(1200 + at_bar), 300, 6000),
                  dsp.secs((at_bar - length_bars) * 4 * L.beat), 0.4)
    swells = L.fold(swells)

    def air_fx(s, g, w):
        return dsp.highpass(s, 3000) * 0.8 + dsp.reverb(g, 5.0, 0.5, 0.5, 0.04, 1.8, seed=1205) + dsp.reverb(w, 3.0, 0.4, 0.5, 0.02, 1.5, seed=1206)

    out["air"] = stem(L, L.fx(air_fx, shaker, shimmer * duck, swells), 0.0, hp=350)

    out = balance(out, dict(beat=-19.5, bass=-20.5, chords=-21.5, lead=-26.5, air=-28.0))
    return calibrate(out, target_lufs=-16.5), L
