# Milestone 5 report - Solar system

Status: **built, not yet run in Unity.** Same position as Milestones 1 to 4. The rules of the solar system (orbits, gravity, points of view) are plain maths and are properly tested here: 102 rule tests run and pass outside Unity. Everything you would see or feel (how the planets look, how the test craft handles, the map, frame times) compiles but has never been run.

This milestone also adds the six **game-building agents** you asked for on 10 November. They are described at the end, and they have already reviewed this milestone.

Same branch as before (`claude/unity-milestone-1`).

## What changed

**1. A solar system that never stops moving**
- 20 bodies: the Sun, eight planets and eleven moons (Moon, Phobos, Deimos, Io, Europa, Ganymede, Callisto, Enceladus, Titan, Titania, Triton), plus a belt of 110 drifting, tumbling asteroids between Mars and Jupiter. Saturn has rings.
- Planets orbit the Sun, moons orbit planets, everything spins. Venus spins backwards and Triton orbits backwards, as in real life. The Moon and most moons keep one face to their planet.
- **Seeded.** The bodies and their orbits are the same every run. Where each body sits along its orbit, and the whole asteroid belt, come from the seed. Seed 1234 always gives the same sky.
- **Shrunk to stay playable.** Earth has a 500 m radius and sits 22 km from the Sun; Neptune is 120 km out. Orbit speeds follow from each body's gravity, so the physics is consistent, just small. Earth's year is about 13 minutes; the Moon goes round Earth in about 7.
- All of it is one data asset (`Resources/SolarSystem/SolarSystem`). Sizes, distances, gravity and day lengths can be tuned in the Inspector without touching code.

**2. Gravity for each body**
- Real values as the starting point, tuned: Earth 9.8, Moon 1.6, Mars 3.7, Jupiter 16 (real is 24.8, which would be unflyable), and so on.
- **One rule, easy to learn:** you are always inside exactly one body's neighbourhood, and you feel that body's pull plus its moons'. Outside every planet's neighbourhood you feel the Sun. The HUD always names whose pull you are in, and the map draws the neighbourhood as a ring.
- Objects in motion stay in motion. Nothing in space is slowed by drag.

**3. A point of view that follows you**
- Unity cannot place things accurately tens of kilometres from the centre of the scene. So true positions are kept in high precision, and the world is drawn around whichever body you are near. Near Earth, Earth stands still and the Sun goes round the sky.
- Close to a spinning body the view turns with it, so the ground holds still under you and landing on a moving, spinning moon needs no special handling later.
- Switching between points of view never moves or jolts anything. A test flies a craft through several switches and checks it stays on course to within a few metres over five minutes.

**4. A test craft (the bare minimum to judge the system)**
- A first-person floating camera with thrusters in six directions, roll, boost, and **match velocity**. It is not the ship: no model, no cockpit, no fuel. Those are Milestones 7, 9 and 10.
- Aim at a body and the HUD shows its name, distance and closing speed. Hold match velocity and you match the body you are aimed at (or the body whose neighbourhood you are in).
- The HUD says **BRAKE NOW** when you need to start slowing to stop at your target, and warns when a body's pull is stronger than your thrusters.
- Starts in orbit 450 m above Earth.

**5. Live map and markers**
- Press **M** (or d-pad up) for a top-down map that updates as everything moves: orbit lines, bodies, your position and heading, the neighbourhood ring, and a scale bar. Zoom, and cycle what it is centred on.
- In flight, every body in view has a marker with its name and distance, so you can find Mars when it is one pixel wide.

**6. Looks (placeholder, on purpose)**
- Each body has a generated colour texture so they are recognisable: blue and green Earth with seas and ice caps, rust Mars with a polar cap, banded gas giants, a grey mottled Moon. This is a stand-in. Real NASA-based textures, atmosphere tints and enterable gas giants are Milestone 6.
- About 1,700 stars in three tints, and a Sun that lights everything from the right direction.

**Also**
- The main menu now offers **Fly the solar system** and **Combat test arena**. The arena from Milestones 1 to 4 is unchanged.
- A second set of rebindable controls for flying appears in Pause > Controls.
- The save file remembers which of the two you played last (save version 2, with an upgrade step for older saves).
- Build label is now "Milestone 5 - solar system".

## Why, and which principles it serves

- **Fun core gameplay first (1):** the system exists to be flown through, so a flyable craft and a readable HUD came with it instead of waiting for Milestone 10.
- **Learnable challenge, no hidden rules (3):** the one-neighbourhood gravity rule is shown on the HUD and on the map. Your orbit around a planet behaves exactly as the on-screen information says it will.
- **Legible risk/reward (5):** BRAKE NOW and the "pull is stronger than your thrusters" warning tell you about danger before it is too late to act.
- **Manage cognitive load (7):** markers only for bodies, three readouts, one state line.
- **Discovery that matters (4):** real moons in their real order give Milestone 6 distinct places to make interesting.

## Three choices you should know about

1. **No tides.** In real life the Sun tugs slightly on things orbiting Earth. In a system this small that tug is large enough to drag a low orbit into the ground within minutes, which felt like a hidden rule. So only the body whose neighbourhood you are in (and its moons) pulls on you. Kerbal Space Program does the same.
2. **Circular orbits on rails.** Planets follow fixed circles and cannot be knocked off them. This is what Outer Wilds effectively does for its planets, and it keeps the system the same for every run.
3. **Sizes.** Earth at 500 m radius is roughly twice the scale of an Outer Wilds planet. It was picked to give Milestone 6 room for canyons and mountains while keeping a trip from Earth to Mars to a few minutes. It is one number per body and I expect to tune it after you fly it.

## What was tested

Run here, outside Unity:
- **102 rule tests pass** (`dotnet test`). 41 are new for this milestone:
  - Orbits: periods match the gravity sums, moons stay with their planets, same seed gives the same system, different seeds differ, backwards orbits and spins go backwards, everything else goes anticlockwise seen from the north as in the real system.
  - Gravity: surface gravity matches the data on every body, a circular orbit stays circular, every neighbourhood contains its moons and none overlap, the asteroid belt stays clear of Mars and Jupiter.
  - Points of view: switching anchor or turning co-rotation on and off moves nothing, sliding the world back to the centre moves nothing relative to you, and a five-minute flight through several switches lands within metres of where the plain maths says.
  - Save upgrade from version 1 to 2.
- **Everything compiles** against Unity's engine assemblies and the real Input System and UI assemblies, including the 53 PlayMode tests and the editor setup script (those two against stand-ins for the editor and test-runner).

## Agent review

Three of the new agents reviewed this milestone before it was committed. Each read the code; none could run it.

**Game designer.** Applied: a stronger boost so long trips do not drag, aiming at a body shows its closing speed and lets you match its velocity, the BRAKE NOW cue, the warning when gravity beats your thrusters, the neighbourhood ring on the map, a narrower and denser asteroid belt, and two data corrections (Mercury's day, Uranus's spin). Not applied: bigger neighbourhoods for small moons (Phobos sits too close to Mars for that).

**Deck performance.** Applied: planets drop to simpler meshes when small on screen, HUD text refreshes 8 times a second instead of every frame, moving markers are on their own canvas so they do not force the rest of the HUD to redraw, orbit lines are worked out once, and the Sun casts no shadows yet (there is nothing to shadow until Milestone 6). Not applied: spreading the texture generation over several frames behind a loading screen. Expect a pause of a second or more when the solar system starts; the first Unity run will say how long.

**Unity reviewer.** Confirmed the mesh facing, rotation maths, control directions and update order. Applied:
- The craft used a collision setting that does not work against script-moved planets, so a fast craft could have passed through a small moon. Fixed.
- Orbits and spins ran clockwise and the surface maps were mirrored. It was self-consistent, but real NASA textures in Milestone 6 would have come out backwards. Everything now turns the real way, with a test.
- Planets' night sides would have picked up a blue sheen from Unity's default sky. Switched off in space and restored for the arena.
- The fastest asteroids spun hard enough to fling a landed craft off. Slowest day is now 90 s, not 40.
- Faint stars were under a pixel wide and would have flickered. Enlarged.
- On-screen help now shows controller buttons when you are using a controller.
- A test tidy-up so one PlayMode test's planets cannot leak into the next.

Left as known limitations:
- **Planet phases.** One light shines from the Sun toward you, so far-away planets are lit from slightly the wrong side. Fixing it needs a small custom shader, which belongs with the planet look in Milestone 6.
- **Steering may feel a touch soft.** Turning is applied on the physics step (50 times a second), not every frame. It should be fine at 60 fps, and the real fix belongs with ship flight in Milestone 10.
- **Asteroids collide as spheres**, slightly smaller than the lumpy rock you see.
- **Stars turn in 50 steps a second** when the view is turning with a planet. Probably invisible; easy to smooth if not.

## Still unverified

- **Nothing has been run in Unity.** Five milestones are now stacked without a first run. A first-open compile error or a package version mismatch is possible, and one early mistake could affect everything after it.
- **How it looks.** Planet textures, rings, stars, sunlight, the size of things on a Deck screen. All unseen.
- **How it feels.** Thrust, boost, turning, match velocity, whether a trip to Mars is a pleasure or a chore. The milestone's refinement bar ("flying between bodies is a pleasure, scale feels grand but travel never drags") cannot be judged from here, so I am not claiming it is met.
- **Frame times.** No measurements. The budget lines in `docs/performance_budget.md` are estimates.
- **The 53 PlayMode tests**, 11 of them new (craft falls toward Earth, holds an orbit, survives the world sliding back to the centre, match velocity, map opens, and so on).
- **Floating-point jitter far from the Sun.** The design should prevent it; only flying to Neptune will prove it.

## Decisions for you

None of these block anything. I have gone with the recommended option in each case.

1. **Pluto?** Not included. Recommended: leave it out unless you want it as a secret.
2. **Gas giants are solid balls for now.** Flying into them is Milestone 6. Recommended: fine until then.
3. **No tides** (above). Recommended: keep.
4. **Planet sizes and distances** (above). Tune after flying.
5. **Should boost cost something** (fuel, heat)? Recommended: decide in Milestone 10 with the real ship.
6. **The Moon moves at about 33 m/s round Earth.** Slow enough to chase down easily. Faster makes it harder to land on. Tune after flying.

## Playtest checklist (when you first open Unity)

1. From the main menu choose **Fly the solar system**. Note how long it takes to appear.
2. Look around. Can you tell Earth, the Moon and the Sun apart at a glance? Are the stars steady or flickering?
3. Read the top-left budget line and tell me: OK, TIGHT or OVER, and the frame time.
4. Do nothing for a minute. You should stay in orbit, with Earth turning below.
5. Aim at the Moon, thrust toward it with boost, and brake when the HUD says BRAKE NOW. Did you arrive under control?
6. Near the Moon, hold match velocity. Does the Moon stop drifting against the stars around you?
7. Press M. Is the map readable? Do the planets move?
8. Fly to Mars. How long did it take, and was it boring at any point?
9. Fly toward the Sun until the HUD warns that its pull is stronger than your thrusters. Did the warning come early enough to escape?
10. Repeat steps 5 to 7 on a controller.
11. Run the tests (Window > General > Test Runner): EditMode should show 102 passing, PlayMode 53.

## Assets I will need (for Milestone 6, not now)

- NASA surface textures for each body (I can list exact sources when Milestone 6 starts).
- Nothing is needed from you for this milestone.

---

# Game-building agents

You asked for Fable 5.2 agents to help build the game. Six are now in the repository under `.claude/agents/`, each a specialist with its own brief and each told to follow the build prompt, never invent playtest results or faction names, and say plainly what it could not check.

| Agent | What it does |
|---|---|
| **unity-reviewer** | Reads engine-facing code for mistakes that only show up inside Unity, since I cannot run the editor here. |
| **gameplay-engineer** | Builds a system end to end in this project's style: rules in plain classes, tunables in data, tests. |
| **game-designer** | Checks work against the design constitution and the milestone's refinement bar, and proposes tuning. |
| **qa-tester** | Writes rule and PlayMode tests and hunts for edge cases. |
| **technical-artist** | Looks, shaders, meshes, effects, and the exact asset lists you need to generate. |
| **deck-performance** | Audits cost against the Steam Deck budget. |

How they are used: after building engine-facing code I send it to the reviewer, the designer and the performance agent before committing, and summarise what they found under "Agent review" in each report, as above. `.claude/agents/README.md` explains each one. There is nothing you need to do to switch them on.

One limit to be clear about: the agents read and reason about code. Like me, they cannot open Unity, so they reduce the risk of the first run but do not replace it.
