"""Notes, chords and rhythm helpers: the "sheet music" side of the pipeline.

Scores are written as short text lines so they stay readable and editable:

    "D4:2 A4:2 | G4:3 r:1"        pitch:beats, r = rest, | is only a visual bar line
    "[D3 F3 A3 C4]:4"             a chord
    "D4:1@110"                    @ sets the MIDI velocity for that note

Every score can be written out as a standard MIDI file (see sampler.save_midi) and opened in any DAW.
"""
from __future__ import annotations

from dataclasses import dataclass, replace

import numpy as np

NOTE_INDEX = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


@dataclass(frozen=True)
class Note:
    start: float  # beats from the start of the loop
    dur: float  # beats
    pitch: int  # MIDI note number
    vel: int = 90  # 1..127


def midi(name: str) -> int:
    """'D4' -> 62, 'Bb2' -> 46, 'F#5' -> 78."""
    letter = name[0].upper()
    rest = name[1:]
    acc = 0
    while rest and rest[0] in "#b":
        acc += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 12 * (int(rest) + 1) + NOTE_INDEX[letter] + acc


def hz(pitch: float) -> float:
    return 440.0 * 2.0 ** ((pitch - 69.0) / 12.0)


def line(text: str, start: float = 0.0, vel: int = 90, transpose: int = 0) -> list[Note]:
    """Parses a score line into notes, placed one after another starting at `start` beats."""
    notes: list[Note] = []
    t = start
    text = text.replace("|", " ")
    tokens: list[str] = []
    depth = 0
    cur = ""
    for ch in text:
        if ch == "[":
            depth += 1
        if ch == "]":
            depth -= 1
        if ch.isspace() and depth == 0:
            if cur:
                tokens.append(cur)
            cur = ""
        else:
            cur += ch
    if cur:
        tokens.append(cur)
    for tok in tokens:
        v = vel
        if "@" in tok:
            tok, vs = tok.rsplit("@", 1)
            v = int(vs)
        body, d = tok.rsplit(":", 1)
        dur = float(d)
        if body.lower() != "r":
            names = body.strip("[]").split() if body.startswith("[") else [body]
            for nm in names:
                notes.append(Note(t, dur, midi(nm) + transpose, v))
        t += dur
    return notes


def length(text: str) -> float:
    """Total beats in a score line (for checking a bar adds up)."""
    total = 0.0
    depth = 0
    cur = ""
    for ch in text.replace("|", " ") + " ":
        if ch == "[":
            depth += 1
        if ch == "]":
            depth -= 1
        if ch.isspace() and depth == 0:
            if cur:
                total += float(cur.rsplit("@", 1)[0].rsplit(":", 1)[1])
            cur = ""
        else:
            cur += ch
    return total


def bars(lines: list[str], beats_per_bar: float = 4.0, start_bar: int = 0, vel: int = 90, transpose: int = 0,
         check: bool = True) -> list[Note]:
    """One score line per bar. Raises if a bar does not add up, so typing slips are caught at build time."""
    out: list[Note] = []
    for i, text in enumerate(lines):
        if check and text.strip():
            got = length(text)
            if abs(got - beats_per_bar) > 1e-6:
                raise ValueError(f"bar {start_bar + i + 1} has {got} beats, expected {beats_per_bar}: {text!r}")
        out += line(text, (start_bar + i) * beats_per_bar, vel, transpose)
    return out


def shift(notes: list[Note], beats: float = 0.0, semis: int = 0, vel_scale: float = 1.0) -> list[Note]:
    return [replace(n, start=n.start + beats, pitch=n.pitch + semis, vel=int(np.clip(n.vel * vel_scale, 1, 127))) for n in notes]


def repeat(notes: list[Note], every: float, times: int) -> list[Note]:
    out: list[Note] = []
    for k in range(times):
        out += shift(notes, k * every)
    return out


def clip_to(notes: list[Note], lo: float, hi: float) -> list[Note]:
    return [n for n in notes if lo <= n.start < hi]


def swing(notes: list[Note], amount: float = 0.6, grid: float = 0.5) -> list[Note]:
    """Delays every off-beat on the grid. amount 0.5 = straight, 0.667 = triplet swing."""
    out = []
    for n in notes:
        pos = n.start / grid
        whole = np.floor(pos + 1e-6)
        frac = pos - whole
        if int(whole) % 2 == 1 and frac < 0.5:
            start = (whole - 1 + 2 * amount + frac) * grid
            out.append(replace(n, start=start, dur=max(0.05, n.dur - (start - n.start))))
        else:
            out.append(n)
    return out


def humanize(notes: list[Note], rng: np.random.Generator, timing: float = 0.012, vel: float = 7.0,
             bpm: float = 120.0) -> list[Note]:
    """Small random timing (seconds) and velocity changes so nothing sounds machine-stamped."""
    out = []
    beat = 60.0 / bpm
    for n in notes:
        dt = rng.normal(0, timing) / beat
        out.append(replace(n, start=max(0.0, n.start + dt), vel=int(np.clip(n.vel + rng.normal(0, vel), 1, 127))))
    return out


# ----------------------------------------------------------------------------- harmony

CHORDS = {
    "maj": [0, 4, 7], "m": [0, 3, 7], "7": [0, 4, 7, 10], "m7": [0, 3, 7, 10], "maj7": [0, 4, 7, 11],
    "m9": [0, 3, 7, 10, 14], "maj9": [0, 4, 7, 11, 14], "9": [0, 4, 7, 10, 14], "13": [0, 4, 10, 14, 21],
    "m11": [0, 3, 7, 10, 14, 17], "maj7#11": [0, 4, 7, 11, 18], "6/9": [0, 4, 7, 9, 14], "add9": [0, 4, 7, 14],
    "7sus4": [0, 5, 7, 10], "13sus": [0, 5, 10, 14, 21], "m7b5": [0, 3, 6, 10], "7b9": [0, 4, 7, 10, 13],
    "7#9": [0, 4, 10, 15], "7#5": [0, 4, 8, 10], "dim7": [0, 3, 6, 9], "13b9": [0, 4, 10, 13, 21],
    "maj7#5": [0, 4, 8, 11], "madd9": [0, 3, 7, 14], "sus2": [0, 2, 7], "5": [0, 7], "m6": [0, 3, 7, 9],
    "7alt": [0, 4, 10, 13, 20],
}


@dataclass(frozen=True)
class Chord:
    root: int  # pitch class 0..11
    kind: str
    bass: int | None = None  # pitch class of a slash bass note

    @property
    def tones(self) -> list[int]:
        return [(self.root + i) % 12 for i in CHORDS[self.kind]]

    @property
    def minor(self) -> bool:
        return self.kind.startswith("m") and not self.kind.startswith("maj")

    @property
    def dominant(self) -> bool:
        return self.kind[0] in "791" and not self.kind.startswith("maj")


def chord(symbol: str) -> Chord:
    """'Dm9', 'Bbmaj7#11', 'A/G', 'C6/9', 'F#7alt'."""
    bass = None
    if "/" in symbol and not symbol.endswith("6/9"):
        symbol, b = symbol.rsplit("/", 1)
        bass = midi(b + "0") % 12
    i = 1
    while i < len(symbol) and symbol[i] in "#b":
        i += 1
    root = midi(symbol[:i] + "0") % 12
    kind = symbol[i:] or "maj"
    if kind not in CHORDS:
        raise ValueError(f"unknown chord kind {kind!r} in {symbol!r}")
    return Chord(root, kind, bass)


def voicing(ch: Chord, low: int, count: int = 4, rootless: bool = False, top: int | None = None) -> list[int]:
    """Closest-position voicing of a chord starting at or above MIDI note `low`.

    rootless drops the root (the bass player has it), which is how a jazz pianist's left hand would voice it.
    """
    ivs = list(CHORDS[ch.kind])
    if rootless and len(ivs) > 3:
        ivs = ivs[1:]
    pcs = [(ch.root + i) % 12 for i in ivs]
    if len(pcs) > count:
        # keep the colour tones: 3rd, 7th and the top extensions
        pcs = pcs[:2] + pcs[-(count - 2):] if count > 2 else pcs[:count]
    notes = []
    cur = low
    # start from the chord tone nearest above `low`
    order = sorted(pcs, key=lambda p: (p - low) % 12)
    for p in order:
        n = low + ((p - low) % 12)
        notes.append(n)
    notes.sort()
    if top is not None:
        notes = [n if n <= top else n - 12 for n in notes]
        notes.sort()
    return notes


def progression(spec: list[tuple[str, float]]) -> list[tuple[float, float, Chord]]:
    """[('Dm9', 8), ('G13', 8)] (symbol, beats) -> [(start, beats, Chord)]."""
    out = []
    t = 0.0
    for sym, beats in spec:
        out.append((t, beats, chord(sym)))
        t += beats
    return out


def chord_at(prog: list[tuple[float, float, Chord]], beat: float) -> Chord:
    for start, dur, ch in prog:
        if start <= beat < start + dur:
            return ch
    return prog[-1][2]


def nearest_pc(pitch: int, pcs: list[int]) -> int:
    """The nearest note to `pitch` whose pitch class is in pcs."""
    best = pitch
    best_d = 99
    for d in range(-6, 7):
        if (pitch + d) % 12 in pcs and abs(d) < best_d:
            best, best_d = pitch + d, abs(d)
    return best
