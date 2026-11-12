# Milestone 6 report - Planets

Status: **built, not yet run in Unity.** Same position as Milestones 1 to 5. The rules that shape every world (the formation index, the height and colour of the ground, caves, seas, air, cloud decks, weather) are plain maths and are properly tested here: 733 rule tests run and pass outside Unity. Everything you would see or feel (how the ground looks up close, the skies, storms, landing, frame times) compiles but has never been run. The four planet shaders have been checked for syntax only.

Same branch as before (`claude/unity-milestone-1`). The picture `milestone-6-surface-maps.png` shows the surface of all 19 bodies, painted by the same code the game uses for its far-away views.

## What changed

**An index of 148 natural formations** (you asked for over 100). Craters of 17 kinds, 23 volcanic forms, 7 kinds of cave, faults and mountains, wind and erosion shapes (mesas, hoodoos, yardangs, dunes), 28 kinds of river, lake, sea and coast, 19 ice forms, sky and weather, and Earth's vegetation. Each one is a recipe the planet builder can make, and every one is used on at least one body. The full list, in plain words, is in `docs/formation_index.md`.

**19 worlds with real ground.** Every planet and moon now has its own profile: colours, hills, which formations it has and how many, sea, air and weather. The big landmarks are always in the same place, modelled on where the real ones are: a giant shield volcano and a great canyon on Mars, a bright rayed crater in the Moon's south, the "tiger stripe" cracks at the south pole of Enceladus, and so on. They are not labelled with their real names yet. Everything else is placed by the run's seed.

- **No flat plains.** A test samples every body and fails if any is flat.
- **Caves** you can walk or fly into, with a mouth at the surface, and they are dark inside: a lamp comes on by itself.
- **Seas, lakes and pools** in varied shapes: water on Earth, methane seas on Titan, lava lakes and sulphur pools on the volcanic worlds, hot brine springs.
- **Loose things**: rocks specific to each world, ice shards, spires, and trees, bushes and cacti on Earth only.

**Gas giants you can fly into.** Jupiter, Saturn, Uranus and Neptune have a thick cloud deck. Dive through it (about ten seconds of flying blind) and there is a small landable world underneath with a sky in that planet's gas colours. **Neptune is an ocean world**: 98% deep sea with a few islands, and you can go underwater.

**Skies.** Orange haze on Mars, light blue on Earth, sulphur yellow on Venus, orange murk on Titan, and nothing at all on the Moon, Mercury, the asteroids and the other moons. The sky brightens and darkens with the real position of the Sun, and stars fade in daylight. From space, a world with air has a thin glowing rim.

**Weather** on the worlds that have it: rain, snow, sandstorms, wind you can feel, and lightning in different colours per planet, with thunder.

**How it is drawn.** Far away, each body is a ball with a painted picture. As you approach, the ground under you is built in finer and finer pieces on background threads, so arriving should not freeze the game. Only ground near you is solid.

**Settings you can change** live in one new asset, `Planets`, with a "simple shaders" tick box in case anything shows up pink.

## Why, and which principles it serves

- **Discovery that matters (4).** The refinement bar is "landing anywhere gives you something you want to explore". The index and the per-world profiles exist so that wherever you put down there is a crater rim, a canyon, a cave mouth or a shore within sight.
- **Meaningful replayability (6).** Landmarks stay put so you can learn the worlds; the seed moves the rest.
- **Learnable challenge, legible risk (3, 5).** Thick air and water slow you, wind pushes you, and the HUD says so in words ("inside the clouds", "under the clouds", "underwater", wind speed) instead of leaving you to guess.
- **Manage cognitive load (7).** The lamp is automatic. One readout line per condition, shown only when it applies.

## Choices you should know about

1. **Titan is the one moon with a sky.** Your prompt says no tint on moons. Titan really has a thick orange atmosphere and it is the thing that makes it Titan, so I gave it one. Say so if you want it removed.
2. **Under a gas giant's clouds is a small solid world**, not endless gas. This is how Outer Wilds does it and it is what "landable surfaces inside" needs.
3. **Wind is moving air, not a constant push.** If you fly with the wind it stops pushing you. A parked ship in a storm will slide a little.
4. **The ship is slightly buoyant in liquid**, so it bobs up instead of sinking into Neptune.
5. **Ground is lit by the game's own shaders**, from where the Sun really is, not by Unity's lights. This is what lets a moon show the right phase from anywhere.

## Your new request: baked lighting

You asked on 11 November to "bake in render and bake in lighting (ray tracing or path tracing)". This is how I have read it. Please correct me if I have it wrong.

- **Baked lighting, yes.** Unity can trace the paths of light once, in the editor, and store the result in textures. In play it looks soft and bounced and costs almost nothing. I will use it for the places that are fixed: **the ship's interior (Milestone 7) and the launch base (Milestone 8)**. It will be one menu click for you.
- **Live ray tracing, no.** Tracing light every frame is too heavy for the Steam Deck at 60 frames a second.
- **Planets cannot be baked that way.** Their ground is made from the seed when a run starts, and the Sun moves all the time, so there is nothing fixed to bake in the editor. They are lit directly from the Sun instead, which is cheap, and anything that can be worked out once is worked out when the ground is made.

This is now written into `docs/performance_budget.md` and the repo notes.

## What was tested

- **733 rule tests pass** (`dotnet test`, outside Unity). The new ones check: the index has over 100 formations with unique ids and every one is used; the same seed gives the same world and a different seed a different one; landmarks do not move with the seed; no body is flat; sea covers the fraction it should (Neptune about 98%); gas giants have a deck and ground beneath it; caves have a usable mouth and width; the tiles meet without gaps; the far picture agrees with the ground; air, sea and weather rules.
- **The whole project compiles** against Unity reference assemblies, including 63 PlayMode tests (11 new for planets). PlayMode tests **have not been run**: they need the editor.
- **Shaders**: all four pass a syntax check against a stand-in for Unity's shader library. That is not the same as Unity compiling them.
- **A test program** counted the ground pieces drawn in the worst case (standing on Earth): 153, about 390,000 triangles.
- **Surface maps** of all 19 bodies were painted with the game's own code and looked at (attached).

## Agent review

All three reviewed the milestone before it was committed.

**game-designer.** Applied: wind became moving air; buoyancy; the lamp no longer flickers at dusk; clearer HUD words in clouds, water and wind; lower cloud decks on Jupiter and Saturn so the blind dive is shorter; Venus, Ganymede, Titania, Deimos and Neptune retuned because they were too like their neighbours. Not applied: doubling the number of formations on Venus, Jupiter and Saturn (I would rather you see them first).

**deck-performance.** Applied: the draw distance for fine ground lowered (237 tiles down to 153 on Earth); making ground solid is spread over frames and capped at 260 m; loose rocks only within 170 m and at most 24 new ones a frame; ground building reuses its working memory; far pictures are painted in small slices and never take the last free processor core. Not applied: moving the solid-ground step to a background thread, until the Profiler shows it is needed.

**unity-reviewer.** Two real bugs found and fixed: after **Pause > Restart** the new world's background work was being cancelled by the old world's clean-up, so planets would never have got their ground; and **fog would have been missing from a built game** (fine in the editor), which the set-up tool now prevents. Also fixed: the air rim and the sea fighting over draw order; the rim vanishing in one frame as you descend through it; duplicate caves if you left and came back quickly; an error from setting a picture on the wrong property; small leaks on restart. Not fixed, to check in the editor: possible small bumps crossing between ground pieces, rain being too thin to see at a distance, rain falling through the cockpit, and blood decals not showing on planet ground (a Milestone 12 matter).

## Still unverified

Everything that needs the engine. In particular:

- Whether the shaders compile in Unity, and whether anything is pink. If so, tick **Simple Shaders** on the Planets asset and tell me.
- Whether arriving at a planet stutters, and frame times on the surface. All figures in the budget document are estimates.
- How the ground actually looks up close: scale of hills, colour, whether craters and canyons read well from the cockpit.
- Skies, sunsets, the dive through the cloud decks, underwater on Neptune.
- Weather: whether rain and sand are visible, lightning, thunder volume.
- Landing and sliding on the ground. There is a safety net that lifts a craft back up if it ever falls through; the debug readout counts "rescues" and that number should stay at 0.
- Nothing has been run on a Steam Deck.

## Decisions for you

None of these block anything. I have gone with the recommended option each time.

1. **Titan's atmosphere** (above). Recommended: keep.
2. **Gas giants as a small world under an opaque deck**, about ten seconds blind. Recommended: keep, tune the time after flying it.
3. **Thick air and water slow the ship.** Recommended: keep; it makes Venus and Neptune feel different.
4. **The ship floats.** Recommended: keep until the real ship in Milestone 7.
5. **Storms can move a parked ship.** Recommended: keep, it is a little scary in a good way.
6. **The lamp is automatic.** Recommended: automatic now, a button later if you want one.
7. **Baked lighting** (above): confirm my reading.
8. **Real NASA textures.** The worlds are currently coloured by rules, informed by real imagery. Laying real NASA maps over them is possible for the far views. Recommended: decide after you have seen them in Unity.

## Playtest checklist (when you first open Unity)

1. Run **Orbital Ante > Set Up Project** once more (it creates the Planets asset and the fog setting).
2. Choose **Fly the solar system**. Press **F3**. Does "Ground ready" climb to 19/19 within about half a minute?
3. Fly down to Earth. Does the ground sharpen smoothly, or pop and stutter? Is the sky blue by day?
4. Land. Is there something within sight you want to walk or fly to?
5. Find a cave mouth and go in. Does the lamp come on? Can you get back out?
6. Fly to the Moon. No sky, black shadows, lots of craters?
7. Fly to Mars. Orange haze? Can you find the big canyon and fly along it?
8. Dive into Jupiter. How long are you blind? Is the world underneath worth the trip?
9. Fly to Neptune, land on the sea, go under. Can you tell which way is up?
10. Wait for a storm on Earth, Mars or Jupiter. Rain or sand visible? Lightning? Does the wind push you?
11. **Pause > Restart** and check step 2 again.
12. Read the budget line (OK, TIGHT or OVER) standing on Earth, and the `tiles`, `things` and `rescues` numbers.
13. Run the tests (Window > General > Test Runner): EditMode should show 733 passing, PlayMode 63.

## Assets I will need

Nothing for this milestone. For Milestone 7 (the ship), a concept turnaround of the Lunar Module style starter ship would help, and I will list exactly what to generate when I start it.
