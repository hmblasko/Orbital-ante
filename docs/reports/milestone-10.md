# Milestone 10: Ship navigation

13 Nov 2026. Branch `claude/milestone-10-navigation` (built on Milestone 9).

**Nothing in this milestone has been run in Unity.** The rules were tested outside the engine (including simulated autopilot trips), and the game code and PlayMode tests were compiled against stand-in Unity libraries. How flying, landing and crashing feel is unknown until you fly it.

## What changed

**Target lock.** Point the nose at a body and press **T** (controller: X / Square). It stays locked wherever the nose goes next. Press T on the same body, or on empty sky, to let go. Match Velocity and the dash's speed reading use the locked body. The HUD says "LOCKED ON".

**Autopilot.** With a body locked, press **R** (controller: B / Circle). The ship turns its nose to the body, burns toward it, coasts, brakes and stops at a safe place above it, well clear of the ground. It never lands for you. The dash shows what it is doing and how long is left ("AUTOPILOT: SLOWING 1:05").
- Any push from you, or R again, and the ship is yours at once.
- It refuses, or lets go, in plain words: nothing locked, another body in the way (it names the body), on the ground, tank nearly dry, nobody buckled in, a hard knock. Letting go by itself in mid-flight also sounds an alarm tone.
- When it arrives it says "AUTOPILOT DONE: FLY, YOU ARE FALLING", because from that moment gravity is yours to deal with.
- A damaged or dry ship makes a weaker autopilot. It is never stronger than the ship in your hands.
- It keeps flying while you read the map.

**Landing on moving, spinning bodies.** Close above a body, Match Velocity and the speed reading are measured against the ground turning under you, not the body's centre, and the dash says so ("MOON GROUND"). Holding Match Velocity over a spinning moon holds you over one spot. The dash warns "TOO FAST TO LAND: OVER 4 m/s" when you would hit the ground within a few seconds at more than that.

**Crashes are felt by the person inside.** What counts is how much the ship's velocity changes in the blow.
- A small knock jolts your view. A bigger one throws a loose body about the cabin and dazes it for a moment. A big enough one kills.
- **The belt is the difference.** Loose, a knock of 24 m/s kills. Buckled, it takes 85 m/s. The dash warns "NOT BUCKLED IN" if you are moving with the belt off.
- Several touches in the same instant (four footpads) count as one blow.
- Death is first person: your head drops where you were, then a page says in plain words what killed you ("...you were not buckled in") and offers the same seed, a new seed, or the main menu. There is no outside view at any point.

**Crash sound.** A dull thump through the hull, deeper and louder the harder the blow; the structure groaning after a real one; metal scraping while the ship slides. No music, no stings. The ship also bounces a little more (0.28) so a hard landing skips before it settles.

**Fuel.** The autopilot's push and Match Velocity both cost fuel. The autopilot costs a little more than flying the same trip by hand (25%), so flying well is still worth it.

## Why, and which principles it serves

- **Fun core first (1) and learnable challenge (3):** the autopilot does the long dull stretch and stops short, so the skilful part (the landing) is always yours. Every refusal and every death says why in plain words. The belt rule and the landing speed are both on the dash before they matter.
- **Agency and trade-offs (2):** autopilot is convenient but dearer in fuel and stops well above the ground; unbuckling to walk about in flight is a real risk you can see.
- **Legible risk and reward (5):** "TOO FAST TO LAND" and "NOT BUCKLED IN" appear while there is still time to act.
- **Cognitive load (7):** still one notice at a time, most urgent first. The autopilot's countdown only ever counts down, so it does not flicker.

## Choices you should know about

1. **A buckled pilot can still die**, above 85 m/s. The build prompt only says an unbuckled player can die. I added this so flying into a planet at full speed is not survivable; the design review agreed. Say if you would rather the belt made you safe at any speed.
2. **Autopilot top speed is 2,500 m/s.** This sets how long trips take and is the number most in need of your flight. It is one dial (`autopilot` on the Test Craft asset).
3. **The autopilot does not dodge asteroids**, only planets and moons. An asteroid knock makes it let go.
4. **When it refuses because something is in the way, it does not brake for you.** It tells you and leaves the ship as it was.
5. **The big front window is no stronger than the small ones were**, although it is larger.
6. **Bounce changed from its old value to 0.28.** If a Ship config asset already exists on your machine it may keep the old number; `tools/check-in-unity.sh` re-runs setup.

## What was tested

- **1,053 rule tests pass** outside Unity (5 skipped, as before). New for this milestone: simulated autopilot trips from several distances and starting speeds (each arrives within tolerance with no overshoot and no dithering between burning and braking); the safe closing speed; the blocked-path check; the words for every way it lets go; the lock button; every crash outcome with and without the belt; touches summed into one blow; the view kick settling; the landing-speed and belt notices and their order.
- **The game code and all PlayMode tests compile** against stand-in Unity libraries. (The one known gap is unrelated: the stand-in editor library has no `AssetPostprocessor`.)

## Still unverified (needs Unity on your machine)

- **How it feels.** Whether landing on a spinning moon is "an achievement, not a fight with the controls" can only be judged by flying it.
- The 4 new PlayMode tests (86 in all) have not been run.
- The knock measurement on real collisions: whether an ordinary landing is felt as a jolt and not more, whether resting on the ground stays quiet, and whether hard boosting off the ground gives a false jolt.
- Autopilot on a real trip: time taken, where it stops, whether the nose swings smoothly.
- The crash sounds in the mix, and the scrape.
- The death view and the death page, including that a held button cannot skip it.

## Agent review

**game-designer.** Applied: every autopilot stop has words, and stops it chose itself are heard too; the arrival message warns that you are falling; the autopilot is charged fuel with a surcharge; the belt and landing-speed warnings come before the event, not after; the countdown is steady. Flagged for you, not changed: choices 1 to 5 above.

**deck-performance.** Applied: one groan at a time in a tumbling crash; the autopilot's words are only rebuilt when they change; the blocked-path check and the knock are plain sums with no searching of the scene. Estimates are in "Navigation" in `docs/performance_budget.md`.

**unity-reviewer.** Applied: a daze in the seat or the lift no longer freezes the belt or the lift ride; opening the map no longer stops the autopilot; the knock is measured from the ship's real change of velocity each physics step (the engine's reported impulse is not reliable here) and told once per step; pitched thumps have their own sound source so other sounds keep their pitch; a dead pilot cannot use the dash, the cameras or the map, and the view returns to their own eyes; the death page waits 0.8 s before a button can be pressed; the tests no longer assume the Moon is in view for the seed. Noted, outside this milestone: saved key bindings may not load back because binding ids are made afresh each launch. That belongs to the pause menu and needs its own fix.

## Codex (ChatGPT) work checked

Codex reviewed this pull request on request and raised two points. Both were real and both are fixed.
- **The autopilot flew for free while the map was open.** Its fuel is now charged whenever it is pushing, whether or not your hands are on the controls.
- **A blow against two surfaces at once (a floor and a wall) was measured against only the first.** The knock now counts every surface touched in the same physics step, and each surface only once.

## After the first run on the Deck (14 Nov 2026)

Matthew's first run found two things.
- **The ship moved about by itself on the pad.** Most likely cause, from reading the code (not yet confirmed in Unity): the steering turned the ship directly even while it stood on the ground, so any movement of the stick, mouse or trackpad twisted its feet against the pad and the physics pushed it about. Now, standing on the ground with no push held, nothing turns the ship. Any push and it steers again. If it still moves, the F3 readout line starting "Anchored to" will say what the physics thinks is happening.
- **Lock-on.** A rough aim is now enough: Lock takes the body nearest the nose within 25 degrees, and the flight readout names it ("Nearest the nose: Moon (T locks it)"). Pressing Autopilot with nothing locked locks that body and goes. Locking a different body while the autopilot is flying sends it there instead of switching it off.

## Second round from the Deck (14 Nov 2026)

None of this has been run in Unity. It compiles, and the rule tests pass (1,059 of the repository's own).

- **Falling through the ground when landing.** Three causes found by reading the code, all fixed. (1) Ground was only kept solid round whichever of the ship or the suit you were in, so a parked ship could lose its ground. Both now keep the ground under them solid and in its finest shape. (2) When ground you are standing on is swapped for a finer shape, a foot can end up under the new surface, and ground is only solid from above, so the leg dropped through. The game now notices the swap and stands the ship or the suit back on top once. (3) The safety net that puts sunk things back measured from the ship's cabin floor, which is 3.8 m above its feet, so the whole ship had to be inside the Moon before it acted. It now acts when the cabin floor is within 0.6 m of the ground, and only where the ground is in its finest shape (coarser ground can honestly lie lower). Serves principle 3 (no hidden rules: the ground is the ground). New PlayMode test `AShipHalfSunkInTheGroundIsStoodBackOnTop`.
- **Seat controls.** Matthew: the mouse looks around, thrusters move the ship. Buckled in, the mouse turns your head (125 degrees either way, 70 up, 60 down). Thrusters are W A S D, Space and Left Ctrl. Hold the right mouse button and the mouse turns the ship instead, and your eyes ease back to the front. On a controller the right stick still turns the ship, because a held button there would tie up a trigger finger during a landing; click the right stick to look around, click again to go back. Roll stays on Q / E and the bumpers. Lock takes what is in front of your eyes when your head is turned more than 10 degrees from the nose. Principles 2 and 7.
- **Thruster sound by direction.** The big engines rumble only for lift. The small thrusters have their own hiss with a different note for sideways, forward, back, down and turning, heard from the side the firing thruster is on. Rule in `ThrusterHeard`, 5 rule tests. This is a stand-in: the audio thread's recorded-quality version (one loop per direction) replaces it when its pull request merges, reading `TestCraft.Firing` and `TurnHeard`.
- **The hatch never breaks**, and opens by looking at it (from inside or from underneath) and pressing Interact. The lever is no longer used; the look thread removes the prop with its next cabin bake, so it is still visible on this branch.
- **A broken ship does not end the run.** Buckled in, no crash kills you; the ship takes the damage. Not buckled in, a hard enough crash still kills (build prompt, section 7). `CrashRules.beltAlwaysSaves`.

### Agent review of this round

**unity-reviewer.** Applied: the leg-through-reshaped-ground cause and its fix above; the safety net no longer throws a ship resting honestly on coarse ground; which device is steering goes by what last moved the view, so a controller on the desk cannot take the mouse's turn; an action with nothing bound on one device keeps an empty slot so it can be bound in the menu. Not applied: saved key bindings may not load back (older, known; still to do).

**game-designer.** Applied: controller keeps stick-turns-ship with a click to look; lock follows the eyes; rolling is heard; gentler return of the view; the belly hatch itself can be looked at from outside. Left for Matthew: whether a buckled pilot should be dazed by a very hard crash. Not applied: one sound per direction (the audio thread's version does this).

**deck-performance.** Applied: solid ground round the thing you are not in reaches 30 m, not the fast-arrival range. Counted by a test program on a smooth ball (not measured): up to 15 more ground pieces drawn and 9 to 24 more solid when you are far from the ship. Ground within 12 m of the ship or suit is made solid at once, up to 6 pieces in one frame on first arrival (a guess of 1 to 3 ms each on the Deck). No per-frame garbage found.

### Still unverified

All of the above in Unity. First things to look at: land fast on the Moon and watch the F3 line for "rescues"; fly with mouse and keyboard while a controller is connected; walk 100 m from the ship and back.

## Added afterwards: the controller's right stick looks first (14 Nov 2026)

**What changed.** Matthew: "You can reverse the driving look system". In the seat the controller's right stick now looks around the cabin by default, the same as the mouse. Clicking the stick switches it to turning the ship, and clicking again goes back to looking. Leaving the seat resets it to looking. The hint line on the flight HUD and `docs/SETUP.md` say the same.

**Why.** One rule for both devices: your hand looks, the thrusters move the ship. **Principle 7** (manage cognitive load).

**Tested.** Compiles against the reference assemblies. **Not run in Unity**: how it feels on the Deck's sticks, and whether a click to turn is comfortable when lining up a landing by hand.
