"""The alien creatures' own sounds. The Screamer (the cicada) first: its sound is its weapon, so the warning and the
scream must be learnable (always the same shape) and heard over gunfire, without being a sound nobody can bear."""
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
