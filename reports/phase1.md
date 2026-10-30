# Phase 1 report: Foundation (30 Oct 2026)

Built to vertical-slice scope, as section 16 asks. Branch `phase-1-foundation`.

## What changed
| Area | Files | What it does |
|---|---|---|
| Run seed | `rust/game/src/seed/` | One master seed per run, shown as an 8-character code like `7K3M-Q9TD` (typing is forgiving: lowercase, O for 0 and I/L for 1 all work). Each system calls `SeededRng::derive(seed, "procgen")` to get its own stream. Hand-written generator, so results are identical on every platform. `RunSeed` engine singleton |
| Saves | `rust/game/src/save/`, `godot/data/save/save.ron` | Versioned save files with a migration path. Every write goes to a temp file and is then renamed, so a crash or Deck suspend mid-save can't corrupt a save. Keeps the last 3 autosaves plus a save-and-quit slot. Loading picks the newest file that loads cleanly, skips damaged ones (and keeps them for bug reports), and refuses files from newer builds. `SaveData` engine singleton |
| Data files | `godot/data/points/antes.ron`, `sources.ron` | The decided ante targets, timings and point values (sections 17.5 and 18), with tests that the target curve keeps its decided shape |
| Boot flow | `rust/game/src/app/`, `godot/scenes/main.tscn`, `scenes/ui/main_menu.*` | Main scene boots, picks a seed, loads the save, and shows a PLACEHOLDER menu (Start, New seed, Quit, with the seed and version shown). Start begins a run, autosaves and loads the test scene |
| Crash log | `rust/game/src/app/crash.rs` | If Rust code panics, writes `user://crash_logs/crash_<time>.txt` with the seed, version and backtrace. Nothing is sent anywhere |
| Strings | `godot/text/strings.csv` | All menu text comes from a translation table (section 14) |
| Input map | `godot/project.godot` | Unchanged from Phase 0. It already had the section 6 Deck layout, and I added an Esc-only "release mouse" action |
| Verification, in game | `rust/game/src/verify/` | Command-line test modes: `--smoke` (60 s autoplay bot), `--replay=file` (replays input and checks it ends in the same place), `--record=file` (records your own play), `--screenshots` (fixed viewpoints). Detectors for stuck, out of world and physics explosion. Writes `results.json` and exits 0 or 1 |
| Verification, offline | `rust/verify/` (`oa_verify` tool), `tools/verify.sh` | SSIM screenshot comparison plus automatic checks (magenta missing textures, black or white frames), log error counting, the HTML report dashboard, bug drafts, and GitHub issue filing through `gh` that skips duplicates. `tools/verify.sh` runs the whole thing with one command |
| Build route | `godot/export_presets.cfg`, `.github/workflows/ci.yml`, `tools/ci_install_godot.sh` | Linux and Windows export presets. CI runs verification, then builds Linux (the Deck build) and Windows on every push, and attaches both to a release for `v*` tags |
| Repo | `rust/Cargo.toml` | `rust/` is now a workspace: `game/` (the Godot library) and `verify/` (the tool) |

## Why (pillar and fun rule)
- **Run seed:** pillar 3 (every run counts and teaches: a run can be replayed and shared) and fun rule 7 (people fun: shareable seeds). It's also required for honest odds (section 13).
- **Saves:** priority 2 (no save loss) and pillar 3 (unlocks and the locker must never be lost). Fun rule 9 (every run counts).
- **Data files:** standing rule 5. They feed fun rules 2 and 5 once the ante loop exists in Phase 7.
- **Verification layer, crash log, replays:** priority 2 (stability) and section 19. Replays also back fun rule 8 (fair failure): any death can be reproduced exactly.
- **Placeholder menu:** fun rule 5 (clear goals: a clean start point). The final menu art is a later task.
- **CI and exports:** priority 3 (Deck budgets can only be checked on a Deck build) and section 15's build pipeline.

## How to test it
**PC**
1. Pull the branch: `git fetch && git checkout phase-1-foundation`, then `cd rust && cargo build`.
2. Run `cargo test --workspace`. Expect 56 passed (51 game, 5 tool).
3. Open `godot/project.godot` in Godot 4.7.2 and press F5. You should see the placeholder menu with a seed code. Press **New seed** and check the code changes.
4. Press **Start** (A on a controller). You should land in the sphere-walk scene. Walk around as in Phase 0.
5. Quit and press F5 again. The menu's "runs started" count should be one higher than before you pressed Start: Start autosaves, and the menu reads that save back.
6. Run the full checks from Git Bash: `GODOT=<your godot path> tools/verify.sh`, then open `verify_out/latest/report.html`.
7. Record your own run: `<godot> --path godot -- --record=my_run.ron`. Walk a bit and quit. Then replay it with `<godot> --path godot -- --replay=my_run.ron --out=replay_check`. The replay should end with `Harness: replay PASS`.

**Steam Deck**
1. On GitHub, open **Actions**, then the latest green run, and download **OrbitalAnte-linux** from Artifacts (SETUP.md section 6, Route 1).
2. Copy it to the Deck, mark `OrbitalAnte.x86_64` executable, add it to Steam as a non-Steam game, and launch it in Gaming Mode.
3. Check the menu is readable on the 7-inch screen and fully usable with the controller: D-pad or stick to move between buttons, A to press.
4. Start, walk for 2 minutes with the overlay on (L3), quit, and send me `~/.local/share/godot/app_userdata/Orbital Ante/frametime_log.csv`.
5. Quit from the Steam menu while in the scene, relaunch, and check the menu's run counter went up by one (the autosave from pressing Start survived).

## Measured results
- **Steam Deck: not measured.**
- Smoke test (this cloud machine, headless, fixed 60 Hz): 60 game seconds, 0 detector hits, 0 log errors.
- Replay determinism: the replay ended **0.0000 m** from the recording, both at fixed 60 fps and in real time. The exported release build also ended in exactly the same place as the editor build.
- Linux export: built locally and passed the smoke test. Size is 73.5 MB engine, 6.9 MB Rust library, 27 KB game data.
- Rendering frame times: not measured. My only rendering here is software-only, which says nothing about a Deck.

## Unverified or placeholder
- **Windows export:** the CI job is written but I couldn't build the Windows library here, so it's unverified until the first CI run finishes. I'll fix it if it fails.
- **Bots:** only one simple profile ("wanderer") so far. The five section 19.2 profiles and the behaviour-tree framework need objectives, combat and ships, so they come with those phases. Soft-lock, unfair-death and economy detectors also wait for those systems.
- **Nightly runs (200 seeds x 5 profiles):** not set up. There's only one bot profile and one scene to run them on.
- **Screenshot baselines:** none approved yet. That's yours to do (SETUP.md section 7).
- **Log error gate:** counted from Godot's output by `oa_verify`. I tried hooking Godot's logger from Rust, but it panicked inside gdext 0.5.5, so I dropped that approach.
- **Remapping UI and Deck button glyphs:** not built. The input map exists, but in-game remapping is a release-checklist item.
- **Menu:** placeholder. It fails the section 11 art bar on purpose.
- **GameState, Audio, Points and Notifications singletons:** not created yet; they come with the systems that need them.

## Verification report summary (section 19.6)
| Gate | Result |
|---|---|
| Rust unit tests | PASS: 56 (51 game + 5 tool) |
| Determinism tests | PASS: 12 (seed streams, reference xoshiro values, pinned golden values, bot inputs) |
| Data-file validation | PASS: 6 |
| Smoke test (boot, menu, slice, 60 s bot, quit) | PASS |
| Input replay determinism | PASS: 0.0 m drift |
| Screenshot regression | NOT RUN: no approved baselines; 3 new screenshots wait for you |
| Bots on 50 seeds x 5 profiles, balance simulator, fun proxies | NOT RUN: the systems they need don't exist yet |
| Steam Deck session and rubric | NOT RUN: needs your Deck |

Open bugs: none. Found and fixed during the phase:
- The replay ended one physics tick off (0.083 m), because recording and playback disagreed on when a run ends. The replay check caught it.
- Godot's logger hook panicked inside gdext.

Flagged screenshots: none flagged. 3 new ones need your approval, and the AI review needs your confirmation: they show the placeholder sphere lit correctly from the orbit and terminator views.
Fun proxies: not applicable yet.

## Rust concepts used for the first time
- **Workspace:** several crates in one `rust/` folder sharing one `target/` build folder.
- **`Result<T, E>`:** a value that is either `Ok(thing)` or `Err(problem)`. The save code uses it so a failed write is handled instead of crashing.
- **`static` with `Mutex` / `Once`:** a value shared by the whole program. `Mutex` lets only one piece of code change it at a time, and `Once` makes setup run exactly once. The crash handler uses both.
- **Singleton (`#[class(singleton)]`):** a Godot object that exists exactly once and is reachable from anywhere, including GDScript, by name, such as `RunSeed.get_code()`.
- **`serde` (`#[derive(Serialize, Deserialize)]`):** auto-generated code that turns a Rust struct into RON or JSON text and back.
