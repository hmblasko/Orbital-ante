"""Transition cues: short one-shots the music director places around a change of music.

`preSeconds` is how much of the cue comes BEFORE the bar line where the change happens. A riser has a long
lead-in and lands its hit exactly on the bar line; an ending starts on it.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, sampler, synth
from oa_audio.dsp import SR, secs
from oa_audio.sampler import DR, GM, KITS
from oa_audio.seq import Note, line

from .common import glue


def _sf(notes, bpm, seconds, program, drums=False, gain=1.0):
    return sampler.render(notes, bpm, seconds * bpm / 60.0, program, drums, tail=0.0, gain=gain)


def _syn(notes, bpm, seconds, voice, seed, **kw):
    return synth.play(notes, bpm, secs(seconds), voice, np.random.default_rng(seed), **kw)


def _master(x, peak_db=-2.0, fade_out=0.6):
    x = dsp.highpass(x, 30)
    x = dsp.limit(x / (np.max(np.abs(x)) + 1e-9) * 1.25, peak_db)
    return dsp.fade(x, 0.0, fade_out)


def to_combat() -> dict:
    """One bar of the exploration tempo (two bars of the combat tempo): a reversed cymbal and a climbing sweep
    under a snare roll that doubles its speed, then the downbeat lands with a sub drop, a crash and a brass hit."""
    bpm = 172.0
    pre = 8 * 60.0 / bpm
    total = pre + 3.2
    rng = np.random.default_rng(71)
    x = np.zeros((secs(total), 2))
    dsp.place(x, synth.riser(pre, rng, 250, 9000, tone=146.83), 0, 0.8)
    dsp.place(x, synth.reverse_cymbal(pre * 0.8, rng), secs(pre * 0.2), 0.6)
    roll = [Note(k * 0.5, .25, DR["snare"], int(48 + 5 * k)) for k in range(8)] + \
           [Note(4 + k * 0.25, .2, DR["snare"], int(84 + 2.6 * k)) for k in range(16)]
    toms = [Note(7 + k * 0.25, .2, [DR["tom_hi"], DR["tom_hi"], DR["tom_mid"], DR["tom_lo"]][k], 112) for k in range(4)]
    x += dsp.pad_to(_sf(roll + toms, bpm, total, KITS["power"], drums=True), len(x)) * 0.8
    hit = synth.impact(rng, 44, 2.6, 1.0)
    dsp.place(x, hit, secs(pre), 0.4)
    stab = line("[D3 A3 D4 F4 A4 C5 E5]:2", start=8, vel=118)
    x += dsp.pad_to(_sf(stab, bpm, total, GM["brass"]), len(x)) * 0.5
    x += dsp.pad_to(_syn(stab, bpm, total, synth.v_stab, 72, bright=1.2, release=0.4), len(x)) * 0.5
    crash = _sf([Note(8, 2, DR["crash"], 118), Note(8, 1, DR["kick"], 124)], bpm, total, KITS["power"], drums=True)
    x += dsp.pad_to(crash, len(x)) * 0.9
    x = dsp.reverb(x, 1.6, 0.16, 0.5, 0.01, 1.0, seed=601)
    return dict(id="to_combat", audio=_master(x, -1.5, 1.0), preSeconds=round(pre, 6), bpm=bpm,
                note="Riser and snare roll into a downbeat hit. Starts one exploration bar before the change.")


def hit_combat() -> dict:
    """The downbeat hit alone, for when there is no time to play the riser."""
    bpm = 172.0
    rng = np.random.default_rng(73)
    total = 3.0
    x = np.zeros((secs(total), 2))
    dsp.place(x, synth.impact(rng, 44, 2.6, 1.0), 0, 0.35)
    stab = line("[D3 A3 D4 F4 A4 C5 E5]:2", vel=118)
    x += dsp.pad_to(_sf(stab, bpm, total, GM["brass"]), len(x)) * 0.5
    x += dsp.pad_to(_sf([Note(0, 2, DR["crash"], 118), Note(0, 1, DR["kick"], 124)], bpm, total, KITS["power"], drums=True), len(x)) * 0.9
    x = dsp.reverb(x, 1.6, 0.16, 0.5, 0.01, 1.0, seed=602)
    return dict(id="hit_combat", audio=_master(x, -2.0, 1.0), preSeconds=0.0, bpm=bpm, note="Downbeat hit with no lead-in.")


def resolve_combat() -> dict:
    """The fight is over: one last hit, then the minor chord opens out to a bare D with its ninth, held by
    brass, strings and glass while a bell answers with the start of the Exploration theme. A proper ending."""
    bpm = 86.0
    total = 7.5
    rng = np.random.default_rng(75)
    x = np.zeros((secs(total), 2))
    x += dsp.pad_to(_sf([Note(0, 1, DR["kick"], 120), Note(0, 3, DR["crash"], 104), Note(0, .5, DR["snare"], 110)], bpm, total, KITS["power"], drums=True), len(x)) * 0.8
    dsp.place(x, synth.impact(rng, 40, 2.2, 0.6), 0, 0.6)
    chord1 = line("[D2 A2 D3 F3 A3]:1.5", vel=110)
    chord2 = line("r:1.5 [D2 A2 E3 A3 D4 E4]:7", vel=84)
    x += dsp.pad_to(_sf(chord1 + chord2, bpm, total, GM["brass"]), len(x)) * 0.35
    x += dsp.pad_to(_sf(line("r:1.5 [D3 A3 E4 A4 D5]:7", vel=80), bpm, total, GM["strings"]), len(x)) * 0.5
    x += dsp.pad_to(_syn(line("r:1.5 [D3 A3 E4]:6", vel=70), bpm, total, synth.v_pad, 76, attack=0.4, release=2.5), len(x))
    x += dsp.pad_to(_syn(line("r:3 D5:1 A5:3", vel=78), bpm, total, synth.v_bell, 77, decay=2.0, bright=0.6), len(x)) * 0.8
    x += dsp.pad_to(_sf(line("D1:1.5 D1:6", vel=100), bpm, total, GM["finger_bass"]), len(x)) * 0.6
    x = dsp.reverb(x, 3.2, 0.3, 0.5, 0.02, 1.4, seed=603)
    return dict(id="resolve_combat", audio=_master(x, -3.0, 2.5), preSeconds=0.0, bpm=bpm,
                note="A musical ending for combat: final hit, the chord resolves and rings out.")


def to_flight() -> dict:
    """Two bars at the flight tempo: engines-up sweep, a disco string run and tom fill, landing on the downbeat."""
    bpm = 124.0
    pre = 8 * 60.0 / bpm
    total = pre + 2.6
    rng = np.random.default_rng(79)
    x = np.zeros((secs(total), 2))
    dsp.place(x, synth.riser(pre, rng, 300, 11000, tone=110.0), 0, 0.75)
    dsp.place(x, synth.reverse_cymbal(pre * 0.6, rng), secs(pre * 0.4), 0.6)
    run = line("r:4 A4:.5 B4:.5 D5:.5 E5:.5 F#5:.25 G5:.25 A5:.25 B5:.25 C#6:.25 D6:.25 E6:.25 F#6:.25", vel=100)
    x += dsp.pad_to(_sf(run, bpm, total, GM["strings"]), len(x)) * 0.7
    x += dsp.pad_to(_sf(line("r:6.5 [A3 C#4 E4 G4]:1.25", vel=108), bpm, total, GM["brass"]), len(x)) * 0.45
    fill = [Note(6 + k * 0.25, .2, [DR["snare"], DR["snare"], DR["tom_hi"], DR["tom_hi"], DR["tom_mid"], DR["tom_mid"], DR["tom_lo"], DR["tom_lo"]][k], 96 + 3 * k) for k in range(8)]
    kicks = [Note(k, .5, DR["kick"], 100) for k in range(4, 8)]
    x += dsp.pad_to(_sf(fill + kicks + [Note(8, 2, DR["crash"], 112), Note(8, 1, DR["kick"], 120)], bpm, total, KITS["standard"], drums=True), len(x)) * 0.8
    dsp.place(x, synth.impact(rng, 48, 2.0, 1.0), secs(pre), 0.7)
    x += dsp.pad_to(_sf(line("r:8 [G3 B3 D4 F#4 A4]:2", vel=100), bpm, total, GM["rhodes"]), len(x)) * 0.6
    x = dsp.reverb(x, 1.6, 0.18, 0.45, 0.012, 1.0, seed=604)
    return dict(id="to_flight", audio=_master(x, -2.0, 1.0), preSeconds=round(pre, 6), bpm=bpm,
                note="Lift-off: sweep, string run and fill into the flight music's first downbeat.")


def to_calm() -> dict:
    """Energy leaving: a falling sweep, a soft low bell on D, and air. Used when loud music gives way to quiet."""
    total = 5.0
    rng = np.random.default_rng(83)
    n = secs(total)
    t = np.arange(n) / SR
    noise = dsp.stereo(dsp.white(n, rng))
    noise[:, 1] = dsp.white(n, rng)
    down = dsp.svf(noise, 7000 * np.exp(-t / 0.9) + 200, 1.5, "bp") * np.exp(-t / 1.1)[:, None] * 0.35
    x = down + _syn(line("[D3 A3]:2 r:3", vel=80) + line("D5:4", vel=60), 60.0, total, synth.v_bell, 84, decay=2.2, bright=0.4)
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
