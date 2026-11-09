# Orbital Ante: instructions for the coding agent

**Source of truth:** `docs/BUILD_PROMPT.md` (Matthew's full build prompt, saved word for word).
Read it before starting any milestone. `docs/DECISIONS.md` records answers to open questions.
If this file and the build prompt ever disagree, the build prompt wins; ask Matthew.

The earlier Godot + Rust build is archived on the `archive/godot-rust` branch. It is reference
only; do not port its rules or content without asking.

## Stack
- Unity 6 LTS, C#, URP, Input System package. Steam Deck first (native Linux, 1280x800),
  keyboard and mouse fully supported.
- Code lives in `Assets/OrbitalAnte/Scripts/` in assembly definitions: `OrbitalAnte.Runtime`,
  `OrbitalAnte.Editor`, tests in `Assets/OrbitalAnte/Tests/` (`OrbitalAnte.Tests.EditMode`).
- Tunable numbers live in ScriptableObjects under `Assets/OrbitalAnte/Resources/Config/`.
  No magic numbers in gameplay code.
- Scenes hold only a `GameBootstrap` object; everything else is built in code. The editor menu
  **Orbital Ante > Run Project Setup** creates the URP asset, config assets, materials and scenes.
- All randomness goes through `RunSeed.CreateStream("name")` (SplitMix64), never `UnityEngine.Random`,
  except for purely cosmetic effects such as gib spin.
- Every script, folder and asmdef under `Assets/` needs a committed `.meta` file with a stable GUID.
  When adding files from outside Unity, write the `.meta` too (see existing ones for the format).

## Standing rules (from the build prompt)
1. Matthew is learning Unity. Anything Matthew must do: numbered, click-by-click steps.
2. Everything is first person. No third-person camera, ever, including crashes and death.
3. Never invent faction names (use Faction A/B/C). Unlocks are sidegrades only.
4. Phase 1 rule: refine before expanding. No Phase 2 features during Phase 1 beyond the bare
   minimum to test a Phase 1 system. Stop at the Phase 1 sign-off gate.
5. Reporting: never invent playtest results or frame times. After each piece of work, report what
   changed, why, which constitution principle it serves, what was tested, and what is unverified.
6. For each change state the problem, hypothesis, smallest test, acceptance criteria and playtest
   protocol (constitution principle 10).
7. List exactly which art/audio assets are needed for each system so Matthew can generate them.
8. Log every third-party asset in `docs/asset_licence_ledger.md`.

## Verifying work
- This repo's cloud agent cannot run the Unity editor. Code is compiled and playtested on
  Matthew's machine (Unity Test Runner + Play mode). Say so plainly in every report.
- EditMode tests: Window > General > Test Runner > EditMode > Run All.
