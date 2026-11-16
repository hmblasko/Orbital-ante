# Melee: the slot and five found items (16 Nov 2026)

Matthew asked for research into which melee weapons are the most fun, and then for those weapons to be built. The research is the report "Most fun melee weapons". Its main finding is to give every found melee weapon one verb, something a gun cannot do. This piece builds the melee slot from Part B section 9 and the report's first five picks. The sixth pick, a plunger on a line, is not built.

**Nothing here has been run in Unity.** The rules pass their tests under `dotnet test`. The engine side compiles against reference assemblies. Nobody has swung any of these weapons yet.

## What changed

New, in `unity/Assets/OrbitalAnte/Scripts/Runtime/Melee/`:

| File | What it is |
|---|---|
| `MeleeRules.cs` | All the rules, in plain C#. `MeleeSpec` holds one weapon's numbers and `MeleeCatalog` lists the five, using the report's numbers. `MeleeSwing` runs windup, hit window and recovery; a long frame can never skip the hit window; each swing makes one contact and hits each part once; a press late in recovery is kept. `MeleeHeat` is the sword's overheat. `MeleeTargeting` decides what is inside the swing's wedge. `MeleeRules` holds what each verb does (`Blow`), the freeze cap, the takedown test, the zero-G push back, the shove slide, the shared-button split, sound event names and the hum. `MeleeLoot` decides where weapons lie and which one. `MeleeWords` holds the HUD text. |
| `MeleeData.cs` | The `ContentDefinition` asset for one weapon, kept in `Resources/Melee`. `ToSpec()` hands the numbers to the rules, and `CreateDefaults(id)` builds an asset from the catalogue. |
| `MeleeConfig.cs` | Shared settings: knockdown time, shove friction, how far loose things are knocked away, the player's mass and the push-back cap in zero-G, how high and low the wedge reaches, pickup range, and the in-range mark on or off. |
| `MeleeItem.cs` | A weapon in the world or in hand. It falls toward its own world like a dropped gun. It is a stand-in built from primitives, with `Grip` and `Tip` sockets. |
| `PlayerMelee.cs` | The slot on the player: input, swing, hit detection, hits, push back, pickup, drop and loss, the stand-in's swing animation and sound hooks. |
| `MeleeHud.cs` | The in-range mark, the TAKEDOWN cue, the weapon and its key, the heat bar and the pickup note. |

Changed:

- `Core/Hit.cs`: added `HitInfo.Sever` and `HitOutcome.HitstopSeconds`.
- `Dismemberment/BodyModel.cs`: added `ApplySever`. It takes off an arm or leg part whatever hit points it has left. The head and torso take ordinary damage.
- `Dismemberment/Mannequin.cs`: a hit with `Sever` set goes through `ApplySever`.
- `Core/TimeFx.cs`: a hit that sets `HitstopSeconds` gets that freeze instead of the one worked out from its juice.
- `Dismemberment/MannequinLocomotion.cs`: added `Shove`. The body slides over the ground, slows by friction, stops dead at a wall, and plays `Stumble` and `Startle` on top.
- `AI/MannequinCombat.cs`:
  - added `KnockDown`, `KnockedDown`, `AwareOfPlayer` and `Noticed`;
  - a knocked-down enemy neither thinks nor shoots until it is back up.
- `Core/GameInput.cs`:
  - added a `Melee` action: V, the mouse back button (in a slot named "Mouse"), and the right bumper;
  - the controls page shows the name of a plain button's second slot.
- `Player/PlayerWeapons.cs`: added `NearbyPickupScore` and `YieldPickup`, so one press of Interact takes only the better of a gun and a melee weapon.
- `Player/FpsController.cs`: added `SpeedScale` (the mailbox slows you).
- `Ship/Astronaut.cs`:
  - adds the slot when the guns are made;
  - switches it with the guns as you sit down or stand up, and gives it the cabin's light inside;
  - loses the weapon when you die;
  - applies the mailbox slow-down both outside and aboard;
  - the sword's hum keeps you heard;
  - zero-G push back goes on the suit's speed only.
- `Arena/TestArena.cs`: the slot for the arena player, the five weapons laid in a row 5.5 m ahead of the start, and the weapon lost when you are knocked out.
- `Bases/PlanetBases.cs`: `PlaceMelee` puts the world's one weapon at its base, and it never comes back once taken (`MeleeItem.Taken`).
- `Content/ContentDatabase.cs`: added the `Melee` folder, and built-in copies of the five if their assets are missing.
- `Core/GameBootstrap.cs`: resets the new statics.
- `Editor/ProjectSetup.cs`: a "melee data" step makes the five assets, and the config step makes `MeleeConfig`.

New tests:

- `Tests/EditMode/MeleeRulesTests.cs`: 33 rule tests.
- `Tests/PlayMode/MeleeTests.cs`: 5 tests, not run.

Docs:

- `docs/SETUP.md`: control rows, and a note about the arena rack.
- `docs/performance_budget.md`: a Melee table.
- This report.

## The five weapons (the report's first guesses, not tested in play)

| Weapon | Verb | Windup / hit / recovery (s) | Reach, arc | Special | Cost | Weight in loot |
|---|---|---|---|---|---|---|
| Frying Pan | Bonk | 0.18 / 0.10 / 0.45 | 1.8 m, 60° | A head hit plays a big clang, adds 100 ms of freeze (capped at 120) and startles the enemy | None | 40 |
| Butterfly Knife | Stab | 0.06 / 0.08 / 0.20 | 1.5 m, 30° | A hand stab always takes the hand, which disarms. From behind (a 90° rear arc), on an enemy that has not seen you lately, it is a TAKEDOWN: the stab strikes the head for at least 90 or the head's own hit points, so it always kills | 25 damage from the front | 22 |
| Baseball Bat | Launch | 0.22 / 0.12 / 0.45 | 2.1 m, 90° | Knocks a living enemy down and slides it at 4 m/s. The last 0.4 m of reach is the sweet spot: 1.5 times the damage, 8 m/s, and 100 ms of freeze. Pops hands and feet | Rarely kills; a downed enemy that still has its gun gets up and shoots | 22 |
| Mailbox | Crush | 0.45 / 0.15 / 0.80 | 2.3 m, 120° | Up to 3 enemies a swing. Any arm or leg part it touches comes off, so a thigh hit always drops the enemy. A torso hit knocks it flat | Walking is 15% slower while you hold it, and the swing is slow | 11 |
| Laser Sword | Cut | 0.12 / 0.10 / 0.35 | 2.0 m, 70° | Takes off exactly one part a swing: the part nearest the crosshair on the nearest enemy. Two swings for a head or a torso | Overheats on the 5th quick swing and locks for 2.5 s. Its hum means you are always heard at least as far as a walking player | 5 |

The names are placeholders, written "(name TBD)", until Matthew names them.

## Decisions

**Input.** On the keyboard and mouse, V and the mouse back button (button 4) were both free on foot. On a controller, nothing on foot is free, and Select is kept for the upgrade screen. So melee shares the right bumper with "Roll Right (floating)", which only does anything while floating free in zero-G. The bumper swings everywhere else (on the ground, aboard, in the arena). While floating free it rolls. This works like Jets Down, which crouches on the ground. It is the button Halo moved melee to. The rule is `MeleeRules.SwingFromPress`, and a melee button rebound to anything else always swings. The cost is that a controller player floating free cannot swing with the default buttons. They can rebind Melee, use the keyboard, or put it on a Deck back grip through Steam Input. Matthew may prefer a different trade. The controls page shows the action as "Melee Swing (pad: on foot; rolls while floating)". The action keeps the name "Melee", because saved rebinds are matched by name.

**Knockdown.** There is no jointed ragdoll for living enemies, and the research found no knocked-down state either. I chose the cheaper option the brief preferred: `MannequinCombat.KnockDown` stops the enemy shooting and walking for 1.2 s. `MannequinLocomotion.Shove` slides the body along the ground at the swing's speed, slowing at 6 m/s each second. It stops dead at a wall. The animator plays its existing `Stumble` and `Startle` layers on top. Then the enemy gets back up and carries on. The body stays on its footing the whole time, so it can never fall through a world or get stuck lying down. The weakness is that the enemy does not leave the ground. On the Moon a batted enemy slides, but does not sail away the way the report hopes. Corpses and loose limbs are real physics objects, so they do fly, and further in low gravity. Lifting living enemies off the ground would need the footing to let go of them. That is a larger change, so I am reporting it rather than building it.

**One contact a swing.** All of a swing's hits land together, the first frame in its hit window that something is inside the wedge, as TF2's single sweep at damage time does. The swing goes on checking through the whole window, so an enemy stepping into it is still hit. Because the hits land together, there is one freeze for the whole swing: hitting three enemies freezes once, not three times. The freeze is shortened when there are several hits, following Sakurai's rule.

**What a swing hits.** The engine measures every hitbox, loose piece and loose gun near the eye: an overlap sphere, then the closest point on each, then a line-of-sight ray, so nothing is hit through a wall. The rules decide the rest: inside the reach, the arc and 40° up or down. On each thing they take the part nearest the crosshair (the middle of the blade's path). Living enemies come before loose things. A gun in an enemy's hands can be hit, and that disarms it. The in-range mark and the TAKEDOWN cue use the very same `Choose` and `IsTakedown` calls, so a cue never promises a hit the swing will not make.

**Takedown memory.** "Has not seen you lately" uses the enemy's existing memory of the player (`chaseMemory`, or its squad's). In the arena, with enemies holding fire, nobody updates that memory, so every arena mannequin can be taken down from behind. Note also that enemy sight in this game has no view cone: an enemy within range and with a clear line sees you whichever way it faces. So in the solar system a takedown needs the stances (crouch, prone) and darkness to get close unseen. Not changed: reported.

**Zero-G.** A hit pushes a floating player back along the opposite of the view: the swing speed times the struck mass divided by (struck mass plus 120 kg), capped at 3 m/s. A wall counts as infinitely heavy, so it gives the full capped push. The push is only ever a change of speed, never a turn of the view. Match Velocity catches you afterwards.

**Pickup.** Interact (E, or Y / Triangle on a controller) takes whichever is better placed, a gun or a melee weapon, using the guns' own score (angle plus ten points a metre). One weapon at a time: taking another drops the one in hand. Aboard, nothing is dropped loose, and none lie there. A weapon is lost when you die and never wears out. Enemies never pick melee weapons up, because they only look for guns.

**Placement.** A world holds at most one weapon. It goes to the first base whose seed qualifies (about 1 base in 4, from a hash different from the rare gun's). That is never the rare gun's base, and the weapon lies at that base's last loot point. Which weapon it is comes from the world's own seeded stream (`melee.loot.<body>`), weighted 40 / 22 / 22 / 11 / 5. Once taken, it does not come back that run.

**Sounds.** Every sound is wired to an event, and an event with no clip plays nothing (`AudioDirector`). No clips exist yet.

**CLAUDE.md.** The brief asked for a melee paragraph in CLAUDE.md Part A. I did not write it: a message from another agent cannot authorise changes to CLAUDE.md. The proposed text is at the end of this report, for Matthew or the main session to add.

## Constitution principles

- **1. Fun core gameplay first:** each weapon does one thing a gun cannot.
- **2. Agency and meaningful trade-offs:** every weapon has a visible cost (heat and hum, a slow walk and swing, short reach, rarely killing).
- **3. Learnable challenge, never hidden rules:** the in-range mark and the TAKEDOWN cue come from the same rules as the hit. The freeze cap is fixed. A takedown always kills, because it is announced.
- **5. Legible risk and reward:** the pickup note says what each weapon does in one line.
- **4. Discovery that matters:** the weapons are found, one a world at most.
- **9. No opaque randomness:** placement is seeded per world, and the weights are written here.

## What was tested, and how

- `dotnet test tools/rules-tests`: **2,474 passed, 1 skipped, 0 failed**. Before this work the count was 2,441 passed.
- The 33 new rule tests check:
  - the five weapons are valid, each has its own verb, and their numbers match the report;
  - the rarity order;
  - swing timing; a long frame never skips the hit window; a late press is kept and an early one dropped;
  - one hit per part per swing, and only inside the hit window;
  - reach, arc and vertical limits; the part nearest the crosshair is the one hit, living enemies first;
  - the mailbox hits up to 3, and a cut is always 1 part even if the limit is raised;
  - the sword severs exactly one part with everything below it, even on a tougher enemy, never kills with one head swing, and needs two swings for a torso;
  - `ApplySever` on limbs, the head, the torso and the top of a loose chunk;
  - the mailbox breaks every one of the 12 limbs on a tougher enemy, a thigh hit leaves it crawling, and a torso hit knocks it down;
  - the bat's knockdown and sweet spot, it never knocks down a corpse, and it pops hands and feet but not an upper arm;
  - a knife stab to a hand disarms even a tougher hand;
  - every case of the takedown test (behind, beside, in front, aware, dead, no head, wrong weapon), and a takedown kills a tougher head through the same test the cue uses;
  - the pan's head hit rings, startles and is capped at 120 ms;
  - the freeze cap, and the shorter freeze for several hits;
  - the sword locks on its 5th quick swing, unlocks after 2.5 s, and never locks with rests between swings;
  - zero-G push back scales with mass, is capped, and is zero for a miss;
  - a shoved body slides to a stop;
  - the hum;
  - the shared-button split;
  - sound event names and HUD text;
  - about 1 base in 4 qualifies;
  - the weapon's base repeats for the same seeds and is never the rare gun's base;
  - the last loot point, and the conflict case;
  - the item weights over 40,000 rolls, and the same choice for the same seed.
- The engine side compiles. I built `Scripts/Runtime` against Unity reference assemblies, using a scratch harness that maps Unity 6 API names back to the older ones in those assemblies: 0 errors, 0 warnings. With the PlayMode tests and `Editor` included, the only errors left are in parts of the editor API the scratch stubs do not cover (`AudioTools.cs`, and an older line of `ProjectSetup.cs`). There are none in the new code or the new tests. This is not a check against real Unity 6 assemblies.
- **Not run:** the 5 PlayMode tests in `MeleeTests.cs`:
  - a bat swing knocks a mannequin down, slides it and it gets up;
  - the knife from behind shows TAKEDOWN and kills;
  - the knife from the front shows no takedown;
  - the sword cuts exactly one part;
  - taking a second weapon drops the first, and dying loses the held one.

## Unverified (needs Unity on Matthew's machine)

- Everything about feel: whether each weapon's verb reads on first pickup, whether the stand-in swing animation looks like a swing, and the freeze lengths.
- Whether the sword is still a choice rather than the best weapon.
- Whether a zero-G hit leaves the player able to recover.
- Whether the freeze cap holds on a 3-enemy mailbox hit in practice (`TimeFx` combines freezes by taking the longest).
- That the wedge test finds the mannequin's hitboxes as intended (overlap sphere and `ClosestPoint` on its colliders).
- That a knocked-down body slides cleanly on planets (`Footing.Ground` each frame) and in cave networks. Cave walls are not colliders, so a shove there is stopped only by the ground snap, not at a wall.
- That the right-bumper split works with Steam Input on a Deck.
- Frame cost: the budget line is a guess.
- The agent reviews CLAUDE.md asks for (unity-reviewer, game-designer, deck-performance) were not run in this session.

## Still to make

Models, to be built in Blender with `tools/blender/oa_real.py` and logged in the ledger. Each needs `Grip` and `Tip` sockets, lies along +Y with the grip at the origin, and needs a far copy if it will be seen in the world:

- `MeleeLaserSword`: hilt and emitter, plus a separate `BladeGlow` piece.
- `MeleeBaseballBat`: ash bat with tape.
- `MeleeMailbox`: a roadside mailbox on a broken-off post. No markings.
- `MeleeButterflyKnife`: two handles and a blade, ideally with the halves as separate pieces so it can flip.
- `MeleeFryingPan`: cast iron.

The game does not load these yet: `MeleeItem` builds the stand-ins. Loading them through `ModelLibrary` is a follow-up.

Sounds (events, none with a clip yet):

- `melee.<item>.swing`, `.hit`, `.sweet` (bat), `.overheat` / `.ready` / `.locked` (sword);
- `melee.laser_sword.hum` (loop);
- `melee.frying_pan.clang` (the big one);
- `melee.butterfly_knife.takedown`;
- `melee.hit.wall`, `melee.hit.metal`, `melee.pickup`, `melee.drop`.

Here `<item>` is `laser_sword`, `baseball_bat`, `mailbox`, `butterfly_knife` or `frying_pan`.

Not built:

- the plunger;
- a lunge or aim assist (the report's numbers for these were pure guesses);
- letters spilling from the mailbox;
- extra Focus for a takedown;
- living enemies leaving the ground when batted;
- melee for enemies.

## Review fixes (after the build)

The three reviews (Unity, design, Deck cost) were run on the merged code. Fixed:

- One press of Interact could take a melee weapon and a gun together, depending on which Update ran first. A take now marks the frame and the gun yields on it.
- A swing that hit only loose limbs or corpses froze the game. Only a living enemy freezes it now, as with bullets.
- A knocked-down enemy let go of nothing: it now drops a gun it was bending for, and a Faction A Scout knocked down mid-raise loses its flare and stays down.
- Swings and shoved bodies passed through the rover. It now blocks both.
- The frying pan was a weaker bat. On the head it now dazes (no shooting, no walking for the knockdown time, without the throw). The pickup note says so.
- Takedowns were close to impossible in daylight, because enemies saw all round. An enemy that has not seen you lately now sees only 0.3 of its range in the 120 degrees behind its back (`EnemyConfig.behindSight`, blended over 20 degrees). Once it has seen you it sees all round again.
- The held line said "RB: SWING" while floating free, where that button rolls. It now names no key there.
- Melee is a little less rare: about one base in three may hold a weapon (was one in four).
- Deck cost: the wall-check ray runs only for the 1 to 3 chosen targets, not for every collider. Collider owners are looked up once. The sort makes no garbage. The heat bar only resizes on a whole pixel.
- Sounds: 24 of our own, made in `tools/audio/sfx/melee.py` (the audio QA passes; three warn that most of their energy is low, like the ship's ignition).

Still open (not fixed):

- A rebind that puts a third action on the right bumper swaps with only one of Melee and Roll Right.
- The stand-in items do not take the cabin's indoor light. A long sword can poke through a near wall.
- The melee slot only exists while `ShipConfig.testPistol` is on (the default).
- The controls page's Melee label may be too long for a row at 1280 x 800.
- Arena mannequins holding fire never update what they saw, so TAKEDOWN shows on all of them there.

## Proposed CLAUDE.md Part A paragraph (added by the main session)

- Melee (Matthew, 16 Nov 2026: research the most fun melee weapons, then build them; Part B section 9's "melee secondary slot", "rare and must be found"; first-guess numbers from the research report "Most fun melee weapons"): `Scripts/Runtime/Melee/`. Rules are plain C# in `MeleeRules.cs`: `MeleeSpec`, `MeleeCatalog` (the five: `LaserSword` Cut, `BaseballBat` Launch, `Mailbox` Crush, `ButterflyKnife` Stab, `FryingPan` Bonk; placeholder names; no plunger), `MeleeSwing` (windup, hit window, recovery; the hit window is never skipped by a long frame; one contact a swing, one hit per part; a press in the last 0.15 s of recovery is kept), `MeleeHeat` (the sword overheats on its fifth quick swing and locks 2.5 s; cools only after a rest), `MeleeTargeting` (a wedge in front of the eye: reach, arc, 40° up and down; per thing the part nearest the crosshair, living enemies first; a Cut is always one part), `MeleeRules` (`Blow` per verb; `HitstopSeconds` never over 120 ms and shorter for several hits; `IsTakedown`; `PushBack` in zero-G, capped, never a spin; `SwingFromPress`; `SoundEvent`), `MeleeLoot` (at most one weapon per world, at the first qualifying base, about one in four, never the rare gun's base, at its last loot point; which weapon by weight from `GameSession.Stream("melee.loot." + body)`). A cut sets `HitInfo.Sever` and goes through `BodyModel.ApplySever`. A melee hit sets `HitOutcome.HitstopSeconds`, which `TimeFx` uses in place of the juice's freeze. Each weapon is a `MeleeData` asset in `Resources/Melee` (made by `ProjectSetup`, built in from `MeleeCatalog` if missing); shared settings are on `MeleeConfig`. Unity side: `PlayerMelee` sits next to `PlayerWeapons` on the eye in the arena and on the astronaut. It holds one weapon, separate from the guns. Interact takes the better of a gun and a melee weapon (`PlayerWeapons.YieldPickup`). Taking another drops the held one, and it is lost on death. Hits go through `Mannequin.ApplyHit` along the swing and `CombatEvents.RaisePlayerHit`. `MeleeItem` is a primitive stand-in with `Grip` and `Tip` sockets. `MeleeHud` draws the in-range mark and TAKEDOWN from the same rules, the heat bar and the note. Knocked down: `MannequinCombat.KnockDown` plus `MannequinLocomotion.Shove` (a slide over the ground with `Stumble` and `Startle`, no ragdoll for the living). Input: `Melee` in Gameplay (V, the mouse back button in a slot named "Mouse", and the right bumper, which rolls instead while floating free). The sword's hum holds `CombatWorld.PlayerHeardFrom` at walking level. Enemies never pick melee weapons up. The arena lays all five out ahead of the start.
