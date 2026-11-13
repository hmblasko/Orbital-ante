"""Burn Window: spaceflight. 124 BPM, D major, 64 bars (A A B B breakdown A A B), about 2 min 4 s.

House and disco built on a funky bass guitar, as the game's build prompt asks. This is where the Orbital Ante
theme is stated in full. Five stems: drifting is just the chords; a hard burn brings in bass, drums, the theme
and finally disco strings.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, motifs, synth
from oa_audio.sampler import DR, GM, KITS
from oa_audio.seq import Note, bars, chord_at, humanize, progression, shift, swing, voicing

from .common import approach, balance, bars_of, calibrate, comp, glue, held, pattern, root_of, stem, presence

ID = "flight_burn_window"
BPM = 124.0
BARS = 64

META = dict(
    title="Burn Window", context="flight", bpm=BPM, beatsPerBar=4, bars=BARS, key="D major",
    motifs=["orbital_ante"], entryBars=[1, 17, 41],
    stems=[
        dict(id="chords", lo=0, hi=100, fadeInBars=2, fadeOutBars=4),
        dict(id="bass", lo=12, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="drums", lo=24, hi=100, fadeInBars=1, fadeOutBars=2),
        dict(id="lead", lo=22, hi=100, fadeInBars=2, fadeOutBars=2),
        dict(id="top", lo=50, hi=100, fadeInBars=2, fadeOutBars=4),
    ],
)

SEC_A = [(c, 4) for c in ["Gmaj9", "A13", "F#m7", "Bm9", "Gmaj9", "A13", "Dmaj9", "F#7alt"]]
SEC_B = [(c, 4) for c in ["Bm9", "E9", "Gmaj7", "A13sus", "Bm9", "E9", "Em9", "A13"]]
SEC_K = [("Gmaj9", 8), ("F#m7", 8), ("Em9", 8), ("A13sus", 4), ("A7#5", 4)]

THEME = motifs.ORBITAL_ANTE + [
    "r:.5 A4:.5 B4:.5 D5:1 F#5:.5 A5:1",
    "G5:.75 F#5:.75 E5:.5 C5:1 A4:1",
    "r:.5 B4:.5 D5:.5 F#5:1 E5:.5 D5:1",
    "E5:1 D5:.5 A4:.5 D5:2",
]
THEME_2 = [
    "r:.5 A4:.5 B4:.5 D5:1 E5:.5 F#5:1", "A5:.75 F#5:.75 C5:.5 A4:1 r:1",
    "r:.5 B4:.5 D5:.5 E5:1 F#5:.5 A5:1", "B5:.5 A5:.5 F#5:1 D5:.5 F#5:1.5",
    "r:.5 A4:.5 B4:.5 D5:1 F#5:.5 A5:1", "G5:.75 F#5:.75 E5:.5 C5:1 A4:1",
    "r:.5 D5:.5 F#5:.5 A5:1 B5:.5 A5:1", "A#5:1 F#5:.5 D5:.5 E5:1 C#5:1",
]
LINE_B = [
    "F#5:1.5 D5:.5 B4:2", "G#5:1 F#5:1 E5:2", "r:.5 B4:.5 D5:.5 F#5:.5 A5:2", "G5:1 F#5:1 E5:2",
    "F#5:1.5 D5:.5 B4:1 D5:1", "G#5:1.5 B5:.5 G#5:1 E5:1", "G5:1 F#5:.5 E5:.5 B4:2", "C#5:1 E5:1 F#5:1 A5:1",
]
LINE_K = [
    "r:.5 A4:.5 B4:.5 D5:2.5", "r:4", "r:.5 B4:.5 D5:.5 E5:2.5", "r:4",
    "r:.5 A4:.5 B4:.5 D5:1 F#5:.5 A5:1", "r:4", "B5:2 A5:2", "G5:1 F#5:1 E5:1 C#5:1",
]

BASS_V1 = [(0, .4, 0, 112), (.75, .2, 0, 72), (1.0, .2, 12, 98), (1.5, .4, 0, 102), (2.25, .2, 12, 92), (2.5, .4, 0, 106),
           (3.0, .2, 7, 90), (3.5, .2, 10, 94)]
BASS_V2 = [(0, .4, 0, 112), (.5, .2, 12, 94), (1.0, .4, 0, 102), (1.75, .2, 7, 82), (2.0, .4, 0, 106), (2.5, .2, 12, 98),
           (3.0, .4, 10, 92), (3.5, .2, 12, 96)]
BASS_V3 = [(0, .4, 0, 112), (.5, .2, 0, 66), (1.0, .4, 3, 96), (1.5, .2, 5, 90), (2.0, .4, 7, 104), (2.5, .2, 7, 66),
           (3.0, .4, 10, 96), (3.5, .2, 12, 98)]


def bass_notes(prog) -> list[Note]:
    notes: list[Note] = []
    total = prog[-1][0] + prog[-1][1]
    for b in range(BARS):
        at = b * 4.0
        ch = chord_at(prog, at)
        nxt = chord_at(prog, (at + 4.0) % total)
        r = root_of(ch, 36)
        nr = root_of(nxt, 36)
        if 32 <= b < 38:  # breakdown: held roots
            if at == [s for s, d, c in prog if s <= at < s + d][0]:
                notes.append(Note(at, 7.5, r, 92))
            continue
        sec_b = (16 <= b < 32) or b >= 56
        riff = BASS_V3 if sec_b and b % 2 == 0 else BASS_V2 if b % 2 else BASS_V1
        major7 = ch.kind in ("maj9", "maj7", "6/9")
        for pos, ln, iv, vel in riff:
            if iv == 3 and not ch.minor:
                iv = 4
            if iv == 10 and major7:
                iv = 9
            notes.append(Note(at + pos, ln, r + iv, vel))
        notes.append(Note(at + 3.75, .2, approach(r, nr) if nr != r else r + 11, 88))
    return notes


def drum_bar(b: int) -> dict:
    if 32 <= b < 38:
        return dict(kick="", clap="....x.......x..." if b >= 34 else "", hat="..x...x...x...x.", shut="x.xxx.xxx.xxx.xx", snare="", tom="")
    if b == 38:
        return dict(kick="x...x...x...x...", clap="....x.......x...", hat="..x...x...x...x.", shut="x.xxx.xxx.xxx.xx", snare="x...x...x...x...", tom="")
    if b == 39:
        return dict(kick="x...x...x.x.x.x.", clap="....x.......x...", hat="", shut="xxxxxxxxxxxxxxxx", snare="x.x.x.x.xxxxxxxx", tom="............x.xx")
    if b % 8 == 7:
        return dict(kick="x...x...x...x...", clap="....x.......x...", hat="..x...x...x.....", shut="x.xxx.xxx.xx....", snare="..........o.x.xx", tom="............x..x")
    return dict(kick="x...x...x...x...", clap="....x.......x...", hat="..x...x...x...x.", shut="x.oxx.oxx.oxx.ox" if b % 2 == 0 else "x.oxx.oxx.oxxoox",
                snare="" if b % 4 != 3 else ".......o.......o", tom="")


def build(session_dir: str):
    L = mixing.Loop(BPM, BARS, 4, seed=12401, tail=6.0, session_dir=session_dir)
    rng = L.rng
    prog = progression(SEC_A + SEC_A + SEC_B + SEC_B + SEC_K + SEC_A + SEC_A + SEC_B)
    out = {}

    def lane(key, pitch, vel, **kw):
        return bars_of(lambda b: drum_bar(b)[key], BARS, pitch, vel=vel, **kw)

    kick_n = lane("kick", DR["kick"], 116)
    duck = L.duck(kick_n, -7.0, 0.004, 0.2)
    duck_soft = L.duck(kick_n, -3.5, 0.004, 0.16)

    # ---------------------------------------------------------------- chords: off-beat Rhodes, a pumping pad, funk guitar
    def rhodes_rhythm(b):
        if 32 <= b < 40:
            return [(0.0, 3.5, 0.8)]
        return [(0.5, .4, .9), (1.5, .4, 1.0), (2.75, .4, .85), (3.5, .45, .95)] if b % 2 == 0 else \
            [(0.5, .4, .9), (1.5, .4, 1.0), (2.5, .4, .9), (3.25, .4, .85)]

    rh_n = humanize(comp(prog, rhodes_rhythm, low=57, count=4, vel=92, top=76), rng, 0.006, 6, BPM)
    rhodes = L.sf("chords_rhodes", rh_n, GM["rhodes"], gain=1.0)
    tines = L.synth("chords_tines", rh_n, synth.v_tine)
    pad = L.synth("chords_pad", held(prog, low=52, count=4, vel=64, top=71), synth.v_pad, bright=1.1, attack=0.25, release=0.6)
    chop_n = []
    for b in range(BARS):
        if 32 <= b < 40:
            continue
        v = voicing(chord_at(prog, b * 4.0), 64, 3, True, 79)
        for n in pattern("..x.x..x ..x.o.x." if b % 2 == 0 else "..x.x..x .ox.x.xo", 0, start=b * 4.0, vel=90, ghost=54):
            for p in v:
                chop_n.append(Note(n.start, 0.14, p, n.vel))
    chops = L.sf("chords_guitar", humanize(swing(chop_n, 0.54, 0.25), rng, 0.004, 6, BPM), GM["clean_guitar"], gain=1.0)

    def chords_fx(r, t, p, c):
        r = dsp.chorus(r * 0.9 + t * 0.4, L.rate(0.7), 1.5, 8.0, 0.3)
        p = dsp.chorus(p, L.rate(0.3), 3.5, 13.0, 0.5)
        g = dsp.pan(dsp.mono(dsp.peak_eq(dsp.highpass(c, 300), 2200, 3.0)), -0.4) * 0.75
        wet = dsp.reverb(r + g, 1.5, 0.2, 0.45, 0.012, 1.0, seed=301)
        return wet + dsp.reverb(p, 2.4, 0.25, 0.5, 0.02, 1.2, seed=302) * 0.8

    x = L.fx(chords_fx, rhodes, tines, pad * duck, chops)
    out["chords"] = stem(L, x, 0.0, hp=130)

    # ---------------------------------------------------------------- bass: the funk
    b_notes = bass_notes(prog)
    finger = L.sf("bass_finger", humanize(swing([n for n in b_notes if n.pitch < 50], 0.54, 0.25), rng, 0.004, 5, BPM), GM["finger_bass"], gain=1.0)
    pops = L.sf("bass_pops", humanize(swing([n for n in b_notes if n.pitch >= 50], 0.54, 0.25), rng, 0.004, 6, BPM), GM["slap_bass"], gain=1.0)
    sub = L.synth("bass_sub", [n for n in b_notes if n.pitch < 50 and n.dur >= .4], synth.v_sub, drive=1.2)

    def bass_fx(f, p, s):
        b = dsp.saturate(f * 1.0 + p * 0.8, 1.6, 0.7, 0.05)
        b = dsp.compress(b, -18, 4.0, 0.008, 0.14, makeup_db=4)
        b = dsp.peak_eq(b, 900, 3.0, 1.2)
        return dsp.width(presence(b, level_db=-8.0), 0.25) + dsp.lowpass(s, 140) * 0.3

    out["bass"] = stem(L, L.fx(bass_fx, finger, pops, sub) * duck_soft, 0.0, hp=36, lp=7000)

    # ---------------------------------------------------------------- drums
    clap_n = lane("clap", DR["clap"], 108)
    hat_n = lane("hat", DR["hat_open"], 92)
    shut_n = swing(lane("shut", DR["hat"], 84, ghost=46), 0.54, 0.25)
    snare_n = lane("snare", DR["snare"], 104, ghost=50)
    tom_n = lane("tom", DR["tom_mid"], 100)
    crash_n = [Note(b * 4.0, 1, DR["crash"], 96) for b in (0, 16, 40, 56)]
    kick = L.hits("drums_kick", kick_n, lambda n, r: synth.kick(n.vel / 127, 50, 160, 0.15, 0.6, 2.2, r))
    clap = L.hits("drums_clap", humanize(clap_n, rng, 0.003, 4, BPM), lambda n, r: synth.clap(n.vel / 127, r, 0.11))
    ohat = L.hits("drums_open_hat", hat_n, lambda n, r: synth.hat(n.vel / 127, 0.11, 1.0, r), pan=0.2)
    chat = L.hits("drums_hats", humanize(shut_n, rng, 0.003, 7, BPM), lambda n, r: synth.hat(n.vel / 127, 0.022, 1.1, r), pan=-0.2)
    kit = L.sf("drums_kit", humanize(clap_n + snare_n + tom_n + crash_n, rng, 0.004, 5, BPM), KITS["standard"], drums=True, gain=1.0)
    shaker_n = swing(bars_of(lambda b: "" if 32 <= b < 34 else "o.xoo.xoo.xoo.xo", BARS, DR["shaker"], vel=80, ghost=48), 0.54, 0.25)
    shaker = L.hits("drums_shaker", humanize(shaker_n, rng, 0.004, 8, BPM), lambda n, r: synth.shaker(n.vel / 127, 0.06, r), pan=0.4)

    def drum_fx(k, c, o, h, t, s):
        tops = (c * 1.5 + o * 1.0 + h * 1.0 + t * 1.2 + s * 0.9) * 1.8
        tops = tops + dsp.reverb(c * 0.8 + t * 0.5, 0.9, 1.0, 0.5, 0.01, 0.8, seed=303, wet_only=True) * 0.5
        return glue(k * 1.0 + tops, 13.0)

    out["drums"] = stem(L, L.fx(drum_fx, kick, clap, ohat, chat, kit, shaker), 0.0, hp=32)

    # ---------------------------------------------------------------- lead: the Orbital Ante theme
    lead_n = (bars(THEME, 4, 0, vel=104) + bars(THEME_2, 4, 8, vel=106) + bars(LINE_B, 4, 16, vel=102) + bars(LINE_B, 4, 24, vel=104)
              + bars(LINE_K, 4, 32, vel=92) + bars(THEME, 4, 40, vel=106) + bars(THEME_2, 4, 48, vel=108) + bars(LINE_B, 4, 56, vel=104))
    lead_n = shift(lead_n, 0, 12)
    harmony = shift([n for n in lead_n if 96 <= n.start < 128 or n.start >= 224], 0, -9, 0.75)  # sixths below, second time through
    syn = L.synth("lead_synth", [n for n in lead_n if not 128 <= n.start < 160], synth.v_lead, bright=1.1, vibrato=0.18)
    vib = L.sf("lead_vibes", humanize(lead_n, rng, 0.006, 6, BPM), GM["vibes"], gain=1.0)
    har = L.synth("lead_harmony", harmony, synth.v_lead, bright=0.8, vibrato=0.12)

    def lead_fx(s, v, h):
        x = s * 0.8 + v * 0.9 + h * 0.5
        x = dsp.delay(x, L.beat * 0.75, L.beat * 1.5, 0.3, 0.22, 4200)
        return dsp.reverb(x, 1.8, 0.2, 0.45, 0.015, 1.0, seed=304)

    out["lead"] = stem(L, L.fx(lead_fx, syn, vib, har) * duck_soft, 0.0, hp=250)

    # ---------------------------------------------------------------- top (intensity 70+): disco strings, runs and lift
    str_n: list[Note] = []
    total = prog[-1][0] + prog[-1][1]
    for b in range(BARS):
        if 32 <= b < 36:
            continue
        at = b * 4.0
        v = voicing(chord_at(prog, at), 72, 4, True, 91)
        topn = v[-1]
        if b % 4 == 3:  # the classic string run up into the next phrase
            nv = voicing(chord_at(prog, (at + 4.0) % total), 72, 4, True, 91)[-1] + 12
            str_n += [Note(at, 2.5, topn, 92), Note(at, 2.5, v[-2], 84)]
            scale = [0, 2, 4, 6, 7, 9, 11]  # D major
            run = [p for p in range(nv - 11, nv) if (p - 62) % 12 in scale][-6:]
            for k, p in enumerate(run):
                str_n.append(Note(at + 2.5 + k * 0.25, 0.25, p, 86 + 5 * k))
        elif b % 2 == 0:
            str_n += [Note(at, 3.5, topn + 12, 88), Note(at, 3.5, v[-2] + 12, 80), Note(at, 3.5, v[-3], 76)]
        else:
            str_n += [Note(at, 1.0, topn + 12, 96), Note(at + 1.5, .5, topn + 12, 86), Note(at + 2.0, 1.75, v[-2] + 12, 90)]
    strings = L.sf("top_strings", humanize(str_n, rng, 0.008, 5, BPM), GM["strings"], gain=1.0)
    saws = L.synth("top_string_synth", str_n, synth.v_stab, bright=0.7, release=0.25)
    ride_n = bars_of(lambda b: "" if 32 <= b < 40 else "x.x.x.x.x.x.x.x.", BARS, DR["ride"], vel=70)
    tamb_n = swing(bars_of(lambda b: "" if 32 <= b < 40 else "..x...x...x...xo", BARS, DR["tamb"], vel=84, ghost=56), 0.54, 0.25)
    tops = L.sf("top_ride_tamb", humanize(ride_n + tamb_n, rng, 0.004, 6, BPM), KITS["standard"], drums=True, gain=1.0)
    sweeps = np.zeros((L.n_render, 2))
    for at_bar, length_bars in [(15, 2), (31, 2), (39, 2), (55, 2), (63, 2)]:
        dsp.place(sweeps, synth.riser(length_bars * 4 * L.beat, np.random.default_rng(at_bar), 500, 11000),
                  dsp.secs((at_bar - 1) * 4 * L.beat), 0.5)
    sweeps = L.fold(sweeps)

    def top_fx(s, y, t, w):
        st = dsp.peak_eq(dsp.highpass(s * 1.0 + y * 0.35, 350), 3500, 2.5)
        return dsp.reverb(st, 2.0, 0.28, 0.4, 0.02, 1.2, seed=305) + dsp.highpass(t, 400) * 0.7 + w

    out["top"] = stem(L, L.fx(top_fx, strings, saws, tops, sweeps), 0.0, hp=300)

    out = balance(out, dict(chords=-23.5, bass=-20.5, drums=-19.5, lead=-21.5, top=-24.5))
    return calibrate(out, target_lufs=-15.0), L
