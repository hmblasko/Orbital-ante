# The Drifter: a new enemy with a jointed body

19 Nov 2026. Branch `claude/ragdoll-enemy`, pull request into `claude/playtest`.

**Nothing in this report was run in Unity.** The rules were tested in the cloud (2,556 rule tests pass), the Unity-side code was compiled against reference assemblies, and the pictures and the clip are offline previews, not game footage. The PlayMode tests written for this work have never been run.

## What Matthew asked for

15 Nov 2026: "I really want a new enemy character to implement. For now, I want a very realistic ragdoll, maybe even from a ragdoll simulator. The ragdoll should have soft limits with tension. The art style can be generally consistent with the current art style but should be more refined than the current enemies. It should not look like Roblox. It should have all the same gibbing mechanics and animations."

16 Nov 2026: "Is there anyway you can make the enemies look less cheap? Like I want something that isn't just cheap looking blender assets".

## What changed

### 1. Bodies and limbs now have joints (the ragdoll)

Until now, whatever was shot loose (a dead body, an arm, a leg with its shin and foot) flew off as one stiff piece, frozen in the pose it had. A dead enemy fell over like a statue.

Now, for a jointed kind of enemy, every part below the top of a loose piece has its own weight and hangs from the part above by a joint:

- **Ranges like a person's.** Each joint has a range about three directions (for the shoulder: forward and back, out to the side, and twisting). Knees and elbows are hinges and never bend backwards. The numbers are ordinary human ranges, widened a little where the game's own running animation goes further.
- **Soft limits.** The end of a range is a spring, not a wall. A limb that swings into its stop pushes into it and is pushed back, harder the further it goes. Heavier, longer limbs get stiffer stops so a thigh and a hand feel alike.
- **Tension.** Each joint is pulled weakly toward the middle of its range, the way a limp limb settles, and has drag. The drag starts thick and goes limp over the first half second, so a body gives way instead of dropping like a puppet with cut strings.
- **Folds round the hit.** The part the bullet struck gets its own knock, so the body bends at the hit.
- **Everything else is the same.** What comes off, when, the stumps, the blood, the pops, the fades, the caps, and shooting a loose piece apart again all work as before: the joints are added on top of the same pieces. A jointed arm shot at the elbow splits, and the hand still hangs from the forearm.
- **Living enemies are unchanged.** They still walk, hunch, startle, scramble and crawl by the same animation. Only what has been shot loose, and the dead, are jointed.

To keep the Deck's frame rate, only three loose pieces are jointed at once. A piece goes stiff again (in the pose it has landed in) once it has lain still for a second, or after 12 seconds, or when a newer one needs its place.

Files: `Dismemberment/RagdollRules.cs` (the rules, plain C#), `Dismemberment/GibRagdoll.cs` (the Unity side), small changes in `Gib`, `Mannequin`, `GibManager`, `BodyPart`, `MannequinConfig`.

**On "maybe even from a ragdoll simulator":** no ragdoll package or simulator asset was used. The joints are Unity's own physics joints, set up by our code, because every package found would either replace the gib system (the ask was to keep it) or could not sit in the public repository. The ranges are ordinary human joint ranges.

### 2. A new enemy: the Drifter (placeholder name, no faction)

- A fifth unaligned kind. Slightly taller than the trooper (1.05), a little faster on its feet (1.15), carries the pistol, and takes cover and flanks like the trooper. It stands further back than the trooper (10 to 24 m, between the trooper and the marksman), waits longer before its first shot (0.85 s against the trooper's 0.6) and then aims straighter (2.2 degrees against 3). The pause is the tell. Toughness is the same as the trooper's: no inflated health. These numbers are first guesses.
- It is the only kind with a jointed body for now, so the two can be compared side by side in the same fight. One tick box turns joints on for every kind (below).
- Its model is new: a high-altitude pilot's pressure suit with a survival vest, built by `tools/blender/build_enemy_drifter.py`. A close white helmet with a wide dark visor and a breathing hose to a green regulator on the chest, a dark vest with pouches and a parachute harness over a tan suit, restraint cords and lacing on the limbs, two small oxygen bottles and a seat kit on the back instead of the trooper's big pack, tall laced boots. Same skeleton, same fourteen breakable pieces, so every gib, stump, hitbox and animation works on it.
- 21,512 triangles (the trooper has 21,176), with a far copy of 4,585.

### 3. "Less cheap": an honest answer

The pictures show the Drifter beside the trooper. It has more worked detail, but it is still built the same way and drawn the same way, and to my eye it is a modest step, not the jump Matthew is asking for.

The main reason the enemies look like cheap Blender models is not their shapes. It is that the game draws one flat colour per corner and has **no textures**: no cloth weave, no stitching, no scuffs, no fine shading in the folds. The pictures ending `_textured` show the same two models with that kind of surface detail worked in, to show how much it adds. The game cannot draw that today. Reading textures in the game is owned by the thread "Remake enemies and guns realistically", so it was not duplicated here; the helper that made these pictures (`tools/blender/oa_materials.py`) is there for that thread to use.

There are two routes to a real jump, and they can be combined:

1. **Textures on our own models** (the remake thread's plan). No cost, safe for the public repository, everything stays under our control.
2. **A professionally sculpted model**, cut into our fourteen pieces. Options found, nothing bought:

| Model | Price | Licence | Can it go in the public repository? |
|---|---|---|---|
| "Mercenary Astronaut" by Peter_D (Sketchfab) | Free | CC BY 4.0 (credit required) | Yes |
| "Sci-Fi Astronaut" by CG Cobble Games (Unity Asset Store) | $20 | Asset Store EULA | No |
| "Astronaut Soldier" by Maksim Bugrimov (Unity Asset Store) | $19.99 | Asset Store EULA | No |
| "Modular Soldier Future Sci-Fi Character Vol. 1" by Temka1a (Unity Asset Store) | $49.90 | Asset Store EULA | No |

The Asset Store's licence lets an asset ship inside a game but not sit in a public repository, so any of the paid three would mean making the repository private or keeping those files out of git. The free one is sculpt quality (about 700,000 triangles with 4K textures), so it would need slimming and baking down for the Deck, and it also needs texture support in the game. The cloud workspace cannot reach Sketchfab, so Matthew would have to download it and upload it here.

Links: sketchfab.com/3d-models/mercenary-astronaut-7fd399d488ae4576a0fc62715892956c, assetstore.unity.com/packages/3d/characters/sci-fi-astronaut-hdrp-urp-built-in-rigged-200136, assetstore.unity.com/packages/3d/characters/humanoids/sci-fi/astronaut-soldier-200676, assetstore.unity.com/packages/3d/characters/humanoids/modular-soldier-future-sci-fi-character-vol-1-307000. Prices as listed on 19 Nov 2026.

## Why (constitution)

- Principle 1, fun core gameplay: a kill that crumples and a limb that flops are the pay-off of the game's main verb.
- Principle 3, learnable challenge: the new kind differs in size, gun and outline, not in hidden health.
- Principle 7, cognitive load: the Drifter's white helmet and dark vest are meant to be told from the trooper's white suit and gold visor at a glance.
- Principle 10, evidence: the problem (stiff bodies), the hypothesis (joints with soft stops and tone read as a body, not a statue), the smallest test (one kind jointed, the others not, in the same fight) and what would count as passing are below.

## What was tested, and how

- `dotnet test tools/rules-tests`: 2,556 pass, 1 skipped (the usual one). New: `RagdollRulesTests` (13 tests: ranges, left and right mirrored, hinges, the animator's poses stay near every range, the stop is a spring, a joint settles, the brace fades, the budget) and the model tests now cover `EnemyDrifter` and `EnemyDrifterFar` (skeleton, hitbox fit, far copy, triangle caps).
- The Unity-side code compiles against Unity reference assemblies (an older Unity's, with Unity 6 names mapped back). This catches spelling and types, not behaviour.
- `tools/ragdoll_preview` drops a body using the same rule numbers in its own small physics. That is where the clip comes from. It is not Unity's physics.

## What is not verified

1. **Nothing was run in Unity.** In particular, how Unity's joint measures its range when it is made is the least certain part. The code turns each part to the middle of its range before making the joint so the limits are equal both ways; if Unity does not take its reference at that moment, joints would be bent the wrong way. The PlayMode test `AJointBendsOnlyWithinItsRange` is there to catch exactly that.
2. Whether the spring and drag numbers feel right in Unity. They are first guesses, sized from each limb's weight and length.
3. The frame-time cost (guessed 0.15 to 0.4 ms a physics step for three whole bodies falling; see `docs/performance_budget.md`).
4. Fewer blood splats may come from a jointed limb's bounces than from a stiff one's, because the bounce is now felt by the part, not the piece as a whole. Not looked at.
5. The Drifter's shine and colours under the game's shader. The pictures are Cycles, not the game.
6. Adding a fifth kind changes which kinds stand in which camp for old seeds.

## Not built

- A living enemy is never a ragdoll. Being knocked down, shoved or run over still plays the animation. A "powered" ragdoll for the living (falling and getting up through physics) would be the next step if the dead ones feel right.
- No new sounds. No gun of its own (it carries the pistol).
- Textures in the game (see above).

## For Matthew: seeing it on the Deck

1. In a terminal in `~/Orbital-ante`: `git fetch`, then `git checkout claude/playtest`, then `git pull`. (This only has the new enemy once the pull request is merged into `claude/playtest`.)
2. Open the project in Unity and wait for it to finish importing.
3. In the top menu choose **Orbital Ante > Set Up Project** once, so the Enemies asset picks up the new kind.
4. Press Play and fight at any camp. The Drifter is the one in the tan suit, dark vest and white helmet with the black visor. About one enemy in six is one.
5. Shoot one in the head, and shoot an arm or a leg off one. Compare with the same shots on a trooper, which still falls stiff.
6. To make every kind jointed: in the Project window open `Assets/OrbitalAnte/Resources/Config`, click the **EnemyConfig** asset, and tick **Jointed Bodies For All** in the Inspector.
7. To change the feel: the same folder's **MannequinConfig** asset, section **Ragdoll**. `Limit Hz` is how hard the stops are, `Tone Hz` how strongly a limb is pulled back to rest, `Limp Damping` how floppy, `Brace Seconds` how long a body takes to give way.
8. To run the new tests: **Window > General > Test Runner > PlayMode**, then run `RagdollTests`. If any fail, the names and messages are what I need.

## Playtest protocol (for when it runs)

- Problem: bodies and limbs fall as statues. Hypothesis: jointed, soft-stopped, toned bodies read as bodies and make kills funnier without costing frame rate.
- Smallest test: ten Drifter kills and ten trooper kills at one camp on the Moon and one on Earth, plus five arms and five legs shot off each.
- Passing: no knee or elbow ever bends backwards; no limb stretches off its joint or jitters on the ground; a body is at rest within about three seconds; the F3 Budget line stays OK in a fight with three bodies falling.

## Agent review

Codex could not review the pull request (its usage limit for reviews was reached). The three specialist agents read the work instead. None of them ran anything in Unity.

- **game-designer:** the Drifter's numbers were all within about 15% of the trooper's, and its only real differences (a faster, straighter shot) were hidden ones. Changed: it now stands further back (10 to 24 m), waits 0.85 s before its first shot and keeps the straighter aim, so where it stands and its pause identify it. The report now answers the "ragdoll simulator" part of the ask. Nothing drifted from the build prompt or the constitution.
- **deck-performance:** parts of one body colliding with each other could keep it from resting (fixed for neighbouring parts; the rest still touch, see the budget row); the kill frame was guessed too low (now one jointed chunk a frame, and the row says 1 to 3 ms); joint passes lowered from 12 to 8; the budget rows were corrected, including the per-part pull, which was kept because a change of the point of view rewrites every mover.
- **unity-reviewer:** a re-centre could move a jointed limb twice after a chunk was shot apart (fixed in `SolarSystem.UpdateFrame`); the soft stop was soft enough for a knee to bend visibly backwards on a hard hit (`limitHz` raised from 5 to 9); a piece shot off a jointed chunk took the wrong starting speed (fixed). It judged the joint set-up (the reference pose, child rigidbodies, the springs and the drive) likely right, and said what to check first in Unity: the `RagdollTests`, whether a dead Drifter's thighs jitter and whether it rests within about two seconds, and shooting a jointed arm apart and then flying far enough to force a re-centre.
