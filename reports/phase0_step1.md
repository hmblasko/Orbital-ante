# Phase 0, step 1 report (30 Oct 2026)

## Versions
- **Godot 4.7.2-stable** is the latest stable 4.x (4.8 is still in beta).
- **godot-rust (gdext) 0.5.5** is the latest release. It has an `api-4-7` feature, so it matches Godot 4.7.
- They're compatible: I loaded the extension in Godot 4.7.2 and it reported `API v4.7.stable, runtime v4.7.2.stable`.
- Rust 1.94 or newer is required (I used 1.97). Jolt physics is built into Godot 4.7 and switched on in the project.

## What changed
| File | What it does |
|---|---|
| `CLAUDE.md` | Your master build prompt, word for word |
| `docs/SETUP.md` | Click-by-click setup: Rust, Godot, Git + LFS, building, running, the Steam Deck route |
| `rust/` | The Rust crate with all section 14 modules. Modules for later phases are empty stubs that say which phase fills them in |
| `rust/src/orbits/gravity.rs` | Point gravity: inverse-square outside the surface, linear inside it, zero beyond an influence radius |
| `rust/src/orbits/body.rs` | `CelestialBody` node: reads its RON file, builds a placeholder sphere and collision, adds 24 marker pillars, provides gravity |
| `rust/src/player/controller.rs`, `motion.rs` | `PlayerController`: first-person capsule that turns to stay upright on curved gravity. Walk, jump, crouch (lowers the camera), stick and mouse look |
| `rust/src/ui_bridge/frame_overlay.rs` | Frame-time overlay: fps, average / worst 1% / max frame time, render CPU and GPU time, physics time, draw calls, and a graph with 16.6 ms and 25 ms lines. Writes a CSV row every 5 s |
| `rust/src/data/mod.rs` | Loads RON files from `res://data/`. A broken file logs an error instead of crashing |
| `godot/data/**.ron` | All tunable numbers: movement, test sphere, overlay |
| `godot/project.godot` | Input map with the section 6 Deck layout plus keyboard and mouse, Jolt physics, 1280x800 window |
| `godot/scenes/player/player.tscn`, `scenes/tests/sphere_walk.tscn` | Player scene and the test scene (the main scene for now) |
| `godot/tests/sphere_walk_smoke.gd` | Scripted test: walks for 80 s, jumps and turns, then checks the player stayed on the surface and upright |
| `.gitattributes`, `.gitignore` | Models, textures and audio under `godot/assets/` go through Git LFS |
| `docs/asset_licence_ledger.md` | Empty licence ledger, ready for the first asset |
| `reports/phase0_step1.html` | The section 19.6 dashboard for this step |

## Why (pillar and fun rule)
- **Point-gravity walking**: Pillar 4 (everything is physical) and Pillar 1 (bodies are real places you stand on). Fun rule 10 (game feel) and rule 3 (controls learnable in 5 minutes).
- **Frame-time overlay and CSV log**: priority 3 (Deck performance budgets), so frame times are measured, not guessed. Fun rule 5 (flow: no immersion breaks from stutter). It's a developer tool.
- **RON data loading**: standing rule 5 (no magic numbers).
- **Marker pillars and the plain sphere**: only there so you can see yourself move. They're labelled `PLACEHOLDER_` and should be cut when real terrain lands in Phase 5.

## How to test it
**PC**
1. Follow `docs/SETUP.md` sections 1 to 4, then run `cargo test` in `rust/`. Expect 15 passed.
2. Open `godot/project.godot` in Godot 4.7.2 and press **F5**.
3. Walk forward with the left stick or W. Check that the horizon curves and that you can walk all the way round the sphere and come back without falling off.
4. Jump (A / Space). You should land back on the surface wherever you are, including on the "underside".
5. Hold crouch (B / Ctrl) and check that the camera drops and you slow down.
6. Look with the right stick and with the mouse. Check that the view never rolls sideways while you walk.
7. Toggle the overlay with L3 / F3. On a normal PC the numbers should be green.
8. Optional: run the scripted test from `godot/`:
   `godot --headless --path . --fixed-fps 60 --script res://tests/sphere_walk_smoke.gd`. Expect `PASS`.

**Steam Deck**
1. Build and copy it with Route A in `docs/SETUP.md` section 6.
2. Launch it in Gaming Mode with the **Gamepad with Mouse Trackpad** controller template.
3. Repeat PC steps 3 to 7 with the Deck controls, including right-trackpad look.
4. Walk for 2 minutes with the overlay on, then copy
   `~/.local/share/godot/app_userdata/Orbital Ante/frametime_log.csv` and send it to me.
5. Score the rubric if you like, though this step only has movement.

## Measured results
- **Steam Deck frame times: not measured.**
- PC frame times: not measured. My only rendered run used software rendering in a cloud container with no GPU (about 100 ms a frame), which tells us nothing about real hardware.
- Movement (headless, 60 Hz fixed step, measured): the player stayed between 60.90 and 62.83 m from the centre (the surface is 60 m; the high value is the jump). After 80 s of walking the body was 0.08 degrees off upright.

## Unverified or placeholder
- Not run on Windows, a real GPU or a Steam Deck. The Linux export (Route A) is unverified: I didn't download export templates here.
- Sphere, noise texture, marker pillars, lighting and sky are all placeholders and fail the section 11 art bar on purpose.
- Crouch only lowers the camera. The collision capsule doesn't shrink yet, so you can't crouch under things.
- No jetpack, oxygen, suit or zero-g movement yet (Phase 2). Leaving the gravity range keeps your last "up".
- Input map is in `project.godot`; remapping UI and Deck button glyphs come later.
- No RunSeed, save, CI, bots or screenshot harness yet (Phase 1).

## Verification report summary (section 19.6)
- **Passed:** 15 Rust unit tests (including 3 data-file validations) and the scripted sphere-walk test.
- **Not run:** determinism tests (no seeded systems yet), boot-to-menu smoke test and bots (Phase 1), screenshot regression (no baselines yet), balance simulator, fun proxies, Deck session.
- **Open bugs:** none known. Fixed during this step: the B button also freed the mouse (Godot's built-in "cancel" action uses B), and the planet rendered too dark.
- **Flagged screenshots:** 1, `reports/phase0_step1_sphere_walk.png`. It's an AI review finding and needs your confirmation. Everything in it is placeholder.
- **Fun proxies:** not applicable yet.

## Rust concepts used (one line each)
- **Ownership:** every value has exactly one owner, and it's freed when the owner goes away. That's how Rust avoids memory bugs without a garbage collector.
- **Trait:** a set of methods a type promises to have. `ICharacterBody3D` is the trait that lets our `PlayerController` receive Godot's `ready` and `physics_process` calls.
- **`Gd<T>`:** a handle to an object that lives inside Godot, like a node. You call its methods through the handle, and Godot owns the memory.
- **`Option<T>`:** a value that is either `Some(thing)` or `None`. We use it where something can be missing, like a data file that fails to load.
- **`#[derive(...)]` / `#[class(...)]`:** labels that make the compiler write boilerplate for us, such as registering a Rust struct as a Godot node type.
