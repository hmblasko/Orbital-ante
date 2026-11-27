# Critique loop, round 2 (26 Nov 2026)

Matthew, 26 Nov 2026: "Refine until the game is perfect". Round 2 takes the findings round 1 left open
(`docs/reports/critique-loop-1.md`, rows 10 to 13) without waiting for ChatGPT's round, which will be folded in
when it arrives. Built on top of round 1 (PR 91). **Nothing here has been run in Unity.**

## What changed

| Round 1 finding | Problem | Principle | Change |
|---|---|---|---|
| 10. Ten rows of words in one corner | In a fight the eyes can spare a glance; the rows that matter were lost among rows that could wait | Cognitive load (rule 7) | **Readout focus** (`Tasks/ReadoutFocus.cs`): while an enemy has had you in sight in the last 6 s, the readout shows only the two newest "+points" lines, the world's task line waits, and tips wait unless they are about the clock or the target. Ante, clock, score, bar, streak, pace and PERK READY stay. Everything returns when the fight ends; a tip that waited is not marked as seen |
| 11. Two presses from dying to retrying | After the 0.8 s hold nothing was selected: one press to get a cursor, another to pick | Retry friction after failure (competence) | **Selection hold** (`UI/SelectionHold.cs`, in `PauseMenu`): on the death page, the ante break and choice pages, the default selects itself once the hold is over and the confirm button has been up for 0.25 s in a row. A button held or mashed through the hold still picks nothing. Nothing starts by itself: it still takes a deliberate press |
| 13. Perk costs in danger red | A fair trade was framed as a threat (loss aversion), and red meant four things | Agency (rule 2), legible risk (rule 5) | Costs on the star chart are now a cool tone as bright as the gains, with the same minus sign. Red is left for damage, the late clock and overtime |

Finding 12 (buttons whose meaning changes with where you are) was checked and left: PERK READY is already hidden
aboard, where Select is the floodlights, and the controls page names both uses of Jets Down. No mode error was found
in the prompts that can be read from code; it needs hands on a Deck.

## Hypothesis and how to tell (rule 10)

- Readout: with fewer rows in a fight, the player still notices PERK READY and the clock. Test: fight at a camp near
  a mast; the task line should vanish during the fight and return about 6 s after the last enemy loses sight of you.
- Retry: die, keep your hands still; a highlight should appear on "Try again with the same seed" within about a
  second, and one press restarts. Die while mashing A: nothing should be picked until you stop.
- Costs: on the star chart a cost line should read as clearly as a gain line, and not look like a warning.

## Tests

- `dotnet test tools/rules-tests`: 2,720 passed, 1 skipped (6 new in `CritiqueRound2Tests`).
- Runtime compile check against Unity reference assemblies: clean apart from the two known `excludeLayers` lines.
- Not run in Unity: how the readout looks when rows drop out, the cursor appearing on the death page, the cost
  colour on the Deck's screen.

## Agent review

- **unity-reviewer:** a slow frame as the page opens could eat the whole hold in one lump (fixed: one frame counts
  for at most 0.05 s, with a test); hidden "+points" rows could flash for a frame on a new kill (fixed in
  `ShiftPops`). It confirmed from the code that selecting a button never fires it without a fresh press, because the
  event system has already handled the frame's input.
- **game-designer:** keep all three. Raise the fight hold from 4 s to 6 s so the task line does not flicker back
  while enemies duck to reload (done). Make the cost tone as bright as the gains so a downside is never fine print
  (done). Auto-select does not break rule 9: no timer, nothing starts by itself.
- **deck-performance:** no allocations per frame; at most three `SetActive` calls when a fight starts or ends.

## Still open

- ChatGPT's round 1 review, not back yet.
- Matthew's two calls from round 1: the in-run STREAK counter against rule 9, and a fresh envelope on a same-seed retry.
- A 24 pt "big" pop sits in a 24 px row; needs a look on a real screen before it is moved.
