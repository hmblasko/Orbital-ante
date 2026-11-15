# Enemy animation: startle, scramble, hobble, lean (15 Nov 2026)

Matthew, 15 Nov 2026: "See how you can improve the enemy animations. See about researching open source animations that make their movement look realistic. When they drop weapons, they simply walk around and that doesn't look good. They should look startled when dropping the weapon and scramble to get it back. They should also have a more interesting hobble when losing limbs. They should also lean some naturally when losing limbs. They are just stiff and it makes it look unrealistic when shooting limbs off."

**Nothing here has been run in Unity.** It compiles against the Unity reference assemblies and the animation rule tests pass (58, of which 17 are new). The pictures are offline renders of the animation rules, not game footage.

## Why they wandered

After a disarm, `MannequinCombat` ignored a freshly dropped gun for 0.8 s ("let it land"). With no gun to go for, the unarmed brain looked for cover, and where `CombatWorld.FindCover` found none (most planet ground) it set no destination, and `MannequinLocomotion` falls back to wandering between random points. So the first thing a disarmed enemy did was stroll off. It only sprinted when the gun was more than 4 m away, and the gun was simply in its hand on arrival.

## What changed

**Disarm (MannequinCombat, MannequinLocomotion, BodyAnimator)**
1. Startled beat, 0.55 s: the shoulders come up, the hands come up and in, the knees give, the trunk curls, a half step back; the head snaps to the gun and follows it; the body stays rooted (`Hold`) and turns slowly toward it.
2. Scramble: it goes for its own gun first (the nearest one otherwise), sprinting even the last few metres, bent low (about 30 degrees more lean), with quicker, shorter steps; eyes on the gun, with a nervous glance at the player now and then.
3. Snatch: within reach it stops and bends down (hips about 27 cm lower, trunk about 60 degrees forward, the reaching hand straight down to the ground, the other on its knee) and takes the gun at the bottom of the reach, 0.4 s later (0.35 s for a legless body, which stretches an arm out along the ground). That bend is the player's readable last chance: shoot the gun away (a gun moved more than 1.76 m cancels the snatch) or shoot the reaching arm. Arrival is checked every frame while scrambling, so a sprint never carries it past the gun.
4. Guns it cannot get to are left: more than 1.2 m above or below it (a ledge, a pit), or chased for six seconds without getting closer. A lookout on a tower does not leave its post; it stares at the gun below.
5. No hands, or no gun it can get: it runs for cover, and where there is none, eight metres straight away from the player (one place, kept until reached), looking back over its shoulder, but only while it can see the player or saw them within the chase memory. It never knows where the player is through a hill.

**Losing limbs (BodyAnimator, MannequinLocomotion)**
1. Losing an arm or forearm: for a few seconds (the shock fades over about 2.4 s) the trunk curls toward the wound, the head drops, the breathing is heavy and the good hand is pressed to the wound (a small arm IK), more loosely while it runs for its gun. A hand holding a gun stays on the gun. A slight lean toward the missing weight stays for good, and the arm that is left swings wider.
2. Losing a leg: a lurch first (arms flung up, trunk thrown forward), then the hobble.
3. The hobble is no longer a metronome: each hop has its own length (82 to 118% of the usual), now and then a bad landing holds it on the spot teetering with its arms flung out (10% of hops, 16% while scrambling), and rarely it stumbles (3%, 7% scrambling). These change where the body is, so they draw from a seeded stream per enemy, not Unity's random. It leans out over the good leg, hardest just after landing and more when the whole thigh is gone. What is left of the other leg swings on its hip like a pendulum, lagging the hop and overshooting.
4. Crawling and wriggling scramble faster after a gun and look at it.

**Less stiff everywhere**: a lean into turns, a lean with the head toward whatever it is looking at, breathing that gets heavier after running.

## Research: where motion could come from, and the licences

| Source | Licence | Can it ship in a commercial game? |
|---|---|---|
| CMU Graphics Lab Motion Capture Database (mocap.cs.cmu.edu; BVH conversion by B. Hahne) | "free for use in research and commercial projects worldwide"; may not be resold as data | Yes. Used here, for measurements only |
| Quaternius Universal Animation Library 1 and 2 (quaternius.com) | CC0 | Yes, no credit needed. 120+ clips on one humanoid rig: locomotion, crawling, deaths, combat. No limp, startle or pick-up listed |
| Mixamo (Adobe) | Free, royalty free in games; raw files may not be redistributed | Yes inside a built game, but this repository is public, so raw Mixamo files must never be committed |
| Ubisoft La Forge LAFAN1 | CC BY-NC-ND 4.0 | No (non-commercial) |
| Bandai Namco Research motion datasets 1 and 2 | CC BY-NC 4.0 | No (non-commercial) |
| AMASS | Non-commercial research licence | No |
| Unity Animation Rigging package | Unity Companion Licence | Yes in Unity projects; not needed (no Humanoid avatars, see below) |

Why measurements and not clips: the enemies have no Animator or Humanoid avatar; `BodyAnimator` builds every pose in plain C#, which is what lets limbs come off, any part of the body be missing, feet stay planted on round worlds and the body kinds share one skeleton at different sizes. Clips would need a Humanoid avatar per enemy (already flagged as a cost) and a clip for every combination of missing parts. So real motion was used where it is safest and cheapest: seventeen CMU clips were measured offline (`bvh_measure.py`, not committed) and the numbers were written into the rules.

| CMU clip | What it is | What was taken |
|---|---|---|
| 69_70, 69_73, 111_17 | squat and bend-over pick-ups | 0.7 s down and 0.6 s up calm (halved for a scramble); hips down to 0.38 leg lengths in a squat, trunk to about 105 degrees bending over (the game uses a mix: about 60 degrees and 27 cm) |
| 104_13 | stumbling walk | trunk thrown 40 to 65 degrees forward, arms flung 100 to 140 degrees up, about 1.5 s to recover |
| 49_18 | balancing on one leg | arms about 70 degrees out from the sides, trunk about 20 degrees forward, free knee about 95 degrees |
| 49_02, 83_40, 83_48 | hopping on one foot | about 0.55 s a hop, hips lifted about a sixth of a leg (0.14 m on this skeleton; was 0.20) |
| 91_24, 77_19, 77_20, 91_16 | hurt-leg walks and limps | a lasting 10 degree lean to one side, a slower, bent gait |
| 91_26 | hurt-stomach walk | one hand held to the body, a 6 degree lean toward it |
| 79_73, 91_32, 22_12, 91_59 | scared walk, stumbles into someone | checked, not used directly |

The startle follows the classic pattern described by Landis and Hunt (1939): shoulders up, elbows bent, trunk and knees flexed within about a tenth of a second, over within about half a second.

If hand-animated clips are ever wanted (for example a death or a taunt), the Quaternius libraries are the safe source; they would need the Humanoid route, which is a separate decision.

## Previews

`tools/blender/motion_dump` runs three scenes (disarm, hobble, arm) through the rules with simplified movement and writes the frames; `tools/blender/preview_enemy_motion.py` renders them with the common enemy model. Side-by-side GIFs (before on the left, after on the right) are in `docs/art/previews/enemy_motion_*.gif`. Not game footage: flat ground, no physics, the gun is a box, a shot-off part is just not drawn (no wound cap, no flying limb), and the game's gun-holding arms (posed by `MannequinCombat` in `LateUpdate`) are not shown.

## How it was tested

- Compile: all runtime scripts against UnityEngine.Modules 2021.3.33 and LethalCompany.GameLibs.Steam (only the three known IHittable name-clash errors from that library).
- `BodyAnimatorTests`: 58 pass, 17 new: startle flinches at once and settles; looking turns the head; reaching puts a hand near the ground with the feet left planted (both hands); scrambling is quicker footed, bent low, and planted feet still do not skate; losing an arm leans toward the wound with the other hand at it and a small lean that lasts (both sides); on one leg the head sits over the good leg (both sides); a bad landing throws the arms out and keeps the foot down; losing a leg lurches then recovers; every reaction together in every way of moving gives a sound pose.

## Still unverified (needs Unity on the Deck)

- How it looks and feels in a real fight, including with the gun-holding arms, on round worlds and with the new suited model.
- Whether the 0.55 s startle and 0.4 s snatch give the player a fair chance to deny the gun.
- The PlayMode tests in `WeaponTests` (`NoHands_CannotPickUpOrFire_ButStillMoves`, `UnarmedCharacter_FetchesANearbyGun`).
- Cost with 30 enemies (expected small: no allocations, a few more trig calls per enemy).

Playtest protocol (constitution principle 10): problem, enemies look stiff and wander after a disarm; hypothesis, a startle and a scramble make disarms read and feel like a win; smallest test, in the arena shoot the gun out of five enemies' hands and the legs off five more; acceptance, every disarmed enemy visibly flinches, looks at its gun and runs for it within a second, none wanders off, and a one-legged enemy never hops in a steady rhythm for more than a few hops.

## Agent review

Three reviews (unity-reviewer, game-designer, deck-performance), all reading the code only:

- **Unity reviewer.** No stuck state found (Hold, Reach, Scramble and LookAt are cleared on every way out, including death mid-grab and the player taking the gun), no event leak, no NaN. Fixed from its findings: the run-away chose a new point every think and so never arrived, and knew where the player was through terrain (now one point, and only while seen or recently seen); the head whipped between shoulders looking straight back (now keeps the same shoulder); guns on ledges or down pits were "reached" from far above or chased forever (height limit, six-second give-up); lookouts stood hunched for good (they stay put and look); the startle ran a think-tick long; the startle flinched toward the hand rather than where the gun flew; look angles were skewed on wide or narrow kinds (height scale only now). Left to check in Unity: the PlayMode test `NoHands_CannotPickUpOrFire_ButStillMoves` has less margin now that a disarm roots the body for 0.55 s.
- **Game designer.** The window from disarm to the next possible shot is about 2.7 to 3.0 s, about what it was (2.6 to 2.9 s), now with a visible story; fair and learnable. Taken: the snatch is 0.4 s (was 0.3), a legless body reaches visibly and takes 0.35 s (was 0.2 with no cue), arrival is checked every frame, the hand stays on the wound at 40% while scrambling, the hobble's randomness is seeded. Noted: the two-hands-up startle could read as surrender if surrender is ever a mechanic.
- **Deck performance.** No allocations; about 10 to 30 microseconds more for 30 enemies (counted, not timed). The line in `docs/performance_budget.md` is updated.
