# Phase 2 report: Player and suit (30 Oct 2026)

Built to vertical-slice scope (section 16). Branch `phase-2-player`, built on top of Phase 1.

## What changed
| Area | Files | What it does |
|---|---|---|
| Suit | `rust/game/src/player/suit.rs`, `godot/data/player/suit.ron` | Health, oxygen and jetpack fuel. 4 minutes of air; outside breathable air it drains one second per second. The warning starts at 25%. At zero you lose 10 health a second, so you get 60 seconds of warning and then 10 seconds before death. Standing at a supply point refills both air and fuel. Fuel also refills slowly on the ground after 1.5 s without thrusting |
| Jetpack and inertia | `player/controller.rs`, `player/motion.rs` | On the ground you walk as before. In the air there's no more "air steering": you keep your momentum, and the left stick, A (up) and B (down) fire the jetpack, which burns fuel. Out of fuel means pure inertia |
| Zero-g | `player/controller.rs` | Away from every body's gravity, the player stops turning to stay upright and simply drifts |
| Match velocity | `player/controller.rs` | Hold LT (Left Shift) in the air to cancel your motion. It brakes against the nearest body; bodies don't move until Phase 3, so for now it brakes to a stop |
| Thruster light | `player/controller.rs` | A warm light glows on the side the jetpack fires from (the Outer Wilds tell for which way you're pushing) |
| Safety tether | `player/tether.rs` | Press D-pad down (T) near an anchor to clip on. The line stops you drifting further than 25 m but never reels you in, as section 6 asks. Press again to unclip |
| Oxygen tanks | `rust/game/src/items/`, `godot/data/items/oxygen_tanks.ron` | 4 tanks per body, placed from the run seed (same seed gives the same spots). X (E) picks one up, you can carry 2, and D-pad left (G) uses one for +90 s of air. A tank is never wasted on a full suit |
| Supply point | `player/supply.rs` | PLACEHOLDER blue stand-in for the ship's air and fuel until the lander exists in Phase 4 |
| Second body | `godot/data/bodies/test_moonlet.ron`, `scenes/tests/sphere_walk.tscn` | PLACEHOLDER moonlet (22 m) about 260 m away with a zero-g gap in between, so jetpacking between bodies can be tested. It has breathable air, for testing only |
| Visor HUD | `player/hud.rs`, `godot/data/ui/suit_hud.ron` | Every gauge has a different shape: oxygen is a ring with the time left, bottom left; tanks are pips beside it; fuel is an upright bar, bottom right; health is a thin line, bottom centre. LOW OXYGEN blinks red-orange at the top. PLACEHOLDER art |
| Death recap | `player/hud.rs`, `app/root.rs` | When you die, the view stays first person and one line names the cause ("You ran out of oxygen."). After 1.5 s, A continues: the run is cleared from the save (death ends the run, section 2), the game autosaves, and you're back at the menu |
| Input map | `godot/project.godot` | New actions: match velocity (LT / Left Shift), interact (X / E), use oxygen tank (D-pad left / G), tether (D-pad down / T) |
| Verification | `verify/bot.rs`, `verify/replay.rs`, `verify/harness.rs`, `godot/tests/suit_feature_test.gd`, `tools/verify.sh` | The bot now uses the jetpack in short bursts and match velocity. Replays record the 4 new actions (replay format v2). An unexpected death fails a bot run. The new feature test runs 16 checks, listed below. verify.sh now has a "Feature test scenes" gate |

## Why (pillar and fun rule)
- **Jetpack, inertia, zero-g and match velocity:** pillar 4 (everything is physical: the player has the same inertia as everything else) and fun rule 10 (game feel). Fun rule 3 also applies: three buttons, learnable in minutes.
- **Oxygen, low-air warning and death recap:** pillar 2 (goofy but deadly: real danger) and fun rule 8 (fair failure). You get 60 seconds of visible warning before any damage, and the recap names the cause in one line.
- **Oxygen tanks:** pillar 3 (every run counts: the seed changes where they are) and fun rule 4 (curiosity: exploring finds air).
- **Tether:** pillar 4, and fun rule 5 (flow). Drifting off into space by accident is frustration, not challenge.
- **Visor HUD:** fun rule 5 (clear goals, instant feedback) and section 6 (diegetic and minimal).
- **Placeholder moonlet and supply point:** test scaffolding only; cut them when the Moon (Phase 3) and the lander (Phase 4) exist.

## How to test it
**PC**
1. `git fetch && git checkout phase-2-player`, then `cd rust && cargo build`.
2. Run `cargo test --workspace`. Every line should say `ok` (74 tests).
3. Open the project in Godot 4.7.2 and press F5, then Start.
4. Look at the visor HUD. The oxygen ring should count down from 4:00, one second per second.
5. Walk to the blue cylinder nearby and stand next to it. The ring should fill back up.
6. Jump, then keep holding A (Space). The jetpack should lift you, the amber bar should drop, and a warm glow should appear below you. Let go and you fall back. On the ground the bar refills after a moment.
7. Find a glowing cyan capsule (an oxygen tank) and press X (E) next to it. A pip fills beside the ring. Press D-pad left (G) to use it later.
8. Find the small blue-grey moonlet on the horizon. Jetpack towards it: hold A to climb, then push the stick towards it. Once "Zero-g" shows at the top, let go of everything. You should keep drifting at the same speed. Hold LT (Left Shift) and you should brake to a stop.
9. Stand at the blue cylinder and press D-pad down (T) to clip on. "Tethered" shows at the top. Jetpack away; the line stops you at 25 m. Press again to unclip.
10. To see the death recap without waiting 4 minutes: stay away from the cylinder and let the air run out. You get about 1 minute of LOW OXYGEN, then 10 seconds, then the recap. Press A to go back to the menu.
11. Optional, the full checks: `GODOT=<your godot path> tools/verify.sh`, then open `verify_out/latest/report.html`.

**Steam Deck**
1. Download **OrbitalAnte-linux** from the newest green Actions run of this branch's PR (SETUP.md section 6, Route 1). Copy it over the Phase 1 build.
2. Repeat PC steps 4 to 10 with the Deck controls.
3. Check that the HUD text is readable on the 7-inch screen. The smallest text is 18 px at 1280x800.
4. Fly around for 2 minutes with the overlay on (L3), then send me `frametime_log.csv` (same path as before).
5. If you want, score the rubric (section 12). This is the first phase with movement worth judging.

## Measured results
- **Steam Deck: not measured.**
- Feature test (headless, fixed 60 Hz, measured):
  - Oxygen used 5.00 s of air in 5 s.
  - The supply point refilled 100 → 180 s in 2 s.
  - A jetpack burst of 1 s reached an 8.4 m apex; a plain jump reaches about 1.7 m.
  - With no input in zero-g, the player was still drifting at exactly 3.0 m/s after 1 s. Match velocity brought it to 0.000 m/s within 1 s.
  - After a 30 m/s push on the 25 m tether, the furthest distance was 25.20 m.
  - Suffocation death came 10 s after the air ran out.
- Smoke and replay: the 60 s bot run, now using the jetpack, had 0 detector hits and 0 log errors. The replay ended 0.0 m from the recording.
- Rendering frame times: not measured. The only rendering here is software (lavapipe, around 100 ms a frame), which says nothing about a Deck.

## Unverified or placeholder
- **Placeholders (fail the section 11 art bar on purpose):** the HUD art, the supply stand-in, the tank capsules, the tether line (a flat line) and the moonlet.
- **No suit model yet.** It's first person, so nothing of the suit is visible yet. Hands and a visor frame come with the asset pass. That needs the NASA spacesuit asset (section 6); see question 3.
- **Not built yet, because the systems they hook into don't exist:**
  - Hauling tanks to the ship for extra value (needs the ship and points).
  - Suit oxygen refilling from the ship (the stand-in does this for now).
  - The "muffled helmet" low-oxygen audio filter and the jetpack sound (Phase 9 audio).
  - Controller rumble.
- **The breathable moonlet** is a test fixture. No body in the real roster will have breathable air unless you decide otherwise.
- **Stealth crouch** (quieter movement) needs enemies to hear it, so it waits for Phase 6. Crouch still lowers the camera and slows you down.
- **In-game remapping and Deck glyphs:** still not built (release checklist).

## Verification report summary (section 19.6)
| Gate | Result |
|---|---|
| Rust unit tests | PASS: 74 |
| Determinism tests | PASS: 13 (adds seeded tank placement) |
| Data-file validation | PASS: 10 |
| Smoke test (boot, menu, slice, 60 s bot with jetpack, quit) | PASS |
| Feature test scenes | PASS: 2 scripts, 16 named checks (suit) plus the Phase 0 walk test |
| Input replay determinism | PASS: 0.0 m drift |
| Screenshot regression | NOT RUN: no approved baselines; 5 screenshots wait for you (2 new: the moonlet from the sphere, the supply point close up) |
| Bots on 50 seeds x 5 profiles, balance simulator, fun proxies | NOT RUN: still need objectives, combat and ships |
| Steam Deck session and rubric | NOT RUN: needs your Deck |

Open bugs: none. Found and fixed during the phase:
- The HUD drew at zero size because it set its anchors without its offsets. A rendered screenshot caught it.
- The moonlet first sat with its dark side towards the player. I moved it into the sunlight.

Flagged screenshots: none. The AI review findings below need your confirmation: everything in them is placeholder, the HUD text is legible at 1280x800, and nothing overlaps.

Fun proxies: not applicable yet.

## Rust concepts used for the first time
- **`if let ... && ...` (let chains):** checks that something exists and a condition holds in one `if`. The death signal uses it: "if the suit has a death cause and the player was alive last tick".
- **`#[signal]`:** declares a Godot signal from Rust. `died(cause_key)` lets the HUD, GameRoot and tests react to death without the player knowing about them.
- **`Option::take()`:** moves a value out and leaves `None` behind. The tether toggle uses it: clipped on means unclip, otherwise clip on.

## Questions for you
1. **LT conflict:** section 6 gives LT to "Aim" on foot. I put match velocity on LT for now, because aiming doesn't exist until Phase 6. When guns arrive, should LT aim on the ground and match velocity in the air, or do you want a different button?
2. **D-pad:** I used D-pad left for the oxygen tank and D-pad down for the tether. That leaves up for the radio and right for the map. OK?
3. **Assets:** where are the Tripo models and NASA 3D Resources (including the spacesuit and the launch pad you uploaded)? Nothing is in `godot/assets/` yet.
4. **Your PC:** Windows or Linux? It changes which build steps I give you.
5. **Test sphere:** keep it at 60 m, or make it Moon-sized (150-400 m) now that the jetpack exists?
