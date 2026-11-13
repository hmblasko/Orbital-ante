# Ante and point system - design and preparation

Status: **rules, numbers and tests only. Nothing here is switched on in the game.** The ante loop is Milestone 14, in Phase 2, after the Phase 1 sign-off and Milestone 13. This document and the code beside it are the preparation Matthew's point-system handoff of 13 Nov 2026 allows before then: balance data, plain C# scoring maths, event contracts and rule tests. No gameplay file was changed, no scene, no HUD, no save layout.

Source of truth: the build prompt (`CLAUDE.md` Part B) first, then the handoff. Every number below is the handoff's unless marked **(mine)**.

## What exists now

All in `unity/Assets/OrbitalAnte/Scripts/Runtime/Ante/`, namespace `OrbitalAnte.Ante`. Everything except the last file is plain C# with no Unity types.

| File | What it is |
|---|---|
| `AnteBalance.cs` | Every number of the system in one class. Provisional. |
| `ScoreMath.cs` | The award formula, in decimal arithmetic. |
| `RunLedger.cs` | The three counts of points. |
| `ClaimRegistry.cs` | What has already paid out, and the counters behind caps. |
| `ScoreService.cs` | The event contract (`ScoreEvent`) and the one place events become points. Keeps the score log. |
| `AnteDirector.cs` | Which ante, the timer, the stopwatch, how an ante ends, what Select shows. |
| `AttributeShop.cs` | The four attributes. |
| `Perks.cs` | Perk data, the six example perks, the pick-1-of-3 draft. |
| `ScratchOff.cs` | The scratch-off and its published odds. |
| `AnteRun.cs` | All of the above assembled into one object, plus the save state (`AnteRunState`). |
| `AnteBalanceData.cs` | The ScriptableObject that holds an `AnteBalance` for the Inspector. Falls back to code defaults if the asset is missing. |

Tests: `Tests/EditMode/AnteScoringTests.cs`, `AnteDirectorTests.cs`, `AnteRewardsTests.cs`, `AnteEdgeTests.cs`.

## The rules, in plain words

**Three ledgers.** `AnteEarned` is compared with the ante's target and starts from zero each ante. `LifetimeRunScore` is the leaderboard number and never goes down. `SpendablePoints` is the wallet. A scoring event adds to all three. Buying or wagering takes from the wallet only. Scratch-off cash and casino winnings add to the wallet only. Score beyond a target does not carry into the next ante; the wallet does. There is no second currency.

**Award.** `Round(Base x K(ante) x (1 + min(0.25, perk bonuses)) x Skill x Risk)`. Skill is 1.0, or 1.1 when the game itself verified a feat, ceiling 1.2. Risk is 1.0, or 1.15 for a risk the player chose beforehand, ceiling 1.25. Halves round away from zero. The maths is done in decimal, so a seed gives the same whole numbers on a Steam Deck as on a desktop. Enemy tier is in the base points and is never looked at again.

**Nothing pays twice.** Every scoreable thing has one claim id for the whole run, built from seeded world ids (`enemy:mars.base2.guard7`, `landmark:mars.canyon`, `landing:europa`, `bounty:poster12`). Claims are part of the save. A bounty has one id whether it comes back dead or alive. Anything that respawned is flagged by the reporting system and refused. An event with no id is refused. There is no event at all for hits, limbs, disarms, time survived, distance flown or scans.

**Antes.** Nine targets. The timer stops while paused and between antes. A stopwatch adds 30 seconds, at most 90 per ante. Timer out short of the target ends the run. Dying ends the run.

**Select and Start.** Start stays Pause. Select only ever opens a screen: a read-only status view during an ante, the upgrade screen between antes. Ending an ante early is a separate, confirmed action (`ConfirmAdvance`), so Select cannot end one by accident. A test presses Select twenty times in each mode and checks nothing moved.

**Between antes.** Optional attribute purchases, one free pick from three different perks, one free scratch-off. The draft and the ticket are rolled from streams that depend only on the run seed and the ante number, so saving and reloading cannot re-roll either.

## Numbers (all provisional, all in `AnteBalance`)

| Ante | Target | K | Target in ante-1 points | Per minute at 300 s |
|---|---|---|---|---|
| 1 | 850 | 1.000 | 850 | 170 |
| 2 | 1,100 | 1.245 | 884 | 177 |
| 3 | 1,450 | 1.560 | 929 | 186 |
| 4 | 1,850 | 1.945 | 951 | 190 |
| 5 | 2,350 | 2.400 | 979 | 196 |
| 6 | 3,000 | 2.925 | 1,026 | 205 |
| 7 | 3,700 | 3.520 | 1,051 | 210 |
| 8 | 4,550 | 4.185 | 1,087 | 217 |
| 9 | 5,500 | 4.920 | 1,118 | 224 |

The last two columns are arithmetic, not a test result. They divide each target by K to show how much real work an ante asks for.

Base points: tier 1 / 2 / 3 enemy 45 / 85 / 150, elite 260, ship destroyed 180, hijacked 260, bounty dead 260, alive 420 (alive also flags goodwill for the posting faction), new moon 110, new planet 150, landmark 120, cave or ruin objective 170, satellite repaired 180, dish network 260, difficult landing 100, optional mission 250 to 550, under-threat recovery 80.

Attributes, four levels: damage 250 / 450 / 700 / 1,000 for +8% each (added); movement and jump the same prices for +6% each (player only, never the ship); oxygen 200 / 350 / 550 / 800 for +10% each; defense 250 / 450 / 700 / 1,000 for 6% less incoming damage each (compounding, so level 4 is about 22% less).

Perk draft: 70 / 25 / 5 common / uncommon / rare per slot. Scratch-off: 50% +150, 25% +300, 15% repair or refill voucher, 8% temporary tactical perk, 2% cosmetic token.

## Things I noticed that you should know

1. **The 2.25 ceiling can never be reached.** With every smaller cap at its limit the multiplier is 1.25 x 1.2 x 1.25 = 1.875. I read the 2.25 as a safety net over perk x skill x risk and kept K outside it, because K reaches 4.92 by ante 9 and a 2.25 ceiling that included K would flatten the whole scale from ante 5 on. The ceiling is in the code and tested, but today it does nothing.
2. **The targets ask for only about a third more real work in ante 9 than in ante 1** (1,118 against 850 ante-1 points). So nearly all of the difficulty has to come from the world: more and tougher enemies, retakes, coalitions, hazards. That matches "not just HP", but it means the targets alone will not make first-timers fail around ante 3 to 4. Whether they do depends on Phase 2 content that does not exist yet.
3. **Whether 170 base points a minute is fair cannot be known yet.** It is 19 tier-1 kills, or about seven landmarks, in five minutes. Flight between bodies is Milestone 10 and is not built, so there are no real travel times to check that against. I have not run any simulation, because a simulation of travel that does not exist would be made-up numbers.
4. **Wallet size.** Clearing all nine targets with nothing to spare earns 24,350. Maxing all four attributes costs 9,100.

## Choices of mine, flagged

- **Salvage has no value.** The handoff lists salvage as a way to earn but gives no number. `SalvageDelivered` exists and scores nothing until you give one. The build prompt's "bonus for clearing all bases on a planet" has no number either and has no event yet.
- **Caps the handoff asks for without numbers (mine):** under-threat recovery pays at most 3 times per ante; a retaken base pays for at most 2 retake missions per run, each scored as an optional mission; no perk stat can be stacked past 40% either way.
- **"First-time wreck discovery"** is a Landmark event tagged `wreck`, since there is no separate wreck value.
- **Perk names, tags and rarities (mine, placeholders).** Only the six example effects from the handoff exist. The real perk list is Milestone 14 content.
- **The example perk list is too short for a full run.** All six are unique, so a nine-ante run would see three choices after antes 1 to 4, then two, then one, then no draft after antes 7 and 8. When nothing is left the draft is skipped rather than shown empty. A full run needs at least 10 perks for three choices every time; the real list is Milestone 14 content.
- **Free picks not taken are given up** when the next ante starts. The upgrade screen should warn before leaving.
- **Attributes can only be bought between antes.**
- **End condition meanings.** OnTimer: the ante always runs its full time. Hybrid: you may leave early once the target is met, otherwise the timer decides. OnPlayerAdvance: once the target is met there is no more time pressure and you leave when you like; timer out before the target still ends the run.

## Open questions (yours)

1. **Ante length and what ends an ante.** The data says 300 seconds and Hybrid as a stand-in so tests can run. All three modes are built and tested. Not final until you say.
   One thing to weigh: in OnPlayerAdvance the ante stays open after the timer, and fresh things still score. Nothing can be farmed (each thing pays once), but a leaderboard chaser could stay and sweep up every remaining reward with no deadline, which pulls against "varied intensity" and "no grind". Hybrid and OnTimer do not have this. If you pick OnPlayerAdvance, I would stop scoring once the timer is out, and this needs a playtest either way.
2. **Ninth scratch-off.** The data says between antes only (eight tickets in a full run). A ticket at victory could still pay cash or a voucher that has nothing left to be spent on, so only the 2% cosmetic token would matter. Both settings are built and tested.
3. A point value for salvage, and for clearing every base on a planet.

## How future systems plug in

A world system reports a completed thing through one door and never works out points itself:

```csharp
ScoreResult result = anteRun.Report(new ScoreEvent
{
    claimId = "enemy:" + seededSpawnId,   // stable for the run, same after save and load
    kind = ScoreEventKind.EnemyTier2,
    isRespawn = cameBackAfterBeingCleared,
});
```

`ScoreService.Submitted` is the hook for the HUD count-up, notifications and telemetry. `ScoreService.Log` is the score log for the developer balance panel. `result.grantsGoodwill` is the hook for diplomacy.

## Not built, on purpose (Milestone 14)

- The MonoBehaviour that owns an `AnteRun`, ticks it and feeds it `GameSession.Paused`.
- `AnteRunState` as a field of `SaveData` (version bump and migration step).
- One line in `ProjectSetup.cs` to create the `AnteBalance` asset. Until then the code defaults are used.
- Select bound in `GameInput` (it is reserved, unbound, today), the upgrade screen, HUD, notifications.
- The developer balance panel (seeds, quotas, events, score log).
- Perk machines, the stopwatch item, vouchers, temporary perks, cosmetic tokens, casino, bonus missions, faction anger.
- The 10,000-run simulation. It needs real traversal, encounter and resource models first.
- Anything that reads the starting resource values (HP 100, hull 160, fuel 80 of 100, cabin oxygen 60 of 95, suit oxygen 55 of 65, tie-up 3 shots). They are stored only.

## Constitution

- Legible risk and reward (5): one formula, published scratch odds, perk text written from the effects so it cannot disagree with them.
- Learnable challenge (3): targets and K are fixed tables, not hidden scaling; no health inflation anywhere in this system.
- Ethical design (9): no re-rolling by reload, no farms, no second currency, odds shown.
- Evidence-based iteration (10): every number is in one asset so it can be tuned from play, and none is claimed to be balanced.

## Tested, and not

**Run for real (13 Nov 2026, `dotnet test`, .NET 8, outside Unity): 93 new rule tests, all passed**, alongside the 10 existing `SeededRandom` tests they depend on. They cover: the formula and every cap; deterministic rounding; the handoff numbers; the three ledgers and spending separation; claims across save and load and across antes; respawns; caps; alive and dead bounty; ante thresholds and resets; all three end conditions; pause; stopwatch; Select; attributes; draft distinctness, determinism, uniqueness, usefulness and rarity distribution (200,000 rolls); scratch odds (200,000 rolls), wallet-only cash and no re-roll; independent streams; the final-ante ticket setting.

**Not verified:**
- Nothing has been compiled or run in Unity. `AnteBalanceData.cs` was compiled against hand-written stand-ins for Unity's types.
- Save and load was tested by copying the state object in memory, not by writing it through `SaveSystem` and Unity's JSON.
- No balance claim of any kind. No simulation was run. No playtest exists.
- Not tested because the systems do not exist: faction anger, route feasibility, resource recovery, controller navigation at 1280x800, Steam Deck performance.

## What you need to do

Nothing is required. Pulling this changes nothing you can see in the game.

If you want to see the tests pass in Unity:

1. Open the project in Unity.
2. In the top menu choose **Window**, then **General**, then **Test Runner**.
3. Click the **EditMode** tab.
4. Click **Run All**.
5. Look for `AnteScoringTests`, `AnteDirectorTests` and `AnteRewardsTests` in the list. Each should have a green tick. If any line is red, click it and send me the text that appears at the bottom.
