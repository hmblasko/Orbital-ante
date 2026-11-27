# Alien creatures: the Screamer (20 Nov 2026)

The fifth of Matthew's nine (his go-ahead of 19 Nov 2026: "Continue to build these, spider crab looks good"): the **Screamer** (placeholder name), from the cicada. It is the first creature that flies, and the first that hurts you from a distance.

**Nothing here has been run in Unity.** The pictures and clips are Blender's, drawn from the game's model and the game's own movement rules; they are not Unity screenshots. The scream's rings are the game's own effect and are not in the clips.

## What it is

A cicada 1.7 m long with wings that span 3.6 m: a broad black armoured fore body with wide-set red eyes, a banded orange and black hollow abdomen, four dark glassy wings with orange veins, six thin legs, and a pale round drum on each flank, which is what makes the noise. Pictures: `docs/art/previews/creatures/screamer_*.jpg`.

- **It flies in and lands near you.** 9 m/s in the air (you sprint at 8), about 3.4 m up. It picks a place about 10 m from you; if you are on the move it lands ahead of you, not where you were.
- **It warns for a second.** Landed, it lifts its abdomen and fans its wings while a rising rattle plays. For the first 0.6 s it still turns to follow you. Then wings and abdomen jerk to full height: from that moment it is aimed.
- **It screams for 2.4 s.** The scream is a round cone from its mouth, 0.42 rad (24 degrees) either side and 16 m long, and it hurts in beats five times a second: 16 damage a second close up, 6 at the far end. One pale ring races out along the cone on every beat, so where the rings pass is where it hurts. While screaming it can only swing round slowly (20 degrees a second), so you walk out of the side of the cone, or get behind anything solid, which stops it.
- **It pays for a scream.** It sags for 1.3 s, out of breath, then flies somewhere else.
- **Its voice can be shot off.** A drum (70: three rifle hits) shot away while it warns or screams chokes it: the scream stops and it reels for 1.8 s. With one drum it screams at 0.6 strength and reaches 13 m. With none it is silent for good: it makes off once, stays there, and does nothing more to you.
- **It can be brought down.** Wings come off (fore 40, hind 30). It flies on three, slower; down to two in the air it falls, lies stunned 2.2 s, and crawls from then on (2.2 m/s, slower for each leg lost; fewer than three legs and it lies on its belly). Grounded, it still screams.
- **Walk up to it and it takes off**, but only once before its next scream, so it cannot be kept in the air for ever by crowding it.
- **It dies two ways:** the soft abdomen (150; it bleeds), or the armoured fore body and head (300; it knocks, no blood). Dead, everything comes off.

## How it moves (no animation clips)

Its root stays on the ground under it and the pose carries its height. In the air the body pitches into its speed and banks into turns, the four wings beat in a blur (fore and hind a little out of step) and the legs hang under it. Landing, the legs reach down and take the weight on springs. On the ground it walks three legs at a time, wings folded back over the abdomen like a roof. The warning lifts the abdomen and opens the wings in a shiver; the scream holds them spread and shakes the whole animal on every beat, drums flexing. Clips: the flight in, landing, warning, scream and leaving; and being shot apart, in the project files.

## Sounds

Its own, made by code in `tools/audio/sfx/creature.py` like the rest of the game's audio: `creature.screamer.windup` (a rising rattle of clicks, one second) and the loop `creature.screamer.scream` (a pulsed drone that beats five times a second, in time with the rings and the hurt). However many scream at once there is one voice, at the loudest one's place. A preview is in the project files.

## What changed

- `Scripts/Runtime/Creatures/`: `ScreamerLayout`, `ScreamerBody`, `ScreamerAnimator`, `ScreamerBrain` (plain C#), `Screamer` with `ScreamerPart` and `ScreamRings` (Unity side); `CreatureConfig.screamer`, `arenaScreamers`.
- `tools/blender/build_creatures.py`: the Screamer, textured at 2048, and its far copy. `tools/blender/creature_dump`: its scenes (`ScreamerDump.cs`).
- `Resources/Models/Screamer.bytes`, `ScreamerFar.bytes`, `Textures/Screamer_*.png`; `Resources/Audio/Sfx/creature/`.
- The combat arena starts with one Screamer three quarters of the way down, on the right.

## Constitution

- **1, fun core:** a fifth verb: break the line. The others are fought at arm's length; this one makes you move or find cover while everything else is still coming.
- **2, agency:** step out of the cone, get behind something, shoot a drum to stop the scream, clip its wings so it cannot follow, or go for the abdomen.
- **3, learnable challenge:** a one-second warning with a clear moment it takes aim; the rings show exactly where it hurts and when; nothing is random and nothing has hidden health.
- **5, legible risk:** the drums are on show and are what they look like; armour knocks and flesh bleeds.
- **8, varied intensity:** flight, a held breath, the scream, a sag, and gone.

## Tested

- 33 rule tests in `ScreamerRulesTests`: what comes off and the two ways it dies; flying on three wings and falling on two; the voice with two, one and no drums; the warning, the moment of aim and a step aside after it; the cone (round, measured from the mouth, stopped by anything solid, weaker with distance); a choke stopping the scream; sagging afterwards and flying on; someone walking backwards still being screamed at; being flushed only once; a silent one making off once and staying; legs reaching the ground and tucked in the air; the left wing as the right one turned over; the model against the layout. Whole suite: 2,657 passed, 1 skipped (the cabin bake check, which needs a file the cloud does not build).
- The Unity-side code compiles against Unity reference assemblies.
- The model: 3,106 triangles in the file, 4,378 as drawn (a wing is drawn twice, a leg six times), 1,488 as the far copy; seen from inside 0.00%.

## Agent review

- **unity-reviewer** found five things, all fixed: the scream's one loop could follow the wrong animal when two screamed; at a high frame rate the floor could stop it taking off; it could land inside an obstacle; a scream could pass through a wall when its mouth was inside the wall; the rings were too thin to read. It noted that `CombatWorld.Player` is the arena's player only, which is why the Screamer is in the arena only.
- **game-designer** found: walking backwards meant it never got to scream (it now lands ahead of someone on the move and takes aim again in flight); crowding it kept it in the air for ever (it is flushed once only before its next scream); a drum was cheaper to shoot than a wing (40 to 70); a silent one fled and came back for no reason (it makes off once and stays); the hurt was a flat wedge where the rings are round (now a round cone, measured from its mouth); nearer than 0.6 m did no harm (removed); the cone was wide enough to be hard to leave (0.6 to 0.42 rad); it followed you through walls while warning (it turns only while it can see you). Left as for the other creatures: pieces shot off give Focus.
- **deck-performance** found no garbage per frame, asked that the wounds on pieces be skipped when it dies (done), and gave the budget line now in `docs/performance_budget.md`.

## Not verified, and not built

- Not run in Unity: whether it appears, how the flight and landing look, whether the rings read against the arena, whether a second is the right warning and 2.4 s the right scream, how the two sounds sit in the mix, frame cost.
- **Perching on trees and cliffs** (the brief's places for it) is not built: it lands on level ground, in the arena only.
- **Enemies do not come to the scream.** Alarms are a faction's own so far.
- The drums are an addition to the brief's "wings, legs": they are what a real cicada sings with, and they give the scream a way to be stopped. Say if you would rather it had none.
- Melee and the rover cannot hurt it. The wings are solid dark sheets with raised veins, not see-through: the game's model shader has no glass.
- The scream only hurts: no deafening, no blur, no knock back.

## Seeing it on the Deck

1. In Desktop Mode open a terminal in `~/Orbital-ante` and run `git fetch`, `git checkout claude/playtest`, `git pull` (after PR 70 is merged there).
2. Open the project in Unity and wait for it to import.
3. Press Play, choose **Combat test arena**.
4. The Screamer is three quarters of the way down the arena on the right. Press **H** (D-pad down) to start the fight.
5. Try: stand still through one scream and watch the rings; then step sideways out of it; stand behind a wall; shoot a drum while it warns; shoot two wings off while it flies.
6. If nothing appears, look in the Console for a line starting "Orbital Ante: the Screamer's model" and send it to me.
