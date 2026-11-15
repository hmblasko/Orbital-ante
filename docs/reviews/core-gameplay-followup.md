# Core gameplay follow-up for Fable

15 November 2026. Review base: `d715ee109450ee2ce98fd3839821704a317c7f76` on `claude/playtest` (PR #38). This follow-up preserves Fable's enemy startle, gun scramble, hobble and wound-lean changes. Matthew requested the gameplay implementation; Fable 5.2 continues to lead design, assets, presentation and feel. Every new tuning number below is an initial trial for Fable, not a balance verdict.

The code adds crouching, peeking, bounded wall-running and ledge climbing; remembers the player's ship after exit; introduces a small fleet using existing ships and actors; allows enemy fire to damage an occupied vessel and its exposed occupant; and corrects terrain support and weapon timing. No art, shader, model, audio, scene, prefab, `.meta` or config asset is changed by this follow-up.

**Unity, Unity physics, visuals, feel and Steam Deck performance have not been run.** Compiled PlayMode drafts are pending engine checks. The ground changes address identified source defects; they do not establish that all clipping is gone.

## Changes and design hypotheses

| Area and relevant files | Problem and implemented behavior | Hypothesis and constitution principles |
|---|---|---|
| Stance and peeking: `Player/FpsController`, `PlayerConfig`, `StanceRules`, `CameraClearance`, `CabinHeadClearance`, `Ship/Astronaut`, `InteriorWalker`, input display labels | The player lacked crouching and side peeks. Height changes keep the boots fixed, retry blocked standing, reduce crouched speed and suppress sprint. Camera clearance includes the near-plane corners, with an additional check after smoothing. The cabin uses its analytical interior profile and obstacles rather than testing against the convex outer hull. | Controlled exposure and low-clearance routes offer meaningful choices. Test whether the stance is legible without adding control burden: #2 agency, #3 learnable challenge, #7 cognitive load. |
| Traversal: `Player/ParkourRules`, `ParkourMotor`, arena and suit controllers | Sprinting forward through an airborne, parallel wall contact can start a finite wall run. Jump pushes away. A fresh airborne Jump press with forward input can acquire a clear ledge; hold Jump to finish, release to drop. Sweeps test the capsule's initial position, route and destination each step. | Predictable movement choices can support expressive escape and approach routes. The smallest test is one readable wall and one ledge, before constructing content around them: #1 core gameplay, #2 agency, #3 learnable challenge, #10 evidence. |
| Ship locator: `UI/ShipLocator`, `ShipLocatorMath`, `Space/SolarSystemWorld` | Leaving the ship previously gave no persistent bearing. The locator captures the original ship, draws its bearing and distance through occlusion, and uses a screen-edge pointer behind or outside the view. Boarding does not silently change ownership; `Track` is the explicit ownership-change API. Unavailable targets produce a status instead of an invented position. | Finding the ship should support exploration rather than become a memory chore: #4 discovery, #7 cognitive load. |
| NPC vessels and crew: `Enemies/EnemyFleet`, `EnemyShipCrew`, `EnemyShipRules`, `EnemySpawner`, `EnemyConfig`, `Ship/Ship`, `HatchLift`, `Space/TestCraft`, `Cockpit/CockpitDash` | Up to two existing camp actors become pilots of reused starter landers. Local guidance approaches, hovers, lands, disembarks and reboards. Actors retain their member identity. Pilot and lift ownership prevent competing control; NPC craft bypass global player input and use the same fuel/damage thrust limits. Boarding requires the hatch to be physically open, a free lift and valid cabin dimensions. | A visible arrival and an open hatch create understandable threats and choices without increasing the actor cap: #2 agency, #3 learnable challenge, #5 risk/reward, #6 replayability. |
| Occupied-ship combat: `AI/CombatWorld`, `MannequinCombat`, `Weapons/Ballistics`, `Ship/Ship` | Exterior enemies can see and attack the occupied vessel through its first hull hit. Damage goes to the actual struck ship part. An enemy in the cabin can hit the logical occupant while the exterior suit object is inactive; physical hull, terrain and analytical interior walls still take precedence when nearer. Target identity changes reset stale aim/tells; moving between seat, cabin and lift on the same ship does not repeatedly grant a fresh reaction delay. Armed and unarmed visibility share the same target rules. | The ship can provide physical protection while remaining vulnerable in understandable ways: #3 learnable challenge, #5 legible risk. |
| Planet collision: `Planets/PlanetWorld`, `PlanetSurface`, `TerrainCollision` | Mandatory support now follows canonical cube-face/tile containment and angular footprint distance instead of a coarse-height tile-center bound. Support persists under parked craft when the eye leaves. Parent colliders remain until the relevant child/cave collider is ready. Cave exemptions cover bounded mouth/tunnel volumes. Support and recovery use matching planet/mover physics poses. | Reliable contact is a prerequisite for trusting landing and traversal: #1 core gameplay, #3 learnable challenge, #10 evidence. |
| Weapon reliability: `Weapons/FirearmState`, `EnemySpawner` | Continuing automatic fire carries fractional frame lateness without a catch-up volley. Release, reload, drop, hand loss and empty magazines end that carry and respect the last actual shot time. Quiet bloom recovery also follows actual shot time. Enemy cleanup checks `WeaponHold.Dropped`, so a captured player-held gun with no enemy holder is retained. | Identical configured rates should behave consistently across frame rates, and captured weapons should remain reliable: #3 learnable challenge, #10 evidence. |

## Controls and trial defaults

Existing action names, bindings and stable IDs are preserved. Only controls-menu display labels clarify the reused actions; saved rebinds continue to refer to the same actions. Defaults below apply unless rebound.

| Action | Keyboard | Controller | Context |
|---|---|---|---|
| Crouch | Left Ctrl | B / east button | Grounded arena/suit and aboard. The same action remains down jets while airborne. Holding crouch through departure/climbing cannot silently start down jets; release first. |
| Lean left/right | Z / C | LB / RB | Arena, upright suit and aboard. These actions retain roll in free-floating posture. Opposite peeks cancel. |
| Sprint | Left Shift | Left-stick click | Forward movement; no sprint while crouched. |
| Jump / climb / wall push | Space | A / south button | A fresh airborne press plus forward input requests a ledge. Hold to finish the climb, release to drop. Wall-running also requires sprint and parallel airborne travel. |

Deep Breath keeps Q / right-stick click. Select remains reserved. Check simultaneous inputs on Deck, including sprint/jump and lean/aim/fire, before accepting the mapping.

`PlayerConfig` trials: crouch height **1.15 m**, eye **1.02 m**, speed share **0.5**, stance speed **3.5 m/s**; peek distance **0.28 m**, roll **12°**, transition **7/s**, minimum head radius **0.14 m**. Geometry can reduce a peek or prevent standing.

Parkour trials: minimum gravity **1 m/s²**; wall reach beyond the capsule **0.22 m**, minimum speed **3.5 m/s**, duration **0.9 s**, gravity share **0.3**, fall-speed limit **2.5 m/s**, maximum forward/normal dot **0.35**; wall push **4 m/s** away and **5 m/s** up. Ledge reach **0.65 m**, rise **0.45–1.65 m**, climb speed **2.8 m/s**, timeout **1.5 s**. One wall run and one climb can be spent per safe landing; changing walls or releasing does not refill them.

Traversal is implemented for the arena and the upright exterior suit under sufficient gravity. Free-floating space controls, seats and lift travel retain their existing modes. Cabin climbing is excluded because `InteriorWalker` furniture does not define solid climbable height geometry. No copied movement animation, design or asset is introduced.

NPC trials: **two ships maximum**, drawn from the existing **12 live actors**. Default approach/hover heights are **40/14 m**, speed limit **16 m/s**, guidance acceleration **24 m/s²**, settled interval **1.2 s**, settle speed **1 m/s**, arrival distance **1.5 m**, ground clearance **5.6 m**, descent speed **2 m/s**. Hover/outside/cabin intervals are **4/18/12 s** and boarding seek range **12 m**. Actor-specific stream names isolate ship choices from existing camp random draws. The starter walker/lift accepts upright capsules up to **1.8 m tall / 0.3 m radius**; oversized roster members remain ground actors.

## Agent review

Independent source reviews covered Unity-facing traversal/contact behavior, occupied-ship combat, fleet authority and lifecycle, stance/view ordering, terrain support, weapon timing and UI cost. Findings corrected before handoff:

- Climb anchors now use the supporting Rigidbody's physics position and rotation, including child-collider offsets. Interpolated rendering does not define physics anchors. Destroyed support bodies terminate the climb rather than reinterpret stale coordinates.
- Arena stand/climb queries use the actual CharacterController skin width. Moving supports use relative sweeps against their compound shape and full world sweeps against other blockers. CharacterBody can block clearance; only world solids can be grabbed. Full query buffers refuse movement.
- Wall continuity uses the Collider reference; the plain airtime budget needs only a presence token. There is no new `GetInstanceID()` call.
- Pilot/lift cancellation releases only its owner. Egress carries point velocity, including rotation and center-of-mass offset. Oversized actors cannot be placed into or board the fixed starter cabin.
- Exterior ship-part damage and interior occupant damage remain distinct. World/hull occlusion wins over a more distant logical player hit. Fable's expanded `ClearGoals` reset survives integration, including scramble, reach, look and hold state.
- Interrupted automatic fire cannot borrow scheduling credit that was already consumed by its prior burst. Actual cooldown also governs quiet bloom recovery.
- Locator positions use the same rendered frame as the eye, then the double-precision reference frame; cached geometry and limited text updates avoid rebuilding UI each frame.

These are source findings and fixes, not observations of Unity behavior. Final creative and feel review remains with Fable.

## Checks performed outside Unity

The session-local compatibility harness was invoked exactly as follows:

```bash
python3 /workspace/work/core-gameplay-check/compile_workspace.py --runtime --plain-suite /workspace/work/core-gameplay-check/all-plain-ship-regression.json --engine-suite /workspace/work/core-gameplay-check/all-engine.json
git diff --check
```

Both commands exited **0**. The harness and manifests live in this execution workspace, not in the repository; this is an evidence record, not a new CI entry point.

| Check | Result | Limit |
|---|---|---|
| Runtime compatibility compilation | **210 C# source files compiled** | Roslyn with older Unity 2021.3 reference assemblies; Unity 6 names are mapped only in temporary copies (`linearVelocity`, damping, `PhysicsMaterial`, object-finding APIs). Input System 1.11.2 and a uGUI 6000 compatibility assembly were referenced. This is not a Unity 6 project build. |
| Plain production rules and tests | **233 passed, 0 failed, 0 skipped** | Actual unmodified selected rule/test sources compiled and executed with .NET SDK 8.0.428 and NUnitLite 3.13.3, without Unity references or engine behavior shims. This is the selected suite, not every repository test. |
| PlayMode source compilation | **10 files / 84 source cases compiled** | Six new files contain 50 cases; existing Weapon, Ship, Spacewalk and Navigation files contain 34. Metadata-only `UnityTest`/`UnityTearDown` attribute shims allow compilation. **None of these cases executed.** NUnit's netstandard 2.0→2.1 compatibility warning CS1701 remains. |
| Patch whitespace | Passed | No runtime assurance. |

The 233 executed cases comprise: ship rules **34**; enemy ship guidance **17**, ownership **7**, cabin fit **11**; parkour rules **20**; stance rules **7**; terrain collision **8**; locator math **21**; ray-hit volumes **22**; existing firearm rules **14**, cadence **61**, cadence boundaries **11**.

No engine draft is intentionally expected to fail on the candidate. They still need the actual Unity Test Framework and native physics. An unexpected fixture failure must be investigated rather than treated as proof of a game defect or skipped as harmless.

## Smallest engine and playtest pass

Fable should run the existing local Unity workflow first. For a manual run:

1. Check out the follow-up PR locally, with Unity closed. Follow `docs/CLAUDE_CODE_LOCAL.md` for the installed editor path.
2. At the repository root, run `bash tools/check-in-unity.sh` on macOS/Linux, or `powershell -ExecutionPolicy Bypass -File tools\check-in-unity.ps1` on Windows. Run the full command, including PlayMode.
3. Read `unity/Logs/oa-check/summary.txt`, the test XML and logs. Record real counts and fix compile/test failures before assessing feel.
4. Open the project and compare the following fixed-seed scenarios against Fable's base. Record the seed, body, control device, frame cap and failed expectation. Use **1280×800** for readability checks.

| Minimal scenario | Acceptance criteria to evaluate in Unity |
|---|---|
| Arena wall, low ceiling and ledge; repeat on a planet with another local up direction | Boots stay fixed through stance changes; no standing through ceilings; neither eye nor near-plane corners pass through solids during a peek. Aim and enemy hit exposure follow the visible eye. Wall-run requires deliberate forward/sprint contact; one press yields one wall push. Release, change walls and spam Jump: no unlimited midair refresh. |
| Climb a static block, an interpolated translating/rotating support and a child collider; add a thin blocker during movement | Full-capsule checks stop blocked climbs, including ceilings and CharacterBody. Moving support remains stable without dragging through another solid. Releasing Jump, timeout, removed support and gravity/posture changes leave coherent momentum. Actual CharacterController contact at its configured skin permits a legal climb and stand. |
| Ground→air transitions while holding crouch; free-flight, tether and lift transitions | No unintended down jets until physical release. No free-flight wall-running. Lean/roll and existing Match Velocity, breathing and tether functions keep their expected modes. The first-person death pose is retained. |
| One camp with two eligible pilots; player holds every flight input while watching | No new actors beyond the live cap, no mirrored player commands and no camera/focus takeover. Each lander completes approach→hover→land→exit→return with existing damage/fuel gates. Death, disabling a crew member, lost hands and contested seats/lifts release authority. No crew duplication when camps sleep/wake. |
| Player's hatch closed, opening, open, then closing; simultaneous NPC/player lift requests | NPCs board only while physically open and through the lift. A closed/opening hatch refuses entry. One owner occupies the lift; unrelated cancellation cannot release it. Large crew cannot clip into the cabin. Exit from a moving/rotating ship inherits its point velocity. |
| Exterior enemy firing at an occupied ship, then an enemy inside its cabin | Visible hull permits attacks. The struck leg/window/hull receives part damage rather than blanket cabin damage. Intact intervening hull and terrain stop shots. A clear interior shot can damage the inactive-suit occupant; analytical walls stop blocked interior shots. No stale marksman beam or burst survives stowing/piloting. |
| High-relief terrain, cube-face seams and a cave mouth with slow streaming; park the ship and walk away | Both suit and ship retain collision support while tiles refine. Cave openings become traversable only after their collision is ready. Being deep under a cave mouth is not an infinite exemption from sinking recovery. Planet interpolation and reference-frame changes do not redirect support. Watch F3 rescues; a continuously rising count fails this pass. |
| Leave the ship, turn around, move behind terrain and trigger a reference-frame change | Bearing and distance remain tied to the player's ship. Behind-view pointers stay at a readable edge; bottom text avoids suit status. The marker hides on map/pause/own cabin, seat and lift; a missing ship shows truthful unavailable status. |
| Hold automatic fire at 30/40/60/120 fps; test release/repress, hitch, reload, hand loss and captured guns | Configured sustained rate stays consistent without multiple shots in one frame or a post-hitch volley. No short interruption bypasses the actual interval. Captured guns survive owner-corpse/camp cleanup. Compare both player and enemy pressure: restored cadence and bloom are material balance changes even with unchanged damage. |

For feel, start with a brief repeated route and encounter rather than a long run: can the player intentionally peek, traverse, regain the ship and explain how an enemy entered? Fable should adjust timing, camera motion, controls, boarding intent and threat readability based on that observation. No subjective fun claim follows from passing code checks.

## Limits and remaining work

Autonomous flight is local approach/hover/landing/reboarding, not a complete interplanetary tactics or orbital dogfighting system. Enemies use existing handheld guns to attack ships; mounted ship weapons and new combat/boarding animation assets are outside this follow-up. Existing ship art, cabin dimensions and animation remain Fable's work.

Before merge, the actual Unity 6 import/build, full tests, moving-body/cave/contact regressions, input combinations, Fable's feel review and Deck measurements are still required. See `docs/performance_budget.md` for source caps and first profiling targets. No claim of 60 fps, complete noclip elimination or zero bugs is made.
