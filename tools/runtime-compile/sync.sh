#!/bin/bash
# Copy the game's runtime scripts into src/ with the Unity 6 API names mapped back to the 2021.3 reference
# assemblies, so `dotnet build` can compile them in a cloud session that has no Unity. See README.md.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
SRC="$HERE/../../unity/Assets/OrbitalAnte/Scripts/Runtime"
rm -rf "$HERE/src" && mkdir -p "$HERE/src"
cd "$SRC"
find . -name '*.cs' | while read -r f; do
  mkdir -p "$HERE/src/$(dirname "$f")"
  sed -e 's/\blinearVelocity\b/velocity/g' -e 's/\blinearDamping\b/drag/g' -e 's/\bangularDamping\b/angularDrag/g' \
      -e 's/\bFindFirstObjectByType\b/FindObjectOfType/g' -e 's/\bFindAnyObjectByType\b/FindObjectOfType/g' \
      -e 's/\bPhysicsMaterialCombine\b/PhysicMaterialCombine/g' -e 's/\bPhysicsMaterial\b/PhysicMaterial/g' \
      "$f" > "$HERE/src/$f"
done
