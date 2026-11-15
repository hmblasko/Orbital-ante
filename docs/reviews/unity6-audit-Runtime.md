# Unity 6 runtime audit — main

Source-only support review for Claude and Fable. Scope: `unity/Assets/OrbitalAnte/Scripts/Runtime/` on `main`, commit `96264e13187659d90dba198703bba188721f4e23`. The project records Unity **6000.0.23f1**, URP **17.0.3**, Input System **1.11.2**, and uGUI **2.0.0**. This audit does not include the unmerged ship/look branches. No code, config, shaders, assets or standing rules changed.

No Unity execution, compilation, PlayMode run, rendered capture or device measurement was performed. Findings describe source paths and conditions to check, not established runtime failures or a compatibility certificate. Ranked by potential to affect play, with explicit trigger conditions. Proposals are for Claude's technical review and Fable's design/feel decisions; none chooses new tuning values.

Paths below are relative to `unity/Assets/OrbitalAnte/Scripts/Runtime/`. Line numbers are from the pinned commit.

## 1. Moving animated hitboxes and shot queries lack an explicit synchronization boundary

**Rank: first to investigate; conditional on physics transform-sync settings and script order.**

`Dismemberment/MannequinLocomotion.cs:80–140` moves the mannequin root through its Transform and applies the limb pose in `Update`. `Player/PlayerWeapons.cs:95–113` also runs in `Update`, calling aim and firing; the aim ray is at `:117–125`, the shot query at `Weapons/Ballistics.cs:38–51`. No explicit execution order or `Physics.SyncTransforms` boundary joins these paths. The physics settings asset is not present in this snapshot, so this audit cannot establish the applied `Physics.autoSyncTransforms` value.

With automatic transform synchronization disabled, collider Transform edits may not be reflected in a raycast until the next synchronization/physics step. Separately, if the weapon query executes before locomotion/pose, it can query the previous pose even with automatic synchronization enabled. A visible hand/limb and the collider sampled by a shot may therefore represent different instants. This is not an instruction to move all CharacterController or visual work to `FixedUpdate`: those have different responsibilities.

**Smallest engine check:** use a laterally moving/posing mannequin and a stationary firing camera, inspect collider position and query timing with the applied sync setting recorded. Repeat at render rates above and below the fixed rate, aiming at a small extremity. **Proposal:** Claude should establish one deliberate pose/query synchronization policy and avoid paying for global `SyncTransforms` before every individual ray. Principles **3 (learnable challenge)** and **5 (legible risk)** require a visible hit to mean what the player expects; **10** asks for evidence before changing architecture.

## 2. The gib cap does not bound active physics during overflow fades

**Rank: source finding with impact dependent on detachment burst size.**

`Dismemberment/GibManager.cs:108–122` counts only chunks that are not fading. Overflow calls `BeginFade`; `Dismemberment/Gib.cs:61–66` only sets fade state and duration. It leaves Rigidbody/colliders active until `GibManager.Update` releases the object after the fade (`:147–184`). `Spawn` can create another container in the meantime (`:94–105`). The nominal cap therefore is not a hard limit on Unity physics objects. Shrinking active colliders during fade also continues to change their collision geometry.

**Smallest engine check:** burst detachments past the configured cap before overflow fades finish, recording total active rigidbodies/colliders versus nonfading count and the frame/physics profile. **Proposal:** Claude and Fable should distinguish the intended hard physics budget from a visual fade grace allowance. This audit does not select immediate recycling, a new fade duration or a new cap. Principles **1 (fun core)** and **10 (evidence-based iteration)** protect the most energetic moment from a misleading limit. The related cost report gives the precise budget mismatch.

## 3. Flight can retain cached thrust/look commands across pause/resume

**Rank: small lifecycle edge, likely limited to the first resumed physics step.**

`Space/TestCraft.Update` returns while paused (`:118–120`), leaving `ThrustInput`, `Boosting`, `MatchingVelocity` and potentially `_lookPending` unchanged. Its `FixedUpdate` consumes those cached values (`:206–237`) and does not clear them on pause or map disable. `Core/GameBootstrap.cs:277–283` disables/re-enables the play action map through `OnPauseChanged`, but does not reset this component's cache. Unity can run the first resumed fixed step before the next `Update` refreshes the controls.

**Trigger:** hold thrust or match velocity, open pause, release the button while paused, then resume. A cached command may be consumed once before the next render-frame sample. A steering delta accumulated before pause can similarly remain pending. This is a source inference about the transition; it was not observed in a running player.

**Proposal:** ask Claude to clear/sample cached commands deliberately at pause and control-lock transitions, retaining the intended input responsiveness. **Smallest engine check:** the hold-pause-release-resume sequence, with thrust/steering input and Rigidbody delta logged around the first resumed fixed step; include controller and mouse. Principles **2 (agency)** and **3 (learnable challenge)** favor commands matching what the player currently holds, not hidden leftover input. No feel value is proposed.

## 4. Gravity registration depends on the order a scene creates its objects

**Rank: conditional integration edge; the current scripted flight build uses the safe order.**

`Space/GravityBody.Awake` disables Unity gravity (`:19–22`). `OnEnable` registers only if `SolarSystem.Instance` already exists (`:25–28`). `SolarSystem.Build` assigns the instance (`Space/SolarSystem.cs:69–72`), but does not sweep already-enabled GravityBody objects. Thus an enabled loose object created before Build can have both flat gravity disabled and no membership in the solar-system mover list. Its `OnDisable` also asks the current global instance rather than remembering the system that registered it (`GravityBody.cs:30–32`).

The normal `SolarSystemWorld.Build` creates/builds the system before the craft (`Space/SolarSystemWorld.cs:25–39`), and `SolarSystem.Place` explicitly registers its mover (`:186–193`), so this is **not** a claim that the current default craft floats unregistered. It matters to a serialized scene, pooled loose object or a test fixture that changes that creation order.

**Proposal:** Claude should make registration ownership/retry explicit if those creation orders are supported. **Smallest engine check:** create an enabled GravityBody before building a system, then compare registration and acceleration with an otherwise identical body created afterward; also disable/re-enable across a system replacement. Principles **3** and **10** support consistent motion and a focused lifecycle case. No new object/feature is requested.

## Areas inspected without asserting an API defect

- **Physics placement:** solar motion and flight forces use `FixedUpdate`, `Rigidbody.MovePosition`/`MoveRotation` and Unity 6 `linearVelocity` (`Space/SolarSystem.cs:230–246`, `:328–347`, `Space/TestCraft.cs:206–237`). The arena's CharacterController uses frame-delta movement intentionally (`Player/FpsController.cs:43–57`, `:147–172`); “movement in Update” alone is not a defect.
- **Renamed APIs:** this source uses `PhysicsMaterial`, `PhysicsMaterialCombine`, `linearVelocity`, `angularDamping` and `FindFirstObjectByType`, consistent with its recorded Unity 6 target. There is no basis here to request a blanket rename back to older APIs. Source inspection cannot prove all platform/player paths compile.
- **Input System:** actions are sampled in the expected update paths; pointer deltas and gamepad rates are separated (`Player/FpsController.cs:114–136`, `Space/TestCraft.cs:136–141`). Pause leaves the fixed System/UI maps available, and interactive rebinding disables its target action before starting (`UI/ControlsPage.cs:283–295`) and disposes the operation in completion/cancel callbacks (`:310–325`). No legacy `Input.GetKey` migration is proposed.
- **Late destruction:** `GameBootstrap.DestroyWorld` deactivates the old world before deferred `Destroy` (`Core/GameBootstrap.cs:237–247`). Several singletons guard their teardown with identity checks. Those existing protections should not be removed while addressing resource ownership or registration edges.
- **Serialization/data:** this source uses Resources-backed ScriptableObjects with code defaults and JsonUtility save DTOs. No identified source finding justifies editing serialized YAML, standing rules, field names or save versions in this documentation task. A player persistence/rebinding exercise is still needed; no save compatibility claim is made.
- **URP 17:** the main branch has planet shaders and the reflection-based decal backend, but not the runtime look/post-processing system now under PR #8. Shader availability, reflected `DecalProjector` members and the setup renderer feature must be exercised in a Linux player build; a fallback path in source is not proof that the intended projector backend renders. No specific wrong URP 17 member name was established in this audit, so none is invented.

## Review handoff

Ask Claude to reproduce the two highest play risks first: shot/pose synchronization and overflow physics count. Then exercise pause transitions and the alternate registration order. Keep input, physics settings, fixed timestep, source SHA and actual configuration in the evidence packet. The companion draft `docs/reviews/deck-cost-audit.md` expands the native resource, collider-work and allocation findings; all cost magnitudes remain unmeasured. Fable retains feel and design decisions, Claude checks code/tests before merging, and this report does not clear a quality gate or expand Phase 1.
