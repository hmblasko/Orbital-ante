"""Loop rendering, file export and measurements."""
from __future__ import annotations

import os
import subprocess
import tempfile
import zlib

import numpy as np
import pyloudnorm
import soundfile as sf
from scipy import signal

from . import dsp, sampler, synth
from .dsp import SR, secs
from .seq import Note, shift


class Loop:
    """A seamless musical loop.

    Each part is rendered once with room for its notes to ring out, and whatever rings past the end of the loop
    is folded back onto the start. The part is then exactly periodic: the end runs into the start as if the
    music had simply kept playing. Effects are run over several joined copies of the loop and a middle copy is
    kept (`fx`), so reverb and delay tails wrap around as well.
    """

    def __init__(self, bpm: float, bars: int, beats_per_bar: int = 4, seed: int = 1, tail: float = 8.0,
                 session_dir: str | None = None):
        self.bpm = bpm
        self.bars = bars
        self.bpb = beats_per_bar
        self.beats = bars * beats_per_bar
        self.n = int(round(self.beats * 60.0 / bpm * SR))
        self.tail = tail
        self.n_render = self.n + secs(tail)
        self.seed = seed
        self.rng = np.random.default_rng(seed)
        self.session_dir = session_dir
        self.parts: dict[str, tuple[list[Note], int, bool]] = {}
        # OA_SEAM_CHECK=1 makes `fx` process one more copy of the loop and compare the last two. If they match,
        # the kept copy joins onto itself exactly as continuous playback would. Values are dB relative to the
        # signal (the more negative the better; below -60 dB is far under anything audible).
        self.check = bool(os.environ.get("OA_SEAM_CHECK"))
        self.exactness: list[float] = []

    @property
    def beat(self) -> float:
        return 60.0 / self.bpm

    @property
    def seconds(self) -> float:
        return self.n / SR

    def bar(self, index: int) -> float:
        """Beat position of a bar (1-based, like sheet music)."""
        return (index - 1) * self.bpb

    def rate(self, hz: float) -> float:
        """The nearest rate that fits a whole number of cycles into the loop. Effects that sweep over time
        (chorus, tremolo, auto-pan) use this so they are at the same point of their cycle at the wrap."""
        return max(1, round(hz * self.seconds)) / self.seconds

    def part_seed(self, name: str) -> int:
        return zlib.crc32(name.encode()) ^ self.seed

    def fold(self, x: np.ndarray) -> np.ndarray:
        """Wraps everything past the loop's end back onto its start."""
        x = dsp.stereo(x)
        out = x[: self.n].copy()
        rest = x[self.n:]
        while len(rest):
            m = min(len(rest), self.n)
            out[:m] += rest[:m]
            rest = rest[m:]
        return out

    def log(self, name: str, notes: list[Note], program: int = 0, drums: bool = False) -> None:
        """Remembers a part so its MIDI file is saved with the session."""
        self.parts[name] = (notes, program, drums)
        if self.session_dir:
            sampler.save_midi(os.path.join(self.session_dir, name + ".mid"), notes, self.bpm, program, drums, name)

    def synth(self, name: str, notes: list[Note], voice, gain: float = 1.0, **kw) -> np.ndarray:
        self.log(name, notes)
        return self.fold(synth.play(notes, self.bpm, self.n_render, voice, self.rng, gain,
                                    period=self.beats, seed=self.part_seed(name), **kw))

    def sf(self, name: str, notes: list[Note], program: int, drums: bool = False, font: str = "fluid",
           gain: float = 0.8) -> np.ndarray:
        self.log(name, notes, program, drums)
        x = sampler.render(notes, self.bpm, self.beats, program, drums, font, self.tail + 0.5, gain)
        return self.fold(x)

    def hits(self, name: str, notes: list[Note], make, gain: float = 1.0, pan: float = 0.0) -> np.ndarray:
        """Places one-shot drum sounds. `make(note, rng)` returns the mono sound for that note."""
        self.log(name, notes, 0, True)
        buf = np.zeros((self.n_render, 2))
        seed = self.part_seed(name)
        for n in notes:
            s = make(n, synth.note_rng(seed, n, self.beats))
            dsp.place(buf, dsp.pan(s, pan) if s.ndim == 1 else s, secs(n.start * self.beat), gain)
        return self.fold(buf)

    def duck(self, notes: list[Note], depth_db: float = -6.0, attack: float = 0.005, release: float = 0.22) -> np.ndarray:
        """Side-chain gain curve for one loop (a column, ready to multiply a stereo part)."""
        times = [n.start * self.beat for n in notes]
        times = times + [t + self.seconds for t in times]
        g = dsp.duck_curve(2 * self.n, times, depth_db, attack, release)
        return g[self.n:][:, None]

    def lfo(self, hz: float, phase: float = 0.0) -> np.ndarray:
        return dsp.lfo(self.n, self.rate(hz), phase)

    def fx(self, fn, *parts: np.ndarray) -> np.ndarray:
        """Runs an effect chain so that its tails wrap around the loop: the chain processes joined copies of
        the loop and the last full copy is kept."""
        copies = 3 if self.check else 2
        tiled = [np.concatenate([dsp.stereo(p)] * copies) for p in parts]
        y = dsp.stereo(fn(*tiled))
        keep = y[(copies - 1) * self.n: copies * self.n]
        if self.check:
            prev = y[self.n: 2 * self.n]
            first = y[: self.n]
            # Compare the copy before the kept one with the one before that: equal means the chain has settled.
            err = np.sqrt(np.mean((keep - prev) ** 2)) / (np.sqrt(np.mean(keep ** 2)) + 1e-12)
            self.exactness.append(dsp.to_db(err))
            del first
        return keep.copy()


# ----------------------------------------------------------------------------- export

def write_wav(path: str, x: np.ndarray, rng: np.random.Generator | None = None) -> None:
    """16-bit WAV with triangular dither."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    rng = rng or np.random.default_rng(0)
    lsb = 1.0 / 32768.0
    d = (rng.uniform(-0.5, 0.5, x.shape) + rng.uniform(-0.5, 0.5, x.shape)) * lsb
    sf.write(path, np.clip(x + d, -1.0, 1.0 - lsb), SR, subtype="PCM_16")


def write_ogg(path: str, x: np.ndarray, quality: float = 6.0) -> None:
    """Ogg Vorbis through ffmpeg. Unity imports Ogg loops gaplessly (MP3 would add silence at the ends)."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        wav = os.path.join(tmp, "in.wav")
        sf.write(wav, x, SR, subtype="PCM_24")
        cmd = ["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", str(quality),
               "-map_metadata", "-1", "-bitexact", path]
        subprocess.run(cmd, check=True)


def read(path: str) -> np.ndarray:
    data, sr = sf.read(path, dtype="float64", always_2d=False)
    if sr != SR:
        raise RuntimeError(f"{path}: sample rate {sr}, expected {SR}")
    return data


# ----------------------------------------------------------------------------- measurement

_METER = pyloudnorm.Meter(SR)


def lufs(x: np.ndarray) -> float | None:
    """Integrated loudness (ITU-R BS.1770). None for sounds too short to measure (under 0.4 s)."""
    if len(x) < int(0.45 * SR):
        return None
    try:
        v = _METER.integrated_loudness(x)
    except Exception:
        return None
    return None if not np.isfinite(v) else float(v)


def true_peak_db(x: np.ndarray) -> float:
    up = signal.resample_poly(x, 4, 1, axis=0)
    return dsp.to_db(np.max(np.abs(up)))


def measure(x: np.ndarray) -> dict:
    m = dsp.mono(x)
    peak = float(np.max(np.abs(x)))
    rms = float(np.sqrt(np.mean(m ** 2)))
    spec = np.abs(np.fft.rfft(m * np.hanning(len(m)))) ** 2
    freqs = np.fft.rfftfreq(len(m), 1 / SR)
    total = spec.sum() + 1e-20
    env = np.abs(m)
    win = max(1, secs(0.002))
    env = np.convolve(env, np.ones(win) / win, mode="same")
    pk = env.max() + 1e-12
    above10 = np.argmax(env > 0.1 * pk)
    above90 = np.argmax(env > 0.9 * pk)
    audible = np.where(env > pk * dsp.db(-50))[0]

    def band(lo, hi):
        return float(spec[(freqs >= lo) & (freqs < hi)].sum() / total)

    out = {
        "seconds": round(len(x) / SR, 4),
        "channels": 1 if x.ndim == 1 else x.shape[1],
        "peak_db": round(dsp.to_db(peak), 2),
        "true_peak_db": round(true_peak_db(x), 2),
        "rms_db": round(dsp.to_db(rms), 2),
        "crest_db": round(dsp.to_db(peak) - dsp.to_db(rms), 2),
        "lufs": None if lufs(x) is None else round(lufs(x), 2),
        "dc": round(float(abs(np.mean(m))), 6),
        "centroid_hz": round(float((freqs * spec).sum() / total), 1),
        "attack_ms": round(max(0, above90 - above10) / SR * 1000, 2),
        "audible_seconds": round((audible[-1] - audible[0]) / SR, 4) if len(audible) else 0.0,
        "sub_pct": round(100 * band(0, 60), 1),
        "low_pct": round(100 * band(60, 250), 1),
        "lowmid_pct": round(100 * band(250, 500), 1),
        "mid_pct": round(100 * band(500, 2000), 1),
        "highmid_pct": round(100 * band(2000, 6000), 1),
        "high_pct": round(100 * band(6000, 24000), 1),
        "clipped_samples": int(np.sum(np.abs(x) >= 0.9999)),
    }
    if x.ndim == 2:
        l, r = x[:, 0], x[:, 1]
        denom = np.sqrt(np.sum(l * l) * np.sum(r * r)) + 1e-20
        out["stereo_correlation"] = round(float(np.sum(l * r) / denom), 3)
    return out


def seam(x: np.ndarray) -> dict:
    """How clean a loop's wrap point is.

    jump_ratio compares the sample-to-sample step across the wrap with the largest steps inside the file
    (99.9th percentile). At or below 1 the wrap is no rougher than the material itself.
    flux_ratio compares the short-time spectrum change across the wrap with the typical change between
    neighbouring windows; near 1 means the timbre does not lurch at the wrap either.
    """
    m = dsp.mono(x)
    steps = np.abs(np.diff(m))
    ref = np.percentile(steps, 99.9) + 1e-9
    jump = abs(m[0] - m[-1])
    w = 2048

    def mag(seg):
        return np.abs(np.fft.rfft(seg * np.hanning(len(seg))))

    fluxes = []
    for i in range(w, len(m) - w, max(w, (len(m) - 2 * w) // 200)):
        a, b = mag(m[i - w:i]), mag(m[i:i + w])
        fluxes.append(np.sum((a - b) ** 2) / (np.sum(a ** 2) + np.sum(b ** 2) + 1e-12))
    a, b = mag(m[-w:]), mag(m[:w])
    seam_flux = np.sum((a - b) ** 2) / (np.sum(a ** 2) + np.sum(b ** 2) + 1e-12)
    typical = float(np.percentile(fluxes, 90)) + 1e-9
    return {"jump_ratio": round(float(jump / ref), 3), "flux_ratio": round(float(seam_flux / typical), 3)}
