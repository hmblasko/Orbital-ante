"""The alien creatures' own sounds. The Screamer (the cicada) first: its sound is its weapon, so the warning and the
scream must be learnable (always the same shape) and heard over gunfire, without being a sound nobody can bear.
Then the Ambusher (the bobbit worm): a wet hiss as it rears, the scrape of its jaws flying wide where it takes aim,
the snap of the strike, and the ground breaking where it comes up."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp
from oa_audio.dsp import SR, secs

from .common import finish, finish_loop, sfx

CREATURE = dict(bus="CombatImpacts", spatial=1.0, minDist=5, maxDist=220, priority=50, vacuum="air", maxVoices=2)

# A cicada sings by buckling a ribbed drum: every rib is a click, and its hollow abdomen rings to them. A real one
# rings near 4 kHz; an animal the size of a person rings much lower.
RING = (1150.0, 1720.0, 2380.0, 3300.0)


def _drum(clicks: np.ndarray, rng, air: float = 0.0) -> np.ndarray:
    """A train of clicks through the abdomen's resonances, with a little breath of noise."""
    n = len(clicks)
    x = dsp.resonators(clicks, RING, (0.006, 0.005, 0.004, 0.003), (1.0, 0.8, 0.5, 0.3))
    body = dsp.bandpass(clicks, 180, 600) * 0.5
    hiss = dsp.bandpass(dsp.white(n, rng), 2500, 7000) * air
    return x + body + hiss


def _clicks(rate: np.ndarray, rng, jitter: float = 0.06) -> np.ndarray:
    """One sample at each click, the clicks coming at `rate` a second, not quite evenly."""
    phase = np.cumsum(rate * (1.0 + jitter * dsp.smooth_noise(len(rate), 40.0, rng))) / SR
    edge = np.diff(np.floor(phase), prepend=0.0) > 0
    return edge.astype(float) * rng.uniform(0.7, 1.0, len(rate))


@sfx("creature.screamer.windup", "creature", variants=1, pitchVar=0.2, volumeDb=-5, cooldownMs=300, **CREATURE)
def screamer_windup(rng, v):
    """The Screamer's warning: single dry clicks of its drums that quicken into a rattle over one second, rising,
    with a hard double click at 0.6 s where it takes aim. The end of the climb is the moment the scream starts."""
    length = 1.0
    n = secs(length)
    t = np.arange(n) / SR
    ramp = np.clip(t / length, 0, 1)
    rate = 9.0 + 150.0 * ramp ** 2.2
    x = _drum(_clicks(rate, rng), rng) * (0.35 + 0.65 * ramp)
    lock = np.zeros(n)
    for at in (0.60, 0.635):
        lock[secs(at)] = 1.6
    x += _drum(lock, rng) * 1.2
    x *= np.minimum((length - t) / 0.01, 1.0)
    return finish(x, -4, 0.01, hp=120)


@sfx("creature.screamer.scream", "creature", variants=1, volumeDb=-3, loop=True, **CREATURE)
def screamer_scream(rng, v):
    """The Screamer's scream, a loop for as long as it lasts: both drums buckling about 190 times a second, not
    quite together, ringing its hollow abdomen; it throbs five times a second, which is the beat it hurts on."""
    length = 3.0 + 0.5 + 0.25
    n = secs(length)
    t = np.arange(n) / SR
    one = _clicks(np.full(n, 188.0), rng, 0.03)
    two = _clicks(np.full(n, 193.0), rng, 0.03)
    x = _drum(one + two * 0.9, rng, air=0.05)
    throb = 0.72 + 0.28 * np.sin(dsp.TAU * 5.0 * t) ** 2
    x = dsp.saturate(x * throb, 1.6, 0.6)
    # Not the piercing top of a real cicada: this has to be borne for two and a half seconds at a time.
    x = dsp.lowpass(x, 5200)
    return finish_loop(x, -6, 0.5, hp=140)


AMBUSHER = dict(bus="CombatImpacts", spatial=1.0, minDist=3, maxDist=90, priority=55, vacuum="air", maxVoices=2)


def _blade(n: int, rng, low: float, high: float) -> np.ndarray:
    """Hard chitin drawn over hard chitin: a short ringing scrape."""
    t = np.arange(n) / SR
    scrape = dsp.bandpass(dsp.white(n, rng), low, high) * np.exp(-t / 0.035)
    ring = dsp.resonators(scrape, (2100.0, 3350.0, 4700.0), (0.030, 0.022, 0.015), (1.0, 0.6, 0.35))
    return scrape * 0.6 + ring * 0.5


@sfx("creature.ambusher.coil", "creature", variants=1, pitchVar=0.15, volumeDb=-5, cooldownMs=300, **AMBUSHER)
def ambusher_coil(rng, v):
    """The Ambusher's warning, 0.9 s: a wet hiss that tightens as it rears, ticks of its jaws half closing, and at
    0.5 s (where it takes aim) the scrape of both jaws flying wide. After the scrape, stepping aside is a miss."""
    length = 0.9
    n = secs(length)
    t = np.arange(n) / SR
    ramp = np.clip(t / length, 0, 1)
    hiss = dsp.svf(dsp.pink(n, rng), 500.0 + 2600.0 * ramp ** 1.5, 2.5, "bp") * (0.25 + 0.75 * ramp) * (1.0 + 0.3 * dsp.smooth_noise(n, 30.0, rng))
    ticks = np.zeros(n)
    for at in (0.06, 0.17, 0.26, 0.33, 0.39, 0.44):
        ticks[secs(at)] = rng.uniform(0.6, 1.0)
    click = dsp.resonators(ticks, (1500.0, 2600.0), (0.008, 0.005), (1.0, 0.6))
    x = hiss * 0.5 + click * 0.9
    lock = _blade(secs(0.22), rng, 1800, 7000)
    dsp.place(x, lock, secs(0.495), 1.5)
    x *= np.minimum((length - t) / 0.015, 1.0)
    return finish(x, -4, 0.015, hp=150)


@sfx("creature.ambusher.strike", "creature", variants=2, pitchVar=0.2, volumeDb=-3, cooldownMs=150, **AMBUSHER)
def ambusher_strike(rng, v):
    """The strike: its length thrown out through the air in under a fifth of a second, and the jaws snapping shut."""
    n = secs(0.42)
    t = np.arange(n) / SR
    rush = dsp.svf(dsp.white(n, rng), 700.0 + 3800.0 * np.clip(t / 0.16, 0, 1), 1.8, "bp") * np.exp(-((t - 0.10) / 0.06) ** 2)
    x = rush * 0.7
    snap = _blade(secs(0.16), rng, 1200, 6000)
    thud = dsp.sine(140.0 * np.exp(-np.arange(secs(0.12)) / SR / 0.05) + 60.0, secs(0.12)) * dsp.env_exp(secs(0.12), 0.035)
    dsp.place(x, snap, secs(0.17), 1.6)
    dsp.place(x, thud, secs(0.17), 0.9)
    return finish(x, -3, 0.03, hp=70)


@sfx("creature.ambusher.rise", "creature", variants=2, pitchVar=0.25, volumeDb=-6, cooldownMs=400, **AMBUSHER)
def ambusher_rise(rng, v):
    """It comes up: the ground heaving, then breaking, and grit falling back. The heave comes first on purpose, so
    there is time to look down."""
    n = secs(1.1)
    t = np.arange(n) / SR
    heave = dsp.lowpass(dsp.brown(n, rng), 160) * np.clip(t / 0.4, 0, 1) ** 2 * np.exp(-np.clip(t - 0.5, 0, None) / 0.2)
    crack = dsp.bandpass(dsp.white(n, rng), 300, 2800) * np.exp(-np.clip(t - 0.42, 0, None) / 0.07) * (t > 0.42)
    grains = (rng.random(n) < (220.0 / SR) * np.clip((t - 0.45) / 0.1, 0, 1) * np.exp(-np.clip(t - 0.55, 0, None) / 0.25)) * rng.uniform(0.3, 1.0, n)
    grit = dsp.resonators(grains, (900.0, 1900.0, 3100.0), (0.004, 0.003, 0.002), (1.0, 0.7, 0.4))
    x = heave * 2.2 + crack * 0.8 + grit * 0.6
    return finish(x, -5, 0.08, hp=40)
