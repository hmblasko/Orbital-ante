# Milestone 8 report - Launch

Status: **built, not yet run in Unity.** The rules pass their tests here (925 rule tests). The Unity side compiles against reference assemblies but nothing in it has been seen or heard: no smoke, no shake, no sound, no frame time. The four new PlayMode tests compile and have not been run. By the letter of the build prompt the milestone is **not finished**: heat shimmer is missing (see "Choices").

Branch `claude/milestone-8-launch`, built on top of Milestone 7 (pull request #6) and look pass 1 (pull request #8), so it contains both.

## What changed

**A run now starts on the launch pad.** You begin buckled into the lander on a concrete pad on Earth, near the real Cape Canaveral. The exact spot is chosen per seed: the nearest dry, level ground to the Cape that is not sea or a cave mouth. Tests and the config can still start in orbit.

**The launch base.** A raised concrete mound with a flame dish under the ship, a service tower with a swinging arm that rests against the cabin, a water tower, a small hut, a ring of pad lamps and four floodlight masts. No flags. Rocks and trees are kept off the concrete. The solid pieces can be bumped into. For now these are simple stand-in shapes with soft baked shade; the proper Blender model is being made in the visual style thread and drops in by name (`LaunchPad` model, pieces listed in `LaunchLayout`), with the stand-ins kept as the fallback.

**The lift-off.** Hold "up" while standing still on the ground:
1. The engines light with a thump and a rising rush. For about a second they build: flame grows under the four bells, the ground lights orange, smoke rolls out across the pad, grit is thrown, the cabin shakes harder and harder, and the service arm swings clear. The ship is held at 12% push, so it stays down.
2. Then it lets go with a kick and the ship climbs on full push. The burn never drops at that moment; it steps up.
3. Smoke, grit, crackle and most of the shake fade as you climb through the first 45 m. In space the same engines are a steady hum and a faint tremor.

Everything follows three things: how hard the engines push, how near the ground is, and how much air there is. So Earth is violent, the Moon throws grit but no billowing smoke and no crackle, and open space is calm. Smoke takes the colour of the ground it is blown off.

**It never gets in the way of flying.** The build-up only happens to a ship standing still on the ground. Bouncing off a bad landing or scraping a cliff, the engines answer at once. Let go early and nothing is lost. Engines that ran in the last 3 seconds relight in about a third of a second, so short hops are not a wait. A prompt in the seat says "Hold [key]: lift off" while you are parked.

**Sounds** are made by the game (placeholders, like the other sounds so far): a deep roar loop, a crackle loop that needs air and ground, an ignition rush and a release thump.

## Why, and which principles it serves

- **Fun core first (1)** and the refinement bar "every launch gives a little rush, even the tenth time": a short build-up you cause, then a release.
- **Varied intensity (8).** The loudest, shakiest moment in the game so far is followed by quiet space.
- **Learnable, no hidden rules (3).** The rule is one sentence (parked ships build up, moving ships do not) and the seat prompt says to hold.
- **Agency (2).** The build-up only ever holds back upward push while parked. It cannot cost you a save from a bad landing.
- **Cognitive load (7).** Shake in space was cut to less than half after review so it does not blur the gauges.
- **Everything first person.** The shake only turns your view in the seat.

## Choices you should know about

1. **Starting on the pad is now the default.** Say if you would rather keep starting in orbit while flight is unfinished; it is one tick box on the Launch asset (`Start On Pad`).
2. **Heat shimmer is not built.** It needs a screen-bending effect that I cannot check without Unity, and a broken one would spoil every launch. The rules already work out how strong it should be. I would add it once this has been seen running.
3. **"Built around the NASA launch pad model."** The base is the game's own design in the new art style, with a model made in Blender, not NASA's file. Tell me if you want the real NASA pad model simplified instead.
4. **No locker.** Section 8 puts your locker at this base; that belongs to loot (Phase 2).
5. **The roar is new and sits on top of the existing thruster hum.** Section 14 says to keep the earlier flying sound, so I did. They may be too much together: needs ears.
6. **All numbers are first guesses**: 1.1 s build-up, 0.35 s hot relight, shake 1.1 degrees on the ground and 0.05 in space, 1.5x kick for 0.4 s. All on the Launch asset.

## What was tested

- **925 rule tests pass** (`dotnet test`, outside Unity). New for launch: the site is dry, level and the same for a seed; the pad's axes; the mound reaches below the ground; build-up then release; the release is a step up with a kick; hot engines relight fast and cold ones do not; letting go early costs nothing; smoke fits under its cap; everything rises and falls smoothly; broken engines burn weaker; shapes are well formed; the ship fits the pad; every named piece has a stand-in.
- **Compiles**: runtime, editor and PlayMode test code, against reference assemblies with Unity 6 names mapped back.
- **Also fixed on the way** (found by Codex's Unity 6 audit, checked against the code): thrust held when the pause menu opened could fire for one physics step on resume. Held input is now forgotten on pause.

## Still unverified (needs Unity on your machine)

- All of the look, sound and feel. Whether the smoke can be seen from the seat at all (it is thrown nearly flat, below the cabin).
- The four new PlayMode tests, and that the older PlayMode tests still pass with the new orbit-start argument.
- No hop at the start of the build-up at low frame rates (fixed by reading, not seen).
- Frame cost. See "Launch" in `docs/performance_budget.md` for what to read off.
- The planet shader edits from merging the look pass (`OA_Terrain.shader` now takes glow from both sides).

## Agent review

**unity-reviewer** (8 findings, 7 fixed):
- Full push could leak for a frame or two at the start of the build-up, because the ship copied the gate before the launch code lowered it. Fixed: the launch code runs later and sets it directly.
- The service arm could swing out at the very start of a run. Fixed: the ship is marked as resting when placed.
- Smoke had no material where there is no worked-out ground (asteroids). Fixed with a default.
- Any touch counted as "on the ground", even wreckage in space. Fixed: only ground and fixed things count.
- A timing check in a test was fragile. Loosened and timed properly.
- The shade job held up the start of a run. Now starts on the first frame.
- Smoke could freeze while out of view. Set to always simulate.
- Not done: ground bumps might poke through the pad between sample points. Needs eyes.

**game-designer** (all applied except two that are your call):
- The build-up was a hidden rule on any contact. Now parked ships only.
- Lift-off was an anticlimax (burn dropped 20% at release). Now a step up plus a kick.
- Hops waited the full build-up. Hot relight added.
- Smoke overflowed its cap with boost. Rate lowered from 46 to 34 a second.
- Shake in space lowered from 0.12 to 0.05 degrees.
- Nothing said to hold. Seat prompt added. Ignition sound now stops if you let go.
- Your call: heat shimmer and the NASA pad model (choices 2 and 3).

**deck-performance**: no per-frame garbage found. Applied: smoke and grit drawn without painted detail; grit is 8 triangles instead of 132; the site search drops a spot at the first bit of sea; the arm is only turned when it moves. Left for the Profiler: site search and sound making on a background thread. Its budget table is in `docs/performance_budget.md`.

## Codex (ChatGPT) work checked

- **Test drafts, pull requests #11 to #14** (53 tests on guns, planets, space and gib damage): all compile and pass against the current code here, and use no files, network or processes. Suggest merging; that is your button.
- **Unity 6 audit, #19**: pause finding real and fixed here. Gib cap not counting fading chunks is real; kept for the combat polish milestone. Shot and pose timing touches the enemy files being reworked in the visual style thread; kept for that. Gravity sign-up order does not happen in the current build; not acted on.
- **Seed audit, #10**: correct that dropped-gun spin and gib spin use Unity's random. Not changed: exact replay of physics is not something the game promises, and the rule allows visual scatter. Worth revisiting in Milestone 12.
- **Deck audit, #18**: overlaps the above (gib cap); the rest needs the Profiler.
- **Setup rewrite, #21**: written against an older `SETUP.md` than the one on this branch, so it would undo newer steps. Suggest closing it and redoing after this merges.
- **Not yet read closely**: #20 (feel numbers), #17 (Milestone 7 questions), #15 (playtest checklist).
