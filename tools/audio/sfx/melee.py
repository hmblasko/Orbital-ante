"""The five found melee weapons (Milestone 13). Each has its own swing and its own hit, so the weapon is known
by ear: the sword hums and sizzles, the bat cracks, the mailbox clangs hollow, the knife is quick and quiet,
the pan rings like a bell. All made here from noise and partials, not taken from any game."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import finish, finish_loop, metal_hit, sfx

OWN = dict(bus="PlayerEquipment", spatial=0.0, priority=30, vacuum="self", maxVoices=3)
HIT = dict(bus="CombatImpacts", spatial=1.0, minDist=2, maxDist=50, priority=25, vacuum="air", maxVoices=4)


def _t(n):
    return np.arange(n) / SR


def _whoosh(rng, length, lo, hi, peak_at=0.4, weight=1.0):
    """Air torn by something swung: band-limited noise that sweeps up and back with a swell."""
    n = secs(length)
    t = _t(n)
    env = np.exp(-((t / length - peak_at) ** 2) / 0.03)
    x = np.zeros(n)
    for k in range(3):
        f = lo + (hi - lo) * (0.4 + 0.6 * env) * (1 + 0.15 * k)
        band = dsp.bandpass(dsp.white(n, rng), max(80.0, float(np.mean(f)) * 0.6), float(np.mean(f)) * 1.6)
        x += band * env * (0.6 / (k + 1))
    x += dsp.lowpass(dsp.white(n, rng), lo) * env * 0.4 * weight
    return x


def _thud(rng, length, f0, decay, crack=0.0):
    """A body blow: a low drop in pitch, a slap of noise, and optionally a dry crack on top."""
    n = secs(length)
    t = _t(n)
    x = dsp.sine(f0 * (1 + 1.2 * np.exp(-t / 0.008)), n) * np.exp(-t / decay)
    x += dsp.lowpass(dsp.white(n, rng), 1800) * np.exp(-t / 0.025) * 0.7
    if crack:
        x += dsp.bandpass(dsp.white(n, rng), 2500, 9000) * np.exp(-t / 0.006) * crack
    return x


# ----------------------------------------------------------------------------- shared

@sfx("melee.pickup", "melee", volumeDb=-8, pitchVar=0.8, volVarDb=1.0, cooldownMs=200, **OWN)
def pickup(rng, v):
    """Taking a melee weapon in hand: a grip creak and a soft knock."""
    n = secs(0.35)
    t = _t(n)
    x = dsp.bandpass(dsp.white(n, rng), 400, 2500) * np.exp(-t / 0.06) * 0.6
    x += dsp.sine(260, n) * np.exp(-t / 0.04) * 0.5
    return finish(x, -4, 0.05)


@sfx("melee.drop", "melee", volumeDb=-10, pitchVar=1.0, volVarDb=1.5, cooldownMs=200, **OWN)
def drop(rng, v):
    """Letting go of one: a dull clatter."""
    x = _thud(rng, 0.4, 250, 0.06, crack=0.5)
    return finish(x, -4, 0.05, hp=170)


@sfx("melee.hit.wall", "melee", variants=2, pitchVar=1.0, volumeDb=-6, **HIT)
def hit_wall(rng, v):
    """A swing into rock or a wall: a jarring knock."""
    x = _thud(rng, 0.35, 230 + 30 * v, 0.05, crack=0.7)
    return finish(dsp.saturate(x, 1.5), -3, 0.05, hp=170)


@sfx("melee.hit.metal", "melee", variants=2, pitchVar=1.0, volumeDb=-6, **HIT)
def hit_metal(rng, v):
    """A swing into a gun or a metal thing: a clank with a short ring."""
    x = metal_hit(rng, 0.5, [620 + 80 * v, 1430, 2320, 3710], decays=[0.12, 0.08, 0.05, 0.03], hardness=1.6)
    return finish(x, -3, 0.08)


# ----------------------------------------------------------------------------- laser sword

@sfx("melee.laser_sword.hum", "melee", volumeDb=-14, loop=True, **OWN)
def sword_hum(rng, v):
    """The blade's hum while held: two close tones beating slowly over a soft buzz. The game sets its level."""
    n = secs(6.0)
    t = _t(n)
    x = dsp.sine(98, n) * 0.5 + dsp.sine(98.7, n) * 0.45 + dsp.sine(196.3, n) * 0.18
    x += dsp.bandpass(dsp.white(n, rng), 300, 1200) * 0.05 * (1 + 0.3 * np.sin(dsp.TAU * 0.5 * t))
    return finish_loop(x, -8, 1.0)


@sfx("melee.laser_sword.swing", "melee", variants=3, pitchVar=1.0, volumeDb=-5, **OWN)
def sword_swing(rng, v):
    """A sword swing: the hum swept up a fifth and back (a Doppler swoosh) over a hiss of air."""
    length = 0.4
    n = secs(length)
    t = _t(n)
    env = np.exp(-((t / length - 0.35) ** 2) / 0.04)
    f = 196 * (1 + 0.5 * env)
    x = (dsp.sine(f, n) * 0.7 + dsp.sine(f * 2.01, n) * 0.3) * (0.3 + env)
    x += _whoosh(rng, length, 900, 3500, 0.35) * 0.5
    return finish(dsp.saturate(x, 1.3), -3, 0.05)


@sfx("melee.laser_sword.hit", "melee", variants=3, pitchVar=1.0, volumeDb=-3, **HIT)
def sword_hit(rng, v):
    """The blade through a limb: a crackling sizzle and a burst of the hum, no thud (it cuts, it does not strike)."""
    n = secs(0.5)
    t = _t(n)
    crackle = dsp.bandpass(dsp.white(n, rng), 1500, 8000) * (rng.random(n) < 0.08) * np.exp(-t / 0.15) * 2.0
    sizzle = dsp.bandpass(dsp.white(n, rng), 3000, 10000) * np.exp(-t / 0.2) * 0.5
    burst = (dsp.sine(196, n) + dsp.sine(294, n) * 0.5) * np.exp(-t / 0.08) * 0.6
    return finish(crackle + sizzle + burst, -3, 0.08)


@sfx("melee.laser_sword.sweet", "melee", variants=1, volumeDb=-3, pitchVar=0.4, volVarDb=1.0, **HIT)
def sword_sweet(rng, v):
    """Same as a hit, longer and brighter."""
    return sword_hit(rng, 1)


@sfx("melee.laser_sword.overheat", "melee", volumeDb=-6, cooldownMs=500, **OWN)
def sword_overheat(rng, v):
    """Too many swings: the hum stutters and drops away with a hiss of venting heat."""
    n = secs(1.0)
    t = _t(n)
    f = 98 * np.exp(-t / 0.4)
    x = dsp.sine(f, n) * np.exp(-t / 0.3) * (0.5 + 0.5 * (np.sin(dsp.TAU * 18 * t) > 0))
    x += dsp.bandpass(dsp.white(n, rng), 2000, 8000) * np.minimum(t / 0.05, 1) * np.exp(-t / 0.5) * 0.6
    return finish(x, -4, 0.1)


@sfx("melee.laser_sword.locked", "melee", volumeDb=-10, cooldownMs=300, **OWN)
def sword_locked(rng, v):
    """A swing tried while it cools: a dead click and a fizz."""
    n = secs(0.2)
    t = _t(n)
    x = dsp.bandpass(dsp.white(n, rng), 1000, 5000) * np.exp(-t / 0.01)
    x += dsp.bandpass(dsp.white(n, rng), 3000, 9000) * np.exp(-t / 0.08) * 0.3
    return finish(x, -6, 0.03)


@sfx("melee.laser_sword.ready", "melee", volumeDb=-8, cooldownMs=500, **OWN)
def sword_ready(rng, v):
    """Cooled and ready: the hum springs back, rising into place."""
    n = secs(0.5)
    t = _t(n)
    f = 196 * (0.75 + 0.25 * np.minimum(t / 0.25, 1))
    x = (dsp.sine(f, n) + dsp.sine(f * 2.01, n) * 0.4) * np.minimum(t / 0.05, 1) * np.exp(-np.maximum(t - 0.3, 0) / 0.08)
    return finish(x, -5, 0.05)


# ----------------------------------------------------------------------------- baseball bat

@sfx("melee.baseball_bat.swing", "melee", variants=3, pitchVar=1.0, volumeDb=-6, **OWN)
def bat_swing(rng, v):
    """A bat swing: a full, round whoosh."""
    return finish(_whoosh(rng, 0.45, 300, 1600, 0.45), -4, 0.05)


@sfx("melee.baseball_bat.hit", "melee", variants=3, pitchVar=1.0, volumeDb=-3, **HIT)
def bat_hit(rng, v):
    """A bat on a body: a solid, hollow thock."""
    x = _thud(rng, 0.4, 260, 0.07, crack=0.6)
    x += dsp.sine(420, len(x)) * np.exp(-_t(len(x)) / 0.03) * 0.3
    return finish(dsp.saturate(x, 1.6), -3, 0.05, hp=170)


@sfx("melee.baseball_bat.sweet", "melee", variants=2, pitchVar=0.6, volumeDb=-1, **HIT)
def bat_sweet(rng, v):
    """The sweet spot: a sharp crack like a home run, with a ring of wood under it."""
    n = secs(0.6)
    t = _t(n)
    x = dsp.bandpass(dsp.white(n, rng), 1200, 9000) * np.exp(-t / 0.004) * 2.0
    x += (dsp.sine(520, n) + dsp.sine(1180, n) * 0.5) * np.exp(-t / 0.06) * 0.6
    x += _thud(rng, 0.6, 120, 0.06) * 0.6
    return finish(dsp.saturate(x, 1.4), -2, 0.08)


# ----------------------------------------------------------------------------- mailbox

@sfx("melee.mailbox.swing", "melee", variants=2, pitchVar=0.8, volumeDb=-5, **OWN)
def mailbox_swing(rng, v):
    """Heaving the mailbox round: a slow, heavy whoosh and the rattle of something loose inside."""
    length = 0.8
    x = _whoosh(rng, length, 150, 700, 0.55, weight=1.5)
    n = len(x)
    t = _t(n)
    rattle = dsp.bandpass(dsp.white(n, rng), 1500, 5000) * (rng.random(n) < 0.004) * 6.0 * np.exp(-t / 0.5)
    return finish(x + dsp.lowpass(rattle, 6000), -4, 0.06)


@sfx("melee.mailbox.hit", "melee", variants=3, pitchVar=1.0, volumeDb=-1, **HIT)
def mailbox_hit(rng, v):
    """The mailbox on a body: a big hollow steel bong over a heavy thump."""
    x = metal_hit(rng, 0.9, [300 + 20 * v, 470, 890, 1460], decays=[0.3, 0.18, 0.1, 0.06], hardness=1.2)
    th = _thud(rng, 0.9, 190, 0.1)
    return finish(dsp.saturate(x * 0.8 + th, 1.5), -2, 0.1, hp=170)


@sfx("melee.mailbox.sweet", "melee", variants=1, volumeDb=-1, pitchVar=0.6, volVarDb=1.0, **HIT)
def mailbox_sweet(rng, v):
    return mailbox_hit(rng, 2)


# ----------------------------------------------------------------------------- butterfly knife

@sfx("melee.butterfly_knife.swing", "melee", variants=3, pitchVar=1.5, volumeDb=-10, **OWN)
def knife_swing(rng, v):
    """A quick, thin swish and the click of the handles."""
    x = _whoosh(rng, 0.18, 2000, 7000, 0.4) * 0.7
    n = len(x)
    t = _t(n)
    x += dsp.bandpass(dsp.white(n, rng), 3000, 9000) * np.exp(-t / 0.002) * 0.6
    return finish(x, -5, 0.03)


@sfx("melee.butterfly_knife.hit", "melee", variants=3, pitchVar=1.2, volumeDb=-5, **HIT)
def knife_hit(rng, v):
    """A stab: short, wet and quiet."""
    n = secs(0.25)
    t = _t(n)
    x = dsp.bandpass(dsp.white(n, rng), 500, 3000) * np.exp(-t / 0.03)
    x += dsp.sine(180, n) * np.exp(-t / 0.02) * 0.4
    return finish(x, -4, 0.04)


@sfx("melee.butterfly_knife.sweet", "melee", volumeDb=-5, pitchVar=0.6, volVarDb=1.0, **HIT)
def knife_sweet(rng, v):
    return knife_hit(rng, 1)


@sfx("melee.butterfly_knife.takedown", "melee", volumeDb=-3, cooldownMs=300, **HIT)
def knife_takedown(rng, v):
    """A takedown from behind: a deep, final thump and a short low swell, so it lands as an event."""
    n = secs(0.9)
    t = _t(n)
    x = _thud(rng, 0.9, 150, 0.15) * 1.2
    x += dsp.bandpass(dsp.white(n, rng), 400, 2000) * np.exp(-t / 0.04) * 0.5
    x += dsp.sine(110, n) * np.minimum(t / 0.1, 1) * np.exp(-t / 0.35) * 0.5
    return finish(x, -2, 0.1, hp=170)


# ----------------------------------------------------------------------------- frying pan

@sfx("melee.frying_pan.swing", "melee", variants=3, pitchVar=1.0, volumeDb=-7, **OWN)
def pan_swing(rng, v):
    """A pan swing: a flat, fluttering whoosh."""
    x = _whoosh(rng, 0.38, 400, 2000, 0.4)
    x *= 1 + 0.3 * np.sin(dsp.TAU * 35 * _t(len(x)))
    return finish(x, -4, 0.05)


@sfx("melee.frying_pan.hit", "melee", variants=3, pitchVar=1.0, volumeDb=-3, **HIT)
def pan_hit(rng, v):
    """The pan on a body: a dull iron bonk."""
    x = metal_hit(rng, 0.5, [340 + 25 * v, 760, 1350], decays=[0.08, 0.05, 0.03], hardness=1.0)
    return finish(x * 0.7 + _thud(rng, 0.5, 130, 0.05), -3, 0.06)


@sfx("melee.frying_pan.sweet", "melee", volumeDb=-3, pitchVar=0.5, volVarDb=1.0, **HIT)
def pan_sweet(rng, v):
    return pan_hit(rng, 1)


@sfx("melee.frying_pan.clang", "melee", volumeDb=0, pitchVar=0.7, volVarDb=1.5, cooldownMs=150, **HIT)
def pan_clang(rng, v):
    """The pan on a helmet: the one sound everybody waits for, a long bright CLANG that rings on."""
    x = metal_hit(rng, 1.6, [523, 1247, 2093, 3011, 4410], decays=[0.6, 0.4, 0.25, 0.15, 0.08],
                  gains=[1.0, 0.7, 0.5, 0.35, 0.2], hardness=2.2)
    return finish(dsp.saturate(x, 1.3), -1, 0.2)
