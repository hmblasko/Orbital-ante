# Milestone 13: Faction A and five melee weapons (16 Nov 2026)

Matthew approved Phase 1 and the start of Phase 2 on 16 Nov 2026. He then asked for research into which melee weapons are the most fun, and for the winners to be built. Milestone 13 in Part B is "First real faction (Faction A, three tiers), more weapons and melee", so both are covered here. Both are on `claude/playtest`.

**Nothing here has been run in Unity.** The code compiles against reference assemblies. All 2,499 rule tests pass under `dotnet test` (1 skipped). The PlayMode tests compile but have not been run. Feel, frame times and readability have not been tried by anyone.

## What changed

### Melee: five weapons that are found

Full detail is in `docs/reports/melee.md`. The research behind it is "Most fun melee weapons" (`/mnt/project-files/orbital-ante/research/`). Its main finding: each found weapon should give one verb that no gun gives.

| Weapon | Verb | What only it does | Rarity weight |
|---|---|---|---|
| Laser sword | Cut | Takes off one limb per swing, whatever that limb's hit points. Overheats on the fifth quick swing. Its hum gives you away. | 5 |
| Baseball bat | Launch | Knocks the target down and slides it away. Hitting with the end of the bat (the sweet spot) does more of both. | 22 |
| Mailbox | Crush | Hits up to three enemies in one swing. You walk 15% slower while holding it. | 11 |
| Butterfly knife | Stab | A stab to the hand disarms. Behind an enemy that has not seen you, it is a TAKEDOWN that always kills. | 22 |
| Frying pan | Bonk | A head hit rings like a bell and dazes the enemy: it cannot shoot or walk for 1.2 s. | 40 |

How it works:
- **Controls.** Melee is V, the mouse back button, or the right bumper. While you float free in zero-G, the right bumper rolls instead, and the HUD line then names no key.
- **Hit freeze.** Capped at 120 ms. It happens only on living enemies.
- **Zero-G.** Hitting something pushes you back.
- **Where weapons appear.** At most one weapon per world, lying at about one base in three. It is never at the base that holds the rare gun. Once taken, it does not come back that run. The arena lays all five out ahead of the start.
- **Sounds.** 24 of our own, made in `tools/audio/sfx/melee.py`.
- **Not built.** The plunger and enemy melee were not built.

One change reaches past melee. An enemy that has not seen you lately now sees only 0.3 of its usual range in the 120 degrees behind its back. This makes the knife's takedown possible in daylight. Once it has seen you, it sees all round again. The dial is `EnemyConfig.behindSight`.

### Faction A: three tiers

Faction A is a placeholder name, with no name, emblem or flag. The tiers differ in size, gun and one ability, never in limb health. Each ability has a tell you can see or hear, and a way to stop it.

| Tier | Size | Gun | Ability | Tell | Counter |
|---|---|---|---|---|---|
| Scout | 0.85 | Scout Carbine | On first sight it raises its free arm for 1 s, then fires a flare. Its own side within 150 m comes to look where you were. | The hand glows, plus a strike-and-fizz sound and a red ball 30 m up. | Shoot the hand, forearm or gun during the raise, kill it, or knock it down. |
| Line | 1.0 | Line Rifle | While a squadmate is out flanking, it fires long, wide bursts (2.4 s, spread ×1.5). | A long, unbroken burst means someone is coming round the side. | Watch your flank. |
| Heavy | 1.3 | Spinner | Plants itself and spins up for 1.2 s, then fires for 3 s. While braced it turns at most 20° a second. | The gun's cell grows brighter, plus a rising whine. | Get round it close in, break line of sight, take a leg (it drops and never fires again), or shoot the gun away. |

Where they live:
- Every camp and guard on the home world belongs to Faction A. The home world is Venus (Matthew confirmed it on 16 Nov 2026).
- Elsewhere, 30% of camps belong to Faction A. A camp is never mixed.
- A large base has two Heavies and a medium base has one.

Until the faction's own suits are modelled:
- The three tiers wear the Runner, Trooper and Bruiser bodies.
- They carry those kinds' borrowed gun models.
- A band tint on the helmet and chest marks them as Faction A.

Rules are in `Enemies/FactionRules.cs`. The Unity side is in `AI/MannequinCombat.Faction.cs`. The dials are on `EnemyConfig` (`factions`, `flare`, `spinUp`, `suppress`).

**Seeds.** Camps moved for old seeds, because each camp now draws one more seeded number.

## Why (constitution)

- **1. Fun core.** Melee gives a second verb per fight. Each weapon gives a different moment worth laughing about: the pan's clang, the bat's launch, the mailbox through three.
- **2. Trade-offs.** Each weapon pays for its strength: the sword overheats and hums, the mailbox is slow, and the knife is short.
- **3. Learnable.** Every Faction A ability has a tell and a counter. The cue for being in range and the TAKEDOWN cue ask the same question the swing asks.
- **5. Legible risk.** Being seen from behind is a stated rule with a dial, not hidden.
- **9. Ethical.** The loot weights are seeded and written down. There are no timers.

## Tested

- `dotnet test tools/rules-tests`: 2,499 passed, 1 skipped. This includes:
  - 33 melee rule tests;
  - 15 Faction A tests: tiers and sizes; not tougher than the unaligned kinds; camps never mixed; the home world all Faction A; a share near 30% elsewhere; the same camps for the same seed; caps per camp; the flare's timing, cancel and once per life; the spin-up cycle and turn cap.
- The runtime compiles with 0 errors.
- The PlayMode tests compile, except for the three stub gaps that were already there.
- Audio QA: PASS. Some sounds warn that most of their energy is low, as some existing sounds already do.
- Reviews by the unity-reviewer, game-designer and deck-performance agents ran on both halves. Their findings were fixed, as listed below.

## Agent review

**Faction A.**
- *Unity reviewer:* a disarmed Heavy kept its turn cap; the gun's cell was resized and passed on; the Heavy almost never finished a burst (it walked in and switched between moving and shooting); the flare's position was wrong if it went up in the frame before a pause; squads rarely let the Scout flare.
- *Designer:* the tells were silent (sounds added, glow doubled); the Heavy did less damage than a Trooper (player damage per hit raised from 3.5 to 6.5); circling only works close in (turn cap lowered from 30° to 20° a second, and the Heavy now fires from 32 m); covering fire ignored squad turns; flares woke the unaligned kinds (now only Faction A hears them).
- *Deck reviewer:* small fixes (an allocation per listener, colliders destroyed at once).

**Melee.**
- *Unity reviewer:* one press could take a gun and a melee weapon together; loose limbs froze the game; knocked-down enemies kept grabbing and flaring; the rover did not block swings.
- *Designer:* the pan was a weaker bat (it now dazes); takedowns were nearly impossible in daylight (the narrow sight behind an enemy); the right bumper label was wrong in zero-G; rarity changed from 1 base in 4 to 1 in 3.
- *Deck reviewer:* a ray per collider each frame (now only for the chosen targets); sort garbage; HUD rebuilds.

## Still unverified or open

- All feel and timing, in Unity: swing weight, hit freeze, shove distance, the Heavy's bursts, whether the flare is visible at 30 m on the Deck, and how the band tint reads.
- Whether two Faction A tiers wearing unaligned bodies can be told apart from the unaligned kinds by shape. They cannot until Fable builds the suits. The brief is `docs/briefs/faction-a-and-melee-models.md`.
- A flare reaches only enemies already awake near the player. It does not wake sleeping camps.
- A Heavy that loses a leg never fires again. This is deliberate, but untested in play.
- Melee issues still open are listed in `docs/reports/melee.md`. They include a rebind that puts a third action on the right bumper, the cabin light on held items, and the arena's TAKEDOWN showing on every mannequin.
- The Phase 1 sign-off checklist boxes are unticked in the repository, though Matthew approved in conversation.

## Playtest (on `claude/playtest`)

1. In the arena, pick up each of the five weapons and swing at the mannequins. Check that the pan's head hit clangs and dazes, the bat slides them, the mailbox hits three, and the sword overheats on the fifth quick swing.
2. Press E once where a gun and a melee weapon are both in reach. Only one should be taken.
3. On Venus, find a camp. Let a Scout see you and watch for the raised glowing hand. Shoot that hand before the flare goes up.
4. Fight a Heavy. Listen for the whine, then get round it close in or break line of sight.
5. Sneak up behind an unaware enemy, crouched, with the knife. Check that TAKEDOWN shows and kills.
