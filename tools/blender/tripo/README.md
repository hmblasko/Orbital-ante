# Bringing a Tripo body into the game

How Matthew's Tripo soldier (20 Nov 2026) became the game's trooper (`Enemy.bytes`, `EnemyFar.bytes`,
`Textures/Enemy_*.png`). The source GLB is not in the repository (about 2 million triangles with 4K pictures);
it is `common soldier 3d model_Clone1.glb` in the project's files.

Run in order with Blender 4.2's Python. The scripts keep the paths of the cloud work folder they were written
in (`/tmp/claude-0/tripo/...`); change those at the top of each before running anywhere else.

1. `tripo_prep.py -- <model.glb> <work>/soldier.blend`: imports, welds the seams, keeps a 300,000 triangle
   working copy with Tripo's own pictures.
2. `tripo_badges.py`: prints where strongly coloured patches sit, so flags and badges can be painted out
   (the positions go in `BADGES` in `tripo_fit.py`).
3. `tripo_cut.py`: turns it to face +Y at game size (`S`), thins it (`LOW_TRIANGLES`), and cuts it into the 14
   pieces at the joints listed in `J` (read off straight-on pictures; these are per model). Draws every piece in
   its own colour to check.
4. `tripo_fit.py -- <out folder> <pictures folder> [--draft]`: closes each cut with a dome in the suit's colour,
   lays the pieces out, bakes Tripo's colour, bumps and roughness/metal onto them (2048), paints the corner
   colours from the bake, turns every piece onto the game's skeleton (`build_enemy.ORIGINS`), stretches it along
   its bone, writes the model, the far copy and pictures.

Copy the out folder's files into `Resources/Models` and run `dotnet test tools/rules-tests`.

What is per model and must be done again for the next body: `S`, the joint table, the badge positions, and the
fitting numbers in `rest_matrix` (leg thinning, the torso press). Not seen in Unity yet.
