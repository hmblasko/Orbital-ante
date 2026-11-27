# Critique loop, round 1: Claude (26 Nov 2026, redone on Fable 5.2)

Matthew (26 Nov 2026): "I want you and ChatGPT to go back and forth working together to criticize the game in order
to refine it to the best standards. Your ultimate goal is considering behavioral psychology in all elements of the
game such as sound visuals goals controls and work together to refine the game." Later the same day, after a first
pass on Opus 5.5: "Redo using fable".

**How the loop works.** Each round, one side critiques and builds, the other reviews that work and adds its own
critique. Claude goes first (this report and PR 91), ChatGPT (Codex) answers in
`docs/reviews/critique-loop-1-chatgpt.md` on a `codex/` branch made from this one, and Claude then checks every
ChatGPT finding (tests, compile, the code itself) and builds what holds up in round 2. Findings from either side are
leads until checked.

**Nothing here has been run in Unity or played.** This round is a reading of the code and the earlier research
(`docs/reports/fun-psychology.md`, `docs/reviews/engagement-goals-and-rewards.md`), never a playtest. Every sound
was measured by meter and never heard.

## What the redo changed about the first pass

The first pass built four things (seen warning, radio call, clock by ear, jump grace) and skipped the specialist
review the repository asks for. The redo ran that review (unity-reviewer and game-designer agents, both on Fable),
checked each finding against the code, and found the first pass wrong in eight places:

| First pass | What was wrong | Now |
|---|---|---|
| Amber arc timed on real time | The enemy's beat runs on game time. In Focus (world at 0.35 speed) a 0.6 s beat lasts about 1.7 s, but the arc was gone after about 1 s: the warning ended before the shot it warned of. A pause aged it away too | Arc runs on game time |
| Arc held for the enemy's plain reaction time | From cover the first shot comes at 0.7 of that, and a flanker's later, so the arc could outlast or undershoot the real beat | The event carries the beat that enemy will really be given (`FirstShotBeat`) |
| Sting after "6 s unseen" | 6 was tied to nothing. Enemies hunt for 8 s after losing sight, so the sting could re-arm mid-fight | Sting only when the player was really hidden: nobody had seen them for the chase memory, the same rule as "once spotted you are hunted" |
| Disarmed enemies' sightings did not count as "seen" | A sting could play mid-fight | They count |
| Radio call every time a squad was sent | A squad already on its way was called again every 20 to 35 s, and the call played for squads with no gun (nobody comes) | Only for a squad with a gun that was not already coming; panned fully; a note in words for a muted Deck |
| Clock: warn at 30 s, ten ticks rising in volume | 30 s is too late to act on. Ten rising ticks is a bomb countdown for an outcome that is only overtime (the threat was overstated), and in the pilot's seat nothing can be done about it | Warn with a minute left (the same moment as the guide's tip), five ticks at one volume, no ticks in the seat |
| Jump buffer everywhere | A Jump tap just before touching down from a jet descent became a hop | Presses are kept only on foot; in flight Jump is the jets |
| Two of my own new sounds sat below what the Deck's speakers can play | The low-armour pulse measured 71% of its energy under 60 Hz: silent on the Deck without headphones | Rebuilt with a knock the small speakers can play (4% under 60 Hz now) |

## Critique

The lens: what does the player need to know, when, and through which sense, so that a loss feels fair and a win
feels earned (competence and legible risk; constitution rules 1, 3, 5, 7, 9)?

| # | Area | Finding | Principle | This round |
|---|---|---|---|---|
| 1 | Visuals + sound | Enemies wait a beat between seeing you and shooting, but nothing showed it. Being seen read as being shot from nowhere; sneaking could not be learned | Learnable challenge (3), legible risk (5) | **Built** |
| 2 | Sound + goals | A squad sent after a long quiet arrived with no tell | Legible risk (5), varied intensity (8) | **Built** |
| 3 | Goals + sound | The clock is in a corner the eyes are not on in a fight | Legible risk (5), cognitive load (7) | **Built** |
| 4 | Controls | Jumps pressed a moment off the ground were eaten; inconsistent controls make players blame themselves | Competence (SDT) | **Built** |
| 5 | Sound | Overtime began with the suit's two-falling-note alarm; low air is also two falling notes. One shape, two dangers | Cognitive load (7) | **Built**: overtime has its own sound in the clock's family |
| 6 | Sound | "About to die" was told only by sight (plate gauge, cracks). The amber plate shows for any worn plate, so it is not a last-plates signal either (`VisorReadout.LastPlateWorn`) | Legible risk (5), fair attribution of failure | **Built**: a pulse on the last two plates that stops when the suit mends |
| 7 | Goals + sound | Meeting the ante's target, the whole point of an ante, played the same sound as a two-feat kill, and a bigger kill outranked it. Its one instruction could be written over by the next note | Goal gradient needs a clear arrival; peak salience | **Built**: its own sound, and notes that cannot be written over |
| 8 | Goals | In overtime, enemies near you are told where you are every 15 s. Nothing told the player: a hidden rule | Learnable challenge (3) | **Built**: you hear their radio each time |
| 9 | Goals | The death page ended on the shortfall or a record line, never on what to aim at next. A first death read "850 short of 850" | Peak-end; negative feedback lowers competence (Burgers 2015) | **Built**: plain last line "Next mark: clear ante n"; the empty shortfall is dropped |
| 10 | Visuals | Top right can hold ante + clock, score and bar, guide line, world task line, PERK READY or pace words, four "+points" lines and, outside, the ship's damage list: up to ten rows in one corner. A 24 pt big pop sits in a 24 px row | Cognitive load (7) | Open: proposal is a quiet/fight split (in a fight: ante, score, bar, PERK READY, two pops). ChatGPT to audit; needs eyes on a real screen |
| 11 | Controls | Death to playing again: 2.6 s burst hold + 0.8 s page hold, then one press to select and one to confirm, because nothing is selected until a press | Retry friction after failure | Open: auto-select the default when the hold ends. Not built blind: whether a held button would then fire needs Unity |
| 12 | Controls | Select means Floodlights in the seat and Upgrades on foot; Jets Down is crouch on the ground and thrust in the air | Mode errors | Open: ChatGPT to check every prompt that names them |
| 13 | Visuals | Red means damage, the late clock, overtime and perk costs. A cost in danger red frames a fair trade as a threat (loss aversion) | Agency (2) | Open: ChatGPT to weigh in; a colour change is the art lead's call |
| 14 | Goals | Bonus missions (PR 89) are built but not merged | Agency (2), replayability (6) | Waiting on Matthew |

Two decisions for Matthew that came out of the review (not changed here):
- The readout's "STREAK x n" is an in-run streak. Constitution rule 9 bans streak pressure; this one is inside a run
  and ends with it, which I read as allowed, but it is his rule.
- The prize envelope shows at every run start, including a same-seed retry, which lengthens the retry. Skip it on a
  retry?

## What changed

1. **"You have been seen" warning** (`UI/ThreatCues.cs`, drawn by `UI/DamageFeedback.cs`). An armed enemy that picks
   the player out after not having them in sight raises `CombatWorld.PlayerSpotted` with its head position, the
   beat it will really be given, and whether the player was hidden. The visor shows a short amber arc toward it on
   a ring just outside the red damage ring, held for that beat (game time) with a gentle pulse, then fading. A squad
   seen together shares one arc. The sting `enemy.spotted` plays only if the player was hidden, at most once in 4 s.
2. **Radio call** (`EnemySpawner.Restless` raises `CombatWorld.SquadSent`): `enemy.radio.call` panned toward the
   squad, plus the note "RADIO CHATTER: A SQUAD IS COMING TO LOOK". Only for an armed squad not already on its way.
   In overtime the same call plays each time the hunters are told where you are (no note, out on foot only).
3. **Clock by ear** (`Ante/ClockCues.cs`): `ante.clock.warn` (three low woodblock knocks) with a minute left,
   `ante.clock.tick` for each of the last five seconds at one volume, not in the seat. Silent once the target is
   met, on hold, in overtime, between antes and outside a run.
4. **Overtime's own sound** (`ante.overtime`: the clock's wood struck hard over a falling drum, then a short rattle).
   The ticks lead into it; the suit's air warning can no longer be taken for it.
5. **Pulse on the last plates** (`UI/VitalsCues.cs` `PulseClock`, played from `DamageFeedback`): `player.armour.low`
   starts at 20% armour and stops at 30% (two marks, so it never flutters), 57 beats a minute rising to 80 with no
   armour left, on game time. Taking cover is answered by the sound stopping. It is a heart, never an alarm.
6. **Target met** has its own sound, `reward.target` (a D major chord thrown open and one bell left ringing), and its
   note is important: `Ante/NoteBoard.cs` keeps an important note (target met, overtime) on screen for 3 s whatever
   arrives; the latest plain note waits and shows after it.
7. **Recap's last line** (`RunRecap.NextLine`): "Next mark: clear ante n." or, with every ante cleared, "Next mark:
   a higher run score." Never "so close". The "n short of target" clause is dropped when nothing was banked.
8. **Jump grace** (`Spacewalk/JumpGrace.cs`): a press is kept 0.12 s and used on the first step with ground; ground
   counts 0.12 s after the feet leave it, once. Presses are kept only on foot.
9. **Sounds** (`tools/audio/sfx/alerts.py`, `reward.target` in `rewards.py`). Rules for the file: a warning never
   sounds like a reward (rewards are musical and in D; warnings are dry and outside that ladder), each family has
   one timbre (the clock is wood, the suit is tones, your body is the pulse, enemies are their sensor pip and
   radio), and anything meant to be felt low must also have a part above about 200 Hz for the Deck's speakers.

| Sound | Length | Peak | Loudness | Energy under 60 Hz / 60-250 / above |
|---|---|---|---|---|
| `enemy.spotted` | 0.42 s | -5.0 dB | rms -17.3 | 0 / 28 / 72 % |
| `enemy.radio.call` | 1.35 s | -6.0 dB | -23.7 LUFS | 0 / 0 / 100 % |
| `ante.clock.warn` | 0.50 s | -6.0 dB | -22.4 LUFS | 0 / 0 / 100 % |
| `ante.clock.tick` | 0.12 s | -6.0 dB | rms -21.7 | 0 / 0 / 100 % |
| `ante.overtime` | 1.30 s | -4.0 dB | -20.6 LUFS | 0 / 70 / 30 % |
| `player.armour.low` | 0.46 s | -5.0 dB | -25.7 LUFS | 4 / 74 / 22 % |
| `reward.target` | 1.90 s | -3.0 dB | -11.4 LUFS | 0 / 0 / 100 % |

Measured by `tools/audio/table.py`. Not listened to: the first thing to change after Matthew hears them. The pulse
and the overtime drum are still mostly below 250 Hz and may be faint on the Deck's own speakers.

Problem, hypothesis, smallest test, acceptance (constitution rule 10) for each is in "On the Deck" below: each step
names what should happen, and a step that does not happen that way is the finding.

## Tests

- Rule tests: `dotnet test tools/rules-tests`, 2,714 passed, 1 skipped (2,698 before the round). `CritiqueRound1Tests`
  has 16: sting only when hidden, arc hold and fade, one warn and five ticks, no ticks seated, silence when met / on
  hold / overtime / between antes, the pulse's two marks and human rate, notes not written over, the recap's last
  line never dressing up a near miss, jump buffer, stale press, coyote once, take-off step, jets in flight.
- Compile check of the whole runtime against Unity reference assemblies: clean apart from the two known
  `excludeLayers` lines that only newer Unity has.
- **Not run in Unity:** how the arcs look and where they sit, whether the pans point the right way, every sound's
  loudness in the mix, how the jump feels. PlayMode tests were not run.

## Agent review

- **unity-reviewer** (read-only): found the arc's time base (certain), the repeated radio call (likely), the hop on
  a jet landing (likely) and the cover-path beat and disarmed-sighting gaps (minor). All five fixed. Checked and
  found sound: press timing between `Update` and `FixedUpdate`, the coyote branch, the event subscriptions across
  restarts, that the cue is alive in the solar-system game and never fires beyond engage range, the clock across
  stopwatch, Short Fuse, ante change and load.
- **game-designer** (read-only): judged the four first-pass changes (keep the arc and grace, change the sting's
  rule and the clock) and ranked six gaps; findings 6 to 11 above are its, confirmed in the code before building.
  Not taken: its one-shot armour cue (I kept a pulse because the armour mends, so the useful signal is one that
  ends when you are safe; ChatGPT is asked to argue the other side) and a guard against Jump also lighting the jets
  after a jump (that is how every jump has always worked: hold to fly).
- deck-performance was not run: the additions are a handful of compares a frame (see `docs/performance_budget.md`).

## On the Deck

1. In Desktop Mode open Konsole and run: `cd ~/Orbital-ante`, then `git fetch`, then
   `git checkout claude/critique-loop-1`, then `git pull` (after the PR is merged, use `claude/playtest`).
2. Open the project in Unity Hub and press Play in the main scene.
3. Land near a camp, stay out of sight, then step into view. A short amber arc should point at the enemy and two
   quick pips should play before its first shot. In a running fight new arcs should appear with no pips.
4. Hold Focus and get seen. The arc should last until the shot.
5. Hide behind a rock for about 40 seconds. You should hear a crackly radio call from the squad's side and read
   RADIO CHATTER at the top of the screen before they arrive, and not hear it again while they are still walking.
6. Take damage down to two plates. A slow pulse should start; get behind cover and it should stop as you mend.
7. Meet an ante's target. A chord you have not heard before should play and TARGET MET should stay readable.
8. Let a clock run down while short of the target. Three low knocks at 1:00, five ticks at the end, then a hard
   wooden strike as overtime starts. In overtime you should hear the radio about every 15 seconds.
9. Run off a rock edge and press Jump as you leave it: you should jump. Come down on the jets and tap Jump just
   before landing: you should not hop.
10. Die and read the last line of the page.
11. Tell Claude (or ChatGPT) what felt off. One player, n runs.
