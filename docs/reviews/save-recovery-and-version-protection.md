# Save recovery and newer-build protection

Support engineering for Fable 5.2 to review. Base: `main` at `4cfae583df19dff5379963d4c4339f37fa8b5230`. Fable retains design, assets, feel and presentation authority. This implements the two separate save findings recorded with controls PR #39; it does not change that PR or Fable's branches.

## Problem, hypothesis and scope

The foundation report promises that newer-build saves are left alone and that a damaged main can recover from a backup. Two code paths break those promises:

- `SaveSystem.Load` refuses a newer format, but run start, menu return and application quit still call `Save`. The next writes can overwrite the newer main and then remove its remaining backup. A newer backup is also at risk when a compatible main loads successfully.
- After `SaveFileStore.TryRead` recovers a good backup, the old `Write` deletes that backup and promotes the corrupt main into its place before committing the replacement. A later successful save leaves a corrupt backup; a failed promotion can leave no usable committed save.

Hypothesis: inspect both committed slots before writing, preserve files that require a newer build, and replace a damaged main without rotating it over the recovered backup. The smallest test is a real filesystem recovery followed by another save, plus Unity JSON drafts for the version/lifecycle paths.

Changed files:

- `Scripts/Runtime/Save/SaveFileStore.cs`: read-only `AnyFileMatches`; optional current-main validator on `Write`; preserve the backup after recovery; `File.Replace` for existing-main promotion, or `File.Move` when no main exists; clear recovery state only after success or deletion.
- `Scripts/Runtime/Save/SaveSystem.cs`: probe main and backup before load and each unprotected save; inspect a version-only JSON header; latch `WriteProtectedByNewerSave`; preserve compatible in-memory loading; reject writes after a read failure that prevented loading any usable data until an explicit reload/reset.
- `Tests/EditMode/SaveFileStoreTests.cs`: nine new filesystem regressions and stronger deletion-state coverage.
- `Tests/PlayMode/SaveProtectionTests.cs`: 21 new engine-dependent cases for future formats, byte preservation, resets, migration, corruption recovery and read errors.
- This report and the checkpoint cost note in `docs/performance_budget.md`.

The save layout, file names, `SaveData.CurrentVersion`, migrations and gameplay values are unchanged. This is Phase 1 reliability work, serving constitution principles 2 (agency), 6 (meaningful replayability) and 10 (evidence-based iteration).

## Behavior and boundaries

A future version in either main or backup blocks serialization, `Data.version` mutation, temporary-file writing and promotion. A current/old compatible slot may still load for the session. The warning occurs once while the protection latch remains set; a public `Load`, `UseFolder` or subsystem reset clears the latch and can warn again after inspecting the files. Every new unprotected save inspects both committed slots, including a save called before `Load` and a future file appearing after a successful load.

The version probe deserializes only a small header class so changed types in future payload fields do not have to parse as current `SaveData`. It still reads each entire file. A malformed/unrecognized file without a readable future-version header follows the existing corruption policy; this is not a general parser for unknown formats.

Inspection read errors abort writes before touching any file. During `Load`, a read error does not hide the other readable slot: compatible main/backup recovery is still attempted. If no usable data loads after such an error, a private reload-required latch prevents the fresh in-memory session from later overwriting unread progress after access returns. A successful explicit `Load`, folder change or reset clears that state; every actual write still performs the normal inspections.

For recovery, `Write` receives the current-format validator from `SaveSystem`, rechecks the current main before promotion and retains the existing backup if the main is invalid. Generic callers can also validate explicitly; calls without a validator rely on this instance's last recovery state. Normal subsequent writes again back up the preceding good main. Failed promotions do not clear recovery protection.

No new fallback to delete-and-move is used if `File.Replace` is unsupported: a failed save logs a warning and leaves the existing committed files subject to the target runtime's operation semantics. This intentionally requires local runtime validation. The change does not promise cross-process/Steam Cloud transactional coordination or that directory metadata survives power loss on every filesystem. Changes by another writer between inspection and promotion are outside its guarantees.

## Checks performed

- `git diff --check`: passed.
- `python3 /workspace/work/save-check/compile_workspace.py --plain-suite /workspace/work/save-check/OriginalBackupRegression.json`: the one-case filesystem reproduction **failed as expected against unchanged main**, observing `broken` in the backup where `save:good` was required. This demonstrates the recovery defect outside Unity.
- `python3 /workspace/work/save-check/compile_workspace.py --plain-suite /workspace/work/save-check/FixedBackupRegression.json`: the same one-case reproduction passed against the changed production file.
- `python3 /workspace/work/save-check/compile_workspace.py --runtime --plain-suite /workspace/work/save-check/plain.json`: 20 plain save/filesystem/migration cases passed, using the real temporary filesystem; no Unity types or behavior substitutes. This includes failed promotion/retry, previous-good rotation, fresh instances, explicit validation and probe/read-error cases.
- `python3 /workspace/work/save-check/compile_workspace.py --baseline`: unchanged main's 174 runtime source files compiled against the available compatibility references.
- `python3 /workspace/work/save-check/compile_workspace.py --runtime --engine-suite /workspace/work/save-check/engine.json`: final exit 0, 174 candidate runtime source files and 30 engine-dependent cases compiled: 21 new protection cases plus 9 existing Foundation cases. These engine cases were **not executed**.
- Applied this runtime patch cleanly to a temporary local copy of Fable's current PR #38 at `ab085c4b7c21a9dfc3d7a3345cd1b6b9b84a6c99`. Also applied the already-published controls PR #39 runtime/tests locally to check their combined integration. `python3 /workspace/work/save-integration-check/compile_workspace.py --runtime --engine-suite /workspace/work/save-integration-check/engine.json`: exit 0, 198 combined runtime source files and 51 engine-dependent cases compiled only (the 30 above plus PR #39's 21). Neither Fable's branch nor PR #39 was changed.

The session-local harness uses .NET SDK 8.0.428 / runtime 8.0.34, Roslyn directly, and NUnit/NUnitLite 3.13.3. The plain production/test files are compiled unchanged without Unity references. XML results and exact compiler inputs/hashes/logs are retained under `/workspace/work/save-check/`; the normal 20-case result is `plain/SaveRulesAndFilesystem/results.xml`. The regression fixture and original production snapshot are scratch artifacts, not shipped test/code changes.

Compatibility compilation uses older UnityEngine 2021.3.33 reference assemblies, the actual Input System 1.11.2 source assembly and a temporary uGUI compatibility assembly. Temporary source copies map Unity 6 velocity/damping, physics-material and object-finding names to those older references. The combined controls check additionally uses a metadata-only `UnityTestAttribute` shim for its three coroutine drafts, with no behavioral implementation. NUnit emits a netstandard 2.0 / 2.1 reference-unification warning. Integration manifests, source hashes and logs are under `/workspace/work/save-integration-check/`.

This establishes a .NET 8 Linux filesystem regression and source compatibility checks. It does not establish a Unity 6 build, Unity JSON execution, Mono/IL2CPP filesystem behavior, application shutdown behavior, target-platform atomicity/power-loss durability or Steam Deck frame time. No engine case or gameplay outcome is reported as passed.

## Agent review

Independent Codex support agents reviewed the repository's Unity-reviewer, game-designer and Deck-performance checklists. They were not Claude/Fable sessions and do not replace Fable's review.

- Unity/lifecycle: review caught that the initial version probe could abort `Load` before trying a healthy other slot. Fixed by isolating probe errors and retaining fallback, with a reload-required latch when recovery cannot load data. Re-review found no remaining concrete blocker in sequential operation. Three engine drafts cover these read-error cases.
- Design: this fulfills the existing foundation promises without mechanics, tuning, assets or Phase 2 content. Player-facing communication of a protected session remains for Fable (below).
- Deck: no new per-frame work. An unprotected checkpoint performs up to two full-file probes and one old-main validation, plus existing JSON/write/flush work. Protected repeated saves short-circuit. Storage latency and allocations remain unmeasured; the existing small save size is not a future file-size cap.

## Fable handoff and acceptance

Acceptance: both newer committed slots and any pre-existing temporary file remain byte-for-byte unchanged through repeated blocked saves; normal compatible saves/migrations still work; recovery preserves the good backup; readable fallback still loads during an inspection failure; no usable load means writes remain blocked until reload. All checks on actual Unity/Deck remain unverified.

Presentation question for Fable: how should the menu communicate that this session will not persist? Current UI displays in-memory seed/counter data and has no save-status notice. The backend exposes `WriteProtectedByNewerSave` and logs the reason, but a Console warning is not a visible player notice. Fable should choose the text/presentation, including other read/write failures, before shipping. No UI design or asset change is included here.

For local Claude/Fable with the Unity Editor:

1. Open `unity/` through Unity Hub. Run `tools/check-in-unity.ps1` on Windows, or `bash tools/check-in-unity.sh` on macOS/Linux, using `docs/CLAUDE_CODE_LOCAL.md`. Report actual EditMode/PlayMode results, especially `SaveProtectionTests` and `FoundationTests`.
2. Run tests only with throwaway save folders. Confirm the future-main/current-backup and current-main/future-backup cases preserve main, backup and temporary bytes after repeated saves, including calls before load and a future slot appearing after load. Check a future header with changed/unknown payload fields.
3. Confirm directory-obstacle/read-error cases load the readable slot, block writes while inspection fails, and require reload if no data could be loaded. Remove the obstacle and check that previously unread progress is loaded before saving resumes.
4. In a disposable profile, exercise real run start, return to menu and application quit. Check last seed, counters and migration after relaunch. Exercise failed promotion/retry on each supported target runtime; plain .NET 8 on Linux does not establish Unity Mono/IL2CPP or Windows behavior.
5. On Deck, profile a Linux Development Build with Deep Profile off. Record checkpoint-frame CPU time, GC Alloc and storage stalls on internal storage and microSD, including first-write and recovered-write cases. Compare observations with the existing 16.7 ms target and 25 ms hitch floor; no performance result is claimed here.
