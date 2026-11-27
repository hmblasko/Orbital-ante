# Sinking into the ground and bouncing (20 Nov 2026)

Matthew, 20 Nov 2026: "i keep no clipping through the ground, sometimes in the ground sometimes half way and it does this bounce, that needs to be fundamental fixed in the game, its on every planet."

Nothing here has been run in Unity or on the Deck.

## What was wrong

The game has a safety net (`PlanetWorld.GuardAgainstSinking`) that puts the suit, the ship and the rover back on top if they get below the ground. It measured "the ground" with `PlanetTerrain.Height`: the ideal ground, with every fine bump in it. No tile ever draws that ground. A tile's mesh, which is also its collider, leaves out bumps smaller than about two of its squares and is made of flat triangles, which cut straight across a crag edge, a ridge crest, a tower's cliff band or a dune.

Measured offline over every world (3,000 places each, finest tiles): the two disagree by more than 0.15 m at 0.5 to 7% of places and by more than 0.7 m at up to 2.4%, worst 3 m on Earth, 5 m inside Jupiter and 12.6 m on Venus. The rougher the world, the worse. Coarser tiles, which you stand on until the finest ones arrive, are further off still. So:

- **The bounce.** Where the ideal ground stands above the real one, someone standing honestly on the real ground looked sunk, was lifted, fell back and was lifted again.
- **Half way in.** Where the real ground stands above the ideal one, someone pushed under the collider (a collider only holds from above) looked fine to the safety net and was left there, walking inside the ground. The net only woke when they had sunk knee deep (0.7 m) below the *ideal* ground, and then put them back on the ideal ground, which could still be under the real one, so they fell in again: half way, then bounce, then half way.
- **Tile swaps.** When the tile under you is swapped for a finer one, you are put back on top if the new surface is above your boots. That too was measured on the ideal ground, so it could leave you under the new surface.

None of this is tied to one world or one landform, which is why it happened everywhere. The new landforms (canyons, ridges, towers, dunes) made it more common.

## What changed

- `PlanetSurface.GroundRadius` is now the ground really built: the triangle of the tile showing in that direction, worked out the way `TerrainMesher.Build` makes it (same corners, same left-out bumps, same diagonal). It is exact to the mesh (rule test, to 2 mm). `BuiltGround` remembers the triangles asked about lately, so asking again where someone stands costs a few sums, cheaper than the old single ground sample. The ideal ground is still there as `IdealGroundRadius` for deciding where things go.
- Everything that asked `GroundRadius` now agrees with the collider: the safety net, the height readout, enemies' feet (`PlanetFooting`, `CaveFooting`) and the height enemies are placed at (`EnemySpawner`).
- The safety net uses each thing's own "sunk" depth everywhere. The old 2.5 m allowance for coarse ground is gone: it was there only because the old measurement could be metres off.
- The suit counts as sunk a quarter of a metre deep (was 0.7 m, knee deep) and is put back standing just on the ground (0.2 m up, was 0.25 m over the ideal ground). The boots are the lowest point of the suit's capsule and never honestly below the ground, even on a slope.
- The ground's collider no longer has the skirts in it. A skirt is the curtain hanging from a tile's edge that hides hairline gaps between tiles of different detail; in the collider it was an upright wall standing a little proud at those seams, which the suit could catch on and be shoved by. Tiles keep their skirts for drawing.

## Why, and which principles

Fun core gameplay first (1) and learnable challenge (3): walking on a world must just work; falling into it is a broken rule the player cannot learn around.

## Tested

- Rule tests in the cloud: 2,554 pass, 4 of them new (`BuiltGroundTests`): the built ground is exactly the tile's triangles at the finest and coarser detail on eight worlds; the ideal height really is more than 0.7 m off it on rough worlds, both ways; asking again is remembered and gives the same answer; the collider takes the ground triangles only.
- Compile check of the game's scripts against reference assemblies: no new errors.

## Not verified (needs Unity / the Deck)

- That the bounce and the half-way sinking are gone in play. Please watch the F3 readout's "rescues" count while walking: it should stay at 0 or close to it.
- Enemies now stand on the drawn ground, so they should no longer float or sink a little on rough ground. Not seen.
- Rocks and other scattered things are still placed on the ideal ground, so a few may still float or sink slightly on rough spots. Not changed here.

## Agent review

One reviewer read the change as the Unity reviewer and the Deck performance agent. Nothing had to be fixed before commit: the triangle choice matches the mesh, no caller of `GroundRadius` breaks (enemies' feet raycast the colliders first and only fall back to it), nothing makes garbage, and the remembered triangles make the common case cheaper than before. Acted on: the skirts were in the collider (now left out), and the suit's sunk depth of 0.3 m equalled the capsule's radius, where physics may push either way (now 0.25 m). Not acted on, low risk: base floors are placed on the ideal ground, so rarely the real ground may stand within a few centimetres of a floor; things nobody rides that have no clean-up of their own can still be put back on ground that is drawn but not solid (unchanged from before). To watch in Unity: rescues while walking over tile seams, walking into a cave mouth, and the cost during a big fight.

