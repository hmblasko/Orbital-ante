# Graphics and UI pass (30 Oct 2026)

Matthew asked for much better graphics and much more polished, pleasing interfaces. This pass is on branch `phase-2-player` (PR #2). Picture: `reports/graphics_ui_pass.png`.

## What changed
| Area | Files | What it does |
|---|---|---|
| Sky | `godot/shaders/space_sky.gdshader`, `godot/environments/space.tres` | Two layers of tinted stars, a dusty galactic band with a dark lane, faint nebula colour, and a sun disc with a corona that follows the scene's sun. The soft parts render at half resolution to keep it cheap |
| Lighting and post | `environments/space.tres`, `scenes/tests/sphere_walk.tscn` | AgX tonemapping, ambient occlusion, bloom on bright lights and a light colour grade. The sun is warmer and lower, so hills cast long shadows. Soft shadows |
| Terrain | `rust/game/src/procgen/{noise,terrain}.rs`, `orbits/body.rs`, `godot/data/bodies/*.ron` | Bodies are no longer smooth spheres. They have rolling hills, ridges and craters with raised rims, built from a fixed seed per body. Collision matches the visible ground exactly |
| Painted shading | `godot/shaders/painted_terrain.gdshader`, `painted_prop.gdshader` | TF2-style illustrative shading after Valve's paper: half-Lambert light through a warm-to-cool ramp, so shadows are never pure black, plus a soft rim light. Albedo is painted from crater floors, rims, height and slope, with broad brush-like variation and fine regolith bumps |
| Rocks | `procgen/rocks.rs` | Clusters of chipped, squashed boulders, each with its own seed and a convex collider |
| Props | `items/look.rs`, `items/oxygen_tank.rs`, `player/supply.rs` | New placeholder models built from rounded parts. The oxygen tank is a teal cylinder with rubber bands, a brass valve and a glowing fill gauge. The supply post has hazard-orange trim, a glowing ring and a hose coil. It now sits on the terrain |
| Fonts and theme | `godot/assets/fonts/`, `godot/ui/theme.tres` | Lilita One (chunky display) and Fredoka (rounded body), both free for commercial use (SIL Open Font License), logged in the licence ledger. Rounded buttons with clear hover, press and a bright controller-focus ring |
| Main menu | `scenes/ui/main_menu.*` | A big outlined title over the real test planet, slowly turning, lit side-on. A fade and slide-in on open, a seed pill, a controller hint, and Start focused on open |
| Visor HUD | `player/hud.rs`, `godot/shaders/visor.gdshader`, `data/ui/suit_hud.ron` | Glass plates. Oxygen is a 270-degree ring gauge with ticks, big time digits and tank capsules. Jetpack fuel is a segmented upright bar with a percentage. Suit integrity is a segmented line that only shows once you're hurt. Warnings show as status pills, and there's a centre dot. A soft helmet frame pulses red-orange at the edges when air is low |
| Button prompts | `player/hud.rs`, `player/controller.rs` | "X Take oxygen tank" and "▼ Clip on tether" appear when they apply. They switch to E or T after you use the keyboard |
| Death screen | `player/hud.rs` | A "RUN OVER" banner, the cause in one line, and Continue |
| Dev overlay | `data/debug/frame_overlay.ron` | Now off by default. F3 / L3 shows it |

## Why (pillar and fun rule)
- Section 11 asks for a TF2 look with Source 2-quality lighting. This pass covers the rim light, the warm/cool ramp, and shadows that are never black.
- Pillar 1 (a place worth being) and fun rules 4 and 6 (curiosity; aesthetics first: Discovery and Sensation).
- The HUD and prompts serve fun rule 5 (clear goals, instant feedback) and section 10's interactable readability.
- The terrain serves section 4 ("hilly and fun to drive, never flat plains").

## Checks
- Rust unit tests: 80 passed. Determinism: 16. Data: 10.
- Smoke: pass. Replay drift: 0.0 m.
- Feature scenes: 2 scripts pass, now measuring height above the real ground.
- Screenshots: not compared, because there are no approved baselines yet.

## Measured / not measured
- Steam Deck frame times: **not measured**.
- Cost added:
  - Terrain is about 110k triangles for the 60 m test body.
  - Ambient occlusion and bloom are on.
  - The sky renders at half resolution.
  - These need a Deck session before I can say they fit the 16.6 ms budget. If they don't, the first things to cut are ambient occlusion quality and the terrain subdivision, which is a data value.

## Still placeholder
- The tank, supply post and rocks are procedural stand-ins. They are much better than primitives but are not final art under section 11. Real models need the Tripo and NASA assets.
- The terrain is slice scope. Streaming, LOD and the full feature sets (caves, canyons, ruins) are Phase 5.
