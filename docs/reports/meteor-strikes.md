# Meteor strikes (16 Nov 2026)

Matthew asked: "Have random meteors hit planets (somewhat regularly but not too regularly). Have them break naturally into pieces in a satisfying way and leave impacts and explode."

**Nothing here has been run in Unity.** It compiles against reference assemblies, and the rules are covered by tests that run without the engine. How it looks, sounds and feels, and what it costs on the Deck, are all unverified until it is played.

## What a strike is

1. **When.** Strikes only come down on the world you are on or flying low over (below 600 m). They never come in a cave or underwater.
   - None fall in the first 90 s of a run, and none in the first 45 s after you arrive at a world.
   - After that, the gap between strikes is at least 45 s, usually about 110 s, and never more than 240 s. Most gaps fall near the usual one, with a few short and a few long, so you can't time them.
   - Each world has its own rate: Earth 0.8, Mars 1.1, the Moon and Mercury 1.3 (airless worlds get hit more often), Phobos and Deimos 0.7. A world not on the list has rate 1, and a rate of 0 switches a world off. Stars and gas giants never get strikes.
   - The waits are not taken from the seed, so restarting with the same seed doesn't tell you when the first rock will fall. Where rocks fall does come from the seed.
2. **Where.** Strikes land 45 to 240 m from you: usually wide, sometimes a near miss 22 to 34 m away, and sometimes in among enemies within that range.
   - Rocks never land on you or close to the launch pad, the lander, the rover, a base, a cave mouth or skylight, or the sea.
   - Every piece's landing point is checked, not just the aim point. If a piece would land somewhere it shouldn't, the fall is planned again; after four tries the strike is skipped.
3. **The fall.** The whole fall is worked out ahead of time on a background thread: air drag, the world's own pull, and the rock coming in at 30 to 65 degrees. Every fall lasts at least 4.5 s, so even on the small moons there is time to see it.
   - **On worlds with air**, the rock glows white-hot with a flame tail and a smoke trail. The biggest rock lights the ground under it orange as it comes down, so you can see a strike coming on the ground around you whichever way you face.
   - **On airless worlds**, you see a dark, sunlit rock with a faint streak. Nothing burns.
4. **Breaking up.** On worlds with air, 4 in 5 rocks burst between 120 and 320 m up, with a crack and a flash, into 3 to 5 pieces of different sizes. The biggest piece may burst again (at most 7 pieces in all). The pieces fly apart naturally and land within about 50 m of each other. Airless rocks land whole.
5. **The impact.**
   - Each landing gives a flash, a fireball (with air), a ring of dust rolling out across the ground and a column thrown up, and hot grit in high arcs. In vacuum the dust flies in clean arcs and falls straight back.
   - Real rocks are thrown out and stay lying around the crater, glowing as they cool.
   - Smoke keeps rising for about 18 s.
   - You hear the boom when the sound would reach you: in air, sound travels at 343 m/s, so a strike 300 m away is heard almost a second late.
6. **The crater.** Each crater lasts the run, up to 24; after that the oldest goes first.
   - The ground can't be dug, so a crater is a shape laid over the ground: a raised, lumpy rim, a floor that glows red and cools over 25 s, and an edge that sinks under the ground so there is no seam.
   - On worlds with air it is scorched with soot streaks. On airless worlds it is dark melt with pale rays.
   - It uses the ground tiles' own material, so it takes the same light, grit and distant colour as the ground around it. It is solid, so you and the rover ride over the rim, and enemies walk over it.
7. **Damage.**
   - In the scorched middle (0.6 of the crater's radius) everything dies.
   - Further out, the damage falls off. Enemy limbs come off well before the edge, and enemies are thrown back. Loose limbs, dropped guns and the rover are thrown outward and up. The lander's parts take a knock.
   - Your suit takes up to 140 at the very centre. A big rock kills you only within about 5 m of where it lands, and the death screen says "Caught in a meteor strike."
   - Meteor kills don't count as yours: they give no gun ladder steps and no Focus.
   - The blast ignores cover, because it is a shock through the ground.
8. **Shake.** The view shakes with strength and distance, in the seat and on the rover too.

## Sounds

These are new and made from code (`tools/audio/sfx/meteor.py`):
- `meteor.roar`: a loop that follows the biggest falling rock.
- `meteor.breakup`: a crack, then thunder (2 versions).
- `meteor.impact`: a deep punch, crunch and falling gravel (3 versions).
- `meteor.splash`.
- `meteor.debris`: a knock and skitter when a thrown rock lands (4 versions).

Audio QA passes (0 failed checks, 5 warnings). The impact versions carry 26 to 32% of their energy in the mids so the Deck's speakers carry them. These are measurements only. Nobody has listened to them.

## Dials

Everything is on `Assets/OrbitalAnte/Resources/Config/MeteorConfig`, which "Set Up Project" creates. The main dials:
- **Timing:** Enabled, Grace Seconds, Arrival Grace, Min / Mean / Max Gap, and Rates (per world).
- **Aim:** Max Distance, Near Miss Chance, Enemy Aim Chance.
- **The rock:** Warning Seconds, Min / Max Crater, Air Breakup Chance.
- **The blast:** Lethal Core, Player Damage.
- **Caps:** Max Craters, Max Chunks, Max Particles.

## How to try it (Matthew)

1. In a terminal: `cd ~/Orbital-ante`, then `git fetch`, `git checkout claude/playtest`, `git pull` (once the pull request below is merged).
2. Open the project in Unity and wait for it to finish compiling.
3. On the top menu, click **Orbital Ante > Set Up Project**. This creates the new `MeteorConfig` asset.
4. Press Play and stay on Earth near the pad. Press **F3**: the line starting "Meteors:" counts down the seconds to the next strike (the first one usually comes 3 to 5 minutes in), then says "falling".
5. To see strikes quickly: stop Play, open `Assets/OrbitalAnte/Resources/Config/MeteorConfig` in the Project window, set **Grace Seconds** and **Arrival Grace** to 0 and **Min Gap / Mean Gap / Max Gap** to 5 / 8 / 12, then press Play again. Set them back afterwards (or delete the asset and run Set Up Project again).
6. Try it on the Moon too (no air: no fire, clean dust arcs, pale rays).

## Agent review

Three specialist reviews read the code: Unity, design and Deck performance. What they found, and what was done:

- **Fixed: pieces could land on the player or near the pad, ship or a base.** Only the aim point was checked, and a burst piece can drift about 50 m.
  - Every landing is now checked, the fall is replanned if any piece breaks a rule, and the strike is skipped after four tries.
  - Pieces fly apart more gently (top spread speed 13 cut to 8).
  - Pad and base clearances now include the widest crater's apron. The rover is kept clear too.
- **Fixed: strikes were too frequent for "not too regularly"** (about once a minute on the Moon). Gaps went from 30/75/170 s to 45/110/240 s, plus a 45 s grace on arriving at each world. On the Moon that is about one strike every 85 s.
- **Fixed: the warning only worked if you happened to look up.** The fall now takes at least 4.5 s (was 3.2), and on worlds with air the falling rock lights the ground around you. On airless worlds the only cues are still the sight of the rock and its roar (see below).
- **Fixed: aiming.**
  - Rocks aimed less often at enemies (35% cut to 20%), and only at enemies within range, so a camp a long way off is never chosen for a strike nobody sees.
  - The lethal core shrank from 0.8 to 0.6 of the crater, so the core matches the glowing floor.
  - Strike timing is no longer from the seed.
- **Fixed: a failed plan on the background thread would have stopped meteors for the rest of the run.** It is now caught and the strike is skipped.
- **Fixed: a crater could cap a cave entrance.** Strikes now avoid cave mouths and skylights.
- **Fixed: the flame tail had zero normals,** which can draw NaN pixels that bloom spreads into blocks. The normals are now set by hand.
- **Fixed: the shake did nothing in the pilot's seat or on the rover.**
- **Fixed: smoke from the last strike jumped to the next one on the same world.** Live smoke is now carried into the new frame.
- **Fixed: the flare could stay lit when the launch effects weren't running.** It now puts out its own light.
- **Fixed: thrown rocks from an earlier landing settled on the latest landing's ground.** Each rock now keeps its own.
- **Fixed: frame spikes.**
  - Several craters landing in one frame are now added one a frame.
  - Particles use 20-face shapes instead of 80 or 240.
  - The falling-rock code no longer looks up components every frame.
  - The rim check for enemies is a single dot product per crater.
- **Fixed: crater seams.** Craters now share the ground tiles' own material (far colour included) and sit in body space, so the close-up grit carries on across the edge.
- **Not done: no on-screen warning on airless worlds.** There is no warning that works whichever way you face, because a rock there doesn't glow. Only the rock and its roar show it's coming. A suit warning such as "IMPACT" could be added if strikes there feel unfair.
- **Not done: crater floors may poke through or flicker against distant ground.** The floor sits 7 cm above the exact ground height, and distant ground is drawn coarser than that. This needs looking at in Unity (from 0 to 300 m).
- **Not done: craters' colliders are made on the main thread.** If the Profiler shows that as the cost, the next step is making them on a worker.

## Constitution

- **3 (Learnable challenge) and 5 (Legible risk):** a warning of at least 4.5 s, a glow on the ground, a blast you can read (the scorched middle kills, the edge only hurts), no strike ever planned onto you, and a death message in plain words.
- **8 (Varied intensity):** irregular gaps, quiet at arrival, and a strike now and then rather than a steady drizzle.
- **4 (Discovery that matters):** craters last the run, so a world you come back to shows where strikes fell.
- **9 (Ethical design):** no opaque randomness that punishes you; meteor kills don't count as yours.

## Tested

- 11 meteor rule tests pass. They cover:
  - gaps and how irregular they are;
  - aim bounds and enemy aim;
  - places a strike isn't allowed, and a tiny moon;
  - a whole rock landing on target on worlds from 40 to 500 m radius;
  - an air burst's pieces, volume and scatter;
  - smooth paths;
  - blast falloff;
  - crater shape;
  - the crater mesh facing up, its edge buried and its colours capped;
  - the crater registry.
- The full rule suite passes (2,477 pass, 1 skipped).
- The game code compiles against reference assemblies. The only errors are the known name clash with the checking library.

## Unverified (needs Unity)

- Everything visual: rock and tail, fire, smoke, dust, glow, crater seams at distance, the ground glow, bloom on the tail.
- Sounds in the game: level, delay, the roar following the rock.
- Damage and knockback in practice, enemies walking over rims, the rover riding over them.
- Deck frame time on a landing frame and during the dust.
