# Phase 3, first chunk: the living solar system (31 Oct 2026)

## What changed
- `orbits/orbit.rs`: orbit and spin maths in f64, as pure functions of the run clock. The starting angles are seeded from the run seed. Tests: radius, closure, speed, inclination, spin direction, determinism.
- `orbits/system.rs`: the `SolarSystem` node.
  - Spawns the roster from `data/system/solar_system.ron`.
  - Moves every body along its orbit and turns it each physics tick.
  - Carries the player with the body whose gravity dominates. Near the surface the player also turns with the body's spin.
  - Converts the player's velocity when they change frame, so momentum is kept.
  - Re-centres the world on the player beyond 1.5 km (floating origin).
  - Aims the sunlight from the sun to the player.
  - Asks nearby bodies for full detail.
  - Starts the player on a flat, dry, sunlit spot on Earth.
- `orbits/body.rs`:
  - Bodies can start as a cheap preview (coarse mesh plus a safety sphere) and build full detail (fine tiles, collision, rocks, hoodoos) on a worker thread.
  - New looks: gas giant cloud decks (with a storm oval and rings), a glowing sun, and air glow shells.
  - Surface lookups now work in the body's own rotating frame, and landmarks ride along.
- New shaders: `gas_giant`, `planet_ring`, `star`, `air_glow`. The model shader's dust direction is now per part, so it stays correct while bodies turn.
- 14 bodies in `data/bodies/system/` with real-colour palettes and their own landforms:
  - Sun
  - Mercury (scarps, craters)
  - Venus (volcanoes, yellow air)
  - Earth (mountains, lakes, rivers, blue air)
  - Moon
  - Mars (canyons, volcanoes, dunes, buttes, hoodoos, thin orange air)
  - Phobos
  - Jupiter (with Io and Europa)
  - Saturn (rings, Titan with methane lakes and orange haze)
  - Uranus (on its side)
  - Neptune

  The script `tools/gen_system_bodies.py` made the first version; tune the RON files directly.

## Why
Pillar 1 (a living clockwork solar system) and pillar 4 (one consistent physics, momentum kept). Fun rule 4 (curiosity: the sky changes as you watch it), rule 5 (flow: no loading screens) and rule 11 (emergent stories).

## Scale (needs Matthew's decision)
At section 4 body sizes, the bodies need about 70 km across to orbit without ever touching. A unit test checks this over 3 hours of run time. That is wider than the 25 km in section 4. A decision card is open.

## How to test (PC)
1. In the Godot editor, open `scenes/tests/solar_system.tscn` and press F6.
2. You start standing on Earth in daylight. Earth is orbiting at about 17 m/s and spinning, but you should feel still.
3. Look up to see the sun, the Moon and the other planets. Jetpack up (hold A or Space) to leave the blue air and see Earth from space.
4. Automated: `godot --headless --path godot --fixed-fps 60 --script res://tests/solar_system_test.gd` (12 checks).

## Measured
- Feature test: drift while standing on Earth was 0.005 m over 5 s. The largest absolute-speed jump across frame changes was 0.17 m/s per tick. The largest position step while re-centring was 2.55 m at 140 m/s.
- The Moon built full detail in the background in 5.8 s on this 4-core cloud machine.
- Steam Deck: not measured.

## Not done yet / placeholder
- Live map, ship flight between planets (Phase 4), heat damage near the sun, gas giant interiors and Neptune's ocean (Phase 5), the asteroid belt (Phase 8).
- Bodies never drop their full detail once built, so memory grows as you visit more bodies. Unloading comes next.
- The slice and main menu still use the test planet. The new scene is a test scene.
- Saturn's rings are a first pass.

## Part 2 (31 Oct 2026): far detail release and the live map

- Bodies drop their full-detail surface and go back to the light preview once you are 1.6x beyond their detail distance (except the body you are riding). Measured in the feature test: the Moon built full detail in the background in 4.7 s and dropped it again within 3 frames.
- Live system map on a wrist screen: M on keyboard, D-pad right on the controller. Zoom with PgUp/PgDn, the mouse wheel, or RB/LB. Four zoom steps from the whole system (38 km half-height, centred on the sun) down to 1.8 km around you. Orbits and bodies are live, each planet is lit on its sun side, rings show on the gas giants, the body you are in the gravity of has a teal ring, and your arrow points where you look. Labels that would overlap are dropped, most important first. No landmark icons at all yet; only discovered ones will get icons when discovery arrives (Phase 7). The game keeps running while the map is open. Layout, colours and zoom steps are in godot/data/ui/system_map.ron.
- Screenshot: reports/phase3_live_map.png (top: whole system; bottom: closest zoom near Earth).
- Gates: 112 unit, 21 determinism, 11 data tests; smoke test; 3 feature scripts (33 checks, solar_system_test now 17); replay 0.0 m. Screenshot regression not run (--no-render). Steam Deck: not measured.
