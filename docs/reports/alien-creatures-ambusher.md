# Alien creatures: the Ambusher (26 Nov 2026)

The sixth of Matthew's nine (his go-ahead of 19 Nov 2026: "Continue to build these, spider crab looks good"): the **Ambusher** (placeholder name), from the bobbit worm. It is the first creature that waits for you instead of coming at you, and the first that takes hold.

**Nothing here has been run in Unity.** The pictures and clips are Blender's, drawn from the game's model and the game's own movement rules; they are not Unity screenshots.

## What it is

A worm as thick as a thigh and 5.4 m long, nearly all of it down a hole. What shows is a low heap of thrown-up ground, a dark armoured head level with it, two sickle jaws lying open either side like a trap, and five banded feelers standing up and swaying. Its skin is dark and ringed, with an oil-on-water sheen. Pictures: `docs/art/previews/creatures/ambusher_*.jpg`.

- **It feels footsteps, not sight.** It feels you at 9 m when you walk, less when you sneak, hardly at all when you stand still (within 1.6 m it always knows). Its feelers stand up and lean your way when it feels you, and droop when it does not: that is how you tell.
- **It warns for 0.9 s.** Inside 4.6 m it rears a metre out of its hole, jaws half closing, and follows you, aiming where you are walking to. At half way its jaws fly wide with a scrape: from that moment it is aimed, and a step aside is a miss. Back out of reach before then and it calls the strike off.
- **The strike takes under a fifth of a second.** A hit bites for 14 and, with both jaws, **holds you**: you can look round and shoot but not walk or jump, it drags you toward its hole, and it bites for 8 a second in beats, for 3 s at most. Hurt it for 40 while it holds and its jaws slacken (you see them open) and it lets go. Shooting a jaw off lets go at once.
- **A miss leaves it lying out on the ground for a second,** its whole soft length in the open. That is the time to cut it.
- **Then it goes under and comes after you.** It tunnels at 4.2 m/s (you walk at about 5) under a low moving mound, comes up ahead of where you are heading with the ground heaving first, and waits again. Under ground nothing reaches it. Shot at while it waits, it ducks and comes up nearer the shooter.
- **Its jaws come off** (60 each). With one it bites at half strength and cannot hold. With none it is thrown out on the ground for 2 s, then goes back to waiting in its hole, harmless, until you finish it. Feelers come off too (15 each): each one lost shortens how far it feels you.
- **It dies two ways:** shot through any length of its body (90 each; damage to a length is remembered), which cuts it in two, or through the armoured head (200; it knocks, no blood). Dead, everything out of the ground comes apart at the joints; the heap is left behind.

## How it moves (no animation clips)

Its body is one line from the bottom of its burrow to its head, and the pieces are strung along that line, so it never stretches. Waiting, the line stands in the hole. Rearing, the line arches back like a drawn spring. Striking, it straightens along the ground to where it aimed. Holding, it arches with the head at chest height and pulls in. Spent, it lies slack over the lip of the hole. The jaws swing on their hinges with the mode; the feelers sway by themselves and lean toward what it feels. The heap swells as it comes up and runs low while it tunnels. Clips in the project files: a strike that lands and holds, a miss with the tunnel and the ambush, and being taken apart.

## Sounds

Its own, made by code in `tools/audio/sfx/creature.py`: `creature.ambusher.coil` (a tightening wet hiss with ticks, and the scrape of the jaws flying wide at the moment it takes aim), `creature.ambusher.strike` (a rush and a snap), `creature.ambusher.rise` (the ground heaving, breaking, grit falling). There is no sound for tunnelling yet: the moving mound is the only sign.

## What changed

- `Scripts/Runtime/Creatures/`: `AmbusherLayout`, `AmbusherBody`, `AmbusherAnimator`, `AmbusherBrain` (plain C#), `Ambusher` with `AmbusherPart` (Unity side); `CreatureConfig.ambusher`, `arenaAmbushers`.
- `Player/FpsController`: `Carry(velocity)`, for anything that takes hold of the player. It must be said every frame; the frame it is not, they are free. Respawning and freezing clear it.
- `tools/blender/build_creatures.py`: the Ambusher, textured at 2048, and its far copy; `oa_materials.py`: `sheen`, `banded`, `earth`. `tools/blender/creature_dump`: its scenes (`AmbusherDump.cs`); a frame of a pose file may now say where the person stands.
- `Resources/Models/Ambusher.bytes`, `AmbusherFar.bytes`, `Textures/Ambusher_*.png`; three sounds in `Resources/Audio/Sfx/creature/`.
- The combat arena starts with one Ambusher a little way down from the start, to the left.

## Constitution

- **1, fun core:** a sixth verb: watch the ground. The others come to you; this one makes where you walk matter.
- **2, agency:** sneak past, bait the strike and step aside, back out of reach, shoot the jaws off first, or cut it while it lies spent. Held, you choose between the jaw (60, lets go at once) and the body (40 to make it let go, and on the way to killing it).
- **3, learnable challenge:** the feelers say whether it feels you; the warning has a fixed length and one clear moment of aim; the jaws slacken before it lets go; nothing is random and no health is hidden.
- **5, legible risk:** the heap marks it from a distance and marks where it is tunnelling; armour knocks and flesh bleeds.
- **8, varied intensity:** long stillness, under a second of violence, a held struggle or an open kill, then the mound coming after you.

## Tested

- 36 rule tests in `AmbusherRulesTests`: what comes off and the two ways it dies; feeling by noise, less with feelers lost; the warning, the moment of aim, a step aside after it, backing out before it; the strike's reach and a jump over it; the hold (no walking, the drag, the beats of hurt, the time limit, letting go when hurt, at once for a jaw); one jaw biting without holding; no jaws being thrown out and then waiting harmless; lying spent; going under, tunnelling no faster than its pace and only under ground, coming up ahead of someone on the move and only where the ground is open; never leaving its range; the body never stretching and never above the ground while buried; the left jaw as the right one rolled over; the model against the layout. Whole suite: 2,693 passed, 1 skipped (the cabin bake check, which needs a file the cloud does not build).
- The Unity-side code compiles against Unity reference assemblies.
- The model: 2,388 triangles in the file, 8,008 as drawn with every length out of the ground (a length is one mesh drawn fifteen times; about half of them are ever out at once), 4,228 as the far copy; seen from inside 0.00%.

## Agent review

- **game-designer** found: its jaws could be shot off while it was still coming up (hits are refused until its head is out, and a hit while rising makes it duck); a jawless one went away for good and could never be finished (it now stays to be killed); backing away during the warning gave a free kill (it calls the strike off); the time to step aside was too thin (warning 0.9 s, aim at 0.55 of it); the way out of a hold was hidden and long (jaws slacken as it is hurt; 3 s at most); the head was dearer than it needed to be (260 to 200); nothing showed whether it had felt you (feelers droop). Not done: a sound for tunnelling.
- **deck-performance** found pieces thrown off still being switched with the animal, hidden pieces still drawn, and posing while buried; all three fixed. It gave the budget line now in `docs/performance_budget.md`.
- **unity-reviewer** found seven things, all fixed or answered: a hold could outlive a killing bite into the respawn; `FpsController.Held` depended on script order; pieces down the burrow showed for one frame on rising; a wound on the head stayed visible under ground; it could come up inside a crate (the check over its head now runs every frame while it tunnels); it could come up outside the arena (it now needs ground under it); and a comment promised more than the check does (it comes up under people gladly; the comment now says so).

## Not verified, and not built

- Not run in Unity: whether it appears, how the strike and the drag feel in first person, whether 0.9 s is the right warning and 3 s the right hold, whether the mound reads on the arena floor, how the sounds sit in the mix, frame cost.
- **Being held takes your legs away for up to 3 s.** Nothing else in the game does that. It is the animal's whole point, and it has three ways out, but it is the first thing to judge on the Deck. Say if it should be shorter, or only slow you.
- **A jawless one stays to be finished** rather than leaving. Say if you would rather it fled.
- It lives in the combat arena only, on level ground. "Soft ground" on the planets (the brief's place for it) needs it to know what ground is soft: not built.
- Damage numbers were set against the rifle; the pistol and the others were not checked one by one.
- The heap is one colour of earth; on a planet it should take the colour of the ground it is in.
