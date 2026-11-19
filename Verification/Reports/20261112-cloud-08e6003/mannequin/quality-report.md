# ORBITAL ANTE - INDEPENDENT QUALITY REPORT

| | |
|---|---|
| Build ID | 20261112-cloud-08e6003 |
| Review ID | R-20261112-mannequin-01 (Level 2, milestone label M1-M6, first review of this target) |
| Commit | 08e6003 (branch claude/visual-qa-system) |
| Unity version | None used. The project declares 6000.0.23f1; no Unity editor exists on the reviewing machine |
| Review target | mannequin: Playtest mannequin (character, movement states, limb loss) |
| Scene(s) | None captured |
| Critic identity | orbital-critic (claude-fable-5-2), a separate agent started by the Quality Orchestrator, on a Linux cloud container |
| Inspection method | None possible. No capture run was started and none existed. See "What was checked" below |
| Screenshot directory | Does not exist |
| Number of screenshots | 0 |
| Animation evidence | None (no contact sheets, no playback) |
| Technical test coverage | None. No tests ran in Unity; no test-results.json exists |
| **Aesthetic score** | **Not scored** |
| **Verified unresolved errors** | **0 (nothing could be checked; this is not a clean result)** |
| **Verdict** | **BLOCKED** |

## What was checked

These are the only facts this review rests on. Each was checked by the Critic on this machine.

- `which Unity unity-editor unityhub` prints nothing. `~/Unity` does not exist. `/opt` holds no Unity. There is no display (`DISPLAY` is empty) and no GPU tooling.
- `Verification/Reports/` did not exist before this review created its own folder. No `test-results.json`, `capture-manifest.json`, screenshot or contact sheet exists anywhere in the repository. `Verification/Baselines/` and `Verification/History/` do not exist.
- `docs/reports/milestone-1.md` states "Nothing has been run in Unity." `Verification/README.md` (Status) states the capture suites "have not yet been run in Unity" and "until a capture has run, every target is BLOCKED."
- The capture test for this target, `CharacterCaptures.MannequinStudio`, exists in source. Reading it shows what it is meant to photograph. That is intent, not evidence, and is not used to judge anything.

No picture was opened because none exists. Nothing below describes how the mannequin looks or moves.

## A. Evaluation scorecard

| Category | Weight | Score | Why (with evidence) |
|---|---|---|---|
| Aesthetic cohesion and art direction | 20% | not scored | not inspected: no pictures |
| Modeling, geometry, and silhouettes | 20% | not scored | not inspected: no pictures |
| Environmental composition and visual interest | 15% | not scored | not inspected: no pictures |
| Materials, lighting, and visual effects | 15% | not scored | not inspected: no pictures |
| Animation and movement presentation | 15% | not scored | not inspected: no contact sheets, no playback |
| Gameplay readability and visual feedback | 10% | not scored | not inspected: no gameplay captures |
| Overall polish and distinctive appeal | 5% | not scored | not inspected |

Weighted score, with the working shown: none. No category was scored, so there is no sum.

Categories left out as not applicable, and why: none were marked not applicable. All seven are simply unscored because nothing was seen. (`issues.json` carries an empty `categories` list for this reason; see the note to the Orchestrator at the end.)

## B. Strongest visual features

Not inspected. No strengths can be named without pictures.

## C. Ranked issues

| Rank | ID | Severity | Kind | Affected | Problem in one line |
|---|---|---|---|---|---|
| 1 | MAN-001 | P0 | verified_defect (of the evidence, not of the model) | mannequin, and every other target | No Unity capture run exists for this build, so nothing can be verified |
| 2 | MAN-002 | P2 | suspected_defect, needs design decision | mannequin, combat-arena, weapons | The approved design asks for a primitive placeholder; the gate lists it as a hero asset that must reach 8.5 |

## D. Detailed findings

**MAN-001 (P0, verified).** There is no evidence for commit 08e6003. The machine this review ran on has no Unity editor and no graphics device, and no earlier run was committed. All five required inspections (`views.standard`, `distances.three`, `animation.sequences`, `technical.sceneChecks`, `technical.tests`) are blocked. By the project's own reports the project has never been opened in Unity, so it is also unknown whether it compiles in the editor and whether the capture suite itself works. Reproduce with the commands listed under "What was checked". Root cause: cloud sessions have no editor, and the first capture has not yet been run on a machine that does. What is verified is the absence of evidence; no defect in the mannequin is claimed.

**MAN-002 (P2, suspected, from documents only).** `CLAUDE.md` Part B Milestone 1 asks for "a placeholder humanoid (mannequin) from primitives", to be swapped for a proper enemy model in Milestone 12. `Verification/Config/targets.json` lists the mannequin as a `hero` target for M1, M2 and M12, and a target passes only at 8.5. The Critic's standard places placeholder primitives in the 3 to 6 range however cleanly they are built. If the mannequin is what the design asked for, it very likely cannot pass on art grounds, and Milestones 1 and 2 could not close their visual gate until Milestone 12. This is raised now so it does not arrive as a surprise after the first capture. It is not a judgment of the actual model, which has not been seen. Matthew decides.

## E. Recommended improvements

1. Get one complete Level 2 capture run for this commit onto disk and into the repository (MAN-001). Nothing else can move until then.
2. Have Matthew say how the gate applies to an intended placeholder character (MAN-002).

No visual improvement is recommended, because nothing visual was inspected.

## F. Revision comparison

| Metric | Previous | Current |
|---|---|---|
| Overall aesthetic score | - | not scored |
| Unresolved verified errors | - | 0 (nothing checked) |
| P0/P1 issues | - | 1 |
| Resolved issues | - | 0 |
| New regressions | - | 0 |
| Verdict | - | BLOCKED |

First review. No previous review, no baseline, no comparison.

## G. Builder handoff

There is nothing for the Builder to change in the game from this review. The tasks are for the Orchestrator and Matthew.

1. **MAN-001.** On a machine with Unity 6000.0.23f1 and a graphics device, at commit 08e6003 or the commit to be reviewed:
   - By hand: open the `unity` folder in Unity, stop Play mode if running, choose **Orbital Ante > Verification > Level 2 - Full Capture**, wait for the Console line starting `Orbital Ante verification '...' finished`, then commit the new files under `Verification/Reports` and push.
   - Or from a shell with the project closed in the editor (do not pass `-nographics`):
     `<Unity> -batchmode -projectPath unity -logFile - -executeMethod OrbitalAnte.Verification.EditorTools.VerificationRunner.RunFromCommandLine -oaLevel 2 -oaTargets mannequin`
   - If Unity shows compile errors (`error CS` lines) or the runner fails, send the first red line back. Each such failure is a P0 for the Builder until a run completes.
2. **MAN-001, continued.** Start a new Critic review with the new Build-ID. This folder stays as it is.
3. **MAN-002.** Matthew: choose how a deliberately placeholder character is gated (keep at 8.5, defer its art gate to M12, or bring a real model forward).

## H. Acceptance criteria

The next review must find and verify:

- A Level 2 run under `Verification/Reports/[new Build-ID]/` whose commit matches the build under review.
- `test-results.json` showing EditMode and PlayMode tests ran, with every failure and every manifest error classified.
- `mannequin/capture-manifest.json` and pictures for front, rear, left, right, front three-quarter, rear three-quarter and elevated three-quarter, at close, medium and wide.
- Contact sheets for idle, walk, run, turn, limb-pop, one-leg, crawl, wriggle and head-only.
- The mannequin through the game camera (combat-arena capture) at 1280x800 and 1920x1080.
- All seven rubric categories scored from pictures the Critic actually opened.
- An answer from Matthew to MAN-002.

## I. Final verdict

**BLOCKED.** No Unity editor, no graphics device, no capture run, no tests run in Unity, no pictures. All five required inspections are blocked. Nothing was scored and nothing was approved. This is not a FAIL of the mannequin and it is not a pass: the mannequin's quality is unknown.

Not tested: everything visual; all motion; whether the project compiles and opens in Unity; whether the capture tooling works; Unity tests; gameplay context; performance (not measured); Steam Deck hardware (none used, and no 1280x800 pictures exist).

Questions for a real playtest, once the game runs: do limb pops, disarms and leg shots read instantly and feel good? Are the hop, crawl, wriggle and head-roll states funny and fair to fight?

Note to the Orchestrator on this file's form: `categories` in `issues.json` is empty and `overallScore` is null because the gate rejects an applicable category without a score, and marking categories not applicable would misstate why they are unscored. The milestone label "M1-M6" was used as given; `targets.json` lists this target under M1, M2 and M12.

These scores are structured professional judgments backed by the evidence above. They are not measurements of beauty.
