# Steam Deck cost audit — main runtime

Source-only draft for Claude to check and Fable to consider. Inspected `main` at `96264e13187659d90dba198703bba188721f4e23`, scope `unity/Assets/OrbitalAnte/Scripts/Runtime/`, against `docs/performance_budget.md` at that same commit. PR #6 ship and PR #8 look changes are **outside this main-branch audit**. No game code, shaders, assets, config values or standing rules are changed.

No Unity execution, player build, allocation recording or Deck timing was performed. Nothing below establishes an observed hitch, leak magnitude or final performance verdict. Counts/lifecycles described are source findings; costs and player impact are hypotheses requiring measurement. The budget's 16.7 ms frame target, 4 ms game-script target and 3 ms physics target are existing targets, not measured availability. Every follow-up is a proposal for the lead, not a tuning decision.

Paths below are relative to `unity/Assets/OrbitalAnte/Scripts/Runtime/`; line numbers refer to the pinned commit.

## Highest-priority findings

### 1. The advertised 40-gib limit excludes fading physics bodies

**Evidence:** `Dismemberment/GibManager.cs:94–105` enforces the cap, then allocates a new container if the free pool is empty. `:108–122` counts only `!Fading` chunks, and handles overflow by calling `BeginFade`. `Dismemberment/Gib.cs:61–66` changes only fade state/duration; it does not disable Rigidbody simulation or colliders. `GibManager.cs:147–184` releases the chunk only after its fade completes. The default overflow fade is 0.3 s (`Gore/GoreConfig.cs:32`).

**Trigger and consequence:** a burst of detachments before any overflow fade completes can exceed `maxActiveGibs` in live colliders, rigidbodies, rendered parts and `ActiveCount`, and can grow the container pool. “40 at once; oldest recycled” in the scene budget is therefore a cap on nonfading chunks, not total active physics. The initial prewarm of 24 is also below the configured 40, so even the first ordinary ramp to the nominal cap can instantiate containers during play. This is not proof that a specific arena run exceeds its frame budget.

**Proposal:** ask Claude to distinguish total active, nonfading and pooled-container counts, and have Fable choose whether the intended budget is a hard physics cap or a fading grace allowance. Do not silently choose a new lifetime, fade or cap. Smallest capture: a repeatable burst exceeding the configured nonfading cap, recording those three counts and physics/GC samples through fade completion. Principle **1 (fun core)** protects a limb-pop moment from a hitch; **5 (legible risk)** and **10 (evidence-based iteration)** prevent a misleading budget readout.

### 2. A tile's scatter can exceed the “24 placed shapes per frame” allowance

**Evidence:** `Planets/PlanetFeatures.cs:91` initializes `_spawnsLeft` from `featuresPerFrame`. `:97–110` only checks whether the remaining allowance is positive, subtracts the **whole** `placements.Count`, and loops over that whole group. `:155–205` can create GameObjects, renderers and MeshColliders during this loop. The total live feature cap is checked separately and remains effective (`:157–160`). `Planets/PlanetData.cs:43–44` describes 24 as the most put out in a frame.

**Trigger and consequence:** one requested group larger than the remaining allowance admits all of its placements up to the live cap. For example, one allowance remaining does not restrict a larger tile group to one spawn. This is a per-frame burst risk, not an uncapped total feature count. `PlanetScatter.ForTile` computes group size from density and tile area (`:19–36`), so group size is not fixed by the frame allowance.

**Proposal:** retain partial group progress or explicitly define a separate whole-group burst allowance, with the choice owned by Claude/Fable. Request a landing/low-flight sample that logs group size, feature spawns and collider assignments per frame. Principle **1** keeps movement smooth; **10** makes the documented cap meaningful without retuning it here.

### 3. The terrain collider work limit has an unbudgeted safety path

**Evidence:** `Planets/PlanetSurface.cs:402` resets `_bakesLeft=1`. `:487–496` limits ordinary `UpdateSolid` work to one tile, or an extra close tile. But `Refresh` calls `MakeSolid` directly for newly ready children of a solid parent (`:454–463`), without debiting `_bakesLeft`. `MakeSolid` creates/assigns/enables the MeshCollider (`:510–515`) on the main thread. A split can visit four children, and nested ready splits can visit more. This source assigns `sharedMesh`; it does not explicitly call `Physics.BakeMesh`, despite that name being used for the known follow-up in the budget document.

**Trigger and consequence:** crossing an LOD boundary near the craft may cook/activate more than the stated one-or-two terrain colliders in one frame. The path exists to preserve solid ground under the craft, so simply deleting it would risk falling through terrain. Feature and cave collider assignments are separate from the terrain-tile allowance as well.

**Proposal:** record all collider assignments, including this safety path, when testing a slow descent and a fast LOD-boundary crossing. Claude can consider a prepared-child handover that retains the parent until required child collision is ready; Fable should judge any visible/feel tradeoff. Principle **3 (learnable challenge)** protects landing from ground disappearance; **1** protects smooth control. No claim is made about the cost of one cook on Deck.

### 4. Repeated restarts create native resources without a corresponding owner cleanup

**Evidence:** concrete creation/teardown pairs:

| Resource | Creation | Current teardown gap |
|---|---|---|
| Arena ground material and checker texture | `Arena/TestArena.cs:176–177` | `OnDestroy` at `:63–66` resets combat state but does not destroy these resources. |
| Generated star meshes and own-shader materials | `Space/SolarSystem.cs:129–138`; `Space/SpaceMeshes.cs:209` | `SolarSystem.OnDestroy` at `:158–170` restores sky state only. |
| Per-body material and any immediate procedural picture | `Space/CelestialBody.cs:114–116` | No resource-owner `OnDestroy` in `CelestialBody`; destroying renderers does not itself destroy shared material/texture objects. |
| Gib PhysicsMaterial | `Dismemberment/GibManager.cs:63–70` | `OnDestroy` at `:76–79` only updates singleton ownership. |
| Decal runtime material/texture or fallback splat meshes | `Gore/DecalPool.cs:81–85`, `:139–142` | No disposal of those generated resources from `GoreFx.OnDestroy` (`Gore/GoreFx.cs:74–78`). |

These are different from deliberately shared, bounded mesh/material caches. `Mat.Lit`/`Unlit` cache by colour/smoothness (`Core/Mat.cs:25–71`), so repeated same-colour requests are reused. `PlanetMaterials.Configure` destroys its vertex cache (`Planets/PlanetMaterials.cs:45–67`), and `PlanetSurface` owns/destroys its ground material/picture (`:244–245`, `:301`, `:908–921`). Those existing cleanup paths should be preserved.

**Proposal:** assign ownership to per-run native objects and distinguish them from shared caches before adding cleanup. Request memory/resource counts after alternating arena/solar starts and after repeated same-seed restarts, allowing destruction to complete. Compare retained Materials, Meshes, Textures and PhysicsMaterials, rather than managed GC alone. Expected accumulation is a source-based hypothesis; there is no measured byte or leak-rate claim. Principle **1** protects a long play session; **10** requires ownership evidence rather than guessing new limits.

### 5. Combat HUD formatting still allocates every rendered frame

**Evidence:** `UI/Hud.cs:187–195` calls `UpdateCombatReadouts` every visible HUD frame and, when debug is shown, formats the aimed target. `:204–207` concatenates ammo and magazine values even when unchanged. `:215–221` obtains two binding-display strings and builds the pickup prompt each frame a pickup is in range. Numeric debug formatting is correctly throttled to 0.5 s (`:252–270`), but that does not throttle the combat strings. Setting identical Text content may avoid a rebuild; it does not undo the string allocations that happened before assignment.

**Proposal:** cache by state changes (weapon/ammo/reload/pickup/device/binding changes) and request a GC.Alloc recording while standing still with a gun and while looking at a dropped pickup. Do not choose a slower player-feedback refresh rate. Principle **7 (cognitive load)** preserves responsive readouts; **1** avoids unnecessary garbage during combat. No bytes-per-frame estimate is asserted.

### 6. Resetting the job lists does not stop already-running work

**Evidence:** `Planets/PlanetJobs.cs:25–26` cancellation only sets a flag. `ResetStatics` cancels and clears both lists (`:37–43`), while `Task.Run(next.Work)` (`:105`) has no cooperative cancellation token. `PlanetWorld.OnDestroy` can call this reset (`Planets/PlanetWorld.cs:148–155`). Normal running cancellation stays tracked until completion; reset is the path that forgets running tasks.

**Trigger and consequence:** leaving a solar world and quickly starting another can let the old terrain/picture tasks finish in parallel with a newly filled worker allowance. “3 background workers” counts the new tracked list, not necessarily all tasks still using cores and temporary arrays immediately after that transition. This is transient overlap, not proof of a permanent thread or memory leak.

**Proposal:** have Claude evaluate cooperative cancellation or a shared accounting/drain policy without blocking the main thread on all terrain work. Request rapid restart/menu/solar transitions while recording task and memory activity. Principle **1** protects restart responsiveness and **10** tests the worker-budget claim.

## Reviewed costs that are bounded or already acknowledged

- **Physics queries:** player aim and each shot use single-hit `Physics.Raycast`, not allocating `RaycastAll` result arrays (`Player/PlayerWeapons.cs:117–125`, `Weapons/Ballistics.cs:38–51`). AI sight uses one linecast on a thinking interval (`AI/MannequinCombat.cs:166–170`, `:220–221`); locomotion ground/obstacle rays are single-hit queries. Total query load scales with actor count, so capture the actual configured arena count; no new crowd is requested.
- **No hot-path LINQ found in the inspected Runtime source.** Startup `ToArray` calls in body-layout/model construction are not per-frame LINQ findings. This search result is not proof of allocation-free play; Unity calls and string formatting still matter.
- **Map while open:** `UI/SystemMap.cs:163–164` updates labels and dirties its Graphic every frame; `:254–267` rebuilds orbit line vertices from cached orbit outlines. This matches the existing “redrawn every frame” budget entry. Its **1 ms** remains the budget author's estimate, not this audit's measurement. Capture map closed/open under the same scene before proposing changes.
- **Terrain streaming:** `PlanetWorld.Update` pumps an upload-count allowance (`Planets/PlanetWorld.cs:161–176`), not a millisecond allowance. A large texture/result callback can still be expensive even when only one result is delivered. The 19 far pictures/worker count are existing limits; profiler evidence should separate upload/cook time from background generation.
- **Particles and decals:** fixed slot arrays/clamps in `Gore/DecalPool.cs:52–64`, configured particle maxima, the fixed five plume emitters, and 160 fixed-placement checks per feature tick are useful bounds. They do not bound transparent overdraw or substitute for GPU timing.

## Measurement packet for Claude and the lead

Keep native 1280×800 and the actual source SHA/config values in each recording. Capture short, repeatable cases: idle arena; sustained limb/dismember burst; looking at a pickup; map closed/open; first descent with terrain arriving; fast low flight; repeated restarts. Record total CPU/GPU frame times, script/physics/upload/collider samples, GC.Alloc and resource counts, then compare with the existing frame/scene targets. Do not infer GPU costs from CPU frame samples alone. The current `PerfMonitor` reports a 600-frame rolling window (`Perf/PerfMonitor.cs:26`, `:73–81`), so the end-of-run line is not an entire-session summary; preserve the actual recording around a hitch rather than relying only on the final window.

This report changes documentation only. All proposed follow-ups await Claude's code checks and engine evidence; Fable retains feel/design choices. No task here alters caps, tuning numbers, standing rules or Phase 1 scope.
