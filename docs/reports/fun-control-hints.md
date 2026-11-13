# Psychology research and truthful control hints

Baseline: `main` at `224642ed5bf6da5553334c02b0f7236864cd8b64`. Status: research and code prepared for Fable's review; no Unity execution, player observations or Deck measurements. This is an initial application, not a claim that enjoyment has improved.

## What changed and why

The [research handoff](../research/fun-and-motivation/README.md) synthesizes 14 publications about motivation, choice, goals, curiosity, challenge, feedback, responsiveness and engagement. Each evidence file includes methods, samples, source links and transfer limits. Fable retains creative, design, art, feel and tuning authority.

The concrete problem was a mismatch between accepted rebindings and subsequent instructions. Arena help and its start-fight banner advertised default keys; flight movement and map zoom advertised WASD/left stick. Meanwhile the simple display helper assumed a gamepad binding at index 1, which is a keyboard composite part for movement.

The implementation:

- `Core/GameInput.cs`: describe effective bindings by device group; include composite parts and optionally filter an axis. Missing or disabled bindings say `Unbound`. Increment a revision when the existing controls flow saves or resets bindings.
- `UI/Hud.cs`: update arena instructions and the hold-fire banner from the current bindings; cache the pickup label for the device in use instead of looking up two display strings every frame.
- `UI/FlightHud.cs`: describe the current movement composite/vector and fixed pause binding.
- `UI/SystemMap.cs`: show effective keyboard Up/Down parts, or the bound pad vector with an explicit up/down cue.
- `Core/InputHintRefresh.cs`: plain-C# cache gate shared by the views. Initial display, accepted binding changes and explicit reopening refresh without the periodic wait. Device-only text rebuilds are spaced at least 0.25 seconds apart; stable hints refresh every two seconds. Flight evaluates this on its existing 0.125-second text tick. This gate does not delay input or gameplay actions.

This supports constitution principles 2 (agency), 3 (learnable challenge), 7 (cognitive load) and 10 (evidence-based iteration). The hypothesis is that truthful instructions reduce avoidable control errors and make customization easier to use. The studies do not establish that this specific patch increases competence, enjoyment or retention.

No weapons, damage, balance, rewards, difficulty, physics, shaders, assets, scoring or progression changed. No pending ship/look/launch implementation was copied onto main. Larger psychology-informed proposals and Phase 2 points questions remain in the handoff for Fable.

## Checks and their limits

**Seven plain-C# refresh-rule cases ran outside Unity: 7 passed, 0 failed, 0 skipped.** The unchanged production `InputHintRefresh.cs` and `InputHintRefreshTests.cs` were compiled with NUnit/NUnitLite 3.13.3 and executed on .NET 8.0.34. No Unity assemblies, Input System, devices or game stand-ins were part of that runner. Cases cover first display, stable polling, revision bypass, device timing, alternating-device limits, reopening and periodic refresh. These results concern only the refresh gate.

**Eight new Input System cases were compiled but not executed.** `Tests/PlayMode/InputHintTests.cs` covers device-group selection with reordered bindings, all rebound movement keys, a pad vector beyond composite parts, keyboard zoom filtering, pad zoom, override reset, disabled bindings and missing groups. Each case owns/disposes its actions without changing live controls, PlayerPrefs or devices. PlayMode's assembly definition now explicitly references `Unity.InputSystem`.

**A compatibility compilation completed with exit code 0 for all 116 runtime C# files and the new Input System fixture. This was not a Unity 6 project build.** It used Roslyn from .NET SDK 8.0.428, older `UnityEngine.Modules` 2021.3.33 reference assemblies, Input System 1.11.2 sources from Unity's package registry and official uGUI `6000.0` sources at commit `58c0ead281dc1cf042e8831606850413524971bb`.

Temporary copies mapped Unity 6 names to their older equivalents: `linearVelocity`→`velocity`, `linearDamping`→`drag`, `angularDamping`→`angularDrag`, `PhysicsMaterial[Combine]`→`PhysicMaterial[Combine]`, and `FindFirstObjectByType`→`FindObjectOfType`. uGUI's temporary copy omitted two unsupported `RectTransform.sendChildDimensionsChange` assignments and supplied a compile-only `PenStatus.None` enum. Input System's Unity 6000-specific defines and InputForUI subassembly were omitted. No repository source was changed for these compatibility steps. NUnit compilation emitted framework-reference identity warnings (CS1701).

These checks can catch some syntax/type problems; they cannot establish original Unity 6 API/package compatibility, shader compilation, lifecycle, saved-input integration, controller behavior, layout, feel or performance. The compatibility harness is scratch infrastructure, not a shipped alternative build. The committed fixtures can be run in the actual Unity project as described below. `git diff --check` also completed without whitespace errors.

## Agent review

Three independent support reviewers used the repository's Unity-reviewer, game-designer and Deck-performance checklists. These were source reviews by Codex support agents, not reviews conducted by Fable itself.

- **Unity:** no blocking source/API defect found. Verified display-method behavior against Input System 1.11.2 source. Retained explicit limits: flight waits for its next text tick, and long bindings need actual layout inspection.
- **Design:** the bounded correction supports agency and learnable controls without new Phase 2 mechanics. Corrected map wording to retain physical up/down directions. Research claims remain hypotheses for this game; familiarity, comprehension and enjoyment need separate observation.
- **Deck performance:** the first draft let device changes bypass every interval, potentially rebuilding text repeatedly with timestamp jitter. Replaced it with the shared rate gate. No blocking cost issue found in the final source; allocations still occur on refresh, and intentional-device inference is unchanged. No performance measurements were made.

## Playtest protocol for Fable

**Problem:** accepted customization can disagree with instructions. **Prediction:** truthful hints reduce use of an obsolete key without increasing confusion or hiding the action needed next. **Smallest comparison:** the main baseline and this patch, with mechanics/tuning unchanged and the same starting scenario. Counterbalance build order if comparing player behavior; record prior experience and practice effects. This is a formative usability check, not a powered efficacy study.

1. Open the `unity/` project in Unity 6 using [SETUP](../SETUP.md). Resolve actual editor/package errors first. Open **Window > General > Test Runner**; run `InputHintRefreshTests` in **EditMode** and `InputHintTests` in **PlayMode**. Record actual results rather than inferring them from the standalone compilation.
2. In the combat arena, pause, open **Controls**, and rebind hold-fire and respawn-dummies to unused keys. Resume and inspect help plus the hold-fire banner. Exercise accepted changes, a conflict swap, a canceled change and a group reset. Repeat for the controller. Resetting one device must retain the other device's custom bindings. Check the pickup instruction as well.
3. In flight, rebind movement to I/K/J/L or another accepted layout. Inspect thrust help and open the map: zoom must show the effective Up/Down keys, without Left/Right. Close/reopen the map, reset, and inspect again. If changing the pad vector to another supported control, resolve any conflict through the existing dialog, then check the actual accepted mapping and physical up/down hint.
4. Alternate keyboard/mouse and controller use, then leave each idle. Check whether labels name the device being used without distracting oscillation. The gate bounds reconstruction; it does not prove that raw device timestamps identify player intention correctly. Hot-plug/disconnect behavior remains an engine check.
5. At 1280×800, inspect long accepted bindings such as mouse or shoulder/stick names. Text must stay readable, fit its help area and avoid combat/map information. Ask what action the instruction means before giving a hint. Fable chooses any presentation revision if the four-key binding order or longer names confuse players.
6. In a Development build on Deck, record CPU frame time, **GC Alloc**, GC collections and text/canvas rebuilds in Unity's Profiler, comparing equivalent scenes and seeds with Deep Profile initially off. Inspect steady use, a visible pickup, rebinding, rapid mixed-device input and map open/closed states. Do not assign this patch a millisecond cost from source estimates.

Functional acceptance requires every accepted mapping to be advertised correctly after the next scheduled text refresh, canceled edits to retain the old mapping, reset/swap/reopen flows to agree with actual input, and readable labels for the accepted layouts. The seven plain cases do not establish those end-to-end conditions. A device-only gate change can also wait for a view's text tick; there is no strict 250 ms rendered-response promise.

For the player comparison, record obsolete-key errors and whether players can explain the correct next action without prompting. Ask enjoyment and perceived control separately; any voluntary retry is context, not proof of lasting engagement. Revise if the patch trades a correctness gain for recurring layout confusion or unwanted distraction. Report individual failures as well as totals. Fable makes the final presentation and design judgment.

## Where this work stops

The research, first code correction, source reviews, plain-rule execution and compatibility checks are complete. Actual Unity execution, end-to-end control tests, screenshots, Deck measurements and player observation remain outstanding. Points, difficulty, discovery, pacing and reward changes are proposals for Fable rather than implemented systems. No existing remote branch was updated, and no merge or deployment is part of this change.
