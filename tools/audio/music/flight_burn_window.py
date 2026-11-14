"""Burn Window: spaceflight. 120 BPM deep house, D minor, 64 bars, 2 min 8 s.

Rewritten on 14 Nov 2026 to Matthew's direction (existential, not upbeat, a good deep house beat). The first
version was bright disco in D major; this one is the same record as the main theme with the engines lit.

Drifting, it is a pad and a slow arpeggio turning over like something in orbit. Thrust brings in the bass,
then the drums. The Orbital Ante theme is here in the minor on a soft synth, four bars of tune then four of
nothing. A hard burn adds a ride and high air.
"""
from __future__ import annotations

from oa_audio import dsp, mixing, synth
from oa_audio.sampler import DR
from oa_audio.seq import Note, bars, chord_at, humanize, progression, voicing

from . import deep
from .common import balance, bars_of, calibrate, held, stem

ID = "flight_burn_window"
BPM = deep.BPM
BARS = 64

META = dict(
    title="Burn Window", context="flight", bpm=BPM, beatsPerBar=4, bars=BARS, key="D minor",
    motifs=["orbital_ante (in the minor)"], entryBars=[1, 17, 41],
    stems=[
        dict(id="chords", lo=0, hi=100, fadeInBars=2, fadeOutBars=4),
        dict(id="bass", lo=12, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="drums", lo=24, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="lead", lo=22, hi=100, fadeInBars=2, fadeOutBars=2),
        dict(id="top", lo=50, hi=100, fadeInBars=2, fadeOutBars=4),
    ],
)

SEC_A = [("Dm9", 8), ("Fmaj9", 8), ("Gm9", 8), ("Bbmaj9", 8)]     # climbs, and lands somewhere that is not home
SEC_B = [("Gm9", 8), ("Am7", 8), ("Bbmaj9", 8), ("Cadd9", 8)]
SEC_BREAK = [("Bbmaj9", 16), ("Am7", 16)]

# The Orbital Ante theme with its thirds and sevenths flattened: the same shape, a long way from home.
THEME = ["r:.5 A4:.5 C5:.5 D5:1.5 r:1", "F5:.75 E5:.75 C5:.5 A4:2", "r:.5 C5:.5 D5:.5 E5:1.5 r:1", "F5:.5 E5:.5 D5:1 C5:.5 D5:1.5",
         "r:4", "r:4", "r:4", "r:4"]
LINE_B = ["A5:4", "G5:3 F5:1", "E5:4", "r:4", "F5:4", "E5:2 D5:2", "C5:4", "r:4"]
LINE_BREAK = ["r:.5 A4:.5 C5:.5 D5:2.5", "r:4", "r:4", "r:4", "r:.5 C5:.5 D5:.5 E5:2.5", "r:4", "r:4", "r:4"]

BASS_1 = [(0.5, .4, 0, 104), (1.5, .4, 0, 98), (2.5, .4, 0, 100), (3.5, .4, 12, 90)]
BASS_2 = [(0.5, .4, 0, 104), (1.5, .4, 0, 98), (2.5, .4, 7, 92), (3.0, .2, 0, 70), (3.5, .2, 10, 86), (3.75, .2, 12, 82)]


def in_break(b: int) -> bool:
    return 32 <= b < 40


def drum_bar(b: int) -> dict:
    if in_break(b):
        return dict(kick="x..............." if b == 32 else "x...x...x...x..." if b >= 38 else "",
                    hat="..x...x...x...x." if b >= 36 else "", shut="o.oxo.oxo.oxo.ox" if b >= 34 else "", clap="", rim="..........x.....")
    return dict(kick="x...x...x...x...", hat="..x...x...x...x.",
                shut="o.oxo.oxo.oxo.ox" if b % 8 != 7 else "o.oxo.oxo.oxoxox",
                clap="....x.......x...", rim="...x......x....." if b % 2 else "......x......x..")


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=12402, tail=8.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(SEC_A + SEC_A + SEC_B + SEC_B + SEC_BREAK + SEC_A + SEC_A + SEC_B)
    out = {}
    kick_n, k = deep.kit(L, rng, drum_bar, "drums", hard=0.35)
    duck = L.duck(kick_n, -8.0, 0.004, 0.24)
    duck_soft = L.duck(kick_n, -4.0, 0.004, 0.18)

    # ---------------------------------------------------------------- chords: a pad and something in orbit
    pad = L.synth("chords_pad", held(prog, low=50, count=4, vel=58, top=69), synth.v_pad, bright=0.75, attack=1.4, release=2.2)
    arp_n = []
    for b in range(BARS):
        v = voicing(chord_at(prog, b * 4.0), 62, 4, True, 81)
        order = [0, 2, 1, 3, 2, 1] if b % 2 == 0 else [3, 1, 2, 0, 1, 2]
        for step in range(16):
            if step % 8 in (3, 6) or (in_break(b) and step % 4):
                continue    # holes in the pattern: it turns over, it does not chatter
            arp_n.append(Note(b * 4.0 + step * 0.25, 0.22, v[order[step % 6] % len(v)], 70 + 12 * (step % 4 == 2)))
    arp = L.synth("chords_arp", humanize(arp_n, rng, 0.003, 5, BPM), synth.v_pluck, bright=0.7, decay=0.28, res=1.4)
    sweep = deep.breathing(L, 4.0, 600, 3200)

    def chords_fx(p, a):
        a = dsp.svf(a, sweep(len(a)), 1.5, "lp", 2)
        a = dsp.delay(a, L.beat * 0.75, L.beat * 1.5, 0.48, 0.4, 2800)
        a = dsp.reverb(a, 3.0, 0.3, 0.55, 0.02, 1.5, seed=1241)
        p = dsp.chorus(dsp.lowpass(p, 2600), L.rate(0.21), 4.0, 14.0, 0.5)
        return a * 0.9 + dsp.reverb(p, 4.5, 0.4, 0.6, 0.03, 1.6, seed=1242) * 0.9

    out["chords"] = stem(L, L.fx(chords_fx, pad * duck, arp * duck_soft), 0.0, hp=140)

    # ---------------------------------------------------------------- bass
    b_notes = humanize(deep.bass_notes(L, prog, lambda b: BASS_2 if b % 4 == 3 else BASS_1, in_break), rng, 0.003, 4, BPM)
    sub = L.synth("bass_sub", b_notes, synth.v_sub, release=0.1, drive=1.5)
    body = L.synth("bass_body", b_notes, synth.v_pulse, release=0.08)
    out["bass"] = stem(L, L.fx(deep.bass_fx(600, 0.6), sub, body) * duck_soft, 0.0, hp=34, lp=5000)

    # ---------------------------------------------------------------- drums
    out["drums"] = stem(L, L.fx(deep.kit_fx(L, 1243), k["kick"], k["ohat"], k["chat"], k["clap"], k["rim"], k["ride"]), 0.0, hp=30)

    # ---------------------------------------------------------------- lead: the theme, then the same length of silence
    lead_n = (bars(THEME, 4, 8, vel=92) + bars(LINE_B, 4, 16, vel=90) + bars(LINE_B, 4, 24, vel=94)
              + bars(LINE_BREAK, 4, 32, vel=80) + bars(THEME, 4, 40, vel=94) + bars(THEME, 4, 48, vel=96) + bars(LINE_B, 4, 56, vel=92))
    lead_n = humanize(lead_n, rng, 0.008, 6, BPM)
    lead = L.synth("lead_synth", lead_n, synth.v_lead, bright=0.45, vibrato=0.12, release=0.5)
    under = L.synth("lead_whistle", lead_n, synth.v_whistle, release=0.6)

    def lead_fx(a, w):
        x = dsp.lowpass(a, 3200) * 0.8 + w * 0.6
        x = dsp.delay(x, L.beat * 1.5, L.beat * 2.0, 0.5, 0.35, 3000)
        return dsp.reverb(x, 4.5, 0.4, 0.5, 0.04, 1.8, seed=1244)

    out["lead"] = stem(L, L.fx(lead_fx, lead, under) * duck_soft, 0.0, hp=240)

    # ---------------------------------------------------------------- top (a hard burn): ride, shaker, high air
    ride_n = bars_of(lambda b: "" if in_break(b) else "x.x.x.x.x.x.x.x.", BARS, DR["ride"], vel=64)
    ride = L.hits("top_ride", ride_n, lambda n, r: synth.cymbal(n.vel / 127, 0.3, 1.2, r) * 0.5, pan=-0.3)
    shaker_n = bars_of(lambda b: "" if in_break(b) else "o.xoo.xoo.xoo.xo", BARS, DR["shaker"], vel=74, ghost=44)
    shaker = L.hits("top_shaker", humanize(shaker_n, rng, 0.004, 8, BPM), lambda n, r: synth.shaker(n.vel / 127, 0.06, r), pan=0.4)
    air_n = [Note(start, dur, voicing(ch, 74, 4, True, 93)[-1], 54) for start, dur, ch in prog]
    air = L.synth("top_air", air_n, synth.v_pad, bright=1.3, attack=2.0, release=3.0)
    sw = deep.swells(L, [(16, 2), (32, 2), (40, 4), (56, 2), (64, 2)], 1240)

    def top_fx(r, s, a, w):
        return dsp.highpass(r, 2500) * 0.8 + dsp.highpass(s, 3000) * 0.8 + dsp.reverb(dsp.highpass(a, 900), 5.0, 0.5, 0.5, 0.04, 1.8, seed=1245) \
            + dsp.reverb(w, 3.0, 0.4, 0.5, 0.02, 1.5, seed=1246)

    out["top"] = stem(L, L.fx(top_fx, ride, shaker, air * duck, sw), 0.0, hp=350)

    out = balance(out, dict(chords=-22.0, bass=-20.5, drums=-19.5, lead=-24.5, top=-26.5))
    return calibrate(out, target_lufs=-16.0), L
