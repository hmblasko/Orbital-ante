# Persistent controls and access on Steam Deck

Support engineering for Fable 5.2 to review. Base: `main` at `4cfae583df19dff5379963d4c4339f37fa8b5230`. Fable keeps design, art, feel, default bindings and tuning authority. No Unity Editor or Steam Deck execution was possible here.

## Problem and smallest change

The build prompt requires bindings that persist between sessions and a pause menu fully navigable with keyboard, mouse and controller. Two source-level defects undermine that:

- `GameInput.Initialize` builds fresh bindings each launch. Input System 1.11.2 generates a random binding ID in `InputActionSetupExtensions.AddBindingInternal`. `SaveBindingOverridesAsJson` stores that ID, and `LoadBindingOverridesFromJsonInternal` searches only by ID; the saved action label is not a fallback. A later construction therefore cannot find the saved controls.
- `ControlsPage` puts every row in an expanding card without a scroll view. Current main defines 18 keyboard / 15 controller rows on foot and 21 / 18 in flight, including unbound slots and fixed mouse look. The flight keyboard list alone requests 836 layout units before the title, tabs and footer. The canvas reference height is 800.

Hypothesis: stable logical slot identities restore saved overrides after reconstruction, and a clipped scrolling list with an explicit controller traversal makes every existing row and footer reachable without altering Fable's presentation or controls.

Changes:

- `Core/BindingIdentity.cs`: plain C# identity rule, hashing a versioned, length-prefixed namespace, map, action, device groups, slot name, kind and composite context. Binding paths and list positions are excluded so a changed default or an inserted action cannot redirect a saved control.
- `Core/InputBindingIdentity.cs`: assign those IDs through public Input System APIs before maps are enabled. Copy each complete binding before replacing its ID, preserving composites, groups, defaults, processors and overrides. Sort group names for stable multi-group identities; reject ambiguous slots instead of assigning two controls the same ID.
- `Core/GameInput.cs`: assign Gameplay and Flight IDs immediately before loading overrides. System and UI controls keep their existing fixed behavior.
- `UI/ControlsPage.cs`: contain the existing rows within a 480-unit vertical viewport; retain the existing text, buttons, colours and row sizes. Follow a newly selected binding into view while allowing wheel/drag scrolling. Link controller traversal through the rebindable rows and the existing tabs/footer.
- Regression drafts under `Tests/EditMode` and `Tests/PlayMode`, plus the cost notes in `docs/performance_budget.md`.

This serves constitution principles 2 (agency), 7 (cognitive load) and 10 (evidence-based iteration). It adds no mechanic, balance change or Phase 2 feature.

## Save contract and integration

The existing PlayerPrefs key `oa.bindings.v1` and Input System override JSON format stay in use. Overrides saved after this fix have repeatable IDs. No saved preferences are deleted by this change.

Old JSON written with random IDs cannot be reliably migrated: it records an action and an override path, but not which original device/composite slot that ID represented. Those old overrides remain unmatched; the player must rebind affected controls once. Guessing could bind the wrong direction or device. This limitation needs Fable's review before release.

Map/action names and explicit slot names are now persistent identifiers. Renaming one requires a deliberate migration. If an action gains multiple slots of the same kind in one group, give those slots unique names; name repeated composite roots as well. Unnamed duplicate slots fail during initialization with an explanatory exception. New actions with unique names need no special ID bookkeeping.

Fable's PR #38 adds gameplay actions, including Deep Breath. The assignment runs after all definitions, so those actions are included when the integration is reviewed. This PR targets main and does not update Fable's branches. It does not supersede the existing navigation, tactics or control-hint support PRs.

## Checks performed

- `git diff --check`: passed.
- `python3 /workspace/work/controls-check/compile_workspace.py --baseline`: exit 0, 174 runtime source files from the unchanged main snapshot.
- `python3 /workspace/work/controls-check/compile_workspace.py --runtime --plain-suite /workspace/work/controls-check/plain.json --engine-suite /workspace/work/controls-check/engine.json`: the 13 plain identity cases passed; the first runtime compile caught a `Navigation` namespace/type collision in the new explicit UI links. It was corrected with a `UiNavigation` alias.
- `python3 /workspace/work/controls-check/compile_workspace.py --runtime --engine-suite /workspace/work/controls-check/engine.json`: final exit 0, 176 runtime source files and 21 engine-dependent cases compiled. Those 21 cases were **not executed**: 18 Input System persistence cases and 3 controls layout/navigation cases.
- Applied the runtime patch cleanly to a temporary local copy of Fable's PR #38 at `e19070561334b17dc220e349206b6a5d63370f35`, preserving Deep Breath and all other playtest changes. `python3 /workspace/work/controls-playtest-check/compile_workspace.py --runtime --engine-suite /workspace/work/controls-playtest-check/engine.json`: exit 0, 191 combined runtime source files and the same 21 engine cases compiled only. Fable's branch was not changed.

The temporary harness uses the local .NET SDK 8.0.428 / runtime 8.0.34 and Roslyn directly, with NUnit/NUnitLite 3.13.3. The plain suite compiles the unchanged production `BindingIdentity.cs` and its test file without Unity references or behavioral substitutes. Its XML result is `/workspace/work/controls-check/plain/BindingIdentity/results.xml`.

Compatibility compiles use older UnityEngine reference assemblies (2021.3.33), the actual Input System 1.11.2 source assembly and a temporary uGUI compatibility assembly. Temporary source copies map Unity 6 API names to the older equivalents (`linearVelocity`, damping, `PhysicsMaterial`, and object-finding APIs). A metadata-only `UnityTestAttribute` shim permits compiling the three coroutine cases; it supplies no engine behavior. NUnit emits a netstandard 2.0 / 2.1 reference-unification warning. Harness manifests, source hashes, compiler response files and logs remain under `/workspace/work/controls-check/` and `/workspace/work/controls-playtest-check/`; these are session-local artifacts, not repository tools.

These checks are not a Unity 6 build, PlayMode execution, a player build, layout verification or a Deck performance result. No engine-dependent case is reported as passed. The final Unity and Deck protocol below remains required before merge.

## Agent review

Independent Codex support agents reviewed this against the repository's Unity-reviewer, game-designer and Deck-performance checklists. They are not Fable/Claude sessions, and this does not replace Fable's review.

- Unity/Input System: no remaining actionable source findings in stable identities, complete-binding replacement, composite context, initialization order, scroll bounds or reset interactions. The compiler caught and resolved the UI namespace collision noted above.
- Design: the result serves the existing saved-controls and controller-navigation requirements without adding mechanics, picking feel values or changing presentation. Old random-ID saves and future identifier renames remain explicit migration limits for Fable.
- Deck/access: explicit navigation fixes the risk of automatic navigation choosing the fixed footer before an off-screen row. Tests now traverse the real selectable links in both directions, including the footer's Back link. Cached geometry and selection-change gating avoid new continuous layout work. `Canvas.ForceUpdateCanvases` on each changed row remains the main cost to measure while holding D-pad navigation.

## Acceptance criteria and engine protocol

Acceptance: after a full application restart, on-foot and flight overrides remain on the intended device/action; resetting one device in the active map preserves other overrides. At 1280 x 800, tabs and footer remain on-screen, every rebindable row is reachable in both controller directions, selection is fully visible, and wheel/drag scrolling does not snap back to an unchanged selection. Reserved pause controls and cancel/swap behavior continue working.

For Fable/local Claude with the Unity Editor:

1. Open `unity/` in Unity Hub. Run `tools/check-in-unity.ps1` on Windows, or `bash tools/check-in-unity.sh` on macOS/Linux, following `docs/CLAUDE_CODE_LOCAL.md`. Report the real EditMode/PlayMode results; the cloud compile is not a substitute.
2. In the Game view, set a 1280 x 800 resolution. Start a run; press Esc or controller Menu, then Controls. Check the keyboard and controller tabs on foot and while seated in flight. Navigate from tabs through every rebindable row to Reset/Back and upward again; check each selected row is fully visible.
3. Rebind one on-foot keyboard movement direction and one controller action. In flight, rebind one keyboard action and one controller action. Quit the application fully, relaunch, and inspect/use all four overrides. Also check an intentionally unbound slot and a slot whose default is empty.
4. Reset only the flight controller tab, restart the application, and confirm the flight keyboard and both on-foot overrides remain. Exercise rebind timeout, cancel, a conflicting binding's cancel and swap, plus fixed Esc/Menu pause access.
5. Wheel-scroll or drag while a row remains selected. Confirm the list stays where placed until controller selection changes. Reopen Controls, switch tabs, open/cancel a conflict prompt and reconnect a controller; confirm no row, footer or focus becomes inaccessible.
6. On Deck, repeat controller navigation and the full restart check in a native Linux build. In Unity's CPU Profiler inspect Controls while idle and while holding navigation for layout spikes and GC Alloc. Record actual observations; no frame-rate claim is made here.

## Separate follow-up findings

The parallel source audit found two save-safety issues outside this controls change. They are recorded for the next bounded engineering pass, with no save-system changes included here:

- `SaveSystem.Load` rejects a future-version save, but `SaveSystem.Save` and `GameBootstrap.OnApplicationQuit` can then overwrite its slot with fresh/older data. Two writes can remove the last newer-version backup. Protect an incompatible slot from writes; test byte-for-byte preservation across repeated saves, including a compatible older backup.
- `SaveFileStore.TryRead` can recover a good backup while leaving the damaged main file in place. The next `Write` deletes that good backup and promotes the damaged main into its place before committing the replacement. Preserve the known-good recovered backup through replacement; test recovery followed by another save, including interruption before final promotion.

These are source findings, not reproduced Unity or player incidents. Fable should review their save/UI implications when that follow-up is implemented.
