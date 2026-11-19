#!/usr/bin/env python3
"""Automated audio checks. Measures every file fresh from disk and writes docs/audio/qa_report.md.

    python3 tools/audio/qa.py          exit code 1 if any check fails

These are measurements, not listening. They can prove a file is clean, level, loops exactly and differs from
its neighbours; they cannot say whether it sounds good.
"""
from __future__ import annotations

import json
import os
import sys

import numpy as np
import soundfile as sf
from scipy import signal

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from oa_audio import dsp, mixing  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
RES = os.path.join(REPO, "unity", "Assets", "OrbitalAnte", "Resources")
REPORT = os.path.join(REPO, "docs", "audio", "qa_report.md")
SR = 48000

FIREARMS = ["rifle", "smg", "handcannon", "sniper", "suppressed", "laser_revolver", "raygun", "laser_shotgun"]


def path_of(clip: str) -> str | None:
    for ext in (".wav", ".ogg"):
        p = os.path.join(RES, clip + ext)
        if os.path.exists(p):
            return p
    return None


def features(x: np.ndarray) -> np.ndarray:
    """What makes one gunshot different from another, as numbers: how long it lasts, how fast the tail dies,
    where its energy sits in eight bands, how bright it is, and how pitched it is."""
    m = x.mean(axis=1) if x.ndim == 2 else x
    m = m / (np.max(np.abs(m)) + 1e-12)
    env = np.abs(signal.hilbert(m))
    env = signal.sosfiltfilt(signal.butter(2, 80, fs=SR, output="sos"), env)
    pk = int(np.argmax(env))
    below = np.where(env[pk:] < 10 ** (-40 / 20))[0]
    dur = (below[0] if len(below) else len(env) - pk) / SR
    below20 = np.where(env[pk:] < 0.1)[0]
    d20 = (below20[0] if len(below20) else len(env) - pk) / SR
    f, p = signal.welch(m, SR, nperseg=min(4096, len(m)))
    edges = [30, 120, 250, 500, 1000, 2000, 4000, 8000, 20000]
    bands = np.array([p[(f >= a) & (f < b)].sum() for a, b in zip(edges[:-1], edges[1:])])
    bands = 10 * np.log10(bands / (bands.sum() + 1e-20) + 1e-6)
    centroid = (f * p).sum() / (p.sum() + 1e-20)
    tonal = 10 * np.log10(p.max() / (np.median(p[(f > 100) & (f < 8000)]) + 1e-20))
    # scaled so each feature moves by about 1 between clearly different sounds
    return np.concatenate([[np.log2(dur / 0.3) * 1.5, np.log2(d20 / 0.08) * 1.5, np.log2(centroid / 1500.0) * 1.5, tonal / 12.0], bands / 6.0])


def main() -> int:
    manifest = json.load(open(os.path.join(RES, "Audio", "audio_manifest.json")))
    build_meas = {}
    mp = os.path.join(HERE, "build", "measurements.json")
    if os.path.exists(mp):
        build_meas = json.load(open(mp))
    lines: list[str] = []
    fails: list[str] = []
    warns: list[str] = []

    def check(ok: bool, text: str, warn: bool = False) -> None:
        if not ok:
            (warns if warn else fails).append(text)

    # ------------------------------------------------------------ sound effects
    rows = []
    n_files = 0
    for e in manifest["events"]:
        for clip in e["clips"]:
            p = path_of(clip)
            if p is None:
                check(False, f"{e['id']}: missing file {clip}")
                continue
            n_files += 1
            info = sf.info(p)
            check(info.samplerate == SR, f"{clip}: sample rate {info.samplerate}")
            x = mixing.read(p)
            x2 = x if x.ndim == 2 else x[:, None]
            m = mixing.measure(x)
            clipped = int(np.sum(np.abs(x2) >= 0.9999))
            check(clipped == 0, f"{clip}: {clipped} clipped samples")
            check(m["true_peak_db"] <= -0.3, f"{clip}: true peak {m['true_peak_db']} dB is above -0.3")
            check(m["dc"] < 0.01, f"{clip}: DC offset {m['dc']}")
            small = m["sub_pct"] + m["low_pct"]
            if e["bus"] != "Environment" and not e["loop"]:
                check(small < 85, f"{clip}: {small:.0f}% of its energy is under 250 Hz, so little of it would reach Deck speakers", warn=True)
            if e["loop"]:
                s = mixing.seam(x)
                check(s["jump_ratio"] <= 1.5, f"{clip}: loop wrap step is {s['jump_ratio']:.1f}x the roughest step inside the file")
            else:
                first, last = float(np.max(np.abs(x2[0]))), float(np.max(np.abs(x2[-1])))
                check(first < 0.02, f"{clip}: first sample is {first:.3f}, not silence, so it starts on a step")
                check(last < 0.005, f"{clip}: last sample is {last:.3f}, not silence, which can click")
            rows.append((e["id"], os.path.basename(p), m, e))
    lines.append(f"## Sound effects\n\n{len(manifest['events'])} events, {n_files} files, all 48 kHz.\n")
    loud = [m["lufs"] + e["volumeDb"] for _, _, m, e in rows if m["lufs"] is not None and not e["loop"]]
    lines.append(f"- Highest true peak in any file: {max(m['true_peak_db'] for _, _, m, _ in rows):.1f} dB.")
    lines.append(f"- Clipped samples: {sum(1 for f in fails if 'clipped' in f)} files affected.")
    if loud:
        lines.append(f"- Loudness of one-shots as played (file loudness plus the event's volume): {min(loud):.0f} to {max(loud):.0f} LUFS.")
    lines.append("")

    # ------------------------------------------------------------ weapons
    feats = {}
    for w in FIREARMS:
        e = next((e for e in manifest["events"] if e["id"] == f"weapon.{w}.shot"), None)
        if e is None:
            check(False, f"no shot event for {w}")
            continue
        fs = [features(mixing.read(path_of(c))) for c in e["clips"]]
        feats[w] = fs
        check(len(fs) >= 2, f"{w}: only one recording, so it will machine-gun")
    names = list(feats)
    lines.append("## Weapon distinctiveness\n")
    lines.append("Each shot is reduced to twelve numbers (length, tail decay, brightness, how pitched it is, energy in eight bands). "
                 "The table is the distance between weapons; the last column is the largest distance between two recordings of the *same* weapon. "
                 "A weapon is distinct when every other weapon is further away than its own recordings are from each other.\n")
    lines.append("| | " + " | ".join(names) + " | own spread |")
    lines.append("|---|" + "---|" * (len(names) + 1))
    for a in names:
        ca = np.mean(feats[a], axis=0)
        own = max((np.linalg.norm(u - v) for u in feats[a] for v in feats[a]), default=0.0)
        cells = []
        nearest = (9e9, "")
        for b in names:
            if a == b:
                cells.append("-")
                continue
            d = float(np.linalg.norm(ca - np.mean(feats[b], axis=0)))
            cells.append(f"{d:.1f}")
            nearest = min(nearest, (d, b))
        lines.append(f"| **{a}** | " + " | ".join(cells) + f" | {own:.1f} |")
        check(nearest[0] > max(1.5, own * 1.5), f"{a} measures too close to {nearest[1]} (distance {nearest[0]:.1f}, its own recordings spread {own:.1f})")
        check(own > 0.05, f"{a}: its recordings are identical")
    lines.append("")

    # ------------------------------------------------------------ music
    lines.append("## Music\n")
    lines.append("| Piece | Tempo | Bars | Length | Stems | Full mix | True peak | Under 250 Hz | Above 2 kHz | Loop exactness |")
    lines.append("|---|---|---|---|---|---|---|---|---|---|")
    for t in manifest["music"]["tracks"]:
        stems = []
        for s in t["stems"]:
            p = path_of(s["clip"])
            if p is None:
                check(False, f"{t['id']}: missing stem {s['clip']}")
                continue
            stems.append(mixing.read(p))
        lengths = {len(s) for s in stems}
        check(len(lengths) == 1, f"{t['id']}: stems differ in length {lengths}; they would drift apart")
        full = sum(stems)
        expect = t["bars"] * t["beatsPerBar"] * 60.0 / t["bpm"] * SR
        check(abs(len(full) - expect) <= 1.0, f"{t['id']}: {len(full)} samples but its bars add up to {expect:.1f}; it would drift off the beat")
        check(any(s["lo"] <= 0 for s in t["stems"]), f"{t['id']}: silent at intensity 0")
        tp = mixing.true_peak_db(full)
        # Mastered to -1.5 dB; Ogg encoding moves peaks by up to a decibel, and the game keeps 3 dB above this.
        check(tp <= -0.3, f"{t['id']}: all stems together peak at {tp:.1f} dB")
        m = mixing.measure(full)
        low = m["sub_pct"] + m["low_pct"]
        high = 100 - m["sub_pct"] - m["low_pct"] - m["lowmid_pct"] - m.get("mid_pct", 0)
        f, pw = signal.welch(full.mean(axis=1), SR, nperseg=16384)
        low = 100 * pw[f < 250].sum() / pw.sum()
        high = 100 * pw[f >= 2000].sum() / pw.sum()
        ex = build_meas.get(f"Music/{t['id']}/_full_mix", {}).get("loop_exactness_db")
        check(ex is not None and ex < -100, f"{t['id']}: loop exactness not proven ({ex})")
        # the wrap itself, measured on the decoded Ogg the game will play
        k = 2048
        inside = np.abs(np.diff(full.mean(axis=1)))
        wrap = abs(full[0].mean() - full[-1].mean())
        check(wrap <= np.percentile(inside, 99.99) * 1.5, f"{t['id']}: the decoded file steps by {wrap:.4f} at the wrap, more than anything inside it")
        lines.append(f"| {t['title']} | {t['bpm']:.0f} | {t['bars']} | {len(full) / SR:.1f} s | {len(stems)} | {mixing.lufs(full):.1f} LUFS | {tp:.1f} dB | {low:.0f}% | {high:.0f}% | {ex} dB |")
    lines.append("")
    lines.append("Loop exactness: each piece is rendered as three joined copies and the third is compared with the second. "
                 "Below -100 dB means going round the loop is, sample for sample, the same as carrying on. Measured before Ogg encoding.\n")
    lines.append("| Cue | Length | Lead-in | Loudness | True peak |\n|---|---|---|---|---|")
    for c in manifest["music"]["cues"]:
        p = path_of(c["clip"])
        if p is None:
            check(False, f"missing cue {c['clip']}")
            continue
        x = mixing.read(p)
        tp = mixing.true_peak_db(x)
        check(tp <= -0.3, f"cue {c['id']}: true peak {tp:.1f} dB")
        lines.append(f"| {c['id']} | {len(x) / SR:.1f} s | {c['preSeconds']:.2f} s | {mixing.lufs(x):.1f} LUFS | {tp:.1f} dB |")
    lines.append("")

    # ------------------------------------------------------------ showcase
    sp = os.path.join(HERE, "build", "demo", "showcase_measurements.json")
    if os.path.exists(sp):
        s = json.load(open(sp))
        lines.append("## The five-minute showcase (offline preview of the game's own decisions)\n")
        lines.append(f"Whole run {s['lufs']} LUFS. Highest peak of everything added together, with no limiter: **{s['peak_before_limit_db']} dBFS**; samples over full scale: **{s['samples_over']}**.\n")
        check(s["samples_over"] == 0, f"the showcase mix goes over full scale on {s['samples_over']} samples")
        lines.append("| Section | Starts | Loudness | Peak | Music alone |\n|---|---|---|---|---|")
        for sec in s["sections"]:
            lines.append(f"| {sec['section'].title()} | {int(sec['start']) // 60}:{int(sec['start']) % 60:02d} | {sec['lufs']} LUFS | {sec['peak']} dB | {sec['music_lufs']} LUFS |")
        lines.append("")

    head = ["# Audio QA report", "",
            "Written by `tools/audio/qa.py`. Every number here was measured from the files on disk. **None of it is a listening result**: "
            "these checks can prove a file is clean, level, loops exactly and measures differently from its neighbours. They cannot say it sounds good.", "",
            f"**Result: {'PASS' if not fails else 'FAIL'}** ({len(fails)} failed checks, {len(warns)} warnings).", ""]
    if fails:
        head += ["## Failed"] + [f"- {f}" for f in fails] + [""]
    if warns:
        head += ["## Warnings"] + [f"- {w}" for w in warns] + [""]
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    open(REPORT, "w").write("\n".join(head + lines) + "\n")
    print("\n".join(head))
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
