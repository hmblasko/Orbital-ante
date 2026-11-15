# Motion dump

A small console program that runs the enemy's animation rules (`BodyAnimator`) through short scenes outside Unity (a disarm and scramble, a leg shot off, an arm shot off, a plain walk or run at a given speed), moving the body the way `MannequinLocomotion` and `MannequinCombat` would, and writes every frame to JSON:

    dotnet run -c Release -- disarm.json disarm      (or: hobble, arm, or walk 2.0 for a walk at 2 m/s)
    add "dense" to write all 60 frames a second (for tools/gait_measure), "old" for the older animator
    blender -b -P tools/blender/preview_enemy_motion.py -- disarm.json <folder> --eye 2.0,1.3,-3.2 --lens 50 --follow --size 360x440
    ffmpeg -framerate 20 -i <folder>/f%03d.png ... (or any tool) to make a GIF

Build it like `pose_dump`: a project that compiles this file with `Core/Vec3d.cs`, `Dismemberment/BodyAnimator.cs`, `BodySkeleton.cs` and `BodyLayout.cs`. For before-and-after pictures a second file can set `MakeOld` to an older copy of the animator and the scene is run with a third argument, `old`.

The scenes stand in for the game's movement code (flat ground, no physics, the gun is a box; a shot-off part flies on a simple thrown path in the preview), so the pictures are previews of the animation rules, not game footage.
