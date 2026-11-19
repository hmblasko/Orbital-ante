"""Renders a short preview of the ship's directional thruster sounds, in the order printed at the end.

    python3 tools/audio/render_thrusters.py

It follows ThrusterMix.cs by hand (same dead zone, rise and fall rates, valve sounds) at the levels in the
audio manifest. It is a preview for listening, not a test: the game's own mixing is not in it.
"""
from __future__ import annotations

import json
import os

import numpy as np
import soundfile as sf

ROOT = os.path.dirname(os.path.abspath(__file__))
AUDIO = os.path.join(ROOT, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Audio")
OUT = os.path.join(ROOT, "build", "demo")
SR = 48000

LOOPS = {"main": "ship.engine.loop", "nose": "ship.thruster.nose.loop", "left": "ship.thruster.left.loop",
         "right": "ship.thruster.right.loop", "belly": "ship.thruster.belly.loop", "roof": "ship.thruster.roof.loop",
         "turn": "ship.thruster.turn.loop"}
PAN = {"left": -0.75, "right": 0.75}
# (seconds, what the pilot does, which thrusters fire)
SCRIPT = [
    (3.0, "push forward (main engine, behind)", ["main"]),
    (3.0, "push backward (nose thrusters, ahead)", ["nose"]),
    (3.0, "push right (left-side thrusters, left ear)", ["left"]),
    (3.0, "push left (right-side thrusters, right ear)", ["right"]),
    (3.0, "push up (belly thrusters, under the floor)", ["belly"]),
    (3.0, "push down (roof thrusters, overhead)", ["roof"]),
    (3.0, "turn (attitude jets)", ["turn"]),
    (3.0, "forward and right together", ["main", "left"]),
    (3.0, "up and backward together", ["belly", "nose"]),
]
GAP = 1.2


def load(clip):
    for ext in (".wav", ".ogg"):
        p = os.path.join(AUDIO, clip[len("Audio/"):] + ext)
        if os.path.exists(p):
            x, sr = sf.read(p, always_2d=True)
            assert sr == SR
            return x if x.shape[1] == 2 else np.repeat(x, 2, axis=1)
    raise FileNotFoundError(clip)


def main():
    manifest = json.load(open(os.path.join(AUDIO, "audio_manifest.json")))
    events = {e["id"]: e for e in manifest["events"]}
    total = sum(s[0] + GAP for s in SCRIPT) + 1.0
    n = int(total * SR)
    out = np.zeros((n, 2))
    target = {k: np.zeros(n) for k in LOOPS}
    at = 0.5
    lines = []
    rng = np.random.default_rng(7)
    for seconds, what, fire in SCRIPT:
        a, b = int(at * SR), int((at + seconds) * SR)
        for k in fire:
            target[k][a:b] = 1.0
            if k not in ("main", "turn"):
                for ev, when, gain in (("ship.thruster.start", a, 1.0), ("ship.thruster.stop", b, 0.8)):
                    e = events[ev]
                    x = load(e["clips"][rng.integers(len(e["clips"]))]) * 10 ** (e["volumeDb"] / 20) * gain
                    p = PAN.get(k, 0.0)
                    x = x * np.array([min(1.0, 1 - p), min(1.0, 1 + p)])
                    out[when:when + len(x)] += x[: n - when]
        lines.append(f"{int(at // 60)}:{at % 60:04.1f}  {what}")
        at += seconds + GAP
    for k, ev in LOOPS.items():
        up, down = (3.5, 1.8) if k == "main" else (9.0, 5.0)
        level = np.zeros(n)
        v = 0.0
        for i in range(0, n, 48):
            t = target[k][i]
            v = min(t, v + up * 0.001) if t > v else max(t, v - down * 0.001)
            level[i:i + 48] = v
        if not level.any():
            continue
        e = events[ev]
        x = load(e["clips"][0])
        x = np.tile(x, (int(np.ceil(n / len(x))), 1))[:n]
        out += x * (level * 10 ** (e["volumeDb"] / 20))[:, None]
    game_peak = 20 * np.log10(np.max(np.abs(out))) - 4
    # Turned up for listening on its own: in the game these sit under the music and effects.
    out *= 10 ** (-3 / 20) / np.max(np.abs(out))
    os.makedirs(OUT, exist_ok=True)
    sf.write(os.path.join(OUT, "thruster_preview.ogg"), out, SR, format="OGG", subtype="VORBIS")
    print(f"{total:.0f} s; peak in the game would be about {game_peak:.1f} dBFS; this file is turned up to -3")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
