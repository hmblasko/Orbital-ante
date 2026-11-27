# Critique loop, round 4 (27 Nov 2026)

Matthew, 27 Nov 2026: "You make the executive decisions, continue to build the game until it is finalized,
including using Chat to help". Round 4 settles the questions rounds 1 to 3 left for him, and brings the branch up to
date with playtest (bonus missions, PR 89, and ChatGPT's PR 96 are now in it). **Nothing here has been run in Unity.**

## Decisions

| Question | Decision | Why (principle) |
|---|---|---|
| Is the in-run STREAK counter against rule 9 ("no streaks")? | **Keep the rule, change the word.** It now reads "ON A ROLL x3" (`Tasks/RunWords.cs`) | Rule 9 is about counts kept across days that punish a day off. This one is a combo inside one run: earned by playing well, pays one style feat, costs nothing when it lapses. But "streak" is the other kind's name, and a word carries its pressure with it |
| Should a restart show the prize envelope again? | **No.** A run restarted from the death page or the pause menu seals its envelope as always but skips the showing (`RunEnvelope.ShowsAtStart`). A run begun from the title screen shows it | Every extra screen between failing and trying again makes another try less likely; the player saw the envelope minutes ago. Nothing is lost: the envelope still exists and still opens after ante 9 |
| Does "(n next ante)" in the shop push the player to spend? | **Keep it as ChatGPT wrote it** | It is true, and hiding it would make the price rise a hidden rule (rule 3). Points held across antes really do buy less. ChatGPT may still propose better words |
| Bonus mission line (PR 89) in a fight? | **Stays on screen in a fight** | Most contracts are advanced by fighting, so it is one of the rows a fight needs (the round 2 rule asks every new row in that corner to answer this) |

## What changed

- `Tasks/RunWords.cs` (plain C#): the readout line and the feat label say ON A ROLL. The code keeps the name
  `Streak` so the TaskConfig asset keeps its numbers.
- `Items/EnvelopeRules.cs` `RunEnvelope.ShowsAtStart`, `EnvelopeDirector.RunStarted(bool)`, `GameBootstrap`: a
  restart skips the envelope's showing.
- Playtest merged in. One conflict (`AnteLoop.cs`, the readout's notes against the bonus mission line): both kept.

## Tests

- `dotnet test tools/rules-tests`: 2,747 passed, 1 skipped (two new tests; bonus missions' and ChatGPT's included).
- Runtime compile check against Unity reference assemblies: clean apart from the two known `excludeLayers` lines.
- Not sent to the specialist agents: two wording rules and one boolean on an existing path.
- Not run in Unity: that a restart really goes straight to the pad with no envelope, and that the first-run goal
  card (which waits for the envelope to close) still appears on a restart.

## On the Deck (rounds 1 to 4 together)

1. Start a run from the title screen: the envelope shows. Die, press once on "Try again": no envelope, straight to the pad.
2. Do two tasks close together: the readout says ON A ROLL x2.
3. Then the 11 steps in `critique-loop-1.md` and the three checks in `critique-loop-2.md`.
