---
name: unity-refine
description: Run Orbital Ante in Unity on Matthew's machine (setup, EditMode and PlayMode tests), read what failed, make the smallest fix and run again. Use for local refinement sessions, especially on Opus 5.5.
---

# Refine Orbital Ante with real Unity runs

This skill runs on Matthew's own computer, where Unity 6 is installed. Unlike a cloud session, you can actually run the engine here, so use it: every claim in a report must come from a run you did.

## The loop

1. Make sure the Unity editor window is closed (batch mode cannot open a project that is already open).
2. Run the checks:
   - Windows: `powershell -ExecutionPolicy Bypass -File tools\check-in-unity.ps1`
   - macOS or Linux: `bash tools/check-in-unity.sh`
   - Add `-SkipPlayMode` (Windows) or `--quick` (macOS/Linux) for a fast run while iterating; always finish with a full run.
3. Read `unity/Logs/oa-check/summary.txt` first. Then, for each failure:
   - Compile errors (`error CS....`): read `2-editmode.log`, fix them before anything else, nothing else runs until they are gone.
   - A failed test: read its message in the summary, open the test and the code it covers, and find the cause. Do not weaken, skip or delete a test to make it pass. If you believe the test itself is wrong, say why in the report and change it only with a clear reason.
   - Setup problems: read `1-setup.log` for lines starting `Orbital Ante:`.
4. Make the smallest change that fixes the cause, following `CLAUDE.md` Part A (rules in plain C#, numbers in config assets, seeded randomness, Unity 6 API names).
5. Run the checks again. Repeat until the summary says `Overall: PASS`.
6. If a fix touches engine-facing code, send it to the `unity-reviewer` agent before committing.
7. Commit on a branch named `claude/<short-topic>` (never straight to `main`), push, and open a pull request.

## What to write in the report (and the pull request)

- Which checks ran, and the exact pass/fail counts from `summary.txt`. Paste them; never round or guess.
- What failed, the cause, and the fix.
- What is still not checked: tests prove rules and wiring, not feel. Say plainly that nobody has played it unless Matthew has.

## Leave these for Fable 5.2

Matthew keeps these for Fable 5.2. If a fix needs one, stop, describe it, and leave it:
- Making any model, texture or asset (Fable makes them in Blender). Code-built shapes are stand-ins only.
- The look and feel passes: launch effects and sound, cockpit dash designs, flight and crash handling, tuning numbers that change how the game feels.
- Design decisions and new features. Phase 1 rule: refine before expanding.

## Useful extra runs

- Play a scene for real: open Unity, **Orbital Ante > Open Test Arena**, press Play. Only Matthew can judge feel; ask him what he saw rather than assuming.
- Console errors while playing are saved in `Editor.log` (Windows: `%LOCALAPPDATA%\Unity\Editor\Editor.log`; macOS: `~/Library/Logs/Unity/Editor.log`). Read it after Matthew plays a session and fix any red errors it holds.
