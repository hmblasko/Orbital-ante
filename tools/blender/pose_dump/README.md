# Pose dump

A small console program that runs the enemy's animation rules (`BodyAnimator`) outside Unity and writes the poses to a JSON file, so Blender can render them:

    dotnet run -- poses.json sheet      (or: walk, run)
    blender -b -P tools/blender/build_enemy.py -- --poses poses.json --preview <folder>

It needs a project that references the game's runtime scripts compiled against Unity reference assemblies (see the "Verifying without the editor" note in CLAUDE.md). Only `Program.cs` is kept here.
