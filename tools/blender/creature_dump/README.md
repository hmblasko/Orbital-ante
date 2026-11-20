# creature_dump

Runs a creature's own animation rules (the same plain C# the game uses: `WalkerBody`, `WalkerAnimator`, `WalkerBrain`) outside
Unity and writes every frame as piece matrices, so Blender can draw the movement with the real model. It is how a
walk, a strike or a limb loss is looked at in a cloud session, where there is no Unity.

```
cd tools/blender/creature_dump
dotnet build -c Release
dotnet bin/Release/net8.0/creature_dump.dll walk.json walk Strider     # or: strike, lose; or: Sprinter
blender -b -P ../build_creatures.py -- --write-to <trial folder> --reuse --poses walk.json --preview <pictures folder>
ffmpeg -framerate 15 -i <pictures folder>/strider_walk_%04d.png -pix_fmt yuv420p walk.mp4
```

`--reuse` uses the pictures already baked into the trial folder (run the build once without it first). Scenes:
`walk` (a walk with a turn; the Sprinter dashes, stops, turns and dashes), `strike` (both claws, at someone standing
in reach; the Sprinter's lunge and bite, twice), `lose` (six shots that take legs and a claw off until it goes down;
the Sprinter loses a jaw as well). Frames are 15 a second. The loose pieces' flight in `lose` is worked out here
in a simple way, only for the picture; in the game they are physics objects.

These pictures are Blender's, not Unity screenshots: they show the model and the movement, not the game's lighting.

For a still picture, copy a scene's file and add `"only": <frame>` with its own `"eye"`, `"target"`, `"lens"`,
`"size"` and `"samples"`: one frame is drawn, as large and as clean as asked.

## The Cave hunter

`dotnet run -- out.json <walk|strike|lose> Hunter` runs the Cave hunter's rules (`HunterDump.cs`): `walk` is a run with a turn each way (the body following its head), `strike` its own brain against someone standing still (the weave in, the rear, the strike, its head lying still, the loop away), `lose` shoots three legs off, cuts it in two twice and then bursts it. Every frame carries its own camera. A loose segment carries its legs with it.

## The Roller

`dotnet run -- out.json <walk|roll|lose> Roller` runs the Roller's rules (`RollerDump.cs`): `roll` is its own brain against someone standing still (the curl, the roll, on its back, getting up), `lose` shoots plates, legs and the tail off and then bursts it. Every frame carries its own camera, which follows the animal. Segments and plates are written with their widening in the matrix.

`dotnet run -- out.json <strike|lose> Screamer` runs the Screamer's rules (`ScreamerDump.cs`): `strike` is its own brain against someone standing still (the flight in, the landing, the warning, the scream, sagging afterwards, flying on), `lose` shoots a drum off in the warning, two wings in the air (it falls), two legs and another wing, then bursts it. Every frame carries its own camera. The rings of the scream are the game's own effect and are not drawn.
