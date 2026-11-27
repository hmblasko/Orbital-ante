# Critique loop, round 5 (27 Nov 2026)

Rounds 1 to 4 merged into `claude/playtest` on 27 Nov (PR 97, Matthew's card). ChatGPT's reply has not been started
yet, so round 5 is a new finding of Claude's own in an area the loop had not looked at: how the suit tells you that
you are running out of air. **Nothing here has been run in Unity.**

## Finding 15: the deadliest step in the suit was silent

| | |
|---|---|
| Problem | The suit had one warning tone for four troubles (jet fuel out, jets burning air, air low, no air). It sounded when any trouble began and then every 12 s for as long as any trouble lasted. So (a) when low air became no air, 14 s from death, nothing new sounded: the next tone could be up to 12 s away, and the whole countdown got one tone at most two; (b) an empty jet tank beeped every 12 s for as long as you stayed outside, which teaches the ear to ignore the same tone that later means "you are dying" |
| Principle | Legible risk (5), learnable challenge (3): a death the player was not told about is blamed on the game. Alarm fatigue: a warning that repeats with nothing to act on stops being heard (cognitive load, 7) |
| Evidence | `Spacewalk/SuitFx.cs` (the old `_wasAlarm` / `_nextWarn` lines), `SuitReadout.IsAlarm`, `SuitRules.airGraceSeconds` 14, `airLow` 0.25 of 8 minutes |
| Change | `Spacewalk/SuitAlarmClock.cs` (plain C#). Every step up in danger sounds at once. How often the tone comes back says how bad it is: a caution (fuel out, jets on air) is said once and its words stay on the visor; low air returns every 30 s (about four tones in its two minutes); no air sounds every 2 s through the grace (about seven tones), a little louder. Getting better is silent. It runs on game time, so a pause never ages it |
| Not changed | The tone itself (same clip, the suit's family of tones, per the round 1 sound rules). No new audio |

Hypothesis: with the countdown audible, fewer deaths by suffocation will come as a surprise, and the low-fuel
caution will stop being background noise. Smallest test on the Deck: stand outside until the air runs out with the
sound on and eyes off the gauge; you should know, by ear alone, when low air starts and when the last seconds start.
Acceptance: Matthew can say which state he is in without looking. If seven tones in the grace feels like nagging,
`SuitAlarmClock.OutEvery` is the dial.

## Tests

- `dotnet test tools/rules-tests`: 2,784 passed, 1 skipped (five new tests in `CritiqueRound5Tests`).
- Runtime compile check against Unity reference assemblies: clean apart from the two known `excludeLayers` lines.
- Not sent to the specialist agents: the engine-facing change is three lines in `SuitFx.Update` calling a tested rule.
- Not run in Unity: how the faster tone sounds over a fight, and against the armour pulse when both are on.

## Still open

- ChatGPT's turn: paste `chatgpt/critique-loop-round-3.md` in the project files (rounds 1 to 4; round 5 is on this branch).
- A 24 pt "big" pop in a 24 px row (needs a real screen).
- The PERK READY line is hidden while walking the cabin, though Upgrades works there (the ship's damage list owns that corner aboard).
