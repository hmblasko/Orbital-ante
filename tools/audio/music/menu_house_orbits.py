"""The House Always Orbits: menus, upgrades and the casino. 104 BPM swing, D major, 40 bars (AABA + vamp), about 1 min 32 s.

A small jazz group: upright bass, brushes, Rhodes, guitar, and the Orbital Ante theme on vibraphone with a
muted trumpet joining for the last chorus. A fourth stem of glockenspiel, finger snaps and shaker is added
when the player is spending or gambling.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, synth
from oa_audio.sampler import DR, GM, KITS
from oa_audio.seq import Note, bars, chord_at, humanize, nearest_pc, progression, shift, swing, voicing

from .common import approach, balance, bars_of, calibrate, comp, glue, pattern, root_of, stem, presence

ID = "menu_house_orbits"
BPM = 104.0
BARS = 40
SWING = 0.64

META = dict(
    title="The House Always Orbits", context="menu", bpm=BPM, beatsPerBar=4, bars=BARS, key="D major",
    motifs=["orbital_ante"], entryBars=[1, 17, 33],
    stems=[
        dict(id="rhythm", lo=0, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="keys", lo=0, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="melody", lo=20, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="sparkle", lo=60, hi=100, fadeInBars=1, fadeOutBars=2),
    ],
)

A1 = [("Dmaj9", 4), ("B7#9", 4), ("Em9", 4), ("A13b9", 4), ("F#m7", 4), ("B7", 4), ("Em9", 2), ("A13", 2), ("D6/9", 2), ("Eb9", 2)]
A3 = A1[:-2] + [("D6/9", 4)]
BRIDGE = [("Gmaj7", 4), ("G#dim7", 4), ("Dmaj9/A", 4), ("B7b9", 4), ("E9", 8), ("Em9", 4), ("A13b9", 4)]
VAMP = [("Dmaj9", 4), ("B7#9", 4), ("Em9", 4), ("A13b9", 4)] * 2

MEL_A = [
    "r:.5 A4:.5 B4:.5 D5:1.5 r:1", "F#5:1 E5:.5 C5:.5 A4:2", "r:.5 B4:.5 D5:.5 E5:1.5 r:1", "G5:1 F#5:.5 E5:.5 Bb4:1 A4:1",
    "r:.5 A4:.5 C#5:.5 E5:1.5 F#5:1", "D#5:1 C#5:.5 B4:.5 A4:2", "G4:1 B4:1 C#5:1 E5:1", "D5:3 r:1",
]
MEL_A2 = [
    "r:.5 A4:.5 B4:.5 D5:1 F#5:.5 A5:1", "F#5:1 E5:.5 C5:.5 A4:1.5 C5:.5", "r:.5 B4:.5 D5:.5 E5:1 G5:.5 B5:1", "G5:1 F#5:.5 E5:.5 Bb4:1 A4:1",
    "r:.5 A4:.5 C#5:.5 E5:1 F#5:.5 A5:1", "D#5:1 C#5:.5 B4:.5 A4:1 F#4:1", "G4:1 B4:1 C#5:1 E5:1", "D5:2.5 r:1.5",
]
MEL_B = [
    "B4:1 D5:1 F#5:2", "F5:1 D5:1 B4:2", "A4:1 D5:1 F#5:1 A5:1", "G5:1.5 F#5:.5 D#5:2",
    "E5:1 G#5:1 B5:2", "A5:.5 G#5:.5 F#5:1 D5:2", "r:1 E5:1 G5:1 B5:1", "Bb5:2 A5:1 r:1",
]
MEL_VAMP = [
    "r:.5 A4:.5 B4:.5 D5:.5 F#5:.5 E5:.5 D5:1", "D#5:.5 F#5:.5 A5:.5 C6:.5 B5:1 r:1",
    "r:.5 B4:.5 D5:.5 E5:.5 G5:.5 F#5:.5 E5:1", "C#5:.5 E5:.5 G5:.5 Bb5:.5 A5:1 r:1",
    "r:.5 D5:.5 F#5:.5 A5:.5 C#6:1 B5:.5 A5:.5", "A5:.5 F#5:.5 D#5:.5 C5:.5 B4:2",
    "r:.5 E5:.5 G5:.5 B5:.5 D6:1 B5:1", "Bb5:.5 A5:.5 G5:.5 E5:.5 C#5:.5 E5:.5 D5:1",
]


def walking_bass(prog, rng) -> list[Note]:
    """Upright bass: two-feel for the first chorus, then walking four to the bar with chromatic lead-ins."""
    notes: list[Note] = []
    total = prog[-1][0] + prog[-1][1]
    last = 38
    for b in range(BARS):
        at = b * 4.0
        two_feel = b < 8 or 32 <= b < 36
        for half in range(2):
            t0 = at + half * 2.0
            ch = chord_at(prog, t0)
            nxt = chord_at(prog, (t0 + 2.0) % total)
            r = nearest_pc(last, [root_of(ch, 0) % 12])
            while r < 31:
                r += 12
            while r > 50:
                r -= 12
            nr = nearest_pc(r, [root_of(nxt, 0) % 12])
            same = chord_at(prog, (t0 + 2.0) % total) is ch or (root_of(nxt, 0) % 12 == root_of(ch, 0) % 12)
            if two_feel:
                if half == 0:
                    notes.append(Note(t0, 1.8, r, 100))
                else:
                    fifth = r + 7 if r + 7 <= 52 else r - 5
                    notes += [Note(t0, 1.3, fifth if same else r, 92), Note(t0 + 1.5, .5, approach(r, nr) if not same else r, 80)]
                last = r
                continue
            tones = [p for p in range(r - 7, r + 13) if p % 12 in ch.tones and p != r and 30 <= p <= 55]
            mid = int(rng.choice(tones)) if tones else r + 7
            if same and half == 0:
                notes += [Note(t0, .95, r, 104), Note(t0 + 1, .95, mid, 90)]
                last = mid
            else:
                notes += [Note(t0, .95, r if half == 0 or not same else mid, 100), Note(t0 + 1, .95, approach(last, nr) if abs(nr - r) > 0 else r + 7, 92)]
                last = nr
    return notes


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=10401, tail=6.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(A1 + A1 + BRIDGE + A3 + VAMP)
    out = {}

    def sw(notes):
        return swing(notes, SWING, 0.5)

    # ---------------------------------------------------------------- rhythm: upright bass and brushes
    bass_n = humanize(walking_bass(prog, rng), rng, 0.01, 6, BPM)
    bass = L.sf("rhythm_upright_bass", bass_n, GM["upright_bass"], gain=1.0)
    ride_n = sw(bars_of(lambda b: "x.xxx.xx" if b % 4 != 3 else "x.xxxxxx", BARS, DR["ride"], vel=76, step=0.5))
    hat_n = bars_of(lambda b: ".x.x", BARS, DR["hat_pedal"], vel=70, step=1.0)
    snare_n = sw(bars_of(lambda b: [".......o", "...o...o", ".....o..", "...o..oo"][b % 4], BARS, DR["snare"], vel=70, step=0.5, ghost=44))
    swirl_n = bars_of(lambda b: "x.x.", BARS, DR["brush_swirl"], vel=52, step=1.0, dur=1.8)
    kick_n = bars_of(lambda b: "x..." if b % 2 == 0 else "x.o.", BARS, DR["kick"], vel=58, step=1.0, ghost=40)
    kit = L.sf("rhythm_brushes", humanize(ride_n + hat_n + snare_n + swirl_n + kick_n, rng, 0.008, 7, BPM), KITS["brush"], drums=True, gain=1.0)

    def rhythm_fx(b, k):
        b = dsp.compress(dsp.saturate(b, 1.3, 0.5), -20, 3.0, 0.015, 0.2, makeup_db=3)
        return dsp.width(presence(b, level_db=-9.0), 0.15) * 0.8 + dsp.reverb(k, 0.9, 0.16, 0.5, 0.008, 0.8, seed=401) * 2.4

    out["rhythm"] = stem(L, L.fx(rhythm_fx, bass, kit), 0.0, hp=38)

    # ---------------------------------------------------------------- keys: Rhodes comping and a quiet rhythm guitar
    def comp_rhythm(b):
        v = b % 4
        if v == 0:
            return [(0.0, 1.3, 1.0), (1.5, 1.0, .85)]
        if v == 1:
            return [(1.0, .4, .9), (2.5, 1.2, .95)]
        if v == 2:
            return [(0.0, .5, 1.0), (1.5, .4, .85), (3.5, .5, .9)]
        return [(0.5, 1.2, .9), (2.0, .4, .85), (3.0, .8, .9)]

    comp_n = humanize(sw(comp(prog, comp_rhythm, low=55, count=4, vel=80, top=74)), rng, 0.012, 7, BPM)
    rhodes = L.sf("keys_rhodes", comp_n, GM["rhodes"], gain=1.0)
    tines = L.synth("keys_tines", comp_n, synth.v_tine)
    gtr_n = []
    for b in range(BARS):
        for k in range(4):
            at = b * 4.0 + k
            v = voicing(chord_at(prog, at), 50, 3, True, 67)
            for p in v:
                gtr_n.append(Note(at, 0.3, p, 74 if k in (1, 3) else 62))
    gtr = L.sf("keys_guitar", humanize(gtr_n, rng, 0.008, 5, BPM), GM["jazz_guitar"], gain=1.0)

    def keys_fx(r, t, g):
        r = dsp.chorus(r * 0.9 + t * 0.3, L.rate(0.9), 1.4, 8.0, 0.3)
        sway = 0.15 * dsp.lfo(len(r), L.rate(5.0))
        r = r * np.stack([0.85 + sway, 0.85 - sway], axis=1)
        return dsp.reverb(r + dsp.pan(dsp.mono(g), -0.5) * 0.55, 1.3, 0.18, 0.5, 0.012, 0.9, seed=402)

    out["keys"] = stem(L, L.fx(keys_fx, rhodes, tines, gtr), 0.0, hp=120)

    # ---------------------------------------------------------------- melody: the theme on vibes; trumpet joins last time
    mel = bars(MEL_A, 4, 0, vel=98) + bars(MEL_A2, 4, 8, vel=100) + bars(MEL_B, 4, 16, vel=98) + bars(MEL_A, 4, 24, vel=102) \
        + bars(MEL_VAMP, 4, 32, vel=96)
    mel = humanize(sw(mel), rng, 0.012, 7, BPM)
    vibes = L.sf("melody_vibes", mel, GM["vibes"], gain=1.0)
    tpt_n = [n for n in mel if 96 <= n.start < 128]
    tpt = L.sf("melody_muted_trumpet", shift(tpt_n, 0, 0, 0.85), GM["muted_trumpet"], gain=1.0)

    def melody_fx(v, t):
        v = v * (0.9 + 0.1 * dsp.lfo(len(v), L.rate(5.5)))[:, None]  # the vibraphone's motor
        x = v + dsp.pan(dsp.mono(t), 0.35) * 0.6
        return dsp.reverb(dsp.delay(x, L.beat * 1.0, L.beat * 1.5, 0.18, 0.12, 3500), 1.7, 0.22, 0.45, 0.015, 1.0, seed=403)

    out["melody"] = stem(L, L.fx(melody_fx, vibes, tpt), 0.0, hp=180)

    # ---------------------------------------------------------------- sparkle (spending and gambling)
    glock_n = []
    for b in range(BARS):
        v = voicing(chord_at(prog, b * 4.0), 84, 4, True, 100)
        order = [0, 1, 2, 3, 2, 1] if b % 2 == 0 else [3, 2, 1, 0, 1, 2]
        for k in range(6):
            if (b % 4 == 3 and k >= 4):
                continue
            glock_n.append(Note(b * 4.0 + 1.0 + k * 0.5, 0.4, v[order[k] % len(v)], 70 + 6 * (k % 3)))
    glock = L.sf("sparkle_glock", humanize(sw(glock_n), rng, 0.008, 6, BPM), GM["glock"], gain=1.0)
    snap_n = bars_of(lambda b: ".x.x", BARS, DR["clap"], vel=90, step=1.0)
    snaps = L.hits("sparkle_snaps", humanize(snap_n, rng, 0.006, 6, BPM),
                   lambda n, r: dsp.highpass(synth.clap(n.vel / 127, r, 0.05), 1800) * 0.8, pan=0.3)
    shaker_n = sw(bars_of(lambda b: "xxxxxxxx", BARS, DR["shaker"], vel=76, step=0.5))
    shaker_n = [Note(n.start, n.dur, n.pitch, n.vel + (16 if int(round(n.start * 2)) % 2 else 0)) for n in shaker_n]
    shaker = L.hits("sparkle_shaker", humanize(shaker_n, rng, 0.006, 7, BPM), lambda n, r: synth.shaker(n.vel / 127, 0.07, r), pan=-0.35)
    tri_n = bars_of(lambda b: "...x" if b % 2 else "", BARS, DR["triangle"], vel=70, step=1.0)
    tri = L.sf("sparkle_triangle", tri_n, KITS["standard"], drums=True, gain=1.0)

    def sparkle_fx(g, s, h, t):
        return dsp.reverb(g * 0.8 + s * 0.7 + h * 0.6 + t * 0.6, 1.4, 0.22, 0.35, 0.012, 0.9, seed=404)

    out["sparkle"] = stem(L, L.fx(sparkle_fx, glock, snaps, shaker, tri), 0.0, hp=400)

    out = balance(out, dict(rhythm=-21.0, keys=-23.5, melody=-21.0, sparkle=-26.0))
    return calibrate(out, target_lufs=-17.5), L
