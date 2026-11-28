# Creature kills pay, and the music hears danger (Ralph loop pass 3, 28 Nov 2026)

Matthew asked for a game with a clear goal and enjoyable gameplay. Two things worked against both. A creature fight
paid nothing toward the ante: the task director scored only the camps' soldiers, so a fight with a Drifting maw or a
pack of Pack biters in Earth's sea was time taken away from the goal. And in the solar system the music heard only
gunfire: being seen, hunted, hurt (the suit's armour was never watched there) or set on by a creature (which never
shoots) left it where the place put it. **Nothing here has been run in Unity, and the music has not been listened
to.**

## What changed

| | |
|---|---|
| Problem | A creature fight scored nothing, so the seas were a detour from the ante. The music could not tell a player who was being hunted, or bitten, from one walking alone. |
| Hypothesis | If a creature kill pays like a camp's kill (same table, same feats, same bonus missions and unlocks), creature fights become one more way toward the target. If the music's steady level follows how things stand for the player (calm, hunted, fighting, on the last plates), what the player hears tells them where they stand, as Part B section 14 asks ("intensifies at higher antes and in intense moments"). |
| Principle | Agency and meaningful trade-offs (2): another way to the target to choose. Legible risk and reward (5): the music says when you are found. Varied intensity (8): four steps instead of one level and bursts of gunfire. |
| Smallest test | Rule tests (`CreatureKillTests`, `MusicDangerTests`, 14 tests) on the tiers, the claims, a pack woken again, every creature naming itself as a hit's target, and on each step of the music's danger, its hold, its hysteresis and how fast the music's own model follows it. |
| Acceptance (offline, met) | Every creature kind scores as a fight, never as an elite; claims are one per world, haunt and place; a pack woken again behaves as the pack it was. On foot, no ante lifts the calm as far as the tension layer, and flying never reaches the fight music; a sighting brings the fight band within 1.25 s and it lets go within 6 s of the readout's fight ending. |
| Acceptance (Deck, not yet run) | Below. |
| Playtest protocol | At the end of this report. |

## Creature kills pay

- Every creature names itself as the target of the hits it takes (`HitOutcome.Target`) and carries a
  `CreatureCredit` (`Creatures/CreatureCredit.cs`, plain C#: its kind, its world, its claim), given by whatever woke
  it. Only the seas wake creatures on real worlds today, so only the Drifting maw and the Pack biter pay; the seven
  land creatures live in the practice range, which has no ante, and will pay as soon as a spawner gives them credit.
- The task director pays on the killing hit itself (`TaskDirector.OnCreatureHit`), through the same `PayKill` as a
  camp's kill: the tier, the style feats, ON A ROLL, the Ghost set's time back for a LONG SHOT, a bonus mission's
  bounty or style, and the kill unlocks.
- Tiers (`Tasks/TaskRules.cs`, `CreatureTiers`), on the camps' table: the Pack biter Tier 1 (a shot or two in the
  head; the pack is the danger), the Drifting maw and the Strider Tier 3 (hundreds of points of body and a strike that
  holds or flattens), the rest Tier 2. A pack of three pays about what one maw does.
- Feats: ONE SHOT (the first hit that landed), LONG SHOT (from the eye to the creature's head), AIRBORNE, FRESH GUN.
  Never TAKEN APART, which is the camps' four limbs.
- Claims are one per world, haunt and place (`TaskIds.Creature`), so an animal pays once a run. The seas now remember
  a pack's dead fish one by one (`SeaCreatures`, `BiterShoal.Lost`): a pack put away and woken again comes back
  without them, and still counts them toward its size (`BiterPack.KilledBefore`), so its last fish makes off as it
  would have.
- A kill made while swimming is no longer AIRBORNE (a floating suit is "not grounded"): the task director checks
  whether the suit's boots are under the sea (`InTheSea`). This applied to camp kills from the water too.

## The music hears danger

The steady level the music follows (`Audio/MusicDanger.cs`, plain C#; events still add heat over it in
`IntensityModel`):

| Step | When | Level | What plays |
|---|---|---|---|
| Calm | Nobody after you | On foot 22, flying 20 + 36 x engine (at most 56); plus 2 an ante past the first, at most 8 | The place's own piece. At ante 5 on foot the explore piece's percussion joins; flying, coasting carries its drums from ante 3 |
| Hunted | An armed enemy knows where you are but has lost sight of you, or one is on its way to look (a restless squad, an alarm, overtime's hunt) | 46 | The explore piece's tension layer, the unknown's pulse |
| Fight | An enemy had you in sight within 6 s (the readout's own fight, `ReadoutFocus`), or a creature began a warning at you as recently | 64 | The fight piece (it takes over at 60, and plays at least 14 s) |
| Last plates | In a fight, armour at or under 20%, until it is back to 30% (with the suit's pulse, `PulseClock`) | 86 | The fight piece's top layer |

- In the solar system the suit's armour is watched now (`GameAudioHooks.Watch`), so being shot or bitten is heard
  (`DamageTaken`), and a creature's bites count as hostile, so the player's own hits on it can lift the music too.
- Who is after the player is read four times a second (`EnemySpawner.Hunters`: armed enemies present who know where
  the player is, and whether one is on its way); a creature's warning is heard from `CombatWorld.CreatureWindup`.
- Out on foot, which piece plays (cave and sea are the unknown, near the ground is the explore piece) is read where the
  player is, not where the ship is: walk into a cave or swim under the sea and the music follows you.
- The practice range keeps its steady 24, with sightings, creatures and the last plates over it.

## The specialist reviews and what was done

(Filled in below as the reviews come back.)

## Tests

- `CreatureKillTests` (7 tests): every creature kind scores as a fight and is worth something; which kinds are which
  tier, and a pack of three against a maw; the tier list names every creature model; claims are one per world, haunt
  and place and never an enemy's; a pack woken again behaves as the pack it was, and `KilledBefore` never takes away;
  every creature class implements `ICreature` and names itself as the target of every hit it counts.
- `MusicDangerTests` (7 tests): the calm grows with the ante, stops growing, and on foot never reaches the tension
  layer; flying never reaches the fight music, and ante 1 is what it was; nobody after you leaves the calm alone, even
  on low armour; being looked for is tension; a sighting or a creature's warning is a fight for exactly the readout's
  six seconds; the last plates are chaos, on and off with the pulse and never from an old fight; a sighting run
  through the music's own model brings the fight band in 0.75 s and lets go 3.75 s after the fight ends.

## Still unverified

- Nothing here has run in Unity, and the music has not been listened to.
