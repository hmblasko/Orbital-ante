# Persistent targeting, autopilot, velocity lock and space exits

Original implementation baseline: Fable's launch branch at `5556189d89284d7b4d5caa4f385e69fb78ed2235` (PR #23). Integrated onto its updated tip `c51a7dacbf51ec049649d590bf51f4e985473b58`, which includes Fable's audio-system and launch integration. This is a small follow-up for that branch, as Matthew requested, rather than a second copy of its pending ship/look/launch work in a main-targeted PR. Fable retains design, assets, presentation, feel and tuning authority.

Status: implementation, source reviews, plain-rule execution and compatibility compilation complete. No Unity Editor/player, engine test execution, flight footage, Deck measurements or player outcomes are claimed.

## Behavior

Looking at a planet, gas giant or moon produces a candidate. Pressing **Target Lock** captures that body; looking or steering elsewhere does not replace it. Pressing it on the same body or empty space clears the lock. Stars and asteroids are not destinations. The HUD distinguishes the candidate from the persistent selection, including when the locked world is behind the camera.

**Autopilot** accelerates and brakes toward the moving locked body using thruster forces. It accounts for orbital motion, modeled gravity, sideways drift, stopping distance and damaged-engine authority. It approaches a conservative body/terrain envelope plus clearance, then engages velocity lock and displays **LAND MANUALLY**. Steering and the first-person camera remain under the player's control; there is no teleport, velocity snap or automatic landing.

**Velocity Lock** continuously matches the selected body's motion. Without a lock it captures the current anchor once. Looking away or changing reference frames does not silently select another body. Near the selected body it follows rotating ground motion evaluated at the ship's position; farther away it follows orbital translation.

Existing **hold Match Velocity** remains available. Each hold captures the locked body, otherwise the aimed body, otherwise the anchor. Clearing/changing the target during a hold requires releasing that button before a new match can begin. Pause clears sampled input; map opening, leaving the seat, component disable, explicit cancellation, changed/cleared target, manual translational input, invalid navigation or insufficient thrust cancels persistent assist. Reseating/closing the map does not silently restart it. The target itself remains selected on ordinary assist cancellation.

Autopilot checks the current straight route against other celestial envelopes and refuses/cancels an obstructed course. It can temporarily refuse while the destination's existing terrain job is loading. Refusal does not remove inertia: the pilot must steer/brake or fly clear before reengaging. There is no route-around-obstacles planner, collision-free travel guarantee, atmosphere/wind feedforward or automatic terrain landing. Gas-giant aiming handles inside-nominal-radius views; cloud-interior behavior still needs Unity inspection.

The belly lift no longer requires ground below. Unbuckle, walk to the open well and descend into space while the ship is moving. Release preserves the ship's linear and rotational point velocity at the suit's centre of mass. The whole suit clears the belly before activation, and a capsule check rejects an obstructed release. If ground disappears during descent, the ride continues down without returning to the cabin floor or snapping backward.

The release point remains within the lift's working reach. Automatic pickup does not immediately undo the exit; **Interact** explicitly reboards without needing to step out of the beam. Looking at a damaged part still prioritizes repair, and the hatch button keeps its existing interaction priority. Very short lift settings receive enough working reach for full-suit clearance and reboarding. Existing broken-hatch and physical-clearance rules remain. Outside movement is still the existing basic suit; a complete jetpack/tether system is separate work.

## Default controls

All new flight actions appear in the existing rebindable Controls page. Select/View remains reserved.

| Action | Keyboard | Controller |
|---|---|---|
| Target Lock toggle | T | Right stick press |
| Autopilot toggle | P | X / west face button |
| Velocity Lock toggle | V | D-pad down |
| Match Velocity while held | F | A / south face button |
| Unbuckle | B | Y / north face button |
| Hatch | G | D-pad left |
| Interact / repair / explicit reboard | E | Y / north face button, on-foot map |

The existing `TestCraftConfig` exposes starting guesses for Fable: clearance **150 m**, autopilot acceleration **30 m/s²** (also limited by unboosted thrust and damage), cruise **300 m/s**, response **0.5 s**, arrival tolerance **3 m / 0.5 m/s**, other-body clearance **20 m**, aim forgiveness **7°**, manual-cancel threshold **0.1**. Existing match thrust remains **60 m/s²** before damage. These are implementation defaults, not validated tuning.

## Engineering and checks

`FlightNavigation` and `FlightGuidance` are plain C# rules. `TestCraft` translates their commands into physics and explicit assist states. `SolarSystem.PhysicsStates` retains the start-of-step celestial snapshot: the solar system otherwise publishes end-of-step states before Unity integrates the dynamic ship. Every physics guidance conversion uses the same start snapshot. Render/readout code uses current published states. No orbital model is reevaluated just to reconstruct the old step.

**58 plain-rule cases passed outside Unity: 18 navigation cases, 8 new exit cases and 32 existing ship-rule regressions; 0 failures.** The NUnitLite 3.13.3 runners compiled production/test files directly on .NET 8.0.34 without Unity, Input System or game-assembly behavior substitutes. Guidance cases integrate bounded forces over time toward moving/accelerating destinations at 0.02/0.05 s steps, including fast closing/sideslip, braking, clearance, arrival and continued matching. Other cases cover reference invariance, invalid inputs, authority, target/assist state, obstruction geometry, no-ground descent and reboarding.

**149 runtime C# files and 18 new engine case definitions compiled with a compatibility harness; the engine cases were not executed.** Those definitions comprise 14 navigation and 4 exit cases. They cover real-input boundaries, persistent selection, inside-radius aiming, frame references, snapshot timing, map/seat/pause transitions, held-match retarget release, blocked routes, no-thrust refusal, centre-of-mass release and off-axis obstruction. They do not establish the complete playable flight/exit sequence.

This was **not a Unity 6 project build**. Compilation used Roslyn from SDK 8.0.428, older `UnityEngine.Modules` 2021.3.33 references, actual Input System 1.11.2 source and official uGUI `6000.0` source at `58c0ead281dc1cf042e8831606850413524971bb`. Temporary copies mapped Unity 6 velocity/damping/material/find-object names to older equivalents. Supporting uGUI copies omitted two unsupported `RectTransform.sendChildDimensionsChange` assignments and supplied a compile-only `PenStatus.None` enum. InputForUI and Unity 6000-specific package defines were omitted. NUnit emitted CS1701 framework-reference warnings. No source was changed for these compatibility steps, and no engine behavior was simulated by them.

Final integration commands (local compatibility harness, outside Unity):

```sh
python /workspace/work/navigation/compile/compile_workspace.py --runtime --engine-suite /workspace/work/navigation/compile/navigation-engine.json
python /workspace/work/navigation/compile/compile_workspace.py --engine-suite /workspace/work/navigation/compile/ship-engine.json
```

Both commands exited 0. The harness was configured for the integrated worktree; it compiled the complete runtime, including Fable's new audio and launch integration. Source-hash comparison, rather than a new execution, establishes that the 58 earlier plain-rule results apply to the unchanged rule/test inputs.

`git diff --check` completed without whitespace errors. After integration onto `c51a7dacbf51ec049649d590bf51f4e985473b58`, all 149 runtime sources and both engine suites were recompiled successfully. The 14 direct production/test source files used by the previously passing 58 plain-rule cases matched their successful-run hashes exactly, so those unchanged cases were not rerun. Final runtime and engine source hashes matched the integrated files at handoff. Full Unity package/API integration, physics, collisions, UI/controller layout, responsiveness, flight feel and Deck performance remain unverified.

Exact standalone commands used (scratch harness, not a shipped Unity build path):

```text
python /workspace/work/navigation/compile/compile_workspace.py --plain-suite /workspace/work/navigation/compile/navigation-rules.json
python /workspace/work/navigation/compile/compile_workspace.py --plain-suite /workspace/work/navigation/compile/ship-rules.json
python /workspace/work/navigation/compile/compile_workspace.py --runtime --engine-suite /workspace/work/navigation/compile/navigation-engine.json
python /workspace/work/navigation/compile/compile_workspace.py --engine-suite /workspace/work/navigation/compile/ship-engine.json
```

The rule runs used the original reviewed worktree; final runtime/engine compilation used the integrated worktree. The results and limits above apply to these commands.

## Agent review

Independent Codex support reviewers used the repository's Unity, game-design and Deck-performance checklists; these were not reviews conducted by Fable itself.

- **Unity:** addressed a start/end-of-step reference mismatch with the retained snapshot; canceled assist synchronously at seat transitions; added an actual capsule-vs-scene check before suit release. Final source review found no outstanding blocker. These fixes still require engine execution.
- **Design:** forwarded navigation abort notices through the visible ship HUD during map/unbuckling, named the common speed/drift reference and retained the manual-landing arrival instruction. Final source review found the requested functional scope represented without new Phase 2 systems or camera/asset redesign.
- **Deck:** no source-level cost blocker found. Existing body scans, marker creation/layout and the new per-step state copy require profiling; configured body counts are not an enforced 150-body cap. [Budget notes](../performance_budget.md) describe frequencies and scope without a measured frame-time claim.

## Smallest playtest and acceptance

Problem: aim-following selection, incomplete navigation assistance and a ground-only lift interrupt intended flight/EVA choices. Hypothesis: explicit locks, predictable assist/cancel behavior and motion-preserving exits make those choices understandable and usable. This serves constitution principles 1, 2, 3, 5, 7 and 10. No enjoyment improvement has been observed.

1. Open `unity/` using [SETUP](../SETUP.md). In **Window > General > Test Runner**, run `FlightNavigationTests`, `ShipExitRulesTests` and `ShipRulesTests` in **EditMode**, then `FlightNavigationPlayModeTests` and `ShipExitTests` in **PlayMode**. Record actual editor results.
2. Start the orbital scenario. Look at a visible planet/moon, press Target Lock, then look away. Confirm the selected name remains. Retarget, clear it, and repeat with controller and remapped controls. Hold Match Velocity, change/clear the target and confirm matching waits for physical release before recapture.
3. Engage Autopilot toward a moving moon after terrain is ready. Inspect acceleration, sideways correction, braking and clearance; confirm arrival engages velocity lock and requests manual landing. Try a blocked course, a departure inside another body's envelope, damaged/no-authority engines and a gas-giant interior. Record refusals and any unsafe behavior rather than treating the route guard as a guarantee.
4. Cross a rotating/inertial reference boundary while matching; confirm the labeled speed and drift agree without a frame-change kick. Pause/release controls/resume, open/close the map, unbuckle/reseat and disable/re-enable flight with nonzero assist demand. Each cancellation must stop assist immediately and remain canceled until requested again.
5. While coasting and rotating in open space, unbuckle, open the belly hatch and descend. Confirm no ground is required, the suit inherits motion without a kick, and the lift does not immediately recapture it. Look away from repair/hatch targets and use Interact to reboard. Repeat landed, with disappearing ground, a short configured reach and an off-axis obstacle; blocked release must return inside safely.
6. At 1280×800, inspect long remapped labels and all refusal/arrival states with map open and closed. On Deck, compare CPU/GC/physics/text rebuilds against the baseline in equivalent scenes and seeds. Ask players to explain their selected body, velocity reference and cancellation cause before offering hints.

Acceptance requires correct persistent selection, coherent reference motion, bounded physical commands, explicit cancellation, readable states, no-ground exit/reboard and preserved repair/hatch priorities. Failures require revision; standalone checks do not satisfy these engine/player criteria. Fable chooses final defaults and presentation.

## Integration and stopping point

This follow-up depends on Fable's PR #23 and is intended for its branch, with the reviewed changes transplanted onto `c51a7dacbf51ec049649d590bf51f4e985473b58`. The three-way application retained Fable's `ThrusterSound` audio hook and changed no launch/audio asset or implementation file. It does not update that branch or merge anything. It overlaps the effective-binding helper and flight-help areas in unmerged PR #26; when combining both, retain group/composite-aware labels and reconcile that PR's refresh gate/revision invalidation with the new navigation hints.

Research/AI swarm work is delivered separately. Automatic landing, obstacle routing, a full jetpack/tether, art/cockpit creation, actual Unity execution and Deck/player evaluation are outside this patch. Implementation and review stop at a concrete draft for Fable to integrate and rehearse.
