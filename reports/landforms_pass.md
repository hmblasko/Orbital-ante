# Landforms, assets and NASA hardware pass (2026-10-30)

Matthew's ask: refine graphics; add mountains, mesas, hoodoos, canyons, volcanoes, lakes, rivers and pools; make assets look less cheap; bring in the NASA satellites and ships.

## What changed
- Test planet grown from 60 m to 150 m radius (the bottom of the moon range), painted-desert palette, 128 cells per cube face drawn as 96 tiles (off-screen tiles are culled).
- `procgen/landforms.rs`: mountains, mesas (stepped cliffs, flat caprock), volcanoes (caldera, lava lake, lava streaks), canyons cut into a raised plateau with a stream on the floor, meandering rivers that drain into lakes, lakes with damp shores, and spring pools ringed by white mineral terraces. Placed by hand near the landing zone, plus seeded extras elsewhere. All sizes and counts in `data/bodies/test_sphere.ron`.
- Water shader (depth colour, shore foam, ripples, sun glint) and lava glow in the terrain shader. Layered rock bands on mesa and canyon walls.
- Hoodoos: eroded stone columns with banded colours and a darker caprock, clustered around mesas and canyon rims.
- NASA 3D Resources models: Apollo Lunar Module (parked by the landing zone), DSN 34 m dish, Viking lander wreck, and Hubble, TDRS and Voyager on inclined orbits. All decimated to Deck budgets and merged to a few draw calls. The lander's US flag and "UNITED STATES" textures were replaced with plain gold foil.
- Smoke test now measures ground clearance against the real collision with a ray (cliffs make the mesh differ from the height function between vertices) and treats "left the surface" as more than 3 s airborne.

## Pillars and fun rules
Pillar 1 (satellites orbit) and pillar 3 (landmarks to learn); fun rule 4 (curiosity) and rule 11 (systems to collide with: cliffs, water, lava).

## Measured
All automated gates pass locally (88 unit, 19 determinism, 11 data, smoke, 2 feature scenes, replay 0.0 m). Steam Deck frame times: not measured. Terrain is now about 197k triangles plus 6 NASA models (about 116k triangles in total).

## Known limits / placeholders
- Canyon walls and stream edges look faceted: the 1.8 m terrain cells are too coarse for them. Phase 5's streamed, level-of-detail terrain fixes this.
- Water is visual only (you walk on the lake bed; no swimming yet), and liquid water on an airless test world is for showing the shader.
- NASA models still use their original materials, not the TF2 paint pass.

## Refinement pass (same day)
- NASA models are relit with a painted model shader (same ramp and rim as the world, softened textures, gentle metal highlight). Wrecks can carry settled sand (`grime` in the landmark data).
- Terrain detail levels: 0.9 m cells within about 110 m of the camera, 1.8 m beyond (data: `lod` in test_sphere.ron). Tiles are centred on themselves so the switch distance is measured per tile. Collision stays on the 1.8 m grid.
- Canyons bend in long smooth curves; plateau tops are no longer striped.
- Boulders are kept out of water and off cliffs.
- Bug fix: the painted shaders added a constant "shadow floor" for every light, so lamps (supply ring, lava glow) lit a hard-edged box of ground around them. The floor now applies to the sun only.
- Not measured: Steam Deck frame time and memory with the finer near terrain (about 790k triangles exist in total, of which only nearby tiles are drawn).

## Third pass: movement feel and the icy moonlet (31 Oct 2026)

What changed
- `rust/game/src/player/effects.rs` + `godot/data/player/effects.ron`: jetpack exhaust plume (hot white-blue to orange to grey, aimed opposite the push, fades near the camera so it never fills the view), footstep dust every stride (smaller when crouching), and a landing burst scaled by impact speed with a small camera dip (set `camera_dip_m: 0.0` to turn it off). Dust takes the colour of the body you stand on and falls back under that body's gravity. Pillar 4 (everything is physical), fun rule 10 (game feel).
- `godot/shaders/painted_ice.gdshader` + `land_look.frozen`: lakes and pools can freeze solid. Clear blue ice with pale cracks and frost at the shore, plus a collision shell so you can walk on it.
- `test_moonlet.ron`: the jetpack-hop moonlet is now icy, with frozen pools and a small lake, frost-capped ridges and a crevasse. Pillar 3 (each place shows something new), fun rule 4 (curiosity).

Screenshot: `reports/movement_ice_pass.png` (top: jetpack plume; middle: footstep and landing dust; bottom: frozen lake and pools). The white capsule is a render-only stand-in for the player's body, which first-person view doesn't show.

Gates: unit 91, determinism 19, data 11, smoke, feature scripts (16 checks), replay 0.0 m: all PASS. Screenshot regression not run (no approved baselines). Steam Deck: not measured.
Placeholder: puffs use a generated soft-circle sprite; no sound yet for footsteps, landings or the jetpack (Phase 9).

## Placeholder clean-up (31 Oct 2026)

Matthew asked for unattractive placeholder assets to be removed.
- Removed the primitive air-and-tether station. The parked NASA Apollo Lunar Module now holds the supply point and tether anchor (`supply_height` in `test_sphere_landmarks.ron`), so you refill and clip on beside the lander. Supply and tether ranges went from 4.0 m to 5.5 m to reach from beside its hull.
- Removed the primitive oxygen tank model and the tanks from the play scene, by Matthew's choice, until a properly modelled tank exists. The tank code stays; the feature test adds its own tanks to check pickup and use.
- Screenshot: `reports/lander_supply.png`. Gates: all PASS (unit 91, determinism 19, data 11, smoke, feature scripts 16 checks, replay 0.0 m). Steam Deck: not measured.

## Fourth pass: hoodoos, tether rope, desert peaks (31 Oct 2026)

- Hoodoos are rebuilt as one continuous eroded column: a flared foot, soft bulges and necks, rain-cut flutes, small ledges at the layer lines, and a flat, tilted caprock. They no longer read as stacked balls.
- The tether is now a real rope: a braided orange-and-white tube that sags when slack and lies on the ground instead of sinking into it. Its look is in `interactables.ron` under `tether_rope`.
- The test sphere's mountain tops are now pale, wind-bleached sandstone instead of white snow, which looked like cake icing from orbit. Snow caps on other worlds are broken into drifts, and spring terraces get travertine lips and iron streaks.
- Screenshot: `reports/refine_pass_3.png`. Gates: all PASS (unit 92, determinism 19, data 11, smoke, feature scripts 16 checks, replay 0.0 m). Steam Deck: not measured.
- Still placeholder: 104 hoodoos are separate draw calls, to be batched in the Phase 10 performance pass or earlier if the Deck needs it.

## Fifth pass: bigger planet and new landforms (31 Oct 2026)

Matthew asked to scale the planets up a lot and add more geography (and said not to worry about the Deck for now).

- The test planet goes from 150 m to 600 m radius (mid-range for a rocky planet in section 4). The moonlet goes from 22 m to 66 m. The satellites, the moonlet's position, the main-menu camera and the player spawn all move to match.
- Five new landform types in `procgen/landforms.rs`, each with a unit test:
  - Butte: a narrow, tall mesa remnant with a sheer cliff and a wide talus apron.
  - Dune field: crescent-free transverse dunes with long windward ramps and steep slip faces. The sand buries the hills under it.
  - Complex crater: terraced walls, a flat floor and a central peak. If it digs below the water table it floods, and the peak stands as an island.
  - Wrinkle ridge: a long, lumpy raised ridge that follows a path.
  - Sinkhole: a steep collapse pit. Below the water table it becomes a cenote.
- The showcase near the landing zone now has a mesa with two buttes, a sinkhole a short walk from the lander, a volcano, a canyon, a lake fed by a river, a spring pool and a mountain pair. Further out are a big crater, a dune sea and a wrinkle ridge. About 130 more landforms are seeded over the rest of the planet.
- Rocks (1,400) and hoodoos now have draw distances, so the bigger scatter only draws what is near.
- Terrain build is threaded, and the dev build optimises the game crate. The planet's collision is now split into 64 x 64-cell tiles: one 3-million-triangle shape ran out of Jolt's sub-shape id bits.
- Test fixes: the walk smoke test now checks metres travelled (373 m measured) instead of degrees. The zero-g point moved outside both fields. The drift push now happens one tick after the teleport, because a body teleported while standing loses part of its first slide to floor snapping.

Screenshot sheet: `reports/bigger_planet.png` (rendered with lavapipe, not on a Deck).
Steam Deck performance: not measured.
