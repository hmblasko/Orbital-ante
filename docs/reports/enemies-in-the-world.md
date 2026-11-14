# Enemies in the world (14 Nov 2026)

Asked for by Matthew on 14 Nov 2026: several enemy types with their own attributes and weapons, numerous enemies everywhere, fighting near constantly. **None of this has been run in Unity.** It compiles against reference assemblies and the rule tests pass in the cloud; everything about feel, frame rate and how it looks is unverified until it runs on the Deck.

## What changed

- **Enemies stand on round worlds.** A character now asks its `Footing` which way is up and where the ground is. The arena keeps flat footing; on a planet `PlanetFooting` points up away from the body's centre. Enemies are children of the body they stand on, so they ride with it.
- **Four types** (`EnemyRoster`, placeholder names, no faction):

| Type | Build | Gun | How it fights |
|---|---|---|---|
| Trooper | ordinary | rifle | bursts, side-steps between them |
| Runner | small, quick, fragile | "Buzz Gun" (fast, weak) | sprints straight at you and sprays from close up |
| Bruiser | big, wide, slow, tough in the middle | "Double Zapper" (two heavy blasts, short range) | walks you down without stopping |
| Marksman | tall, thin, stands still | "Long Shot" (one slow heavy shot) | a red line shows where it is aiming for one second before every shot; never more than one in a camp |

- **Camps on every world** (`EnemyField`): squads of 3 to 5, seeded, the same for a seed every time, none within 170 m of the launch pad. Only the camps near the player are real characters (at most 12 present, the fallen included); the rest are numbers. A cleared camp stays cleared for the run.
- **The suit can be shot.** "SUIT nn%" shows on the visor only when hurt. It mends when you go back aboard. Death is first person, with the cause in words.
- The player carries the rifle outside.

## Why, and which principle

- Principle 1 (fun core first) and Matthew's "fighting near constantly": on the Moon and Mars about five camps sit within waking distance of anywhere you land.
- Principle 3 (learnable, never hidden rules): toughness is only in the torso and head, so limbs come off as easily on every type. The Marksman announces every shot. Enemies out of sight only come after you if they saw you in the last 8 seconds or you are within 60 m.
- Principle 8 (varied intensity): a cleared camp stays quiet, and enemies that lose you go back to wandering.

## Agent review

**game-designer** (7 points, all applied except where noted):
- Marksman was unfair: three hits killed and nothing announced the later shots. Now 25 damage (was 34), a one second aim line before every shot, one per camp.
- Awake enemies knew where you were from 230 m. Now sight memory of 8 s or hearing at 60 m.
- Runner and Bruiser did not close in as described. They now come straight at you until inside their fighting distance; Bruiser speed 0.6 to 0.8.
- A camp you left came back with the wrong members. Each member is now remembered. A head rolling about no longer holds a place.
- Small moons had 12 enemies on a 35 m rock (least camps 4 to 1). Earth's cap raised 70 to 120 camps.
- Types did not read at a glance: each now has its own proportions (width) and, on the stand-in shapes, chest colour. **Not done:** colour on the real model, which needs one model per type from the look thread.
- **Not done:** loose limbs still fall with Unity's gravity pointed the way the player feels down, not each with its own pull. Wrong only far round a small moon from the player.

**unity-reviewer** (9 points, all applied):
- Enemies would never have fired over large parts of every planet (the aim check flattened on world Y). Fixed.
- No blood, pop sounds or splats on planets (the gore objects were only made in the arena). Fixed.
- Restarting from the solar system into the arena left arena enemies blind. Fixed.
- Fights further than the solid ground (90 m) ignored hills. Nobody now engages past 80 m.
- Dropped guns fell through the world once the player walked away. They are cleared with their owner.
- Wandering re-planned every frame on steep slopes. A hillside is no longer a wall.
- Loose chunks jumped when the world re-centres; armour did not mend aboard; chunk gravity froze after the last kill. Fixed.

**deck-performance** (6 points):
- Waking a camp made about 400 objects in one frame (guessed 10 to 30 ms). Now one enemy a frame.
- Dropped guns were never removed. Now cleared with the body, and never more than 12 loose.
- The cap counted the living only. The fallen now count until they are cleared (25 s).
- Unity's gravity was set every physics step. Now only when it changes.
- **Not done:** skipping the unused Humanoid avatar per enemy outside the arena, and thinking at half rate far away. Left until measured.

## Also in this push: three fixes taken from ChatGPT Codex pull request 33

- Speed against a locked body is now measured at the ship's own place (`SolarSystem.VelocityOfSeenFrom`). Close to a spinning body the old reading was off by the spin times the distance to the target.
- When the autopilot lets go by itself, the push it was asking for is dropped at once.
- Unbuckling drops whatever was being asked of the engines.

## Tested

- Runtime and PlayMode test code compile against reference assemblies.
- Rule tests: 1158 pass, 5 skipped, 1 fails (`CabinFittingsFile_IsTheDashHousing_WithTheCabinsLightBakedIn`, the cabin bake from the look thread, not this work). New: `EnemyRulesTests` (7).

## Unverified (needs Unity on the Deck)

Everything visible: whether enemies stand, walk and aim properly on a slope and at every latitude; whether the types read apart; the aim line; blood and sound; frame rate with 12 present; whether density feels right on each world. The numbers the design review gave (hit chances, seconds to die) are arithmetic, not play.

## What to try

1. Land on the Moon or walk away from the pad on Earth. Press F3 and note the frame rate before enemies appear and during a fight.
2. Shoot a limb off: there should be blood and a pop.
3. Find a tall thin enemy standing still: a red line should show for a second before each shot.
4. Leave a half-cleared camp, fly off and come back: only the ones you left should be there.

## Deep Breath: limited-use slow motion (14 Nov 2026)

Asked for by Matthew: "Max Payne style slow-mo ... only for a limited use". Built as the game's own mechanic. **Not run in Unity.**

- Hold **Q** (keyboard) or **click the right stick** (controller) while on foot. The world slows to about a third of its speed; your own aim does not.
- A full meter lasts 6 real seconds. It **never refills by waiting**: a limb shot off gives back a tenth, a kill a quarter. It needs 15% to start (the notch on the bar). A run starts full.
- A thin bar low in the middle of the screen shows only when it is not full or in use; a cool rim round the view shows while the world is slowed.
- The refill rule is the one recommended on the decision card (refill by fighting). It is three numbers on the `DeepBreathConfig` asset if Matthew picks another.
- Principle 2 (a meaningful choice of when to spend it), 5 (the whole rule is on screen), 9 (no timer to wait on).
- All changes to game speed now go through one place (`TimeFlow`), so pausing during slow motion cannot leave time stuck.
- Tested: 13 rule tests (`DeepBreathRulesTests`) pass; `DeepBreathTests` (PlayMode) compile but have not run.
- Unverified: the feel of the slowing, whether physics stays smooth at a third speed, the bar's place on the Deck's screen, and sound (the audio thread follows `DeepBreathFx.Amount`).
