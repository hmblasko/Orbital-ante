"""Transition cues: short one-shots the music director places around a change of music.

`preSeconds` is how much of the cue comes BEFORE the bar line where the change happens. A riser has a long
lead-in and lands its hit exactly on the bar line; an ending starts on it.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, synth
from oa_audio.dsp import SR, secs
from oa_audio.seq import Note, line



def _syn(notes, bpm, seconds, voice, seed, **kw):
    return synth.play(notes, bpm, secs(seconds), voice, np.random.default_rng(seed), **kw)


def _master(x, peak_db=-2.0, fade_out=0.6):
    x = dsp.highpass(x, 30)
    x = dsp.limit(x / (np.max(np.abs(x)) + 1e-9) * 1.25, peak_db)
    return dsp.fade(x, 0.0, fade_out)


def _kit_hit(x, at, rng, crash=0.6):
    """The combat piece's own kick and a synthesised crash, so a cue's hit is the first beat of the record."""
    dsp.place(x, dsp.stereo(synth.kick(1.0, 48, 200, 0.18, 0.68, 2.8, rng)), secs(at), 0.9)
    dsp.place(x, dsp.stereo(synth.cymbal(0.9, 1.4, 0.9, rng)), secs(at), crash * 0.5)


def to_combat() -> dict:
    """One bar at 120: a climbing sweep and a reversed cymbal under a clap roll that doubles its speed, then
    the downbeat lands with a sub drop, the kick, a crash and a hard minor stab. All synthesised."""
    bpm = 120.0
    pre = 4 * 60.0 / bpm
    total = pre + 3.2
    rng = np.random.default_rng(71)
    x = np.zeros((secs(total), 2))
    dsp.place(x, synth.riser(pre, rng, 250, 9000, tone=146.83), 0, 0.8)
    dsp.place(x, synth.reverse_cymbal(pre * 0.8, rng), secs(pre * 0.2), 0.6)
    beat = 60.0 / bpm
    for k in range(4):
        dsp.place(x, dsp.stereo(synth.clap(0.45 + 0.05 * k, rng, 0.08)), secs(k * 0.5 * beat), 0.7)
    for k in range(8):
        dsp.place(x, dsp.stereo(synth.snare(0.6 + 0.05 * k, 185 + 6 * k, 1.0, 0.1, rng)), secs((2 + k * 0.25) * beat), 0.6)
    for k in range(4):
        dsp.place(x, dsp.stereo(synth.tom(1.0, 150 - 22 * k, 0.2, rng)), secs((3 + k * 0.25) * beat), 0.8)
    dsp.place(x, synth.impact(rng, 44, 2.6, 1.0), secs(pre), 0.4)
    stab = line("[D3 A3 D4 F4 A4 C5 E5]:1.5", start=4, vel=118)
    x += dsp.pad_to(_syn(stab, bpm, total, synth.v_stab, 72, bright=1.1, release=0.4), len(x)) * 0.9
    _kit_hit(x, pre, rng)
    x = dsp.reverb(x, 1.6, 0.16, 0.5, 0.01, 1.0, seed=601)
    return dict(id="to_combat", audio=_master(x, -1.5, 1.0), preSeconds=round(pre, 6), bpm=bpm,
                note="Riser and roll into a downbeat hit. Starts one bar before the change.")


def hit_combat() -> dict:
    """The downbeat hit alone, for when there is no time to play the riser."""
    bpm = 120.0
    rng = np.random.default_rng(73)
    total = 3.0
    x = np.zeros((secs(total), 2))
    dsp.place(x, synth.impact(rng, 44, 2.6, 1.0), 0, 0.35)
    stab = line("[D3 A3 D4 F4 A4 C5 E5]:1.5", vel=118)
    x += dsp.pad_to(_syn(stab, bpm, total, synth.v_stab, 74, bright=1.1, release=0.4), len(x)) * 0.9
    _kit_hit(x, 0.0, rng)
    x = dsp.reverb(x, 1.6, 0.16, 0.5, 0.01, 1.0, seed=602)
    return dict(id="hit_combat", audio=_master(x, -2.0, 1.0), preSeconds=0.0, bpm=bpm, note="Downbeat hit with no lead-in.")


def resolve_combat() -> dict:
    """The fight is over: one last kick, then the minor chord opens out to a bare D with its ninth on a pad
    while a far-off plucked string answers with the start of the Exploration theme. A proper ending."""
    bpm = 120.0
    total = 7.5
    rng = np.random.default_rng(75)
    x = np.zeros((secs(total), 2))
    _kit_hit(x, 0.0, rng, crash=0.4)
    dsp.place(x, synth.impact(rng, 40, 2.2, 0.6), 0, 0.6)
    x += dsp.pad_to(_syn(line("[D3 A3 D4 F4 A4]:1.5", vel=108), bpm, total, synth.v_stab, 78, bright=0.8, release=0.5), len(x)) * 0.7
    x += dsp.pad_to(_syn(line("r:2 [D2 A2 D3 A3 E4 A4]:9", vel=72), bpm, total, synth.v_pad, 76, attack=0.5, release=2.5, bright=0.7), len(x))
    x += dsp.pad_to(_syn(line("r:4 D4:2 A4:5", vel=84), bpm, total, synth.v_string_pluck, 77, bright=0.5), len(x)) * 0.9
    x += dsp.pad_to(_syn(line("D1:2 r:.5 D1:8", vel=100), bpm, total, synth.v_sub, 79, release=0.6), len(x)) * 0.5
    x = dsp.reverb(x, 3.2, 0.3, 0.5, 0.02, 1.4, seed=603)
    return dict(id="resolve_combat", audio=_master(x, -3.0, 2.5), preSeconds=0.0, bpm=bpm,
                note="A musical ending for combat: final hit, the chord resolves and rings out.")


def to_flight() -> dict:
    """Two bars at 120: an engines-up sweep, a climbing plucked run in D minor and four kicks, landing on the
    flight music's first downbeat. All synthesised."""
    bpm = 120.0
    pre = 8 * 60.0 / bpm
    total = pre + 2.6
    rng = np.random.default_rng(79)
    beat = 60.0 / bpm
    x = np.zeros((secs(total), 2))
    dsp.place(x, synth.riser(pre, rng, 300, 9000, tone=110.0), 0, 0.75)
    dsp.place(x, synth.reverse_cymbal(pre * 0.6, rng), secs(pre * 0.4), 0.6)
    run = line("r:4 A3:.5 C4:.5 D4:.5 E4:.5 F4:.5 A4:.5 C5:.5 D5:.5", vel=92)
    r = _syn(run, bpm, total, synth.v_pluck, 80, bright=0.8, decay=0.3)
    x += dsp.pad_to(r + dsp.delay(r, beat * 0.75, beat * 1.5, 0.4, 1.0, 2800) * 0.4, len(x)) * 0.9
    for k in range(4, 8):
        dsp.place(x, dsp.stereo(synth.kick(0.85, 46, 160, 0.2, 0.45, 2.0, rng)), secs(k * beat), 0.7)
    _kit_hit(x, pre, rng, crash=0.5)
    dsp.place(x, synth.impact(rng, 48, 2.0, 1.0), secs(pre), 0.5)
    x += dsp.pad_to(_syn(line("r:8 [D3 A3 E4 F4 C5]:2", vel=96), bpm, total, synth.v_stab, 81, bright=0.7, release=0.8), len(x)) * 0.7
    x = dsp.reverb(x, 1.8, 0.2, 0.45, 0.012, 1.0, seed=604)
    return dict(id="to_flight", audio=_master(x, -2.0, 1.0), preSeconds=round(pre, 6), bpm=bpm,
                note="Lift-off: sweep, climbing run and kicks into the flight music's first downbeat.")


def to_calm() -> dict:
    """Energy leaving: a falling sweep, a soft low tine on D, and air. Used when loud music gives way to quiet."""
    total = 5.0
    rng = np.random.default_rng(83)
    n = secs(total)
    t = np.arange(n) / SR
    noise = dsp.stereo(dsp.white(n, rng))
    noise[:, 1] = dsp.white(n, rng)
    down = dsp.svf(noise, 7000 * np.exp(-t / 0.9) + 200, 1.5, "bp") * np.exp(-t / 1.1)[:, None] * 0.35
    x = down + _syn(line("[D3 A3]:2 r:3", vel=80) + line("D5:4", vel=60), 60.0, total, synth.v_tine, 84, release=2.0)
    x = dsp.reverb(x, 3.5, 0.35, 0.5, 0.03, 1.5, seed=605)
    return dict(id="to_calm", audio=_master(x, -8.0, 2.0), preSeconds=0.0, bpm=60.0,
                note="A soft landing when loud music hands over to quiet.")


def build_all() -> list[dict]:
    out = []
    for make in (to_combat, hit_combat, resolve_combat, to_flight, to_calm):
        cue = make()
        cue["seconds"] = round(len(cue["audio"]) / SR, 4)
        out.append(cue)
    return out
