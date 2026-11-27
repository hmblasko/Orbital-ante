# Runtime compile check (cloud)

Cloud sessions have no Unity, so this compiles the game's runtime scripts (`unity/Assets/OrbitalAnte/Scripts/Runtime`)
against reference assemblies. It catches compile errors in method bodies. It cannot catch anything that only shows at
run time in the engine.

```
tools/runtime-compile/sync.sh
dotnet build tools/runtime-compile -nologo -v q
```

- `sync.sh` copies the scripts into `src/` and maps the Unity 6 names back: `linearVelocity` to `velocity`,
  `linearDamping` to `drag`, `angularDamping` to `angularDrag`, `FindFirstObjectByType` and `FindAnyObjectByType` to
  `FindObjectOfType`, `PhysicsMaterial` to `PhysicMaterial`, `PhysicsMaterialCombine` to `PhysicMaterialCombine`.
- References: NuGet `UnityEngine.Modules` 2021.3.33 for the engine. From NuGet `LethalCompany.GameLibs.Steam`
  81.0.5-ngd.0, only `ref/netstandard2.1/Unity.InputSystem.dll` and `UnityEngine.UI.dll`, as explicit references. Never
  its Assembly-CSharp: that clashes with the game's `IHittable`, and Roslyn then skips method bodies, which gives false
  passes.
- The only errors expected are the two `excludeLayers` lines in `PlayerBurstFx`. That property is newer than 2021.3.
- .NET 8 SDK is needed (`apt-get install dotnet-sdk-8.0`). NuGet must be reachable for the first restore.
