# Milestone 12: Combat and gibbing polish pass

16 Nov 2026. On `claude/playtest`.

**Nothing in this milestone has been run in Unity.** The game code compiles against stand-in Unity libraries, and so do the three new PlayMode tests. All 2,416 rule tests pass outside the engine. Whether every fight "produces a moment worth laughing about" can only be judged by playing.

## The problem

Milestones 1 to 3 were built in the flat test arena, which has one gravity pointing down Unity's -Y. Since then fights have moved into the real solar system: on worlds with their own pull, in orbit beside the ship, in caves and at bases. I read through what happens to limbs, guns, blood and marks there and found ten problems:

1. Limbs and dropped guns used Unity's gravity, which was switched back to -Y whenever no enemies were near. A limb could fall sideways off a moon.
2. Even when it was set, it was the player's "down" for everything, which is wrong anywhere else on a small moon.
3. In orbit nothing fell along with the ship, so a gun dropped on a spacewalk fell away from it.
4. Limbs and guns had no protection against sinking through ground that is only solid near the player.
5. Guns the player dropped sat outside every system: they were never counted or cleared, and never moved when the world re-centred.
6. Hits outside the ship gave no camera kick.
7. Bullet holes and blood splats stayed in mid-air when the ship flew off or the world re-centred.
8. Blood sprayed toward Unity's +Y, not away from the ground.
9. URP's default decal method (DBuffer) would never show on the game's own shaders, so blood decals would have been invisible on the ground.
10. Nothing stops limbs coming up into the cabin through the lift. This one is not fixed (see "Still open").

## What changed

- **Everything loose falls toward its own world.** A limb shot off on the Moon falls onto the Moon. In orbit it drifts along with the ship. Dropped guns behave the same way. Unity's gravity is no longer used by any rigidbody in the solar system.
- **Nothing sinks, and nothing vanishes in the air.** A limb or gun that falls through ground that is not solid yet (far from the player, which on a light world can be one shot away) is laid on top of it. A gun lies still there and rides with the ground until it is shot or picked up. A limb fades out where it lies.
- **Loose things rest.** Once something has lain still on the ground for half a second it goes to sleep and costs nothing, until it is knocked or shot.
- **True spin in the solar system.** A tumbling limb keeps nearly all its spin; only the air and the ground slow it.
- **Blood floats in free fall.** It falls with the world, and only gets its extra weight from the part of the pull you actually feel. Standing on the ground, sprays arc and drip as before. On a spacewalk in orbit they drift as a cloud round the wound. Sparks work the same way.
- **Marks stay put.** Bullet holes and splats stay on the ship's hull as it flies and on the ground when the world re-centres. Blood in the air moves with a re-centre too.
- **Guns the player drops** keep the speed they were carried at, count toward the cap of 12 loose guns, and are cleared with the rest. The Wildcard is never cleared. In the cabin a gun let go of is carried instead, because the cabin has no furniture for anything loose to rest on.
- **A camera kick on hits** in the real game, outside and aboard, the same as in the arena. Pause > Feel switches it off.
- **Decals show on our shaders.** Setup now switches URP's decal feature to Screen Space, which draws over any shader.
- **Fixes the reviews found:**
  - The death burst no longer pulls its pieces twice.
  - A gun dropped on death no longer leaves at twice the suit's speed.
  - A gun on a world no longer jumps about 2 km when the world re-centres.
  - The Wildcard on show in a base is held still.

## Which principles it serves

- Fun core gameplay first (1): fights in the real game behave the way they did in the arena.
- Learnable challenge (3): a limb or gun goes where physics says, on every world. Nothing teleports or vanishes.
- Real space inertia (section 4): in orbit things fall together, and spin is kept.

## What was tested, and how

- The game code and the PlayMode tests compile against stand-in Unity libraries. The only errors are the two already known in files this work does not touch.
- All 2,416 rule tests pass in `tools/rules-tests`; 1 is skipped as before.
- Three new PlayMode tests in `SolarCombatTests` have been written but not run:
  - A limb beside the ship in orbit stays beside it.
  - A dropped gun does the same, and is only pulled while loose.
  - A bullet mark on the hull stays on the hull as the ship flies.
- Three reviews read the code (Unity behaviour, Steam Deck cost, design against the prompt). What they found is below.

## Agent review

- **unity-reviewer**:
  - Guns under a world's transform moved twice on a re-centre. Fixed: loose guns are parented to the spawner, and laid-down guns are handed to their world.
  - The death burst pulled its pieces twice. Fixed.
  - A gun dropped on death left at twice the suit's speed. Fixed.
  - The bullet-mark test compared poses from two different moments. Fixed.
  - It confirmed from memory that `m_Settings.technique` with value 2 is Screen Space in URP 17. That is not checked against the URP source. Setup prints a warning if the field is not found.
- **deck-performance**:
  - Loose things could never sleep under a force every step. They now sleep once still, and sleeping ones are skipped.
  - Each loose thing asked a full ground height question every step. It is now one step in four.
  - The particle buffers are now made up front.
  - Screen Space decals need the depth texture. A depth prepass would be costly, so the budget says what to check.
  - Estimates are in `docs/performance_budget.md`. None are measured.
- **game-designer**:
  - Blood rained toward the planet in orbit. Fixed.
  - Far limbs vanished. Fixed.
  - Spin died in vacuum. Fixed.
  - Guns could be dropped into the cabin. Fixed.
  - The shake only worked outside. Fixed.
  - It also listed what the sign-off checklist must cover. That list is in `docs/reports/phase-1-signoff.md`.
  - It flagged as deliberate deviations, for Matthew to confirm at sign-off: carried guns against "one gun at a time", and stances, garrisons, the gun ladder and the Wildcard pulled forward from Phase 2.

## Still open

- **Inside the ship.** Nothing stops a limb or gun coming up the lift into the cabin, where it would fall through the floor. No enemy fights aboard yet, so this needs a fight right under an open hatch to happen.
- **Ship hull hits** play the dirt impact sound, not a metal one.
- **A laid-down gun** sits at the coarse ground's height until it is shot or picked up, so it may float a little or be half buried once the fine ground is built under it.
- **Blood aboard** follows the world's pull, not the cabin's floor. In orbit it floats in the cabin.
- **Unmeasured on the Deck:** physics time and sleep with 40 limbs down, and whether the decals force a depth prepass. The budget doc says how to read these.
