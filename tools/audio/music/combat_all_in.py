"""All In: combat. 172 BPM, D minor, 64 bars (A, B, breakdown, A', turnaround), about 1 min 29 s.

Breakbeat drums, slap bass doubled by a growling synth, brass stabs, and the Conflict theme on a hard lead.
In the third section the Orbital Ante theme arrives in the minor and trades phrases with the Conflict theme.
The tempo is exactly twice the exploration piece's, so they hand over on a shared bar line.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, motifs, synth
from oa_audio.sampler import DR, GM, KITS
from oa_audio.seq import Note, bars, chord_at, humanize, progression, shift, voicing

from .common import balance, bars_of, calibrate, comp, glue, pattern, root_of, stem, presence

ID = "combat_all_in"
BPM = 172.0
BARS = 64

META = dict(
    title="All In", context="combat", bpm=BPM, beatsPerBar=4, bars=BARS, key="D minor",
    motifs=["conflict", "orbital_ante (minor, bars 41-56)"], entryBars=[1, 41],
    stems=[
        dict(id="drums", lo=0, hi=100, fadeInBars=0, fadeOutBars=2),
        dict(id="bass", lo=0, hi=100, fadeInBars=0, fadeOutBars=2),
        dict(id="stabs", lo=0, hi=100, fadeInBars=0, fadeOutBars=2),
        dict(id="lead", lo=60, hi=100, fadeInBars=2, fadeOutBars=4),
        dict(id="chaos", lo=80, hi=100, fadeInBars=2, fadeOutBars=4),
    ],
)

SEC_A = [("Dm9", 16), ("Bbmaj9", 8), ("C9", 8), ("Dm9", 16), ("Gm9", 8), ("A7#9", 8)]
SEC_B = [("Fmaj9", 8), ("Cadd9/E", 8), ("Dm9", 8), ("Bbmaj9", 8), ("Gm9", 8), ("Ebmaj7#11", 8), ("A7sus4", 8), ("A7#9", 8)]
SEC_C = [("Bbmaj7", 16), ("A7b9", 16)]
SEC_D = [("Gm9", 8), ("Ebmaj7#11", 8), ("Dm9/A", 8), ("A7#9", 8)]

CF = motifs.CONFLICT
LEAD = {
    9: CF,
    13: ["G5:.5 G5:.5 Ab5:.5 G5:1 F5:.5 D5:1", "Eb5:1.5 r:.5 Bb5:.5 A5:.5 G5:1",
         "A5:.5 A5:.5 Bb5:.5 A5:1 G5:.5 E5:1", "C#5:1 E5:.5 G5:.5 Bb5:2"],
    17: ["A5:2 C6:1 A5:1", "G5:1.5 F5:.5 E5:2", "G5:2 E5:1 G5:1", "C6:3 r:1"],
    21: [CF[0], CF[1], "D5:.5 D5:.5 Eb5:.5 D5:1 Bb4:.5 F5:1", "D5:3 r:1"],
    25: ["Bb5:1 A5:.5 G5:.5 D5:2", "G5:.5 G5:.5 Ab5:.5 G5:1 F5:.5 D5:1", "Bb5:2 G5:1 Eb5:1", "A5:3 r:1"],
    29: ["D5:1 E5:1 G5:1 A5:1", "D6:4", "C#6:1.5 r:.5 Bb5:.5 A5:.5 G5:1", "E5:.5 E5:.5 F5:.5 E5:1 C#5:.5 A4:1"],
    # the Orbital Ante theme, bent into the minor
    41: ["r:.5 A4:.5 C5:.5 D5:1.5 r:1", "F5:.75 E5:.75 C5:.5 A4:2", "r:.5 C5:.5 D5:.5 F5:1.5 r:1", "G5:.5 F5:.5 D5:1 C5:.5 D5:1.5"],
    45: ["r:.5 F5:.5 G5:.5 Bb5:1.5 r:1", "D6:.75 C6:.75 A5:.5 F5:2", "r:.5 G5:.5 A5:.5 C6:1.5 r:1", "E6:.5 D6:.5 C6:1 G5:.5 C6:1.5"],
    49: CF,
    53: ["r:.5 D5:.5 F5:.5 G5:1.5 r:1", "Bb5:.75 A5:.75 F5:.5 D5:2", "r:.5 E5:.5 G5:.5 A5:1.5 r:1", "C#6:.5 Bb5:.5 A5:1 G5:.5 E5:1.5"],
    57: ["G5:.5 G5:.5 Ab5:.5 G5:1 F5:.5 D5:1", "Bb5:2 A5:1 G5:1", "G5:1 Bb5:1 D6:2", "C6:1.5 Bb5:.5 G5:2"],
    61: ["A5:.5 A5:.5 Bb5:.5 A5:1 F5:.5 D5:1", "F5:1 E5:.5 D5:.5 A4:2", "C#5:1 E5:1 G5:1 Bb5:1", "A5:.5 A5:.5 Bb5:.5 A5:1 G5:.5 E5:1"],
}

# Bass riff as (beat, length, semitones above the root, velocity); two bars long.
RIFF_MINOR = [(0, .5, 0, 112), (.75, .25, 0, 80), (1, .5, 12, 100), (2, .5, 10, 92), (2.5, .5, 12, 104), (3.5, .5, 7, 90),
              (4, .5, 0, 112), (5, .5, 3, 92), (5.5, .5, 5, 96), (6.5, .5, 6, 88), (7, .5, 7, 100), (7.5, .5, 10, 94)]
RIFF_MINOR_2 = [(0, .5, 0, 112), (.75, .25, 0, 80), (1, .5, 12, 100), (2, .5, 10, 92), (2.5, .5, 12, 104), (3.5, .5, 7, 90),
                (4, .5, 0, 112), (4.75, .25, 0, 78), (5, .5, 12, 100), (5.5, .5, 10, 90), (6, .5, 7, 96), (6.5, .5, 5, 90),
                (7, .5, 3, 96), (7.5, .5, 5, 100)]
RIFF_DRIVE = [(k * .5, .4, [0, 0, 12, 0, 0, 12, 0, 7][k % 8], [112, 82, 100, 84, 106, 100, 86, 94][k % 8]) for k in range(16)]


def bass_notes(prog) -> list[Note]:
    notes: list[Note] = []
    for start, dur, ch in prog:
        r = root_of(ch, 36)
        if r > 43:
            r -= 12
        bar = int(start // 4)
        if 32 <= bar < 40:  # breakdown: long, low notes
            notes.append(Note(start, dur - 0.5, r, 100))
            continue
        k = 0
        while k * 8 < dur:
            if 16 <= bar < 32:
                riff = RIFF_DRIVE
            else:
                riff = RIFF_MINOR if k % 2 == 0 else RIFF_MINOR_2
            for pos, ln, iv, vel in riff:
                if not ch.minor and iv == 3:
                    iv = 4
                if ch.kind in ("maj9", "maj7", "maj7#11", "add9") and iv == 10:
                    iv = 9
                if ch.kind in ("maj9", "maj7", "maj7#11", "add9") and iv == 6:
                    iv = 7
                notes.append(Note(start + k * 8 + pos, ln, r + iv, vel))
            k += 1
    return notes


def drum_bar(b: int) -> dict:
    """Step patterns for one bar (16 steps). The breakdown (bars 33-40) drops to half time and builds a roll."""
    if 32 <= b < 38:
        return dict(kick="x...............", snare="........x.......", ghost="..............o.", hat="x...x...x...x...", open="", tom="")
    if b == 38:
        return dict(kick="x.......x.......", snare="x.x.x.x.x.x.x.x.", ghost="", hat="x...x...x...x...", open="", tom="")
    if b == 39:
        return dict(kick="x...x...x...x...", snare="xxxxxxxxxxxxxxxx", ghost="", hat="", open="", tom="........x.x.xxxx")
    if b % 8 == 7:  # fill
        return dict(kick="x.....x.........", snare="....x...x.x.x.xx", ghost="", hat="x.x.x.x.........", open="", tom="..........x.x.xx")
    v = b % 4
    if v == 0:
        return dict(kick="x.........x.....", snare="....x.......x...", ghost=".......o.o......", hat="x.x.x.x.x.x.x.x.", open="..............x.", tom="")
    if v == 1:
        return dict(kick="x.....x...x.....", snare="....x.......x...", ghost=".........o.....o", hat="x.x.x.x.x.x.x.x.", open="......x.........", tom="")
    if v == 2:
        return dict(kick="x.........x..x..", snare="....x.......x...", ghost=".......o.o......", hat="x.x.x.xxx.x.x.x.", open="..............x.", tom="")
    return dict(kick="x.........xx....", snare="....x..o....x..o", ghost=".........o......", hat="x.x.x.x.x.x.xxx.", open="......x.........", tom="")


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=17201, tail=6.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(SEC_A + SEC_B + SEC_C + SEC_A + SEC_D)
    out = {}

    # ---------------------------------------------------------------- drums
    def lane(key, pitch, vel, **kw):
        return bars_of(lambda b: drum_bar(b)[key], BARS, pitch, vel=vel, **kw)

    kick_n = lane("kick", DR["kick"], 118)
    snare_n = lane("snare", DR["snare"], 114)
    for i, n in enumerate(snare_n):  # the roll in bar 40 swells
        if n.start >= 39 * 4:
            snare_n[i] = Note(n.start, n.dur, n.pitch, int(60 + 60 * (n.start - 156) / 4))
        elif n.start >= 38 * 4:
            snare_n[i] = Note(n.start, n.dur, n.pitch, int(55 + 30 * (n.start - 152) / 4))
    ghost_n = lane("ghost", DR["snare"], 100, ghost=42)
    hat_n = lane("hat", DR["hat"], 84)
    open_n = lane("open", DR["hat_open"], 88)
    tom_n = []
    for n in lane("tom", DR["tom_mid"], 104):
        step = int(round((n.start % 4) * 4))
        tom_n.append(Note(n.start, n.dur, DR["tom_hi"] if step < 12 else DR["tom_mid"] if step < 14 else DR["tom_lo"], n.vel))
    crash_n = [Note(b * 4.0, 1, DR["crash"], 100) for b in (0, 16, 40, 56)]
    kit_notes = humanize(kick_n + snare_n + ghost_n + hat_n + open_n + tom_n + crash_n, rng, 0.004, 6, BPM)
    kit = L.sf("drums_kit", kit_notes, KITS["power"], drums=True, gain=1.0)
    k_syn = L.hits("drums_kick_layer", kick_n, lambda n, r: synth.kick(n.vel / 127, 52, 170, 0.16, 0.5, 2.0, r))
    s_syn = L.hits("drums_snare_layer", snare_n, lambda n, r: synth.snare(n.vel / 127, 200, 1.1, 0.11, r))

    def drum_fx(k, ks, ss):
        dry = k * 1.0 + ks * 0.55 + ss * 0.4
        smash = dsp.compress(dsp.saturate(dry, 2.5), -28, 8.0, 0.003, 0.09, makeup_db=10)  # parallel compression
        room = dsp.reverb(dry, 0.5, 1.0, 0.6, 0.004, 0.6, seed=201, wet_only=True)
        return glue(dsp.transient(dry, 0.25) + smash * 0.35 + room * 0.5, 14.0)

    out["drums"] = stem(L, L.fx(drum_fx, kit, k_syn, s_syn), 0.0, hp=32)

    # ---------------------------------------------------------------- bass
    b_notes = bass_notes(prog)
    slap = L.sf("bass_slap", humanize(b_notes, rng, 0.004, 5, BPM), GM["slap_bass"], gain=1.0)
    reese = L.synth("bass_growl", b_notes, synth.v_reese, move=0.3, top=1.1)
    duck = L.duck(kick_n, -5.0, 0.004, 0.11)

    def bass_fx(s, r):
        s = dsp.compress(dsp.saturate(dsp.highpass(s, 70), 1.8, 0.7), -18, 4.0, 0.006, 0.12, makeup_db=4)
        return dsp.width(presence(s, level_db=-7.0), 0.3) * 0.95 + dsp.lowpass(r, 900) * 0.7

    out["bass"] = stem(L, L.fx(bass_fx, slap, reese) * duck, 0.0, hp=34, lp=6500)

    # ---------------------------------------------------------------- stabs (brass + synth) and funk guitar chops
    def stab_rhythm(b):
        if 32 <= b < 40:
            return [(0.0, 3.5, 0.7)] if b % 4 == 0 else []
        if 16 <= b < 32:
            return [(0.0, .3, 1.0), (1.5, .3, .85), (2.5, .3, .9)] if b % 2 == 0 else [(0.75, .3, .9), (2.0, .3, 1.0), (3.5, .3, .85)]
        return [(0.0, .3, 1.0), (0.75, .3, .85), (1.5, .3, .95)] if b % 2 == 0 else [(1.5, .3, .9), (2.0, .3, 1.0), (2.75, .3, .9)]

    stab_n = comp(prog, stab_rhythm, low=60, count=4, vel=106, top=79)
    brass = L.sf("stabs_brass", humanize(stab_n, rng, 0.005, 5, BPM), GM["brass"], gain=1.0)
    saws = L.synth("stabs_synth", stab_n, synth.v_stab, bright=1.1)
    chop_n = []
    for b in range(BARS):
        if 32 <= b < 40:
            continue
        ch = chord_at(prog, b * 4.0)
        v = voicing(ch, 62, 3, True, 76)
        for n in pattern("..x.o.x. o.x...xo" if b % 2 == 0 else "..x.o.x. ..x.x.o.", 0, start=b * 4.0, vel=88, ghost=52):
            for p in v[:2]:
                chop_n.append(Note(n.start, 0.12, p, n.vel))
    chops = L.sf("stabs_guitar", humanize(chop_n, rng, 0.004, 6, BPM), GM["muted_guitar"], gain=1.0)

    def stab_fx(b, s, c):
        hit = dsp.saturate(b * 0.9 + s * 0.75, 1.6)
        hit = dsp.delay(hit, L.beat * 0.75, L.beat * 0.5, 0.25, 0.16, 3000)
        return dsp.reverb(hit, 1.1, 0.18, 0.5, 0.01, 0.9, seed=202) + dsp.pan(dsp.mono(c), 0.45) * 0.7

    out["stabs"] = stem(L, L.fx(stab_fx, brass, saws, chops), 0.0, hp=140)

    # ---------------------------------------------------------------- lead
    lead_n: list[Note] = []
    for bar, lines in LEAD.items():
        lead_n += bars(lines, 4, bar - 1, vel=104)
    lead = L.synth("lead", lead_n, synth.v_lead_hard)
    lead_tp = L.sf("lead_trumpet", [n for n in lead_n if n.start >= 160], GM["trumpet"], gain=1.0)

    def lead_fx(l, t):
        x = dsp.delay(l + t * 0.5, L.beat * 1.5, L.beat * 0.75, 0.3, 0.22, 3800)
        return dsp.reverb(x, 1.6, 0.2, 0.45, 0.015, 1.0, seed=203)

    out["lead"] = stem(L, L.fx(lead_fx, lead, lead_tp), 0.0, hp=220)

    # ---------------------------------------------------------------- chaos (intensity 80+): more of everything on top
    ride_n = bars_of(lambda b: "" if 32 <= b < 38 else "xxxxxxxxxxxxxxxx", BARS, DR["ride"], vel=70)
    ride_n = [Note(n.start, n.dur, n.pitch, n.vel + (18 if int(round(n.start * 4)) % 4 == 2 else 0)) for n in ride_n]
    conga_n = bars_of(lambda b: "" if 32 <= b < 40 else "..x..x.o..x.x..o", BARS, DR["conga_hi"], vel=92, ghost=60) + \
        bars_of(lambda b: "" if 32 <= b < 40 else "x.....x.....o.x.", BARS, DR["conga_lo"], vel=90, ghost=60)
    tamb_n = bars_of(lambda b: "....x.......x...", BARS, DR["tamb"], vel=84)
    top = L.sf("chaos_percussion", humanize(ride_n + conga_n + tamb_n, rng, 0.004, 7, BPM), KITS["standard"], drums=True, gain=1.0)
    arp_n: list[Note] = []
    for b in range(BARS):
        if 32 <= b < 40:
            continue
        ch = chord_at(prog, b * 4.0)
        v = voicing(ch, 74, 4, True, 93)
        order = [0, 2, 1, 3, 2, 3, 1, 2] if b % 2 == 0 else [3, 1, 2, 0, 1, 2, 3, 1]
        for k in range(16):
            arp_n.append(Note(b * 4.0 + k * 0.25, 0.2, v[order[k % 8] % len(v)], 96 if k % 4 == 0 else 72))
    arp = L.synth("chaos_arp", arp_n, synth.v_pluck, bright=1.3, decay=0.09, res=2.2)
    sweeps = np.zeros((L.n_render, 2))
    for at_bar, length_bars in [(15, 2), (31, 2), (39, 2), (55, 2), (63, 2)]:
        dur = length_bars * 4 * L.beat
        start = dsp.secs((at_bar - 1) * 4 * L.beat)
        dsp.place(sweeps, synth.riser(dur, np.random.default_rng(at_bar), 400, 10000, tone=293.66), start, 0.6)
    sweeps = L.fold(sweeps)

    def chaos_fx(t, a, s):
        a = dsp.delay(a, L.beat * 0.75, L.beat * 0.5, 0.3, 0.25, 5000)
        return dsp.highpass(t, 300) * 0.9 + dsp.reverb(a * 0.7 + s, 1.2, 0.2, 0.4, 0.01, 0.9, seed=204)

    out["chaos"] = stem(L, L.fx(chaos_fx, top, arp, sweeps), 0.0, hp=250)

    out = balance(out, dict(drums=-19.0, bass=-21.0, stabs=-23.0, lead=-21.5, chaos=-24.5))
    return calibrate(out, target_lufs=-14.5), L
