# Visual quality gate

Matthew asked for this on 12 Nov 2026. It is a permanent part of how Orbital Ante is built: after any substantial visual work, an independent Critic looks at the real game, scores it, and the work is refined until it passes.

**PASS = aesthetic score of at least 8.5 out of 10 AND zero unresolved verified errors AND every required inspection done AND tests run and passed AND no open regression.** Anything that could not really be verified is **BLOCKED**, never PASS.

```
BUILD -> TEST -> CAPTURE -> CRITIQUE -> SCORE -> RANK -> REFINE -> RETEST
```

## Who does what

| Role | Who | May | May not |
|---|---|---|---|
| **Builder** | `orbital-builder` agent, or the session building a milestone | Change game code and assets; choose how to fix an issue | Score its own work; touch `Verification/Reports`, `Baselines`, `History`; weaken tests, the rubric or the capture cameras |
| **Critic** | `orbital-critic` agent, always a separate agent from the Builder | Start capture runs; write the report and `issues.json` inside `Verification/Reports/[Build-ID]/` | Edit anything else; accept the Builder's opinion as evidence; score what it has not seen |
| **Orchestrator** | The main Claude session | Decide when to review and what the scope is; check the Critic's file with the gate; hand issues to the Builder; keep the history; escalate | Change a score or verdict; let a failed gate through; record a review the gate rejects |
| **Matthew** | | Everything. Final creative authority. Only Matthew can waive an issue (`wont_fix`) or change the rubric | |

The gate tool, not any agent, works out the overall score and the verdict from the Critic's category scores and facts. If the Critic's stated verdict disagrees with the rules, the file is rejected.

## What is where

```
.claude/agents/orbital-builder.md, orbital-critic.md     the two roles
unity/Assets/OrbitalAnte/Verification/
    Rules/      plain C#: rubric, gate, issue ranking, picture comparison, test summary, history
    Capture/    PlayMode capture suites: cameras, screenshots, sequences, scene checks, log watch
    Editor/     the menu and command-line runner, baseline comparison, review check
    Tests/      EditMode tests for the rules
Verification/
    Config/     targets.json, issues.schema.json, quality-report-template.md
    Tools/      oa-verify (the gate without Unity) and its test project
    Baselines/  approved reference pictures per target (appear when a target first passes)
    Reports/[Build-ID]/
        test-results.json, editmode-results.xml, playmode-results.xml
        [target]/capture-manifest.json     every picture's scene, camera, lens, size, state, lighting
        [target]/Screenshots/              .jpg (committed) and .png (lossless, stays on the capturing machine)
        [target]/Screenshots/sequences/    contact sheets for motion
        [target]/Comparisons/              difference pictures and numbers against baseline or an earlier run
        [target]/quality-report.md, issues.json     the Critic's review
    History/history.json    one line per accepted review, only ever added to
```

Nothing in a finished `Reports/[Build-ID]` folder is edited or overwritten afterwards. A new look at the game is a new Build-ID.

## The two levels

**Level 1, lightweight.** After routine changes to anything visible. Rule tests, then a couple of targeted pictures per target with the scene checks. Read `test-results.json` and glance at the pictures for obvious breakage (blank or pink frames are flagged automatically). No score.

**Level 2, full Critic review.** After creating or substantially changing a character model, an animation set, a spacecraft or its interior, a major environment asset or planet, major lighting or materials, destruction visuals, a substantial visual feature, or at the end of a milestone. All tests, the full capture, then the Critic.

## Running a capture

A capture needs the Unity editor with a graphics device. Cloud sessions do not have one: there the step is BLOCKED until it is run on Matthew's computer (through Remote Control, or by hand as below).

### For Matthew, by hand

1. Open the project in Unity (the `unity` folder) and wait for it to finish loading.
2. If Unity is in Play mode, press the Play button to stop it.
3. In the menu bar at the very top choose **Orbital Ante > Verification > Level 2 - Full Capture** (or **Level 1 - Quick Check**).
4. Leave Unity alone. It runs the tests, then flickers into Play mode by itself and takes pictures. Level 2 takes several minutes.
5. When it finishes, the **Console** tab (menu **Window > General > Console**) shows a line starting `Orbital Ante verification '...' finished`. That line names the folder with the evidence.
6. To look at the pictures: **Orbital Ante > Verification > Open Reports Folder**.
7. If the menu is missing or the Console shows red errors instead, copy the first red line and send it to Claude. If a run seems stuck for more than 20 minutes, choose **Orbital Ante > Verification > Cancel Stuck Run** and tell Claude.
8. To let Claude see the results from the cloud: in GitHub Desktop, commit the new files under `Verification/Reports` and push.

### For an agent with a shell on a machine that has Unity

Close the project in the Unity editor first (two editors cannot open one project). Find the editor: Windows `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`, macOS `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`, Linux `~/Unity/Hub/Editor/<version>/Editor/Unity`. Do not pass `-nographics`.

```
<Unity> -batchmode -projectPath unity -logFile - \
  -executeMethod OrbitalAnte.Verification.EditorTools.VerificationRunner.RunFromCommandLine \
  -oaLevel 2 [-oaTargets mannequin,weapons] [-oaBuildId <id>]
```

Exit code 0: everything ran and passed. 2: something failed (read `test-results.json`). 3 or anything else: it could not run; that is BLOCKED. If the scripts do not compile, Unity exits before the runner starts and the log holds `error CS` lines: that is a P0.

Compare with the approved baselines, or with an earlier run for before and after:

```
<Unity> -batchmode -projectPath unity -logFile - \
  -executeMethod OrbitalAnte.Verification.EditorTools.BaselineTool.CompareFromCommandLine -oaBuildId <id> [-oaAgainst <earlier id>]
```

After a target passes, make its pictures the new baseline (refused unless that target's `issues.json` is a valid PASS):

```
<Unity> -batchmode -projectPath unity -logFile - \
  -executeMethod OrbitalAnte.Verification.EditorTools.BaselineTool.PromoteFromCommandLine -oaBuildId <id> -oaTarget <target>
```

## The gate tool (works anywhere with the .NET 8 SDK)

```
dotnet run --project Verification/Tools/oa-verify -- validate Verification/Reports/<id>/<target>/issues.json [--previous <earlier issues.json>]
dotnet run --project Verification/Tools/oa-verify -- record   Verification/Reports/<id>/<target>/issues.json [--previous ...]
dotnet run --project Verification/Tools/oa-verify -- rank     Verification/Reports/<id>/<target>/issues.json
dotnet run --project Verification/Tools/oa-verify -- status [milestone]
dotnet test Verification/Tools/oa-verify.tests
```

`validate` exits 0 only for a review file that is complete and consistent. `record` also adds it to the history and prints `CHECKPOINT` when a target has failed three full cycles in a row.

## The Orchestrator's loop

1. **Ready?** The Builder says a change is ready, with a change note. Confirm compilation and the rule tests really ran.
2. **Scope.** Pick the targets from `Config/targets.json` that the change touches, plus any target whose earlier issues it could have disturbed. Decide Level 1 or Level 2.
3. **Start the Critic** as a separate agent (`orbital-critic`). Give it: the commit, the targets, the previous review path if there is one, and the Builder's change note marked as "claims, not evidence". Never give it a score to confirm.
4. **Check the footprint.** After the Critic finishes, `git status --porcelain` must show changes only under `Verification/Reports/<this Build-ID>/`. Otherwise discard the review and run it again.
5. **Check the file.** `oa-verify validate` (with `--previous` when there is one). If it is rejected, send the list of problems back to the Critic. Do not edit the file yourself.
6. **Record.** `oa-verify record`. Commit the report folder.
7. **PASS**: promote the baseline, tell the Builder and Matthew. **FAIL**: hand `issues.json` and the report to the Builder; when it hands back, go to step 2 with a new Build-ID and a matched before and after comparison (`-oaAgainst`). **BLOCKED**: say exactly what is missing and who can supply it; do not continue as if it had passed.
8. **Regressions.** Every re-review re-tests the issues resolved earlier (the `regressionChecks` list). An issue that comes back is `regressed` and fails the gate by itself.
9. **Three failed cycles** on one target in one milestone: stop and assess before a fourth. Which issues remain, are the fixes working, is the design approach wrong, is a tool or asset missing (often: a real model from the asset pipeline instead of code-built shapes)? Recommend a targeted redesign or alternative, and tell Matthew. Three cycles is a checkpoint, not a pass and not a full stop.
10. **Milestone gate.** A milestone's visual work is done when every one of its required targets has passed by itself (`oa-verify status M7`). Scores are never averaged across targets.
11. **Escalate to Matthew**: issues marked `needsDesignDecision`, disagreements between the Critic's standard and the approved design, anything that needs a human playtest, and stalled work.

## Conventions for review files

- **A BLOCKED review with nothing seen**: `categories` is an empty list, `overallScore` is null, `strengths` is empty, and `technical.unresolvedErrorCount` 0 means "nothing could be checked", not "clean". The thing that blocked it is an issue of kind `verification_gap` whose evidence is the document or log that shows the gap (and the review's own `quality-report.md`).
- **Evidence paths** are written from the repository root. Paths relative to the review's own folder are also accepted.
- **`milestone`** is the single milestone whose gate the review counts toward (for example `M7`), because cycle counting and `oa-verify status M7` match it exactly. Reviews of older work done before the gate existed use `M1-M6`.
- **Inspections beyond the required list** (gameplay context, baseline comparison, performance) are recorded with `required: false` and the ids `views.gameplay`, `comparison.baseline`, `performance`.
- **Pictures** are opened with the Read tool, one file at a time.
- **No Unity on the machine**: that is BLOCKED. Do not try to install Unity.

## Honesty rules (all roles)

1. Never fabricate screenshots or observations. Pictures come from Unity cameras only; never from an image model, never from the non-Unity preview pictures.
2. Never say Unity tests passed unless they ran.
3. Never call a review independent if the Builder reviewed its own work.
4. Never inflate a score to reach 8.5, and never change the rubric to make a build pass.
5. Never hide or suppress a verified error, and never remove a failing test to get green.
6. Never trade away an approved design requirement for a score.
7. Never declare PASS when a required verification was blocked.
8. More complexity is not more quality.
9. Keep confirmed findings, suspicions and visual judgments apart (`kind` on every issue).
10. Keep the evidence of earlier reviews intact.
11. Steam Deck: 1280x800 pictures show Deck framing only. Nothing is "tested on Deck" without a Deck.
12. The Critic's score is a development tool. It does not replace Matthew's taste, and Matthew can overrule it in either direction.

## Status

As of 12 Nov 2026 the rules are tested outside Unity (33 tests). The capture suites, the runner and the baseline tool compile against Unity reference assemblies but have **not yet been run in Unity**; expect the first run to need fixes. Until a capture has run, every target is BLOCKED.

Not yet covered by the capture suites: the ship (Milestone 7), launch, cockpit, jetpack, weather, caves, gas giant interiors, underwater, the player's own firing effects, the controls page, video capture, and profiler numbers. Each is listed in `Config/targets.json` or in a suite's "not captured" notes so the gap shows up in every manifest.
