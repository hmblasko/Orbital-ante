# Milestones 2 + 3: gibs that feel amazing, weapons and disarming

## What's in it
**M2, gibs that feel amazing**
- Blood: a burst at every wound (entry spray plus an exit spray along the bullet), a trailing spray on
  flying gibs, and URP decal splats under wounds and where gibs land (pooled, max 64, fade after 45 s).
- Hitstop: a tiny freeze on a limb pop (0.05 s) and a bigger one on a kill (0.075 s).
- Camera shake on a separate shake rig (Perlin "trauma"), so it never moves your aim.
- A distinct procedural pop for each part type, a squelch when gibs hit the floor, a hit ping.
- Gibs shrink away at the end of their life and are pooled; new dummies reuse them instead of allocating.
- Gibs bounce more (bounciness 0.5, was 0.35).
- Optional headshot slow-mo (M / D-pad right), off by default.
- All numbers in `Resources/Config/GameFeelConfig`.

**M3, weapons and disarming**
- Two guns, each one `WeaponDefinition` asset in `Resources/Weapons/` (tune them in the Inspector):
  - **AK Spoof (working name)**: full auto, 600 rpm, 30 rounds, 2.1 s reload.
  - **Test Pistol**: semi auto, 12 rounds, 1.4 s reload. Test arena only (see DECISIONS #9).
- Per-region damage multipliers (head, torso, arms, hands, legs, feet) on each gun.
- Spread and bloom (hip vs aimed), recoil that kicks the camera and recovers, viewmodel kick, muzzle flash,
  tracer, impact sparks. Spread comes from the run seed.
- Aim down sights (right mouse / left trigger): lower FOV, slower look, gun moves to the centre.
- Reload (R / X), auto reload when you fire on empty, dry-fire click otherwise.
- Real magazines, endless reserve (DECISIONS #5). HUD shows ammo bottom right, `reloading`, gun name.
- One gun at a time. Look at a loose gun and press Interact (E / Y) to swap: your old gun is thrown.
- **Dummies carry guns** (40 % pistol, 60 % rifle, chosen by seed). In walk mode **Attack** (G cycles
  Stand, Wander, Approach, Attack) they walk to about 9 m and fire bursts at you.
  - There is no player health yet. A hit flashes the screen edge on the side the shot came from.
- **Disarming**:
  - Shoot the hand (or the arm above it) holding the gun: the gun drops.
  - Shoot the gun itself: it is knocked out of the hand.
  - A dummy with its other hand left will walk to the nearest loose gun within 10 m and pick it up,
    then fight one-handed.
  - No hands: it can't shoot at all.
- **One-handed rules** (both for dummies and, via the H / right-stick-click test toggle, for you):
  more spread (x3 rifle, x1.6 pistol), more recoil, slower reload (x1.8). `CombatRulesConfig` has the
  toggle "one hand can't reload" (DECISIONS #6).
- A weapon table near your spawn with a spare rifle and the pistol. T restocks it with the dummies.
- The M1 debug gun is gone.

## Constitution principles served
- **1 Fun core first:** guns that kick and pop limbs, and gibs that splash, are the core loop.
- **5 Legible risk/reward:** shooting a gun hand visibly disarms; a one-handed enemy is visibly worse.
- **3 Learnable challenge:** fixed multipliers per region, all visible in one asset per gun.
- **9 Performance budget:** pooled gibs, pooled decals, pooled audio voices, pooled tracers.

## Problem, hypothesis, test (principle 10)
- **Problem:** M1 limbs popped but felt weightless; there was nothing to fight.
- **Hypothesis:** hitstop + shake + blood + distinct pops make each hit readable and satisfying; enemies that
  shoot back and can be disarmed make limb targeting a real choice.
- **Smallest test:** rows 1-10 below in the arena, Attack mode.
- **Accept if:** you can say which part you hit without looking at the HUD, and disarming a dummy changes how
  you fight it.

## Playtest checklist (please fill in; I won't guess these)
| # | Do this | Pass if | Result |
|---|---|---|---|
| 1 | Shoot off a hand, a leg, the head | Each has its own pop, blood, a short freeze; feels good | |
| 2 | Watch gibs land | Splat decals appear, squelch sound, they bounce a bit | |
| 3 | Wait 30 s after a big fight | Gibs shrink away, no pop-out | |
| 4 | Spray full auto, then aim and tap | Hip fire spreads, aimed shots group tight, recoil climbs and settles | |
| 5 | Empty a magazine | Ammo counts down, auto reload, magazine dips | |
| 6 | G until "Attack" | Dummies approach and shoot bursts; edge flash points at the shooter | |
| 7 | Shoot an armed dummy's gun hand | Gun drops; dummy walks to it and picks it up with the other hand | |
| 8 | Shoot a gun out of a dummy's hand | Gun flies away | |
| 9 | Take both hands | Dummy stops shooting | |
| 10 | Pick up the pistol from the table (E) | Rifle is thrown, pistol in hand, prompt and HUD update | |
| 11 | H, then fire both guns | One-handed is clearly worse; pistol suffers less | |
| 12 | F3 with 6 dummies firing and lots of gibs | Note avg / worst 1 % ms here: | |
| 13 | Is it fun to shoot now? (1-7) | | |

## What I could NOT verify
- **Nothing has been compiled or run.** No Unity editor in the cloud machine. Expect compile errors on first open;
  send me the Console list.
- Gun feel numbers (spread, recoil, damage, fire rate) are first guesses.
- Dummy gun aim: the gun is parented to the hand and the arm pose is procedural, so the gun might point
  slightly off where the shots go (shots are aimed from the muzzle at you, not along the barrel). A dummy only
  fires when you are within 30 degrees of where it faces.
- URP decals need the Decal renderer feature; setup adds it via reflection. If no splats appear, send me the log.
- If you ran setup before this change, your `MannequinConfig` keeps bounciness 0.35; set it to 0.5 in the Inspector.
  An old `DebugGunConfig.asset` can be deleted.

## Assets needed for M2 + M3 (placeholders are procedural until then)
- **Audio** (WAV, short, dry): 4-6 limb pops (small, medium, big/head), 2 squelches, hit ping, rifle shot
  (close), pistol shot, dry-fire click, magazine out, magazine in, gun clatter on floor, distant enemy shot.
- **Textures:** 3 blood splat decals (512 px, alpha), 1 blood droplet sprite, 1 muzzle-flash sprite, 1 spark sprite.
- **Models:** CC0 AK-47-style rifle and a pistol with child transforms named Grip, SupportHand, Muzzle, Magazine
  (or I add them). Candidates in `Assets/ThirdParty/Weapons/README.md`. Log each in `docs/asset_licence_ledger.md`.
