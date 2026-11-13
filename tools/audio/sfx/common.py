"""Shared building blocks for sound effects, and the registry that turns designs into game audio events."""
from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
from scipy import signal

from oa_audio import dsp
from oa_audio.dsp import SR, secs

REGISTRY: list["Design"] = []


@dataclass
class Design:
    event: str  # game event id, for example "weapon.rifle.shot"
    category: str  # folder under Resources/Audio/Sfx
    fn: object
    variants: int = 1
    bus: str = "Environment"
    params: dict = field(default_factory=dict)
    note: str = ""


def sfx(event: str, category: str, variants: int = 1, bus: str = "Environment", note: str = "", **params):
    """Registers a sound design. The function gets (rng, variant index) and returns mono or stereo audio.

    Playback parameters (params) become the event's entry in the game's audio manifest:
      volumeDb, pitchVar (random pitch range in semitones), volVarDb, spatial (0 = 2D, 1 = fully 3D),
      minDist, maxDist, priority (0 most important .. 255), cooldownMs, maxVoices, loop,
      vacuum: "air" (carried by air, so muffled to a thump in vacuum), "self" (the player's own gear, heard
      through the suit), "suit" (inside the helmet, never filtered) or "ui".
    """

    def deco(fn):
        REGISTRY.append(Design(event, category, fn, variants, bus, params, note or (fn.__doc__ or "").strip()))
        return fn

    return deco


# ----------------------------------------------------------------------------- pieces

def click(n: int, rng: np.random.Generator, ms: float = 1.0) -> np.ndarray:
    x = np.zeros(n)
    m = max(2, secs(ms / 1000.0))
    x[:m] = dsp.white(m, rng) * np.hanning(m * 2)[m:]
    return x


def ping(rng: np.random.Generator, length: float, freqs, decays=None, gains=None, noise: float = 0.25,
         noise_hp: float = 2500.0, shimmer: float = 0.004) -> np.ndarray:
    """A struck object by modal synthesis: each resonance is a decaying sine (with a slightly detuned twin so it
    shimmers like real metal instead of ringing like a test tone), under a tiny burst of impact noise."""
    n = secs(length)
    t = np.arange(n) / SR
    freqs = [f for f in freqs if f < SR * 0.45]
    decays = decays if decays is not None else [0.03 * (1200.0 / f) ** 0.5 for f in freqs]
    gains = gains if gains is not None else [1.0 / (1 + i * 0.35) for i in range(len(freqs))]
    x = np.zeros(n)
    for f, d, g in zip(freqs, decays, gains):
        ph = rng.uniform(0, dsp.TAU)
        x += g * np.exp(-t / d) * (np.sin(dsp.TAU * f * t + ph) + 0.6 * np.sin(dsp.TAU * f * (1 + shimmer) * t + ph * 1.7))
    x /= (np.max(np.abs(x)) + 1e-9)
    if noise > 0:
        x += dsp.highpass(dsp.white(n, rng), noise_hp) * np.exp(-t / 0.0025) * noise
    x[: secs(0.0004)] *= np.linspace(0, 1, secs(0.0004))
    return x


def metal_hit(rng: np.random.Generator, length: float, freqs, decays=None, gains=None, hardness: float = 1.0,
              excite_ms: float = 1.2) -> np.ndarray:
    """Struck metal: clashing, inharmonic resonances with a hard noise edge. Peak is about 1."""
    freqs = np.asarray(freqs, dtype=float) * rng.uniform(0.985, 1.015, len(freqs))
    return ping(rng, length, list(freqs), decays, gains, noise=0.3 * hardness)


def norm(x: np.ndarray) -> np.ndarray:
    return x / (np.max(np.abs(x)) + 1e-9)


def friedlander(n: int, t_pos_ms: float, b: float = 1.2) -> np.ndarray:
    """The pressure wave of a blast: a sharp rise, a fall through zero and a shallow negative phase."""
    t = np.arange(n) / SR
    T = t_pos_ms / 1000.0
    return (1 - t / T) * np.exp(-b * t / T)


def outdoor_tail(dry: np.ndarray, rng: np.random.Generator, length: float, echoes: int = 7, first: float = 0.03,
                 spread: float = 0.35, dark: float = 2600.0, diffuse: float = 0.6, slap: float = 0.0,
                 slap_time: float = 0.11) -> np.ndarray:
    """What the landscape does to a loud bang: a few distinct echoes, each duller and later than the last,
    then a rumbling wash that dies away. Returns only the tail (no dry signal)."""
    n = secs(length)
    ir = np.zeros(n)
    for i in range(echoes):
        at = first + spread * (rng.uniform() ** 1.6)
        amp = 0.8 * np.exp(-at / (spread * 0.5)) * rng.choice([-1.0, 1.0]) * rng.uniform(0.5, 1.0)
        s = secs(at)
        if s < n:
            ir[s] += amp
    if slap > 0:
        s = secs(slap_time)
        if s < n:
            ir[s] += slap
            if s * 2 < n:
                ir[s * 2] -= slap * 0.45
    t = np.arange(n) / SR
    wash = rng.standard_normal(n) * np.exp(-t / (length * 0.22)) * np.clip(t / 0.05, 0, 1)
    wash = dsp.lowpass(wash, dark, 2)
    wash /= np.max(np.abs(wash)) + 1e-9
    ir += wash * diffuse * 0.05
    tail = signal.fftconvolve(dry, ir)[:n]
    # Air swallows the highs of distant reflections first: the filter closes as the tail goes on.
    cutoff = dark * 3.0 * np.exp(-t / (length * 0.35)) + dark * 0.5
    return dsp.svf(tail, cutoff, 0.7, "lp")


def gunshot(rng: np.random.Generator, *, length: float = 1.2,
            crack: float = 1.0, crack_tau: float = 0.010, crack_hp: float = 1800.0,
            blast_ms: float = 2.2, blast: float = 0.8,
            body: float = 0.6, body_tau: float = 0.045, body_band=(180.0, 2200.0),
            thump: float = 0.4, thump_f=(150.0, 55.0), thump_tau: float = 0.05,
            bark: float = 0.5, bark_f=(420.0, 780.0, 1250.0), bark_tau: float = 0.03, bark_q: float = 5.0,
            ground_ms: float = 4.0,
            mech=(), drive: float = 2.0,
            tail: float = 0.16, tail_len: float = 0.9, tail_dark: float = 2400.0, tail_echoes: int = 7,
            tail_spread: float = 0.3, slap: float = 0.0, slap_time: float = 0.11,
            presence_db: float = 2.0, low_db: float = 0.0, peak_db: float = -1.0) -> np.ndarray:
    """A layered firearm report.

    Layers (see docs/audio/style_bible.md, "Weapon sound design"). Each is brought to the same peak and then
    mixed at the level given, so the numbers read as a mix: crack 1.0 with body 0.6 means the body sits about
    4 dB under the crack.
      blast    the pressure wave itself, a few milliseconds long
      crack    broadband noise that dies in about 10 ms: the "snap"
      body     50 to 100 ms of mid-range roar, where the weapon's size is heard
      thump    a falling low tone under the body, for weight in the chest
      bark     narrow bands of noise that give each weapon its own voice (barrel and receiver)
      mech     bolt, slide or hammer clicks after the shot
      tail     echoes and rumble from the surroundings, set as a fraction of the shot's peak
    The crack, body and bark are driven into saturation together, the way a real gunshot overloads the air
    and the microphone, so they fuse into one event. The thump is added clean afterwards so the low end
    stays tight instead of turning the whole shot to mud.
    """
    n = secs(length)
    t = np.arange(n) / SR

    p = friedlander(n, blast_ms)
    p[secs(blast_ms * 6 / 1000.0):] = 0
    dp = np.diff(p, prepend=0.0)
    blast_sig = norm(dsp.highpass(p, 90) * 0.6 + norm(dp)) * blast

    w = dsp.white(n, rng)
    crack_sig = norm(dsp.highpass(w, crack_hp) * (np.exp(-t / crack_tau) + 0.25 * np.exp(-t / (crack_tau * 4)))) * crack

    # body: mid roar with a rough, fluttering envelope (turbulent gas, not a smooth synth decay)
    rough = 1.0 + 0.6 * dsp.smooth_noise(n, 160.0, rng)
    body_sig = norm(dsp.bandpass(dsp.white(n, rng), body_band[0], body_band[1]) * np.exp(-t / body_tau) * rough) * body

    bark_sig = np.zeros(n)
    for i, bf in enumerate(bark_f):
        band = dsp.svf(dsp.white(n, rng), bf * rng.uniform(0.96, 1.04), bark_q, "bp")
        bark_sig += band * np.exp(-t / (bark_tau * (1.0 - 0.18 * i))) / (1 + 0.4 * i)
    bark_sig = norm(bark_sig) * bark

    dry = blast_sig + crack_sig + body_sig + bark_sig

    # ground bounce: one very early reflection gives the comb-filter colour of a shot fired near the floor
    if ground_ms > 0:
        d = secs(ground_ms * rng.uniform(0.85, 1.15) / 1000.0)
        bounce = np.concatenate([np.zeros(d), dry])[:n]
        dry = dry + dsp.lowpass(bounce, 5000) * -0.4

    dry = dsp.saturate(norm(dry) * 0.95, drive, 1.0, 0.04)
    # highs die faster than lows, as they do in air
    dry = dsp.svf(dry, 2500 + 17000 * np.exp(-t / 0.06), 0.7, "lp")

    f = thump_f[1] + (thump_f[0] - thump_f[1]) * np.exp(-t / (thump_tau * 0.45))
    thump_sig = np.sin(dsp.TAU * np.cumsum(f) / SR) * np.exp(-t / thump_tau)
    dry = norm(dry) + dsp.saturate(thump_sig, 1.5) * thump

    out = dry.copy()
    if tail > 0:
        tl = outdoor_tail(dry, rng, length, tail_echoes, 0.03, tail_spread, tail_dark, 0.7, slap, slap_time)
        tl *= np.clip(t / 0.02, 0, 1)
        out += norm(tl) * tail * np.max(np.abs(dry))

    for delay_ms, gain, freqs, m_len in mech:
        hit = metal_hit(rng, m_len, freqs, hardness=1.2)
        dsp.place(out, hit, secs(delay_ms * rng.uniform(0.94, 1.06) / 1000.0), gain)

    if low_db:
        out = dsp.shelf(out, 110, low_db, "low")
    out = dsp.peak_eq(out, 3200, presence_db, 0.8)
    out = dsp.highpass(out, 35)
    out = dsp.limit(norm(out) * 1.3, peak_db, 0.0015, 0.05)
    return dsp.fade(out, 0.00035, min(0.25, length * 0.3))


def _tp_safe(x: np.ndarray, peak_db: float) -> np.ndarray:
    """Brings inter-sample peaks under the ceiling too (bright clicks can overshoot between samples)."""
    up = signal.resample_poly(x, 4, 1, axis=0)
    tp = np.max(np.abs(up)) + 1e-12
    ceiling = dsp.db(max(peak_db, -1.0) if peak_db > -1.0 else peak_db + 0.5)
    ceiling = min(ceiling, dsp.db(-1.0))
    if tp > ceiling:
        x = x * (ceiling / tp)
    return x


def finish(x: np.ndarray, peak_db: float = -3.0, fade_out: float = 0.03, hp: float = 30.0, limit: bool = False) -> np.ndarray:
    """Common last step: remove rumble, set the peak level, make sure the end is silent."""
    x = dsp.highpass(x, hp) if hp else x
    if limit:
        x = dsp.limit(x / (np.max(np.abs(x)) + 1e-9) * 1.4, peak_db)
    else:
        x = dsp.normalize_peak(x, peak_db)
    x = _tp_safe(x, peak_db)
    # A third of a millisecond of fade-in: too short to soften any attack, enough that no file starts on a step.
    return dsp.fade(x, 0.00035, fade_out)


def finish_loop(x: np.ndarray, peak_db: float = -8.0, cross: float = 1.5, hp: float = 30.0) -> np.ndarray:
    """Last step for a looping texture. Filtering happens before the loop is closed, because a filter starting
    cold at the top of the file would leave a step at the wrap."""
    if hp:
        x = dsp.highpass(x, hp)
        x = x[secs(0.25):]  # drop the filter's settling time
    x = loopable(x, cross)
    return _tp_safe(dsp.normalize_peak(x, peak_db), peak_db)


def loopable(x: np.ndarray, cross: float = 0.5) -> np.ndarray:
    """Makes a steady texture loop cleanly by cross-fading its end into its start (equal power)."""
    c = secs(cross)
    body = x[c:-c] if x.ndim == 1 else x[c:-c, :]
    head, tail = x[:c], x[-c:]
    k = np.linspace(0, 1, c)
    a, b = np.sin(k * np.pi / 2), np.cos(k * np.pi / 2)
    if x.ndim == 2:
        a, b = a[:, None], b[:, None]
    out = body.copy()
    out[:c] = body[:c] * 0 + (x[c:2 * c] * a + tail * b)
    # the loop now runs: [crossfaded start] ... [material up to where the tail began]
    return np.concatenate([out[:c], x[2 * c:-c]])
