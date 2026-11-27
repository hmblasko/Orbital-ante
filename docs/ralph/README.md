# The Ralph loop

Matthew, 27 Nov 2026: "I want you to implement a Ralph loop with yourself until the game has a clear goal, is easily
understandable, has strong aesthetics, has enjoyable gameplay, has fun unlockables system, and is all around fun."

A Ralph loop runs the same instructions over and over. Each pass reads where the last one stopped (this folder),
scores the game against a fixed checklist, builds the biggest gap, scores again and writes down what it did. It
stops when the checklist passes.

## The checklist

1. **Clear goal.** At every moment the player knows what they are trying to do, why, and what comes next.
2. **Easily understandable.** Every rule, threat and reward can be learned from what the game shows and plays, without
   reading anything twice.
3. **Strong aesthetics.** It looks and sounds like one deliberate, realistic, non-generic game (Matthew, 14 and 26 Nov:
   realistic, not cartoony, not "AI-made").
4. **Enjoyable gameplay.** Moment to moment play (flying, landing, fighting, gibbing, moving on foot) is satisfying and
   varied.
5. **Fun unlockables system.** There is always something worth unlocking in view, unlocks come early and often, they
   change what later runs can be, and getting one is a moment.
6. **All-round fun.** The whole loop, run after run, makes you want one more run.

## Scores

Each line is scored out of 10, by reading the code, rule tests and offline pictures. **No score is a playtest
result**, and none is ever written as one.

| Score | Means |
|---|---|
| 0 to 2 | Missing, broken or unreachable |
| 3 to 4 | There, but weak, or out of most players' reach |
| 5 to 6 | Works on paper, with clear gaps |
| 7 | Solid on paper; small gaps |
| 8 | Everything that can be done without playing it on the Deck is done |
| 9 to 10 | Only after Matthew's playtests say so |

So the loop's bar, **8 on every line**, means "nothing more can be closed in the cloud". Past that, only the Deck can
tell.

## One pass

1. Read `scorecard.md` and `log.md`.
2. Score again anything the last pass changed, honestly, with the code as evidence.
3. Pick the biggest gap that can be closed in the cloud (ties go to the line with the lowest score).
4. Build it fully: rules in plain C# with EditMode tests, the Unity side, sounds or pictures made from code where needed.
5. Check: `dotnet test tools/rules-tests`, the runtime compile check (`docs/ralph/compile-check.md`), the specialist
   agents (unity-reviewer, game-designer, deck-performance) for anything engine-facing, then fix what holds up.
6. Write the pass into `log.md`, update `scorecard.md`, CLAUDE.md and the project's memory, commit, push to
   `claude/ralph-loop` and keep its one pull request into `claude/playtest` up to date.

The loop never claims anything about how the game looks, sounds or feels in Unity. It stops when every line is at 8,
then asks Matthew for a Deck playtest, whose findings start the next loop.

## Restarting the loop

The loop runs as a self-paced `/loop` in a cloud session. Its timer lives in that session's container, so it stops if
the container is released. To start it again, run `/loop` with this text:

> Ralph loop for Orbital Ante (Matthew, 27 Nov 2026, cmsg_01KEHdyAX6e6hXpiV72stnnJPNeA8pyyC5TGkgv3Cufr85): keep
> iterating until the game has a clear goal, is easily understandable, has strong aesthetics, has enjoyable gameplay,
> has a fun unlockables system, and is all-round fun. Each iteration, in /home/claude/orbital-ante on branch
> claude/ralph-loop: read docs/ralph/scorecard.md and docs/ralph/log.md; re-score the six criteria honestly from the
> code (never claim playtest results, never invent them); pick the biggest gap that can be closed in the cloud; build
> it fully (plain C# rules with EditMode tests, Unity wiring, sounds or pictures made from code where needed); run
> dotnet test tools/rules-tests and the runtime compile check in /home/claude/cc (sync.sh, then dotnet build; only the
> two excludeLayers errors are expected); send engine-facing changes to the repo's specialist agents (unity-reviewer,
> game-designer, deck-performance, run as general-purpose agents told to read .claude/agents/<name>.md first) and
> apply what holds up; update the scorecard, the log, CLAUDE.md and project memory; commit, push, keep the one loop PR
> into claude/playtest up to date; update the thread checklist. Post to Matthew only with a result, a blocker or a
> merge card. Stop when every criterion is at 8/10 or more on paper and nothing more can be closed without a Deck
> playtest, then report and ask Matthew to playtest.

The compile check is also kept in the repo, as `tools/runtime-compile` (see `compile-check.md`). Use it when `/home/claude/cc` is missing.
