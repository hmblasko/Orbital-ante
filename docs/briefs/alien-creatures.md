# Alien creatures: the list and the build order

Matthew, 17 Nov 2026: alien assets "that look realistic (not ps2 style) using deep research of the most unusual bugs insects and animal creatures on the planet as inspiration", with "interesting animations", that "gib and lose limbs". He named nine animals. On 19 Nov 2026 he chose "your nine first, extras later": the spider crab as the pilot, shown to him before the other eight are built.

Names here are placeholders. Each creature is original work that starts from a real animal; nothing is copied from another game or film.

## The nine, in build order

| # | Starts from | Working name | Where | What it does | What comes off |
|---|---|---|---|---|---|
| 1 | Japanese spider crab | Strider | open ground | Walks up on eight stilt legs and throws a long claw | 10 limbs of 3 pieces each |
| 2 | Camel spider | Sprinter | deserts, shade | Dashes and stops on the way in, then lunges and bites | 8 legs and 2 feelers of 3 pieces each, 2 jaws |
| 3 | Giant isopod | Roller | anywhere | Armoured; curls into a ball, bowls at you, lands on its back | 7 plates, 14 legs, the tail fan |
| 4 | Giant centipede | Cave hunter | caves | Weaves in, rears and strikes down; shot through, the head end fights on shorter (hanging from the roof and dropping is not built yet) | 16 body segments (it is cut in two at any of 13), 32 legs |
| 5 | Cicada | Screamer | trees, cliffs | Its scream is the weapon | wings, legs |
| 6 | Bobbit worm | Ambusher | soft ground | Waits buried, strikes and drags | jaws, body lengths |
| 7 | Olm | Blind stalker | caves | Blind: finds you by movement and electronics | legs, tail, gills |
| 8 | Goliath tigerfish | Pack biter | water | Hunts in packs | fins, jaw, tail |
| 9 | Megamouth shark | Drifting maw | water | Slow, pulls you in | fins, jaw |

Built so far (20 Nov 2026): 1, 2, 3 and 4, in the combat arena only. 8 and 9 wait for water you can swim in. Eight more animals from the research (whip spider, velvet worm, mantis shrimp, goblin shark, anglerfish, giant sea spider, ribbed newt or hairy frog, harvestman swarm) wait until these nine are done.

The research behind the list is the report "Unusual animals for alien creatures" in the project's files.

## How each one is made

1. **A layout** in plain C# (`WalkerLayout` is the pattern; creatures on eight legs share it and the walker files): every joint, length and piece name. The Blender script carries a copy and a rule test holds the two together.
2. **A model** from `tools/blender/build_creatures.py`: described once, built twice (a low one for the game, a high one that is only baked onto it), textured at 2048, with a far copy. Every piece is modelled from its own joint so it can come off. Repeated limbs are modelled once. Materials for living things (`chitin`, `membrane`, `flesh`, `shell_inside`, `chalk`, `eye`) are in `oa_materials.py`; add new ones there.
3. **Body rules** in plain C#: what each piece's hit points are, what goes with it, what kills the animal, how losing pieces changes how it moves. Limbs stay low (one to three rifle hits), and nothing has hidden rules.
4. **An animator** in plain C# with no clips: the movement is worked out from where the animal is going, so it copes with any limb missing.
5. **A brain** in plain C#: what it does and its one attack, with a warning that can be seen and learned.
6. **One Unity class** that turns those into objects, hitboxes, loose pieces and wounds.
7. **Pictures and a clip** before it goes in: stills from the build, and the game's own animator drawn by `tools/blender/creature_dump`.

## What a creature must show before it goes in the game

- It reads as its animal at a glance and from 40 m, and is not mistaken for any other creature in the list.
- Every piece that can come off looks like a separate part (a joint, a seam), and the wound is on both sides of the break.
- The way to kill it can be seen (the Strider's soft underside against its armoured lid).
- Its attack has a tell of at least half a second.
- The far copy prints 0.00% seen from inside.
