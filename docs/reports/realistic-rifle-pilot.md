# Realistic rifle pilot (19 Nov 2026)

Matthew, 18 Nov 2026: recreate the enemies and weapons "substantially more realistic and not obviously made in blender like the cheap assets they currently look like". The rifle is the pilot: it proves the texture route end to end before any other gun or body is rebuilt. **Nothing in the game has been replaced.** The game's `Rifle.bytes` is unchanged until Matthew has seen the pictures and said the level is right.

## What changed
- `tools/blender/oa_materials.py` (new, shared with every realistic build, the alien creatures included): materials worked out from the shape (coated gun steel rubbed through on some edges, bright working steel, laminated wood under shellac, moulded resin with a pressed grip pattern, hollows), laying shapes out on one picture, baking a detailed model onto the game's model (colour, bumps, mask), and pictures of the result.
- `tools/blender/build_guns_realistic.py` (new): each gun is described once and built twice, a low model for the game and a high one that is only baked. The rifle starts from a real thing, a stamped-steel Kalashnikov-pattern rifle with laminated furniture and a ribbed steel magazine: folded receiver with rivets, pins and the dimple over the magazine, ribbed lid, bolt carrier and charging handle in the port, selector lever, leaf rear sight, gas block, eared front sight, slant-cut muzzle, cleaning rod, sling loops, butt plate. No markings.
- `tools/blender/oa_blender.py`: runs on Blender 4.0 to 4.2 (`auto_smooth`), and two fixes to `save_texture_set` found on the first real bake (a colour-space change and a copy each threw the freshly baked picture away, leaving black files).
- Pictures: `docs/art/previews/rifle_before_after_side.jpg`, `rifle_before_after_held.jpg`, `rifle_after_close.jpg`, `rifle_after_left.jpg`, `rifle_after_far_copy.jpg`.

## The trial files (not in the repository)
Written with `--write-to`: `Rifle.bytes` (OAM3, 3,970 triangles, 3,841 corners; the old rifle is about 7,800 triangles), `RifleFar.bytes` (1,584 triangles, 0.00% seen from inside), `Rifle_Color.png` and `Rifle_Normal.png` at 2048, `Rifle_Mask.png` at 1024. Same sockets, same length (1.04 m), same grip point as the old rifle.

## Why, and which principle
Constitution 1 (fun core first: the gun fills a third of the screen all game) and Part B section 3 as amended by Matthew on 14 and 18 Nov (realistic, not cheap).

## What was tested
- The build runs headless in Blender 4.2 (about 20 minutes on 4 cores, nearly all of it the bake).
- A stand-alone checker repeated the gun rule tests on the trial files (length, muzzle and magazine sockets, both pieces, 9,000 triangle cap, far copy under 2,200 triangles, 0.45 of the triangles and 0.62 of the corners, every corner on the pictures, unit picture directions): all pass.
- Looked at from the side, the left, close up, the held angle, and the far copy with its pictures on (no smearing across seams seen).

## Not verified
- **Nothing has been run in Unity.** The pictures are Blender's renderer wearing the baked pictures, not the game's shader. How the game's `OA_Model` textured path shows it is unknown until it is on the Deck.
- `dotnet test` could not be run in this session (no .NET); the checker is my own copy of the rules, not the real tests.
- The enemy's and player's hand poses against the slimmer grip and handguard have not been looked at.
- My own judgement of the pictures: a clear step from toy to real object, but close up the steel is still plain and the wood grain is quiet. Sharper machining marks and stronger grain are the next things to add if Matthew wants more.

## To put it in the game (after Matthew's yes)
`blender -b -P tools/blender/build_guns_realistic.py -- --game --only Rifle`, then the rule tests, then the Deck check.
