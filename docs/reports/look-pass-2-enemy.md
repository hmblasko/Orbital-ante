# Look pass 2: the enemy (model and animation)

13 Nov 2026. Nothing in this report was run in Unity. Pictures are Blender renders of the game's model posed by the game's own animation rules; they show shape, paint and pose, not the game's shader.

Matthew asked for this on 13 Nov 2026: "the current version of the animations of enemies are not good. They need to be significantly better. The models needs to be better too", and "I like the gibbing and limbs shooting off of the enemies, but the model really needs to be refined". This brings forward Milestone 12's "one proper enemy model built on the dismemberment system". It adds no faction and no Phase 2 feature.

## What changed

**Model.** The capsule mannequin is replaced by an original suit raider made in Blender by `tools/blender/build_enemy.py` (`Resources/Models/Enemy.bytes`, about 14,850 triangles, no textures). It has the same 14 breakable pieces as before. Cream helmet with a wide amber visor, a rust rebreather snout, a crest, an aerial and a lamp; teal cloth suit with a quilted belly; cream chest shell with a control box; a backpack with two tanks and a bedroll; brass joint rings; ribbed rubber bellows at elbows and knees; cream gauntlets and shin guards; mustard mitts; big boots with rust toe caps. The left shoulder has a guard and the right thigh a strap, so the two sides differ. Every piece starts with a ball over its joint so no gap opens when it bends. The red ball wound caps are replaced by a torn stump with a bone end.

**Animation.** The old motion was one sine wave swinging straight limbs. The new rules (`BodyAnimator`, plain C#, no stored clips) work from the ground up:

- Each foot follows a path over the ground worked out from how fast the body really moves and turns, and the legs are bent to reach it. A planted foot stays planted: no skating, walking, running, curving or turning on the spot (it now steps round).
- The pelvis bobs, sways and twists over the feet; the shoulders twist the other way; arms swing against the legs with the elbows bending; the head stays level.
- Running leans forward, kicks the heels up, pumps bent arms and leaves the ground between steps. Speeding up and slowing down lean the body.
- Standing still it breathes, shifts its weight from leg to leg and glances about. Each enemy has its own timing and stance, so a group does not move in step.
- Aiming, it drops into a bladed stance, gun-side foot back, knees bent.
- Being shot knocks the body about on springs, away from the shot, more for a hit high up, a knee buckle for a low one, and much more when a limb comes off. The gun's kick goes through the shoulder into the body. Knocks are limited so automatic fire cannot wind the body up.
- One leg: crouch, spring, tuck, land, with the arms out for balance. Crawling: reach fast, haul slow, in time with the pulls that move it. Wriggling: hump and lunge.
- Losing a leg blends into the hop or the crawl over a fraction of a second instead of snapping.

**Rules that changed.** Hit points, speeds, gib rules and the head-only death rule are unchanged. Hitboxes were re-fitted to the chunkier model, which makes the enemy slightly easier to hit:

| Part | Before | Now |
|---|---|---|
| Head (ball radius) | 0.15 | 0.17 (the helmet is 0.18) |
| Torso (capsule radius) | 0.19 | 0.215 |
| Upper arm | 0.0625 | 0.085 |
| Forearm | 0.0525 | 0.066 |
| Hand (ball radius) | 0.07 | 0.082 |
| Thigh | 0.0825 | 0.10 |
| Shin | 0.065 | 0.076 |
| Foot | 0.05, 0.28 long | 0.058, 0.32 long |

Known gap: the backpack, snout, crest and shoulder guard stick out past their hitboxes, so a shot that only clips those passes through.

## Why, and which principle

Principle 1 (fun core gameplay) and Milestone 12's bar that limb pops, disarms and leg shots "read instantly": each breakable piece is now its own block of colour, the gun hand is bright, and the body reacts to every hit. Principle 3 (learnable challenge): hitboxes follow what is drawn; flinches change nothing in the rules and do not spoil the enemy's aim.

## Tested

- 934 rule tests pass with `dotnet test` (30 new). New ones check: a planted foot stays within 3 cm walking, 5 cm running, curving and turning on the spot; legs always reach the ground and never go through it; an idle enemy's feet do not creep while its body moves; a knock rocks the head away from the shot and dies away; a change of movement never jumps the head; every state gives clean rotations under hostile input; two enemies do not move in step; the model's pieces sit on the skeleton's joints, each has a ball over its joint, the boots stand on the ground, and the triangle cap holds.
- The whole game compiles against Unity reference assemblies.
- Pictures: `docs/art/previews/enemy_model.jpg`, `enemy_poses.jpg`, `enemy_walk.gif`, `enemy_run.gif`.

## Agent review

- **unity-reviewer:** no certain first-Play breaker. Confirmed by hand that the rotation maths matches Unity's, that the gib path still works with the new pieces, and that no PlayMode test should fail. Used: hitboxes fitted per part instead of a flat 14 percent; the helmet rocks on its neck ring in the head-only state instead of rolling through the floor; turn rate capped; prone height raised; budget and ledger entries added. Not done: the mitt is longer than the grip maths assumes, so fingers may poke through the gun.
- **deck-performance:** no per-frame garbage; animation cost is small. Used: shadow copies are only made where a sun casts real shadows (the arena); budget lines and a cap of 12 full-detail enemies until a far copy exists. Not done: the far copy (about 3,000 triangles), and a shadow pass in the game's own shader to remove the copies.
- **game-designer:** no faction and no Phase 2 creep. Used: each breakable piece colour-blocked (cream gauntlets and shin guards, mustard mitts), wider backpack, bigger visor, the ear cups no longer read as eyes, smoother helmet, glances halved, knocks limited and the head damped harder, hitbox sizes listed here. Not done: a still stouter silhouette; glances driven by what the enemy has noticed.

## Still unverified (needs Unity on Matthew's machine)

- How any of it looks and feels in the game: the shader, the motion in play, the flinches against hitstop, the stance while holding a gun.
- PlayMode tests (not runnable here).
- Frame time with 8 enemies and shadows on the Deck.
- Whether fingers poke through the gun, and whether the chest or helmet clip the ground when crawling or as a head.
