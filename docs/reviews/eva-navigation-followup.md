# Space exits and correctness fixes for Fable's navigation

Baseline: Fable PR [#30](https://github.com/hmblasko/Orbital-ante/pull/30), `claude/milestone-10-navigation` at `3dd187e29a40138f6a3a7372b94bc4aacff9c9dc`. Fable's implementation is authoritative. The earlier standalone Codex navigation alternative, [#31](https://github.com/hmblasko/Orbital-ante/pull/31), was closed after #30 appeared during this work. Do not combine two controllers.

This is a support follow-up: optional-ground belly exit/reboarding and contained correctness fixes in Fable's existing controller. It retains Fable's target/autopilot algorithms, nose alignment, held velocity matching, fuel and crash rules, camera controls, dash designs and tuning values. No art, shaders or assets are changed. Fable keeps creative, design, presentation and tuning authority.

Status: code, source reviews, standalone rule execution and compatibility compilation complete. Unity execution, actual flight/exit behavior, collision response, readability, feel and Deck performance remain unverified.

## User-facing behavior and scope

Fable's controls remain: **T / X** locks the body under the nose, **R / B** toggles autopilot, and **F / A** matches the selected body's velocity while held. Target selection persists when looking away. The cockpit's existing dash/camera bindings remain. No competing velocity-toggle binding is added; matching and arrival retain Fable's behavior rather than the closed alternative's latched hover mode.

The added exit flow is **unbuckle → open the belly hatch → walk onto the well → descend**, whether landed, coasting or in space. A missing ground ray selects a release point below the whole suit. Disappearing ground continues the descent without snapping back to the floor. Release assigns the ship's point velocity at the suit's centre of mass, including the ship's rotation, before handing control to the existing outside controller.

A full capsule query rejects a blocked release; a narrow ground ray alone cannot detect an off-axis wall. Short lift settings receive enough working reach for suit clearance and reboarding. A released player is not automatically pulled back in, but **Interact** can explicitly reboard from the beam, including a maximum-reach endpoint. Repair and physical hatch-button interactions keep priority; looking away from those targets exposes the boarding prompt. Existing broken-hatch and physical-obstruction rules still apply.

This is not complete Milestone 11. The existing suit has no new jetpack, tether or resource model. Its inherited air-steering branch can alter motion in stronger gravity even without walking input; release-time velocity inheritance does not establish ballistic motion thereafter. Zero-G maneuverability, boarding discoverability and the whole transition need Fable's Unity rehearsal.

## Correctness fixes

1. **Map-open autopilot pays fuel.** Fable deliberately lets autopilot run while the map is open. The dash previously treated the UI control lock as zero thrust for fuel accounting. It now charges seated autopilot `PilotPushShare` independently of that lock, while manual/camera controls retain their existing gating. Fuel values and the surcharge are unchanged.
2. **Automatic stops discard stale jets.** A stop for arrival, obstruction, missing target or fuel could clear `PilotOn` then fall through into manual force using the previous autopilot display push. Automatic stops now clear that request, and the autopilot physics branch returns even if it stopped. Several catch-up physics steps cannot reuse it. A manual takeover retains the real input already sampled that frame.
3. **Reference velocity is evaluated at the ship.** A rotating frame includes a position-dependent angular term. Subtracting the target-centre local velocity from ship-local velocity introduced artificial drift for distant targets. Both matching and autopilot now evaluate the body's translational velocity at the ship's true position. Fable's near-ground spin reference remains.
4. **Physics uses one moment.** `SolarSystem` advances published celestial states before Unity integrates the dynamic ship. A retained `PhysicsStates` array captures the start after frame/recenter selection and before gravity/advance. Physics guidance, target offsets, blocker checks and matching use that snapshot; UI uses the current published states. This supplies coherent inputs to Fable's planner without replacing it.
5. **Leaving the seat/death cancels immediately.** Ship flags were updated before the astronaut could release the belt later in the frame. Seat/mode transitions now synchronize `Unmanned`; its setter cancels autopilot and clears cached inputs. Death does the same without changing cause, slump or end-page timing. Locked manual physics also refuses stale input; map-open autopilot still runs through its earlier dedicated branch.

These address source-visible implementation problems, not inferred feel failures. They support constitution principles 1, 2, 3, 5, 7 and 10. No player benefit or enjoyment improvement has been measured.

## Files

- `Ship/Astronaut.cs`, `HatchLift.cs`, `Ship.cs`, `ShipConfig.cs`: no-ground exit, safe release, explicit reboard, minimum working reach and immediate pilot-state synchronization.
- `Space/TestCraft.cs`, `SolarSystem.cs`: current controller boundaries and coherent reference sampling.
- `Cockpit/CockpitDash.cs`: map/autopilot fuel accounting only.
- `Tests/EditMode/ShipExitRulesTests.cs`; `Tests/PlayMode/ShipExitTests.cs`, `NavigationCorrectnessTests.cs`: regression definitions.
- This report and [performance notes](../performance_budget.md#m10-support-follow-up-eva-and-navigation-correctness).

Runtime paths above are relative to `unity/Assets/OrbitalAnte/Scripts/Runtime`; test paths are relative to `unity/Assets/OrbitalAnte`.

## Checks actually performed

**42 plain C# cases passed outside Unity, 0 failed/skipped:** nine new exit cases plus 33 current Fable ship-rule regressions. The runner compiled direct production/test sources with NUnit/NUnitLite 3.13.3 and executed on .NET 8.0.34, without Unity or Input System assemblies or behavior substitutes. Cases cover no-ground descent, full-suit rule clearance, short reach, ordinary ground, disappearing ground, maximum-reach reboard, closing doors and a complete lift round trip.

**157 runtime C# files and ten new engine case definitions compatibility-compiled with exit 0; engine cases were not run.** Six navigation cases cover catch-up stop/manual takeover, actual unbuckle input/death, rotating-frame translation/ground matching, start/end state separation and map fuel. Four exit cases cover translating/rotating release, missing ground and an off-axis obstructing wall. The queued-force assertions use Unity 6's real API through reflection; the coroutine fuel case needs a real engine frame. Compilation supplies only UnityTest attribute metadata, never a simulated coroutine or physics engine.

Exact commands:

```text
python /workspace/work/eva/compile/compile_workspace.py --runtime --plain-suite /workspace/work/eva/compile/ship-rules.json --engine-suite /workspace/work/eva/compile/navigation-correctness-engine.json
python /workspace/work/eva/compile/compile_workspace.py --engine-suite /workspace/work/eva/compile/ship-engine.json
```

This is **not a Unity 6 project build**. The harness uses Roslyn from SDK 8.0.428, older `UnityEngine.Modules` 2021.3.33 references, actual Input System 1.11.2 source and official uGUI `6000.0` source. Temporary copies map Unity 6 velocity/damping/material/find-object names to older equivalents. Supporting uGUI copies omit two unsupported `RectTransform.sendChildDimensionsChange` assignments and supply a `PenStatus.None` metadata enum; Unity 6000-specific package defines/InputForUI are omitted. NUnit compilation emits CS1701 framework-reference warnings. Repository code is not changed for those compatibility steps. Full package/API/editor integration remains unverified. Fable's separately reported large rule-suite results are not claimed as runs performed here.

An additional temporary integration check combined AI [#32](https://github.com/hmblasko/Orbital-ante/pull/32) with Fable's #30, then overlaid this follow-up's seven runtime files: **164 combined runtime sources compatibility-compiled, exit 0**. The AI/#30 merge-tree operation reported no conflicts. No existing branch or asset was changed; the scratch tree is not a merged release or evidence of playable integration. Command:

```text
python /workspace/work/ai-fable-integration-check/compile_workspace.py --runtime
```

Current source hashes match the successful compilation/rule inputs. `git diff --check` passed. Unity/PlayMode, screenshots, controls/layout, atmosphere/wind, complete travel/landing/exit sequences and Deck profiling remain outstanding.

## Support review

Independent source reviewers used the repository's Unity, game-design and Deck-performance checklists. They are Codex support reviews, not Fable approval. Final reads found no blocking issue in scope. Findings addressed include off-axis suit overlap, exact-reach reboarding, map fuel, stopped-push reuse, reference-point/time mismatch and immediate pilot loss. Design review retained boarding-priority discoverability and inherited outside steering as explicit M11 rehearsal items. Deck review found one reusable body-state array/copy and one capsule query per release; no measured frame-time claim follows.

## Fable's smallest rehearsal

1. Open `unity/` using [SETUP](../SETUP.md). In **Window > General > Test Runner**, run `ShipExitRulesTests` and `ShipRulesTests` in **EditMode**, then `ShipExitTests` and `NavigationCorrectnessTests` in **PlayMode**. Record actual results.
2. In the orbital scenario, lock a visible body with T/X, start R/B autopilot and compare fuel use with the map closed/open for the same demanded push. Confirm map controls/camera behavior and nose alignment remain as designed.
3. Trigger arrival, missing target, obstruction and empty-fuel stops with previous nonzero thrust; include several physics steps before another rendered update. No cached jet request may become manual thrust. Manual movement should still take over at once.
4. Match a distant body while another planet's frame rotates, then match near rotating ground. Check labeled speed and actual drift through anchor/frame changes. Unbuckle or die during active autopilot and confirm immediate cancellation while the first-person/death flow remains intact.
5. While coasting and rotating in space, unbuckle, open the hatch and descend with no ground. Confirm release clearance and inherited motion, then look away from repair/hatch targets and Interact to reboard. Repeat landed, with disappearing/max-reach ground, very short configured reach and an off-axis wall. A blocked release must return inside rather than activate an overlapping suit.
6. At 1280×800, inspect prompts with remapped inputs. On Deck compare CPU/GC, physics and frame-change/exit spikes against Fable's baseline, including the existing cockpit cameras. Ask whether target, velocity reference, cancellation and boarding choices are understandable; collect enjoyment separately from task success.

Acceptance requires correct functional boundaries and readable choices without changing Fable's intended controls or mechanics. Standalone results do not satisfy engine/player acceptance. Fable chooses final design/presentation and decides integration; no existing branch is pushed, and nothing is merged or deployed here.

Enemy tactics and its eight-source research are separate in #32; [read the research](https://github.com/hmblasko/Orbital-ante/blob/2e59eaf976e826ba29ad35ea0a1825970c4f5b6c/docs/reviews/swarm-tactics-research.md). The closed #31 is reference material only and is not required by this follow-up.
