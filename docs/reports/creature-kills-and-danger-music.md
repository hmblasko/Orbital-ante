# Creature kills pay, and the music hears danger (Ralph loop pass 3, 28 Nov 2026)

Matthew asked for a game with a clear goal and enjoyable gameplay. Two things worked against both.

- **A creature fight paid nothing toward the ante.** The task director scored only the camps' soldiers. A fight with a Drifting maw or a pack of Pack biters in Earth's sea was time taken away from the goal.
- **In the solar system the music heard only gunfire.** Being seen, hunted or hurt left it where the place put it; the suit's armour was never watched there. So did being set on by a creature, which never shoots.

**Nothing here has been run in Unity, and the music has not been listened to.**

## What changed

| | |
|---|---|
| Problem | A creature fight scored nothing, so the seas were a detour from the ante. The music could not tell a player who was being hunted, or bitten, from one walking alone. |
| Hypothesis | If a creature kill pays like a camp's kill (same table, feats, bonus missions and unlocks), creature fights become one more way toward the target. It pays only when the creature could fight back. If the music's steady level follows how things stand for the player (calm, hunted, fighting), what they hear tells them where they stand, as Part B section 14 asks ("intensifies at higher antes and in intense moments"). |
| Principle | Agency and meaningful trade-offs (2): another way to the target to choose. Legible risk and reward (5): the music says when you are found and when they have lost you, and no kill pays for free. Varied intensity (8): three steps instead of one level and bursts of gunfire. |
| Smallest test | 23 rule tests (`CreatureKillTests`, `MusicDangerTests`, and four new music director tests in `AudioRulesTests`). They check the tiers, the claims, a pack woken again, every creature naming itself as a hit's target, and the reach rule. They also check each step of the music's danger, its holds, how fast the music's own model follows it, where the steps sit against the real music's layers and doors, the rests, the fight coming back, and the thinning. |
| Acceptance (offline, met) | Every creature kind scores as a fight, never as an elite, and only from within its reach. Claims are one per world, haunt and place. A pack woken again behaves as the pack it was. On foot no ante lifts the calm to the tension layer, and flying never reaches the fight music. A sighting brings the fight band within 1.25 s, and a search after it falls under the fight music's exit to tension. Every way an enemy comes to look is counted as hunting. |
| Acceptance (Deck, not yet run) | Below. |
| Playtest protocol | At the end of this report. |

## Creature kills pay

- **Every creature names itself as the target** of the hits it takes (`HitOutcome.Target`) and carries a `CreatureCredit` (`Creatures/CreatureCredit.cs`, plain C#: its kind, its world, its claim), given by whatever woke it.
  - Only the seas wake creatures on real worlds today, so only the Drifting maw and the Pack biter pay.
  - The seven land creatures live in the practice range, which has no ante. They will pay as soon as a spawner gives them credit.
- **The kill pays on the killing hit itself** (`TaskDirector.OnCreatureHit`), through the same `PayKill` as a camp's kill. That covers the tier, the style feats, ON A ROLL, the Ghost set's time back for a LONG SHOT, a bonus mission's bounty or style, and the kill unlocks.
- **Tiers** (`Tasks/TaskRules.cs`, `CreatureTiers`) are on the camps' table.
  - The Pack biter is Tier 1: a shot or two in the head, and the pack is the danger.
  - The Drifting maw and the Strider are Tier 3: hundreds of points of body, and a strike that holds or flattens.
  - The rest are Tier 2.
  - A pack's last fish makes off (`BiterPack.Broken`), so a pack of three reliably pays 90 and a pack of five 180, against a maw's 150 (packs are 3 to 5 fish).
- **Only from within its reach** (`ICreature.InReach`). A swimmer reaches only someone in the water with it, the same test it uses to take notice (`PlayerInWater`, the chest at least 0.3 m under).
  - A maw or a fish killed from the shore, or by someone hovering over the sea, pays nothing toward the ante, its bonus missions or the unlocks.
  - The note says why: SEA KILLS PAY ONLY IN THE WATER WITH THEM. A floating suit's chest is about 0.7 m under, so swimmers always count.
  - The land creatures always say they are in reach.
- **Feats:** ONE SHOT (the first hit that landed), LONG SHOT (from the eye to the creature's head), AIRBORNE and FRESH GUN. Never TAKEN APART, which is the camps' four limbs.
- **Claims are one per world, haunt and place** (`TaskIds.Creature`), so an animal pays once a run. The seas remember a pack's dead fish one by one (`SeaCreatures`, `BiterShoal.Lost`).
  - A pack put away and woken again comes back without them.
  - It still counts them toward its size (`BiterPack.KilledBefore`), so its last fish makes off as it would have.
  - A pack whose every fish is dead is never made again: the seas mark the haunt gone, and `BiterShoal.Create` refuses to make an empty one.
- **Swimming is no longer AIRBORNE.** A floating suit is "not grounded", so a kill made while swimming counted as airborne; this applied to camp kills from the water too. Now the task director checks whether the suit's boots are under the sea (`InTheSea`, a radius test against the sea's level; it asks nothing of the ground).

## The music hears danger

The steady level the music follows is `Audio/MusicDanger.cs` (plain C#). Events still add heat over it in `IntensityModel`.

| Step | When | Level | What plays |
|---|---|---|---|
| Calm | Nobody after you | On foot 22. Flying 20 + (36 + the ante's lift) x engine, at most 56. The ante's lift is 4 an ante past the first, at most 8 | The place's own piece. On foot the ante lift brings in the cave and sea choir at ante 2 and the surface's percussion at ante 3, before most first runs end. Coasting stays as quiet as ever at every ante, so a burn still stands out |
| Hunted | Someone is after you without a fight (see below) | 46 | The explore piece's tension layer, or the unknown's pulse |
| Fight | Seen within 6 s (the readout's own fight, `ReadoutFocus`) while an armed enemy knows where you are. Or a creature began a warning at you as recently, or within 24 s while it still has you in mind | 64 | The fight piece. It takes over at 60, plays at least 14 s, and leaves 4 s after the level falls under 48 |

You count as hunted when any of these is true:
- an armed enemy knows where you are but has lost sight of you;
- an armed enemy holds a place to look for you: a sound it heard you make, an alarm (a Scout's flare, a mast's call, a supply drop, overtime's hunt), or a restless squad's call (`MannequinCombat.LookingForPlayer`);
- a sea creature has noticed you in the water (`SeaCreatures.AnyAware`).

**What the steps sound like together:**

- **Hunted is under the fight music's exit** (raised from 38 to 48 in `tools/audio/music/__init__.py`). So a search after a fight is heard as tension, not as the fight going on, and the music tells you when they have lost you.
- **A creature's fight holds between its strikes** while it still has you in mind. The Drifting maw comes again every 12 to 20 s, so its fight is one piece of music, not a fight that ends and starts again.
- **The last plates thin the music.** In a fight with armour at or under 20%, until it is back to 30%, the music holds back instead of growing, so the suit's pulse is heard over it.
  - `MusicBrain.Thin` holds off every layer from the combat level up (the fight piece's lead and chaos).
  - `AudioDirector` dips the music 4 dB (`MusicDanger.ThinDb`).
- **No rests while someone is after you.** After two times round a piece rests for most of a minute. Now it goes round once more instead if someone is after you, and a rest under way ends when they come (`MusicBrain.Hunted`). Being looked for is never heard as silence.
- **A fight back within a minute** of the last one ending comes in on the downbeat hit, not the riser, at one of the fight piece's later entry points (bar 17, then 41). It never opens like the first.

**Where the danger comes from:**

- **In the solar system the suit's armour is watched now** (`GameAudioHooks.Watch`), so being shot or bitten is heard (`DamageTaken`). A creature's bites count as hostile, so the player's own hits on it can lift the music too. A crash's own hurt (the rover's) is never heard as an attack (`Astronaut.TakingAKnock`): Part B wants no crash music.
- **Who is after the player is read four times a second:** armed enemies present who know where the player is, whether one is coming to look, and whether a swimmer has noticed them. A creature's warning is heard from `CombatWorld.CreatureWindup`.
- **Out on foot, which piece plays is read where the player is,** not where the ship is. Cave and sea are the unknown; near the ground is the explore piece. Walk into a cave or swim under the sea and the music follows you.
- **A new world starts the music calm.** That covers a restart from the death page, which goes straight from one run's world to the next while the game's clock is stopped (`GameAudioHooks.NewWorld`). Nothing heard in the last run carries over, not a creature's warning and not the fight's heat.
- **The practice range keeps its steady 24,** with sightings and creature warnings over it. It counts nobody as hunting, so there a sighting alone is a fight.

What the player hears is longer than the danger's own steps. A fight's music plays at least 14 s, waits 4 s under the exit, ends on a two-bar line with a 7.5 s ending, and leaves 5.5 s of hush before the explore piece returns. So a short sighting by an armed enemy costs about 20 to 24 s of fight music. A sighting by a disarmed enemy costs nothing, because it is no fight.

## The specialist reviews and what was done

Each review ran on the first build (a106eff), and each finding was checked against the code before it was applied.

**unity-reviewer**

- **Applied:**
  - A rover crash would have been heard as an attack, and brought fight music. The knock is now kept out of the music.
  - A creature's warning just before dying carried into the next run's pad. "Try again" goes from one world to the next with the clock stopped. A new world now starts calm, and the old fight's heat is cleared too (that one was older).
  - The creature-kill path no longer reads the creature's position if it has been destroyed. A kill heard late still pays, but never as a long shot.
  - An all-dead pack can no longer be made empty, which would never say it was gone and would block other packs.
  - `HitOutcome.Target`'s description now names creatures.
- **Not changed:** the creature code asks `TaskIds` for its claims. All claims' forms live in one place so they cannot collide, and it is all one assembly.
- **Confirmed safe:**
  - the destroy order inside one hit;
  - one payment per animal;
  - the dead fish's memory;
  - the sea test's frame;
  - the subscriptions and clocks.

**game-designer**

- **Applied, the biggest finding:** sea creatures paid full price to a player they could not hurt. Shots pass through the sea's surface, and the swimmers ignore anyone not in the water. So a maw shot from a hover paid Tier 3 at no risk, and a fish ONE SHOT + AIRBORNE.
  - Kills from out of reach now pay nothing, with a note that says what does (the reviewer's fallback).
  - The reviewer's first choice, shots that stop at the sea's surface in both directions, changes every fight near water (a swimmer could dive out of gunfire). It is left as a design question for later.
- **Applied, the music:**
  - Most ways enemies come looking were silent. Only a restless squad's call counted; alarms, overtime's hunt and a heard sound did not. And a sent squad that heard the player stopped counting as it closed in. Every way is now counted, and a rule test reads the code to keep it so.
  - Rests muted being hunted. They are now put off and ended.
  - Creature fights would have flapped between strikes. They now hold while the creature has the player in mind.
  - A fight back soon would have opened with the same riser from bar 1. It now comes in on the hit at a later entry point.
  - A disarmed enemy's sighting held the fight music. It now needs an armed enemy who knows where the player is.
  - The exit was raised to 48, so a search is heard as tension.
  - The last plates' chaos layer would have buried the pulse. It is replaced by the thinning.
  - The ante's lift was heard only from ante 5 on foot, and filled the coast when flying. It is now 4 an ante, at most 8, and goes with the engines when flying.
- **Kept:**
  - The tiers, which the review found fair.
  - The KILL label, since camp kills do not name tiers either.
  - Counting creature kills toward bounties, style missions and unlocks.
- **Corrected:** this report's earlier "lets go within 6 s", which was the danger's band, not what is heard (see above).
- Its Deck acceptance criteria and playtest protocol are below.

**deck-performance**

- **Fits the budget.**
- **Applied:** the sea test asked for a full ground reading on every hit while airborne, which high above a world could cost a full-detail height. It now asks only the sea's radius.
- **Budget rows written** in `docs/performance_budget.md`.

## Tests

- **`CreatureKillTests` (8 tests):**
  - every creature kind scores as a fight and is worth something;
  - which kinds are which tier, and a pack of three against a maw;
  - the tier list names every creature model;
  - claims are one per world, haunt and place, and never an enemy's;
  - a pack woken again behaves as the pack it was, and `KilledBefore` never takes away;
  - every creature class implements `ICreature`, names itself as the target of every hit it counts, and says when it is in reach (the swimmers by their own notice of the player in the water);
  - a kill from out of reach is refused before anything is paid, and the note says what does.
- **`MusicDangerTests` (12 tests):**
  - the calm grows with the ante, stops growing by ante 3, and on foot never reaches the tension layer;
  - the lift is heard by ante 2 in caves and the sea and by ante 3 on the surface (read from the real pieces);
  - flying never reaches the fight music, coasting is as quiet at every ante, and ante 1 is what it was;
  - nobody after you leaves the calm alone, even on low armour, and lets the music rest;
  - being looked for (heard, alarmed, lost sight of, a creature coming) is tension;
  - a sighting or a creature's warning is a fight for exactly the readout's six seconds;
  - in the solar system a sighting is a fight only with an armed enemy aware;
  - a creature's fight holds through the maw's slowest gap while it has you in mind;
  - the last plates thin the music, on and off with the pulse, and never raise it;
  - the steps sit either side of the real music's doors and layers;
  - a sighting run through the music's own model brings the fight band within 1.25 s, and the search after it settles at tension under the exit;
  - every way an enemy comes to look is counted (it reads the code).
- **`AudioRulesTests`, 3 new tests and 1 changed:**
  - a fight back within a minute comes in on the hit at the fight piece's later entry points in turn, and after a long quiet opens like the first again;
  - no rest while someone is after the player, the rest falling at the end of a later time round, and a rest under way ending at once;
  - the last plates hold the fight's top layers off on the next bar and give them back;
  - the existing flare-up test now expects the hit.

## Still unverified

- Nothing here has run in Unity, and the music has not been listened to.
- Whether the three steps, the thinning and a fight's return are told apart by ear on the Deck's speakers and on headphones.
- Whether the thinning (4 dB and the lead held off) is enough for the pulse to stand out, or too much.
- Whether the fight music's length after a short sighting (about 20 to 24 s) feels right, or drags.
- Whether "SEA KILLS PAY ONLY IN THE WATER WITH THEM" is read in time, and whether killing from the surface of the water feels fair. A swimmer counts; someone standing on a shallow shelf does not.
- The kill's "+points" line on the frame of a creature kill, and the dead fish's memory across putting a pack away and waking it again.
- Known limits:
  - an animal put away and woken again comes back healed;
  - sea creatures killed from out of reach still count toward the gun ladder and give Focus back, as they did before this pass;
  - the land creatures pay nothing until a spawner brings them onto a world.

## Acceptance on the Deck

**Creatures**
- Each kill in the water gives one "+points" line at its tier times the ante's value, with its feats.
- No kill pays twice. Kill two fish of a pack, leave past the sleep range (260 m), and come back.
- Nothing pays when shot from the shore or from over the sea, and the note says why.
- Sea points per minute are within 30% of a camp fight's at the same ante.

**Music**
- The tension layer starts within 2 bars of each of these, and holds until they see you or give up:
  - a restless squad being sent;
  - a mast's call;
  - a supply drop;
  - a Scout's flare;
  - each of overtime's hunts.
- Combat never restarts from bar 1 within 60 s of an exit.
- At most 1 combat start in 5 has no shot or strike in its first 6 s.
- The pulse is named in 9 of 10 eyes-shut trials over the fight music, on the speakers.

**Failure looks like:**
- free sea points;
- calm music under THEY'RE HUNTING YOU;
- a sneaking player feeling that the music said "safe" just before being found;
- anyone calling the music annoying or repetitive within 30 minutes.

## Playtest protocol

**Setup**
- 3 to 5 testers, handheld, on the speakers and then on headphones.
- 30 minutes from ante 1.

**What to log:** the music band, the piece and layers, shots, points and claims, from the F9 audio panel and the readout. A per-event log is not built yet.

**Steps.** After each step, ask the tester what the music was telling them.
1. Fight a maw and a pack in Earth's sea. Then try both again from the shore and while hovering over the water.
2. Sneak past two camps, make a noise, and break line of sight after being seen.
3. Let a restless squad come, set off a mast and a supply drop, and run into overtime.
4. Drop to 20% armour in a fight.
