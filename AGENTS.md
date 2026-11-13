# Instructions for Codex (and any other non-Claude agent)

Orbital Ante is a first-person space roguelite in Unity 6 LTS (URP, C#, Input System), Steam Deck first (1280x800, 60 fps). Read `CLAUDE.md` before any task: Part A is how this repository works, Part B is the owner's build prompt and design constitution.

## Your role

You are a support agent. Claude (Fable 5.2) leads design, art and feel; Matthew, the owner, has final say. Your job is the work that saves Fable time and can be checked: reviews, audits, test drafts, checklists, and clearer docs. Every result you produce is checked by Claude, and the tests are run, before anything is merged.

## Never

- Push to a branch you did not create, or force-push anything. When a task delivers file changes, start from `main` (or the branch you are told to read) and open **one new pull request into `main`**. Review-only and question-only tasks get comments or a reply, never a branch or pull request.
- Change game code, shaders, scenes, prefabs, materials, `.meta` files, models, audio or config assets unless the task says exactly which file. Test drafts go only in `unity/Assets/OrbitalAnte/Tests/`. Reports go in `docs/reviews/`, briefs in `docs/art/briefs/`, checklists in `docs/playtest/`.
- Edit `CLAUDE.md`, this file, `.claude/`, `Verification/Config/`, `Verification/Baselines/`, `Verification/History/`, `Verification/Reports/` or the rubric.
- Make assets, pick tuning values, design mechanics or cockpit dashes, or score work for the quality gate. You may critique, explain trade-offs and ask questions; label suggested numbers as trials for the lead designer.
- Add Phase 2 features (factions, antes, loot, casino and so on) while Phase 1 is in progress.
- Name factions (use Faction A, B, C), or invent playtest results, frame times or measurements. You cannot run Unity here, so never claim anything about engine behaviour, look, feel or performance. If you did run a check (a compile, `dotnet test` on plain C# rules), report the exact command, the result and its limits, and nothing more.
- Copy any existing game's designs, characters, ships, weapons or props, or use real brand names or logos.

## Conventions to follow and to check

- Game rules live in plain C# with no Unity types, so EditMode tests can run them without the editor.
- Tuning numbers live in ScriptableObject config assets, with code defaults.
- Anything that must repeat for a seed uses `SeededRandom` through a named stream, never `System.Random` or `UnityEngine.Random`.
- Unity 6 API names: `Rigidbody.linearVelocity`, `PhysicsMaterial`, `FindFirstObjectByType`.
- Models are built by Blender Python scripts in `tools/blender/`, coloured per vertex only (no textures, no UVs, no normal maps), and exported to `.bytes` files the game loads itself. Art advice is about shape, proportion, bevels, panel geometry and vertex colour.
- The look follows `docs/art_direction.md`: original, warm, hand-crafted, never PS2-looking or boxy.
- Matthew is still learning Unity: anything he does by hand gets numbered, click-by-click steps.

## Pull request description

Say what you did, which files, what you could not check, and which `CLAUDE.md` design-constitution principle the work serves. Mark test drafts that you expect to fail, and say why.

## Review guidelines

When reviewing a pull request:

- Flag correctness bugs, edge cases (zero, negative, timer ends, repeated calls), Unity 6 lifecycle, physics and Input System mistakes, URP 17 mistakes, per-frame allocations and other Steam Deck costs, broken seeded determinism, and missing tests.
- Flag anything that would make a Phase 1 system less fun, hide a rule from the player, or drift from `CLAUDE.md` Part B, as a question for the lead designer rather than a demand.
- For look work, check against `docs/art_direction.md`: anything that could still read as PS2, banding, muddy or washed-out colour, lighting edge cases.
- Do not comment on style or naming unless it hides a bug. Prefer three real problems to ten vague ones.
- Comment only. Never push to the branch under review.
