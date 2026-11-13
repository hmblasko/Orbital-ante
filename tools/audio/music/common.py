"""Arranging helpers shared by the compositions."""
from __future__ import annotations

import numpy as np

from oa_audio import dsp, mixing, synth
from oa_audio.dsp import secs
from oa_audio.seq import Chord, Note, chord_at, nearest_pc, voicing


def comp(prog, rhythm_for_bar, low: int = 55, count: int = 4, vel: int = 78, rootless: bool = True, bpb: int = 4,
         top: int | None = None) -> list[Note]:
    """Chord comping. rhythm_for_bar(bar_index) returns [(beat_in_bar, length, velocity_scale)]."""
    total = prog[-1][0] + prog[-1][1]
    notes: list[Note] = []
    for b in range(int(total // bpb)):
        for pos, ln, vs in rhythm_for_bar(b):
            at = b * bpb + pos
            ch = chord_at(prog, at + 0.01)
            for p in voicing(ch, low, count, rootless, top):
                notes.append(Note(at, ln, p, int(vel * vs)))
    return notes


def held(prog, low: int = 48, count: int = 4, vel: int = 64, rootless: bool = False, overlap: float = 0.0,
         top: int | None = None) -> list[Note]:
    """One sustained voicing per chord (pads)."""
    notes = []
    for start, dur, ch in prog:
        for p in voicing(ch, low, count, rootless, top):
            notes.append(Note(start, dur + overlap, p, vel))
    return notes


def root_of(ch: Chord, octave_c: int) -> int:
    """The chord's bass note in the octave starting at MIDI note octave_c."""
    pc = ch.bass if ch.bass is not None else ch.root
    return octave_c + pc


def approach(frm: int, to: int) -> int:
    """A note one step away from `to`, on the side `frm` comes from: the bass player's lead-in."""
    if to == frm:
        return to - 2
    return to - 1 if to > frm else to + 1


def stem(loop: mixing.Loop, x: np.ndarray, gain_db: float = 0.0, hp: float | None = None, lp: float | None = None) -> np.ndarray:
    """Finishes a stem: optional filtering (wrapped around the loop like every other effect) and a trim gain."""
    def chain(t):
        if hp:
            t = dsp.highpass(t, hp)
        if lp:
            t = dsp.lowpass(t, lp)
        return t
    return loop.fx(chain, x) * dsp.db(gain_db)


def presence(x: np.ndarray, lo: float = 550.0, hi: float = 1900.0, drive: float = 7.0, level_db: float = -9.0) -> np.ndarray:
    """Gives a bass something to say above 500 Hz: its low-mid band is overdriven on the side to make
    harmonics (pick, string and amp growl) and mixed back under the clean signal. Small speakers that cannot
    reproduce the notes themselves still carry the line, the way a bass amp's grit does."""
    grit = dsp.bandpass(np.tanh(dsp.bandpass(x, 90.0, 700.0) * drive), lo, hi)
    ref = np.sqrt(np.mean(x ** 2)) + 1e-12
    g = np.sqrt(np.mean(grit ** 2)) + 1e-12
    return x + grit * (ref / g) * dsp.db(level_db)


def balance(stems: dict[str, np.ndarray], levels: dict[str, float]) -> dict[str, np.ndarray]:
    """Sets each stem's loudness relative to the others (LUFS; only the differences matter, calibrate sets the
    total). This is mixing by meter rather than by ear, which is all a build without ears can honestly do; the
    numbers are the mix decisions and are meant to be revised after a human listens."""
    out = {}
    for k, v in stems.items():
        if k in levels:
            out[k] = v * dsp.db(levels[k] - mixing.lufs(v))
        else:
            out[k] = v
    return out


def glue(x: np.ndarray, crest_db: float = 13.0) -> np.ndarray:
    """Brings a spiky bus (drums) to a target crest factor by soft clipping its peaks, the way a drum bus is
    driven into tape or a clipper. Level in equals peak 1.0 out."""
    x = x / (np.max(np.abs(x)) + 1e-12)
    rms = np.sqrt(np.mean(x ** 2)) + 1e-12
    drive = dsp.db(dsp.to_db(1.0 / rms) - crest_db)
    if drive <= 1.0:
        return x
    y = np.tanh(x * drive)
    return y / (np.max(np.abs(y)) + 1e-12)


def tilt(stems: dict[str, np.ndarray]) -> dict[str, np.ndarray]:
    """Mastering EQ by meter. A mix that keeps most of its energy under 250 Hz booms on headphones and all but
    vanishes on a Steam Deck's speakers (which give up below about 200 Hz), and one with almost nothing above
    2 kHz sounds like it is in the next room. This measures both and corrects with two gentle shelves, never
    more than 7 dB, the same on every stem so the stems still add up to the corrected mix. The filters run
    over three joined copies and the middle one is kept, so the loop point stays exact."""
    from scipy import signal
    full = sum(stems.values()).mean(axis=1)
    f, p = signal.welch(full, 48000, nperseg=16384)
    total = p.sum() + 1e-20
    low = p[f < 250].sum() / total
    high = p[f >= 2000].sum() / total
    cut = float(np.clip(1.7 * 10 * np.log10(low / 0.42), 0.0, 6.0)) if low > 0.42 else 0.0
    lift = float(np.clip(0.8 * 10 * np.log10(0.09 / max(high, 1e-4)), 0.0, 7.0)) if high < 0.09 else 0.0
    if cut < 0.3 and lift < 0.3:
        return stems
    print(f"    master tilt: {low * 100:.0f}% of energy under 250 Hz, {high * 100:.0f}% above 2 kHz -> low shelf -{cut:.1f} dB, high shelf +{lift:.1f} dB")
    out = {}
    for k, v in stems.items():
        n = len(v)
        t = np.concatenate([v, v, v])
        if cut >= 0.3:
            t = dsp.shelf(t, 190.0, -cut, "low")
        if lift >= 0.3:
            t = dsp.shelf(t, 2600.0, lift, "high")
        out[k] = t[n: 2 * n]
    return out


def calibrate(stems: dict[str, np.ndarray], target_lufs: float = -16.0, ceiling_db: float = -1.5) -> dict[str, np.ndarray]:
    """Sets the tone and loudness of the whole piece without changing the balance between its stems."""
    stems = tilt(stems)
    full = sum(stems.values())
    n = len(full)
    for _ in range(3):
        g = dsp.db(target_lufs - mixing.lufs(full))
        # Mastering limiter on the whole mix, worked out over three joined copies of the loop so it also wraps
        # cleanly. Its gain curve is then applied to every stem alike: the stems still add up to exactly the
        # limited mix, and the balance between them never moves.
        curve = dsp.limit_gain(np.concatenate([full * g] * 3), ceiling_db, 0.003, 0.12)[n: 2 * n]
        stems = {k: (v * g) * curve[:, None] for k, v in stems.items()}
        full = sum(stems.values())
        if abs(mixing.lufs(full) - target_lufs) < 0.3:
            break
    return stems


def drum_maker(kit: dict):
    """kit maps MIDI drum note -> function(vel 0..1, rng) -> mono sound. Returns a `make` for Loop.hits."""

    def make(n: Note, rng):
        return kit[n.pitch](n.vel / 127.0, rng)

    return make


def pattern(spec: str, pitch: int, step: float = 0.25, vel: int = 100, start: float = 0.0, dur: float | None = None,
            ghost: int = 45, accent: int = 118) -> list[Note]:
    """Step-sequencer line: 'x' hit, 'X' accent, 'o' ghost note, '.' rest. Spaces are ignored."""
    out = []
    i = 0
    for ch in spec:
        if ch == " " or ch == "|":
            continue
        if ch in "xXo":
            v = vel if ch == "x" else accent if ch == "X" else ghost
            out.append(Note(start + i * step, dur or step, pitch, v))
        i += 1
    return out


def bars_of(spec_for_bar, n_bars: int, pitch: int, bpb: int = 4, **kw) -> list[Note]:
    out = []
    for b in range(n_bars):
        spec = spec_for_bar(b)
        if spec:
            out += pattern(spec, pitch, start=b * bpb, **kw)
    return out
