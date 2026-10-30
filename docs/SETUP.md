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
   Expect `test result: ok. 15 passed`.

## 5. Open and run the test scene on PC

1. Start Godot. In the **Project Manager**, click **Import**.
2. Browse to `orbital-ante/godot/project.godot`, select it, and click **Import** (in some versions **Import & Edit**).
3. Wait for the first import to finish. The **Output** panel at the bottom should show
   `Initialize godot-rust (API v4.7...)`. If it says it can't find the library instead, re-run `cargo build` (step 4.2) and restart Godot.
4. Press **F5** (or the ▶ **Run Project** button, top right). The sphere-walk test scene starts.
5. Controls:
   | Action | Controller | Keyboard / mouse |
   |---|---|---|
   | Move | Left stick | W A S D |
   | Look | Right stick | Mouse (or arrow keys) |
   | Jump | A | Space |
   | Crouch (hold) | B | Ctrl or C |
   | Frame-time overlay on/off | L3 (click left stick) | F3 |
   | Free / recapture mouse | | Esc / click |
6. To stop, press **F8** or close the game window.

Whenever you change Rust code: run `cargo build` again. Godot hot-reloads the library while the editor is open; if something looks stale, close and reopen the project.
Whenever you change a `.ron` file in `godot/data/`: just run the scene again, no rebuild needed.

## 6. Steam Deck test build route

The Deck runs Linux, so it needs the **Linux** version of the Rust library (`liborbital_ante.so`).
For Phase 0 there are two routes. Phase 1 adds GitHub Actions, which will build the Linux version
for you on every push, and that becomes the main route.

### Route A (recommended now): export a Linux build from your PC

1. **Get a Linux build of the Rust library.**
   - If your PC runs Linux: `cd rust && cargo build --release`.
   - If your PC runs Windows: install WSL (open PowerShell as admin, run `wsl --install`, reboot, create a user).
     In the Ubuntu window: `sudo apt install build-essential`, install Rust with the Linux steps in section 1, then
     `cd /mnt/c/dev/orbital-ante/rust && cargo build --release`.
     That puts `liborbital_ante.so` in `rust/target/release/`, where `orbital_ante.gdextension` expects it.
2. **Install export templates (once).** In Godot: **Editor** menu > **Manage Export Templates…** > **Download and Install**.
3. **Add a Linux preset (once).** **Project** menu > **Export…** > **Add…** > **Linux**. Leave the defaults, but check that
   **Architecture** is `x86_64`. Close the window; Godot saves `export_presets.cfg`.
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

### Route B: run the editor on the Deck

Possible, but SteamOS has no C compiler by default, so building Rust there needs a container (Distrobox).
I'd skip this for now; Route A plus CI in Phase 1 is simpler.

### Measuring frame times on the Deck

- The in-game overlay (L3 / F3) shows average, worst-1% and max frame time, render CPU/GPU time, physics time and draw calls.
- Every 5 seconds it adds a row to `frametime_log.csv`. On the Deck that file is at
  `/home/deck/.local/share/godot/app_userdata/Orbital Ante/frametime_log.csv`. Copy it off and send it to me; that is a real measurement I can report.
- Steam's own overlay: press **…** (Quick Access) > battery icon > **Performance Overlay Level** 3 or 4 for a frame-time graph.
