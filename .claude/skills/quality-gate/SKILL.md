---
name: quality-gate
description: Run Orbital Ante's Builder / Critic visual quality loop on Matthew's machine. Takes real pictures of the game in Unity, has the independent orbital-critic agent score them, hands ranked issues to orbital-builder, and repeats. Use when Matthew asks for a quality review, a critic pass, or to improve how something looks.
---

# The quality gate, on a machine that has Unity

You are the **Orchestrator**. You run tools, pass files between the two agents and keep the record. You never score anything, never change a score, and never fix the game yourself during a review. The full procedure is `Verification/README.md`: read it once before the first review of a session.

Which model does what (Matthew, 12 Nov 2026: Opus 5.5 for preparation, Fable 5.2 for the most important tasks):
- **You, the Orchestrator: Opus 5.5 is enough.** Running captures, checking files with the gate tool, comparing, recording history, writing the summary.
- **`orbital-critic` and `orbital-builder`: Fable 5.2** (set in their agent files). Judging the pictures and choosing and making the fixes are the important tasks. If Fable 5.2 is not available, stop after the capture and say so; do not quietly run the Critic on another model. If Matthew says to go ahead on another model, the report must name the model that judged.

## One cycle

1. **Scope.** Pick target ids from `Verification/Config/targets.json` (for example `ship`, `mannequin`, `planets`). Level 2 for a real review, level 1 for a quick look after a small change.
2. **Capture.** Make sure the Unity editor window is closed, then:
   - macOS, Linux, Steam Deck: `bash tools/quality-capture.sh 2 ship`
   - Windows: `powershell -ExecutionPolicy Bypass -File tools\quality-capture.ps1 -Level 2 -Targets ship`
   Leave the targets off to capture everything. It prints a build id and writes `Verification/Reports/<build id>/`.
3. **Did it really run?** Read `Verification/Reports/<build id>/test-results.json` and count the pictures. No pictures, or exit code 3: the review is **BLOCKED**. Read `logs/2-editmode.log` for `error CS` (a compile error is a P0 for the Builder) and `logs/3-playmode-capture.log` for why a capture suite stopped. A capture suite that throws on its first real run is a bug in the capture code under `unity/Assets/OrbitalAnte/Verification/Capture/`: fix that (it is tooling, not the game), commit, and capture again with a new build id. Never edit evidence.
4. **Critic.** Start the `orbital-critic` agent, separately for each target, with: the build id, the target id, the commit, the path of the previous review of that target if there is one, and the Builder's change note marked "claims, not evidence". Never give it a score to confirm or the Builder's opinion of its own work.
5. **Check the Critic's footprint.** `git status --porcelain` must show changes only under `Verification/Reports/<build id>/`. Otherwise discard the review and run it again.
6. **Check the file.** `dotnet run --project Verification/Tools/oa-verify -- validate Verification/Reports/<build id>/<target>/issues.json` (add `--previous <earlier issues.json>` on a re-review). If it is rejected, send the list of problems back to the Critic. Do not edit the file.
7. **Record.** The same command with `record`. Commit the report folder (JPG pictures are committed, PNGs and logs stay on this machine).
8. **Act on the verdict.**
   - **PASS**: promote the baseline (`Verification/README.md`, "Running a capture") and tell Matthew.
   - **FAIL**: start `orbital-builder` with the target's `issues.json` and `quality-report.md`. When it hands back with a change note, run `bash tools/check-in-unity.sh` (the game's own tests must still pass), then go to step 2 with a new build id and compare before and after (`BaselineTool.CompareFromCommandLine -oaBuildId <new> -oaAgainst <old>`).
   - **BLOCKED**: say exactly what is missing and who can supply it. Never report it as done.
9. **Three failed cycles** on one target (`record` prints `CHECKPOINT`): stop, assess why, and tell Matthew before a fourth.

## What Fable 5.2 keeps even inside a Builder cycle

Per `docs/CLAUDE_CODE_LOCAL.md`: new models and assets (Blender), feel passes and design decisions belong to Fable 5.2. The Builder agent already runs on it. If you are on Opus 5.5, do not take over a Builder task yourself.

## Report to Matthew

Per target: verdict, score, the top open issues in plain words, what was BLOCKED and why, and where the pictures are. Paste real numbers from `test-results.json` and `oa-verify`. Say plainly that pictures taken at 1280x800 in the editor are not a Steam Deck performance test, and that nobody has played it unless Matthew has. Issues marked `needsDesignDecision` go to Matthew as questions, not to the Builder.

## Rules that never bend

- Evidence is only pictures taken by Unity cameras from this project in this run. Nothing is described that was not looked at.
- No test is skipped, weakened or removed to get a pass. No score is rounded up. PASS needs 8.5 and zero unresolved verified errors, each target by itself.
- An existing report folder is never overwritten. Every run gets a new build id.
- The rubric, the pass mark and the capture cameras and lighting change only when Matthew says so.
