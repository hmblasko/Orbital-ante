"""Shared parts for the deep house pieces (flight, exploration, combat), so they sound like one record.

Direction from Matthew, 14 Nov 2026: existential, not upbeat, a good deep house beat. All of it is synthesised
by this repository's own code. Everything runs at one tempo, 120, in D minor, so the game can mix from any
piece into any other on a bar line the way a DJ would.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, synth
from oa_audio.sampler import DR
from oa_audio.seq import Note, chord_at, humanize, swing

from .common import bars_of, glue, presence, root_of

BPM = 120.0


def kit(L, rng, drum_bar, prefix: str, hard: float = 0.0):
    """The drum machine. drum_bar(bar) returns step patterns for kick, hat (open), shut (closed), clap, rim and
    optionally ride. `hard` 0 is the soft main-theme kit; 1 is the combat kit (more punch and click, louder tops).
    Returns the kick notes (for side-chaining) and the raw parts."""
    def lane(key, pitch, vel, **kw):
        return bars_of(lambda b: drum_bar(b).get(key, ""), L.bars, pitch, vel=vel, **kw)

    kick_n = lane("kick", DR["kick"], 112)
    punch, click, drive = 150 + 50 * hard, 0.38 + 0.3 * hard, 1.9 + 0.9 * hard
    parts = dict(
        kick=L.hits(prefix + "_kick", kick_n, lambda n, r: synth.kick(n.vel / 127, 45 + 3 * hard, punch, 0.21 - 0.03 * hard, click, drive, r)),
        ohat=L.hits(prefix + "_open_hat", lane("hat", DR["hat_open"], 78 + int(14 * hard)),
                    lambda n, r: synth.hat(n.vel / 127, 0.085, 0.85 + 0.15 * hard, r), pan=0.15),
        chat=L.hits(prefix + "_hats", humanize(swing(lane("shut", DR["hat"], 70 + int(10 * hard), ghost=40), 0.55, 0.25), rng, 0.003, 6, BPM),
                    lambda n, r: synth.hat(n.vel / 127, 0.018, 0.9, r), pan=-0.2),
        clap=L.hits(prefix + "_clap", humanize(lane("clap", DR["clap"], 84 + int(16 * hard)), rng, 0.003, 4, BPM),
                    lambda n, r: synth.clap(n.vel / 127, r, 0.1)),
        rim=L.hits(prefix + "_rim", humanize(lane("rim", DR["rim"], 70), rng, 0.004, 6, BPM), lambda n, r: synth.rim(n.vel / 127, r), pan=0.35),
        ride=L.hits(prefix + "_ride", lane("ride", DR["ride"], 62), lambda n, r: synth.cymbal(n.vel / 127, 0.35, 1.1, r) * 0.5, pan=-0.3),
    )
    return kick_n, parts


def kit_fx(L, seed: int, tops_gain: float = 2.3):
    def fx(k, o, h, c, r, d):
        tops = o * 0.9 + h * 0.9 + c * 0.9 + r * 0.7 + d * 0.7
        far = dsp.reverb(c * 0.9 + r * 0.8, 2.2, 1.0, 0.6, 0.03, 1.4, seed=seed, wet_only=True) * 0.45
        echo = dsp.delay(r, L.beat * 0.75, L.beat * 1.5, 0.45, 1.0, 3000) * 0.25
        # the kick's knock (not its boom) is what a handheld's speakers will play
        k = k + dsp.bandpass(k, 700, 2400) * 1.2
        return glue(k * 1.0 + tops * tops_gain + far + echo, 13.0)
    return fx


def bass_notes(L, prog, riff_for_bar, held_bars=lambda b: False) -> list[Note]:
    """riff_for_bar(bar) returns [(beat, length, semitones above the root, velocity)]. In held bars the root is
    held under everything instead (one note per four bars)."""
    notes: list[Note] = []
    for b in range(L.bars):
        at = b * 4.0
        r = root_of(chord_at(prog, at), 36)
        if held_bars(b):
            if b % 4 == 0:
                notes.append(Note(at, 15.0, r, 80))
            continue
        for pos, ln, iv, vel in riff_for_bar(b):
            notes.append(Note(at + pos, ln, r + iv, vel))
    return notes


def bass_fx(mid_cutoff: float = 520.0, mid: float = 0.5, drive: float = 1.5, level_db: float = -10.0):
    def fx(s, m):
        # the sub is for big speakers; the filtered pulse is what a handheld will hear
        m = dsp.svf(m, mid_cutoff, 1.1, "lp", 2)
        x = dsp.lowpass(s, 180) * 1.0 + m * mid
        x = dsp.compress(dsp.saturate(x, drive, 0.7, 0.04), -18, 3.5, 0.01, 0.15, makeup_db=3)
        return dsp.width(presence(x, level_db=level_db), 0.1)
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
