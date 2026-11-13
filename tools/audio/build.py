#!/usr/bin/env python3
"""Builds Orbital Ante's audio from source.

    python3 tools/audio/build.py sfx            every sound effect
    python3 tools/audio/build.py music          every composition (stems) and transition cue
    python3 tools/audio/build.py all
    python3 tools/audio/build.py sfx --only weapon.rifle     only events whose id starts with this
    python3 tools/audio/build.py music --only combat_all_in

Outputs go to unity/Assets/OrbitalAnte/Resources/Audio/ and the game's audio manifest
(Resources/Audio/audio_manifest.json) is rewritten to match, so the game and the files cannot drift apart.
Rendering is seeded: the same source gives the same audio.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from oa_audio import mixing  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(REPO, "unity", "Assets", "OrbitalAnte", "Resources", "Audio")
MANIFEST = os.path.join(OUT, "audio_manifest.json")
SESSIONS = os.path.join(HERE, "sessions")
MEASURE = os.path.join(HERE, "build", "measurements.json")

EVENT_DEFAULTS = dict(volumeDb=0.0, volVarDb=0.0, pitchVar=0.0, spatial=1.0, minDist=2.0, maxDist=60.0, priority=128,
                      cooldownMs=0, maxVoices=4, loop=False, vacuum="air", pitchSteps=[])


def load_json(path: str, default):
    if os.path.exists(path):
        with open(path) as f:
            return json.load(f)
    return default


def save_json(path: str, data) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        json.dump(data, f, indent=1)
        f.write("\n")


def build_sfx(only: str | None) -> None:
    import sfx  # noqa: F401
    from sfx.common import REGISTRY

    manifest = load_json(MANIFEST, {"version": 1, "events": [], "music": {}})
    measures = load_json(MEASURE, {})
    events = {e["id"]: e for e in manifest.get("events", [])}
    for d in REGISTRY:
        if only and not d.event.startswith(only):
            continue
        clips = []
        t0 = time.time()
        for v in range(d.variants):
            seed = zlib.crc32(f"{d.event}#{v}".encode())
            rng = np.random.default_rng(seed)
            audio = d.fn(rng, v)
            name = d.event.replace(".", "_") + (f"_{v + 1:02d}" if d.variants > 1 else "")
            rel = f"Sfx/{d.category}/{name}"
            # Long stereo beds go out as Ogg (they would be megabytes as WAV); everything short stays lossless.
            if len(audio) > 5 * 48000:
                mixing.write_ogg(os.path.join(OUT, rel + ".ogg"), audio, 5.0)
                audio = mixing.read(os.path.join(OUT, rel + ".ogg"))
            else:
                mixing.write_wav(os.path.join(OUT, rel + ".wav"), audio, np.random.default_rng(seed + 1))
            m = mixing.measure(audio)
            if d.params.get("loop"):
                m.update(mixing.seam(audio))
            measures[rel] = m
            clips.append("Audio/" + rel)
        entry = dict(EVENT_DEFAULTS)
        entry.update(d.params)
        entry = {"id": d.event, "bus": d.bus, "clips": clips, **entry, "note": " ".join(d.note.split())}
        events[d.event] = entry
        print(f"  {d.event:32s} {d.variants} file(s)  {time.time() - t0:5.1f}s")
    manifest["events"] = [events[k] for k in sorted(events)]
    save_json(MANIFEST, manifest)
    save_json(MEASURE, measures)
    print(f"{len(manifest['events'])} events in manifest")


def build_music(only: str | None) -> None:
    import music
    manifest = load_json(MANIFEST, {"version": 1, "events": [], "music": {}})
    measures = load_json(MEASURE, {})
    section = manifest.get("music") or {}
    tracks = {t["id"]: t for t in section.get("tracks", [])}
    cues = {c["id"]: c for c in section.get("cues", [])}
    for piece in music.PIECES:
        if only and not piece.ID.startswith(only):
            continue
        t0 = time.time()
        result, loop = piece.build(os.path.join(SESSIONS, piece.ID))
        meta = dict(piece.META)
        stems_meta = []
        full = None
        for stem in meta.pop("stems"):
            audio = result[stem["id"]]
            rel = f"Music/{piece.ID}/{stem['id']}"
            mixing.write_ogg(os.path.join(OUT, rel + ".ogg"), audio)
            m = mixing.measure(audio)
            m.update(mixing.seam(audio))
            measures[rel] = m
            stems_meta.append({**stem, "clip": "Audio/" + rel})
            full = audio.copy() if full is None else full + audio
        m = mixing.measure(full)
        m.update(mixing.seam(full))
        if loop.exactness:
            m["loop_exactness_db"] = round(max(loop.exactness), 1)
            print(f"    loop exactness (pass 3 vs pass 2, worst stem): {m['loop_exactness_db']} dB")
        elif f"Music/{piece.ID}/_full_mix" in measures and "loop_exactness_db" in measures[f"Music/{piece.ID}/_full_mix"]:
            m["loop_exactness_db"] = measures[f"Music/{piece.ID}/_full_mix"]["loop_exactness_db"]
        measures[f"Music/{piece.ID}/_full_mix"] = m
        tracks[piece.ID] = {"id": piece.ID, **meta, "samples": int(len(full)), "seconds": round(len(full) / 48000.0, 6),
                            "stems": stems_meta}
        print(f"  {piece.ID:28s} {len(stems_meta)} stems  {len(full) / 48000.0:6.1f}s  full mix {m['lufs']} LUFS, "
              f"true peak {m['true_peak_db']} dB  ({time.time() - t0:.0f}s)")
    if not only or only.startswith("cue"):
        for cue in music.cues.build_all():
            rel = f"Music/cues/{cue['id']}"
            mixing.write_ogg(os.path.join(OUT, rel + ".ogg"), cue.pop("audio"))
            cue["clip"] = "Audio/" + rel
            cues[cue["id"]] = cue
            measures[rel] = mixing.measure(mixing.read(os.path.join(OUT, rel + ".ogg")))
            print(f"  cue {cue['id']}")
    section["tracks"] = [tracks[k] for k in sorted(tracks)]
    section["cues"] = [cues[k] for k in sorted(cues)]
    section.update(music.DIRECTION)
    manifest["music"] = section
    save_json(MANIFEST, manifest)
    save_json(MEASURE, measures)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("what", choices=["sfx", "music", "all"])
    ap.add_argument("--only", default=None)
    args = ap.parse_args()
    if args.what in ("sfx", "all"):
        build_sfx(args.only)
    if args.what in ("music", "all"):
        build_music(args.only)


if __name__ == "__main__":
    main()
