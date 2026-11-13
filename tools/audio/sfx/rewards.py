"""Rewards and interface sounds.

These are musical. They all come from the Orbital Ante theme's first three notes (A, B, D: the "ante cell")
and stay in D, so a reward always sounds in tune with whatever the soundtrack is doing. Bigger rewards play
more of the theme with richer instruments; that is the ladder from a point tick up to a celebration.
"""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, sampler, synth
from oa_audio.dsp import SR, secs
from oa_audio.sampler import GM
from oa_audio.seq import Note, hz, line, midi

from .common import finish, metal_hit, sfx

UI = dict(bus="UI", spatial=0.0, vacuum="ui")
BPM = 120.0


def _sampled(notes: list[Note], program: int, seconds: float, gain: float = 0.8) -> np.ndarray:
    beats = seconds * BPM / 60.0
    return sampler.render(notes, BPM, beats, program, tail=0.0, gain=gain)


def _synth(notes: list[Note], voice, seconds: float, rng, **kw) -> np.ndarray:
    return synth.play(notes, BPM, secs(seconds), voice, rng, **kw)


def _sparkle(n: int, rng, start: float, dur: float, density: float = 40.0, level: float = 0.05) -> np.ndarray:
    """A scatter of tiny high bell grains, like light catching on coins."""
    out = np.zeros((n, 2))
    scale = [midi(p) for p in ["D7", "E7", "F#7", "A7", "B7", "D8"]]
    count = int(dur * density)
    for _ in range(count):
        at = start + dur * rng.uniform() ** 0.7
        m = secs(0.12)
        tt = np.arange(m) / SR
        f = hz(rng.choice(scale))
        g = np.sin(dsp.TAU * f * tt) * np.exp(-tt / 0.025)
        dsp.place(out, dsp.pan(g, rng.uniform(-0.9, 0.9)), secs(at), level * rng.uniform(0.3, 1))
    return out


@sfx("reward.points.tick", "rewards", volumeDb=-15, pitchVar=0.0, cooldownMs=28, maxVoices=4, priority=140,
     pitchSteps=[0, 2, 4, 7, 9, 12, 14, 16, 19], **UI,
     note="One point counted. The game steps its pitch up the D pentatonic scale as a streak builds, so a run of points climbs like a slot machine paying out.")
def points_tick(rng, v):
    n = secs(0.16)
    t = np.arange(n) / SR
    f = hz(midi("D6"))
    x = dsp.fm(f, n, 4.0, 1.4 * np.exp(-t / 0.012)) * np.exp(-t / 0.035)
    x += metal_hit(rng, 0.16, [f * 2.76, f * 5.4], decays=[0.02, 0.01], gains=[0.5, 0.3], hardness=0.4) * 0.3
    return finish(x, -6, 0.02)


@sfx("reward.points.rack", "rewards", loop=True, volumeDb=-17, priority=150, **UI,
     note="Points racking up: coins pouring at sixteen a second for the count-up animation. Stop it with reward.points.rack_end.")
def points_rack(rng, v):
    n = secs(1.0)
    x = np.zeros((n, 2))
    notes = ["D6", "A6", "F#6", "A6", "B6", "A6", "F#6", "D7"]
    for i in range(16):
        f = hz(midi(notes[i % 8]))
        coin = metal_hit(rng, 0.09, [f, f * 2.41, f * 3.9, f * 5.3], decays=[0.03, 0.02, 0.012, 0.008],
                         gains=[1, 0.6, 0.35, 0.2], hardness=0.8)
        dsp.place(x, dsp.pan(coin, 0.5 * np.sin(i * 1.3)), secs(i / 16.0), 0.6 + 0.4 * (i % 4 == 0))
    wrap = x.copy()
    return finish(wrap, -7, 0.0)


@sfx("reward.points.rack_end", "rewards", volumeDb=-11, priority=90, **UI)
def points_rack_end(rng, v):
    """The count-up landing: the last coins settle and the ante cell rings out once (A, B, D)."""
    secs_len = 1.4
    notes = line("A5:.25 B5:.25 D6:1.5", vel=105)
    x = _sampled(notes, GM["glock"], secs_len, 0.9) * 0.7 + _synth(notes, synth.v_pluck, secs_len, rng, bright=1.3) * 0.6
    x += _sparkle(len(x), rng, 0.25, 0.5, 30, 0.04)
    x = dsp.reverb(x, 1.2, 0.2, 0.4, seed=31)
    return finish(x, -4, 0.3)


@sfx("reward.tier1", "rewards", volumeDb=-13, cooldownMs=120, maxVoices=2, priority=130, **UI)
def tier1(rng, v):
    """Small reward: one bright, short pair of notes (A and D together)."""
    x = _synth(line("[A5 D6]:.5", vel=100), synth.v_pluck, 0.6, rng, bright=1.2, decay=0.18)
    x += _synth(line("D7:.25", vel=70), synth.v_bell, 0.6, rng, decay=0.25) * 0.4
    return finish(x, -5, 0.1)


@sfx("reward.tier2", "rewards", volumeDb=-11, cooldownMs=250, maxVoices=2, priority=100, **UI)
def tier2(rng, v):
    """Good reward: the ante cell as a quick rising arpeggio on celesta, with a soft chord under it."""
    notes = line("A5:.25 B5:.25 D6:1", vel=105)
    x = _sampled(notes, GM["celesta"], 1.2, 0.9) + _synth(notes, synth.v_pluck, 1.2, rng, bright=1.1) * 0.5
    x += _synth(line("r:.5 [D4 A4 F#5]:1", vel=70), synth.v_tine, 1.2, rng) * 0.6
    x = dsp.reverb(x, 1.0, 0.18, 0.4, seed=32)
    return finish(x, -4, 0.25)


@sfx("reward.tier3", "rewards", volumeDb=-9, cooldownMs=600, maxVoices=1, priority=60, **UI)
def tier3(rng, v):
    """Big reward: the cell, then the theme's leap up and step back, on vibraphone over a major-ninth chord."""
    mel = line("A5:.25 B5:.25 D6:.5 F#6:.375 E6:.375 D6:1.75", vel=108)
    x = _sampled(mel, GM["vibes"], 2.4, 0.9) + _sampled(mel, GM["glock"], 2.4, 0.9) * 0.35
    x += _synth(mel, synth.v_pluck, 2.4, rng, bright=1.2) * 0.35
    x += _sampled(line("r:.5 [D3 A3]:3", vel=85), GM["finger_bass"], 2.4, 0.9) * 0.6
    x += _synth(line("r:.5 [F#4 A4 C#5 E5]:2.5", vel=80), synth.v_tine, 2.4, rng) * 0.8
    x += _sparkle(len(x), rng, 0.5, 0.9, 35, 0.045)
    x = dsp.reverb(x, 1.6, 0.22, 0.45, seed=33)
    return finish(x, -3, 0.5, limit=True)


@sfx("reward.tier4", "rewards", volumeDb=-7, cooldownMs=2500, maxVoices=1, priority=20, **UI)
def tier4(rng, v):
    """Exceptional: the whole first phrase of the Orbital Ante theme as a fanfare. Brass, vibes and glockenspiel,
    a run up into it, the theme's flattened-seventh wink in the harmony, and a cymbal to finish."""
    L = 4.6
    run = line("D5:.125 E5:.125 F#5:.125 A5:.125 B5:.125 D6:.125 E6:.125 F#6:.125", vel=95)
    mel = line("r:1 A5:.5 B5:.5 D6:1 F#6:.75 E6:.75 C6:.5 D6:2.5", vel=112)
    brass = line("r:1 [D4 F#4 A4]:.4 r:.6 [D4 F#4 B4]:.4 r:.6 [E4 G4 Bb4 D5]:1 r:.5 [F#4 A4 D5 E5]:2.5", vel=105)
    bass = line("r:1 D2:1 D2:1 C2:1.5 D2:2.5", vel=105)
    x = _sampled(run, GM["harp"], L, 0.9) * 0.7 + _sampled(run, GM["glock"], L, 0.9) * 0.4
    x += _sampled(mel, GM["vibes"], L, 0.9) + _sampled(mel, GM["glock"], L, 0.9) * 0.4
    x += _sampled(mel, GM["trumpet"], L, 0.9) * 0.45
    x += _sampled(brass, GM["brass"], L, 0.9) * 0.7 + _synth(brass, synth.v_stab, L, rng, bright=0.9) * 0.5
    x += _sampled(bass, GM["finger_bass"], L, 0.9) * 0.8
    cym = synth.cymbal(1.0, 1.4, 1.0, rng)
    dsp.place(x, dsp.stereo(cym), secs(0.5), 0.5)
    dsp.place(x, dsp.stereo(synth.cymbal(1.0, 1.8, 0.9, rng)), secs(2.25), 0.45)
    x += _sparkle(len(x), rng, 0.5, 2.5, 45, 0.05)
    x = dsp.reverb(x, 2.0, 0.22, 0.45, seed=34)
    x = dsp.compress(x, -16, 2.5, 0.01, 0.2, makeup_db=2)
    return finish(x, -2, 0.9, limit=True)


@sfx("reward.multiplier", "rewards", volumeDb=-10, cooldownMs=200, maxVoices=2, priority=70,
     pitchSteps=[0, 2, 4, 5, 7, 9, 11, 12], **UI,
     note="Multiplier goes up: the ante cell flicked upward with a zip. The game raises its pitch one scale step per multiplier level.")
def multiplier(rng, v):
    L = 0.9
    notes = line("A5:.125 B5:.125 D6:.75", vel=110)
    x = _synth(notes, synth.v_pluck, L, rng, bright=1.4, decay=0.22)
    x += _sampled(notes, GM["marimba"], L, 0.9) * 0.6
    n = len(x)
    t = np.arange(n) / SR
    zip_ = dsp.svf(dsp.white(n, rng), 800 * 2 ** (np.clip(t / 0.12, 0, 1) * 3), 4.0, "bp") * np.clip(t / 0.12, 0, 1) * (t < 0.13) * 0.25
    x += dsp.stereo(zip_)
    x = dsp.reverb(x, 0.8, 0.15, 0.4, seed=35)
    return finish(x, -4, 0.2)


@sfx("reward.loot.common", "rewards", variants=2, volumeDb=-13, pitchVar=0.7, cooldownMs=100, maxVoices=2,
     priority=120, **UI)
def loot_common(rng, v):
    """Picking something up: a pouch rustle and one warm plucked note."""
    n = secs(0.5)
    t = np.arange(n) / SR
    rustle = dsp.bandpass(dsp.white(n, rng), 1200, 7000) * np.exp(-((t - 0.04) / 0.03) ** 2) * 0.3
    x = dsp.stereo(rustle) + dsp.pad_to(_synth(line("r:.1 D5:.5", vel=95), synth.v_string_pluck, 0.5, rng), n)
    return finish(x, -5, 0.12)


@sfx("reward.loot.rare", "rewards", volumeDb=-8, cooldownMs=1500, maxVoices=1, priority=30, **UI)
def loot_rare(rng, v):
    """Rare find: bells reach up through the Cosmic Mystery theme's sharpened fourth and resolve, in a halo of light."""
    L = 3.2
    mel = line("D5:.5 A5:.5 G#5:.5 A5:.5 D6:3", vel=100)
    x = _synth(mel, synth.v_bell, L, rng, decay=1.2) + _sampled(mel, GM["celesta"], L, 0.9) * 0.5
    x += _synth(line("[D4 A4 E5]:5", vel=60), synth.v_glass, L, rng, attack=0.3, release=1.0)
    x += _sparkle(len(x), rng, 0.0, 2.2, 40, 0.05)
    x = dsp.reverb(x, 2.6, 0.3, 0.35, seed=36)
    return finish(x, -3, 0.8)


@sfx("reward.discovery", "rewards", volumeDb=-9, cooldownMs=4000, maxVoices=1, priority=25, **UI)
def discovery(rng, v):
    """New place found: the Exploration theme's opening (D up to A, settling on G) on harp and glass, opening out."""
    L = 4.2
    mel = line("D4:.75 A4:.75 G4:3", vel=100)
    hi = line("r:1.5 D5:.5 A5:.5 G5:2", vel=75)
    x = _sampled(mel, GM["harp"], L, 0.9) + _sampled(hi, GM["celesta"], L, 0.9) * 0.6
    x += _synth(mel, synth.v_bell, L, rng, decay=1.6, bright=0.6) * 0.5
    x += _synth(line("[D3 A3 E4 G4]:6", vel=70), synth.v_pad, L, rng, attack=0.5, release=1.5)
    x += _synth(line("r:.75 [A4 D5 E5]:5", vel=55), synth.v_glass, L, rng, attack=0.8, release=1.5)
    x = dsp.reverb(x, 3.0, 0.32, 0.4, seed=37)
    return finish(x, -4, 1.2)


@sfx("reward.repair", "rewards", volumeDb=-8, cooldownMs=300, maxVoices=2, priority=50, **UI,
     note="The one repair sound, shared by ship and satellite repairs: a ratchet running up, a seal seating with a puff of air, and two notes (A up to D) that say 'fixed'.")
def repair(rng, v):
    n = secs(1.5)
    x = np.zeros(n)
    at = 0.0
    gap = 0.085
    for i in range(7):
        ratchet = metal_hit(rng, 0.06, [2200 + 90 * i, 3500 + 120 * i, 5400], decays=[0.01, 0.007, 0.004], hardness=1.3)
        dsp.place(x, ratchet, secs(at), 0.4 + 0.07 * i)
        at += gap
        gap *= 0.86
    seat = metal_hit(rng, 0.25, [640, 1250, 1900, 2850], hardness=1.2)
    dsp.place(x, seat, secs(at + 0.03), 1.0)
    m = secs(0.3)
    tt = np.arange(m) / SR
    puff = dsp.bandpass(dsp.white(m, rng), 1500, 8000) * np.minimum(tt / 0.005, 1) * np.exp(-tt / 0.06) * 0.25
    dsp.place(x, puff, secs(at + 0.05), 1.0)
    thunk = dsp.sine(92, m) * np.exp(-tt / 0.04)
    dsp.place(x, thunk, secs(at + 0.03), 0.6)
    out = dsp.stereo(x)
    chime = _synth(line("A5:.25 D6:1.5", start=(at + 0.16) * 2, vel=100), synth.v_bell, 1.5, rng, decay=0.5, bright=0.7)
    out = dsp.pad_to(out, len(chime)) + chime * 0.7
    out = dsp.reverb(out, 0.9, 0.12, 0.5, seed=38)
    return finish(out, -3, 0.2)


# ----------------------------------------------------------------------------- menus

@sfx("ui.move", "ui", volumeDb=-20, pitchVar=0.3, cooldownMs=30, maxVoices=2, priority=200, **UI)
def ui_move(rng, v):
    """Moving between menu items: a soft wooden tick."""
    x = metal_hit(rng, 0.06, [1174, 1760, 2637], decays=[0.012, 0.008, 0.005], gains=[1, 0.4, 0.2], hardness=0.3)
    return finish(x, -8, 0.01)


@sfx("ui.select", "ui", volumeDb=-15, cooldownMs=60, maxVoices=2, priority=150, **UI)
def ui_select(rng, v):
    """Choosing a menu item: a tick with a little upward note (A to D) behind it."""
    x = dsp.stereo(dsp.pad_to(metal_hit(rng, 0.06, [1174, 1760, 2637], hardness=0.5), secs(0.35)))
    x += _synth(line("A5:.1 D6:.4", vel=90), synth.v_pluck, 0.35, rng, bright=0.9, decay=0.1) * 0.8
    return finish(x, -6, 0.08)


@sfx("ui.back", "ui", volumeDb=-16, cooldownMs=60, maxVoices=2, priority=150, **UI)
def ui_back(rng, v):
    """Backing out: the same two notes falling (D to A)."""
    x = _synth(line("D6:.1 A5:.4", vel=85), synth.v_pluck, 0.35, rng, bright=0.7, decay=0.1)
    return finish(x, -6, 0.08)
