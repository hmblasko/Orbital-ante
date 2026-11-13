#!/usr/bin/env python3
"""Orbital Ante: hand bounded refinement tasks to ChatGPT and bring its reply back safely.

Claude cannot call ChatGPT, so Matthew carries a packet over and the reply back.

  pack    Build one self-contained packet (task, files or diff, pictures, exact reply format).
  intake  Check ChatGPT's reply against its packet. Findings become unverified leads; edits are
          dry-run by default and only written with --apply. Nothing is ever committed.

Plain Python 3.9+, standard library only. Run from anywhere inside the repository:

  python3 tools/chatgpt/oa_handoff.py pack --task review-code --title "Hatch timing" \
      --ask "Look for logic bugs" --diff main...HEAD
  python3 tools/chatgpt/oa_handoff.py intake handoff/H-20261112-hatch-timing-1a2b reply.md
  python3 tools/chatgpt/oa_handoff.py intake handoff/H-20261112-hatch-timing-1a2b reply.md --apply

Full procedure: docs/chatgpt-handoff.md.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import fnmatch
import hashlib
import json
import os
import re
import secrets
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

TASKS = ("review-code", "second-opinion", "small-edit", "docs")
EDIT_TASKS = ("small-edit", "docs")
SEVERITIES = ("P0", "P1", "P2", "P3")
# ChatGPT never ran the game or the tests, so it can only raise leads. "verified_defect" is
# reserved for the Critic and is downgraded on intake.
KINDS = ("suspected_defect", "aesthetic_judgment", "verification_gap", "design_question")
CATEGORIES = ("cohesion", "modeling", "environment", "materials", "animation", "readability",
              "polish", "technical", "code", "tests", "docs")
RUBRIC = (("cohesion", 20), ("modeling", 20), ("environment", 15), ("materials", 15),
          ("animation", 15), ("readability", 10), ("polish", 5))

# Paths ChatGPT may never edit, whatever a packet allows. Fable's work, the quality gate's fixed
# parts, Unity's serialized files and the standing instructions.
DENY = (
    "CLAUDE.md", ".claude/*", ".github/*", ".gitignore", ".gitattributes",
    "Verification/Config/*", "Verification/Baselines/*", "Verification/History/*", "Verification/Reports/*",
    "unity/Assets/OrbitalAnte/Verification/Rules/Rubric.cs",
    "unity/ProjectSettings/*", "unity/Packages/*",
    "*.meta", "*.unity", "*.prefab", "*.asset", "*.mat", "*.controller", "*.anim", "*.bytes",
    "*.fbx", "*.blend", "*.png", "*.jpg", "*.jpeg", "*.wav", "*.ogg", "*.dll",
    "tools/chatgpt/*",
)

SOFT_LIMIT = 60_000   # characters; above this ChatGPT starts to skim
HARD_LIMIT = 150_000

GAME_BLURB = (
    "Orbital Ante is a first-person space roguelite in Unity 6 LTS (URP, C#, Input System), "
    "Steam Deck first (1280x800). Art direction: stylised realism with strong cohesion, NASA-inspired "
    "engineering, clean characterful shapes; never boxy, never deliberately retro. Code rules: game "
    "rules in plain C# with no UnityEngine dependency where possible, tuning numbers in config assets, "
    "seeded randomness, Unity 6 API names (e.g. Rigidbody.linearVelocity, PhysicsMaterial). "
    "Factions are only ever called Faction A, Faction B and Faction C."
)

STANDING_RULES = """\
- You are a second-opinion reviewer. You cannot run Unity, the game or the tests, so nothing you say is verified. Claude checks every point and runs the tests before anything is merged.
- Work only from what is in this packet. If you need a file that is not here, say so under "questions" rather than guessing its contents.
- Never invent test results, measurements, frame times, playtest reactions, or faction names.
- Be specific and brief. Prefer three real problems to ten vague ones. If you find nothing worth raising, return an empty findings list and say so.
- Reply with exactly the format under "Reply format" and nothing else: no greeting, no recap."""


# ----------------------------------------------------------------------------- helpers

def repo_root(start: Path | None = None) -> Path:
    here = (start or Path.cwd()).resolve()
    try:
        out = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=here, capture_output=True, text=True)
        if out.returncode == 0 and out.stdout.strip():
            return Path(out.stdout.strip())
    except FileNotFoundError:
        pass
    return Path(__file__).resolve().parents[2]


def git(root: Path, *args: str) -> str:
    out = subprocess.run(["git", *args], cwd=root, capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit(f"git {' '.join(args)} failed: {out.stderr.strip()}")
    return out.stdout


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rel(root: Path, p: str | Path) -> str:
    path = Path(p)
    if not path.is_absolute():
        path = (Path.cwd() / path)
    path = path.resolve()
    try:
        return path.relative_to(root.resolve()).as_posix()
    except ValueError:
        raise SystemExit(f"{p} is outside the repository")


def denied(path: str) -> bool:
    return any(fnmatch.fnmatchcase(path, pat) for pat in DENY)


def slug(text: str) -> str:
    s = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")
    return (s[:32].rstrip("-")) or "task"


def fence_for(text: str) -> str:
    longest = max((len(m) for m in re.findall(r"`{3,}", text)), default=0)
    return "`" * max(3, longest + 1)


def lang_for(path: str) -> str:
    return {".cs": "csharp", ".py": "python", ".json": "json", ".md": "markdown", ".ps1": "powershell",
            ".sh": "bash", ".shader": "hlsl", ".hlsl": "hlsl", ".uss": "css", ".uxml": "xml"}.get(Path(path).suffix.lower(), "")


def parse_file_arg(arg: str) -> tuple[str, int | None, int | None]:
    m = re.match(r"^(.*?):(\d+)-(\d+)$", arg)
    if m:
        return m.group(1), int(m.group(2)), int(m.group(3))
    return arg, None, None


# ----------------------------------------------------------------------------- pack

def reply_format(task: str, packet_id: str) -> str:
    finding = (
        '    {\n'
        '      "id": "GPT-001",\n'
        '      "file": "unity/Assets/... (or omit for a picture)",\n'
        '      "line": 42,\n'
        '      "target": "picture file name or feature, if not a file",\n'
        f'      "category": "one of: {", ".join(CATEGORIES)}",\n'
        '      "severity": "P0 | P1 | P2 | P3",\n'
        f'      "kind": "one of: {", ".join(KINDS)}",\n'
        '      "title": "short",\n'
        '      "description": "what is wrong and why it matters",\n'
        '      "evidence": ["quote the exact code line, or name the picture and where in it"],\n'
        '      "recommendedFixes": ["one or more options"],\n'
        '      "acceptanceCriteria": ["how Claude can tell it is fixed"],\n'
        '      "confidence": 0.6\n'
        '    }'
    )
    envelope = (
        "```json\n{\n"
        f'  "packet": "{packet_id}",\n'
        f'  "task": "{task}",\n'
        '  "summary": "two or three sentences",\n'
        '  "findings": [\n' + finding + '\n  ],\n'
        + ('  "advisoryScores": { "cohesion": 6, "modeling": 5 },\n' if task == "second-opinion" else "")
        + '  "questions": ["anything you needed but did not have"],\n'
        '  "notChecked": ["what you could not judge from this packet"]\n'
        "}\n```"
    )
    text = ["Reply with this JSON block first (valid JSON: double quotes, no comments, no trailing commas):", "", envelope, ""]
    if task in EDIT_TASKS:
        text += [
            "Then put every change in ONE block fenced as ```edits, using search/replace pairs. "
            "SEARCH must be copied exactly from the file in this packet (same spaces, same line breaks) and must "
            "appear only once in that file; include a few surrounding lines to make it unique. "
            "Only edit files listed under \"Files you may edit\". Do not use unified diffs. Do not create new files "
            "unless the task asks for one; for a new file use an empty SEARCH.",
            "",
            "```edits",
            "FILE: path/from/the/repo/root.cs",
            "<<<<<<< SEARCH",
            "exact old lines",
            "=======",
            "new lines",
            ">>>>>>> REPLACE",
            "```",
            "",
            "In the JSON, use findings for anything you noticed but did not change.",
        ]
    else:
        text += ["Do not include an edits block for this task. Put suggested code in recommendedFixes as short snippets."]
    return "\n".join(text)


def task_intro(task: str) -> str:
    if task == "review-code":
        return ("Review the code below for correctness bugs, edge cases, Unity 6 API mistakes, and missing tests. "
                "Do not comment on style unless it hides a bug. Use kind suspected_defect for bugs, "
                "design_question for anything that is a game-design choice rather than a bug.")
    if task == "second-opinion":
        rubric = ", ".join(f"{c} {w}" for c, w in RUBRIC)
        return ("Give a second opinion on the attached pictures from Orbital Ante. They were captured from the "
                "Unity project by its capture run; the manifest notes below say which camera and state each one shows. "
                "Look at every picture. Judge only what you can see. Use the fixed rubric: categories and weights "
                f"{rubric}. Anchors: 5 works but has obvious weaknesses; 7 competent ordinary indie work; 8.5 "
                "coherent, attractive, readable and polished enough for a strong commercial indie game; 9.5 "
                "exceptional. Placeholder primitives and programmer art score 3 to 6 however clean. Scores you give "
                "are advisory only and never count toward the quality gate. Use kind aesthetic_judgment for looks, "
                "suspected_defect for something that looks broken (clipping, missing texture, wrong scale).")
    if task == "small-edit":
        return ("Make the small change described under Ask, touching only the files you may edit. Keep the change "
                "as small as possible, keep existing behaviour that the ask does not mention, and follow the code "
                "rules in Context. If the change would need a file that is not here, or a design decision, do not "
                "guess: raise it under questions and make no edit.")
    return ("Improve the text described under Ask. Plain, short sentences; numbered steps for anything Matthew does "
            "by hand (Matthew is still learning Unity); keep every fact and command exactly as it is unless the "
            "ask says to change it.")


def cmd_pack(a: argparse.Namespace) -> int:
    root = repo_root()
    if a.task in EDIT_TASKS and not a.allow_edit:
        raise SystemExit(f"--task {a.task} needs at least one --allow-edit path")
    if a.task == "second-opinion" and not a.image:
        raise SystemExit("--task second-opinion needs at least one --image")
    if not (a.file or a.diff or a.image or a.allow_edit):
        raise SystemExit("give at least one --file, --diff, --image or --allow-edit")

    today = _dt.date.today().strftime("%Y%m%d")
    packet_id = f"H-{today}-{slug(a.title)}-{secrets.token_hex(2)}"
    out_root = Path(a.out).resolve() if a.out else root / "handoff"
    pdir = out_root / packet_id
    pdir.mkdir(parents=True, exist_ok=False)

    commit = git(root, "rev-parse", "HEAD").strip()
    dirty = bool(git(root, "status", "--porcelain").strip())

    allow = []
    for p in a.allow_edit or []:
        r = rel(root, p)
        if denied(r):
            raise SystemExit(f"{r} is on the never-edit list for ChatGPT (see DENY in {Path(__file__).name})")
        allow.append(r)

    # Files to show: every --file plus every editable file (edits need the full current text).
    shown: list[tuple[str, int | None, int | None]] = []
    seen = set()
    for f in (a.file or []):
        p, s, e = parse_file_arg(f)
        r = rel(root, p)
        if (r, s, e) not in seen:
            shown.append((r, s, e)); seen.add((r, s, e))
    for r in allow:
        if (r, None, None) not in seen:
            shown = [x for x in shown if x[0] != r]  # the whole file replaces any excerpt
            shown.append((r, None, None)); seen.add((r, None, None))

    hashes = {}
    file_sections = []
    for r, s, e in shown:
        path = root / r
        if not path.exists():
            if r in allow:
                file_sections.append(f"### {r}\n\n(This file does not exist yet. Create it with an empty SEARCH.)\n")
                hashes[r] = None
                continue
            raise SystemExit(f"{r} does not exist")
        text = path.read_text(encoding="utf-8", errors="replace")
        hashes[r] = sha256(path)
        lines = text.splitlines()
        numbered = a.task in ("review-code",) and r not in allow
        if s is not None:
            lines_sel = lines[s - 1:e]
            header = f"### {r} (lines {s}-{min(e, len(lines))} of {len(lines)})"
            start = s
        else:
            lines_sel = lines
            header = f"### {r} ({len(lines)} lines{', editable' if r in allow else ''})"
            start = 1
        body = "\n".join(f"{i:>5}| {ln}" for i, ln in enumerate(lines_sel, start)) if numbered else "\n".join(lines_sel)
        fence = fence_for(body)
        file_sections.append(f"{header}\n\n{fence}{lang_for(r)}\n{body}\n{fence}\n")

    diff_text = ""
    if a.diff:
        diff_text = git(root, "diff", "--no-color", "--stat", a.diff) + "\n" + git(root, "diff", "--no-color", "-U5", a.diff)
        if not diff_text.strip():
            raise SystemExit(f"git diff {a.diff} is empty")

    images = []
    if a.image:
        (pdir / "attach").mkdir()
        for i, img in enumerate(a.image, 1):
            src = Path(img).resolve()
            if not src.exists():
                raise SystemExit(f"{img} does not exist")
            name = f"{i:02d}-{src.name}"
            shutil.copy2(src, pdir / "attach" / name)
            try:
                source = src.relative_to(root.resolve()).as_posix()
            except ValueError:
                source = str(src)
            images.append({"name": name, "source": source})

    parts = [
        f"# Orbital Ante review packet {packet_id}",
        "",
        f"Task type: **{a.task}**. Repository commit {commit[:10]}{' plus uncommitted changes' if dirty else ''}.",
        "",
        "## Rules",
        STANDING_RULES,
        "",
        "## Context",
        GAME_BLURB,
    ]
    if a.context:
        parts += ["", a.context.strip()]
    parts += ["", "## What to do", task_intro(a.task), "", "## Ask", a.ask.strip(), ""]
    if allow:
        parts += ["## Files you may edit", *[f"- {r}" for r in allow], ""]
    if images:
        parts += ["## Pictures (attached to this message)",
                  *[f"- {im['name']} (from {im['source']})" for im in images], ""]
        if a.notes:
            parts += [Path(a.notes).read_text(encoding="utf-8").strip(), ""]
    if diff_text:
        fence = fence_for(diff_text)
        parts += [f"## Diff ({a.diff})", "", f"{fence}diff\n{diff_text.rstrip()}\n{fence}", ""]
    if file_sections:
        parts += ["## Files", "", *file_sections]
    parts += ["## Reply format", reply_format(a.task, packet_id), ""]
    packet = "\n".join(parts)

    size = len(packet)
    if size > HARD_LIMIT and not a.force:
        shutil.rmtree(pdir)
        raise SystemExit(f"packet is {size:,} characters (limit {HARD_LIMIT:,}). Narrow it with file:start-end "
                         "ranges or a smaller diff, or pass --force.")
    (pdir / "packet.md").write_text(packet, encoding="utf-8")
    meta = {"id": packet_id, "task": a.task, "title": a.title, "commit": commit, "dirty": dirty,
            "created": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"),
            "allowEdit": allow, "fileHashes": hashes, "images": images, "diff": a.diff, "characters": size}
    (pdir / "packet.json").write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8")

    print(f"Packet {packet_id}")
    print(f"  {pdir / 'packet.md'}  ({size:,} characters, about {size // 4:,} tokens)")
    if images:
        print(f"  attach {len(images)} picture(s) from {pdir / 'attach'}")
    if size > SOFT_LIMIT:
        print(f"  warning: over {SOFT_LIMIT:,} characters; ChatGPT may skim. Consider narrowing it.")
    return 0


# ----------------------------------------------------------------------------- intake

FENCE_RE = re.compile(r"^(?P<fence>`{3,}|~{3,})[ \t]*(?P<lang>[\w+-]*)[^\n]*\n(?P<body>.*?)^(?P=fence)[ \t]*$",
                      re.M | re.S)
EDIT_RE = re.compile(r"^FILE:[ \t]*(?P<file>\S.*?)[ \t]*\n<{7} SEARCH[ \t]*\n(?P<search>.*?)^={7}[ \t]*\n"
                     r"(?P<replace>.*?)^>{7} REPLACE[ \t]*$", re.M | re.S)


def extract_blocks(text: str) -> list[tuple[str, str]]:
    text = text.replace("\r\n", "\n")
    return [(m.group("lang").lower(), m.group("body")) for m in FENCE_RE.finditer(text)]


def find_envelope(text: str, blocks: list[tuple[str, str]]):
    for lang, body in blocks:
        if lang in ("json", ""):
            try:
                obj = json.loads(body)
            except json.JSONDecodeError:
                continue
            if isinstance(obj, dict) and "packet" in obj:
                return obj, None
    # ChatGPT sometimes drops the fence: try the first {...} in the text.
    start = text.find("{")
    if start >= 0:
        try:
            obj, _ = json.JSONDecoder().raw_decode(text[start:])
            if isinstance(obj, dict) and "packet" in obj:
                return obj, "the JSON was not in a ```json fence (accepted anyway)"
        except json.JSONDecodeError as e:
            return None, f"no valid JSON envelope found ({e.msg} at line {e.lineno})"
    return None, "no JSON envelope found"


def check_envelope(env: dict, meta: dict) -> tuple[list[str], list[str], list[dict]]:
    errors, notes, findings = [], [], []
    if env.get("packet") != meta["id"]:
        errors.append(f"reply is for packet {env.get('packet')!r}, not {meta['id']}")
    if env.get("task") != meta["task"]:
        errors.append(f"reply task {env.get('task')!r} does not match packet task {meta['task']!r}")
    if not isinstance(env.get("summary"), str) or not env["summary"].strip():
        errors.append("summary is missing")
    raw = env.get("findings")
    if not isinstance(raw, list):
        errors.append("findings must be a list (it may be empty)")
        raw = []
    ids = set()
    for i, f in enumerate(raw):
        where = f"findings[{i}]"
        if not isinstance(f, dict):
            errors.append(f"{where} is not an object"); continue
        fid = f.get("id") or f"GPT-{i + 1:03d}"
        if fid in ids:
            errors.append(f"{where}: duplicate id {fid}")
        ids.add(fid)
        for key in ("title", "description"):
            if not isinstance(f.get(key), str) or not f[key].strip():
                errors.append(f"{fid}: {key} is missing")
        for key in ("evidence", "recommendedFixes"):
            v = f.get(key)
            if isinstance(v, str):
                f[key] = [v]
            elif not (isinstance(v, list) and v and all(isinstance(x, str) for x in v)):
                errors.append(f"{fid}: {key} must be a non-empty list of strings")
        if f.get("severity") not in SEVERITIES:
            errors.append(f"{fid}: severity must be one of {', '.join(SEVERITIES)}")
        kind = f.get("kind")
        if kind == "verified_defect":
            f["kind"] = "suspected_defect"
            notes.append(f"{fid}: kind verified_defect downgraded to suspected_defect (ChatGPT cannot verify)")
        elif kind not in KINDS:
            errors.append(f"{fid}: kind must be one of {', '.join(KINDS)}")
        if f.get("category") not in CATEGORIES:
            notes.append(f"{fid}: unknown category {f.get('category')!r}, kept as given")
        c = f.get("confidence")
        if not isinstance(c, (int, float)) or not 0 <= c <= 1:
            errors.append(f"{fid}: confidence must be a number from 0 to 1")
        if "line" in f and f["line"] is not None and not isinstance(f["line"], int):
            notes.append(f"{fid}: line {f['line']!r} is not a whole number, ignored"); f["line"] = None
        file = f.get("file")
        if file and file not in meta["fileHashes"] and not (meta.get("diff")):
            notes.append(f"{fid}: refers to {file}, which was not in the packet")
        f["id"] = fid
        f["source"] = "chatgpt"
        f["verified"] = False
        findings.append(f)
    if "notChecked" not in env:
        notes.append("notChecked is missing")
    scores = env.get("advisoryScores")
    if scores is not None:
        if meta["task"] != "second-opinion":
            notes.append("advisoryScores ignored: only second-opinion packets may carry them")
        elif not isinstance(scores, dict) or not all(
                k in dict(RUBRIC) and isinstance(v, (int, float)) and 0 <= v <= 10 for k, v in scores.items()):
            errors.append("advisoryScores must map rubric category ids to numbers from 0 to 10")
    return errors, notes, findings


def parse_edits(blocks: list[tuple[str, str]], text: str) -> tuple[list[dict], list[str]]:
    errors = []
    edit_bodies = [b for lang, b in blocks if lang == "edits"]
    if not edit_bodies and "<<<<<<< SEARCH" in text:
        edit_bodies = [text.replace("\r\n", "\n")]  # fence lost on the way: scan the whole reply
    edits = []
    for body in edit_bodies:
        found = list(EDIT_RE.finditer(body))
        if body.count("<<<<<<< SEARCH") != len(found):
            errors.append("an edit block is malformed (each needs FILE:, <<<<<<< SEARCH, =======, >>>>>>> REPLACE)")
        for m in found:
            edits.append({"file": m.group("file").strip().strip("`"), "search": m.group("search"),
                          "replace": m.group("replace")})
    if any(lang == "diff" for lang, _ in blocks):
        errors.append("reply contains a unified diff; the packet asked for search/replace edits. Ask ChatGPT to "
                      "resend the changes as an edits block")
    return edits, errors


def plan_edits(root: Path, meta: dict, edits: list[dict]) -> tuple[dict[str, str], list[str], list[str]]:
    """Return {path: new text} if every edit applies cleanly, plus errors and notes."""
    errors, notes = [], []
    allow = set(meta["allowEdit"])
    new_text: dict[str, str] = {}
    for n, e in enumerate(edits, 1):
        f = e["file"]
        tag = f"edit {n} ({f})"
        if f.startswith("/") or ".." in Path(f).parts:
            errors.append(f"{tag}: path must be relative to the repository root"); continue
        if denied(f):
            errors.append(f"{tag}: on the never-edit list"); continue
        if f not in allow:
            errors.append(f"{tag}: not one of the files this packet allowed ({', '.join(sorted(allow))})"); continue
        path = root / f
        if f not in new_text:
            if path.exists():
                cur = path.read_bytes()
                if meta["fileHashes"].get(f) and hashlib.sha256(cur).hexdigest() != meta["fileHashes"][f]:
                    notes.append(f"{f} changed since the packet was made; edits are matched against the current file")
                new_text[f] = cur.decode("utf-8").replace("\r\n", "\n")
            else:
                new_text[f] = None  # type: ignore[assignment]
        cur = new_text[f]
        search, replace = e["search"], e["replace"]
        if cur is None:
            if search.strip():
                errors.append(f"{tag}: file does not exist, so SEARCH must be empty"); continue
            new_text[f] = replace; continue
        if not search.strip():
            errors.append(f"{tag}: empty SEARCH on an existing file"); continue
        count = cur.count(search)
        if count == 0:
            # Tolerate trailing-space differences only; anything else is a real mismatch.
            pat = re.compile("\n".join(re.escape(ln.rstrip()) + r"[ \t]*" for ln in search.split("\n")))
            hits = list(pat.finditer(cur))
            if len(hits) == 1:
                cur = cur[:hits[0].start()] + replace + cur[hits[0].end():]
                notes.append(f"{tag}: matched after ignoring trailing spaces")
                new_text[f] = cur; continue
            errors.append(f"{tag}: SEARCH text not found in the file"); continue
        if count > 1:
            errors.append(f"{tag}: SEARCH text appears {count} times; it must be unique"); continue
        new_text[f] = cur.replace(search, replace, 1)
    return new_text, errors, notes


def write_intake(pdir: Path, meta, env, findings, errors, notes, changed, applied, check_out) -> Path:
    status = "REJECTED" if errors else ("APPLIED (uncommitted)" if applied else "ACCEPTED (dry run)")
    lines = [f"# ChatGPT reply intake for {meta['id']}", "",
             f"Status: **{status}**. Task: {meta['task']}. Packet made at commit {meta['commit'][:10]}.", "",
             "Nothing here is verified. Each finding is a lead for Claude to confirm against the code or the "
             "real captures; each edit must pass review and the tests before it is committed.", ""]
    if errors:
        lines += ["## Problems (send these back to ChatGPT, or fix by hand)", *[f"- {e}" for e in errors], ""]
    if notes:
        lines += ["## Notes", *[f"- {n}" for n in notes], ""]
    if env:
        lines += ["## Summary from ChatGPT", str(env.get("summary", "")).strip(), ""]
    if findings:
        lines += ["## Findings (unverified)", "",
                  "| ID | Sev | Kind | Where | Title | Conf. | Claude's check |", "|---|---|---|---|---|---|---|"]
        for f in sorted(findings, key=lambda x: (str(x.get("severity")), -float(x.get("confidence") or 0))):
            where = f.get("file") or f.get("target") or ""
            if f.get("line"):
                where += f":{f['line']}"
            title = str(f.get("title", "")).replace("|", "/")
            lines.append(f"| {f['id']} | {f.get('severity')} | {f.get('kind')} | {where} | {title} | "
                         f"{f.get('confidence')} | to do |")
        lines.append("")
    if env and env.get("advisoryScores") and meta["task"] == "second-opinion":
        lines += ["## Advisory scores (never count toward the gate)",
                  ", ".join(f"{k} {v}" for k, v in env["advisoryScores"].items()), ""]
    for key, head in (("questions", "Questions ChatGPT raised"), ("notChecked", "What ChatGPT says it could not check")):
        if env and env.get(key):
            lines += [f"## {head}", *[f"- {q}" for q in env[key]], ""]
    if changed:
        lines += ["## Edits", *[f"- {f}" for f in changed], ""]
        if check_out:
            lines += ["```", check_out.rstrip(), "```", ""]
    out = pdir / "intake.md"
    out.write_text("\n".join(lines), encoding="utf-8")
    (pdir / "findings.json").write_text(json.dumps(findings, indent=2) + "\n", encoding="utf-8")
    return out


def cmd_intake(a: argparse.Namespace) -> int:
    root = repo_root()
    pdir = Path(a.packet).resolve()
    meta_path = pdir / "packet.json" if pdir.is_dir() else pdir
    if not meta_path.exists():
        raise SystemExit(f"{meta_path} not found: pass the packet folder that `pack` printed")
    pdir = meta_path.parent
    meta = json.loads(meta_path.read_text(encoding="utf-8"))
    text = (sys.stdin.read() if a.reply == "-" else Path(a.reply).read_text(encoding="utf-8")).replace("\r\n", "\n")
    saved = pdir / "reply.md"
    if a.reply == "-" or Path(a.reply).resolve() != saved.resolve():
        saved.write_text(text, encoding="utf-8")

    blocks = extract_blocks(text)
    env, problem = find_envelope(text, blocks)
    errors, notes, findings = [], [], []
    if env is None:
        errors.append(problem)
    else:
        if problem:
            notes.append(problem)
        e, n, findings = check_envelope(env, meta)
        errors += e; notes += n

    changed, applied, check_out = [], False, ""
    edits, e = parse_edits(blocks, text)
    errors += e
    if edits and meta["task"] not in EDIT_TASKS:
        errors.append(f"reply contains edits but a {meta['task']} packet does not allow edits")
    elif meta["task"] in EDIT_TASKS and not edits and not (env and env.get("questions")):
        notes.append("no edits in the reply")
    if edits and meta["task"] in EDIT_TASKS:
        new_text, e, n = plan_edits(root, meta, edits)
        errors += e; notes += n
        changed = sorted(new_text)
        if not errors and a.apply:
            for f, t in new_text.items():
                p = root / f
                p.parent.mkdir(parents=True, exist_ok=True)
                p.write_text(t, encoding="utf-8", newline="")
            applied = True
            try:
                check_out = git(root, "diff", "--stat", "--", *changed)
            except SystemExit:
                check_out = ""
        elif not errors:
            notes.append(f"{len(edits)} edit(s) apply cleanly; run again with --apply to write them")

    out = write_intake(pdir, meta, env, findings, errors, notes, changed, applied, check_out)
    print(out.read_text(encoding="utf-8"))
    return 1 if errors else 0


# ----------------------------------------------------------------------------- main

def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("pack", help="build a packet for ChatGPT")
    p.add_argument("--task", choices=TASKS, required=True)
    p.add_argument("--title", required=True, help="a few words; used in the packet id")
    p.add_argument("--ask", required=True, help="exactly what ChatGPT should do")
    p.add_argument("--context", help="extra background in a sentence or two")
    p.add_argument("--file", action="append", help="file to show, optionally path:start-end")
    p.add_argument("--diff", help="git diff range, e.g. main...HEAD")
    p.add_argument("--image", action="append", help="picture to attach (real Unity captures only)")
    p.add_argument("--notes", help="text file with capture notes, e.g. the camera list from a manifest")
    p.add_argument("--allow-edit", action="append", help="file ChatGPT may change (small-edit and docs)")
    p.add_argument("--out", help="where packets go (default: handoff/ at the repo root)")
    p.add_argument("--force", action="store_true", help="allow a packet over the size limit")
    p.set_defaults(func=cmd_pack)

    i = sub.add_parser("intake", help="check ChatGPT's reply against its packet")
    i.add_argument("packet", help="the packet folder (or its packet.json)")
    i.add_argument("reply", help="file holding ChatGPT's reply, or - for standard input")
    i.add_argument("--apply", action="store_true", help="write the edits to the working tree (never commits)")
    i.set_defaults(func=cmd_intake)

    a = ap.parse_args(argv)
    return a.func(a)


if __name__ == "__main__":
    sys.exit(main())
