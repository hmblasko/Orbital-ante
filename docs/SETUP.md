# Setup: Orbital Ante dev environment

Checked on 30 Oct 2026.

| Piece | Version | Why this one |
|---|---|---|
| Godot | **4.7.2-stable** (standard build, not .NET) | Latest stable 4.x. 4.8 is still in beta. |
| godot-rust (gdext) | **`godot` crate 0.5.5**, feature `api-4-7` | Latest gdext release. It has an `api-4-7` feature, so it is built against the Godot 4.7 API. |
| Rust | **1.94 or newer** (stable) | gdext 0.5.5 needs at least 1.94. I built and tested with 1.97. |
| Physics | Jolt (built into Godot 4.7) | Already set in `project.godot` under Physics > 3D > Physics Engine. |

Compatibility check: I loaded the extension in Godot 4.7.2 and it printed
`Initialize godot-rust (API v4.7.stable.official, runtime v4.7.2.stable.official)`.
If you pick up a newer 4.7.x patch, nothing needs to change. If you move to 4.8 later, first update the
`godot` crate and change `api-4-7` to the matching feature in `rust/Cargo.toml`.

Rust note: a **crate** is a Rust package (ours is `rust/`), and **cargo** is the tool that builds it and downloads its dependencies.

---

## 1. Install Rust

**Windows**
1. Go to https://rustup.rs and download `rustup-init.exe`.
2. Run it. If it says you need the Visual Studio C++ build tools, accept the option to install them. In the Visual Studio installer, tick **Desktop development with C++** and click **Install**.
3. When rustup asks, pick **1) Proceed with standard installation**.
4. Close the terminal and open a new **PowerShell**, then check it worked:
   ```
   rustc --version
   cargo --version
   ```
   The rustc version must be 1.94 or newer. If it is older, run `rustup update stable`.

**Linux (or Steam Deck desktop mode, see section 6)**
1. In a terminal:
   ```
   curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
   ```
2. Choose **1) Proceed with standard installation**, then run `source ~/.cargo/env`.
3. Check: `rustc --version`. You also need a C linker: on Ubuntu/Debian run `sudo apt install build-essential`.

## 2. Install Godot 4.7.2

1. Go to https://godotengine.org/download/archive/4.7.2-stable/ (or the main download page if 4.7.2 is still the newest).
2. Download **Godot Engine, standard**, not the .NET version, for your OS (Windows 64-bit or Linux x86_64).
3. Unzip it somewhere permanent, such as `C:\Tools\Godot\` or `~/Tools/Godot/`. Godot doesn't need installing, the .exe or .x86_64 file is the whole editor.
4. Optional: pin it to your taskbar.

## 3. Install Git and Git LFS

**Windows**
1. Download Git from https://git-scm.com/download/win and run the installer. The defaults are fine and include Git LFS.
2. Open a new PowerShell and run:
   ```
   git --version
   git lfs install
   ```
   `git lfs install` only needs running once per computer. It should say "Git LFS initialized".
3. Tell Git who you are (once):
   ```
   git config --global user.name "Your Name"
   git config --global user.email "you@example.com"
   ```

**Linux**: `sudo apt install git git-lfs`, then `git lfs install` and the two `git config` lines above.

The repo's `.gitattributes` already routes models, textures and audio under `godot/assets/` through LFS.

## 4. Get the project and build the Rust library

This is the "gdext project template" step. The repo already follows the standard gdext layout, so there is nothing to generate:

```
orbital-ante/
  CLAUDE.md                 master build prompt
  godot/                    the Godot project (open this in the editor)
    project.godot
    orbital_ante.gdextension   tells Godot where the Rust library is
    assets/  data/  scenes/  tests/
  rust/                     the Rust crate (all game code)
    Cargo.toml
    src/  orbits/ flight/ player/ combat/ ai/ procgen/ points/ items/
          horror/ audio/ save/ ui_bridge/ data/
  docs/  reports/  tools/
```

1. Put the project folder somewhere without spaces in the path, such as `C:\dev\orbital-ante` or `~/dev/orbital-ante`.
   - If it's on GitHub: `git clone https://github.com/<you>/orbital-ante.git`
   - If you got the zip from me: unzip it there.
2. Build the Rust library:
   ```
   cd orbital-ante/rust
   cargo build
   ```
   The first build downloads dependencies and takes a few minutes. It produces
   `rust/target/debug/orbital_ante.dll` (Windows) or `liborbital_ante.so` (Linux).
3. Run the unit tests:
   ```
   cargo test
   ```
   Expect every line to say `test result: ok` (74 tests in total as of Phase 2).

## 5. Open and run the test scene on PC

1. Start Godot. In the **Project Manager**, click **Import**.
2. Browse to `orbital-ante/godot/project.godot`, select it, and click **Import** (in some versions **Import & Edit**).
3. Wait for the first import to finish. The **Output** panel at the bottom should show
   `Initialize godot-rust (API v4.7...)`. If it says it can't find the library instead, re-run `cargo build` (step 4.2) and restart Godot.
4. Press **F5** (or the ▶ **Run Project** button, top right). The sphere-walk test scene starts.
5. Controls:
   | Action | Controller | Keyboard / mouse |
   |---|---|---|
   | Move (on the ground) / jetpack thrust (in the air) | Left stick | W A S D |
   | Look | Right stick | Mouse (or arrow keys) |
   | Jump; hold in the air for jetpack up | A | Space |
   | Crouch (hold); in the air, jetpack down | B | Ctrl or C |
   | Match velocity (hold, in the air) | LT | Left Shift |
   | Interact (pick up an oxygen tank) | X | E |
   | Use an oxygen tank | D-pad left | G |
   | Clip on / unclip the safety tether | D-pad down | T |
   | Frame-time overlay on/off | L3 (click left stick) | F3 |
   | Free / recapture mouse | | Esc / click |
6. To stop, press **F8** or close the game window.

Whenever you change Rust code: run `cargo build` again. Godot hot-reloads the library while the editor is open; if something looks stale, close and reopen the project.
Whenever you change a `.ron` file in `godot/data/`: just run the scene again, no rebuild needed.

## 6. Steam Deck test build route

The Deck runs Linux, so it needs the **Linux** build.

### Route 1 (recommended): download the build GitHub makes for you

Every push to GitHub runs the CI workflow (`.github/workflows/ci.yml`). It tests the game, then builds Linux and Windows versions.

1. On github.com, open the repo and click the **Actions** tab.
2. Click the newest run with a green tick.
3. Scroll to **Artifacts** and download **OrbitalAnte-linux**. It's a zip containing `OrbitalAnte.x86_64`, `OrbitalAnte.pck` and `liborbital_ante.so`.
4. Unzip it and copy the folder to the Deck, then do steps 5 to 8 of Route 2 below.

Tagged versions such as `v0.1.0` also get a GitHub **Release** with both zips attached.

### Route 2: export a Linux build from your PC

1. **Get a Linux build of the Rust library.**
   - If your PC runs Linux: `cd rust && cargo build --release`.
   - If your PC runs Windows: install WSL (open PowerShell as admin, run `wsl --install`, reboot, create a user).
     In the Ubuntu window: `sudo apt install build-essential`, install Rust with the Linux steps in section 1, then
     `cd /mnt/c/dev/orbital-ante/rust && cargo build --release`.
     That puts `liborbital_ante.so` in `rust/target/release/`, where `orbital_ante.gdextension` expects it.
2. **Install export templates (once).** In Godot: **Editor** menu > **Manage Export Templates…** > **Download and Install**.
3. **Presets are already set up** in `godot/export_presets.cfg` (Linux and Windows). They include the `data/` folder of RON files, which Godot would otherwise leave out.
4. **Export.** **Project** > **Export…** > select the Linux preset > **Export Project…**. Untick **Export With Debug**
   for a release build, or leave it ticked to see errors on the Deck. Save as `builds/linux/OrbitalAnte.x86_64`.
   Godot copies `liborbital_ante.so` next to it. Check that the folder holds `OrbitalAnte.x86_64`, `OrbitalAnte.pck` and `liborbital_ante.so`.
5. **Copy to the Deck.** Any of these:
   - USB stick or microSD card: copy the whole `builds/linux` folder.
   - Network: on the Deck in Desktop Mode, open Konsole, run `passwd` to set a password, then `sudo systemctl start sshd`;
     on the PC run `scp -r builds/linux deck@steamdeck:~/OrbitalAnte`.
   Put it in `/home/deck/OrbitalAnte/`.
6. **Make it launchable (Desktop Mode, once).**
   1. Open Dolphin (file manager), go to `OrbitalAnte`, right-click `OrbitalAnte.x86_64` > **Properties** > **Permissions** > tick **Is executable** > **OK**.
   2. Open Steam > **Games** menu > **Add a Non-Steam Game to My Library…** > **Browse…** > pick `OrbitalAnte.x86_64` > **Add Selected Programs**.
7. **Switch back to Gaming Mode** and launch Orbital Ante from your library (it's under Non-Steam).
8. **Controller layout:** on the game's page, press the controller icon and pick the **Gamepad with Mouse Trackpad** template.
   That makes the right trackpad act as a mouse, which the game already uses for looking.

### Route 3: run the editor on the Deck

Possible, but SteamOS has no C compiler by default, so building Rust there needs a container (Distrobox).
I'd skip this; Route 1 is simpler.

### Measuring frame times on the Deck

- The in-game overlay (L3 / F3) shows average, worst-1% and max frame time, render CPU/GPU time, physics time and draw calls.
- Every 5 seconds it adds a row to `frametime_log.csv`. On the Deck that file is at
  `/home/deck/.local/share/godot/app_userdata/Orbital Ante/frametime_log.csv`. Copy it off and send it to me; that is a real measurement I can report.
- Steam's own overlay: press **…** (Quick Access) > battery icon > **Performance Overlay Level** 3 or 4 for a frame-time graph.

## 7. Running the checks yourself

`tools/verify.sh` runs everything that "every commit" needs (CLAUDE.md section 19.1) and writes an HTML report.
On Windows, run it from **Git Bash** (installed with Git).

```
GODOT=/path/to/Godot_v4.7.2-stable_linux.x86_64 tools/verify.sh
```

On Windows Git Bash, use your Godot path, for example `GODOT="/c/Tools/Godot/Godot_v4.7.2-stable_win64_console.exe" tools/verify.sh`.
Add `--no-render` to skip screenshots. Open `verify_out/latest/report.html` when it finishes.

What it runs: Rust unit tests, determinism tests and data-file checks; a smoke test (boot, menu, test scene, 60-second autoplay bot, quit) with
detectors for getting stuck, falling out of the world and physics blow-ups; a replay of that bot run that must end in exactly the same place;
and screenshots from fixed viewpoints compared against approved baselines.

**Approving screenshot baselines (only you do this):** look at the screenshots in the report. If they look right, run
`rust/target/debug/oa_verify approve verify_out/latest/screenshots tests/baselines` and commit `tests/baselines/`.
Baselines must come from the same kind of machine that checks them (CI uses software rendering), so approve the ones from the CI report artifact.

**Recording your own play for a bug report:**
```
godot --path godot -- --record=/tmp/my_run.ron --seed=7K3M-Q9TD
```
Play, quit, and send the file. Anyone can then reproduce it exactly with `-- --replay=/tmp/my_run.ron`.

**Filing bugs:** `oa_verify bugs verify_out/latest` writes `bugs.md`. Add `--file hmblasko/orbital-ante` to create GitHub issues
(needs the `gh` tool, signed in); open issues with the same signature are skipped.
