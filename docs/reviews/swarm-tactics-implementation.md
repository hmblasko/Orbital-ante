# Coordinated enemy flanking and attacks

Prepared for Matthew's request for deliberate swarm tactics rather than random running. Implementation began at `main` commit `224642ed5bf6da5553334c02b0f7236864cd8b64`; the draft is based on `5df0a8121d58501955c1ce16d32618684b01d497`, whose runtime is unchanged by the intervening delegation/tooling update. Fable 5.2 retains enemy creation, design, art, presentation, feel and tuning authority.

Status: research, implementation, source reviews and standalone checks complete. Unity execution, actual route following, combat feel and Deck measurements remain unverified. The same eight arena enemies use the new system; this does not add enemy types, factions or a larger spawn population.

## Research and architectural choice

The [research review](swarm-tactics-research.md) documents eight original developer publications, their technical constraints and transfer limits. It compares individual behavior trees/FSMs, coordinated utility-based tactics and GOAP/HTN planning. These are industry case studies and engineering references, not experiments proving greater enjoyment in Orbital Ante.

The implemented choice is a **shared squad coordinator, scored tactical positions and individual execution**. A full general-purpose planner or encounter director would add substantial machinery without supplying the missing perception, routes or coordination. This hybrid uses the existing weapons, animation and dismemberment rules.

| Research principle | Concrete implementation |
|---|---|
| Tactical position and route evaluation (Beij/Straatman; Jack) | Hard clearance/reachability/LOS filters precede scoring; bounded A* follows the actual arena footprints. A good angle cannot excuse a blocked route or firing position. |
| Separate group decisions from execution (Orkin) | The coordinator assigns work; `MannequinCombat` keeps reaction, gun, reload, aiming and damage behavior. Interrupts can remove an actor from its assignment. |
| Separate spatial reservations from attack capacity (Dawe) | Spaced destinations and finite rotating fire leases are independent constraints. Death, disarm, reload, loss of sight and disable release eligibility. |
| Knowledge comes from observations (Welsh) | Timestamped shared sightings replace live-position pursuit through walls; firing requires fresh personal sight and a current clearance check. |
| Stable complementary roles (Karlsson) | Anchor, left/right flank and support positions persist until meaningful change, invalidity or a bounded stuck retry. Casualties promote a new anchor. |
| Pacing is distinct from tactics (Booth) | No director, hidden spawn adjustment or adaptive difficulty was added. |
| Bound local work and stagger expensive decisions (Reynolds) | Round-robin sensing, one plan per frame, finite candidate/node/query caps and reusable buffers. |

This is an original implementation of general techniques; no proprietary code, encounters, visual designs or assets were copied. Source weights/capacities are not imported as a scientifically optimal recipe.

## What enemies now do

An **anchor** maintains a useful firing line while mobile members seek **left and right flank** positions. Flanks must reach the correct side and a configurable minimum angle, have a traversable route and see the last observed target position. **Support** members use a wider preferred range and separate goal reservations. If geometry or a bounded search cannot supply a valid tactic, the actor holds and retries; it does not invent a random sidestep.

The group uses shared **last-known information**. Only sensing code reads the living player's transform. When sight is lost, routes use the recorded location; new hidden movement does not continuously redirect the group. Knowledge expires after a configured interval. Existing range-plus-world-LOS sensing remains 360 degrees: no audio perception or FOV-cone system is claimed.

An actual shot requires a ready weapon, usable hands, fresh personal sight, completed reaction, a stationary aiming stance, an attack lease, current muzzle/world clearance and a clear teammate centre lane. Shared contact alone cannot authorize blind fire. Existing spread, recoil, burst/reload timing, one-handed penalties and damage values remain. A denied query fails closed.

Sensing freshness and reaction acquisition are separate. Deferred or stale observations still prevent firing, but they do not continually restart the reaction timer in a large group. A **confirmed** lost sighting restarts reaction on reacquisition. This avoids a scheduling-induced state where continuously visible enemies never become ready.

Movement uses four-way grid routes around the real arena obstacles, inflated for standing/prone bodies. The physical blocker sweeps each proposed step against the world, player and other characters. A blocked step waits. Timed lack of progress triggers a bounded replan using a capped snapshot of teammate occupancy, allowing a route around a parked teammate when space exists. A sealed/crowded route may still require a deliberate hold; this is not a general crowd solver or path-time reservation system.

Disarmed members seek a reachable dropped gun or protection. Weapon recovery still works without player contact and while hold-fire is enabled. The final approach connects to the actual gun position rather than stopping at a coarse grid cell outside pickup range. Reloading does not reshuffle an armed flanker; lost mobility removes flank eligibility. Head-only/handless actors cannot borrow attack permission.

Disabled actors release goals, reservations and leases. Re-enabling registers the same actor once or fails closed if its identity has been replaced. Duplicate IDs cannot attach distinct actors to one permit. Coordinator disable stops movement/shooting and clears contact; respawn forgets old sightings/front rather than handing them to newly spawned enemies. Hold-fire stops combat pursuit/shots while preserving weapon recovery.

## Files and tuning

Plain C# rules: `SquadTactics`, `SightReaction`, `TacticalGrid`, `TacticalPlanner` and `TacticalQueryBudget`. `SquadCoordinator` adapts them to sensing, movement, weapons and actual world queries. `MannequinCombat`, `MannequinLocomotion`, `CombatWorld` and `TestArena` connect the existing actors. Two `ProjectSetup` registrations create the config through the existing setup path; no asset YAML is added.

`Resources/Config/TacticalAIConfig` contains provisional engineering defaults for Fable. Constructor-snapshot values require restarting the arena after changes.

| Setting | Starting value |
|---|---|
| Participant capacity / simultaneous attackers | 32 / 3; hard maxima 64 / 8 |
| Senses per frame / sight interval / freshness | 4 / 0.18 s / 0.45 s |
| Shared memory | 5 s |
| Attack lease / rest | 1.3 s / 0.35 s |
| Role interval / significant front turn | 2.5 s / 60° |
| Minimum flank angle | 35° |
| Plan interval / minimum goal hold / stuck retry | 0.8 s / 2 s / 1.5 s |
| Anchor / flank / support range | 14 / 11 / 19 m |
| Reserved-goal spacing | 2 m |
| Grid spacing / body clearance | 1.5 / 0.7 m |

All values need Fable's playtest-led tuning. [Performance notes](../performance_budget.md#coordinated-arena-tactics-source-limits-not-measurements) list actual hard bounds, including work outside the A* expansion count. A capacity of 64 is not evidence that 64 animated, shooting actors sustain 60 fps on Deck.

Diagnostics and public model/query state support development inspection and tests; a complete tactical overlay or player-facing telegraph system was not added. Fable chooses visual/audio cues that make the pressure readable without exposing implementation details in normal play.

## Checks and review

**56 plain C# cases passed outside Unity: 23 new tactic/path/reaction/budget cases plus 33 existing body/firearm regressions; 0 failed.** NUnitLite 3.13.3 ran the actual production/test files on .NET 8, without Unity, game-assembly behavior substitutes or simulated physics. Cases cover 64-member fair permits, sight/memory expiry, eligibility interruption, 3→2→1 anchor promotion, meaningful hard-angle/LOS flanks, 32 distinct reachable deterministic goals, thin/sealed walls, corner clearance, teammate detours, exhausted search/query budgets and slow sensor cadence versus confirmed-hidden reacquisition.

**Nine new PlayMode fixtures were compiled, not executed.** They cover actor disable/re-enable/duplicate identity, coordinator disable, respawn memory, hidden movement/expiry, physical wall/idle behavior, denied movement budget, teammate firing lanes/disarm and coarse-grid gun recovery without contact/with hold-fire. Compilation supplies only UnityTest attribute metadata; it does not run a coroutine or model the engine.

Whole runtime compatibility compilation completed against older engine references. This is **not a Unity 6 project build**. It uses Roslyn from SDK 8.0.428, `UnityEngine.Modules` 2021.3.33, actual Input System 1.11.2 source and official uGUI `6000.0` source. Temporary copies map Unity 6 velocity/damping/material/find-object names to their older equivalents. Supporting uGUI copies omit two unavailable `RectTransform.sendChildDimensionsChange` assignments and supply a `PenStatus.None` metadata enum; Unity 6000-specific package defines/InputForUI are omitted. NUnit can emit CS1701 framework-reference warnings. Repository sources are not altered for these compatibility steps. The two editor setup registrations were source-reviewed; no editor build is claimed.

Exact standalone commands used (scratch harness, not a shipped Unity build path):

```text
python /workspace/work/swarm/compile/compile_workspace.py --runtime --plain-suite /workspace/work/swarm/compile/tactics-regression.json --engine-suite /workspace/work/swarm/compile/tactics-engine.json
python /workspace/work/swarm/compile/compile_workspace.py --runtime --engine-suite /workspace/work/swarm/compile/tactics-engine.json
```

The combined rule run reported 56/56 passes, including all 23 new cases. After the final engine lifecycle/fixture edits, runtime and fixture compilation ran again: all 122 runtime files and nine engine fixture definitions compiled with exit 0. Engine fixtures remain unrun. The earlier separate 23-case artifact is not the final snapshot. `git diff --check` completed without whitespace errors, and current production/test inputs matched the final successful-run hashes at handoff.

Independent support reviewers followed the repository's Unity, design and Deck checklists; they were not Fable itself. Findings addressed before handoff include stale disable/identity/reset state, inaccessible exact-gun approaches, missing no-contact recovery, blocked-route stalls, missing replacement anchors, near-frontal fallback flanks, deferred-sensing reaction starvation and repeated full-grid nearest-cell scans. The largest remaining planning watchpoint is bounded teammate occupancy work plus search-buffer initialization, outside the node count. No current source blocker remains in the scoped adapter; engine and player evidence remains outstanding.

## Playtest protocol and acceptance

Problem: random sidesteps, live hidden-player chase and uncoordinated pressure made enemy movement arbitrary. Hypothesis: complementary routes, limited shared knowledge and finite attack opportunities make threats understandable and give the player useful counterplay. This serves constitution principles 1, 2, 3, 5, 7, 8 and 10. No improvement in enjoyment/fairness has been observed.

1. Open `unity/` using [SETUP](../SETUP.md); run the normal project setup so `TacticalAIConfig` is created. In **Window > General > Test Runner**, run `SquadTacticsTests`, `TacticalGridTests` and existing `BodyModelTests`/`FirearmStateTests` in **EditMode**, then `SquadCoordinatorTests` and existing mannequin/weapon suites in **PlayMode**. Record actual editor results.
2. Start the combat arena with one fixed seed; keep weapons, health, enemy count and geometry constant when comparing baseline/candidate. Toggle the existing hold-fire control to begin. Look for an anchor maintaining pressure while two mobile enemies use different reachable sides. Role labels alone do not satisfy acceptance: routes must actually arrive and flank angles must be useful.
3. Move behind cover, change position while unseen and wait beyond shared-memory expiry. Enemies may act on the last observed location; they must not follow hidden current movement or fire from shared memory alone. Reacquisition should preserve the intended reaction delay. Include low frame rates and larger development populations to exercise deferred sensing.
4. Disarm/kill the anchor, then another member. Confirm a surviving armed member supplies pressure. Remove a flanker’s legs, empty a gun and deny a firing lane with another body. Existing ability/reload/spread rules must remain, permits must rotate within their cap, and ineligible/disabled actors must immediately stop using permission.
5. Block a route with a parked teammate and a world obstacle. The mover should wait safely, retry through the single-plan lane, take an available detour or hold with no random veer. Test dropped guns near cell boundaries with a coarse grid, no contact and hold-fire enabled. Reset/disable/re-enable actors and the coordinator; no old contact or duplicate identity may leak into the next encounter.
6. At 1280×800, ask players what the group is trying to do and what response they can choose. Record fairness, comprehension and enjoyment separately from deaths/damage. Fable chooses telegraphs, ranges and pressure; fewer deaths or more shots are not proof of a better experience.
7. In a Deck Development build, use **Window > Analysis > Profiler** with Deep Profile initially off. Compare CPU time, GC allocation, planning/physics spikes and route/probe counts during normal pressure, crowded/stuck routes, recovery and reset. Test present and stress populations separately at 30/60 fps; do not infer performance from the plain-rule suite.

Acceptance requires purposeful reachable flanks, pressure continuity after casualties, no hidden live-position chase, bounded fair attack permission, preserved damage/weapon behavior and safe blocked-route handling. Recurring stalls, route oscillation, ineffective angles, unexplained perfect knowledge or unreadable pressure require revision. Small formative sessions can expose those problems; they cannot establish population-level enjoyment or replace sustained playtesting.

## Where this stops

The adapter targets the static flat arena. It adds no NavMesh, dynamic-world rebuild, stairs/jumps, planet/ship/zero-G enemy navigation, full sensory model, predictive aim, full spread-cone friendly-fire simulation, director, faction strategy, multiplayer or new content. Endpoint spacing is not full path-time reservation. Capacity/query exhaustion can intentionally cause a hold.

The research and concrete implementation are ready for Fable to review and rehearse. Nothing is merged or deployed, and no existing remote branch is updated. Ship navigation and space exits are a separate follow-up PR against Fable's launch branch.
