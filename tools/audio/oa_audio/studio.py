"""Studio-grade instruments and effects for the music, on top of this repository's own DSP.

Matthew, 15 Nov 2026: "Make sure it's being built with optimal tools for making music." So the parts that most
decide whether electronic music sounds professional now come from established audio tools, run headless:

* **Faust** (GRAME's audio DSP language, compiled and run by DawDreamer): the analogue-style synths. Its
  library has proper virtual-analogue oscillators, Moog ladder filters and a singing-voice formant model. Used
  for the bass, chord stabs, pads and the shouted vocal chops.
* **Dragonfly Plate** and **ZamVerb** (open source reverbs, GPL; run as plugins by DawDreamer): the space.
  Both are time-invariant, so the music still loops exactly.
* **Spotify's Pedalboard** (JUCE's compressor, limiter and filters): bus glue on the drums.

Install (cloud workspace, once): see tools/audio/README.md. Every one of these is a tool, not a source of
recorded audio: what they output is this repository's own music, so no sample licence is involved. The tool
licences are recorded in docs/audio/licence_manifest.md.
"""
from __future__ import annotations

import functools
import os

import numpy as np

from . import dsp
from .seq import Note

SR = dsp.SR
BLOCK = 256
DRAGONFLY = "/usr/lib/lxvst"

# ---------------------------------------------------------------- Faust voices
# Each voice is polyphonic: Faust's own freq/gain/gate controls are driven by MIDI notes. Extra sliders are
# set per part (cutoff, drive and so on).
_HEAD = """
import("stdfaust.lib");
freq = hslider("freq",110,20,4000,0.01);
gain = hslider("gain",0.5,0,1,0.001);
gate = button("gate");
effect = _,_;
"""

BASS = _HEAD + """
cutoff = hslider("cutoff",260,30,8000,1);
envamt = hslider("envamt",1400,0,8000,1);
res = hslider("res",0.55,0,0.97,0.01);
sub = hslider("sub",1.0,0,3,0.01);
drive = hslider("drive",2.2,1,12,0.01);
decay = hslider("decay",0.16,0.01,2,0.001);
sustain = hslider("sustain",0.55,0,1,0.01);
env = en.adsr(0.0015, decay, sustain, 0.035, gate);
fenv = en.adsr(0.0005, decay*0.7, 0, 0.03, gate);
saws = (os.sawtooth(freq) + os.sawtooth(freq*1.0035) + os.square(freq)*0.5) * 0.35;
top = saws : ve.moog_vcf_2bn(res, min(15000, cutoff + envamt*fenv*(0.4+gain)));
low = os.osc(freq) * sub;
process = (top + low) * drive : ma.tanh : *(env*gain*0.6) <: _,_;
"""

STAB = _HEAD + """
cutoff = hslider("cutoff",1800,100,12000,1);
envamt = hslider("envamt",2500,0,10000,1);
res = hslider("res",0.35,0,0.95,0.01);
decay = hslider("decay",0.22,0.01,3,0.001);
release = hslider("release",0.12,0.005,3,0.001);
env = en.adsr(0.002, decay, 0.25, release, gate);
fenv = en.adsr(0.001, decay*0.6, 0.1, release, gate);
osc = (os.sawtooth(freq*0.998) + os.sawtooth(freq*1.002) + os.square(freq*2)*0.25 + os.osc(freq)*0.6) * 0.3;
process = osc : ve.moog_vcf_2bn(res, min(16000, cutoff + envamt*fenv*gain)) : *(env*gain) <: _,_;
"""

PAD = _HEAD + """
cutoff = hslider("cutoff",1600,100,12000,1);
attack = hslider("attack",1.2,0.001,8,0.001);
release = hslider("release",2.0,0.01,8,0.001);
spread = hslider("spread",0.009,0,0.05,0.0001);
env = en.adsr(attack, 1.0, 0.85, release, gate);
sw(d) = os.sawtooth(freq*(1+d));
left = (sw(0-spread) + sw(spread*0.31) + sw(0-spread*0.62) + sw(spread)*0.6) * 0.22;
right = (sw(spread) + sw(0-spread*0.27) + sw(spread*0.58) + sw(0-spread)*0.6) * 0.22;
lp = ve.moog_vcf_2bn(0.2, cutoff);
process = left, right : lp, lp : *(env*gain), *(env*gain);
"""

LEAD = _HEAD + """
cutoff = hslider("cutoff",2600,100,12000,1);
vib = hslider("vib",0.004,0,0.03,0.0001);
release = hslider("release",0.25,0.005,3,0.001);
env = en.adsr(0.012, 0.3, 0.7, release, gate);
f = freq * (1 + vib*os.osc(5.2)*en.adsr(0.4,0.1,1,0.1,gate));
osc = (os.square(f)*0.5 + os.sawtooth(f*1.003)*0.4 + os.osc(f*2)*0.2) * 0.4;
process = osc : ve.moog_vcf_2bn(0.3, cutoff) : *(env*gain) <: _,_;
"""

# A shouted "hey" (vowel e sliding to i, starting breathy) or "oh" (o): the crowd chant of rally house.
VOX = _HEAD + """
vowel0 = hslider("vowel0",1,0,4,0.01);
vowel1 = hslider("vowel1",2,0,4,0.01);
voice = hslider("voice",4,0,4,1);
t = en.ar(0.0, 0.16, gate);
vowel = vowel1 + (vowel0 - vowel1)*t;
fric = 0.7*en.ar(0.0, 0.03, gate);
drop = 1 - 0.09*(1-en.ar(0.0,0.2,gate));
env = en.adsr(0.006, 0.12, 0.6, 0.06, gate);
process = pm.SFFormantModelBP(voice, vowel, fric, freq*drop, 0.9) * env * gain * 0.15 : fi.highpass(2, 140) <: _,_;
"""


@functools.lru_cache(maxsize=1)
def _check() -> bool:
    import dawdreamer  # noqa: F401  (raises with a clear message if the studio tools are not installed)
    return True


def play(notes: list[Note], bpm: float, n_total: int, code: str, voices: int = 12, gain: float = 1.0,
         pan_spread: float = 0.0, **params) -> np.ndarray:
    """Renders notes (beats) through a Faust voice. Returns (n_total, 2)."""
    _check()
    import dawdreamer as daw
    beat = 60.0 / bpm
    eng = daw.RenderEngine(SR, BLOCK)
    p = eng.make_faust_processor("v")
    p.num_voices = voices
    p.set_dsp_string(code)
    if not p.compile():
        raise RuntimeError("Faust voice did not compile")
    for k, v in params.items():
        _set(p, k, v)
    for n in notes:
        if n.start * beat >= n_total / SR:
            continue
        p.add_midi_note(int(n.pitch), int(max(1, min(127, n.vel))), n.start * beat, max(0.01, n.dur * beat))
    eng.load_graph([(p, [])])
    eng.render(n_total / SR)
    y = eng.get_audio().T[:n_total]
    out = np.zeros((n_total, 2))
    out[: len(y)] = y
    return out * gain


def _set(p, key: str, value: float) -> None:
    for d in p.get_parameters_description():
        if d["name"].endswith(key) or d.get("label") == key:
            p.set_parameter(d["index"], float(value))
            return
    raise KeyError(f"Faust voice has no control named {key}")


def crowd(notes: list[Note], bpm: float, n_total: int, rng: np.random.Generator, size: int = 6, vowel=(1, 2)) -> np.ndarray:
    """A gang shout: the same notes sung by `size` voices with their own pitch, timing, pan and voice type."""
    out = np.zeros((n_total, 2))
    for i in range(size):
        detune = rng.uniform(-0.6, 0.6)
        late = rng.uniform(0.0, 0.025) * bpm / 60.0
        mine = [Note(n.start + late, n.dur, n.pitch + detune, n.vel - rng.integers(0, 15)) for n in notes]
        x = play(mine, bpm, n_total, VOX, voices=4, vowel0=vowel[0], vowel1=vowel[1], voice=[1, 4, 4, 1, 2, 4][i % 6])
        out += dsp.pan(dsp.mono(x), rng.uniform(-0.7, 0.7)) * 0.5
    return out


# ---------------------------------------------------------------- reverbs
# Only time-invariant reverbs are used, so a loop's reverb tail is the same every time round and the music still
# loops exactly: Dragonfly Plate (an algorithmic plate without modulation) and ZamVerb (a convolution room).
# Dragonfly's hall and room reverbs were tried and rejected: their built-in modulation breaks exact looping
# (measured: the second and third passes differ by -26 to -31 dB).


def _plugin(x: np.ndarray, path: str, values: dict[int, float]) -> np.ndarray:
    _check()
    import dawdreamer as daw
    eng = daw.RenderEngine(SR, BLOCK)
    src = eng.make_playback_processor("src", np.ascontiguousarray(dsp.stereo(x).T.astype(np.float32)))
    fx = eng.make_plugin_processor("fx", path)
    for i, v in values.items():
        fx.set_parameter(i, float(v))
    eng.load_graph([(src, []), (fx, ["src"])])
    eng.render(len(x) / SR)
    y = eng.get_audio().T
    out = np.zeros((len(x), 2))
    out[: min(len(x), len(y))] = y[: len(x)]
    return out


def plate(x: np.ndarray, decay: float = 0.12, predelay: float = 0.02, dampen: float = 0.6, width: float = 1.0) -> np.ndarray:
    """Wet-only Dragonfly Plate (its own 0-1 settings): the classic plate for claps, stabs and vocal chops.
    Longer decays stand in for a hall."""
    return _plugin(x, os.path.join(DRAGONFLY, "DragonflyPlateReverb-vst.so"),
                   {0: 0.0, 1: 0.6, 2: 0.5, 3: width, 4: predelay, 5: decay, 6: 0.05, 7: 0.7, 8: dampen})


def hall(x: np.ndarray, decay: float = 0.3, predelay: float = 0.08, dampen: float = 0.75) -> np.ndarray:
    """A long, dark plate used as a hall (see the note above on why not Dragonfly Hall)."""
    return plate(x, decay, predelay, dampen)


def room(x: np.ndarray, size: float = 0.2, decay: float = 0.06) -> np.ndarray:
    """Wet-only ZamVerb room (a convolution reverb): a small, real-sounding space around the drums. The
    arguments are kept for the call sites; the impulse response is fixed."""
    return _plugin(x, "/usr/lib/vst/ZamVerb-vst.so", {0: 0.5, 1: 1.0, 2: 0.0})


# ---------------------------------------------------------------- Pedalboard bus processing
def bus_comp(x: np.ndarray, threshold_db: float = -16.0, ratio: float = 3.0, attack_ms: float = 8.0,
             release_ms: float = 120.0, makeup_db: float = 2.0) -> np.ndarray:
    """JUCE's compressor (via Pedalboard), as drum-bus glue."""
    import pedalboard as pb
    board = pb.Pedalboard([pb.Compressor(threshold_db=threshold_db, ratio=ratio, attack_ms=attack_ms, release_ms=release_ms),
                           pb.Gain(gain_db=makeup_db)])
    return board(np.ascontiguousarray(dsp.stereo(x).T.astype(np.float32)), SR).T.astype(np.float64)
