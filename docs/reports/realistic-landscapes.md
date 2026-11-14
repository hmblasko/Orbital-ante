# More realistic landscapes, and much more geography

14 Nov 2026. Asked for by Matthew: "I don't want everything so cartoony goofy and blocky for art style. I want more realistic looking. I also want landscape to look much more realistic and have more interesting geography and much more geography".

This report covers the land itself, on every world. The models (ship, guns, enemies, bases, trees) and the art direction document belong to the look thread, which is making the same shift.

**Nothing here has been run in Unity.** It was built and checked in the cloud, where there is no Unity Editor. The pictures are drawn by a small program from the game's own ground rules; they are not Unity screenshots, and the game's light, sky, bloom, trees, rocks and bases are not in them.

## Pictures (before on the left, after on the right)

In `docs/art/previews/`: `ground_overview.jpg`, and one each for `ground_earth_on_foot`, `ground_earth_mountains`, `ground_earth_from_the_air`, `ground_earth_from_space`, `ground_mars_on_foot`, `ground_mars_from_the_air`, `ground_mars_from_space`, `ground_moon_on_foot`.

## What changed

### The shape of the land

- **Hills and mountains look worn, not lumpy.** Before, every slope carried the same even crumple of bumps. Now the fine bumps are held back where the land is steep, the way running water (or ages of falling dust) leaves it: flanks run clean down to smooth valley floors, and crests keep their ribs and spurs. Mountains are the same height as before.
- **Winding valleys** cut into the high ground on Earth, Mars, Venus and Titan, with side valleys joining them. They are deepest in the hills and die away toward the lowlands.
- **Stepped country** on Earth, Mars and inside Jupiter: in some regions the land goes down in flat benches with low cliffs between, like layered rock. The benches are level places to land on with something to look at.
- **Crags** in mountains and rough country: broken rock a few metres high.
- **Calmer plains, rougher highlands.** The difference between quiet country and broken country is wider than it was. Nowhere is dead flat.
- **More landforms everywhere.** Each world gets half as many again of every seeded landform (craters, volcanoes, faults, mesas, dunes, ice forms, caves), a fifth more plain craters, and two of anything it had only one of. Landmarks stay where they were. Seas, lakes, weather and woods are unchanged.

### The colour of the land

- **Natural, greyed colours.** Earth's grass is a duller, truer green; Mars is butterscotch-brown instead of bright orange; Io and Titan are toned down. Nothing but snow and ice is brighter than pale sand.
- **Colour follows the shape.** Soil and plants on gentle ground, loose stone where it steepens, bare rock on the faces. Dips are darker, crests paler. The snow line is ragged: it lies in hollows and blows off ridges.
- **Rock faces show beds of unequal thickness** that tilt and wander. The old evenly ruled stripes are gone (they were the most cartoon-like thing in the old ground).
- **Broad patches of slightly different tone**, so no hillside is one flat paint.

### Close to you

- The ground shader now draws **grit, stones and small hummocks** within about 150 m, and the light catches them. This replaces the painted dabs on the ground only; rocks, trees, buildings and the ship keep the paint for now (the look thread owns those).
- A new dial, **Ground Relief** on the Planets asset, turns it down or off.

## Why

Matthew asked for it. Constitution principles served: **4, discovery that matters** (valleys, benches and crags give a reason to pick one landing place over another), **5, legible risk and reward** (steep ground now reads as rock and cliffs at a glance, level ground as soil), and **1, fun core gameplay first** (benches and valley floors are places to land, drive and fight).

## What was tested, and how

- **Rule tests, offline with `dotnet test`: 1,970 pass, 0 fail, 5 skipped.** Twelve new tests: the new noise gives exact values and slopes; worn ground keeps less fine detail on steep slopes than on gentle ones; every world's ground stays between its stated lowest and highest; valleys only cut down and no deeper than their dial; stepping never moves the ground more than one step; Earth, Mars, the Moon, Venus and Titan keep at least 22% of their land level enough to land on and under 30% cliff; ground colours are never garish; hollows are darker and crests paler; rock beds are of unequal thickness; landmarks, seas and woods are not multiplied; the same seed gives the same ground twice.
- **Compile check** against reference assemblies: clean apart from the known Unity 6 name differences.
- **The ground shader's own syntax** with `glslangValidator` (URP's own include is stubbed, so this does not prove it compiles in Unity).
- **By eye**, in the offline pictures.
- **Bases still find places** (counted over eight seeds, old against new): Earth 15 to 18 of 24 (was 15 to 18), Mars 15 to 17 of 17 (was 16 to 17), Titan 4 to 5 of 5 (was 3 to 5). Neptune, an ocean with a few islands, finds 0 or 1 (was 0 to 2).
- **Cost**, measured outside Unity (see `docs/performance_budget.md`): one question of the ground's height costs about a quarter more on Earth and 40% more on Mars. That work is done on background threads, never on the frame.

## What is still unverified

- **Whether the ground shader compiles and how it looks in Unity.** If the ground shows bright pink or the Console names `OA_Terrain`, that is this change. Setting Ground Relief to 0 will not help in that case; tell me and I will fix it.
- How the fine relief looks in motion: it could crawl or shimmer on far ground under a low Sun. It is faded out with distance and with how much ground one dot covers, but that needs eyes on a Deck.
- Whether colour changes visibly where near ground meets far ground, or as ground sharpens when you approach.
- Frame rate on the Deck, and whether ground keeps up when flying low and fast.
- How long the far-away pictures of each world take to arrive at the start of a run.
- Whether valleys and benches are fun to land on and drive over. No playtest has happened.

## Known limits (said plainly)

- The worlds are small (Earth is 500 m in radius), so a mountain range is tens of metres high and the horizon on foot is about 50 m away. Realism here means believable shapes and colours at that scale, not real-world size.
- There are no textures. Everything is worked out from numbers, so the ground will not look like a photograph up close; trees, rocks and grass still do much of that work.
- Valleys are dry: no rivers run in them yet.
- Rocks, trees and base buildings have not changed colour in this work. Next to the greyer ground they may look too bright until the look thread's pass reaches them.
- The pictures of Earth and Mars from space show ground only: the game adds air, sea shine and cloud.

## Agent review

Three of the project's agents read the change before it was committed. None of them ran anything in Unity.

**unity-reviewer** found five likely problems, all fixed: the surface tilt in the shader could give wild answers on edge-on ground and under tile edges (now limited); ground paid for three noise lookups at every distance (the two finer ones are now only worked out up close, as the paint's were); the relief could shimmer far away at low angles (now also faded by how much ground a dot covers); dips were shaded differently on coarse and fine tiles, which would show as seams (now measured the same way at every level); side valleys switched on all at once as ground sharpened (now fade in). It found nothing that should fail to compile, and confirmed rocks, trees and buildings keep their old path.

**deck-performance** timed the old and new rules side by side. Acted on: the shader lookups, as above; slopes in the far-away pictures no longer pay for bumps too small to show (the pictures had gone from 13.2 to 16.7 seconds of one core on a desktop). Its estimates are in the budget document: ground pieces still arrive faster than the game hands them to the graphics card, and the per-frame cost on the main thread rises by under 0.1 ms. It also corrected the budget document: 15 far-away pictures are painted, not 19. Not acted on: moving the search for base sites off the main thread (a guessed 45 to 90 ms once per world on a Deck, unchanged by this work).

**game-designer** raised seven points. Acted on: calmer plains were too flat on the small ice moons (Europa, Enceladus and Triton now keep more of their hills); Mars's steps were taller than the lander's legs over too much of the planet (now 4 m over less of it); river-like valleys did not belong on airless Titania (removed) and were too slot-like on Venus (now broad and shallow); Ganymede's ground was nearly Mercury's (now cooler); the extra landforms were mostly more plain craters (now a fifth more craters, half as many again of the rest, two of each rare form). For Matthew to decide: Mars, Venus and Titan all have brown-tan ground now and rely on their skies to tell them apart, which fits the realism rule but should be judged from orbit; and the ice, sulphur and gas-giant worlds keep stronger colours than the rule allows rocky worlds.

## How to look at it on the Deck

1. Open **Konsole**.
2. Type `cd ~/Orbital-ante` and press Enter.
3. Type `git fetch` and press Enter.
4. Type `git checkout claude/playtest` and press Enter, then `git pull` and press Enter.
5. Open the project in Unity Hub and wait for it to finish importing.
6. Look at the **Console** tab at the bottom. If any red line names `OA_Terrain`, stop and send it to me.
7. Press **Play**. You start on the launch pad on Earth. Look at the ground near your feet and at the hills.
8. Take off and fly low over Earth, then go to Mars and the Moon.
9. Press **F3** and tell me whether the Budget line says OK, TIGHT or OVER standing on Mars looking at the horizon.

To turn the fine relief down: in the Project window open **Assets > OrbitalAnte > Resources > Config**, click **Planets**, and in the Inspector drag **Ground Relief** (1 is full, 0 is off).

Things worth telling me: does the ground look real enough now or still too smooth, do any colours look wrong, does the ground flicker or crawl anywhere, and are the valleys and steps good or annoying to land on.
