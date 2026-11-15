# Seeded-randomness audit — main

Source snapshot: `96264e13187659d90dba198703bba188721f4e23` (`main`). Scope: all C# under `unity/Assets/OrbitalAnte/Scripts/Runtime/`, against the `CLAUDE.md` Part A rule that repeatable gameplay/world generation uses `SeededRandom` and named streams. This is a source-reading report for Claude to check and Fable to decide on; no Unity run, replay, test result or performance measurement is claimed. No game code, tuning or standing rules were changed.

Paths below are relative to `unity/Assets/OrbitalAnte/Scripts/Runtime/`. Line numbers refer only to the stated commit. Proposed stream names are review suggestions, not existing APIs or approved design decisions. A named stream should be acquired once and retained; `GameSession.Stream(name)` starts a fresh generator on each call (`Core/GameSession.cs:52`). For per-entity streams, use a stable spawn identifier, never a Unity instance ID. Seeded choices do not by themselves make PhysX or frame-driven input bit-identical across machines.

## Gameplay-affecting findings

| Priority / location | Source behavior and trigger | Why it crosses the visual-only boundary | Proposed isolation and smallest check |
|---|---|---|---|
| High: `Player/PlayerWeapons.cs:87`, consumed by `Weapons/Weapon.cs:111` | Dropping/swapping the player's gun samples three `UnityEngine.Random.Range(-4,4)` values for its rigidbody spin. | The dropped gun is a persistent, colliding, hittable and collectible item; spin changes collisions and final pose, potentially its resting position and pickup exposure. Its spin depends on unrelated particle/audio/global RNG draws. A visual-scatter allowance does not isolate this physical object from gameplay. | Propose `combat.player.weapon-drop`, retained for the player/run, or a stable drop-event fork. Under the same seed and scripted drop event, add unrelated visual draws before the drop and compare the assigned angular velocity. Principle **3, learnable challenge**, and **10, evidence-based iteration**. |
| High: `AI/MannequinCombat.cs:119` | Every AI weapon drop uses `Random.onUnitSphere * spin`; drops happen on disarm, loss of the holding arm and death (`:123`, `:131`, `:146`). | It writes the same persistent weapon body's angular velocity. A global VFX draw can change an enemy's dropped-gun trajectory/settling and subsequent pickup opportunity. Enemy AI actively seeks those weapons (`:310`). | Propose `combat.mannequin.<stable-spawn-id>.weapon-drop`, separate from aim/decision RNG so added physics draws do not change burst choices. Compare disarm/drop angular velocities with extra unrelated visual draws. Principles **2, agency**, **3**, **10**. |
| High: `Dismemberment/Mannequin.cs:194` and `:211` | Torso-destruction fallback outward direction and every detached gib's angular velocity use Unity's global RNG. | Gibs retain `BodyPart` colliders, can be shot and split again, and interact through rigidbody collisions. The fallback also changes linear launch, not just tumble. The normal non-degenerate outward direction is deterministic, but the fallback and all spin are not derived from the run seed. | Propose `combat.mannequin.<stable-spawn-id>.gibs`, forked by a stable detachment event/group ID. Compare assigned initial velocities for an ordinary limb pop and a degenerate torso-outward case with extra visual RNG draws in between. Principles **1, fun core**, **3**, **10**. |
| Medium: `Player/CameraShake.cs:52`, `Player/FpsController.cs:144`, `Player/PlayerWeapons.cs:122`, `:185` | Shake samples Perlin noise using absolute `Time.unscaledTime * 26`; the camera rotation includes that shake, while aim and damage rays use the camera's transform. | Although shake is feedback, its transform feeds the aim ray (at least through the preceding `LateUpdate`). Starting the same seeded run later in the application changes the phase without changing the seed or input. This is a time-based aim dependency, not random world generation. Whether shots should follow the shaken view is Fable's decision; the source path should not be treated as cosmetic isolation. | If repeatable shaken aim is required, propose `combat.player.camera-shake` for a stable phase and a run/event-relative elapsed-time input. If shake should be presentation only, Claude/Fable should decide how the aim frame is kept separate. Record ray origin/direction for an identical hit-and-fire sequence started at different application uptimes. Principles **3, learnable challenge**, **7, cognitive load**, **10**. |
| Medium: `AI/MannequinCombat.cs:444`, followed by `:450`, `:452`, `:466`; shot origin at `:393` | One-handed rifle sway uses `Time.time * 5.3 + GetInstanceID() * 0.01`. | The comment says “only for show”, but the result feeds smoothed aim/aim-ready eligibility and the weapon transform; the real firing origin is that weapon's muzzle. Unity instance IDs and absolute engine time do not identify a seeded mannequin. This creates a source-level route to different shot origins/timing near cover; actual visible impact needs a replay check. | Propose `combat.mannequin.<stable-spawn-id>.pose` for a phase, plus run/event-relative time; keep it separate from burst/spread draws. Compare one-handed aim-ready transitions and muzzle origins across same-seed arena rebuilds. Principles **3**, **5, legible risk**, **10**. |

The physical gib groups are distinct from the blood particle droplets. There is no proposed change to launch strength, spin magnitude, aim error or any other tuning value.

## Full global-RNG / System.Random inventory and exclusions

Every runtime occurrence of `System.Random`/`UnityEngine.Random`/`Random.*` from the source search is accounted for below. The fixed-seed `System.Random` calls can produce different cosmetic data across runtime implementations, but no gameplay or world-generation consumer was found in those call paths. They are not reported as seeded-world defects.

| File and lines | Uses | Classification |
|---|---|---|
| `Player/PlayerWeapons.cs:87` | Dropped weapon spin | Gameplay physics finding above. |
| `AI/MannequinCombat.cs:119` | Dropped weapon spin | Gameplay physics finding above. |
| `Dismemberment/Mannequin.cs:194`, `:211` | Gib launch fallback and spin | Gameplay physics finding above. |
| `Weapons/WeaponAudio.cs:67` | Random source pitch | Audio variation only in this source. Allowed cosmetic use; no named gameplay stream required. |
| `Weapons/WeaponAudio.cs:79`, `:100` | `new System.Random(77/78)` generating audio samples | Fixed cosmetic clip synthesis; no gameplay consumer found. If exact sound assets must later repeat across runtimes, a dedicated fixed cosmetic `SeededRandom` stream would be a separate asset requirement. |
| `Weapons/WeaponFx.cs:229`, `:230`, `:246`, `:260`–`:262` | Flash rotation/scale, impact-mark size, spark velocity/size/lifetime | Presentation particles/marks. `Weapons/Ballistics` resolves damage before these effects; no feedback into its ray was found. These draws currently also perturb the shared global RNG used for the physical findings above, which is why physical streams should be isolated. |
| `Gore/GoreAudio.cs:105`, `:116`, `:154` | Random pitch; fixed-seed audio synthesis | Cosmetic audio only. |
| `Gore/DecalPool.cs:172`, `:263`, `:315` | Splat rotation; fixed-seed generated splat/noise images | Cosmetic marks/textures only; not world layout. |
| `Gore/GoreFx.cs:203`, `:218`–`:220`, `:231`, `:281`–`:282` | Blood decal size and particle positions, velocities, sizes and lifetimes | Blood VFX only. These are distinct from hittable physical gibs. No blood-particle damage consumer was found. |
| `Planets/PlanetWeatherFx.cs:229`–`:230`, `:240`, `:253`, `:255`, `:258`, `:266`, `:277` | Thunder pitch/clip; lightning delay, placement, shape and lifetime | Cosmetic lightning/thunder in this commit. Wind and atmosphere gameplay come from `WeatherModel`/`PlanetWorld`, not bolt placement. This does not establish that a future damaging lightning feature could retain the same RNG path. |

`Core/SeededRandom.cs:10` mentions `System.Random` only in a comment; `Planets/FormationGenerator.cs:18`, `:70`, `:363` use a field named `Random` whose type is **SeededRandom**, not Unity's global generator. `Core/Vec3d.cs:76` is a vector `GetHashCode`, not a seed source in the inspected generation call paths.

## Time sources: intended progression versus seed leakage

- **Allowed new-seed selection:** `Core/GameSession.cs:41` combines `Environment.TickCount` and `DateTime.UtcNow.Ticks` only to choose a new run seed. That method's purpose is new randomness; `SetSeed`/same-seed restart do not need to reproduce the choice of a new seed. Do not replace it with a stream derived from a seed that has not been selected yet.
- **Seed plus explicit simulation time:** `Space/SolarSystem.cs:233`–`:245` advances `SimTime` from physics steps; `Planets/WeatherModel.cs:73`, `:82`, `:93` evaluates weather against a supplied time. The same seed at a different simulation time is expected to give a different orbital/weather state. This is not clock-derived random world placement. Request equal-time comparisons, not images after unequal play durations.
- **Gameplay timers:** `Dismemberment/MannequinLocomotion.cs:184`–`:192` obstacle avoidance, `AI/MannequinCombat.cs:233`, `:366`–`:387` reactions/burst timing, `Player/FpsController.cs:151`–`:169` coyote time/gravity, `Player/PlayerHealth.cs:39`, `:54`–`:55` regeneration, and `Arena/TestArena.cs:93`–`:107` respawn use ordinary elapsed timers. Replacing timers with RNG streams would be incorrect. Frame-dependent collisions/input still mean this project is not a deterministic replay engine.
- **Unscaled pickup lockouts:** `Weapons/Weapon.cs:109`, `Player/PlayerWeapons.cs:141` and `AI/MannequinCombat.cs:320` intentionally store/compare unscaled drop age. A pause can expire those real-time lockouts without advancing physics. This is a clock-domain question for Fable/Claude, not a random seed source; compare pause behavior separately rather than prescribing a named stream for a cooldown.
- **Presentation/administration clocks:** `Gib.cs:83`/`:94` splat throttling, `GibManager.cs:134` fade age, Gore particle/decal ages, weapon VFX lifetimes, `PlanetWeatherFx.cs:119` effect updates, `PlanetSky.cs:186` lamp fade, `PlumeFx.cs:93`–`:109` visual plume cycle, UI refresh/rebind blocking, `PerfMonitor.cs:44` samples and `GameBootstrap.cs:259` played-time bookkeeping are not procedural placement seeds. `Core/TimeFx` changes gameplay speed intentionally as hit feedback; it must be compared with the same event sequence, not declared a seed violation for using a timer.
- **Clock/identity leaks requiring attention:** camera shake and one-handed AI sway are called out above because their supposedly cosmetic values reach actual aiming transforms. Their propagation, rather than the mere presence of `Time.*`, is the relevant evidence.

## Seeded paths that already isolate world generation

These source examples are useful regression targets, not a claim of executed reproducibility checks:

| Source | Existing stream/seed arrangement |
|---|---|
| `Arena/TestArena.cs:324`, `:343`, `:347` | `arena.mannequins` derives layout and then seeds locomotion/combat. The subordinate seeds are deterministic draws from that named stream. |
| `Space/AsteroidBelt.cs:16`, `:20` | `solar.asteroids`, forked by asteroid index. |
| `Space/SolarSystemModel.cs:95` | `solar.body.<body-id>` for orbit/spin phase, isolating body addition/order better than one global stream. |
| `Planets/FormationGenerator.cs:54`–`:56` | `planet.<body-id>.<formation-id>` for run variation; named `landmark.<body-id>.<formation-id>.<index>` with seed zero intentionally keeps authored landmarks stable across runs. |
| `Planets/PlanetScatter.cs:27` | Named per-body/per-face/per-cell scatter streams. |
| `Planets/WeatherModel.cs:50` | `weather.<body-id>.<weather-index>` and explicit simulation-time evaluation. |
| `Planets/FeatureShapes.cs:66`, `Space/SpaceMeshes.cs:81`, `:179`, `Space/CelestialBody.cs:100` | Fixed deterministic mesh/shape/star variation rather than global RNG. These are asset variants, not uncontrolled wall-clock generation. |

One policy caveat is worth preserving without overstating it: `Player/PlayerWeapons.cs:21` uses a retained `new SeededRandom(12345)` for shot spread (`:185`) and recoil yaw (`:195`). That is repeatable for the same component lifetime/shot order, **not** global-RNG nondeterminism, but it is not named or derived from the run seed. A proposed `combat.player.spread` / `combat.player.recoil` split would express the intended ownership if Fable wants those sequences tied to the run seed; this report does not decide that different run seeds must produce different recoil patterns.

## Suggested confirmation packet for Claude

1. Keep the cited source SHA and identical run seed/input event sequence; record initial velocities/ray directions before relying on a visual comparison.
2. Repeat the relevant action after extra cosmetic RNG draws. World placement and physical outcome initialization should remain unchanged once their streams are isolated.
3. Repeat the same action after rebuilding the same seed at a later application uptime; inspect camera and AI-sway propagation explicitly.
4. Compare generator outputs at the same simulation time, and distinguish a deterministic initialization claim from a bit-identical full PhysX replay claim.

Nothing in this packet has been executed here. Claude should check the findings and run the appropriate rule/PlayMode checks before merging any later fix; Fable retains decisions about feel and what must be seed-dependent.
