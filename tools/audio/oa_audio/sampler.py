"""Sampled instruments. Notes are written to a MIDI file and rendered through FluidSynth with a General MIDI
SoundFont (FluidR3_GM, MIT licence), which gives real recorded bass guitar, electric piano, vibraphone, brass,
upright bass and drum kits to sit alongside the synthesised parts.
"""
from __future__ import annotations

import os
import subprocess
import tempfile

import mido
import numpy as np
import soundfile as sf

from .dsp import SR
from .seq import Note

SOUNDFONTS = {
    "fluid": "/usr/share/sounds/sf2/FluidR3_GM.sf2",
    "muse": "/usr/share/sounds/sf3/MuseScore_General_Full.sf3",
}

# General MIDI program numbers used by the soundtrack (0-based).
GM = {
    "rhodes": 4, "dx_piano": 5, "clav": 7, "celesta": 8, "glock": 9, "vibes": 11, "marimba": 12,
    "nylon_guitar": 24, "jazz_guitar": 26, "clean_guitar": 27, "muted_guitar": 28,
    "upright_bass": 32, "finger_bass": 33, "pick_bass": 34, "fretless_bass": 35, "slap_bass": 36, "slap_bass2": 37,
    "strings": 48, "slow_strings": 49, "pizzicato": 45, "harp": 46, "tremolo_strings": 44,
    "choir": 52, "trumpet": 56, "trombone": 57, "muted_trumpet": 59, "french_horn": 60, "brass": 61,
    "tenor_sax": 66, "flute": 73, "piano": 0,
}
KITS = {"standard": 0, "room": 8, "power": 16, "electronic": 24, "tr808": 25, "jazz": 32, "brush": 40}

# General MIDI drum notes.
DR = {
    "kick": 36, "kick2": 35, "rim": 37, "snare": 38, "clap": 39, "snare2": 40, "tom_lo": 41, "hat": 42, "tom_mid": 45,
    "hat_pedal": 44, "hat_open": 46, "tom_hi": 48, "crash": 49, "ride": 51, "ride_bell": 53, "tamb": 54, "splash": 55,
    "cowbell": 56, "crash2": 57, "ride2": 59, "bongo_hi": 60, "bongo_lo": 61, "conga_mute": 62, "conga_hi": 63,
    "conga_lo": 64, "shaker": 70, "cabasa": 69, "maracas": 70, "claves": 75, "wood_hi": 76, "triangle": 81,
    "brush_tap": 38, "brush_swirl": 40,
}


def save_midi(path: str, notes: list[Note], bpm: float, program: int = 0, drums: bool = False, name: str = "part",
              cents: float = 0.0) -> None:
    """Writes notes as a standard MIDI file (tempo included), ready to open in any DAW."""
    ppq = 480
    mid = mido.MidiFile(ticks_per_beat=ppq)
    track = mido.MidiTrack()
    mid.tracks.append(track)
    ch = 9 if drums else 0
    track.append(mido.MetaMessage("track_name", name=name, time=0))
    track.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(bpm), time=0))
    track.append(mido.Message("program_change", channel=ch, program=program, time=0))
    if cents:
        # the default bend range is two semitones either way
        track.append(mido.Message("pitchwheel", channel=ch, pitch=int(round(cents / 200.0 * 8191)), time=0))
    events = []
    for n in notes:
        on = int(round(n.start * ppq))
        off = int(round((n.start + n.dur) * ppq))
        if off <= on:
            off = on + 1
        events.append((on, 1, mido.Message("note_on", channel=ch, note=int(n.pitch), velocity=int(np.clip(n.vel, 1, 127)))))
        events.append((off, 0, mido.Message("note_off", channel=ch, note=int(n.pitch), velocity=0)))
    events.sort(key=lambda e: (e[0], e[1]))
    last = 0
    for tick, _, msg in events:
        msg.time = tick - last
        last = tick
        track.append(msg)
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    mid.save(path)


def render(notes: list[Note], bpm: float, total_beats: float, program: int = 0, drums: bool = False, font: str = "fluid",
           tail: float = 3.0, gain: float = 0.8, midi_path: str | None = None, name: str = "part",
           dynamics: bool = True) -> np.ndarray:
    """Renders notes through the SoundFont. Returns stereo float audio of exactly total_beats plus `tail` seconds.

    The sound bank's basses, guitars, strings and choirs have one recording per note: played softer they only
    get quieter, and the same note twice is the same sound twice, which is what gives a MIDI file away. With
    `dynamics` on, the part is played as six takes instead of one and added together: soft, medium and hard
    notes are separate takes (soft ones are filtered darker and rounder, hard ones are left bright and driven
    a little), and within each, alternate notes come from two takes tuned a few cents apart and filtered
    slightly differently. A ghost note then really is duller than an accent, and no note repeats exactly.
    """
    if dynamics and not drums and len(notes) > 1:
        from . import dsp
        if midi_path:
            save_midi(midi_path, notes, bpm, program, drums, name)
        out = None
        ordered = sorted(notes, key=lambda n: (n.start, n.pitch))
        takes = [(lambda v: v < 72, 2600.0, 0.0), (lambda v: 72 <= v < 100, 6000.0, 0.0), (lambda v: v >= 100, None, 1.35)]
        for is_in, cutoff, drive in takes:
            group = [n for n in ordered if is_in(n.vel)]
            for alt, cents, shade in ((0, -4.0, 1.0), (1, 5.0, 1.22)):
                part = group[alt::2]
                if not part:
                    continue
                x = _render_once(part, bpm, total_beats, program, False, font, tail, gain, None, name, cents)
                if cutoff:
                    x = dsp.lowpass(x, cutoff * shade, 1)
                elif alt:
                    x = dsp.lowpass(x, 9000.0, 1)
                if drive:
                    # Level-matched, so stepping from the medium take to the hard one changes the tone, not the volume.
                    x = dsp.saturate(x, drive, 0.5) / (0.5 + 0.5 * drive / np.tanh(drive))
                out = x if out is None else out + x
        return out
    return _render_once(notes, bpm, total_beats, program, drums, font, tail, gain, midi_path, name, 0.0)


def _render_once(notes, bpm, total_beats, program, drums, font, tail, gain, midi_path, name, cents) -> np.ndarray:
    n_out = int(round(total_beats * 60.0 / bpm * SR)) + int(tail * SR)
    if not notes:
        return np.zeros((n_out, 2))
    with tempfile.TemporaryDirectory() as tmp:
        mid_path = midi_path or os.path.join(tmp, "part.mid")
        # A silent note at the very end keeps FluidSynth rendering through the tail.
        pad_beats = total_beats + tail * bpm / 60.0 + 1.0
        all_notes = list(notes) + [Note(pad_beats, 0.25, 1, 1)]
        save_midi(mid_path, all_notes, bpm, program, drums, name, cents)
        wav = os.path.join(tmp, "out.wav")
        cmd = ["fluidsynth", "-ni", "-q", "-F", wav, "-r", str(SR), "-g", str(gain), "-R", "0", "-C", "0",
               "-O", "float", "-T", "wav", "-o", "synth.polyphony=512", SOUNDFONTS[font], mid_path]
        res = subprocess.run(cmd, capture_output=True, text=True)
        if res.returncode != 0 or not os.path.exists(wav):
            raise RuntimeError("fluidsynth failed: " + res.stderr[-500:])
        data, sr = sf.read(wav, dtype="float64", always_2d=True)
        if sr != SR:
            raise RuntimeError(f"unexpected sample rate {sr}")
    if len(data) < n_out:
        data = np.concatenate([data, np.zeros((n_out - len(data), 2))])
    return data[:n_out]
