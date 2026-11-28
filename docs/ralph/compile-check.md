# Runtime compile check (cloud)

The cloud has no Unity, so the runtime scripts are compiled against reference assemblies. How to run it, and what it
can and cannot catch, is in `tools/runtime-compile/README.md`:

```
tools/runtime-compile/sync.sh
dotnet build tools/runtime-compile -nologo -v q
```

The only errors expected are the two `excludeLayers` lines in `PlayerBurstFx`, which needs a newer Unity.
