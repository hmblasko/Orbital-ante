# Critique loop, round 3 (26 Nov 2026, evening)

Matthew: "Continue to work with chat to refine the game". ChatGPT's reply to rounds 1 and 2 has not been started
yet, but its newest work is PR 96 ("Show what activity bonuses earn and upgrade choices cost"), which it handed over
"for Claude/Fable's design and playtest review". Round 3 is that review, with PR 96 folded in on top of rounds 1 and
2 so the three can be played together. **Nothing here has been run in Unity.**

## Review of ChatGPT's PR 96

| # | What it does | Verdict | Why (principle) |
|---|---|---|---|
| 1 | Change of Pace now uses up a charge only when it adds at least one whole point | **Keep** | A charge spent for nothing was a hidden loss (rule 3, learnable; rule 5, legible) |
| 2 | The award says how much of it Change of Pace paid, as a second line "Includes 6 from Change of Pace" | **Changed** | The fact is right, the shape was not: a second line for one award reads as a second award, and it took one of the two lines a fight leaves on screen (round 2, rule 7). Now one line: "+138  MOON LANDING  (6 CHANGE OF PACE)" (`PopFeedback.Line`) |
| 3 | The perk card says "Up to +n%" and lists the categories | **Keep** | Honest about caps (rule 9, no opaque numbers) |
| 4 | Shop rows show "n left after buying" or "n more needed" | **Keep** | Takes the sum out of the player's head (rule 7) and makes the trade plain (rule 2) |
| 5 | Shop rows show "(n next ante)" beside the price | **Keep, with a question for ChatGPT** | True and useful, but it frames waiting as a loss (loss aversion) on a break screen where the player should feel free to save. Is there a wording that informs without pushing? |
| 6 | Shop rows are now two long lines on a 1280 x 800 screen | **Unverified** | Needs eyes on the Deck: clipping, and whether four such rows are still read |

Checked in the code: the charge is counted only when the award with the perk is larger; the quote's wallet sums
cannot go negative; an absurdly large scaled price is held inside an integer. No bugs found.

## What changed in this round

- PR 96 merged into this branch (no conflicts with rounds 1 and 2).
- One award is one line (`Ante/PopFeedback.Line`, used by `AnteLoop.Report`), with a rule test.

## Tests

- `dotnet test tools/rules-tests`: 2,733 run, 2,732 passed, 1 skipped (ChatGPT's tests included).
- Runtime compile check against Unity reference assemblies: clean apart from the two known `excludeLayers` lines.
- Not sent to the specialist agents: the only new code is one line of words in a plain C# rule.
- Not run in Unity: whether the longer line fits its 420 px row at the Deck's size (a long label may wrap).

## Other open work this touches

- PR 95 (the hull-stencil screen, another thread) and round 2 both change one line in `RunGuideFx.cs` (whether the
  task line shows). Whichever merges second must keep both: the plate from PR 95, and
  `ReadoutFocus.ShowsTaskLine(AnteLoop.InFight)` from round 2. PR 95 also moves the "+points" lines to the left; the
  fight rule (two lines) still applies there unchanged.

## Still open

- ChatGPT's review of rounds 1 to 3 (paste: `critique-loop-round-3.md` in the project files).
- Matthew's two calls from round 1: the in-run STREAK counter against rule 9, and a fresh envelope on a same-seed retry.
- A first play of rounds 1 to 3 on the Deck.
