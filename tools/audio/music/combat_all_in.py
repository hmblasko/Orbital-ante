"""All In: combat. 120 BPM, D minor, 64 bars, 2 min 8 s. The same deep house record with its teeth out.

Rewritten on 14 Nov 2026 to Matthew's direction (existential, not upbeat, a good deep house beat). The first
version was 172 BPM breakbeat with brass. This one keeps the tempo and key of everything else, so a fight is
the record getting harder, not a different record: a heavier kick, a rolling growl of a bass, off-beat stabs,
and the Conflict theme on a hard lead once the fight is properly on. The top layer, for the worst moments,
is a squelching sixteenth-note line, a ride and noise.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, motifs, synth
from oa_audio.sampler import DR
from oa_audio.seq import Note, bars, chord_at, humanize, progression

from . import deep
from .common import balance, bars_of, calibrate, comp, presence, root_of, stem

ID = "combat_all_in"
BPM = deep.BPM
BARS = 64

META = dict(
    title="All In", context="combat", bpm=BPM, beatsPerBar=4, bars=BARS, key="D minor",
    motifs=["conflict", "orbital_ante (minor, bars 41-48)"], entryBars=[1, 17, 41],
    stems=[
        dict(id="drums", lo=0, hi=100, fadeInBars=0, fadeOutBars=2),
        dict(id="bass", lo=0, hi=100, fadeInBars=0, fadeOutBars=2),
        dict(id="stabs", lo=0, hi=100, fadeInBars=0, fadeOutBars=2),
        dict(id="lead", lo=60, hi=100, fadeInBars=2, fadeOutBars=4),
        dict(id="chaos", lo=80, hi=100, fadeInBars=2, fadeOutBars=4),
    ],
)

SEC_A = [("Dm9", 16), ("Bbmaj9", 8), ("Gm9", 8)]
SEC_B = [("Gm9", 8), ("Bbmaj9", 8), ("Dm9", 8), ("Am7", 8)]
SEC_BREAK = [("Bbmaj9", 16), ("Am7", 16)]

CF = motifs.CONFLICT
REST = ["r:4", "r:4", "r:4", "r:4"]
CF_UP = ["G5:.5 G5:.5 Ab5:.5 G5:1 F5:.5 D5:1", "Eb5:1.5 r:.5 Bb5:.5 A5:.5 G5:1", "A5:.5 A5:.5 Bb5:.5 A5:1 G5:.5 E5:1", "D5:4"]
ANTE_MINOR = ["r:.5 A4:.5 C5:.5 D5:1.5 r:1", "F5:.75 E5:.75 C5:.5 A4:2", "r:.5 C5:.5 D5:.5 F5:1.5 r:1", "G5:.5 F5:.5 D5:1 C5:.5 D5:1.5"]

# rolling sixteenths that leave the kick alone: (beat, length, semitones above the root, velocity)
ROLL_1 = [(0.5, .2, 0, 104), (0.75, .2, 0, 84), (1.5, .2, 0, 104), (1.75, .2, 12, 80), (2.5, .2, 0, 104), (2.75, .2, 0, 84), (3.5, .2, 0, 100), (3.75, .2, 10, 84)]
ROLL_2 = [(0.5, .2, 0, 104), (0.75, .2, 0, 84), (1.5, .2, 0, 104), (1.75, .2, 3, 84), (2.5, .2, 7, 96), (2.75, .2, 0, 84), (3.25, .2, 10, 90), (3.5, .2, 12, 96), (3.75, .2, 1, 88)]


def in_break(b: int) -> bool:
    return 32 <= b < 40


def drum_bar(b: int) -> dict:
    if in_break(b):
        return dict(kick="x..............." if b % 4 == 0 else "x...x...x...x.x." if b == 39 else "",
                    hat="", shut="o.oxo.oxo.oxo.ox" if b >= 36 else "", clap="....x.......x..." if b >= 36 else "",
                    rim="...x..x...x..x..")
    fill = b % 8 == 7
    return dict(kick="x...x...x...x..." if not fill else "x...x...x..xx.x.", hat="..x...x...x...x.",
                shut="oxoxoxoxoxoxoxox" if b % 2 else "o.oxo.oxoxoxo.ox",
                clap="....x.......x..." if not fill else "....x.......x.xx", rim="...x......x..x.." if b % 2 else ".......x.....x..")


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=17202, tail=7.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(SEC_A + SEC_A + SEC_B + SEC_B + SEC_BREAK + SEC_A + SEC_A + SEC_B)
    out = {}
    kick_n, k = deep.kit(L, rng, drum_bar, "drums", hard=1.0)
    duck = L.duck(kick_n, -7.0, 0.004, 0.2)
    duck_soft = L.duck(kick_n, -3.5, 0.004, 0.16)

    # ---------------------------------------------------------------- drums
    toms_n = [Note(b * 4.0 + 3.0 + i * 0.25, .2, 0, 100 + 4 * i) for b in range(15, BARS, 16) for i in range(4)]
    toms = L.hits("drums_toms", toms_n, lambda n, r: synth.tom(n.vel / 127, 150 - 22 * (round(n.start * 4) % 4), 0.2, r), pan=-0.1)
    crash_n = [Note(b * 4.0, 2, DR["crash"], 100) for b in (0, 16, 40, 56)]
    crash = L.hits("drums_crash", crash_n, lambda n, r: synth.cymbal(n.vel / 127, 1.4, 0.9, r) * 0.6, pan=0.2)
    kfx = deep.kit_fx(L, 1721, tops_gain=2.5)

    def drums_fx(kk, o, h, c, r, d, t, cr):
        return kfx(kk, o, h, c, r, d) + dsp.saturate(t, 1.6, 0.7, 0.03) * 1.4 + dsp.reverb(cr, 2.0, 0.3, 0.5, 0.02, 1.4, seed=1722) * 0.9

    out["drums"] = stem(L, L.fx(drums_fx, k["kick"], k["ohat"], k["chat"], k["clap"], k["rim"], k["ride"], toms, crash), 0.0, hp=30)

    # ---------------------------------------------------------------- bass: sub under a rolling growl
    b_notes = humanize(deep.bass_notes(L, prog, lambda b: ROLL_2 if b % 4 == 3 else ROLL_1, in_break), rng, 0.002, 4, BPM)
    sub = L.synth("bass_sub", b_notes, synth.v_sub, release=0.06, drive=1.8)
    growl = L.synth("bass_growl", b_notes, synth.v_reese, release=0.06, move=0.45, top=1.0)
    g_sweep = deep.breathing(L, 8.0, 500, 1900)

    def bass_fx(s, g):
        g = dsp.svf(g, g_sweep(len(g)), 1.6, "lp", 2)
        x = dsp.lowpass(s, 170) * 1.0 + dsp.highpass(g, 110) * 0.7
        x = dsp.compress(dsp.saturate(x, 2.2, 0.7, 0.05), -18, 4.0, 0.006, 0.12, makeup_db=3)
        return dsp.width(presence(x, level_db=-8.0), 0.15)

    out["bass"] = stem(L, L.fx(bass_fx, sub, growl) * duck_soft, 0.0, hp=34, lp=7000)

    # ---------------------------------------------------------------- stabs: off-beat minor chords through a dub echo
    def stab_rhythm(b):
        if in_break(b):
            return [(0.0, 3.5, -14)] if b % 2 == 0 else []
        return [(0.75, .2, 0), (1.5, .25, -6), (2.75, .2, -4), (3.5, .25, -8)] if b % 2 == 0 else [(0.75, .2, 0), (1.5, .25, -6), (3.25, .2, -6)]

    stabs = L.synth("stabs_chord", humanize(comp(prog, stab_rhythm, low=57, count=4, vel=96, top=76), rng, 0.004, 5, BPM),
                    synth.v_stab, bright=0.85, release=0.14)
    pad = L.synth("stabs_pad", [Note(s, d, root_of(ch, 48) + iv, 52) for s, d, ch in prog for iv in (0, 7)], synth.v_pad, bright=0.6, attack=0.8, release=1.5)
    s_sweep = deep.breathing(L, 4.0, 900, 3600)

    def stabs_fx(s, p):
        s = dsp.svf(s, s_sweep(len(s)), 1.8, "lp", 2)
        s = s + dsp.delay(s, L.beat * 0.75, L.beat * 1.5, 0.4, 1.0, 2600) * 0.35
        return dsp.reverb(s, 1.8, 0.2, 0.5, 0.012, 1.3, seed=1723) + dsp.lowpass(p, 1800) * 0.5

    out["stabs"] = stem(L, L.fx(stabs_fx, stabs * duck_soft, pad * duck), 0.0, hp=150)

    # ---------------------------------------------------------------- lead: the Conflict theme, four bars on, four off
    lead_n = (bars(CF + REST, 4, 0, vel=104) + bars(CF_UP + REST, 4, 8, vel=106) + bars(CF + REST, 4, 16, vel=106) + bars(CF_UP + CF, 4, 24, vel=110)
              + bars(ANTE_MINOR + REST, 4, 40, vel=104) + bars(CF + CF_UP, 4, 48, vel=110) + bars(CF + REST, 4, 56, vel=108))
    lead_n = humanize(lead_n, rng, 0.004, 5, BPM)
    hard = L.synth("lead_hard", lead_n, synth.v_lead_hard, release=0.12)
    soft = L.synth("lead_soft", lead_n, synth.v_lead, bright=0.6, vibrato=0.1, release=0.2)

    def lead_fx(h, s):
        x = dsp.lowpass(h, 4200) * 0.7 + s * 0.6
        x = dsp.saturate(x, 1.5, 0.75, 0.03)
        x = x + dsp.delay(x, L.beat * 0.75, L.beat * 1.5, 0.35, 1.0, 3000) * 0.3
        return dsp.reverb(x, 2.2, 0.22, 0.5, 0.02, 1.4, seed=1724)

    out["lead"] = stem(L, L.fx(lead_fx, hard, soft) * duck_soft, 0.0, hp=260)

    # ---------------------------------------------------------------- chaos: squelch, ride and noise
    acid_n = []
    for b in range(BARS):
        if in_break(b):
            continue
        r = root_of(chord_at(prog, b * 4.0), 48)
        for s, iv in enumerate([0, 0, 12, 0, 3, 0, 12, 10, 0, 0, 7, 0, 12, 0, 10, 13 if b % 4 == 3 else 15]):
            if (s + b) % 7 == 5:
                continue
            acid_n.append(Note(b * 4.0 + s * 0.25, 0.3 if s % 4 == 2 else 0.16, r + iv, 96 if s % 4 == 2 else 76))
    acid = L.synth("chaos_acid", acid_n, synth.v_acid, release=0.05, res=4.0, env_amt=1.2)
    ride_n = bars_of(lambda b: "" if in_break(b) else "x.x.x.x.x.x.x.x.", BARS, DR["ride"], vel=70)
    ride = L.hits("chaos_ride", ride_n, lambda n, r: synth.cymbal(n.vel / 127, 0.3, 1.2, r) * 0.5, pan=-0.3)
    sw = deep.swells(L, [(8, 2), (16, 4), (24, 2), (32, 2), (40, 4), (48, 2), (56, 2), (64, 4)], 1720, 400, 9000, 0.5)

    def chaos_fx(a, r, w):
        a = dsp.saturate(dsp.highpass(a, 300), 2.0, 0.7, 0.04)
        a = a + dsp.delay(a, L.beat * 0.75, L.beat * 0.5, 0.3, 1.0, 4000) * 0.25
        return a * 0.9 + dsp.highpass(r, 2500) * 0.9 + dsp.reverb(w, 2.5, 0.35, 0.5, 0.02, 1.5, seed=1725)

    out["chaos"] = stem(L, L.fx(chaos_fx, acid * duck_soft, ride, sw), 0.0, hp=280)

    out = balance(out, dict(drums=-18.5, bass=-19.5, stabs=-22.5, lead=-22.5, chaos=-25.0))
    return calibrate(out, target_lufs=-15.0), L
