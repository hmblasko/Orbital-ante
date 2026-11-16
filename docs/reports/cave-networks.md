# Cave networks with enemy camps (16 Nov 2026)

Matthew asked: "I'd like have some of the planets have interesting and intricate cave systems where enemies will reside".

**Not run in Unity.** Everything below was built and checked in a cloud session with no Unity editor. The rule tests pass, the game code compiles against Unity's libraries and the shader change compiles for Vulkan, but nobody has walked into one of these caves yet, and the picture below is from an offline renderer, not a Unity screenshot.

## What changed

**Four worlds now have cave networks, the rest do not:**

| World | Kind | How many | What it is like |
|---|---|---|---|
| Earth | Karst (limestone) | up to 2 | 6 to 9 rooms on two or three levels, narrower winding passages, stalagmites and hanging spikes, flowstone streaks |
| Moon | Lava tubes | 1 | 5 to 7 rooms, broad flat-floored tunnels, a roof collapse letting light in |
| Mars | Lava tubes | up to 2 | as the Moon, rust-tinted |
| Ganymede | Ice caves | 1 | 5 to 8 halls in blue ice, crystals that glow faintly |

The old single caves (tunnels, caverns, ice caves, lava skylights) are unchanged on every world.

**Each network** is planned from the run's seed (`CaveNetwork.Build`):

- **Rooms on two or three levels.** One great hall, the rest smaller, each with a level floor and a domed roof at least 5 m under the ground.
- **Passages that branch and loop.** Every room can be walked to, and there are extra passages so there is more than one way round. On a slope a passage winds like a path up a hillside and is never steeper than about 1 in 1.6.
- **A shaft** between levels that only the jetpack climbs: a way up or down that enemies cannot follow.
- **Dead ends** that wind off and narrow to nothing.
- **One or two walk-in entrances** sloping down from the surface, and often a **hole in a roof** that drops daylight into a room.
- **Camps:** most rooms other than the first one you walk into hold 2 to 5 enemies (4 or 5 in the big rooms) with work lamps. The room under the roof hole never holds a camp, so dropping in from above is a way in, not a trap.

**The walls** (`CaveNetworkMesher`) are worn rock, meshed at 0.9 m in blocks so blocks out of view are not drawn. They are coloured per world: bedding lines and flowstone in limestone, dark basalt with blown-in dust in lava tubes, blue ice. The floors are darker, and the hollows darker still.

**The light** (`CaveLightVolume`, `OA_CaveLight` in `OA_Look.hlsl`):

- Daylight reaches in from the entrances and roof holes and fades with depth, so deep rooms are dark.
- The camps' work lamps light their rooms warm, spilling a little way down the passages.
- Ice crystals glow faintly blue.
- No real-time lights are added. The light is worked out once, when the cave is built, into a small 3D texture that the shared lighting reads only near a cave. The ship's new floodlights and the suit lamp still light whatever they face.

**Enemies** in a cave camp (`CaveFooting`, `EnemyField.AddCaveCamps`):

- They stand in their room and stroll inside it.
- When they hunt you, they follow the cave's own walk graph through the passages instead of walking into the rock.
- They wake only within 70 m, and only once the cave is solid.
- They see you only through open air, never through a cave wall or the ground.
- The new stealth rules (crouch, prone, sight range) apply in caves as everywhere else.
- **The dark hides you.** In an unlit passage an enemy that has not spotted you yet sees you from about a third of its usual range; in a lamp-lit room or under daylight, from the full range. Once you are spotted, the dark does not save you (the same rule as crouching).

**For testing:** with **F3**, the debug readout shows `cave way in` and the distance to the nearest entrance on the world you are near.

## Why (constitution)

- **4. Discovery that matters.** The caves are hidden places with their own look on each world, worth finding.
- **2. Agency and meaningful trade-offs.** Loops, the jetpack-only shaft and the dark give you ways to sneak, flank or escape.
- **5. Legible risk.** A lit room ahead means a camp; a dark room is empty of camps.
- **8. Varied intensity.** Quiet dark passages break up the near-constant combat Matthew asked for, before the fight in the next room.

## Tested, and how

Rule tests (`dotnet test tools/rules-tests`): 2405 pass, 0 fail (one cabin-bake check is skipped, as before). Eleven of them are new in `CaveNetworkTests`, run on three seeds for each of the four worlds:

- Only some worlds have networks, and the same seed always gives the same network.
- **Intricate:** at least two levels, more passages than a chain needs (loops), at least two dead ends, a way in.
- Every room can be walked to from the way in.
- Every room has a roof and a floor where the plan says.
- **Walls:** watertight, facing into the cave (at least 97%), and within the triangle budget.
- **Light:** daylight just inside the way in and under a roof hole, almost none in the deepest room, and lamplight in every camp.
- **Walking:** a walker that only follows `NextStep`, standing on whatever floor is there, reaches every camp from the way in.
- Every lived-in room gets a camp and a lamp.
- The work lamp's shape faces outward.
- In the dark you are seen from closer, but never from less than a third of the range.

`PlanetRulesTests.CavesStayUnderGroundWithARoofOverThem` now leaves networks to their own tests: they are walked through, not flown into.

Other checks:

- **Compile:** the game code compiles against Unity's libraries, mapped back from Unity 6 names. The only errors are the two already in `PlayerBurstFx.cs` (`excludeLayers`).
- **Shader:** the changed `OA_Look.hlsl` compiles to Vulkan code (`glslangValidator`).
- **Preview:** an offline picture, drawn by a small software renderer from the same walls and light, is at `/mnt/project-files/orbital-ante/cave-networks/cave-networks-preview.png`. It shows the walls' shapes and colours and the baked light only. It does not show the game's shader detail, bloom, the lamp models or the enemies.

Measured outside Unity on a cloud machine (the walls and light are built in the background, never on the frame):

| | |
|---|---|
| Triangles per network | 22,000 to 38,000 |
| Building the walls | 2.0 to 3.7 s of one core, in the background |
| Building the light | 0.1 to 0.35 s |
| Light texture | 0.97 MB at most |

On the frame, the walls are drawn one block a frame, and a block is only made solid (one a frame) once you or the ship are within 90 m of it. A cave being built never holds up the start of a run. See "Cave networks" in `docs/performance_budget.md`.

## Fixed along the way (found by the walking test)

- Passages that sloped sank their floors into steps where the slope changed. Their floors now follow the planned line.
- A ramp could still be high when it came out under a room's dome, leaving a ledge above the floor. It now arrives level.
- Two passages, or one winding back on itself, could pass over each other with only a skin of rock or a hole between. They now keep at least 1.5 m of rock apart, or meet as a junction at the same height.
- A room on another level could sit too close to the side for a ramp to reach it.
- Floor spikes and stones could block a passage mouth.

## Agent review

The engine code went to the three review agents before committing.

- **game-designer.** The dark should matter to stealth. **Done:** you are seen from closer in the dark. The roof hole always dropped you into a camp. **Done:** that room never has one. Rooms need cover, camps need a sound you hear before you see them, lamps should be shootable and the shaft's lip should be easier to spot. **Not done:** these are follow-ups below.
- **deck-performance.** The path search could run many times a frame, network building held up the start of a run, every block cooked a collider, and the model shader read the cave light three times a pixel. **All done:** at most 2 path plans a frame among all enemies, and the nearest-waypoint search stops at the first clear one; networks are background work nobody waits for; colliders are only made near you, one a frame; one read a pixel. The extra sight line is only cast while a network is built.
- **unity-reviewer.** No blocker found. **Done:** one block a frame and lighter collider cooking; the colour conversion moved off the main thread; enemies no longer head straight for you through rock after reaching a step, and their route is kept in the body's own space; the empty light box is bound before any scene loads; the light box is stored as plain numbers, not sRGB; walls do not take full sun under the simple-shader fallback; a lamp facing straight up no longer gets a broken rotation.

## Follow-ups (not built)

- Cover in the rooms (pillars, fallen blocks, ledges).
- A camp sound (a generator hum) you hear before you see the lamps, and lamps at passage mouths.
- Work lamps that can be shot out.
- A mark on the shaft's lip.
- A Blender work lamp to replace the code-made one.
- Close-up rock detail on cave walls, like the ground's grit.

## Still unverified (needs Matthew's machine)

- **Everything in the engine.** Whether the walls, colliders and light line up in Unity, whether walking in feels right, whether enemies path and fight sensibly in the passages, and frame times on the Deck. Also whether making blocks solid only near you keeps up when you arrive fast (check the F3 "rescues" count stays still).
- **Look.** Whether the walls read as real rock up close. They are smooth at the 0.9 m mesh, with only colour for fine detail (no close-up grit like the ground has). This is the first thing to judge.
- **The work lamp model.** It is a stand-in made in code (tripod, lamp head, cable, generator) until a Blender model replaces it.
- **Light.** The shader lighting inside caves, and how it sits with bloom and the floodlights.
- **Old saves.** The Planets asset made on Matthew's machine needs `PlanetProfiles.Version` 4 to pick up the new formations. It was raised, so the asset should refresh by itself.
