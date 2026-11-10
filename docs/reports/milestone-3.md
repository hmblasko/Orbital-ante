# Milestone 3 report - Weapons and disarming

Status: **built, not yet run in Unity.** Same position as Milestones 1 and 2. The code compiles against Unity reference assemblies and the rule tests pass, but nobody has pressed Play. Three milestones are now stacked without a playtest, so treat every number here as a first guess. Setup steps are in `docs/SETUP.md`.

Same branch as before (`claude/unity-milestone-1`).

## What changed

**Weapons as data**
- Each gun is a `WeaponData` asset in `Assets/OrbitalAnte/Resources/Weapons`: damage per body part (8 values), damage to the player, automatic or not, fire rate, range, spread (base, per shot, cap, settle rate), recoil (pitch, yaw, how much returns, how fast), magazine size, reload time, one-handed multipliers, model, sockets, tracer colour, sound pitch, mass.
- **Rifle** ("AK-ish", the AK-47 spoof, name is a placeholder): full-auto, 10 shots a second, 30 rounds, 1.9 s reload, 34 damage everywhere. Limbs come off in 1-2 hits. Spread grows while you hold the trigger and the view climbs.
- **Test Pistol**: one shot per pull, 12 rounds, accurate, 46 to the head and 24-30 to limbs (1-3 hits).
- Ammo in reserve is unlimited (section 9). The magazine is not, so reloading still matters.

**The player's gun**
- Full-auto and single-shot firing, spread, recoil that partly settles back and partly stays (so you pull down against it), reload with R or by pulling the trigger on an empty gun, tracers, muzzle flash with a light, sparks and a dark mark where bullets hit the world, a small blood puff on hits that do not remove anything, shot / click / reload sounds (generated placeholders).
- HUD: gun name and magazine count bottom right, "RELOADING", a prompt when a gun can be picked up.
- The old test rifle is gone.

**Models and sockets**
- Placeholder guns built from primitives, each with the four sockets as empty child transforms: Grip, SupportHand, Muzzle, Magazine. The magazine is a separate piece that disappears and reappears during a reload.
- If you assign a CC0 model on the WeaponData asset it is used instead, scaled to a length you give, with the sockets estimated from its size (or taken from child objects named muzzle / support / magazine, or set by hand). `docs/SETUP.md` section H has the steps.

**Enemies with guns**
- Every mannequin starts armed (six rifles, two pistols, from the seed). They hold the gun in both hands, arms bent to the grip and support sockets, and carry it low when not aiming.
- They use the same gun rules as you: same magazine, same reload, same spread build-up.
- They take 0.6 s between seeing you and firing, shoot in bursts, shift position between bursts, and walk toward you if they lose sight.
- **The arena starts with enemies holding fire.** H or D-pad down starts and stops the fight. A banner says which it is.

**Disarming**
- Losing the gun hand or its forearm (or the whole arm) drops the gun as a physics object.
- Shooting the gun itself knocks it out of their hands along the bullet, with sparks, a clang and a blue hit marker.
- A disarmed character with any hand left goes to the nearest dropped gun and picks it up with that hand.
- One hand on the rifle: 4x spread, 2.5x recoil, 2.2x reload time, and the gun visibly waves about. The pistol is built for one hand and is penalised much less.
- **Toggle:** `One Handed Reload Allowed` on MannequinConfig. Off means a one-handed character cannot reload at all and is stuck once the magazine is empty. It defaults to on.
- No hands: cannot fire or pick anything up. They still move, and head for the nearest cover away from you.
- Dying drops the gun. Guns keep their magazine count as they change hands.
- You can pick up any dropped gun, including theirs (E / Y). It swaps with the one you hold.

**Bare-minimum player health**, only so being shot at can be tested: 100 health, rifle hits do 4 and pistol hits 6, health returns after 4 quiet seconds, a red flash on each hit. At zero the view sinks to the floor (first person throughout), then you are back at the start with fresh mannequins.

**Also**: Reload, Pick Up and Enemies Hold Fire are rebindable in Pause > Controls. The Controls rows are slightly smaller so twelve fit on a Deck screen. New physics layer 12 "Item" for dropped guns.

## The pistol and the final loadout rule

The finished game allows one gun plus a melee slot. I built it that way already: **you carry one gun at a time.** You start with the rifle, the pistol lies on the ground near the start, and picking either up drops the other. So the pistol is a second gun in the world, not a second slot. Nothing needs undoing later; the melee slot is simply not built yet (it is Phase 2 content).

## Why, and which constitution principle each serves

| Decision | Principle |
|---|---|
| Three ways to stop a shooter: shoot the gun (fast, they will fetch it), take the hand (they come back one-handed and worse), take both (out of the fight but alive) | 2 Agency and meaningful trade-offs |
| Enemies run on the same gun rules as the player, with no hidden accuracy tricks; their extra aim error is one visible number | 3 Learnable challenge |
| A fixed beat before an enemy's first shot | 3 Learnable challenge; 5 Legible risk/reward |
| A one-handed rifle visibly waves, and a blue marker and clang confirm a disarm | 5 Legible risk/reward |
| Arena starts calm; you choose when the fight starts | 7 Manage cognitive load; 8 Varied intensity |
| Guns are data assets | Quality bar in section 2; 10 Evidence-based iteration |
| Gun rules in a plain class with tests | 10 Evidence-based iteration |

## Evidence-based iteration (principle 10)

- **Problem:** the mannequins could be taken apart but had no way to fight, so there was nothing to disarm and no reason to choose one body part over another.
- **Hypothesis:** if arms, hands and the gun itself each produce a different, visible result, players will start aiming on purpose rather than at centre mass.
- **Smallest test:** the arena, eight armed mannequins, H to start the fight.
- **Acceptance criteria:** you can disarm on purpose by shooting the gun; you can tell a one-handed shooter from a two-handed one at a glance; an unarmed enemy fetching a gun reads as intent, not a bug; being shot at feels fair (you see the tracer, you know who hit you); recoil is controllable with practice.
- **Playtest protocol:** the checklist below.

## Decisions you should know about

1. **Enemies shooting back and player health are in.** The spec says no-hands characters "can still move and take cover" and head-only ones "can't shoot", which only means something if the others do shoot. I kept health to the bare minimum and it is a stand-in, not the real suit and damage model.
2. **Enemy bullets pass through other enemies.** No friendly fire. It would be funny, but they would dismember each other before you got the chance. Easy to switch on if you want to see it.
3. **The player always has two hands.** The one-hand and no-hand rules apply to enemies, because the player has no body to dismember yet.
4. **Weapon names are placeholders** ("AK-ish", "Test Pistol"). Naming the roster is yours.
5. **No hit "ping" sound yet.** Section 9 asks for one, but it is not in the Milestone 3 list, so I left it for the polish pass rather than guess at it. The hit marker is there. Say the word and I will add it now.
6. **All sounds are generated placeholders**, as in Milestone 2.
7. **The gun can clip through a wall** if you stand nose to it. The usual fix (a separate weapon camera) is a Milestone 12 polish item.
8. Enemy steering is simple: walk at the goal, veer around walls. It is enough for a flat arena and will need replacing for real terrain.

## What I tested

- **Rule tests, actually run** (`dotnet test`, outside Unity): 33 of 33 pass. 14 are new: fire rate, single-shot, dry fire, reload timing, spread build-up and cap, one-hand spread, recoil and reload penalties, the one-handed reload toggle, losing a hand mid-reload, no hands, and hands following the arms. Writing them caught a real mistake: spread was settling faster than it built up, so holding the trigger never made the rifle wander. Fixed.
- **Compile check, actually run**: all runtime scripts compile against Unity engine modules (2021.3) plus the real Input System and Unity UI assemblies. The setup tool and all 33 PlayMode tests compile against hand-written stand-ins for the editor and test-runner APIs.
- **PlayMode tests, written but NOT run** (14 new): sockets and data; held gun; lost hand drops the gun to the floor; lost forearm; shot gun, directly and by a real raycast; pick up with the remaining hand and the one-hand penalty; no hands; reload toggle; death drops the gun; unarmed character fetches a gun; armed character waits a beat, then shoots and hits, and hold-fire stops it; player swap; recoil.

## What is NOT verified

1. **Nothing has been run in Unity, for any of the three milestones.** A first-open compile error is possible. If you see red in the Console, send me the first line.
2. **How the arms and gun look.** The arm bending is maths I could not see. Expect elbows in odd places. This is the thing I most expect to fix.
3. **How the fight feels**: enemy accuracy, damage, burst timing, recoil, spread. All guesses.
4. **Imported gun models.** The model path is written but I had no model to try it on.
5. **The sounds and the muzzle flash.** Never heard, never seen.
6. **Steam Deck performance.** Each muzzle flash is a real light (six at most). Not measured.
7. Whether the PlayMode tests pass. The ones that wait for an enemy to walk or shoot depend on timing.

## Playtest checklist

**Setup**
- [ ] No red errors in the Console.
- [ ] EditMode tests: 33 green. PlayMode tests: 33 green. (Send me the text of any red one.)
- [ ] `Assets/OrbitalAnte/Resources/Weapons` contains Rifle and Pistol.

**Your rifle** (enemies still holding fire)
- [ ] Hold the trigger: full-auto, tracers, muzzle flash, sparks and marks on walls.
- [ ] The view climbs as you hold fire and partly settles when you stop. Controllable? Too much? Too little?
- [ ] Long bursts wander more than taps. Can you feel the difference at 20 metres?
- [ ] The counter goes down. R reloads; the gun tips over and the magazine disappears and comes back. Does 1.9 s feel right?
- [ ] An empty gun clicks, then reloads itself.
- [ ] Limbs still come off in 1-2 hits.

**Swapping**
- [ ] The pistol is on the ground to the right of the start. Look at it: a prompt appears. E or Y swaps, and the rifle is tossed forward.
- [ ] The pistol fires once per pull and feels different from the rifle. Headshots in 2?
- [ ] Swap back. The rifle has the ammo it had when you dropped it.

**Disarming** (still holding fire is fine)
- [ ] Every mannequin holds a gun. Do the arms and hands look right? Which poses look wrong?
- [ ] Shoot a gun: it flies out of their hands with sparks and a clang, and the hit marker is blue.
- [ ] Shoot a gun hand off: the gun drops and bounces.
- [ ] Shoot a forearm off: same.
- [ ] A disarmed mannequin walks to a gun and picks it up. Does that read clearly?
- [ ] One that lost its right hand picks the gun up in its left, and a rifle in one hand waves about.
- [ ] Take both hands: it cannot pick anything up, and goes behind a wall.
- [ ] Kill one: its gun drops. You can pick it up.

**The fight** (press H)
- [ ] The banner disappears and they start shooting after a short beat.
- [ ] You can see where shots come from. Does being hit feel fair?
- [ ] Health bar and red flash work. Health comes back after a few quiet seconds.
- [ ] With all eight shooting, how long do you last standing in the open? Too deadly, too soft?
- [ ] A one-handed rifleman misses far more than a two-handed one.
- [ ] Empty a mannequin's magazine (let it shoot): it reloads.
- [ ] Get knocked down: the view sinks, then you restart at the start. Never third person.
- [ ] In `MannequinConfig`, untick **One Handed Reload Allowed**, press Play, take a left hand and let that mannequin run dry. It should never reload.

**Controls**
- [ ] Pause > Controls shows Reload, Pick Up / Swap Weapon and Enemies Hold Fire on both tabs, and everything fits on screen.
- [ ] Rebind Reload. It works.

**Overall**
- [ ] Which felt best: shooting the gun, the hand, or the leg?
- [ ] What looked most broken?

## Assets needed from you

Optional: a CC0 gun pack in `unity/Assets/ThirdParty/Weapons/` (Quaternius "Ultimate Gun Pack", stein-indie "Free Classic Weapons Pack", or 3dmodelscc0 "Guns & Explosives"). `docs/SETUP.md` section H has the steps. Without one the placeholder shapes stay.

## Next

Milestone 4 (foundation: seeded RNG, save system, data pipeline, Deck performance budget). It is lean and mostly testable without the editor. After that, Milestone 5 onward is the solar system, where I can no longer build blind with much confidence, so a first run in Unity before then matters a great deal.
