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

    # ------------------------------------------------------------ creature warnings against the clock's wood
    # A creature's warning must never be taken for the ante clock (game-designer review, Ralph loop pass 2): no mark
    # rings within two semitones of the clock's tick (979 and 1629 Hz) and no wind-up knocks ring near its warning's
    # 640 Hz. Each pair here is a creature's wind-up and its aim (keep in step with Creatures/Windups.cs; the rule test
    # EveryCreature_HasItsOwnWarningAndAimSounds holds that list to the manifest).
    windups = {"strider": "raise", "sprinter": "raise", "roller": "curl", "hunter": "rear", "maw": "gape",
               "biter": "mark", "screamer": "windup", "ambusher": "coil", "stalker": "gape"}
    by_id = {e["id"]: e for e in manifest["events"]}
    hp200 = signal.butter(2, 200, "high", fs=SR, output="sos")

    def mono(clip):
        x = mixing.read(path_of(clip))
        return x.mean(axis=1) if x.ndim == 2 else x

    def loudest(x, count=1, apart=0.05):
        """Starts of the loudest 5 ms windows, at least `apart` seconds from each other, and the loudest one's level."""
        w = int(0.005 * SR)
        p = np.convolve(x * x, np.ones(w) / w, "valid")
        found, q = [], p.copy()
        for _ in range(count):
            i = int(np.argmax(q))
            if q[i] <= 0:
                break
            found.append(i / SR)
            q[max(0, i - int(apart * SR)):i + int(apart * SR)] = 0
        return found, 10 * np.log10(np.max(p) + 1e-20)

    def ring_over(x, at, f0):
        """How far the band two semitones either side of f0 stands over the bands four to seven semitones off, in dB,
        over 30 ms from `at`: something ringing at f0 stands 20 dB or more over them (the clock's own tick, 37 to 55);
        noise and other pitches stand near 0."""
        k = max(0, int(at * SR))
        seg = x[k:k + int(0.03 * SR)]
        if len(seg) < 64:
            return -99.0
        spec = np.abs(np.fft.rfft(seg * np.hanning(len(seg)), 1 << 15)) ** 2
        fr = np.fft.rfftfreq(1 << 15, 1 / SR)

        def band(lo, hi):
            m = (fr >= f0 * 2 ** (lo / 12)) & (fr <= f0 * 2 ** (hi / 12))
            return spec[m].mean() + 1e-20

        side = (band(-7, -4) + band(4, 7)) / 2
        return float(10 * np.log10(band(-2, 2) / side))

    lines.append("## Creature warnings and the clock\n")
    lines.append("Each creature warns with two sounds: its wind-up, then its aim (one hard mark, played when it takes aim). "
                 "*Ring* is how far a sound stands, in dB, over its neighbouring pitches two semitones either side of the clock's "
                 "tick (979 and 1629 Hz) or of its warning's knocks (640 Hz); the clock's own tick stands 37 to 55 dB over. "
                 "Allowed: under 10. *Mark over wind-up* compares the loudest 5 ms of each above 200 Hz, as played (at least 5 dB). "
                 "*Wind-up heard* is the wind-up's loudest 100 ms above 200 Hz, so a quiet one is not lost on the Deck's speakers "
                 "(at least -35 dB).\n")
    lines.append("| Creature | Aim's mark at | Mark rings at 979 / 1629 Hz | Wind-up rings at 979 / 1629 / 640 Hz | Mark over wind-up | Wind-up heard |")
    lines.append("|---|---|---|---|---|---|")
    for kind, warn in windups.items():
        w, a = by_id.get(f"creature.{kind}.{warn}"), by_id.get(f"creature.{kind}.aim")
        if w is None or a is None:
            check(False, f"creature {kind}: no wind-up or no aim sound")
            continue
        aim, wind = signal.sosfilt(hp200, mono(a["clips"][0])), signal.sosfilt(hp200, mono(w["clips"][0]))
        (mark,), mark_db = loudest(aim)
        peaks, wind_db = loudest(wind, 3)
        a979, a1629 = (ring_over(aim, mark - 0.004, f) for f in (979.0, 1629.0))
        w_rings = [max(ring_over(wind, at - 0.002, f) for at in peaks) for f in (979.0, 1629.0, 640.0)]
        over = (mark_db + a["volumeDb"]) - (wind_db + w["volumeDb"])
        k = int(0.1 * SR)
        heard = 10 * np.log10(np.max(np.convolve(wind ** 2, np.ones(k) / k, "valid")) + 1e-20)
        check(mark <= 0.045, f"creature.{kind}.aim: its loudest moment is {mark * 1000:.0f} ms in, not its mark at the front")
        check(max(a979, a1629) < 10, f"creature.{kind}.aim: its mark rings by the clock's tick ({a979:.0f} / {a1629:.0f} dB)")
        check(max(w_rings) < 10, f"creature.{kind}.{warn}: its wind-up rings like the clock's wood ({' / '.join(f'{r:.0f}' for r in w_rings)} dB)")
        check(over >= 5, f"creature.{kind}.aim: its mark stands only {over:.1f} dB over its wind-up")
        check(heard >= -35, f"creature.{kind}.{warn}: its loudest 100 ms is {heard:.1f} dB, too quiet to be heard")
        lines.append(f"| {kind} | {mark * 1000:.0f} ms | {a979:.0f} / {a1629:.0f} | {' / '.join(f'{r:.0f}' for r in w_rings)} | {over:.1f} dB | {heard:.1f} dB |")
    lines.append("")

    # ------------------------------------------------------------ the player's own glances and knocks against the marks
    # A shot that a creature's armour turns plays the glance (in the player's head, every shot) and the creature's knock
    # (impact.armor, where it landed), while the creatures that face the player armour-first wind up; the one hard mark
    # that says "move" must never be buried by them or taken for one (game-designer review, Ralph loop pass 7). So both
    # are soft-fronted and dull where the bright marks are hard, the glance is quieter than every mark as played, and
    # each measures far from every mark. The knock is measured at every pitch the creatures play it at (keep in step
    # with their impact.armor calls: Walker, Roller, Maw, Hunter, Screamer, Stalker, Ambusher).
    knock_pitches = (0.45, 0.55, 0.6, 0.7, 0.8, 0.85, 0.9, 1.0)

    def front(x):
        """How hard its front is: its level 1 ms after it starts (reaches 5 per cent of its loudest), as a share of its
        loudest moment in the first 60 ms, on a 0.5 ms envelope. The bright marks are at 0.6 to 1, a soft rise near 0.1."""
        w = max(1, int(0.0005 * SR))
        e = np.sqrt(np.convolve(x * x, np.ones(w) / w, "same"))
        pk = int(np.argmax(e[:int(0.06 * SR)]))
        start = int(np.argmax(e[:pk + 1] >= 0.05 * e[pk]))
        return float(e[min(pk, start + int(0.001 * SR))] / (e[pk] + 1e-20))

    def early_centroid(x):
        seg = x[:int(0.045 * SR)]
        f, pw = signal.welch(seg, SR, nperseg=min(1024, len(seg)))
        return float((f * pw).sum() / (pw.sum() + 1e-20))

    def at_pitch(x, p):
        return x if abs(p - 1.0) < 1e-6 else signal.resample(x, int(round(len(x) / p)))

    marks = {}
    for kind in windups:
        a = by_id.get(f"creature.{kind}.aim")
        if a is not None:
            x = signal.sosfilt(hp200, mono(a["clips"][0]))
            marks[kind] = (x, loudest(x)[1] + a["volumeDb"], features(x))
    lines.append("## The glance and the armour knock against the marks\n")
    lines.append("A shot that a creature's armour turns plays the glance and the creature's knock, while the creature may be about "
                 "to give its one hard mark. *Front* is its level 1 ms after it starts as a share of its loudest moment, above 200 Hz "
                 "(the bright marks are at 0.6 to 1); allowed for the glance, which is in the player's head on every shot: at most "
                 "0.35 (the knock keeps a knock's front). *Brightness* is the centre of its first 45 ms; allowed: under 2 kHz (the "
                 "bright marks are at 2.4 to 4.8 kHz). *Under the quietest mark* compares the loudest 5 ms above 200 Hz as played (the glance only; the knock plays "
                 "where the mark does); allowed: at least 6 dB. *Nearest mark* is the gunshots' measure of difference to the closest "
                 "mark (two sounds closer than 1.5 are told apart badly); allowed: at least 3. *Rings*: as for the wind-ups, under 10.\n")
    lines.append("| Sound | Front | Brightness | Under the quietest mark | Nearest mark | Rings at 979 / 1629 / 640 Hz |")
    lines.append("|---|---|---|---|---|---|")
    g = by_id.get("hit.glance")
    k = by_id.get("impact.armor")
    check(g is not None and k is not None, "no hit.glance or impact.armor event")
    # (name, the file's sound above 200 Hz, its volume, the pitches it is played at, in the player's head)
    played = []
    if g is not None:
        played.append(("hit.glance", signal.sosfilt(hp200, mono(g["clips"][0])), g["volumeDb"], (1.0,), True))
    if k is not None:
        # Each creature's pitch, and as far as the event's own pitch spread takes it either way.
        spread = 2.0 ** (float(k.get("pitchVar", 0.0)) / 12.0)
        pitches = sorted({round(p * f, 4) for p in knock_pitches for f in (1.0 / spread, 1.0, spread)})
        for i, clip in enumerate(k["clips"]):
            played.append((f"impact.armor {i + 1} (worst of {pitches[0]:.2f} to {pitches[-1]:.2f})",
                           signal.sosfilt(hp200, mono(clip)), k["volumeDb"], tuple(pitches), False))
    for name, base, vol, pitches, own in played:
        fr = br = -1.0
        nearest, rings = (99.0, ""), [-99.0, -99.0, -99.0]
        for p in pitches:
            x = base if p == 1.0 else signal.sosfilt(hp200, at_pitch(base, p))
            fr, br = max(fr, front(x)), max(br, early_centroid(x))
            nearest = min(nearest, min((float(np.linalg.norm(features(x) - fm)), kind) for kind, (_, _, fm) in marks.items()))
            rings = [max(r, max(ring_over(x, at - 0.002, f0) for at in loudest(x, 3)[0])) for r, f0 in zip(rings, (979.0, 1629.0, 640.0))]
        under = min(mdb for _, mdb, _ in marks.values()) - (loudest(base)[1] + vol) if own else None
        check(br < 2000.0, f"{name}: its first 45 ms centre at {br:.0f} Hz, as bright as a creature's mark")
        check(nearest[0] >= 3.0, f"{name}: measures {nearest[0]:.1f} from creature.{nearest[1]}.aim")
        check(max(rings) < 10, f"{name}: rings like the clock's wood ({' / '.join(f'{r:.0f}' for r in rings)} dB)")
        if own:
            # The glance is in the player's head on every shot, so its front must be soft as well; the knock is the
            # shell where the shot landed, and keeps a knock's front.
            check(fr <= 0.35, f"{name}: {fr:.2f} of its loudest within 1 ms of starting, as hard as a creature's mark")
            check(under >= 6.0, f"{name}: only {under:.1f} dB under the quietest mark as played")
        lines.append(f"| {name} | {fr:.2f} | {br:.0f} Hz | {'' if under is None else f'{under:.1f} dB'} | "
                     f"{nearest[0]:.1f} ({nearest[1]}) | {' / '.join(f'{r:.0f}' for r in rings)} |")
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
