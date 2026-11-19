"""Burn Window: the music of the solar system (flying). 126 BPM deep house, D minor, 64 bars, about 2 min 2 s.

Matthew, 16 Nov 2026: the music on entering the solar system was "too upbeat and friendly and goofy"; he wants
"an interesting deep house music that keeps the player engaged and has a good deep house beat and bass beat".
(What he heard in the game was still the first version, 124 BPM disco in D major.) So this is dark and
hypnotic: no crowd shouts, no bongos, nothing bright.

Built like deep house that keeps you listening for ten minutes: one minor-ninth stab on the off-beat, the same
every bar, sent into a long dub echo while a filter slowly opens and closes over sixteen bars, so it is never
quite the same twice. Under it, a deep rolling sub (Faust, through a Moog ladder) that breathes with its own
filter, and a solid four-on-the-floor with swung hats. The harmony keeps leaving home: D minor, then B flat,
then the dark E flat (the flat second), and it never resolves. The Orbital Ante theme is here low and far
off, four bars on, four off. A hard burn adds a ride, a quiet acid line and swells.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, studio, synth
from oa_audio.sampler import DR
from oa_audio.seq import Note, bars, chord_at, humanize, progression, shift, voicing

from . import deep
from .common import balance, bars_of, calibrate, comp, held, root_of, stem

ID = "flight_burn_window"
BPM = deep.BPM
BARS = 64

META = dict(
    title="Burn Window", context="flight", bpm=BPM, beatsPerBar=4, bars=BARS, key="D minor",
    motifs=["orbital_ante (in the minor, low)"], entryBars=[1, 17, 41],
    stems=[
        dict(id="chords", lo=0, hi=100, fadeInBars=2, fadeOutBars=4),
        dict(id="bass", lo=12, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="drums", lo=24, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="lead", lo=22, hi=100, fadeInBars=2, fadeOutBars=2),
        dict(id="top", lo=50, hi=100, fadeInBars=2, fadeOutBars=4),
    ],
)

SEC_A = [("Dm9", 16), ("Bbmaj7", 8), ("Am7", 8)]
SEC_B = [("Gm9", 8), ("Dm9", 8), ("Ebmaj7", 8), ("Am7", 8)]        # E flat: the flat second, the darkest move in D minor
SEC_BREAK = [("Bbmaj7", 16), ("Am7", 16)]

# The Orbital Ante theme in the minor, an octave lower than before: the same shape, further away.
THEME = ["r:.5 A4:.5 C5:.5 D5:1.5 r:1", "F5:.75 E5:.75 C5:.5 A4:2", "r:.5 C5:.5 D5:.5 E5:1.5 r:1", "F5:.5 E5:.5 D5:1 C5:.5 D5:1.5",
         "r:4", "r:4", "r:4", "r:4"]
LINE_B = ["A4:4", "G4:3 F4:1", "E4:4", "r:4", "F4:4", "Eb4:2 D4:2", "C4:4", "r:4"]
LINE_BREAK = ["r:.5 A4:.5 C5:.5 D5:2.5", "r:4", "r:4", "r:4", "r:.5 C5:.5 D5:.5 Eb5:2.5", "r:4", "r:4", "r:4"]

# deep bass: off the beat for the first eight bars, then rolling for the rest of the piece
DEEP_ROLL = [(0.25, .2, 0, 84), (0.5, .3, 0, 112), (1.25, .2, 0, 84), (1.5, .3, 0, 108), (1.75, .15, 12, 76),
             (2.25, .2, 0, 84), (2.5, .3, 0, 112), (3.25, .2, 0, 84), (3.5, .3, 0, 108), (3.75, .15, -2, 86)]
DEEP_TURN = DEEP_ROLL[:7] + [(3.0, .2, 3, 92), (3.5, .3, 5, 104), (3.75, .15, 7, 92)]


def in_break(b: int) -> bool:
    return 32 <= b < 40


def drum_bar(b: int) -> dict:
    if in_break(b):
        return dict(kick="x..............." if b == 32 else "x...x...x...x..." if b >= 38 else "",
                    hat="..x...x...x...x." if b >= 36 else "", shut="o.oxo.oxo.oxo.ox" if b >= 34 else "", clap="",
                    rim="...x..x...x....." if b >= 34 else "")
    return dict(kick="x...x...x...x...", hat="..x...x...x...x.",
                shut="o.oxo.oxo.oxo.ox" if b % 8 != 7 else "o.oxo.oxo.oxoxox",
                clap="....x.......x...",
                # a three-against-four rim figure: the thing your ear keeps following
                rim="...x..x..x..x..." if b % 2 else "x..x..x...x..x..")


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=12403, tail=9.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(SEC_A + SEC_A + SEC_B + SEC_B + SEC_BREAK + SEC_A + SEC_A + SEC_B)
    out = {}
    kick_n, k = deep.kit(L, rng, drum_bar, "drums", hard=0.4)
    duck = L.duck(kick_n, -9.0, 0.004, 0.26)
    duck_soft = L.duck(kick_n, -4.5, 0.004, 0.2)

    # ---------------------------------------------------------------- chords: one stab, a long echo, a slow filter
    def stab_rhythm(b):
        if in_break(b):
            return [(0.0, 3.5, -10)] if b % 4 == 0 else []
        return [(1.5, .22, 0)] if b % 4 != 3 else [(1.5, .22, 0), (3.75, .2, -12)]

    stabs = deep.faust(L, "chords_stab", humanize(comp(prog, stab_rhythm, low=53, count=4, vel=100, top=69), rng, 0.002, 4, BPM),
                       studio.STAB, voices=16, cutoff=700, envamt=2600, res=0.55, decay=0.2, release=0.15)
    pad = deep.faust(L, "chords_pad", held(prog, low=45, count=4, vel=56, top=62), studio.PAD, cutoff=800, attack=2.0, release=3.0)
    sweep = deep.breathing(L, 4.0, 450, 3400)

    def chords_fx(s, p):
        s = dsp.svf(s, sweep(len(s)), 1.8, "lp", 2)
        s = s + dsp.delay(s, L.beat * 0.75, L.beat * 1.5, 0.62, 1.0, 2200) * 0.55     # dub echo, dotted
        s = s + studio.plate(s, 0.22, 0.02, 0.7) * 0.5
        p = dsp.chorus(p, L.rate(0.13), 5.0, 16.0, 0.5)
        return s + (p + studio.hall(p, 0.4) * 0.7) * 0.7

    out["chords"] = stem(L, L.fx(chords_fx, stabs * duck_soft, pad * duck), 0.0, hp=120)

    # ---------------------------------------------------------------- bass: deep, rolling, breathing
    def riff(b):
        if b < 8:
            return deep.OFFBEAT_TURN if b % 4 == 3 else deep.OFFBEAT
        return DEEP_TURN if b % 4 == 3 else DEEP_ROLL

    b_notes = humanize(deep.bass_notes(L, prog, riff, in_break), rng, 0.002, 4, BPM)
    low = deep.bass(L, "bass_sub", b_notes, weight=1.35, cutoff=170, envamt=1000, drive=2.6, decay=0.14, res=0.62)
    b_sweep = deep.breathing(L, 8.0, 260, 1500)
    bfx = deep.bass_fx(-8.5, 0.06)

    def bass_fx(x):
        sub = dsp.lowpass(x, 150)
        top = dsp.svf(x - sub, b_sweep(len(x)), 1.4, "lp", 2)     # the growl opens and closes every eight bars
        return bfx(sub + top)

    out["bass"] = stem(L, L.fx(bass_fx, low) * duck_soft, 0.0, hp=30, lp=5000)

    # ---------------------------------------------------------------- drums
    out["drums"] = stem(L, L.fx(deep.kit_fx(L, 1243, tops_gain=2.1), *deep.kit_parts(k)), 0.0, hp=30)

    # ---------------------------------------------------------------- lead: the theme, low and far off
    lead_n = (bars(THEME, 4, 8, vel=90) + bars(LINE_B, 4, 16, vel=88) + bars(LINE_B, 4, 24, vel=92)
              + bars(LINE_BREAK, 4, 32, vel=80) + bars(THEME, 4, 40, vel=92) + bars(THEME, 4, 48, vel=94) + bars(LINE_B, 4, 56, vel=90))
    lead_n = humanize(shift(lead_n, 0, -12, 1.0), rng, 0.008, 6, BPM)
    lead = deep.faust(L, "lead_synth", lead_n, studio.LEAD, voices=6, cutoff=1500, vib=0.003, release=0.6)

    def lead_fx(a):
        x = dsp.delay(a, L.beat * 1.5, L.beat * 2.0, 0.5, 0.4, 2400)
        return x + studio.hall(x, 0.35, 0.06) * 0.8

    out["lead"] = stem(L, L.fx(lead_fx, lead) * duck_soft, 0.0, hp=200)

    # ---------------------------------------------------------------- top (a hard burn): ride, shaker, a quiet acid line, swells
    ride_n = bars_of(lambda b: "" if in_break(b) else "..x...x...x...x.", BARS, DR["ride"], vel=62)
    ride = L.hits("top_ride", ride_n, lambda n, r: synth.cymbal(n.vel / 127, 0.3, 1.2, r) * 0.5, pan=-0.3)
    shaker_n = bars_of(lambda b: "" if in_break(b) else "o.xoo.xoo.xoo.xo", BARS, DR["shaker"], vel=70, ghost=42)
    shaker = L.hits("top_shaker", humanize(shaker_n, rng, 0.004, 8, BPM), lambda n, r: synth.shaker(n.vel / 127, 0.06, r), pan=0.4)
    acid_n = []
    for b in range(BARS):
        if in_break(b):
            continue
        r = root_of(chord_at(prog, b * 4.0), 48)
        for st, iv in enumerate([0, 0, 12, 0, 0, 3, 0, 10, 0, 0, 12, 0, 7, 0, 3, 0]):
            if st % 3 == 1:
                continue
            acid_n.append(Note(b * 4.0 + st * 0.25, 0.18, r + iv, 92 if st % 4 == 2 else 70))
    acid = L.synth("top_acid", acid_n, synth.v_acid, release=0.05, res=3.6, env_amt=0.8)
    a_sweep = deep.breathing(L, 2.0, 300, 1600)
    sw = deep.swells(L, [(16, 2), (32, 2), (40, 4), (56, 2), (64, 2)], 1240, 250, 5000)

    def top_fx(r, s, a, w):
        a = dsp.svf(dsp.highpass(a, 180), a_sweep(len(a)), 1.6, "lp", 2)
        a = a + dsp.delay(a, L.beat * 0.75, L.beat * 1.5, 0.4, 1.0, 2500) * 0.3
        return dsp.highpass(r, 2500) * 0.8 + dsp.highpass(s, 3000) * 0.7 + a * 0.8 + dsp.reverb(w, 3.0, 0.4, 0.5, 0.02, 1.5, seed=1246)

    out["top"] = stem(L, L.fx(top_fx, ride, shaker, acid * duck_soft, sw), 0.0, hp=200)

    out = balance(out, dict(chords=-21.5, bass=-19.5, drums=-19.0, lead=-25.5, top=-26.0))
    return calibrate(out, target_lufs=-15.5), L
