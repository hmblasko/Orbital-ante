"""Core signal processing for the Orbital Ante audio pipeline.

Everything works on float64 numpy arrays at 48 kHz. Mono is shape (n,), stereo is shape (n, 2).
Per-sample loops are compiled with numba so whole songs render in seconds.
"""
from __future__ import annotations

import numpy as np
from numba import njit
from scipy import signal
from scipy.ndimage import maximum_filter1d

SR = 48000
TAU = 2.0 * np.pi


# ----------------------------------------------------------------------------- helpers

def secs(t: float) -> int:
    return int(round(t * SR))


def db(x: float) -> float:
    """Decibels to linear gain."""
    return 10.0 ** (x / 20.0)


def to_db(x: float) -> float:
    return 20.0 * np.log10(max(float(x), 1e-12))


def as_array(v, n: int) -> np.ndarray:
    if np.isscalar(v):
        return np.full(n, float(v))
    v = np.asarray(v, dtype=np.float64)
    if len(v) == n:
        return v
    return np.interp(np.linspace(0, 1, n), np.linspace(0, 1, len(v)), v)


def stereo(x: np.ndarray) -> np.ndarray:
    if x.ndim == 2:
        return x
    return np.stack([x, x], axis=1)


def mono(x: np.ndarray) -> np.ndarray:
    if x.ndim == 1:
        return x
    return x.mean(axis=1)


def pan(x: np.ndarray, pos: float) -> np.ndarray:
    """Equal-power pan of a mono signal. pos is -1 (left) to 1 (right)."""
    a = (pos + 1.0) * np.pi / 4.0
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def width(x: np.ndarray, amount: float) -> np.ndarray:
    """Mid/side width. 0 = mono, 1 = unchanged, above 1 = wider."""
    x = stereo(x)
    mid = (x[:, 0] + x[:, 1]) * 0.5
    side = (x[:, 0] - x[:, 1]) * 0.5 * amount
    return np.stack([mid + side, mid - side], axis=1)


def place(buf: np.ndarray, sig: np.ndarray, start: int, gain: float = 1.0) -> None:
    """Adds sig into buf at a sample position, clipping at the ends."""
    if start >= len(buf) or len(sig) == 0:
        return
    if start < 0:
        sig = sig[-start:]
        start = 0
    end = min(len(buf), start + len(sig))
    if end <= start:
        return
    if buf.ndim == 2 and sig.ndim == 1:
        buf[start:end, 0] += sig[: end - start] * gain
        buf[start:end, 1] += sig[: end - start] * gain
    elif buf.ndim == 1 and sig.ndim == 2:
        buf[start:end] += sig[: end - start].mean(axis=1) * gain
    else:
        buf[start:end] += sig[: end - start] * gain


def fade(x: np.ndarray, fade_in: float = 0.0, fade_out: float = 0.0) -> np.ndarray:
    x = x.copy()
    n = len(x)
    a = min(n, secs(fade_in))
    b = min(n, secs(fade_out))
    if a > 1:
        ramp = 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, a))
        x[:a] = (x[:a].T * ramp).T
    if b > 1:
        ramp = 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, b))
        x[-b:] = (x[-b:].T * ramp).T
    return x


def pad_to(x: np.ndarray, n: int) -> np.ndarray:
    if len(x) >= n:
        return x[:n]
    shape = (n - len(x),) + x.shape[1:]
    return np.concatenate([x, np.zeros(shape)])


# ----------------------------------------------------------------------------- envelopes

def env_exp(n: int, tau: float, attack: float = 0.0005) -> np.ndarray:
    """Fast attack, exponential decay. tau is the time to fall to 1/e, in seconds."""
    t = np.arange(n) / SR
    e = np.exp(-t / max(tau, 1e-5))
    a = max(1, secs(attack))
    if a < n:
        e[:a] *= np.linspace(0.0, 1.0, a)
    return e


def adsr(n: int, a: float, d: float, s: float, r: float, hold: float | None = None, curve: float = 3.0) -> np.ndarray:
    """ADSR with exponential-ish segments. `hold` is the gate length in seconds (default: n minus release)."""
    if hold is None:
        hold = max(0.0, n / SR - r)
    na, nd, nr = max(1, secs(a)), max(1, secs(d)), max(1, secs(r))
    nh = max(0, secs(hold))
    out = np.zeros(n)
    att = np.linspace(0, 1, na) ** (1.0 / 1.5)
    dec = s + (1 - s) * np.exp(-curve * np.linspace(0, 1, nd))
    dec = s + (dec - dec[-1]) * (1 - s) / max(1e-9, (1 - dec[-1])) if s < 1 else np.ones(nd)
    body = np.concatenate([att, dec])
    if len(body) < nh:
        body = np.concatenate([body, np.full(nh - len(body), s)])
    else:
        body = body[:nh] if nh > 0 else body[:1]
    level = body[-1] if len(body) else 0.0
    rel = level * np.exp(-curve * 1.6 * np.linspace(0, 1, nr))
    rel = rel - rel[-1] * np.linspace(0, 1, nr)
    full = np.concatenate([body, rel])
    m = min(n, len(full))
    out[:m] = full[:m]
    return out


def lfo(n: int, rate: float, phase: float = 0.0, shape: str = "sine") -> np.ndarray:
    t = np.arange(n) / SR
    ph = rate * t + phase
    if shape == "tri":
        return 2.0 * np.abs(2.0 * (ph - np.floor(ph + 0.5))) - 1.0
    return np.sin(TAU * ph)


def smooth_noise(n: int, rate: float, rng: np.random.Generator) -> np.ndarray:
    """Slowly wandering random curve in -1..1 (interpolated noise), for drift and humanisation."""
    pts = max(4, int(n / SR * rate) + 4)
    vals = rng.uniform(-1, 1, pts)
    xs = np.linspace(0, pts - 1, n)
    i = np.floor(xs).astype(int)
    f = xs - i
    f = f * f * (3 - 2 * f)
    i2 = np.minimum(i + 1, pts - 1)
    return vals[i] * (1 - f) + vals[i2] * f


# ----------------------------------------------------------------------------- oscillators

@njit(cache=True)
def _polyblep(t: float, dt: float) -> float:
    if t < dt:
        t /= dt
        return t + t - t * t - 1.0
    if t > 1.0 - dt:
        t = (t - 1.0) / dt
        return t * t + t + t + 1.0
    return 0.0


@njit(cache=True)
def _saw(freq: np.ndarray, phase0: float) -> np.ndarray:
    n = len(freq)
    out = np.empty(n)
    ph = phase0 % 1.0
    for i in range(n):
        dt = freq[i] / 48000.0
        out[i] = 2.0 * ph - 1.0 - _polyblep(ph, dt)
        ph += dt
        if ph >= 1.0:
            ph -= 1.0
    return out


@njit(cache=True)
def _square(freq: np.ndarray, pw: np.ndarray, phase0: float) -> np.ndarray:
    n = len(freq)
    out = np.empty(n)
    ph = phase0 % 1.0
    for i in range(n):
        dt = freq[i] / 48000.0
        w = pw[i]
        v = 1.0 if ph < w else -1.0
        v += _polyblep(ph, dt)
        t2 = ph - w
        if t2 < 0.0:
            t2 += 1.0
        v -= _polyblep(t2, dt)
        out[i] = v
        ph += dt
        if ph >= 1.0:
            ph -= 1.0
    return out


def saw(freq, n: int, phase: float = 0.0) -> np.ndarray:
    return _saw(as_array(freq, n), phase)


def square(freq, n: int, pw=0.5, phase: float = 0.0) -> np.ndarray:
    return _square(as_array(freq, n), as_array(pw, n), phase)


def sine(freq, n: int, phase: float = 0.0) -> np.ndarray:
    f = as_array(freq, n)
    ph = np.cumsum(f) / SR
    return np.sin(TAU * (ph + phase))


def tri(freq, n: int, phase: float = 0.0) -> np.ndarray:
    f = as_array(freq, n)
    ph = np.cumsum(f) / SR + phase
    return 2.0 * np.abs(2.0 * (ph - np.floor(ph + 0.5))) - 1.0


def supersaw(freq, n: int, voices: int = 7, detune: float = 0.18, rng: np.random.Generator | None = None,
             spread: float = 0.9) -> np.ndarray:
    """A stack of detuned saws spread across the stereo field. detune is in semitones for the outer pair."""
    rng = rng or np.random.default_rng(1)
    f = as_array(freq, n)
    out = np.zeros((n, 2))
    for v in range(voices):
        k = (v / (voices - 1) * 2 - 1) if voices > 1 else 0.0
        ratio = 2.0 ** (k * detune / 12.0)
        s = _saw(f * ratio, rng.uniform(0, 1))
        out += pan(s, k * spread) * (1.0 if abs(k) < 0.01 else 0.8)
    return out / np.sqrt(voices)


def fm(freq, n: int, ratio: float, index, feedback: float = 0.0, phase: float = 0.0) -> np.ndarray:
    """Two-operator FM. `index` may be an envelope array."""
    f = as_array(freq, n)
    idx = as_array(index, n)
    ph = np.cumsum(f) / SR
    modulator = np.sin(TAU * ph * ratio + phase)
    if feedback:
        modulator = np.sin(TAU * ph * ratio + feedback * modulator)
    return np.sin(TAU * ph + idx * modulator)


def white(n: int, rng: np.random.Generator) -> np.ndarray:
    return rng.uniform(-1.0, 1.0, n)


def pink(n: int, rng: np.random.Generator) -> np.ndarray:
    """Pink noise by shaping white noise in the frequency domain."""
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / SR)
    f[0] = f[1] if n > 1 else 1.0
    spec /= np.sqrt(f)
    x = np.fft.irfft(spec, n)
    return x / (np.max(np.abs(x)) + 1e-12)


def brown(n: int, rng: np.random.Generator) -> np.ndarray:
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / SR)
    f[0] = f[1] if n > 1 else 1.0
    spec /= f
    spec[f < 12.0] = 0
    x = np.fft.irfft(spec, n)
    return x / (np.max(np.abs(x)) + 1e-12)


@njit(cache=True)
def _karplus(excite: np.ndarray, n: int, period: float, damp: float, decay: float) -> np.ndarray:
    size = int(period) + 2
    line = np.zeros(size)
    out = np.empty(n)
    frac = period - int(period)
    w = 0
    last = 0.0
    for i in range(n):
        r = w - int(period)
        if r < 0:
            r += size
        r2 = r - 1
        if r2 < 0:
            r2 += size
        d = line[r] * (1.0 - frac) + line[r2] * frac
        last = last + (d - last) * damp
        x = last * decay
        if i < len(excite):
            x += excite[i]
        line[w] = x
        out[i] = x
        w += 1
        if w >= size:
            w = 0
    return out


def karplus(freq: float, n: int, rng: np.random.Generator, brightness: float = 0.5, sustain: float = 0.996,
            excite: np.ndarray | None = None) -> np.ndarray:
    """Plucked string (Karplus-Strong) with fractional delay."""
    period = SR / freq
    if excite is None:
        m = int(period) + 1
        excite = rng.uniform(-1, 1, m)
        excite = signal.lfilter([brightness, 1 - brightness], [1.0], excite)
    return _karplus(excite, n, period, 0.25 + 0.75 * brightness, sustain)


# ----------------------------------------------------------------------------- filters

@njit(cache=True)
def _svf(x: np.ndarray, cutoff: np.ndarray, q: np.ndarray, mode: int) -> np.ndarray:
    """Zavalishin trapezoidal state-variable filter with per-sample cutoff. mode: 0 LP, 1 BP, 2 HP, 3 notch."""
    n = len(x)
    out = np.empty(n)
    ic1 = 0.0
    ic2 = 0.0
    for i in range(n):
        fc = cutoff[i]
        if fc > 21000.0:
            fc = 21000.0
        if fc < 10.0:
            fc = 10.0
        g = np.tan(np.pi * fc / 48000.0)
        k = 1.0 / q[i]
        a1 = 1.0 / (1.0 + g * (g + k))
        a2 = g * a1
        a3 = g * a2
        v3 = x[i] - ic2
        v1 = a1 * ic1 + a2 * v3
        v2 = ic2 + a2 * ic1 + a3 * v3
        ic1 = 2.0 * v1 - ic1
        ic2 = 2.0 * v2 - ic2
        if mode == 0:
            out[i] = v2
        elif mode == 1:
            out[i] = v1
        elif mode == 2:
            out[i] = x[i] - k * v1 - v2
        else:
            out[i] = x[i] - k * v1
    return out


def _each(x: np.ndarray, fn) -> np.ndarray:
    if x.ndim == 1:
        return fn(x)
    return np.stack([fn(np.ascontiguousarray(x[:, c])) for c in range(x.shape[1])], axis=1)


def svf(x: np.ndarray, cutoff, q=0.707, mode: str = "lp", poles: int = 2) -> np.ndarray:
    """Modulatable filter. poles=4 runs it twice for a steeper slope."""
    n = len(x)
    c = as_array(cutoff, n)
    qq = as_array(q, n)
    m = {"lp": 0, "bp": 1, "hp": 2, "notch": 3}[mode]

    def run(ch):
        y = _svf(ch, c, qq, m)
        if poles == 4:
            y = _svf(y, c, qq, m)
        return y

    return _each(x, run)


def lowpass(x, hz: float, order: int = 2):
    sos = signal.butter(order, min(hz, SR * 0.45), "low", fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=0)


def highpass(x, hz: float, order: int = 2):
    sos = signal.butter(order, hz, "high", fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=0)


def bandpass(x, lo: float, hi: float, order: int = 2):
    sos = signal.butter(order, [lo, min(hi, SR * 0.45)], "band", fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=0)


def _biquad(b0, b1, b2, a0, a1, a2):
    return np.array([[b0 / a0, b1 / a0, b2 / a0, 1.0, a1 / a0, a2 / a0]])


def peak_eq(x, hz: float, gain_db: float, q: float = 1.0):
    a = 10 ** (gain_db / 40.0)
    w = TAU * hz / SR
    alpha = np.sin(w) / (2 * q)
    sos = _biquad(1 + alpha * a, -2 * np.cos(w), 1 - alpha * a, 1 + alpha / a, -2 * np.cos(w), 1 - alpha / a)
    return signal.sosfilt(sos, x, axis=0)


def shelf(x, hz: float, gain_db: float, kind: str = "low"):
    a = 10 ** (gain_db / 40.0)
    w = TAU * hz / SR
    cs, sn = np.cos(w), np.sin(w)
    alpha = sn / 2 * np.sqrt(2.0)
    sq = 2 * np.sqrt(a) * alpha
    if kind == "low":
        sos = _biquad(a * ((a + 1) - (a - 1) * cs + sq), 2 * a * ((a - 1) - (a + 1) * cs), a * ((a + 1) - (a - 1) * cs - sq),
                      (a + 1) + (a - 1) * cs + sq, -2 * ((a - 1) + (a + 1) * cs), (a + 1) + (a - 1) * cs - sq)
    else:
        sos = _biquad(a * ((a + 1) + (a - 1) * cs + sq), -2 * a * ((a - 1) + (a + 1) * cs), a * ((a + 1) + (a - 1) * cs - sq),
                      (a + 1) - (a - 1) * cs + sq, 2 * ((a - 1) - (a + 1) * cs), (a + 1) - (a - 1) * cs - sq)
    return signal.sosfilt(sos, x, axis=0)


def resonators(x: np.ndarray, freqs, decays, gains) -> np.ndarray:
    """A bank of ringing band-pass filters: turns a click or noise burst into struck metal, wood or glass."""
    out = np.zeros_like(x)
    for f, d, g in zip(freqs, decays, gains):
        if f >= SR * 0.45:
            continue
        r = np.exp(-1.0 / (max(d, 1e-4) * SR))
        w = TAU * f / SR
        b = [1 - r, 0, -(1 - r)]
        a = [1, -2 * r * np.cos(w), r * r]
        out += signal.lfilter(b, a, x, axis=0) * g
    return out


def comb(x: np.ndarray, hz: float, feedback: float, damp: float = 0.3) -> np.ndarray:
    period = max(2, int(round(SR / hz)))
    b = np.zeros(period + 1)
    b[0] = 1
    a = np.zeros(period + 2)
    a[0] = 1
    a[period] = -feedback * (1 - damp)
    a[period + 1] = -feedback * damp
    return signal.lfilter(b, a, x, axis=0)


# ----------------------------------------------------------------------------- dynamics and colour

def saturate(x: np.ndarray, drive: float = 1.0, mix: float = 1.0, bias: float = 0.0) -> np.ndarray:
    """Tanh saturation, level-compensated. A little bias adds even harmonics (tape or valve warmth)."""
    d = max(drive, 1e-3)
    y = np.tanh(x * d + bias) - np.tanh(bias)
    y /= np.tanh(d) if d < 3 else 1.0
    return x * (1 - mix) + y * mix


def softclip(x: np.ndarray, ceiling: float = 1.0) -> np.ndarray:
    return ceiling * np.tanh(x / ceiling)


def bitcrush(x: np.ndarray, hold: int) -> np.ndarray:
    idx = (np.arange(len(x)) // hold) * hold
    return x[idx]


@njit(cache=True)
def _comp_gain(level: np.ndarray, thresh: float, ratio: float, knee: float, att: float, rel: float) -> np.ndarray:
    n = len(level)
    out = np.empty(n)
    env = 0.0
    for i in range(n):
        l = level[i]
        if l > env:
            env += (l - env) * att
        else:
            env += (l - env) * rel
        ldb = 20.0 * np.log10(env + 1e-9)
        over = ldb - thresh
        if over <= -knee * 0.5:
            g = 0.0
        elif over >= knee * 0.5:
            g = -over * (1.0 - 1.0 / ratio)
        else:
            t = over + knee * 0.5
            g = -(1.0 - 1.0 / ratio) * t * t / (2.0 * knee)
        out[i] = 10.0 ** (g / 20.0)
    return out


def compress(x: np.ndarray, thresh_db: float = -18.0, ratio: float = 3.0, attack: float = 0.01, release: float = 0.15,
             knee: float = 6.0, makeup_db: float = 0.0, sidechain: np.ndarray | None = None) -> np.ndarray:
    key = x if sidechain is None else sidechain
    level = np.abs(key) if key.ndim == 1 else np.max(np.abs(key), axis=1)
    level = pad_to(level, len(x))
    g = _comp_gain(level, thresh_db, ratio, max(knee, 0.01), 1 - np.exp(-1 / (attack * SR)), 1 - np.exp(-1 / (release * SR)))
    g *= db(makeup_db)
    return (x.T * g).T


def limit(x: np.ndarray, ceiling_db: float = -1.0, lookahead: float = 0.004, release: float = 0.08) -> np.ndarray:
    """Look-ahead brickwall limiter. Checks 4x oversampled peaks so inter-sample overs are caught too."""
    g = limit_gain(x, ceiling_db, lookahead, release)
    return (x.T * g).T


def limit_gain(x: np.ndarray, ceiling_db: float = -1.0, lookahead: float = 0.004, release: float = 0.08) -> np.ndarray:
    """The limiter's gain curve on its own, so one curve can be applied to several stems of the same mix."""
    ceiling = db(ceiling_db)
    up = signal.resample_poly(x, 4, 1, axis=0)
    peak4 = np.abs(up) if up.ndim == 1 else np.max(np.abs(up), axis=1)
    peak = peak4[: len(x) * 4].reshape(len(x), 4).max(axis=1)
    look = max(1, secs(lookahead))
    need = np.minimum(1.0, ceiling / np.maximum(peak, 1e-9))
    need = -maximum_filter1d(-need, size=2 * look + 1, mode="nearest")
    rel = np.exp(-1.0 / (release * SR))
    g = _limit_smooth(need, rel)
    win = np.hanning(look * 2 + 1)
    win /= win.sum()
    g = np.minimum(g, np.convolve(g, win, mode="same"))
    g = np.minimum(g, need)
    return g


@njit(cache=True)
def _limit_smooth(need: np.ndarray, rel: float) -> np.ndarray:
    out = np.empty(len(need))
    g = 1.0
    for i in range(len(need)):
        if need[i] < g:
            g = need[i]
        else:
            g = need[i] + (g - need[i]) * rel
        out[i] = g
    return out


def duck_curve(n: int, hits: list[float], depth_db: float = -8.0, attack: float = 0.005, release: float = 0.22) -> np.ndarray:
    """Gain curve that dips at each hit time (seconds) and recovers: the pumping of a kick-ducked bass or pad."""
    g = np.ones(n)
    na, nr = max(1, secs(attack)), max(1, secs(release))
    dip = db(depth_db)
    shape = np.concatenate([np.linspace(1, dip, na), dip + (1 - dip) * (np.linspace(0, 1, nr) ** 1.6)])
    for t in hits:
        s = secs(t)
        if s >= n:
            continue
        e = min(n, s + len(shape))
        g[s:e] = np.minimum(g[s:e], shape[: e - s])
    return g


def transient(x: np.ndarray, amount: float = 0.5, fast: float = 0.001, slow: float = 0.03) -> np.ndarray:
    """Transient shaper: boosts (or softens, if negative) attacks by comparing a fast and a slow envelope."""
    level = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), axis=1)
    ef = signal.lfilter([1 - np.exp(-1 / (fast * SR))], [1, -np.exp(-1 / (fast * SR))], level)
    es = signal.lfilter([1 - np.exp(-1 / (slow * SR))], [1, -np.exp(-1 / (slow * SR))], level)
    g = 1.0 + amount * np.clip((ef - es) / (es + 1e-4), -1, 4)
    return (x.T * g).T


# ----------------------------------------------------------------------------- space and time effects

def chorus(x: np.ndarray, rate: float = 0.6, depth_ms: float = 3.0, base_ms: float = 12.0, mix: float = 0.5) -> np.ndarray:
    x = stereo(x)
    n = len(x)
    idx = np.arange(n, dtype=np.float64)
    out = np.empty_like(x)
    for c in range(2):
        mod = base_ms + depth_ms * np.sin(TAU * rate * idx / SR + c * np.pi / 2)
        pos = np.clip(idx - mod * SR / 1000.0, 0, n - 1)
        out[:, c] = np.interp(pos, idx, x[:, c])
    return x * (1 - mix * 0.5) + out * mix


@njit(cache=True)
def _delay(x: np.ndarray, dl: int, dr: int, fb: float, damp: float, cross: float) -> np.ndarray:
    n = x.shape[0]
    out = np.zeros((n, 2))
    bl = np.zeros(dl + 1)
    br = np.zeros(dr + 1)
    wl = 0
    wr = 0
    ll = 0.0
    lr = 0.0
    for i in range(n):
        yl = bl[wl]
        yr = br[wr]
        ll += (yl - ll) * damp
        lr += (yr - lr) * damp
        out[i, 0] = yl
        out[i, 1] = yr
        bl[wl] = x[i, 0] + (ll * (1 - cross) + lr * cross) * fb
        br[wr] = x[i, 1] + (lr * (1 - cross) + ll * cross) * fb
        wl += 1
        if wl > dl - 1:
            wl = 0
        wr += 1
        if wr > dr - 1:
            wr = 0
    return out


def delay(x: np.ndarray, time_l: float, time_r: float | None = None, feedback: float = 0.35, mix: float = 0.3,
          damp_hz: float = 4000.0, cross: float = 0.5, low_cut: float = 250.0) -> np.ndarray:
    """Stereo ping-pong delay with a darkening feedback path. Returns dry plus wet."""
    x = stereo(x)
    time_r = time_r if time_r is not None else time_l
    damp = 1 - np.exp(-TAU * damp_hz / SR)
    wet = _delay(np.ascontiguousarray(x), max(1, secs(time_l)), max(1, secs(time_r)), feedback, damp, cross)
    wet = highpass(wet, low_cut)
    return x + wet * mix


_IR_CACHE: dict = {}


def reverb_ir(decay: float, damping: float = 0.5, predelay: float = 0.012, size: float = 1.0, seed: int = 7,
              early: float = 0.5, low_cut: float = 180.0) -> np.ndarray:
    """A stereo impulse response built from shaped noise: a few early echoes, then a smooth tail whose highs die faster.

    decay is the time to fall 60 dB in the mids. damping 0..1 makes the highs decay faster (1 = very dark room).
    """
    key = (round(decay, 3), round(damping, 3), round(predelay, 4), round(size, 3), seed, round(early, 3), round(low_cut))
    if key in _IR_CACHE:
        return _IR_CACHE[key]
    rng = np.random.default_rng(seed)
    n = secs(decay * 1.25 + predelay + 0.05)
    t = np.arange(n) / SR
    ir = np.zeros((n, 2))
    bands = [(low_cut, 500, 1.15), (500, 2000, 1.0), (2000, 6000, 1.0 - 0.55 * damping), (6000, 16000, 1.0 - 0.85 * damping)]
    for c in range(2):
        noise = rng.standard_normal(n)
        acc = np.zeros(n)
        for lo, hi, mult in bands:
            rt = max(0.05, decay * mult)
            band = bandpass(noise, lo, hi, 2)
            acc += band * np.exp(-6.908 * t / rt) * (1.0 if lo < 2000 else 0.8)
        build = np.clip(t / (0.02 * size + 0.004), 0, 1) ** 1.5
        ir[:, c] = acc * build
    ir /= np.max(np.abs(ir)) + 1e-12
    n_early = int(6 + 6 * size)
    for i in range(n_early):
        at = rng.uniform(0.004, 0.06 * size + 0.01)
        amp = early * (0.9 - 0.6 * at / (0.06 * size + 0.01)) * rng.choice([-1, 1])
        p = rng.uniform(-0.8, 0.8)
        s = secs(at)
        if s < n:
            ir[s, 0] += amp * np.cos((p + 1) * np.pi / 4)
            ir[s, 1] += amp * np.sin((p + 1) * np.pi / 4)
    pre = secs(predelay)
    ir = np.concatenate([np.zeros((pre, 2)), ir])
    ir /= np.sqrt(np.sum(ir ** 2) / 2 + 1e-12)
    _IR_CACHE[key] = ir
    return ir


def reverb(x: np.ndarray, decay: float = 1.8, mix: float = 0.25, damping: float = 0.5, predelay: float = 0.015,
           size: float = 1.0, seed: int = 7, early: float = 0.5, low_cut: float = 180.0, wet_only: bool = False) -> np.ndarray:
    """Convolution reverb. Output keeps the input length (render with spare room at the end for the tail)."""
    x = stereo(x)
    ir = reverb_ir(decay, damping, predelay, size, seed, early, low_cut)
    wet = np.stack([
        signal.fftconvolve(x[:, 0], ir[:, 0])[: len(x)] * 0.7 + signal.fftconvolve(x[:, 1], ir[:, 0])[: len(x)] * 0.3,
        signal.fftconvolve(x[:, 1], ir[:, 1])[: len(x)] * 0.7 + signal.fftconvolve(x[:, 0], ir[:, 1])[: len(x)] * 0.3,
    ], axis=1) * 0.25
    if wet_only:
        return wet
    return x * (1 - mix * 0.3) + wet * mix


def with_tail(x: np.ndarray, tail: float) -> np.ndarray:
    shape = (secs(tail),) + x.shape[1:]
    return np.concatenate([x, np.zeros(shape)])


def haas(x: np.ndarray, ms: float = 12.0, side: int = 1) -> np.ndarray:
    """Mono to wide stereo by delaying one side a few milliseconds."""
    x = mono(x)
    d = secs(ms / 1000.0)
    delayed = np.concatenate([np.zeros(d), x])[: len(x)]
    return np.stack([x, delayed], axis=1) if side == 1 else np.stack([delayed, x], axis=1)


def pitch_shift_resample(x: np.ndarray, semitones: float) -> np.ndarray:
    """Plain resampling pitch change (also changes length), as a tape would."""
    ratio = 2.0 ** (semitones / 12.0)
    n = int(len(x) / ratio)
    idx = np.arange(n) * ratio
    base = np.arange(len(x))
    if x.ndim == 1:
        return np.interp(idx, base, x)
    return np.stack([np.interp(idx, base, x[:, c]) for c in range(x.shape[1])], axis=1)


def normalize_peak(x: np.ndarray, peak_db: float = -1.0) -> np.ndarray:
    p = np.max(np.abs(x)) + 1e-12
    return x * (db(peak_db) / p)


def remove_dc(x: np.ndarray) -> np.ndarray:
    return highpass(x, 18.0, 1)
