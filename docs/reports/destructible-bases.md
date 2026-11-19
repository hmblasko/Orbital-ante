# Bases you can walk into and bring down, and trees you can fell

16 Nov 2026. Asked for by Matthew: "The building assets in the base and trees should all be destructible. Also fix the satellite assets. Also, I want to be able to go in the buildings in the bases." On the question of which satellite assets, Matthew chose "Dishes on the base towers".

**Nothing here has been run in Unity.** It was built and checked in the cloud, where there is no Unity Editor. The pictures are Blender renders of the model files, not Unity screenshots: the game's sky, light, haze and bloom are not in them, and the rooms are shown with their baked light the way the game draws them.

## What changed

### Every structure on a base can be brought down
- All five base designs were rebuilt as lists of structures: each pod, dome, shed, tower, mast, dish, tank, length of wall, lamp post and stack of crates stands and falls on its own.
- Each one was cut in Blender, ahead of time, into the pieces it falls into (up to 16; the cut faces are raw, the colour of torn panel or broken concrete), and into the ruin left standing (wall stubs, footings and a heap of rubble). The game cuts nothing while it runs: it swaps the whole thing for its pieces and lets physics have them.
- Shots chip it, and the ship flown into it knocks it down.
  - Props (crates, lamps, masts) fall to a few rifle shots.
  - A building you can walk into, or anything big, takes about a third of a bullet's damage: roughly three rifle magazines for a living pod, so cover does not vanish in two seconds.
  - The ship does nothing below 5 m/s (a careful landing on a roof is safe). Flown into a pod at 15 m/s, it brings the pod down.
- It warns before it goes: a creak and a puff of dust at half worn, sparks and a groan at four-fifths. Walls give no hit marker or ping (those stay the sign you hit an enemy); the marker comes once, on the shot that brings something down.
- What fell stays fallen for the rest of the run: come back and it is a ruin.
- Pieces tumble under the world's own gravity, then come to rest and become part of the base's ground: you can climb the rubble. At most 48 are loose at once.

### Buildings you can walk into
- The living pods, the domes, the sheds and the stronghold's command tower are hollow, with rooms: bunks, a mess table, desks with screens, lockers, shelves, a workbench, pumps, and lamps. Each room's light comes from its own lamps, baked in Blender (path traced into the corner colours, like the ship's cabin), so a room looks the same at noon and at midnight.
- A sliding door opens as you come within about 3 m and closes behind you.
- Every base has at least one **air station** inside: look at it and hold Interact to fill the suit's air (from empty in 6 seconds). Each holds one full suit tank and stays empty once drained. (The designer review flagged this as close to Phase 2's oxygen tanks: see "Open questions".)

### The satellite dishes
- New ground-station dishes stand on the bases: a real paraboloid with its seams, rim, back ribs, four feed legs and a counterweight on a turning mount. The relay masts carry microwave drum antennas with red and white bands and a feeder cable. They can be shot down like everything else.

### Trees can be felled
- Every tree on Earth carries its stump and its falling top, cut across near its foot with pale fresh wood showing.
- A woodland tree comes down to about 6 or 7 rifle shots (thicker trunks take more; giant trees about 60). The ship fells one when flown into it above 4 m/s. A creak warns when it is more than half cut through.
- The top falls the way the last blow pushed it and then lies there; the stump stays for the rest of the run.

## Why
Matthew asked for it. Constitution principles served: **1, fun core gameplay first** (things that break when shot are the TF2 and BeamNG side of the prompt), **2, agency and meaningful trade-offs** (shoot the cover or flank it; bring down a tower with a guard on it), **4, discovery that matters** (rooms to go into, with something useful inside), **5, legible risk and reward** (creaks and groans before anything falls, a hit marker only when it matters).

## What was tested, and how
- **Rule tests:** `dotnet test tools/rules-tests` (Unity-free rules and EditMode tests). New tests:
  - `StructureRulesTests`: toughness grows with size up to a ceiling; bullets chip buildings but fell props; the warnings; a ram counts nothing below the safe speed; health falls once; wrecks are remembered per base and per structure; doors; an air station fills a suit once.
  - Felling: thicker trunks take more; a landing does not fell a tree; a felled tree is remembered by where it stood.
  - `BaseModelTests`, rewritten for the new files. Every structure has a drawn piece and a solid; each can fall, into no more than about twice what it was made of, and leaves a lower ruin. The caps hold outside, inside and solid. Each base has a room with a door that slides about its own width, nothing solid in the doorway, and an air station at chest height in its room. The yard is open; nobody spawns in a wall.
  - `SceneryModelTests`: every tree has a stump and a top that meet exactly at the cut.
- **Compile check:** the runtime scripts compile against Unity's reference libraries (only the known false error about `IHittable` from a name clash in those libraries).
- **Blender:** every base was built, checked against its caps, its floor and its triangle counts, and rendered from the air, at walking height, inside its first room, and with everything fallen.
- **Reviews (agents, from reading the code):**
  - The Unity reviewer found a ruin could be brought down again after a base was rebuilt, colliders that stayed solid for a frame after a fall, and settled pieces still moving for a frame. All three are fixed.
  - The game designer found buildings fell to one magazine, that nothing warned before a fall, that walls gave the enemy hit marker, and that one landing counted as four knocks. All four are fixed. The designer also found air stations too generous: they now hold one tank, not two and a half.
  - The Steam Deck reviewer raised several costs. Fixed now: loose pieces capped at 48, settled rubble capped, small rubble given no solid shape, doors and rooms only looked after within 60 m, ruin shapes built at loading, and pieces and points looked up through a table.

## Still unverified (needs Unity on the Deck)
- Everything in play: the falls, the pieces' physics, the doors, walking through doorways and up porch steps, the prompts, the rooms' light in the game's shader.
- Frame time while a big building falls (guessed 1 to 3 ms for the frame of the fall), with the stronghold in view (about 140 draw calls, 52,000 triangles outside), and the run's loading time with the bigger model files.
- Whether guards on a tower that is shot away land properly on the floor.

## Open questions and follow-ups
- **Air stations:** kept small (one suit tank each). The designer suggests leaving them out until Phase 2's oxygen tanks. Matthew decides.
- Falling pieces and trees hurt nobody yet (a tower falling on its guard would be a good moment): to add once the falls have been seen in Unity.
- Proper creak, groan and collapse sounds: the game uses stand-ins (a low metal impact and a dirt impact) until the audio thread makes them.
- A few of the stronghold command tower's falling pieces still have an open face (the Blender preview shows it magenta; in the game it would show as a see-through gap in that piece of rubble). Cosmetic; to close in the next pass.
- A far version of the bases, if the Deck needs it.
