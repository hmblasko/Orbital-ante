# Alien creatures: the Blind stalker (26 Nov 2026)

The seventh of Matthew's nine (his go-ahead of 19 Nov 2026: "Continue to build these, spider crab looks good"): the **Blind stalker** (placeholder name), from the olm, the blind cave salamander. It is the first creature that cannot see you at all. It finds you by the noise you make.

**Nothing here has been run in Unity.** The pictures and clips are Blender's, drawn from the game's model and the game's own movement rules; they are not Unity screenshots.

## What it is

A pale, eyeless salamander 4.4 m long: a flat spade of a head, a soft trunk as thick as a thigh on four thin legs, a tail with a fin above and below, and three red feathery gills standing out either side of its neck. Its skin is the colour of something that has never seen light, pink where blood shows through, with thin violet veins. Pictures: `docs/art/previews/creatures/stalker_*.jpg`.

- **It is blind. It hears.** Someone walking is heard at 20 m, someone sprinting further, a shot at over 40 m. Someone standing still is not heard at all. Inside 1.5 m it feels you through its snout however still you are.
- **It goes where the sound was, not where you are.** It takes a new place from what it hears once a second, and each time it does, its gills kick out and it gives a quiet double click. That click is the tell: where you were at that moment is known. Move away from that spot without a sound (or just move and then stand still) and it walks to the empty place, casts about for five seconds and goes still again.
- **A walk brings it at a walk** (2.2 m/s, far slower than you). **A shot brings it at a run** (6.4 m/s, faster than you walk, slower than you sprint). A sprint is heard further off but does not make it run.
- **It warns for 0.9 s.** Within about 3 m of the place, and only if what it heard is fresh, it stops, lifts its head and drops its jaw with a long wet breath in. While its mouth is opening it listens as hard as it can and follows every sound. At half way the jaw falls wide with a pop and the gills snap out: from then it is aimed, and a step aside is a miss.
- **The lunge takes a fifth of a second.** Its tail throws it up to 3.2 m and its jaws shut on the place it aimed at, for 24. Then it lies where it landed for 1.2 s. That is the opening.
- **Its gills are its ears, and they come off** (24 each: one rifle hit). With one it hears from 0.6 as far, with none from 0.2 as far. Losing one makes it reel for a second and calls off a warning under way (not twice running). Being hurt tells it where the shot came from, as a noise would.
- **Legs come off** (45 each): it slows with each, and on fewer than two it lies on its belly and swims along the ground. **The tail can be cut** at any of its four lengths (60 each); each length lost shortens the lunge.
- **It dies two ways:** through the soft trunk (200, one shared number) or the armoured skull (280). Dead, it comes apart at every joint.
- **It keeps to its own ground.** It never goes after a sound further than 28 m from where it lives, never lunges at the edge of that ground, and walks home when nothing has been heard for 10 s.

## How it moves (no animation clips)

The body follows the head over the ground it has really covered, like the Cave hunter's, and bends from side to side as it walks, more toward the tail. Its feet stay planted and step a diagonal pair at a time, elbows out. Still, it sinks onto its belly and hardly breathes. Hearing something, its gills stand out and its head comes round. The warning lifts its head and shoulders; the lunge straightens it like a thrown rope. Clips: `stalker_strike.mp4`, `stalker_lost.mp4`, `stalker_lose.mp4` in the project's files (`orbital-ante/alien-creatures/`).

## Sounds

Its own, made by code in `tools/audio/sfx/creature.py`: `creature.stalker.hear` (a string of wet clicks that quickens as its head comes round), `creature.stalker.click` (the quiet double click each time it places you), `creature.stalker.gape` (the breath in, and the pop at the moment it takes aim), `creature.stalker.lunge` (a rush over the ground and the jaws clapping shut).

## What changed

- `Scripts/Runtime/Creatures/`: `StalkerLayout`, `StalkerBody`, `StalkerAnimator`, `StalkerBrain` (plain C#), `Stalker` with `StalkerPart` (Unity side); `CreatureConfig.stalker`, `arenaStalkers`.
- `tools/blender/build_creatures.py`: the Blind stalker, textured at 2048, and its far copy; `oa_materials.py`: `pallid` (pale veined skin), `plume` (gill). `tools/blender/creature_dump`: its scenes (`StalkerDump.cs`).
- `Resources/Models/Stalker.bytes`, `StalkerFar.bytes`, `Textures/Stalker_*.png`; four sounds in `Resources/Audio/Sfx/creature/`.
- The combat arena starts with one Blind stalker part way down, off to one side.

## Constitution

- **1, fun core:** a seventh verb: be quiet. Every other creature is answered by moving and shooting; this one punishes both.
- **2, agency:** stand still and let it pass, creep away from where it last heard you, shoot its gills off so it is half deaf, bait the lunge and step aside, or fire from far off and take the run that follows.
- **3, learnable challenge:** nothing is random. The click says when it has placed you; the pop says when it is aimed; the gills show whether it is hearing you now. It cannot be kept reeling, and it has no hidden health.
- **5, legible risk:** a shot is the loudest thing you can do, and it shows: the animal runs.
- **8, varied intensity:** long quiet, a held breath while it walks past, then a fifth of a second of violence.

## Tested

- 38 rule tests in `StalkerRulesTests`: what comes off and the two ways it dies; hearing by noise and less with gills lost; feeling up close; going where the sound was and lagging by its hearing's beat; the click at each new place; standing still away from the sound not being found; a shot bringing it at a run; the warning, the moment of aim, a step aside before and after; the lunge's reach and bite; reeling, not twice running; never lunging at the edge of its ground; giving up a place it is kept from by a wall; facing someone who circles it up close; walking home; how the body, feet, jaw and gills move; and the model file matching the layout.
- The Unity-side code compiles against Unity reference assemblies.
- The model: 3,282 triangles in the file, 7,208 as drawn (a length of trunk is one mesh drawn seven times, a length of tail four, a leg four, a gill twice), 1,894 as the far copy; seen from inside 0.00%.

## Agent review

- **game-designer** found that standing still never saved you and moving always did, which is the opposite of the idea: it felt you from 3 m and took a new place twice a second. Now it feels from 1.5 m and takes a place once a second, and says so with the click. It also found: shot from beyond its ground it lunged at the air on repeat (it no longer lunges at the edge); it could stand at a wall for ever (it gives the place up and casts about); hugging its shoulder was safe (it turns 2.5 times quicker to something it feels); a sprint made it run though the rules said only a shot does (a sprint is now under "loud"); too little time to step aside (warning 0.65 s to 0.9 s, aim at half way); gills not worth shooting (35 to 24, one hit each).
- **deck-performance** found it posed and synced physics every frame while lying still, which is most of its life: resting, it is now posed five times a second and syncs only on frames it has changed with the player within 30 m. `StalkerHit` became a struct. It gave the budget line now in `docs/performance_budget.md`.
- **unity-reviewer** confirmed the wall and the sprint, and found: noise overstated on slow frames (it is now measured over the frame's real length, along the ground only, so a jump is not a footstep); hitboxes lagging while the arena is calm (they now follow whenever the player is near); a comment that said the cut tail still flexes (it does not).

## Not verified, and not built

- Not run in Unity: whether it appears, whether standing still really feels safe, whether the click is heard and understood, whether 0.9 s is the right warning, how the sounds sit in the mix, frame cost.
- **In the arena, noise is worked out from how fast you move and whether you have just fired,** because the arena's player has no crouch or sneak. In the suit it uses the game's own noise figure (stance, jets, shots), but it is not placed on any planet yet.
- It walks in straight lines. In a cave it needs the cave's walk graph (`CaveFooting`), which it does not use yet: the brief's place for it is caves, and that is the next step for it.
- A prone player in the suit would be out of the bite's reach as written (the feet are taken as 1.6 m below the eye); to fix before it leaves the arena.
- Two dying together make up to 40 loose pieces against a shared cap of 34, so some will vanish early.
- A cut tail leaves as one stiff piece.
- Its legs are the weakest part of the model: thin and plain. Say if they should be redone.
- Damage numbers were set against the rifle; the other guns were not checked one by one.
