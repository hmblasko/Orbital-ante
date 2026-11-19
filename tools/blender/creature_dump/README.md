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
