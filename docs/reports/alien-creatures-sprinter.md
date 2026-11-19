# Alien creatures: the Sprinter (19 Nov 2026)

Matthew saw the Strider pilot on 19 Nov 2026 and said: "Continue to build these, spider crab looks good". This is the second of his nine: the **Sprinter** (placeholder name), from the camel spider.

**Nothing here has been run in Unity.** The pictures and clips are Blender's, drawn from the game's model and the game's own movement rules; they are not Unity screenshots.

## What it is

A sand-coloured, bristly hunter 2.3 m long that carries its body knee high (0.56 m) on eight legs, with two thick feelers held out in front and a pair of rust-red jaws a third the length of its body. Pictures: `docs/art/previews/creatures/sprinter_*.jpg`.

- **It is fast, in bursts.** It runs at 6.2 m/s: quicker than you walk (5), slower than you sprint (8). On the way in it dashes for 1.3 s and stops dead for 0.55 s, as the real animal does. The stop is your time to shoot. It only stops while it is more than 8 m away and only if the dash gained ground on you, so walking backwards never keeps it off for good, and sprinting does.
- **It bites with its whole body.** From 2.5 m it rears its feelers and opens its jaws wide (the warning), then throws itself forward up to 0.85 m and snaps them shut. Where the bite will land is fixed a quarter of the way through the 1.05 s lunge, so a step aside after that is a miss. 22 damage with both jaws, 11 with one.
- **It comes apart.** Eight legs and two feelers of three pieces each (45, 35 and 25 damage; the rifle does 24), and two jaws (45 each) that are shot off whole, moving finger and all. A piece that breaks takes everything beyond it.
- **What you shoot off changes the fight.** A jaw gone halves the bite. Both feelers gone and it can no longer fix where you are: the bite goes where you were when the lunge began. Both jaws gone and it runs away. Legs lost slow it and lower it; with fewer than five, or fewer than two on a side, it drags itself.
- **It dies two ways.** The soft striped abdomen behind (150) or the armoured head in front (200). It always faces you, so the easy target is the one it hides: you get at the abdomen by stepping round it during a stop or straight after a missed lunge.

## How it moves (no animation clips)

It uses the Strider's walker, with its own numbers: legs step in pairs across the body (a trot, four feet down and four up at a run), the body rides low on quicker springs, the abdomen swings behind the head as it turns, and the jaws saw against each other all the time. Clips: walk, strike, and losing legs, in the project files.

## What changed

- The Strider's code became shared walker code: `WalkerLayout` (one per kind), `WalkerBody`, `WalkerAnimator`, `WalkerBrain` and the Unity class `Walker`. The Strider behaves as before (its tests are unchanged and pass). A new eight-legged creature is now a layout, a set of rules and a model.
- New in the walker for this kind: jaws as their own limbs, a lunge, dash-and-stop pacing, a soft part that swings, trotting.
- `tools/blender/build_creatures.py`: the Sprinter (textured at 2048) and its far copy. New material `bristly` in `oa_materials.py`. About 900 bristles, four triangles each, sharing one patch of the pictures; the far copy has none.
- `Resources/Models/Sprinter.bytes`, `SprinterFar.bytes`, `Textures/Sprinter_*.png`.
- The combat arena starts with two Sprinters in the far corners (`CreatureConfig.arenaSprinters`).

## Constitution

- **1, fun core:** a quick enemy that makes you move, against the Strider that makes you aim.
- **2, agency:** jaws, feelers, legs or the abdomen: each choice changes what it can still do.
- **3, learnable challenge:** low limb health, no hidden rule: the stop, the rear-up and the open jaws are all in view, and the bite commits early.
- **5, legible risk:** it can be outrun but not out-walked, and the test `TheSprinter_CatchesSomeoneWalkingAway_ButNotSomeoneSprinting` holds that.
- **8, varied intensity:** dash and stop gives the fight a pulse instead of a constant rush.

## Tested

- 24 rule tests in `WalkerRulesTests` (the Strider's, and the Sprinter's own: the catch-up rule, the pause, the lunge and its lock, jaws and feelers, fleeing, trot footfalls, the model against the layout, the far copy). The whole suite: 2,574 passed, 1 skipped.
- The Unity-side code compiles against Unity reference assemblies.
- The model: 4,722 triangles in the file, 10,516 as drawn, 2,934 as the far copy; seen from inside 0.00%.

## Agent review

- **unity-reviewer** found: the bite was measured from where the body started, not from the jaws after the lunge (now from the live jaw tips); a bite could land through a wall (a line of sight is checked first); hitboxes lagged a frame behind a lunging body (synced after the pose for kinds that lunge); the first pose was given a zero time step; several creatures made at once all looked for the player on the same frame (staggered by seed). All fixed.
- **game-designer** found: it could never catch a player who simply walked backwards, because it eased off as it closed and stopped too often (it now runs flat out to the lunge and stops only when far and gaining); jaws at 60 and the head at 260 were slower to break than their size promised (45 and 200); the feelers and a single jaw did nothing when lost (they now do, as above). All done.
- **deck-performance** found no garbage per frame; asked for no shadows on the meat lumps (done), one cap on loose pieces across all walkers (34), and the far copy worn a little sooner than size alone says. Its budget line is in `docs/performance_budget.md`.

## Not verified, and not built

- Not run in Unity: whether it appears, how the bristles look on the Deck's screen (they are 3.4 mm wide and may shimmer; the fallback is leaving them out beyond 10 m), whether the lunge reads, frame cost.
- The far copy's abdomen is only 40 triangles. It is worn from about 26 m; if it looks faceted there it gets more.
- Level ground only, arena only, as for the Strider. Melee and the rover cannot hurt it. No sounds of its own.
- "Runs for your shadow" (the real animal chases shade) is not built: it needs to know where the Sun is and where you cast shade, which matters on planets, not in the arena.

## Seeing it on the Deck

1. In Desktop Mode open a terminal in `~/Orbital-ante` and run `git fetch`, `git checkout claude/playtest`, `git pull` (after PR 70 is merged there).
2. Open the project in Unity and wait for it to import.
3. Press Play, choose **Combat test arena**.
4. The two Sprinters are in the far corners, left and right; the Strider is at the far end. They only wander until you press **H** (D-pad down), which starts the fight.
5. Try: walk backwards from one (it should catch you), sprint from it (it should not), step aside as its jaws open, shoot a jaw off, shoot both feelers off, then get round to the striped abdomen.
6. If nothing appears, look in the Console for a line starting "Orbital Ante: the Sprinter's model" and send it to me.
