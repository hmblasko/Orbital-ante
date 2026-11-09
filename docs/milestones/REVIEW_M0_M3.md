# Code review of Milestones 0 to 3 (9 Nov 2026)

Matthew asked for a quality review before more was built. I read every script in `Assets/OrbitalAnte`
(about 6,300 lines) line by line. There is still no compiler on the cloud machine, so this was a reading
review: it removes the errors I could find by reading, it does not prove the project compiles.

## Fixed: would not have compiled
| File | Problem |
|---|---|
| `Fx/DecalFx.cs` | A static method used an instance field directly (`next`). |
| `Editor/ProjectSetup.cs` | Used URP's `DecalRendererFeature` class by name, but that class is not public. It is now found by name at run time. |

## Fixed: would have behaved wrongly
| Area | Problem | Fix |
|---|---|---|
| Dummies aiming | The walk animation and the aim pose both moved the gun arm every frame, so the arm would have hung about halfway between "swinging" and "aiming". | An arm that is aiming is posed only by the aim code. |
| Dummies shooting | A dummy fired from its muzzle straight at you even while facing another way. | It only fires when you are within 30 degrees of where it faces (in `CombatRulesConfig`). |
| Blood bursts and sparks | These particle systems were stopped and only "emitted into", which may not draw at all. | They are kept playing with emission off. |
| Shooting guns | A bullet only recognised a gun or body part if the script sat on the exact object hit. A gun model with child colliders would have been ignored. | It now looks up the parent chain. |
| Seeded randomness | Shot spread and recoil jitter used Unity's global random, against the "all randomness through the seed" rule. | Both use a seeded stream. |
| Rebinding | Pressing Esc or B on the "Already in use" prompt left both actions on the same key. | The old binding is restored first; only "Swap them" changes anything. |
| Pause menu | The button press that chose Resume could also make you jump or fire. | Input is ignored on the frame the menu closes. |
| Aim down sights | The rifle and pistol sat too high and would have covered the centre of the screen. | Lowered so the front sight tip sits just under the centre. |
| Hitstop | Mouse look could freeze if the hitstop time scale was set to 0. | Look now runs before the time check. |
| Mannequin hands | Hands were built on edge (thin side up). | Flat, palm down. |
| Placeholder sounds | Noise in the sounds differed between launches. | Uses a stable hash. |
| Weapon table | The spare rifle overhung the table edge. | Moved. |
| Pause text | "This run will end" was no longer true once runs are saved. | Says you can continue the seed. |

## Known and left as is (not bugs, worth knowing)
- A dummy's bullet can be stopped by the gun in your own hands (it counts as a hit on the gun, not on you).
- Blood decals project onto anything inside their box, including a dummy's feet standing in them.
- Bullets test up to 48 objects along their path; a huge pile of gibs in one line could hide a target behind it.
- Material colours are cached by colour only, so two materials of the same colour share one smoothness.

## Still unverified (needs Unity on your machine)
- Whether everything compiles. Send me the Console's red lines on first open.
- Whether the code-built Humanoid avatar is accepted by Unity (a warning is logged if not; the dummies do not
  depend on it).
- Whether setup's URP asset and decal feature creation work on your exact URP version.
- All feel, physics stability and performance.
