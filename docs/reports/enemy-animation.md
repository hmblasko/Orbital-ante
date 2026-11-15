# Enemy animation: startle, scramble, hobble, lean (15 Nov 2026)

Matthew, 15 Nov 2026: "See how you can improve the enemy animations. See about researching open source animations that make their movement look realistic. When they drop weapons, they simply walk around and that doesn't look good. They should look startled when dropping the weapon and scramble to get it back. They should also have a more interesting hobble when losing limbs. They should also lean some naturally when losing limbs. They are just stiff and it makes it look unrealistic when shooting limbs off."

**Nothing here has been run in Unity.** It compiles against the Unity reference assemblies and the animation rule tests pass (60, of which 19 are new). The pictures are offline renders of the animation rules, not game footage.

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
1. Losing an arm or forearm: the hit spins that shoulder back (about 26 degrees) and doubles the trunk over, gone within about a second, and the head turns down to look at the stump for most of a second before looking ahead again (`Wounded(side, severity, arm: true)`). For a few seconds (the shock fades over about 2.4 s) the trunk curls toward the wound, the head drops, and the breathing is heavy. The good arm is thrown out a little for balance as the hit spins the body; it does not reach across to the wound (Matthew asked for that to go after the second arm preview). A slight lean toward the missing weight stays for good, and the arm that is left swings wider.
2. Losing a leg: a lurch first (arms flung up, trunk thrown forward), then the hobble.
3. The hobble is no longer a metronome: each hop has its own length (82 to 118% of the usual), now and then a bad landing holds it on the spot teetering with its arms flung out (10% of hops, 16% while scrambling), and rarely it stumbles (3%, 7% scrambling). These change where the body is, so they draw from a seeded stream per enemy, not Unity's random. It leans out over the good leg, hardest just after landing and more when the whole thigh is gone. What is left of the other leg swings on its hip like a pendulum, lagging the hop and overshooting.
4. Crawling and wriggling scramble faster after a gun and look at it.

**Less stiff everywhere**: a lean into turns, a lean with the head toward whatever it is looking at, breathing that gets heavier after running.

## Walking and running (second request, 15 Nov 2026)

Matthew, after the arm preview: "Much better. Also, fix the walking generally. It's way too rigid and unnatural. Make it look better."

**What was wrong, measured.** The plain walk was compared with CMU walks and runs on the same definitions (`tools/gait_measure`). At the trooper's walk (2.0 m/s) it took 1.75 strides a second of 1.14 m, where people take about 1.1 a second of about 1.8 m: quick, short, sewing-machine steps. The planted knee sat at one bend the whole time and never straightened, so the body moved like it was on stilts with bent knees. The feet barely left the ground (7 cm), there was no roll from heel to toe, the thigh never went behind the hip, and the run was a high-kneed jog at nearly twice the real step rate.

**What changed (all in `BodyAnimator`, plain C#):**
1. Strides follow measured lengths (`StrideLength`): walking 1.25 times the square root of the speed in leg lengths a second, running half a leg plus 0.6 of the speed, levelling off past a fast run. Time on the ground per stride (`Duty`) is 61% walking and about 35% at a jog down to a fifth at a sprint. Past about 2.6 leg lengths a second a body runs whatever its kind's own walk speed says.
2. The planted leg is nearly straight (knee about 10 degrees), gives about 15 degrees as the weight comes on after the heel strike, and straightens again. The pelvis rises over the planted leg and dips between steps from the legs themselves, not from a sine wave.
3. Heel to toe: the heel strikes with the toes up, the foot goes flat, and from 60% of the way through the heel lifts and the foot rolls over the ball (`Rolled` moves the ankle about the ball or the heel, so the contact point stays put).
4. The swing is one smooth curve that leaves the ground moving the way the rolling foot was moving and lands moving back at the ground's speed, so neither toe-off nor heel strike jerks or slides; the heel kicks up behind (higher and sooner running) and the knee folds about 60 degrees walking. A leg in the air eases out near full stretch (`SoftReach`) instead of locking straight in one frame, and only pulls the hips down as its heel comes down.
5. The pelvis turns about 7 degrees each way and the hip over the swinging leg drops about 4 degrees; the shoulders turn against it. The arms swing more the faster it walks, a little more behind than in front; running, the elbows bend to about 100 degrees and the arms pump mostly behind.
6. The kinds: the Runner's stride 1.12 to 1.0 (busier legs), the Bruiser's 0.9 to 1.18 (long, slow, heavy strides), the Marksman's 1.05 to 1.15 (gliding). Cadence at walk and sprint: trooper 1.25 and 1.46 strides a second, Runner 1.40 and 2.02, Bruiser 0.83 and 1.14, Marksman 0.94 and 1.21.

**Measured against CMU after the change** (trooper; people in brackets): at 1.2 m/s, 1.48 leg lengths a stride (1.50 to 1.59), knee 15 to 69 degrees (9 to 72), hip -4 to 41 (-17 to 32); at 2.0 m/s, 1.91 (1.76), knee 9 to 69 (16 to 67), hip -21 to 43 (-16 to 34); running at 4.6 m/s, 3.79 legs a stride at 1.46 a second (2.86 at 1.45 for a slower runner), knee 8 to 104 (0 to 116), hip -24 to 53 (-35 to 41). The run still lifts the knee a little high and does not drive the leg as far behind as a real runner. The knee's quickest change from one frame to the next is now 8 to 16 degrees walking and up to about 21 running, against 6 to 12 and about 11 for people (99th percentile): close for the walk, still a little brisk for the run (20).

Rule tests changed with it: running takes "more than 12 steps in 5 s" (was 15) and sprinting turns the legs over "more than 1.1 times" as fast as walking (was 1.3), because people running take longer strides as well as quicker ones (measured: 1.15 to 1.35 times). Everything else passes unchanged, including feet that do not slide, kinds that never squat to reach their stride, and every reaction in every way of moving.

**Agent review of the walk.** Unity reviewer (code and a frame-by-frame probe at 60 and 30 frames a second, every kind, 0 to 2.3 times walk speed and turning on the spot): no NaN, joins continuous, planted feet slide less than before and no longer dip below the ground; it found the foot snapping up at toe-off (4.7 cm and 22 degrees of knee in one frame walking, 14 cm and 50 degrees running) and the knee being twitchy with very straight legs. Fixed: the smooth swing curve above, a 7 mm margin in `SinkToReach` and the eased reach; now about 2 to 4 cm and 15 to 22 degrees a frame, which is the swing's own speed. Game designer: the Bruiser at 0.78 strides a second could read as slow motion and be mistaken for Deep Breath, so its stride went from 1.25 to 1.18; the Runner should look busy (1.12 to 1.0); speed and aim, not cadence, carry the threat. Deck performance: about 16 to 20 more trig calls and one or two `Pow` per enemy, an estimated 5 to 15 microseconds for 30 enemies (counted, not timed), no garbage; budget line updated.

Still unverified: all of it in Unity; how the walk looks on the realistic trooper model and the other kinds at Deck size; the run's knee speed.

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
| 02_01, 07_01, 08_01, 16_15, 35_01 | ordinary to brisk walks | stride length against speed, time on the ground, knee, hip, pelvis, arm and foot curves through the stride |
| 09_01, 35_17 | runs and jogs | the same for running |

The startle follows the classic pattern described by Landis and Hunt (1939): shoulders up, elbows bent, trunk and knees flexed within about a tenth of a second, over within about half a second.

If hand-animated clips are ever wanted (for example a death or a taunt), the Quaternius libraries are the safe source; they would need the Humanoid route, which is a separate decision.

## Previews

`tools/blender/motion_dump` runs three scenes (disarm, hobble, arm) through the rules with simplified movement and writes the frames; `tools/blender/preview_enemy_motion.py` renders them with the common enemy model. Side-by-side GIFs (before on the left, after on the right) are in `docs/art/previews/enemy_motion_*.gif` (disarm, hobble, arm, walk, run). Not game footage: flat ground, no physics engine (a cut-off limb flies on a simple thrown path with a few bounces, standing in for the game's gib physics), no blood, the gun is a box, and the game's gun-holding arms (posed by `MannequinCombat` in `LateUpdate`) are not shown.

Matthew, 15 Nov 2026: "The first two animation demos look great. Redo the arm shoot off one." The first arm preview was seen from the wrong side, far away, and the arm simply vanished. The redo: the camera is in front on the wounded side and closer, the arm flies off as one piece and bounces, the model's wound stump shows at the shoulder, and the reaction is stronger (the spin back, the doubling over, the look at the stump). Matthew then asked (same day) to keep the look at the stump but drop the good hand reaching over to the wound: the hand-on-wound arm IK is gone, and the good arm is thrown out for balance instead.

## How it was tested

- Compile: all runtime scripts against UnityEngine.Modules 2021.3.33 and LethalCompany.GameLibs.Steam (only the three known IHittable name-clash errors from that library).
- `BodyAnimatorTests`: 60 pass, 19 new (two added with the redone arm preview: the hit turns the chest toward the lost arm and the head to the stump, then it looks ahead again, checked to fail without the change; and the good hand stays on its own side for two seconds after the loss): startle flinches at once and settles; looking turns the head; reaching puts a hand near the ground with the feet left planted (both hands); scrambling is quicker footed, bent low, and planted feet still do not skate; losing an arm leans toward the wound, the other hand stays on its own side, and a small lean lasts (both sides); on one leg the head sits over the good leg (both sides); a bad landing throws the arms out and keeps the foot down; losing a leg lurches then recovers; every reaction together in every way of moving gives a sound pose.

## Still unverified (needs Unity on the Deck)

- How it looks and feels in a real fight, including with the gun-holding arms, on round worlds and with the new suited model.
- Whether the 0.55 s startle and 0.4 s snatch give the player a fair chance to deny the gun.
- The PlayMode tests in `WeaponTests` (`NoHands_CannotPickUpOrFire_ButStillMoves`, `UnarmedCharacter_FetchesANearbyGun`).
- Cost with 30 enemies (expected small: no allocations, a few more trig calls per enemy).

Playtest protocol (constitution principle 10): problem, enemies look stiff and wander after a disarm; hypothesis, a startle and a scramble make disarms read and feel like a win; smallest test, in the arena shoot the gun out of five enemies' hands and the legs off five more; acceptance, every disarmed enemy visibly flinches, looks at its gun and runs for it within a second, none wanders off, and a one-legged enemy never hops in a steady rhythm for more than a few hops.

## Agent review

Three reviews (unity-reviewer, game-designer, deck-performance), all reading the code only:

- **Unity reviewer.** No stuck state found (Hold, Reach, Scramble and LookAt are cleared on every way out, including death mid-grab and the player taking the gun), no event leak, no NaN. Fixed from its findings: the run-away chose a new point every think and so never arrived, and knew where the player was through terrain (now one point, and only while seen or recently seen); the head whipped between shoulders looking straight back (now keeps the same shoulder); guns on ledges or down pits were "reached" from far above or chased forever (height limit, six-second give-up); lookouts stood hunched for good (they stay put and look); the startle ran a think-tick long; the startle flinched toward the hand rather than where the gun flew; look angles were skewed on wide or narrow kinds (height scale only now). Left to check in Unity: the PlayMode test `NoHands_CannotPickUpOrFire_ButStillMoves` has less margin now that a disarm roots the body for 0.55 s.
- **Game designer.** The window from disarm to the next possible shot is about 2.7 to 3.0 s, about what it was (2.6 to 2.9 s), now with a visible story; fair and learnable. Taken: the snatch is 0.4 s (was 0.3), a legless body reaches visibly and takes 0.35 s (was 0.2 with no cue), arrival is checked every frame, the hand stayed on the wound at 40% while scrambling (since removed at Matthew's request), the hobble's randomness is seeded. Noted: the two-hands-up startle could read as surrender if surrender is ever a mechanic.
- **Deck performance.** No allocations; about 10 to 30 microseconds more for 30 enemies (counted, not timed). The line in `docs/performance_budget.md` is updated.
