# Fun pass: behavioural psychology, the star chart, and a run simulator (21 Nov 2026)

Matthew asked (21 Nov 2026): "deep research about what make the game fun, implement the changes, play test, and
consider principles of behavioral psychology as to whether the user will find the game fun", then "I'm okay with
perks continuing even in overtime", then for an upgrade system that "looks aesthetically pleasing to upgrade during
the game, even if that is a select menu where you apply the rewarded upgrades". He chose the star chart (mockup A).

Research: "Behavioural psychology of game fun" (project files, `orbital-ante/fun-psychology/reports/`). This branch
sits on top of `claude/playable-loop` (PRs 73, 75, 74, 76), which owns the title screen, onboarding, overtime and
Keep going.

**Nothing here has been run in Unity or played.** The "play test" in the cloud is the run simulator: a made-up player
over the game's real rules. It shows the rules' shape, never how the game feels.

## Will a player find it fun? The honest answer

The research says fun cannot be predicted before play. It also says what tends to keep people playing, and the
simulator checks the rules against that:

| Question the research asks | What the build does now | Evidence for this game |
|---|---|---|
| Does the player feel competent (SDT; the strongest predictor of playing on)? | Feedback grows only with what the player did; the recap says where points came from and the best moment | Rule tests only. Unplayed |
| Is feedback tied to performance (Kao 2024, n = 1,699)? | Point pop-ups have four sizes: plain, one style feat, two or more, a fifth of the target in one go. Plain ones have no chime | Rule tests only |
| Is progress visible and honest (goal gradient)? | Target bar has a pace tick; "n/MIN BEATS THE CLOCK" appears only when behind; no head start | Rule tests only |
| Is the challenge learnable, not inflated? | Enemy pressure is still reaction and aim only, shown on the break card. Overtime (loop thread) replaces instant failure | Simulator: first-time median ante 1 → 3 with overtime, at a danger scale set so half reach ante 4 |
| Are choices meaningful (no dominant or dead pick)? | Weak perks and trap hazards retuned from the simulator | Simulator: no perk now loses antes; All In and No Brakes lead; seven perks the model cannot value read +0 |
| Chance with odds shown, nothing staged | Scratch odds printed, envelope odds printed; the envelope now reads as a gain with its whole rule ("Opens when you clear Ante 9. A run that ends sooner leaves it sealed.") | Text only |
| No dark patterns | No streaks, no timers outside a run, nothing sold. Rerolls cost points you earned, never money, and never lower the score | Constitution rule 9 |

My judgement, labelled as a judgement: the loop now has the pieces the research ties to sustained play (competence
feedback, honest goals, real build choices, a comeback window). Whether the fights themselves are fun still decides
everything, and only the Deck can tell.

## What changed

1. **Star chart upgrade screen** (`UI/StarChartView.cs`, rules in `Ante/StarChart.cs`). Each perk family is a
   constellation of three stars over a night sky. Held perks are lit. The card under the cursor makes the star it
   would light pulse, and says "Completes GUNNER: ..." or "GHOST 1 of 3". Each card shows gains (cream) and costs
   (red) on separate lines. The wallet sits top right with "never lowers your score". Buttons: Skip all three (+250),
   New three (reroll), Back.
2. **Upgrade in the field.** Once an ante's target is met, the pick is yours at once. The readout says
   "PERK READY · TAB" (or SELECT). On foot, Tab or Select opens the chart as PERK EARNED; the game pauses while it is
   open. Later keeps it for the break. It is that ante's one pick; the free ticket stays at the break. Since the follow-up
   below, Tab or Select opens an UPGRADES page with the shop as well. Not
   on the last ante. In the seat Select is still Floodlights.
3. **Rerolls**: 150 points × the ante's value, doubling each time in the same break, from the wallet only.
   **Shop prices** follow the ante's value (250 becomes 390 at ante 3), and so do the skip money and the scratch
   ticket's cash, so points and prizes keep meaning the same thing all run.
4. **Sized point pop-ups** (`Ante/PopFeedback.cs`) and the count-up's last coin louder for bigger awards. "Big" is
   the same deed at every ante: an award worth a fifth of ante 1's target, at that ante's value.
5. **Pace tick** on the target bar (`Ante/TargetPace.cs`), with "n/MIN BEATS THE CLOCK" when behind. No call in
   the first 30 s or while the clock waits on the pad. The "+points" lines now start a row lower (-142) to make room.
6. **Recap** (`Ante/RunRecap.cs`): "Cleared 3 antes, reached ante 4", where the points came from, the best moment
   and the style feats, then the records.
7. **Rebalance from the simulator**: Thin Air now halves health regen (it was none, and a trap) at ×1.10; Short Fuse
   ×1.20; Glass Cannon −10% health (was −15%); No Brakes +6% damage taken (was +10%); Quick Mend +40% regen with no
   health cost; Fleet Foot +3% damage taken (was +5%).
8. **`tools/run-sim`**: simulates runs over the real rules (see its README).

Constitution principles served: 1 (fun core: competence feedback), 2 (agency: field picks, rerolls, the whole set in
view), 3 (learnable: pace said plainly), 5 (legible risk: costs on their own lines), 7 (cognitive load: one chart,
three cards), 9 (ethical: no staged near misses, gain-framed envelope), 10 (each change has a stated hypothesis and
test below).

## Simulator results (1000 runs a row; guessed players, not a playtest)

Full tables: project files `orbital-ante/fun-psychology/run-sim-after-changes.md` (before:
`run-sim-first-pass.md`).

- Overtime rule: first-time median ante 3 (half reach ante 4, by calibration); Average clears all 9 in 90% of runs,
  Skilled in 100%. **Open question for Matthew:** that spread is large. Either the made-up Average player is too
  good, or later targets are easy for a practised player. Targets are unchanged (you set them).
- Field picks add a little (Average 8.56 → 8.62 antes cleared), as expected: the perk works for the rest of the ante.
- The model player never rerolls (the draft already favours families it holds), so whether rerolls are a useful
  sink is untested. The wallet still grows past what the shop sells by ante 7 (about 15,500 points at the break).
- Hazards now trade: Hunted costs about one ante and pays +4% score; Thin Air is nearly free (+9% score); Short Fuse
  is about neutral. Thin Air may need a bit more bite once it has been played.

## Tests

- Rule tests: 2,663 passed, 1 skipped (`dotnet test tools/rules-tests`). New: `StarChartTests` (8),
  `UpgradeScreenRulesTests` (8), `PopFeedbackTests` (5), `TargetPaceTests` (5), `RunRecapTests` (3).
- Compile check of the whole runtime against Unity reference assemblies: clean apart from the two known
  `excludeLayers` lines that only newer Unity has.
- **Not run in Unity:** the star chart's look and layout, controller navigation on it, the pause hand-off, pop sizes on
  screen, the pace tick, the new sounds' loudness. PlayMode tests were not run.

## What is still not built from the research's ten

- A pacing director (lulls after fights).
- Hitstop by event size (melee has its own; gun limb pops unchanged).
- A guaranteed off-family offer in every draft (the draft weights held families; not changed).

## Playtest on the Deck

1. In Desktop Mode open Konsole and run: `cd ~/Orbital-ante`, then `git fetch`, then `git checkout claude/playtest`,
   then `git pull`.
2. Open the project in Unity Hub and press Play in the main scene (or run `tools/check-in-unity.sh` first for the tests).
3. Start a new run and play to ante 1's target. Check that "PERK READY · SELECT" appears top right.
4. On foot, press Select (Tab on a keyboard). The star chart should open and the game should pause.
5. Move left and right over the three cards. The star each would light should pulse in its constellation, and the
   card should lift with an amber edge.
6. Press B. The chart closes and play resumes. Press Select again and take a card. A note should name the perk.
7. Bank the ante. At the break, check "Perk:" shows the one you took, and that the ticket and shop still work.
8. In a later ante, wait for a break with a pick, open "Pick a perk", and try "New three" if you can afford it.
9. Watch the "+points" lines: a plain kill should be small, a style kill bigger and blue with a chime.
10. Let a target fall behind pace and check "n/MIN BEATS THE CLOCK" under the bar. Press Tab or Select a second time on the chart and check it closes.
11. Die or run out of time and read the recap.

Solo protocol (from the research): before playing, write down what you expect for each item above. After each
session, note the best moment, the worst moment, the moment you nearly quit, and whether you'd want any of the time
back. Report it as "one player, n runs".

## Follow-up: the shop in the field (21 Nov 2026)

Matthew said yes to Select out on a world opening the shop as well as the perk pick. On foot, Tab or Select now
opens a short UPGRADES page at any time in an ante (paused, clock stopped): "Pick a perk" once the target is met
(it opens the star chart; Later comes back to the page), the shop, and Back to the game. Before the target the perk
row says how many points are still to go. The free ticket and Raise the stakes stay at the break. Shop prices are
the ones for the ante being played, so a level bought mid-ante 3 costs what it would at ante 3's break, a little
more than at ante 2's: buying now or waiting for the break is a small, visible trade. The goal card and How to
play now say "On foot, Tab opens the shop at any time, and this ante's perk once its target is met."

Problem: points sat unused for a whole ante and the wallet grew past what the shop sells. Hypothesis: a shop one
press away gets points spent when they would help. Smallest test: on the Deck, play two antes and note whether you
opened the shop mid-ante and why. Acceptance: the wallet at each break is under about 1.5 times the cheapest
level left, and no mid-fight shop visit felt like cheating. Not run in Unity.

Review (Opus agents): the page could open during the death hold and spend on a lost run (now shut while dying);
Tab / Select did not close the page it opened (now it does); prices rising at the next ante was not said (now on
the page and the shop); the oxygen line now says "suit air lasts 10% longer a level", since the gauge's per cent
does not move. No per-frame cost added.

## Agent review

Three specialist agents read the code (run on Opus because Fable was rate limited). Everything below was fixed
before the commit unless marked open.

- **unity-reviewer:** Tab / Select could never close the chart, because pausing turns the Gameplay map off (the
  Upgrades action alone is now switched on while the chart is up). PERK READY sat on top of the world task line
  (moved to its own row, shared with the pace words, which never show at the same time). The board was cut off on
  screens wider than 16:10 (the chart's canvas now uses Expand). With no offers the chart was never redrawn. Hiding
  the chart could unpause under an open pause menu (now checks `PauseMenu.Instance`). The key name did not follow a
  switch between keyboard and pad. Tab no longer opens the chart in the cabin, where nothing on screen says a pick
  is ready.
- **game-designer:** the envelope line had dropped the "a run that ends sooner leaves it sealed" rule (a hidden
  rule; restored, still framed as a gain). Skip and scratch cash did not scale while prices did (now both do).
  The target-met note told a seated pilot to press Select, which is Floodlights there (now says "on foot"). A
  restart from the break counted one ante fewer cleared (fixed). Big pops drifted rarer each ante (now the same
  deed). "NEED" read as a fail under overtime (reworded). Hunted's text was muddled and Thin Air's code comment was
  stale (both fixed). No dark pattern found. Open: whether Select in the field should also hold the shop (I kept it
  to the perk, so the break still matters), and Thin Air and Short Fuse being cheap under overtime.
- **deck-performance:** the sky texture was built at boot (now on the first open, and faster), the pace words
  allocated every frame while behind (now built only when the numbers change), the pace tick re-batched the readout
  every frame (now moves a whole pixel at a time), the pulse rebuilt graphics every frame (now `SetAlpha`). Open: the
  3D world keeps rendering under any full-screen page (pause menu too); a chart move rebuilds about 60 objects
  (paused, a few ms).
