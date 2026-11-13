"""Regolith Lullaby: planetary exploration. 86 BPM, D Dorian, 48 bars (A, A', B), about 2 min 14 s.

The Exploration theme on vibraphone over Rhodes, a melodic bass guitar and soft percussion. Six stems, so the
same piece can be nearly nothing (pad and far-off bells), a warm tune, or a tense one (pulse and tremolo).
It shares a beat grid with the combat piece (172 BPM is exactly double), so the two can hand over on a bar line.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, motifs, synth
from oa_audio.sampler import DR, GM
from oa_audio.seq import Note, bars, chord_at, humanize, progression, shift

from .common import approach, balance, bars_of, calibrate, comp, held, root_of, stem

ID = "explore_regolith"
BPM = 86.0
BARS = 48

META = dict(
    title="Regolith Lullaby", context="surface", bpm=BPM, beatsPerBar=4, bars=BARS, key="D Dorian",
    motifs=["exploration", "orbital_ante (cell, bars 21-22)"], entryBars=[1, 17, 33],
    stems=[
        dict(id="pad", lo=0, hi=59, fadeInBars=2, fadeOutBars=2),
        dict(id="bells", lo=0, hi=59, fadeInBars=1, fadeOutBars=2),
        dict(id="keys", lo=20, hi=59, fadeInBars=2, fadeOutBars=2),
        dict(id="bass", lo=20, hi=59, fadeInBars=1, fadeOutBars=2),
        dict(id="perc", lo=30, hi=59, fadeInBars=2, fadeOutBars=1),
        dict(id="tension", lo=40, hi=59, fadeInBars=2, fadeOutBars=2),
    ],
)

A = [("Dm9", 8), ("G13", 8), ("Bbmaj7#11", 8), ("Am11", 8),
     ("Dm9", 4), ("Fmaj9", 4), ("G13", 4), ("Em7", 4), ("Bbmaj7#11", 4), ("C6/9", 4), ("Am11", 4), ("A7sus4", 4)]
B = [("Fmaj9", 8), ("Cadd9/E", 8), ("Ebmaj7#11", 8), ("Dm11", 8),
     ("Gm9", 4), ("C13", 4), ("Fmaj9", 4), ("Bbmaj7", 4), ("Em7b5", 4), ("A7b9", 4), ("Dm9", 8)]

MELODY_A = motifs.EXPLORATION + [
    "D4:2 A4:2", "G4:2 E5:2", "D5:1.5 C5:.5 A4:2", "G4:3 r:1",
    "r:1 F4:1 A4:1 E5:1", "D5:2 C5:1 A4:1", "B4:1.5 A4:.5 F4:2", "G4:2 E4:1 D4:1",
    "D4:2 A4:2", "G4:1 A4:1 D5:2", "C5:1.5 B4:.5 G4:1 E4:1", "D4:4",
]
MELODY_A2 = [
    "D4:2 A4:2", "G4:2.5 A4:.5 r:1", "C5:1.5 B4:.5 G4:2", "A4:3 r:1",
    "r:1 A4:.5 B4:.5 D5:2", "E5:1.5 D5:.5 A4:2", "C5:1 D5:1 E5:1 G5:1", "A5:3 r:1",
    "G5:2 E5:2", "C5:1 A4:1 F4:2", "B4:1.5 A4:.5 F4:2", "E4:2 G4:2",
    "D4:2 A4:2", "G4:1 A4:1 E5:2", "D5:1.5 C5:.5 A4:1 G4:1", "A4:4",
]
MELODY_B = [
    "A4:1 C5:1 F5:2", "E5:1.5 C5:.5 A4:2", "G4:1 C5:1 E5:2", "D5:3 r:1",
    "G4:1 Bb4:1 F5:2", "D5:1.5 A4:.5 G4:2", "F4:1 G4:1 A4:1 C5:1", "D5:3 r:1",
    "Bb4:2 D5:1 F5:1", "E5:2 D5:1 Bb4:1", "A4:2 C5:1 E5:1", "D5:2 A4:2",
    "Bb4:1.5 A4:.5 G4:2", "C#5:1 Bb4:1 G4:1 E4:1", "D4:2 A4:2", "G4:2 r:2",
]
BELLS = {1: "D5:2 A5:2", 5: "r:2 G5:2", 9: "C6:1.5 B5:.5 G5:2", 13: "A5:4", 17: "D5:2 A5:2", 21: "r:1 E6:3",
         25: "r:2 F5:1 A5:1", 29: "D6:4", 33: "A5:2 C6:2", 35: "G5:4", 37: "r:2 A5:2", 39: "G5:2 D5:2",
         41: "Bb5:2 A5:2", 45: "r:2 Bb5:2", 47: "r:2 E5:2"}


def bassline(prog, rng) -> list[Note]:
    """Melodic finger bass: root, a lift to the fifth or octave, and a lead-in to the next chord."""
    notes: list[Note] = []
    total = int(prog[-1][0] + prog[-1][1])
    for b in range(total // 4):
        at = b * 4.0
        ch = chord_at(prog, at)
        nxt = chord_at(prog, (at + 4.0) % total)
        r = root_of(ch, 36)
        if r > 45:
            r -= 12
        nr = root_of(nxt, 36)
        if nr > 45:
            nr -= 12
        fifth = r + 7
        variant = b % 4
        if variant == 0:
            notes += [Note(at, 1.5, r, 96), Note(at + 1.5, 0.5, fifth, 70), Note(at + 2.5, 1.0, r + 12, 82)]
        elif variant == 1:
            notes += [Note(at, 2.25, r, 92), Note(at + 2.5, 0.5, fifth, 74), Note(at + 3.0, 0.5, r + 12, 78)]
        elif variant == 2:
            notes += [Note(at, 1.5, r, 96), Note(at + 1.5, 1.0, r + 12, 76), Note(at + 3.0, 0.5, fifth, 72)]
        else:
            third = nearest_minor_or_major(ch, r)
            notes += [Note(at, 1.5, r, 94), Note(at + 2.0, 0.5, third, 72), Note(at + 2.5, 0.5, fifth, 76)]
        if nr != r:
            notes.append(Note(at + 3.5, 0.5, approach(r, nr), 80))
    return notes


def nearest_minor_or_major(ch, r: int) -> int:
    return r + (3 if ch.minor else 4)


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=8601, tail=9.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(A + A + B)

    # ---------------------------------------------------------------- pad
    pad_notes = held(prog, low=50, count=4, vel=62, rootless=False, overlap=0.3, top=69)
    pad = L.synth("pad", pad_notes, synth.v_pad, bright=0.75, attack=1.4, release=2.6)
    glass = L.synth("pad_glass", shift(held(prog, low=62, count=2, vel=50, rootless=True, top=81), 0, 12), synth.v_glass,
                    attack=1.8, release=3.0)
    sub = L.synth("pad_sub", [Note(s, d, root_of(c, 36) if root_of(c, 36) <= 45 else root_of(c, 24), 50) for s, d, c in prog],
                  synth.v_sub, release=0.8, drive=1.0)
    x = L.fx(lambda p, g, s: dsp.reverb(dsp.chorus(p, L.rate(0.35), 4.0, 14.0, 0.5) + g * 0.6 + s * 0.22,
                                        4.5, 0.42, 0.55, 0.03, 1.5, seed=101), pad, glass, sub)
    out = {"pad": stem(L, x, 0.0, hp=45)}

    # ---------------------------------------------------------------- bells (far-off fragments of the theme)
    bell_notes: list[Note] = []
    for bar, text in BELLS.items():
        bell_notes += bars([text], 4, bar - 1, vel=74)
    bells = L.synth("bells", bell_notes, synth.v_bell, ratio=3.5, decay=2.8, bright=0.7)
    pluck = L.synth("bells_harp", shift(bell_notes, 0.0, -12, 0.8), synth.v_string_pluck, bright=0.45)
    x = L.fx(lambda b, p: dsp.reverb(dsp.delay(b + p * 0.5, L.beat * 0.75, L.beat * 1.0, 0.42, 0.4, 3200),
                                     6.0, 0.6, 0.5, 0.04, 1.8, seed=102), bells, pluck)
    out["bells"] = stem(L, x, 0.0, hp=250)

    # ---------------------------------------------------------------- keys (Rhodes comp + the theme on vibraphone)
    def rhodes_rhythm(b):
        if 20 <= b < 28:  # bars 21-28: a rolling, broken-chord feel
            return [(0.0, 1.0, 1.0), (1.5, 0.5, 0.75), (2.0, 1.0, 0.9), (3.5, 0.5, 0.8)]
        if b % 2 == 0:
            return [(0.0, 3.0, 1.0)]
        return [(0.0, 1.75, 0.95), (2.5, 1.25, 0.85)]

    rhodes_notes = humanize(comp(prog, rhodes_rhythm, low=57, count=4, vel=70, top=76), rng, 0.012, 6, BPM)
    rhodes = L.sf("keys_rhodes", rhodes_notes, GM["rhodes"], gain=0.9)
    tines = L.synth("keys_tines", rhodes_notes, synth.v_tine)
    melody = bars(MELODY_A, 4, 0, vel=92) + bars(MELODY_A2, 4, 16, vel=96) + bars(MELODY_B, 4, 32, vel=94)
    melody = humanize(shift(melody, 0, 12), rng, 0.014, 7, BPM)
    vibes = L.sf("keys_melody_vibes", melody, GM["vibes"], gain=0.9)
    whistle = L.synth("keys_melody_air", [n for n in melody if n.start >= 128], synth.v_whistle)

    def keys_fx(r, t, v, w):
        r = dsp.chorus(r, L.rate(0.8), 1.6, 9.0, 0.3)
        sway = 0.18 * dsp.lfo(len(r), L.rate(4.3))  # the Rhodes' stereo tremolo
        r = r * np.stack([0.82 + sway, 0.82 - sway], axis=1)
        lead = dsp.delay(v * 1.15 + w * 0.7, L.beat * 1.5, L.beat * 1.0, 0.25, 0.22, 3500)
        return dsp.reverb(r * 0.9 + t * 0.35 + lead, 2.6, 0.26, 0.5, 0.02, 1.2, seed=103)

    out["keys"] = stem(L, L.fx(keys_fx, rhodes, tines, vibes, whistle), 0.0, hp=110)

    # ---------------------------------------------------------------- bass
    bass_notes = humanize(bassline(prog, rng), rng, 0.008, 5, BPM)
    bass = L.sf("bass", bass_notes, GM["finger_bass"], gain=1.0)
    x = L.fx(lambda b: dsp.width(dsp.compress(dsp.saturate(b, 1.3, 0.5), -20, 3.0, 0.02, 0.2, makeup_db=3), 0.2), bass)
    out["bass"] = stem(L, x, 0.0, hp=38, lp=3800)

    # ---------------------------------------------------------------- perc
    n_bars = BARS
    kick_n = bars_of(lambda b: "x.........x....." if b % 4 != 3 else "x.........x...o.", n_bars, DR["kick"], vel=92)
    rim_n = bars_of(lambda b: "....x.......x..." if b % 8 != 7 else "....x.......x.xo", n_bars, DR["rim"], vel=70)
    shaker_n = bars_of(lambda b: "o.x.o.x.o.x.o.xo", n_bars, DR["shaker"], vel=74, ghost=40)
    tamb_n = bars_of(lambda b: "....x.......x..." if b >= 32 else "", n_bars, DR["tamb"], vel=58)
    kick = L.hits("perc_kick", kick_n, lambda n, r: synth.kick(n.vel / 127, 50, 120, 0.22, 0.12, 1.2, r))
    rim = L.hits("perc_rim", humanize(rim_n, rng, 0.006, 6, BPM), lambda n, r: synth.rim(n.vel / 127, r), pan=-0.25)
    shaker = L.hits("perc_shaker", humanize(shaker_n, rng, 0.007, 8, BPM), lambda n, r: synth.shaker(n.vel / 127, 0.08, r), pan=0.35)
    ride_n = bars_of(lambda b: "x...x...x...x.o." if b % 2 else "x...x..ox...x...", n_bars, DR["ride"], vel=46)
    hats = L.sf("perc_ride_tamb", humanize(ride_n + tamb_n, rng, 0.008, 6, BPM), 0, drums=True, gain=0.8)
    x = L.fx(lambda k, r, s, h: k * 0.5 + dsp.reverb(r * 0.8 + s * 0.9 + h * 1.6, 1.4, 0.2, 0.6, 0.01, 0.8, seed=104),
             kick, rim, shaker, hats)
    out["perc"] = stem(L, x, 0.0, hp=35)

    # ---------------------------------------------------------------- tension (enters at intensity 40)
    pulse_notes: list[Note] = []
    trem_notes: list[Note] = []
    cell_notes: list[Note] = []
    for b in range(n_bars):
        at = b * 4.0
        ch = chord_at(prog, at)
        r = root_of(ch, 36)
        if r > 45:
            r -= 12
        for k in range(8):
            pulse_notes.append(Note(at + k * 0.5, 0.3, r if k % 4 != 3 else r + 12, [100, 58, 78, 62, 92, 58, 80, 70][k]))
        swell = 50 + 12 * (b % 4)
        trem_notes += [Note(at, 4.0, r + 36 + 7, swell), Note(at, 4.0, r + 48, swell - 8)]
        if b % 4 == 3:
            trem_notes.append(Note(at + 2.0, 2.0, r + 48 + 1, swell - 4))  # the semitone rub, last half-bar of each phrase
            # the Conflict theme's first cell, as a far-off warning
            cell_notes += [Note(at + 2.0, 0.4, r + 36, 84), Note(at + 2.5, 0.4, r + 36, 76), Note(at + 3.0, 0.4, r + 37, 88),
                           Note(at + 3.5, 0.5, r + 36, 80)]
    pulse = L.synth("tension_pulse", pulse_notes, synth.v_pulse)
    trem_s = L.synth("tension_tremolo", trem_notes, synth.v_string_trem)
    cell = L.sf("tension_pizz", cell_notes, GM["pizzicato"], gain=0.9)
    tick_n = bars_of(lambda b: "x.oxx.ox x.oxx.oo" if b % 2 else "x.oxx.ox x.oxxoox", n_bars, DR["hat"], vel=70, ghost=38)
    ticks = L.hits("tension_ticks", humanize(tick_n, rng, 0.004, 5, BPM), lambda n, r: synth.hat(n.vel / 127, 0.02, 1.15, r), pan=0.15)
    heart_n = bars_of(lambda b: "x..o............", n_bars, DR["kick"], vel=100, ghost=70)
    heart = L.hits("tension_heart", heart_n, lambda n, r: synth.kick(n.vel / 127, 42, 60, 0.3, 0.0, 1.0, r))
    dry = pulse * 0.7 * L.duck(heart_n, -4.0, 0.01, 0.25) + heart * 0.5 + ticks * 0.3
    x = L.fx(lambda d, t, c: d + dsp.reverb(t * 0.9 + c * 0.8, 3.0, 0.4, 0.5, 0.02, 1.3, seed=105), dry, trem_s, cell)
    out["tension"] = stem(L, x, 0.0, hp=40)

    out = balance(out, dict(pad=-25.0, bells=-27.0, keys=-21.5, bass=-23.0, perc=-25.5, tension=-24.5))
    return calibrate(out, target_lufs=-17.0), L
