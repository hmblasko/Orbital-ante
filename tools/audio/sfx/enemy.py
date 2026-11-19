"""Faction A's tells (Milestone 13): the Scout's flare and the Heavy's spin-up. Each tell must be heard over
gunfire and must be learnable: one clear sound, always the same shape, never a surprise."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import finish, sfx

TELL = dict(bus="CombatImpacts", spatial=1.0, minDist=4, maxDist=180, priority=60, vacuum="air", maxVoices=3)


@sfx("enemy.flare.raise", "enemy", variants=1, pitchVar=0.3, volumeDb=-6, cooldownMs=200, **TELL)
def flare_raise(rng, v):
    """The Scout strikes its flare: a scratch and a rising fizz that lasts as long as the raise (1 s), so the
    player hears how long they have to stop it."""
    n = secs(1.05)
    t = np.arange(n) / SR
    strike = dsp.bandpass(dsp.white(n, rng), 1800, 7000) * np.exp(-t / 0.03) * 0.9
    fizz = dsp.bandpass(dsp.white(n, rng), 2500, 9000) * np.minimum(t / 0.9, 1.0) * 0.45
    fizz *= 0.75 + 0.25 * np.sin(dsp.TAU * 31 * t)
    tone = dsp.sine(900 + 700 * t, n) * np.minimum(t / 0.9, 1.0) * 0.12
    x = strike + fizz + tone
    x *= np.minimum((1.05 - t) / 0.05, 1.0)
    return finish(x, -4, 0.02)


@sfx("enemy.flare.fire", "enemy", variants=1, pitchVar=0.3, volumeDb=-3, cooldownMs=400, **TELL)
def flare_fire(rng, v):
    """The flare goes up: a hollow thump and a hiss climbing away, then a soft high pop where it bursts."""
    n = secs(1.6)
    t = np.arange(n) / SR
    thump = dsp.sine(120 * (1 + 1.5 * np.exp(-t / 0.01)), n) * np.exp(-t / 0.06) * 0.9
    hiss = dsp.bandpass(dsp.white(n, rng), 1500, 6000) * np.exp(-t / 0.5) * np.minimum(t / 0.01, 1) * 0.5
    x = thump + hiss
    pn = secs(0.3)
    pt = np.arange(pn) / SR
    pop = dsp.bandpass(dsp.white(pn, rng), 700, 4000) * np.exp(-pt / 0.05) * 0.5
    dsp.place(x, pop, secs(1.2), 1.0)
    return finish(x, -3, 0.1)


@sfx("enemy.heavy.spinup", "enemy", variants=1, pitchVar=0.2, volumeDb=-4, cooldownMs=300, **TELL)
def heavy_spinup(rng, v):
    """The Heavy's gun spinning up: a motor whine and a rattle of barrels, both rising over the 1.2 s spin, so
    the end of the climb is the moment it fires."""
    length = 1.25
    n = secs(length)
    t = np.arange(n) / SR
    ramp = np.clip(t / 1.2, 0, 1)
    f = 140 + 520 * ramp ** 1.4
    whine = (dsp.sine(f, n) * 0.5 + dsp.sine(f * 2.01, n) * 0.25 + dsp.sine(f * 3.03, n) * 0.1)
    rate = 8 + 52 * ramp
    phase = np.cumsum(rate) / SR
    clicks = (np.mod(phase, 1.0) < 0.08).astype(float)
    rattle = dsp.bandpass(dsp.white(n, rng), 600, 3500) * clicks * 0.6
    x = (whine + rattle) * np.minimum(t / 0.05, 1) * (0.4 + 0.6 * ramp)
    x *= np.minimum((length - t) / 0.04, 1.0)
    return finish(dsp.saturate(x, 1.4), -4, 0.02)
