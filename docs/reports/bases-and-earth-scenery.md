# Bases on the planets, and Earth's trees, mountains and landscapes

14 Nov 2026. Asked for by Matthew: "i want interesting bases spread throughout the planets. also add more geographic landscapes and more trees and mountains on earth. make sure they look really aesthetically pleasing and are done right."

**Nothing here has been run in Unity.** Everything was built and checked in the cloud, where there is no Unity Editor. The pictures are offline renders from Blender, not the game's own shader, so colours and light will differ in the game.

## What changed

Pictures (offline renders): `docs/art/previews/bases_overview.jpg`, one per base in `docs/art/previews/base_*.jpg`, and Earth's scenery in `docs/art/previews/earth_scenery_hillside.jpg`.

### Five bases (new)

All five are made in Blender by `tools/blender/build_bases.py` and stand on the foundation the game already builds. None carries a name, flag or marking: whose base it is belongs to the game, not the model.

| Base | Size | What you see |
|---|---|---|
| Relay outpost | small (10 m floor) | A living pod on cradle legs with a lit door and round windows, a 13 m lattice mast with a dish and a red light, two solar panels, crates and barrels |
| Fuel depot | small | Three round tanks on legs joined by yellow pipework, a pump house, a lookout tower |
| Dome colony | medium (18 m floor) | Three ribbed pressure domes joined by crawlways, one a glowing greenhouse with plants inside, a landing circle with ground lights, water tanks, three solar panels, a mast |
| Mining works | medium | A 13 m winding tower over a shaft, an ore belt climbing to a bin on legs, a workshop, heaps of dug rock, an ore cart on rails, a lookout tower |
| Stronghold | large (30 m floor) | An eight-sided wall with a gate and a second gap, four lookout towers, a 15 m command tower with a lit control room and radar, two bunk pods, a hangar, fuel tanks, a landing circle |

Each base is turned to face one of four ways depending on its seed, so two of the same design do not look alike from where you land.

Where the bases go, how many there are and who guards them is the core game's work (`PlanetBases`, `BaseField`). This work supplies the buildings and tells the game where guards can stand: open ground ("Spawn" points), tower decks ("Guard" points), and where loot will go later.

### Earth scenery (new)

Twenty kinds of tree, plant and rock, four shapes of each, made in Blender by `tools/blender/build_scenery.py`:

- Trees: conifer, broadleaf, palm, birch, maple (autumn colours), blossom, pine, acacia, giant tree (22 to 32 m), dead snag, snow-laden conifer.
- Plants: bush, cactus, flower meadow, reeds, fern, fallen log.
- Rock: crag, snow crag (18 to 34 m, the skyline of a range), outcrop.

They grow in patches, not evenly: woods have edges and clearings, and each kind keeps to its own band of the globe (palms and acacias near the equator, snow forest near the ice).

### Earth's shape

- Mountains are taller and cover more of the land (mountain height 72 m, up from the old value; peaks 58 m on top of that), with more ranges, massifs and lone peaks.
- 14 new natural formations in the index (162 in all): birch grove, autumn wood, blossom orchard, pine stand, savanna, grove of giants, snow forest, ghost wood, flower meadow, reed bed, fern glade, crags, snow peaks, rock ledges.

## Why

Matthew asked for it. Constitution principles served: **4, discovery that matters** (a silhouette on the horizon is now a reason to land), **8, varied intensity** (a quiet meadow and a walled stronghold are different kinds of place), and **1, fun core gameplay first** (bases give the fighting somewhere to happen: cover, towers, a yard).

## What was tested, and how

- **Rule tests, offline with `dotnet test`: 1,896 pass, 0 fail, 5 skipped.** New tests check every base model has its pieces and points, stays inside its floor and its triangle cap, leaves its middle open, that no guard point is inside a wall or on a landing circle; and that every scenery kind has a model of the right size with something solid where there should be.
- **Compile check** against reference assemblies: clean apart from the known Unity 6 name differences.
- **By eye**, in offline Blender renders (in `docs/art/previews/`). Inside-out faces show up bright pink in those renders; there are none.

## What is still unverified

- How any of it looks in the game's own light and shader.
- Frame rate on the Deck with a base and a wood in view (numbers in `docs/performance_budget.md` are estimates).
- Whether guards path sensibly round the buildings.
- Whether the first sight of a base causes a hitch as its model is read.

## Known limits (said plainly)

- **No interiors.** Doors are painted on and do not open. Buildings are solid shapes.
- **The same five designs on every world, in the same colours.** A Mars base and a Moon base look alike for now.
- Ladders are not climbable; lookout towers are for guards.
- Loot points exist in the models but nothing uses them until Phase 2.

## Triangles and cost

| Base | Triangles drawn | Solid triangles | Draw calls |
|---|---|---|---|
| Relay outpost | 6,498 | 216 | 5 |
| Fuel depot | 8,050 | 304 | 5 |
| Dome colony | 12,926 | 636 | 5 |
| Mining works | 15,098 | 680 | 5 |
| Stronghold | 32,990 | 1,052 | 5 |

Scenery caps: a tree 1,700 triangles, a plant 620, a rock 1,400.

## Agent review

Three of the project's agents read the code before it was committed. None of them ran anything in Unity.

**unity-reviewer** found nothing certain to break. It confirmed the bases hook in at the right moment (after the game clears its statics, with domain reload on or off), that a restart from the pause menu keeps them, and that nothing shared is destroyed when a base is put away. Acted on: scenery lookups are now made once per kind and kept (they were making garbage for every tree put out); a tree whose model does not say where its trunk is now gets a plain trunk and a warning, not nothing; base buildings no longer ask to cast shadows. Left for Unity: walking into a base and a crag from every side.

**deck-performance** measured the files outside Unity. Acted on: all models are now read while a run is loading, not on the frame the first tree or base comes into view (it guessed 20 to 60 ms on a Deck for the trees); thin bars in the bases lost their rounded edges, which took the stronghold from 39,774 triangles to 32,990. Not acted on, and written into the budget as the first things to try if a wood is slow: drawing small plants only when close, and a low-detail version of each tree. Its warning stands: 500 trees at once would be over the triangle budget, so the tree counts need reading off on the Deck.

**game-designer** raised five points. Acted on: guards no longer stand on landing circles (a rule test now checks it); the stronghold changed from red to a plain blue so no colour hints at a faction. Not acted on, for Matthew to decide: every world gets the same five designs in the same colours, which falls short of "each world is recognisable at a glance"; doors look like they open and do not; and nothing can be called finished on looks until it has been seen in the game.

## How to look at it on the Deck

1. Open **Konsole**.
2. Type `cd ~/Orbital-ante` and press Enter.
3. Type `git fetch` and press Enter.
4. Type `git checkout claude/playtest` and press Enter, then `git pull` and press Enter. (This work is already merged into that branch.)
5. Open the project in Unity Hub and wait for it to finish importing.
6. In the top menu choose **Orbital Ante > Set Up Project** (this picks up Earth's new numbers).
7. Press **Play**. You start on the launch pad on Earth: look round for trees and mountains before you take off.
8. Fly low over Earth and look for buildings on a grey round platform. Press **F3** and tell me the `things` number and whether the Budget line says OK, TIGHT or OVER near a base and inside a wood.

Things worth telling me: does any base look cheap or toy-like in the game's light, are trees too thick or too thin, are the mountains tall enough, and did anything fall through or get stuck.
