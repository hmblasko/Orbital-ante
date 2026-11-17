#!/usr/bin/env python3
"""Renders an offline preview of the five-minute audio showcase.

    dotnet run --project tools/audio/sim -- unity/Assets/OrbitalAnte/Resources/Audio/audio_manifest.json tools/audio/build/demo/schedule.json
    python3 tools/audio/render_demo.py

The schedule comes from the game's own rules (the music director, intensity model, reward limiter and acoustics
all run for real, on a pretend clock). This script only does what Unity's audio engine would do with that
schedule: start stems on the stated sample, move volumes, play one-shots with distance and filtering, and add
everything up. It is a preview, not a recording of the game: Unity's distance curves, reverb and voice handling
are approximated here. What it proves is the composition, the timing and the mix balance.

Outputs tools/audio/build/demo/orbital_ante_audio_showcase.ogg (+ .wav), a picture of the run, and prints the
level of the sum BEFORE any limiting, which is the number that matters: Unity does not limit, it clips.
"""
from __future__ import annotations

import json
import os
import sys

import numpy as np
from scipy import signal

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from oa_audio import dsp, mixing  # noqa: E402
from oa_audio.dsp import SR  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
RES = os.path.join(REPO, "unity", "Assets", "OrbitalAnte", "Resources")
OUT = os.path.join(HERE, "build", "demo")
MUSIC_SLIDER = 0.8  # the game's default music volume setting


def load(clip: str) -> np.ndarray:
    for ext in (".wav", ".ogg"):
        p = os.path.join(RES, clip + ext)
        if os.path.exists(p):
            x = mixing.read(p)
            return x if x.ndim == 2 else np.stack([x, x], axis=1)
    raise FileNotFoundError(clip)


def gain_line(n: int, moves: list[tuple[float, float, float]], start_value: float) -> np.ndarray:
    """The game's GainLine, rendered per sample: each move heads for its target from wherever the level is."""
    g = np.full(n, start_value)
    value = start_value
    for t, seconds, target in sorted(moves):
        a = int(round(t * SR))
        if a >= n:
            break
        a = max(a, 0)
        value = g[a]
        b = min(n, a + max(1, int(round(seconds * SR))))
        g[a:b] = np.linspace(value, target, b - a, endpoint=False) if seconds > 0 else target
        g[b:] = target
    return g


def curve(g: np.ndarray) -> np.ndarray:
    return np.sin(np.clip(g, 0, 1) * np.pi / 2)


def main() -> None:
    sched = json.load(open(os.path.join(OUT, "schedule.json")))
    manifest = json.load(open(os.path.join(RES, "Audio", "audio_manifest.json")))
    events = {e["id"]: e for e in manifest["events"]}
    tracks = {t["id"]: t for t in manifest["music"]["tracks"]}
    cues = {c["id"]: c for c in manifest["music"]["cues"]}
    n = int(sched["length"] * SR)
    rng = np.random.default_rng(20261113)

    music = np.zeros((n, 2))
    # ---- music: each StartTrack is one playing instance, until its StopTrack
    live: dict[str, dict] = {}
    instances = []
    cue_plays = []
    for c in sched["music"]:
        if c["kind"] == "StartTrack":
            inst = dict(track=c["track"], start=c["time"], offset=c.get("offset", 0.0), master=[(c["time"], c["seconds"], 1.0)] if c["seconds"] > 0 else [],
                        master0=0.0 if c["seconds"] > 0 else 1.0, stems={}, end=sched["length"])
            live[c["track"]] = inst
            instances.append(inst)
        elif c["kind"] == "StopTrack" and c["track"] in live:
            inst = live.pop(c["track"])
            inst["master"].append((c["time"], c["seconds"], 0.0))
            inst["end"] = c["time"] + c["seconds"] + 0.05
        elif c["kind"] == "StemGain" and c["track"] in live:
            live[c["track"]]["stems"].setdefault(c["stem"], []).append((c["time"], c["seconds"], c["gain"]))
        elif c["kind"] == "PlayCue":
            cue_plays.append(dict(cue=c["cue"], start=c["time"], fade_at=None, fade=0.5))
        elif c["kind"] == "StopCue":
            for p in cue_plays:
                if p["cue"] == c["cue"] and p["fade_at"] is None and p["start"] <= c["time"]:
                    p["fade_at"], p["fade"] = c["time"], max(0.05, c["seconds"])
    for p in cue_plays:
        x = load(cues[p["cue"]]["clip"])
        a = int(round(p["start"] * SR))
        b = min(n, a + len(x))
        x = x[: b - a].copy()
        if p["fade_at"] is not None:
            k = (np.arange(len(x)) / SR + p["start"] - p["fade_at"]) / p["fade"]
            x *= np.clip(1.0 - k, 0.0, 1.0)[:, None]
        music[a:b] += x
    for inst in instances:
        t = tracks[inst["track"]]
        a = int(round(inst["start"] * SR))
        b = min(n, int(round(inst["end"] * SR)))
        if b <= a:
            continue
        master = curve(gain_line(n, inst["master"], inst["master0"]))[a:b]
        for stem in t["stems"]:
            x = load(stem["clip"])
            reps = int(np.ceil((b - a) / len(x)))
            off = int(round(inst["offset"] * SR)) % len(x)
            x = np.tile(np.roll(x, -off, axis=0), (reps, 1))[: b - a]
            g = curve(gain_line(n, inst["stems"].get(stem["id"], []), 0.0))[a:b]
            music[a:b] += x * (g * master)[:, None]
    # the reward duck, smoothed the way the game smooths it
    duck = np.ones(n)
    target = 1.0
    last = 0
    level = 1.0
    for t, on in sched["ducks"] + [[sched["length"], 0]]:
        a = min(n, int(t * SR))
        if a > last:
            steps = np.arange(a - last) / SR * 1.6
            seg = level + np.sign(target - level) * steps
            seg = np.clip(seg, min(level, target), max(level, target))
            duck[last:a] = seg
            level = seg[-1]
        target = 0.6 if on else 1.0
        last = a
    music *= (duck * MUSIC_SLIDER ** 2)[:, None]

    # ---- one-shots
    sfx = np.zeros((n, 2))
    wet = np.zeros((n, 2))
    last_variant: dict[str, int] = {}
    for s in sched["sounds"]:
        e = events[s["event"]]
        k = len(e["clips"])
        pick = 0
        if k > 1:
            pick = int(rng.integers(k - 1))
            if pick >= last_variant.get(e["id"], 0):
                pick += 1
            last_variant[e["id"]] = pick
        x = load(e["clips"][pick])
        db = s["gainDb"] + (rng.uniform(-e["volVarDb"], e["volVarDb"]) if e["volVarDb"] > 0 else 0.0)
        semis = 12 * np.log2(s["pitch"]) + (rng.uniform(-e["pitchVar"], e["pitchVar"]) if e["pitchVar"] > 0 else 0.0)
        if abs(semis) > 1e-3:
            x = dsp.pitch_shift_resample(x, semis)
        if s["cutoff"] < 20000:
            x = dsp.lowpass(x, s["cutoff"], 2)
        g = dsp.db(db)
        if s["metres"] > 0:
            # Unity's logarithmic rolloff, and a point source has no width of its own
            g *= e["minDist"] / max(e["minDist"], min(s["metres"], e["maxDist"]))
            mono = x.mean(axis=1)
            p = float(np.clip(s["side"], -1, 1)) * 0.85
            x = np.stack([mono * np.cos((p + 1) * np.pi / 4), mono * np.sin((p + 1) * np.pi / 4)], axis=1) * np.sqrt(2)
        a = int(round(s["time"] * SR))
        b = min(n, a + len(x))
        if b <= a:
            continue
        sfx[a:b] += x[: b - a] * g
        if s["reverb"] > 0 and e["vacuum"] in ("air", "self"):
            wet[a:b] += x[: b - a] * g * s["reverb"]

    # ---- loops
    beds = np.zeros((n, 2))
    by_event: dict[str, list] = {}
    for l in sched["loops"]:
        by_event.setdefault(l["event"], []).append(l)
    for eid, moves in by_event.items():
        e = events[eid]
        x = load(e["clips"][0])
        a = int(moves[0]["time"] * SR)
        level = gain_line(n, [(m["time"], m["seconds"], m["level"]) for m in moves], 0.0)
        nz = np.nonzero(level > 0)[0]
        if len(nz) == 0:
            continue
        a, b = nz[0], nz[-1] + 1
        reps = int(np.ceil((b - a) / len(x)))
        y = np.tile(x, (reps, 1))[: b - a]
        # the bed's treatment changes when the listener's space does
        out = np.zeros_like(y)
        for i, m in enumerate(moves):
            s0 = max(a, int(m["time"] * SR))
            s1 = min(b, int(moves[i + 1]["time"] * SR)) if i + 1 < len(moves) else b
            if s1 <= s0:
                continue
            seg = y[s0 - a: s1 - a]
            if m["cutoff"] < 20000:
                seg = dsp.lowpass(seg, m["cutoff"], 2)
            out[s0 - a: s1 - a] = seg * dsp.db(m["gainDb"])
        beds[a:b] += out * level[a:b, None]

    room = dsp.reverb(wet, 1.1, 1.0, 0.6, 0.01, 0.8, seed=909) if np.any(wet) else wet
    headroom = dsp.db(-4.0)  # MixMath.HeadroomDb in the game
    music, sfx, beds, room = music * headroom, sfx * headroom, beds * headroom, room * headroom
    mix = music + sfx + beds + room * 0.5

    def report(name, x):
        peak = dsp.to_db(np.max(np.abs(x)) + 1e-12)
        print(f"  {name:10s} peak {peak:6.1f} dBFS   loudness {mixing.lufs(x):6.1f} LUFS")
        return peak

    print("Level of each part and of the sum, before any limiting (Unity does not limit; over 0 dBFS would clip):")
    report("music", music)
    report("effects", sfx + room * 0.5)
    report("beds", beds)
    peak = report("everything", mix)
    over = int(np.sum(np.max(np.abs(mix), axis=1) > 1.0))
    print(f"  samples over full scale: {over} ({over / SR * 1000:.1f} ms in total)")

    # per-section loudness, to check the shape of the five minutes
    caps = sched["captions"] + [[sched["length"], "end"]]
    sections = []
    for (t0, name), (t1, _) in zip(caps[:-1], caps[1:]):
        seg = mix[int(t0 * SR): int(t1 * SR)]
        m = dict(section=name.split("  -  ")[0], start=t0, lufs=round(mixing.lufs(seg), 1), peak=round(dsp.to_db(np.max(np.abs(seg)) + 1e-12), 1),
                 music_lufs=round(mixing.lufs(music[int(t0 * SR): int(t1 * SR)]) or -99, 1))
        sections.append(m)
        print(f"  {m['section']:16s} from {int(t0) // 60}:{int(t0) % 60:02d}   {m['lufs']:6.1f} LUFS   peak {m['peak']:5.1f}   music alone {m['music_lufs']:6.1f} LUFS")

    final = dsp.limit(mix, -1.0)
    os.makedirs(OUT, exist_ok=True)
    mixing.write_wav(os.path.join(OUT, "orbital_ante_audio_showcase.wav"), final)
    mixing.write_ogg(os.path.join(OUT, "orbital_ante_audio_showcase.ogg"), final, 6.0)
    json.dump(dict(peak_before_limit_db=round(peak, 2), samples_over=over, lufs=round(mixing.lufs(mix), 2), sections=sections),
              open(os.path.join(OUT, "showcase_measurements.json"), "w"), indent=1)
    picture(sched, mix, music, sfx + beds)


def picture(sched, mix, music, rest) -> None:
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    fig, ax = plt.subplots(3, 1, figsize=(15, 9), sharex=True, gridspec_kw=dict(height_ratios=[1.1, 1.6, 1.2]))
    tr = np.array([[t[0], t[1]] for t in sched["trace"]])
    ax[0].fill_between(tr[:, 0], tr[:, 1], color="#e8872a", alpha=0.6)
    for y in (20, 40, 60, 80):
        ax[0].axhline(y, color="#444", lw=0.5)
    ax[0].set_ylim(0, 100)
    ax[0].set_ylabel("intensity")
    for t, text in sched["captions"]:
        ax[0].axvline(t, color="k", lw=0.8)
        ax[0].text(t + 1, 88, text.split("  -  ")[0].title(), fontsize=8)
    names = []
    for c in sched["music"]:
        if c["kind"] == "StartTrack":
            names.append((c["time"], c["track"]))
        if c["kind"] == "PlayCue":
            ax[1].axvline(c["time"], color="#c0392b", lw=1)
            ax[1].text(c["time"], 1.02, c["cue"], rotation=30, fontsize=7, color="#c0392b", transform=ax[1].get_xaxis_transform())
    f, t, S = signal.spectrogram(mix.mean(axis=1), SR, nperseg=4096, noverlap=2048)
    ax[1].pcolormesh(t, f, 10 * np.log10(S + 1e-12), vmin=-110, vmax=-30, shading="auto", cmap="magma")
    ax[1].set_yscale("log")
    ax[1].set_ylim(30, 18000)
    ax[1].set_ylabel("Hz (whole mix)")
    for t0, name in names:
        ax[1].text(t0 + 0.5, 40, name, color="w", fontsize=7)

    def env(x):
        hop = SR // 10
        k = len(x) // hop
        r = np.sqrt(np.mean(x[: k * hop].mean(axis=1).reshape(k, hop) ** 2, axis=1) + 1e-12)
        return np.arange(k) / 10, 20 * np.log10(r)
    for x, label, col in ((music, "music", "#2e86c1"), (rest, "effects and beds", "#27ae60")):
        tt, e = env(x)
        ax[2].plot(tt, e, label=label, color=col, lw=0.8)
    ax[2].set_ylim(-60, 0)
    ax[2].set_ylabel("level, dB RMS")
    ax[2].set_xlabel("seconds")
    ax[2].legend(loc="lower right")
    fig.tight_layout()
    fig.savefig(os.path.join(OUT, "showcase.png"), dpi=110)


if __name__ == "__main__":
    main()
