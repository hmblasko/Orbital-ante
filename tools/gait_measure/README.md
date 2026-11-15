# Gait measurements

Compares how the enemies walk and run (`BodyAnimator`) with real people, on the same definitions. Nothing here ships and no motion data is kept in the repository.

1. Get some CMU Motion Capture clips as BVH (free for research and commercial use; see `docs/asset_licence_ledger.md`), for example from the B. Hahne conversion mirrored at `https://raw.githubusercontent.com/meshula/cmu-mocap/master/data/<subject>/<clip>.bvh`: walks `002/02_01`, `007/07_01`, `008/08_01`, `016/16_15`; runs `009/09_01`, `035/35_17`. Keep them outside the repository.
2. Write the game's walk to JSON at 60 frames a second: the motion dump (`tools/blender/motion_dump`, see its README for building it): `dotnet run -c Release -- walk.json walk 2.0 dense` (the number is metres a second).
3. `python3 -I tools/gait_measure/gait.py 08_01.bvh walk.json --curves`

It prints, averaged over whole strides: strides a second, speed and stride in leg lengths, pelvis rise and fall, sway, turn and tilt, shoulder turn against the pelvis, trunk lean, hip and knee bend, ankle height, foot pitch, shoulder swing and elbow bend; `--curves` adds each one through the stride (20 points from the left heel strike). Angles are in degrees, lengths in leg lengths (hip joint to ankle). Compare shapes and ranges, not absolute heights: the joints are not placed identically in the two skeletons.

What was found and changed on 15 Nov 2026 is in `docs/reports/enemy-animation.md`.
