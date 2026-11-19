"""Synthesiser patches and drum voices for the Orbital Ante soundtrack.

Each `v_*` function plays one note: (freq in Hz, dur in seconds, vel 0..1, rng) -> audio including its release.
`play` renders a list of notes with a patch into a buffer.
"""
from __future__ import annotations

import numpy as np

from . import dsp
from .dsp import SR, secs
from .seq import Note, hz


def note_rng(seed: int, n: Note, period: float | None) -> np.random.Generator:
    """A random source tied to the note itself (its place in the loop and its pitch). The same note then sounds
    exactly the same on every pass of a loop, which is what lets the loop's end run into its start unbroken."""
    pos = n.start % period if period else n.start
    return np.random.default_rng([seed & 0x7FFFFFFF, int(round(pos * 960)), int(n.pitch), int(round(n.dur * 960))])


def play(notes: list[Note], bpm: float, n_total: int, voice, rng: np.random.Generator, gain: float = 1.0,
         stereo: bool = True, period: float | None = None, seed: int | None = None, **kw) -> np.ndarray:
    """Renders notes (positions in beats) with a patch into a buffer of n_total samples.
    With `seed` set, each note gets its own repeatable randomness (see note_rng) instead of sharing `rng`."""
    buf = np.zeros((n_total, 2)) if stereo else np.zeros(n_total)
    beat = 60.0 / bpm
    for n in notes:
        r = rng if seed is None else note_rng(seed, n, period)
        sig = voice(hz(n.pitch), n.dur * beat, n.vel / 127.0, r, **kw)
        dsp.place(buf, sig, secs(n.start * beat), gain)
    return buf


# ----------------------------------------------------------------------------- pads and textures

def v_pad(freq, dur, vel, rng, bright: float = 1.0, attack: float = 0.9, release: float = 1.8, detune: float = 0.16):
    """Warm analogue-style pad: seven detuned saws, a soft sub, a slowly opening low-pass."""
    n = secs(dur + release)
    drift = 1.0 + 0.0015 * dsp.smooth_noise(n, 0.7, rng)
    x = dsp.supersaw(freq * drift, n, 7, detune, rng)
    x += dsp.stereo(dsp.sine(freq * 0.5, n)) * 0.25
    env = dsp.adsr(n, attack, 1.2, 0.8, release, hold=dur)
    cutoff = (500 + 2400 * bright * (0.35 + 0.65 * env)) * (0.8 + 0.4 * vel) * (1 + 0.12 * dsp.lfo(n, 0.11, rng.uniform()))
    x = dsp.svf(x, cutoff, 0.8, "lp", 4)
    return (x.T * env).T * (0.25 + 0.3 * vel)


def v_glass(freq, dur, vel, rng, attack: float = 0.6, release: float = 2.5, shimmer: float = 1.0):
    """Glassy additive pad: each partial breathes at its own slow rate, so the tone never sits still."""
    n = secs(dur + release)
    partials = [(1, 1.0), (2, 0.5), (3, 0.28), (4.02, 0.2), (5.01, 0.12), (6.03, 0.1), (8.05, 0.06)]
    out = np.zeros((n, 2))
    for i, (ratio, amp) in enumerate(partials):
        if freq * ratio > 15000:
            continue
        trem = 0.6 + 0.4 * dsp.lfo(n, rng.uniform(0.07, 0.35) * shimmer, rng.uniform())
        s = dsp.sine(freq * ratio * (1 + rng.uniform(-0.0008, 0.0008)), n, rng.uniform()) * amp * trem
        out += dsp.pan(s, rng.uniform(-0.7, 0.7))
    env = dsp.adsr(n, attack, 1.0, 0.85, release, hold=dur)
    return (out.T * env).T * (0.18 + 0.2 * vel)


def v_string_trem(freq, dur, vel, rng, rate: float = 11.0, release: float = 0.5):
    """Nervous tremolo strings: saws through a band-limited filter with fast bow tremolo."""
    n = secs(dur + release)
    x = dsp.supersaw(freq, n, 5, 0.1, rng)
    trem = 0.55 + 0.45 * dsp.lfo(n, rate * rng.uniform(0.95, 1.05), rng.uniform(), "tri")
    x = dsp.svf(x, 1800 + 1400 * vel, 1.2, "lp", 2)
    x = dsp.highpass(x, 250)
    env = dsp.adsr(n, 0.25, 0.5, 0.9, release, hold=dur)
    return (x.T * (env * trem)).T * 0.22 * (0.4 + 0.6 * vel)


# ----------------------------------------------------------------------------- bells, keys and plucks

def v_bell(freq, dur, vel, rng, ratio: float = 3.5, decay: float = 2.2, bright: float = 1.0):
    """FM bell: a struck, slightly inharmonic tone with a long glowing tail."""
    n = secs(max(dur, 0.2) + decay * 2.2)
    t = np.arange(n) / SR
    idx = (1.2 + 3.5 * vel) * bright * np.exp(-t / (decay * 0.28))
    a = dsp.fm(freq, n, ratio, idx)
    b = dsp.fm(freq * 2.0, n, ratio * 1.004, idx * 0.5) * 0.3 * np.exp(-t / (decay * 0.4))
    strike = dsp.sine(freq * 6.27, n) * np.exp(-t / 0.025) * 0.12 * vel
    env = np.exp(-t / decay)
    env[: secs(0.002)] *= np.linspace(0, 1, secs(0.002))
    x = (a + b + strike) * env
    return dsp.pan(x, rng.uniform(-0.35, 0.35)) * (0.2 + 0.4 * vel)


def v_tine(freq, dur, vel, rng, release: float = 0.35):
    """Electric-piano tine (FM), used to brighten the sampled Rhodes."""
    n = secs(dur + release)
    t = np.arange(n) / SR
    idx = (0.6 + 2.2 * vel * vel) * np.exp(-t / 0.5)
    x = dsp.fm(freq, n, 1.0, idx) + dsp.fm(freq, n, 14.0, 0.6 * vel * np.exp(-t / 0.05)) * 0.18
    env = dsp.adsr(n, 0.003, 1.6, 0.25, release, hold=dur)
    return dsp.pan(x * env, rng.uniform(-0.2, 0.2)) * (0.15 + 0.35 * vel)


def v_pluck(freq, dur, vel, rng, bright: float = 1.0, decay: float = 0.35, res: float = 1.6):
    """Short synth pluck: saw and square through a snapping filter."""
    n = secs(max(dur, 0.05) + decay * 2.5)
    t = np.arange(n) / SR
    x = dsp.saw(freq, n, rng.uniform()) * 0.6 + dsp.square(freq * 1.003, n, 0.42, rng.uniform()) * 0.4
    cutoff = 300 + (5200 * bright * (0.4 + 0.6 * vel)) * np.exp(-t / (decay * 0.55))
    x = dsp.svf(x, cutoff, res, "lp", 2)
    env = np.exp(-t / decay)
    env[: secs(0.0015)] *= np.linspace(0, 1, secs(0.0015))
    return dsp.pan(x * env, rng.uniform(-0.4, 0.4)) * (0.2 + 0.4 * vel)


def v_string_pluck(freq, dur, vel, rng, bright: float = 0.6):
    """Physical plucked string (Karplus-Strong): harp or koto-like, with real string decay."""
    n = secs(max(dur, 0.3) + 2.0)
    x = dsp.karplus(freq, n, rng, bright * (0.5 + 0.5 * vel), 0.9975)
    x = dsp.highpass(x, 60)
    return dsp.pan(dsp.fade(x, 0.0, 0.4), rng.uniform(-0.5, 0.5)) * (0.25 + 0.4 * vel)


def v_stab(freq, dur, vel, rng, bright: float = 1.0, release: float = 0.12):
    """Chord stab: a tight stack of saws with a quick filter bark, the synth layer under the brass hits."""
    n = secs(dur + release)
    x = dsp.supersaw(freq, n, 5, 0.12, rng)
    x += dsp.stereo(dsp.square(freq * 0.5, n, 0.5)) * 0.2
    env = dsp.adsr(n, 0.004, 0.14, 0.55, release, hold=dur)
    t = np.arange(n) / SR
    cutoff = 700 + 5200 * bright * (0.3 + 0.7 * vel) * np.exp(-t / 0.16) + 900 * bright
    x = dsp.svf(x, cutoff, 1.3, "lp", 2)
    return (x.T * env).T * (0.2 + 0.35 * vel)


# ----------------------------------------------------------------------------- basses

def v_sub(freq, dur, vel, rng, release: float = 0.08, drive: float = 1.4):
    n = secs(dur + release)
    x = dsp.sine(freq, n) + 0.12 * dsp.sine(freq * 2, n)
    env = dsp.adsr(n, 0.006, 0.2, 0.9, release, hold=dur)
    return dsp.stereo(dsp.saturate(x * env, drive)) * (0.3 + 0.4 * vel)


def v_reese(freq, dur, vel, rng, release: float = 0.06, move: float = 0.35, top: float = 1.0):
    """Reese bass: two detuned saws beating against each other through a growling low-pass."""
    n = secs(dur + release)
    x = dsp.saw(freq * 2 ** (0.14 / 12), n, rng.uniform()) + dsp.saw(freq * 2 ** (-0.14 / 12), n, rng.uniform())
    x += dsp.saw(freq * 2.0, n, rng.uniform()) * 0.25
    cutoff = (260 + 900 * vel * top) * (1 + move * dsp.lfo(n, 2.9, rng.uniform()))
    x = dsp.svf(x, cutoff, 1.8, "lp", 4)
    x = dsp.saturate(x, 2.2, 0.8, 0.1)
    sub = dsp.sine(freq, n) * 0.8
    env = dsp.adsr(n, 0.004, 0.12, 0.85, release, hold=dur)
    return dsp.stereo((x * 0.5 + sub) * env) * (0.25 + 0.3 * vel)


def v_acid(freq, dur, vel, rng, release: float = 0.05, res: float = 3.5, env_amt: float = 1.0):
    """Squelchy synth bass: a saw through a resonant filter that snaps shut."""
    n = secs(dur + release)
    t = np.arange(n) / SR
    x = dsp.saw(freq, n, rng.uniform()) * 0.7 + dsp.square(freq * 0.5, n, 0.5) * 0.3
    cutoff = 180 + 2600 * env_amt * (0.25 + 0.75 * vel) * np.exp(-t / 0.11)
    x = dsp.svf(x, cutoff, res, "lp", 2)
    x = dsp.saturate(x, 1.8)
    env = dsp.adsr(n, 0.003, 0.1, 0.8, release, hold=dur)
    return dsp.stereo(x * env) * (0.25 + 0.3 * vel)


def v_pulse(freq, dur, vel, rng, release: float = 0.05, pw_rate: float = 0.23):
    """Pulse bass with slow width movement, for ticking tension ostinatos."""
    n = secs(dur + release)
    pw = 0.32 + 0.14 * dsp.lfo(n, pw_rate, rng.uniform())
    x = dsp.square(freq, n, pw, rng.uniform())
    t = np.arange(n) / SR
    cutoff = 300 + 1500 * (0.3 + 0.7 * vel) * np.exp(-t / 0.2)
    x = dsp.svf(x, cutoff, 1.4, "lp", 2)
    env = dsp.adsr(n, 0.003, 0.12, 0.6, release, hold=dur)
    return dsp.stereo(x * env) * (0.18 + 0.25 * vel)


# ----------------------------------------------------------------------------- leads

def v_lead(freq, dur, vel, rng, release: float = 0.18, bright: float = 1.0, vibrato: float = 0.22):
    """Singing synth lead: two saws and a square with a delayed vibrato."""
    n = secs(dur + release)
    t = np.arange(n) / SR
    vib = 1 + (2 ** (vibrato / 12) - 1) * np.sin(dsp.TAU * 5.6 * t) * np.clip((t - 0.18) / 0.3, 0, 1)
    f = freq * vib
    x = dsp.saw(f * 2 ** (0.07 / 12), n, rng.uniform()) + dsp.saw(f * 2 ** (-0.07 / 12), n, rng.uniform())
    x += dsp.square(f, n, 0.35, rng.uniform()) * 0.5
    env = dsp.adsr(n, 0.012, 0.25, 0.75, release, hold=dur)
    cutoff = 900 + 3800 * bright * (0.4 + 0.6 * vel) * (0.5 + 0.5 * env)
    x = dsp.svf(x, cutoff, 1.5, "lp", 4)
    x = dsp.saturate(x, 1.5)
    return dsp.stereo(x * env) * (0.16 + 0.26 * vel)


def v_lead_hard(freq, dur, vel, rng, release: float = 0.1):
    """Aggressive combat lead: hard-synced style brightness from a square against a saw, driven hard."""
    n = secs(dur + release)
    t = np.arange(n) / SR
    bend = 1 + 0.03 * np.exp(-t / 0.03)
    x = dsp.saw(freq * bend, n, rng.uniform()) + dsp.square(freq * 2.003 * bend, n, 0.3, rng.uniform()) * 0.6
    x += dsp.saw(freq * 0.5, n) * 0.4
    env = dsp.adsr(n, 0.004, 0.18, 0.7, release, hold=dur)
    cutoff = 1200 + 5000 * (0.4 + 0.6 * vel) * (0.4 + 0.6 * np.exp(-t / 0.25))
    x = dsp.svf(x, cutoff, 2.2, "lp", 2)
    x = dsp.saturate(x, 3.0, 0.85, 0.15)
    return dsp.stereo(x * env) * (0.15 + 0.22 * vel)


def v_whistle(freq, dur, vel, rng, release: float = 0.25):
    """Breathy flute-like tone: sine with a little air noise and gentle vibrato."""
    n = secs(dur + release)
    t = np.arange(n) / SR
    vib = 1 + 0.006 * np.sin(dsp.TAU * 4.8 * t) * np.clip((t - 0.25) / 0.4, 0, 1)
    x = dsp.sine(freq * vib, n) + 0.18 * dsp.sine(freq * 2 * vib, n) + 0.06 * dsp.sine(freq * 3 * vib, n)
    air = dsp.bandpass(dsp.white(n, rng), freq * 1.5, min(freq * 6, 12000)) * 0.12
    env = dsp.adsr(n, 0.06, 0.3, 0.85, release, hold=dur)
    return dsp.pan((x + air) * env, rng.uniform(-0.15, 0.15)) * (0.15 + 0.25 * vel)


# ----------------------------------------------------------------------------- drums

def kick(vel: float = 1.0, tune: float = 48.0, punch: float = 190.0, decay: float = 0.28, click: float = 0.5,
         drive: float = 1.6, rng: np.random.Generator | None = None) -> np.ndarray:
    rng = rng or np.random.default_rng(3)
    n = secs(decay * 3.2)
    t = np.arange(n) / SR
    f = tune + punch * np.exp(-t / 0.018) + 40 * np.exp(-t / 0.06)
    body = dsp.sine(f, n) * np.exp(-t / decay)
    cl = dsp.highpass(dsp.white(n, rng), 2500) * np.exp(-t / 0.004) * click
    x = dsp.saturate(body + cl, drive)
    x[: secs(0.0007)] *= np.linspace(0, 1, secs(0.0007))
    return dsp.fade(x, 0, 0.02) * vel


def snare(vel: float = 1.0, tune: float = 185.0, snap: float = 1.0, decay: float = 0.16,
          rng: np.random.Generator | None = None) -> np.ndarray:
    rng = rng or np.random.default_rng(4)
    n = secs(decay * 3.5)
    t = np.arange(n) / SR
    f1 = tune * (1 + 0.35 * np.exp(-t / 0.012))
    tone = (dsp.sine(f1, n) + 0.6 * dsp.sine(f1 * 1.79, n)) * np.exp(-t / (decay * 0.55))
    wires = dsp.bandpass(dsp.white(n, rng), 1400, 9500) * np.exp(-t / decay) * snap
    crack = dsp.highpass(dsp.white(n, rng), 5000) * np.exp(-t / 0.006) * 0.8
    x = dsp.saturate(tone * 0.7 + wires * 0.9 + crack, 1.5)
    return dsp.fade(x, 0, 0.02) * vel


def clap(vel: float = 1.0, rng: np.random.Generator | None = None, decay: float = 0.12) -> np.ndarray:
    rng = rng or np.random.default_rng(5)
    n = secs(0.45)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for k, off in enumerate([0.0, 0.011, 0.021, 0.034]):
        m = n - secs(off)
        burst = dsp.white(m, rng) * np.exp(-np.arange(m) / SR / (0.007 if k < 3 else decay))
        x[secs(off):] += burst * (0.7 if k < 3 else 1.0)
    x = dsp.bandpass(x, 900, 4200) + dsp.highpass(x, 6000) * 0.25
    return dsp.fade(x, 0, 0.03) * vel * 0.9


def hat(vel: float = 1.0, decay: float = 0.035, tone: float = 1.0, rng: np.random.Generator | None = None) -> np.ndarray:
    """Metallic hat from six clashing square waves plus noise (the classic drum-machine recipe)."""
    rng = rng or np.random.default_rng(6)
    n = secs(decay * 5 + 0.02)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for f in [205.3, 304.4, 369.6, 522.7, 540.0, 800.0]:
        x += dsp.square(f * 12.1 * tone * rng.uniform(0.995, 1.005), n, 0.5, rng.uniform())
    x = dsp.bandpass(x / 6, 6500, 14000) + dsp.highpass(dsp.white(n, rng), 8000) * 0.5
    x = dsp.highpass(x, 5500, 4)
    return dsp.fade(x * np.exp(-t / decay), 0.0003, 0.01) * vel * 0.7


def shaker(vel: float = 1.0, length: float = 0.07, rng: np.random.Generator | None = None) -> np.ndarray:
    rng = rng or np.random.default_rng(8)
    n = secs(length * 2)
    t = np.arange(n) / SR
    env = np.minimum(t / (length * 0.35), 1.0) * np.exp(-np.maximum(t - length * 0.35, 0) / (length * 0.4))
    x = dsp.bandpass(dsp.white(n, rng), 4500, 11000) * env
    return x * vel * 0.5


def rim(vel: float = 1.0, rng: np.random.Generator | None = None) -> np.ndarray:
    rng = rng or np.random.default_rng(9)
    n = secs(0.12)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for f, d, g in [(1720, 0.012, 1.0), (2380, 0.008, 0.6), (480, 0.02, 0.5), (3900, 0.004, 0.4)]:
        x += g * np.exp(-t / d) * np.sin(dsp.TAU * f * rng.uniform(0.99, 1.01) * t + rng.uniform(0, 6.28))
    x += dsp.highpass(dsp.white(n, rng), 3000) * np.exp(-t / 0.002) * 0.4
    return dsp.fade(x, 0.0003, 0.02) * vel * 0.5


def tom(vel: float = 1.0, tune: float = 110.0, decay: float = 0.22, rng: np.random.Generator | None = None) -> np.ndarray:
    rng = rng or np.random.default_rng(10)
    n = secs(decay * 3.5)
    t = np.arange(n) / SR
    f = tune * (1 + 0.5 * np.exp(-t / 0.03))
    x = dsp.sine(f, n) * np.exp(-t / decay) + dsp.bandpass(dsp.white(n, rng), 300, 3000) * np.exp(-t / 0.02) * 0.3
    return dsp.fade(dsp.saturate(x, 1.4), 0.0005, 0.02) * vel


def cymbal(vel: float = 1.0, decay: float = 1.2, tone: float = 1.0, rng: np.random.Generator | None = None) -> np.ndarray:
    rng = rng or np.random.default_rng(11)
    n = secs(decay * 3.5)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for f in rng.uniform(2800, 9000, 14) * tone:
        x += dsp.square(f, n, 0.5, rng.uniform()) * rng.uniform(0.4, 1.0)
    x = x / 8 + dsp.white(n, rng) * 0.6
    x = dsp.highpass(x, 3500, 2)
    env = 0.6 * np.exp(-t / (decay * 0.25)) + 0.4 * np.exp(-t / decay)
    return dsp.fade(x * env, 0.001, 0.05) * vel * 0.4


# ----------------------------------------------------------------------------- transitions

def riser(dur: float, rng: np.random.Generator, lo: float = 300.0, hi: float = 9000.0, tone: float | None = None) -> np.ndarray:
    """Noise sweep that climbs and swells into a downbeat; optional pitched whine on top."""
    n = secs(dur)
    k = np.linspace(0, 1, n)
    x = dsp.stereo(dsp.white(n, rng))
    x[:, 1] = dsp.white(n, rng)
    cutoff = lo * (hi / lo) ** (k ** 1.4)
    x = dsp.svf(x, cutoff, 2.5, "bp", 2)
    if tone:
        whine = dsp.saw(tone * 2 ** (k * 1.0), n) * 0.25
        x += dsp.stereo(dsp.svf(whine, cutoff, 1.2, "lp"))
    env = k ** 2.2
    return (x.T * env).T * 0.5


def reverse_cymbal(dur: float, rng: np.random.Generator) -> np.ndarray:
    c = cymbal(1.0, dur * 0.5, 1.0, rng)
    c = dsp.pad_to(c, secs(dur))[::-1].copy()
    return dsp.stereo(dsp.fade(c, 0.02, 0.004))


def impact(rng: np.random.Generator, sub: float = 42.0, length: float = 2.4, bright: float = 1.0) -> np.ndarray:
    """Downbeat impact: sub drop, a low thud and a burst of dark noise."""
    n = secs(length)
    t = np.arange(n) / SR
    drop = dsp.sine(sub * (1 + 1.6 * np.exp(-t / 0.05)), n) * np.exp(-t / (length * 0.22))
    thud = dsp.lowpass(dsp.white(n, rng), 220) * np.exp(-t / 0.09) * 1.6
    hiss = dsp.stereo(dsp.white(n, rng))
    hiss[:, 1] = dsp.white(n, rng)
    hiss = dsp.svf(hiss, 600 + 5000 * bright * np.exp(-t / 0.25), 0.8, "lp")
    hiss = (hiss.T * np.exp(-t / (length * 0.2))).T * 0.35
    x = dsp.stereo(dsp.saturate(drop + thud, 1.8)) * 0.8 + hiss
    return dsp.fade(x, 0, 0.3)
