"""Meteor strikes (Matthew, 16 Nov 2026): the roar of a rock coming down, the crack when it bursts apart in the
air, the boom when it lands, the hiss when it lands in water and the thud of thrown rocks coming back down.
Grounded, not cartoonish: a real bolide is heard as a tearing roar and a crack like a gun, then thunder.
Every important sound has mid content so the Deck's small speakers carry it (they roll off under ~200 Hz)."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import finish, finish_loop, sfx

METEOR = dict(bus="Environment", spatial=1.0, vacuum="air")


def _grains(n: int, rng, rate: float, lo: float, hi: float, length: float, decay: float) -> np.ndarray:
    """A shower of small hard clicks (stones, crackle), thinning out over the sound: rate per second at the start."""
    x = np.zeros(n)
    t = 0.0
    total = n / SR
    while t < total:
        # Fewer as it goes on.
        density = rate * np.exp(-t / decay)
        if density < 0.2:
            break
        t += rng.exponential(1.0 / density)
        if t >= total:
            break
        g = secs(rng.uniform(0.004, 0.02))
        grain = dsp.white(g, rng) * np.exp(-np.arange(g) / (g * 0.25))
        grain = dsp.bandpass(grain, lo * rng.uniform(0.8, 1.3), hi * rng.uniform(0.7, 1.1))
        dsp.place(x, grain * rng.uniform(0.2, 1.0) * np.exp(-t / length), secs(t), 1.0)
    return x


@sfx("meteor.roar", "meteor", loop=True, volumeDb=-3, minDist=25, maxDist=1100, priority=30, maxVoices=1, **METEOR,
     note="A rock tearing down through the sky: deep roar, a ragged sizzle on top and crackle. The game follows the biggest falling piece.")
def roar(rng, v):
    n = secs(7.0)
    t = np.arange(n) / SR
    low = dsp.svf(dsp.brown(n, rng), 160 * (1 + 0.3 * dsp.smooth_noise(n, 4.0, rng)), 0.8, "lp", 2)
    low = dsp.highpass(low, 35)
    tear = dsp.svf(dsp.pink(n, rng), 900 * (1 + 0.4 * dsp.smooth_noise(n, 9.0, rng)), 1.4, "bp", 2)
    tear *= 0.7 + 0.3 * (1 + dsp.smooth_noise(n, 17.0, rng)) * 0.5
    sizzle = dsp.bandpass(dsp.white(n, rng), 2500, 7000) * (0.5 + 0.5 * dsp.smooth_noise(n, 30.0, rng)) * 0.25
    crackle = _grains(n, rng, 40, 1200, 5000, 100, 100) * 0.5
    # A slow beat in the roar: the rock tumbling.
    tumble = 1 + 0.15 * np.sin(dsp.TAU * 1.7 * t)
    x = (dsp.normalize_peak(low, 0) * 0.8 + dsp.normalize_peak(tear, 0) * 0.55 + sizzle + crackle) * tumble
    x = dsp.saturate(x, 1.5)
    return finish_loop(x, -3, 1.2, hp=30)


@sfx("meteor.breakup", "meteor", variants=2, volumeDb=-1, pitchVar=0.6, minDist=30, maxDist=1400, priority=25, cooldownMs=80,
     maxVoices=2, **METEOR)
def breakup(rng, v):
    """The rock bursting apart: a sharp crack like a rifle shot, a split second of breaking stone, then thunder
    that rolls away."""
    n = secs(3.4)
    t = np.arange(n) / SR
    crack_n = secs(0.25)
    ct = np.arange(crack_n) / SR
    crack = dsp.bandpass(dsp.white(crack_n, rng), 700, 6500) * np.exp(-ct / 0.018) * 1.0
    crack += dsp.sine(150 * (1 + 2.5 * np.exp(-ct / 0.006)), crack_n) * np.exp(-ct / 0.05) * 0.7
    x = np.zeros(n)
    dsp.place(x, crack, 0, 1.0)
    # Breaking stone just after: a cluster of smaller cracks.
    for k in range(rng.integers(3, 6)):
        at = rng.uniform(0.02, 0.18)
        m = secs(0.08)
        mt = np.arange(m) / SR
        bit = dsp.bandpass(dsp.white(m, rng), 900, 5000) * np.exp(-mt / 0.01) * rng.uniform(0.25, 0.6)
        dsp.place(x, bit, secs(at), 1.0)
    # Thunder: a low roll with a mid growl, swelling then dying.
    roll = dsp.svf(dsp.brown(n, rng), 220 * (1 + 0.4 * dsp.smooth_noise(n, 6, rng)), 0.9, "lp", 2)
    growl = dsp.svf(dsp.pink(n, rng), 500, 1.2, "bp", 2)
    env = np.clip((t - 0.05) / 0.25, 0, 1) * np.exp(-np.maximum(t - 0.3, 0) / 0.9)
    env *= 0.75 + 0.25 * (1 + dsp.smooth_noise(n, 5, rng)) * 0.5
    x += (dsp.normalize_peak(roll, 0) * 0.55 + dsp.normalize_peak(growl, 0) * 0.3) * env
    x = dsp.saturate(x, 1.3)
    return finish(x, -1, 0.3, hp=30, limit=True)


@sfx("meteor.impact", "meteor", variants=3, volumeDb=0, pitchVar=0.8, minDist=30, maxDist=1500, priority=20, cooldownMs=60,
     maxVoices=3, **METEOR)
def impact(rng, v):
    """A rock hitting the ground: a deep punch felt in the chest, the crunch of ground blown apart and stones
    raining down, then a long low rumble."""
    n = secs(4.2)
    t = np.arange(n) / SR
    x = np.zeros(n)
    # The punch: a falling low tone and a burst of noise.
    punch = dsp.sine(32 + 90 * np.exp(-t / 0.07), n) * np.exp(-t / 0.35) * 0.5
    blast = dsp.bandpass(dsp.white(n, rng), 120, 1800) * np.exp(-t / 0.09) * 0.6
    snap = dsp.bandpass(dsp.white(n, rng), 1500, 6000) * np.exp(-t / 0.012) * 0.7
    # The mids that small speakers carry: the crunch of the ground breaking, and a dull thud.
    crunch = dsp.bandpass(dsp.white(n, rng), 350, 2600) * np.exp(-t / 0.4) * (0.7 + 0.3 * dsp.smooth_noise(n, 40, rng))
    body = dsp.sine(240 * (1 + 0.6 * np.exp(-t / 0.03)), n) * np.exp(-t / 0.12) * 0.5
    x += punch + blast + snap + body + dsp.normalize_peak(crunch, 0) * 1.5
    # Ground blown apart, then gravel and stones raining back down.
    x += dsp.normalize_peak(_grains(n, rng, 260, 400, 3500, 0.5, 0.35), 0) * 1.1
    rain = dsp.normalize_peak(_grains(n, rng, 70, 600, 4500, 1.2, 1.0), 0)
    x += np.concatenate([np.zeros(secs(0.6)), rain[: n - secs(0.6)]]) * 0.4
    # The rumble rolling away.
    rumble = dsp.svf(dsp.brown(n, rng), 140, 0.8, "lp", 2)
    env = np.clip(t / 0.08, 0, 1) * np.exp(-t / 1.3)
    x += dsp.normalize_peak(rumble, 0) * env * 0.25
    x = dsp.saturate(x, 1.8)
    return finish(x, -1, 0.4, hp=25, limit=True)


@sfx("meteor.splash", "meteor", variants=1, volumeDb=-2, pitchVar=0.6, minDist=25, maxDist=1200, priority=30, cooldownMs=80,
     maxVoices=2, **METEOR)
def splash(rng, v):
    """A rock landing in water: a heavy slap, a column of water thrown up and falling back, and a steam hiss."""
    n = secs(3.5)
    t = np.arange(n) / SR
    slap = dsp.lowpass(dsp.white(n, rng), 2500) * np.exp(-t / 0.05) + dsp.sine(60 + 60 * np.exp(-t / 0.05), n) * np.exp(-t / 0.3) * 0.8
    fall = dsp.bandpass(dsp.white(n, rng), 500, 4000) * np.clip((t - 0.5) / 0.4, 0, 1) * np.exp(-np.maximum(t - 0.9, 0) / 0.8) * 0.4
    fall *= 0.6 + 0.4 * (1 + dsp.smooth_noise(n, 30, rng)) * 0.5
    hiss = dsp.highpass(dsp.white(n, rng), 3500) * np.clip(t / 0.2, 0, 1) * np.exp(-t / 1.4) * 0.3
    return finish(slap + fall + hiss, -2, 0.4, hp=30)


@sfx("meteor.debris", "meteor", variants=4, volumeDb=-9, pitchVar=1.5, volVarDb=2, minDist=4, maxDist=160, priority=120,
     cooldownMs=50, maxVoices=4, **METEOR)
def debris(rng, v):
    """A thrown rock coming back down: a dull knock on the ground and a skitter of grit."""
    n = secs(0.5)
    t = np.arange(n) / SR
    f = rng.uniform(320, 560)
    knock = dsp.sine(f * (1 + 0.8 * np.exp(-t / 0.01)), n) * np.exp(-t / 0.04) * 0.8
    knock += dsp.sine(f * 0.45, n) * np.exp(-t / 0.06) * 0.4
    knock += dsp.normalize_peak(dsp.bandpass(dsp.white(n, rng), 700, 3500) * np.exp(-t / 0.03), 0) * 1.2
    grit = dsp.normalize_peak(_grains(n, rng, 120, 1500, 6000, 0.15, 0.12), 0) * 0.4
    return finish(knock + grit, -3, 0.05, hp=50)
