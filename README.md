# Orbital Ante (working title)

First-person space-exploration roguelite for Steam Deck. Godot 4.7 + Rust (gdext 0.5).

- `CLAUDE.md` is the master build prompt and the single source of truth.
- `docs/SETUP.md` has the toolchain setup, how to run, and the Steam Deck build route.
- `reports/` holds the verification report for each phase.

Quick start (after setup):

```
cd rust && cargo build && cargo test
```

Then open `godot/project.godot` in Godot 4.7.2 and press F5.

Scripted smoke test (from `godot/`):

```
godot --headless --path . --fixed-fps 60 --script res://tests/sphere_walk_smoke.gd
```
