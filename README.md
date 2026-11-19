# Orbital Ante (working title)

First-person space-exploration roguelite for Steam, Steam Deck first. Unity 6 LTS, URP, C#.

- `CLAUDE.md` holds the build prompt (the source of truth for the design) and notes on how this repo works.
- `docs/SETUP.md` has step-by-step setup: installing Unity, opening the project, playing, running the tests.
- `docs/reports/` has one report per milestone, each with a playtest checklist.
- `unity/` is the Unity project. Open that folder from Unity Hub.

The earlier Godot + Rust version is kept on the `godot-archive` branch.

## Where the build is

Phase 1, Milestones 1 to 7: breakable playtest characters in a combat test arena, gib juice (blood, splats, hitstop, camera shake, pop sounds, pooled gibs), data-driven weapons with disarming, the foundation (seeded random streams, save system, content database, Steam Deck performance budget in `docs/performance_budget.md`), the moving solar system with a stand-in test craft and live map, and the planets: real ground on all 19 planets and moons built from an index of 148 natural formations (`docs/formation_index.md`), caves, seas, gas giants you fly down into, skies, haze and weather, and the starter ship: a Lunar Module style lander with a walk-about cabin lit by baked light, belly hatch and gravity lift, buckle-in seat, breakable glass, and parts that can be damaged, mended or torn off. Not yet run in Unity. See `docs/reports/milestone-1.md` to `milestone-7.md`.

Specialist agents that help build the game are described in `.claude/agents/README.md`.
