# Critique loop, round 1: Claude (26 Nov 2026)

Matthew (26 Nov 2026): "I want you and ChatGPT to go back and forth working together to criticize the game in order
to refine it to the best standards. Your ultimate goal is considering behavioral psychology in all elements of the
game such as sound visuals goals controls and work together to refine the game."

**How the loop works.** Each round, one side critiques and builds, the other reviews that work and adds its own
critique. Claude goes first (this report and its PR), ChatGPT (Codex) answers in `docs/reviews/critique-loop-1-chatgpt.md`
on a `codex/` branch made from this one, Claude then checks every ChatGPT finding (tests, compile, the code itself)
and builds what holds up in round 2. Findings from either side are leads until checked.

**Nothing here has been run in Unity or played.** This round is a reading of the code and the earlier research
(`docs/reports/fun-psychology.md`, `docs/reviews/engagement-goals-and-rewards.md`), not a playtest. Every sound below
was measured by meter, never heard.

## Where the game already stands (so neither side redoes it)

Goals and rewards have had three psychology passes (roguelite fun, fun psychology, playable loop): performance-tied
pop sizes, honest pace tick, overtime instead of instant failure, printed odds, no streaks or timers outside a run,
costs written on every perk. Hit feedback is sized (limb, torso and kill hitstop; a ding per hit kind). Damage has
direction arcs, edge glow, visor cracks and a panned hurt sound. Aim assist is slowdown only. So round 1 looks
for the gaps that are left, mostly at the edges between systems: what the player is told *before* something
happens to them.

## Critique

The lens: what does the player need to know, when, and through which sense, so that a loss feels fair and a win
feels earned (competence and legible risk; constitution rules 1, 3, 5, 7, 9)?

| # | Area | Finding | Principle | This round |
|---|---|---|---|---|
| 1 | Visuals + sound | Every armed enemy already waits a beat between seeing you and shooting, but nothing shows that beat. Being seen reads as being shot out of nowhere, and sneaking (crouch, prone, sneak) cannot be learned because its failure has no signal. Stealth games teach detection with a warning before the punishment. | Learnable challenge (3), legible risk (5) | **Built**: amber arc toward the enemy for its beat, and a suit "threat sensor" sting when a quiet stretch ends |
| 2 | Sound + goals | Restless camps (refine pass 3) send a squad after 35 s of quiet. Nothing tells the player, so it feels like enemies teleporting in. The open question from that pass was "a tell when a lull squad is sent". | Legible risk (5), varied intensity (8) | **Built**: a garbled enemy radio call, panned toward the squad, as it sets off |
| 3 | Goals + sound | The clock turns red at 30 s in the top-right corner, where the eyes are not during a fight. A timer that runs out unseen feels like the game's fault. | Legible risk (5), cognitive load (7) | **Built**: one soft heads-up at 30 s and a dry tick each of the last 10, only while short of the target |
| 4 | Controls | A jump only went if Jump was pressed on the very physics step the feet were found. A press just before landing or just after an edge became a jet burst instead. On rough ground the feet flicker, so jumps were eaten at random. Inconsistent controls make players blame themselves. | Competence (SDT), fun core (1) | **Built**: 0.12 s press buffer and 0.12 s coyote time |
| 5 | Sound | Overtime starts with `player.suit.alarm` (designed as the suit's low-air and breach alarm: two falling notes a tritone apart), while the real low-air warning is a separate code-made beep in `SuitAudio` that is also two falling notes. Two dangers with one shape slow reading ("is my air low?"). | Cognitive load (7) | Open: give overtime its own sting and make the suit's air warning the manifest alarm (audio is Claude's, round 2). This round's clock sound was kept out of that shape on purpose |
| 6 | Sound | When the last armour plate is amber there is no sound change. The cracks and plate gauge are visual only, and the eye is on the target. | Legible risk (5) | Open: a low-armour breath or heartbeat layer, round 2 |
| 7 | Visuals | The top right now holds ante + clock, score / target bar with pace tick, PERK READY or pace words, up to four "+points" lines, the world task line, a hazard note, and outside the ship's damage list at -250. That is a lot of moving text in one corner. | Cognitive load (7) | Open: ChatGPT to audit from the code (rows, sizes, what moves) |
| 8 | Controls | Select means Floodlights in the seat and Upgrades on foot; Jets Down means crouch / prone on the ground and down-thrust in the air. Context-dependent buttons cause mode errors unless the prompt on screen always names the current meaning. | Cognitive load (7), competence | Open: ChatGPT to check every place a prompt names these buttons |
| 9 | Visuals | Red is used for damage, the warning clock, perk costs and overtime. Costs on perk cards in danger red frame a trade-off as a threat (loss aversion), which may push players away from fair picks. | Agency (2), legible risk (5) | Open: ChatGPT to weigh in; any colour change is the art lead's call |
| 10 | Goals | Bonus missions (PR 89) are built but not merged, so the "something to chase after each ante" layer is not in the testing branch yet. | Agency (2), replayability (6) | Waiting on Matthew's merge |

## What changed

1. **"You have been seen" warning** (`UI/ThreatCues.cs` rules, drawn by `UI/DamageFeedback.cs`). When an armed
   enemy that had not seen the player for its chase memory picks them out, `MannequinCombat` raises
   `CombatWorld.PlayerSpotted` (where its head is, its reaction beat, how long nobody had seen the player). The
   visor shows a short amber arc toward it on a ring just outside the red damage ring, held for the beat with a
   gentle pulse (never below 0.65), then fading. A squad seen together shares one arc (within 25 degrees). The
   sting `enemy.spotted` (a low thrum and two rising pips, panned) plays only if nobody had the player in sight for
   6 s and never twice within 4 s, so it is news when hiding fails and silent inside a running fight.
2. **Radio call** (`EnemySpawner.Restless` raises `CombatWorld.SquadSent`): the suit plays `enemy.radio.call`, a
   squelch, a few garbled syllables (never words) and the squelch closing, panned softly toward the squad.
3. **Clock by ear** (`Ante/ClockCues.cs`, called from `AnteLoop.Update`): `ante.clock.warn` (three quick low
   woodblock knocks, the same wooden family as the tick, so the ear files both under "the clock") at 30 s, `ante.clock.tick` (a dry woodblock) each of the last 10 s, louder each second, never faster. Silent once
   the target is met, while the clock is on hold, in overtime, between antes and outside a run.
4. **Jump grace** (`Spacewalk/JumpGrace.cs`, used by `Astronaut`): a press is kept 0.12 s and used on the first step
   with ground; ground counts 0.12 s after the feet leave it, once, and the jump's own take-off step is never taken
   for a landing. Upright on foot only; floating free, Jump is the jets as before.
5. **Sounds** (`tools/audio/sfx/alerts.py`, rendered with `python3 tools/audio/build.py sfx --only ...`). The rule for
   the file: a warning never sounds like a reward. Rewards are musical, bright and in D; these are dry, short and
   outside that ladder, so "I did well" and "watch out" stay two separate meanings. Each warning family has its own
   source and timbre: the clock is wood, the suit is electronic tones, enemies are their sensor pip and their radio.

| Sound | Length | Peak | Loudness | Where the energy is |
|---|---|---|---|---|
| `enemy.spotted` | 0.42 s | -5.0 dB | rms -17.3 | mostly mid (thrum + pips) |
| `enemy.radio.call` | 1.35 s | -6.0 dB | -23.7 LUFS | mid band (a handset) |
| `ante.clock.warn` | 0.50 s | -6.0 dB | -22.4 LUFS | mid |
| `ante.clock.tick` | 0.12 s | -6.0 dB | rms -21.7 | mid |

Measured by `tools/audio/table.py alerts`. Not listened to: the first thing to change after Matthew hears them.

Problem, hypothesis, smallest test, acceptance (constitution rule 10):
- Seen warning. Problem: being spotted is unreadable. Hypothesis: an amber arc plus a sting lets a player answer "who
  saw me and how long do I have?". Test: on the Deck, walk into a camp's sight three times, once crouched. Accept if
  you can point to the enemy before its first shot every time and the sting never plays mid-fight.
- Radio call. Test: stand still out of sight for 40 s near a camp. Accept if you hear the call before the squad
  arrives and can guess the direction.
- Clock. Test: let an ante's clock run under 30 s while short. Accept if you notice the heads-up without looking at
  the corner and the ticks do not feel naggy.
- Jump. Test: jump off a rock edge while running, and press Jump just before landing from a fall. Accept if every
  press jumps and no jump fires twice.

## Tests

- Rule tests: `dotnet test tools/rules-tests`, 2,709 passed, 1 skipped (was 2,698). New: `CritiqueRound1Tests` (11:
  sting rules, arc hold and fade, clock warn once and ten ticks, silence when met / on hold / overtime / between
  antes, tick volume, jump buffer, stale press, coyote once, take-off step, jets long after leaving the ground).
- Compile check of the whole runtime against Unity reference assemblies (UnityEngine.Modules 2021.3.33 plus the
  Input System and UI dlls): clean apart from the two known `excludeLayers` lines that only newer Unity has.
- **Not run in Unity:** how the arcs look and where they sit, whether the pans point the right way, the four sounds'
  loudness in the mix, how the jump feels. PlayMode tests were not run.

## On the Deck

1. In Desktop Mode open Konsole and run: `cd ~/Orbital-ante`, then `git fetch`, then `git checkout claude/playtest`,
   then `git pull` (once this PR is merged; before that, `git checkout claude/critique-loop-1`).
2. Open the project in Unity Hub and press Play in the main scene.
3. Land near a camp. Stay out of sight, then step into view. You should see a short amber arc pointing at the
   enemy and hear two quick pips before it shoots.
4. Hide behind a rock and wait about 40 seconds without fighting. You should hear a crackly radio call before a
   squad turns up.
5. Let an ante clock run down while short of the target. At 30 s you should hear three quick low knocks, then
   ticks for the last 10 s.
6. Run off a rock edge and press Jump just as you leave it. You should jump.
7. Tell Claude (or ChatGPT) what felt off. One player, n runs.
