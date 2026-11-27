# Critique loop, round 6 (27 Nov 2026)

These are the two items left open at the end of round 5. Both are small screen fixes that can be checked with rule tests. **Nothing here has been run in Unity.**

## Finding 16: a big award was squeezed into a row too small for it

| | |
|---|---|
| Problem | The "+points" lines are 24 px apart and 24 px tall, but a Big award is drawn at 24 pt with an outline. The rarest and best moment of the readout could be clipped, or touch the line under it. |
| Principle | Legible risk/reward (5): a reward the eye cannot read cleanly is a weaker reward. Feedback should grow with what the player did (fun psychology report). |
| Evidence | `AnteLoop` built the rows at `-170 - i * 24` with a 24 px height. `PopFeedback.LookFor(Big)` used font size 24. |
| Change | `PopFeedback.RowPitch` is now 28 and `BiggestFont` is 24. The rows are built from these, and a rule test holds every tier to at least 4 px of room. The four rows now end at -282 instead of -266. |

## Finding 17: PERK READY disappeared in the cabin, where its button works

| | |
|---|---|
| Problem | PERK READY names the button that opens the pick (Tab or Select). Walking the cabin, that button works, but the line was hidden whenever the player was aboard. The old reason, the ship's damage list sharing that corner, has not held since stage 5 moved the damage list to the right. So the prompt vanished right where the player is safest to make the pick. |
| Principle | Manage cognitive load (7) and learnable challenge (3): a prompt should show wherever its action works, and only there. |
| Evidence | `AnteLoop.UpdateHud` checked `!Astronaut.PlayerInShip`. `ShipHud._damage` is anchored top right. Upgrades opens on foot, aboard or outside, but not in the seat. |
| Change | `ReadoutFocus.ShowsPerkReady(earned, seated)`: the line shows on foot and while walking the cabin, and stays hidden in the seat, where Select is the floodlights. The "+points" lines still wait aboard, because the seat view's own readout may sit there (PR 72). |

## Tests

- `dotnet test tools/rules-tests`: 2,800 passed, 1 skipped. Two of those are new tests in `CritiqueRound6Tests`.
- Runtime compile check: clean apart from the two known `excludeLayers` lines.
- Not sent to the specialist agents. The engine-facing change is two lines in `AnteLoop`, each calling a tested rule.

## Still open

- ChatGPT's turn: paste `chatgpt/critique-loop-round-3.md` from the project files. It covers rounds 1 to 4. Rounds 5 and 6 are in `docs/reports/`.
- Whether a 28 px pitch looks right on the Deck's 1280 x 800 screen. That needs a real screen.
