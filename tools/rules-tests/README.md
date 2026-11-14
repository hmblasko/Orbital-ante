# Rules tests without Unity

Runs the game's plain C# rules and the EditMode rule tests with `dotnet test`, so they can be checked anywhere (a cloud session, CI, any Linux or Windows machine) with no Unity install.

```
dotnet test tools/rules-tests
```

Needs the .NET 8 SDK (`apt-get install dotnet-sdk-8.0` on Ubuntu) and access to nuget.org the first time, for NUnit.

## What it compiles

Every `.cs` file under `unity/Assets/OrbitalAnte/Scripts/Runtime/` and `unity/Assets/OrbitalAnte/Tests/EditMode/`, except files that use Unity. A file counts as Unity-side if a line outside a comment has a `using Unity...` directive, `UnityEngine.`, `UnityEditor.`, `MonoBehaviour` or `ScriptableObject`. The build prints how many files it took and skipped:

```
rules-tests: compiling 85 Unity-free files, skipping 90 Unity-side files
```

There is no file list to keep up to date: new rule files and tests on any branch are picked up on their own. That relies on the existing rule that game rules live in plain C# with no Unity types. If a rule file starts to use a Unity type, it drops out, and any test that needs it fails to compile, which is the signal to move the Unity part out. If a file has no Unity references but still needs a Unity-side type, exclude it in `RulesTests.csproj` with `<OAExclude Include="$(OARoot)path/File.cs" />`, and say why.

Model-file tests find `Resources/Models` by walking up from the test folder (or from `OA_MODELS` if set). The audio tests find `tools/audio` the same way and are ignored if it is missing.

## What it does not cover

PlayMode tests, MonoBehaviours, shaders, scenes, and anything about look, feel or frame time. Those still need Unity on Matthew's machine. It also uses the .NET 8 runtime, not Unity's Mono, so a test that passes here can in rare cases behave differently in the editor (floating point formatting, for example).

Checked 14 Nov 2026: `main` gives 763 passed, and `claude/milestone-10-navigation` gives 1,058 passed (the same total as that branch's report, which listed 5 audio tests as skipped because it ran outside the repo).
