# Bonus missions (25 Nov 2026)

Matthew asked (25 Nov 2026, given to Codex and to Claude): "refine the game until it is perfect ... playable and enjoyable and has clear and fun goals. Keep in mind behavioral psychology ... I want a point system and goals that make the game engaging. I also want you to debug the game." Claude and Codex split the work: Codex takes the debugging pass, and Claude builds the one piece of Part B's point system that was still missing, the bonus missions of section 5 ("Bonus missions unlock after each ante for extra points").

**Nothing here has been run in Unity or played.**

## Problem, hypothesis, test (constitution rule 10)

- **Problem.** The ante target is the only goal with a name. Everything else is "do whatever scores". The research we already have ("Behavioural psychology of game fun", `docs/reports/fun-psychology.md`) says specific, slightly hard goals hold people better than "do your best" (goal-setting theory). It also says a visible count toward a goal pulls players on as it nears the end (the goal gradient). Its section on autonomy adds that a goal works best when the player picked it.
- **Hypothesis.** One optional, named goal per ante, chosen from two at the break, gives each ante a small story ("this ante I'm hunting on Mars"). It also pulls players toward things they might skip: other worlds, style kills, masts, salvage. Nothing is lost by refusing or failing one.
- **Smallest test.** Two offers at every break, one kind each, four numbers on the TaskConfig asset.
- **Accept if** a player on the Deck can say what their bonus mission is without looking, takes one at most breaks, and doesn't call any kind unfair or impossible.

## What it does

1. **At the break**, a new row: **Pick a bonus mission (2 on offer)**. It opens a page with two contracts of different kinds, each with its points and one sentence. There is also **No bonus mission**. Taking one puts it on the break row ("Bonus mission: BOUNTY ON MARS (+430)").
2. **Kinds** (only things every world already has):
   - **Bounty on <world>:** take down 5 enemies there (one more every three antes, up to 9; a bigger bounty pays more). A world with fewer enemies left than that is never named.
   - **Survey <world>:** land the ship on a world you haven't landed on this run (pays the least, because the first landing pays too).
   - **<Style> kills:** 3 ONE SHOT, LONG SHOT or AIRBORNE kills, or 1 TAKEN APART. MELEE only comes up if you hold a melee weapon, since those are rare.
   - **Signal jammer:** bring down a signal mast, anywhere.
   - **Salvage run:** bring a salvage crate aboard, anywhere.
3. **Named worlds** come from the nearest few planets and moons to you when the break opens, so a contract never asks for a trip the clock can't fit. The Sun, asteroids and gas giants are never named.
4. **In the ante**, a line under the readout shows it: `BONUS · BOUNTY ON MARS 3/6 ~+430` (the "~" because perks and the stakes are added when it pays). Each step plays a tick. It pays once, the moment it's done, as a big pop-up ("+430 BONUS MISSION: BOUNTY ON MARS") with a reward sound and a note in the middle of the screen. The line then turns gold and says DONE. The map labels the named world "· BONUS MISSION".
5. **It lasts one ante, overtime included.** At the next break you're told what came of it ("done: +430", or "not finished: no loss, it just ends").
6. **Points.** Contracts pay through the existing mission scoring (`OptionalMission`): base 260 to 400, times the ante's value, plus perks and the stakes like any award. That's about a third of an early ante's target: worth taking, but never a substitute for playing.

Constitution principles served: 1 (fun core: a reason to go and fight somewhere), 2 (agency: pick one of two, or none), 4 (discovery: surveys send you to new worlds), 5 (legible risk and reward: points shown before you pick), 7 (cognitive load: one line, one goal), 9 (ethical: no penalty, no streak, nothing outside the run).

## Code

- `Tasks/ContractRules.cs` (plain C#): `ContractRules` (dials on `TaskConfig.contracts`), `Contract` (progress, words), `ContractBoard.Offer`.
- `Tasks/AnteLoop.Contracts.cs` (a partial of `AnteLoop`): offers, the break page, the readout line, payment, `ContractBody` for the map.
- `TaskDirector`: reports kills, touchdowns, masts and delivered crates (after their own scores). `NearbyWorlds` and `HoldsMelee` are what the offers are drawn from.
- `AnteLoop.cs`: the "+points" lines move down a row (-142 to -164) for the contract line at -140.
- `SystemMap`: the named world's label.
- `PauseMenu.ShowChoices` takes a button height; the break uses 56 px rows when all seven rows are there, so the card still fits 1280 x 800.
- `ShipHud`: the damage list outside starts at -272, under the moved "+points" lines.

## Tests

- Rule tests: 2,711 passed, 1 skipped (`dotnet test tools/rules-tests`). New: `ContractRulesTests` (13): two kinds per break, the same offers for the same seed, named worlds only from the nearest few, a survey never on a world you've landed on, melee only when held, counts per kind, the bounty's growth, every reward inside the mission range.
- Runtime compile check against Unity reference assemblies: clean apart from the two known `excludeLayers` lines.
- **Not run in Unity:** the break page and its layout, the readout line, the map label, the sounds, and whether the offered worlds feel near enough. `tools/run-sim` doesn't model contracts yet.

## Playtest on the Deck

1. In Desktop Mode open Konsole and run `cd ~/Orbital-ante`, then `git fetch`, then `git checkout claude/playtest`, then `git pull`.
2. Open the project in Unity Hub and press Play in the main scene.
3. Start a new run and bank ante 1. At the break, check that the row "Pick a bonus mission (2 on offer)" is there, above "Start ante 2".
4. Open it. Check both offers read clearly and show points. Pick one. The break row should now name it.
5. Start ante 2. Check the line `BONUS · ...` under the readout, top right, and that the "+points" lines appear below it without overlapping.
6. Open the map (if the contract names a world). That world's label should say "· BONUS MISSION".
7. Do the contract. Each step should tick; the last should pay with a big pop-up and a note, and the line should go gold and say DONE.
8. At the next break, check what it says about the contract. Then try an ante where you take one and don't finish it.

Before playing, write down which kinds you expect to take and which to skip. After each session, note which contract you took, whether it changed where you went, and whether any felt unfair. Report it as "one player, n runs".

## Agent review

One reviewer agent read the commit through the unity-reviewer, game-designer and deck-performance briefs (run as a general-purpose agent on Opus). It found no lifecycle, payout or body-index bugs. What it did find, all fixed before the PR:

- The break card with seven rows was about 892 px tall on an 800 px screen. It now uses 56 px rows and a 176 px body when all seven are there (about 780 px). **Unverified until seen in Unity.**
- A bounty could name a small moon with fewer enemies than it asked for (Deimos and Phobos have one camp). `EnemySpawner.EnemiesLeftAtLeast` now counts what is left, and such worlds aren't named.
- The readout line built a string every frame. It now rebuilds only when the count changes.
- The fourth "+points" line ran 10 px into the ship's damage list outside. The list moved to -272.
- Rewards: a survey also pays its first landing, so it now pays the least (250) and says so; a bounty pays 40 more for each enemy over 5; the readout's figure is marked "~" because perks and the stakes are added at payout.
- Offers are capped at 5 so the page's buttons always fit, and the page body has room for its three lines.
