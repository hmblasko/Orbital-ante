"""Shared parts for the house pieces, so they all sound like one record.

Direction from Matthew: 14 Nov 2026, "existential, not upbeat, a good deep house beat"; 15 Nov 2026, "more deep
house, deep bass, high bpm, kick step, rally house", built "with optimal tools for making music". So the record
now runs at 126 (club tempo), on a hard, tuned kick with a rolling sub bass stepping around it, off-beat hats,
bongos and shouted crowd chops. The synths are Faust instruments and the spaces Dragonfly reverbs
(oa_audio/studio.py). Every piece shares this tempo and stays around D, so the game can mix from any piece into
any other on a bar line, the way a DJ would.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, studio, synth
from oa_audio.sampler import DR
from oa_audio.seq import Note, chord_at, humanize, swing

from .common import bars_of, glue, presence, root_of

BPM = 126.0

# rolling bass figures that step around a four-on-the-floor kick: (beat, length, semitones above root, velocity)
ROLL = [(0.25, .2, 0, 92), (0.5, .22, 0, 110), (0.75, .18, 12, 84), (1.25, .2, 0, 92), (1.5, .22, 0, 110), (1.75, .18, 0, 80),
        (2.25, .2, 0, 92), (2.5, .22, 0, 110), (2.75, .18, 12, 84), (3.25, .2, 0, 92), (3.5, .22, 10, 108), (3.75, .18, 12, 90)]
ROLL_TURN = ROLL[:9] + [(3.25, .2, 3, 96), (3.5, .22, 5, 108), (3.75, .18, 7, 100)]
OFFBEAT = [(0.5, .35, 0, 108), (1.5, .35, 0, 104), (2.5, .35, 0, 108), (3.5, .3, 12, 96)]
OFFBEAT_TURN = [(0.5, .35, 0, 108), (1.5, .35, 0, 104), (2.5, .3, 7, 100), (3.25, .2, 10, 92), (3.5, .2, 12, 100), (3.75, .2, 0, 90)]


def faust(L, name: str, notes: list[Note], code: str, **kw) -> np.ndarray:
    """A Faust instrument as a loop part (logged to the session's MIDI, folded so the loop is exact)."""
    L.log(name, notes)
    return L.fold(studio.play(notes, L.bpm, L.n_render, code, **kw))


def kit(L, rng, drum_bar, prefix: str, hard: float = 0.0):
    """The drum machine. drum_bar(bar) returns step patterns for kick, hat (open), shut (closed), clap, rim,
    ride, perc (bongos) and vox (crowd shouts). `hard` 0 is soft, 1 is the combat kit. Returns the kick notes
    (for side-chaining) and the parts."""
    def lane(key, pitch, vel, **kw):
        return bars_of(lambda b: drum_bar(b).get(key, ""), L.bars, pitch, vel=vel, **kw)

    kick_n = lane("kick", DR["kick"], 116)
    # a tuned, punchy club kick: tight sub, hard attack, a little drive. D is the key, so the body sits on D.
    punch, click, drive = 210 + 50 * hard, 0.6 + 0.3 * hard, 2.6 + 1.2 * hard
    bongo_n = humanize(lane("perc", 60, 92, ghost=60), rng, 0.004, 8, L.bpm)
    vox_n = lane("vox", 50, 112)
    parts = dict(
        kick=L.hits(prefix + "_kick", kick_n, lambda n, r: synth.kick(n.vel / 127, 49, punch, 0.2 + 0.03 * (1 - hard), click, drive, r)),
        ohat=L.hits(prefix + "_open_hat", lane("hat", DR["hat_open"], 92 + int(10 * hard)),
                    lambda n, r: synth.hat(n.vel / 127, 0.07, 0.95 + 0.1 * hard, r), pan=0.12),
        chat=L.hits(prefix + "_hats", humanize(swing(lane("shut", DR["hat"], 74 + int(10 * hard), ghost=42), 0.56, 0.25), rng, 0.003, 6, L.bpm),
                    lambda n, r: synth.hat(n.vel / 127, 0.016, 1.0, r), pan=-0.2),
        clap=L.hits(prefix + "_clap", humanize(lane("clap", DR["clap"], 100 + int(12 * hard)), rng, 0.002, 4, L.bpm),
                    lambda n, r: synth.clap(n.vel / 127, r, 0.13)),
        rim=L.hits(prefix + "_rim", humanize(lane("rim", DR["rim"], 74), rng, 0.004, 6, L.bpm), lambda n, r: synth.rim(n.vel / 127, r), pan=0.35),
        ride=L.hits(prefix + "_ride", lane("ride", DR["ride"], 66), lambda n, r: synth.cymbal(n.vel / 127, 0.32, 1.15, r) * 0.5, pan=-0.3),
        perc=L.hits(prefix + "_bongos", bongo_n, lambda n, r: synth.tom(n.vel / 127, 330 if n.vel > 80 else 250, 0.09, r) * 0.8,
                    pan=0.3),
        vox=L.fold(studio.crowd(vox_n, L.bpm, L.n_render, np.random.default_rng(L.part_seed(prefix + "_shout")), 6))
        if vox_n else np.zeros((L.n, 2)),
    )
    L.log(prefix + "_shout", vox_n)
    return kick_n, parts


PARTS = ("kick", "ohat", "chat", "clap", "rim", "ride", "perc", "vox")


def kit_parts(k: dict) -> tuple:
    return tuple(k[p] for p in PARTS)


def kit_fx(L, seed: int, tops_gain: float = 2.3, vox_gain: float = 1.0):
    """Drum bus: kick with a knock a handheld can play, tops in a small Dragonfly room, claps and shouts on a
    Dragonfly plate, a dub echo on the rim, then a JUCE compressor as glue."""
    def fx(k, o, h, c, r, d, p, v):
        tops = o * 0.9 + h * 0.9 + r * 0.7 + d * 0.7 + p * 0.9
        room = studio.room(tops + c * 0.5, 0.18, 0.06) * 0.5
        plate = studio.plate(c * 0.8 + v * 0.8, 0.1, 0.015) * 0.45
        echo = dsp.delay(r + p * 0.5, L.beat * 0.75, L.beat * 1.5, 0.45, 1.0, 3000) * 0.22
        k = k + dsp.bandpass(k, 700, 2400) * 1.3
        x = k * 1.0 + (tops + c * 0.95) * tops_gain + v * 1.6 * vox_gain + room + plate + echo
        return studio.bus_comp(glue(x, 13.0), -14.0, 2.5, 10.0, 110.0, 1.0)
    return fx


def bass_notes(L, prog, riff_for_bar, held_bars=lambda b: False, octave_c: int = 36) -> list[Note]:
    """riff_for_bar(bar) returns [(beat, length, semitones above the root, velocity)]. In held bars the root is
    held under everything instead (one note per four bars)."""
    notes: list[Note] = []
    for b in range(L.bars):
        at = b * 4.0
        r = root_of(chord_at(prog, at), octave_c)
        if held_bars(b):
            if b % 4 == 0:
                notes.append(Note(at, 15.0, r, 80))
            continue
        for pos, ln, iv, vel in riff_for_bar(b):
            notes.append(Note(at + pos, ln, r + iv, vel))
    return notes


def bass(L, name: str, notes: list[Note], weight: float = 1.0, cutoff: float = 240.0, envamt: float = 1300.0,
         drive: float = 2.4, decay: float = 0.15, res: float = 0.55) -> np.ndarray:
    """The deep bass: a Faust sine sub under detuned saws through a Moog ladder whose envelope makes each note
    pluck. `weight` scales the sub."""
    return faust(L, name, notes, studio.BASS, cutoff=cutoff, envamt=envamt, drive=drive, decay=decay, res=res, sub=1.1 * weight)


def bass_fx(level_db: float = -9.0, width: float = 0.08):
    def fx(x):
        x = dsp.compress(x, -18, 3.5, 0.008, 0.12, makeup_db=3)
        # keep the sub mono and solid; the ladder's harmonics are what a handheld will hear
        return dsp.width(presence(dsp.highpass(x, 30), level_db=level_db), width)
    return fx


def breathing(L, cycles: float, lo: float = 700.0, span: float = 2600.0):
    """A filter cutoff that opens and closes `cycles` times over the loop (an array as long as the effect's input)."""
    def cutoff(n: int) -> np.ndarray:
        return lo + span * (0.5 - 0.5 * np.cos(dsp.TAU * np.arange(n) / (L.n / cycles))) ** 1.5
    return cutoff


def swells(L, spots, seed: int, lo: float = 300.0, hi: float = 6000.0, gain: float = 0.4) -> np.ndarray:
    """Noise swells that rise into the given bars. spots = [(bar it lands on, length in bars)]."""
    out = np.zeros((L.n_render, 2))
    for at_bar, length in spots:
        dsp.place(out, synth.riser(length * 4 * L.beat, np.random.default_rng(seed + at_bar), lo, hi),
                  dsp.secs((at_bar - length) * 4 * L.beat), gain)
    return L.fold(out)
